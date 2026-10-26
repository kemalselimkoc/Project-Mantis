// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace ProjectMantis
{
    internal enum ButtonKind { Primary, Secondary, Ghost, Danger }

    // Reusable building blocks. Pages compose these instead of styling controls by hand.
    public partial class MainWindow
    {
        private readonly Dictionary<string, ControlTemplate> templates = new Dictionary<string, ControlTemplate>();
        private Style focusVisual;

        // ---------- Typography ----------

        private TextBlock Label(string text, double size, FontWeight weight, string color)
        {
            return new TextBlock { Text = text, FontFamily = size >= Theme.H2 ? Theme.DisplayFont : Theme.TextFont, FontSize = size, FontWeight = weight, Foreground = Theme.Brush(color), TextTrimming = TextTrimming.CharacterEllipsis };
        }

        // Wrapping body copy with a fixed 1.5 line height for a steady vertical rhythm.
        private TextBlock Paragraph(string text, double size, string color)
        {
            TextBlock t = Label(text, size, FontWeights.Normal, color);
            t.TextWrapping = TextWrapping.Wrap; t.TextTrimming = TextTrimming.None;
            t.LineHeight = Math.Round(size * 1.5); t.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            return t;
        }

        // Small uppercase label with open letter spacing (WPF has no tracking, so thin spaces are used).
        private TextBlock Eyebrow(string text, string color)
        {
            return Label(Track(text), Theme.Caption, FontWeights.Bold, color);
        }

        private static string Track(string text)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                sb.Append(text[i] == ' ' ? " " : text[i].ToString());
                if (i < text.Length - 1) sb.Append(' ');
            }
            return sb.ToString();
        }

        private static void Tabular(TextBlock t) { System.Windows.Documents.Typography.SetNumeralAlignment(t, FontNumeralAlignment.Tabular); }

        // ---------- Icons ----------

        private static Viewbox Glyph(Geometry g, double size, string color) { Path p; return Glyph(g, size, color, out p); }

        private static Viewbox Glyph(Geometry g, double size, string color, out Path path)
        {
            path = new Path
            {
                Data = g, Stroke = Theme.Brush(color), StrokeThickness = size <= 16 ? 2.1 : Theme.IconStroke,
                StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
            };
            Canvas c = new Canvas { Width = 24, Height = 24 }; c.Children.Add(path);
            return new Viewbox { Width = size, Height = size, Child = c, Stretch = Stretch.Uniform };
        }

        // Tinted rounded square holding an icon; the standard leading visual for cards and rows.
        private Border IconBadge(Geometry g, string color, double box, double icon)
        {
            Viewbox v = Glyph(g, icon, color); v.HorizontalAlignment = HorizontalAlignment.Center; v.VerticalAlignment = VerticalAlignment.Center;
            return new Border
            {
                Width = box, Height = box, CornerRadius = new CornerRadius(Math.Round(box * 0.3)), Child = v,
                Background = Theme.Brush(Theme.Alpha(color, 0x1C)), BorderBrush = Theme.Brush(Theme.Alpha(color, 0x2B)), BorderThickness = new Thickness(1)
            };
        }

        // ---------- Surfaces ----------

        private Border Glass(double radius) { return Glass(radius, new Thickness(1)); }

        // Two-layer glass: translucent surface plus a stroke whose top edge carries a brighter 1px light.
        private Border Glass(double radius, Thickness border)
        {
            return new Border { Background = Theme.LiveBrush(Theme.Surface), BorderBrush = Theme.EdgeLight(Theme.Stroke), BorderThickness = border, CornerRadius = new CornerRadius(radius) };
        }

        // Elevated glass with a soft outer shadow; interactive cards lift and brighten on hover.
        private Border Card(double radius, bool interactive)
        {
            Border b = Glass(radius); b.Effect = Theme.Elevation(1);
            if (interactive) MakeHoverable(b);
            return b;
        }

        private void MakeHoverable(Border b)
        {
            b.Cursor = Cursors.Hand;
            TranslateTransform lift = new TranslateTransform(); b.RenderTransform = lift;
            Brush restStroke = b.BorderBrush, hoverStroke = Theme.EdgeLight(Theme.StrokeStrong);
            b.MouseEnter += delegate
            {
                SolidColorBrush bg = b.Background as SolidColorBrush;
                if (bg != null && !bg.IsFrozen) bg.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(Theme.ToColor(Theme.SurfaceHover), Theme.Fast));
                b.BorderBrush = hoverStroke;
                lift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-2, Theme.Normal) { EasingFunction = Ease() });
            };
            b.MouseLeave += delegate
            {
                SolidColorBrush bg = b.Background as SolidColorBrush;
                if (bg != null && !bg.IsFrozen) bg.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(Theme.ToColor(Theme.Surface), Theme.Normal));
                b.BorderBrush = restStroke;
                lift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, Theme.Normal) { EasingFunction = Ease() });
            };
        }

        // ---------- Buttons ----------

        private Button PillButton(string title, ButtonKind kind, Func<Task> click) { return PillButton(title, kind, click, null, Theme.AccentPrimary); }

        // Three variants (+ danger): Primary = filled accent, Secondary = surface + stroke, Ghost = text only.
        // All get hover (120ms color ease), pressed (scale) and disabled (dimmed) states from the template.
        private Button PillButton(string title, ButtonKind kind, Func<Task> click, Geometry icon, string accent)
        {
            string normal, hover, stroke, fg;
            switch (kind)
            {
                case ButtonKind.Primary: normal = accent; hover = Theme.Mix(accent, "#FFFFFF", 0.22); stroke = Theme.Mix(accent, "#FFFFFF", 0.3); fg = Theme.OnAccent; break;
                case ButtonKind.Ghost: normal = Theme.Alpha(accent, 0); hover = Theme.Alpha(accent, 0x1A); stroke = Theme.Alpha(accent, 0); fg = accent; break;
                case ButtonKind.Danger: normal = Theme.Alpha(Theme.Danger, 0x1F); hover = Theme.Alpha(Theme.Danger, 0x38); stroke = Theme.Alpha(Theme.Danger, 0x4D); fg = Theme.Danger; break;
                default: normal = Theme.Control; hover = Theme.ControlHover; stroke = Theme.Stroke; fg = Theme.TextPrimary; break;
            }
            StackPanel inner = new StackPanel { Orientation = Orientation.Horizontal };
            if (icon != null) { Viewbox v = Glyph(icon, 16, fg); v.Margin = new Thickness(0, 0, Theme.S2, 0); inner.Children.Add(v); }
            inner.Children.Add(new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center });
            Button b = new Button
            {
                Content = inner, Height = Theme.ControlHeight, MinWidth = kind == ButtonKind.Ghost ? 0 : 120, Padding = new Thickness(kind == ButtonKind.Ghost ? 12 : 18, 0, kind == ButtonKind.Ghost ? 12 : 18, 0),
                Margin = new Thickness(0, 0, Theme.S3, 0), Foreground = Theme.Brush(fg), FontFamily = Theme.TextFont, FontSize = Theme.Body, FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, FocusVisualStyle = FocusVisual(),
                Template = ButtonTemplate(Theme.RadiusMd - 2, normal, hover, stroke)
            };
            b.Click += async delegate { if (!busy) await click(); };
            return b;
        }

        // Compact text link for secondary actions inside dense areas (sidebar footer, rows).
        private Button LinkButton(string title, string color, Action click)
        {
            Button b = new Button
            {
                Content = new TextBlock { Text = title }, Padding = new Thickness(0), Foreground = Theme.Brush(color), FontFamily = Theme.TextFont, FontSize = Theme.Micro, FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand, HorizontalAlignment = HorizontalAlignment.Left, FocusVisualStyle = FocusVisual(), Template = ButtonTemplate(4, "#00000000", "#00000000", "#00000000")
            };
            b.MouseEnter += delegate { b.Opacity = 0.8; }; b.MouseLeave += delegate { b.Opacity = 1; };
            b.Click += delegate { click(); };
            return b;
        }

        private Button IconButton(Geometry g, string hoverBg, string color, string tooltip, Action click, out Path path)
        {
            Viewbox v = Glyph(g, 16, color, out path); v.HorizontalAlignment = HorizontalAlignment.Center;
            Button b = new Button
            {
                Content = v, Width = 42, Height = 38, ToolTip = tooltip, Cursor = Cursors.Hand, FocusVisualStyle = FocusVisual(),
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
                Template = ButtonTemplate(Theme.RadiusSm, "#00000000", hoverBg, "#00000000")
            };
            b.Click += delegate { click(); };
            return b;
        }

        private ControlTemplate ButtonTemplate(double radius, string normal, string hover, string stroke)
        {
            string key = radius.ToString(CultureInfo.InvariantCulture) + "|" + normal + "|" + hover + "|" + stroke;
            ControlTemplate cached;
            if (templates.TryGetValue(key, out cached)) return cached;
            const string bgPath = "(Border.Background).(SolidColorBrush.Color)";
            string xaml =
                "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'>" +
                "<Border x:Name='B' CornerRadius='" + radius.ToString(CultureInfo.InvariantCulture) + "' BorderThickness='1' Padding='{TemplateBinding Padding}' RenderTransformOrigin='0.5,0.5'>" +
                "<Border.Background><SolidColorBrush Color='" + normal + "'/></Border.Background>" +
                "<Border.BorderBrush><SolidColorBrush Color='" + stroke + "'/></Border.BorderBrush>" +
                "<ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='{TemplateBinding VerticalContentAlignment}'/></Border>" +
                "<ControlTemplate.Triggers>" +
                "<Trigger Property='IsMouseOver' Value='True'>" +
                "<Trigger.EnterActions><BeginStoryboard><Storyboard><ColorAnimation Storyboard.TargetName='B' Storyboard.TargetProperty='" + bgPath + "' To='" + hover + "' Duration='0:0:0.12'/></Storyboard></BeginStoryboard></Trigger.EnterActions>" +
                "<Trigger.ExitActions><BeginStoryboard><Storyboard><ColorAnimation Storyboard.TargetName='B' Storyboard.TargetProperty='" + bgPath + "' Duration='0:0:0.2'/></Storyboard></BeginStoryboard></Trigger.ExitActions>" +
                "</Trigger>" +
                "<Trigger Property='IsPressed' Value='True'><Setter TargetName='B' Property='RenderTransform'><Setter.Value><ScaleTransform ScaleX='0.97' ScaleY='0.97'/></Setter.Value></Setter></Trigger>" +
                "<Trigger Property='IsEnabled' Value='False'><Setter TargetName='B' Property='Opacity' Value='0.45'/></Trigger>" +
                "</ControlTemplate.Triggers></ControlTemplate>";
            ControlTemplate t = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(xaml);
            templates[key] = t;
            return t;
        }

        // Keyboard focus ring that matches the dark theme (the default dotted black one is invisible here).
        private Style FocusVisual()
        {
            if (focusVisual != null) return focusVisual;
            string xaml = "<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><Setter Property='Control.Template'><Setter.Value><ControlTemplate>" +
                "<Rectangle Margin='-3' RadiusX='14' RadiusY='14' Stroke='" + Theme.Alpha(Theme.AccentPrimary, 0xB3) + "' StrokeThickness='1.5'/>" +
                "</ControlTemplate></Setter.Value></Setter></Style>";
            focusVisual = (Style)System.Windows.Markup.XamlReader.Parse(xaml);
            return focusVisual;
        }

        // Slim, rounded scrollbar for every window of the app (the default one is bright and wide).
        private static void InstallGlobalStyles()
        {
            if (Application.Current == null || Application.Current.Resources.Contains(typeof(System.Windows.Controls.Primitives.ScrollBar))) return;
            string thumb = Theme.Alpha(Theme.TextMuted, 0x38), thumbHover = Theme.Alpha(Theme.TextMuted, 0x70);
            string xaml =
                "<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ScrollBar'>" +
                "<Style.Triggers><Trigger Property='Orientation' Value='Vertical'>" +
                "<Setter Property='Width' Value='10'/><Setter Property='MinWidth' Value='10'/>" +
                "<Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ScrollBar'>" +
                "<Grid Background='Transparent'><Track x:Name='PART_Track' IsDirectionReversed='True'><Track.Thumb><Thumb>" +
                "<Thumb.Template><ControlTemplate TargetType='Thumb'><Border x:Name='T' CornerRadius='3' Width='6' Background='" + thumb + "'/>" +
                "<ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='T' Property='Background' Value='" + thumbHover + "'/></Trigger></ControlTemplate.Triggers>" +
                "</ControlTemplate></Thumb.Template></Thumb></Track.Thumb></Track></Grid>" +
                "</ControlTemplate></Setter.Value></Setter></Trigger></Style.Triggers></Style>";
            Application.Current.Resources.Add(typeof(System.Windows.Controls.Primitives.ScrollBar), System.Windows.Markup.XamlReader.Parse(xaml));
        }

        // ---------- Layout ----------

        private Grid PageGrid() { return new Grid { Margin = new Thickness(Theme.S2, Theme.S1, Theme.S2, Theme.S2) }; }

        private FrameworkElement Header(string title, string subtitle, params UIElement[] actions) { return Header(null, null, title, subtitle, actions); }

        // Shared page skeleton head: optional eyebrow, display title, subtitle and a right-aligned action bar.
        private FrameworkElement Header(string eyebrow, string eyebrowColor, string title, string subtitle, params UIElement[] actions)
        {
            Grid g = new Grid { Margin = new Thickness(0, 0, 0, Theme.S5) };
            g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel left = new StackPanel();
            if (!string.IsNullOrEmpty(eyebrow)) { TextBlock e = Eyebrow(eyebrow, eyebrowColor ?? Theme.AccentPrimary); e.Margin = new Thickness(1, 0, 0, Theme.S2); left.Children.Add(e); }
            left.Children.Add(Label(title, Theme.Display, FontWeights.SemiBold, Theme.TextPrimary));
            TextBlock s = Paragraph(subtitle, Theme.Body, Theme.TextMuted); s.Margin = new Thickness(1, Theme.S1 + 2, 0, 0); left.Children.Add(s);
            g.Children.Add(left);
            if (actions != null && actions.Length > 0)
            {
                StackPanel bar = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(Theme.S5, 0, 0, 0) };
                foreach (UIElement a in actions)
                {
                    FrameworkElement fe = a as FrameworkElement;
                    if (fe != null) fe.Margin = new Thickness(Theme.S2, 0, 0, 0);
                    bar.Children.Add(a);
                }
                Grid.SetColumn(bar, 1); g.Children.Add(bar);
            }
            return g;
        }

        // Equal-width tile grid whose column count follows the available width (no ragged empty right edge).
        private UniformGrid AdaptiveGrid(double minItemWidth, int maxColumns)
        {
            UniformGrid grid = new UniformGrid { Columns = 2, Margin = new Thickness(0, 0, -Theme.S4, 0), VerticalAlignment = VerticalAlignment.Top };
            grid.SizeChanged += delegate
            {
                int cols = Math.Max(1, Math.Min(maxColumns, (int)((grid.ActualWidth) / (minItemWidth + Theme.S4))));
                if (grid.Columns != cols) grid.Columns = cols;
            };
            return grid;
        }

        private TextBlock SectionLabel(string text, bool first)
        {
            TextBlock t = Eyebrow(text, Theme.TextFaint); t.Margin = new Thickness(2, first ? 0 : Theme.S5, 0, Theme.S3); return t;
        }

        private ScrollViewer Scroll(UIElement body)
        {
            return new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0, 0, Theme.S1, 0), Focusable = false };
        }

        // ---------- Rows & badges ----------

        private Border Badge(string text, string color)
        {
            return new Border
            {
                CornerRadius = new CornerRadius(9), Padding = new Thickness(9, 3, 9, 4), VerticalAlignment = VerticalAlignment.Center,
                Background = Theme.Brush(Theme.Alpha(color, 0x1C)), BorderBrush = Theme.Brush(Theme.Alpha(color, 0x33)), BorderThickness = new Thickness(1),
                Child = Label(text, Theme.Micro, FontWeights.SemiBold, color)
            };
        }

        // List row with a thin colored severity strip on the left so the eye can scan state quickly.
        private Border ListRow(Geometry icon, string tag, string title, string detail, string color, UIElement trailing)
        {
            Border row = Glass(Theme.RadiusMd); row.Margin = new Thickness(0, 0, 0, Theme.S2); row.Padding = new Thickness(Theme.S3, Theme.S3 + 2, Theme.S4, Theme.S3 + 2);
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Border strip = new Border { Width = 3, CornerRadius = new CornerRadius(1.5), Background = Theme.Brush(color), Margin = new Thickness(0, 4, Theme.S3, 4) };
            g.Children.Add(strip);
            if (icon != null) { Border badge = IconBadge(icon, color, 38, 18); badge.Margin = new Thickness(0, 0, Theme.S4, 0); badge.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(badge, 1); g.Children.Add(badge); }
            StackPanel text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            if (!string.IsNullOrEmpty(tag)) { TextBlock t = Eyebrow(tag, color); t.Margin = new Thickness(0, 0, 0, 3); text.Children.Add(t); }
            text.Children.Add(Label(title, Theme.Body + 1, FontWeights.Medium, Theme.TextPrimary));
            if (!string.IsNullOrEmpty(detail)) { TextBlock d = Paragraph(detail, Theme.Small, Theme.TextMuted); d.Margin = new Thickness(0, 2, 0, 0); text.Children.Add(d); }
            Grid.SetColumn(text, 2); g.Children.Add(text);
            if (trailing != null) { FrameworkElement fe = trailing as FrameworkElement; if (fe != null) { fe.Margin = new Thickness(Theme.S4, 0, 0, 0); fe.VerticalAlignment = VerticalAlignment.Center; } Grid.SetColumn(trailing, 3); g.Children.Add(trailing); }
            row.Child = g;
            return row;
        }

        // ---------- States ----------

        // Inviting "not run yet" state: icon, short text and a primary action instead of an empty list.
        private Border EmptyState(Geometry icon, string title, string text, string actionText, Func<Task> action, string accent)
        {
            Border card = Glass(Theme.RadiusLg); card.Padding = new Thickness(Theme.S6, Theme.S6 + Theme.S2, Theme.S6, Theme.S6 + Theme.S2);
            StackPanel s = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = 420 };
            Border badge = IconBadge(icon, accent, 60, 28); badge.CornerRadius = new CornerRadius(30); badge.HorizontalAlignment = HorizontalAlignment.Center; s.Children.Add(badge);
            TextBlock t = Label(title, Theme.H3 + 2, FontWeights.SemiBold, Theme.TextPrimary); t.HorizontalAlignment = HorizontalAlignment.Center; t.Margin = new Thickness(0, Theme.S4, 0, 0); s.Children.Add(t);
            TextBlock p = Paragraph(text, Theme.Body, Theme.TextMuted); p.TextAlignment = TextAlignment.Center; p.Margin = new Thickness(0, Theme.S2, 0, 0); s.Children.Add(p);
            if (!string.IsNullOrEmpty(actionText) && action != null)
            {
                Button b = PillButton(actionText, ButtonKind.Primary, action, Icons.ArrowRight, accent); b.Margin = new Thickness(0, Theme.S5, 0, 0); b.HorizontalAlignment = HorizontalAlignment.Center; s.Children.Add(b);
            }
            card.Child = s;
            return card;
        }

        // Skeleton rows with a staggered pulse plus a meaningful status line, instead of a bare spinner.
        private StackPanel LoadingState(string status, int rows)
        {
            StackPanel s = new StackPanel();
            StackPanel line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2, 0, 0, Theme.S4) };
            Border dot = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = Theme.Brush(Theme.Warning), Margin = new Thickness(0, 0, Theme.S2, 0), VerticalAlignment = VerticalAlignment.Center };
            Pulse(dot, 1, 0.3, 0.7, 0); line.Children.Add(dot);
            line.Children.Add(Label(status, Theme.Small, FontWeights.Medium, Theme.TextMuted)); s.Children.Add(line);
            string fill = Theme.Alpha(Theme.TextPrimary, 0x12);
            for (int i = 0; i < rows; i++)
            {
                Border row = Glass(Theme.RadiusMd); row.Height = 66; row.Margin = new Thickness(0, 0, 0, Theme.S2); row.Padding = new Thickness(Theme.S4, 0, Theme.S4, 0);
                Grid g = new Grid(); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition());
                g.Children.Add(new Border { Width = 38, Height = 38, CornerRadius = new CornerRadius(11), Background = Theme.Brush(fill), Margin = new Thickness(0, 0, Theme.S4, 0) });
                Grid bars = new Grid { VerticalAlignment = VerticalAlignment.Center };
                bars.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(i % 2 == 0 ? 0.55 : 0.4, GridUnitType.Star) }); bars.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(i % 2 == 0 ? 0.45 : 0.6, GridUnitType.Star) });
                bars.RowDefinitions.Add(new RowDefinition()); bars.RowDefinitions.Add(new RowDefinition());
                bars.Children.Add(new Border { Height = 10, CornerRadius = new CornerRadius(5), Background = Theme.Brush(fill) });
                Border second = new Border { Height = 8, CornerRadius = new CornerRadius(4), Background = Theme.Brush(fill), Margin = new Thickness(0, Theme.S2, 0, 0) };
                Grid.SetRow(second, 1); Grid.SetColumnSpan(second, 2); bars.Children.Add(second);
                Grid.SetColumn(bars, 1); g.Children.Add(bars);
                row.Child = g;
                Pulse(row, 1, 0.45, 0.9, i * 0.12);
                s.Children.Add(row);
            }
            return s;
        }

        // Positive result instead of a dry "0 results".
        private Border GoodState(string title, string text)
        {
            return EmptyState(Icons.CheckCircle, title, text, null, null, Theme.Success);
        }

        // Gentle failure / needs-admin state with a restart action, never a crash or raw exception.
        private Border ErrorState(string title, string text, bool needsAdmin)
        {
            if (!needsAdmin || IsAdmin()) return EmptyState(Icons.Alert, title, text, null, null, Theme.Warning);
            return EmptyState(Icons.ShieldCheck, title, text, T("restartAdmin"), delegate { RestartAsAdmin(); return Task.FromResult(0); }, Theme.Warning);
        }

        // ---------- Motion ----------

        private static IEasingFunction Ease() { return new CubicEase { EasingMode = EasingMode.EaseOut }; }

        private static void Pulse(UIElement e, double from, double to, double seconds, double delay)
        {
            DoubleAnimation a = new DoubleAnimation(from, to, TimeSpan.FromSeconds(seconds))
            {
                AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, BeginTime = TimeSpan.FromSeconds(delay),
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            e.BeginAnimation(UIElement.OpacityProperty, a);
        }
    }
}
