// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace ProjectMantis
{
    internal struct SystemMetrics { public double Cpu; public double Ram; public double DiskFree; }

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
            return new SystemMetrics { Cpu = Math.Max(0, Math.Min(100, cpu)), Ram = ms.dwMemoryLoad, DiskFree = drive.TotalSize == 0 ? 0 : 100.0 * drive.AvailableFreeSpace / drive.TotalSize };
        }
        public static long UsedMemoryBytes() { Native.MemoryStatus ms = new Native.MemoryStatus(); ms.dwLength = (uint)Marshal.SizeOf(ms); Native.GlobalMemoryStatusEx(ref ms); return (long)(ms.ullTotalPhys - ms.ullAvailPhys); }
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
