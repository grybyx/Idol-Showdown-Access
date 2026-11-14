using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace IdolShowdownAccess
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "de.access.idolshowdown";
        public const string Name = "Idol Showdown Access";
        public const string Version = "1.1.0";

        internal static ManualLogSource Log;
        internal static string Dir;

        private void Awake()
        {
            Log = Logger;
            Dir = Path.GetDirectoryName(Info.Location);
            FileLog.Start(Dir, Logger.SourceName, Version);
            ModConfig.Bind(Config);
            Log.LogInfo("Idol Showdown Access " + Version + " startet. Einstellungen: " + Config.ConfigFilePath + (ModConfig.FirstRun ? " (erster Start)" : ""));

            Speech.Init(Dir);
            Audio.MasterVolume = ModConfig.CueVolume.Value / 100f;
            Audio.Init();

            Patcher.H = new Harmony(Guid);
            MenuReader.ApplyPatches();
            CharSelect.ApplyPatches();
            FightMonitor.ApplyPatches();
            TextHooks.ApplyPatches();
            Log.LogInfo($"Patches: {Patcher.Ok} ok, {Patcher.Failed} fehlgeschlagen.");

            var go = new GameObject("IdolShowdownAccessRunner");
            go.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(go);
            go.AddComponent<Runner>();

            // Versionsansage beim Start: so fällt sofort auf, wenn eine alte Fassung läuft
            Speech.Say(ModConfig.ModEnabled.Value
                ? Loc.T("Idol Showdown Access " + Version + " geladen.", "Idol Showdown Access " + Version + " loaded.")
                : Loc.T("Idol Showdown Access " + Version + " ist ausgeschaltet. F8 schaltet ihn ein.", "Idol Showdown Access " + Version + " is turned off. Press F8 to turn it on."));
        }
    }

    internal class Runner : MonoBehaviour
    {
        private bool startupHandled;

        private static void Safe(string what, Action a)
        {
            try { a(); } catch (Exception e) { Plugin.Log.LogWarning(what + ": " + e.Message); }
        }

        private void Update()
        {
            Safe("Scheduler", Scheduler.Update);
            Safe("Tasten", Hotkeys.Update);
            Safe("Töne", Samples.Update);
            Safe("Startabfrage", StartupPrompt);
            Safe("Ducking", Ducking.Update);
            Safe("Stimmen", Sapi32.Update);
            Safe("Sprache", WatchLanguage);
            if (!ModConfig.ModEnabled.Value) return;
            Safe("Menüleser", MenuReader.Update);
            Safe("Kampf", FightMonitor.Update);
            Safe("Texte", TextHooks.Update);
        }

        /// <summary>
        /// Mod-Menü, sobald das erste Spielmenü erscheint: beim ersten Start, nach einem Update
        /// mit neuen Einträgen (MenuVersion) oder wenn der Spieler es bei jedem Start will.
        /// </summary>
        private void StartupPrompt()
        {
            if (startupHandled || !MenuReader.AnyMenuSeen) return;
            if (Time.realtimeSinceStartup - MenuReader.FirstMenuSeenAt < 1.2f) return;
            startupHandled = true;
            bool newEntries = ModConfig.MenuVersion.Value < ModConfig.CurrentMenuVersion;
            if (ModConfig.AskOnStartup.Value || newEntries || ModConfig.FirstRun)
            {
                ModMenu.Open(true);
                if (newEntries && !ModConfig.FirstRun)
                    Speech.Queue(Loc.T("Das Menü hat neue Einträge.", "The menu has new entries."));
            }
            else if (ModConfig.ModEnabled.Value) Speech.Queue(Loc.T("F1 oder Ansicht-Taste für Hilfe.", "F1 or View button for help."));
        }

        private string appliedLocale, candidateLocale;
        private float candidateSince, nextLocaleCheck;
        private bool firstLocale = true;

        /// <summary>
        /// Spielsprache beobachten. Ein neuer Wert gilt erst, wenn er 1,2 Sekunden stabil ist.
        /// Wechsel werden angesagt, außer in den ersten Sekunden nach dem Start.
        /// </summary>
        private void WatchLanguage()
        {
            float now = Time.realtimeSinceStartup;
            if (now < nextLocaleCheck) return;
            nextLocaleCheck = now + 0.3f;
            string loc = IdolShowdown.Localization.Localization.Locale;
            if (string.IsNullOrEmpty(loc)) return;
            if (loc != candidateLocale) { candidateLocale = loc; candidateSince = now; return; }
            if (loc == appliedLocale || now - candidateSince < 1.2f) return;
            appliedLocale = loc;
            Sapi32.SetGameLocale(loc);
            // Die erste erkannte Sprache beim Start nicht ansagen
            if (firstLocale) { firstLocale = false; if (now < 20f) return; }
            // Ansage in der neuen Sprache, nachdem der Sprachhelfer die Stimme gewechselt hat
            Scheduler.In(0.6f, () =>
            {
                int primary = Sapi32.Primary(Loc.ModVoiceCodes);
                string lang = Loc.T("Deutsch", "English");
                string s = Loc.T("Spielsprache gewechselt. Ansagen jetzt auf ", "Game language changed. Announcements now in ") + lang + ".";
                if (ModConfig.SpeechViaSapi.Value && Sapi32.Ready && !Sapi32.HasVoiceFor(primary))
                    s += Loc.T(" Für Deutsch ist keine 32-Bit-Stimme installiert, es bleibt die bisherige Stimme.", " No 32-bit voice is installed for English, the previous voice stays.");
                Speech.Say(s);
            });
        }

        private void OnApplicationQuit()
        {
            Ducking.Restore();
            Audio.Shutdown();
            Speech.Shutdown();
        }
    }
}
