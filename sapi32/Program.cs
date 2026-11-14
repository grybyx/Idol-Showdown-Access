using System;
using System.Reflection;
using System.Text;

// 32-Bit-Sprachausgabe-Helfer: steuert die 32-Bit-SAPI-Engine (SAPI.SpVoice) per COM.
// Befehle über die Standardeingabe, eine Zeile pro Befehl, Text Base64 (UTF-8):
//   S <b64>  sprechen, laufende Ausgabe abbrechen
//   Q <b64>  sprechen, anhängen
//   X        Ausgabe stoppen
//   R <n>    Tempo -10..10
//   L <n>    Lautstärke 0..100
//   N <b64>  Stimme fest wählen (Teil des Namens, leer = Standard), schaltet Sprachmischung ab
//   M <codes> Stimme für Mod-Ansagen nach Sprache (LCIDs hex, ";"-getrennt, z. B. 407)
//   G <codes> Stimme für Spieltexte nach Sprache (z. B. 409;809)
// Spieltexte sind im Text mit \u0001 ... \u0002 markiert und werden mit der Spielstimme gesprochen.
// Antworten: "VOICES\t<Name1>\t<Name2>..." und "VOICE\t<aktuelle Stimme>".
internal static class Program
{
    private const int Async = 1, Purge = 2, NotXml = 16;
    private static object voice;

    [System.Runtime.InteropServices.DllImport("ole32.dll")]
    private static extern int CoCreateInstance(ref Guid clsid, IntPtr outer, uint context, ref Guid iid, out IntPtr ppv);

    private static object Call(object o, string name, params object[] args)
    {
        return o.GetType().InvokeMember(name, BindingFlags.InvokeMethod, null, o, args, null, null, null);
    }

    private static object Prop(object o, string name)
    {
        return o.GetType().InvokeMember(name, BindingFlags.GetProperty, null, o, null, null, null, null);
    }

    private static void SetProp(object o, string name, object value)
    {
        o.GetType().InvokeMember(name, BindingFlags.SetProperty, null, o, new object[] { value }, null, null, null);
    }

    private static string Dec(string b64)
    {
        if (string.IsNullOrEmpty(b64)) return "";
        return Encoding.UTF8.GetString(Convert.FromBase64String(b64));
    }

    private const int IsXml = 8;
    private static string modVoiceName, gameVoiceName;

    private static string Esc(string t)
    {
        return t.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }

    private static void Speak(string text, bool purge)
    {
        bool mix = !string.IsNullOrEmpty(gameVoiceName) && gameVoiceName != modVoiceName && text.IndexOf('\u0001') >= 0;
        if (!mix)
        {
            string plain = text.Replace("\u0001", "").Replace("\u0002", "");
            Call(voice, "Speak", plain, Async | NotXml | (purge ? Purge : 0));
            return;
        }
        // Spieltexte in der Stimme der Spielsprache, Rest in der Mod-Stimme (SAPI-XML)
        var sb = new StringBuilder();
        var seg = new StringBuilder();
        bool inGame = false;
        foreach (char ch in text)
        {
            if (ch == '\u0001' || ch == '\u0002')
            {
                bool toGame = ch == '\u0001';
                if (toGame == inGame) continue;
                sb.Append(Esc(seg.ToString())); seg.Length = 0;
                if (toGame) sb.Append("<voice required=\"Name=").Append(Esc(gameVoiceName)).Append("\">");
                else sb.Append("</voice>");
                inGame = toGame;
                continue;
            }
            seg.Append(ch);
        }
        sb.Append(Esc(seg.ToString()));
        if (inGame) sb.Append("</voice>");
        Call(voice, "Speak", sb.ToString(), Async | IsXml | (purge ? Purge : 0));
    }

    private static string TokenName(object tok)
    {
        try { return (string)Call(tok, "GetAttribute", "Name"); } catch { }
        try { return (string)Call(tok, "GetDescription", 0); } catch { return ""; }
    }

    /// <summary>Erste installierte Stimme zu einer der Sprachen (LCID hex) suchen.</summary>
    private static object FindByLanguage(string codes)
    {
        foreach (string c in codes.Split(';'))
        {
            string code = c.Trim();
            if (code.Length == 0) continue;
            try
            {
                object tokens = Call(voice, "GetVoices", "Language=" + code, "");
                if ((int)Prop(tokens, "Count") > 0) return Call(tokens, "Item", 0);
            }
            catch { }
        }
        return null;
    }

    private static void SelectModVoice(string codes)
    {
        object tok = FindByLanguage(codes);
        if (tok != null)
        {
            try { SetProp(voice, "Voice", tok); } catch { }
            modVoiceName = TokenName(tok);
        }
        else modVoiceName = TokenName(Prop(voice, "Voice"));
        ReportVoices();
    }

    private static void SelectGameVoice(string codes)
    {
        object tok = FindByLanguage(codes);
        gameVoiceName = tok != null ? TokenName(tok) : null; // keine passende Stimme: Mod-Stimme verwenden
        ReportVoices();
    }

    private static object FindByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        try
        {
            object tokens = Call(voice, "GetVoices", "", "");
            int n = (int)Prop(tokens, "Count");
            for (int i = 0; i < n; i++)
            {
                object tok = Call(tokens, "Item", i);
                if (TokenName(tok) == name) return tok;
            }
        }
        catch { }
        return null;
    }

    private static void SelectModVoiceByName(string name)
    {
        object tok = FindByName(name);
        if (tok != null)
        {
            try { SetProp(voice, "Voice", tok); } catch { }
            modVoiceName = TokenName(tok);
        }
        ReportVoices();
    }

    private static void SelectGameVoiceByName(string name)
    {
        object tok = FindByName(name);
        gameVoiceName = tok != null ? TokenName(tok) : null;
        ReportVoices();
    }

    private static void ReportVoices()
    {
        Out("VOICE\t" + (modVoiceName ?? CurrentVoice()) + "\t" + (gameVoiceName ?? ""));
    }

    private static readonly object outLock = new object();

    private static void Out(string line)
    {
        lock (outLock)
        {
            Console.Out.WriteLine(line);
            Console.Out.Flush();
        }
    }

    private static void WatchSpeaking()
    {
        bool last = false;
        while (true)
        {
            bool speaking = false;
            try
            {
                object status = Prop(voice, "Status");
                speaking = (int)Prop(status, "RunningState") == 2; // 1 = fertig, 2 = spricht
            }
            catch { }
            if (speaking != last)
            {
                last = speaking;
                Out("SPK\t" + (speaking ? "1" : "0"));
            }
            System.Threading.Thread.Sleep(30);
        }
    }

    private static string CurrentVoice()
    {
        try { return (string)Call(Prop(voice, "Voice"), "GetDescription", 0); } catch { return ""; }
    }

    private static void ListVoices()
    {
        var sb = new StringBuilder("VOICES");
        try
        {
            object tokens = Call(voice, "GetVoices", "", "");
            int n = (int)Prop(tokens, "Count");
            for (int i = 0; i < n; i++)
            {
                object tok = Call(tokens, "Item", i);
                string lang = "";
                try { lang = (string)Call(tok, "GetAttribute", "Language") ?? ""; } catch { }
                // Eintrag: Anzeigename \u001F Name-Attribut \u001F Sprachcode(s)
                sb.Append('\t').Append(((string)Call(tok, "GetDescription", 0)).Replace('\t', ' '))
                  .Append('\u001F').Append(TokenName(tok)).Append('\u001F').Append(lang);
            }
        }
        catch { }
        Out(sb.ToString());
        ReportVoices();
    }

    private static void SelectVoice(string part)
    {
        try
        {
            object tokens = Call(voice, "GetVoices", "", "");
            int n = (int)Prop(tokens, "Count");
            for (int i = 0; i < n; i++)
            {
                object tok = Call(tokens, "Item", i);
                string d = (string)Call(tok, "GetDescription", 0);
                if (part.Length == 0 || d.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    SetProp(voice, "Voice", tok);
                    modVoiceName = TokenName(tok);
                    gameVoiceName = null;
                    break;
                }
            }
        }
        catch { }
        ReportVoices();
    }

    // Ohne [STAThread] läuft Main im MTA: SpVoice ist freethreaded, der Status-Thread darf mitlesen.
    private static int Main()
    {
        try
        {
            Guid clsid = new Guid("96749377-3391-11D2-9EE3-00C04F797396"); // SAPI.SpVoice
            Guid iid = new Guid("00020400-0000-0000-C000-000000000046");   // IDispatch
            IntPtr p;
            int hr = CoCreateInstance(ref clsid, IntPtr.Zero, 1 /* CLSCTX_INPROC_SERVER */, ref iid, out p);
            if (hr != 0 || p == IntPtr.Zero) throw new Exception("SAPI nicht verfuegbar, HRESULT 0x" + hr.ToString("X8"));
            voice = System.Runtime.InteropServices.Marshal.GetObjectForIUnknown(p);
        }
        catch (Exception e)
        {
            Out("ERROR\t" + e.Message);
            return 1;
        }
        try { modVoiceName = TokenName(Prop(voice, "Voice")); } catch { }
        ListVoices();
        // Status-Thread: meldet "SPK 1" beim Sprechbeginn und "SPK 0" am Ende (für Audio-Ducking)
        var watcher = new System.Threading.Thread(WatchSpeaking);
        watcher.IsBackground = true;
        watcher.Start();
        var input = new System.IO.StreamReader(Console.OpenStandardInput(4096), Encoding.ASCII);
        string line;
        while ((line = input.ReadLine()) != null)
        {
            try
            {
                if (line.Length == 0) continue;
                char c = line[0];
                string arg = line.Length > 2 ? line.Substring(2) : "";
                switch (c)
                {
                    case 'S': Speak(Dec(arg), true); break;
                    case 'Q': Speak(Dec(arg), false); break;
                    case 'X': Speak("", true); break;
                    case 'R': SetProp(voice, "Rate", Math.Max(-10, Math.Min(10, int.Parse(arg)))); break;
                    case 'L': SetProp(voice, "Volume", Math.Max(0, Math.Min(100, int.Parse(arg)))); break;
                    case 'N': SelectVoice(Dec(arg)); break;
                    case 'M': SelectModVoice(arg); break;
                    case 'P': SelectModVoiceByName(Dec(arg)); break;
                    case 'H': SelectGameVoiceByName(Dec(arg)); break;
                    case 'G': SelectGameVoice(arg); break;
                }
            }
            catch (Exception e)
            {
                Out("ERROR\t" + e.Message);
            }
        }
        try { Speak("", true); } catch { }
        return 0;
    }
}
