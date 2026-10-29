// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ProjectMantis
{
    // Overview: live system summary strip on top, asymmetric bento grid below
    // (one large recommended-maintenance card + compact state cards).
    public partial class MainWindow
    {
        private sealed class RingStat { public Ring Ring; public TextBlock Center; public TextBlock Sub; }

        private readonly List<double> cpuHistory = new List<double>();
        private const int HistoryLength = 40;
        private SystemMetrics lastMetrics;
        private bool hasMetrics;

        private RingStat dashCpu, dashRam, dashDisk;
        private Sparkline dashSpark;
        private TextBlock dashUptime;
        private TextBlock dashMood;
        private TextBlock dashMemoryState;
        private TextBlock dashBoostState, dashAwakeState;
        private Border dashBoostBadge, dashAwakeBadge;

        private void ShowDashboard()
        {
            Grid page = PageGrid();
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            FrameworkElement header = Header(language == "tr" ? "GENEL BAKIŞ" : "OVERVIEW", Theme.SectionOverview, Greeting(), DashboardMood());
            StackPanel headText = ((Grid)header).Children[0] as StackPanel;
            dashMood = headText == null ? null : headText.Children[headText.Children.Count - 1] as TextBlock;
            page.Children.Add(header);

            FrameworkElement strip = BuildPulseStrip(); strip.Margin = new Thickness(0, 0, 0, Theme.S4);
            Grid.SetRow(strip, 1); page.Children.Add(strip);

            Grid bento = new Grid();
            bento.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });
            bento.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 260 });
            for (int i = 0; i < 3; i++) bento.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 82 });
            double half = Theme.S4 / 2;

            Border hero = CleanupHero(); hero.Margin = new Thickness(0, 0, half, 0); Grid.SetRowSpan(hero, 3); bento.Children.Add(hero);

            Border memory = QuickCard(Icons.Chip, Theme.Info, language == "tr" ? "Belleği Uçur" : "Lift Off Memory", out dashMemoryState, null, async delegate { await RunMemoryOptimization(); });
            memory.Margin = new Thickness(half, 0, 0, half); Grid.SetColumn(memory, 1); bento.Children.Add(memory);
            Border boost = QuickCard(Icons.Bolt, Theme.Warning, language == "tr" ? "Oyun Modu" : "Game Mode", out dashBoostState, dashBoostBadge = Badge("", Theme.Warning), async delegate { await ToggleBoost(); });
            boost.Margin = new Thickness(half, half, 0, half); Grid.SetColumn(boost, 1); Grid.SetRow(boost, 1); bento.Children.Add(boost);
            Border awake = QuickCard(Icons.Coffee, Theme.Violet, language == "tr" ? "Uyanık Tut" : "Keep Awake", out dashAwakeState, dashAwakeBadge = Badge("", Theme.Violet), delegate { ToggleAwake(); return Task.FromResult(0); });
            awake.Margin = new Thickness(half, half, 0, 0); Grid.SetColumn(awake, 1); Grid.SetRow(awake, 2); bento.Children.Add(awake);

            Grid.SetRow(bento, 2); page.Children.Add(bento);

            // Lower row: recent activity + Check-Up. It takes whatever height the bento leaves, so a
            // maximized window gets more content instead of stretched, emptier cards.
            Grid lower = new Grid { Margin = new Thickness(0, Theme.S4, 0, 0) };
            lower.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });
            lower.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 260 });
            Border activity = ActivityCard(); activity.Margin = new Thickness(0, 0, half, 0); lower.Children.Add(activity);
            Border checkup = CheckupCard(); checkup.Margin = new Thickness(half, 0, 0, 0); Grid.SetColumn(checkup, 1); lower.Children.Add(checkup);
            Grid.SetRow(lower, 3); page.Children.Add(lower);
            page.SizeChanged += delegate
            {
                double used = header.ActualHeight + header.Margin.Bottom + strip.ActualHeight + strip.Margin.Bottom;
                double available = Math.Max(240, page.ActualHeight - used - 2);
                double bentoHeight = Math.Max(240, Math.Min(400, available));
                double rest = available - bentoHeight - Theme.S4;
                bool showLower = rest >= 150;
                bento.Height = showLower ? bentoHeight : available;
                lower.Visibility = showLower ? Visibility.Visible : Visibility.Collapsed;
                if (showLower) lower.Height = rest;
            };
            SetPage(page, "dashboard");
            UpdateDashboardStates();
            if (hasMetrics) UpdateDashboardLive(lastMetrics, true);
        }

        // Subtitle reacts to the machine instead of always saying the same thing.
        private string DashboardMood()
        {
            if (hasMetrics && lastMetrics.Ram >= 85) return language == "tr" ? "Bellek neredeyse dolu. \"Belleği Uçur\" biraz nefes aldırabilir." : "Memory is almost full. \"Lift Off Memory\" can free some room.";
            if (hasMetrics && lastMetrics.DiskFree < 12) return language == "tr" ? "Sistem sürücüsünde yer azalıyor. Önce tarayıp ne kazanacağını gör." : "The system drive is running low. Scan first to see what you can reclaim.";
            return language == "tr" ? "Bilgisayarın sakin. İstersen biraz daha hafifletelim." : "Your PC looks calm. Let's keep it light.";
        }

        // ---------- Live summary strip ----------

        private FrameworkElement BuildPulseStrip()
        {
            Border strip = Card(Theme.RadiusLg, false); strip.Padding = new Thickness(Theme.S5 - 4, Theme.S4, Theme.S5 - 4, Theme.S4);
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            FrameworkElement cpu = RingItem("CPU", Theme.Success, out dashCpu); g.Children.Add(cpu);
            FrameworkElement ram = RingItem("RAM", Theme.Info, out dashRam); Grid.SetColumn(ram, 1); g.Children.Add(ram);
            FrameworkElement disk = RingItem(language == "tr" ? "DİSK" : "DISK", Theme.Violet, out dashDisk); Grid.SetColumn(disk, 2); g.Children.Add(disk);

            Grid trend = new Grid { Margin = new Thickness(Theme.S2, 0, Theme.S5, 0), MinWidth = 120 };
            trend.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); trend.RowDefinitions.Add(new RowDefinition());
            trend.Children.Add(Eyebrow(language == "tr" ? "CPU · 60 SN" : "CPU · 60 S", Theme.TextFaint));
            dashSpark = new Sparkline(Theme.Success, HistoryLength) { Height = 40, Margin = new Thickness(0, Theme.S2, 0, 0), VerticalAlignment = VerticalAlignment.Bottom };
            Grid.SetRow(dashSpark, 1); trend.Children.Add(dashSpark);
            Grid.SetColumn(trend, 3); g.Children.Add(trend);

            Border divider = new Border { Width = 1, Background = Theme.Brush(Theme.StrokeSubtle), Margin = new Thickness(0, 2, Theme.S5 - 4, 2), HorizontalAlignment = HorizontalAlignment.Left };
            StackPanel info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, MinWidth = 150 };
            info.Children.Add(Eyebrow(language == "tr" ? "AÇIK KALMA" : "UPTIME", Theme.TextFaint));
            dashUptime = Label(FormatUptime(SystemMonitor.Uptime()), Theme.H3, FontWeights.SemiBold, Theme.TextPrimary); dashUptime.Margin = new Thickness(0, 3, 0, Theme.S2); Tabular(dashUptime); info.Children.Add(dashUptime);
            info.Children.Add(Label(Environment.MachineName + "  ·  " + SystemMonitor.OsLabel(), Theme.Micro, FontWeights.Normal, Theme.TextMuted));
            Grid infoWrap = new Grid(); infoWrap.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); infoWrap.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            infoWrap.Children.Add(divider); Grid.SetColumn(info, 1); infoWrap.Children.Add(info);
            Grid.SetColumn(infoWrap, 4); g.Children.Add(infoWrap);

            // Narrow windows drop the least important columns first instead of squeezing everything.
            g.SizeChanged += delegate
            {
                double w = g.ActualWidth;
                infoWrap.Visibility = w >= 760 ? Visibility.Visible : Visibility.Collapsed;
                trend.Visibility = w >= 560 ? Visibility.Visible : Visibility.Collapsed;
            };
            strip.Child = g;
            return strip;
        }

        private FrameworkElement RingItem(string caption, string color, out RingStat stat)
        {
            stat = new RingStat { Ring = new Ring(54, 5, color) };
            stat.Center = Label("—", Theme.Small, FontWeights.SemiBold, Theme.TextPrimary); Tabular(stat.Center);
            stat.Center.HorizontalAlignment = HorizontalAlignment.Center; stat.Center.VerticalAlignment = VerticalAlignment.Center;
            stat.Ring.Children.Add(stat.Center);
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, Theme.S4, 0) };
            row.Children.Add(stat.Ring);
            StackPanel text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(Theme.S3, 0, 0, 0), MinWidth = 72 };
            text.Children.Add(Eyebrow(caption, color));
            stat.Sub = Label("—", Theme.Micro, FontWeights.Normal, Theme.TextMuted); stat.Sub.Margin = new Thickness(0, 4, 0, 0); Tabular(stat.Sub); text.Children.Add(stat.Sub);
            row.Children.Add(text);
            return row;
        }

        // Called on every metrics tick; cheap when the dashboard is not on screen.
        private void UpdateDashboardLive(SystemMetrics m, bool first)
        {
            if (currentPage != "dashboard" || dashCpu == null) return;
            int ms = first ? 900 : 500;
            SetRing(dashCpu, m.Cpu, Environment.ProcessorCount + (language == "tr" ? " çekirdek" : " cores"), ms, false);
            SetRing(dashRam, m.Ram, FormatGb(m.RamUsedBytes) + " / " + FormatGb(m.RamTotalBytes), ms, false);
            SetRing(dashDisk, 100 - m.DiskFree, FormatGb(m.DiskFreeBytes) + (language == "tr" ? " boş" : " free"), ms, true);
            dashSpark.SetValues(cpuHistory);
            dashUptime.Text = FormatUptime(SystemMonitor.Uptime());
            if (dashMood != null) { string mood = DashboardMood(); if (dashMood.Text != mood) dashMood.Text = mood; }
            if (dashMemoryState != null) dashMemoryState.Text = FormatGb(m.RamUsedBytes) + " / " + FormatGb(m.RamTotalBytes) + (language == "tr" ? " kullanımda" : " in use");
        }

        private void SetRing(RingStat s, double percent, string sub, int ms, bool isDisk)
        {
            s.Ring.AnimateTo(percent, ms);
            s.Ring.SetColor(isDisk ? (percent >= 90 ? Theme.Danger : percent >= 80 ? Theme.Warning : Theme.Violet) : Theme.LoadColor(percent));
            s.Center.Text = percent.ToString("0") + "%";
            s.Sub.Text = sub;
        }

        // Toggle cards show their real state; refreshed in place after each action.
        private void UpdateDashboardStates()
        {
            if (dashBoostState == null) return;
            dashBoostState.Text = boostActive ? (language == "tr" ? "Yüksek performans planı açık" : "High-performance plan is on") : (language == "tr" ? "Kapalı · tek tıkla aç / geri al" : "Off · one click on, one click back");
            SetToggleBadge(dashBoostBadge, boostActive, Theme.Warning);
            dashAwakeState.Text = keepAwake ? (language == "tr" ? "Mantis açıkken uyku engelleniyor" : "Sleep is blocked while Mantis is open") : (language == "tr" ? "Kapalı · render ve indirmeler için" : "Off · for renders and downloads");
            SetToggleBadge(dashAwakeBadge, keepAwake, Theme.Violet);
        }

        private void SetToggleBadge(Border badge, bool on, string color)
        {
            if (badge == null) return;
            string c = on ? color : Theme.TextFaint;
            badge.Background = Theme.Brush(Theme.Alpha(c, 0x1C)); badge.BorderBrush = Theme.Brush(Theme.Alpha(c, 0x33));
            TextBlock t = (TextBlock)badge.Child; t.Text = on ? (language == "tr" ? "AÇIK" : "ON") : (language == "tr" ? "KAPALI" : "OFF"); t.Foreground = Theme.Brush(on ? color : Theme.TextMuted);
        }

        // ---------- Bento cards ----------

        private Border CleanupHero()
        {
            string accent = Theme.SectionCleaner;
            Border card = Card(Theme.RadiusXl - 4, false); card.Padding = new Thickness(Theme.S5 + 4, Theme.S5, Theme.S5 + 4, Theme.S5);
            LinearGradientBrush tint = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            tint.GradientStops.Add(new GradientStop(Theme.ToColor(Theme.Mix(Theme.Surface, accent, 0.07)), 0));
            tint.GradientStops.Add(new GradientStop(Theme.ToColor(Theme.Surface), 0.55));
            SetPanelFill(card, tint); SetPanelShape(card, Theme.Chamfer + 6, true);

            Grid g = new Grid { ClipToBounds = false };
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Oversized faint mark gives the card depth without adding noise.
            Viewbox mark = Glyph(Icons.Sparkle, 190, Theme.Alpha(accent, 0x16)); mark.HorizontalAlignment = HorizontalAlignment.Right; mark.VerticalAlignment = VerticalAlignment.Top; mark.Margin = new Thickness(0, -Theme.S3, -Theme.S4, 0); mark.IsHitTestVisible = false;
            Grid.SetRowSpan(mark, 3); g.Children.Add(mark);

            StackPanel top = new StackPanel { Orientation = Orientation.Horizontal };
            top.Children.Add(IconBadge(Icons.Sparkle, accent, 44, 22));
            TextBlock eye = Eyebrow(language == "tr" ? "ÖNERİLEN BAKIM" : "RECOMMENDED CARE", accent); eye.VerticalAlignment = VerticalAlignment.Center; eye.Margin = new Thickness(Theme.S3 + 2, 0, 0, 0); top.Children.Add(eye);
            g.Children.Add(top);

            StackPanel middle = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, Theme.S4, Theme.S6, Theme.S4) };
            middle.Children.Add(Label(language == "tr" ? "Temizle!" : "Clean it!", Theme.Display + 4, FontWeights.SemiBold, Theme.TextPrimary));
            TextBlock d = Paragraph(language == "tr" ? "Geçici dosyaları, shader cache'i, çökme raporlarını ve Adobe cache'lerini güvenle süpür. Açık uygulamaların dosyalarına dokunulmaz." : "Safely sweep temp files, shader cache, crash reports and Adobe caches. Files used by open apps are never touched.", Theme.Body, Theme.TextMuted);
            d.Margin = new Thickness(0, Theme.S2, 0, 0); d.MaxWidth = 440; d.HorizontalAlignment = HorizontalAlignment.Left; middle.Children.Add(d);
            Grid.SetRow(middle, 1); g.Children.Add(middle);
            // On short windows the description goes first so the stats and buttons always stay visible.
            Button details = null; Grid stats = new Grid { Margin = new Thickness(0, 0, 0, Theme.S5) };
            card.SizeChanged += delegate
            {
                d.Visibility = card.ActualHeight >= 372 ? Visibility.Visible : Visibility.Collapsed;
                Visibility wide = card.ActualWidth >= 470 ? Visibility.Visible : Visibility.Collapsed;
                if (stats.Children.Count > 2) stats.Children[2].Visibility = wide;
                if (details != null) details.Visibility = wide;
            };

            CleanupHistory h = CleanupHistory.Read();
            for (int i = 0; i < 3; i++) stats.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            string never = language == "tr" ? "Henüz yok" : "Not yet";
            AddStat(stats, 0, language == "tr" ? "SON TEMİZLİK" : "LAST CLEANUP", h.LastClean.HasValue ? RelativeTime(h.LastClean.Value) : never, h.LastClean.HasValue ? FormatBytes(h.LastCleanBytes) + (language == "tr" ? " kazanıldı" : " reclaimed") : (language == "tr" ? "İlk temizliğini yap" : "Run your first cleanup"));
            AddStat(stats, 1, language == "tr" ? "SON TARAMA" : "LAST SCAN", h.LastScan.HasValue ? FormatBytes(h.LastScanBytes) : never, h.LastScan.HasValue ? (language == "tr" ? "kazanılabilir · " : "reclaimable · ") + RelativeTime(h.LastScan.Value) : (language == "tr" ? "Silmeden önce gör" : "See before you delete"));
            AddStat(stats, 2, language == "tr" ? "TOPLAM" : "ALL TIME", h.CleanRuns > 0 ? FormatBytes(h.TotalCleanBytes) : "—", h.CleanRuns + (language == "tr" ? " temizlik" : h.CleanRuns == 1 ? " cleanup" : " cleanups"));
            Grid.SetRow(stats, 2); g.Children.Add(stats);

            StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal };
            actions.Children.Add(PillButton(language == "tr" ? "Temizle!" : "Clean it!", ButtonKind.Primary, async delegate { await RunCleanup(false); }, Icons.Sparkle, Theme.AccentSecondary));
            actions.Children.Add(PillButton(language == "tr" ? "Önce Tara" : "Scan First", ButtonKind.Secondary, async delegate { await RunCleanup(true); }, Icons.Search, accent));
            details = PillButton(language == "tr" ? "Ayrıntılar" : "Details", ButtonKind.Ghost, delegate { ShowCleaner(); return Task.FromResult(0); }, null, accent);
            actions.Children.Add(details);
            Grid.SetRow(actions, 3); g.Children.Add(actions);

            card.Child = g;
            return card;
        }

        private void AddStat(Grid stats, int column, string caption, string value, string sub)
        {
            StackPanel s = new StackPanel { Margin = new Thickness(0, 0, Theme.S6, 0) };
            s.Children.Add(Eyebrow(caption, Theme.TextFaint));
            TextBlock v = Label(value, Theme.H3 + 1, FontWeights.SemiBold, Theme.TextPrimary); v.Margin = new Thickness(0, 4, 0, 2); Tabular(v); s.Children.Add(v);
            s.Children.Add(Label(sub, Theme.Micro, FontWeights.Normal, Theme.TextMuted));
            Grid.SetColumn(s, column); stats.Children.Add(s);
        }

        // Compact horizontal card: icon, title, one live state line and an optional state badge.
        private Border QuickCard(Geometry icon, string color, string title, out TextBlock state, Border badge, Func<Task> action)
        {
            Border card = Card(Theme.RadiusLg, true); card.Padding = new Thickness(Theme.S4 + 2, Theme.S3, Theme.S4, Theme.S3);
            Grid g = new Grid { VerticalAlignment = VerticalAlignment.Center };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Border b = IconBadge(icon, color, 42, 20); b.Margin = new Thickness(0, 0, Theme.S4 - 2, 0); g.Children.Add(b);
            StackPanel text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(Label(title, Theme.H3, FontWeights.SemiBold, Theme.TextPrimary));
            state = Label("", Theme.Small, FontWeights.Normal, Theme.TextMuted); state.Margin = new Thickness(0, 3, 0, 0); text.Children.Add(state);
            Grid.SetColumn(text, 1); g.Children.Add(text);
            FrameworkElement right = badge;
            if (right == null) { Viewbox arrow = Glyph(Icons.ArrowUpRight, 16, Theme.TextFaint); right = arrow; }
            right.Margin = new Thickness(Theme.S3, 0, 0, 0); right.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(right, 2); g.Children.Add(right);
            card.Child = g;
            card.MouseLeftButtonUp += async delegate { if (!busy) await action(); };
            return card;
        }

        // Recent operations from mantis.log, newest first, as a compact timeline.
        private Border ActivityCard()
        {
            Border card = Card(Theme.RadiusLg, false); card.Padding = new Thickness(Theme.S5 - 4, Theme.S4, Theme.S5 - 4, Theme.S3);
            Grid g = new Grid(); g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); g.RowDefinitions.Add(new RowDefinition());
            g.Children.Add(Eyebrow(language == "tr" ? "SON ETKİNLİK" : "RECENT ACTIVITY", Theme.TextMuted));
            StackPanel list = new StackPanel { Margin = new Thickness(0, Theme.S3, 0, 0) };
            List<string[]> events = new List<string[]>();
            try { if (System.IO.File.Exists(Settings.LogPath)) foreach (string line in System.IO.File.ReadAllLines(Settings.LogPath)) { string[] p = line.Split('|'); if (p.Length >= 3) events.Add(p); } } catch { }
            events.Reverse();
            foreach (string[] e in events.Take(12))
            {
                DateTime when; if (!DateTime.TryParse(e[1].Trim(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeLocal, out when)) continue;
                list.Children.Add(ActivityRow(e[0].Trim(), e[2].Trim(), when));
            }
            if (list.Children.Count == 0) list.Children.Add(Paragraph(language == "tr" ? "Henüz bir işlem yapılmadı. Temizlik, güncelleme ve düzeltmeler burada listelenir." : "Nothing has run yet. Cleanups, updates and fixes will be listed here.", Theme.Small, Theme.TextFaint));
            ScrollViewer sv = Scroll(list); sv.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden; Grid.SetRow(sv, 1); g.Children.Add(sv);
            card.Child = g;
            return card;
        }

        private FrameworkElement ActivityRow(string kind, string details, DateTime when)
        {
            Func<string, string> field = delegate(string key) { foreach (string f in details.Split(' ')) if (f.StartsWith(key + "=")) return f.Substring(key.Length + 1); return ""; };
            string color, text;
            long bytes; long.TryParse(field("bytes"), out bytes);
            if (kind == "CLEAN") { color = Theme.SectionCleaner; text = (language == "tr" ? "Temizlik · " : "Cleanup · ") + FormatBytes(bytes) + (language == "tr" ? " kazanıldı" : " reclaimed"); }
            else if (kind == "SCAN") { color = Theme.Violet; text = (language == "tr" ? "Tarama · " : "Scan · ") + FormatBytes(bytes) + (language == "tr" ? " kazanılabilir" : " reclaimable"); }
            else if (kind == "UPDATE") { color = Theme.SectionUpdates; text = (language == "tr" ? "Güncelleme · " : "Update · ") + field("id") + " → " + field("to") + (field("result") == "Failed" ? (language == "tr" ? " (hata)" : " (failed)") : ""); }
            else if (kind == "FIX") { color = Theme.SectionSecurity; text = (language == "tr" ? "Check-Up düzeltmesi · " : "Check-Up fix · ") + field("key") + (field("ok") == "True" ? "" : (language == "tr" ? " (uygulanamadı)" : " (not applied)")); }
            else if (kind == "STARTUP") { int n = details.IndexOf("name="); color = Theme.SectionTools; text = (language == "tr" ? "Başlangıç · " : "Startup · ") + (n >= 0 ? details.Substring(n + 5) : "") + (field("ok") != "True" ? (language == "tr" ? " (değiştirilemedi)" : " (not changed)") : field("enabled") == "True" ? (language == "tr" ? " açıldı" : " enabled") : (language == "tr" ? " kapatıldı" : " disabled")); }
            else if (kind == "SERVICE") { color = Theme.SectionTools; text = (language == "tr" ? "Servis · " : "Service · ") + field("id") + (field("ok") != "True" ? (language == "tr" ? " (kısmen uygulandı)" : " (partly applied)") : field("enabled") == "True" ? (language == "tr" ? " açıldı" : " on") : (language == "tr" ? " kapatıldı" : " off")); }
            else if (kind == "TWEAK") { color = Theme.SectionTools; text = (language == "tr" ? "Tweak · " : "Tweak · ") + field("id") + (field("ok") != "True" ? (language == "tr" ? " (uygulanamadı)" : " (not applied)") : field("applied") == "True" ? (language == "tr" ? " uygulandı" : " applied") : (language == "tr" ? " varsayılana döndü" : " reset")); }
            else if (kind == "HOSTS") { color = Theme.SectionTools; text = language == "tr" ? "Hosts dosyası kaydedildi (yedekli)" : "Hosts file saved (backed up)"; }
            else if (kind == "POWER") { color = Theme.SectionTools; text = (language == "tr" ? "Güç · " : "Power · ") + details.Split(' ')[0]; }
            else if (kind == "INSTALL") { color = Theme.AccentPrimary; text = (language == "tr" ? "Kuruldu · v" : "Installed · v") + field("version"); }
            else if (kind == "UNINSTALL") { color = Theme.TextMuted; text = (language == "tr" ? "Kaldırıldı · v" : "Uninstalled · v") + field("version"); }
            else { color = Theme.TextMuted; text = kind; }
            Grid row = new Grid { Margin = new Thickness(0, 0, 0, Theme.S2 + 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new Border { Width = 6, Height = 6, Background = Theme.Brush(color), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 1, Theme.S3, 0) });
            TextBlock t = Label(text, Theme.Small, FontWeights.Normal, Theme.TextPrimary); Grid.SetColumn(t, 1); row.Children.Add(t);
            TextBlock w = new TextBlock { Text = RelativeTime(when), FontFamily = Theme.MonoFont, FontSize = 10.5, Foreground = Theme.Brush(Theme.TextFaint), Margin = new Thickness(Theme.S3, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(w, 2); row.Children.Add(w);
            return row;
        }

        // Check-Up teaser: last score when available, otherwise an invitation to run it.
        private Border CheckupCard()
        {
            string accent = Theme.SectionSecurity;
            Border card = Card(Theme.RadiusLg, true); card.Padding = new Thickness(Theme.S5 - 4, Theme.S4, Theme.S5 - 4, Theme.S4);
            bool hasRun = securityResults.Count > 0;
            int score = hasRun ? SecurityScore.Compute(LatestFindings()) : 0;
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(Eyebrow("CHECK-UP", accent));
            TextBlock title = Label(hasRun ? ScoreVerdict(score) : (language == "tr" ? "Güvenlik muayenesi" : "Security check-up"), Theme.H2, FontWeights.SemiBold, Theme.TextPrimary); title.Margin = new Thickness(0, Theme.S2, 0, Theme.S1); text.Children.Add(title);
            TextBlock d = Paragraph(hasRun ? (language == "tr" ? "Son check-up " : "Last check-up ") + (lastCheckup.HasValue ? RelativeTime(lastCheckup.Value) : "") : (language == "tr" ? "Güvenlik Duvarı, Defender, kalıcılık ve zararlı yazılım kontrolleri; sonunda 0–100 skor." : "Firewall, Defender, persistence and malware checks, ending in a 0–100 score."), Theme.Small, Theme.TextMuted);
            text.Children.Add(d);
            StackPanel go = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, Theme.S3, 0, 0) };
            go.Children.Add(Label(hasRun ? (language == "tr" ? "Ayrıntılar" : "Details") : (language == "tr" ? "Check-Up başlat" : "Start Check-Up"), Theme.Small, FontWeights.SemiBold, accent));
            Viewbox arrow = Glyph(Icons.ArrowRight, 14, accent); arrow.Margin = new Thickness(Theme.S2 - 2, 1, 0, 0); go.Children.Add(arrow);
            text.Children.Add(go);
            g.Children.Add(text);
            Ring ring = new Ring(74, 6, hasRun ? ScoreColor(score) : Theme.TextFaint);
            TextBlock n = Label(hasRun ? score.ToString() : "—", Theme.H2, FontWeights.SemiBold, Theme.TextPrimary); Tabular(n); n.HorizontalAlignment = HorizontalAlignment.Center; n.VerticalAlignment = VerticalAlignment.Center; ring.Children.Add(n);
            if (hasRun) ring.AnimateTo(score, 900);
            ring.Margin = new Thickness(Theme.S4, 0, 0, 0); Grid.SetColumn(ring, 1); g.Children.Add(ring);
            card.Child = g;
            card.MouseLeftButtonUp += delegate { if (!busy) ShowSecurity(); };
            return card;
        }

        // ---------- Formatting ----------

        private string FormatGb(long bytes)
        {
            double gb = bytes / 1073741824.0;
            return gb.ToString(gb >= 100 ? "0" : "0.0", UiCulture) + " GB";
        }

        private string FormatUptime(TimeSpan t)
        {
            if (t.TotalDays >= 1) return language == "tr" ? (int)t.TotalDays + " gün " + t.Hours + " sa" : (int)t.TotalDays + "d " + t.Hours + "h";
            return language == "tr" ? t.Hours + " sa " + t.Minutes + " dk" : t.Hours + "h " + t.Minutes + "m";
        }

        private string RelativeTime(DateTime when)
        {
            TimeSpan d = DateTime.Now - when;
            if (d.TotalMinutes < 1) return language == "tr" ? "az önce" : "just now";
            if (d.TotalHours < 1) return language == "tr" ? (int)d.TotalMinutes + " dk önce" : (int)d.TotalMinutes + " min ago";
            if (d.TotalDays < 1) return language == "tr" ? (int)d.TotalHours + " sa önce" : (int)d.TotalHours + " h ago";
            if (d.TotalDays < 30) return language == "tr" ? (int)d.TotalDays + " gün önce" : (int)d.TotalDays + " days ago";
            return when.ToString("d MMM yyyy", language == "tr" ? new System.Globalization.CultureInfo("tr-TR") : System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
