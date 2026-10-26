// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace ProjectMantis
{
    internal static class StartupManager
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public static bool IsEnabled() { try { using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey)) return k != null && k.GetValue("ProjectMantis") != null; } catch { return false; } }
        public static void SetEnabled(bool enabled)
        {
            try { using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey)) { if (enabled) k.SetValue("ProjectMantis", "\"" + Process.GetCurrentProcess().MainModule.FileName + "\""); else k.DeleteValue("ProjectMantis", false); } } catch { }
        }
    }

    internal sealed class Settings
    {
        public string Language = "tr";
        public string CloseBehavior = "ask";
        public bool TrayNoticeShown;
        public bool MinimizeOnMinimize;
        public string CheckupModules;     // comma-separated module ids for Full Check-Up; null = defaults
        public static string DataDir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProjectMantis"); } }
        public static string LogPath { get { return Path.Combine(DataDir, "mantis.log"); } }
        private static string FilePath { get { return Path.Combine(DataDir, "settings.ini"); } }
        public static Settings Load()
        {
            Settings s = new Settings();
            try
            {
                if (!File.Exists(FilePath)) return s;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    string[] pair = line.Split(new[] { '=' }, 2); if (pair.Length != 2) { if (line == "tr" || line == "en") s.Language = line; continue; }
                    if (pair[0] == "language") s.Language = pair[1] == "en" ? "en" : "tr";
                    else if (pair[0] == "close") s.CloseBehavior = pair[1] == "tray" || pair[1] == "exit" ? pair[1] : "ask";
                    else if (pair[0] == "trayNotice") s.TrayNoticeShown = pair[1] == "1";
                    else if (pair[0] == "minimizeToTray") s.MinimizeOnMinimize = pair[1] == "1";
                    else if (pair[0] == "checkupModules") s.CheckupModules = pair[1];
                }
            }
            catch { }
            return s;
        }
        public void Save()
        {
            try
            {
                Directory.CreateDirectory(DataDir);
                List<string> lines = new List<string> { "language=" + Language, "close=" + CloseBehavior, "trayNotice=" + (TrayNoticeShown ? "1" : "0"), "minimizeToTray=" + (MinimizeOnMinimize ? "1" : "0") };
                if (CheckupModules != null) lines.Add("checkupModules=" + CheckupModules);
                File.WriteAllLines(FilePath, lines);
            }
            catch { }
        }
    }
}
