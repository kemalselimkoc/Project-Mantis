// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ProjectMantis
{
    // Update Center (spec B.1): winget app updates + Windows Update status, in plain language.
    // Scanning is read-only; every update runs only after a confirmation that says exactly what will happen.
    public partial class MainWindow
    {
        private WingetScan wingetScan;
        private bool wingetScanning;
        private WindowsUpdateInfo wuInfo;
        private bool wuScanning;
        private bool updatingPackages;
        private bool stopUpdates;
        private StackPanel updatesActions;
        private StackPanel updatesBody;

        private void ShowUpdates()
        {
            Grid page = PageGrid();
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            updatesActions = new StackPanel { Orientation = Orientation.Horizontal };
            page.Children.Add(Header(language == "tr" ? "GÜNCELLEMELER" : "UPDATES", Theme.SectionUpdates, T("updates"),
                language == "tr" ? "Uygulamalarını ve Windows'u güncel tut. Her güncelleme senin onayınla başlar." : "Keep your apps and Windows current. Every update starts only when you confirm it.", updatesActions));
            updatesBody = new StackPanel();
            ScrollViewer scroller = Scroll(updatesBody); Grid.SetRow(scroller, 1); page.Children.Add(scroller);
            SetPage(page, "updates");
            RenderUpdates();
            // Opening the page is the request: the read-only scan starts once per session, then on demand.
            if (wingetScan == null && !wingetScanning) StartUpdateScan();
        }

        private void StartUpdateScan()
        {
            ScanWinget();
            ScanWindowsUpdate();
        }

        private async void ScanWinget()
        {
            if (wingetScanning || updatingPackages) return;
            wingetScanning = true; RenderUpdates();
            WingetScan scan = await Task.Run(() => WingetService.ListUpgrades());
            // App Installer updates winget itself, so it always goes last.
            scan.Packages = scan.Packages.OrderBy(p => string.Equals(p.Id, "Microsoft.AppInstaller", StringComparison.OrdinalIgnoreCase) ? 1 : 0).ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            wingetScan = scan; wingetScanning = false;
            UpdatesBadge(); RenderUpdates();
        }

        private async void ScanWindowsUpdate()
        {
            if (wuScanning) return;
            wuScanning = true; RenderUpdates();
            WindowsUpdateInfo info = await Task.Run(() => WindowsUpdateStatus.Read());
            wuInfo = info; wuScanning = false;
            RenderUpdates();
        }

        private void UpdatesBadge()
        {
            int n = wingetScan == null ? 0 : wingetScan.Packages.Count(p => p.State == UpdateState.Available || p.State == UpdateState.Failed);
            SetNavBadge("updates", n > 0 ? n.ToString() : null);
        }

        // Rebuilds header actions and body in place (no page transition), keeping the scroll position.
        private void RenderUpdates()
        {
            if (currentPage != "updates" || updatesBody == null) return;
            string accent = Theme.SectionUpdates;
            updatesActions.Children.Clear();
            List<WingetPackage> pending = PendingPackages();
            if (updatingPackages)
            {
                Button stop = PillButton(stopUpdates ? (language == "tr" ? "Duruyor…" : "Stopping…") : (language == "tr" ? "Durdur" : "Stop"), ButtonKind.Danger, delegate { stopUpdates = true; RenderUpdates(); return Task.FromResult(0); }, Icons.Close, Theme.Danger, true);
                stop.IsEnabled = !stopUpdates; updatesActions.Children.Add(stop);
            }
            else
            {
                Button rescan = PillButton(language == "tr" ? "Yeniden tara" : "Scan again", ButtonKind.Secondary, delegate { StartUpdateScan(); return Task.FromResult(0); }, Icons.Refresh, accent);
                rescan.IsEnabled = !wingetScanning; rescan.Margin = new Thickness(0, 0, Theme.S2, 0); updatesActions.Children.Add(rescan);
                if (pending.Count > 0)
                {
                    Button all = PillButton((language == "tr" ? "Tümünü Güncelle" : "Update All") + "  ·  " + pending.Count, ButtonKind.Primary, async delegate { await UpdatePackages(PendingPackages()); }, Icons.Download, accent);
                    all.Margin = new Thickness(0); updatesActions.Children.Add(all);
                }
            }

            updatesBody.Children.Clear();
            Grid summary = new Grid { Margin = new Thickness(0, 0, 0, Theme.S4) };
            summary.ColumnDefinitions.Add(new ColumnDefinition()); summary.ColumnDefinitions.Add(new ColumnDefinition());
            Border apps = AppsSummaryCard(); apps.Margin = new Thickness(0, 0, Theme.S2, 0); summary.Children.Add(apps);
            Border wu = WindowsUpdateCard(); wu.Margin = new Thickness(Theme.S2, 0, 0, 0); Grid.SetColumn(wu, 1); summary.Children.Add(wu);
            updatesBody.Children.Add(summary);

            if (wingetScanning && wingetScan == null)
            {
                updatesBody.Children.Add(LoadingState(language == "tr" ? "winget ile güncellenebilir uygulamalar aranıyor…" : "Looking for app updates with winget…", 4));
                return;
            }
            if (wingetScan == null) return;
            if (wingetScan.WingetMissing)
            {
                updatesBody.Children.Add(EmptyState(Icons.Download, language == "tr" ? "winget bulunamadı" : "winget was not found",
                    language == "tr" ? "Uygulama güncellemeleri Windows'un kendi paket yöneticisi winget ile yapılır. Microsoft Store'daki \"Uygulama Yükleyici\" ile ücretsiz gelir." : "App updates use winget, Windows' own package manager. It comes free with \"App Installer\" from the Microsoft Store.",
                    language == "tr" ? "Uygulama Yükleyici'yi aç" : "Open App Installer", delegate { OpenUrl("ms-windows-store://pdp/?ProductId=9NBLGGH4NNS1"); return Task.FromResult(0); }, accent));
                return;
            }
            if (wingetScan.Failed)
            {
                updatesBody.Children.Add(ErrorState(language == "tr" ? "winget yanıt vermedi" : "winget did not respond", (language == "tr" ? "Liste alınamadı. Biraz sonra yeniden tarayabilirsin. Ayrıntı: " : "The list could not be read. Try scanning again in a moment. Details: ") + wingetScan.Error, false));
                return;
            }
            if (wingetScan.Packages.Count == 0)
            {
                updatesBody.Children.Add(GoodState(language == "tr" ? "Tüm uygulamaların güncel" : "All your apps are up to date", language == "tr" ? "winget'in tanıdığı her uygulama en son sürümünde." : "Every app winget knows about is on its latest version."));
                return;
            }

            updatesBody.Children.Add(SectionLabel(language == "tr" ? "UYGULAMALAR" : "APPS", true));
            foreach (WingetPackage p in wingetScan.Packages) updatesBody.Children.Add(PackageRow(p));
            TextBlock note = Paragraph(language == "tr"
                ? "Güncellemeler winget ile sessiz kurulur; paket lisansları ve kaynak sözleşmeleri senin adına kabul edilir. Bazı kurulumlar Windows yönetici onayı (UAC) isteyebilir, açık uygulama kapanabilir. Geri almak için uygulamanın eski sürümünü yeniden kurman gerekir."
                : "Updates install silently through winget; package licences and source agreements are accepted on your behalf. Some installers ask for Windows administrator approval (UAC) and may close the app. To roll back, reinstall the app's older version.", Theme.Micro, Theme.TextFaint);
            note.Margin = new Thickness(2, Theme.S3, 0, Theme.S2); updatesBody.Children.Add(note);
        }

        private List<WingetPackage> PendingPackages()
        {
            return wingetScan == null ? new List<WingetPackage>() : wingetScan.Packages.Where(p => p.State == UpdateState.Available || p.State == UpdateState.Failed).ToList();
        }

        private Border AppsSummaryCard()
        {
            string accent = Theme.SectionUpdates;
            string big, line, sub;
            if (wingetScan == null || (wingetScanning && wingetScan == null)) { big = "…"; line = language == "tr" ? "uygulama taranıyor" : "scanning apps"; sub = "winget"; }
            else if (wingetScan.WingetMissing) { big = "—"; line = language == "tr" ? "winget yüklü değil" : "winget is not installed"; sub = language == "tr" ? "Uygulama Yükleyici gerekli" : "App Installer required"; }
            else if (wingetScan.Failed) { big = "!"; line = language == "tr" ? "liste alınamadı" : "list unavailable"; sub = "winget"; }
            else
            {
                int left = PendingPackages().Count, done = wingetScan.Packages.Count(p => p.State == UpdateState.Done || p.State == UpdateState.NeedsRestart);
                big = left.ToString();
                line = left == 0 ? (language == "tr" ? "bekleyen uygulama güncellemesi yok" : "no app updates waiting") : (language == "tr" ? "uygulama güncellenebilir" : (left == 1 ? "app can be updated" : "apps can be updated"));
                sub = "winget  ·  " + (language == "tr" ? "son kontrol " : "checked ") + RelativeTime(wingetScan.When) + (done > 0 ? "  ·  " + done + (language == "tr" ? " güncellendi" : " updated") : "");
            }
            return SummaryCard(Icons.Download, accent, language == "tr" ? "UYGULAMALAR" : "APPS", big, line, sub, wingetScanning, null);
        }

        private Border WindowsUpdateCard()
        {
            string accent = Theme.Info;
            string big, line, sub;
            if (wuInfo == null) { big = "…"; line = language == "tr" ? "Windows Update denetleniyor" : "checking Windows Update"; sub = language == "tr" ? "Bu biraz sürebilir" : "This can take a moment"; }
            else if (wuInfo.Failed) { big = "?"; line = language == "tr" ? "durum okunamadı" : "status unavailable"; sub = wuInfo.Error; }
            else
            {
                big = wuInfo.Pending.ToString();
                line = wuInfo.Pending == 0 ? (language == "tr" ? "Windows güncel" : "Windows is up to date") : (language == "tr" ? "Windows güncellemesi bekliyor" : (wuInfo.Pending == 1 ? "Windows update waiting" : "Windows updates waiting"));
                sub = (wuInfo.Important > 0 ? wuInfo.Important + (language == "tr" ? " güvenlik/kritik  ·  " : " security/critical  ·  ") : "") + (wuInfo.LastSearch.HasValue ? (language == "tr" ? "Windows son kontrol: " : "Windows last checked ") + RelativeTime(wuInfo.LastSearch.Value) : (language == "tr" ? "az önce denetlendi" : "checked just now"));
            }
            Button open = PillButton(language == "tr" ? "Windows Update'i aç" : "Open Windows Update", ButtonKind.Ghost, delegate { OpenUrl("ms-settings:windowsupdate"); return Task.FromResult(0); }, Icons.ArrowUpRight, accent);
            Border card = SummaryCard(Icons.ShieldCheck, wuInfo != null && wuInfo.Important > 0 ? Theme.Warning : accent, "WINDOWS UPDATE", big, line, sub, wuScanning, open);
            if (wuInfo != null && wuInfo.RebootRequired)
            {
                Grid g = (Grid)card.Child;
                Border reboot = Badge(language == "tr" ? "Yeniden başlatma bekliyor" : "Restart pending", Theme.Warning);
                reboot.HorizontalAlignment = HorizontalAlignment.Right; reboot.VerticalAlignment = VerticalAlignment.Top;
                Grid.SetColumnSpan(reboot, 2); g.Children.Add(reboot);
            }
            return card;
        }

        private Border SummaryCard(Geometry icon, string color, string eyebrow, string big, string line, string sub, bool scanning, Button action)
        {
            Border card = Card(Theme.RadiusLg, false); card.Padding = new Thickness(Theme.S5 - 4, Theme.S4 + 2, Theme.S5 - 4, Theme.S4);
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition());
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            StackPanel head = new StackPanel { Orientation = Orientation.Horizontal };
            head.Children.Add(IconBadge(icon, color, 36, 18));
            TextBlock eye = Eyebrow(eyebrow, color); eye.VerticalAlignment = VerticalAlignment.Center; eye.Margin = new Thickness(Theme.S3, 0, 0, 0); head.Children.Add(eye);
            Grid.SetColumnSpan(head, 2); g.Children.Add(head);
            StackPanel numbers = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, Theme.S4 - 2, 0, 0) };
            TextBlock n = Label(big, 40, FontWeights.SemiBold, Theme.TextPrimary); Tabular(n); n.Margin = new Thickness(0, -6, Theme.S3, 0); numbers.Children.Add(n);
            if (scanning) Pulse(n, 1, 0.35, 0.7, 0);
            StackPanel text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(Label(line, Theme.Body + 1, FontWeights.Medium, Theme.TextPrimary));
            TextBlock s = Label(sub, Theme.Micro, FontWeights.Normal, Theme.TextMuted); s.Margin = new Thickness(0, 3, 0, 0); text.Children.Add(s);
            numbers.Children.Add(text);
            Grid.SetRow(numbers, 1); Grid.SetColumnSpan(numbers, 2); g.Children.Add(numbers);
            if (action != null)
            {
                action.HorizontalAlignment = HorizontalAlignment.Right; action.VerticalAlignment = VerticalAlignment.Bottom; action.Margin = new Thickness(0, 0, -Theme.S2, -4); action.Height = 34;
                Grid.SetRow(action, 1); Grid.SetColumn(action, 1); g.Children.Add(action);
            }
            card.Child = g;
            return card;
        }

        private Border PackageRow(WingetPackage p)
        {
            string accent = Theme.SectionUpdates;
            string strip = p.State == UpdateState.Done ? Theme.Success : p.State == UpdateState.NeedsRestart ? Theme.Warning : p.State == UpdateState.Failed ? Theme.Danger : p.State == UpdateState.Updating ? Theme.Warning : accent;
            Border row = Glass(Theme.RadiusMd); row.Margin = new Thickness(0, 0, 0, Theme.S2); row.Padding = new Thickness(Theme.S3, Theme.S3, Theme.S3, Theme.S3);
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            g.Children.Add(new Border { Width = 3, CornerRadius = new CornerRadius(1.5), Background = Theme.Brush(strip), Margin = new Thickness(0, 4, Theme.S3, 4) });

            Border avatar = Avatar(p.Name); avatar.Margin = new Thickness(0, 0, Theme.S3 + 2, 0); Grid.SetColumn(avatar, 1); g.Children.Add(avatar);

            StackPanel text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(Label(p.Name, Theme.Body + 1, FontWeights.Medium, Theme.TextPrimary));
            StackPanel meta = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 0) };
            meta.Children.Add(Label(p.Id, Theme.Micro, FontWeights.Normal, Theme.TextFaint));
            if (!string.IsNullOrEmpty(p.Source)) { Border src = Badge(p.Source, Theme.TextMuted); src.Padding = new Thickness(6, 0, 6, 1); src.Margin = new Thickness(Theme.S2, 0, 0, 0); ((TextBlock)src.Child).FontSize = 9.5; meta.Children.Add(src); }
            text.Children.Add(meta);
            Grid.SetColumn(text, 2); g.Children.Add(text);

            // Fixed-width version columns so the arrows line up down the whole list.
            Grid versions = new Grid { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(Theme.S4, 0, Theme.S4, 0) };
            versions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(104) }); versions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); versions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(104) });
            TextBlock from = Label(p.Version, Theme.Small, FontWeights.Normal, Theme.TextMuted); Tabular(from); from.TextAlignment = TextAlignment.Right; from.ToolTip = p.Version; versions.Children.Add(from);
            Viewbox arrow = Glyph(Icons.ArrowRight, 14, Theme.TextFaint); arrow.Margin = new Thickness(Theme.S2, 1, Theme.S2, 0); Grid.SetColumn(arrow, 1); versions.Children.Add(arrow);
            TextBlock to = Label(p.Available, Theme.Small, FontWeights.SemiBold, p.State == UpdateState.Done ? Theme.Success : accent); Tabular(to); to.ToolTip = p.Available; Grid.SetColumn(to, 2); versions.Children.Add(to);
            Grid.SetColumn(versions, 3); g.Children.Add(versions);

            FrameworkElement status = PackageStatus(p);
            status.HorizontalAlignment = HorizontalAlignment.Right; status.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(status, 4); g.Children.Add(status);
            row.Child = g;
            if (!string.IsNullOrEmpty(p.Message)) row.ToolTip = p.Message;
            return row;
        }

        private FrameworkElement PackageStatus(WingetPackage p)
        {
            switch (p.State)
            {
                case UpdateState.Queued: return Badge(language == "tr" ? "Sırada" : "Queued", Theme.TextMuted);
                case UpdateState.Updating:
                    {
                        StackPanel s = new StackPanel { Orientation = Orientation.Horizontal };
                        Border dot = new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(4), Background = Theme.Brush(Theme.Warning), Margin = new Thickness(0, 1, Theme.S2, 0), VerticalAlignment = VerticalAlignment.Center };
                        Pulse(dot, 1, 0.3, 0.6, 0); s.Children.Add(dot);
                        s.Children.Add(Label(language == "tr" ? "Güncelleniyor…" : "Updating…", Theme.Small, FontWeights.SemiBold, Theme.Warning));
                        return s;
                    }
                case UpdateState.Done: return Badge(language == "tr" ? "Güncellendi" : "Updated", Theme.Success);
                case UpdateState.NeedsRestart: return Badge(language == "tr" ? "Yeniden başlat" : "Restart needed", Theme.Warning);
                case UpdateState.Failed:
                    {
                        Button retry = PillButton(language == "tr" ? "Tekrar dene" : "Retry", ButtonKind.Danger, async delegate { await UpdatePackages(new List<WingetPackage> { p }); });
                        retry.MinWidth = 0; retry.Height = 34; retry.Margin = new Thickness(0); retry.ToolTip = p.Message;
                        return retry;
                    }
                default:
                    {
                        Button update = PillButton(language == "tr" ? "Güncelle" : "Update", ButtonKind.Secondary, async delegate { await UpdatePackages(new List<WingetPackage> { p }); }, null, Theme.SectionUpdates);
                        update.MinWidth = 104; update.Height = 34; update.Margin = new Thickness(0); update.IsEnabled = !updatingPackages;
                        return update;
                    }
            }
        }

        // Letter avatar tinted by a stable hash of the app name (real app icons are not reliably available).
        private Border Avatar(string name)
        {
            string[] palette = { Theme.Violet, Theme.Info, Theme.Success, Theme.Warning, Theme.Amber };
            int hash = 0; foreach (char c in name ?? "") hash = (hash * 31 + c) & 0x7FFFFFFF;
            string color = palette[hash % palette.Length];
            string letter = string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpper(UiCulture);
            TextBlock t = Label(letter, Theme.H3, FontWeights.SemiBold, color); t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center;
            return new Border { Width = 38, Height = 38, CornerRadius = new CornerRadius(11), Background = Theme.Brush(Theme.Alpha(color, 0x1C)), BorderBrush = Theme.Brush(Theme.Alpha(color, 0x2B)), BorderThickness = new Thickness(1), Child = t, VerticalAlignment = VerticalAlignment.Center };
        }

        private async Task UpdatePackages(List<WingetPackage> list)
        {
            if (busy || updatingPackages || list == null || list.Count == 0) return;
            string names = string.Join("\n", list.Take(12).Select(p => "•  " + p.Name + "   " + p.Version + " → " + p.Available));
            if (list.Count > 12) names += "\n•  …" + (list.Count - 12) + (language == "tr" ? " uygulama daha" : " more");
            string body = (language == "tr" ? "Şu uygulamalar winget ile güncellenecek:\n\n" : "These apps will be updated with winget:\n\n") + names + "\n\n" +
                (language == "tr"
                    ? "• Kurulum sessizdir; paket lisansı ve kaynak sözleşmeleri senin adına kabul edilir.\n• Bazı uygulamalar yönetici onayı (UAC) isteyebilir veya güncelleme sırasında kapanabilir.\n• Geri almak için uygulamanın eski sürümünü yeniden kurman gerekir.\n\nDevam edilsin mi?"
                    : "• Installs are silent; package licences and source agreements are accepted on your behalf.\n• Some apps may ask for administrator approval (UAC) or close while updating.\n• To roll back, reinstall the app's older version.\n\nContinue?");
            if (MessageBox.Show(body, T("updates"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            updatingPackages = true; stopUpdates = false;
            foreach (WingetPackage p in list) { p.State = UpdateState.Queued; p.Message = null; }
            SetBusy(true, language == "tr" ? "Güncelleme hazırlanıyor…" : "Preparing updates…");
            RenderUpdates();
            int done = 0, failed = 0, restart = 0;
            for (int i = 0; i < list.Count; i++)
            {
                WingetPackage p = list[i];
                if (stopUpdates) { for (int j = i; j < list.Count; j++) list[j].State = UpdateState.Available; break; }
                p.State = UpdateState.Updating;
                statusText.Text = (language == "tr" ? "Güncelleniyor: " : "Updating: ") + p.Name + "  (" + (i + 1) + "/" + list.Count + ")";
                RenderUpdates();
                string message = null;
                UpdateState state = await Task.Run(() => { string m; UpdateState s = WingetService.Upgrade(p, out m); message = m; return s; });
                p.State = state; p.Message = message;
                if (state == UpdateState.Done) done++; else if (state == UpdateState.NeedsRestart) { done++; restart++; } else failed++;
                AppendUpdateLog(p);
                RenderUpdates();
            }
            updatingPackages = false;
            SetBusy(false, T("ready"));
            statusText.Text = language == "tr" ? done + " güncellendi" + (failed > 0 ? " · " + failed + " hata" : "") + (restart > 0 ? " · yeniden başlatma gerekli" : "") : done + " updated" + (failed > 0 ? " · " + failed + " failed" : "") + (restart > 0 ? " · restart needed" : "");
            UpdatesBadge(); RenderUpdates();
        }

        private static void AppendUpdateLog(WingetPackage p)
        {
            try
            {
                System.IO.Directory.CreateDirectory(Settings.DataDir);
                System.IO.File.AppendAllText(Settings.LogPath, "UPDATE | " + DateTime.Now.ToString("s") + " | id=" + p.Id + " from=" + p.Version + " to=" + p.Available + " result=" + p.State + Environment.NewLine);
            }
            catch { }
        }
    }
}
