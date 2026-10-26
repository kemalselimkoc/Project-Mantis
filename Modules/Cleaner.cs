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
            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(full, "*", SearchOption.AllDirectories).ToArray(); }
            catch { files = SafeFiles(full); }
            foreach (string file in files)
            {
                try
                {
                    FileInfo info = new FileInfo(file); long size = info.Exists ? info.Length : 0;
                    result.Files++; result.Bytes += size;
                    if (!scanOnly) { info.IsReadOnly = false; info.Delete(); }
                }
                catch { result.Skipped++; }
            }
            if (!scanOnly)
            {
                try
                {
                    foreach (string dir in Directory.EnumerateDirectories(full, "*", SearchOption.AllDirectories).OrderByDescending(x => x.Length))
                        try { Directory.Delete(dir, false); } catch { }
                }
                catch { }
            }
        }

        private static IEnumerable<string> SafeFiles(string root)
        {
            List<string> found = new List<string>(); Queue<string> queue = new Queue<string>(); queue.Enqueue(root);
            while (queue.Count > 0)
            {
                string dir = queue.Dequeue();
                try { found.AddRange(Directory.GetFiles(dir)); } catch { }
                try { foreach (string sub in Directory.GetDirectories(dir)) queue.Enqueue(sub); } catch { }
            }
            return found;
        }

        private static void AppendLog(string line)
        {
            try { Directory.CreateDirectory(Path.GetDirectoryName(Settings.LogPath)); File.AppendAllText(Settings.LogPath, line + Environment.NewLine); } catch { }
        }
    }
}
