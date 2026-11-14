using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdolShowdownAccess
{
    /// <summary>
    /// Alle Töne des Mods an einer Stelle: Name, Erklärung und Abspielen. Tontest und
    /// Mod-Menü spielen sie von hier, damit Spiel und Vorführung nie auseinanderlaufen.
    /// </summary>
    internal static class Samples
    {
        public sealed class Sample
        {
            public string Id;
            public Func<string> Explain;
            public Action Play;
            public float Length;
        }

        private static string T(string de, string en) => Loc.T(de, en);

        private static void Repeat(int n, float gap, Action a)
        {
            for (int i = 0; i < n; i++) Scheduler.In(i * gap, a);
        }

        public static readonly List<Sample> All = new List<Sample>
        {
            new Sample { Id = "enemy_far", Length = 1.4f,
                Explain = () => T("Gegnerortung: Gegner links, weit weg. Runder Plopp, langsam.", "Opponent locator: opponent left, far away. Round plop, slow."),
                Play = () => Repeat(3, 0.6f, () => Audio.EnemyGround(-0.95f)) },
            new Sample { Id = "enemy_near", Length = 1.0f,
                Explain = () => T("Gegner rechts, nah. Je näher, desto schneller.", "Opponent right, close. The closer, the faster."),
                Play = () => Repeat(7, 0.14f, () => Audio.EnemyGround(0.6f)) },
            new Sample { Id = "enemy_air", Length = 1.0f,
                Explain = () => T("Gegner in der Luft, links. Heller, nach oben gleitender Klang.", "Opponent in the air, left. Bright rising sound."),
                Play = () => Repeat(3, 0.35f, () => Audio.EnemyAir(-0.7f)) },
            new Sample { Id = "enemy_close", Length = 0.8f,
                Explain = () => T("Gegner direkt bei dir. Raues Schnarren aus der Mitte.", "Opponent right next to you. Rough buzz from the center."),
                Play = () => Repeat(4, 0.14f, () => Audio.EnemyClose()) },
            new Sample { Id = "side_switch", Length = 0.5f,
                Explain = () => T("Seitenwechsel: Rauschen wandert zur neuen Seite des Gegners.", "Side switch: noise moves to the opponent's new side."),
                Play = () => Audio.SideSwitch(1) },
            new Sample { Id = "wall", Length = 0.5f,
                Explain = () => T("Ecke: zweimal dumpfes Klopfen auf der Seite der Wand.", "Corner: two dull knocks on the side of the wall."),
                Play = () => Audio.WallKnock(-0.9f) },
            new Sample { Id = "projectile", Length = 0.4f,
                Explain = () => T("Geschoss: zwei harte, kurze Töne von der Seite des Gegners, wenn er etwas abfeuert.", "Projectile: two hard short beeps from the opponent's side when they fire something."),
                Play = () => Audio.ProjectileWarn(0.8f) },
            new Sample { Id = "invul", Length = 0.9f,
                Explain = () => T("Du bist unverwundbar: zwei helle Glockentöne. Ende: leiser Ton abwärts.", "You are invincible: two bright chimes. End: soft falling tone."),
                Play = () => { Audio.InvulStart(); Scheduler.In(0.6f, Audio.InvulEnd); } },
            new Sample { Id = "armor", Length = 0.4f,
                Explain = () => T("Super-Rüstung: metallisches Kling.", "Super armor: metallic clink."),
                Play = () => Audio.ArmorStart() },
            new Sample { Id = "bighit", Length = 0.6f,
                Explain = () => T("Großer Schaden in einer Trefferserie: tiefer, abfallender Einschlag.", "Big damage in one combo: deep falling impact."),
                Play = () => Audio.BigHit() },
            new Sample { Id = "heartbeat", Length = 1.6f,
                Explain = () => T("Kritische Gesundheit: Herzschlag.", "Critical health: heartbeat."),
                Play = () => Repeat(2, 0.85f, () => Audio.Heartbeat(0.6f)) },
        };

        public static Sample Get(string id)
        {
            foreach (var s in All) if (s.Id == id) return s;
            return null;
        }

        // ---- Vorspielen nach der Ansage ----
        private static Sample pending;
        private static float pendingSince;

        /// <summary>Ton spielen, sobald die laufende Ansage zu Ende ist (spätestens nach 6 s).</summary>
        public static void PreviewAfterSpeech(string id)
        {
            pending = Get(id);
            pendingSince = Time.realtimeSinceStartup;
        }

        // ---- Alle Töne vorspielen ----
        private static int demoIndex = -1, demoPhase;
        private static float demoT;

        public static bool DemoRunning => demoIndex >= 0;

        public static void PlayAll()
        {
            Cancel();
            demoIndex = 0;
            demoPhase = 0;
        }

        public static void Cancel()
        {
            pending = null;
            demoIndex = -1;
        }

        public static void Update()
        {
            float now = Time.realtimeSinceStartup;
            if (pending != null && now - pendingSince > 0.3f && (!Speech.IsSpeaking || now - pendingSince > 6f))
            {
                pending.Play();
                pending = null;
            }
            if (demoIndex < 0) return;
            if (demoIndex >= All.Count) { demoIndex = -1; return; }
            var s = All[demoIndex];
            switch (demoPhase)
            {
                case 0:
                    Speech.Say(s.Explain());
                    demoT = now; demoPhase = 1;
                    break;
                case 1:
                    if (now - demoT > 0.3f && (!Speech.IsSpeaking || now - demoT > 8f))
                    {
                        s.Play();
                        demoT = now; demoPhase = 2;
                    }
                    break;
                case 2:
                    if (now - demoT > s.Length + 0.5f) { demoIndex++; demoPhase = 0; }
                    break;
            }
        }
    }
}
