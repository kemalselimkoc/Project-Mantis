// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace ProjectMantis
{
    internal sealed class AdapterInfo { public string Name, Description, Ipv4, Ipv6, Gateway, Dns, Mac; public long Speed; public bool Wireless; }
    internal sealed class FileHit { public string Path; public long Size; }
    internal sealed class FolderScan
    {
        public string Root; public long Total; public int Files; public int Skipped;
        public List<FileHit> Largest = new List<FileHit>();
        public List<FileHit> Folders = new List<FileHit>();              // immediate subfolders by total size
        public List<List<FileHit>> Duplicates = new List<List<FileHit>>(); // identical content groups, biggest waste first
        public long DuplicateWaste;
    }
    internal sealed class PowerPlan { public string Guid, Name; public bool Active; }

    // Quick Tools (spec B.5): small read-mostly helpers. Nothing here changes the system without a confirmed click.
    internal static class QuickTools
    {
        // ---------- Network ----------

        public static List<AdapterInfo> Adapters()
        {
            List<AdapterInfo> list = new List<AdapterInfo>();
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback || ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                    IPInterfaceProperties p = ni.GetIPProperties();
                    string v4 = string.Join(", ", p.UnicastAddresses.Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork).Select(a => a.Address.ToString()));
                    if (string.IsNullOrEmpty(v4)) continue;
                    byte[] mac = ni.GetPhysicalAddress().GetAddressBytes();
                    list.Add(new AdapterInfo
                    {
                        Name = ni.Name, Description = ni.Description, Ipv4 = v4, Speed = ni.Speed,
                        Ipv6 = string.Join(", ", p.UnicastAddresses.Where(a => a.Address.AddressFamily == AddressFamily.InterNetworkV6 && !a.Address.IsIPv6LinkLocal).Select(a => a.Address.ToString())),
                        Gateway = string.Join(", ", p.GatewayAddresses.Where(g => !g.Address.Equals(IPAddress.Any)).Select(g => g.Address.ToString())),
                        Dns = string.Join(", ", p.DnsAddresses.Where(d => d.AddressFamily == AddressFamily.InterNetwork).Select(d => d.ToString())),
                        Mac = string.Join("-", mac.Select(b => b.ToString("X2"))),
                        Wireless = ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211
                    });
                }
            }
            catch { }
            return list.OrderBy(a => string.IsNullOrEmpty(a.Gateway) ? 1 : 0).ToList();
        }

        // Only on an explicit click: asks api.ipify.org (plain text, no key, nothing sent but the request itself).
        public static string PublicIp()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;   // TLS 1.2 on .NET 4.8 defaults
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create("https://api.ipify.org");
                req.Timeout = 8000; req.UserAgent = "ProjectMantis/" + AppInfo.Version;
                using (WebResponse r = req.GetResponse())
                using (StreamReader sr = new StreamReader(r.GetResponseStream()))
                {
                    string ip = sr.ReadToEnd().Trim(); IPAddress parsed;
                    return IPAddress.TryParse(ip, out parsed) ? parsed.ToString() : null;
                }
            }
            catch { return null; }
        }

        // ---------- Large & duplicate files ----------

        public static FolderScan ScanFolder(string root, int top, bool duplicates, CancellationToken token, Action<int> progress)
        {
            FolderScan s = new FolderScan { Root = root };
            List<FileHit> largest = new List<FileHit>();
            Dictionary<long, List<string>> bySize = new Dictionary<long, List<string>>();
            Dictionary<string, long> folders = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            Stack<string> dirs = new Stack<string>(); dirs.Push(root);
            while (dirs.Count > 0)
            {
                token.ThrowIfCancellationRequested();
                string dir = dirs.Pop();
                DirectoryInfo di = new DirectoryInfo(dir);
                FileSystemInfo[] items;
                try { items = di.GetFileSystemInfos(); } catch { s.Skipped++; continue; }
                foreach (FileSystemInfo item in items)
                {
                    try
                    {
                        if ((item.Attributes & FileAttributes.ReparsePoint) != 0) continue;   // never follow junctions / symlinks
                        if ((item.Attributes & FileAttributes.Directory) != 0) { dirs.Push(item.FullName); continue; }
                        long len = ((FileInfo)item).Length;
                        s.Total += len; s.Files++;
                        if (s.Files % 2000 == 0 && progress != null) progress(s.Files);
                        string first = FirstLevel(root, item.FullName);
                        if (first != null) { long v; folders.TryGetValue(first, out v); folders[first] = v + len; }
                        if (largest.Count < top || len > largest[largest.Count - 1].Size)
                        {
                            largest.Add(new FileHit { Path = item.FullName, Size = len });
                            largest.Sort((a, b) => b.Size.CompareTo(a.Size));
                            if (largest.Count > top) largest.RemoveAt(largest.Count - 1);
                        }
                        if (duplicates && len >= 1024 * 1024)
                        {
                            List<string> l; if (!bySize.TryGetValue(len, out l)) bySize[len] = l = new List<string>(); l.Add(item.FullName);
                        }
                    }
                    catch { s.Skipped++; }
                }
            }
            s.Largest = largest;
            s.Folders = folders.Select(kv => new FileHit { Path = kv.Key, Size = kv.Value }).OrderByDescending(f => f.Size).Take(top).ToList();
            if (duplicates) FindDuplicates(s, bySize, token);
            return s;
        }

        // Same size → same first 64 KB → same SHA-256 of the whole file. Reports only; nothing is removed here.
        private static void FindDuplicates(FolderScan s, Dictionary<long, List<string>> bySize, CancellationToken token)
        {
            foreach (KeyValuePair<long, List<string>> group in bySize.Where(g => g.Value.Count > 1).OrderByDescending(g => g.Key))
            {
                foreach (IGrouping<string, string> quick in group.Value.GroupBy(p => Hash(p, 65536, token)).Where(g => g.Key != null && g.Count() > 1))
                    foreach (IGrouping<string, string> full in quick.GroupBy(p => Hash(p, long.MaxValue, token)).Where(g => g.Key != null && g.Count() > 1))
                    {
                        s.Duplicates.Add(full.Select(p => new FileHit { Path = p, Size = group.Key }).ToList());
                        s.DuplicateWaste += group.Key * (full.Count() - 1);
                    }
                if (s.Duplicates.Count >= 200) break;
            }
            s.Duplicates = s.Duplicates.OrderByDescending(g => g[0].Size * (g.Count - 1)).ToList();
        }

        private static string Hash(string path, long limit, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16))
                using (SHA256 sha = SHA256.Create())
                {
                    if (limit >= fs.Length) return BitConverter.ToString(sha.ComputeHash(fs));
                    byte[] buf = new byte[limit]; int read = 0, n;
                    while (read < buf.Length && (n = fs.Read(buf, read, buf.Length - read)) > 0) read += n;
                    return BitConverter.ToString(sha.ComputeHash(buf, 0, read));
                }
            }
            catch { return null; }
        }

        private static string FirstLevel(string root, string file)
        {
            string rel = file.Substring(root.TrimEnd('\\').Length).TrimStart('\\');
            int slash = rel.IndexOf('\\');
            return slash < 0 ? null : Path.Combine(root, rel.Substring(0, slash));
        }

        // Moves files to the Recycle Bin (undoable), never a permanent delete.
        public static bool Recycle(IEnumerable<string> paths)
        {
            string from = string.Join("\0", paths.Where(File.Exists)) + "\0\0";
            if (from.Length <= 2) return false;
            const uint FO_DELETE = 3; const ushort FOF_ALLOWUNDO = 0x40, FOF_NOCONFIRMATION = 0x10, FOF_SILENT = 0x4, FOF_NOERRORUI = 0x400;
            ushort flags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI;
            try
            {
                if (IntPtr.Size == 8) { ShFileOp64 op = new ShFileOp64 { wFunc = FO_DELETE, pFrom = from, fFlags = flags }; return SHFileOperation64(ref op) == 0 && !op.fAnyOperationsAborted; }
                ShFileOp32 op32 = new ShFileOp32 { wFunc = FO_DELETE, pFrom = from, fFlags = flags }; return SHFileOperation32(ref op32) == 0 && !op32.fAnyOperationsAborted;
            }
            catch { return false; }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ShFileOp64 { public IntPtr hwnd; public uint wFunc; public string pFrom; public string pTo; public ushort fFlags; [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted; public IntPtr hNameMappings; public string lpszProgressTitle; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
        private struct ShFileOp32 { public IntPtr hwnd; public uint wFunc; public string pFrom; public string pTo; public ushort fFlags; [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted; public IntPtr hNameMappings; public string lpszProgressTitle; }
        [DllImport("shell32.dll", EntryPoint = "SHFileOperationW", CharSet = CharSet.Unicode)] private static extern int SHFileOperation64(ref ShFileOp64 op);
        [DllImport("shell32.dll", EntryPoint = "SHFileOperationW", CharSet = CharSet.Unicode)] private static extern int SHFileOperation32(ref ShFileOp32 op);

        // ---------- Hosts ----------

        public static string HostsPath { get { return Path.Combine(Environment.SystemDirectory, @"drivers\etc\hosts"); } }
        public static string HostsBackupDir { get { return Path.Combine(Settings.DataDir, "hosts-backups"); } }

        public static string ReadHosts() { try { return File.ReadAllText(HostsPath); } catch { return null; } }

        // Saves a timestamped copy first; writes only if that copy succeeded.
        public static bool WriteHosts(string text, out string backup, out string message)
        {
            backup = null; message = null;
            try
            {
                Directory.CreateDirectory(HostsBackupDir);
                backup = Path.Combine(HostsBackupDir, "hosts-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
                File.Copy(HostsPath, backup, true);
                File.WriteAllText(HostsPath, text.Replace("\r\n", "\n").Replace("\n", "\r\n"), new UTF8Encoding(false));
                try { File.AppendAllText(Settings.LogPath, "HOSTS | " + DateTime.Now.ToString("s") + " | ok=True backup=" + Path.GetFileName(backup) + Environment.NewLine); } catch { }
                return true;
            }
            catch (Exception ex) { message = ex is UnauthorizedAccessException ? "access denied" : ex.Message; return false; }
        }

        public static string LatestHostsBackup()
        {
            try { return Directory.Exists(HostsBackupDir) ? Directory.GetFiles(HostsBackupDir, "hosts-*.txt").OrderByDescending(f => f).FirstOrDefault() : null; } catch { return null; }
        }

        // ---------- Power ----------

        // powercfg prints localized text, so only GUIDs, the '*' active marker and hex values are parsed.
        public static List<PowerPlan> PowerPlans()
        {
            List<PowerPlan> plans = new List<PowerPlan>();
            CommandResult r = PowerTools.Exec("powercfg.exe", "/list", 15000);
            if (!r.Ok) return plans;
            foreach (string line in r.Output.Split('\n'))
            {
                Match m = Regex.Match(line, @"([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\s*\((.*)\)\s*(\*)?");
                if (m.Success) plans.Add(new PowerPlan { Guid = m.Groups[1].Value.ToLowerInvariant(), Name = m.Groups[2].Value.Trim(), Active = m.Groups[3].Success });
            }
            return plans;
        }

        // Minutes (0 = never) for AC and DC; -1 when unknown.
        public static int[] Timeout(string sub, string setting)
        {
            int[] v = { -1, -1 };
            CommandResult r = PowerTools.Exec("powercfg.exe", "/query SCHEME_CURRENT " + sub + " " + setting, 15000);
            if (!r.Ok) return v;
            List<int> seconds = new List<int>();
            foreach (string line in r.Output.Split('\n'))
            {
                Match m = Regex.Match(line.Trim(), @":\s*0x([0-9a-fA-F]{8})\s*$");
                if (m.Success) seconds.Add(Convert.ToInt32(m.Groups[1].Value, 16));
            }
            if (seconds.Count >= 2) { v[0] = seconds[seconds.Count - 2] / 60; v[1] = seconds[seconds.Count - 1] / 60; }
            return v;
        }

        // kind: "monitor" or "standby"; minutes from a fixed list chosen in the UI.
        public static bool SetTimeout(string kind, bool ac, int minutes)
        {
            if ((kind != "monitor" && kind != "standby") || minutes < 0 || minutes > 600) return false;
            bool ok = PowerTools.Run("powercfg.exe", "/change " + kind + "-timeout-" + (ac ? "ac" : "dc") + " " + minutes) == 0;
            try { File.AppendAllText(Settings.LogPath, "POWER | " + DateTime.Now.ToString("s") + " | " + kind + (ac ? "-ac" : "-dc") + "=" + minutes + " ok=" + ok + Environment.NewLine); } catch { }
            return ok;
        }

        public static bool HasBattery()
        {
            try { return System.Windows.Forms.SystemInformation.PowerStatus.BatteryChargeStatus != System.Windows.Forms.BatteryChargeStatus.NoSystemBattery; } catch { return false; }
        }
    }
}
