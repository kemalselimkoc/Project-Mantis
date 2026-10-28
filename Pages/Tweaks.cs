// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ProjectMantis
{
    // "Windows'u Rahatlat" page (spec B.4): per-user tweaks as toggles; off = back to the previous/default state.
    public partial class MainWindow
    {
        private StackPanel tweaksBody;
        private List<QolTweak> qolTweaks;

        private void ShowTweaks()
        {
            Grid page = PageGrid();
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Button back = PillButton(language == "tr" ? "Araçlar" : "Tools", ButtonKind.Ghost, delegate { ShowTools(); return Task.FromResult(0); }, null, Theme.SectionTools);
            Button explorer = PillButton(language == "tr" ? "Gezgin'i yeniden başlat" : "Restart Explorer", ButtonKind.Secondary, async delegate { await AskRestartExplorer(false); }, Icons.Refresh, Theme.SectionTools);
            page.Children.Add(Header(language == "tr" ? "ARAÇLAR / TWEAK" : "TOOLS / TWEAKS", Theme.SectionTools, language == "tr" ? "Windows'u Rahatlat" : "Ease Up Windows",
                language == "tr" ? "Çok aranan küçük ayarlar. Yalnızca senin kullanıcı ayarların değişir; kapatınca önceki hâline döner." : "Small, much-wanted settings. Only your user settings change; switching off puts back what was there.", back, explorer));
            tweaksBody = new StackPanel();
            ScrollViewer scroller = Scroll(tweaksBody); Grid.SetRow(scroller, 1); page.Children.Add(scroller);
            SetPage(page, "tools");
            try { qolTweaks = QoLTweaks.List(); } catch { qolTweaks = new List<QolTweak>(); }
            RenderTweaks();
        }

        private void RenderTweaks()
        {
            tweaksBody.Children.Clear();
            string group = null;
            foreach (QolTweak t in qolTweaks)
            {
                string g = language == "tr" ? t.GroupTr : t.GroupEn;
                if (g != group) { tweaksBody.Children.Add(SectionLabel(g, group == null)); group = g; }
                Button toggle = Toggle(t.Applied, Theme.SectionTools, null, delegate(bool value) { Dispatcher.BeginInvoke(new Action(async delegate { await ToggleTweak(t, value); })); });
                string tag = t.Applied ? (language == "tr" ? "UYGULANDI" : "APPLIED") : (language == "tr" ? "VARSAYILAN" : "DEFAULT");
                if (t.NeedsExplorer) tag += "  ·  " + (language == "tr" ? "GEZGİN YENİDEN BAŞLAR" : "EXPLORER RESTART");
                tweaksBody.Children.Add(ListRow(Icons.Sliders, tag, language == "tr" ? t.NameTr : t.NameEn, language == "tr" ? t.DescTr : t.DescEn, t.Applied ? Theme.SectionTools : Theme.TextFaint, toggle));
            }
            TextBlock note = Paragraph(language == "tr" ? "Uygulamadan önce her değerin eski hâli kaydedilir. Hepsi HKCU altındadır; yönetici izni gerekmez ve diğer kullanıcılar etkilenmez." : "Each value's previous state is saved before applying. Everything lives under HKCU: no administrator rights needed and other users are not affected.", Theme.Small, Theme.TextFaint);
            note.Margin = new Thickness(2, Theme.S3, 0, Theme.S4); tweaksBody.Children.Add(note);
        }

        private async Task ToggleTweak(QolTweak t, bool apply)
        {
            string title = language == "tr" ? "Windows'u Rahatlat" : "Ease Up Windows";
            string name = language == "tr" ? t.NameTr : t.NameEn;
            string what = apply
                ? name + "\n\n" + (language == "tr" ? t.DescTr : t.DescEn) + (language == "tr" ? "\n\nGeri almak için anahtarı kapatman yeterli." : "\n\nTo undo, switch it off again.")
                : (language == "tr" ? name + " varsayılana dönecek (uygulanmadan önceki değerler geri yüklenir)." : name + " will go back to default (the values from before are restored).");
            if (MessageBox.Show(what + (language == "tr" ? "\n\nUygulansın mı?" : "\n\nApply it?"), title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) { RenderTweaks(); return; }
            string message;
            bool ok = apply ? QoLTweaks.Apply(t, out message) : QoLTweaks.Revert(t, out message);
            RenderTweaks();
            if (!ok)
            {
                MessageBox.Show(message == "locked" || message == "access denied"
                    ? (language == "tr" ? "Windows bu ayarı korumaya almış; değişiklik uygulanamadı. Ayarlar uygulamasından değiştirmeyi deneyebilirsin." : "Windows protects this setting, so it could not be changed. You can try changing it in the Settings app.")
                    : (language == "tr" ? "Uygulanamadı: " : "Could not apply: ") + message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (t.NeedsExplorer) await AskRestartExplorer(true);
        }

        private async Task AskRestartExplorer(bool afterTweak)
        {
            string q = language == "tr"
                ? (afterTweak ? "Değişikliğin hemen görünmesi için Gezgin yeniden başlatılsın mı?\n\n" : "") + "Görev çubuğu bir an kaybolup geri gelir; açık Dosya Gezgini pencereleri kapanır. Diğer uygulamalar etkilenmez."
                : (afterTweak ? "Restart Explorer so the change shows up now?\n\n" : "") + "The taskbar disappears for a moment and comes back; open File Explorer windows close. Other apps are not affected.";
            if (MessageBox.Show(q, language == "tr" ? "Gezgin'i yeniden başlat" : "Restart Explorer", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            SetBusy(true, language == "tr" ? "Gezgin yeniden başlatılıyor…" : "Restarting Explorer…");
            bool ok = await Task.Run(() => QoLTweaks.RestartExplorer());
            SetBusy(false, T("ready"));
            if (!ok) MessageBox.Show(language == "tr" ? "Gezgin yeniden başlatılamadı. Oturumu kapatıp açmak da aynı işi görür." : "Explorer could not be restarted. Signing out and back in does the same.", "Project Mantis", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
