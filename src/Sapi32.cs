using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace IdolShowdownAccess
{
    /// <summary>
    /// Sprachausgabe über die 32-Bit-SAPI-Engine. Das Spiel ist 64-Bit und kann
    /// 32-Bit-Stimmen nicht selbst laden, deshalb läuft ein kleiner 32-Bit-Helfer
    /// (ISAccessSapi32.exe) als eigener Prozess und bekommt die Texte zugeschickt.
    /// </summary>
    internal static class Sapi32
    {
        private static Process proc;
        private static StreamWriter input;
        private static readonly object sync = new object();
        public class VoiceInfo { public string Desc, Name; public int Primary; }
        private static readonly List<VoiceInfo> voices = new List<VoiceInfo>();
        private static volatile bool voicesArrived;
        private static bool everReady, voicesLogged;
        private static float startedAt;

        /// <summary>Der Helfer hat sich gemeldet (Stimmenliste empfangen).</summary>
        public static bool Ready => everReady;

        /// <summary>Gestartet, aber nach 8 Sekunden noch keine Rückmeldung: dann spricht der Mod über das Ersatz-SAPI.</summary>
        public static bool TimedOut => !everReady && (proc == null || UnityEngine.Time.realtimeSinceStartup - startedAt > 8f);

        public static bool HasVoiceFor(int primary)
        {
            lock (sync) foreach (var v in voices) if (v.Primary == primary) return true;
            return false;
        }
        private static string currentVoice = "";
        private static string gameVoice = "";
        private static string gameLocale = "";
        public static string GameVoice { get { lock (sync) return gameVoice; } }
        public static bool Running => proc != null && !proc.HasExited && input != null;

        public static VoiceInfo[] Voices { get { lock (sync) return voices.ToArray(); } }
        /// <summary>Anzeigename der Stimme für die Mod-Ansagen.</summary>
        public static string CurrentVoice { get { lock (sync) return DescOf(currentVoice); } }
        public static string CurrentVoiceName { get { lock (sync) return currentVoice; } }

        private static string DescOf(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            foreach (var v in voices) if (v.Name == name || v.Desc == name) return v.Desc;
            return name;
        }

        /// <summary>Hauptsprache aus SAPI-Sprachcode(s), z. B. "409;809" -> 9 (Englisch).</summary>
        public static int Primary(string codes)
        {
            if (string.IsNullOrEmpty(codes)) return -1;
            string c = codes.Split(';')[0].Trim();
            try { return Convert.ToInt32(c, 16) & 0x3FF; } catch { return -1; }
        }

        private static bool Has(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            lock (sync) foreach (var v in voices) if (v.Name == name) return true;
            return false;
        }

        /// <summary>Bevorzugte Stimme je Sprache, gespeichert als "9=Name|7=Name".</summary>
        public static string Preferred(int primary)
        {
            foreach (string e in (ModConfig.PreferredVoices.Value ?? "").Split('|'))
            {
                int i = e.IndexOf('=');
                if (i > 0 && e.Substring(0, i) == primary.ToString()) return e.Substring(i + 1);
            }
            return null;
        }

        public static void SetPreferred(int primary, string name)
        {
            var parts = new List<string>();
            foreach (string e in (ModConfig.PreferredVoices.Value ?? "").Split('|'))
                if (e.Length > 0 && !e.StartsWith(primary + "=")) parts.Add(e);
            parts.Add(primary + "=" + name);
            ModConfig.PreferredVoices.Value = string.Join("|", parts.ToArray());
            ApplyVoices();
        }

        /// <summary>Im Hauptthread aufrufen: sobald die Stimmenliste da ist, Stimmen passend wählen.</summary>
        public static void Update()
        {
            if (!voicesArrived) return;
            voicesArrived = false;
            everReady = true;
            if (!voicesLogged)
            {
                voicesLogged = true;
                LogVoices();
            }
            ApplyVoices();
        }

        /// <summary>Gefundene Stimmen ins Protokoll und nach IdolShowdownAccessVoices.txt (ohne BOM).</summary>
        private static void LogVoices()
        {
            var lines = new List<string>();
            lock (sync) foreach (var v in voices) lines.Add(v.Name + "\t" + "Sprache " + v.Primary + "\t" + v.Desc);
            if (lines.Count == 0) Plugin.Log.LogError("SAPI-Helfer meldet 0 Stimmen! Ohne 32-Bit-Stimme kann der Mod nicht über SAPI sprechen.");
            else Plugin.Log.LogInfo("32-Bit-Stimmen (" + lines.Count + "): " + string.Join(" | ", lines.ToArray()).Replace("\t", ", "));
            try
            {
                string dir = FileLog.Folder ?? Plugin.Dir;
                File.WriteAllText(Path.Combine(dir, "IdolShowdownAccessVoices.txt"), string.Join("\r\n", lines.ToArray()) + "\r\n", new UTF8Encoding(false));
            }
            catch { }
        }

        public static bool Start(string pluginDir)
        {
            try
            {
                if (string.IsNullOrEmpty(gameLocale)) gameLocale = ModConfig.LastGameLocale.Value ?? "";
                string exe = Path.Combine(pluginDir, "ISAccessSapi32.exe");
                if (!File.Exists(exe)) { Plugin.Log.LogError("ISAccessSapi32.exe fehlt im Pluginordner."); return false; }
                // Die gekürzte System.dll des Spiels kennt diese Eigenschaften zur Compilezeit
                // nicht; zur Laufzeit (vollständige Bibliotheken) sind sie da -> per Reflection.
                var psi = new ProcessStartInfo(exe, "");
                SetP(psi, "UseShellExecute", false);
                SetP(psi, "CreateNoWindow", true);
                SetP(psi, "RedirectStandardInput", true);
                SetP(psi, "RedirectStandardOutput", true);
                SetP(psi, "WorkingDirectory", pluginDir);
                startedAt = UnityEngine.Time.realtimeSinceStartup;
                proc = Process.Start(psi);
                if (proc == null) return false;
                input = (StreamWriter)GetP(proc, "StandardInput");
                input.AutoFlush = true;
                var reader = (StreamReader)GetP(proc, "StandardOutput");
                var t = new Thread(() => ReadLoop(reader)) { IsBackground = true, Name = "ISAccessSapiReader" };
                t.Start();
                ApplySettings();
                Plugin.Log.LogInfo("32-Bit-SAPI-Helfer gestartet.");
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("32-Bit-SAPI-Helfer konnte nicht gestartet werden: " + e.Message);
                proc = null; input = null;
                return false;
            }
        }

        private static void ReadLoop(StreamReader reader)
        {
            try
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] parts = line.Split('\t');
                    if (parts[0] == "VOICES")
                    {
                        lock (sync)
                        {
                            voices.Clear();
                            for (int i = 1; i < parts.Length; i++)
                            {
                                if (parts[i].Length == 0) continue;
                                string[] f = parts[i].Split('\u001F');
                                voices.Add(new VoiceInfo
                                {
                                    Desc = f[0],
                                    Name = f.Length > 1 && f[1].Length > 0 ? f[1] : f[0],
                                    Primary = f.Length > 2 ? Primary(f[2]) : -1
                                });
                            }
                        }
                        voicesArrived = true;
                    }
                    else if (parts[0] == "VOICE" && parts.Length > 1)
                    {
                        lock (sync)
                        {
                            currentVoice = parts[1];
                            gameVoice = parts.Length > 2 ? parts[2] : "";
                        }
                    }
                    else if (parts[0] == "SPK" && parts.Length > 1)
                    {
                        helperSpeaking = parts[1] == "1";
                    }
                    else if (parts[0] == "ERROR")
                    {
                        Plugin.Log.LogWarning("SAPI-Helfer: " + (parts.Length > 1 ? parts[1] : ""));
                    }
                }
            }
            catch { }
        }

        private static void Send(string cmd)
        {
            if (!Running) return;
            try { lock (sync) input.WriteLine(cmd); }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("SAPI-Helfer nicht erreichbar: " + e.Message);
                input = null;
            }
        }

        private static void SetP(object o, string name, object value)
        {
            o.GetType().GetProperty(name).SetValue(o, value, null);
        }

        private static object GetP(object o, string name)
        {
            return o.GetType().GetProperty(name).GetValue(o, null);
        }

        private static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s ?? ""));

        private static volatile bool helperSpeaking;
        private static float lastSent = -10f;

        /// <summary>Spricht gerade (laut Helfer) – kurz nach dem Senden schon "ja", damit das Ducking sofort greift.</summary>
        public static bool IsSpeaking => Running && (helperSpeaking || UnityEngine.Time.realtimeSinceStartup - lastSent < 0.3f);

        public static void Speak(string text, bool interrupt)
        {
            lastSent = UnityEngine.Time.realtimeSinceStartup;
            Send((interrupt ? "S " : "Q ") + B64(text));
        }

        public static void Stop() { lastSent = -10f; Send("X"); }

        public static void ApplySettings()
        {
            Send("R " + ModConfig.SapiRate.Value);
            Send("L " + ModConfig.SapiVolume.Value);
            ApplyVoices();
        }

        private static void ApplyVoices()
        {
            if (ModConfig.AutoVoice.Value)
            {
                string modCodes = Loc.ModVoiceCodes;
                string pm = Preferred(Primary(modCodes));
                if (Has(pm)) Send("P " + B64(pm)); else Send("M " + modCodes);
                string codes = LanguageCodes(gameLocale);
                if (codes.Length == 0) codes = modCodes;
                string pg = Preferred(Primary(codes));
                if (Has(pg)) Send("H " + B64(pg)); else Send("G " + codes);
            }
            else
            {
                Send("G "); // keine Sprachmischung
                if (!string.IsNullOrEmpty(ModConfig.SapiVoice.Value)) Send("N " + B64(ModConfig.SapiVoice.Value));
            }
        }

        /// <summary>Spielsprache hat sich geändert (Locale-Code des Spiels, z. B. "en", "jp").</summary>
        public static void SetGameLocale(string locale)
        {
            gameLocale = locale ?? "";
            Loc.SetGameLocale(gameLocale);
            Plugin.Log.LogInfo("Spielsprache: " + gameLocale + " -> SAPI-Sprachcodes " + LanguageCodes(gameLocale));
            ApplyVoices();
        }

        /// <summary>Nach Änderung der Mod-Sprache die Stimmen neu wählen.</summary>
        public static void RefreshVoices() => ApplyVoices();

        /// <summary>Spiel-Locale -> SAPI-Sprachcodes (LCID hex), Reihenfolge = Vorrang.</summary>
        public static string LanguageCodes(string locale)
        {
            string l = (locale ?? "").Trim().ToLowerInvariant();
            if (l.Length == 0) return "";
            string p = l.Length >= 2 ? l.Substring(0, 2) : l;
            if (l.StartsWith("english") || l == "eng") p = "en";
            else if (l == "ger" || l == "deu") p = "de";
            else if (l == "jpn") p = "ja";
            else if (l.StartsWith("japan")) p = "ja";
            else if (l.StartsWith("span") || l.StartsWith("espa")) p = "es";
            else if (l.StartsWith("german") || l.StartsWith("deutsch")) p = "de";
            else if (l.StartsWith("french") || l.StartsWith("fran")) p = "fr";
            else if (l.StartsWith("chin")) p = "zh";
            else if (l.StartsWith("korea")) p = "ko";
            else if (l.StartsWith("portug")) p = "pt";
            else if (l.StartsWith("indones")) p = "id";
            else if (l.StartsWith("russ")) p = "ru";
            else if (l.StartsWith("ital")) p = "it";
            switch (p)
            {
                case "en": return "409;809;c09;1009";
                case "ja": case "jp": return "411";
                case "es": return "c0a;40a;80a";
                case "de": return "407;c07;807";
                case "fr": return "40c;c0c";
                case "zh": case "cn": return l.Contains("tw") || l.Contains("hant") ? "404;c04;804" : "804;404;c04";
                case "ko": case "kr": return "412";
                case "pt": case "br": return l.Contains("pt-pt") ? "816;416" : "416;816";
                case "id": return "421";
                case "ru": return "419";
                case "it": return "410";
                default: return "";
            }
        }

        public static void SelectVoice(string name)
        {
            lock (sync) currentVoice = name;
            Send("N " + B64(name));
        }

        public static void Shutdown()
        {
            try { input?.Close(); } catch { }
            try { if (proc != null && !proc.WaitForExit(500)) proc.GetType().GetMethod("Kill", Type.EmptyTypes).Invoke(proc, null); } catch { }
            proc = null; input = null;
        }
    }
}
