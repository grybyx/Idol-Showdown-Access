using System;
using IdolShowdown;
using IdolShowdown.Managers;
using IdolShowdown.UI;
using UnityEngine;

namespace IdolShowdownAccess
{
    /// <summary>
    /// Liest Einblendungen vor: Dialoge/Sprechblasen, Hinweise, Popups,
    /// Tutorial-Aufgaben, Kampf-Banner und Siegtexte. (Superchat-/Chat-Einblendungen bewusst nicht.)
    /// </summary>
    internal static class TextHooks
    {
        public static void ApplyPatches()
        {
            var h = typeof(TextHooks);
            Patcher.Patch(typeof(SPDialogueWriter), "Run", h, prefix: nameof(DialoguePre));
            Patcher.Patch(typeof(GameNotification), "InitMe", h, postfix: nameof(NotificationPost));
            Patcher.Patch(typeof(NullBaseMenu), "SpawnToastCr", h, prefix: nameof(ToastPre));
            Patcher.Patch(typeof(GameplayUIVictoryScreen), "OnGameEnd", h, postfix: nameof(VictoryPost));
            Patcher.Patch(typeof(GameplayUITutorial), "SuccessInput", h, postfix: nameof(TutorialSuccessPost));
            Patcher.Patch(typeof(GameplayUITutorial), "FailedInput", h, postfix: nameof(TutorialFailPost));
            Patcher.Patch(typeof(GameplayUITutorial), "PlaySuccessRepeatsProgress", h, postfix: nameof(TutorialProgressPost));
        }

        private static bool On => ModConfig.On(ModConfig.TextOverlays);

        private static void DialoguePre(string __0)
        {
            if (!On) return;
            Speech.Say(Speech.G(__0));
        }

        private static void NotificationPost(string __0)
        {
            if (!On) return;
            Speech.Queue(Speech.G(__0));
        }

        private static void ToastPre(string __0)
        {
            if (!On) return;
            Speech.Queue(Speech.G(__0));
        }

        private static void VictoryPost(GameplayUIVictoryScreen __instance)
        {
            if (!On) return;
            Scheduler.In(2.0f, () =>
            {
                if (__instance == null || !__instance.isActiveAndEnabled) return;
                string name = TextUtil.Tmp(R.Get<TMPro.TMP_Text>(__instance, "charName"));
                string win = TextUtil.Tmp(R.Get<TMPro.TMP_Text>(__instance, "winText"));
                string s = (name + " " + win).Trim();
                if (s.Length > 0) Speech.Queue(Speech.G(s));
            });
        }

        private static void TutorialSuccessPost()
        {
            if (!On) return;
            Audio.Tone(660, 0.08f, 0, 0.45f, Audio.Wave.Triangle);
            Audio.Tone(990, 0.12f, 0, 0.45f, Audio.Wave.Triangle, -1, 0.08f);
        }

        private static void TutorialFailPost()
        {
            if (!On) return;
            Audio.Tone(300, 0.15f, 0, 0.4f, Audio.Wave.Square, 200);
        }

        private static void TutorialProgressPost(int __0)
        {
            if (!On) return;
            Speech.Queue(__0.ToString());
        }

        // ---------------- Tutorial-Texte beobachten ----------------

        private static GameplayUITutorial tutorial;
        private static float nextSearch;
        private static string lastTutorialText = "";
        private static string lastProceed = "";

        public static void Update()
        {
            if (!On) return;
            float now = Time.realtimeSinceStartup;
            if (tutorial == null)
            {
                if (now < nextSearch) return;
                nextSearch = now + 2f;
                var gm = GlobalManager.Instance != null ? GlobalManager.Instance.GameManager : null;
                if (gm == null) return;
                try { if (!gm.IsGameMode(GameMode.tutorial)) return; } catch { return; }
                tutorial = UnityEngine.Object.FindObjectOfType<GameplayUITutorial>();
                if (tutorial == null) return;
            }
            if (!tutorial.isActiveAndEnabled) return;
            try
            {
                string label = TextUtil.Tmp(R.Get<TMPro.TMP_Text>(tutorial, "tutorialLabel"));
                string desc = TextUtil.Tmp(R.Get<TMPro.TMP_Text>(tutorial, "tutorialDescription"));
                string s = (label + (label.Length > 0 && desc.Length > 0 ? ". " : "") + desc).Trim();
                if (s.Length > 0 && s != lastTutorialText)
                {
                    lastTutorialText = s;
                    Speech.Say("Tutorial: " + Speech.G(s));
                }
                var proceed = R.Get<GameObject>(tutorial, "proceedPanel");
                string p = proceed != null && proceed.activeInHierarchy ? "1" : "";
                if (p != lastProceed)
                {
                    lastProceed = p;
                    if (p.Length > 0) Speech.Queue(Loc.T("Lektion geschafft.", "Lesson complete."));
                }
            }
            catch { }
        }

        public static void RepeatTutorial()
        {
            if (!string.IsNullOrEmpty(lastTutorialText)) Speech.Say(Speech.G(lastTutorialText));
            else Speech.RepeatLast();
        }

        public static bool TutorialActive => tutorial != null && tutorial.isActiveAndEnabled;
    }
}
