// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace ProjectMantis
{
    // One per-user DWORD. Default = the Windows default (null = value absent).
    internal sealed class RegSetting
    {
        public string Path, Name; public int On; public int? Default;
        public RegSetting(string path, string name, int on, int? def) { Path = path; Name = name; On = on; Default = def; }
    }

    internal sealed class QolTweak
    {
        public string Id, GroupTr, GroupEn, NameTr, NameEn, DescTr, DescEn;
        public List<RegSetting> Values = new List<RegSetting>();
        public string ClassicMenuKey;    // set only for the Windows 11 classic context menu (a key, not a value)
        public bool NeedsExplorer;       // takes effect after Explorer restarts
        public bool Win11Only;
        public bool Applied;
    }

    // Quality-of-life tweaks (spec B.4). Every tweak is a handful of HKCU values written with the Registry class
    // (no admin, no reg.exe). Before applying, the previous state is saved; "back to default" restores that
    // saved state, or the Windows default when nothing was saved.
    internal static class QoLTweaks
    {
        private const string Adv = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        private const string Cdm = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
        private const string Classic = @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}";
        private static string BackupPath { get { return Path.Combine(Settings.DataDir, "tweaks-backup.ini"); } }

        public static bool IsWindows11
        {
            get
            {
                try { using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion")) { int b; return k != null && int.TryParse(Convert.ToString(k.GetValue("CurrentBuildNumber")), out b) && b >= 22000; } }
                catch { return false; }
            }
        }

        public static List<QolTweak> List()
        {
            List<QolTweak> list = new List<QolTweak>
            {
                Make("bing", "ARAMA", "SEARCH", "Başlat aramasında web/Bing sonuçlarını kapat", "Turn off web/Bing results in Start search",
                    "Başlat'ta yazınca yalnızca uygulamalar, ayarlar ve dosyalar çıkar; web önerileri gelmez.", "Typing in Start shows only apps, settings and files; no web suggestions.", true,
                    new RegSetting(@"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1, null),
                    new RegSetting(@"Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled", 0, null)),
                Make("extensions", "DOSYA GEZGİNİ", "FILE EXPLORER", "Dosya uzantılarını göster", "Show file extensions",
                    "\"fatura.pdf.exe\" gibi aldatıcı adları fark etmeni sağlar.", "Helps you spot deceptive names like \"invoice.pdf.exe\".", true,
                    new RegSetting(Adv, "HideFileExt", 0, 1)),
                Make("hidden", "DOSYA GEZGİNİ", "FILE EXPLORER", "Gizli dosyaları göster", "Show hidden files",
                    "Gizli klasör ve dosyalar soluk olarak görünür. Sistem dosyaları gizli kalır.", "Hidden files and folders appear faded. Protected system files stay hidden.", true,
                    new RegSetting(Adv, "Hidden", 1, 2)),
                Make("thispc", "DOSYA GEZGİNİ", "FILE EXPLORER", "Gezgin'i \"Bu bilgisayar\"da aç", "Open Explorer to \"This PC\"",
                    "Gezgin, Giriş yerine sürücülerinle açılır.", "Explorer opens on your drives instead of Home.", false,
                    new RegSetting(Adv, "LaunchTo", 1, null)),
                Make("searchbox", "GÖREV ÇUBUĞU", "TASKBAR", "Arama kutusunu gizle", "Hide the search box",
                    "Görev çubuğunda yer açar. Başlat'a basıp yazınca arama yine çalışır.", "Frees taskbar space. Search still works when you press Start and type.", false,
                    new RegSetting(@"Software\Microsoft\Windows\CurrentVersion\Search", "SearchboxTaskbarMode", 0, null)),
                Make("taskview", "GÖREV ÇUBUĞU", "TASKBAR", "Görev görünümü düğmesini gizle", "Hide the Task View button",
                    "Win+Tab kısayolu çalışmaya devam eder.", "The Win+Tab shortcut keeps working.", false,
                    new RegSetting(Adv, "ShowTaskViewButton", 0, 1)),
                Make("widgets", "GÖREV ÇUBUĞU", "TASKBAR", "Pencere öğelerini (Widgets) gizle", "Hide Widgets",
                    "Hava durumu/haber düğmesini kaldırır. Bazı Windows 11 sürümleri bu ayarı kilitler.", "Removes the weather/news button. Some Windows 11 builds lock this setting.", false,
                    new RegSetting(Adv, "TaskbarDa", 0, null)),
                Make("suggestions", "ÖNERİLER", "SUGGESTIONS", "Reklam ve önerileri azalt", "Fewer ads and suggestions",
                    "Başlat önerileri, ipucu bildirimleri, kilit ekranı tanıtımları ve kendiliğinden kurulan uygulamalar kapanır.", "Turns off Start suggestions, tip notifications, lock-screen promos and silently installed apps.", false,
                    new RegSetting(Cdm, "SubscribedContent-338388Enabled", 0, null), new RegSetting(Cdm, "SubscribedContent-338389Enabled", 0, null),
                    new RegSetting(Cdm, "SystemPaneSuggestionsEnabled", 0, null), new RegSetting(Cdm, "SilentInstalledAppsEnabled", 0, null),
                    new RegSetting(Cdm, "SoftLandingEnabled", 0, null), new RegSetting(@"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0, null)),
            };
            QolTweak menu = Make("classicmenu", "BAĞLAM MENÜSÜ", "CONTEXT MENU", "Klasik sağ tık menüsü", "Classic right-click menu",
                "Windows 11'de \"Daha fazla seçenek göster\"e basmadan tam menü açılır.", "On Windows 11 the full menu opens without \"Show more options\".", true);
            menu.ClassicMenuKey = Classic; menu.Win11Only = true;
            list.Insert(4, menu);
            bool win11 = IsWindows11;
            list = list.Where(t => win11 || !t.Win11Only).ToList();
            foreach (QolTweak t in list) t.Applied = IsApplied(t);
            return list;
        }

        public static bool IsApplied(QolTweak t)
        {
            try
            {
                if (t.ClassicMenuKey != null) { using (RegistryKey k = Registry.CurrentUser.OpenSubKey(t.ClassicMenuKey + @"\InprocServer32")) return k != null; }
                foreach (RegSetting s in t.Values)
                    using (RegistryKey k = Registry.CurrentUser.OpenSubKey(s.Path))
                    {
                        object v = k == null ? null : k.GetValue(s.Name);
                        if (!(v is int) || (int)v != s.On) return false;
                    }
                return true;
            }
            catch { return false; }
        }

        public static bool Apply(QolTweak t, out string message)
        {
            message = null;
            Dictionary<string, string> backup = ReadBackup();
            try
            {
                if (t.ClassicMenuKey != null)
                {
                    using (RegistryKey k = Registry.CurrentUser.CreateSubKey(t.ClassicMenuKey + @"\InprocServer32")) k.SetValue("", "");
                }
                else foreach (RegSetting s in t.Values)
                {
                    string id = BackupId(t, s);
                    using (RegistryKey k = Registry.CurrentUser.CreateSubKey(s.Path))
                    {
                        if (!backup.ContainsKey(id)) { object v = k.GetValue(s.Name); backup[id] = v is int ? ((int)v).ToString() : "absent"; }
                        k.SetValue(s.Name, s.On, RegistryValueKind.DWord);
                    }
                }
                return Done(t, true, backup, null, ref message);
            }
            catch (Exception ex) { return Done(t, true, backup, ex, ref message); }
        }

        public static bool Revert(QolTweak t, out string message)
        {
            message = null;
            Dictionary<string, string> backup = ReadBackup();
            try
            {
                if (t.ClassicMenuKey != null) Registry.CurrentUser.DeleteSubKeyTree(t.ClassicMenuKey, false);
                else foreach (RegSetting s in t.Values)
                {
                    string id = BackupId(t, s), saved; int prev;
                    int? target = s.Default;
                    if (backup.TryGetValue(id, out saved)) target = int.TryParse(saved, out prev) ? (int?)prev : null;
                    using (RegistryKey k = Registry.CurrentUser.CreateSubKey(s.Path))
                    {
                        if (target.HasValue) k.SetValue(s.Name, target.Value, RegistryValueKind.DWord); else k.DeleteValue(s.Name, false);
                    }
                    backup.Remove(id);
                }
                return Done(t, false, backup, null, ref message);
            }
            catch (Exception ex) { return Done(t, false, backup, ex, ref message); }
        }

        // Restarts Explorer so taskbar and Explorer changes show up. Open Explorer windows close.
        public static bool RestartExplorer()
        {
            try
            {
                int session = Process.GetCurrentProcess().SessionId;
                foreach (Process p in Process.GetProcessesByName("explorer")) { try { if (p.SessionId == session) { p.Kill(); p.WaitForExit(5000); } } catch { } finally { p.Dispose(); } }
                Thread.Sleep(1500);   // Windows normally restarts the shell itself
                if (!Process.GetProcessesByName("explorer").Any(p => p.SessionId == session))
                    Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe")) { UseShellExecute = true });
                return true;
            }
            catch { return false; }
        }

        private static bool Done(QolTweak t, bool apply, Dictionary<string, string> backup, Exception ex, ref string message)
        {
            WriteBackup(backup);
            t.Applied = IsApplied(t);
            bool ok = ex == null && t.Applied == apply;
            if (ex != null) message = ex is UnauthorizedAccessException || ex is System.Security.SecurityException ? "access denied" : ex.Message;
            else if (!ok) message = "locked";   // e.g. TaskbarDa on Windows 11 builds that protect it
            try { Directory.CreateDirectory(Settings.DataDir); File.AppendAllText(Settings.LogPath, "TWEAK | " + DateTime.Now.ToString("s") + " | id=" + t.Id + " applied=" + apply + " ok=" + ok + Environment.NewLine); } catch { }
            return ok;
        }

        private static string BackupId(QolTweak t, RegSetting s) { return t.Id + "|" + s.Path + "|" + s.Name; }

        private static Dictionary<string, string> ReadBackup()
        {
            Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try { if (File.Exists(BackupPath)) foreach (string line in File.ReadAllLines(BackupPath)) { int eq = line.LastIndexOf('='); if (eq > 0) d[line.Substring(0, eq)] = line.Substring(eq + 1); } } catch { }
            return d;
        }

        private static void WriteBackup(Dictionary<string, string> d)
        {
            try { Directory.CreateDirectory(Settings.DataDir); File.WriteAllLines(BackupPath, d.Select(kv => kv.Key + "=" + kv.Value)); } catch { }
        }

        private static QolTweak Make(string id, string groupTr, string groupEn, string nameTr, string nameEn, string descTr, string descEn, bool explorer, params RegSetting[] values)
        {
            QolTweak t = new QolTweak { Id = id, GroupTr = groupTr, GroupEn = groupEn, NameTr = nameTr, NameEn = nameEn, DescTr = descTr, DescEn = descEn, NeedsExplorer = explorer };
            t.Values.AddRange(values);
            return t;
        }
    }
}
