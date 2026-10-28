// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceProcess;

namespace ProjectMantis
{
    // One hand-picked service group from the allowlist (spec B.3). Several services can form one card (Xbox).
    internal sealed class ServiceTweak
    {
        public string Id;
        public string[] Services;
        public int[] DefaultStart;       // Windows defaults, used to restore when no backup exists (2 auto, 3 manual)
        public bool[] DefaultDelayed;
        public string NameTr, NameEn, WhatTr, WhatEn, EffectTr, EffectEn;
        public bool Installed;           // at least one of the services exists
        public bool Enabled;             // at least one installed service is not Disabled
        public bool Running;
    }

    // Only this fixed allowlist is ever touched: service names are constants, never user or system input.
    // "Off" = start type Disabled + stop; "On" = the start type saved before the change (or the Windows default).
    internal static class ServiceTweaks
    {
        private static string BackupPath { get { return Path.Combine(Settings.DataDir, "services-backup.ini"); } }

        public static List<ServiceTweak> List()
        {
            List<ServiceTweak> list = new List<ServiceTweak>
            {
                T("spooler", new[] { "Spooler" }, new[] { 2 }, "Yazıcı Kuyruğu", "Print Spooler",
                    "Yazdırma işlerini yazıcıya sırayla gönderir.", "Queues print jobs for your printers.",
                    "Kapatırsan hiçbir yazıcıya (PDF yazıcısı dahil) yazdıramazsın.", "When off, you cannot print to any printer, including Print to PDF."),
                T("fax", new[] { "Fax" }, new[] { 3 }, "Faks", "Fax",
                    "Bilgisayardan faks gönderip almayı sağlar.", "Sends and receives faxes from this PC.",
                    "Faks kullanmıyorsan kapatmanın bir etkisi yoktur.", "If you do not use fax, turning it off has no effect."),
                T("telemetry", new[] { "DiagTrack", "dmwappushservice" }, new[] { 2, 3 }, "Telemetri (DiagTrack)", "Telemetry (DiagTrack)",
                    "Kullanım ve tanılama verilerini Microsoft'a gönderir.", "Sends usage and diagnostic data to Microsoft.",
                    "Kapatırsan daha az veri gönderilir; Windows normal çalışmaya devam eder.", "When off, less data is sent; Windows keeps working normally."),
                T("bluetooth", new[] { "bthserv" }, new[] { 3 }, "Bluetooth Desteği", "Bluetooth Support",
                    "Bluetooth cihazlarının bulunmasını ve eşleşmesini sağlar.", "Discovers and pairs Bluetooth devices.",
                    "Kapatırsan Bluetooth kulaklık, fare ve klavyeler çalışmaz.", "When off, Bluetooth headphones, mice and keyboards stop working."),
                T("xbox", new[] { "XblAuthManager", "XblGameSave", "XboxNetApiSvc", "XboxGipSvc" }, new[] { 3, 3, 3, 3 }, "Xbox servisleri", "Xbox services",
                    "Xbox oturumu, bulut kayıtları, Xbox ağı ve Xbox aksesuarları.", "Xbox sign-in, cloud saves, Xbox networking and Xbox accessories.",
                    "Kapatırsan Game Pass / Xbox uygulaması oyunları ve Xbox kumandası yazılımı düzgün çalışmayabilir.", "When off, Game Pass / Xbox app games and Xbox controller software may not work properly."),
                T("maps", new[] { "MapsBroker" }, new[] { 2 }, "İndirilen Haritalar", "Downloaded Maps",
                    "Haritalar uygulaması için çevrimdışı haritaları günceller.", "Updates offline maps for the Maps app.",
                    "Kapatırsan çevrimdışı haritalar güncellenmez.", "When off, offline maps are not updated."),
                T("wer", new[] { "WerSvc" }, new[] { 3 }, "Hata Raporlama", "Error Reporting",
                    "Çöken uygulamalar için rapor oluşturur ve gönderir.", "Creates and sends reports when apps crash.",
                    "Kapatırsan çökme raporları oluşturulmaz; sorun gidermek zorlaşabilir.", "When off, no crash reports are made; troubleshooting may be harder."),
            };
            list[2].DefaultDelayed = new[] { false, false };
            list[5].DefaultDelayed = new[] { true };
            foreach (ServiceTweak t in list) Refresh(t);
            return list;
        }

        public static void Refresh(ServiceTweak t)
        {
            t.Installed = false; t.Enabled = false; t.Running = false;
            foreach (string name in t.Services)
            {
                int start = StartType(name);
                if (start < 0) continue;
                t.Installed = true;
                if (start != 4) t.Enabled = true;
                try { using (ServiceController sc = new ServiceController(name)) if (sc.Status == ServiceControllerStatus.Running) t.Running = true; } catch { }
            }
        }

        public static bool SetEnabled(ServiceTweak t, bool enable, out string message)
        {
            message = null;
            Dictionary<string, string> backup = ReadBackup();
            List<string> errors = new List<string>();
            for (int i = 0; i < t.Services.Length; i++)
            {
                string name = t.Services[i];
                int current = StartType(name);
                if (current < 0) continue;
                if (!enable)
                {
                    string previous = current + "," + (Delayed(name) ? "1" : "0");
                    if (!Config(name, "disabled", errors)) continue;
                    if (current != 4) backup[name] = previous;
                    try { using (ServiceController sc = new ServiceController(name)) if (sc.Status != ServiceControllerStatus.Stopped && sc.CanStop) { sc.Stop(); sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15)); } }
                    catch (Exception ex) { errors.Add(name + ": " + ex.Message); }
                }
                else
                {
                    int start = t.DefaultStart[i]; bool delayed = t.DefaultDelayed != null && t.DefaultDelayed[i];
                    string saved;
                    if (backup.TryGetValue(name, out saved))
                    {
                        string[] p = saved.Split(','); int s;
                        if (p.Length == 2 && int.TryParse(p[0], out s) && s >= 2 && s <= 3) { start = s; delayed = p[1] == "1"; }
                    }
                    if (!Config(name, start == 2 ? (delayed ? "delayed-auto" : "auto") : "demand", errors)) continue;
                    backup.Remove(name);
                    if (start == 2)
                        try { using (ServiceController sc = new ServiceController(name)) if (sc.Status == ServiceControllerStatus.Stopped) sc.Start(); } catch { }   // a manual-start service starts on its own when needed
                }
            }
            WriteBackup(backup);
            Refresh(t);
            bool ok = errors.Count == 0;
            if (!ok) message = string.Join("\n", errors);
            try { Directory.CreateDirectory(Settings.DataDir); File.AppendAllText(Settings.LogPath, "SERVICE | " + DateTime.Now.ToString("s") + " | id=" + t.Id + " enabled=" + enable + " ok=" + ok + Environment.NewLine); } catch { }
            return ok;
        }

        // sc.exe with a constant service name from the allowlist and a constant start keyword.
        private static bool Config(string name, string start, List<string> errors)
        {
            CommandResult r = PowerTools.Exec("sc.exe", "config " + name + " start= " + start, 20000);
            if (r.Ok) return true;
            errors.Add(name + ": " + (r.Started ? (r.Output + " " + r.Error).Trim() : "sc.exe not available"));
            return false;
        }

        // Registry Start value: 2 auto, 3 manual, 4 disabled; -1 = not installed.
        private static int StartType(string name)
        {
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + name))
                {
                    if (k == null) return -1;
                    object v = k.GetValue("Start");
                    return v is int ? (int)v : -1;
                }
            }
            catch { return -1; }
        }

        private static bool Delayed(string name)
        {
            try { using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + name)) { object v = k == null ? null : k.GetValue("DelayedAutostart"); return v is int && (int)v == 1; } }
            catch { return false; }
        }

        private static Dictionary<string, string> ReadBackup()
        {
            Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try { if (File.Exists(BackupPath)) foreach (string line in File.ReadAllLines(BackupPath)) { string[] p = line.Split(new[] { '=' }, 2); if (p.Length == 2) d[p[0]] = p[1]; } } catch { }
            return d;
        }

        private static void WriteBackup(Dictionary<string, string> d)
        {
            try { Directory.CreateDirectory(Settings.DataDir); File.WriteAllLines(BackupPath, d.Select(kv => kv.Key + "=" + kv.Value)); } catch { }
        }

        private static ServiceTweak T(string id, string[] services, int[] defaults, string nameTr, string nameEn, string whatTr, string whatEn, string effectTr, string effectEn)
        {
            return new ServiceTweak { Id = id, Services = services, DefaultStart = defaults, NameTr = nameTr, NameEn = nameEn, WhatTr = whatTr, WhatEn = whatEn, EffectTr = effectTr, EffectEn = effectEn };
        }
    }
}
