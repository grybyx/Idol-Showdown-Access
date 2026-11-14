using System;
using System.Collections.Generic;
using IdolShowdown;
using IdolShowdown.Managers;
using UnityEngine;

namespace IdolShowdownAccess
{
    /// <summary>
    /// Charakterauswahl: Ansagen für Figur, Kostüm, Kollab-Partner und Status,
    /// plus "Einrasten" des frei beweglichen Cursors auf die nächste Figur.
    /// </summary>
    internal static class CharSelect
    {
        private static CSSController controller;
        private static CSSPlayerData lastPD;
        private static bool longIntroDone;

        public static bool IsActive => controller != null && controller.isActiveAndEnabled;

        public static void ApplyPatches()
        {
            var h = typeof(CharSelect);
            Patcher.Patch(typeof(CSSController), "OnEnable", h, postfix: nameof(ControllerEnablePost));
            Patcher.Patch(typeof(CSSController), "OnFinishStartAnimation", h, postfix: nameof(StartAnimPost));
            Patcher.Patch(typeof(CSSPlayerData), "UpdateCharacterData", h, postfix: nameof(CharacterHoverPost));
            Patcher.Patch(typeof(CSSPlayerData), "UpdateCollab", h, postfix: nameof(CollabHoverPost));
            Patcher.Patch(typeof(CSSSubMenuButton), "UpdatePlayerOne", h, postfix: nameof(SubMenuHoverP1));
            Patcher.Patch(typeof(CSSSubMenuButton), "UpdatePlayerTwo", h, postfix: nameof(SubMenuHoverP2));
            Patcher.Patch(typeof(CSSInputHandler), "GoToNextState", h, postfix: nameof(StatePost));
            Patcher.Patch(typeof(CSSInputHandler), "GoToPreviousState", h, postfix: nameof(StatePost));
            Patcher.Patch(typeof(CSSCosplayController), "UpdateSelectedCosplay", h, prefix: nameof(CosplayPre), postfix: nameof(CosplayPost));
            Patcher.Patch(typeof(CSSCursor), "Update", h, prefix: nameof(CursorUpdatePre));
            Patcher.Patch(typeof(CSSInputHandler), "Update", h, prefix: nameof(InputHandlerUpdatePre));
            Patcher.Patch(typeof(CSSCosplayController), "Update", h, prefix: nameof(BlockWhenMenuOpen));
            Patcher.Patch(typeof(CSSCollabNavigator), "Update", h, prefix: nameof(BlockWhenMenuOpen));
        }

        public static bool OwnsObject(GameObject go)
        {
            // Kollab-Schaltflächen werden über UpdateCollab angesagt.
            return go != null && go.GetComponent<CSSCollabButtonData>() != null;
        }

        private static bool Speak => ModConfig.On(ModConfig.MenuSpeech);

        private static string PlayerPrefix(CSSPlayerData pd)
        {
            if (pd == null) return "";
            var num = R.Get<PlayerNumber>(pd, "playerNum");
            return num == PlayerNumber.playerTwo ? Loc.T("Spieler 2: ", "Player 2: ") : "";
        }

        // ---------------- Patches ----------------

        private static void ControllerEnablePost(CSSController __instance)
        {
            controller = __instance;
        }

        private static void StartAnimPost(CSSController __instance)
        {
            controller = __instance;
            if (!Speak) return;
            string s = Loc.T("Charakterauswahl.", "Character select.");
            if (!longIntroDone)
            {
                longIntroDone = true;
                s += ModConfig.CssSnapCursor.Value
                    ? Loc.T(" Jeder Richtungsdruck springt zur nächsten Figur. Bestätigen wählt, danach folgen Kostüm und Kollab-Partner. Ansicht-Taste oder F2: Infos zur Figur.", " Each direction press jumps to the next character. Confirm selects, then costume and collab partner follow. View button or F2: character info.")
                    : Loc.T(" Bewege den Cursor über die Figuren. Bestätigen wählt.", " Move the cursor over the characters. Confirm selects.");
            }
            Speech.Say(s);
        }

        private static void CharacterHoverPost(CSSPlayerData __instance, CSSCharacterButtonController __0)
        {
            if (!Speak || __0 == null) return;
            if (__instance.CurrentState != SelectState.CHARACTER) return;
            lastPD = __instance;
            string name = Speech.Clean(__0.FullName);
            if (name.Length == 0 && __0.Character != null) name = __0.Character.charName;
            if (name.Length == 0) name = TextUtil.PrettyName(__0.gameObject.name);
            Speech.Say(PlayerPrefix(__instance) + Speech.G(name));
        }

        private static void CollabHoverPost(CSSPlayerData __instance, CSSCollabButtonData __0)
        {
            if (!Speak || __0 == null) return;
            lastPD = __instance;
            string name = Speech.Clean(__0.Name);
            if (name.Length == 0) name = TextUtil.Join(TextUtil.Texts(__0.gameObject, 2));
            Speech.Say(PlayerPrefix(__instance) + Loc.T("Kollab ", "Collab ") + Speech.G(name));
        }

        private static void SubMenuHoverP1(CSSSubMenuButton __instance, bool __0) { if (__0) SubMenuHover(__instance, ""); }
        private static void SubMenuHoverP2(CSSSubMenuButton __instance, bool __0) { if (__0) SubMenuHover(__instance, Loc.T("Spieler 2: ", "Player 2: ")); }

        private static void SubMenuHover(CSSSubMenuButton b, string prefix)
        {
            if (!Speak || b == null) return;
            string label = TextUtil.Join(TextUtil.Texts(b.gameObject, 3));
            if (label.Length == 0 && b.menuObject != null) label = TextUtil.PrettyName(b.menuObject.name);
            if (label.Length == 0) label = TextUtil.PrettyName(b.gameObject.name);
            Speech.Say(prefix + Speech.G(label) + Loc.T(", Untermenü", ", submenu"));
        }

        private static void StatePost(CSSInputHandler __instance)
        {
            if (!Speak || __instance == null) return;
            var state = R.Get<CSSPlayerState>(__instance, "currentState");
            var pd = R.Get<CSSPlayerData>(__instance, "playerData");
            string p = PlayerPrefix(pd);
            string s = null;
            switch (state)
            {
                case CSSPlayerState.SelectCharacter: s = Loc.T("Figur wählen", "Choose character"); break;
                case CSSPlayerState.SelectCosplay:
                    s = (pd != null && pd.SelectedCharacter != null ? Speech.G(Speech.Clean(pd.SelectedCharacter.charName)) + Loc.T(" gewählt. ", " selected. ") : "") +
                        Loc.T("Kostüm wählen: Hoch und Runter, Links und Rechts blättert.", "Choose costume: up and down, left and right change page.");
                    break;
                case CSSPlayerState.SelectCollab: s = Loc.T("Kollab-Partner wählen: Hoch und Runter.", "Choose collab partner: up and down."); break;
                case CSSPlayerState.SelectionFinished:
                    s = Loc.T("Bereit", "Ready");
                    if (pd != null)
                    {
                        string c = pd.SelectedCharacter != null ? Speech.Clean(pd.SelectedCharacter.charName) : "";
                        string k = TextUtil.Tmp(pd.collabName);
                        if (c.Length > 0) s += ": " + Speech.G(c);
                        if (k.Length > 0) s += Loc.T(" mit ", " with ") + Speech.G(k);
                    }
                    break;
            }
            if (s != null) Scheduler.In(0.05f, () => Speech.Say(p + s));
        }

        private static void CosplayPre(CSSCosplayController __instance, out int __state)
        {
            __state = R.Get<int>(__instance, "pageScrollDir");
        }

        private static void CosplayPost(CSSCosplayController __instance, int __state)
        {
            if (!Speak || __instance == null || __state == 0) return; // nur bei Seitenwechsel; Hoch/Runter liest die Auswahl selbst
            Scheduler.In(0.12f, () =>
            {
                var btn = R.Get<UnityEngine.UI.Button>(__instance, "selectedCosplayButton");
                int page = R.Get<int>(__instance, "page");
                string label = btn != null ? TextUtil.Join(TextUtil.Texts(btn.gameObject, 2)) : "";
                Speech.Say(Loc.T("Seite ", "Page ") + (page + 1) + (label.Length > 0 ? ", " + Speech.G(label) : ""));
            });
        }

        private static bool BlockWhenMenuOpen() => !ModMenu.IsOpen && !ModMenu.RecentlyClosed;

        private static bool InputHandlerUpdatePre(CSSInputHandler __instance)
        {
            if (!ModMenu.IsOpen)
            {
                if (!ModMenu.RecentlyClosed) return true;
                // Tastendruck, der das Mod-Menü geschlossen hat, verwerfen
                for (int i = 0; i <= 5; i++) __instance.RequestInputDown((CSSInput)i);
                return false;
            }
            // Steuerkreuz/Stick des Spiels an das Mod-Menü weiterleiten.
            if (__instance.RequestInputDown(CSSInput.Up)) ModMenu.Handle(ModMenu.Act.Up);
            if (__instance.RequestInputDown(CSSInput.Down)) ModMenu.Handle(ModMenu.Act.Down);
            if (__instance.RequestInputDown(CSSInput.Left)) ModMenu.Handle(ModMenu.Act.Left);
            if (__instance.RequestInputDown(CSSInput.Right)) ModMenu.Handle(ModMenu.Act.Right);
            if (__instance.RequestInputDown(CSSInput.Select)) ModMenu.Handle(ModMenu.Act.Ok);
            if (__instance.RequestInputDown(CSSInput.Back)) ModMenu.Handle(ModMenu.Act.Back);
            return false;
        }

        // ---------------- Cursor einrasten ----------------

        private static bool CursorUpdatePre(CSSCursor __instance)
        {
            if (ModMenu.IsOpen || ModMenu.RecentlyClosed) return false;
            if (!ModConfig.On(ModConfig.CssSnapCursor)) return true;
            try
            {
                bool allow = R.Get<bool>(__instance, "allowInput");
                var ih = R.Get<CSSInputHandler>(__instance, "inputHandler");
                if (!allow || ih == null) return true; // Original kehrt ohnehin sofort zurück
                var trans = R.Get<RectTransform>(__instance, "trans");
                if (trans == null) return true;

                // Trefferpunkt wie im Original berechnen
                float po = __instance.PointerOffset;
                Vector3 c = trans.TransformPoint(trans.rect.center);
                __instance.position = (Vector2)(c + new Vector3(__instance.playerNumber != PlayerNumber.playerOne ? -po : po, -po, 0f));

                int dir = -1;
                if (ih.RequestInputDown(CSSInput.Left)) dir = 0;
                else if (ih.RequestInputDown(CSSInput.Right)) dir = 1;
                else if (ih.RequestInputDown(CSSInput.Up)) dir = 2;
                else if (ih.RequestInputDown(CSSInput.Down)) dir = 3;
                if (dir >= 0)
                {
                    lastPD = __instance.player;
                    Snap(__instance, trans, dir);
                }

                if (ih.RequestInputDown(CSSInput.Select)) R.Call(__instance, "HandleSelect");

                // Position übernehmen (mit Begrenzung wie im Original)
                Vector2 p = R.Get<Vector2>(__instance, "inputDir");
                var b = __instance.bounds;
                if (b != null)
                {
                    if (p.x < b.westBound) p.x = b.westBound;
                    if (p.x > b.eastBound) p.x = b.eastBound;
                    if (p.y < b.southBound) p.y = b.southBound;
                    if (p.y > b.northBound) p.y = b.northBound;
                }
                __instance.ForcePosition(p);
                trans.localPosition = p;
                return false;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("CursorUpdatePre: " + e.Message);
                return true;
            }
        }

        private static Vector2 Center(CSSButtonBase b)
        {
            var bounds = R.Get<List<Vector2>>(b, "bounds");
            if (R.Get<bool>(b, "initialized") && bounds != null && bounds.Count > 0)
            {
                Vector2 s = Vector2.zero;
                foreach (var v in bounds) s += v;
                return s / bounds.Count;
            }
            var rt = b.trans != null ? b.trans : b.GetComponent<RectTransform>();
            if (rt == null) return b.transform.position;
            return rt.TransformPoint(rt.rect.center);
        }

        private static readonly Vector2[] Dirs = { Vector2.left, Vector2.right, Vector2.up, Vector2.down };

        private static void Snap(CSSCursor cursor, RectTransform trans, int dir)
        {
            if (controller == null) controller = UnityEngine.Object.FindObjectOfType<CSSController>();
            if (controller == null) return;
            var cands = new List<CSSButtonBase>();
            if (controller.ButtonControllers != null)
                foreach (var b in controller.ButtonControllers) if (b != null && b.isActiveAndEnabled) cands.Add(b);
            if (controller.MenuButtonControllers != null)
                foreach (var b in controller.MenuButtonControllers) if (b != null && b.isActiveAndEnabled) cands.Add(b);
            if (cands.Count == 0) return;

            CSSButtonBase current = cursor.selectableButton;
            bool onCurrent = current != null && (cursor.playerNumber == PlayerNumber.playerOne ? current.p1Select : current.p2Select);
            Vector2 origin = onCurrent ? Center(current) : cursor.position;

            // typischer Abstand zwischen Schaltflächen als Maßstab
            Vector2 d = Dirs[dir];
            CSSButtonBase best = null;
            float bestScore = float.MaxValue;
            foreach (var b in cands)
            {
                if (onCurrent && b == current) continue;
                Vector2 v = Center(b) - origin;
                float along = Vector2.Dot(v, d);
                if (along <= 1e-3f) continue;
                float perp = Mathf.Abs(v.x * d.y - v.y * d.x);
                // Schaltflächen ungefähr in derselben Zeile/Spalte stark bevorzugen
                if (perp > along * 2.5f) continue;
                float score = along + perp * 2.2f;
                if (score < bestScore) { bestScore = score; best = b; }
            }
            if (best == null && !onCurrent)
            {
                // Cursor steht auf keiner Figur: einfach die nächste nehmen
                foreach (var b in cands)
                {
                    float dist = (Center(b) - origin).sqrMagnitude;
                    if (dist < bestScore) { bestScore = dist; best = b; }
                }
            }
            if (best == null)
            {
                Audio.Edge();
                return;
            }

            Vector2 target = Center(best);
            Vector2 delta = target - cursor.position;
            Transform parent = trans.parent;
            Vector3 localDelta = parent != null
                ? parent.InverseTransformPoint((Vector3)target) - parent.InverseTransformPoint((Vector3)cursor.position)
                : (Vector3)delta;
            Vector2 p = R.Get<Vector2>(cursor, "inputDir");
            p += new Vector2(localDelta.x, localDelta.y);
            cursor.ForcePosition(p);
        }

        // ---------------- Details (F2 / Ansicht-Taste) ----------------

        public static bool SpeakDetails()
        {
            if (!IsActive) return false;
            CSSPlayerData pd = lastPD != null ? lastPD : (controller != null ? controller.P1Data : null);
            if (pd == null) return false;
            var parts = new List<string>();
            if (pd.CurrentState == SelectState.COLLAB)
            {
                parts.Add(Loc.T("Kollab ", "Collab ") + Speech.G(TextUtil.Tmp(pd.collabName)));
                parts.Add(TextUtil.Tmp(pd.collabMoveName));
                parts.Add(TextUtil.Tmp(pd.collabMoveDescription));
                parts.Add(TextUtil.Tmp(pd.callInMoveName));
                parts.Add(TextUtil.Tmp(pd.callInMoveDescription));
            }
            else
            {
                var b = pd.highlightedButton;
                if (b != null)
                {
                    parts.Add(Speech.Clean(b.FullName));
                    parts.Add(Speech.Clean(b.Info));
                    parts.Add("#" + Loc.T("Angriff ", "Offense ") + b.Offense + Loc.T(", Verteidigung ", ", defense ") + b.Defense + Loc.T(", Reichweite ", ", range ") + b.Range + Loc.T(", Tempo ", ", speed ") + b.Speed + Loc.T(", Schaden ", ", damage ") + b.Damage);
                }
                else
                {
                    parts.Add(TextUtil.Tmp(pd.radarName));
                    parts.Add(TextUtil.Tmp(pd.radarInfo));
                }
            }
            parts.RemoveAll(string.IsNullOrEmpty);
            if (parts.Count == 0) return false;
            for (int i = 0; i < parts.Count; i++)
                if (parts[i].StartsWith("#")) parts[i] = parts[i].Substring(1); else if (parts[i].IndexOf('\u0001') < 0) parts[i] = Speech.G(parts[i]);
            Speech.Say(string.Join(". ", parts.ToArray()));
            return true;
        }
    }
}
