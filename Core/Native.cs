// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ProjectMantis
{
    internal static class Native
    {
        [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        [DllImport("psapi.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool EmptyWorkingSet(IntPtr process);
        [DllImport("kernel32.dll")] public static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);
        [DllImport("kernel32.dll")] public static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
        [DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(uint flags);
        [DllImport("kernel32.dll")] public static extern uint GetOEMCP();
        [DllImport("kernel32.dll")] public static extern ulong GetTickCount64();
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int count);
        [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out WindowRect rect);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")] public static extern IntPtr GetWindowLongPtr64(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "GetWindowLong")] public static extern IntPtr GetWindowLongPtr32(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")] public static extern IntPtr SetWindowLongPtr64(IntPtr hwnd, int index, IntPtr value);
        [DllImport("user32.dll", EntryPoint = "SetWindowLong")] public static extern IntPtr SetWindowLongPtr32(IntPtr hwnd, int index, IntPtr value);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint colorKey, byte alpha, uint flags);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
        public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
        public static IntPtr GetWindowStyle(IntPtr hwnd) { return IntPtr.Size == 8 ? GetWindowLongPtr64(hwnd, -20) : GetWindowLongPtr32(hwnd, -20); }
        public static IntPtr SetWindowStyle(IntPtr hwnd, IntPtr value) { return IntPtr.Size == 8 ? SetWindowLongPtr64(hwnd, -20, value) : SetWindowLongPtr32(hwnd, -20, value); }
        [StructLayout(LayoutKind.Sequential)] public struct WindowRect { public int Left; public int Top; public int Right; public int Bottom; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)] public struct MemoryStatus { public uint dwLength; public uint dwMemoryLoad; public ulong ullTotalPhys; public ulong ullAvailPhys; public ulong ullTotalPageFile; public ulong ullAvailPageFile; public ulong ullTotalVirtual; public ulong ullAvailVirtual; public ulong ullAvailExtendedVirtual; }
        [StructLayout(LayoutKind.Sequential)] public struct NativePoint { public int X; public int Y; }
        [StructLayout(LayoutKind.Sequential)] public struct MinMaxInfo { public NativePoint Reserved; public NativePoint MaxSize; public NativePoint MaxPosition; public NativePoint MinTrackSize; public NativePoint MaxTrackSize; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] public struct MonitorInfo { public int cbSize; public WindowRect Monitor; public WindowRect Work; public uint Flags; }

        // Keeps a borderless maximized window inside the monitor work area so it never covers the taskbar.
        public static void FitMaximizedToWorkArea(IntPtr hwnd, IntPtr lParam)
        {
            IntPtr monitor = MonitorFromWindow(hwnd, 2);
            if (monitor == IntPtr.Zero) return;
            MonitorInfo info = new MonitorInfo(); info.cbSize = Marshal.SizeOf(typeof(MonitorInfo));
            if (!GetMonitorInfo(monitor, ref info)) return;
            MinMaxInfo mmi = (MinMaxInfo)Marshal.PtrToStructure(lParam, typeof(MinMaxInfo));
            mmi.MaxPosition.X = info.Work.Left - info.Monitor.Left;
            mmi.MaxPosition.Y = info.Work.Top - info.Monitor.Top;
            mmi.MaxSize.X = info.Work.Right - info.Work.Left;
            mmi.MaxSize.Y = info.Work.Bottom - info.Work.Top;
            Marshal.StructureToPtr(mmi, lParam, true);
        }

        public static int ColorRef(System.Windows.Media.Color c) { return c.R | (c.G << 8) | (c.B << 16); }
    }
}
