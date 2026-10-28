// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ProjectMantis
{
    internal sealed class StartupItem
    {
        public AutostartEntry Entry;
        public string Publisher;
        public bool Machine;           // HKLM / all-users item: changing it needs administrator rights
        public bool CanToggle;         // RunOnce and policy entries are not managed through StartupApproved
        public bool Heavy;             // likely to slow sign-in noticeably
        public bool Missing;           // the file it points to no longer exists
        public string ApprovedKey;     // StartupApproved subkey (under HKCU or HKLM, 64-bit view)
        public string ApprovedName;    // value name inside ApprovedKey
    }

    // Startup Manager (spec B.2). Reads the same sources as the Persistence scan, but for speed and tidiness.
    // Items are never deleted: on/off goes through the StartupApproved flags that Task Manager uses,
    // so every change is reversible here or in Task Manager.
    internal static class StartupTweaks
    {
        private const string Approved = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\";
        private static readonly string[] HeavyHints = { "teams", "discord", "steam", "spotify", "onedrive", "epicgames", "creative cloud", "adobe", "skype", "dropbox", "googledrive", "battle.net", "eadesktop", "origin", "ubisoft", "riotclient", "zoom", "slack", "whatsapp", "telegram", "icloud", "msedge", "opera", "chrome" };

        public static List<StartupItem> List()
        {
            List<StartupItem> items = new List<StartupItem>();
            foreach (AutostartEntry e in PersistenceScanner.RunKeys())
            {
                StartupItem s = new StartupItem { Entry = e, Machine = e.Location.StartsWith("HKLM", StringComparison.OrdinalIgnoreCase) };
                string key = e.Location.Replace(" (32-bit)", "");
                if (key.EndsWith(@"\CurrentVersion\Run", StringComparison.OrdinalIgnoreCase))
                {
                    s.CanToggle = true; s.ApprovedName = e.Name;
                    s.ApprovedKey = Approved + (e.Location.EndsWith("(32-bit)") ? "Run32" : "Run");
                }
                items.Add(Describe(s));
            }
            foreach (AutostartEntry e in PersistenceScanner.StartupFolders())
            {
                bool common = string.Equals(e.Location.TrimEnd('\\'), Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
                StartupItem s = new StartupItem { Entry = e, Machine = common, ApprovedKey = Approved + "StartupFolder" };
                string file = FileInFolder(e.Location, e.Name);
                if (file != null)
                {
                    s.CanToggle = true; s.ApprovedName = Path.GetFileName(file);
                    s.Entry.Enabled = IsApprovedOn(common, s.ApprovedKey, s.ApprovedName);
                }
                items.Add(Describe(s));
            }
            return items.OrderBy(i => i.Entry.Enabled ? 0 : 1).ThenBy(i => i.Entry.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        // Writes the same 12-byte value Task Manager writes: 02.. = enabled, 03.. + FILETIME = disabled.
        public static bool SetEnabled(StartupItem item, bool enabled, out string message)
        {
            message = null;
            if (item == null || !item.CanToggle) { message = "not toggleable"; return false; }
            try
            {
                byte[] data = new byte[12];
                data[0] = (byte)(enabled ? 2 : 3);
                if (!enabled) Array.Copy(BitConverter.GetBytes(DateTime.UtcNow.ToFileTimeUtc()), 0, data, 4, 8);
                using (RegistryKey b = RegistryKey.OpenBaseKey(item.Machine ? RegistryHive.LocalMachine : RegistryHive.CurrentUser, RegistryView.Registry64))
                using (RegistryKey k = b.CreateSubKey(item.ApprovedKey))
                    k.SetValue(item.ApprovedName, data, RegistryValueKind.Binary);
                item.Entry.Enabled = enabled;
                Log("STARTUP | " + DateTime.Now.ToString("s") + " | enabled=" + enabled + " ok=True name=" + item.Entry.Name);
                return true;
            }
            catch (Exception ex)
            {
                message = ex is UnauthorizedAccessException || ex is System.Security.SecurityException ? "access denied" : ex.Message;
                Log("STARTUP | " + DateTime.Now.ToString("s") + " | enabled=" + enabled + " ok=False name=" + item.Entry.Name);
                return false;
            }
        }

        internal static bool IsApprovedOn(bool machine, string key, string name)
        {
            try
            {
                using (RegistryKey b = RegistryKey.OpenBaseKey(machine ? RegistryHive.LocalMachine : RegistryHive.CurrentUser, RegistryView.Registry64))
                using (RegistryKey k = b.OpenSubKey(key))
                {
                    byte[] data = k == null ? null : k.GetValue(name) as byte[];
                    return data == null || data.Length == 0 || (data[0] & 1) == 0;
                }
            }
            catch { return true; }
        }

        private static StartupItem Describe(StartupItem s)
        {
            string path = s.Entry.Path;
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    string company = FileVersionInfo.GetVersionInfo(path).CompanyName;
                    s.Publisher = string.IsNullOrWhiteSpace(company) ? null : company.Trim();
                    s.Heavy = new FileInfo(path).Length > 80L * 1024 * 1024;
                }
                else if (!string.IsNullOrEmpty(path) && path.IndexOf('\\') >= 0) s.Missing = true;
            }
            catch { }
            string probe = ((s.Entry.Name ?? "") + " " + (path ?? "")).ToLowerInvariant();
            if (HeavyHints.Any(h => probe.Contains(h))) s.Heavy = true;
            if (!s.CanToggle) s.Heavy = false;   // one-shot RunOnce/policy items are not a sign-in cost to manage here
            return s;
        }

        private static string FileInFolder(string dir, string nameWithoutExtension)
        {
            try { return Directory.GetFiles(dir).FirstOrDefault(f => string.Equals(Path.GetFileNameWithoutExtension(f), nameWithoutExtension, StringComparison.OrdinalIgnoreCase)); }
            catch { return null; }
        }

        private static void Log(string line)
        {
            try { Directory.CreateDirectory(Settings.DataDir); File.AppendAllText(Settings.LogPath, line + Environment.NewLine); } catch { }
        }
    }
}
