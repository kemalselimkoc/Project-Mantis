// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ProjectMantis
{
    // Screen text tool: Ctrl+Shift+X (or the Tools tile) → drag a region → text is read offline and copied.
    // Translation is a separate, explicit click that sends the text to Google Translate.
    public partial class MainWindow
    {
        internal const int ScreenTextHotkey = 0x4F43;
        private bool capturingText;
        private string ocrLanguage;
        private static readonly string[] TranslateTargets = { "tr", "en", "de", "fr", "es", "it", "ru", "ar", "ja", "zh-CN" };

        private async Task StartScreenText(bool hideMantis)
        {
            if (capturingText) return;
            if (!ScreenText.Available())
            {
                MessageBox.Show(language == "tr" ? "Ekran yazısı okuma Windows 10 veya 11'in yerleşik OCR motorunu kullanır; bu sistemde bulunamadı. Ayarlar → Saat ve dil → Dil bölümünden bir dil paketi eklemek motoru getirir." : "Screen text uses the built-in OCR engine of Windows 10 or 11, which was not found here. Adding a language pack in Settings → Time & language → Language brings it in.", language == "tr" ? "Ekrandan Yazı Al" : "Grab Text", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            capturingText = true;
            bool wasVisible = IsVisible && WindowState != WindowState.Minimized;
            try
            {
                if (hideMantis && wasVisible) { Hide(); await Task.Delay(220); }
                System.Drawing.Rectangle vs = System.Windows.Forms.SystemInformation.VirtualScreen;
                BitmapSource shot = CaptureScreen(vs);
                if (shot == null) return;
                Matrix toDip = Matrix.Identity;
                HwndSource src = PresentationSource.FromVisual(this) as HwndSource;
                if (src != null && src.CompositionTarget != null) toDip = src.CompositionTarget.TransformFromDevice;
                Point tl = toDip.Transform(new Point(vs.Left, vs.Top)), br = toDip.Transform(new Point(vs.Right, vs.Bottom));
                RegionSelector selector = new RegionSelector(shot, new Rect(tl, br), language == "tr" ? "Okunacak alanı sürükleyerek seç   ·   Esc: iptal   ·   V: panodaki görüntüyü oku" : "Drag over the text to read   ·   Esc: cancel   ·   V: read the clipboard image");
                bool? ok = selector.ShowDialog();
                if (hideMantis && wasVisible) { Show(); Activate(); }
                if (ok != true) return;
                byte[] png = selector.UseClipboard ? ClipboardPng() : CropPng(shot, selector.Selection);
                if (png == null)
                {
                    MessageBox.Show(language == "tr" ? "Panoda bir görüntü yok. Önce Win+Shift+S ile bir alan kes, sonra tekrar dene." : "There is no image on the clipboard. Snip an area with Win+Shift+S first, then try again.", language == "tr" ? "Ekrandan Yazı Al" : "Grab Text", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                await ShowScreenTextResult(png);
            }
            finally
            {
                if (hideMantis && wasVisible && !IsVisible) Show();
                capturingText = false;
            }
        }

        private static BitmapSource CaptureScreen(System.Drawing.Rectangle vs)
        {
            try
            {
                using (System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(vs.Width, vs.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bmp)) g.CopyFromScreen(vs.Left, vs.Top, 0, 0, vs.Size);
                    IntPtr h = bmp.GetHbitmap();
                    try { BitmapSource s = Imaging.CreateBitmapSourceFromHBitmap(h, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions()); s.Freeze(); return s; }
                    finally { Native.DeleteObject(h); }
                }
            }
            catch { return null; }
        }

        // Small crops are upscaled: the OCR engine reads tiny UI text much better at 2-3x.
        private static byte[] CropPng(BitmapSource shot, Int32Rect r)
        {
            if (r.Width <= 0 || r.Height <= 0) return null;
            BitmapSource crop = new CroppedBitmap(shot, r);
            double scale = r.Height < 120 ? 3 : r.Height < 400 ? 2 : 1;
            if (Math.Max(r.Width, r.Height) * scale > 9000) scale = 1;
            if (scale > 1) crop = new TransformedBitmap(crop, new ScaleTransform(scale, scale));
            return Png(crop);
        }

        private static byte[] ClipboardPng()
        {
            try { return Clipboard.ContainsImage() ? Png(Clipboard.GetImage()) : null; } catch { return null; }
        }

        private static byte[] Png(BitmapSource source)
        {
            PngBitmapEncoder enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(source));
            using (MemoryStream ms = new MemoryStream()) { enc.Save(ms); return ms.ToArray(); }
        }

        private Task ShowScreenTextResult(byte[] png)
        {
            string title = language == "tr" ? "Ekrandan Yazı Al" : "Grab Text";
            Window dialog = ThemedDialog(title, 760, 600);
            dialog.ResizeMode = ResizeMode.CanResize; dialog.MinWidth = 560; dialog.MinHeight = 420;
            if (!IsVisible) { dialog.Owner = null; dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen; dialog.Topmost = true; }   // invoked from the tray
            Grid root = new Grid { Margin = new Thickness(Theme.S4) };
            for (int i = 0; i < 6; i++) root.RowDefinitions.Add(new RowDefinition { Height = i == 1 || i == 4 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });

            TextBlock status = Paragraph(language == "tr" ? "Okunuyor…" : "Reading…", Theme.Small, Theme.TextMuted);
            status.Margin = new Thickness(0, 0, 0, Theme.S2); root.Children.Add(status);
            TextBox source = TextArea(false); Grid.SetRow(source, 1); root.Children.Add(source);

            WrapPanel ocrLangs = new WrapPanel { Margin = new Thickness(0, Theme.S2, 0, 0) };
            Grid.SetRow(ocrLangs, 2); root.Children.Add(ocrLangs);

            StackPanel translateBar = new StackPanel { Margin = new Thickness(0, Theme.S3, 0, Theme.S2) };
            translateBar.Children.Add(Paragraph(language == "tr" ? "ÇEVİR  ·  dile tıkla. Çeviri için metin Google Translate'e gönderilir; okuma kısmı tamamen çevrimdışıdır." : "TRANSLATE  ·  click a language. The text is sent to Google Translate for this; reading itself is fully offline.", Theme.Small, Theme.TextFaint));
            WrapPanel targets = new WrapPanel { Margin = new Thickness(0, Theme.S1, 0, 0) }; translateBar.Children.Add(targets);
            Grid.SetRow(translateBar, 3); root.Children.Add(translateBar);
            TextBox translated = TextArea(true); translated.Visibility = Visibility.Collapsed; Grid.SetRow(translated, 4); root.Children.Add(translated);

            StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, Theme.S3, 0, 0) };
            Button again = PillButton(language == "tr" ? "Yeni alan" : "New area", ButtonKind.Ghost, async delegate { dialog.Close(); await StartScreenText(true); }, null, Theme.SectionTools);
            Button copyT = PillButton(language == "tr" ? "Çeviriyi kopyala" : "Copy translation", ButtonKind.Secondary, delegate { Copy(translated.Text, status); return Task.FromResult(0); }, null, Theme.SectionTools);
            copyT.Visibility = Visibility.Collapsed; copyT.Margin = new Thickness(Theme.S2, 0, 0, 0);
            Button copy = PillButton(language == "tr" ? "Metni kopyala" : "Copy text", ButtonKind.Primary, delegate { Copy(source.Text, status); return Task.FromResult(0); }, Icons.Check, Theme.SectionTools);
            copy.Margin = new Thickness(Theme.S2, 0, 0, 0);
            actions.Children.Add(again); actions.Children.Add(copyT); actions.Children.Add(copy);
            Grid.SetRow(actions, 5); root.Children.Add(actions);
            dialog.Content = root;

            Func<string, Task> read = async delegate(string lang)
            {
                status.Text = language == "tr" ? "Okunuyor…" : "Reading…";
                string error = null;
                string text = await Task.Run(() => ScreenText.Recognize(png, lang, out error));
                source.Text = text ?? "";
                if (text == null) status.Text = (language == "tr" ? "Okunamadı: " : "Could not read: ") + error;
                else if (text.Trim().Length == 0) status.Text = language == "tr" ? "Bu alanda yazı bulunamadı. Daha geniş bir alan ya da başka bir okuma dili dene." : "No text found in this area. Try a larger area or another reading language.";
                else { Copy(text, null); status.Text = language == "tr" ? "Okundu ve panoya kopyalandı. Düzenleyebilirsin." : "Read and copied to the clipboard. You can edit it."; }
            };
            foreach (KeyValuePair<string, string> l in ScreenText.Languages())
            {
                string tag = l.Key;
                Button b = PillButton(l.Value, ButtonKind.Ghost, async delegate { ocrLanguage = tag; await read(tag); }, null, Theme.SectionTools);
                b.Height = 28; b.MinWidth = 0; b.Margin = new Thickness(0, 0, Theme.S1 + 2, 0); b.ToolTip = language == "tr" ? "Bu dille yeniden oku" : "Read again in this language";
                ocrLangs.Children.Add(b);
            }
            if (ocrLangs.Children.Count > 0) ocrLangs.Children.Insert(0, new TextBlock { Text = language == "tr" ? "OKUMA DİLİ  " : "READ AS  ", FontFamily = Theme.MonoFont, FontSize = 10.5, Foreground = Theme.Brush(Theme.TextFaint), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, Theme.S2, 0) });
            foreach (string t in TranslateTargets)
            {
                string target = t;
                Button b = PillButton(LanguageName(target), ButtonKind.Secondary, async delegate
                {
                    if (source.Text.Trim().Length == 0) return;
                    status.Text = language == "tr" ? "Çevriliyor…" : "Translating…";
                    string error = null, text = source.Text;
                    string result = await Task.Run(() => ScreenText.Translate(text, target, out error));
                    if (result == null) { status.Text = (language == "tr" ? "Çevrilemedi: " : "Could not translate: ") + (error == "offline" ? (language == "tr" ? "internet bağlantısı yok" : "no internet connection") : error); return; }
                    translated.Text = result; translated.Visibility = Visibility.Visible; copyT.Visibility = Visibility.Visible;
                    status.Text = (language == "tr" ? "Çevrildi: " : "Translated: ") + LanguageName(target);
                }, Icons.Globe, Theme.SectionTools);
                b.Height = 30; b.MinWidth = 0; b.Margin = new Thickness(0, 0, Theme.S1 + 2, Theme.S1 + 2);
                targets.Children.Add(b);
            }
            dialog.Loaded += async delegate { await read(ocrLanguage); };
            dialog.ShowDialog();
            return Task.FromResult(0);
        }

        private TextBox TextArea(bool readOnly)
        {
            return new TextBox
            {
                AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, IsReadOnly = readOnly, FontSize = 13.5, MinHeight = 90,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Theme.Brush(Theme.BgBase), Foreground = Theme.Brush(Theme.TextPrimary),
                BorderBrush = Theme.Brush(Theme.Stroke), CaretBrush = Theme.Brush(Theme.AccentPrimary), Padding = new Thickness(Theme.S2), SelectionBrush = Theme.Brush(Theme.AccentPrimary)
            };
        }

        private void Copy(string text, TextBlock status)
        {
            if (string.IsNullOrEmpty(text)) return;
            try { Clipboard.SetText(text); if (status != null) status.Text = language == "tr" ? "Panoya kopyalandı." : "Copied to the clipboard."; } catch { }
        }

        private string LanguageName(string tag)
        {
            switch (tag)
            {
                case "tr": return "Türkçe";
                case "en": return "English";
                case "de": return "Deutsch";
                case "fr": return "Français";
                case "es": return "Español";
                case "it": return "Italiano";
                case "ru": return "Русский";
                case "ar": return "العربية";
                case "ja": return "日本語";
                case "zh-CN": return "中文";
                default: return tag;
            }
        }
    }
}
