// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ProjectMantis
{
    // Full-screen overlay over a frozen screenshot of all monitors: drag to pick a region, Esc to cancel,
    // V to use the image on the clipboard instead. Selection is returned in screenshot pixels.
    internal sealed class RegionSelector : Window
    {
        private readonly BitmapSource shot;
        private readonly Canvas canvas = new Canvas();
        private readonly Path shade = new Path { Fill = new SolidColorBrush(Color.FromArgb(0x99, 0, 0, 0)), IsHitTestVisible = false };
        private readonly Rectangle frame = new Rectangle { StrokeThickness = 1.5, Visibility = Visibility.Collapsed, IsHitTestVisible = false };
        private Point start;
        private bool dragging;

        public Int32Rect Selection { get; private set; }
        public bool UseClipboard { get; private set; }

        public RegionSelector(BitmapSource screenshot, Rect bounds, string hint)
        {
            shot = screenshot;
            WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; Topmost = true;
            Left = bounds.Left; Top = bounds.Top; Width = bounds.Width; Height = bounds.Height;
            Cursor = Cursors.Cross; Background = Brushes.Black;
            frame.Stroke = Theme.Brush(Theme.AccentPrimary);
            Grid root = new Grid();
            root.Children.Add(new Image { Source = screenshot, Stretch = Stretch.Fill });
            canvas.Background = Brushes.Transparent;
            root.Children.Add(canvas);
            canvas.Children.Add(shade); canvas.Children.Add(frame);
            Border tip = new Border
            {
                Background = Theme.Brush("#E60B0F0D"), BorderBrush = Theme.Brush(Theme.StrokeStrong), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14, 8, 14, 8), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 24, 0, 0), IsHitTestVisible = false,
                Child = new TextBlock { Text = hint, FontFamily = Theme.MonoFont, FontSize = 12, Foreground = Theme.Brush(Theme.TextPrimary) }
            };
            root.Children.Add(tip);
            Content = root;
            Loaded += delegate { Activate(); Focus(); UpdateShade(new Rect()); };
            KeyDown += OnKey;
            canvas.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) { start = e.GetPosition(canvas); dragging = true; canvas.CaptureMouse(); };
            canvas.MouseMove += delegate(object s, MouseEventArgs e) { if (dragging) UpdateShade(new Rect(start, e.GetPosition(canvas))); };
            canvas.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
            {
                if (!dragging) return;
                dragging = false; canvas.ReleaseMouseCapture();
                Rect r = new Rect(start, e.GetPosition(canvas));
                if (r.Width < 6 || r.Height < 6) { UpdateShade(new Rect()); return; }
                double sx = shot.PixelWidth / canvas.ActualWidth, sy = shot.PixelHeight / canvas.ActualHeight;
                int x = (int)Math.Max(0, Math.Floor(r.X * sx)), y = (int)Math.Max(0, Math.Floor(r.Y * sy));
                int w = (int)Math.Min(shot.PixelWidth - x, Math.Ceiling(r.Width * sx)), h = (int)Math.Min(shot.PixelHeight - y, Math.Ceiling(r.Height * sy));
                Selection = new Int32Rect(x, y, w, h);
                DialogResult = true;
            };
        }

        private void OnKey(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) DialogResult = false;
            else if (e.Key == Key.V) { UseClipboard = true; DialogResult = true; }
        }

        private void UpdateShade(Rect sel)
        {
            Rect all = new Rect(0, 0, Math.Max(1, canvas.ActualWidth), Math.Max(1, canvas.ActualHeight));
            shade.Data = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(all), new RectangleGeometry(sel));
            if (sel.Width <= 0) { frame.Visibility = Visibility.Collapsed; return; }
            frame.Visibility = Visibility.Visible;
            Canvas.SetLeft(frame, sel.X); Canvas.SetTop(frame, sel.Y); frame.Width = sel.Width; frame.Height = sel.Height;
        }
    }
}
