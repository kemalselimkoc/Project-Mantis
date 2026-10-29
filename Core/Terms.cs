// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ProjectMantis
{
    // License agreement shown before first use, like any installer (and again whenever Version changes).
    // The user must tick "I have read and accept" before Accept enables; declining or closing exits Mantis.
    internal static class Terms
    {
        public const string Version = "2";

        public static bool EnsureAccepted()
        {
            Settings s = Settings.Load();
            if (s.TermsAccepted == Version) return true;
            bool tr = (App.LanguageOverride ?? s.Language) != "en";
            // Shown before Application.Run: keep WPF from treating this window's close as the app's last window.
            ShutdownMode mode = Application.Current.ShutdownMode;
            Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            bool? ok = Build(tr).ShowDialog();
            Application.Current.ShutdownMode = mode;
            if (ok != true) return false;
            s.TermsAccepted = Version;
            s.Save();
            return true;
        }

        // Developer aid (--terms-snapshot=file.png): renders the agreement off-screen for visual checks.
        internal static void Snapshot(string file, bool tr)
        {
            Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Window w = Build(tr);
            w.WindowStartupLocation = WindowStartupLocation.Manual; w.Left = -20000; w.Top = -20000; w.ShowActivated = false; w.ShowInTaskbar = false;
            w.ContentRendered += delegate
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
                w.Close();
            };
            w.ShowDialog();
        }

        private static Window Build(bool tr)
        {
            Window w = new Window
            {
                Title = tr ? "Project Mantis — Lisans Sözleşmesi" : "Project Mantis — License Agreement",
                Width = 720, Height = 640, MinWidth = 560, MinHeight = 480, WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = Theme.Brush(Theme.BgBase), Foreground = Theme.Brush(Theme.TextPrimary), FontFamily = Theme.TextFont, ResizeMode = ResizeMode.CanResize
            };
            Grid root = new Grid { Margin = new Thickness(Theme.S5), Background = Theme.Brush(Theme.BgBase) };
            for (int i = 0; i < 5; i++) root.RowDefinitions.Add(new RowDefinition { Height = i == 2 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });

            TextBlock eyebrow = new TextBlock { Text = tr ? "KURULUM  ·  ADIM 1 / 1" : "SETUP  ·  STEP 1 OF 1", FontFamily = Theme.MonoFont, FontSize = Theme.Caption, Foreground = Theme.Brush(Theme.AccentPrimary) };
            root.Children.Add(eyebrow);
            StackPanel head = new StackPanel { Margin = new Thickness(0, Theme.S1, 0, Theme.S3) };
            head.Children.Add(new TextBlock { Text = tr ? "Lisans Sözleşmesi ve Sorumluluk Reddi" : "License Agreement and Disclaimer", FontFamily = Theme.DisplayFont, FontSize = 24, FontWeight = FontWeights.SemiBold });
            head.Children.Add(new TextBlock { Text = tr ? "Devam etmek için lütfen sözleşmeyi okuyup kabul edin." : "Please read and accept the agreement to continue.", FontSize = Theme.Body, Foreground = Theme.Brush(Theme.TextMuted), Margin = new Thickness(0, Theme.S1, 0, 0) });
            Grid.SetRow(head, 1); root.Children.Add(head);

            TextBox body = new TextBox
            {
                Text = tr ? TextTr : TextEn, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, FontSize = 12.5,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Theme.Brush(Theme.BgElevated), Foreground = Theme.Brush(Theme.TextPrimary),
                BorderBrush = Theme.Brush(Theme.Stroke), Padding = new Thickness(Theme.S3), SelectionBrush = Theme.Brush(Theme.AccentPrimary)
            };
            Grid.SetRow(body, 2); root.Children.Add(body);

            CheckBox agree = new CheckBox
            {
                Content = new TextBlock { Text = tr ? "Sözleşmeyi okudum ve kabul ediyorum. Yazılımı kendi riskimle kullandığımı ve bilgisayarıma, verilerime veya donanımıma gelebilecek zararlardan geliştiricinin sorumlu olmadığını anlıyorum." : "I have read and accept the agreement. I understand that I use the software at my own risk and that the developer is not responsible for any damage to my computer, data or hardware.", TextWrapping = TextWrapping.Wrap, Foreground = Theme.Brush(Theme.TextPrimary), FontSize = Theme.Small },
                Margin = new Thickness(0, Theme.S3, 0, 0), Foreground = Theme.Brush(Theme.TextPrimary)
            };
            Grid.SetRow(agree, 3); root.Children.Add(agree);

            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, Theme.S4, 0, 0) };
            Button decline = MakeButton(tr ? "Reddet ve çık" : "Decline and exit", false);
            Button accept = MakeButton(tr ? "Kabul et ve devam et" : "Accept and continue", true);
            accept.IsEnabled = false; accept.Opacity = 0.45; accept.Margin = new Thickness(Theme.S2, 0, 0, 0);
            agree.Checked += delegate { accept.IsEnabled = true; accept.Opacity = 1; };
            agree.Unchecked += delegate { accept.IsEnabled = false; accept.Opacity = 0.45; };
            decline.Click += delegate { w.DialogResult = false; };
            accept.Click += delegate { if (agree.IsChecked == true) w.DialogResult = true; };
            buttons.Children.Add(decline); buttons.Children.Add(accept);
            Grid.SetRow(buttons, 4); root.Children.Add(buttons);
            w.Content = new Border { Background = Theme.Brush(Theme.BgBase), Child = root };
            return w;
        }

        private static Button MakeButton(string text, bool primary)
        {
            Button b = new Button
            {
                Content = text, Height = 38, MinWidth = 150, Padding = new Thickness(Theme.S4, 0, Theme.S4, 0), FontWeight = FontWeights.SemiBold, Cursor = System.Windows.Input.Cursors.Hand,
                Background = Theme.Brush(primary ? Theme.AccentPrimary : Theme.Control), Foreground = Theme.Brush(primary ? Theme.OnAccent : Theme.TextPrimary), BorderBrush = Theme.Brush(primary ? Theme.AccentPrimary : Theme.Stroke)
            };
            return b;
        }

        internal const string TextTr =
            "PROJECT MANTIS SON KULLANICI LİSANS SÖZLEŞMESİ (Sürüm 2)\n" +
            "Telif hakkı (c) 2026 kemalselimkoc\n\n" +
            "Bu yazılımı kurarak, çalıştırarak veya kullanarak aşağıdaki koşulları kabul etmiş olursunuz. Kabul etmiyorsanız \"Reddet ve çık\" seçeneğini kullanın ve yazılımı silin.\n\n" +
            "1. LİSANS\n" +
            "Project Mantis, MIT Lisansı ile dağıtılan ücretsiz ve açık kaynaklı bir yazılımdır. Bağışlar gönüllüdür; hizmet, destek veya garanti satın alınması anlamına gelmez.\n\n" +
            "2. GARANTİ YOKTUR\n" +
            "YAZILIM \"OLDUĞU GİBİ\" VE \"MEVCUT HALİYLE\" SUNULUR. TİCARİ ELVERİŞLİLİK, BELİRLİ BİR AMACA UYGUNLUK, HATASIZLIK VE KESİNTİSİZ ÇALIŞMA DAHİL HİÇBİR AÇIK VEYA ZIMNİ GARANTİ VERİLMEZ.\n\n" +
            "3. SORUMLULUK REDDİ\n" +
            "YASALARIN İZİN VERDİĞİ AZAMİ ÖLÇÜDE, GELİŞTİRİCİ; BİLGİSAYARINIZA, İŞLETİM SİSTEMİNİZE, DONANIMINIZA, YAZILIMLARINIZA, VERİLERİNİZE VEYA AĞINIZA GELEBİLECEK HER TÜRLÜ DOĞRUDAN, DOLAYLI, ARIZİ, ÖZEL VEYA SONUÇSAL ZARARDAN (VERİ KAYBI, SİSTEMİN AÇILMAMASI, ARIZA, İŞ VEYA KAZANÇ KAYBI DAHİL) SORUMLU TUTULAMAZ. BU, GELİŞTİRİCİYE BÖYLE BİR ZARAR OLASILIĞI BİLDİRİLMİŞ OLSA BİLE GEÇERLİDİR. YAZILIMIN KULLANIMINDAN DOĞAN TÜM RİSK SİZE AİTTİR.\n" +
            "Bulunduğunuz ülkenin hukukunun sınırlanmasına izin vermediği sorumluluklar (ör. kast veya ağır ihmal) ile tüketici olarak sahip olduğunuz zorunlu yasal haklar bu sözleşmeden etkilenmez.\n\n" +
            "4. KULLANICININ SORUMLULUĞU\n" +
            "• Yazılımı yalnızca size ait ya da yönetmeye yetkili olduğunuz bilgisayarlarda kullanın.\n" +
            "• Her değişiklik önce size gösterilir ve yalnızca onayınızla uygulanır. Onayladığınız işlemlerin sonuçlarından siz sorumlusunuz.\n" +
            "• Servis, başlangıç, kayıt defteri, hosts, güç veya güvenlik ayarlarını değiştirmeden önce önemli dosyalarınızı yedekleyin ve mümkünse bir sistem geri yükleme noktası oluşturun.\n" +
            "• Yerel ağ taramasını yalnızca size ait ya da taramak için açık izin aldığınız ağlarda kullanın. İzinsiz ağ taraması birçok ülkede yasa dışıdır.\n" +
            "• Ekrandan yazı alırken ve çevirirken başkalarının telif haklarına ve gizliliğine saygı gösterin.\n\n" +
            "5. GÜVENLİK CHECK-UP\n" +
            "Güvenlik Check-Up'ı bir antivirüs veya güvenlik garantisi değildir; dosya silmez veya karantinaya almaz, bulguları raporlar ve Microsoft Defender'a yönlendirir. Temiz bir sonuç, sistemin tehditlerden arındığı anlamına gelmez.\n\n" +
            "6. ÜÇÜNCÜ TARAF YAZILIM VE HİZMETLER\n" +
            "Uygulama güncellemeleri Windows'un winget aracıyla yapılır; bu şekilde kurulan yazılımlar kendi yayıncılarının lisansına tabidir. Google Translate ve api.ipify.org yalnızca siz tıkladığınızda, kendi koşullarıyla kullanılır. Bu hizmetlerin içeriğinden ve çalışmasından geliştirici sorumlu değildir.\n\n" +
            "7. GİZLİLİK\n" +
            "Mantis telemetri toplamaz ve geliştiriciye hiçbir kişisel veri göndermez. Ayrıntılar: PRIVACY.md.\n\n" +
            "8. TİCARİ MARKALAR\n" +
            "Microsoft, Windows, Microsoft Defender, BitLocker, Xbox ve Bing, Microsoft şirketler grubunun; Google ve Google Translate, Google LLC'nin ticari markalarıdır. Project Mantis bağımsız bir projedir ve bu şirketlerle bağlantılı değildir.\n\n" +
            "9. SONA ERME\n" +
            "Bu sözleşmeye uymazsanız yazılımı kullanma hakkınız sona erer. Yazılımı istediğiniz zaman silerek sözleşmeyi sonlandırabilirsiniz.\n\n" +
            "10. BÖLÜNEBİLİRLİK\n" +
            "Bu sözleşmenin herhangi bir hükmü geçersiz sayılırsa, diğer hükümler yürürlükte kalır.\n\n" +
            "İletişim: kemalselimkoc@gmail.com\n" +
            "Proje: https://github.com/kemalselimkoc/Project-Mantis";

        internal const string TextEn =
            "PROJECT MANTIS END USER LICENSE AGREEMENT (Version 2)\n" +
            "Copyright (c) 2026 kemalselimkoc\n\n" +
            "By installing, running or using this software you agree to the terms below. If you do not agree, choose \"Decline and exit\" and delete the software.\n\n" +
            "1. LICENSE\n" +
            "Project Mantis is free, open-source software distributed under the MIT License. Donations are voluntary and do not purchase any service, support or warranty.\n\n" +
            "2. NO WARRANTY\n" +
            "THE SOFTWARE IS PROVIDED \"AS IS\" AND \"AS AVAILABLE\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE, ACCURACY AND UNINTERRUPTED OR ERROR-FREE OPERATION.\n\n" +
            "3. LIMITATION OF LIABILITY\n" +
            "TO THE MAXIMUM EXTENT PERMITTED BY APPLICABLE LAW, THE DEVELOPER SHALL NOT BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL OR CONSEQUENTIAL DAMAGE TO YOUR COMPUTER, OPERATING SYSTEM, HARDWARE, SOFTWARE, DATA OR NETWORK (INCLUDING DATA LOSS, A SYSTEM THAT NO LONGER STARTS, MALFUNCTION, OR LOSS OF BUSINESS OR PROFIT), EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE. YOU ASSUME THE ENTIRE RISK OF USING THE SOFTWARE.\n" +
            "Liability that cannot be limited under the law of your country (for example for intent or gross negligence) and your mandatory consumer rights are not affected by this agreement.\n\n" +
            "4. YOUR RESPONSIBILITY\n" +
            "• Use the software only on computers you own or are authorized to administer.\n" +
            "• Every change is shown first and applied only after you confirm. You are responsible for the actions you confirm.\n" +
            "• Back up important files and, where possible, create a System Restore point before changing services, startup items, registry settings, the hosts file, power or security settings.\n" +
            "• Use the local network scan only on networks you own or have explicit permission to test. Unauthorized network scanning is illegal in many countries.\n" +
            "• Respect the copyright and privacy of others when capturing or translating screen text.\n\n" +
            "5. SECURITY CHECK-UP\n" +
            "The Security Check-Up is not an antivirus or a guarantee of security. It never deletes or quarantines files; it reports findings and points you to Microsoft Defender. A clean result does not mean a system is free of threats.\n\n" +
            "6. THIRD-PARTY SOFTWARE AND SERVICES\n" +
            "App updates are performed by Windows' winget tool; software installed this way is licensed by its own publisher. Google Translate and api.ipify.org are used only when you click, under their own terms. The developer is not responsible for their content or operation.\n\n" +
            "7. PRIVACY\n" +
            "Mantis collects no telemetry and sends no personal data to the developer. Details: PRIVACY.md.\n\n" +
            "8. TRADEMARKS\n" +
            "Microsoft, Windows, Microsoft Defender, BitLocker, Xbox and Bing are trademarks of the Microsoft group of companies; Google and Google Translate are trademarks of Google LLC. Project Mantis is independent and not affiliated with these companies.\n\n" +
            "9. TERMINATION\n" +
            "Your right to use the software ends if you fail to comply with this agreement. You may end it at any time by deleting the software.\n\n" +
            "10. SEVERABILITY\n" +
            "If any provision of this agreement is held invalid, the remaining provisions stay in effect.\n\n" +
            "Contact: kemalselimkoc@gmail.com\n" +
            "Project: https://github.com/kemalselimkoc/Project-Mantis";
    }
}
