using FishingFun;
using log4net;
using log4net.Repository.Hierarchy;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

#nullable enable

namespace FishingFun
{
    public static class WowProcess
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)] // Addition for Shift autoloot
        public static extern void keybd_event(uint bVk, uint bScan, uint dwFlags, uint dwExtraInfo);     // Addition for Shift autoloot
        public static ILog logger = LogManager.GetLogger("Fishbot");

        private const UInt32 WM_KEYDOWN = 0x0100;
        private const UInt32 WM_KEYUP = 0x0101;
        private static ConsoleKey lastKey;
        private static Random random = new Random();
        public static int LootDelay = 1000;

        // selectable loot method
        public enum LootMethod
        {
            LiamCooper, // current implementation that toggles shift during right-click
            NoShift     // right-click without modifying shift
        }

        // default loot method
        public static LootMethod LootMethodSetting = LootMethod.LiamCooper;

        //Get the wow-process, if success returns the process else null
        public static Process? Get(string name = "")
        {
            var names = string.IsNullOrEmpty(name) ? new List<string> { "Wow", "WowClassic", "Wow-64", "World of Warcraft" } : new List<string> { name };

            var processList = Process.GetProcesses();
            foreach (var p in processList)
            {
                if (names.Any(name => string.Equals(name, p.ProcessName, StringComparison.OrdinalIgnoreCase)))
                {
                    return p;
                }
            }

            logger.Error($"Failed to find the wow process, tried: {string.Join(", ", names)}");

            return null;
        }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, UInt32 Msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindowThreadProcessId(IntPtr hWnd, out uint ProcessId);

        private static Process GetActiveProcess()
        {
            IntPtr hwnd = GetForegroundWindow();
            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);
            return Process.GetProcessById((int)pid);
        }

        private static void KeyDown(ConsoleKey key)
        {
            lastKey = key;
            var wowProcess = Get();
            if (wowProcess != null)
            {
                PostMessage(wowProcess.MainWindowHandle, WM_KEYDOWN, (int)key, 0);
            }
        }

        private static void KeyUp()
        {
            KeyUp(lastKey);
        }

        public static void PressKey(ConsoleKey key)
        {
            KeyDown(key);
            Thread.Sleep(30 + random.Next(0, 50));
            KeyUp(key);
        }

        public static void KeyUp(ConsoleKey key)
        {
            var wowProcess = Get();
            if (wowProcess != null)
            {
                PostMessage(wowProcess.MainWindowHandle, WM_KEYUP, (int)key, 0);
            }
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetCursorPos(int x, int y);

        // public entry - delegates to the currently selected loot method (option available in UI)
        public static void RightClickMouse(ILog logger, System.Drawing.Point position)
        {
            switch (LootMethodSetting)
            {
                case LootMethod.NoShift:
                    RightClickMouse_NoShift(logger, position);
                    break;
                case LootMethod.LiamCooper:
                default:
                    RightClickMouse_LiamCooper(logger, position);
                    break;
            }
        }

        // Helper: ensure WoW window is foreground and cursor set; returns false if any step fails
        private static bool PrepareWindowAndCursor(Process? wowProcess, Point position, ILog? logger)
        {
            if (wowProcess == null || wowProcess.MainWindowHandle == IntPtr.Zero) { logger?.Warn("Cannot loot: the WoW window was not found."); return false; }
            var wowWindow = wowProcess.MainWindowHandle;
            var activationRequested = SetForegroundWindow(wowWindow);
            Thread.Sleep(100);
            var foregroundWindow = GetForegroundWindow();
            uint foregroundProcessId = 0;
            if (foregroundWindow != IntPtr.Zero) { GetWindowThreadProcessId(foregroundWindow, out foregroundProcessId); }
            if (foregroundProcessId != (uint)wowProcess.Id)
            {
                logger?.Warn($"Cannot loot: WoW is not foreground. SetForegroundWindow returned {activationRequested}; foreground process ID is {foregroundProcessId}, expected {wowProcess.Id}.");
                return false;
            }
            if (!SetCursorPos(position.X, position.Y)) { logger?.Warn($"Cannot move the cursor to {position}."); return false; }
            return true;
        }

        // original implementation (presses shift down during right click)
        public static void RightClickMouse_LiamCooper(ILog logger, Point position)
        {
            var wowProcess = Get();
            if (!PrepareWindowAndCursor(wowProcess, position, logger)) return;

            keybd_event(0xA0, 0, 0, 0); // LEFT SHIFT DOWN
            mouse_event((int)MouseEventFlags.RightDown, position.X, position.Y, 0, 0);
            try { Thread.Sleep(30 + random.Next(0, 50)); }
            finally
            {
                mouse_event((int)MouseEventFlags.RightUp, position.X, position.Y, 0, 0);
                keybd_event(0xA0, 0, 0x0002, 0); // LEFT SHIFT RELEASE
            }
            Thread.Sleep(500);
        }

        // alternate implementation: right click without touching shift key
        public static void RightClickMouse_NoShift(ILog logger, Point position)
        {
            var wowProcess = Get();
            if (!PrepareWindowAndCursor(wowProcess, position, logger)) return;

            mouse_event((int)MouseEventFlags.RightDown, position.X, position.Y, 0, 0);
            try { Thread.Sleep(30 + random.Next(0, 50)); }
            finally { mouse_event((int)MouseEventFlags.RightUp, position.X, position.Y, 0, 0); }
            Thread.Sleep(500);
        }

        [DllImport("user32.dll")]
        private static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);

        [Flags]
        public enum MouseEventFlags
        {
            LeftDown = 0x00000002,
            LeftUp = 0x00000004,
            MiddleDown = 0x00000020,
            MiddleUp = 0x00000040,
            Move = 0x00000001,
            Absolute = 0x00008000,
            RightDown = 0x00000008,
            RightUp = 0x00000010
        }
    }
}