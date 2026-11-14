using System;
using System.IO;
using System.Text;
using BepInEx.Logging;

namespace IdolShowdownAccess
{
    /// <summary>
    /// Eigenes Protokoll des Mods: IdolShowdownAccess.log im Pluginordner (Ausweichordner
    /// %LOCALAPPDATA%\IdolShowdownAccess). Beim Start wird das alte Log nach
    /// IdolShowdownAccess_vorher.log verschoben, damit ein Absturz nicht überschrieben wird.
    /// Alle Meldungen der Quelle des Mods aus dem BepInEx-Log landen hier zusätzlich.
    /// </summary>
    internal sealed class FileLog : ILogListener
    {
        private readonly StreamWriter writer;
        private readonly string source;
        public static string Folder { get; private set; }

        private FileLog(StreamWriter w, string src) { writer = w; source = src; }

        public static void Start(string pluginDir, string source, string version)
        {
            StreamWriter w = null;
            foreach (string dir in new[] { pluginDir, Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? pluginDir, "IdolShowdownAccess") })
            {
                try
                {
                    Directory.CreateDirectory(dir);
                    string log = Path.Combine(dir, "IdolShowdownAccess.log");
                    string old = Path.Combine(dir, "IdolShowdownAccess_vorher.log");
                    if (File.Exists(log))
                    {
                        if (File.Exists(old)) File.Delete(old);
                        // File.Move fehlt in der gekürzten Spielbibliothek; zur Laufzeit ist die vollständige da
                        typeof(File).GetMethod("Move", new[] { typeof(string), typeof(string) }).Invoke(null, new object[] { log, old });
                    }
                    w = new StreamWriter(new FileStream(log, FileMode.Create, FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(true));
                    w.AutoFlush = true;
                    Folder = dir;
                    break;
                }
                catch { w = null; }
            }
            if (w == null) return;
            w.WriteLine("Idol Showdown Access " + version + " - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Logger.Listeners.Add(new FileLog(w, source));
        }

        public void LogEvent(object sender, LogEventArgs e)
        {
            try
            {
                if (e.Source == null || e.Source.SourceName != source) return;
                string lvl = e.Level == LogLevel.Warning ? "WARNUNG " : e.Level == LogLevel.Error || e.Level == LogLevel.Fatal ? "FEHLER  " : "";
                lock (writer) writer.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff") + "  " + lvl + e.Data);
            }
            catch { }
        }

        public void Dispose()
        {
            try { writer.Dispose(); } catch { }
        }
    }
}
