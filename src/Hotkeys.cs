using UnityEngine;

namespace IdolShowdownAccess
{
    /// <summary>
    /// Tastatur- und Controller-Kürzel des Mods.
    /// Strg (allein) unterbricht immer die Sprache, auch im Mod-Menü und mit JAWS.
    /// F1 Hilfe, F2 Details, F5 Kampfstatus, F6 Gegnerposition, F7 Ortungston an/aus,
    /// F8 Mod an/aus, F9 Wiederholen, F10 Sprache stoppen, F11 Mod-Menü.
    /// </summary>
    internal static class Hotkeys
    {
        private static float statusDownAt = -1f;
        private static bool statusHoldFired;

        private static string T(string de, string en) => Loc.T(de, en);

        public static void SpeakHelp()
        {
            Speech.Say(T(
                "Tastatur: Strg unterbricht die Sprache. F1 Hilfe. F2 Details zum Menüpunkt oder zur Figur. F5 Kampfstatus. F6 Gegnerposition. " +
                "F7 Ortungston an oder aus. F8 Mod an oder aus. F9 letzte Ansage wiederholen. F10 Sprache stoppen. F11 Mod-Menü. " +
                "Controller: Ansicht kurz: im Kampf Status, sonst Details. Ansicht halten: Mod-Menü. " +
                "Linken Stick drücken: Gegnerposition. Rechten Stick drücken: Wiederholen. Die Tasten lassen sich im Mod-Menü unter Steuerung ändern. " +
                "Im Mod-Menü: Hoch und Runter wählen, Links, Rechts oder A ändern, B zurück, Y erklärt, F11 speichert.",
                "Keyboard: Control stops speech. F1 help. F2 details about the menu item or character. F5 match status. F6 opponent position. " +
                "F7 locator sound on or off. F8 mod on or off. F9 repeat last announcement. F10 stop speech. F11 mod menu. " +
                "Controller: tap View: match status in a fight, otherwise details. Hold View: mod menu. " +
                "Press left stick: opponent position. Press right stick: repeat. You can change these buttons in the mod menu under Controls. " +
                "In the mod menu: up and down select, left, right or A change, B goes back, Y explains, F11 saves."));
        }

        private static bool K(int vk) => Keys.Pressed(vk);

        private static bool Pad(int n, bool down = true)
        {
            if (n < 0 || n > 19) return false;
            var k = (KeyCode)((int)KeyCode.JoystickButton0 + n);
            try { return down ? Input.GetKeyDown(k) : Input.GetKey(k); } catch { return false; }
        }

        private static bool PadUp(int n)
        {
            if (n < 0 || n > 19) return false;
            var k = (KeyCode)((int)KeyCode.JoystickButton0 + n);
            try { return Input.GetKeyUp(k); } catch { return false; }
        }

        public static void Update()
        {
            Keys.Poll();
            float now = Time.realtimeSinceStartup;

            // ---- Sprache stoppen: immer, überall ----
            bool stop = Keys.CtrlAlonePressed || K(Keys.F(10)) || (ModConfig.PadShortcuts.Value && Pad(ModConfig.PadStopButton.Value));
            if (stop)
            {
                Speech.Silence();
                Samples.Cancel();
            }

            // ---- Mod-Menü offen: eigene Navigation ----
            if (ModMenu.IsOpen)
            {
                if (K(Keys.VK_UP)) ModMenu.Handle(ModMenu.Act.Up);
                if (K(Keys.VK_DOWN)) ModMenu.Handle(ModMenu.Act.Down);
                if (K(Keys.VK_LEFT)) ModMenu.Handle(ModMenu.Act.Left);
                if (K(Keys.VK_RIGHT)) ModMenu.Handle(ModMenu.Act.Right);
                if (K(Keys.VK_RETURN) || K(Keys.VK_SPACE)) ModMenu.Handle(ModMenu.Act.Ok);
                if (K(Keys.VK_ESCAPE) || K(Keys.VK_BACK)) ModMenu.Handle(ModMenu.Act.Back);
                if (K(Keys.F(11))) ModMenu.Handle(ModMenu.Act.Save);
                if (K(Keys.VK_HOME)) ModMenu.First();
                if (K(Keys.VK_END)) ModMenu.Last();
                if (K(Keys.F(1))) ModMenu.Hint();
                if (K(Keys.F(9))) Speech.RepeatLast();
                // Controller: A, B, X (verringern), Y (erklären), LB/RB blättern
                if (Pad(0)) ModMenu.Handle(ModMenu.Act.Ok);
                if (Pad(1)) ModMenu.Handle(ModMenu.Act.Back);
                if (Pad(2)) ModMenu.Handle(ModMenu.Act.Left);
                if (Pad(3)) ModMenu.Handle(ModMenu.Act.Info);
                if (Pad(4)) ModMenu.Handle(ModMenu.Act.Up);
                if (Pad(5)) ModMenu.Handle(ModMenu.Act.Down);
                HandleStatusButton(now, true);
                return;
            }

            // ---- Tastatur ----
            if (K(Keys.F(11))) { TryOpenMenu(); return; }
            if (K(Keys.F(8)))
            {
                ModConfig.ModEnabled.Value = !ModConfig.ModEnabled.Value;
                ModConfig.Save();
                if (!ModConfig.ModEnabled.Value) Audio.StopAll();
                Speech.Say(ModConfig.ModEnabled.Value ? T("Barrierefreiheits-Mod an", "Accessibility mod on") : T("Barrierefreiheits-Mod aus", "Accessibility mod off"));
                return;
            }
            if (K(Keys.F(9))) Speech.RepeatLast();
            if (!ModConfig.ModEnabled.Value) { HandleStatusButton(now, false); return; }

            if (K(Keys.F(1))) SpeakHelp();
            if (K(Keys.F(2))) Details();
            if (K(Keys.F(5))) Status();
            if (K(Keys.F(6))) FightMonitor.SpeakEnemyPosition();
            if (K(Keys.F(7)))
            {
                ModConfig.EnemyLocator.Value = !ModConfig.EnemyLocator.Value;
                ModConfig.Save();
                Speech.Say(ModConfig.EnemyLocator.Value ? T("Ortungston an", "Locator sound on") : T("Ortungston aus", "Locator sound off"));
            }

            // ---- Controller ----
            if (ModConfig.PadShortcuts.Value)
            {
                HandleStatusButton(now, false);
                if (Pad(ModConfig.PadEnemyButton.Value))
                {
                    if (FightMonitor.InFight) FightMonitor.SpeakEnemyPosition();
                    else Speech.RepeatLast();
                }
                if (Pad(ModConfig.PadRepeatButton.Value)) Speech.RepeatLast();
            }
        }

        /// <summary>Status-Taste (Standard Ansicht): kurz = Status/Details, gehalten = Mod-Menü.</summary>
        private static void HandleStatusButton(float now, bool menuOpen)
        {
            if (!ModConfig.PadShortcuts.Value) return;
            int b = ModConfig.PadStatusButton.Value;
            if (b < 0) return;
            if (Pad(b)) { statusDownAt = now; statusHoldFired = false; }
            if (statusDownAt > 0 && !statusHoldFired && Pad(b, false) && now - statusDownAt > 0.65f)
            {
                statusHoldFired = true;
                if (menuOpen) ModMenu.Handle(ModMenu.Act.Save); else TryOpenMenu();
            }
            if (PadUp(b))
            {
                bool shortPress = statusDownAt > 0 && !statusHoldFired && now - statusDownAt < 0.65f;
                statusDownAt = -1f;
                if (shortPress && !menuOpen && ModConfig.ModEnabled.Value)
                {
                    if (FightMonitor.InFight) Status(); else Details();
                }
            }
        }

        private static void Status()
        {
            if (FightMonitor.InFight) FightMonitor.SpeakStatus();
            else Details();
        }

        private static void Details()
        {
            if (CharSelect.SpeakDetails()) return;
            if (TextHooks.TutorialActive) { TextHooks.RepeatTutorial(); return; }
            MenuReader.SpeakDetails();
        }

        /// <summary>Mod-Menü öffnen. Im laufenden Offline-Kampf pausiert der Mod vorher das Spiel.</summary>
        public static void TryOpenMenu()
        {
            if (FightMonitor.InFight && !FightMonitor.Paused)
            {
                if (!FightMonitor.TryPauseGame())
                {
                    Speech.Say(T("Bitte zuerst das Spiel pausieren, dann das Mod-Menü öffnen.", "Please pause the game first, then open the mod menu."));
                    return;
                }
                Scheduler.In(0.3f, () =>
                {
                    if (FightMonitor.Paused) ModMenu.Open(false);
                    else Speech.Say(T("Das Spiel ließ sich gerade nicht pausieren. Bitte selbst pausieren und dann F11 drücken.", "The game could not be paused right now. Please pause it yourself, then press F11."));
                });
                return;
            }
            ModMenu.Open(false);
        }
    }
}
