// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ProjectMantis
{
    // Security / Check-Up (spec 2.2–2.4): an on-demand health check, not a resident antivirus.
    // Modules only read and report; a fix runs only after a confirmation that says what it changes.
    public partial class MainWindow
    {
        private readonly List<SecurityModule> securityModules = SecurityModules.All();
        private readonly Dictionary<string, ModuleResult> securityResults = new Dictionary<string, ModuleResult>();
        private readonly HashSet<string> expandedPassed = new HashSet<string>();
        private HashSet<string> checkupSelection;
        private string runningModule;
        private bool checkupRunning;
        private CancellationTokenSource checkupCancel;
        private DateTime? lastCheckup;
        private int shownScore = -1;
        private StackPanel securityActions;
        private StackPanel securityBody;

        private void ShowSecurity()
        {
            if (checkupSelection == null) checkupSelection = LoadCheckupSelection();
            shownScore = -1;
            Grid page = PageGrid();
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            securityActions = new StackPanel { Orientation = Orientation.Horizontal };
            page.Children.Add(Header(language == "tr" ? "GÜVENLİK" : "SECURITY", Theme.SectionSecurity, "Check-Up",
                language == "tr" ? "Bilgisayarının savunma durumunu bir doktor muayenesi gibi gözden geçir. Hiçbir ayar sen onaylamadan değişmez." : "Review your PC's defences like a doctor's check-up. No setting changes without your approval.", securityActions));
            securityBody = new StackPanel();
            ScrollViewer scroller = Scroll(securityBody); Grid.SetRow(scroller, 1); page.Children.Add(scroller);
            SetPage(page, "security");
            RenderSecurity();
        }

        // Developer aid (--run=hardening,persistence): opens Check-Up and runs those read-only modules on start.
        internal void AutoRun(string ids)
        {
            Loaded += async delegate
            {
                ShowSecurity();
                HashSet<string> wanted = new HashSet<string>(ids.Split(','), StringComparer.OrdinalIgnoreCase);
                await RunModules(securityModules.Where(m => wanted.Contains(m.Id)).ToList());
            };
        }

        private HashSet<string> LoadCheckupSelection()
        {
            HashSet<string> set = new HashSet<string>(StringComparer.Ordinal);
            if (settings.CheckupModules != null) { foreach (string id in settings.CheckupModules.Split(',')) if (id.Trim().Length > 0) set.Add(id.Trim()); }
            else foreach (SecurityModule m in securityModules) if (m.InFullCheckByDefault) set.Add(m.Id);
            return set;
        }

        private List<SecurityModule> RunnableSelection() { return securityModules.Where(m => m.Run != null && checkupSelection.Contains(m.Id)).ToList(); }

        private List<Finding> LatestFindings() { return securityResults.Values.SelectMany(r => r.Findings).ToList(); }

        private void RenderSecurity()
        {
            if (currentPage != "security" || securityBody == null) return;
            string accent = Theme.SectionSecurity;
            securityActions.Children.Clear();
            if (checkupRunning)
            {
                Button stop = PillButton(language == "tr" ? "Durdur" : "Stop", ButtonKind.Danger, delegate { if (checkupCancel != null) checkupCancel.Cancel(); RenderSecurity(); return Task.FromResult(0); }, Icons.Close, Theme.Danger, true);
                stop.IsEnabled = checkupCancel != null && !checkupCancel.IsCancellationRequested; stop.Margin = new Thickness(0); securityActions.Children.Add(stop);
            }
            else
            {
                Button full = PillButton(language == "tr" ? "Tam Check-Up" : "Full Check-Up", ButtonKind.Primary, async delegate { await RunModules(RunnableSelection()); }, Icons.ShieldCheck, accent);
                full.IsEnabled = RunnableSelection().Count > 0; full.Margin = new Thickness(0); securityActions.Children.Add(full);
            }

            securityBody.Children.Clear();
            Border hero = ScoreHero(); hero.Margin = new Thickness(0, 0, 0, Theme.S4); securityBody.Children.Add(hero);

            securityBody.Children.Add(SectionLabel(language == "tr" ? "MODÜLLER" : "MODULES", true));
            foreach (SecurityModule m in securityModules) securityBody.Children.Add(ModuleRow(m));

            if (securityResults.Count == 0 && !checkupRunning) return;
            securityBody.Children.Add(SectionLabel(language == "tr" ? "BULGULAR" : "FINDINGS", false));
            if (checkupRunning && runningModule != null && !securityResults.ContainsKey(runningModule))
            {
                SecurityModule m = securityModules.First(x => x.Id == runningModule);
                StackPanel loading = LoadingState(language == "tr" ? m.BusyTr : m.BusyEn, 2); loading.Margin = new Thickness(0, 0, 0, Theme.S3); securityBody.Children.Add(loading);
            }
            List<Finding> all = LatestFindings();
            if (!checkupRunning && securityResults.Count > 0 && all.All(f => f.Passed) && securityResults.Values.All(r => r.Error == null))
            {
                Border good = GoodState(language == "tr" ? "Her şey yolunda" : "Everything looks good", language == "tr" ? "Çalıştırılan modüllerde hiçbir sorun bulunmadı." : "The modules you ran found no problems.");
                good.Margin = new Thickness(0, 0, 0, Theme.S3); securityBody.Children.Add(good);
            }
            foreach (SecurityModule m in securityModules)
            {
                ModuleResult r;
                if (!securityResults.TryGetValue(m.Id, out r)) continue;
                RenderModuleFindings(m, r);
            }
            TextBlock note = Paragraph(language == "tr"
                ? "Check-Up gerçek zamanlı koruma değildir ve antivirüsün yerine geçmez; Windows Defender'ı tamamlar. Mantis dosya silmez veya karantinaya almaz. Bulgular ipucudur; şüpheli bir şey görürsen Windows Güvenliği'nde çevrimdışı tarama yap."
                : "Check-Up is not real-time protection and does not replace an antivirus; it complements Windows Defender. Mantis never deletes or quarantines files. Findings are hints; if something looks suspicious, run an offline scan in Windows Security.", Theme.Micro, Theme.TextFaint);
            note.Margin = new Thickness(2, Theme.S4, 0, Theme.S2); securityBody.Children.Add(note);
        }

        // ---------- Score ----------

        private Border ScoreHero()
        {
            bool hasRun = securityResults.Count > 0;
            List<Finding> all = LatestFindings();
            int score = hasRun ? SecurityScore.Compute(all) : 0;
            string color = !hasRun ? Theme.TextFaint : ScoreColor(score);

            Border card = Card(Theme.RadiusXl - 4, false); card.Padding = new Thickness(Theme.S5 + 4, Theme.S5, Theme.S5 + 4, Theme.S5);
            LinearGradientBrush tint = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            tint.GradientStops.Add(new GradientStop(Theme.ToColor(Theme.Mix(Theme.Surface, hasRun ? color : Theme.SectionSecurity, 0.08)), 0));
            tint.GradientStops.Add(new GradientStop(Theme.ToColor(Theme.Surface), 0.6));
            card.Background = tint;

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition());
            Ring ring = new Ring(148, 11, color);
            StackPanel center = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            TextBlock number = Label(hasRun ? score.ToString() : "—", 46, FontWeights.SemiBold, Theme.TextPrimary); Tabular(number); number.HorizontalAlignment = HorizontalAlignment.Center; number.Margin = new Thickness(0, -6, 0, -4); center.Children.Add(number);
            TextBlock outOf = Label("/ 100", Theme.Small, FontWeights.Medium, Theme.TextMuted); outOf.HorizontalAlignment = HorizontalAlignment.Center; center.Children.Add(outOf);
            ring.Children.Add(center);
            if (hasRun) { if (shownScore != score) { ring.Value = 0; ring.AnimateTo(score, 1200); shownScore = score; } else ring.Value = score; }
            g.Children.Add(ring);

            StackPanel text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(Theme.S6, 0, 0, 0) };
            text.Children.Add(Eyebrow(language == "tr" ? "SAĞLIK SKORU" : "HEALTH SCORE", hasRun ? color : Theme.SectionSecurity));
            TextBlock title = Label(hasRun ? ScoreVerdict(score) : (language == "tr" ? "Henüz check-up yapılmadı" : "No check-up yet"), Theme.H2 + 6, FontWeights.SemiBold, Theme.TextPrimary); title.Margin = new Thickness(0, Theme.S2, 0, 0); text.Children.Add(title);
            if (!hasRun)
            {
                TextBlock p = Paragraph(language == "tr" ? "Tam Check-Up seçtiğin modülleri sırayla çalıştırır ve sonunda 0–100 arası, nedenleri açıkça yazan bir skor verir. Her şey yerelde kalır." : "Full Check-Up runs the modules you pick, one by one, and ends with a 0–100 score that explains every point. Everything stays on this PC.", Theme.Body, Theme.TextMuted);
                p.Margin = new Thickness(0, Theme.S2, 0, 0); p.MaxWidth = 520; p.HorizontalAlignment = HorizontalAlignment.Left; text.Children.Add(p);
            }
            else
            {
                WrapPanel counts = new WrapPanel { Margin = new Thickness(0, Theme.S3, 0, 0) };
                AddCount(counts, all.Count(f => !f.Passed && f.Severity == Severity.High), language == "tr" ? "yüksek" : "high", Theme.Danger);
                AddCount(counts, all.Count(f => !f.Passed && f.Severity == Severity.Medium), language == "tr" ? "orta" : "medium", Theme.Warning);
                AddCount(counts, all.Count(f => !f.Passed && f.Severity == Severity.Low), language == "tr" ? "düşük" : "low", Theme.Success);
                AddCount(counts, all.Count(f => f.Passed), language == "tr" ? "kontrol geçti" : "checks passed", Theme.Info);
                text.Children.Add(counts);
                TextBlock meta = Label((language == "tr" ? "Son check-up: " : "Last check-up: ") + (lastCheckup.HasValue ? RelativeTime(lastCheckup.Value) : "—") + "  ·  " + securityResults.Count + (language == "tr" ? " modül" : securityResults.Count == 1 ? " module" : " modules") + "  ·  " + (language == "tr" ? "Puan: yüksek −20, orta −8, düşük −3" : "Scoring: high −20, medium −8, low −3"), Theme.Micro, FontWeights.Normal, Theme.TextFaint);
                meta.Margin = new Thickness(0, Theme.S3, 0, 0); text.Children.Add(meta);
            }
            Grid.SetColumn(text, 1); g.Children.Add(text);
            card.Child = g;
            return card;
        }

        private void AddCount(WrapPanel panel, int n, string label, string color)
        {
            if (n == 0) return;
            Border b = Badge(n + " " + label, color); b.Margin = new Thickness(0, 0, Theme.S2, Theme.S1); panel.Children.Add(b);
        }

        private static string ScoreColor(int score) { return score >= 90 ? Theme.Success : score >= 75 ? Theme.AccentSecondary : score >= 50 ? Theme.Warning : Theme.Danger; }

        private string ScoreVerdict(int score)
        {
            if (score >= 90) return language == "tr" ? "Harika durumdasın" : "In great shape";
            if (score >= 75) return language == "tr" ? "İyi durumdasın" : "In good shape";
            if (score >= 50) return language == "tr" ? "Biraz dikkat istiyor" : "Needs some attention";
            return language == "tr" ? "Risk altında" : "At risk";
        }

        // ---------- Modules ----------

        private static Geometry ModuleIcon(string id)
        {
            switch (id)
            {
                case "hardening": return Icons.ShieldCheck;
                case "persistence": return Icons.Key;
                case "malware": return Icons.Bug;
                case "software": return Icons.Download;
                default: return Icons.Network;
            }
        }

        private Border ModuleRow(SecurityModule m)
        {
            string accent = Theme.SectionSecurity;
            bool available = m.Run != null;
            ModuleResult r; securityResults.TryGetValue(m.Id, out r);
            Border row = Glass(Theme.RadiusMd); row.Margin = new Thickness(0, 0, 0, Theme.S2); row.Padding = new Thickness(Theme.S4, Theme.S3, Theme.S3, Theme.S3);
            if (!available) row.Opacity = 0.6;
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Button toggle = Toggle(checkupSelection.Contains(m.Id), accent, language == "tr" ? "Tam Check-Up'a dahil et" : "Include in Full Check-Up", delegate(bool on)
            {
                if (on) checkupSelection.Add(m.Id); else checkupSelection.Remove(m.Id);
                settings.CheckupModules = string.Join(",", checkupSelection); settings.Save();
                RenderSecurity();
            });
            toggle.IsEnabled = available && !checkupRunning; toggle.Margin = new Thickness(0, 0, Theme.S4, 0); g.Children.Add(toggle);

            Border badge = IconBadge(ModuleIcon(m.Id), accent, 38, 18); badge.Margin = new Thickness(0, 0, Theme.S4 - 2, 0); Grid.SetColumn(badge, 1); g.Children.Add(badge);
            StackPanel text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(Label(language == "tr" ? m.NameTr : m.NameEn, Theme.Body + 1, FontWeights.Medium, Theme.TextPrimary));
            TextBlock d = Paragraph(language == "tr" ? m.DescTr : m.DescEn, Theme.Small, Theme.TextMuted); d.Margin = new Thickness(0, 2, Theme.S3, 0); text.Children.Add(d);
            Grid.SetColumn(text, 2); g.Children.Add(text);

            FrameworkElement status = ModuleStatus(m, r, available);
            status.VerticalAlignment = VerticalAlignment.Center; status.Margin = new Thickness(0, 0, Theme.S3, 0);
            Grid.SetColumn(status, 3); g.Children.Add(status);

            Button run = PillButton(language == "tr" ? "Çalıştır" : "Run", ButtonKind.Secondary, async delegate { await RunModules(new List<SecurityModule> { m }); }, null, accent);
            run.MinWidth = 92; run.Height = 34; run.Margin = new Thickness(0); run.IsEnabled = available && !checkupRunning; run.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(run, 4); g.Children.Add(run);
            row.Child = g;
            return row;
        }

        private FrameworkElement ModuleStatus(SecurityModule m, ModuleResult r, bool available)
        {
            if (!available) return Badge(language == "tr" ? "Yakında" : "Soon", Theme.TextMuted);
            if (checkupRunning && runningModule == m.Id)
            {
                StackPanel s = new StackPanel { Orientation = Orientation.Horizontal };
                Border dot = new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(4), Background = Theme.Brush(Theme.Warning), Margin = new Thickness(0, 1, Theme.S2, 0), VerticalAlignment = VerticalAlignment.Center };
                Pulse(dot, 1, 0.3, 0.6, 0); s.Children.Add(dot);
                s.Children.Add(Label(language == "tr" ? "Taranıyor…" : "Scanning…", Theme.Small, FontWeights.SemiBold, Theme.Warning));
                return s;
            }
            if (r == null) return m.LocalNetworkOnly ? Badge(language == "tr" ? "Yalnızca yerel ağ" : "Local network only", Theme.Info) : (FrameworkElement)new Border();
            if (r.Error != null) return Badge(language == "tr" ? "Tamamlanamadı" : "Incomplete", Theme.Warning);
            int high = r.Issues.Count(f => f.Severity == Severity.High), medium = r.Issues.Count(f => f.Severity == Severity.Medium), low = r.Issues.Count(f => f.Severity == Severity.Low);
            if (high + medium + low == 0) return Badge(r.NeedsAdmin ? (language == "tr" ? "Temiz · kısmi" : "Clean · partial") : (language == "tr" ? "Temiz" : "Clean"), Theme.Success);
            List<string> parts = new List<string>();
            if (high > 0) parts.Add(high + (language == "tr" ? " yüksek" : " high"));
            if (medium > 0) parts.Add(medium + (language == "tr" ? " orta" : " medium"));
            if (low > 0) parts.Add(low + (language == "tr" ? " düşük" : " low"));
            return Badge(string.Join(", ", parts), high > 0 ? Theme.Danger : medium > 0 ? Theme.Warning : Theme.Success);
        }

        // ---------- Findings ----------

        private void RenderModuleFindings(SecurityModule m, ModuleResult r)
        {
            TextBlock head = Label((language == "tr" ? m.NameTr : m.NameEn) + "  ·  " + RelativeTime(r.When), Theme.Small, FontWeights.SemiBold, Theme.TextMuted);
            head.Margin = new Thickness(2, Theme.S3, 0, Theme.S2); securityBody.Children.Add(head);
            if (r.Error != null)
            {
                string msg = r.Error == "cancelled" ? (language == "tr" ? "Tarama durduruldu." : "The scan was stopped.") : (language == "tr" ? "Bu modül tamamlanamadı: " : "This module could not finish: ") + r.Error;
                securityBody.Children.Add(ListRow(Icons.Alert, language == "tr" ? "TAMAMLANAMADI" : "INCOMPLETE", language == "tr" ? "Sonuçlar eksik olabilir" : "Results may be incomplete", msg, Theme.Warning, null));
            }
            if (r.NeedsAdmin && !IsAdmin())
            {
                Button elevate = PillButton(T("restartAdmin"), ButtonKind.Secondary, delegate { RestartAsAdmin(); return Task.FromResult(0); }, Icons.ShieldCheck, Theme.Warning);
                elevate.Height = 34; elevate.MinWidth = 0;
                securityBody.Children.Add(ListRow(Icons.Lock, language == "tr" ? "YÖNETİCİ" : "ADMIN", language == "tr" ? "Daha fazlası için yönetici olarak yeniden başlat" : "Restart as administrator to check more", language == "tr" ? "Bazı kontroller standart modda okunamadı ve atlandı; skor yalnızca okunabilenlere göre hesaplandı." : "Some checks cannot be read in standard mode and were skipped; the score only counts what could be read.", Theme.Warning, elevate));
            }
            foreach (Finding f in r.Issues.OrderByDescending(x => x.Severity)) securityBody.Children.Add(FindingRow(f));
            List<Finding> info = r.Findings.Where(f => !f.Passed && f.Severity == Severity.Info).ToList();
            foreach (Finding f in info) securityBody.Children.Add(FindingRow(f));
            List<Finding> passed = r.Findings.Where(f => f.Passed).ToList();
            if (passed.Count == 0) return;
            bool open = expandedPassed.Contains(m.Id);
            Button more = LinkButton((open ? "▾  " : "▸  ") + passed.Count + (language == "tr" ? " kontrol geçti" : " checks passed") + (open ? "" : (language == "tr" ? " — göster" : " — show")), Theme.Success, delegate
            {
                if (!expandedPassed.Remove(m.Id)) expandedPassed.Add(m.Id);
                RenderSecurity();
            });
            more.Margin = new Thickness(4, Theme.S1, 0, Theme.S2); securityBody.Children.Add(more);
            if (open) foreach (Finding f in passed) securityBody.Children.Add(FindingRow(f));
        }

        private static string SeverityColor(Severity s) { return s == Severity.High ? Theme.Danger : s == Severity.Medium ? Theme.Warning : s == Severity.Low ? Theme.Success : Theme.Info; }

        private string SeverityLabel(Severity s)
        {
            if (language == "tr") return s == Severity.High ? "YÜKSEK" : s == Severity.Medium ? "ORTA" : s == Severity.Low ? "DÜŞÜK" : "BİLGİ";
            return s == Severity.High ? "HIGH" : s == Severity.Medium ? "MEDIUM" : s == Severity.Low ? "LOW" : "INFO";
        }

        // One finding: severity strip and tag, title, explanation, recommendation and (when safe) an action.
        private Border FindingRow(Finding f)
        {
            string color = f.Passed ? Theme.Success : SeverityColor(f.Severity);
            Geometry icon = f.Passed ? Icons.CheckCircle : f.Severity >= Severity.Medium ? Icons.Alert : Icons.InfoCircle;
            string detail = language == "tr" ? f.DetailTr : f.DetailEn;
            string rec = language == "tr" ? f.RecommendationTr : f.RecommendationEn;
            if (!string.IsNullOrEmpty(rec)) detail = (string.IsNullOrEmpty(detail) ? "" : detail + "\n") + (language == "tr" ? "Öneri: " : "Suggestion: ") + rec;
            UIElement action = f.Passed ? (UIElement)Badge("OK", Theme.Success) : FixButton(f);
            return ListRow(icon, f.Passed ? (language == "tr" ? "GEÇTİ" : "PASSED") : SeverityLabel(f.Severity), language == "tr" ? f.TitleTr : f.TitleEn, detail, color, action);
        }

        private UIElement FixButton(Finding f)
        {
            Button b = null;
            if (f.FixKind == FixKinds.Command && SecurityFixes.Exists(f.FixPayload))
                b = PillButton(language == "tr" ? "Düzelt" : "Fix", ButtonKind.Secondary, async delegate { await ApplyFinding(f); }, Icons.Check, Theme.SectionSecurity);
            else if (f.FixKind == FixKinds.OpenSettings)
                b = PillButton(language == "tr" ? "Ayarı aç" : "Open setting", ButtonKind.Ghost, async delegate { await ApplyFinding(f); }, Icons.ArrowUpRight, Theme.Info);
            else if (f.FixKind == FixKinds.Defender)
                b = PillButton(language == "tr" ? "Windows Güvenliği" : "Windows Security", ButtonKind.Ghost, async delegate { await ApplyFinding(f); }, Icons.ArrowUpRight, Theme.Info);
            else if (f.FixKind == FixKinds.Reveal)
                b = PillButton(language == "tr" ? "Konumu aç" : "Show file", ButtonKind.Ghost, async delegate { await ApplyFinding(f); }, Icons.Folder, Theme.Info);
            if (b == null) return null;
            b.Height = 34; b.MinWidth = 0; b.Margin = new Thickness(0);
            if (f.FixKind == FixKinds.Command && f.RequiresAdmin && !IsAdmin()) b.ToolTip = language == "tr" ? "Yönetici izni gerekir" : "Needs administrator rights";
            return b;
        }

        private async Task ApplyFinding(Finding f)
        {
            if (f.FixKind == FixKinds.OpenSettings || f.FixKind == FixKinds.Defender)
            {
                if (!SecurityFixes.OpenUri(string.IsNullOrEmpty(f.FixPayload) ? "windowsdefender:" : f.FixPayload))
                    MessageBox.Show(language == "tr" ? "İlgili Windows ekranı açılamadı." : "The Windows settings page could not be opened.", "Check-Up", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (f.FixKind == FixKinds.Reveal)
            {
                if (!SecurityFixes.Reveal(f.FixPayload)) MessageBox.Show(language == "tr" ? "Dosya artık bulunamıyor." : "The file can no longer be found.", "Check-Up", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (f.FixKind != FixKinds.Command) return;
            string title = language == "tr" ? f.TitleTr : f.TitleEn;
            string what = language == "tr" ? f.FixDescriptionTr : f.FixDescriptionEn;
            if (f.RequiresAdmin && !IsAdmin())
            {
                if (MessageBox.Show((language == "tr" ? "Bu düzeltme yönetici izni ister:\n\n" : "This fix needs administrator rights:\n\n") + what + (language == "tr" ? "\n\nMantis yönetici olarak yeniden başlatılsın mı? Check-Up sonra yeniden çalıştırılabilir." : "\n\nRestart Mantis as administrator? You can run Check-Up again afterwards."), "Check-Up", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) RestartAsAdmin();
                return;
            }
            if (MessageBox.Show(title + "\n\n" + what + (language == "tr" ? "\n\nUygulansın mı?" : "\n\nApply it?"), language == "tr" ? "Düzeltmeyi onayla" : "Confirm fix", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            SetBusy(true, language == "tr" ? "Düzeltiliyor…" : "Applying fix…");
            string message = null;
            bool ok = await Task.Run(() => { string m; bool r = SecurityFixes.Apply(f.FixPayload, out m); message = m; return r; });
            SetBusy(false, T("ready"));
            MessageBox.Show(ok ? (language == "tr" ? "Uygulandı. Modül yeniden kontrol ediliyor." : "Applied. Re-checking the module.") : (language == "tr" ? "Uygulanamadı: " : "Could not apply: ") + message, "Check-Up", MessageBoxButton.OK, ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
            SecurityModule module = securityModules.FirstOrDefault(x => x.Id == f.ModuleId);
            if (module != null) await RunModules(new List<SecurityModule> { module });
        }

        // ---------- Engine ----------

        private async Task RunModules(List<SecurityModule> modules)
        {
            modules = modules.Where(m => m.Run != null).ToList();
            if (busy || checkupRunning || modules.Count == 0) return;
            checkupRunning = true; checkupCancel = new CancellationTokenSource();
            CancellationToken token = checkupCancel.Token;
            for (int i = 0; i < modules.Count; i++)
            {
                SecurityModule m = modules[i];
                if (token.IsCancellationRequested) break;
                runningModule = m.Id;
                securityResults.Remove(m.Id);
                SetBusy(true, (language == "tr" ? m.BusyTr : m.BusyEn) + (modules.Count > 1 ? "  (" + (i + 1) + "/" + modules.Count + ")" : ""));
                RenderSecurity();
                ModuleResult r = await Task.Run(() => m.SafeRun(token));
                securityResults[m.Id] = r;
            }
            runningModule = null; checkupRunning = false; lastCheckup = DateTime.Now;
            checkupCancel.Dispose(); checkupCancel = null;
            SetBusy(false, T("ready"));
            int score = SecurityScore.Compute(LatestFindings());
            statusText.Text = (language == "tr" ? "Check-Up tamam · skor " : "Check-Up done · score ") + score + "/100";
            SetNavBadge("security", securityResults.Values.SelectMany(x => x.Issues).Any(f => f.Severity >= Severity.Medium) ? "!" : null);
            RenderSecurity();
        }
    }
}
