// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Shape = System.Windows.Shapes.Path;

namespace ProjectMantis
{
    public partial class MainWindow : Window
    {
        private sealed class NavItem
        {
            public string Key; public string Accent; public Action Show;
            public Border Fill; public Border Indicator; public Shape IconPath; public TextBlock Text; public Border Badge; public TextBlock Index;
        }

        private sealed class Gauge { public TextBlock Value; public ScaleTransform Scale; public Border Fill; public bool Inverse; }

        private readonly Settings settings = Settings.Load();
        private readonly Dictionary<string, string> tr = new Dictionary<string, string>();
        private readonly Dictionary<string, string> en = new Dictionary<string, string>();
        private readonly DispatcherTimer monitorTimer = new DispatcherTimer();
        private readonly SystemMonitor monitor = new SystemMonitor();
        private readonly List<NavItem> navItems = new List<NavItem>();
        private string currentPage = "dashboard";
        private Grid content;
        private Border glowPrimary;
        private Border glowSecondary;
        private Gauge cpuGauge;
        private Gauge ramGauge;
        private Gauge diskGauge;
        private TextBlock statusText;
        private Border statusDot;
        private TextBlock crumbSection;
        private Border busyBar;
        private Shape maximizeIcon;
        private bool busy;
        private string language;
        private string lastPowerScheme;
        private bool boostActive;
        private bool keepAwake;
        private bool allowExit;
        private Forms.NotifyIcon trayIcon;
        private HwndSource hwndSource;

        public MainWindow()
        {
            language = App.LanguageOverride ?? (settings.Language == "en" ? "en" : "tr");
            SeedTranslations();
            InstallGlobalStyles();
            Title = "Project Mantis";
            Width = 1160;
            Height = 740;
            MinWidth = 960;
            MinHeight = 640;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;
            Background = Theme.Brush(Theme.BgBase);
            AllowsTransparency = false;
            UseLayoutRounding = true;
            // Native resize borders, Aero Snap, double-click maximize and drag-to-restore come from the
            // caption region; only the window buttons opt out of it.
            WindowChrome.SetWindowChrome(this, new WindowChrome
            {
                CaptionHeight = Theme.ShellMargin + Theme.TitleBarHeight - 6, ResizeBorderThickness = new Thickness(6),
                GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(0), UseAeroCaptionButtons = false
            });
            Icon = LoadLogo();
            Content = BuildShell();
            SourceInitialized += ApplyBackdrop;
            Closed += OnClosed;
            Loaded += delegate { App.Log("MainWindow loaded"); };
            Closing += OnClosing;
            StateChanged += OnWindowStateChanged;
            InitializeTray();
            monitorTimer.Interval = TimeSpan.FromSeconds(1.5);
            monitorTimer.Tick += delegate { RefreshMetrics(); };
            monitorTimer.Start();
            ShowDashboard();
        }

        private void SeedTranslations()
        {
            tr["dashboard"] = "Genel Bakış"; en["dashboard"] = "Overview";
            tr["clean"] = "Temizleyici"; en["clean"] = "Cleaner";
            tr["tools"] = "Araçlar"; en["tools"] = "Tools";
            tr["settings"] = "Ayarlar"; en["settings"] = "Settings";
            tr["ready"] = "Sistem hazır"; en["ready"] = "System ready";
            tr["working"] = "Çalışıyor…"; en["working"] = "Working…";
            tr["admin"] = "Yönetici"; en["admin"] = "Administrator";
            tr["standard"] = "Standart mod"; en["standard"] = "Standard mode";
            tr["restartAdmin"] = "Yönetici olarak yeniden başlat"; en["restartAdmin"] = "Restart as administrator";
            tr["run"] = "Çalıştır"; en["run"] = "Run";
            tr["updates"] = "Güncellemeler"; en["updates"] = "Updates";
            tr["security"] = "Güvenlik"; en["security"] = "Security";
            tr["minimize"] = "Küçült"; en["minimize"] = "Minimize";
            tr["maximize"] = "Büyüt / Geri al"; en["maximize"] = "Maximize / Restore";
            tr["close"] = "Kapat"; en["close"] = "Close";
        }

        private string T(string key) { return (language == "en" ? en : tr).ContainsKey(key) ? (language == "en" ? en[key] : tr[key]) : key; }

        // ---------- Shell ----------

        private UIElement BuildShell()
        {
            Grid root = new Grid();
            root.Background = new LinearGradientBrush(Theme.ToColor(Theme.BgBase), Theme.ToColor(Theme.BgElevated), 28);

            // Instrument grid fading out toward the bottom, plus one dim section-tinted light behind the content.
            Border grid = new Border { Background = Theme.Grid(), IsHitTestVisible = false };
            LinearGradientBrush fade = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            fade.GradientStops.Add(new GradientStop(Colors.Black, 0)); fade.GradientStops.Add(new GradientStop(Color.FromArgb(0x40, 0, 0, 0), 0.7)); fade.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
            grid.OpacityMask = fade;
            root.Children.Add(grid);
            glowPrimary = new Border { Width = 1100, Height = 900, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -460, -360, 0), Background = Theme.Glow(Theme.AccentPrimary, 0.09), IsHitTestVisible = false };
            glowSecondary = new Border { Width = 700, Height = 700, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(-380, 0, 0, -420), Background = Theme.Glow(Theme.SectionCleaner, 0.04), IsHitTestVisible = false };
            root.Children.Add(glowPrimary);
            root.Children.Add(glowSecondary);

            Grid shell = new Grid { Margin = new Thickness(Theme.ShellMargin) };
            shell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Theme.SidebarWidth) });
            shell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.Children.Add(shell);
            shell.Children.Add(BuildSidebar());

            Grid right = new Grid { Margin = new Thickness(Theme.ShellGap, 0, 0, 0) };
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(Theme.TitleBarHeight) });
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(right, 1);
            shell.Children.Add(right);
            right.Children.Add(BuildTitleBar());
            content = new Grid { Margin = new Thickness(0, Theme.ShellGap + Theme.S2, 0, 0) };
            Grid.SetRow(content, 1);
            right.Children.Add(content);
            return root;
        }

        private UIElement BuildSidebar()
        {
            Border side = Glass(Theme.RadiusLg); SetPanelFill(side, Theme.LiveBrush(Theme.BgSidebar)); SetPanelShape(side, Theme.Chamfer + 6, true); side.Effect = Theme.Elevation(1);
            Grid grid = new Grid { Margin = new Thickness(Theme.S4 + 2) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            side.Child = grid;

            StackPanel brand = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2, 2, 0, Theme.S5) };
            brand.Children.Add(new Image { Width = 46, Height = 46, Stretch = Stretch.Uniform, Source = LoadLogo() });
            StackPanel names = new StackPanel { Margin = new Thickness(Theme.S3, 4, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            names.Children.Add(Label("MANTIS", Theme.H2 + 1, FontWeights.SemiBold, Theme.TextPrimary));
            TextBlock sub = Eyebrow("SYSTEM CARE", Theme.AccentPrimary); sub.FontSize = 9; sub.Margin = new Thickness(1, 1, 0, 0); names.Children.Add(sub);
            brand.Children.Add(names);
            grid.Children.Add(brand);

            UniformGrid metrics = new UniformGrid { Columns = 3, Margin = new Thickness(0, 0, 0, Theme.S5) };
            cpuGauge = MetricMini("CPU", metrics, false, language == "tr" ? "İşlemci kullanımı" : "Processor load");
            ramGauge = MetricMini("RAM", metrics, false, language == "tr" ? "Bellek kullanımı" : "Memory in use");
            diskGauge = MetricMini(language == "tr" ? "DİSK" : "DISK", metrics, true, language == "tr" ? "Sistem sürücüsünde boş alan" : "Free space on the system drive");
            Grid.SetRow(metrics, 1); grid.Children.Add(metrics);

            StackPanel nav = new StackPanel();
            nav.Children.Add(SectionLabel(language == "tr" ? "MENÜ" : "MENU", true));
            nav.Children.Add(Nav("dashboard", Icons.Home, T("dashboard"), Theme.SectionOverview, ShowDashboard));
            nav.Children.Add(Nav("clean", Icons.Sparkle, T("clean"), Theme.SectionCleaner, ShowCleaner));
            nav.Children.Add(Nav("tools", Icons.Apps, T("tools"), Theme.SectionTools, ShowTools));
            nav.Children.Add(Nav("updates", Icons.Download, T("updates"), Theme.SectionUpdates, ShowUpdates));
            nav.Children.Add(Nav("security", Icons.Shield, T("security"), Theme.SectionSecurity, ShowSecurity));
            nav.Children.Add(Nav("settings", Icons.Sliders, T("settings"), Theme.SectionSettings, ShowSettings));
            ScrollViewer navScroll = Scroll(nav); navScroll.Padding = new Thickness(0);
            Grid.SetRow(navScroll, 2); grid.Children.Add(navScroll);

            Border foot = new Border { Background = Theme.Brush(Theme.SurfaceRaised), BorderBrush = Theme.Brush(Theme.StrokeSubtle), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(Theme.RadiusMd), Padding = new Thickness(Theme.S3 + 2), Margin = new Thickness(0, Theme.S3, 0, 0) };
            StackPanel fs = new StackPanel();
            bool admin = IsAdmin();
            StackPanel privilege = new StackPanel { Orientation = Orientation.Horizontal };
            privilege.Children.Add(new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(4), Background = Theme.Brush(admin ? Theme.Success : Theme.Warning), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 1, Theme.S2, 0) });
            privilege.Children.Add(Label(admin ? T("admin") : T("standard"), Theme.Small, FontWeights.SemiBold, admin ? Theme.Success : Theme.Warning));
            fs.Children.Add(privilege);
            if (!admin) { Button elevate = LinkButton((language == "tr" ? "Yönetici olarak aç" : "Open as administrator") + "  →", Theme.TextMuted, RestartAsAdmin); elevate.Margin = new Thickness(15, 3, 0, 0); fs.Children.Add(elevate); }
            TextBlock version = Label("v" + AppInfo.Version + "  ·  LOCAL & PRIVATE", 9.5, FontWeights.Medium, Theme.TextFaint); version.Margin = new Thickness(0, Theme.S2 + 2, 0, 0); fs.Children.Add(version);
            TextBlock powered = Label("Powered by kemalselimkoc", 9.5, FontWeights.SemiBold, Theme.AccentPrimary); powered.Margin = new Thickness(0, 3, 0, 0); fs.Children.Add(powered);
            foot.Child = fs; Grid.SetRow(foot, 3); grid.Children.Add(foot);
            return side;
        }

        private UIElement BuildTitleBar()
        {
            Border bar = Glass(Theme.RadiusLg - 2); bar.Effect = Theme.Elevation(1);
            Grid g = new Grid { Margin = new Thickness(Theme.S4 + 2, 0, Theme.S2 + 2, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bar.Child = g;
            StackPanel state = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            statusDot = new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(1), Background = Theme.Brush(Theme.Success), Margin = new Thickness(0, 0, Theme.S2 + 2, 0) };
            statusText = Label(T("ready"), Theme.Small, FontWeights.Medium, Theme.TextMuted);
            state.Children.Add(statusDot); state.Children.Add(statusText);
            g.Children.Add(state);

            // Mono breadcrumb "MANTIS / <SECTION>" plus privilege, the instrument's readout line.
            StackPanel crumb = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, Theme.S5, 0) };
            crumb.Children.Add(new TextBlock { Text = "MANTIS  /  ", FontFamily = Theme.MonoFont, FontSize = 10.5, Foreground = Theme.Brush(Theme.TextFaint) });
            crumbSection = new TextBlock { FontFamily = Theme.MonoFont, FontSize = 10.5, FontWeight = FontWeights.SemiBold, Foreground = Theme.Brush(Theme.AccentPrimary) };
            crumb.Children.Add(crumbSection);
            crumb.Children.Add(new TextBlock { Text = "   ·   " + (IsAdmin() ? "ADMIN" : "STD"), FontFamily = Theme.MonoFont, FontSize = 10.5, Foreground = Theme.Brush(IsAdmin() ? Theme.Success : Theme.Warning) });
            Grid.SetColumn(crumb, 1); g.Children.Add(crumb);

            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            Shape unused;
            Button min = IconButton(Icons.Minimize, "#1CFFFFFF", Theme.TextMuted, T("minimize"), delegate { WindowState = WindowState.Minimized; }, out unused);
            Button max = IconButton(WindowState == WindowState.Maximized ? Icons.Restore : Icons.Maximize, "#1CFFFFFF", Theme.TextMuted, T("maximize"), ToggleMaximize, out maximizeIcon);
            Button close = IconButton(Icons.Close, Theme.Alpha(Theme.Danger, 0x38), Theme.Danger, T("close"), RequestClose, out unused);
            foreach (Button b in new[] { min, max, close }) { WindowChrome.SetIsHitTestVisibleInChrome(b, true); buttons.Children.Add(b); }
            Grid.SetColumn(buttons, 2); g.Children.Add(buttons);

            // Thin indeterminate progress line shown along the bottom edge while a task is running.
            LinearGradientBrush sweep = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5), RelativeTransform = new TranslateTransform(-1, 0) };
            sweep.GradientStops.Add(new GradientStop(Theme.ToColor(Theme.Alpha(Theme.Warning, 0)), 0.2));
            sweep.GradientStops.Add(new GradientStop(Theme.ToColor(Theme.Warning), 0.5));
            sweep.GradientStops.Add(new GradientStop(Theme.ToColor(Theme.Alpha(Theme.Warning, 0)), 0.8));
            busyBar = new Border { Height = 2, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(Theme.S4, 0, Theme.S4, 1), CornerRadius = new CornerRadius(1), Background = sweep, Opacity = 0, IsHitTestVisible = false };
            Grid.SetColumnSpan(busyBar, 3); g.Children.Add(busyBar);
            return bar;
        }

        private Gauge MetricMini(string name, UniformGrid parent, bool inverse, string tooltip)
        {
            Border b = new Border { Padding = new Thickness(9, 8, 9, 10), Margin = new Thickness(parent.Children.Count == 0 ? 0 : 3, 0, parent.Children.Count == 2 ? 0 : 3, 0), CornerRadius = new CornerRadius(Theme.RadiusSm), Background = Theme.Brush(Theme.SurfaceRaised), BorderBrush = Theme.Brush(Theme.StrokeSubtle), BorderThickness = new Thickness(1), ToolTip = tooltip };
            StackPanel s = new StackPanel();
            TextBlock caption = new TextBlock { Text = name, FontFamily = Theme.MonoFont, FontSize = 9.5, Foreground = Theme.Brush(Theme.TextFaint) }; s.Children.Add(caption);
            TextBlock value = Label("—", Theme.H3 + 1, FontWeights.SemiBold, Theme.TextPrimary); value.FontFamily = Theme.DisplayFont; value.Margin = new Thickness(0, 2, 0, 0); Tabular(value); s.Children.Add(value);
            Grid track = new Grid { Height = 3, Margin = new Thickness(0, 7, 0, 0) };
            track.Children.Add(new Border { Background = Theme.Brush(Theme.Alpha(Theme.TextPrimary, 0x14)) });
            ScaleTransform scale = new ScaleTransform(0, 1);
            Border fill = new Border { Background = Theme.Brush(Theme.Success), RenderTransform = scale, RenderTransformOrigin = new Point(0, 0.5) };
            track.Children.Add(fill); s.Children.Add(track);
            b.Child = s; parent.Children.Add(b);
            return new Gauge { Value = value, Scale = scale, Fill = fill, Inverse = inverse };
        }

        private void UpdateGauge(Gauge g, double percent)
        {
            if (g == null) return;
            percent = Math.Max(0, Math.Min(100, percent));
            g.Value.Text = percent.ToString("0") + "%";
            g.Fill.Background = Theme.Brush(g.Inverse ? Theme.LoadColor(100 - percent) : Theme.LoadColor(percent));
            g.Scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(percent / 100.0, TimeSpan.FromMilliseconds(500)) { EasingFunction = Ease() });
        }

        private Button Nav(string key, Geometry icon, string title, string accent, Action action)
        {
            NavItem item = new NavItem { Key = key, Accent = accent, Show = action };
            Grid row = new Grid { Height = 44 };
            item.Fill = new Border { CornerRadius = new CornerRadius(Theme.RadiusMd - 2), Background = Brushes.Transparent };
            row.Children.Add(item.Fill);
            item.Indicator = new Border { Width = 3, Height = 18, CornerRadius = new CornerRadius(1.5), HorizontalAlignment = HorizontalAlignment.Left, Background = Theme.Brush(accent), Opacity = 0, Margin = new Thickness(0, 0, 0, 0) };
            row.Children.Add(item.Indicator);
            StackPanel inner = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(Theme.S4 - 2, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            Shape path; Viewbox v = Glyph(icon, 18, Theme.TextMuted, out path); item.IconPath = path; inner.Children.Add(v);
            item.Text = Label(title, Theme.Body, FontWeights.Normal, Theme.TextMuted); item.Text.Margin = new Thickness(Theme.S3, 0, 0, 0); item.Text.VerticalAlignment = VerticalAlignment.Center; inner.Children.Add(item.Text);
            row.Children.Add(inner);
            item.Index = new TextBlock { Text = (navItems.Count + 1).ToString("00"), FontFamily = Theme.MonoFont, FontSize = 10, Foreground = Theme.Brush(Theme.TextFaint), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, Theme.S3, 0) };
            row.Children.Add(item.Index);
            // Small count pill on the right (e.g. available updates); hidden until a page sets it.
            item.Badge = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(7, 1, 7, 2), Margin = new Thickness(0, 0, Theme.S3, 0), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Background = Theme.Brush(Theme.Alpha(accent, 0x26)), Visibility = Visibility.Collapsed, Child = Label("", Theme.Micro - 0.5, FontWeights.SemiBold, accent) };
            Tabular((TextBlock)item.Badge.Child);
            row.Children.Add(item.Badge);
            Button b = new Button
            {
                Content = row, Margin = new Thickness(0, 0, 0, Theme.S1), Cursor = System.Windows.Input.Cursors.Hand, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch, FocusVisualStyle = FocusVisual(), Template = ButtonTemplate(Theme.RadiusMd - 2, "#00000000", Theme.Alpha(Theme.TextPrimary, 0x0C), "#00000000")
            };
            b.Click += delegate { action(); };
            navItems.Add(item);
            return b;
        }

        private void SetNavBadge(string key, string text)
        {
            foreach (NavItem n in navItems)
                if (n.Key == key && n.Badge != null)
                {
                    ((TextBlock)n.Badge.Child).Text = text ?? "";
                    n.Badge.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
                    n.Index.Visibility = string.IsNullOrEmpty(text) ? Visibility.Visible : Visibility.Collapsed;
                }
        }

        private void SelectNav(string key)
        {
            foreach (NavItem n in navItems)
            {
                bool active = n.Key == key;
                n.Fill.Background = active ? Theme.Brush(Theme.Alpha(n.Accent, 0x17)) : Brushes.Transparent;
                n.Indicator.Opacity = active ? 1 : 0;
                n.IconPath.Stroke = Theme.Brush(active ? n.Accent : Theme.TextMuted);
                n.Text.Foreground = Theme.Brush(active ? Theme.TextPrimary : Theme.TextMuted);
                n.Text.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
                n.Index.Foreground = Theme.Brush(active ? n.Accent : Theme.TextFaint);
            }
        }

        private string AccentFor(string key)
        {
            foreach (NavItem n in navItems) if (n.Key == key) return n.Accent;
            return Theme.AccentPrimary;
        }

        // Page transition: fade plus an 8px upward ease-out slide; the background light shifts to the section hue.
        private void SetPage(UIElement page, string key)
        {
            currentPage = key;
            SelectNav(key);
            if (crumbSection != null)
            {
                NavItem item = navItems.FirstOrDefault(n => n.Key == key);
                crumbSection.Text = (item != null ? item.Text.Text : key).ToUpper(UiCulture);
                crumbSection.Foreground = Theme.Brush(AccentFor(key));
            }
            content.Children.Clear(); content.Children.Add(page);
            TranslateTransform slide = new TranslateTransform(0, 8);
            page.RenderTransform = slide;
            page.Opacity = 0;
            page.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, Theme.Normal) { EasingFunction = Ease() });
            slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(320)) { EasingFunction = Ease() });
            SetGlow(AccentFor(key));
        }

        private void SetGlow(string accent)
        {
            RadialGradientBrush brush = glowPrimary == null ? null : glowPrimary.Background as RadialGradientBrush;
            if (brush == null) return;
            Color c = Theme.ToColor(accent);
            double[] strengths = { 0.09, 0.09 * 0.35, 0 };
            for (int i = 0; i < brush.GradientStops.Count && i < strengths.Length; i++)
                brush.GradientStops[i].BeginAnimation(GradientStop.ColorProperty, new ColorAnimation(Color.FromArgb((byte)(255 * strengths[i]), c.R, c.G, c.B), Theme.Slow));
        }

        private void Navigate(string key)
        {
            foreach (NavItem n in navItems) if (n.Key == key) { n.Show(); return; }
            ShowDashboard();
        }

        internal void OpenPage(string key)
        {
            if (key == "gallery") ShowGallery(); else if (key == "startup") ShowStartup(); else Navigate(key);
        }

        private string Greeting()
        {
            int h = DateTime.Now.Hour;
            if (h >= 5 && h < 12) return language == "tr" ? "Günaydın." : "Good morning.";
            if (h >= 12 && h < 18) return language == "tr" ? "İyi günler." : "Good afternoon.";
            return language == "tr" ? "İyi akşamlar." : "Good evening.";
        }

        // ---------- Pages ----------

        private void ShowCleaner()
        {
            Grid page = PageGrid();
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Button scanButton = PillButton(language == "tr" ? "Önce Tara" : "Scan First", ButtonKind.Secondary, async delegate { await RunCleanup(true); }, Icons.Search, Theme.SectionCleaner);
            Button cleanButton = PillButton(language == "tr" ? "Temizle!" : "Clean it!", ButtonKind.Primary, async delegate { await RunCleanup(false); }, Icons.Sparkle, Theme.AccentSecondary);
            page.Children.Add(Header(language == "tr" ? "BAKIM" : "MAINTENANCE", Theme.SectionCleaner, language == "tr" ? "Akıllı Temizlik" : "Smart Cleanup", language == "tr" ? "Bilinen ve güvenli cache konumları. Açık uygulamaların dosyaları atlanır." : "Known safe cache locations. Files used by open apps are skipped.", scanButton, cleanButton));

            StackPanel list = new StackPanel();
            list.Children.Add(SectionLabel(language == "tr" ? "TEMİZLENEN KONUMLAR" : "LOCATIONS", true));
            string safe = language == "tr" ? "Güvenli" : "Safe";
            list.Children.Add(ListRow(Icons.Folder, "TEMP", language == "tr" ? "Kullanıcı ve Windows geçici dosyaları" : "User and Windows temporary files", language == "tr" ? "%TEMP% · Windows\\Temp · Prefetch (son ikisi yönetici erişimi ister)" : "%TEMP% · Windows\\Temp · Prefetch (the last two need administrator access)", Theme.Success, Badge(safe, Theme.Success)));
            list.Children.Add(ListRow(Icons.Film, "ADOBE", language == "tr" ? "After Effects / Premiere medya cache ve peak dosyaları" : "After Effects / Premiere media cache and peak files", language == "tr" ? "Media Cache · Peak Files — bir Adobe uygulaması açıksa tamamen atlanır" : "Media Cache · Peak Files — skipped entirely while an Adobe app is open", Theme.Violet, Badge(language == "tr" ? "Akıllı atlama" : "Smart skip", Theme.Violet)));
            list.Children.Add(ListRow(Icons.Chip, "GPU", "DirectX shader cache", language == "tr" ? "%LOCALAPPDATA%\\D3DSCache — Windows gerektiğinde yeniden oluşturur" : "%LOCALAPPDATA%\\D3DSCache — Windows rebuilds it when needed", Theme.Info, Badge(safe, Theme.Info)));
            list.Children.Add(ListRow(Icons.Alert, "CRASH", language == "tr" ? "Eski çökme dökümleri ve hata raporları" : "Old crash dumps and error reports", "CrashDumps · WER ReportArchive · WER ReportQueue", Theme.Warning, Badge(safe, Theme.Warning)));
            list.Children.Add(SectionLabel(language == "tr" ? "GÜVENCE" : "PEACE OF MIND", false));
            list.Children.Add(ListRow(Icons.Lock, null, language == "tr" ? "Yalnızca bu listedeki konumlara dokunulur" : "Only the locations above are ever touched", language == "tr" ? "Kilitli dosyalar atlanır ve her işlem yerel kayda geçer (Ayarlar → Temizlik raporu)." : "Locked files are skipped and every run is written to a local log (Settings → Cleanup report).", Theme.SectionSettings, null));
            ScrollViewer scroller = Scroll(list); Grid.SetRow(scroller, 1); page.Children.Add(scroller); SetPage(page, "clean");
        }

        private void ShowTools()
        {
            Grid page = PageGrid(); page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); page.RowDefinitions.Add(new RowDefinition());
            page.Children.Add(Header(language == "tr" ? "ARAÇLAR" : "TOOLS", Theme.SectionTools, language == "tr" ? "İnce Araçlar" : "Thoughtful Tools", language == "tr" ? "Günlük kullanımda gerçekten işe yarayan, geri alınabilir dokunuşlar." : "Small, reversible touches that genuinely help every day."));
            UniformGrid tools = AdaptiveGrid(250, 4);
            string c = Theme.SectionTools;
            tools.Children.Add(ToolTile(Icons.Power, language == "tr" ? "Başlangıç Yöneticisi" : "Startup Manager", language == "tr" ? "Açılışta çalışanları gör, gereksizleri kapat." : "See what runs at startup and switch off the extras.", c, delegate { ShowStartup(); return Task.FromResult(0); }));
            tools.Children.Add(ToolTile(Icons.Chip, language == "tr" ? "Belleği Rahatlat" : "Release Memory", language == "tr" ? "Boştaki çalışma kümelerini kırpar." : "Trims idle working sets.", c, async delegate { await RunMemoryOptimization(); }));
            tools.Children.Add(ToolTile(Icons.Bolt, language == "tr" ? "Odak / Oyun" : "Focus / Game", language == "tr" ? "Güç planını performansa alır." : "Switches power plan for performance.", c, async delegate { await ToggleBoost(); }));
            tools.Children.Add(ToolTile(Icons.Coffee, language == "tr" ? "Uyanık Tut" : "Keep Awake", language == "tr" ? "Render veya indirme bitene kadar uykuya geçmez." : "Prevents sleep during renders or downloads.", c, delegate { ToggleAwake(); return Task.FromResult(0); }));
            tools.Children.Add(ToolTile(Icons.Refresh, language == "tr" ? "DNS Tazele" : "Flush DNS", language == "tr" ? "Bağlantı önbelleğini yeniler." : "Refreshes the connection cache.", c, async delegate { await FlushDns(); }));
            tools.Children.Add(ToolTile(Icons.Layers, "Ghost Overlay", language == "tr" ? "Video penceresini üstte ama tıklanamaz tutar." : "Keeps a video window on top but click-through.", c, delegate { ShowGhostOverlay(); return Task.FromResult(0); }));
            tools.Children.Add(ToolTile(Icons.Headphones, language == "tr" ? "Ses Dengesi" : "Audio Balance", language == "tr" ? "Sol ve sağ kulaklık kanalını ayrı ayarla." : "Adjust left and right headphone channels separately.", c, delegate { ShowAudioBalance(); return Task.FromResult(0); }));
            ScrollViewer scroller = Scroll(tools); Grid.SetRow(scroller, 1); page.Children.Add(scroller); SetPage(page, "tools");
        }

        private void ShowSettings()
        {
            Grid page = PageGrid(); page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); page.RowDefinitions.Add(new RowDefinition());
            page.Children.Add(Header(language == "tr" ? "TERCİHLER" : "PREFERENCES", Theme.SectionSettings, T("settings"), language == "tr" ? "Mantis sana göre çalışsın." : "Make Mantis work your way."));
            StackPanel list = new StackPanel();
            list.Children.Add(SectionLabel(language == "tr" ? "GENEL" : "GENERAL", true));
            list.Children.Add(SettingRow(Icons.Globe, language == "tr" ? "Dil" : "Language", language == "tr" ? "Türkçe" : "English", delegate { language = language == "tr" ? "en" : "tr"; settings.Language = language; settings.Save(); Rebuild(); }));
            list.Children.Add(SettingRow(Icons.Power, language == "tr" ? "Windows ile başlat" : "Launch at startup", StartupManager.IsEnabled() ? (language == "tr" ? "Açık" : "On") : (language == "tr" ? "Kapalı" : "Off"), delegate { StartupManager.SetEnabled(!StartupManager.IsEnabled()); ShowSettings(); }));
            list.Children.Add(SettingRow(Icons.Monitor, language == "tr" ? "Çarpıya basınca" : "When closing", CloseBehaviorLabel(), delegate { AskCloseBehavior(true); ShowSettings(); }));
            list.Children.Add(SectionLabel(language == "tr" ? "GELİŞMİŞ" : "ADVANCED", false));
            list.Children.Add(SettingRow(Icons.ShieldCheck, T("restartAdmin"), language == "tr" ? "Windows Temp ve Prefetch erişimi için" : "For Windows Temp and Prefetch access", RestartAsAdmin));
            list.Children.Add(SettingRow(Icons.FileText, language == "tr" ? "Temizlik raporu" : "Cleanup report", language == "tr" ? "Son işlem kayıtlarını aç" : "Open the latest operation log", OpenLog));
            list.Children.Add(SettingRow(Icons.ShieldCheck, language == "tr" ? "Son Check-Up raporu" : "Latest Check-Up report", language == "tr" ? "Yerelde saklanan HTML raporu aç" : "Open the locally stored HTML report", delegate { OpenReport(lastReport ?? SecurityReport.LatestHtml()); }));
            list.Children.Add(SectionLabel(language == "tr" ? "HAKKINDA" : "ABOUT", false));
            list.Children.Add(SettingRow(Icons.Code, "GitHub", "github.com/kemalselimkoc", delegate { OpenUrl("https://github.com/kemalselimkoc"); }));
            list.Children.Add(SettingRow(Icons.Coffee, "Buy Me a Coffee", "buymeacoffee.com/kemalselimkoc", delegate { OpenUrl("https://buymeacoffee.com/kemalselimkoc"); }));
            TextBlock signature = Label("Powered by kemalselimkoc  ·  v" + AppInfo.Version, Theme.Micro, FontWeights.SemiBold, Theme.AccentPrimary); signature.Margin = new Thickness(4, Theme.S3, 0, Theme.S2); list.Children.Add(signature);
            ScrollViewer scroller = Scroll(list);
            Grid.SetRow(scroller, 1); page.Children.Add(scroller); SetPage(page, "settings");
        }

        // Component gallery (Mantis.exe --page=gallery): renders every state for visual review.
        private void ShowGallery()
        {
            Grid page = PageGrid(); page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); page.RowDefinitions.Add(new RowDefinition());
            page.Children.Add(Header("DESIGN SYSTEM", Theme.Violet, "Components", "Buttons, rows and the four result states.",
                PillButton("Ghost", ButtonKind.Ghost, delegate { return Task.FromResult(0); }, null, Theme.Violet),
                PillButton("Secondary", ButtonKind.Secondary, delegate { return Task.FromResult(0); }, Icons.Search, Theme.AccentPrimary),
                PillButton("Primary", ButtonKind.Primary, delegate { return Task.FromResult(0); }, Icons.Download, Theme.Violet)));
            StackPanel list = new StackPanel();
            // Sample findings (gallery only) to review FindingRow in every severity.
            list.Children.Add(FindingRow(Finding.Issue("G1", Severity.High, "SMBv1 açık", "SMBv1 is enabled", "Eski dosya paylaşım protokolü; fidye yazılımlarının sık kullandığı bir giriş.", "Legacy file-sharing protocol often abused by ransomware.").Recommend("Eski bir cihaz gerektirmiyorsa kapat.", "Turn it off unless an old device needs it.").WithFix(FixKinds.OpenSettings, "ms-settings:", true, "", "")));
            list.Children.Add(FindingRow(Finding.Issue("G2", Severity.Medium, "BitLocker kapalı", "BitLocker is off", "Sistem sürücüsü şifreli değil.", "The system drive is not encrypted.").WithFix(FixKinds.OpenSettings, "ms-settings:deviceencryption", false, "", "")));
            list.Children.Add(FindingRow(Finding.Issue("G3", Severity.Low, "Yerleşik Administrator etkin", "Built-in Administrator is enabled", "Kullanılmıyorsa devre dışı bırakmak saldırı yüzeyini azaltır.", "Disabling it when unused reduces attack surface.")));
            list.Children.Add(FindingRow(Finding.Ok("G4", "Güvenlik Duvarı tüm profillerde açık", "Firewall is on for all profiles")));
            Grid states = new Grid(); states.ColumnDefinitions.Add(new ColumnDefinition()); states.ColumnDefinitions.Add(new ColumnDefinition());
            Border empty = EmptyState(Icons.Shield, language == "tr" ? "Henüz taranmadı" : "Not scanned yet", language == "tr" ? "Check-Up hiçbir ayarı değiştirmeden sisteminin durumunu raporlar." : "Check-Up reports your system's state without changing any setting.", language == "tr" ? "Başlat" : "Start", delegate { return Task.FromResult(0); }, Theme.Amber);
            empty.Margin = new Thickness(0, 0, Theme.S2, Theme.S4); states.Children.Add(empty);
            Border good = GoodState(language == "tr" ? "Her şey yolunda" : "All good", language == "tr" ? "Hiç bulgu yok. Sistemin temiz görünüyor." : "No findings. Your system looks healthy.");
            good.Margin = new Thickness(Theme.S2, 0, 0, Theme.S4); Grid.SetColumn(good, 1); states.Children.Add(good);
            list.Children.Add(states);
            Grid states2 = new Grid(); states2.ColumnDefinitions.Add(new ColumnDefinition()); states2.ColumnDefinitions.Add(new ColumnDefinition());
            StackPanel loading = LoadingState(language == "tr" ? "Kalıcılık taranıyor…" : "Scanning persistence…", 3); loading.Margin = new Thickness(0, 0, Theme.S2, Theme.S4); states2.Children.Add(loading);
            Border error = ErrorState(language == "tr" ? "Bu kontrol yönetici ister" : "This check needs administrator", language == "tr" ? "BitLocker ve Secure Boot durumu standart modda okunamıyor." : "BitLocker and Secure Boot cannot be read in standard mode.", true);
            error.Margin = new Thickness(Theme.S2, 0, 0, Theme.S4); Grid.SetColumn(error, 1); states2.Children.Add(error);
            list.Children.Add(states2);
            ScrollViewer scroller = Scroll(list); Grid.SetRow(scroller, 1); page.Children.Add(scroller); SetPage(page, "gallery");
        }

        private void Rebuild()
        {
            settings.Language = language; settings.Save();
            string page = currentPage;
            navItems.Clear(); Content = BuildShell(); UpdatesBadge(); OpenPage(page); RefreshMetrics();
        }

        private Border ToolTile(Geometry icon, string title, string desc, string color, Func<Task> action)
        {
            Border tile = Card(Theme.RadiusLg, true); tile.Height = 166; tile.Margin = new Thickness(0, 0, Theme.S4, Theme.S4); tile.Padding = new Thickness(Theme.S5 - 4);
            Grid g = new Grid(); g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); g.RowDefinitions.Add(new RowDefinition());
            Grid top = new Grid(); top.ColumnDefinitions.Add(new ColumnDefinition()); top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Border badge = IconBadge(icon, color, 40, 20); badge.HorizontalAlignment = HorizontalAlignment.Left; top.Children.Add(badge);
            Viewbox open = Glyph(Icons.ArrowUpRight, 16, Theme.TextFaint); open.VerticalAlignment = VerticalAlignment.Top; Grid.SetColumn(open, 1); top.Children.Add(open);
            g.Children.Add(top);
            TextBlock t = Label(title, Theme.H3, FontWeights.SemiBold, Theme.TextPrimary); t.Margin = new Thickness(0, Theme.S4 - 2, 0, 0); Grid.SetRow(t, 1); g.Children.Add(t);
            TextBlock d = Paragraph(desc, Theme.Small, Theme.TextMuted); d.Margin = new Thickness(0, Theme.S1, 0, 0); Grid.SetRow(d, 2); g.Children.Add(d);
            tile.Child = g;
            tile.MouseLeftButtonUp += async delegate { if (!busy) await action(); }; return tile;
        }

        private Border SettingRow(Geometry icon, string title, string value, Action action)
        {
            Border row = Glass(Theme.RadiusMd); row.Height = 64; row.Margin = new Thickness(0, 0, 0, Theme.S2); row.Padding = new Thickness(Theme.S3 + 2, 0, Theme.S3, 0); MakeHoverable(row);
            Grid g = new Grid(); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Border badge = IconBadge(icon, Theme.SectionSettings, 36, 18); badge.Margin = new Thickness(0, 0, Theme.S4 - 2, 0); g.Children.Add(badge);
            TextBlock t = Label(title, Theme.Body + 1, FontWeights.Medium, Theme.TextPrimary); t.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(t, 1); g.Children.Add(t);
            StackPanel rhs = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            rhs.Children.Add(Label(value, Theme.Small, FontWeights.Normal, Theme.TextMuted));
            Viewbox chevron = Glyph(Icons.ChevronRight, 16, Theme.AccentPrimary); chevron.Margin = new Thickness(Theme.S2, 0, 0, 0); rhs.Children.Add(chevron);
            Grid.SetColumn(rhs, 2); g.Children.Add(rhs); row.Child = g;
            row.MouseLeftButtonUp += delegate { action(); }; return row;
        }

        private BitmapSource LoadLogo()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "mantis-logo.png");
                if (!File.Exists(path)) return null;
                BitmapImage image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.UriSource = new Uri(path, UriKind.Absolute); image.EndInit(); image.Freeze(); return image;
            }
            catch { return null; }
        }

        // Owned dialog window that follows the app theme, including a dark Windows title bar.
        private Window ThemedDialog(string title, double width, double height)
        {
            Window dialog = new Window { Title = title, Width = width, Height = height, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this, Background = Theme.Brush(Theme.BgElevated), Foreground = Theme.Brush(Theme.TextPrimary), ResizeMode = ResizeMode.NoResize, FontFamily = Theme.TextFont, Icon = Icon };
            dialog.SourceInitialized += delegate
            {
                try
                {
                    IntPtr hwnd = new WindowInteropHelper(dialog).Handle;
                    int dark = 1; Native.DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
                    int caption = Native.ColorRef(Theme.ToColor(Theme.BgElevated)); Native.DwmSetWindowAttribute(hwnd, 35, ref caption, sizeof(int));
                }
                catch { }
            };
            return dialog;
        }

        private void ShowAudioBalance()
        {
            float[] levels;
            try { levels = AudioBalance.GetChannelLevels(); }
            catch (Exception ex)
            {
                MessageBox.Show((language == "tr" ? "Varsayılan ses çıkışı okunamadı. Kulaklığın bağlı ve etkin olduğundan emin ol.\n\n" : "The default audio output could not be read. Make sure your headphones are connected and active.\n\n") + ex.Message, language == "tr" ? "Ses Dengesi" : "Audio Balance", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (levels.Length < 2)
            {
                MessageBox.Show(language == "tr" ? "Seçili çıkış aygıtı ayrı sol/sağ kanal sunmuyor. Stereo bir kulaklık çıkışı seçip tekrar dene." : "The selected output device does not expose separate left/right channels. Select a stereo headphone output and try again.", language == "tr" ? "Ses Dengesi" : "Audio Balance", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Window dialog = ThemedDialog(language == "tr" ? "Ses Dengesi" : "Audio Balance", 740, 450);
            Grid root = new Grid { Margin = new Thickness(Theme.S5) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            StackPanel intro = new StackPanel(); intro.Children.Add(Label(language == "tr" ? "Sol / Sağ Ses Dengesi" : "Left / Right Audio Balance", 24, FontWeights.SemiBold, Theme.TextPrimary));
            TextBlock desc = Paragraph(language == "tr" ? "Varsayılan Windows çıkışının iki kanalını bağımsız ayarlar. Değerler cihaz değiştirilene veya Windows tarafından sıfırlanana kadar korunur." : "Adjusts both channels of the default Windows output independently. Values remain until the device changes or Windows resets them.", Theme.Small, Theme.TextMuted); desc.Margin = new Thickness(0, Theme.S2, 0, Theme.S4); intro.Children.Add(desc); root.Children.Add(intro);

            Grid controls = new Grid { Margin = new Thickness(0, Theme.S1, 0, Theme.S3) }; controls.ColumnDefinitions.Add(new ColumnDefinition()); controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) }); controls.ColumnDefinitions.Add(new ColumnDefinition());
            Slider leftSlider; TextBlock leftValue; Border leftCard = AudioChannelCard(language == "tr" ? "SOL KULAK" : "LEFT EAR", levels[0], out leftSlider, out leftValue); controls.Children.Add(leftCard);
            TextBlock divider = Eyebrow("L · R", Theme.AccentPrimary); divider.HorizontalAlignment = HorizontalAlignment.Center; divider.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(divider, 1); controls.Children.Add(divider);
            Slider rightSlider; TextBlock rightValue; Border rightCard = AudioChannelCard(language == "tr" ? "SAĞ KULAK" : "RIGHT EAR", levels[1], out rightSlider, out rightValue); Grid.SetColumn(rightCard, 2); controls.Children.Add(rightCard);
            leftSlider.ValueChanged += delegate { leftValue.Text = ((int)Math.Round(leftSlider.Value)) + "%"; };
            rightSlider.ValueChanged += delegate { rightValue.Text = ((int)Math.Round(rightSlider.Value)) + "%"; };
            Grid.SetRow(controls, 1); root.Children.Add(controls);

            StackPanel footer = new StackPanel();
            TextBlock warning = Paragraph(language == "tr" ? "İpucu: Sağ kulağı desteklemek için sağı yüksek bırakıp solu kademeli azalt. Ani yüksek sesten kaçın." : "Tip: To support the right ear, keep Right higher and gradually lower Left. Avoid sudden loud output.", Theme.Micro, Theme.Warning); warning.Margin = new Thickness(0, 0, 0, Theme.S3); footer.Children.Add(warning);
            StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            actions.Children.Add(PillButton(language == "tr" ? "Ortala" : "Center", ButtonKind.Secondary, delegate { double centered = Math.Min(100, Math.Max(leftSlider.Value, rightSlider.Value)); leftSlider.Value = centered; rightSlider.Value = centered; return Task.FromResult(0); }));
            actions.Children.Add(PillButton(language == "tr" ? "Solu Destekle" : "Support Left", ButtonKind.Secondary, delegate { leftSlider.Value = Math.Max(leftSlider.Value, 90); rightSlider.Value = Math.Max(15, leftSlider.Value * 0.70); return Task.FromResult(0); }));
            actions.Children.Add(PillButton(language == "tr" ? "Sağı Destekle" : "Support Right", ButtonKind.Secondary, delegate { rightSlider.Value = Math.Max(rightSlider.Value, 90); leftSlider.Value = Math.Max(15, rightSlider.Value * 0.70); return Task.FromResult(0); }));
            Button apply = PillButton(language == "tr" ? "Uygula" : "Apply", ButtonKind.Primary, delegate
            {
                try { AudioBalance.SetChannelLevels((float)(leftSlider.Value / 100.0), (float)(rightSlider.Value / 100.0)); statusText.Text = language == "tr" ? "Ses dengesi uygulandı" : "Audio balance applied"; dialog.Close(); }
                catch (Exception ex) { MessageBox.Show(ex.Message, language == "tr" ? "Ses Dengesi" : "Audio Balance", MessageBoxButton.OK, MessageBoxImage.Warning); }
                return Task.FromResult(0);
            }, Icons.Check, Theme.AccentSecondary);
            apply.Margin = new Thickness(0); actions.Children.Add(apply);
            footer.Children.Add(actions); Grid.SetRow(footer, 2); root.Children.Add(footer); dialog.Content = root; dialog.ShowDialog();
        }

        private Border AudioChannelCard(string title, float value, out Slider slider, out TextBlock valueLabel)
        {
            Border card = Glass(Theme.RadiusLg - 2); card.Padding = new Thickness(Theme.S4 + 2);
            StackPanel stack = new StackPanel(); TextBlock heading = Eyebrow(title, Theme.AccentPrimary); heading.HorizontalAlignment = HorizontalAlignment.Center; stack.Children.Add(heading);
            valueLabel = Label(((int)Math.Round(value * 100)) + "%", Theme.Display, FontWeights.SemiBold, Theme.TextPrimary); Tabular(valueLabel); valueLabel.HorizontalAlignment = HorizontalAlignment.Center; valueLabel.Margin = new Thickness(0, Theme.S3, 0, Theme.S3); stack.Children.Add(valueLabel);
            slider = new Slider { Minimum = 0, Maximum = 100, Value = Math.Max(0, Math.Min(100, value * 100)), TickFrequency = 1, IsSnapToTickEnabled = true, Margin = new Thickness(2, 0, 2, 6) }; stack.Children.Add(slider);
            Grid scale = new Grid(); scale.Children.Add(Label("0", 9, FontWeights.Normal, Theme.TextFaint)); TextBlock max = Label("100", 9, FontWeights.Normal, Theme.TextFaint); max.HorizontalAlignment = HorizontalAlignment.Right; scale.Children.Add(max); stack.Children.Add(scale);
            card.Child = stack; return card;
        }

        private void ShowGhostOverlay()
        {
            Window dialog = ThemedDialog("Ghost Overlay", 620, 490);
            Grid root = new Grid { Margin = new Thickness(Theme.S5 - 2) }; root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            StackPanel intro = new StackPanel(); intro.Children.Add(Label("Ghost Overlay", 24, FontWeights.SemiBold, Theme.TextPrimary)); TextBlock desc = Paragraph(language == "tr" ? "Önce Edge/Chrome içinden videoyu Resim İçinde Resim moduna al.\nMantis yalnızca küçük video penceresini listeler; ana tarayıcı tıklanabilir kalır.\nCtrl + Shift + M: video kontrollerini kilitle / aç." : "First put the video into Picture-in-Picture from Edge/Chrome.\nMantis lists only the compact video window; the main browser stays clickable.\nCtrl + Shift + M: lock/unlock video controls.", Theme.Small, Theme.TextMuted); desc.Margin = new Thickness(0, Theme.S2, 0, Theme.S3); intro.Children.Add(desc); root.Children.Add(intro);
            ListBox windows = new ListBox { Margin = new Thickness(0, Theme.S2, 0, Theme.S3), Background = Theme.Brush(Theme.SurfaceRaised), Foreground = Theme.Brush(Theme.TextPrimary), BorderBrush = Theme.Brush(Theme.Stroke), BorderThickness = new Thickness(1), FontFamily = Theme.TextFont, FontSize = Theme.Body, Padding = new Thickness(Theme.S2) };
            foreach (WindowTarget target in OverlayManager.ListWindows(Process.GetCurrentProcess().Id)) windows.Items.Add(target);
            Grid.SetRow(windows, 1); root.Children.Add(windows);
            StackPanel bottom = new StackPanel();
            Grid opacity = new Grid { Margin = new Thickness(0, 0, 0, Theme.S3) }; opacity.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) }); opacity.ColumnDefinitions.Add(new ColumnDefinition()); opacity.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
            TextBlock opacityTitle = Label(language == "tr" ? "Saydamlık" : "Opacity", Theme.Small, FontWeights.Medium, Theme.TextPrimary); opacityTitle.VerticalAlignment = VerticalAlignment.Center; opacity.Children.Add(opacityTitle);
            Slider opacitySlider = new Slider { Minimum = 25, Maximum = 100, Value = 92, TickFrequency = 5, IsSnapToTickEnabled = true, Margin = new Thickness(Theme.S2, 0, Theme.S3, 0), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(opacitySlider, 1); opacity.Children.Add(opacitySlider);
            TextBlock opacityValue = Label("92%", Theme.Small, FontWeights.SemiBold, Theme.AccentPrimary); Tabular(opacityValue); opacityValue.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(opacityValue, 2); opacity.Children.Add(opacityValue);
            opacitySlider.ValueChanged += delegate { opacityValue.Text = ((int)opacitySlider.Value) + "%"; };
            bottom.Children.Add(opacity);
            StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Button restore = PillButton(language == "tr" ? "Aktif Pencereyi Bırak" : "Release Active", ButtonKind.Secondary, delegate { OverlayManager.Restore(); statusText.Text = T("ready"); return Task.FromResult(0); });
            Button apply = PillButton(language == "tr" ? "Video Penceresini Kilitle" : "Lock Video Window", ButtonKind.Primary, delegate { WindowTarget target = windows.SelectedItem as WindowTarget; if (target != null) { OverlayManager.Apply(target.Handle, (byte)Math.Round(255 * opacitySlider.Value / 100.0)); statusText.Text = language == "tr" ? "Video kilitli • Ctrl+Shift+M ile aç" : "Video locked • Ctrl+Shift+M to interact"; dialog.Close(); } return Task.FromResult(0); }, Icons.Lock, Theme.AccentSecondary);
            apply.Margin = new Thickness(0);
            actions.Children.Add(restore); actions.Children.Add(apply); bottom.Children.Add(actions); Grid.SetRow(bottom, 2); root.Children.Add(bottom); dialog.Content = root; dialog.ShowDialog();
        }

        // ---------- Actions ----------

        private async Task RunCleanup(bool scanOnly)
        {
            SetBusy(true, scanOnly ? (language == "tr" ? "Taranıyor…" : "Scanning…") : (language == "tr" ? "Temizleniyor…" : "Cleaning…"));
            CleanupResult result = await Task.Run(() => Cleaner.Run(scanOnly));
            SetBusy(false, T("ready"));
            string title = scanOnly ? (language == "tr" ? "Tarama tamamlandı" : "Scan complete") : (language == "tr" ? "Temizlik tamamlandı" : "Cleanup complete");
            string body = (scanOnly ? (language == "tr" ? "Kazanılabilir: " : "Reclaimable: ") : (language == "tr" ? "Kazanılan alan: " : "Space reclaimed: ")) + FormatBytes(result.Bytes) + "\n" + result.Files.ToString("N0", UiCulture) + (language == "tr" ? " dosya bulundu/işlendi." : " files found/processed.") + (result.Skipped > 0 ? "\n" + result.Skipped + (language == "tr" ? " kilitli veya erişilemeyen öğe atlandı." : " locked or inaccessible items skipped.") : "") + (result.AdobeRunning ? "\n\n" + (language == "tr" ? "Adobe uygulaması açık olduğu için Adobe cache güvenle atlandı." : "Adobe cache was safely skipped because an Adobe app is open.") : "");
            MessageBox.Show(body, title, MessageBoxButton.OK, MessageBoxImage.Information);
            if (currentPage == "dashboard") ShowDashboard();
        }

        private async Task RunMemoryOptimization()
        {
            SetBusy(true, language == "tr" ? "Bellek rahatlatılıyor…" : "Releasing memory…");
            long before = SystemMonitor.UsedMemoryBytes();
            int count = await Task.Run(() => MemoryOptimizer.TrimProcesses());
            await Task.Delay(500);
            long after = SystemMonitor.UsedMemoryBytes();
            SetBusy(false, T("ready"));
            long delta = Math.Max(0, before - after);
            MessageBox.Show((language == "tr" ? "Windows'a geri verilen bellek: " : "Memory returned to Windows: ") + FormatBytes(delta) + "\n" + count + (language == "tr" ? " süreç güvenle kırpıldı. İhtiyaç olursa bellek otomatik geri yüklenir." : " processes were safely trimmed. Memory is restored automatically when needed."), language == "tr" ? "Uçuş tamam" : "Lift-off complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task ToggleBoost()
        {
            SetBusy(true, language == "tr" ? "Güç planı ayarlanıyor…" : "Adjusting power plan…");
            bool ok = await Task.Run(delegate
            {
                if (!boostActive)
                {
                    lastPowerScheme = PowerTools.GetActiveScheme();
                    boostActive = PowerTools.SetHighPerformance(); return boostActive;
                }
                bool restored = PowerTools.SetScheme(lastPowerScheme); if (restored) boostActive = false; return restored;
            });
            SetBusy(false, boostActive ? (language == "tr" ? "Oyun modu açık" : "Game mode active") : T("ready"));
            UpdateDashboardStates();
            MessageBox.Show(ok ? (boostActive ? (language == "tr" ? "Yüksek performans güç planı açıldı. Aynı düğmeyle eski plana dönebilirsin." : "High-performance power plan enabled. Use the same button to restore the previous plan.") : (language == "tr" ? "Önceki güç planı geri yüklendi." : "Previous power plan restored.")) : (language == "tr" ? "Güç planı değiştirilemedi. Yönetici olarak deneyebilirsin." : "Could not change the power plan. Try as administrator."), "Project Mantis", MessageBoxButton.OK, ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }

        private void ToggleAwake()
        {
            keepAwake = !keepAwake;
            PowerTools.KeepAwake(keepAwake);
            SetBusy(false, keepAwake ? (language == "tr" ? "Uyku engelleniyor" : "Sleep is prevented") : T("ready"));
            UpdateDashboardStates();
            MessageBox.Show(keepAwake ? (language == "tr" ? "Bilgisayar Mantis açıkken uykuya geçmeyecek." : "Your PC will stay awake while Mantis is open.") : (language == "tr" ? "Normal uyku davranışı geri geldi." : "Normal sleep behavior restored."), "Project Mantis");
        }

        private async Task FlushDns()
        {
            SetBusy(true, language == "tr" ? "DNS yenileniyor…" : "Refreshing DNS…");
            int code = await Task.Run(() => PowerTools.Run("ipconfig.exe", "/flushdns"));
            SetBusy(false, T("ready"));
            MessageBox.Show(code == 0 ? (language == "tr" ? "DNS önbelleği yenilendi." : "DNS cache refreshed.") : (language == "tr" ? "DNS yenilenemedi." : "DNS could not be refreshed."), "Project Mantis");
        }

        // Busy: amber pulsing dot plus a sweeping progress line; ready: steady green dot.
        private void SetBusy(bool value, string status)
        {
            busy = value; statusText.Text = status;
            statusDot.Background = Theme.Brush(value ? Theme.Warning : Theme.Success);
            if (value) Pulse(statusDot, 1, 0.35, 0.6, 0); else statusDot.BeginAnimation(OpacityProperty, null);
            TranslateTransform sweep = ((LinearGradientBrush)busyBar.Background).RelativeTransform as TranslateTransform;
            busyBar.BeginAnimation(OpacityProperty, new DoubleAnimation(value ? 1 : 0, Theme.Normal));
            if (sweep != null) sweep.BeginAnimation(TranslateTransform.XProperty, value ? new DoubleAnimation(-1, 1, TimeSpan.FromSeconds(1.3)) { RepeatBehavior = RepeatBehavior.Forever } : null);
        }

        private void RefreshMetrics()
        {
            try
            {
                SystemMetrics m = monitor.Read();
                UpdateGauge(cpuGauge, m.Cpu);
                UpdateGauge(ramGauge, m.Ram);
                UpdateGauge(diskGauge, m.DiskFree);
                bool first = !hasMetrics;
                lastMetrics = m; hasMetrics = true;
                cpuHistory.Add(m.Cpu); if (cpuHistory.Count > HistoryLength) cpuHistory.RemoveAt(0);
                UpdateDashboardLive(m, first);
            }
            catch { }
        }

        private void RestartAsAdmin()
        {
            if (IsAdmin()) { MessageBox.Show(language == "tr" ? "Mantis zaten yönetici olarak çalışıyor." : "Mantis is already running as administrator.", "Project Mantis"); return; }
            try { Process.Start(new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName, "--elevated --page=" + currentPage) { Verb = "runas", UseShellExecute = true }); allowExit = true; Close(); Application.Current.Shutdown(); } catch { }
        }

        private void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        }

        private string CloseBehaviorLabel()
        {
            if (settings.CloseBehavior == "tray") return language == "tr" ? "Sistem tepsisine indir" : "Minimize to system tray";
            if (settings.CloseBehavior == "exit") return language == "tr" ? "Uygulamayı kapat" : "Exit application";
            return language == "tr" ? "İlk seferinde sor" : "Ask the first time";
        }

        // ---------- Window & tray ----------

        private void ToggleMaximize() { WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }

        private void OnWindowStateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized && settings.MinimizeOnMinimize) MinimizeToTray(false);
            if (maximizeIcon != null) maximizeIcon.Data = WindowState == WindowState.Maximized ? Icons.Restore : Icons.Maximize;
        }

        private void InitializeTray()
        {
            trayIcon = new Forms.NotifyIcon();
            trayIcon.Text = "Project Mantis";
            try { trayIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Process.GetCurrentProcess().MainModule.FileName); } catch { trayIcon.Icon = System.Drawing.SystemIcons.Application; }
            Forms.ContextMenuStrip menu = new Forms.ContextMenuStrip();
            menu.Items.Add(language == "tr" ? "Mantis'i Aç" : "Open Mantis", null, delegate { Dispatcher.Invoke(RestoreFromTray); });
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add(language == "tr" ? "Tamamen Kapat" : "Exit Completely", null, delegate { Dispatcher.Invoke(ExitCompletely); });
            trayIcon.ContextMenuStrip = menu;
            trayIcon.DoubleClick += delegate { Dispatcher.Invoke(RestoreFromTray); };
        }

        private void RequestClose()
        {
            if (settings.CloseBehavior == "ask") AskCloseBehavior(false);
            if (settings.CloseBehavior == "tray") MinimizeToTray(true);
            else if (settings.CloseBehavior == "exit") ExitCompletely();
        }

        private void AskCloseBehavior(bool fromSettings)
        {
            MessageBoxResult result = MessageBox.Show(
                language == "tr" ? "Çarpıya bastığında Project Mantis ne yapsın?\n\nEvet: Sistem tepsisine insin\nHayır: Tamamen kapansın\nİptal: Karar verme" : "What should Project Mantis do when you press close?\n\nYes: Minimize to system tray\nNo: Exit completely\nCancel: Keep asking",
                language == "tr" ? "Kapatma davranışı" : "Close behavior",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes) settings.CloseBehavior = "tray";
            else if (result == MessageBoxResult.No) settings.CloseBehavior = "exit";
            else settings.CloseBehavior = "ask";
            settings.Save();
            if (!fromSettings && result == MessageBoxResult.Cancel) Activate();
        }

        private void MinimizeToTray(bool notify)
        {
            ShowInTaskbar = false; Hide(); trayIcon.Visible = true;
            if (notify && !settings.TrayNoticeShown)
            {
                trayIcon.BalloonTipTitle = "Project Mantis";
                trayIcon.BalloonTipText = language == "tr" ? "Mantis sistem tepsisinde çalışmaya devam ediyor. Geri açmak için simgeye çift tıkla." : "Mantis is still running in the system tray. Double-click the icon to restore it.";
                trayIcon.ShowBalloonTip(3500); settings.TrayNoticeShown = true; settings.Save();
            }
        }

        private void RestoreFromTray()
        {
            Show(); ShowInTaskbar = true; WindowState = WindowState.Normal; Activate(); trayIcon.Visible = false;
        }

        private void ExitCompletely()
        {
            allowExit = true; if (trayIcon != null) trayIcon.Visible = false; Close(); Application.Current.Shutdown();
        }

        internal void SelfTestClose()
        {
            allowExit = true;
            Close();
        }

        // Developer aid for README images and visual checks (--snapshot=file.png): renders this window
        // off-screen without taking focus, writes a PNG and exits. It never captures the desktop.
        internal void SnapshotAndExit(string file)
        {
            WindowStartupLocation = WindowStartupLocation.Manual; Left = -20000; Top = -20000; ShowActivated = false; ShowInTaskbar = false;
            ContentRendered += delegate
            {
                DispatcherTimer wait = new DispatcherTimer { Interval = TimeSpan.FromSeconds(Math.Max(0.5, App.SnapshotWait)) };
                wait.Tick += delegate
                {
                    wait.Stop();
                    try
                    {
                        FrameworkElement root = (FrameworkElement)Content;
                        double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
                        RenderTargetBitmap bmp = new RenderTargetBitmap((int)(root.ActualWidth * scale), (int)(root.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
                        bmp.Render(root);
                        PngBitmapEncoder png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bmp));
                        using (FileStream fs = File.Create(Path.GetFullPath(file))) png.Save(fs);
                    }
                    catch (Exception ex) { App.Log("SNAPSHOT " + ex.Message); }
                    allowExit = true; Close(); Application.Current.Shutdown();
                };
                wait.Start();
            };
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            if (allowExit) return;
            e.Cancel = true;
            if (settings.CloseBehavior == "ask") AskCloseBehavior(false);
            if (settings.CloseBehavior == "tray") MinimizeToTray(true);
            else if (settings.CloseBehavior == "exit") Dispatcher.BeginInvoke(new Action(ExitCompletely));
        }

        private void OpenLog()
        {
            string path = Settings.LogPath;
            try
            {
                if (!File.Exists(path)) { Directory.CreateDirectory(Settings.DataDir); File.WriteAllText(path, language == "tr" ? "Henüz işlem kaydı yok." : "No operations recorded yet."); }
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch { }
        }

        private void ApplyBackdrop(object sender, EventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int dark = 1; Native.DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
            int backdrop = 2; Native.DwmSetWindowAttribute(hwnd, 38, ref backdrop, sizeof(int));
            int corners = 2; Native.DwmSetWindowAttribute(hwnd, 33, ref corners, sizeof(int));
            int border = Native.ColorRef(Theme.ToColor("#1E2B23")); Native.DwmSetWindowAttribute(hwnd, 34, ref border, sizeof(int));
            hwndSource = HwndSource.FromHwnd(hwnd); if (hwndSource != null) hwndSource.AddHook(WindowHook);
            Native.RegisterHotKey(hwnd, 0x4D41, 0x0002 | 0x0004, 0x4D);
        }

        private IntPtr WindowHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x0024) Native.FitMaximizedToWorkArea(hwnd, lParam);
            if (msg == 0x0312 && wParam.ToInt32() == 0x4D41)
            {
                bool locked = OverlayManager.ToggleInputLock();
                statusText.Text = locked ? (language == "tr" ? "Video kilitli • Ctrl+Shift+M ile aç" : "Video locked • Ctrl+Shift+M to interact") : (language == "tr" ? "Video kontrolleri açık • Ctrl+Shift+M ile kilitle" : "Video controls enabled • Ctrl+Shift+M to lock");
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void OnClosed(object sender, EventArgs e)
        {
            if (boostActive && !string.IsNullOrWhiteSpace(lastPowerScheme)) PowerTools.SetScheme(lastPowerScheme);
            if (keepAwake) PowerTools.KeepAwake(false);
            OverlayManager.Restore();
            try { Native.UnregisterHotKey(new WindowInteropHelper(this).Handle, 0x4D41); } catch { }
            if (hwndSource != null) hwndSource.RemoveHook(WindowHook);
            if (trayIcon != null) { trayIcon.Visible = false; trayIcon.Dispose(); }
        }

        private static bool IsAdmin()
        {
            try { return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator); } catch { return false; }
        }

        // Numbers follow the UI language, not the Windows locale ("25,6 GB" in Turkish, "25.6 GB" in English).
        private System.Globalization.CultureInfo UiCulture { get { return language == "tr" ? new System.Globalization.CultureInfo("tr-TR") : new System.Globalization.CultureInfo("en-US"); } }

        private string FormatBytes(long value)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" }; double n = value; int i = 0;
            while (n >= 1024 && i < units.Length - 1) { n /= 1024; i++; }
            return n.ToString(i == 0 ? "0" : "0.##", UiCulture) + " " + units[i];
        }
    }
}
