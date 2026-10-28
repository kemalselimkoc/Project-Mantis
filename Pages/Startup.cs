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
    // Startup Manager page (spec B.2). Listing is read-only; each on/off is confirmed and reversible.
    public partial class MainWindow
    {
        private StackPanel startupBody;
        private List<StartupItem> startupItems;

        private void ShowStartup()
        {
            Grid page = PageGrid();
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Button back = PillButton(language == "tr" ? "Araçlar" : "Tools", ButtonKind.Ghost, delegate { ShowTools(); return Task.FromResult(0); }, null, Theme.SectionTools);
            Button refresh = PillButton(language == "tr" ? "Yenile" : "Refresh", ButtonKind.Secondary, async delegate { await LoadStartup(); }, Icons.Refresh, Theme.SectionTools);
            page.Children.Add(Header(language == "tr" ? "ARAÇLAR / BAŞLANGIÇ" : "TOOLS / STARTUP", Theme.SectionTools, language == "tr" ? "Başlangıç Yöneticisi" : "Startup Manager",
                language == "tr" ? "Windows açılırken çalışanlar. Kapatmak silmez; istediğin an geri açabilirsin." : "What runs when Windows starts. Turning one off deletes nothing; switch it back on any time.", back, refresh));
            startupBody = new StackPanel();
            ScrollViewer scroller = Scroll(startupBody); Grid.SetRow(scroller, 1); page.Children.Add(scroller);
            SetPage(page, "tools");
            if (startupItems == null) { Task t = LoadStartup(); } else RenderStartup();
        }

        private async Task LoadStartup()
        {
            if (startupBody == null) return;
            startupBody.Children.Clear();
            startupBody.Children.Add(LoadingState(language == "tr" ? "Başlangıç öğeleri okunuyor…" : "Reading startup items…", 4));
            List<StartupItem> items = null;
            try { items = await Task.Run(() => StartupTweaks.List()); } catch { }
            startupItems = items ?? new List<StartupItem>();
            RenderStartup();
        }

        private void RenderStartup()
        {
            startupBody.Children.Clear();
            if (startupItems.Count == 0)
            {
                startupBody.Children.Add(GoodState(language == "tr" ? "Başlangıçta çalışan bir şey yok" : "Nothing starts with Windows", language == "tr" ? "Run anahtarlarında ve Başlangıç klasörlerinde öğe bulunamadı." : "No items were found in the Run keys or Startup folders."));
                return;
            }
            int on = startupItems.Count(i => i.Entry.Enabled), heavy = startupItems.Count(i => i.Entry.Enabled && i.Heavy);
            string summary = language == "tr"
                ? on + " öğe açık, " + (startupItems.Count - on) + " kapalı" + (heavy > 0 ? " · açık olanlardan " + heavy + " tanesi açılışı yavaşlatabilir" : "")
                : on + " on, " + (startupItems.Count - on) + " off" + (heavy > 0 ? " · " + heavy + " of the enabled ones may slow sign-in" : "");
            TextBlock s = Paragraph(summary, Theme.Small, Theme.TextMuted); s.Margin = new Thickness(2, 0, 0, Theme.S3); startupBody.Children.Add(s);
            startupBody.Children.Add(SectionLabel(language == "tr" ? "AÇIK" : "ENABLED", true));
            bool offLabel = false;
            foreach (StartupItem item in startupItems)
            {
                if (!item.Entry.Enabled && !offLabel) { startupBody.Children.Add(SectionLabel(language == "tr" ? "KAPALI" : "DISABLED", false)); offLabel = true; }
                startupBody.Children.Add(StartupRow(item));
            }
            TextBlock note = Paragraph(language == "tr" ? "Şüpheli görünen başlangıç öğeleri için Check-Up → Kalıcılık taramasını kullan. Zamanlanmış görevler ve servisler orada listelenir." : "For startup items that look suspicious, use Check-Up → Persistence. Scheduled tasks and services are listed there.", Theme.Small, Theme.TextFaint);
            note.Margin = new Thickness(2, Theme.S3, 0, Theme.S4); startupBody.Children.Add(note);
        }

        private Border StartupRow(StartupItem item)
        {
            AutostartEntry e = item.Entry;
            string kind = e.Kind == "startup" ? (language == "tr" ? "KLASÖR" : "FOLDER") : "RUN";
            string scope = item.Machine ? (language == "tr" ? "TÜM KULLANICILAR" : "ALL USERS") : (language == "tr" ? "BU KULLANICI" : "THIS USER");
            string detail = (item.Publisher ?? (language == "tr" ? "Yayıncı bilinmiyor" : "Unknown publisher")) + "  ·  " + (e.Path ?? e.Command);
            string color = !e.Enabled ? Theme.TextFaint : item.Missing ? Theme.Warning : item.Heavy ? Theme.Amber : Theme.SectionTools;
            StackPanel trailing = new StackPanel { Orientation = Orientation.Horizontal };
            Action<UIElement> add = delegate(UIElement u) { FrameworkElement fe = u as FrameworkElement; if (fe != null) fe.Margin = new Thickness(Theme.S2, 0, 0, 0); trailing.Children.Add(u); };
            if (item.Heavy && e.Enabled) add(Badge(language == "tr" ? "AĞIR" : "HEAVY", Theme.Amber));
            if (item.Missing) add(Badge(language == "tr" ? "DOSYA YOK" : "FILE MISSING", Theme.Warning));
            if (item.Machine && !IsAdmin() && item.CanToggle) add(Badge("ADMIN", Theme.TextMuted));
            if (item.CanToggle)
            {
                Button toggle = Toggle(e.Enabled, Theme.SectionTools, e.Enabled ? (language == "tr" ? "Başlangıçta çalışmasını kapat" : "Stop it starting with Windows") : (language == "tr" ? "Başlangıçta yeniden çalıştır" : "Start it with Windows again"), delegate(bool value) { Dispatcher.BeginInvoke(new Action(delegate { ToggleStartup(item, value); })); });
                add(toggle);
            }
            else add(Badge(language == "tr" ? "TEK SEFERLİK / İLKE" : "ONCE / POLICY", Theme.TextFaint));
            Border row = ListRow(Icons.Power, kind + "  ·  " + scope, e.Name, detail, color, trailing);
            if (!e.Enabled) row.Opacity = 0.72;
            return row;
        }

        private void ToggleStartup(StartupItem item, bool enable)
        {
            string name = item.Entry.Name;
            if (item.Machine && !IsAdmin())
            {
                if (MessageBox.Show((language == "tr" ? "\"" + name + "\" tüm kullanıcılar için kayıtlı; değiştirmek yönetici izni ister.\n\nMantis yönetici olarak yeniden başlatılsın mı?" : "\"" + name + "\" is registered for all users; changing it needs administrator rights.\n\nRestart Mantis as administrator?"), language == "tr" ? "Başlangıç Yöneticisi" : "Startup Manager", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) RestartAsAdmin();
                RenderStartup(); return;
            }
            string what = enable
                ? (language == "tr" ? "\"" + name + "\" Windows açılışında yeniden çalışacak.\n\nDosya veya kayıt değiştirilmez; yalnızca Görev Yöneticisi'nin kullandığı 'etkin' işareti açılır." : "\"" + name + "\" will start with Windows again.\n\nNo file or entry is changed; only the 'enabled' flag that Task Manager uses is switched on.")
                : (language == "tr" ? "\"" + name + "\" bir sonraki açılıştan itibaren otomatik başlamayacak.\n\nHiçbir şey silinmez. Geri almak için buradan tekrar açabilir ya da Görev Yöneticisi → Başlangıç uygulamaları'nı kullanabilirsin." : "\"" + name + "\" will no longer start automatically from the next sign-in.\n\nNothing is deleted. To undo, switch it back on here or in Task Manager → Startup apps.");
            if (MessageBox.Show(what + (language == "tr" ? "\n\nUygulansın mı?" : "\n\nApply it?"), language == "tr" ? "Başlangıç Yöneticisi" : "Startup Manager", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) { RenderStartup(); return; }
            string message;
            if (!StartupTweaks.SetEnabled(item, enable, out message))
                MessageBox.Show((language == "tr" ? "Değiştirilemedi: " : "Could not change it: ") + message, language == "tr" ? "Başlangıç Yöneticisi" : "Startup Manager", MessageBoxButton.OK, MessageBoxImage.Warning);
            startupItems = startupItems.OrderBy(i => i.Entry.Enabled ? 0 : 1).ThenBy(i => i.Entry.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            RenderStartup();
        }
    }
}
