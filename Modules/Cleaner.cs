// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ProjectMantis
{
    internal sealed class CleanupResult { public long Bytes; public int Files; public int Skipped; public bool AdobeRunning; }

    // Reads past runs back from mantis.log ("CLEAN | 2026-10-24T04:18:56 | bytes=... files=...").
    internal sealed class CleanupHistory
    {
        public DateTime? LastClean; public long LastCleanBytes; public long TotalCleanBytes; public int CleanRuns;
        public DateTime? LastScan; public long LastScanBytes;

        public static CleanupHistory Read()
        {
            CleanupHistory h = new CleanupHistory();
            try
            {
                if (!File.Exists(Settings.LogPath)) return h;
                foreach (string line in File.ReadAllLines(Settings.LogPath))
                {
                    string[] parts = line.Split('|');
                    if (parts.Length < 3) continue;
                    string kind = parts[0].Trim();
                    DateTime when;
                    if (!DateTime.TryParse(parts[1].Trim(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeLocal, out when)) continue;
                    long bytes = 0;
                    foreach (string field in parts[2].Trim().Split(' '))
                        if (field.StartsWith("bytes=")) long.TryParse(field.Substring(6), out bytes);
                    if (kind == "CLEAN") { h.LastClean = when; h.LastCleanBytes = bytes; h.TotalCleanBytes += bytes; h.CleanRuns++; }
                    else if (kind == "SCAN") { h.LastScan = when; h.LastScanBytes = bytes; }
                }
            }
            catch { }
            return h;
        }
    }

    internal static class Cleaner
    {
        private static readonly string[] AdobeProcesses = { "AfterFX", "Adobe Premiere Pro", "MediaEncoder", "dynamiclinkmanager", "aerender" };

        public static CleanupResult Run(bool scanOnly)
        {
            CleanupResult result = new CleanupResult();
            result.AdobeRunning = AdobeProcesses.Any(n => Process.GetProcessesByName(n).Length > 0);
            foreach (string root in GetTargets(result.AdobeRunning).Distinct(StringComparer.OrdinalIgnoreCase))
                ProcessDirectory(root, scanOnly, result);
            AppendLog((scanOnly ? "SCAN" : "CLEAN") + " | " + DateTime.Now.ToString("s") + " | bytes=" + result.Bytes + " files=" + result.Files + " skipped=" + result.Skipped + " adobeRunning=" + result.AdobeRunning);
            return result;
        }

        private static IEnumerable<string> GetTargets(bool adobeRunning)
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string temp = Path.GetTempPath();
            yield return temp;
            yield return Path.Combine(local, "Temp");
            yield return Path.Combine(local, "D3DSCache");
            yield return Path.Combine(local, "CrashDumps");
            yield return Path.Combine(local, "Microsoft", "Windows", "WER", "ReportArchive");
            yield return Path.Combine(local, "Microsoft", "Windows", "WER", "ReportQueue");
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch");
            if (!adobeRunning)
            {
                yield return Path.Combine(local, "Adobe", "Common", "Media Cache");
                yield return Path.Combine(local, "Adobe", "Common", "Media Cache Files");
                yield return Path.Combine(roaming, "Adobe", "Common", "Media Cache");
                yield return Path.Combine(roaming, "Adobe", "Common", "Media Cache Files");
                yield return Path.Combine(roaming, "Adobe", "Common", "Peak Files");
                yield return Path.Combine(local, "Temp", "Adobe");
            }
        }

        private static void ProcessDirectory(string root, bool scanOnly, CleanupResult result)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return;
            string full;
            try { full = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar); } catch { return; }
            if (full.Length < 8 || full == Path.GetPathRoot(full)) return;
            if (IsReparsePoint(full)) return;
            List<string> dirs;
            IEnumerable<string> files = SafeFiles(full, out dirs);
            foreach (string file in files)
            {
                try
                {
                    FileInfo info = new FileInfo(file);
                    // A file symlink planted in a temp folder could point anywhere; never delete through one.
                    if (!info.Exists || (info.Attributes & FileAttributes.ReparsePoint) != 0) { result.Skipped++; continue; }
                    long size = info.Length;
                    // Count a file only once it is really gone, so "space reclaimed" never includes skipped files.
                    if (!scanOnly) { info.IsReadOnly = false; info.Delete(); }
                    result.Files++; result.Bytes += size;
                }
                catch { result.Skipped++; }
            }
            if (!scanOnly)
            {
                foreach (string dir in dirs.OrderByDescending(x => x.Length))
                    try { if (!IsReparsePoint(dir)) Directory.Delete(dir, false); } catch { }
            }
        }

        // Walks the tree without entering junctions or directory symlinks. Temp folders are writable by
        // ordinary users, so when Mantis runs as administrator a planted link must never steer the
        // cleaner into System32 or another user's profile.
        private static IEnumerable<string> SafeFiles(string root, out List<string> dirs)
        {
            List<string> found = new List<string>(); dirs = new List<string>();
            Queue<string> queue = new Queue<string>(); queue.Enqueue(root);
            while (queue.Count > 0)
            {
                string dir = queue.Dequeue();
                try { found.AddRange(Directory.GetFiles(dir)); } catch { }
                try
                {
                    foreach (string sub in Directory.GetDirectories(dir))
                    {
                        if (IsReparsePoint(sub)) continue;
                        dirs.Add(sub); queue.Enqueue(sub);
                    }
                }
                catch { }
            }
            return found;
        }

        private static bool IsReparsePoint(string path)
        {
            try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; } catch { return true; }
        }

        private static void AppendLog(string line)
        {
            try { Directory.CreateDirectory(Path.GetDirectoryName(Settings.LogPath)); File.AppendAllText(Settings.LogPath, line + Environment.NewLine); } catch { }
        }
    }
}
