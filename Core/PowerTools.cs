// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ProjectMantis
{
    internal sealed class CommandResult
    {
        public bool Started;
        public bool TimedOut;
        public int ExitCode = -1;
        public string Output = "";
        public string Error = "";
        public bool Ok { get { return Started && !TimedOut && ExitCode == 0; } }
    }

    internal static class PowerTools
    {
        public static string GetActiveScheme()
        {
            try { string output = Capture("powercfg.exe", "/getactivescheme"); int a = output.IndexOf(':'); int b = output.IndexOf('(', a + 1); return a >= 0 ? output.Substring(a + 1, (b > a ? b : output.Length) - a - 1).Trim() : ""; } catch { return ""; }
        }
        public static bool SetHighPerformance() { return Run("powercfg.exe", "/setactive SCHEME_MIN") == 0; }
        public static bool SetScheme(string guid) { return !string.IsNullOrWhiteSpace(guid) && IsSafeToken(guid) && Run("powercfg.exe", "/setactive " + guid) == 0; }
        public static void KeepAwake(bool on) { Native.SetThreadExecutionState(on ? 0x80000003u : 0x80000000u); }

        public static int Run(string file, string args) { return Exec(file, args, 15000).ExitCode; }

        public static string Capture(string file, string args) { return Exec(file, args, 30000).Output; }

        public static CommandResult Exec(string file, string args, int timeoutMs) { return Exec(file, args, timeoutMs, null); }

        // Runs a console tool without a window, reads stdout/stderr asynchronously (no pipe deadlock),
        // kills it on timeout and never throws: a missing tool becomes Started=false.
        public static CommandResult Exec(string file, string args, int timeoutMs, Encoding encoding)
        {
            CommandResult result = new CommandResult();
            StringBuilder output = new StringBuilder(), error = new StringBuilder();
            try
            {
                Encoding enc = encoding ?? ConsoleEncoding();
                ProcessStartInfo psi = new ProcessStartInfo(ResolveSystemTool(file), args ?? "")
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                    StandardOutputEncoding = enc, StandardErrorEncoding = enc
                };
                using (Process p = new Process { StartInfo = psi })
                {
                    p.OutputDataReceived += (s, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
                    p.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (error) error.AppendLine(e.Data); };
                    if (!p.Start()) return result;
                    result.Started = true;
                    p.BeginOutputReadLine(); p.BeginErrorReadLine();
                    if (!p.WaitForExit(timeoutMs))
                    {
                        result.TimedOut = true;
                        try { p.Kill(); } catch { }
                    }
                    else
                    {
                        p.WaitForExit();
                        result.ExitCode = p.ExitCode;
                    }
                }
            }
            catch (Exception ex) { error.AppendLine(ex.Message); }
            lock (output) result.Output = output.ToString();
            lock (error) result.Error = error.ToString();
            return result;
        }

        // Runs a PowerShell script passed as -EncodedCommand, so no user value is ever re-parsed by cmd quoting.
        // Output is forced to UTF-8 to stay readable on Turkish and other non-English Windows installs.
        public static CommandResult PowerShell(string script, int timeoutMs)
        {
            string full = "$ProgressPreference='SilentlyContinue';[Console]::OutputEncoding=[System.Text.Encoding]::UTF8;" + script;
            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(full));
            string exe = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe");
            return Exec(exe, "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded, timeoutMs, new UTF8Encoding(false));
        }

        // Quotes one argument using the CommandLineToArgvW rules.
        public static string QuoteArg(string arg)
        {
            if (arg == null) return "\"\"";
            if (arg.Length > 0 && arg.IndexOfAny(new[] { ' ', '\t', '"' }) < 0) return arg;
            StringBuilder sb = new StringBuilder("\"");
            int slashes = 0;
            foreach (char c in arg)
            {
                if (c == '\\') { slashes++; continue; }
                if (c == '"') { sb.Append('\\', slashes * 2 + 1); sb.Append('"'); slashes = 0; continue; }
                sb.Append('\\', slashes); slashes = 0; sb.Append(c);
            }
            sb.Append('\\', slashes * 2); sb.Append('"');
            return sb.ToString();
        }

        // Letters, digits and . - _ { } only: safe for GUIDs and package ids on a command line.
        public static bool IsSafeToken(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 128) return false;
            foreach (char c in value)
                if (!(char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_' || c == '{' || c == '}')) return false;
            return true;
        }

        // Bare tool names resolve to System32 first so a planted copy in the working folder or PATH is never used.
        private static string ResolveSystemTool(string file)
        {
            try
            {
                if (file.IndexOfAny(new[] { '\\', '/' }) < 0)
                {
                    string system = Path.Combine(Environment.SystemDirectory, file);
                    if (File.Exists(system)) return system;
                }
            }
            catch { }
            return file;
        }

        private static Encoding ConsoleEncoding()
        {
            try { return Encoding.GetEncoding((int)Native.GetOEMCP()); } catch { return Encoding.Default; }
        }
    }
}
