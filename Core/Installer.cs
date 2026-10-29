// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ProjectMantis
{
    internal sealed class InstallOptions
    {
        public string Folder;
        public bool DesktopShortcut = true;
        public bool StartMenuShortcut = true;
        public bool StartAtLogin;
    }

    // Per-user install (no administrator rights): copies this exe into the chosen folder, adds shortcuts and an
    // entry under Settings → Apps so Mantis can be removed like any other app. The same exe runs the setup when
    // its file name contains "Setup" (the release download) or with --install, and the uninstaller with --uninstall.
    internal static class Installer
    {
        public const string ExeName = "ProjectMantis.exe";
        private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ProjectMantis";
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string LinkName = "Project Mantis.lnk";

        public static string DefaultFolder { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Project Mantis"); } }
        public static string CurrentExe { get { return Process.GetCurrentProcess().MainModule.FileName; } }

        public static bool IsSetupRun(string[] args)
        {
            if (args != null && args.Contains("--install")) return true;
            try { return Path.GetFileName(CurrentExe).IndexOf("setup", StringComparison.OrdinalIgnoreCase) >= 0; } catch { return false; }
        }

        public static bool IsUninstallRun(string[] args) { return args != null && args.Contains("--uninstall"); }

        // Folder of an existing install, or null.
        public static string InstalledFolder()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(UninstallKey))
                {
                    string dir = k == null ? null : k.GetValue("InstallLocation") as string;
                    return dir != null && File.Exists(Path.Combine(dir, ExeName)) ? dir : null;
                }
            }
            catch { return null; }
        }

        // Accepts only a plain local folder outside Windows; characters cmd.exe treats specially are refused
        // because the uninstaller removes its own folder through cmd after exiting.
        public static string NormalizeFolder(string input, out string error)
        {
            error = null;
            try
            {
                string dir = (input ?? "").Trim();
                if (dir.Length == 0 || !Path.IsPathRooted(dir) || dir.StartsWith(@"\\")) { error = "path"; return null; }
                if (dir.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || dir.IndexOfAny(new[] { '%', '^', '!', '&', '"', '*', '?', '<', '>', '|' }) >= 0) { error = "chars"; return null; }
                dir = Path.GetFullPath(dir).TrimEnd('\\');
                if (dir.Length <= 3 || dir.Length > 180) { error = "path"; return null; }
                string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\');
                if (dir.Equals(win, StringComparison.OrdinalIgnoreCase) || dir.StartsWith(win + "\\", StringComparison.OrdinalIgnoreCase)) { error = "windows"; return null; }
                if (!Path.GetFileName(dir).Equals("Project Mantis", StringComparison.OrdinalIgnoreCase)) dir = Path.Combine(dir, "Project Mantis");
                return dir;
            }
            catch { error = "path"; return null; }
        }

        public static bool Install(InstallOptions o, out string target, out string error)
        {
            error = null; target = null;
            try
            {
                string dir = NormalizeFolder(o.Folder, out error);
                if (dir == null) return false;
                target = Path.Combine(dir, ExeName);
                if (IsRunning(target)) { error = "running"; return false; }
                Directory.CreateDirectory(dir);
                string source = CurrentExe;
                if (!string.Equals(Path.GetFullPath(source), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
                {
                    string temp = target + ".new";
                    File.Copy(source, temp, true);
                    if (File.Exists(target)) File.Delete(target);
                    File.Move(temp, target);
                }
                WriteLogo(dir);

                string desktopLink = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), LinkName);
                string menuLink = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), LinkName);
                if (o.DesktopShortcut) CreateShortcut(desktopLink, target); else DeleteOurShortcut(desktopLink, target);
                if (o.StartMenuShortcut) CreateShortcut(menuLink, target); else DeleteOurShortcut(menuLink, target);

                using (RegistryKey run = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (o.StartAtLogin) run.SetValue("ProjectMantis", "\"" + target + "\"");
                    else run.DeleteValue("ProjectMantis", false);
                }

                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(UninstallKey))
                {
                    k.SetValue("DisplayName", "Project Mantis");
                    k.SetValue("DisplayVersion", AppInfo.Version);
                    k.SetValue("Publisher", "kemalselimkoc");
                    k.SetValue("DisplayIcon", target + ",0");
                    k.SetValue("InstallLocation", dir);
                    k.SetValue("UninstallString", "\"" + target + "\" --uninstall");
                    k.SetValue("URLInfoAbout", "https://github.com/kemalselimkoc/Project-Mantis");
                    k.SetValue("HelpLink", "https://github.com/kemalselimkoc/Project-Mantis/issues");
                    k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                    k.SetValue("EstimatedSize", (int)Math.Max(1, new FileInfo(target).Length / 1024), RegistryValueKind.DWord);
                    k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                }
                Settings s = Settings.Load(); s.TermsAccepted = Terms.Version; s.Save();
                LogActivity("INSTALL", "version=" + AppInfo.Version + " dir=" + dir);
                return true;
            }
            catch (UnauthorizedAccessException) { error = "access"; }
            catch (Exception ex) { error = ex.Message; }
            return false;
        }

        // Removes shortcuts, the login entry and the Apps entry; optionally the user's Mantis data. The exe
        // cannot delete itself while running, so a hidden cmd waits a moment, deletes it and the folder if empty.
        public static bool Uninstall(bool removeData, out string error)
        {
            error = null;
            try
            {
                string exe = CurrentExe;
                string dir = Path.GetDirectoryName(exe);
                string desktopLink = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), LinkName);
                string menuLink = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), LinkName);
                string registered = InstalledFolder();
                foreach (string link in new[] { desktopLink, menuLink })
                {
                    DeleteOurShortcut(link, exe);
                    if (registered != null) DeleteOurShortcut(link, Path.Combine(registered, ExeName));
                }
                using (RegistryKey run = Registry.CurrentUser.OpenSubKey(RunKey, true)) { if (run != null) run.DeleteValue("ProjectMantis", false); }
                Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false);
                if (removeData) { try { if (Directory.Exists(Settings.DataDir)) Directory.Delete(Settings.DataDir, true); } catch { } }
                else LogActivity("UNINSTALL", "version=" + AppInfo.Version);

                // Only self-delete from a real install folder (".../Project Mantis/ProjectMantis.exe" with safe characters).
                string unused, safeDir = NormalizeFolder(dir, out unused);
                if (Path.GetFileName(exe).Equals(ExeName, StringComparison.OrdinalIgnoreCase) && string.Equals(safeDir, dir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                {
                    string logo = Path.Combine(dir, "assets", "mantis-logo.png");
                    string cmd = "/c ping 127.0.0.1 -n 3 > nul & del /f /q \"" + exe + "\" & del /f /q \"" + logo + "\" & rmdir \"" + Path.Combine(dir, "assets") + "\" & rmdir \"" + dir + "\"";
                    Process.Start(new ProcessStartInfo("cmd.exe", cmd) { CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden });
                }
                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        private static bool IsRunning(string exe)
        {
            int self = Process.GetCurrentProcess().Id;
            foreach (Process p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ExeName)))
            {
                try { if (p.Id != self && string.Equals(p.MainModule.FileName, exe, StringComparison.OrdinalIgnoreCase)) return true; } catch { }
            }
            return false;
        }

        private static void WriteLogo(string dir)
        {
            try
            {
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("ProjectMantis.logo.png"))
                {
                    if (s == null) return;
                    Directory.CreateDirectory(Path.Combine(dir, "assets"));
                    using (FileStream f = File.Create(Path.Combine(dir, "assets", "mantis-logo.png"))) s.CopyTo(f);
                }
            }
            catch { }
        }

        private static void CreateShortcut(string link, string target)
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(shellType);
            try
            {
                object lnk = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { link });
                Type t = lnk.GetType();
                t.InvokeMember("TargetPath", BindingFlags.SetProperty, null, lnk, new object[] { target });
                t.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, lnk, new object[] { Path.GetDirectoryName(target) });
                t.InvokeMember("IconLocation", BindingFlags.SetProperty, null, lnk, new object[] { target + ",0" });
                t.InvokeMember("Description", BindingFlags.SetProperty, null, lnk, new object[] { "Project Mantis - System Care" });
                t.InvokeMember("Save", BindingFlags.InvokeMethod, null, lnk, null);
                Marshal.ReleaseComObject(lnk);
            }
            finally { Marshal.ReleaseComObject(shell); }
        }

        // Deletes a "Project Mantis" shortcut only when it points at this install (or at nothing that exists).
        private static void DeleteOurShortcut(string link, string target)
        {
            try
            {
                if (!File.Exists(link)) return;
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                object shell = Activator.CreateInstance(shellType);
                string points;
                try
                {
                    object lnk = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { link });
                    points = lnk.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, lnk, null) as string;
                    Marshal.ReleaseComObject(lnk);
                }
                finally { Marshal.ReleaseComObject(shell); }
                if (string.Equals(points, target, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(points) || !File.Exists(points)) File.Delete(link);
            }
            catch { }
        }

        private static void LogActivity(string kind, string fields)
        {
            try { Directory.CreateDirectory(Settings.DataDir); File.AppendAllText(Settings.LogPath, kind + " | " + DateTime.Now.ToString("s") + " | " + fields + Environment.NewLine); } catch { }
        }
    }
}
