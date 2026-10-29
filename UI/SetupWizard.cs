// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;

namespace ProjectMantis
{
    // Installer wizard in the Mantis theme: 1 license → 2 options → 3 install/finish. Nothing is written
    // before the user presses Install; Cancel at any earlier step leaves the computer untouched.
    internal sealed class SetupWizard : Window
    {
        private readonly bool tr;
        private readonly string existing = Installer.InstalledFolder();
        private int step;
        private bool agreed;
        private string installedExe;
        private readonly StackPanel rail = new StackPanel();
        private readonly ContentControl page = new ContentControl();
        private readonly Button back, next, cancel;
        private readonly TextBox folder = new TextBox();
        private readonly CheckBox desktop, menu, login, launch;

        public SetupWizard(bool turkish)
        {
            tr = turkish;
            Title = tr ? "Project Mantis Kurulumu" : "Project Mantis Setup";
            Width = 780; Height = 560; MinWidth = 680; MinHeight = 500; ResizeMode = ResizeMode.CanMinimize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Theme.Brush(Theme.BgBase); Foreground = Theme.Brush(Theme.TextPrimary); FontFamily = Theme.TextFont;
            BitmapSource logo = Logo(); if (logo != null) Icon = logo;

            desktop = Check(tr ? "Masaüstü kısayolu oluştur" : "Create a desktop shortcut", true);
            menu = Check(tr ? "Başlat menüsüne ekle" : "Add to the Start menu", true);
            login = Check(tr ? "Windows açılışında başlat (sistem tepsisinde)" : "Start with Windows (in the system tray)", false);
            launch = Check(tr ? "Kurulumdan sonra Project Mantis'i başlat" : "Launch Project Mantis when setup finishes", true);
            folder.Text = existing ?? Installer.DefaultFolder;

            Grid shell = new Grid { Background = Theme.Brush(Theme.BgBase) };
            shell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
            shell.ColumnDefinitions.Add(new ColumnDefinition());
            shell.RowDefinitions.Add(new RowDefinition());
            shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Border side = new Border { Background = Theme.Brush(Theme.BgElevated), BorderBrush = Theme.Brush(Theme.Stroke), BorderThickness = new Thickness(0, 0, 1, 0), Padding = new Thickness(Theme.S5) };
            Grid.SetRowSpan(side, 2);
            StackPanel sideStack = new StackPanel();
            if (logo != null) sideStack.Children.Add(new Image { Source = logo, Width = 44, Height = 44, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, Theme.S3) });
            sideStack.Children.Add(new TextBlock { Text = "PROJECT MANTIS", FontFamily = Theme.DisplayFont, FontSize = 17, FontWeight = FontWeights.SemiBold });
            sideStack.Children.Add(new TextBlock { Text = "v" + AppInfo.Version + "  ·  " + (tr ? "KURULUM" : "SETUP"), FontFamily = Theme.MonoFont, FontSize = Theme.Caption, Foreground = Theme.Brush(Theme.AccentPrimary), Margin = new Thickness(0, 2, 0, Theme.S6) });
            sideStack.Children.Add(rail);
            side.Child = sideStack;
            shell.Children.Add(side);

            page.Margin = new Thickness(Theme.S6, Theme.S5, Theme.S6, 0);
            Grid.SetColumn(page, 1); shell.Children.Add(page);

            StackPanel bar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(Theme.S6, Theme.S4, Theme.S6, Theme.S5) };
            back = MakeButton(tr ? "Geri" : "Back", false); cancel = MakeButton(tr ? "İptal" : "Cancel", false); next = MakeButton("", true);
            back.Margin = new Thickness(0, 0, Theme.S2, 0); next.Margin = new Thickness(Theme.S2, 0, 0, 0);
            bar.Children.Add(cancel); bar.Children.Add(back); bar.Children.Add(next);
            Grid.SetColumn(bar, 1); Grid.SetRow(bar, 1); shell.Children.Add(bar);

            back.Click += delegate { if (step > 0) GoTo(step - 1); };
            cancel.Click += delegate { Close(); };
            next.Click += async delegate { await Next(); };
            Content = shell;
            GoTo(0);
        }

        // Developer aid (--setup-snapshot=file.png --setup-step=N): renders a step off-screen.
        internal static void Snapshot(string file, bool tr, int step)
        {
            SetupWizard w = new SetupWizard(tr);
            if (step >= 1) { w.agreed = true; w.GoTo(Math.Min(step, 2)); }
            if (step >= 2) w.ShowDone(null);
            w.WindowStartupLocation = WindowStartupLocation.Manual; w.Left = -20000; w.Top = -20000; w.ShowActivated = false; w.ShowInTaskbar = false;
            w.ContentRendered += delegate { RenderTo(w, file); w.Close(); };
            w.ShowDialog();
        }

        internal static void RenderTo(Window w, string file)
        {
            try
            {
                FrameworkElement root = (FrameworkElement)w.Content;
                double scale = VisualTreeHelper.GetDpi(w).DpiScaleX;
                RenderTargetBitmap bmp = new RenderTargetBitmap((int)(root.ActualWidth * scale), (int)(root.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
                bmp.Render(root);
                PngBitmapEncoder png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bmp));
                using (FileStream fs = File.Create(Path.GetFullPath(file))) png.Save(fs);
            }
            catch (Exception ex) { App.Log("SNAPSHOT " + ex.Message); }
        }

        private void GoTo(int index)
        {
            step = index;
            rail.Children.Clear();
            string[] names = tr ? new[] { "Lisans sözleşmesi", "Seçenekler", "Kurulum" } : new[] { "License agreement", "Options", "Install" };
            for (int i = 0; i < names.Length; i++)
            {
                bool current = i == step, done = i < step;
                StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, Theme.S3) };
                row.Children.Add(new Border
                {
                    Width = 22, Height = 22, CornerRadius = new CornerRadius(11), BorderThickness = new Thickness(1.5),
                    BorderBrush = Theme.Brush(current || done ? Theme.AccentPrimary : Theme.Stroke), Background = Theme.Brush(done ? Theme.AccentPrimary : "#00000000"),
                    Child = new TextBlock { Text = done ? "✓" : (i + 1).ToString(), FontSize = 11, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = Theme.Brush(done ? Theme.OnAccent : current ? Theme.AccentPrimary : Theme.TextFaint) }
                });
                row.Children.Add(new TextBlock { Text = names[i], Margin = new Thickness(Theme.S2 + 2, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, FontSize = Theme.Body, FontWeight = current ? FontWeights.SemiBold : FontWeights.Normal, Foreground = Theme.Brush(current ? Theme.TextPrimary : Theme.TextMuted) });
                rail.Children.Add(row);
            }
            back.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
            cancel.Visibility = step < 2 ? Visibility.Visible : Visibility.Collapsed;
            if (step == 0) { page.Content = LicensePage(); next.Content = tr ? "İleri" : "Next"; SetNext(agreed); }
            else if (step == 1) { page.Content = OptionsPage(); next.Content = existing != null ? (tr ? "Güncelle" : "Update") : (tr ? "Kur" : "Install"); SetNext(true); }
        }

        private async Task Next()
        {
            if (step == 0 && agreed) { GoTo(1); return; }
            if (step == 1)
            {
                InstallOptions o = new InstallOptions { Folder = folder.Text, DesktopShortcut = desktop.IsChecked == true, StartMenuShortcut = menu.IsChecked == true, StartAtLogin = login.IsChecked == true };
                GoTo(2);
                page.Content = Heading(tr ? "Kuruluyor…" : "Installing…", tr ? "Dosyalar kopyalanıyor ve kısayollar oluşturuluyor." : "Copying files and creating shortcuts.", new ProgressBar { IsIndeterminate = true, Height = 4, Margin = new Thickness(0, Theme.S5, 0, 0), Foreground = Theme.Brush(Theme.AccentPrimary), Background = Theme.Brush(Theme.Control), BorderThickness = new Thickness(0) });
                SetNext(false); next.Content = tr ? "Bitir" : "Finish";
                string target = null, error = null;
                bool ok = await Task.Run(() => Installer.Install(o, out target, out error));
                if (ok) { installedExe = target; ShowDone(null); }
                else ShowDone(error ?? "?");
                return;
            }
            if (step == 2)
            {
                if (installedExe != null && launch.IsChecked == true) { try { Process.Start(new ProcessStartInfo(installedExe) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(installedExe) }); } catch { } }
                Close();
            }
        }

        private void ShowDone(string error)
        {
            GoTo(2);
            if (error == null)
            {
                StackPanel extra = new StackPanel { Margin = new Thickness(0, Theme.S5, 0, 0) };
                extra.Children.Add(Note(tr ? "Mantis'i Ayarlar → Uygulamalar'dan istediğin zaman kaldırabilirsin. Ayarların ve raporların " + Settings.DataDir + " içinde, yalnızca bu bilgisayarda kalır." : "You can remove Mantis any time from Settings → Apps. Your settings and reports stay on this computer in " + Settings.DataDir + "."));
                launch.Margin = new Thickness(0, Theme.S4, 0, 0); Detach(launch); extra.Children.Add(launch);
                page.Content = Heading(tr ? "Kurulum tamamlandı" : "Setup complete", tr ? "Project Mantis v" + AppInfo.Version + " kuruldu." : "Project Mantis v" + AppInfo.Version + " is installed.", extra);
                next.Content = tr ? "Bitir" : "Finish"; SetNext(true); back.Visibility = Visibility.Collapsed;
            }
            else
            {
                string text = error == "running" ? (tr ? "Project Mantis şu anda açık. Sistem tepsisindeki simgeden tamamen çıkıp tekrar dene." : "Project Mantis is running. Exit it from the tray icon and try again.")
                    : error == "access" ? (tr ? "Bu klasöre yazma izni yok. Varsayılan klasörü kullan ya da başka bir klasör seç." : "No permission to write to this folder. Use the default folder or choose another one.")
                    : error == "windows" ? (tr ? "Windows klasörüne kurulum yapılamaz." : "Mantis cannot be installed inside the Windows folder.")
                    : error == "chars" || error == "path" ? (tr ? "Klasör yolu geçersiz. % ^ ! & gibi özel karakterler içermeyen yerel bir klasör seç." : "The folder path is not valid. Choose a local folder without special characters such as % ^ ! &.")
                    : (tr ? "Kurulum tamamlanamadı: " : "Setup could not finish: ") + error;
                page.Content = Heading(tr ? "Kurulum yapılamadı" : "Setup failed", text, null);
                back.Visibility = Visibility.Visible; back.Content = tr ? "Geri" : "Back";
                cancel.Visibility = Visibility.Visible; SetNext(false);
            }
        }

        private FrameworkElement LicensePage()
        {
            Grid g = new Grid();
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition());
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.Children.Add(Heading(existing != null ? (tr ? "Project Mantis güncellemesi" : "Update Project Mantis") : (tr ? "Project Mantis'e hoş geldin" : "Welcome to Project Mantis"),
                tr ? "Devam etmeden önce lisans sözleşmesini okuyup kabul et. Yönetici izni gerekmez." : "Read and accept the license agreement to continue. No administrator rights needed.", null));
            TextBox body = new TextBox
            {
                Text = tr ? Terms.TextTr : Terms.TextEn, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, FontSize = 12,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Theme.Brush(Theme.BgElevated), Foreground = Theme.Brush(Theme.TextPrimary),
                BorderBrush = Theme.Brush(Theme.Stroke), Padding = new Thickness(Theme.S3), Margin = new Thickness(0, Theme.S4, 0, 0), SelectionBrush = Theme.Brush(Theme.AccentPrimary)
            };
            Grid.SetRow(body, 1); g.Children.Add(body);
            CheckBox agree = Check(tr ? "Sözleşmeyi okudum ve kabul ediyorum. Yazılımı kendi riskimle kullandığımı ve bilgisayarıma, verilerime veya donanımıma gelebilecek zararlardan geliştiricinin sorumlu olmadığını anlıyorum." : "I have read and accept the agreement. I understand that I use the software at my own risk and that the developer is not responsible for any damage to my computer, data or hardware.", agreed);
            agree.Margin = new Thickness(0, Theme.S3, 0, 0);
            agree.Checked += delegate { agreed = true; SetNext(true); };
            agree.Unchecked += delegate { agreed = false; SetNext(false); };
            Grid.SetRow(agree, 2); g.Children.Add(agree);
            return g;
        }

        private FrameworkElement OptionsPage()
        {
            StackPanel s = new StackPanel();
            s.Children.Add(Heading(tr ? "Kurulum seçenekleri" : "Install options", existing != null
                ? (tr ? "Mevcut kurulum yeni sürümle değiştirilecek; ayarların ve raporların korunur." : "The existing install will be replaced with this version; your settings and reports are kept.")
                : (tr ? "Varsayılanlar çoğu kişi için uygundur." : "The defaults suit most people."), null));
            s.Children.Add(new TextBlock { Text = tr ? "KURULUM KLASÖRÜ" : "INSTALL FOLDER", FontFamily = Theme.MonoFont, FontSize = Theme.Caption, Foreground = Theme.Brush(Theme.TextFaint), Margin = new Thickness(0, Theme.S5, 0, Theme.S1) });
            Grid row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            folder.Height = 36; folder.VerticalContentAlignment = VerticalAlignment.Center; folder.Padding = new Thickness(Theme.S2, 0, Theme.S2, 0);
            folder.Background = Theme.Brush(Theme.BgElevated); folder.Foreground = Theme.Brush(Theme.TextPrimary); folder.BorderBrush = Theme.Brush(Theme.Stroke); folder.CaretBrush = Theme.Brush(Theme.AccentPrimary);
            folder.IsEnabled = existing == null;
            Detach(folder); row.Children.Add(folder);
            Button browse = MakeButton(tr ? "Gözat…" : "Browse…", false); browse.Height = 36; browse.MinWidth = 96; browse.Margin = new Thickness(Theme.S2, 0, 0, 0); browse.IsEnabled = existing == null;
            browse.Click += delegate
            {
                using (Forms.FolderBrowserDialog d = new Forms.FolderBrowserDialog { Description = tr ? "Project Mantis'in kurulacağı klasörü seç" : "Choose where to install Project Mantis", ShowNewFolderButton = true })
                {
                    if (d.ShowDialog() == Forms.DialogResult.OK) { string err; string n = Installer.NormalizeFolder(d.SelectedPath, out err); folder.Text = n ?? d.SelectedPath; }
                }
            };
            Grid.SetColumn(browse, 1); row.Children.Add(browse);
            s.Children.Add(row);
            s.Children.Add(Note(tr ? "Kişisel kurulum: yalnızca senin kullanıcı hesabın için, yönetici izni olmadan." : "Per-user install: for your account only, no administrator rights."));
            s.Children.Add(new TextBlock { Text = tr ? "KISAYOLLAR" : "SHORTCUTS", FontFamily = Theme.MonoFont, FontSize = Theme.Caption, Foreground = Theme.Brush(Theme.TextFaint), Margin = new Thickness(0, Theme.S5, 0, Theme.S1) });
            foreach (CheckBox c in new[] { desktop, menu, login }) { Detach(c); c.Margin = new Thickness(0, Theme.S1, 0, Theme.S1); s.Children.Add(c); }
            return s;
        }

        private StackPanel Heading(string title, string subtitle, UIElement extra)
        {
            StackPanel s = new StackPanel();
            s.Children.Add(new TextBlock { Text = title, FontFamily = Theme.DisplayFont, FontSize = 24, FontWeight = FontWeights.SemiBold });
            s.Children.Add(new TextBlock { Text = subtitle, FontSize = Theme.Body, TextWrapping = TextWrapping.Wrap, Foreground = Theme.Brush(Theme.TextMuted), Margin = new Thickness(0, Theme.S1, 0, 0) });
            if (extra != null) s.Children.Add(extra);
            return s;
        }

        private static TextBlock Note(string text) { return new TextBlock { Text = text, FontSize = Theme.Small, TextWrapping = TextWrapping.Wrap, Foreground = Theme.Brush(Theme.TextFaint), Margin = new Thickness(0, Theme.S2, 0, 0) }; }

        private static void Detach(FrameworkElement e) { Panel p = e.Parent as Panel; if (p != null) p.Children.Remove(e); }

        private void SetNext(bool enabled) { next.IsEnabled = enabled; next.Opacity = enabled ? 1 : 0.45; }

        internal static CheckBox Check(string text, bool on)
        {
            return new CheckBox { IsChecked = on, Foreground = Theme.Brush(Theme.TextPrimary), Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = Theme.Small + 0.5, Foreground = Theme.Brush(Theme.TextPrimary) } };
        }

        internal static Button MakeButton(string text, bool primary)
        {
            return new Button
            {
                Content = text, Height = 38, MinWidth = 120, Padding = new Thickness(Theme.S4, 0, Theme.S4, 0), FontWeight = FontWeights.SemiBold, Cursor = System.Windows.Input.Cursors.Hand,
                Background = Theme.Brush(primary ? Theme.AccentPrimary : Theme.Control), Foreground = Theme.Brush(primary ? Theme.OnAccent : Theme.TextPrimary), BorderBrush = Theme.Brush(primary ? Theme.AccentPrimary : Theme.Stroke)
            };
        }

        internal static BitmapSource Logo()
        {
            try
            {
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("ProjectMantis.logo.png"))
                {
                    if (s == null) return null;
                    BitmapImage image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = s; image.EndInit(); image.Freeze(); return image;
                }
            }
            catch { return null; }
        }
    }

    // Uninstaller started from Settings → Apps (--uninstall).
    internal static class UninstallUi
    {
        public static void Run(bool tr)
        {
            Window w = new Window
            {
                Title = tr ? "Project Mantis'i kaldır" : "Uninstall Project Mantis", Width = 520, Height = 330, ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterScreen, Background = Theme.Brush(Theme.BgBase), Foreground = Theme.Brush(Theme.TextPrimary), FontFamily = Theme.TextFont
            };
            BitmapSource logo = SetupWizard.Logo(); if (logo != null) w.Icon = logo;
            StackPanel s = new StackPanel { Margin = new Thickness(Theme.S6) };
            s.Children.Add(new TextBlock { Foreground = Theme.Brush(Theme.TextPrimary), Text = tr ? "Project Mantis kaldırılsın mı?" : "Uninstall Project Mantis?", FontFamily = Theme.DisplayFont, FontSize = 22, FontWeight = FontWeights.SemiBold });
            s.Children.Add(new TextBlock { Text = tr ? "Uygulama, kısayollar ve Windows açılış kaydı silinir. Mantis'in yaptığı ayar değişiklikleri (servisler, tweak'ler) olduğu gibi kalır; kaldırmadan önce Mantis içinden geri alabilirsin." : "The app, its shortcuts and its login entry are removed. Settings Mantis changed (services, tweaks) stay as they are; you can revert them inside Mantis before uninstalling.", TextWrapping = TextWrapping.Wrap, FontSize = Theme.Body, Foreground = Theme.Brush(Theme.TextMuted), Margin = new Thickness(0, Theme.S2, 0, 0) });
            CheckBox data = SetupWizard.Check(tr ? "Ayarlarımı, işlem geçmişimi ve Check-Up raporlarımı da sil" : "Also delete my settings, activity log and Check-Up reports", false);
            data.Margin = new Thickness(0, Theme.S5, 0, 0); s.Children.Add(data);
            StackPanel bar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, Theme.S6, 0, 0) };
            Button keep = SetupWizard.MakeButton(tr ? "Vazgeç" : "Cancel", false);
            Button remove = SetupWizard.MakeButton(tr ? "Kaldır" : "Uninstall", true); remove.Margin = new Thickness(Theme.S2, 0, 0, 0);
            keep.Click += delegate { w.Close(); };
            remove.Click += delegate
            {
                string error;
                bool ok = Installer.Uninstall(data.IsChecked == true, out error);
                MessageBox.Show(w, ok ? (tr ? "Project Mantis kaldırıldı." : "Project Mantis has been uninstalled.") : (tr ? "Kaldırma tamamlanamadı: " : "Uninstall could not finish: ") + error, "Project Mantis", MessageBoxButton.OK, ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
                w.Close();
            };
            bar.Children.Add(keep); bar.Children.Add(remove); s.Children.Add(bar);
            w.Content = s;
            w.ShowDialog();
        }
    }
}
