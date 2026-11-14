using System.Runtime.InteropServices;

namespace IdolShowdownAccess
{
    /// <summary>
    /// Sprache der Mod-Ansagen. Automatisch: Deutsch, wenn das Spiel auf Deutsch läuft,
    /// sonst Englisch – damit Spieltexte, Mod-Ansagen und Stimme zusammenpassen.
    /// </summary>
    internal static class Loc
    {
        private static string gameLocale = "";

        public static void SetGameLocale(string locale)
        {
            gameLocale = locale ?? "";
            if (gameLocale.Length > 0 && ModConfig.LastGameLocale.Value != gameLocale)
            {
                ModConfig.LastGameLocale.Value = gameLocale;
                ModConfig.Save();
            }
        }

        [DllImport("kernel32.dll")] private static extern ushort GetUserDefaultUILanguage();

        /// <summary>Solange die Spielsprache unbekannt ist: Sprache der Windows-Oberfläche.</summary>
        private static string WindowsLocale()
        {
            try { return (GetUserDefaultUILanguage() & 0x3FF) == 7 ? "de" : "en"; } catch { return "en"; }
        }

        private static string Locale => gameLocale.Length > 0 ? gameLocale
            : !string.IsNullOrEmpty(ModConfig.LastGameLocale.Value) ? ModConfig.LastGameLocale.Value : WindowsLocale();

        public static bool IsGerman(string l)
        {
            l = (l ?? "").ToLowerInvariant();
            return l.StartsWith("de") || l.StartsWith("german") || l == "ger";
        }

        /// <summary>true = Mod spricht Englisch.</summary>
        public static bool En
        {
            get
            {
                switch (ModConfig.ModLanguage.Value)
                {
                    case 1: return false;
                    case 2: return true;
                    default:
                        return !IsGerman(Locale);
                }
            }
        }

        /// <summary>Text in der aktuellen Mod-Sprache.</summary>
        public static string T(string de, string en) => En ? en : de;

        /// <summary>SAPI-Sprachcodes für die Mod-Ansagen.</summary>
        public static string ModVoiceCodes => En ? "409;809;c09;1009" : "407;c07;807";
    }
}
