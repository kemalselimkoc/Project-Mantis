// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ProjectMantis
{
    internal enum UpdateState { Available, Queued, Updating, Done, NeedsRestart, Failed }

    internal sealed class WingetPackage
    {
        public string Name, Id, Version, Available, Source;
        public UpdateState State;
        public string Message;
    }

    internal sealed class WingetScan
    {
        public bool WingetMissing;
        public bool Failed;
        public string Error;
        public DateTime When = DateTime.Now;
        public List<WingetPackage> Packages = new List<WingetPackage>();
    }

    // The friendly face of `winget upgrade`, shared by the Updates page and the Check-Up software audit.
    // Nothing is ever updated without an explicit per-run confirmation in the UI.
    internal static class WingetService
    {
        private const int NoApplicableUpgrade = unchecked((int)0x8A15002B);
        private const int NoPackagesFound = unchecked((int)0x8A150014);
        private const int RebootRequiredToFinish = unchecked((int)0x8A150109);
        private const int RebootInitiated = unchecked((int)0x8A15010A);

        public static string Exe()
        {
            try
            {
                string alias = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\WindowsApps\winget.exe");
                if (File.Exists(alias)) return alias;
            }
            catch { }
            return "winget.exe";
        }

        public static WingetScan ListUpgrades()
        {
            WingetScan scan = new WingetScan();
            CommandResult r = PowerTools.Exec(Exe(), "upgrade --include-unknown --accept-source-agreements --disable-interactivity", 120000, new UTF8Encoding(false));
            scan.When = DateTime.Now;
            if (!r.Started) { scan.WingetMissing = true; return scan; }
            if (r.TimedOut) { scan.Failed = true; scan.Error = "winget did not answer within 2 minutes."; return scan; }
            scan.Packages = ParseTable(r.Output);
            if (scan.Packages.Count == 0 && r.ExitCode != 0 && r.ExitCode != NoApplicableUpgrade && r.ExitCode != NoPackagesFound)
            {
                scan.Failed = true;
                scan.Error = LastMeaningfulLine(r.Output + "\n" + r.Error) + " (0x" + r.ExitCode.ToString("X8") + ")";
            }
            return scan;
        }

        // Runs one upgrade. The id and source are validated, then passed as single quoted arguments.
        public static UpdateState Upgrade(WingetPackage p, out string message)
        {
            message = "";
            if (!IsSafePackageId(p.Id)) { message = "Invalid package id."; return UpdateState.Failed; }
            string args = "upgrade --id " + PowerTools.QuoteArg(p.Id) + " --exact --silent --include-unknown --accept-package-agreements --accept-source-agreements --disable-interactivity";
            if (PowerTools.IsSafeToken(p.Source)) args += " --source " + p.Source;
            CommandResult r = PowerTools.Exec(Exe(), args, 30 * 60 * 1000, new UTF8Encoding(false));
            if (!r.Started) { message = "winget could not be started."; return UpdateState.Failed; }
            if (r.TimedOut) { message = "Timed out after 30 minutes."; return UpdateState.Failed; }
            if (r.ExitCode == 0) return UpdateState.Done;
            if (r.ExitCode == RebootRequiredToFinish || r.ExitCode == RebootInitiated) return UpdateState.NeedsRestart;
            message = LastMeaningfulLine(r.Output + "\n" + r.Error) + " (0x" + r.ExitCode.ToString("X8") + ")";
            return UpdateState.Failed;
        }

        public static bool IsSafePackageId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 128) return false;
            foreach (char c in id)
                if (!(char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_' || c == '+')) return false;
            return true;
        }

        // Parses every winget result table. Columns come from the header's positions (measured in console
        // cells, so wide CJK names stay aligned); header words are never matched, so any UI language works.
        public static List<WingetPackage> ParseTable(string output)
        {
            List<WingetPackage> result = new List<WingetPackage>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> lines = CleanLines(output);
            for (int i = 1; i < lines.Count; i++)
            {
                string dash = lines[i].Trim();
                if (dash.Length < 10 || dash.Any(c => c != '-')) continue;
                List<int> starts = ColumnStarts(ToCells(lines[i - 1]));
                if (starts.Count < 4) continue;
                for (int j = i + 1; j < lines.Count; j++)
                {
                    List<char> cells = ToCells(lines[j]);
                    if (lines[j].Trim().Length == 0 || cells.Count <= starts[2]) break;
                    string[] f = new string[starts.Count];
                    for (int k = 0; k < starts.Count; k++) f[k] = Slice(cells, starts[k], k + 1 < starts.Count ? starts[k + 1] : cells.Count);
                    WingetPackage p = new WingetPackage { Name = f[0], Id = f[1], Version = f[2], Available = f[3], Source = starts.Count > 4 ? f[4] : "" };
                    if (!IsSafePackageId(p.Id) || p.Available.Length == 0 || !seen.Add(p.Id)) continue;
                    result.Add(p);
                }
            }
            return result;
        }

        private static List<string> CleanLines(string output)
        {
            List<string> lines = new List<string>();
            foreach (string raw in (output ?? "").Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                int cr = line.LastIndexOf('\r');
                if (cr >= 0) line = line.Substring(cr + 1);   // progress spinners redraw with carriage returns
                lines.Add(line.Replace("\b", "").TrimEnd());
            }
            return lines;
        }

        private static List<char> ToCells(string line)
        {
            List<char> cells = new List<char>(line.Length + 8);
            foreach (char c in line) { cells.Add(c); if (IsWide(c)) cells.Add('\0'); }
            return cells;
        }

        private static List<int> ColumnStarts(List<char> header)
        {
            List<int> starts = new List<int>();
            for (int i = 0; i < header.Count; i++)
                if (header[i] != ' ' && header[i] != '\0' && (i == 0 || header[i - 1] == ' ')) starts.Add(i);
            return starts;
        }

        private static string Slice(List<char> cells, int from, int to)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = from; i < to && i < cells.Count; i++) if (cells[i] != '\0') sb.Append(cells[i]);
            return sb.ToString().Trim();
        }

        private static bool IsWide(char c)
        {
            return (c >= 0x1100 && c <= 0x115F) || (c >= 0x2E80 && c <= 0xA4CF) || (c >= 0xAC00 && c <= 0xD7A3) ||
                   (c >= 0xF900 && c <= 0xFAFF) || (c >= 0xFE30 && c <= 0xFE4F) || (c >= 0xFF00 && c <= 0xFF60) || (c >= 0xFFE0 && c <= 0xFFE6);
        }

        private static string LastMeaningfulLine(string text)
        {
            List<string> lines = CleanLines(text);
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                string t = lines[i].Trim();
                if (t.Length > 3 && t.Any(char.IsLetter)) return t.Length > 160 ? t.Substring(0, 160) + "…" : t;
            }
            return "winget error";
        }
    }
}
