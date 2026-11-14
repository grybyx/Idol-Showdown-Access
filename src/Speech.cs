using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Mathf = UnityEngine.Mathf;

namespace IdolShowdownAccess
{
    /// <summary>
    /// Sprachausgabe über Tolk (NVDA, JAWS, Narrator/SAPI usw.).
    /// </summary>
    internal static class Speech
    {
        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string path);

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string path);

        [DllImport("Tolk.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void Tolk_Load();

        [DllImport("Tolk.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void Tolk_Unload();

        // Hinweis: Die Spiel-mscorlib ist gekürzt (kein MarshalAs), daher bool als byte.
        [DllImport("Tolk.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void Tolk_TrySAPI(byte trySapi);

        [DllImport("Tolk.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern byte Tolk_Output(string str, byte interrupt);

        [DllImport("Tolk.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern byte Tolk_Speak(string str, byte interrupt);

        [DllImport("Tolk.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void Tolk_PreferSAPI(byte preferSapi);

        [DllImport("Tolk.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern byte Tolk_Silence();

        [DllImport("Tolk.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr Tolk_DetectScreenReader();

        private static bool loaded;
        private static string lastSpoken = "";
        private static string lastText = "";
        private static float lastTime = -10f;

        public static string LastSpoken => lastSpoken;

        /// <summary>Markiert Spieltext: wird mit der Stimme der Spielsprache gesprochen.</summary>
        public static string G(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return "\u0001" + text.Replace("\u0001", "").Replace("\u0002", "") + "\u0002";
        }

        public static string Strip(string text) => string.IsNullOrEmpty(text) ? "" : text.Replace("\u0001", "").Replace("\u0002", "");
        private static float tolkSpeakingUntil;

        /// <summary>Läuft gerade eine Ansage? (für Audio-Ducking)</summary>
        public static bool IsSpeaking =>
            UseHelper ? Sapi32.IsSpeaking : UnityEngine.Time.realtimeSinceStartup < tolkSpeakingUntil;

        /// <summary>SAPI-Ausgabe über den 32-Bit-Helfer (Normalfall).</summary>
        private static bool UseHelper => ModConfig.SpeechViaSapi.Value && Sapi32.Running && !Sapi32.TimedOut;

        private static bool fallbackActive;

        /// <summary>Ersatz, wenn der Sprachhelfer nicht antwortet: SAPI im Spielprozess über Tolk.</summary>
        private static void EnsureFallback()
        {
            if (fallbackActive || !loaded) return;
            fallbackActive = true;
            try { Tolk_TrySAPI(1); Tolk_PreferSAPI(1); } catch { }
            Plugin.Log.LogError("Der 32-Bit-Sprachhelfer antwortet nicht. Ersatz: SAPI im Spielprozess (ohne Stimmenwahl).");
        }
        public static bool Available => loaded;

        private static string dir;

        public static void Init(string pluginDir)
        {
            dir = pluginDir;
            // SAPI läuft über den 32-Bit-Helfer (die 64-Bit-SAPI-Stimmen funktionieren nicht).
            if (ModConfig.SpeechViaSapi.Value) Sapi32.Start(pluginDir);
            try
            {
                // Tolk und die Screenreader-Treiber liegen im Plugin-Ordner oder im Spielordner.
                if (!string.IsNullOrEmpty(pluginDir) && Directory.Exists(pluginDir))
                {
                    SetDllDirectory(pluginDir);
                    string tolkPath = Path.Combine(pluginDir, "Tolk.dll");
                    if (File.Exists(tolkPath)) LoadLibrary(tolkPath);
                }
                // Tolk nur für den Screenreader-Modus, ohne 64-Bit-SAPI.
                Tolk_TrySAPI(0);
                Tolk_PreferSAPI(0);
                Tolk_Load();
                loaded = true;
                // Standard-Suchpfad wiederherstellen, damit das Spiel nicht beeinflusst wird.
                try { SetDllDirectory(null); } catch { }
                string sr = null;
                try
                {
                    IntPtr p = Tolk_DetectScreenReader();
                    if (p != IntPtr.Zero) sr = Marshal.PtrToStringUni(p);
                }
                catch { }
                Plugin.Log.LogInfo("Tolk geladen. Ausgabe: " + (ModConfig.SpeechViaSapi.Value ? "SAPI" : "Screenreader") + ", erkannter Screenreader: " + (sr ?? "keiner"));
            }
            catch (Exception e)
            {
                loaded = false;
                Plugin.Log.LogError("Tolk konnte nicht geladen werden: " + e.Message);
            }
        }

        /// <summary>Ausgabeweg umschalten: true = Windows-Sprachausgabe (SAPI), false = Screenreader.</summary>
        public static void ApplyOutputMode()
        {
            if (ModConfig.SpeechViaSapi.Value) { if (!Sapi32.Running) Sapi32.Start(dir); }
            else Sapi32.Stop();
            if (loaded && fallbackActive && !ModConfig.SpeechViaSapi.Value) { try { Tolk_TrySAPI(0); Tolk_PreferSAPI(0); } catch { } fallbackActive = false; }
        }

        public static void Shutdown()
        {
            Sapi32.Shutdown();
            if (!loaded) return;
            try { Tolk_Unload(); } catch { }
            loaded = false;
        }

        /// <summary>Spricht einen Text. interrupt=true unterbricht die laufende Ausgabe.</summary>
        public static void Say(string text, bool interrupt = true)
        {
            text = Clean(text);
            if (string.IsNullOrEmpty(text) || Strip(text).Trim().Length == 0) return;
            float now = UnityEngine.Time.realtimeSinceStartup;
            // Doppelte Ansagen innerhalb kurzer Zeit unterdrücken.
            // Nur echte Doppelauslösungen (gleicher Text fast gleichzeitig) unterdrücken.
            // Eine Ansage mit Unterbrechung (z. B. neuer Menüpunkt) wird sonst IMMER gesprochen.
            if (text == lastText && now - lastTime < (interrupt ? 0.08f : 0.35f)) return;
            lastText = text;
            lastTime = now;
            lastSpoken = text;
            if (ModConfig.LogSpeech.Value) Plugin.Log.LogInfo((interrupt ? "SAGT: " : "SAGT+: ") + Strip(text));
            if (UseHelper) { Sapi32.Speak(text, interrupt); return; }
            if (ModConfig.SpeechViaSapi.Value) EnsureFallback();
            if (!loaded) return;
            // Screenreader melden nicht, wann sie fertig sind: 250 ms plus 45 ms je Zeichen, höchstens 10 s.
            float est = Mathf.Min(10f, 0.25f + Strip(text).Length * 0.045f);
            tolkSpeakingUntil = interrupt ? now + est : Mathf.Max(tolkSpeakingUntil, now) + est;
            try { Tolk_Output(Strip(text), (byte)(interrupt ? 1 : 0)); }
            catch (Exception e) { Plugin.Log.LogError("Tolk: " + e.Message); }
        }

        /// <summary>Hängt an die laufende Ausgabe an, ohne zu unterbrechen.</summary>
        public static void Queue(string text) => Say(text, false);

        public static void Silence()
        {
            if (UseHelper) { Sapi32.Stop(); return; }
            tolkSpeakingUntil = 0f;
            if (!loaded) return;
            try { Tolk_Silence(); } catch { }
        }

        public static void RepeatLast()
        {
            if (string.IsNullOrEmpty(lastSpoken)) { Say(Loc.T("Noch nichts gesagt.", "Nothing said yet.")); return; }
            lastText = "";
            Say(lastSpoken);
        }

        private static readonly Regex SpriteName = new Regex("<sprite[^>]*name=\"?([^\" >]+)\"?[^>]*>", RegexOptions.IgnoreCase);
        private static readonly Regex Tags = new Regex("<[^>]*>");
        private static readonly Regex Spaces = new Regex("\\s+");

        /// <summary>Entfernt Rich-Text-Tags; Button-Sprites werden durch ihren Namen ersetzt.</summary>
        public static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.IndexOf("<sprite", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var sb = new StringBuilder();
                int last = 0;
                foreach (Match m in SpriteName.Matches(s, 0))
                {
                    sb.Append(s, last, m.Index - last);
                    sb.Append(' ').Append(PrettySprite(m.Groups[1].Value)).Append(' ');
                    last = m.Index + m.Length;
                }
                sb.Append(s, last, s.Length - last);
                s = sb.ToString();
            }
            s = Tags.Replace(s, " ");
            s = s.Replace("​", "").Replace(" ", " ");
            s = Spaces.Replace(s, " ").Trim();
            return s;
        }

        private static string PrettySprite(string n)
        {
            if (string.IsNullOrEmpty(n)) return "";
            var sb = new StringBuilder();
            foreach (char c in n)
            {
                if (c == '_' || c == '-') sb.Append(' ');
                else sb.Append(c);
            }
            return sb.ToString().Trim();
        }
    }
}
