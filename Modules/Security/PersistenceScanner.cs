// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Text;
using System.Threading;

namespace ProjectMantis
{
    // One thing that starts automatically: a Run value, a Startup shortcut, a scheduled task or a service.
    internal sealed class AutostartEntry
    {
        public string Kind;        // "run", "startup", "task", "service"
        public string Name;
        public string Location;    // registry key, folder, task path or service name
        public string Command;     // raw command line
        public string Path;        // resolved executable or script the entry really runs
        public bool AsSystem;
        public bool Enabled = true;
    }

    internal sealed class SignatureInfo { public string Status; public string Signer; public bool Trusted { get { return Status == "Valid"; } } }

    // Persistence & keylogger hunt (spec 3.2). Lists where software makes itself start, plus processes and
    // outbound connections, and flags what looks wrong. Read-only: nothing is disabled or deleted here.
    internal static class PersistenceScanner
    {
        private static readonly string[] Interpreters = { "powershell.exe", "pwsh.exe", "cmd.exe", "mshta.exe", "wscript.exe", "cscript.exe", "rundll32.exe", "regsvr32.exe", "conhost.exe" };

        public static ModuleResult Run(CancellationToken token)
        {
            ModuleResult r = new ModuleResult { ModuleId = "persistence" };
            List<AutostartEntry> entries = new List<AutostartEntry>();
            entries.AddRange(RunKeys());
            entries.AddRange(StartupFolders());
            token.ThrowIfCancellationRequested();
            entries.AddRange(ScheduledTasks());
            token.ThrowIfCancellationRequested();
            entries.AddRange(AutoServices());
            token.ThrowIfCancellationRequested();

            List<ProcessInfo> processes = Processes();
            token.ThrowIfCancellationRequested();

            // One signature pass for everything that lives outside the protected Windows folder.
            HashSet<string> toCheck = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (AutostartEntry e in entries) if (NeedsSignatureCheck(e.Path)) toCheck.Add(e.Path);
            foreach (ProcessInfo p in processes) if (NeedsSignatureCheck(p.Path)) toCheck.Add(p.Path);
            Dictionary<string, SignatureInfo> sigs = Signatures(toCheck, token);

            CheckWinlogon(r);
            CheckAutostarts(r, entries, sigs);
            token.ThrowIfCancellationRequested();
            CheckWmi(r);
            CheckProcesses(r, processes, entries, sigs);
            return r;
        }

        // ---------- Autostart sources ----------

        internal static IEnumerable<AutostartEntry> RunKeys()
        {
            List<AutostartEntry> list = new List<AutostartEntry>();
            string[] paths = { @"Software\Microsoft\Windows\CurrentVersion\Run", @"Software\Microsoft\Windows\CurrentVersion\RunOnce", @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer\Run" };
            foreach (RegistryHive hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
                foreach (RegistryView view in hive == RegistryHive.LocalMachine ? new[] { RegistryView.Registry64, RegistryView.Registry32 } : new[] { RegistryView.Registry64 })
                    foreach (string path in paths)
                    {
                        try
                        {
                            using (RegistryKey b = RegistryKey.OpenBaseKey(hive, view))
                            using (RegistryKey k = b.OpenSubKey(path))
                            {
                                if (k == null) continue;
                                HashSet<string> disabled = DisabledByStartupApproved(hive, view);
                                foreach (string name in k.GetValueNames())
                                {
                                    string cmd = Convert.ToString(k.GetValue(name));
                                    if (string.IsNullOrWhiteSpace(cmd)) continue;
                                    string where = (hive == RegistryHive.CurrentUser ? "HKCU\\" : "HKLM\\") + path + (view == RegistryView.Registry32 ? " (32-bit)" : "");
                                    if (list.Any(x => x.Location == where.Replace(" (32-bit)", "") && x.Name == name && x.Command == cmd)) continue;
                                    list.Add(new AutostartEntry { Kind = "run", Name = name, Location = where, Command = cmd, Path = ResolveCommand(cmd), Enabled = !disabled.Contains(name) });
                                }
                            }
                        }
                        catch { }
                    }
            return list;
        }

        // Task Manager's "Disabled" startup items keep their Run value; StartupApproved marks them off.
        private static HashSet<string> DisabledByStartupApproved(RegistryHive hive, RegistryView view)
        {
            HashSet<string> off = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (RegistryKey b = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
                using (RegistryKey k = b.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run" + (view == RegistryView.Registry32 ? "32" : "")))
                {
                    if (k == null) return off;
                    foreach (string name in k.GetValueNames())
                    {
                        byte[] data = k.GetValue(name) as byte[];
                        if (data != null && data.Length > 0 && (data[0] & 1) == 1) off.Add(name);
                    }
                }
            }
            catch { }
            return off;
        }

        internal static IEnumerable<AutostartEntry> StartupFolders()
        {
            List<AutostartEntry> list = new List<AutostartEntry>();
            foreach (Environment.SpecialFolder f in new[] { Environment.SpecialFolder.Startup, Environment.SpecialFolder.CommonStartup })
            {
                string dir;
                try { dir = Environment.GetFolderPath(f); if (!Directory.Exists(dir)) continue; } catch { continue; }
                foreach (string file in SafeFiles(dir))
                {
                    if (System.IO.Path.GetFileName(file).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                    string target = file.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ? ShortcutTarget(file) : file;
                    list.Add(new AutostartEntry { Kind = "startup", Name = System.IO.Path.GetFileNameWithoutExtension(file), Location = dir, Command = target ?? file, Path = target ?? file });
                }
            }
            return list;
        }

        private static IEnumerable<AutostartEntry> ScheduledTasks()
        {
            List<AutostartEntry> list = new List<AutostartEntry>();
            const string script = "Get-ScheduledTask -ErrorAction SilentlyContinue | ForEach-Object { $t=$_; foreach ($a in $t.Actions) { if ($a.Execute) { 'TASK=' + (($t.TaskPath + $t.TaskName), $t.State, $t.Principal.UserId, $a.Execute, $a.Arguments -join \"`t\").Replace(\"`r\",' ').Replace(\"`n\",' ') } } }";
            KeyValues kv = KeyValues.Parse(PowerTools.PowerShell(script, 120000).Output);
            foreach (string line in kv.All("TASK"))
            {
                string[] f = line.Split('\t');
                if (f.Length < 4) continue;
                string exe = Environment.ExpandEnvironmentVariables(f[3].Trim().Trim('"'));
                string args = f.Length > 4 ? f[4] : "";
                string user = f[2] ?? "";
                list.Add(new AutostartEntry
                {
                    Kind = "task", Name = f[0], Location = f[0], Command = (exe + " " + args).Trim(), Path = ResolveCommand("\"" + exe + "\" " + args),
                    AsSystem = user.IndexOf("SYSTEM", StringComparison.OrdinalIgnoreCase) >= 0 || user == "S-1-5-18",
                    Enabled = !f[1].Equals("Disabled", StringComparison.OrdinalIgnoreCase)
                });
            }
            return list;
        }

        private static IEnumerable<AutostartEntry> AutoServices()
        {
            List<AutostartEntry> list = new List<AutostartEntry>();
            try
            {
                using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT Name, DisplayName, PathName, StartName FROM Win32_Service WHERE StartMode='Auto'"))
                    foreach (ManagementObject o in s.Get())
                        using (o)
                        {
                            string cmd = Convert.ToString(o["PathName"]);
                            if (string.IsNullOrWhiteSpace(cmd)) continue;
                            string account = Convert.ToString(o["StartName"]);
                            list.Add(new AutostartEntry
                            {
                                Kind = "service", Name = Convert.ToString(o["DisplayName"]), Location = Convert.ToString(o["Name"]), Command = cmd, Path = ResolveCommand(cmd),
                                AsSystem = account.Length == 0 || account.Equals("LocalSystem", StringComparison.OrdinalIgnoreCase)
                            });
                        }
            }
            catch { }
            return list;
        }

        // ---------- Checks ----------

        private static void CheckWinlogon(ModuleResult r)
        {
            try
            {
                using (RegistryKey b = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey k = b.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon"))
                {
                    string shell = Convert.ToString(k.GetValue("Shell")).Trim();
                    string userinit = Convert.ToString(k.GetValue("Userinit")).Trim().TrimEnd(',').Trim();
                    string expectedInit = System.IO.Path.Combine(Environment.SystemDirectory, "userinit.exe");
                    bool shellOk = shell.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase);
                    bool initOk = userinit.Equals(expectedInit, StringComparison.OrdinalIgnoreCase) || userinit.Equals("userinit.exe", StringComparison.OrdinalIgnoreCase);
                    if (shellOk && initOk) { r.Add(Finding.Ok("WINLOGON_OK", "Oturum açma kabuğu (Winlogon) varsayılan", "Sign-in shell (Winlogon) is the default")); return; }
                    r.Add(Finding.Issue("WINLOGON_CHANGED", Severity.High, "Oturum açma kabuğu değiştirilmiş", "Sign-in shell has been changed",
                            "Winlogon Shell = \"" + shell + "\", Userinit = \"" + userinit + "\". Burada fazladan bir program, her oturum açılışında çalışır.", "Winlogon Shell = \"" + shell + "\", Userinit = \"" + userinit + "\". An extra program here runs at every sign-in.")
                        .Recommend("Bunu bilerek yapmadıysan Windows Defender çevrimdışı tarama çalıştır.", "If you did not do this on purpose, run a Windows Defender offline scan.")
                        .WithFix(FixKinds.Defender, "windowsdefender://threat", false, "", ""));
                }
            }
            catch { }
        }

        private static void CheckAutostarts(ModuleResult r, List<AutostartEntry> entries, Dictionary<string, SignatureInfo> sigs)
        {
            List<AutostartEntry> missing = new List<AutostartEntry>(), unsignedElsewhere = new List<AutostartEntry>();
            int reviewed = 0;
            foreach (AutostartEntry e in entries)
            {
                if (!e.Enabled || IsSelf(e.Path)) continue;
                reviewed++;
                string lolbin = SuspiciousInterpreterUse(e.Command);
                if (lolbin != null)
                {
                    r.Add(Finding.Issue("AUTO_SCRIPT", e.AsSystem ? Severity.High : Severity.Medium, KindTr(e.Kind) + " gizli bir komut çalıştırıyor: " + e.Name, KindEn(e.Kind) + " runs a hidden command: " + e.Name,
                            lolbin + "\n" + Short(e.Command) + "\n" + e.Location, lolbin + "\n" + Short(e.Command) + "\n" + e.Location)
                        .Recommend("Tanımıyorsan Windows Defender ile tara; ardından Başlangıç Yöneticisi'nden devre dışı bırak.", "If you don't recognise it, scan with Windows Defender, then disable it in the Startup Manager.")
                        .WithFix(FixKinds.Defender, "windowsdefender://threat", false, "", ""));
                    continue;
                }
                if (string.IsNullOrEmpty(e.Path)) continue;
                if (!File.Exists(e.Path)) { if (e.Kind != "service") missing.Add(e); continue; }
                SignatureInfo sig;
                if (!sigs.TryGetValue(e.Path, out sig) || sig.Trusted) continue;
                if (IsUserWritable(e.Path))
                    r.Add(Finding.Issue("AUTO_UNSIGNED_USERDIR", e.AsSystem ? Severity.High : Severity.Medium, KindTr(e.Kind) + ": imzasız program kullanıcı klasöründen başlıyor", KindEn(e.Kind) + ": unsigned program starts from a user folder",
                            e.Name + "\n" + e.Path + "\n" + SignatureTr(sig) + (e.AsSystem ? " · SYSTEM yetkisiyle" : ""), e.Name + "\n" + e.Path + "\n" + SignatureEn(sig) + (e.AsSystem ? " · runs as SYSTEM" : ""))
                        .Recommend("Bu tek başına zararlı demek değil; tanımıyorsan Windows Defender ile tara.", "This alone does not mean malware; if you don't recognise it, scan it with Windows Defender.")
                        .WithFix(FixKinds.Reveal, e.Path, false, "", ""));
                else if (!IsUnder(e.Path, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)) && !IsUnder(e.Path, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)))
                    unsignedElsewhere.Add(e);
            }
            if (missing.Count > 0)
                r.Add(Finding.Issue("AUTO_MISSING", Severity.Low, missing.Count + " başlangıç kaydı var olmayan dosyayı gösteriyor", missing.Count + " startup entries point to missing files",
                        "Kaldırılmış programlardan kalmış olabilir: " + string.Join(", ", missing.Take(6).Select(m => m.Name)) + (missing.Count > 6 ? "…" : ""), "Probably leftovers of uninstalled programs: " + string.Join(", ", missing.Take(6).Select(m => m.Name)) + (missing.Count > 6 ? "…" : ""))
                    .Recommend("Zararsızdır; Başlangıç Yöneticisi'nden temizleyebilirsin.", "Harmless; you can tidy them up in the Startup Manager."));
            if (unsignedElsewhere.Count > 0)
                r.Add(Finding.Issue("AUTO_UNSIGNED", Severity.Low, unsignedElsewhere.Count + " imzasız başlangıç öğesi", unsignedElsewhere.Count + " unsigned startup items",
                        string.Join("\n", unsignedElsewhere.Take(5).Select(e => e.Name + " — " + e.Path)) + (unsignedElsewhere.Count > 5 ? "\n…" : ""), string.Join("\n", unsignedElsewhere.Take(5).Select(e => e.Name + " — " + e.Path)) + (unsignedElsewhere.Count > 5 ? "\n…" : ""))
                    .Recommend("İmzasız olmak kötü amaçlı demek değildir; tanıdığın programlarsa sorun yok.", "Unsigned does not mean malicious; if you know these programs, all is fine."));
            r.Add(Finding.Ok("AUTO_REVIEWED", reviewed + " başlangıç öğesi incelendi (kayıt defteri, Başlangıç klasörü, görevler, servisler)", reviewed + " startup items reviewed (registry, Startup folder, tasks, services)"));
        }

        private static void CheckWmi(ModuleResult r)
        {
            List<string> suspicious = new List<string>();
            int consumers = 0;
            try
            {
                ManagementScope scope = new ManagementScope(@"\\.\root\subscription");
                scope.Connect();
                using (ManagementObjectSearcher s = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT * FROM __EventConsumer")))
                    foreach (ManagementObject o in s.Get())
                        using (o)
                        {
                            consumers++;
                            string cls = Convert.ToString(o["__CLASS"]);
                            if (cls == "CommandLineEventConsumer" || cls == "ActiveScriptEventConsumer")
                            {
                                string detail = cls == "CommandLineEventConsumer" ? Convert.ToString(o["CommandLineTemplate"]) : Convert.ToString(o["ScriptFileName"]) + " " + Short(Convert.ToString(o["ScriptText"]));
                                suspicious.Add(Convert.ToString(o["Name"]) + " (" + cls + "): " + Short(detail));
                            }
                        }
            }
            catch (UnauthorizedAccessException) { r.NeedsAdmin = true; return; }
            catch (ManagementException ex) { if (ex.ErrorCode == ManagementStatus.AccessDenied) r.NeedsAdmin = true; return; }
            catch { return; }
            if (suspicious.Count > 0)
                r.Add(Finding.Issue("WMI_PERSIST", Severity.High, "WMI üzerinden komut çalıştıran kalıcılık kaydı", "WMI subscription that runs commands",
                        string.Join("\n", suspicious) + "\nBu, kötü amaçlı yazılımların sık kullandığı gizli bir başlatma yöntemidir.", string.Join("\n", suspicious) + "\nThis is a stealthy start-up method often used by malware.")
                    .Recommend("Yönetim veya güvenlik yazılımın kurmadıysa Windows Defender çevrimdışı tarama çalıştır.", "Unless your management or security software created it, run a Windows Defender offline scan.")
                    .WithFix(FixKinds.Defender, "windowsdefender://threat", false, "", ""));
            else r.Add(Finding.Ok("WMI_OK", "WMI'da komut çalıştıran kalıcılık kaydı yok (" + consumers + " tüketici)", "No command-running WMI subscriptions (" + consumers + " consumers)"));
        }

        // ---------- Processes, connections, keylogger-like behaviour ----------

        private sealed class ProcessInfo { public int Pid; public string Name; public string Path; }

        private static List<ProcessInfo> Processes()
        {
            List<ProcessInfo> list = new List<ProcessInfo>();
            foreach (Process p in Process.GetProcesses())
            {
                try { if (p.Id > 4) list.Add(new ProcessInfo { Pid = p.Id, Name = p.ProcessName, Path = Native.ProcessPath(p.Id) }); }
                catch { }
                finally { p.Dispose(); }
            }
            return list;
        }

        private static void CheckProcesses(ModuleResult r, List<ProcessInfo> processes, List<AutostartEntry> entries, Dictionary<string, SignatureInfo> sigs)
        {
            List<Native.TcpConnection> connections;
            try { connections = Native.EstablishedTcp().Where(c => IsPublic(c.Remote)).ToList(); } catch { connections = new List<Native.TcpConnection>(); }
            HashSet<int> windowed;
            try { windowed = Native.PidsWithVisibleWindow(); } catch { windowed = new HashSet<int>(); }
            HashSet<string> autostartPaths = new HashSet<string>(entries.Where(e => e.Enabled && e.Path != null).Select(e => e.Path), StringComparer.OrdinalIgnoreCase);
            Dictionary<int, int> connCount = connections.GroupBy(c => c.Pid).ToDictionary(g => g.Key, g => g.Count());

            List<string> lowList = new List<string>();
            foreach (ProcessInfo p in processes)
            {
                if (p.Path == null || IsSelf(p.Path)) continue;
                SignatureInfo sig;
                if (!sigs.TryGetValue(p.Path, out sig) || sig.Trusted || !IsUserWritable(p.Path)) continue;
                bool network = connCount.ContainsKey(p.Pid), autostart = autostartPaths.Contains(p.Path), hidden = !windowed.Contains(p.Pid);
                int signals = (network ? 1 : 0) + (autostart ? 1 : 0) + (hidden ? 1 : 0);
                string facts = SignatureTr(sig) + (autostart ? " · otomatik başlıyor" : "") + (network ? " · " + connCount[p.Pid] + " dış bağlantı" : "") + (hidden ? " · görünür penceresi yok" : "");
                string factsEn = SignatureEn(sig) + (autostart ? " · starts automatically" : "") + (network ? " · " + connCount[p.Pid] + " outbound connections" : "") + (hidden ? " · no visible window" : "");
                if (signals >= 2 && network)
                    r.Add(Finding.Issue("PROC_SPYLIKE", signals == 3 ? Severity.High : Severity.Medium, "Casus yazılım davranışına benzeyen süreç: " + p.Name, "Process behaving like spyware: " + p.Name,
                            p.Path + "\n" + facts + "\nBu bir ipucudur, kesin teşhis değildir. Keylogger'lar genelde böyle görünür: imzasız, kullanıcı klasöründen, sessizce ve internete bağlı.",
                            p.Path + "\n" + factsEn + "\nThis is a hint, not a diagnosis. Keyloggers often look like this: unsigned, from a user folder, silent and online.")
                        .Recommend("Tanımıyorsan Windows Defender çevrimdışı tarama yap; programı tanıyorsan sorun yok.", "If you don't recognise it, run a Windows Defender offline scan; if you know the program, it's fine.")
                        .WithFix(FixKinds.Defender, "windowsdefender://threat", false, "", ""));
                else lowList.Add(p.Name + " — " + p.Path);
            }
            if (lowList.Count > 0)
                r.Add(Finding.Issue("PROC_UNSIGNED", Severity.Low, lowList.Count + " imzasız süreç kullanıcı klasöründen çalışıyor", lowList.Count + " unsigned processes run from user folders",
                        string.Join("\n", lowList.Distinct().Take(5)) + (lowList.Count > 5 ? "\n…" : ""), string.Join("\n", lowList.Distinct().Take(5)) + (lowList.Count > 5 ? "\n…" : ""))
                    .Recommend("Tek başına zararlı demek değil; tanıdığın programlarsa sorun yok.", "On its own this is not malware; if you know these programs, all is fine."));
            else r.Add(Finding.Ok("PROC_OK", "Kullanıcı klasörlerinden çalışan imzasız süreç yok", "No unsigned processes running from user folders"));

            // Plain-language summary of who talks to the internet right now (information, never scored).
            // Grouped by program name: a browser or Electron app runs many processes but is one program.
            Dictionary<int, string> names = processes.ToDictionary(p => p.Pid, p => p.Name);
            var programs = connCount.GroupBy(kv => names.ContainsKey(kv.Key) ? names[kv.Key] : "?")
                .Select(g => new { Name = g.Key, Count = g.Sum(x => x.Value) }).OrderByDescending(x => x.Count).ToList();
            List<string> top = programs.Take(6).Select(x => x.Name + " (" + x.Count + ")").ToList();
            if (top.Count > 0)
                r.Add(Finding.Issue("NET_SUMMARY", Severity.Info, programs.Count + " program şu an internete bağlı (" + connections.Count + " bağlantı)", programs.Count + " programs are online right now (" + connections.Count + " connections)",
                    "En çok: " + string.Join(", ", top), "Most: " + string.Join(", ", top)));
        }

        // ---------- Signatures ----------

        // Authenticode check through PowerShell, which also understands catalog-signed Windows files.
        // Paths go through a temporary file, never through the command line.
        internal static Dictionary<string, SignatureInfo> Signatures(HashSet<string> paths, CancellationToken token)
        {
            Dictionary<string, SignatureInfo> result = new Dictionary<string, SignatureInfo>(StringComparer.OrdinalIgnoreCase);
            List<string> existing = paths.Where(p => { try { return File.Exists(p); } catch { return false; } }).ToList();
            if (existing.Count == 0) return result;
            string list = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mantis-sig-" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                File.WriteAllLines(list, existing, new UTF8Encoding(false));
                string script = "Get-Content -LiteralPath '" + list.Replace("'", "''") + "' -Encoding UTF8 | ForEach-Object { try { $s = Get-AuthenticodeSignature -LiteralPath $_ -ErrorAction Stop; 'SIG=' + $s.Status + \"`t\" + $(if ($s.SignerCertificate) { $s.SignerCertificate.Subject } else { '' }) + \"`t\" + $_ } catch { 'SIG=Error' + \"`t`t\" + $_ } }";
                token.ThrowIfCancellationRequested();
                foreach (string line in KeyValues.Parse(PowerTools.PowerShell(script, 180000).Output).All("SIG"))
                {
                    string[] f = line.Split('\t');
                    if (f.Length < 3) continue;
                    result[f[2]] = new SignatureInfo { Status = f[0], Signer = SubjectName(f[1]) };
                }
            }
            finally { try { File.Delete(list); } catch { } }
            return result;
        }

        private static string SubjectName(string subject)
        {
            if (string.IsNullOrEmpty(subject)) return "";
            foreach (string part in subject.Split(','))
            {
                string t = part.Trim();
                if (t.StartsWith("O=")) return t.Substring(2).Trim('"');
            }
            foreach (string part in subject.Split(',')) { string t = part.Trim(); if (t.StartsWith("CN=")) return t.Substring(3).Trim('"'); }
            return subject;
        }

        private static string SignatureTr(SignatureInfo s) { return s.Status == "NotSigned" ? "İmzasız" : "İmza doğrulanamadı (" + s.Status + ")"; }
        private static string SignatureEn(SignatureInfo s) { return s.Status == "NotSigned" ? "Unsigned" : "Signature not valid (" + s.Status + ")"; }

        // ---------- Path helpers ----------

        // Executable (or script) a command line really runs; interpreters resolve to the script they load.
        internal static string ResolveCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return null;
            string cmd = Environment.ExpandEnvironmentVariables(command.Trim());
            string exe, rest;
            if (cmd.StartsWith("\"")) { int end = cmd.IndexOf('"', 1); exe = end > 0 ? cmd.Substring(1, end - 1) : cmd.Trim('"'); rest = end > 0 ? cmd.Substring(end + 1) : ""; }
            else
            {
                int ext = IndexOfExecutableExtension(cmd);
                exe = ext > 0 ? cmd.Substring(0, ext) : cmd.Split(' ')[0];
                rest = ext > 0 ? cmd.Substring(ext) : cmd.Substring(Math.Min(cmd.Length, exe.Length));
            }
            exe = exe.Trim();
            if (exe.StartsWith(@"\??\")) exe = exe.Substring(4);
            if (exe.StartsWith(@"\SystemRoot\", StringComparison.OrdinalIgnoreCase)) exe = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), exe.Substring(12));
            if (exe.IndexOf('\\') < 0)
            {
                string sys = System.IO.Path.Combine(Environment.SystemDirectory, exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? exe : exe + ".exe");
                if (File.Exists(sys)) exe = sys;
            }
            string name = System.IO.Path.GetFileName(exe).ToLowerInvariant();
            if (Interpreters.Contains(name))
            {
                // wscript script.vbs / rundll32 C:\x\y.dll,Entry → the file it loads is what matters
                foreach (string token in SplitArgs(rest))
                {
                    string t = token.Trim().Trim('"');
                    int comma = t.IndexOf(','); if (comma > 0 && name == "rundll32.exe") t = t.Substring(0, comma);
                    if (t.Length > 3 && t[1] == ':' && t.IndexOfAny(System.IO.Path.GetInvalidPathChars()) < 0) return t;
                }
            }
            return exe;
        }

        private static int IndexOfExecutableExtension(string cmd)
        {
            foreach (string ext in new[] { ".exe", ".bat", ".cmd", ".com", ".scr", ".vbs", ".js", ".ps1", ".lnk" })
            {
                int i = cmd.IndexOf(ext + " ", StringComparison.OrdinalIgnoreCase);
                if (i < 0 && cmd.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) i = cmd.Length - ext.Length;
                if (i > 0) return i + ext.Length;
            }
            return -1;
        }

        private static IEnumerable<string> SplitArgs(string s)
        {
            List<string> parts = new List<string>(); StringBuilder cur = new StringBuilder(); bool quoted = false;
            foreach (char c in s ?? "")
            {
                if (c == '"') { quoted = !quoted; continue; }
                if (c == ' ' && !quoted) { if (cur.Length > 0) { parts.Add(cur.ToString()); cur.Clear(); } continue; }
                cur.Append(c);
            }
            if (cur.Length > 0) parts.Add(cur.ToString());
            return parts;
        }

        // Interpreter tricks that malware uses to hide a payload in an autostart command line.
        private static string SuspiciousInterpreterUse(string command)
        {
            if (string.IsNullOrEmpty(command)) return null;
            string c = command.ToLowerInvariant();
            bool ps = c.Contains("powershell") || c.Contains("pwsh");
            if (ps && (c.Contains(" -enc") || c.Contains(" -e ") || c.Contains("frombase64string"))) return "PowerShell kodlanmış komut / encoded command";
            if (ps && (c.Contains("downloadstring") || c.Contains("invoke-webrequest") || c.Contains(" iwr ") || c.Contains("iex"))) return "PowerShell indir-çalıştır / download-and-run";
            if (c.Contains("mshta") && (c.Contains("http") || c.Contains("javascript:") || c.Contains("vbscript:"))) return "mshta uzak betik / remote script";
            if (c.Contains("regsvr32") && c.Contains("/i:http")) return "regsvr32 uzak betik / remote scriptlet";
            if (c.Contains("rundll32") && (c.Contains("javascript:") || c.Contains("http"))) return "rundll32 betik / script";
            if ((c.Contains("certutil") && c.Contains("-urlcache")) || c.Contains("bitsadmin /transfer")) return "Komut satırından indirme / command-line download";
            return null;
        }

        private static bool NeedsSignatureCheck(string path)
        {
            if (string.IsNullOrEmpty(path) || IsSelf(path)) return false;
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (IsUnder(path, windows)) return false;                       // protected by TrustedInstaller
            if (path.IndexOf(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase) >= 0) return false;   // MSIX packages, signed by the Store
            return true;
        }

        internal static bool IsUserWritable(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string[] roots =
            {
                System.IO.Path.GetTempPath(), Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), System.IO.Path.Combine(Environment.GetEnvironmentVariable("PUBLIC") ?? @"C:\Users\Public", ""),
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
            };
            return roots.Any(root => !string.IsNullOrEmpty(root) && IsUnder(path, root));
        }

        private static bool IsUnder(string path, string root)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(root)) return false;
            return path.StartsWith(root.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSelf(string path)
        {
            try { return !string.IsNullOrEmpty(path) && string.Equals(path, Process.GetCurrentProcess().MainModule.FileName, StringComparison.OrdinalIgnoreCase); } catch { return false; }
        }

        private static bool IsPublic(IPAddress a)
        {
            if (a == null || IPAddress.IsLoopback(a) || a.Equals(IPAddress.Any) || a.Equals(IPAddress.IPv6Any)) return false;
            if (a.IsIPv4MappedToIPv6) a = a.MapToIPv4();
            byte[] b = a.GetAddressBytes();
            if (b.Length == 4) return !(b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168) || (b[0] == 169 && b[1] == 254) || (b[0] == 100 && b[1] >= 64 && b[1] <= 127) || b[0] == 127);
            return !(a.IsIPv6LinkLocal || a.IsIPv6SiteLocal || (b[0] & 0xFE) == 0xFC);
        }

        private static string ShortcutTarget(string lnk)
        {
            object shell = null, shortcut = null;
            try
            {
                Type t = Type.GetTypeFromProgID("WScript.Shell");
                shell = Activator.CreateInstance(t);
                shortcut = t.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
                string target = Convert.ToString(shortcut.GetType().InvokeMember("TargetPath", System.Reflection.BindingFlags.GetProperty, null, shortcut, null));
                return string.IsNullOrEmpty(target) ? null : target;
            }
            catch { return null; }
            finally
            {
                try { if (shortcut != null) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut); } catch { }
                try { if (shell != null) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell); } catch { }
            }
        }

        private static IEnumerable<string> SafeFiles(string dir) { try { return Directory.GetFiles(dir); } catch { return new string[0]; } }

        private static string Short(string s) { if (string.IsNullOrEmpty(s)) return ""; s = s.Replace('\n', ' ').Replace('\r', ' '); return s.Length > 180 ? s.Substring(0, 180) + "…" : s; }

        private static string KindTr(string kind) { return kind == "task" ? "Zamanlanmış görev" : kind == "service" ? "Servis" : kind == "startup" ? "Başlangıç klasörü" : "Başlangıç kaydı"; }
        private static string KindEn(string kind) { return kind == "task" ? "Scheduled task" : kind == "service" ? "Service" : kind == "startup" ? "Startup folder" : "Startup entry"; }
    }
}
