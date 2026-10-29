// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ProjectMantis
{
    // Quick Tools page (spec B.5): shortcuts, network info, large/duplicate files, hosts and power timeouts.
    public partial class MainWindow
    {
        private StackPanel quickBody;
        private StackPanel diskResults;
        private StackPanel netBody;
        private StackPanel powerBody;
        private string publicIp;
        private CancellationTokenSource diskScan;
        private static readonly int[] TimeoutChoices = { 1, 5, 10, 15, 30, 60, 0 };

        private void ShowQuickTools()
        {
            Grid page = PageGrid();
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Button back = PillButton(language == "tr" ? "Araçlar" : "Tools", ButtonKind.Ghost, delegate { ShowTools(); return Task.FromResult(0); }, null, Theme.SectionTools);
            page.Children.Add(Header(language == "tr" ? "ARAÇLAR / HIZLI" : "TOOLS / QUICK", Theme.SectionTools, language == "tr" ? "Hızlı Araçlar" : "Quick Tools",
                language == "tr" ? "Her gün lazım olan küçük şeyler, tek ekranda." : "The small things you need every day, on one screen.", back));
            quickBody = new StackPanel();
            ScrollViewer scroller = Scroll(quickBody); Grid.SetRow(scroller, 1); page.Children.Add(scroller);
            SetPage(page, "tools");

            quickBody.Children.Add(SectionLabel(language == "tr" ? "SIK KULLANILAN YERLER" : "FREQUENT PLACES", true));
            quickBody.Children.Add(PlacesRow());

            quickBody.Children.Add(SectionLabel(language == "tr" ? "AĞ BİLGİSİ" : "NETWORK INFO", false));
            netBody = new StackPanel(); quickBody.Children.Add(netBody); RenderNetwork();

            quickBody.Children.Add(SectionLabel(language == "tr" ? "DİSK: BÜYÜK VE YİNELENEN DOSYALAR" : "DISK: LARGE AND DUPLICATE FILES", false));
            quickBody.Children.Add(DiskCard());

            quickBody.Children.Add(SectionLabel("HOSTS", false));
            quickBody.Children.Add(HostsRow());

            quickBody.Children.Add(SectionLabel(language == "tr" ? "GÜÇ VE UYKU" : "POWER AND SLEEP", false));
            powerBody = new StackPanel(); quickBody.Children.Add(powerBody); RenderPower();
            quickBody.Children.Add(new Border { Height = Theme.S4 });
        }

        // ---------- Places ----------

        private FrameworkElement PlacesRow()
        {
            string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            var places = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>(language == "tr" ? "İndirilenler" : "Downloads", Path.Combine(user, "Downloads")),
                new KeyValuePair<string, string>(language == "tr" ? "Ekran görüntüleri" : "Screenshots", Path.Combine(pictures, "Screenshots")),
                new KeyValuePair<string, string>(language == "tr" ? "Masaüstü" : "Desktop", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)),
                new KeyValuePair<string, string>("AppData", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)),
                new KeyValuePair<string, string>("Temp", Path.GetTempPath()),
                new KeyValuePair<string, string>(language == "tr" ? "Başlangıç klasörü" : "Startup folder", Environment.GetFolderPath(Environment.SpecialFolder.Startup)),
                new KeyValuePair<string, string>(language == "tr" ? "Mantis raporları" : "Mantis reports", SecurityReport.Folder),
            };
            WrapPanel wrap = new WrapPanel { Margin = new Thickness(0, 0, 0, Theme.S2) };
            foreach (var p in places)
            {
                string path = p.Value;
                Button b = PillButton(p.Key, ButtonKind.Secondary, delegate { OpenFolder(path); return Task.FromResult(0); }, Icons.Folder, Theme.SectionTools);
                b.Height = 34; b.MinWidth = 0; b.Margin = new Thickness(0, 0, Theme.S2, Theme.S2); wrap.Children.Add(b);
            }
            return wrap;
        }

        private void OpenFolder(string path)
        {
            try
            {
                if (!Directory.Exists(path)) { MessageBox.Show((language == "tr" ? "Klasör bulunamadı:\n" : "Folder not found:\n") + path, "Project Mantis", MessageBoxButton.OK, MessageBoxImage.Information); return; }
                Process.Start(new ProcessStartInfo("explorer.exe", PowerTools.QuoteArg(path)) { UseShellExecute = true });
            }
            catch { }
        }

        // ---------- Network ----------

        private void RenderNetwork()
        {
            netBody.Children.Clear();
            List<AdapterInfo> adapters = QuickTools.Adapters();
            if (adapters.Count == 0) { netBody.Children.Add(Paragraph(language == "tr" ? "Etkin bir ağ bağlantısı bulunamadı." : "No active network connection was found.", Theme.Small, Theme.TextMuted)); return; }
            foreach (AdapterInfo a in adapters)
            {
                Border card = Glass(Theme.RadiusMd); card.Margin = new Thickness(0, 0, 0, Theme.S2); card.Padding = new Thickness(Theme.S4, Theme.S3, Theme.S4, Theme.S3);
                StackPanel s = new StackPanel();
                TextBlock tag = Eyebrow((a.Wireless ? "WI-FI" : "ETHERNET") + (a.Speed > 0 ? "  ·  " + (a.Speed >= 1000000000 ? (a.Speed / 1000000000.0).ToString("0.#") + " Gbps" : (a.Speed / 1000000) + " Mbps") : ""), Theme.SectionTools);
                s.Children.Add(tag);
                TextBlock name = Label(a.Name, Theme.Body + 1, FontWeights.Medium, Theme.TextPrimary); name.Margin = new Thickness(0, 3, 0, Theme.S2); s.Children.Add(name);
                s.Children.Add(CopyRow(language == "tr" ? "Yerel IP" : "Local IP", a.Ipv4));
                if (!string.IsNullOrEmpty(a.Ipv6)) s.Children.Add(CopyRow("IPv6", a.Ipv6));
                s.Children.Add(CopyRow(language == "tr" ? "Ağ geçidi" : "Gateway", a.Gateway));
                s.Children.Add(CopyRow("DNS", a.Dns));
                s.Children.Add(CopyRow("MAC", a.Mac));
                card.Child = s; netBody.Children.Add(card);
            }
            Grid pub = new Grid { Margin = new Thickness(0, Theme.S1, 0, Theme.S2) };
            pub.ColumnDefinitions.Add(new ColumnDefinition()); pub.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            if (publicIp != null) pub.Children.Add(CopyRow(language == "tr" ? "Genel IP" : "Public IP", publicIp));
            else pub.Children.Add(Paragraph(language == "tr" ? "Genel IP'n yalnızca istersen öğrenilir: api.ipify.org'a tek bir istek gider, başka veri gönderilmez." : "Your public IP is looked up only if you ask: one request goes to api.ipify.org, nothing else is sent.", Theme.Small, Theme.TextFaint));
            Button show = PillButton(language == "tr" ? "Genel IP'yi göster" : "Show public IP", ButtonKind.Secondary, async delegate
            {
                string ip = await Task.Run(() => QuickTools.PublicIp());
                if (ip == null) { MessageBox.Show(language == "tr" ? "Genel IP alınamadı. İnternet bağlantını kontrol et." : "Could not get the public IP. Check your internet connection.", "Project Mantis", MessageBoxButton.OK, MessageBoxImage.Information); return; }
                publicIp = ip; RenderNetwork();
            }, Icons.Globe, Theme.SectionTools);
            show.Height = 34; show.MinWidth = 0; show.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(show, 1); show.Margin = new Thickness(Theme.S3, 0, 0, 0);
            pub.Children.Add(show);
            netBody.Children.Add(pub);
        }

        // Label + value; clicking copies the value.
        private FrameworkElement CopyRow(string label, string value)
        {
            Grid g = new Grid { Margin = new Thickness(0, 1, 0, 1), Background = Brushes.Transparent, Cursor = System.Windows.Input.Cursors.Hand, ToolTip = language == "tr" ? "Kopyalamak için tıkla" : "Click to copy" };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) }); g.ColumnDefinitions.Add(new ColumnDefinition());
            g.Children.Add(new TextBlock { Text = label.ToUpper(UiCulture), FontFamily = Theme.MonoFont, FontSize = 10.5, Foreground = Theme.Brush(Theme.TextFaint), VerticalAlignment = VerticalAlignment.Center });
            TextBlock v = new TextBlock { Text = string.IsNullOrEmpty(value) ? "—" : value, FontFamily = Theme.MonoFont, FontSize = 12.5, Foreground = Theme.Brush(Theme.TextPrimary), TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(v, 1); g.Children.Add(v);
            g.MouseLeftButtonUp += delegate
            {
                if (string.IsNullOrEmpty(value)) return;
                try { Clipboard.SetText(value); statusText.Text = (language == "tr" ? "Kopyalandı: " : "Copied: ") + label; } catch { }
            };
            return g;
        }

        // ---------- Disk ----------

        private Border DiskCard()
        {
            Border card = Glass(Theme.RadiusMd); card.Margin = new Thickness(0, 0, 0, Theme.S2); card.Padding = new Thickness(Theme.S4, Theme.S3 + 2, Theme.S4, Theme.S3 + 2);
            StackPanel s = new StackPanel();
            Grid top = new Grid(); top.ColumnDefinitions.Add(new ColumnDefinition()); top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            top.Children.Add(Paragraph(language == "tr" ? "Bir klasör seç: en büyük dosyalar ve alt klasörler listelenir. İstersen 1 MB üstü yinelenen dosyalar içerik özetiyle (SHA-256) bulunur. Hiçbir şey kendiliğinden silinmez." : "Pick a folder to list its largest files and subfolders. Optionally, duplicate files over 1 MB are found by content hash (SHA-256). Nothing is deleted on its own.", Theme.Small, Theme.TextMuted));
            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(Theme.S3, 0, 0, 0) };
            Button large = PillButton(language == "tr" ? "Büyük dosyalar" : "Large files", ButtonKind.Secondary, async delegate { await RunDiskScan(false); }, Icons.Search, Theme.SectionTools);
            Button dup = PillButton(language == "tr" ? "Yinelenenler" : "Duplicates", ButtonKind.Secondary, async delegate { await RunDiskScan(true); }, Icons.Layers, Theme.SectionTools);
            large.Height = dup.Height = 34; large.MinWidth = dup.MinWidth = 0; dup.Margin = new Thickness(Theme.S2, 0, 0, 0);
            buttons.Children.Add(large); buttons.Children.Add(dup); Grid.SetColumn(buttons, 1); top.Children.Add(buttons);
            s.Children.Add(top);
            diskResults = new StackPanel { Margin = new Thickness(0, Theme.S2, 0, 0) }; s.Children.Add(diskResults);
            card.Child = s;
            return card;
        }

        private async Task RunDiskScan(bool duplicates)
        {
            if (diskScan != null) { diskScan.Cancel(); return; }
            string folder;
            using (System.Windows.Forms.FolderBrowserDialog dlg = new System.Windows.Forms.FolderBrowserDialog { Description = language == "tr" ? "Taranacak klasör" : "Folder to scan", SelectedPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads") })
            {
                if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                folder = dlg.SelectedPath;
            }
            diskScan = new CancellationTokenSource();
            CancellationToken token = diskScan.Token;
            diskResults.Children.Clear();
            TextBlock progress = Paragraph(language == "tr" ? "Taranıyor…" : "Scanning…", Theme.Small, Theme.TextMuted); diskResults.Children.Add(progress);
            Button stop = PillButton(language == "tr" ? "Durdur" : "Stop", ButtonKind.Ghost, delegate { if (diskScan != null) diskScan.Cancel(); return Task.FromResult(0); }, null, Theme.Warning);
            stop.HorizontalAlignment = HorizontalAlignment.Left; diskResults.Children.Add(stop);
            FolderScan result = null;
            try
            {
                result = await Task.Run(() => QuickTools.ScanFolder(folder, 15, duplicates, token, delegate(int n) { Dispatcher.BeginInvoke(new Action(delegate { progress.Text = (language == "tr" ? "Taranıyor… " : "Scanning… ") + n.ToString("N0", UiCulture) + (language == "tr" ? " dosya" : " files"); })); }));
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { progress.Text = ex.Message; }
            diskScan = null;
            diskResults.Children.Clear();
            if (result == null) { diskResults.Children.Add(Paragraph(language == "tr" ? "Tarama durduruldu." : "Scan stopped.", Theme.Small, Theme.TextMuted)); return; }
            RenderDisk(result, duplicates);
        }

        private void RenderDisk(FolderScan r, bool duplicates)
        {
            diskResults.Children.Clear();
            diskResults.Children.Add(Paragraph(r.Root + "  ·  " + r.Files.ToString("N0", UiCulture) + (language == "tr" ? " dosya, " : " files, ") + FormatBytes(r.Total) + (r.Skipped > 0 ? (language == "tr" ? "  ·  " + r.Skipped + " öğe erişilemedi" : "  ·  " + r.Skipped + " items not accessible") : ""), Theme.Small, Theme.TextMuted));
            if (duplicates)
            {
                if (r.Duplicates.Count == 0) { diskResults.Children.Add(Paragraph(language == "tr" ? "1 MB üstü yinelenen dosya bulunamadı." : "No duplicate files over 1 MB were found.", Theme.Small, Theme.Success)); return; }
                diskResults.Children.Add(Paragraph((language == "tr" ? r.Duplicates.Count + " grup · fazladan kopyalar " : r.Duplicates.Count + " groups · extra copies take ") + FormatBytes(r.DuplicateWaste), Theme.Small, Theme.Warning));
                foreach (List<FileHit> group in r.Duplicates.Take(30))
                {
                    TextBlock head = Eyebrow(group.Count + " × " + FormatBytes(group[0].Size), Theme.SectionTools); head.Margin = new Thickness(0, Theme.S3, 0, 2); diskResults.Children.Add(head);
                    for (int i = 0; i < group.Count; i++) diskResults.Children.Add(FileLine(group[i], i > 0, group));
                }
                return;
            }
            TextBlock f1 = Eyebrow(language == "tr" ? "EN BÜYÜK DOSYALAR" : "LARGEST FILES", Theme.SectionTools); f1.Margin = new Thickness(0, Theme.S3, 0, 2); diskResults.Children.Add(f1);
            foreach (FileHit f in r.Largest) diskResults.Children.Add(FileLine(f, false, null));
            if (r.Folders.Count > 0)
            {
                TextBlock f2 = Eyebrow(language == "tr" ? "EN BÜYÜK ALT KLASÖRLER" : "LARGEST SUBFOLDERS", Theme.SectionTools); f2.Margin = new Thickness(0, Theme.S3, 0, 2); diskResults.Children.Add(f2);
                foreach (FileHit f in r.Folders) diskResults.Children.Add(FileLine(f, false, null));
            }
        }

        // Size, path, "show" and (for an extra duplicate copy) "to Recycle Bin".
        private FrameworkElement FileLine(FileHit f, bool extraCopy, List<FileHit> group)
        {
            Grid g = new Grid { Margin = new Thickness(0, 1, 0, 1) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(86) }); g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(new TextBlock { Text = FormatBytes(f.Size), FontFamily = Theme.MonoFont, FontSize = 11.5, Foreground = Theme.Brush(Theme.TextMuted), VerticalAlignment = VerticalAlignment.Center });
            TextBlock p = new TextBlock { Text = f.Path, FontSize = Theme.Small, Foreground = Theme.Brush(Theme.TextPrimary), TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, ToolTip = f.Path };
            Grid.SetColumn(p, 1); g.Children.Add(p);
            Button show = LinkButton(language == "tr" ? "Göster" : "Show", Theme.SectionTools, delegate { SecurityFixes.Reveal(f.Path); });
            show.Margin = new Thickness(Theme.S3, 0, 0, 0); Grid.SetColumn(show, 2); g.Children.Add(show);
            if (extraCopy)
            {
                Button recycle = LinkButton(language == "tr" ? "Geri Dönüşüm'e taşı" : "Move to Recycle Bin", Theme.Warning, delegate
                {
                    string keep = group[0].Path;
                    if (MessageBox.Show((language == "tr" ? "Bu kopya Geri Dönüşüm Kutusu'na taşınacak:\n\n" : "This copy will be moved to the Recycle Bin:\n\n") + f.Path + (language == "tr" ? "\n\nAynı içerik şurada kalıyor:\n" : "\n\nThe same content stays at:\n") + keep + (language == "tr" ? "\n\nGeri Dönüşüm Kutusu'ndan geri yükleyebilirsin. Devam edilsin mi?" : "\n\nYou can restore it from the Recycle Bin. Continue?"), "Project Mantis", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                    if (!File.Exists(keep)) { MessageBox.Show(language == "tr" ? "Saklanacak kopya artık yok; işlem yapılmadı." : "The copy to keep no longer exists; nothing was done.", "Project Mantis"); return; }
                    if (QuickTools.Recycle(new[] { f.Path })) { g.Opacity = 0.4; g.IsEnabled = false; statusText.Text = language == "tr" ? "Geri Dönüşüm Kutusu'na taşındı" : "Moved to the Recycle Bin"; }
                    else MessageBox.Show(language == "tr" ? "Dosya taşınamadı (kullanımda olabilir)." : "The file could not be moved (it may be in use).", "Project Mantis", MessageBoxButton.OK, MessageBoxImage.Warning);
                });
                recycle.Margin = new Thickness(Theme.S3, 0, 0, 0); Grid.SetColumn(recycle, 3); g.Children.Add(recycle);
            }
            return g;
        }

        // ---------- Hosts ----------

        private FrameworkElement HostsRow()
        {
            string text = QuickTools.ReadHosts();
            int entries = text == null ? 0 : text.Split('\n').Count(l => l.Trim().Length > 0 && !l.TrimStart().StartsWith("#"));
            StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal };
            Button open = PillButton(language == "tr" ? "Görüntüle / düzenle" : "View / edit", ButtonKind.Secondary, delegate { ShowHostsEditor(); return Task.FromResult(0); }, Icons.FileText, Theme.SectionTools);
            open.Height = 34; open.MinWidth = 0; actions.Children.Add(open);
            string detail = text == null ? (language == "tr" ? "Hosts dosyası okunamadı." : "The hosts file could not be read.")
                : (language == "tr" ? entries + " etkin satır. Kaydetmeden önce zaman damgalı bir yedek alınır; kaydetmek yönetici izni ister." : entries + " active lines. A timestamped backup is taken before saving; saving needs administrator rights.");
            return ListRow(Icons.FileText, null, QuickTools.HostsPath, detail, Theme.SectionTools, actions);
        }

        private void ShowHostsEditor()
        {
            string text = QuickTools.ReadHosts();
            if (text == null) { MessageBox.Show(language == "tr" ? "Hosts dosyası okunamadı." : "The hosts file could not be read.", "Hosts", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            Window dialog = ThemedDialog("Hosts", 760, 560);
            dialog.ResizeMode = ResizeMode.CanResize;
            Grid root = new Grid { Margin = new Thickness(Theme.S4) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            TextBlock info = Paragraph(IsAdmin()
                ? (language == "tr" ? "Değişiklikler 'Kaydet' ile yazılır; önce mevcut dosyanın yedeği alınır." : "Changes are written on Save; the current file is backed up first.")
                : (language == "tr" ? "Salt okunur: düzenlemek için Mantis'i yönetici olarak aç." : "Read-only: open Mantis as administrator to edit."), Theme.Small, Theme.TextMuted);
            info.Margin = new Thickness(0, 0, 0, Theme.S3); root.Children.Add(info);
            TextBox box = new TextBox
            {
                Text = text, AcceptsReturn = true, AcceptsTab = true, IsReadOnly = !IsAdmin(), FontFamily = Theme.MonoFont, FontSize = 12.5,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = Theme.Brush(Theme.BgBase), Foreground = Theme.Brush(Theme.TextPrimary), BorderBrush = Theme.Brush(Theme.Stroke), CaretBrush = Theme.Brush(Theme.AccentPrimary), Padding = new Thickness(Theme.S2)
            };
            Grid.SetRow(box, 1); root.Children.Add(box);
            StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, Theme.S3, 0, 0) };
            Button backups = PillButton(language == "tr" ? "Yedek klasörü" : "Backups folder", ButtonKind.Ghost, delegate { Directory.CreateDirectory(QuickTools.HostsBackupDir); OpenFolder(QuickTools.HostsBackupDir); return Task.FromResult(0); }, null, Theme.SectionTools);
            actions.Children.Add(backups);
            if (IsAdmin())
            {
                Button save = PillButton(language == "tr" ? "Kaydet" : "Save", ButtonKind.Primary, delegate
                {
                    if (box.Text == text) { dialog.Close(); return Task.FromResult(0); }
                    if (MessageBox.Show(language == "tr" ? "Hosts dosyası değiştirilecek. Önce mevcut hâlinin yedeği alınır; geri almak için yedeği aynı yere kopyalayabilirsin.\n\nKaydedilsin mi?" : "The hosts file will be changed. The current version is backed up first; to undo, copy the backup back.\n\nSave it?", "Hosts", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return Task.FromResult(0);
                    string backup, message;
                    if (QuickTools.WriteHosts(box.Text, out backup, out message)) { statusText.Text = language == "tr" ? "Hosts kaydedildi (yedek alındı)" : "Hosts saved (backup taken)"; dialog.Close(); }
                    else MessageBox.Show((language == "tr" ? "Kaydedilemedi: " : "Could not save: ") + message, "Hosts", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return Task.FromResult(0);
                }, Icons.Check, Theme.SectionTools);
                save.Margin = new Thickness(Theme.S2, 0, 0, 0); actions.Children.Add(save);
            }
            else
            {
                Button admin = PillButton(language == "tr" ? "Yönetici olarak aç" : "Open as admin", ButtonKind.Secondary, delegate { dialog.Close(); RestartAsAdmin(); return Task.FromResult(0); }, null, Theme.Warning);
                admin.Margin = new Thickness(Theme.S2, 0, 0, 0); actions.Children.Add(admin);
            }
            Grid.SetRow(actions, 2); root.Children.Add(actions);
            dialog.Content = root; dialog.ShowDialog();
        }

        // ---------- Power ----------

        private async void RenderPower()
        {
            powerBody.Children.Clear();
            powerBody.Children.Add(Paragraph(language == "tr" ? "Güç ayarları okunuyor…" : "Reading power settings…", Theme.Small, Theme.TextMuted));
            List<PowerPlan> plans = null; int[] display = null, sleep = null; bool battery = false;
            await Task.Run(() => { plans = QuickTools.PowerPlans(); display = QuickTools.Timeout("SUB_VIDEO", "VIDEOIDLE"); sleep = QuickTools.Timeout("SUB_SLEEP", "STANDBYIDLE"); battery = QuickTools.HasBattery(); });
            powerBody.Children.Clear();
            if (plans.Count > 0)
            {
                WrapPanel wrap = new WrapPanel();
                foreach (PowerPlan p in plans)
                {
                    PowerPlan plan = p;
                    Button b = PillButton(p.Name, p.Active ? ButtonKind.Primary : ButtonKind.Secondary, delegate
                    {
                        if (plan.Active) return Task.FromResult(0);
                        if (MessageBox.Show((language == "tr" ? "Güç planı \"" + plan.Name + "\" yapılacak. Eski plana buradan tek tıkla dönebilirsin." : "The power plan will be set to \"" + plan.Name + "\". You can switch back here with one click.") + (language == "tr" ? "\n\nUygulansın mı?" : "\n\nApply it?"), language == "tr" ? "Güç planı" : "Power plan", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return Task.FromResult(0);
                        PowerTools.SetScheme(plan.Guid); RenderPower(); return Task.FromResult(0);
                    }, Icons.Bolt, Theme.SectionTools);
                    b.Height = 34; b.MinWidth = 0; b.Margin = new Thickness(0, 0, Theme.S2, Theme.S2); wrap.Children.Add(b);
                }
                powerBody.Children.Add(PowerLine(language == "tr" ? "PLAN" : "PLAN", wrap));
            }
            powerBody.Children.Add(TimeoutLine(language == "tr" ? "EKRAN · PRİZ" : "DISPLAY · PLUGGED", "monitor", true, display[0]));
            powerBody.Children.Add(TimeoutLine(language == "tr" ? "UYKU · PRİZ" : "SLEEP · PLUGGED", "standby", true, sleep[0]));
            if (battery)
            {
                powerBody.Children.Add(TimeoutLine(language == "tr" ? "EKRAN · PİL" : "DISPLAY · BATTERY", "monitor", false, display[1]));
                powerBody.Children.Add(TimeoutLine(language == "tr" ? "UYKU · PİL" : "SLEEP · BATTERY", "standby", false, sleep[1]));
            }
        }

        private FrameworkElement PowerLine(string label, UIElement content)
        {
            Grid g = new Grid { Margin = new Thickness(0, 0, 0, Theme.S1) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) }); g.ColumnDefinitions.Add(new ColumnDefinition());
            g.Children.Add(new TextBlock { Text = label, FontFamily = Theme.MonoFont, FontSize = 10.5, Foreground = Theme.Brush(Theme.TextFaint), Margin = new Thickness(0, 10, 0, 0) });
            Grid.SetColumn(content, 1); g.Children.Add(content);
            return g;
        }

        private FrameworkElement TimeoutLine(string label, string kind, bool ac, int current)
        {
            WrapPanel wrap = new WrapPanel();
            foreach (int m in TimeoutChoices)
            {
                int minutes = m;
                Button b = PillButton(MinutesLabel(minutes), minutes == current ? ButtonKind.Primary : ButtonKind.Ghost, delegate
                {
                    if (minutes == current) return Task.FromResult(0);
                    string what = (kind == "monitor" ? (language == "tr" ? "Ekran" : "The display") : (language == "tr" ? "Bilgisayar" : "The PC")) + " "
                        + (language == "tr" ? (ac ? "prizdeyken " : "pildeyken ") : (ac ? "on power " : "on battery "))
                        + (minutes == 0 ? (language == "tr" ? (kind == "monitor" ? "hiç kapanmayacak." : "hiç uykuya geçmeyecek.") : (kind == "monitor" ? "will never turn off." : "will never sleep."))
                            : (language == "tr" ? MinutesLabel(minutes) + " boşta kalınca " + (kind == "monitor" ? "kapanacak." : "uykuya geçecek.") : (kind == "monitor" ? "turns off" : "sleeps") + " after " + MinutesLabel(minutes) + " idle."))
                        + (current >= 0 ? (language == "tr" ? "\nŞu anki: " : "\nCurrently: ") + MinutesLabel(current) : "");
                    if (MessageBox.Show(what + (language == "tr" ? "\n\nUygulansın mı?" : "\n\nApply it?"), language == "tr" ? "Güç ve uyku" : "Power and sleep", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return Task.FromResult(0);
                    if (!QuickTools.SetTimeout(kind, ac, minutes)) MessageBox.Show(language == "tr" ? "Ayar uygulanamadı." : "The setting could not be applied.", "Project Mantis", MessageBoxButton.OK, MessageBoxImage.Warning);
                    RenderPower(); return Task.FromResult(0);
                }, null, Theme.SectionTools);
                b.Height = 30; b.MinWidth = 0; b.Margin = new Thickness(0, 0, Theme.S1 + 2, Theme.S1 + 2); wrap.Children.Add(b);
            }
            return PowerLine(label, wrap);
        }

        private string MinutesLabel(int minutes)
        {
            if (minutes == 0) return language == "tr" ? "Hiç" : "Never";
            if (minutes < 0) return "?";
            if (minutes % 60 == 0) return (minutes / 60) + (language == "tr" ? " sa" : " h");
            return minutes + (language == "tr" ? " dk" : " min");
        }
    }
}
