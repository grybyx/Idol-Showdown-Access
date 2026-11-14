using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace IdolShowdownAccess
{
    /// <summary>Zugriff auf private Felder/Methoden mit Cache.</summary>
    internal static class R
    {
        private static readonly Dictionary<string, FieldInfo> fields = new Dictionary<string, FieldInfo>();
        private static readonly Dictionary<string, MethodInfo> methods = new Dictionary<string, MethodInfo>();

        public static FieldInfo Field(Type t, string name)
        {
            if (t == null) return null;
            string key = t.FullName + "::" + name;
            if (fields.TryGetValue(key, out var fi)) return fi;
            fi = AccessTools.Field(t, name);
            fields[key] = fi;
            if (fi == null) Plugin.Log.LogWarning("Feld nicht gefunden: " + key);
            return fi;
        }

        public static T Get<T>(object o, string name)
        {
            if (o == null) return default(T);
            try
            {
                var fi = Field(o.GetType(), name);
                if (fi == null) return default(T);
                object v = fi.GetValue(o);
                if (v is T tv) return tv;
                return default(T);
            }
            catch { return default(T); }
        }

        public static object Call(object o, string name, params object[] args)
        {
            if (o == null) return null;
            try
            {
                Type t = o.GetType();
                string key = t.FullName + "::" + name + "/" + args.Length;
                if (!methods.TryGetValue(key, out var mi))
                {
                    foreach (var m in AccessTools.GetDeclaredMethods(t))
                        if (m.Name == name && m.GetParameters().Length == args.Length) { mi = m; break; }
                    if (mi == null) mi = AccessTools.Method(t, name);
                    methods[key] = mi;
                }
                return mi?.Invoke(o, args);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Aufruf fehlgeschlagen " + name + ": " + e.Message);
                return null;
            }
        }
    }

    /// <summary>Robustes Patchen: ein fehlender Einhakpunkt bricht nicht den ganzen Mod.</summary>
    internal static class Patcher
    {
        public static Harmony H;
        public static int Ok, Failed;

        public static void Patch(Type target, string method, Type host, string prefix = null, string postfix = null, Type[] args = null)
        {
            try
            {
                MethodInfo orig = args == null ? AccessTools.Method(target, method) : AccessTools.Method(target, method, args);
                if (orig == null)
                {
                    Failed++;
                    Plugin.Log.LogWarning("Methode nicht gefunden: " + target?.FullName + "." + method);
                    return;
                }
                HarmonyMethod pre = prefix != null ? new HarmonyMethod(AccessTools.Method(host, prefix)) : null;
                HarmonyMethod post = postfix != null ? new HarmonyMethod(AccessTools.Method(host, postfix)) : null;
                H.Patch(orig, pre, post);
                Ok++;
            }
            catch (Exception e)
            {
                Failed++;
                Plugin.Log.LogError("Patch fehlgeschlagen " + target?.FullName + "." + method + ": " + e.Message);
            }
        }
    }

    /// <summary>Einfacher Zeitplaner für verzögerte Ansagen (läuft im Hauptthread).</summary>
    internal static class Scheduler
    {
        private struct Job { public float At; public Action A; }
        private static readonly List<Job> jobs = new List<Job>();

        public static void In(float seconds, Action a)
        {
            jobs.Add(new Job { At = Time.realtimeSinceStartup + seconds, A = a });
        }

        public static void Update()
        {
            if (jobs.Count == 0) return;
            float now = Time.realtimeSinceStartup;
            for (int i = 0; i < jobs.Count; i++)
            {
                if (jobs[i].At <= now)
                {
                    var a = jobs[i].A;
                    jobs.RemoveAt(i);
                    i--;
                    try { a(); } catch (Exception e) { Plugin.Log.LogWarning("Geplante Aktion: " + e.Message); }
                }
            }
        }
    }

    internal static class TextUtil
    {
        /// <summary>Alle sichtbaren Texte (TextMeshPro und Unity-Text) unter einem Objekt.</summary>
        public static List<string> Texts(GameObject root, int max = 8, Transform exclude = null)
        {
            var list = new List<string>();
            if (root == null) return list;
            try
            {
                foreach (var t in root.GetComponentsInChildren<TMPro.TMP_Text>(false))
                {
                    if (t == null || !t.enabled) continue;
                    if (exclude != null && t.transform.IsChildOf(exclude)) continue;
                    if (t.color.a < 0.05f) continue;
                    string s = Speech.Clean(t.text);
                    if (s.Length == 0 || list.Contains(s)) continue;
                    list.Add(s);
                    if (list.Count >= max) break;
                }
                if (list.Count == 0)
                {
                    foreach (var t in root.GetComponentsInChildren<UnityEngine.UI.Text>(false))
                    {
                        if (t == null || !t.enabled) continue;
                        string s = Speech.Clean(t.text);
                        if (s.Length == 0 || list.Contains(s)) continue;
                        list.Add(s);
                        if (list.Count >= max) break;
                    }
                }
            }
            catch { }
            return list;
        }

        public static string Join(List<string> parts, int maxChars = 400)
        {
            var sb = new StringBuilder();
            foreach (var p in parts)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(p);
                if (sb.Length > maxChars) break;
            }
            return sb.ToString();
        }

        /// <summary>Macht aus Objektnamen wie "ExitButton_02" lesbaren Text.</summary>
        public static string PrettyName(string n)
        {
            if (string.IsNullOrEmpty(n)) return "";
            n = n.Replace("(Clone)", "");
            var sb = new StringBuilder();
            for (int i = 0; i < n.Length; i++)
            {
                char c = n[i];
                if (c == '_' || c == '-') { sb.Append(' '); continue; }
                if (i > 0 && char.IsUpper(c) && char.IsLower(n[i - 1])) sb.Append(' ');
                sb.Append(c);
            }
            string s = sb.ToString();
            foreach (var w in new[] { " Button", "Button ", "Btn", " Obj", "Panel" })
                s = s.Replace(w, " ");
            return s.Trim();
        }

        public static string Tmp(TMPro.TMP_Text t)
        {
            if (t == null) return "";
            try { return Speech.Clean(t.text); } catch { return ""; }
        }
    }
}
