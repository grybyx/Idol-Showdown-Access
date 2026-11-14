using BepInEx.Configuration;

namespace IdolShowdownAccess
{
    internal static class ModConfig
    {
        /// <summary>Erhöhen, wenn das Mod-Menü neue Einträge bekommt: dann öffnet es sich nach dem Update einmal.</summary>
        public const int CurrentMenuVersion = 110;

        public static ConfigEntry<bool> ModEnabled;
        public static ConfigEntry<bool> AskOnStartup;
        public static ConfigEntry<bool> SpeechViaSapi;
        public static ConfigEntry<string> SapiVoice;
        public static ConfigEntry<bool> AutoVoice;
        public static ConfigEntry<string> PreferredVoices;
        public static ConfigEntry<int> SettingsVersion;
        public static ConfigEntry<int> MenuVersion;
        public static ConfigEntry<int> ModLanguage;
        public static ConfigEntry<string> LastGameLocale;
        public static ConfigEntry<int> SapiRate;
        public static ConfigEntry<int> SapiVolume;
        public static ConfigEntry<bool> Ducking;
        public static ConfigEntry<bool> DuckTones;
        public static ConfigEntry<int> DuckingStrength; // alt (bis 1.0.7), wird übernommen
        public static ConfigEntry<int> DuckLevel;
        public static ConfigEntry<bool> MenuSpeech;
        public static ConfigEntry<bool> MenuPosition;
        public static ConfigEntry<bool> MenuDescriptions;
        public static ConfigEntry<bool> CssSnapCursor;
        public static ConfigEntry<bool> EnemyLocator;
        public static ConfigEntry<bool> SideSwitchCue;
        public static ConfigEntry<bool> WallCue;
        public static ConfigEntry<bool> ProjectileCue;
        public static ConfigEntry<bool> HealthWarnings;
        public static ConfigEntry<bool> InvulCue;
        public static ConfigEntry<bool> BigHitCue;
        public static ConfigEntry<int> BigHitPercent;
        public static ConfigEntry<bool> EnemyHealth;
        public static ConfigEntry<bool> FightAnnouncements;
        public static ConfigEntry<bool> ResourceAnnouncements;
        public static ConfigEntry<bool> TextOverlays;
        public static ConfigEntry<bool> LogSpeech;
        public static ConfigEntry<int> CueVolume;
        public static ConfigEntry<bool> MenuTonePreview;
        public static ConfigEntry<int> CriticalHealthPercent;
        public static ConfigEntry<int> LocalPlayerSide;
        public static ConfigEntry<float> PanRange;

        public static ConfigEntry<int> PadStatusButton;
        public static ConfigEntry<int> PadEnemyButton;
        public static ConfigEntry<int> PadRepeatButton;
        public static ConfigEntry<int> PadStopButton;
        public static ConfigEntry<bool> PadShortcuts;

        public static ConfigFile File;
        /// <summary>true beim allerersten Start (keine Einstellungsdatei vorhanden).</summary>
        public static bool FirstRun;

        public static void Bind(ConfigFile cfg)
        {
            File = cfg;
            FirstRun = !System.IO.File.Exists(cfg.ConfigFilePath);
            ModEnabled = cfg.Bind("Allgemein", "ModAktiv", true, "Schaltet den gesamten Barrierefreiheits-Mod an oder aus.");
            AskOnStartup = cfg.Bind("Allgemein", "StartabfrageBeiJedemStart", true, "Beim Spielstart das gesprochene Mod-Menü öffnen.");
            SpeechViaSapi = cfg.Bind("Allgemein", "SprachausgabeUeberSAPI", true, "true = alle Ansagen über die 32-Bit-Windows-Sprachausgabe (SAPI), unabhängig vom Screenreader. false = über den laufenden Screenreader (NVDA, JAWS ...).");

            ModLanguage = cfg.Bind("Sprache", "ModAnsagenSprache", 0, new ConfigDescription("Sprache der Mod-Ansagen: 0 = automatisch wie das Spiel (Deutsch nur bei deutschem Spiel, sonst Englisch), 1 = Deutsch, 2 = Englisch.", new AcceptableValueRange<int>(0, 2)));
            LastGameLocale = cfg.Bind("Sprache", "ZuletztErkannteSpielsprache", "", "Wird automatisch gesetzt, damit schon die erste Ansage in der richtigen Sprache kommt.");
            AutoVoice = cfg.Bind("Sprache", "StimmeAutomatisch", true, "Stimmen automatisch wählen: Spieltexte in einer Stimme der Spielsprache, Mod-Ansagen in einer Stimme der Ansagensprache.");
            PreferredVoices = cfg.Bind("Sprache", "BevorzugteStimmen", "", "Bevorzugte 32-Bit-Stimme je Sprache (wird im Mod-Menü unter Stimme gesetzt), z. B. 9=Microsoft David Desktop|7=Microsoft Hedda Desktop.");
            SapiVoice = cfg.Bind("Sprache", "Stimme", "", "Feste Stimme, nur wenn StimmeAutomatisch = false.");
            SapiRate = cfg.Bind("Sprache", "Tempo", 0, new ConfigDescription("Sprechtempo von -10 bis 10.", new AcceptableValueRange<int>(-10, 10)));
            SapiVolume = cfg.Bind("Sprache", "Lautstaerke", 100, new ConfigDescription("Lautstärke der Stimme in Prozent.", new AcceptableValueRange<int>(0, 100)));
            Ducking = cfg.Bind("Sprache", "AudioDucking", true, "Spiel leiser, solange die Sprachausgabe spricht.");
            DuckTones = cfg.Bind("Sprache", "DuckingBeiToenen", false, "Spiel auch leiser, solange Mod-Töne klingen.");
            DuckingStrength = cfg.Bind("Sprache", "DuckingStaerke", 60, new ConfigDescription("Veraltet (bis 1.0.7). Wird einmalig nach SpielLautstaerkeBeimDucking übernommen.", new AcceptableValueRange<int>(10, 90)));
            DuckLevel = cfg.Bind("Sprache", "SpielLautstaerkeBeimDucking", 60, new ConfigDescription("Lautstärke des Spiels in Prozent, solange der Mod spricht.", new AcceptableValueRange<int>(10, 90)));

            MenuSpeech = cfg.Bind("Menüs", "SprechendeMenues", true, "Menüs, Schalter und Regler vorlesen.");
            MenuPosition = cfg.Bind("Menüs", "PositionAnsagen", true, "Position im Menü ansagen (z. B. 3 von 7).");
            MenuDescriptions = cfg.Bind("Menüs", "BeschreibungenVorlesen", true, "Beschreibungstexte und Fenstertexte zusätzlich vorlesen.");
            CssSnapCursor = cfg.Bind("Charakterauswahl", "CursorEinrasten", true, "Der Auswahl-Cursor springt mit jedem Richtungsdruck direkt zur nächsten Figur bzw. Schaltfläche.");
            TextOverlays = cfg.Bind("Texte", "EinblendungenVorlesen", true, "Dialoge, Tutorial-Texte, Popups und Hinweise vorlesen.");

            EnemyLocator = cfg.Bind("Kampf", "Gegnerortung", true, "Ortungston für den Gegner: Stereo = Seite, Tempo = Entfernung, heller gleitender Klang = in der Luft.");
            SideSwitchCue = cfg.Bind("Kampf", "SeitenwechselSignal", true, "Signal, wenn der Gegner die Seite wechselt.");
            WallCue = cfg.Bind("Kampf", "WandWarnung", true, "Dumpfes Klopfen, wenn du in der Ecke stehst.");
            ProjectileCue = cfg.Bind("Kampf", "GeschossWarnung", true, "Warnton von der Seite des Gegners, wenn er ein Geschoss abfeuert.");
            InvulCue = cfg.Bind("Kampf", "UnverwundbarSignal", true, "Glockenklang, wenn deine Figur unverwundbar wird, leiser Ton, wenn es endet; Kling bei Super-Rüstung.");
            BigHitCue = cfg.Bind("Kampf", "GrosserSchadenSignal", true, "Einschlag-Klang bei viel Schaden in einer Trefferserie, danach Ansage des Verlusts.");
            BigHitPercent = cfg.Bind("Kampf", "GrosserSchadenAbProzent", 15, new ConfigDescription("Ab so viel Prozent Verlust in einer Serie gilt es als großer Schaden.", new AcceptableValueRange<int>(5, 50)));
            HealthWarnings = cfg.Bind("Kampf", "GesundheitsWarnungen", true, "Herzschlag und Ansagen bei niedriger eigener Gesundheit.");
            CriticalHealthPercent = cfg.Bind("Kampf", "KritischeGesundheitProzent", 25, new ConfigDescription("Ab diesem Prozentwert beginnt der Herzschlag.", new AcceptableValueRange<int>(5, 60)));
            EnemyHealth = cfg.Bind("Kampf", "GegnerGesundheitAnsagen", true, "Ansage, wenn der Gegner unter 50 und 25 Prozent fällt.");
            FightAnnouncements = cfg.Bind("Kampf", "KampfAnsagen", true, "Runden, Ergebnisse und Zeitwarnungen ansagen.");
            ResourceAnnouncements = cfg.Bind("Kampf", "RessourcenAnsagen", true, "Ansage, wenn Super-Leiste, Burst oder Kollab bereit werden.");
            LocalPlayerSide = cfg.Bind("Kampf", "EigeneSeiteLokal", 1, new ConfigDescription("Im lokalen Versus: 1 = du bist Spieler 1, 2 = du bist Spieler 2.", new AcceptableValueRange<int>(1, 2)));

            CueVolume = cfg.Bind("Audio", "LautstaerkeModToene", 70, new ConfigDescription("Lautstärke der Mod-Töne in Prozent.", new AcceptableValueRange<int>(0, 100)));
            MenuTonePreview = cfg.Bind("Audio", "ToeneImMenueVorspielen", true, "Im Mod-Menü nach der Ansage eines Eintrags dessen Ton vorspielen.");
            PanRange = cfg.Bind("Audio", "PanoramaBreite", 0f, "Abstand in Spieleinheiten, ab dem der Ortungston ganz links/rechts klingt. 0 = automatisch aus der Bühnenbreite.");

            PadShortcuts = cfg.Bind("Controller", "ControllerKuerzelAktiv", true, "Mod-Funktionen über Controller-Tasten.");
            PadStatusButton = cfg.Bind("Controller", "TasteStatus", 6, "Joystick-Tastennummer für Status/Details, gedrückt halten = Mod-Menü (Xbox: 0 A, 1 B, 2 X, 3 Y, 4 LB, 5 RB, 6 Ansicht, 7 Menü, 8 linker Stick, 9 rechter Stick; -1 = aus).");
            PadEnemyButton = cfg.Bind("Controller", "TasteGegnerposition", 8, "Joystick-Tastennummer für die Gegnerposition (-1 = aus).");
            PadRepeatButton = cfg.Bind("Controller", "TasteWiederholen", 9, "Joystick-Tastennummer für Letzte Ansage wiederholen (-1 = aus).");
            PadStopButton = cfg.Bind("Controller", "TasteSpracheStoppen", -1, "Joystick-Tastennummer, die die Sprachausgabe unterbricht (-1 = aus).");

            LogSpeech = cfg.Bind("Diagnose", "AusfuehrlichesProtokoll", FirstRun ? false : true, "Jede Ansage und Zusatzdaten ins Protokoll IdolShowdownAccess.log schreiben (hilft bei der Fehlersuche).");
            SettingsVersion = cfg.Bind("Diagnose", "EinstellungsVersion", 0, "Intern, nicht ändern.");
            MenuVersion = cfg.Bind("Diagnose", "MenueVersion", 0, "Intern, nicht ändern.");

            // Übernahme alter Werte
            if (SettingsVersion.Value < 107) AutoVoice.Value = true; // 1.0.7: Stimmenwahl wieder automatisch
            if (SettingsVersion.Value < 110 && !FirstRun)
            {
                DuckLevel.Value = Clamp(100 - DuckingStrength.Value, 10, 90); // "um X % leiser" -> "auf Y %"
            }
            SettingsVersion.Value = 110;
        }

        private static int Clamp(int v, int a, int b) => v < a ? a : (v > b ? b : v);

        public static void Save()
        {
            try { File?.Save(); } catch { }
        }

        public static bool On(ConfigEntry<bool> e) => ModEnabled.Value && e.Value;
    }
}
