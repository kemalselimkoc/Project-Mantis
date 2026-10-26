// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace ProjectMantis
{
    // The only place where Check-Up changes the system. Each fix is a known, reversible action keyed
    // by name; the UI shows its description and asks for confirmation before calling Apply.
    internal static class SecurityFixes
    {
        private static readonly Dictionary<string, Func<CommandResult>> fixes = new Dictionary<string, Func<CommandResult>>(StringComparer.Ordinal);

        // Modules register their fixes here (see each scanner's static constructor).
        public static void Register(string key, Func<CommandResult> apply) { fixes[key] = apply; }

        public static bool Exists(string key) { return key != null && fixes.ContainsKey(key); }

        public static bool Apply(string key, out string message)
        {
            message = "";
            Func<CommandResult> apply;
            if (key == null || !fixes.TryGetValue(key, out apply)) { message = "Unknown fix."; return false; }
            CommandResult r;
            try { r = apply(); }
            catch (Exception ex) { r = new CommandResult { Error = ex.Message }; }
            bool ok = r != null && r.Ok;
            if (!ok) message = r == null ? "No result." : (r.TimedOut ? "Timed out." : (r.Error ?? "").Trim().Length > 0 ? r.Error.Trim() : "Exit code " + r.ExitCode);
            Log("FIX | " + DateTime.Now.ToString("s") + " | key=" + key + " ok=" + ok + (ok ? "" : " error=" + message.Replace('\n', ' ')));
            return ok;
        }

        // Settings deep links are limited to Windows' own URI schemes.
        public static bool OpenUri(string uri)
        {
            if (string.IsNullOrEmpty(uri) || !(uri.StartsWith("ms-settings:", StringComparison.OrdinalIgnoreCase) || uri.StartsWith("windowsdefender:", StringComparison.OrdinalIgnoreCase))) return false;
            try { Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true }); return true; } catch { return false; }
        }

        private static void Log(string line)
        {
            try { Directory.CreateDirectory(Settings.DataDir); File.AppendAllText(Settings.LogPath, line + Environment.NewLine); } catch { }
        }
    }
}
