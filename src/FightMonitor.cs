using System;
using IdolShowdown;
using IdolShowdown.Managers;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace IdolShowdownAccess
{
    /// <summary>
    /// Kampf: Gegnerortung per Stereo-Ton, Herzschlag bei kritischer Gesundheit,
    /// Ansagen zu Runden, Zeit, Ressourcen und Status.
    /// </summary>
    internal static class FightMonitor
    {
        private static bool inFight;
        private static float nextPing;
        private static int lastSide;          // -1 links, +1 rechts
        private static float nextWallCue;
        private static float nextHeartbeat;
        private static float groundY = float.NaN;
        private static bool roundLive;

        // Schwellen-Merker
        private static bool myHalf, myCrit, myLow, oppHalf, oppCrit;
        private static bool superOne, superFull, burstReady, collabReady, offCollabReady;
        private static int lastTimeWarn = 999;
        private static bool pausedLast;
        private static bool resInit, versusPending;
        private static int p1WinsAtStart, p2WinsAtStart;

        public static void ApplyPatches()
        {
            var h = typeof(FightMonitor);
            Patcher.Patch(typeof(GameStateManager), "OnGameStartInvoke", h, postfix: nameof(GameStartPost));
            Patcher.Patch(typeof(GameStateManager), "OnRoundResetEventInvoke", h, postfix: nameof(RoundResetPost));
            Patcher.Patch(typeof(GameStateManager), "OnRoundEndEventInvoke", h, postfix: nameof(RoundEndPost));
            // Pause-/Menü-Überlagerungen im Kampf erkennen (GameManager.IsPaused reicht nicht überall)
            Patcher.Patch(typeof(PauseMenu), "OnEnable", h, postfix: nameof(OverlayOpened));
            Patcher.Patch(typeof(TrainingPauseMenuFunctions), "OnEnable", h, postfix: nameof(OverlayOpened));
            Patcher.Patch(typeof(IdolShowdown.UI.CommandList.CommandList), "OnEnable", h, postfix: nameof(OverlayOpened));
            Patcher.Patch(typeof(SettingsHelper), "OnEnable", h, postfix: nameof(OverlayOpened));
            // Geschosse: das Spiel gibt jedem Geschoss beim Abfeuern seinen Besitzer
            Patcher.Patch(typeof(Ownership), "SetParent", h, postfix: nameof(ProjectileOwnerSet));
            Patcher.Patch(typeof(Ownership), "SetParentNoParentAction", h, postfix: nameof(ProjectileOwnerSet));
        }

        // ---------------- Geschosse ----------------

        private static float lastProjectileCue;
        private static int lastProjectileId;

        private static void ProjectileOwnerSet(Ownership __instance, GameObject __0)
        {
            try
            {
                if (!ModConfig.On(ModConfig.ProjectileCue) || !inFight || !roundLive || __instance == null || __0 == null) return;
                var gsm = Gsm;
                if (gsm == null || !gsm.CountDownEnded || Paused) return;
                if (!TryGetFighters(out var f) || __0 != f.Opp.charObject) return; // nur Geschosse des Gegners
                int id = __instance.GetInstanceID();
                float now = Time.realtimeSinceStartup;
                if (id == lastProjectileId && now - lastProjectileCue < 1f) return;
                if (now - lastProjectileCue < 0.25f) return;
                lastProjectileId = id; lastProjectileCue = now;
                // Seite: wo das Geschoss entsteht, sonst die Seite des Gegners
                float dx = __instance.transform.position.x - f.Me.charObject.transform.position.x;
                if (Mathf.Abs(dx) < 0.01f) dx = f.Opp.charObject.transform.position.x - f.Me.charObject.transform.position.x;
                float pan = Mathf.Sign(dx) * Mathf.Clamp(0.6f + Mathf.Abs(dx) / Mathf.Max(1f, Range()) * 0.4f, 0.6f, 1f);
                Audio.ProjectileWarn(pan);
                if (ModConfig.LogSpeech.Value) Plugin.Log.LogInfo("Geschoss des Gegners: " + __instance.gameObject.name + (dx > 0 ? " rechts" : " links"));
            }
            catch (Exception e) { Plugin.Log.LogWarning("Geschoss: " + e.Message); }
        }

        /// <summary>Spiel pausieren (nur offline). true, wenn die Pause angefordert wurde.</summary>
        public static bool TryPauseGame()
        {
            try
            {
                var gm = Gm;
                if (gm == null || gm.CurrentGamemodeIsOnline) return false;
                gm.PauseAction();
                return true;
            }
            catch (Exception e) { Plugin.Log.LogWarning("Pausieren: " + e.Message); return false; }
        }

        // ---------------- Zugriff auf Spielzustand ----------------

        private struct Fighters
        {
            public GameCharacterManager.PlayerCharacter Me, Opp;
            public int MyIndex;
        }

        private static GameStateManager Gsm
        {
            get
            {
                var g = GlobalManager.Instance;
                return g != null ? g.GameStateManager : null;
            }
        }

        private static GameManager Gm
        {
            get
            {
                var g = GlobalManager.Instance;
                return g != null ? g.GameManager : null;
            }
        }

        private static IdolShowdown.Match.IdolMatch cachedMatch;
        private static float nextMatchSearch;

        private static int stickyIndex = -1;

        /// <summary>
        /// Welche Seite steuerst du? Zuerst direkt an den Figuren ablesen: die Figur mit aktiver KI
        /// bzw. die Online-Gegnerin ist es nicht. Das Ergebnis wird pro Match gemerkt.
        /// </summary>
        private static int DetectFromFighters(GameCharacterManager.PlayerCharacter p1, GameCharacterManager.PlayerCharacter p2)
        {
            bool h1 = IsLocalHuman(p1), h2 = IsLocalHuman(p2);
            if (h1 && !h2) return 0;
            if (h2 && !h1) return 1;
            return -1; // beide Mensch (lokales Versus) oder beide KI
        }

        private static bool IsLocalHuman(GameCharacterManager.PlayerCharacter pc)
        {
            try
            {
                var cc = pc?.charComponents;
                if (cc == null) return false;
                var ai = cc.AIController;
                if (ai != null && ai.isActiveAndEnabled && ai.AIComponentActivated) return false;
                var input = cc.Input;
                if (input != null && input.IsRemoteControllerPlayer) return false;
                return true;
            }
            catch { return false; }
        }

        /// <summary>0 = du bist Spieler 1, 1 = du bist Spieler 2.</summary>
        public static int LocalIndex()
        {
            if (stickyIndex >= 0) return stickyIndex;
            try
            {
                var gsm = Gsm;
                var cm = gsm != null ? gsm.CharacterManager : null;
                if (cm != null && cm.Player1 != null && cm.Player2 != null)
                {
                    int d = DetectFromFighters(cm.Player1, cm.Player2);
                    if (d >= 0)
                    {
                        stickyIndex = d;
                        Plugin.Log.LogInfo("Eigene Seite erkannt: Spieler " + (d + 1));
                        return d;
                    }
                }
            }
            catch { }
            return LocalIndexFallback();
        }

        private static int LocalIndexFallback()
        {
            var gm = Gm;
            if (gm == null) return 0;
            GameMode mode;
            try { mode = gm.GetGameMode(); } catch { return 0; }
            switch (mode)
            {
                case GameMode.onlinePVP:
                    if (cachedMatch == null && Time.realtimeSinceStartup >= nextMatchSearch)
                    {
                        // Suche nur gelegentlich, um Online-Kämpfe nicht zu belasten
                        nextMatchSearch = Time.realtimeSinceStartup + 2f;
                        cachedMatch = UnityEngine.Object.FindObjectOfType<IdolShowdown.Match.IdolMatch>();
                    }
                    if (cachedMatch != null)
                    {
                        int li = R.Get<int>(cachedMatch, "localPlayerIndex");
                        return li == 1 ? 1 : 0;
                    }
                    return 0;
                case GameMode.localPVP:
                    return ModConfig.LocalPlayerSide.Value == 2 ? 1 : 0;
                case GameMode.aiVai:
                    return 0;
                default:
                    return gm.AIPlayerNumber == PlayerNumber.playerOne ? 1 : 0;
            }
        }

        private static bool TryGetFighters(out Fighters f)
        {
            f = default(Fighters);
            var gsm = Gsm;
            if (gsm == null) return false;
            var cm = gsm.CharacterManager;
            if (cm == null) return false;
            var p1 = cm.Player1;
            var p2 = cm.Player2;
            if (p1 == null || p2 == null || p1.charObject == null || p2.charObject == null) return false;
            if (!p1.charObject.activeInHierarchy || !p2.charObject.activeInHierarchy) return false;
            f.MyIndex = LocalIndex();
            f.Me = f.MyIndex == 0 ? p1 : p2;
            f.Opp = f.MyIndex == 0 ? p2 : p1;
            return true;
        }

        private static string Name(GameCharacterManager.PlayerCharacter pc)
        {
            try
            {
                if (pc?.idol != null && !string.IsNullOrEmpty(pc.idol.charName)) return Speech.Clean(pc.idol.charName);
            }
            catch { }
            return "";
        }

        private static float Hp(GameCharacterManager.PlayerCharacter pc)
        {
            try
            {
                var h = pc?.charComponents?.Health;
                if (h == null) return -1f;
                return Mathf.Clamp01(h.HealthPercent);
            }
            catch { return -1f; }
        }

        private static float Range()
        {
            float cfg = ModConfig.PanRange.Value;
            if (cfg > 0.1f) return cfg;
            try
            {
                var sp = StageProperties.Instance;
                if (sp != null)
                {
                    var l = R.Get<FixedRigidBody>(sp, "leftStageWallRigidBody");
                    var r = R.Get<FixedRigidBody>(sp, "rightStageWallRigidBody");
                    if (l != null && r != null)
                    {
                        float w = Mathf.Abs(r.transform.position.x - l.transform.position.x);
                        if (w > 1f) return Mathf.Clamp(w * 0.4f, 2f, 40f);
                    }
                }
            }
            catch { }
            return 6f;
        }

        private static bool Walls(out float left, out float right)
        {
            left = right = 0;
            try
            {
                var sp = StageProperties.Instance;
                if (sp == null) return false;
                var l = R.Get<FixedRigidBody>(sp, "leftStageWallRigidBody");
                var r = R.Get<FixedRigidBody>(sp, "rightStageWallRigidBody");
                if (l == null || r == null) return false;
                left = l.transform.position.x;
                right = r.transform.position.x;
                return right - left > 1f;
            }
            catch { return false; }
        }

        // ---------------- Pause-Erkennung ----------------

        private static readonly System.Collections.Generic.List<Behaviour> overlays = new System.Collections.Generic.List<Behaviour>();

        private static void OverlayOpened(Behaviour __instance)
        {
            if (__instance != null && !overlays.Contains(__instance)) overlays.Add(__instance);
        }

        /// <summary>true, solange ein Pause-, Trainings-, Optionen- oder Befehlslisten-Menü offen ist.</summary>
        public static bool OverlayOpen()
        {
            for (int i = overlays.Count - 1; i >= 0; i--)
            {
                var b = overlays[i];
                if (b == null) { overlays.RemoveAt(i); continue; }
                if (b.isActiveAndEnabled) return true;
            }
            return false;
        }

        public static bool Paused
        {
            get
            {
                // Maßgeblich ist die Pause des laufenden Matches (MatchRunner/IdolMatch.IsPaused).
                try
                {
                    var g = GlobalManager.Instance;
                    var mr = g != null ? g.MatchRunner : null;
                    if (mr != null && mr.IsPaused) return true;
                }
                catch { }
                var gm = Gm;
                return (gm != null && gm.IsPaused) || OverlayOpen();
            }
        }

        // ---------------- Ereignisse ----------------

        private static void GameStartPost()
        {
            ResetMatchState();
            versusPending = true;
            // Normalfall: "A gegen B" kommt zusammen mit "Runde 1". Ersatz, falls kein Rundenstart folgt.
            Scheduler.In(3f, () => { if (versusPending) { string v = VersusText(); versusPending = false; if (v.Length > 0 && ModConfig.On(ModConfig.FightAnnouncements)) Speech.Say(v); } });
        }

        private static string VersusText()
        {
            if (!TryGetFighters(out var f)) return "";
            string me = Name(f.Me), opp = Name(f.Opp);
            if (me.Length == 0 || opp.Length == 0) return "";
            return Speech.G(me) + Loc.T(" gegen ", " versus ") + Speech.G(opp) + ".";
        }

        private static void RoundResetPost()
        {
            ResetRoundState();
            var g0 = Gsm;
            if (g0 != null) { p1WinsAtStart = g0.Player1WinCount; p2WinsAtStart = g0.Player2WinCount; }
            Scheduler.In(0.4f, () =>
            {
                var gsm = Gsm;
                if (gsm == null) return;
                string v = versusPending ? VersusText() : "";
                versusPending = false;
                if (!ModConfig.On(ModConfig.FightAnnouncements)) return;
                int round = gsm.Player1WinCount + gsm.Player2WinCount + 1;
                Speech.Say((v.Length > 0 ? v + " " : "") + Loc.T("Runde ", "Round ") + round);
            });
        }

        private static void RoundEndPost(PlayerNumber __1, bool __2)
        {
            roundLive = false;
            if (!ModConfig.On(ModConfig.FightAnnouncements)) return;
            int me = LocalIndex();
            bool matchOver = __2;
            float start = Time.realtimeSinceStartup;
            Action check = null;
            check = () =>
            {
                var gsm = Gsm;
                int w1 = gsm != null ? gsm.Player1WinCount : 0, w2 = gsm != null ? gsm.Player2WinCount : 0;
                // Sieger: an den Siegzählern ablesen (zuverlässig), sonst aus der Meldung des Spiels
                PlayerNumber winner = PlayerNumber.playerNone;
                if (w1 > p1WinsAtStart && w2 == p2WinsAtStart) winner = PlayerNumber.playerOne;
                else if (w2 > p2WinsAtStart && w1 == p1WinsAtStart) winner = PlayerNumber.playerTwo;
                else if (__1 != PlayerNumber.playerNone && Time.realtimeSinceStartup - start > 0.6f) winner = __1;
                bool decided = winner != PlayerNumber.playerNone || (w1 > p1WinsAtStart && w2 > p2WinsAtStart);
                if (!decided && Time.realtimeSinceStartup - start < 1.5f) { Scheduler.In(0.15f, check); return; }
                bool won = (winner == PlayerNumber.playerOne && me == 0) || (winner == PlayerNumber.playerTwo && me == 1);
                string s;
                if (winner == PlayerNumber.playerNone)
                    s = w1 > p1WinsAtStart && w2 > p2WinsAtStart ? Loc.T("Unentschieden.", "Draw.") : Loc.T("Runde vorbei.", "Round over.");
                else if (matchOver) s = won ? Loc.T("Sieg! Kampf gewonnen.", "Victory! Match won.") : Loc.T("Niederlage. Kampf verloren.", "Defeat. Match lost.");
                else s = won ? Loc.T("Runde gewonnen.", "Round won.") : Loc.T("Runde verloren.", "Round lost.");
                if (!matchOver && gsm != null)
                {
                    int a2 = me == 0 ? w1 : w2, b2 = me == 0 ? w2 : w1;
                    s += Loc.T(" Stand ", " Score ") + a2 + Loc.T(" zu ", " to ") + b2 + ".";
                }
                if (ModConfig.LogSpeech.Value) Plugin.Log.LogInfo("Rundenende: Spiel meldet " + __1 + ", Siege " + w1 + ":" + w2 + " (vorher " + p1WinsAtStart + ":" + p2WinsAtStart + "), eigene Seite " + (me + 1));
                Speech.Say(s);
            };
            Scheduler.In(0.3f, check);
        }

        private static void ResetMatchState()
        {
            groundY = float.NaN;
            cachedMatch = null;
            nextMatchSearch = 0f;
            stickyIndex = -1;
            ResetRoundState();
        }

        private static void ResetRoundState()
        {
            myHalf = myCrit = myLow = oppHalf = oppCrit = false;
            superOne = superFull = burstReady = collabReady = offCollabReady = false;
            lastTimeWarn = 999;
            lastHpForDamage = -1f; burstDamage = 0f; burstCueDone = false;
            lastInvul = lastArmor = false;
            lastSide = 0;
            roundLive = true;
            resInit = false;
        }

        // ---------------- Laufende Überwachung ----------------

        public static void Update()
        {
            if (!ModConfig.ModEnabled.Value) { inFight = false; return; }
            Fighters f;
            var gsm = Gsm;
            bool onStage = StageProperties.Instance != null;
            bool active = gsm != null && (gsm.matchInProgress || onStage) && !gsm.MatchEnded && TryGetFighters(out f);
            if (!active)
            {
                if (inFight) inFight = false;
                return;
            }
            TryGetFighters(out f);
            if (!inFight)
            {
                inFight = true;
                groundY = float.NaN;
            }
            bool paused = Paused;
            if (paused != pausedLast)
            {
                pausedLast = paused;
                Plugin.Log.LogInfo(paused ? "Pause erkannt." : "Pause beendet.");
                if (!paused && ModConfig.On(ModConfig.FightAnnouncements)) Speech.Say(Loc.T("Weiter", "Resumed"));
            }
            if (paused || ModMenu.IsOpen) return;

            float now = Time.realtimeSinceStartup;
            Vector3 mp = f.Me.charObject.transform.position;
            Vector3 op = f.Opp.charObject.transform.position;
            float lowY = Mathf.Min(mp.y, op.y);
            if (float.IsNaN(groundY) || lowY < groundY) groundY = lowY;
            float range = Range();

            if (gsm.CountDownEnded)
            {
                UpdateHealth(f, now);
                UpdateBigDamage(f, now);
                UpdateInvulnerable(f);
            }
            // Orientierungstöne erst, wenn der Kampf freigegeben ist, und nur bis zum Rundenende
            if (gsm.CountDownEnded && roundLive)
            {
                UpdateLocator(mp, op, range, now);
                UpdateWall(mp, range, now);
            }
            if (gsm.CountDownEnded)
            {
                UpdateResources(f);
                UpdateTimer(gsm);
            }
        }

        private static void UpdateLocator(Vector3 mp, Vector3 op, float range, float now)
        {
            float dx = op.x - mp.x;
            float adx = Mathf.Abs(dx);
            float center = Mathf.Max(0.25f, range * 0.05f);
            int side = adx < center ? lastSide : (dx > 0 ? 1 : -1);

            if (lastSide != 0 && side != lastSide && ModConfig.On(ModConfig.SideSwitchCue))
            {
                // aufsteigender Wisch zur neuen Seite
                Audio.SideSwitch(side);
            }
            if (side != 0) lastSide = side;

            if (!ModConfig.On(ModConfig.EnemyLocator)) return;
            if (now < nextPing) return;
            float t = Mathf.Clamp01(adx / range);
            float pan = adx < center ? 0f : Mathf.Sign(dx) * (0.35f + 0.65f * t);
            bool air = !float.IsNaN(groundY) && op.y - groundY > Mathf.Max(0.3f, range * 0.08f);
            if (adx < center) Audio.EnemyClose();
            else if (air) Audio.EnemyAir(pan);
            else Audio.EnemyGround(pan);
            nextPing = now + Mathf.Lerp(0.13f, 0.75f, t);
        }

        private static void UpdateWall(Vector3 mp, float range, float now)
        {
            if (!ModConfig.On(ModConfig.WallCue)) return;
            if (!Walls(out float l, out float r)) return;
            float zone = Mathf.Max(0.6f, range * 0.12f);
            float dl = mp.x - l, dr = r - mp.x;
            if (dl > zone && dr > zone) { nextWallCue = 0; return; }
            if (now < nextWallCue) return;
            float pan = dl <= zone ? -0.9f : 0.9f;
            Audio.WallKnock(pan);
            nextWallCue = now + 1.2f;
        }

        // ---------------- Unverwundbarkeit ----------------

        private static bool lastInvul, lastArmor;

        /// <summary>Eigene Unverwundbarkeit (z. B. bei Reversal-/Super-Angriffen) und Super-Rüstung hörbar machen.</summary>
        private static void UpdateInvulnerable(Fighters f)
        {
            if (!ModConfig.On(ModConfig.InvulCue)) { lastInvul = lastArmor = false; return; }
            try
            {
                var att = f.Me.charComponents != null ? f.Me.charComponents.Attackable : null;
                if (att == null) return;
                // Das Spiel berechnet den Zustand jeden Frame und merkt ihn sich in lastInvincible
                bool invul = R.Get<bool>(att, "lastInvincible");
                bool armor = att.IsSuperArmorActive;
                if (invul && !lastInvul) Audio.InvulStart();
                else if (!invul && lastInvul) Audio.InvulEnd();
                if (armor && !lastArmor && !invul) Audio.ArmorStart();
                lastInvul = invul;
                lastArmor = armor;
            }
            catch { }
        }

        // ---------------- Großer Schaden ----------------

        private static float lastHpForDamage = -1f, burstDamage, lastDamageTime;
        private static bool burstCueDone;

        /// <summary>Viel Gesundheit auf einmal verloren (Kombo/Super): Warnklang, danach Ansage des Verlusts.</summary>
        private static void UpdateBigDamage(Fighters f, float now)
        {
            float hp = Hp(f.Me);
            if (hp < 0f) return;
            if (lastHpForDamage < 0f || hp > lastHpForDamage + 0.001f) { lastHpForDamage = hp; return; } // Start/Heilung
            float lost = lastHpForDamage - hp;
            lastHpForDamage = hp;
            if (!ModConfig.On(ModConfig.BigHitCue)) { burstDamage = 0f; return; }
            float threshold = ModConfig.BigHitPercent.Value / 100f;
            if (lost > 0.0005f)
            {
                if (now - lastDamageTime > 1.0f) { burstDamage = 0f; burstCueDone = false; } // neue Trefferserie
                burstDamage += lost;
                lastDamageTime = now;
                if (!burstCueDone && burstDamage >= threshold)
                {
                    burstCueDone = true;
                    Audio.BigHit();
                }
            }
            else if (burstCueDone && burstDamage > 0f && now - lastDamageTime > 1.0f)
            {
                // Serie vorbei: Gesamtverlust ansagen
                int pct = Mathf.RoundToInt(burstDamage * 100f);
                burstDamage = 0f;
                burstCueDone = false;
                if (hp > 0f) Speech.Queue(Loc.T("Minus ", "Minus ") + pct + Loc.T(" Prozent", " percent"));
            }
        }

        private static void UpdateHealth(Fighters f, float now)
        {
            float me = Hp(f.Me), opp = Hp(f.Opp);
            if (me < 0 || opp < 0) return;
            float crit = ModConfig.CriticalHealthPercent.Value / 100f;

            if (ModConfig.On(ModConfig.HealthWarnings))
            {
                if (!myHalf && me <= 0.5f && me > crit) { myHalf = true; Speech.Queue(Loc.T("Hälfte deiner Gesundheit", "Half health")); }
                if (!myCrit && me <= crit && me > 0f) { myCrit = myHalf = true; Speech.Queue(Loc.T("Gesundheit kritisch, ", "Health critical, ") + Pct(me)); }
                if (!myLow && me <= 0.1f && me > 0f) { myLow = myCrit = myHalf = true; Speech.Queue(Loc.T("Fast besiegt", "Almost defeated")); }
                if (me > crit + 0.05f) { myCrit = false; myLow = false; }
                if (me > 0.55f) myHalf = false;

                if (me > 0f && me <= crit && roundLive && now >= nextHeartbeat)
                {
                    Audio.Heartbeat(0.6f);
                    nextHeartbeat = now + (me <= 0.1f ? 0.5f : 0.85f);
                }
            }

            if (ModConfig.On(ModConfig.EnemyHealth))
            {
                if (!oppHalf && opp <= 0.5f && opp > 0.25f) { oppHalf = true; Speech.Queue(Loc.T("Gegner unter der Hälfte", "Opponent below half")); }
                if (!oppCrit && opp <= 0.25f && opp > 0f) { oppCrit = oppHalf = true; Speech.Queue(Loc.T("Gegner kritisch, ", "Opponent critical, ") + Pct(opp)); }
                if (opp > 0.3f) oppCrit = false;
                if (opp > 0.55f) oppHalf = false;
            }
        }

        private static void UpdateResources(Fighters f)
        {
            if (!ModConfig.On(ModConfig.ResourceAnnouncements)) return;
            try
            {
                var cc = f.Me.charComponents;
                if (cc == null) return;
                if (!resInit)
                {
                    // Zu Rundenbeginn nur den Stand merken, nicht ansagen (F5 sagt ihn auf Wunsch)
                    resInit = true;
                    var sm0 = cc.SuperMeter;
                    if (sm0 != null) { superOne = sm0.oneBar; superFull = sm0.superMeterFull; burstReady = sm0.BurstReady(); }
                    if (cc.CollabHolder != null) { collabReady = CallInMeterReady(cc); offCollabReady = OffCollabMeterReady(cc.CollabHolder); }
                    return;
                }
                var sm = cc.SuperMeter;
                if (sm != null)
                {
                    bool one = sm.oneBar, full = sm.superMeterFull;
                    if (full && !superFull) Speech.Queue(Loc.T("Super-Leiste voll", "Super meter full"));
                    else if (one && !superOne) Speech.Queue(Loc.T("Super bereit", "Super ready"));
                    superFull = full; superOne = one;
                    bool br = sm.BurstReady();
                    if (br && !burstReady) Speech.Queue(Loc.T("Burst bereit", "Burst ready"));
                    burstReady = br;
                }
                var ch = cc.CollabHolder;
                if (ch != null)
                {
                    // Nur nach Ressourcen urteilen (IsCallInReady wird bei jeder Trefferstarre kurz falsch).
                    bool r = CallInMeterReady(cc);
                    if (r && !collabReady) Speech.Queue(Loc.T("Kollab-Ruf bereit", "Collab call-in ready"));
                    collabReady = r;
                    bool o = OffCollabMeterReady(ch);
                    if (o && !offCollabReady) Speech.Queue(Loc.T("Off-Kollab bereit", "Off-collab ready"));
                    offCollabReady = o;
                }
            }
            catch { }
        }

        /// <summary>Kollab-Ruf (Vorwärts + Kollab): braucht Kollab verfügbar und genug Super-Leiste.</summary>
        private static bool CallInMeterReady(CharacterComponents cc)
        {
            try
            {
                var ch = cc.CollabHolder;
                if (ch == null || !R.Get<bool>(ch, "collabDeployable") || ch.CollabLockedOut) return false;
                var g = GlobalManager.Instance;
                float cost = g != null && g.Constants != null && g.Constants.meterData != null ? g.Constants.meterData.SUPER_COST : 0f;
                return cc.SuperMeter != null && (float)cc.SuperMeter.currentSuperMeter >= cost;
            }
            catch { return false; }
        }

        /// <summary>Off-Kollab (Kollab-Taste): braucht genug Superchat-Leiste.</summary>
        private static bool OffCollabMeterReady(CollabHolder ch)
        {
            try { return R.Get<bool>(ch, "collabDeployable") && !ch.CollabLockedOut && ch.OffCollabMeterReady(); }
            catch { return false; }
        }

        private static float TimeLeftSeconds(GameStateManager gsm)
        {
            try
            {
                float t = (float)gsm.TimeLeft;
                if (t > 150f) t /= 60f; // vermutlich Frames
                return t;
            }
            catch { return -1f; }
        }

        private static void UpdateTimer(GameStateManager gsm)
        {
            if (!ModConfig.On(ModConfig.FightAnnouncements)) return;
            float t = TimeLeftSeconds(gsm);
            if (t <= 0f || t > 100f) return;
            int[] marks = { 30, 10 };
            foreach (int m in marks)
            {
                if (t <= m && lastTimeWarn > m && t > m - 3)
                {
                    lastTimeWarn = m;
                    Speech.Queue(Loc.En ? m + " seconds left" : "Noch " + m + " Sekunden");
                }
            }
        }

        private static string Pct(float v) => Mathf.RoundToInt(v * 100f) + Loc.T(" Prozent", " percent");

        // ---------------- Abfragen per Taste ----------------

        public static bool InFight => inFight;

        public static void SpeakStatus()
        {
            if (!TryGetFighters(out var f)) { Speech.Say(Loc.T("Kein Kampf aktiv.", "No match running.")); return; }
            var gsm = Gsm;
            string s = Loc.T("Du ", "You ") + Pct(Mathf.Max(0, Hp(f.Me))) + Loc.T(", Gegner ", ", opponent ") + Pct(Mathf.Max(0, Hp(f.Opp))) + ".";
            float t = TimeLeftSeconds(gsm);
            if (t > 0 && t <= 100) s += Loc.T(" Zeit ", " Time ") + Mathf.CeilToInt(t) + ".";
            if (gsm != null)
            {
                int a = f.MyIndex == 0 ? gsm.Player1WinCount : gsm.Player2WinCount;
                int b = f.MyIndex == 0 ? gsm.Player2WinCount : gsm.Player1WinCount;
                s += Loc.T(" Runden ", " Rounds ") + a + Loc.T(" zu ", " to ") + b + ".";
            }
            try
            {
                var cc = f.Me.charComponents;
                var sm = cc?.SuperMeter;
                if (sm != null)
                {
                    float max = (float)sm.superMeterMax;
                    float cur = (float)sm.currentSuperMeter;
                    if (max > 0) s += " Super " + Mathf.RoundToInt(cur / max * 100f) + Loc.T(" Prozent", " percent") + (sm.oneBar ? Loc.T(", einsatzbereit", ", ready") : "") + ".";
                    s += sm.BurstReady() ? Loc.T(" Burst bereit.", " Burst ready.") : Loc.T(" Burst lädt.", " Burst charging.");
                }
                var ch = cc?.CollabHolder;
                if (ch != null)
                {
                    s += CallInMeterReady(cc) ? Loc.T(" Kollab-Ruf bereit.", " Collab call-in ready.") : Loc.T(" Kollab-Ruf nicht bereit.", " Collab call-in not ready.");
                    s += OffCollabMeterReady(ch) ? Loc.T(" Off-Kollab bereit.", " Off-collab ready.") : Loc.T(" Off-Kollab nicht bereit.", " Off-collab not ready.");
                }
                var sc = cc?.SuperChatHolder;
                if (sc != null && sc.MaxSCMeter > 0) s += Loc.T(" Off-Kollab-Leiste ", " Off-collab meter ") + sc.SCMeter + Loc.T(" von ", " of ") + sc.MaxSCMeter + ".";
            }
            catch { }
            Speech.Say(s);
        }

        public static void SpeakEnemyPosition()
        {
            if (!TryGetFighters(out var f)) { Speech.Say(Loc.T("Kein Kampf aktiv.", "No match running.")); return; }
            Vector3 mp = f.Me.charObject.transform.position;
            Vector3 op = f.Opp.charObject.transform.position;
            float range = Range();
            float dx = op.x - mp.x, adx = Mathf.Abs(dx);
            string dir = adx < Mathf.Max(0.25f, range * 0.05f) ? Loc.T("direkt bei dir", "right next to you") : (dx > 0 ? Loc.T("rechts", "right") : Loc.T("links", "left"));
            string dist = adx < range * 0.25f ? Loc.T("nah", "close") : (adx < range * 0.6f ? Loc.T("mittlere Entfernung", "medium distance") : Loc.T("weit weg", "far away"));
            bool air = !float.IsNaN(groundY) && op.y - groundY > Mathf.Max(0.3f, range * 0.08f);
            string s = Name(f.Opp);
            s = (s.Length > 0 ? Speech.G(s) : Loc.T("Gegner", "Opponent")) + " " + dir + ", " + dist + (air ? Loc.T(", in der Luft", ", in the air") : Loc.T(", am Boden", ", on the ground")) + ".";
            if (Walls(out float l, out float r))
            {
                float zone = Mathf.Max(0.6f, range * 0.12f);
                if (mp.x - l <= zone) s += Loc.T(" Du stehst in der linken Ecke.", " You are in the left corner.");
                else if (r - mp.x <= zone) s += Loc.T(" Du stehst in der rechten Ecke.", " You are in the right corner.");
            }
            Speech.Say(s);
        }

        /// <summary>Vorführung aller Töne (Mod-Menü).</summary>
        public static void DemoLocator() => Samples.PlayAll();
    }
}
