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
    // Services page (spec B.3): a short, hand-picked allowlist; never the full service list.
    public partial class MainWindow
    {
        private StackPanel servicesBody;
        private List<ServiceTweak> serviceTweaks;

        private void ShowServices()
        {
            Grid page = PageGrid();
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Button back = PillButton(language == "tr" ? "Araçlar" : "Tools", ButtonKind.Ghost, delegate { ShowTools(); return Task.FromResult(0); }, null, Theme.SectionTools);
            page.Children.Add(Header(language == "tr" ? "ARAÇLAR / SERVİSLER" : "TOOLS / SERVICES", Theme.SectionTools, language == "tr" ? "Servisler" : "Services",
                language == "tr" ? "Sık sorulan birkaç Windows servisi. Kapatmanın etkisi her satırda yazar; geri açmak tek tık." : "A few commonly asked-about Windows services. Each row says what turning it off does; turning it back on is one click.", back));
            servicesBody = new StackPanel();
            ScrollViewer scroller = Scroll(servicesBody); Grid.SetRow(scroller, 1); page.Children.Add(scroller);
            SetPage(page, "tools");
            LoadServices();
        }

        private async void LoadServices()
        {
            servicesBody.Children.Clear();
            servicesBody.Children.Add(LoadingState(language == "tr" ? "Servisler okunuyor…" : "Reading services…", 3));
            List<ServiceTweak> list = null;
            try { list = await Task.Run(() => ServiceTweaks.List()); } catch { }
            serviceTweaks = list ?? new List<ServiceTweak>();
            RenderServices();
        }

        private void RenderServices()
        {
            servicesBody.Children.Clear();
            if (!IsAdmin())
                servicesBody.Children.Add(ListRow(Icons.ShieldCheck, "ADMIN", language == "tr" ? "Değiştirmek yönetici izni ister" : "Changing services needs administrator rights",
                    language == "tr" ? "Durumları görebilirsin; aç/kapat için Mantis yönetici olarak yeniden başlatılır." : "You can see their state; switching asks to restart Mantis as administrator.", Theme.Warning,
                    PillButton(language == "tr" ? "Yönetici olarak aç" : "Open as admin", ButtonKind.Secondary, delegate { RestartAsAdmin(); return Task.FromResult(0); }, null, Theme.Warning)));
            servicesBody.Children.Add(SectionLabel(language == "tr" ? "SEÇİLMİŞ SERVİSLER" : "HAND-PICKED SERVICES", true));
            foreach (ServiceTweak t in serviceTweaks.Where(x => x.Installed)) servicesBody.Children.Add(ServiceRow(t));
            TextBlock note = Paragraph(language == "tr" ? "Kapatılan servisin önceki başlangıç türü kaydedilir; geri açınca aynısı geri yüklenir. Bu listede olmayan servislere Mantis dokunmaz." : "A service's previous start type is saved when you turn it off and restored when you turn it back on. Mantis never touches services outside this list.", Theme.Small, Theme.TextFaint);
            note.Margin = new Thickness(2, Theme.S3, 0, Theme.S4); servicesBody.Children.Add(note);
        }

        private Border ServiceRow(ServiceTweak t)
        {
            string tag = (t.Running ? (language == "tr" ? "ÇALIŞIYOR" : "RUNNING") : t.Enabled ? (language == "tr" ? "GEREKİNCE BAŞLAR" : "STARTS WHEN NEEDED") : (language == "tr" ? "KAPALI" : "OFF")) + "  ·  " + string.Join(", ", t.Services);
            string detail = (language == "tr" ? t.WhatTr + " " + t.EffectTr : t.WhatEn + " " + t.EffectEn);
            Button toggle = Toggle(t.Enabled, Theme.SectionTools, null, delegate(bool value) { Dispatcher.BeginInvoke(new Action(delegate { ToggleService(t, value); })); });
            Border row = ListRow(Icons.Sliders, tag, language == "tr" ? t.NameTr : t.NameEn, detail, t.Enabled ? Theme.SectionTools : Theme.TextFaint, toggle);
            if (!t.Enabled) row.Opacity = 0.78;
            return row;
        }

        private async void ToggleService(ServiceTweak t, bool enable)
        {
            string title = language == "tr" ? "Servisler" : "Services";
            string name = language == "tr" ? t.NameTr : t.NameEn;
            if (!IsAdmin())
            {
                if (MessageBox.Show(language == "tr" ? "Servisleri değiştirmek yönetici izni ister.\n\nMantis yönetici olarak yeniden başlatılsın mı?" : "Changing services needs administrator rights.\n\nRestart Mantis as administrator?", title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) RestartAsAdmin();
                RenderServices(); return;
            }
            string what = enable
                ? (language == "tr" ? name + " yeniden açılacak; önceki başlangıç türü geri yüklenir." : name + " will be turned back on with its previous start type.")
                : (language == "tr" ? name + " durdurulacak ve başlangıç türü 'Devre dışı' yapılacak.\n\n" + t.EffectTr + "\n\nGeri almak için buradan tekrar açman yeterli." : name + " will be stopped and its start type set to Disabled.\n\n" + t.EffectEn + "\n\nTo undo, switch it back on here.");
            if (MessageBox.Show(what + (language == "tr" ? "\n\nUygulansın mı?" : "\n\nApply it?"), title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) { RenderServices(); return; }
            SetBusy(true, language == "tr" ? "Servis ayarlanıyor…" : "Updating service…");
            string message = null;
            bool ok = await Task.Run(() => { string m; bool r = ServiceTweaks.SetEnabled(t, enable, out m); message = m; return r; });
            SetBusy(false, T("ready"));
            if (!ok) MessageBox.Show((language == "tr" ? "Tamamı uygulanamadı:\n\n" : "Not everything could be applied:\n\n") + message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            RenderServices();
        }
    }
}
