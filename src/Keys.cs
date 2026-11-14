using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace IdolShowdownAccess
{
    /// <summary>
    /// Tastatur per Abfrage (GetAsyncKeyState) statt über Unitys Eingabe: funktioniert auch,
    /// wenn JAWS Tasten abfängt und als injiziert weiterreicht. Nur aktiv, solange das
    /// Spielfenster im Vordergrund ist. Controller-Tasten kommen weiter über Unity.
    /// </summary>
    internal static class Keys
    {
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentProcessId();

        public const int VK_BACK = 0x08, VK_TAB = 0x09, VK_RETURN = 0x0D, VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12,
            VK_ESCAPE = 0x1B, VK_SPACE = 0x20, VK_END = 0x23, VK_HOME = 0x24, VK_LEFT = 0x25, VK_UP = 0x26, VK_RIGHT = 0x27, VK_DOWN = 0x28,
            VK_F1 = 0x70;

        private static readonly bool[] down = new bool[256];
        private static readonly bool[] pressed = new bool[256];
        private static readonly bool[] released = new bool[256];
        private static bool usable = true;
        private static uint myPid;
        private static int lastFrame = -1;
        private static bool ctrlClean;

        /// <summary>true, wenn Strg allein (ohne weitere Taste) gerade gedrückt wurde.</summary>
        public static bool CtrlAlonePressed { get; private set; }

        public static bool Focused { get; private set; }

        /// <summary>Einmal pro Frame aufrufen.</summary>
        public static void Poll()
        {
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            CtrlAlonePressed = false;
            if (!usable) { FallbackUnity(); return; }
            try
            {
                if (myPid == 0) myPid = GetCurrentProcessId();
                GetWindowThreadProcessId(GetForegroundWindow(), out uint pid);
                Focused = pid == myPid;
                for (int vk = 1; vk < 256; vk++)
                {
                    bool d = Focused && (GetAsyncKeyState(vk) & 0x8000) != 0;
                    pressed[vk] = d && !down[vk];
                    released[vk] = !d && down[vk];
                    down[vk] = d;
                }
                // Strg allein: beim Drücken sofort auslösen, solange keine andere Taste dazukommt
                if (pressed[VK_CONTROL] || pressed[0xA2] || pressed[0xA3])
                {
                    ctrlClean = true;
                    for (int vk = 0x08; vk < 256; vk++)
                    {
                        if (vk == VK_CONTROL || vk == 0xA2 || vk == 0xA3 || vk == VK_SHIFT || vk == 0xA0 || vk == 0xA1) continue;
                        if (down[vk]) { ctrlClean = false; break; }
                    }
                    if (ctrlClean && !down[VK_MENU]) CtrlAlonePressed = true;
                }
            }
            catch (Exception e)
            {
                usable = false;
                Plugin.Log.LogWarning("Tastaturabfrage nicht verfügbar, nutze Unity-Eingabe: " + e.Message);
            }
        }

        private static void FallbackUnity()
        {
            Focused = Application.isFocused;
            if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl)) CtrlAlonePressed = true;
        }

        public static bool Down(int vk) => usable ? down[vk & 0xFF] : false;

        /// <summary>Taste gerade gedrückt (Flanke). F-Tasten nur ohne Alt (Alt+F4 ist kein F4).</summary>
        public static bool Pressed(int vk)
        {
            if (!usable) return FallbackPressed(vk);
            if (!pressed[vk & 0xFF]) return false;
            if (vk >= VK_F1 && vk <= VK_F1 + 11 && down[VK_MENU]) return false;
            return true;
        }

        public static bool Shift => usable ? down[VK_SHIFT] : (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));

        public static int F(int n) => VK_F1 + n - 1;

        private static bool FallbackPressed(int vk)
        {
            KeyCode k;
            if (vk >= VK_F1 && vk <= VK_F1 + 11) k = KeyCode.F1 + (vk - VK_F1);
            else switch (vk)
                {
                    case VK_UP: k = KeyCode.UpArrow; break;
                    case VK_DOWN: k = KeyCode.DownArrow; break;
                    case VK_LEFT: k = KeyCode.LeftArrow; break;
                    case VK_RIGHT: k = KeyCode.RightArrow; break;
                    case VK_RETURN: k = KeyCode.Return; break;
                    case VK_SPACE: k = KeyCode.Space; break;
                    case VK_ESCAPE: k = KeyCode.Escape; break;
                    case VK_BACK: k = KeyCode.Backspace; break;
                    case VK_HOME: k = KeyCode.Home; break;
                    case VK_END: k = KeyCode.End; break;
                    default: return false;
                }
            try { return Input.GetKeyDown(k); } catch { return false; }
        }
    }
}
