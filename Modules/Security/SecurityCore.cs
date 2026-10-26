// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ProjectMantis
{
    internal enum Severity { Info = 0, Low = 1, Medium = 2, High = 3 }

    // Fix kinds. A "command" fix never carries a command line: FixPayload is the key of a fix that is
    // implemented in code (SecurityFixes), so nothing read from the system is ever executed.
    internal static class FixKinds
    {
        public const string None = "none";
        public const string Command = "command";
        public const string OpenSettings = "open-settings";
        public const string Defender = "defender";
    }

    internal sealed class Finding
    {
        public string Id;                 // stable key, e.g. "FW_OFF"
        public string ModuleId;
        public Severity Severity;
        public bool Passed;               // a check that is fine; shown as "OK", never scored
        public string TitleTr, TitleEn;
        public string DetailTr, DetailEn;
        public string RecommendationTr, RecommendationEn;
        public string FixKind = FixKinds.None;
        public string FixPayload;         // fix key or ms-settings:/windowsdefender: URI
        public string FixDescriptionTr, FixDescriptionEn;   // what the fix will change and how to undo it
        public bool RequiresAdmin;

        public static Finding Ok(string id, string titleTr, string titleEn)
        {
            return new Finding { Id = id, Severity = Severity.Info, Passed = true, TitleTr = titleTr, TitleEn = titleEn };
        }

        public static Finding Issue(string id, Severity severity, string titleTr, string titleEn, string detailTr, string detailEn)
        {
            return new Finding { Id = id, Severity = severity, TitleTr = titleTr, TitleEn = titleEn, DetailTr = detailTr, DetailEn = detailEn };
        }

        public Finding Recommend(string tr, string en) { RecommendationTr = tr; RecommendationEn = en; return this; }

        public Finding WithFix(string kind, string payload, bool admin, string descriptionTr, string descriptionEn)
        {
            FixKind = kind; FixPayload = payload; RequiresAdmin = admin; FixDescriptionTr = descriptionTr; FixDescriptionEn = descriptionEn; return this;
        }

        public Finding Detail(string tr, string en) { DetailTr = tr; DetailEn = en; return this; }
    }

    internal sealed class ModuleResult
    {
        public string ModuleId;
        public bool Completed;
        public bool NeedsAdmin;           // some checks were skipped because Mantis is not elevated
        public string Error;
        public DateTime When = DateTime.Now;
        public TimeSpan Duration;
        public List<Finding> Findings = new List<Finding>();

        public IEnumerable<Finding> Issues { get { return Findings.Where(f => !f.Passed && f.Severity > Severity.Info); } }

        public void Add(Finding f) { f.ModuleId = ModuleId; Findings.Add(f); }
    }

    // Simple, explainable score: start at 100, High -20, Medium -8, Low -3, Info 0, floor 0.
    internal static class SecurityScore
    {
        public static int Compute(IEnumerable<Finding> findings)
        {
            int score = 100;
            foreach (Finding f in findings)
            {
                if (f.Passed) continue;
                if (f.Severity == Severity.High) score -= 20;
                else if (f.Severity == Severity.Medium) score -= 8;
                else if (f.Severity == Severity.Low) score -= 3;
            }
            return Math.Max(0, score);
        }
    }

    internal sealed class SecurityModule
    {
        public string Id;
        public string NameTr, NameEn, DescTr, DescEn, BusyTr, BusyEn;
        public bool InFullCheckByDefault = true;
        public bool LocalNetworkOnly;
        public Func<CancellationToken, ModuleResult> Run;   // null until the module is implemented

        // Never throws: an unexpected failure becomes a gentle "could not complete" result.
        public ModuleResult SafeRun(CancellationToken token)
        {
            DateTime start = DateTime.Now;
            ModuleResult r;
            try { r = Run(token); }
            catch (OperationCanceledException) { r = new ModuleResult { ModuleId = Id, Error = "cancelled" }; }
            catch (Exception ex) { r = new ModuleResult { ModuleId = Id, Error = ex.Message }; }
            if (r == null) r = new ModuleResult { ModuleId = Id, Error = "no result" };
            r.ModuleId = Id;
            r.Completed = r.Error == null;
            foreach (Finding f in r.Findings) f.ModuleId = Id;
            r.When = DateTime.Now; r.Duration = r.When - start;
            return r;
        }
    }

    internal static class SecurityModules
    {
        public static List<SecurityModule> All()
        {
            return new List<SecurityModule>
            {
                new SecurityModule { Id = "hardening", NameTr = "Sağlamlaştırma Denetimi", NameEn = "Hardening Audit", DescTr = "Güvenlik Duvarı, Defender, UAC, BitLocker, SMBv1, RDP, Secure Boot ve daha fazlası.", DescEn = "Firewall, Defender, UAC, BitLocker, SMBv1, RDP, Secure Boot and more.", BusyTr = "Güvenlik ayarları denetleniyor…", BusyEn = "Auditing security settings…" },
                new SecurityModule { Id = "persistence", NameTr = "Kalıcılık & Keylogger İzleri", NameEn = "Persistence & Keylogger Hunt", DescTr = "Başlangıç kayıtları, zamanlanmış görevler, servisler, WMI ve şüpheli süreçler. Hiçbir şey silinmez.", DescEn = "Autostart entries, scheduled tasks, services, WMI and suspicious processes. Nothing is deleted.", BusyTr = "Kalıcılık taranıyor…", BusyEn = "Scanning persistence…" },
                new SecurityModule { Id = "malware", NameTr = "Zararlı Yazılım Taraması", NameEn = "Malware Scan", DescTr = "Windows Defender durumu ve hızlı tarama; başlangıç, Temp ve İndirilenler'de imzasız dosyalar.", DescEn = "Windows Defender status and quick scan; unsigned files in startup, Temp and Downloads.", BusyTr = "Defender ile taranıyor…", BusyEn = "Scanning with Defender…" },
                new SecurityModule { Id = "software", NameTr = "Yazılım Güncelliği", NameEn = "Software Vulnerabilities", DescTr = "Bekleyen Windows güncellemeleri ve eski kalmış uygulamalar.", DescEn = "Pending Windows updates and outdated apps.", BusyTr = "Güncellemeler denetleniyor…", BusyEn = "Checking for updates…" },
                new SecurityModule { Id = "network", NameTr = "Yerel Ağ Taraması", NameEn = "Local Network Scan", DescTr = "Yalnızca bağlı olduğun yerel alt ağdaki cihazlar ve açık yaygın portlar. İnternete tarama yapılmaz.", DescEn = "Only devices and common open ports on the local subnet you are connected to. Nothing on the internet is scanned.", BusyTr = "Yerel ağ taranıyor…", BusyEn = "Scanning the local network…", InFullCheckByDefault = false, LocalNetworkOnly = true },
            };
        }
    }
}
