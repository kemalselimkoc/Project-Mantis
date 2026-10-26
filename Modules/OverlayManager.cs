// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace ProjectMantis
{
    internal sealed class WindowTarget
    {
        public IntPtr Handle; public string Title; public string ProcessName; public override string ToString() { return Title + "   ·   " + ProcessName; }
    }

    internal static class OverlayManager
    {
        private static IntPtr active;
        private static IntPtr originalStyle;
        private static bool clickThrough;
        private const long WS_EX_TRANSPARENT = 0x20;
        private const long WS_EX_LAYERED = 0x80000;
        private const uint SWP_NOMOVE = 0x0002, SWP_NOSIZE = 0x0001, SWP_NOACTIVATE = 0x0010, SWP_FRAMECHANGED = 0x0020;
        private static readonly HashSet<string> Browsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "chrome", "msedge", "firefox", "brave", "opera", "opera_gx", "vivaldi" };
        private static readonly HashSet<string> MediaPlayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "vlc", "mpv", "wmplayer", "potplayermini64", "kodi" };

        public static List<WindowTarget> ListWindows(int ownProcess)
        {
            List<WindowTarget> result = new List<WindowTarget>();
            Native.EnumWindows(delegate(IntPtr hwnd, IntPtr p)
            {
                if (!Native.IsWindowVisible(hwnd)) return true;
                int length = Native.GetWindowTextLength(hwnd); if (length < 2) return true;
                uint pid; Native.GetWindowThreadProcessId(hwnd, out pid); if (pid == ownProcess) return true;
                string processName; try { processName = Process.GetProcessById((int)pid).ProcessName; } catch { return true; }
                if (!Browsers.Contains(processName) && !MediaPlayers.Contains(processName)) return true;
                if (Browsers.Contains(processName))
                {
                    Native.WindowRect rect; if (!Native.GetWindowRect(hwnd, out rect)) return true;
                    int width = Math.Max(0, rect.Right - rect.Left), height = Math.Max(0, rect.Bottom - rect.Top);
                    StringBuilder titleProbe = new StringBuilder(length + 1); Native.GetWindowText(hwnd, titleProbe, titleProbe.Capacity); string lowerTitle = titleProbe.ToString().ToLowerInvariant();
                    bool namedPip = lowerTitle.Contains("picture in picture") || lowerTitle.Contains("picture-in-picture") || lowerTitle.Contains("resim içinde resim") || lowerTitle.Contains("pip");
                    bool compactVideoShape = width >= 220 && height >= 120 && width <= 960 && height <= 720 && width > height;
                    if (!namedPip && !compactVideoShape) return true;
                }
                StringBuilder text = new StringBuilder(length + 1); Native.GetWindowText(hwnd, text, text.Capacity);
                string title = text.ToString().Trim(); if (title.Length > 0) result.Add(new WindowTarget { Handle = hwnd, Title = title, ProcessName = processName }); return true;
            }, IntPtr.Zero);
            return result.OrderBy(x => x.Title).ToList();
        }

        public static bool Apply(IntPtr hwnd, byte opacity)
        {
            if (hwnd == IntPtr.Zero) return false; Restore(); active = hwnd; originalStyle = Native.GetWindowStyle(hwnd);
            long style = originalStyle.ToInt64() | WS_EX_TRANSPARENT | WS_EX_LAYERED;
            Native.SetWindowStyle(hwnd, new IntPtr(style)); clickThrough = true;
            Native.SetLayeredWindowAttributes(hwnd, 0, opacity, 0x00000002);
            return Native.SetWindowPos(hwnd, new IntPtr(-1), 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED);
        }

        public static void Restore()
        {
            if (active == IntPtr.Zero) return;
            Native.SetWindowStyle(active, originalStyle);
            Native.SetWindowPos(active, new IntPtr(-2), 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED);
            active = IntPtr.Zero; originalStyle = IntPtr.Zero; clickThrough = false;
        }

        public static bool ToggleInputLock()
        {
            if (active == IntPtr.Zero) return false;
            long style = Native.GetWindowStyle(active).ToInt64();
            if (clickThrough) style &= ~WS_EX_TRANSPARENT; else style |= WS_EX_TRANSPARENT;
            Native.SetWindowStyle(active, new IntPtr(style));
            Native.SetWindowPos(active, new IntPtr(-1), 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED);
            clickThrough = !clickThrough; return clickThrough;
        }
    }
}
