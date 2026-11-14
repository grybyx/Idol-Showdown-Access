using System;
using IdolShowdown.Managers;
using UnityEngine;

namespace IdolShowdownAccess
{
    /// <summary>
    /// Audio-Ducking: Solange die Sprachausgabe spricht, wird die Gesamtlautstärke
    /// des Spiels (Wwise-Master) abgesenkt und danach weich wieder angehoben.
    /// Die Mod-Töne (Ortung, Herzschlag) laufen getrennt und bleiben unverändert.
    /// Die Spieleinstellung selbst wird dabei nie gespeichert oder verändert.
    /// </summary>
    internal static class Ducking
    {
        private static float level = 1f;       // aktueller Faktor (1 = volle Lautstärke)
        private static float applied = -1f;    // zuletzt an Wwise gesendeter Faktor
        private static float lastFrame;
        private static bool warned;

        public static void Update()
        {
            float now = Time.realtimeSinceStartup;
            float dt = Mathf.Clamp(now - lastFrame, 0f, 0.1f);
            lastFrame = now;

            bool want = ModConfig.ModEnabled.Value &&
                ((ModConfig.Ducking.Value && Speech.IsSpeaking) || (ModConfig.DuckTones.Value && Audio.Busy));
            float target = want ? ModConfig.DuckLevel.Value / 100f : 1f;
            // schnell absenken (ca. 0,1 s), langsam zurück (ca. 0,4 s)
            float speed = target < level ? 1f / 0.1f : 1f / 0.4f;
            level = Mathf.MoveTowards(level, target, speed * dt);

            if (applied >= 0f && Mathf.Abs(level - applied) < 0.01f && !(level == 1f && applied != 1f)) return;
            if (applied < 0f && level >= 1f) return; // noch nie geduckt: nichts anfassen
            Apply(level);
        }

        private static void Apply(float factor)
        {
            try
            {
                var gm = GlobalManager.Instance;
                var am = gm != null ? gm.AudioManager : null;
                if (am == null || am.masterRTPC == null) return;
                am.masterRTPC.SetGlobalValue(am.MasterVolume * factor);
                applied = factor;
            }
            catch (Exception e)
            {
                if (!warned) { warned = true; Plugin.Log.LogWarning("Ducking nicht möglich: " + e.Message); }
            }
        }

        /// <summary>Beim Beenden/Abschalten die Originallautstärke wiederherstellen.</summary>
        public static void Restore()
        {
            level = 1f;
            if (applied >= 0f && applied != 1f) Apply(1f);
        }
    }
}
