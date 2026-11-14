using System;
using System.Collections.Generic;
using System.Diagnostics;
using BepInEx.Configuration;

namespace IdolShowdownAccess
{
    /// <summary>
    /// Gesprochenes Mod-Menü mit Untermenüs. Wird auch als Startabfrage verwendet.
    /// Hoch/Runter wählen, Links/Rechts ändern, Bestätigen schaltet oder öffnet,
    /// Zurück geht eine Ebene zurück (auf der Hauptebene: verwerfen), F11 speichert und schließt.
    /// </summary>
    internal static class ModMenu
    {
        private class Item
        {
            public Func<string> Label;
            public Func<string> Value;
            public Action<int> Change;   // -1 / +1
            public Action Activate;      // Bestätigen
            public Func<string> Hint;
            public string Sample;        // Ton, der nach der Ansage vorgespielt wird
            public bool NoEcho;          // nach dem Auslösen nicht erneut vorlesen
            public Func<List<Item>> Submenu;
        }

        private sealed class Level { public List<Item> Items; public int Index; public Func<string> Title; public Func<List<Item>> Build; }

        private static readonly List<Level> stack = new List<Level>();
        private static Dictionary<ConfigDefinition, object> snapshot;
        public static bool IsOpen { get; private set; }
        public static float OpenedAt;
        public static float ClosedAt = -10f;
        /// <summary>Kurz nach dem Schließen keine Eingaben ans Spiel durchreichen (gleicher Tastendruck).</summary>
        public static bool RecentlyClosed => UnityEngine.Time.realtimeSinceStartup - ClosedAt < 0.3f;

        private static string T(string de, string en) => Loc.T(de, en);
        private static string OnOff(bool v) => v ? T("an", "on") : T("aus", "off");
        private static string Pct(int v) => v + T(" Prozent", " percent");
        private static int Clamp(int v, int a, int b) => v < a ? a : (v > b ? b : v);

        public const string DonationUrl = "https://buymeacoffee.com/blindretrogamer/";
        public const string GrybyxUrl = "https://github.com/grybyx/";

        // ---------------- Bausteine ----------------

        private static Item Toggle(Func<string> label, ConfigEntry<bool> e, Func<string> hint = null, string sample = null, Action after = null)
        {
            return new Item
            {
                Label = label,
                Value = () => OnOff(e.Value),
                Change = d => { e.Value = !e.Value; after?.Invoke(); },
                Activate = () => { e.Value = !e.Value; after?.Invoke(); },
                Hint = hint,
                Sample = sample
            };
        }

        private static Item Number(Func<string> label, ConfigEntry<int> e, int min, int max, int step, Func<int, string> fmt, Action after = null, Func<string> hint = null, string sample = null)
        {
            return new Item
            {
                Label = label,
                Value = () => fmt(e.Value),
                Change = d => { e.Value = Clamp(e.Value + d * step, min, max); after?.Invoke(); },
                Activate = () => { e.Value = e.Value + step > max ? min : e.Value + step; after?.Invoke(); },
                Hint = hint,
                Sample = sample
            };
        }

        private static Item Sub(Func<string> label, Func<List<Item>> build, Func<string> hint = null)
        {
            return new Item { Label = label, Value = () => T("Untermenü", "submenu"), Submenu = build, Hint = hint, NoEcho = true };
        }

        private static Item Action(Func<string> label, Action act, Func<string> hint = null)
        {
            return new Item { Label = label, Value = () => "", Change = d => { }, Activate = act, Hint = hint, NoEcho = true };
        }

        private static Item Back() => Action(() => T("Zurück", "Back"), () => GoBack());

        // ---------------- Menüs ----------------

        private static List<Item> MainMenu()
        {
            return new List<Item>
            {
                Toggle(() => T("Mod aktiv", "Mod enabled"), ModConfig.ModEnabled,
                    () => T("Schaltet alle Ansagen und Töne des Mods an oder aus.", "Turns all announcements and sounds of the mod on or off.")),
                Sub(() => T("Sprache und Stimme", "Language and voice"), LanguageMenu),
                Sub(() => T("Vorlesen", "Reading"), ReadingMenu),
                Sub(() => T("Kampf und Gegner", "Fight and opponent"), FightMenu),
                Sub(() => T("Töne und Lautstärke", "Sounds and volume"), SoundMenu),
                Sub(() => T("Steuerung", "Controls"), ControlsMenu),
                Sub(() => T("Erweitert", "Advanced"), AdvancedMenu),
                Action(() => "Donation", () => OpenUrl(DonationUrl, T("die Spendenseite", "the donation page")),
                    () => T("Öffnet die Spendenseite im Browser.", "Opens the donation page in your browser.")),
                Action(() => "Accessibility Mods by Grybyx", () => OpenUrl(GrybyxUrl, T("die Seite mit weiteren Zugänglichkeits-Mods", "the page with more accessibility mods")),
                    () => T("Öffnet die Seite mit weiteren Zugänglichkeits-Mods im Browser.", "Opens the page with more accessibility mods in your browser.")),
                Action(() => T("Speichern und schließen", "Save and close"), () => Close(true),
                    () => T("Speichert alle Änderungen. F11 macht das von überall im Menü.", "Saves all changes. F11 does this from anywhere in the menu.")),
                Action(() => T("Verwerfen", "Discard"), () => Close(false),
                    () => T("Schließt das Menü und stellt alle Werte auf den Stand beim Öffnen zurück.", "Closes the menu and restores all values to how they were when it opened.")),
            };
        }

        private static string LangName(bool en) => en ? T("Englisch", "English") : T("Deutsch", "German");

        private static List<Item> LanguageMenu()
        {
            return new List<Item>
            {
                new Item
                {
                    Label = () => T("Sprache des Mods", "Mod language"),
                    Value = () => ModConfig.ModLanguage.Value == 1 ? "Deutsch" : ModConfig.ModLanguage.Value == 2 ? "English"
                        : T("automatisch wie das Spiel, jetzt ", "automatic like the game, currently ") + LangName(Loc.En),
                    Change = d => SetLanguage((ModConfig.ModLanguage.Value + d + 3) % 3),
                    Activate = () => SetLanguage((ModConfig.ModLanguage.Value + 1) % 3),
                    Hint = () => T("Automatisch: Deutsch, wenn das Spiel auf Deutsch läuft, sonst Englisch. So passen Spieltexte, Ansagen und Stimme zusammen.",
                                   "Automatic: German when the game runs in German, otherwise English, so game texts, announcements and voice match.")
                },
                new Item
                {
                    Label = () => T("Sprachausgabe", "Speech output"),
                    Value = () => ModConfig.SpeechViaSapi.Value ? T("Windows-Sprachausgabe SAPI", "Windows speech SAPI") : T("Screenreader", "screen reader"),
                    Change = d => { ModConfig.SpeechViaSapi.Value = !ModConfig.SpeechViaSapi.Value; Speech.ApplyOutputMode(); },
                    Activate = () => { ModConfig.SpeechViaSapi.Value = !ModConfig.SpeechViaSapi.Value; Speech.ApplyOutputMode(); },
                    Hint = () => T("SAPI spricht alles selbst mit einer 32-Bit-Stimme, unabhängig von NVDA oder JAWS. Screenreader gibt alles an deinen laufenden Screenreader weiter; Tempo und Stimme bestimmt dann der Screenreader.",
                                   "SAPI speaks everything itself with a 32-bit voice, independent of NVDA or JAWS. Screen reader passes everything to your running screen reader, which then decides rate and voice.")
                },
                new Item
                {
                    Label = () => T("Stimme für ", "Voice for ") + LangName(Loc.En),
                    Value = () =>
                    {
                        if (!ModConfig.SpeechViaSapi.Value) return T("nur bei SAPI-Ausgabe", "only with SAPI output");
                        string v = Sapi32.CurrentVoice;
                        return v.Length > 0 ? v : T("keine gefunden", "none found");
                    },
                    Change = d => CycleVoice(d),
                    Activate = () => CycleVoice(1),
                    NoEcho = true,
                    Hint = () => T("Links und Rechts wählen unter den installierten 32-Bit-Stimmen dieser Sprache. Die Wahl wird je Sprache gemerkt. Spieltexte in einer anderen Sprache spricht automatisch eine Stimme jener Sprache.",
                                   "Left and right choose among the installed 32-bit voices for this language. The choice is remembered per language. Game texts in another language automatically use a voice of that language.")
                },
                Number(() => T("Sprechtempo", "Speech rate"), ModConfig.SapiRate, -10, 10, 1, v => v.ToString(), () => Sapi32.ApplySettings()),
                Number(() => T("Lautstärke der Stimme", "Voice volume"), ModConfig.SapiVolume, 10, 100, 10, Pct, () => Sapi32.ApplySettings()),
                Toggle(() => T("Ducking bei Sprache", "Ducking while speaking"), ModConfig.Ducking,
                    () => T("Das Spiel wird leiser, solange der Mod spricht, und danach sanft wieder lauter.", "The game gets quieter while the mod speaks and then fades back.")),
                Toggle(() => T("Ducking bei Tönen", "Ducking for sounds"), ModConfig.DuckTones,
                    () => T("Das Spiel wird auch leiser, solange ein Mod-Ton klingt. Im Kampf eher störend, deshalb standardmäßig aus.", "The game also gets quieter while a mod sound plays. Usually distracting in fights, so off by default.")),
                Number(() => T("Spiel-Lautstärke beim Ducking", "Game volume while ducking"), ModConfig.DuckLevel, 10, 90, 10, Pct),
                Back()
            };
        }

        private static List<Item> ReadingMenu()
        {
            return new List<Item>
            {
                Toggle(() => T("Sprechende Menüs", "Spoken menus"), ModConfig.MenuSpeech,
                    () => T("Liest Menüpunkte, Schalter, Regler und die Charakterauswahl vor.", "Reads menu items, switches, sliders and the character select.")),
                Toggle(() => T("Position im Menü ansagen", "Announce position in menus"), ModConfig.MenuPosition,
                    () => T("Sagt zum Beispiel 3 von 7.", "Says for example 3 of 7.")),
                Toggle(() => T("Beschreibungen und Fenstertexte", "Descriptions and dialog texts"), ModConfig.MenuDescriptions),
                Toggle(() => T("Charakterauswahl: Cursor springt von Figur zu Figur", "Character select: cursor jumps between characters"), ModConfig.CssSnapCursor,
                    () => T("Jeder Richtungsdruck springt zur nächsten Figur, statt den Cursor frei zu bewegen.", "Each direction press jumps to the next character instead of moving the cursor freely.")),
                Toggle(() => T("Dialoge, Tutorial und Hinweise", "Dialogues, tutorial and notices"), ModConfig.TextOverlays),
                Back()
            };
        }

        private static List<Item> FightMenu()
        {
            return new List<Item>
            {
                Toggle(() => T("Gegnerortung per Ton", "Opponent locator sound"), ModConfig.EnemyLocator,
                    () => T("Plopp aus der Richtung des Gegners. Schneller heißt näher, heller gleitender Klang heißt in der Luft, Schnarren heißt direkt bei dir. Startet erst, wenn der Kampf freigegeben ist.",
                            "Plop from the opponent's direction. Faster means closer, a bright rising sound means in the air, a buzz means right next to you. Starts only once the fight begins."), "enemy_far"),
                Toggle(() => T("Signal bei Seitenwechsel", "Side switch signal"), ModConfig.SideSwitchCue, null, "side_switch"),
                Toggle(() => T("Warnung in der Ecke", "Corner warning"), ModConfig.WallCue,
                    () => T("Dumpfes Klopfen auf der Seite der Wand, wenn du in der Ecke stehst.", "Dull knocking on the wall's side when you are in the corner."), "wall"),
                Toggle(() => T("Warnung bei Geschossen", "Projectile warning"), ModConfig.ProjectileCue,
                    () => T("Zwei harte, kurze Töne von der Seite des Gegners, wenn er etwas abfeuert.", "Two hard short beeps from the opponent's side when they fire something."), "projectile"),
                Toggle(() => T("Signal bei Unverwundbarkeit", "Invincibility signal"), ModConfig.InvulCue,
                    () => T("Zwei helle Glockentöne, wenn deine Figur unverwundbar wird, leiser Ton abwärts, wenn es endet. Kling bei Super-Rüstung.",
                            "Two bright chimes when your character becomes invincible, a soft falling tone when it ends. Clink for super armor."), "invul"),
                Toggle(() => T("Signal bei großem Schaden", "Big damage signal"), ModConfig.BigHitCue,
                    () => T("Einschlag, wenn du in einer Trefferserie viel Gesundheit verlierst, danach die Ansage, wie viel.", "Impact sound when you lose a lot of health in one combo, then how much."), "bighit"),
                Number(() => T("Großer Schaden ab", "Big damage from"), ModConfig.BigHitPercent, 5, 50, 5, Pct),
                Toggle(() => T("Gesundheitswarnungen und Herzschlag", "Health warnings and heartbeat"), ModConfig.HealthWarnings, null, "heartbeat"),
                Number(() => T("Herzschlag ab Gesundheit", "Heartbeat below health"), ModConfig.CriticalHealthPercent, 5, 60, 5, Pct),
                Toggle(() => T("Gegner-Gesundheit ansagen", "Announce opponent health"), ModConfig.EnemyHealth),
                Toggle(() => T("Kampfansagen: Runden, Zeit, Ergebnis", "Match announcements: rounds, time, result"), ModConfig.FightAnnouncements),
                Toggle(() => T("Super, Burst und Kollab bereit ansagen", "Announce super, burst and collab ready"), ModConfig.ResourceAnnouncements),
                new Item
                {
                    Label = () => T("Deine Seite im lokalen Versus", "Your side in local versus"),
                    Value = () => T("Spieler ", "player ") + ModConfig.LocalPlayerSide.Value,
                    Change = d => ModConfig.LocalPlayerSide.Value = ModConfig.LocalPlayerSide.Value == 1 ? 2 : 1,
                    Activate = () => ModConfig.LocalPlayerSide.Value = ModConfig.LocalPlayerSide.Value == 1 ? 2 : 1,
                    Hint = () => T("Nur für zwei Menschen an einem Rechner. Gegen den Computer und online erkennt der Mod deine Seite selbst.",
                                   "Only for two people on one computer. Against the computer and online the mod detects your side itself.")
                },
                Back()
            };
        }

        private static List<Item> SoundMenu()
        {
            return new List<Item>
            {
                new Item
                {
                    Label = () => T("Lautstärke der Mod-Töne", "Volume of mod sounds"),
                    Value = () => Pct(ModConfig.CueVolume.Value),
                    Change = d =>
                    {
                        ModConfig.CueVolume.Value = Clamp(ModConfig.CueVolume.Value + d * 10, 0, 100);
                        Audio.MasterVolume = ModConfig.CueVolume.Value / 100f;
                        Audio.EnemyGround(0f);
                    },
                    Activate = () => Audio.EnemyGround(0f)
                },
                Toggle(() => T("Töne im Menü vorspielen", "Play sounds in the menu"), ModConfig.MenuTonePreview,
                    () => T("Einträge, zu denen ein Ton gehört, spielen ihn nach der Ansage vor.", "Entries that belong to a sound play it after being announced.")),
                Action(() => T("Alle Töne vorspielen", "Play all sounds"), () => Samples.PlayAll(),
                    () => T("Spielt jeden Ton des Mods mit Erklärung. Strg bricht ab.", "Plays every sound of the mod with an explanation. Control stops it.")),
                Back()
            };
        }

        private static readonly int[] PadChoices = { -1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

        public static string PadName(int b)
        {
            switch (b)
            {
                case 0: return T("A, PlayStation Kreuz", "A, PlayStation cross");
                case 1: return T("B, PlayStation Kreis", "B, PlayStation circle");
                case 2: return T("X, PlayStation Quadrat", "X, PlayStation square");
                case 3: return T("Y, PlayStation Dreieck", "Y, PlayStation triangle");
                case 4: return "LB, PlayStation L1";
                case 5: return "RB, PlayStation R1";
                case 6: return T("Ansicht, PlayStation Share", "View, PlayStation Share");
                case 7: return T("Menü, PlayStation Options", "Menu, PlayStation Options");
                case 8: return T("linken Stick drücken, L3", "press left stick, L3");
                case 9: return T("rechten Stick drücken, R3", "press right stick, R3");
                default: return T("aus", "off");
            }
        }

        private static Item PadItem(Func<string> label, ConfigEntry<int> e, Func<string> hint)
        {
            Action<int> step = d =>
            {
                int i = Array.IndexOf(PadChoices, e.Value);
                if (i < 0) i = 0;
                e.Value = PadChoices[(i + d + PadChoices.Length) % PadChoices.Length];
            };
            return new Item { Label = label, Value = () => PadName(e.Value), Change = step, Activate = () => step(1), Hint = hint };
        }

        private static string GameUseNote() => T(" Das Spiel nutzt A, B, X, Y, LB, RB und die Trigger im Kampf, Menü öffnet die Pause. Ansicht und die Stick-Klicks sind im Kampf frei.",
                                                  " The game uses A, B, X, Y, LB, RB and the triggers in fights, Menu opens the pause. View and the stick clicks are free in fights.");

        private static List<Item> ControlsMenu()
        {
            return new List<Item>
            {
                Toggle(() => T("Controller-Kürzel", "Controller shortcuts"), ModConfig.PadShortcuts,
                    () => T("Mod-Funktionen über die unten gewählten Controller-Tasten.", "Mod functions on the controller buttons chosen below.")),
                PadItem(() => T("Taste für Status, halten öffnet dieses Menü", "Button for status, hold opens this menu"), ModConfig.PadStatusButton,
                    () => T("Kurz: im Kampf Kampfstatus, sonst Details. Halten: Mod-Menü.", "Tap: match status in a fight, otherwise details. Hold: mod menu.") + GameUseNote()),
                PadItem(() => T("Taste für Gegnerposition", "Button for opponent position"), ModConfig.PadEnemyButton,
                    () => T("Im Kampf: Seite, Entfernung, Luft oder Boden. Sonst: letzte Ansage wiederholen.", "In a fight: side, distance, air or ground. Otherwise: repeat last announcement.") + GameUseNote()),
                PadItem(() => T("Taste für Wiederholen", "Button for repeat"), ModConfig.PadRepeatButton,
                    () => T("Wiederholt die letzte Ansage.", "Repeats the last announcement.") + GameUseNote()),
                PadItem(() => T("Taste zum Unterbrechen der Sprache", "Button to stop speech"), ModConfig.PadStopButton,
                    () => T("Bricht die laufende Ansage ab, wie Strg auf der Tastatur.", "Stops the current announcement, like Control on the keyboard.") + GameUseNote()),
                Action(() => T("Tastenbelegung anhören", "Hear key bindings"), () => Hotkeys.SpeakHelp()),
                Back()
            };
        }

        private static List<Item> AdvancedMenu()
        {
            return new List<Item>
            {
                Toggle(() => T("Dieses Menü bei jedem Spielstart öffnen", "Open this menu at every game start"), ModConfig.AskOnStartup),
                Toggle(() => T("Ausführliches Protokoll", "Detailed log"), ModConfig.LogSpeech,
                    () => T("Schreibt jede Ansage und Zusatzdaten ins Protokoll. Hilft bei Fehlerberichten mit dem Datensammler.", "Writes every announcement and extra data to the log. Helps with bug reports via the data collector.")),
                Back()
            };
        }

        // ---------------- Aktionen ----------------

        private static void SetLanguage(int v)
        {
            ModConfig.ModLanguage.Value = v;
            Sapi32.RefreshVoices();
            // Menü in der neuen Sprache neu aufbauen
            foreach (var l in stack) { l.Items = l.Build(); l.Index = Math.Min(l.Index, l.Items.Count - 1); }
        }

        private static void CycleVoice(int d)
        {
            if (!ModConfig.SpeechViaSapi.Value) { Speech.Say(T("Die Stimme gibt es nur bei SAPI-Ausgabe.", "Voice selection is only available with SAPI output.")); return; }
            int primary = Sapi32.Primary(Loc.ModVoiceCodes);
            var list = new List<Sapi32.VoiceInfo>();
            foreach (var v in Sapi32.Voices) if (v.Primary == primary) list.Add(v);
            if (list.Count == 0)
            {
                Speech.Say(T("Für diese Sprache ist keine 32-Bit-Stimme installiert.", "No 32-bit voice is installed for this language."));
                return;
            }
            string cur = Sapi32.CurrentVoiceName;
            int i = list.FindIndex(v => v.Name == cur);
            i = i < 0 ? 0 : (i + d + list.Count) % list.Count;
            ModConfig.AutoVoice.Value = true;
            Sapi32.SetPreferred(primary, list[i].Name);
            // Die neue Stimme meldet sich selbst, sobald der Sprachhelfer umgeschaltet hat
            Scheduler.In(0.35f, () => { if (IsOpen) Speech.Say(Sapi32.CurrentVoice); });
        }

        private static void OpenUrl(string url, string what)
        {
            try
            {
                var psi = new ProcessStartInfo(url, "");
                psi.GetType().GetProperty("UseShellExecute").SetValue(psi, true, null);
                Process.Start(psi);
                Speech.Say(T("Öffne ", "Opening ") + what + T(" im Browser. Mit Alt und Tab kommst du zurück ins Spiel.", " in your browser. Use Alt and Tab to return to the game."));
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Browser konnte nicht geöffnet werden: " + e.Message);
                Speech.Say(T("Der Browser ließ sich nicht öffnen. Die Adresse steht in der Anleitung.", "The browser could not be opened. The address is in the readme."));
            }
        }

        // ---------------- Öffnen, Schließen, Navigation ----------------

        private static Level Top => stack.Count > 0 ? stack[stack.Count - 1] : null;

        public static void Open(bool setup)
        {
            if (IsOpen) return;
            snapshot = new Dictionary<ConfigDefinition, object>();
            try { foreach (var kv in ModConfig.File) snapshot[kv.Key] = kv.Value.BoxedValue; } catch (Exception e) { Plugin.Log.LogWarning("Snapshot: " + e.Message); }
            stack.Clear();
            stack.Add(new Level { Build = MainMenu, Items = MainMenu(), Index = 0, Title = () => T("Mod-Menü", "Mod menu") });
            IsOpen = true;
            OpenedAt = UnityEngine.Time.realtimeSinceStartup;
            Samples.Cancel();
            Audio.MenuOpen();
            string intro = setup
                ? T("Idol Showdown Access " + Plugin.Version + ", Mod-Menü. Hoch und Runter wählen, Links, Rechts oder Bestätigen ändern, Zurück geht eine Ebene zurück, F11 speichert. Y oder F1 erklärt den Eintrag. ",
                    "Idol Showdown Access " + Plugin.Version + ", mod menu. Up and down select, left, right or confirm change, back goes up one level, F11 saves. Y or F1 explains the entry. ")
                : T("Mod-Menü. ", "Mod menu. ");
            Speech.Say(intro + Describe(Top, true));
            PreviewTone();
        }

        /// <summary>Schließen. save=false stellt alle Werte auf den Stand beim Öffnen zurück.</summary>
        public static void Close(bool save)
        {
            if (!IsOpen) return;
            if (!save && snapshot != null)
            {
                try
                {
                    foreach (var kv in snapshot) ModConfig.File[kv.Key].BoxedValue = kv.Value;
                }
                catch (Exception e) { Plugin.Log.LogWarning("Verwerfen: " + e.Message); }
                Speech.ApplyOutputMode();
                Sapi32.ApplySettings();
            }
            IsOpen = false;
            stack.Clear();
            Samples.Cancel();
            ClosedAt = UnityEngine.Time.realtimeSinceStartup;
            ModConfig.MenuVersion.Value = ModConfig.CurrentMenuVersion;
            ModConfig.Save();
            Audio.MasterVolume = ModConfig.CueVolume.Value / 100f;
            Audio.MenuClose();
            string msg = save ? T("Gespeichert. Mod-Menü geschlossen.", "Saved. Mod menu closed.") : T("Verworfen. Mod-Menü geschlossen.", "Discarded. Mod menu closed.");
            if (!ModConfig.ModEnabled.Value) msg += T(" Der Mod ist ausgeschaltet. F11 oder Ansicht halten öffnet das Menü wieder.", " The mod is turned off. F11 or holding View opens the menu again.");
            Speech.Say(msg);
        }

        private static string Describe(Level l, bool withPos)
        {
            var it = l.Items[l.Index];
            string v = it.Value();
            string s = it.Label() + (string.IsNullOrEmpty(v) ? "" : ": " + v);
            if (withPos) s += ", " + (l.Index + 1) + T(" von ", " of ") + l.Items.Count;
            return s;
        }

        private static void PreviewTone()
        {
            var l = Top;
            if (l == null) return;
            var it = l.Items[l.Index];
            if (it.Sample != null && ModConfig.MenuTonePreview.Value) Samples.PreviewAfterSpeech(it.Sample);
            else Samples.Cancel();
        }

        private static void Move(int d)
        {
            var l = Top;
            l.Index = (l.Index + d + l.Items.Count) % l.Items.Count;
            Audio.Click();
            Speech.Say(Describe(l, true));
            PreviewTone();
        }

        private static void Change(int d)
        {
            var l = Top;
            var it = l.Items[l.Index];
            if (it.Submenu != null) { if (d > 0) Enter(it); return; }
            it.Change?.Invoke(d);
            if (IsOpen && !it.NoEcho) Speech.Say(Describe(l, false));
            if (it.Sample != null && ModConfig.MenuTonePreview.Value) Samples.PreviewAfterSpeech(it.Sample);
        }

        private static void Activate()
        {
            var l = Top;
            var it = l.Items[l.Index];
            if (it.Submenu != null) { Enter(it); return; }
            it.Activate?.Invoke();
            if (IsOpen && !it.NoEcho) Speech.Say(Describe(l, false));
        }

        private static void Enter(Item it)
        {
            var lvl = new Level { Build = it.Submenu, Items = it.Submenu(), Index = 0, Title = it.Label };
            stack.Add(lvl);
            Audio.MenuOpen();
            Speech.Say(it.Label() + ". " + Describe(lvl, true));
            PreviewTone();
        }

        private static void GoBack()
        {
            if (stack.Count <= 1) { Close(false); return; }
            stack.RemoveAt(stack.Count - 1);
            Audio.MenuClose();
            Speech.Say(Describe(Top, true));
            PreviewTone();
        }

        public static void First() { if (IsOpen) { Top.Index = 0; Speech.Say(Describe(Top, true)); PreviewTone(); } }
        public static void Last() { if (IsOpen) { Top.Index = Top.Items.Count - 1; Speech.Say(Describe(Top, true)); PreviewTone(); } }

        public static void Hint()
        {
            if (!IsOpen) return;
            var it = Top.Items[Top.Index];
            string h = it.Hint != null ? it.Hint() : null;
            Speech.Say(string.IsNullOrEmpty(h) ? T("Keine weitere Beschreibung.", "No further description.") : h);
        }

        // ---- Eingabe-Routing ----
        public enum Act { Up, Down, Left, Right, Ok, Back, Info, Save }

        private static readonly float[] lastActTime = new float[10];

        /// <summary>Verarbeitet eine Aktion; doppelte Auslösung (Tastatur + Spiel-UI) wird gefiltert.</summary>
        public static void Handle(Act a)
        {
            if (!IsOpen) return;
            float now = UnityEngine.Time.realtimeSinceStartup;
            // Direkt nach dem Öffnen keine "durchgereichten" Eingaben annehmen.
            if (now - OpenedAt < 0.25f) return;
            int k = (int)a;
            if (now - lastActTime[k] < 0.12f) return;
            lastActTime[k] = now;
            switch (a)
            {
                case Act.Up: Move(-1); break;
                case Act.Down: Move(1); break;
                case Act.Left: Change(-1); break;
                case Act.Right: Change(1); break;
                case Act.Ok: Activate(); break;
                case Act.Back: GoBack(); break;
                case Act.Info: Hint(); break;
                case Act.Save: Close(true); break;
            }
        }
    }
}
