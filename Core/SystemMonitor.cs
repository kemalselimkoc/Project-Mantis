// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace ProjectMantis
{
    internal struct SystemMetrics
    {
        public double Cpu; public double Ram; public double DiskFree;
        public long RamUsedBytes; public long RamTotalBytes; public long DiskFreeBytes; public long DiskTotalBytes;
    }

    internal sealed class SystemMonitor
    {
        private ulong prevIdle, prevKernel, prevUser;
        public SystemMetrics Read()
        {
            ulong idle, kernel, user; Native.GetSystemTimes(out idle, out kernel, out user);
            ulong idleDelta = idle - prevIdle, total = (kernel - prevKernel) + (user - prevUser);
            double cpu = prevKernel == 0 || total == 0 ? 0 : 100.0 * (total - idleDelta) / total;
            prevIdle = idle; prevKernel = kernel; prevUser = user;
            Native.MemoryStatus ms = new Native.MemoryStatus(); ms.dwLength = (uint)Marshal.SizeOf(ms); Native.GlobalMemoryStatusEx(ref ms);
            DriveInfo drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));
            return new SystemMetrics
            {
                Cpu = Math.Max(0, Math.Min(100, cpu)), Ram = ms.dwMemoryLoad, DiskFree = drive.TotalSize == 0 ? 0 : 100.0 * drive.AvailableFreeSpace / drive.TotalSize,
                RamUsedBytes = (long)(ms.ullTotalPhys - ms.ullAvailPhys), RamTotalBytes = (long)ms.ullTotalPhys, DiskFreeBytes = drive.AvailableFreeSpace, DiskTotalBytes = drive.TotalSize
            };
        }
        public static long UsedMemoryBytes() { Native.MemoryStatus ms = new Native.MemoryStatus(); ms.dwLength = (uint)Marshal.SizeOf(ms); Native.GlobalMemoryStatusEx(ref ms); return (long)(ms.ullTotalPhys - ms.ullAvailPhys); }

        public static TimeSpan Uptime() { try { return TimeSpan.FromMilliseconds(Native.GetTickCount64()); } catch { return TimeSpan.Zero; } }

        // "Windows 11 · 24H2". ProductName still says "Windows 10" on Windows 11, so the build number decides.
        public static string OsLabel()
        {
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    int build; int.TryParse(Convert.ToString(k.GetValue("CurrentBuildNumber")), out build);
                    string name = build >= 22000 ? "Windows 11" : build > 0 ? "Windows 10" : "Windows";
                    string release = Convert.ToString(k.GetValue("DisplayVersion") ?? k.GetValue("ReleaseId"));
                    return string.IsNullOrEmpty(release) ? name : name + " · " + release;
                }
            }
            catch { return "Windows"; }
        }
    }

    internal static class MemoryOptimizer
    {
        public static int TrimProcesses()
        {
            int count = 0;
            foreach (Process p in Process.GetProcesses())
            {
                try
                {
                    if (p.Id == Process.GetCurrentProcess().Id || p.HasExited) continue;
                    if (Native.EmptyWorkingSet(p.Handle)) count++;
                }
                catch { }
                finally { p.Dispose(); }
            }
            GC.Collect(); GC.WaitForPendingFinalizers(); return count;
        }
    }
}
