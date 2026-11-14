using System;
using System.Collections.Generic;
using IdolShowdown;
using IdolShowdown.Managers;
using IdolShowdown.UI;
using UnityEngine;
using UnityEngine.UI;

namespace IdolShowdownAccess
{
    /// <summary>
    /// Liest jedes ausgewählte Menüelement (Schaltflächen, Regler, Umschalter,
    /// Eingabefelder) vor und meldet Wertänderungen.
    /// </summary>
    internal static class MenuReader
    {
        private static GameObject lastGo;
        private static float lastGoTime;
        private static string lastValueSig;
        private static object lastHandler;
        private static string pendingPrefix;
        private static float pendingPrefixTime;
        public static float LastUIInput = -10f;
        public static bool AnyMenuSeen;
        public static float FirstMenuSeenAt = -1f;
        private static string lastDescription;

        public static void ApplyPatches()
        {
            var host = typeof(MenuReader);
            Patcher.Patch(typeof(ISSelectable), "OnSelect", host, postfix: nameof(ISSelectablePost));
            Patcher.Patch(typeof(ISButton), "OnSelect", host, postfix: nameof(ISSelectablePost));
            Patcher.Patch(typeof(ISSlider), "OnSelect", host, postfix: nameof(ISSelectablePost));
            Patcher.Patch(typeof(ISInputField), "OnSelect", host, postfix: nameof(ISSelectablePost));
            Patcher.Patch(typeof(Selectable), "OnSelect", host, postfix: nameof(SelectablePost), args: new[] { typeof(UnityEngine.EventSystems.BaseEventData) });
            Patcher.Patch(typeof(SwitchButtonScript), "OnSliderValueChange", host, postfix: nameof(SwitchChangedPost));
            Patcher.Patch(typeof(ISSlider), "OnLeft", host, postfix: nameof(SliderMovedPost));
            Patcher.Patch(typeof(ISSlider), "OnRight", host, postfix: nameof(SliderMovedPost));
            Patcher.Patch(typeof(ISCanvas), "OnInput", host, prefix: nameof(CanvasInputPre));
            Patcher.Patch(typeof(ISCanvas), "OnInputReleased", host, prefix: nameof(CanvasReleasePre));
            Patcher.Patch(typeof(NullPopup), "Popup", host, postfix: nameof(PopupPost));
            Patcher.Patch(typeof(NullBaseMenu), "FocusTab", host, postfix: nameof(NullTabPost));
            Patcher.Patch(typeof(SettingsHelper), "OnTabLeft", host, postfix: nameof(SettingsTabPost));
            Patcher.Patch(typeof(SettingsHelper), "OnTabRight", host, postfix: nameof(SettingsTabPost));
            Patcher.Patch(typeof(SettingsHelper), "OnEnable", host, postfix: nameof(SettingsOpenPost));
            Patcher.Patch(typeof(PauseMenu), "OnEnable", host, postfix: nameof(PauseOpenPost));
            Patcher.Patch(typeof(IdolShowdown.UI.StageSelect.StagePreviewHandler), "UpdateStagePreview", host, postfix: nameof(StagePreviewPost));
            Patcher.Patch(typeof(IdolShowdown.UI.StageSelect.StageSelectController), "UpdateVariantImages", host, postfix: nameof(StageVariantPost));
            Patcher.Patch(typeof(IdolShowdown.UI.InputSelect.InputDeviceHandler), "OnEnable", host, postfix: nameof(DeviceSelectOpenPost));
            Patcher.Patch(typeof(IdolShowdown.UI.InputSelect.InputDeviceHandler), "HandleIconMovement", host, postfix: nameof(DeviceSelectMovePost));
        }

        // ---------------- Patches ----------------

        private static void ISSelectablePost(ISSelectable __instance)
        {
            try
            {
                var sel = R.Get<Selectable>(__instance, "selectable");
                var handler = R.Get<ISUIElementsHandler>(__instance, "elementsHandler");
                GameObject go = sel != null ? sel.gameObject : __instance.gameObject;
                // Sofort als "ausgewählt" merken (verhindert Doppelansage), aber den Text erst
                // kurz danach lesen, wenn das Spiel Werte und Beschriftungen gesetzt hat.
                MarkSelected(go);
                Scheduler.In(0.04f, () => { if (go != null && go == lastGo) Announce(go, sel, handler, true); });
            }
            catch (Exception e) { Plugin.Log.LogWarning("ISSelectablePost: " + e.Message); }
        }

        private static void SelectablePost(Selectable __instance)
        {
            try
            {
                if (__instance == null) return;
                // Einen Frame warten: kommt die Auswahl über das Spiel-UI-System,
                // sagt ISSelectablePost sie mit Positionsangabe an.
                var go = __instance.gameObject;
                var sel = __instance;
                Scheduler.In(0f, () =>
                {
                    if (go == null) return;
                    if (go == lastGo && Time.realtimeSinceStartup - lastGoTime < 0.3f) return;
                    MarkSelected(go);
                    Announce(go, sel, null, true);
                });
            }
            catch (Exception e) { Plugin.Log.LogWarning("SelectablePost: " + e.Message); }
        }

        /// <summary>Wert eines Umschalters hat sich geändert: genau den neuen Eintrag ansagen.</summary>
        private static void SwitchChangedPost(SwitchButtonScript __instance, float __0)
        {
            try
            {
                if (!ModConfig.On(ModConfig.MenuSpeech) || ModMenu.IsOpen || __instance == null) return;
                // nur Änderungen durch den Spieler (kurz nach einer Eingabe) und am fokussierten Element
                if (Time.realtimeSinceStartup - LastUIInput > 1.0f) return;
                if (lastGo != null && !__instance.transform.IsChildOf(lastGo.transform) && !lastGo.transform.IsChildOf(__instance.transform)) return;
                var opts = __instance.Options;
                int v = Mathf.RoundToInt(__0);
                string t = opts != null && v >= 0 && v < opts.Length ? Speech.Clean(opts[v]) : v.ToString();
                lastValueSig = t;
                if (t.Length > 0) Speech.Say(Speech.G(t));
            }
            catch (Exception e) { Plugin.Log.LogWarning("SwitchChangedPost: " + e.Message); }
        }

        /// <summary>Regler ohne Umschalter-Logik (reine Zahlenregler).</summary>
        private static void SliderMovedPost(ISSlider __instance)
        {
            try
            {
                if (!ModConfig.On(ModConfig.MenuSpeech) || ModMenu.IsOpen || __instance == null) return;
                if (R.Get<SwitchButtonScript>(__instance, "switchLogic") != null) return; // macht SwitchChangedPost
                var sl = R.Get<Slider>(__instance, "slider");
                if (sl == null) return;
                string t = ValueText(sl.gameObject, sl);
                lastValueSig = t;
                if (t.Length > 0) Speech.Say(t);
            }
            catch (Exception e) { Plugin.Log.LogWarning("SliderMovedPost: " + e.Message); }
        }

        private static bool CanvasInputPre(UIInputValue __0)
        {
            LastUIInput = Time.realtimeSinceStartup;
            if (!ModMenu.IsOpen) return !ModMenu.RecentlyClosed;
            switch (__0)
            {
                case UIInputValue.Up: ModMenu.Handle(ModMenu.Act.Up); break;
                case UIInputValue.Down: ModMenu.Handle(ModMenu.Act.Down); break;
                case UIInputValue.Left: ModMenu.Handle(ModMenu.Act.Left); break;
                case UIInputValue.Right: ModMenu.Handle(ModMenu.Act.Right); break;
                case UIInputValue.Submit: ModMenu.Handle(ModMenu.Act.Ok); break;
                case UIInputValue.Cancel: ModMenu.Handle(ModMenu.Act.Back); break;
                case UIInputValue.TabLeft: ModMenu.Handle(ModMenu.Act.Up); break;
                case UIInputValue.TabRight: ModMenu.Handle(ModMenu.Act.Down); break;
            }
            return false; // Spiel bekommt die Eingabe nicht, solange das Mod-Menü offen ist
        }

        private static bool CanvasReleasePre() => !ModMenu.IsOpen && !ModMenu.RecentlyClosed;

        private static void PopupPost(string __0)
        {
            if (!ModConfig.On(ModConfig.MenuSpeech)) return;
            SetPrefix(Speech.G(Speech.Clean(__0)));
        }

        private static void NullTabPost(NullBaseMenu __instance)
        {
            if (!ModConfig.On(ModConfig.MenuSpeech)) return;
            var cur = R.Get<TMPro.TextMeshProUGUI>(__instance, "currentTab");
            string t = TextUtil.Tmp(cur);
            if (t.Length > 0) SetPrefix(Loc.T("Reiter ", "Tab ") + Speech.G(t));
        }

        private static void SettingsTabPost(SettingsHelper __instance)
        {
            if (!ModConfig.On(ModConfig.MenuSpeech)) return;
            Scheduler.In(0.12f, () =>
            {
                if (__instance == null) return;
                var lbl = R.Get<TMPro.TMP_Text>(__instance, "tabsLabel");
                string t = TextUtil.Tmp(lbl);
                if (t.Length > 0) { SetPrefix(Loc.T("Reiter ", "Tab ") + Speech.G(t)); Speech.Say(Loc.T("Reiter ", "Tab ") + Speech.G(t)); }
            });
        }

        private static void SettingsOpenPost(SettingsHelper __instance)
        {
            if (!ModConfig.On(ModConfig.MenuSpeech)) return;
            var lbl = R.Get<TMPro.TMP_Text>(__instance, "tabsLabel");
            string t = TextUtil.Tmp(lbl);
            SetPrefix(Loc.T("Optionen", "Settings") + (t.Length > 0 ? Loc.T(", Reiter ", ", tab ") + Speech.G(t) : "") + Loc.T(". Schulter-Tasten wechseln den Reiter", ". Shoulder buttons switch tabs"));
        }

        private static void PauseOpenPost()
        {
            // Das Pausemenü wird beim Spielstart einmal unsichtbar aktiviert: nur im Kampf ansagen
            if (!ModConfig.On(ModConfig.MenuSpeech) || !FightMonitor.InFight) return;
            SetPrefix(Loc.T("Pause", "Pause"));
        }

        private static void StagePreviewPost(IdolShowdown.UI.StageSelect.StagePreviewHandler __instance, IdolShowdown.StageInfo __0)
        {
            if (!ModConfig.On(ModConfig.MenuSpeech)) return;
            Scheduler.In(0.05f, () =>
            {
                string name = TextUtil.Tmp(R.Get<TMPro.TextMeshProUGUI>(__instance, "currentStageName"));
                if (name.Length == 0 && __0 != null) name = Speech.Clean(__0.StageDisplayName);
                string loc = TextUtil.Tmp(R.Get<TMPro.TextMeshProUGUI>(__instance, "currentStageLocation"));
                string s = Loc.T("Bühne ", "Stage ") + Speech.G(name) + (loc.Length > 0 ? ", " + Speech.G(loc) : "");
                Speech.Say(TakePrefix() + s);
                lastDescription = TextUtil.Tmp(R.Get<TMPro.TextMeshProUGUI>(__instance, "stageDescription"));
            });
        }

        private static void StageVariantPost(IdolShowdown.UI.StageSelect.StageSelectController __instance)
        {
            if (!ModConfig.On(ModConfig.MenuSpeech)) return;
            var info = R.Get<IdolShowdown.StageInfo>(__instance, "selectedStageInfo");
            if (info == null || !info.HasStageVariant) return;
            int v = R.Get<int>(__instance, "selectedVariant");
            Speech.Queue(Loc.T("Variante ", "Variant ") + (v + 1));
        }

        private static void DeviceSelectOpenPost()
        {
            if (!ModConfig.On(ModConfig.MenuSpeech)) return;
            Speech.Say(Loc.T("Eingabegeräte zuordnen. Bewege dein Gerät nach links für Spieler 1 oder nach rechts für Spieler 2, dann bestätigen.", "Assign input devices. Move your device left for player 1 or right for player 2, then confirm."));
        }

        private static void DeviceSelectMovePost(string __0)
        {
            if (!ModConfig.On(ModConfig.MenuSpeech)) return;
            string d = (__0 ?? "").ToLowerInvariant();
            if (d.Contains("left")) Speech.Say(Loc.T("Links, Spieler 1", "Left, player 1"));
            else if (d.Contains("right")) Speech.Say(Loc.T("Rechts, Spieler 2", "Right, player 2"));
            else Speech.Say(Loc.T("Mitte, nicht zugeordnet", "Center, unassigned"));
        }

        // ---------------- Kernlogik ----------------

        public static void SetPrefix(string p)
        {
            if (string.IsNullOrEmpty(p)) return;
            pendingPrefix = p;
            pendingPrefixTime = Time.realtimeSinceStartup;
        }

        private static string TakePrefix()
        {
            if (pendingPrefix == null) return "";
            string p = Time.realtimeSinceStartup - pendingPrefixTime < 0.6f ? pendingPrefix + ". " : "";
            pendingPrefix = null;
            return p;
        }

        private static void MarkSelected(GameObject go)
        {
            lastGo = go;
            lastGoTime = Time.realtimeSinceStartup;
        }

        public static void Announce(GameObject go, Selectable sel, ISUIElementsHandler handler, bool marked = false)
        {
            if (go == null || ModMenu.IsOpen) return;
            float now = Time.realtimeSinceStartup;
            if (!AnyMenuSeen) { AnyMenuSeen = true; FirstMenuSeenAt = now; }
            if (!marked)
            {
                if (go == lastGo && now - lastGoTime < 0.3f) { lastGoTime = now; return; }
                MarkSelected(go);
            }
            if (!go.activeInHierarchy) return;
            lastValueSig = ValueText(go, sel);
            if (!ModConfig.On(ModConfig.MenuSpeech)) return;
            if (CharSelect.IsActive && CharSelect.OwnsObject(go)) return; // Charakterauswahl sagt selbst an

            string text = Describe(go, sel, handler, true);
            string context = "";
            if (handler != null && !ReferenceEquals(handler, lastHandler))
            {
                lastHandler = handler;
                if (ModConfig.On(ModConfig.MenuDescriptions)) context = DialogText(handler, go);
            }
            Speech.Say(TakePrefix() + (context.Length > 0 ? Speech.G(context) + ". " : "") + text);
        }

        /// <summary>Beschreibung eines Elements: Beschriftung, Wert, Rolle, Position.</summary>
        public static string Describe(GameObject go, Selectable sel, ISUIElementsHandler handler, bool withPosition)
        {
            if (sel == null) sel = go.GetComponent<Selectable>();
            var sw = go.GetComponentInChildren<SwitchButtonScript>();
            string value = ValueText(go, sel);
            GameObject row = RowRoot(go);
            var texts = TextUtil.Texts(row, 6);
            if (value.Length > 0) texts.Remove(value);
            string label = TextUtil.Join(texts, 250);
            if (label.Length == 0) label = TextUtil.PrettyName(go.name);

            string role = "";
            if (sw != null || sel is Slider) role = Loc.T("Auswahl", "selector");
            else if (sel is Toggle) role = Loc.T("Kontrollkästchen", "checkbox");
            else if (sel is TMPro.TMP_InputField || sel is InputField) role = Loc.T("Eingabefeld", "edit field");

            // Spieltexte (Beschriftung, Umschalter-/Eingabewert) in der Stimme der Spielsprache
            bool gameValue = sw != null || sel is TMPro.TMP_InputField || sel is InputField;
            string s = Speech.G(label);
            if (value.Length > 0) s += ": " + (gameValue ? Speech.G(value) : value);
            if (role.Length > 0) s += ", " + role;
            if (sel != null && !sel.interactable) s += Loc.T(", nicht verfügbar", ", unavailable");
            if (withPosition && handler != null && ModConfig.MenuPosition.Value)
            {
                string pos = Position(handler, sel != null ? sel.gameObject : go);
                if (pos.Length > 0) s += ", " + pos;
            }
            return s;
        }

        /// <summary>Aktueller Wert von Umschaltern/Reglern/Kontrollkästchen/Eingabefeldern.</summary>
        public static string ValueText(GameObject go, Selectable sel)
        {
            try
            {
                var sw = go.GetComponentInChildren<SwitchButtonScript>();
                if (sw != null)
                {
                    // Maßgeblich ist der Wert des Umschalters, nicht der (evtl. animierte) Text.
                    var opts = sw.Options;
                    int v = sw.GetValue();
                    string t = opts != null && v >= 0 && v < opts.Length ? Speech.Clean(opts[v]) : "";
                    if (t.Length == 0) t = TextUtil.Tmp(R.Get<TMPro.TMP_Text>(sw, "text"));
                    if (t.Length == 0) t = v.ToString();
                    return t;
                }
                if (sel == null) sel = go.GetComponent<Selectable>();
                if (sel is Slider sl)
                {
                    if (sl.wholeNumbers) return ((int)sl.value).ToString() + Loc.T(" von ", " of ") + ((int)sl.maxValue);
                    float range = sl.maxValue - sl.minValue;
                    if (range <= 0) return "";
                    return Mathf.RoundToInt((sl.value - sl.minValue) / range * 100f) + Loc.T(" Prozent", " percent");
                }
                if (sel is Toggle tg) return tg.isOn ? Loc.T("an", "on") : Loc.T("aus", "off");
                if (sel is TMPro.TMP_InputField tif) return tif.text.Length > 0 ? tif.text : Loc.T("leer", "empty");
                if (sel is InputField uif) return uif.text.Length > 0 ? uif.text : Loc.T("leer", "empty");
            }
            catch { }
            return "";
        }

        /// <summary>Steigt nach oben, solange der Eltern-Container nur dieses eine Element enthält (Zeile mit Beschriftung).</summary>
        private static GameObject RowRoot(GameObject go)
        {
            Transform t = go.transform;
            for (int i = 0; i < 2; i++)
            {
                var p = t.parent;
                if (p == null) break;
                var sels = p.GetComponentsInChildren<Selectable>(false);
                if (sels.Length != 1) break;
                var texts = p.GetComponentsInChildren<TMPro.TMP_Text>(false);
                if (texts.Length > 5) break;
                t = p;
            }
            return t.gameObject;
        }

        private static string Position(ISUIElementsHandler handler, GameObject go)
        {
            try
            {
                var list = handler.UIElements;
                if (list == null || list.Count < 2) return "";
                int idx = -1, count = 0;
                foreach (var e in list)
                {
                    var el = e.Element;
                    if (el == null || !el.gameObject.activeInHierarchy) continue;
                    count++;
                    if (el.gameObject == go) idx = count;
                }
                if (idx < 1 || count < 2) return "";
                return idx + Loc.T(" von ", " of ") + count;
            }
            catch { return ""; }
        }

        /// <summary>Bei kleinen Dialogen: Fragetext/Überschrift vorlesen, die nicht zu Schaltflächen gehören.</summary>
        private static string DialogText(ISUIElementsHandler handler, GameObject selected)
        {
            try
            {
                var list = handler.UIElements;
                if (list == null || list.Count == 0 || list.Count > 5) return "";
                GameObject root = handler.GetSearchableObject();
                if (root == null) root = handler.gameObject;
                var sels = root.GetComponentsInChildren<Selectable>(false);
                var parts = new List<string>();
                foreach (var t in root.GetComponentsInChildren<TMPro.TMP_Text>(false))
                {
                    if (t == null || !t.enabled || t.color.a < 0.05f) continue;
                    bool inSel = false;
                    foreach (var s in sels) if (s != null && t.transform.IsChildOf(s.transform)) { inSel = true; break; }
                    if (inSel) continue;
                    string x = Speech.Clean(t.text);
                    if (x.Length < 2 || parts.Contains(x)) continue;
                    parts.Add(x);
                    if (parts.Count >= 4) break;
                }
                return TextUtil.Join(parts, 350);
            }
            catch { return ""; }
        }

        /// <summary>Regelmäßig: Wertänderungen des fokussierten Elements und Menü-Beschreibungen.</summary>
        public static void Update()
        {
            if (ModMenu.IsOpen || !ModConfig.On(ModConfig.MenuSpeech)) return;
            float now = Time.realtimeSinceStartup;
            if (lastGo != null && now - LastUIInput < 1.2f)
            {
                if (!lastGo.activeInHierarchy) { lastGo = null; return; }
                // Umschalter und Regler werden direkt über ihre Wertänderung angesagt.
                if (lastGo.GetComponentInChildren<SwitchButtonScript>() != null || lastGo.GetComponent<Slider>() != null) return;
                string v = ValueText(lastGo, null);
                if (v != lastValueSig)
                {
                    lastValueSig = v;
                    if (v.Length > 0) Speech.Say(v);
                }
            }
            WatchMainMenuDescription();
        }

        private static MainMenuController mainMenu;
        private static float nextMainMenuSearch;

        private static void WatchMainMenuDescription()
        {
            if (!ModConfig.MenuDescriptions.Value) return;
            float now = Time.realtimeSinceStartup;
            if (mainMenu == null)
            {
                if (now < nextMainMenuSearch || FightMonitor.InFight) return;
                nextMainMenuSearch = now + 1.5f;
                mainMenu = UnityEngine.Object.FindObjectOfType<MainMenuController>();
                if (mainMenu == null) return;
            }
            if (!mainMenu.isActiveAndEnabled) return;
            try
            {
                var scroller = mainMenu.TextScroller;
                if (scroller == null || scroller.ScrollingText == null) return;
                string d = TextUtil.Tmp(scroller.ScrollingText);
                if (d.Length > 0 && d != lastDescription)
                {
                    lastDescription = d;
                    // Nicht unterbrechen: wird nach dem Menüpunkt gesprochen.
                    // Nur sprechen, wenn die Beschreibung dann noch aktuell ist (kein Nachhall vom alten Menüpunkt).
                    Scheduler.In(0.15f, () => { if (d == lastDescription) Speech.Queue(Speech.G(d)); });
                }
            }
            catch { }
        }

        /// <summary>Details: aktuelles Element erneut mit Beschreibung vorlesen.</summary>
        public static void SpeakDetails()
        {
            if (lastGo != null && lastGo.activeInHierarchy)
            {
                string s = Describe(lastGo, null, null, false);
                if (!string.IsNullOrEmpty(lastDescription)) s += ". " + Speech.G(lastDescription);
                Speech.Say(s);
            }
            else if (!string.IsNullOrEmpty(lastDescription)) Speech.Say(Speech.G(lastDescription));
            else Speech.RepeatLast();
        }
    }
}
