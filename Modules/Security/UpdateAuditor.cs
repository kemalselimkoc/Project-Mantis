// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ProjectMantis
{
    // Software vulnerabilities (spec 3.4): out-of-date software is the largest real attack surface.
    // Read-only; it reuses the Update Center's services and never installs anything itself.
    internal static class UpdateAuditor
    {
        public static ModuleResult Run(CancellationToken token)
        {
            ModuleResult r = new ModuleResult { ModuleId = "software" };
            CheckWindowsUpdate(r, WindowsUpdateStatus.Read());
            token.ThrowIfCancellationRequested();
            CheckApps(r, WingetService.FreshOrScan(TimeSpan.FromMinutes(10)));
            return r;
        }

        private static void CheckWindowsUpdate(ModuleResult r, WindowsUpdateInfo wu)
        {
            if (wu.Failed)
            {
                r.Add(Finding.Issue("WU_UNKNOWN", Severity.Info, "Windows Update durumu okunamadı", "Windows Update status could not be read", "Ayrıntı: " + wu.Error, "Details: " + wu.Error)
                    .WithFix(FixKinds.OpenSettings, "ms-settings:windowsupdate", false, "", ""));
                return;
            }
            int other = wu.Pending - wu.Important;
            string titles = string.Join("\n", wu.Titles.Take(5)) + (wu.Titles.Count > 5 ? "\n…" : "");
            if (wu.Important > 0)
                r.Add(Finding.Issue("WU_SECURITY", Severity.High, wu.Important + " güvenlik/kritik Windows güncellemesi bekliyor", wu.Important + " security/critical Windows updates are waiting", titles, titles)
                    .Recommend("Windows Update'ten şimdi yükle ve yeniden başlat.", "Install them from Windows Update now and restart.")
                    .WithFix(FixKinds.OpenSettings, "ms-settings:windowsupdate", false, "", ""));
            if (other > 0)
                r.Add(Finding.Issue("WU_PENDING", Severity.Low, other + " Windows güncellemesi bekliyor", other + " Windows updates are waiting", wu.Important > 0 ? "" : titles, wu.Important > 0 ? "" : titles)
                    .WithFix(FixKinds.OpenSettings, "ms-settings:windowsupdate", false, "", ""));
            if (wu.Pending == 0) r.Add(Finding.Ok("WU_CURRENT", "Windows güncel", "Windows is up to date"));
            if (wu.RebootRequired)
                r.Add(Finding.Issue("WU_REBOOT", Severity.Low, "Güncellemeler yeniden başlatma bekliyor", "Updates are waiting for a restart", "Yüklenmiş yamalar yeniden başlatana kadar tam etkili olmaz.", "Installed patches are not fully active until you restart.")
                    .WithFix(FixKinds.OpenSettings, "ms-settings:windowsupdate", false, "", ""));
            if (wu.LastSearch.HasValue && (DateTime.Now - wu.LastSearch.Value).TotalDays > 14)
                r.Add(Finding.Issue("WU_STALE", Severity.Low, "Windows " + (int)(DateTime.Now - wu.LastSearch.Value).TotalDays + " gündür güncelleme denetlemedi", "Windows has not checked for updates in " + (int)(DateTime.Now - wu.LastSearch.Value).TotalDays + " days", "Otomatik denetim engellenmiş veya bilgisayar uzun süredir çevrimdışı olabilir.", "Automatic checks may be blocked or the PC has been offline for a long time.")
                    .WithFix(FixKinds.OpenSettings, "ms-settings:windowsupdate", false, "", ""));
        }

        // Outdated apps are one grouped finding so a long list does not flood the score; a major-version gap
        // or a long list raises it to Medium.
        private static void CheckApps(ModuleResult r, WingetScan scan)
        {
            if (scan.WingetMissing) { r.Add(Finding.Issue("APPS_NO_WINGET", Severity.Info, "winget bulunamadı, uygulama denetimi atlandı", "winget not found, app check skipped", "Microsoft Store'daki \"Uygulama Yükleyici\" ile gelir.", "It comes with \"App Installer\" from the Microsoft Store.")); return; }
            if (scan.Failed) { r.Add(Finding.Issue("APPS_UNKNOWN", Severity.Info, "Uygulama güncellemeleri denetlenemedi", "App updates could not be checked", scan.Error, scan.Error)); return; }
            if (scan.Packages.Count == 0) { r.Add(Finding.Ok("APPS_CURRENT", "winget'in tanıdığı tüm uygulamalar güncel", "Every app winget knows about is up to date")); return; }
            List<WingetPackage> major = scan.Packages.Where(MajorBehind).ToList();
            Severity sev = major.Count > 0 || scan.Packages.Count > 10 ? Severity.Medium : Severity.Low;
            string list = string.Join("\n", scan.Packages.Take(8).Select(p => p.Name + "  " + p.Version + " → " + p.Available + (MajorBehind(p) ? "  ▲" : ""))) + (scan.Packages.Count > 8 ? "\n…" : "");
            r.Add(Finding.Issue("APPS_OUTDATED", sev, scan.Packages.Count + " uygulama güncel değil", scan.Packages.Count + " apps are out of date",
                    list + (major.Count > 0 ? "\n▲ = ana sürüm geride (" + major.Count + ")" : ""), list + (major.Count > 0 ? "\n▲ = a major version behind (" + major.Count + ")" : ""))
                .Recommend("Güncellemeler sayfasından tek tek ya da hepsini birden güncelle.", "Update them one by one or all at once on the Updates page.")
                .WithFix(FixKinds.Navigate, "updates", false, "", ""));
        }

        private static bool MajorBehind(WingetPackage p)
        {
            int a, b;
            return LeadingNumber(p.Version, out a) && LeadingNumber(p.Available, out b) && b > a && b < a + 1000;
        }

        private static bool LeadingNumber(string v, out int n)
        {
            n = 0;
            if (string.IsNullOrEmpty(v)) return false;
            string digits = new string(v.SkipWhile(c => !char.IsDigit(c)).TakeWhile(char.IsDigit).ToArray());
            return digits.Length > 0 && digits.Length < 6 && int.TryParse(digits, out n);
        }
    }
}
