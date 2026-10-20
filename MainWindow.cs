// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace ProjectMantis
{
    public partial class MainWindow : Window
    {
        private const string Accent = "#75FF9B";
        private const string Accent2 = "#2AD66F";
        private const string Bg = "#090C0A";
        private const string Panel = "#D9161D19";
        private const string Panel2 = "#B91D2720";
        private const string Border = "#3475FF9B";
        private const string Text = "#F3F7F4";
        private const string Muted = "#97A39A";

        private readonly Settings settings = Settings.Load();
        private readonly Dictionary<string, string> tr = new Dictionary<string, string>();
        private readonly Dictionary<string, string> en = new Dictionary<string, string>();
        private readonly DispatcherTimer monitorTimer = new DispatcherTimer();
        private readonly SystemMonitor monitor = new SystemMonitor();
        private readonly List<Button> navButtons = new List<Button>();
        private Grid content;
        private TextBlock cpuValue;
        private TextBlock ramValue;
        private TextBlock diskValue;
        private TextBlock statusText;
        private Border statusDot;
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
            language = settings.Language == "en" ? "en" : "tr";
            SeedTranslations();
            Title = "Project Mantis";
            Width = 1120;
            Height = 720;
            MinWidth = 940;
            MinHeight = 620;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResizeWithGrip;
            Background = Brush(Bg);
            AllowsTransparency = false;
            Icon = LoadLogo();
            Content = BuildShell();
            SourceInitialized += ApplyBackdrop;
            Closed += OnClosed;
            Loaded += delegate { App.Log("MainWindow loaded"); };
            Closing += OnClosing;
            StateChanged += delegate { if (WindowState == WindowState.Minimized && settings.MinimizeOnMinimize) MinimizeToTray(false); };
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
        }

        private string T(string key) { return (language == "en" ? en : tr).ContainsKey(key) ? (language == "en" ? en[key] : tr[key]) : key; }
        private static SolidColorBrush Brush(string color) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); }
        private static Thickness Pad(double all) { return new Thickness(all); }

        private UIElement BuildShell()
        {
            Grid root = new Grid();
            root.Background = new LinearGradientBrush(
                (Color)ColorConverter.ConvertFromString("#070A08"),
                (Color)ColorConverter.ConvertFromString("#0D1510"), 28);

            Border glow = new Border { Width = 520, Height = 520, CornerRadius = new CornerRadius(260), Opacity = .10, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(-220, -250, 0, 0), Background = Brush(Accent) };
            root.Children.Add(glow);

            Grid shell = new Grid { Margin = new Thickness(18) };
            shell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(238) });
            shell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.Children.Add(shell);
            shell.Children.Add(BuildSidebar());

            Grid right = new Grid { Margin = new Thickness(14, 0, 0, 0) };
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(62) });
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(right, 1);
            shell.Children.Add(right);
            right.Children.Add(BuildTitleBar());
            content = new Grid { Margin = new Thickness(0, 14, 0, 0) };
            Grid.SetRow(content, 1);
            right.Children.Add(content);
            return root;
        }

        private UIElement BuildSidebar()
        {
            Border side = Glass(22, new Thickness(1));
            side.Background = Brush("#D90E1510");
            Grid grid = new Grid { Margin = new Thickness(18) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            side.Child = grid;

            StackPanel brand = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2, 2, 0, 26) };
            Image logo = new Image { Width = 48, Height = 48, Stretch = Stretch.Uniform, Source = LoadLogo() };
            brand.Children.Add(logo);
            StackPanel names = new StackPanel { Margin = new Thickness(10, 5, 0, 0) };
            names.Children.Add(Label("MANTIS", 21, FontWeights.SemiBold, Text));
            TextBlock sub = Label("S Y S T E M   C A R E", 9, FontWeights.Bold, Accent); names.Children.Add(sub);
            brand.Children.Add(names);
            grid.Children.Add(brand);

            StackPanel metrics = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 22) };
            cpuValue = MetricMini("CPU", metrics);
            ramValue = MetricMini("RAM", metrics);
            diskValue = MetricMini(language == "tr" ? "DİSK" : "DISK", metrics);
            Grid.SetRow(metrics, 1); grid.Children.Add(metrics);

            StackPanel nav = new StackPanel();
            nav.Children.Add(Nav("⌂", T("dashboard"), ShowDashboard));
            nav.Children.Add(Nav("✦", T("clean"), ShowCleaner));
            nav.Children.Add(Nav("⚡", T("tools"), ShowTools));
            nav.Children.Add(Nav("⚙", T("settings"), ShowSettings));
            Grid.SetRow(nav, 2); grid.Children.Add(nav);

            Border foot = new Border { Background = Brush("#16271C"), CornerRadius = new CornerRadius(13), Padding = new Thickness(12) };
            StackPanel fs = new StackPanel();
            TextBlock privilege = Label(IsAdmin() ? "●  " + T("admin") : "●  " + T("standard"), 11, FontWeights.SemiBold, IsAdmin() ? Accent : "#E6BA5B");
            fs.Children.Add(privilege);
            fs.Children.Add(Label("v1.3  •  LOCAL & PRIVATE", 9, FontWeights.Medium, Muted));
            TextBlock powered = Label("Powered by kemalselimkoc", 9, FontWeights.Medium, Accent); powered.Margin = new Thickness(0, 5, 0, 0); fs.Children.Add(powered);
            foot.Child = fs; Grid.SetRow(foot, 3); grid.Children.Add(foot);
            return side;
        }

        private UIElement BuildTitleBar()
        {
            Border bar = Glass(18, new Thickness(1));
            Grid g = new Grid { Margin = new Thickness(18, 0, 10, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bar.Child = g;
            StackPanel state = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            statusDot = new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(4), Background = Brush(Accent), Margin = new Thickness(0, 0, 9, 0) };
            statusText = Label(T("ready"), 12, FontWeights.Medium, Muted);
            state.Children.Add(statusDot); state.Children.Add(statusText);
            g.Children.Add(state);
            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal };
            buttons.Children.Add(TitleButton("—", delegate { WindowState = WindowState.Minimized; }));
            buttons.Children.Add(TitleButton("□", delegate { WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }));
            Button close = TitleButton("×", RequestClose); close.Foreground = Brush("#FF8E8E"); buttons.Children.Add(close);
            Grid.SetColumn(buttons, 1); g.Children.Add(buttons);
            bar.MouseLeftButtonDown += delegate { if (Mouse.LeftButton == MouseButtonState.Pressed) DragMove(); };
            return bar;
        }

        private TextBlock MetricMini(string name, Panel parent)
        {
            Border b = new Border { Width = 60, Padding = new Thickness(7, 8, 7, 8), Margin = new Thickness(0, 0, 7, 0), CornerRadius = new CornerRadius(10), Background = Brush("#17231B") };
            StackPanel s = new StackPanel();
            s.Children.Add(Label(name, 8, FontWeights.Bold, Muted));
            TextBlock value = Label("—", 14, FontWeights.SemiBold, Text); s.Children.Add(value);
            b.Child = s; parent.Children.Add(b); return value;
        }

        private Button Nav(string icon, string title, Action action)
        {
            Button b = new Button { Content = icon + "    " + title, Height = 46, HorizontalContentAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 7), Padding = new Thickness(14, 0, 0, 0), Foreground = Brush(Muted), Background = Brushes.Transparent, BorderThickness = new Thickness(0), FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"), FontSize = 13, Cursor = Cursors.Hand, Tag = action };
            b.Template = RoundedButtonTemplate(12, "#00000000", "#1E75FF9B");
            b.Click += delegate
            {
                foreach (Button n in navButtons) { n.Foreground = Brush(Muted); n.Background = Brushes.Transparent; }
                b.Foreground = Brush(Accent); b.Background = Brush("#1E75FF9B"); action();
            };
            navButtons.Add(b); return b;
        }

        private void SetPage(UIElement page)
        {
            content.Children.Clear(); content.Children.Add(page);
            page.Opacity = 0;
            page.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));
        }

        private void ShowDashboard()
        {
            Grid page = PageGrid();
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            StackPanel head = Header(language == "tr" ? "Günaydın." : "Good morning.", language == "tr" ? "Bilgisayarın sakin. İstersen biraz daha hafifletelim." : "Your PC looks calm. Let's keep it light.");
            page.Children.Add(head);
            Grid cards = new Grid { Margin = new Thickness(0, 18, 0, 0) };
            cards.ColumnDefinitions.Add(new ColumnDefinition()); cards.ColumnDefinitions.Add(new ColumnDefinition());
            cards.RowDefinitions.Add(new RowDefinition()); cards.RowDefinitions.Add(new RowDefinition());
            Border clean = ActionCard("CLEAN", language == "tr" ? "Temizle!" : "Clean it!", language == "tr" ? "Geçici dosyaları ve Adobe cache'lerini güvenle süpür." : "Safely sweep temporary files and Adobe caches.", "✦", Accent, async delegate { await RunCleanup(false); });
            clean.Margin = new Thickness(0, 0, 8, 8); cards.Children.Add(clean);
            Border ram = ActionCard("MEMORY", language == "tr" ? "Uçur!" : "Lift off!", language == "tr" ? "Boşta tutulan çalışma belleğini Windows'a geri ver." : "Return idle working memory back to Windows.", "↗", "#78C7FF", async delegate { await RunMemoryOptimization(); });
            ram.Margin = new Thickness(8, 0, 0, 8); Grid.SetColumn(ram, 1); cards.Children.Add(ram);
            Border boost = ActionCard("FOCUS", language == "tr" ? "Oyun Modu" : "Game Mode", language == "tr" ? "Yüksek performans güç planını tek tıkla aç / geri al." : "Toggle the high-performance power plan and restore it later.", "⚡", "#FFD873", async delegate { await ToggleBoost(); });
            boost.Margin = new Thickness(0, 8, 8, 0); Grid.SetRow(boost, 1); cards.Children.Add(boost);
            Border scan = ActionCard("INSIGHT", language == "tr" ? "Önce Tara" : "Scan First", language == "tr" ? "Hiçbir şeyi silmeden kazanılabilir alanı gör." : "See reclaimable space without deleting anything.", "◎", "#CA9CFF", async delegate { await RunCleanup(true); });
            scan.Margin = new Thickness(8, 8, 0, 0); Grid.SetRow(scan, 1); Grid.SetColumn(scan, 1); cards.Children.Add(scan);
            Grid.SetRow(cards, 1); page.Children.Add(cards); SetPage(page);
        }

        private void ShowCleaner()
        {
            Grid page = PageGrid();
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            page.Children.Add(Header(language == "tr" ? "Akıllı Temizlik" : "Smart Cleanup", language == "tr" ? "Bilinen ve güvenli cache konumları. Açık uygulamaların dosyaları atlanır." : "Known safe cache locations. Files used by open apps are skipped."));
            StackPanel list = new StackPanel { Margin = new Thickness(0, 18, 0, 0) };
            list.Children.Add(InfoRow("TEMP", language == "tr" ? "Kullanıcı ve Windows geçici dosyaları" : "User and Windows temporary files", "#75FF9B"));
            list.Children.Add(InfoRow("ADOBE", language == "tr" ? "After Effects / Premiere medya cache ve peak dosyaları" : "After Effects / Premiere media cache and peak files", "#B894FF"));
            list.Children.Add(InfoRow("GPU", language == "tr" ? "DirectX shader cache" : "DirectX shader cache", "#76C8FF"));
            list.Children.Add(InfoRow("CRASH", language == "tr" ? "Eski çökme dökümleri ve hata raporları" : "Old crash dumps and error reports", "#FFCA73"));
            StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };
            actions.Children.Add(PillButton(language == "tr" ? "Önce Tara" : "Scan First", "#243127", async delegate { await RunCleanup(true); }));
            actions.Children.Add(PillButton(language == "tr" ? "Temizle!" : "Clean it!", Accent2, async delegate { await RunCleanup(false); }));
            list.Children.Add(actions); Grid.SetRow(list, 1); page.Children.Add(list); SetPage(page);
        }

        private void ShowTools()
        {
            Grid page = PageGrid(); page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); page.RowDefinitions.Add(new RowDefinition());
            page.Children.Add(Header(language == "tr" ? "İnce Araçlar" : "Thoughtful Tools", language == "tr" ? "Günlük kullanımda gerçekten işe yarayan, geri alınabilir dokunuşlar." : "Small, reversible touches that genuinely help every day."));
            WrapPanel tools = new WrapPanel { Margin = new Thickness(0, 18, 0, 0) };
            tools.Children.Add(ToolTile("↗", language == "tr" ? "Belleği Rahatlat" : "Release Memory", language == "tr" ? "Boştaki çalışma kümelerini kırpar." : "Trims idle working sets.", async delegate { await RunMemoryOptimization(); }));
            tools.Children.Add(ToolTile("⚡", language == "tr" ? "Odak / Oyun" : "Focus / Game", language == "tr" ? "Güç planını performansa alır." : "Switches power plan for performance.", async delegate { await ToggleBoost(); }));
            tools.Children.Add(ToolTile("☕", language == "tr" ? "Uyanık Tut" : "Keep Awake", language == "tr" ? "Render veya indirme bitene kadar uykuya geçmez." : "Prevents sleep during renders or downloads.", delegate { ToggleAwake(); return Task.FromResult(0); }));
            tools.Children.Add(ToolTile("⌁", language == "tr" ? "DNS Tazele" : "Flush DNS", language == "tr" ? "Bağlantı önbelleğini yeniler." : "Refreshes the connection cache.", async delegate { await FlushDns(); }));
            tools.Children.Add(ToolTile("◫", "Ghost Overlay", language == "tr" ? "Video penceresini üstte ama tıklanamaz tutar." : "Keeps a video window on top but click-through.", delegate { ShowGhostOverlay(); return Task.FromResult(0); }));
            tools.Children.Add(ToolTile("◖◗", language == "tr" ? "Ses Dengesi" : "Audio Balance", language == "tr" ? "Sol ve sağ kulaklık kanalını ayrı ayarla." : "Adjust left and right headphone channels separately.", delegate { ShowAudioBalance(); return Task.FromResult(0); }));
            Grid.SetRow(tools, 1); page.Children.Add(tools); SetPage(page);
        }

        private void ShowSettings()
        {
            Grid page = PageGrid(); page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); page.RowDefinitions.Add(new RowDefinition());
            page.Children.Add(Header(T("settings"), language == "tr" ? "Mantis sana göre çalışsın." : "Make Mantis work your way."));
            StackPanel list = new StackPanel { Margin = new Thickness(0, 18, 0, 0) };
            list.Children.Add(SettingRow(language == "tr" ? "Dil" : "Language", language == "tr" ? "Türkçe" : "English", delegate { language = language == "tr" ? "en" : "tr"; settings.Language = language; settings.Save(); Rebuild(); }));
            list.Children.Add(SettingRow(language == "tr" ? "Windows ile başlat" : "Launch at startup", StartupManager.IsEnabled() ? (language == "tr" ? "Açık" : "On") : (language == "tr" ? "Kapalı" : "Off"), delegate { StartupManager.SetEnabled(!StartupManager.IsEnabled()); ShowSettings(); }));
            list.Children.Add(SettingRow(language == "tr" ? "Çarpıya basınca" : "When closing", CloseBehaviorLabel(), delegate { AskCloseBehavior(true); ShowSettings(); }));
            list.Children.Add(SettingRow(language == "tr" ? "Yönetici olarak yeniden başlat" : "Restart as administrator", language == "tr" ? "Windows Temp ve Prefetch erişimi için" : "For Windows Temp and Prefetch access", RestartAsAdmin));
            list.Children.Add(SettingRow(language == "tr" ? "Temizlik raporu" : "Cleanup report", language == "tr" ? "Son işlem kayıtlarını aç" : "Open the latest operation log", OpenLog));
            list.Children.Add(SettingRow("GitHub", "github.com/kemalselimkoc", delegate { OpenUrl("https://github.com/kemalselimkoc"); }));
            list.Children.Add(SettingRow("Buy Me a Coffee", "buymeacoffee.com/kemalselimkoc", delegate { OpenUrl("https://buymeacoffee.com/kemalselimkoc"); }));
            TextBlock signature = Label("Powered by kemalselimkoc", 11, FontWeights.SemiBold, Accent); signature.Margin = new Thickness(4, 8, 0, 0); list.Children.Add(signature);
            ScrollViewer scroller = new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            Grid.SetRow(scroller, 1); page.Children.Add(scroller); SetPage(page);
        }

        private void Rebuild()
        {
            settings.Language = language; settings.Save();
            navButtons.Clear(); Content = BuildShell(); ShowDashboard(); RefreshMetrics();
        }

        private Border ActionCard(string eyebrow, string title, string desc, string icon, string color, Func<Task> action)
        {
            Border card = Glass(20, new Thickness(1)); card.Cursor = Cursors.Hand; card.Padding = new Thickness(22);
            Grid g = new Grid(); g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); g.RowDefinitions.Add(new RowDefinition()); g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid top = new Grid(); top.ColumnDefinitions.Add(new ColumnDefinition()); top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TextBlock eye = Label(eyebrow, 10, FontWeights.Bold, color); top.Children.Add(eye);
            Border ib = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(12), Background = Brush("#1F" + color.Substring(1)), Child = Label(icon, 19, FontWeights.Medium, color) }; ((TextBlock)ib.Child).HorizontalAlignment = HorizontalAlignment.Center; ((TextBlock)ib.Child).VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(ib, 1); top.Children.Add(ib); g.Children.Add(top);
            StackPanel middle = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            middle.Children.Add(Label(title, 27, FontWeights.SemiBold, Text)); TextBlock d = Label(desc, 13, FontWeights.Normal, Muted); d.TextWrapping = TextWrapping.Wrap; d.Margin = new Thickness(0, 8, 25, 0); middle.Children.Add(d);
            Grid.SetRow(middle, 1); g.Children.Add(middle);
            TextBlock go = Label(language == "tr" ? "Ç A L I Ş T I R   →" : "R U N   →", 10, FontWeights.Bold, color); Grid.SetRow(go, 2); g.Children.Add(go);
            card.Child = g; card.MouseLeftButtonUp += async delegate { if (!busy) await action(); };
            return card;
        }

        private Border ToolTile(string icon, string title, string desc, Func<Task> action)
        {
            Border tile = Glass(18, new Thickness(1)); tile.Width = 290; tile.Height = 155; tile.Margin = new Thickness(0, 0, 14, 14); tile.Padding = new Thickness(18); tile.Cursor = Cursors.Hand;
            StackPanel s = new StackPanel(); s.Children.Add(Label(icon, 22, FontWeights.Normal, Accent)); s.Children.Add(Label(title, 16, FontWeights.SemiBold, Text)); TextBlock d = Label(desc, 12, FontWeights.Normal, Muted); d.Margin = new Thickness(0, 8, 0, 0); d.TextWrapping = TextWrapping.Wrap; s.Children.Add(d); tile.Child = s;
            tile.MouseLeftButtonUp += async delegate { if (!busy) await action(); }; return tile;
        }

        private Border InfoRow(string badge, string title, string color)
        {
            Border row = Glass(15, new Thickness(1)); row.Height = 68; row.Margin = new Thickness(0, 0, 0, 9); row.Padding = new Thickness(16, 0, 16, 0);
            Grid g = new Grid(); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) }); g.ColumnDefinitions.Add(new ColumnDefinition());
            TextBlock b = Label(badge, 10, FontWeights.Bold, color); b.VerticalAlignment = VerticalAlignment.Center; g.Children.Add(b);
            TextBlock t = Label(title, 13, FontWeights.Medium, Text); t.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(t, 1); g.Children.Add(t); row.Child = g; return row;
        }

        private Border SettingRow(string title, string value, Action action)
        {
            Border row = Glass(15, new Thickness(1)); row.Height = 72; row.Margin = new Thickness(0, 0, 0, 10); row.Padding = new Thickness(18, 0, 12, 0); row.Cursor = Cursors.Hand;
            Grid g = new Grid(); g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TextBlock t = Label(title, 14, FontWeights.Medium, Text); t.VerticalAlignment = VerticalAlignment.Center; g.Children.Add(t);
            StackPanel rhs = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            rhs.Children.Add(Label(value, 12, FontWeights.Normal, Muted)); TextBlock arrow = Label("  ›", 22, FontWeights.Light, Accent); rhs.Children.Add(arrow); Grid.SetColumn(rhs, 1); g.Children.Add(rhs); row.Child = g;
            row.MouseLeftButtonUp += delegate { action(); }; return row;
        }

        private StackPanel Header(string title, string subtitle)
        {
            StackPanel p = new StackPanel(); p.Children.Add(Label(title, 30, FontWeights.SemiBold, Text)); TextBlock s = Label(subtitle, 13, FontWeights.Normal, Muted); s.Margin = new Thickness(1, 5, 0, 0); p.Children.Add(s); return p;
        }
        private Grid PageGrid() { return new Grid { Margin = new Thickness(8, 4, 8, 8) }; }

        private Border Glass(double radius, Thickness border)
        {
            return new Border { Background = Brush(Panel), BorderBrush = Brush(Border), BorderThickness = border, CornerRadius = new CornerRadius(radius) };
        }

        private TextBlock Label(string text, double size, FontWeight weight, string color)
        {
            return new TextBlock { Text = text, FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"), FontSize = size, FontWeight = weight, Foreground = Brush(color), TextTrimming = TextTrimming.CharacterEllipsis };
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

            Window dialog = new Window { Title = language == "tr" ? "Ses Dengesi" : "Audio Balance", Width = 570, Height = 430, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this, Background = Brush(Bg), Foreground = Brush(Text), ResizeMode = ResizeMode.NoResize };
            Grid root = new Grid { Margin = new Thickness(24) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            StackPanel intro = new StackPanel(); intro.Children.Add(Label(language == "tr" ? "Sol / Sağ Ses Dengesi" : "Left / Right Audio Balance", 24, FontWeights.SemiBold, Text));
            TextBlock desc = Label(language == "tr" ? "Varsayılan Windows çıkışının iki kanalını bağımsız ayarlar. Değerler cihaz değiştirilene veya Windows tarafından sıfırlanana kadar korunur." : "Adjusts both channels of the default Windows output independently. Values remain until the device changes or Windows resets them.", 12, FontWeights.Normal, Muted); desc.TextWrapping = TextWrapping.Wrap; desc.Margin = new Thickness(0, 7, 0, 18); intro.Children.Add(desc); root.Children.Add(intro);

            Grid controls = new Grid { Margin = new Thickness(0, 6, 0, 12) }; controls.ColumnDefinitions.Add(new ColumnDefinition()); controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) }); controls.ColumnDefinitions.Add(new ColumnDefinition());
            Slider leftSlider; TextBlock leftValue; Border leftCard = AudioChannelCard(language == "tr" ? "SOL KULAK" : "LEFT EAR", levels[0], out leftSlider, out leftValue); controls.Children.Add(leftCard);
            TextBlock divider = Label("L  •  R", 10, FontWeights.Bold, Accent); divider.HorizontalAlignment = HorizontalAlignment.Center; divider.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(divider, 1); controls.Children.Add(divider);
            Slider rightSlider; TextBlock rightValue; Border rightCard = AudioChannelCard(language == "tr" ? "SAĞ KULAK" : "RIGHT EAR", levels[1], out rightSlider, out rightValue); Grid.SetColumn(rightCard, 2); controls.Children.Add(rightCard);
            leftSlider.ValueChanged += delegate { leftValue.Text = ((int)Math.Round(leftSlider.Value)) + "%"; };
            rightSlider.ValueChanged += delegate { rightValue.Text = ((int)Math.Round(rightSlider.Value)) + "%"; };
            Grid.SetRow(controls, 1); root.Children.Add(controls);

            StackPanel footer = new StackPanel();
            TextBlock warning = Label(language == "tr" ? "İpucu: Sağ kulağı desteklemek için sağı yüksek bırakıp solu kademeli azalt. Ani yüksek sesten kaçın." : "Tip: To support the right ear, keep Right higher and gradually lower Left. Avoid sudden loud output.", 11, FontWeights.Normal, "#E6BA5B"); warning.TextWrapping = TextWrapping.Wrap; warning.Margin = new Thickness(0, 0, 0, 12); footer.Children.Add(warning);
            StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            actions.Children.Add(PillButton(language == "tr" ? "Ortala" : "Center", "#243127", delegate { double centered = Math.Min(100, Math.Max(leftSlider.Value, rightSlider.Value)); leftSlider.Value = centered; rightSlider.Value = centered; return Task.FromResult(0); }));
            actions.Children.Add(PillButton(language == "tr" ? "Sağı Destekle" : "Support Right", "#243127", delegate { rightSlider.Value = Math.Max(rightSlider.Value, 90); leftSlider.Value = Math.Max(15, rightSlider.Value * 0.70); return Task.FromResult(0); }));
            actions.Children.Add(PillButton(language == "tr" ? "Uygula" : "Apply", Accent2, delegate
            {
                try { AudioBalance.SetChannelLevels((float)(leftSlider.Value / 100.0), (float)(rightSlider.Value / 100.0)); statusText.Text = language == "tr" ? "Ses dengesi uygulandı" : "Audio balance applied"; dialog.Close(); }
                catch (Exception ex) { MessageBox.Show(ex.Message, language == "tr" ? "Ses Dengesi" : "Audio Balance", MessageBoxButton.OK, MessageBoxImage.Warning); }
                return Task.FromResult(0);
            }));
            footer.Children.Add(actions); Grid.SetRow(footer, 2); root.Children.Add(footer); dialog.Content = root; dialog.ShowDialog();
        }

        private Border AudioChannelCard(string title, float value, out Slider slider, out TextBlock valueLabel)
        {
            Border card = Glass(18, new Thickness(1)); card.Padding = new Thickness(18); card.Margin = new Thickness(0, 0, 0, 0);
            StackPanel stack = new StackPanel(); TextBlock heading = Label(title, 10, FontWeights.Bold, Accent); heading.HorizontalAlignment = HorizontalAlignment.Center; stack.Children.Add(heading);
            valueLabel = Label(((int)Math.Round(value * 100)) + "%", 30, FontWeights.SemiBold, Text); valueLabel.HorizontalAlignment = HorizontalAlignment.Center; valueLabel.Margin = new Thickness(0, 12, 0, 12); stack.Children.Add(valueLabel);
            slider = new Slider { Minimum = 0, Maximum = 100, Value = Math.Max(0, Math.Min(100, value * 100)), TickFrequency = 1, IsSnapToTickEnabled = true, Margin = new Thickness(2, 0, 2, 6) }; stack.Children.Add(slider);
            TextBlock scale = Label("0                                      100", 9, FontWeights.Normal, Muted); scale.HorizontalAlignment = HorizontalAlignment.Stretch; stack.Children.Add(scale); card.Child = stack; return card;
        }

        private void ShowGhostOverlay()
        {
            Window dialog = new Window { Title = "Ghost Overlay", Width = 600, Height = 470, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this, Background = Brush(Bg), Foreground = Brush(Text), ResizeMode = ResizeMode.NoResize };
            Grid root = new Grid { Margin = new Thickness(22) }; root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            StackPanel intro = new StackPanel(); intro.Children.Add(Label("Ghost Overlay", 24, FontWeights.SemiBold, Text)); TextBlock desc = Label(language == "tr" ? "Önce Edge/Chrome içinden videoyu Resim İçinde Resim moduna al.\nMantis yalnızca küçük video penceresini listeler; ana tarayıcı tıklanabilir kalır.\nCtrl + Shift + M: video kontrollerini kilitle / aç." : "First put the video into Picture-in-Picture from Edge/Chrome.\nMantis lists only the compact video window; the main browser stays clickable.\nCtrl + Shift + M: lock/unlock video controls.", 12, FontWeights.Normal, Muted); desc.Margin = new Thickness(0, 7, 0, 14); intro.Children.Add(desc); root.Children.Add(intro);
            ListBox windows = new ListBox { Margin = new Thickness(0, 8, 0, 14), Background = Brush("#131B15"), Foreground = Brush(Text), BorderBrush = Brush(Border), BorderThickness = new Thickness(1), FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"), FontSize = 13, Padding = new Thickness(8) };
            foreach (WindowTarget target in OverlayManager.ListWindows(Process.GetCurrentProcess().Id)) windows.Items.Add(target);
            Grid.SetRow(windows, 1); root.Children.Add(windows);
            StackPanel bottom = new StackPanel();
            Grid opacity = new Grid { Margin = new Thickness(0, 0, 0, 12) }; opacity.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) }); opacity.ColumnDefinitions.Add(new ColumnDefinition()); opacity.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
            TextBlock opacityTitle = Label(language == "tr" ? "Saydamlık" : "Opacity", 12, FontWeights.Medium, Text); opacityTitle.VerticalAlignment = VerticalAlignment.Center; opacity.Children.Add(opacityTitle);
            Slider opacitySlider = new Slider { Minimum = 25, Maximum = 100, Value = 92, TickFrequency = 5, IsSnapToTickEnabled = true, Margin = new Thickness(8, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(opacitySlider, 1); opacity.Children.Add(opacitySlider);
            TextBlock opacityValue = Label("92%", 12, FontWeights.SemiBold, Accent); opacityValue.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(opacityValue, 2); opacity.Children.Add(opacityValue);
            opacitySlider.ValueChanged += delegate { opacityValue.Text = ((int)opacitySlider.Value) + "%"; };
            bottom.Children.Add(opacity);
            StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Button restore = PillButton(language == "tr" ? "Aktif Pencereyi Bırak" : "Release Active", "#243127", delegate { OverlayManager.Restore(); statusText.Text = T("ready"); return Task.FromResult(0); });
            Button apply = PillButton(language == "tr" ? "Video Penceresini Kilitle" : "Lock Video Window", Accent2, delegate { WindowTarget target = windows.SelectedItem as WindowTarget; if (target != null) { OverlayManager.Apply(target.Handle, (byte)Math.Round(255 * opacitySlider.Value / 100.0)); statusText.Text = language == "tr" ? "Video kilitli • Ctrl+Shift+M ile aç" : "Video locked • Ctrl+Shift+M to interact"; dialog.Close(); } return Task.FromResult(0); });
            actions.Children.Add(restore); actions.Children.Add(apply); bottom.Children.Add(actions); Grid.SetRow(bottom, 2); root.Children.Add(bottom); dialog.Content = root; dialog.ShowDialog();
        }

        private Button PillButton(string title, string color, Func<Task> click)
        {
            Button b = new Button { Content = title, Height = 44, MinWidth = 140, Padding = new Thickness(20, 0, 20, 0), Margin = new Thickness(0, 0, 10, 0), Foreground = color == Accent2 ? Brush("#071009") : Brush(Text), Background = Brush(color), BorderThickness = new Thickness(0), FontWeight = FontWeights.SemiBold, Cursor = Cursors.Hand };
            b.Template = RoundedButtonTemplate(13, color, color); b.Click += async delegate { if (!busy) await click(); }; return b;
        }

        private Button TitleButton(string text, Action action)
        {
            Button b = new Button { Content = text, Width = 42, Height = 40, Foreground = Brush(Muted), Background = Brushes.Transparent, BorderThickness = new Thickness(0), FontSize = 16, Cursor = Cursors.Hand };
            b.Template = RoundedButtonTemplate(10, "#00000000", "#1CFFFFFF"); b.Click += delegate { action(); }; return b;
        }

        private ControlTemplate RoundedButtonTemplate(double radius, string normal, string hover)
        {
            string xaml = "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'><Border x:Name='B' Background='{TemplateBinding Background}' CornerRadius='" + radius.ToString(CultureInfo.InvariantCulture) + "'><ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='B' Property='Background' Value='" + hover + "'/></Trigger></ControlTemplate.Triggers></ControlTemplate>";
            return (ControlTemplate)System.Windows.Markup.XamlReader.Parse(xaml);
        }

        private async Task RunCleanup(bool scanOnly)
        {
            SetBusy(true, scanOnly ? (language == "tr" ? "Taranıyor…" : "Scanning…") : (language == "tr" ? "Temizleniyor…" : "Cleaning…"));
            CleanupResult result = await Task.Run(() => Cleaner.Run(scanOnly));
            SetBusy(false, T("ready"));
            string title = scanOnly ? (language == "tr" ? "Tarama tamamlandı" : "Scan complete") : (language == "tr" ? "Temizlik tamamlandı" : "Cleanup complete");
            string body = (scanOnly ? (language == "tr" ? "Kazanılabilir: " : "Reclaimable: ") : (language == "tr" ? "Kazanılan alan: " : "Space reclaimed: ")) + FormatBytes(result.Bytes) + "\n" + result.Files.ToString("N0") + (language == "tr" ? " dosya bulundu/işlendi." : " files found/processed.") + (result.Skipped > 0 ? "\n" + result.Skipped + (language == "tr" ? " kilitli veya erişilemeyen öğe atlandı." : " locked or inaccessible items skipped.") : "") + (result.AdobeRunning ? "\n\n" + (language == "tr" ? "Adobe uygulaması açık olduğu için Adobe cache güvenle atlandı." : "Adobe cache was safely skipped because an Adobe app is open.") : "");
            MessageBox.Show(body, title, MessageBoxButton.OK, MessageBoxImage.Information);
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
            MessageBox.Show(ok ? (boostActive ? (language == "tr" ? "Yüksek performans güç planı açıldı. Aynı düğmeyle eski plana dönebilirsin." : "High-performance power plan enabled. Use the same button to restore the previous plan.") : (language == "tr" ? "Önceki güç planı geri yüklendi." : "Previous power plan restored.")) : (language == "tr" ? "Güç planı değiştirilemedi. Yönetici olarak deneyebilirsin." : "Could not change the power plan. Try as administrator."), "Project Mantis", MessageBoxButton.OK, ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }

        private void ToggleAwake()
        {
            keepAwake = !keepAwake;
            PowerTools.KeepAwake(keepAwake);
            SetBusy(false, keepAwake ? (language == "tr" ? "Uyku engelleniyor" : "Sleep is prevented") : T("ready"));
            MessageBox.Show(keepAwake ? (language == "tr" ? "Bilgisayar Mantis açıkken uykuya geçmeyecek." : "Your PC will stay awake while Mantis is open.") : (language == "tr" ? "Normal uyku davranışı geri geldi." : "Normal sleep behavior restored."), "Project Mantis");
        }

        private async Task FlushDns()
        {
            SetBusy(true, language == "tr" ? "DNS yenileniyor…" : "Refreshing DNS…");
            int code = await Task.Run(() => PowerTools.Run("ipconfig.exe", "/flushdns"));
            SetBusy(false, T("ready"));
            MessageBox.Show(code == 0 ? (language == "tr" ? "DNS önbelleği yenilendi." : "DNS cache refreshed.") : (language == "tr" ? "DNS yenilenemedi." : "DNS could not be refreshed."), "Project Mantis");
        }

        private void SetBusy(bool value, string status)
        {
            busy = value; statusText.Text = status; statusDot.Background = Brush(value ? "#FFD873" : Accent);
        }

        private void RefreshMetrics()
        {
            try
            {
                SystemMetrics m = monitor.Read();
                cpuValue.Text = m.Cpu.ToString("0") + "%";
                ramValue.Text = m.Ram.ToString("0") + "%";
                diskValue.Text = m.DiskFree.ToString("0") + "%";
            }
            catch { }
        }

        private void RestartAsAdmin()
        {
            if (IsAdmin()) { MessageBox.Show(language == "tr" ? "Mantis zaten yönetici olarak çalışıyor." : "Mantis is already running as administrator.", "Project Mantis"); return; }
            try { Process.Start(new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName) { Verb = "runas", UseShellExecute = true }); Close(); } catch { }
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
            if (!File.Exists(path)) File.WriteAllText(path, language == "tr" ? "Henüz işlem kaydı yok." : "No operations recorded yet.");
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }

        private void ApplyBackdrop(object sender, EventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int dark = 1; Native.DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
            int backdrop = 2; Native.DwmSetWindowAttribute(hwnd, 38, ref backdrop, sizeof(int));
            hwndSource = HwndSource.FromHwnd(hwnd); if (hwndSource != null) hwndSource.AddHook(WindowHook);
            Native.RegisterHotKey(hwnd, 0x4D41, 0x0002 | 0x0004, 0x4D);
        }

        private IntPtr WindowHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
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

        private static string FormatBytes(long value)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" }; double n = value; int i = 0;
            while (n >= 1024 && i < units.Length - 1) { n /= 1024; i++; }
            return n.ToString(i == 0 ? "0" : "0.##") + " " + units[i];
        }
    }

    internal sealed class CleanupResult { public long Bytes; public int Files; public int Skipped; public bool AdobeRunning; }

    internal static class Cleaner
    {
        private static readonly string[] AdobeProcesses = { "AfterFX", "Adobe Premiere Pro", "MediaEncoder", "dynamiclinkmanager", "aerender" };

        public static CleanupResult Run(bool scanOnly)
        {
            CleanupResult result = new CleanupResult();
            result.AdobeRunning = AdobeProcesses.Any(n => Process.GetProcessesByName(n).Length > 0);
            foreach (string root in GetTargets(result.AdobeRunning).Distinct(StringComparer.OrdinalIgnoreCase))
                ProcessDirectory(root, scanOnly, result);
            AppendLog((scanOnly ? "SCAN" : "CLEAN") + " | " + DateTime.Now.ToString("s") + " | bytes=" + result.Bytes + " files=" + result.Files + " skipped=" + result.Skipped + " adobeRunning=" + result.AdobeRunning);
            return result;
        }

        private static IEnumerable<string> GetTargets(bool adobeRunning)
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string temp = Path.GetTempPath();
            yield return temp;
            yield return Path.Combine(local, "Temp");
            yield return Path.Combine(local, "D3DSCache");
            yield return Path.Combine(local, "CrashDumps");
            yield return Path.Combine(local, "Microsoft", "Windows", "WER", "ReportArchive");
            yield return Path.Combine(local, "Microsoft", "Windows", "WER", "ReportQueue");
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch");
            if (!adobeRunning)
            {
                yield return Path.Combine(local, "Adobe", "Common", "Media Cache");
                yield return Path.Combine(local, "Adobe", "Common", "Media Cache Files");
                yield return Path.Combine(roaming, "Adobe", "Common", "Media Cache");
                yield return Path.Combine(roaming, "Adobe", "Common", "Media Cache Files");
                yield return Path.Combine(roaming, "Adobe", "Common", "Peak Files");
                yield return Path.Combine(local, "Temp", "Adobe");
            }
        }

        private static void ProcessDirectory(string root, bool scanOnly, CleanupResult result)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return;
            string full;
            try { full = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar); } catch { return; }
            if (full.Length < 8 || full == Path.GetPathRoot(full)) return;
            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(full, "*", SearchOption.AllDirectories).ToArray(); }
            catch { files = SafeFiles(full); }
            foreach (string file in files)
            {
                try
                {
                    FileInfo info = new FileInfo(file); long size = info.Exists ? info.Length : 0;
                    result.Files++; result.Bytes += size;
                    if (!scanOnly) { info.IsReadOnly = false; info.Delete(); }
                }
                catch { result.Skipped++; }
            }
            if (!scanOnly)
            {
                try
                {
                    foreach (string dir in Directory.EnumerateDirectories(full, "*", SearchOption.AllDirectories).OrderByDescending(x => x.Length))
                        try { Directory.Delete(dir, false); } catch { }
                }
                catch { }
            }
        }

        private static IEnumerable<string> SafeFiles(string root)
        {
            List<string> found = new List<string>(); Queue<string> queue = new Queue<string>(); queue.Enqueue(root);
            while (queue.Count > 0)
            {
                string dir = queue.Dequeue();
                try { found.AddRange(Directory.GetFiles(dir)); } catch { }
                try { foreach (string sub in Directory.GetDirectories(dir)) queue.Enqueue(sub); } catch { }
            }
            return found;
        }

        private static void AppendLog(string line)
        {
            try { Directory.CreateDirectory(Path.GetDirectoryName(Settings.LogPath)); File.AppendAllText(Settings.LogPath, line + Environment.NewLine); } catch { }
        }
    }

    internal static class MemoryOptimizer
    {
        public static int TrimProcesses()
        {
            int count = 0;
            foreach (Process p in Process.GetProcesses())
            {
                try
                {
                    if (p.Id == Process.GetCurrentProcess().Id || p.HasExited) continue;
                    if (Native.EmptyWorkingSet(p.Handle)) count++;
                }
                catch { }
                finally { p.Dispose(); }
            }
            GC.Collect(); GC.WaitForPendingFinalizers(); return count;
        }
    }

    internal struct SystemMetrics { public double Cpu; public double Ram; public double DiskFree; }

    internal sealed class SystemMonitor
    {
        private ulong prevIdle, prevKernel, prevUser;
        public SystemMetrics Read()
        {
            ulong idle, kernel, user; Native.GetSystemTimes(out idle, out kernel, out user);
            ulong idleDelta = idle - prevIdle, total = (kernel - prevKernel) + (user - prevUser);
            double cpu = prevKernel == 0 || total == 0 ? 0 : 100.0 * (total - idleDelta) / total;
            prevIdle = idle; prevKernel = kernel; prevUser = user;
            Native.MemoryStatus ms = new Native.MemoryStatus(); ms.dwLength = (uint)Marshal.SizeOf(ms); Native.GlobalMemoryStatusEx(ref ms);
            DriveInfo drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));
            return new SystemMetrics { Cpu = Math.Max(0, Math.Min(100, cpu)), Ram = ms.dwMemoryLoad, DiskFree = drive.TotalSize == 0 ? 0 : 100.0 * drive.AvailableFreeSpace / drive.TotalSize };
        }
        public static long UsedMemoryBytes() { Native.MemoryStatus ms = new Native.MemoryStatus(); ms.dwLength = (uint)Marshal.SizeOf(ms); Native.GlobalMemoryStatusEx(ref ms); return (long)(ms.ullTotalPhys - ms.ullAvailPhys); }
    }

    internal static class PowerTools
    {
        public static string GetActiveScheme()
        {
            try { string output = Capture("powercfg.exe", "/getactivescheme"); int a = output.IndexOf(':'); int b = output.IndexOf('(', a + 1); return a >= 0 ? output.Substring(a + 1, (b > a ? b : output.Length) - a - 1).Trim() : ""; } catch { return ""; }
        }
        public static bool SetHighPerformance() { return Run("powercfg.exe", "/setactive SCHEME_MIN") == 0; }
        public static bool SetScheme(string guid) { return !string.IsNullOrWhiteSpace(guid) && Run("powercfg.exe", "/setactive " + guid) == 0; }
        public static void KeepAwake(bool on) { Native.SetThreadExecutionState(on ? 0x80000003u : 0x80000000u); }
        public static int Run(string file, string args)
        {
            try { using (Process p = Process.Start(new ProcessStartInfo(file, args) { UseShellExecute = false, CreateNoWindow = true })) { p.WaitForExit(15000); return p.HasExited ? p.ExitCode : -1; } } catch { return -1; }
        }
        private static string Capture(string file, string args)
        {
            using (Process p = Process.Start(new ProcessStartInfo(file, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true })) { string s = p.StandardOutput.ReadToEnd(); p.WaitForExit(); return s; }
        }
    }

    internal static class StartupManager
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public static bool IsEnabled() { try { using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey)) return k != null && k.GetValue("ProjectMantis") != null; } catch { return false; } }
        public static void SetEnabled(bool enabled)
        {
            try { using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey)) { if (enabled) k.SetValue("ProjectMantis", "\"" + Process.GetCurrentProcess().MainModule.FileName + "\""); else k.DeleteValue("ProjectMantis", false); } } catch { }
        }
    }

    internal sealed class Settings
    {
        public string Language = "tr";
        public string CloseBehavior = "ask";
        public bool TrayNoticeShown;
        public bool MinimizeOnMinimize;
        public static string DataDir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProjectMantis"); } }
        public static string LogPath { get { return Path.Combine(DataDir, "mantis.log"); } }
        private static string FilePath { get { return Path.Combine(DataDir, "settings.ini"); } }
        public static Settings Load()
        {
            Settings s = new Settings();
            try
            {
                if (!File.Exists(FilePath)) return s;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    string[] pair = line.Split(new[] { '=' }, 2); if (pair.Length != 2) { if (line == "tr" || line == "en") s.Language = line; continue; }
                    if (pair[0] == "language") s.Language = pair[1] == "en" ? "en" : "tr";
                    else if (pair[0] == "close") s.CloseBehavior = pair[1] == "tray" || pair[1] == "exit" ? pair[1] : "ask";
                    else if (pair[0] == "trayNotice") s.TrayNoticeShown = pair[1] == "1";
                    else if (pair[0] == "minimizeToTray") s.MinimizeOnMinimize = pair[1] == "1";
                }
            }
            catch { }
            return s;
        }
        public void Save() { try { Directory.CreateDirectory(DataDir); File.WriteAllLines(FilePath, new[] { "language=" + Language, "close=" + CloseBehavior, "trayNotice=" + (TrayNoticeShown ? "1" : "0"), "minimizeToTray=" + (MinimizeOnMinimize ? "1" : "0") }); } catch { } }
    }

    // Uses the public Windows Core Audio endpoint-volume API. No driver, service,
    // audio capture, process injection, or third-party component is involved.
    internal static class AudioBalance
    {
        private static readonly Guid EndpointVolumeId = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");

        public static float[] GetChannelLevels()
        {
            object deviceObject = null; object endpointObject = null; MMDeviceEnumerator enumerator = null;
            try
            {
                enumerator = (MMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                IMMDevice device; Check(enumerator.GetDefaultAudioEndpoint(0, 1, out device)); deviceObject = device;
                Guid iid = EndpointVolumeId; Check(device.Activate(ref iid, 23, IntPtr.Zero, out endpointObject));
                IAudioEndpointVolume endpoint = (IAudioEndpointVolume)endpointObject;
                uint count; Check(endpoint.GetChannelCount(out count));
                float[] result = new float[count];
                for (uint i = 0; i < count; i++) Check(endpoint.GetChannelVolumeLevelScalar(i, out result[i]));
                return result;
            }
            finally
            {
                if (endpointObject != null && Marshal.IsComObject(endpointObject)) Marshal.FinalReleaseComObject(endpointObject);
                if (deviceObject != null && Marshal.IsComObject(deviceObject)) Marshal.FinalReleaseComObject(deviceObject);
                if (enumerator != null && Marshal.IsComObject(enumerator)) Marshal.FinalReleaseComObject(enumerator);
            }
        }

        public static void SetChannelLevels(float left, float right)
        {
            left = Math.Max(0f, Math.Min(1f, left)); right = Math.Max(0f, Math.Min(1f, right));
            object deviceObject = null; object endpointObject = null; MMDeviceEnumerator enumerator = null;
            try
            {
                enumerator = (MMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                IMMDevice device; Check(enumerator.GetDefaultAudioEndpoint(0, 1, out device)); deviceObject = device;
                Guid iid = EndpointVolumeId; Check(device.Activate(ref iid, 23, IntPtr.Zero, out endpointObject));
                IAudioEndpointVolume endpoint = (IAudioEndpointVolume)endpointObject;
                uint count; Check(endpoint.GetChannelCount(out count));
                if (count < 2) throw new InvalidOperationException("The active output does not expose stereo channels.");
                Guid context = Guid.Empty;
                Check(endpoint.SetChannelVolumeLevelScalar(0, left, ref context));
                Check(endpoint.SetChannelVolumeLevelScalar(1, right, ref context));
            }
            finally
            {
                if (endpointObject != null && Marshal.IsComObject(endpointObject)) Marshal.FinalReleaseComObject(endpointObject);
                if (deviceObject != null && Marshal.IsComObject(deviceObject)) Marshal.FinalReleaseComObject(deviceObject);
                if (enumerator != null && Marshal.IsComObject(enumerator)) Marshal.FinalReleaseComObject(enumerator);
            }
        }

        private static void Check(int result) { if (result < 0) Marshal.ThrowExceptionForHR(result); }
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    internal class MMDeviceEnumeratorComObject { }

    internal enum EDataFlow { Render = 0, Capture = 1, All = 2 }
    internal enum ERole { Console = 0, Multimedia = 1, Communications = 2 }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface MMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, uint stateMask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr client);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, uint classContext, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
        [PreserveSig] int OpenPropertyStore(uint access, out IntPtr properties);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out uint state);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int GetChannelCount(out uint channelCount);
        [PreserveSig] int SetMasterVolumeLevel(float levelDb, ref Guid eventContext);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);
        [PreserveSig] int GetMasterVolumeLevel(out float levelDb);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint channel, float levelDb, ref Guid eventContext);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid eventContext);
        [PreserveSig] int GetChannelVolumeLevel(uint channel, out float levelDb);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid eventContext);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
        [PreserveSig] int GetVolumeStepInfo(out uint step, out uint stepCount);
        [PreserveSig] int VolumeStepUp(ref Guid eventContext);
        [PreserveSig] int VolumeStepDown(ref Guid eventContext);
        [PreserveSig] int QueryHardwareSupport(out uint hardwareSupportMask);
        [PreserveSig] int GetVolumeRange(out float minDb, out float maxDb, out float incrementDb);
    }

    internal static class Native
    {
        [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        [DllImport("psapi.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool EmptyWorkingSet(IntPtr process);
        [DllImport("kernel32.dll")] public static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);
        [DllImport("kernel32.dll")] public static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
        [DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(uint flags);
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int count);
        [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out WindowRect rect);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")] public static extern IntPtr GetWindowLongPtr64(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "GetWindowLong")] public static extern IntPtr GetWindowLongPtr32(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")] public static extern IntPtr SetWindowLongPtr64(IntPtr hwnd, int index, IntPtr value);
        [DllImport("user32.dll", EntryPoint = "SetWindowLong")] public static extern IntPtr SetWindowLongPtr32(IntPtr hwnd, int index, IntPtr value);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint colorKey, byte alpha, uint flags);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
        public static IntPtr GetWindowStyle(IntPtr hwnd) { return IntPtr.Size == 8 ? GetWindowLongPtr64(hwnd, -20) : GetWindowLongPtr32(hwnd, -20); }
        public static IntPtr SetWindowStyle(IntPtr hwnd, IntPtr value) { return IntPtr.Size == 8 ? SetWindowLongPtr64(hwnd, -20, value) : SetWindowLongPtr32(hwnd, -20, value); }
        [StructLayout(LayoutKind.Sequential)] public struct WindowRect { public int Left; public int Top; public int Right; public int Bottom; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)] public struct MemoryStatus { public uint dwLength; public uint dwMemoryLoad; public ulong ullTotalPhys; public ulong ullAvailPhys; public ulong ullTotalPageFile; public ulong ullAvailPageFile; public ulong ullTotalVirtual; public ulong ullAvailVirtual; public ulong ullAvailExtendedVirtual; }
    }

    internal sealed class WindowTarget
    {
        public IntPtr Handle; public string Title; public string ProcessName; public override string ToString() { return Title + "   ·   " + ProcessName; }
    }

    internal static class OverlayManager
    {
        private static IntPtr active;
        private static IntPtr originalStyle;
        private static bool clickThrough;
        private const long WS_EX_TRANSPARENT = 0x20;
        private const long WS_EX_LAYERED = 0x80000;
        private const uint SWP_NOMOVE = 0x0002, SWP_NOSIZE = 0x0001, SWP_NOACTIVATE = 0x0010, SWP_FRAMECHANGED = 0x0020;
        private static readonly HashSet<string> Browsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "chrome", "msedge", "firefox", "brave", "opera", "opera_gx", "vivaldi" };
        private static readonly HashSet<string> MediaPlayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "vlc", "mpv", "wmplayer", "potplayermini64", "kodi" };

        public static List<WindowTarget> ListWindows(int ownProcess)
        {
            List<WindowTarget> result = new List<WindowTarget>();
            Native.EnumWindows(delegate(IntPtr hwnd, IntPtr p)
            {
                if (!Native.IsWindowVisible(hwnd)) return true;
                int length = Native.GetWindowTextLength(hwnd); if (length < 2) return true;
                uint pid; Native.GetWindowThreadProcessId(hwnd, out pid); if (pid == ownProcess) return true;
                string processName; try { processName = Process.GetProcessById((int)pid).ProcessName; } catch { return true; }
                if (!Browsers.Contains(processName) && !MediaPlayers.Contains(processName)) return true;
                if (Browsers.Contains(processName))
                {
                    Native.WindowRect rect; if (!Native.GetWindowRect(hwnd, out rect)) return true;
                    int width = Math.Max(0, rect.Right - rect.Left), height = Math.Max(0, rect.Bottom - rect.Top);
                    StringBuilder titleProbe = new StringBuilder(length + 1); Native.GetWindowText(hwnd, titleProbe, titleProbe.Capacity); string lowerTitle = titleProbe.ToString().ToLowerInvariant();
                    bool namedPip = lowerTitle.Contains("picture in picture") || lowerTitle.Contains("picture-in-picture") || lowerTitle.Contains("resim içinde resim") || lowerTitle.Contains("pip");
                    bool compactVideoShape = width >= 220 && height >= 120 && width <= 960 && height <= 720 && width > height;
                    if (!namedPip && !compactVideoShape) return true;
                }
                StringBuilder text = new StringBuilder(length + 1); Native.GetWindowText(hwnd, text, text.Capacity);
                string title = text.ToString().Trim(); if (title.Length > 0) result.Add(new WindowTarget { Handle = hwnd, Title = title, ProcessName = processName }); return true;
            }, IntPtr.Zero);
            return result.OrderBy(x => x.Title).ToList();
        }

        public static bool Apply(IntPtr hwnd, byte opacity)
        {
            if (hwnd == IntPtr.Zero) return false; Restore(); active = hwnd; originalStyle = Native.GetWindowStyle(hwnd);
            long style = originalStyle.ToInt64() | WS_EX_TRANSPARENT | WS_EX_LAYERED;
            Native.SetWindowStyle(hwnd, new IntPtr(style)); clickThrough = true;
            Native.SetLayeredWindowAttributes(hwnd, 0, opacity, 0x00000002);
            return Native.SetWindowPos(hwnd, new IntPtr(-1), 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED);
        }

        public static void Restore()
        {
            if (active == IntPtr.Zero) return;
            Native.SetWindowStyle(active, originalStyle);
            Native.SetWindowPos(active, new IntPtr(-2), 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED);
            active = IntPtr.Zero; originalStyle = IntPtr.Zero; clickThrough = false;
        }

        public static bool ToggleInputLock()
        {
            if (active == IntPtr.Zero) return false;
            long style = Native.GetWindowStyle(active).ToInt64();
            if (clickThrough) style &= ~WS_EX_TRANSPARENT; else style |= WS_EX_TRANSPARENT;
            Native.SetWindowStyle(active, new IntPtr(style));
            Native.SetWindowPos(active, new IntPtr(-1), 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED);
            clickThrough = !clickThrough; return clickThrough;
        }
    }
}
