// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using System;
using System.Collections.Generic;
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
        [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder name, ref int size);
        [DllImport("iphlpapi.dll", SetLastError = true)] public static extern uint GetExtendedTcpTable(IntPtr table, ref int size, bool sort, int family, int tableClass, uint reserved);
        public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        // Executable path of another process without needing to open it fully (works for most non-protected processes).
        public static string ProcessPath(int pid)
        {
            IntPtr h = OpenProcess(0x1000, false, pid);   // PROCESS_QUERY_LIMITED_INFORMATION
            if (h == IntPtr.Zero) return null;
            try
            {
                StringBuilder sb = new StringBuilder(1024); int size = sb.Capacity;
                return QueryFullProcessImageName(h, 0, sb, ref size) ? sb.ToString(0, size) : null;
            }
            finally { CloseHandle(h); }
        }

        public sealed class TcpConnection { public int Pid; public System.Net.IPAddress Remote; public int RemotePort; }

        // Established TCP connections (IPv4 and IPv6) with their owning process, from the numeric IP Helper
        // table: no netstat text, so the state is never a localized word.
        public static List<TcpConnection> EstablishedTcp()
        {
            List<TcpConnection> list = new List<TcpConnection>();
            foreach (int family in new[] { 2, 23 })
            {
                int size = 0;
                GetExtendedTcpTable(IntPtr.Zero, ref size, false, family, 5, 0);   // TCP_TABLE_OWNER_PID_ALL
                if (size <= 0) continue;
                IntPtr buffer = Marshal.AllocHGlobal(size);
                try
                {
                    if (GetExtendedTcpTable(buffer, ref size, false, family, 5, 0) != 0) continue;
                    int count = Marshal.ReadInt32(buffer);
                    int rowSize = family == 2 ? 24 : 56;
                    for (int i = 0; i < count; i++)
                    {
                        IntPtr row = IntPtr.Add(buffer, 4 + i * rowSize);
                        int state, pid, port; byte[] addr;
                        if (family == 2)
                        {
                            state = Marshal.ReadInt32(row); addr = BitConverter.GetBytes(Marshal.ReadInt32(row, 12));
                            port = PortFrom(Marshal.ReadInt32(row, 16)); pid = Marshal.ReadInt32(row, 20);
                        }
                        else
                        {
                            addr = new byte[16]; Marshal.Copy(IntPtr.Add(row, 24), addr, 0, 16);
                            port = PortFrom(Marshal.ReadInt32(row, 44)); state = Marshal.ReadInt32(row, 48); pid = Marshal.ReadInt32(row, 52);
                        }
                        if (state == 5) list.Add(new TcpConnection { Pid = pid, Remote = new System.Net.IPAddress(addr), RemotePort = port });   // MIB_TCP_STATE_ESTAB
                    }
                }
                finally { Marshal.FreeHGlobal(buffer); }
            }
            return list;
        }

        [DllImport("iphlpapi.dll")] public static extern int GetIpNetTable(IntPtr table, ref int size, bool sort);

        // IPv4 neighbour (ARP) cache: IP -> MAC for devices this PC has talked to; numeric, never `arp -a` text.
        public static Dictionary<string, string> ArpTable()
        {
            Dictionary<string, string> map = new Dictionary<string, string>();
            int size = 0;
            GetIpNetTable(IntPtr.Zero, ref size, false);
            if (size <= 0) return map;
            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                if (GetIpNetTable(buffer, ref size, false) != 0) return map;
                int count = Marshal.ReadInt32(buffer);
                for (int i = 0; i < count; i++)
                {
                    IntPtr row = IntPtr.Add(buffer, 4 + i * 24);   // MIB_IPNETROW
                    int macLen = Marshal.ReadInt32(row, 4), type = Marshal.ReadInt32(row, 20);
                    if (type == 2 || macLen < 6) continue;          // 2 = invalid entry
                    byte[] mac = new byte[6]; Marshal.Copy(IntPtr.Add(row, 8), mac, 0, 6);
                    if (mac[0] == 0xFF || (mac[0] | mac[1] | mac[2] | mac[3] | mac[4] | mac[5]) == 0) continue;
                    string ip = new System.Net.IPAddress(BitConverter.GetBytes(Marshal.ReadInt32(row, 16))).ToString();
                    map[ip] = BitConverter.ToString(mac).Replace('-', ':');
                }
            }
            finally { Marshal.FreeHGlobal(buffer); }
            return map;
        }

        private static int PortFrom(int raw) { return ((raw & 0xFF) << 8) | ((raw >> 8) & 0xFF); }

        // Processes that own at least one visible, titled top-level window.
        public static HashSet<int> PidsWithVisibleWindow()
        {
            HashSet<int> pids = new HashSet<int>();
            EnumWindows(delegate(IntPtr hwnd, IntPtr p)
            {
                if (IsWindowVisible(hwnd) && GetWindowTextLength(hwnd) > 0) { uint pid; GetWindowThreadProcessId(hwnd, out pid); pids.Add((int)pid); }
                return true;
            }, IntPtr.Zero);
            return pids;
        }
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
