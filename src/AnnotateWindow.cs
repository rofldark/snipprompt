using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Drawing = System.Drawing;

namespace SnipPrompt
{
    /// Annotation step (only when Shift was held): the capture stays where it was, a toolbar appears
    /// next to it. Rectangle, arrow, highlighter, text; Ctrl+Z undo; Enter continues; Esc cancels.
    /// The result has the marks burned in at the original pixel size.
    sealed class AnnotateWindow
    {
        enum Tool { Rect, Arrow, Marker, Text }

        static readonly Color[] Colors =
        {
            Color.FromRgb(0xef, 0x44, 0x44), Color.FromRgb(0xfa, 0xcc, 0x15), Color.FromRgb(0x3b, 0x82, 0xf6),
            Color.FromRgb(0x22, 0xc5, 0x5e), Color.FromRgb(0x11, 0x18, 0x27)
        };
        static readonly Color MarkerColor = Color.FromRgb(0xfa, 0xcc, 0x15);

        readonly Window w;
        readonly Canvas canvas;
        readonly Drawing.Bitmap source;
        readonly Drawing.Rectangle area;
        readonly Palette p;
        readonly double scale;
        readonly List<UIElement> marks = new List<UIElement>();
        readonly List<Border> toolButtons = new List<Border>();
        readonly List<Border> colorButtons = new List<Border>();
        Tool tool = Tool.Rect;
        Color color = Colors[0];
        Point dragStart;
        UIElement current;
        Polyline stroke;
        TextBox editing;
        Drawing.Bitmap result;

        public static Drawing.Bitmap Run(Drawing.Bitmap img, Drawing.Rectangle area)
        {
            var a = new AnnotateWindow(img, area);
            a.w.ShowDialog();
            return a.result;
        }

        AnnotateWindow(Drawing.Bitmap img, Drawing.Rectangle area)
        {
            source = img;
            this.area = area;
            p = Theme.Current();
            scale = MonitorScale(area);

            w = new Window
            {
                WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent,
                ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Topmost = true, SizeToContent = SizeToContent.WidthAndHeight,
                FontFamily = Theme.Font, FontSize = 13, Foreground = p.Text, UseLayoutRounding = true
            };

            double cw = img.Width / scale, ch = img.Height / scale;
            canvas = new Canvas { Width = cw, Height = ch, ClipToBounds = true, Cursor = Cursors.Cross, Background = Brushes.Transparent };
            var image = new Image { Source = ToSource(img), Width = cw, Height = ch, Stretch = Stretch.Fill };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
            canvas.Children.Add(image);
            canvas.MouseLeftButtonDown += OnDown;
            canvas.MouseMove += OnMove;
            canvas.MouseLeftButtonUp += OnUp;

            var frame = new Border { BorderBrush = Brushes.White, BorderThickness = new Thickness(1), Child = canvas, HorizontalAlignment = HorizontalAlignment.Left };

            // Toolbar below the capture if there is room on the monitor, else above, else inside at the bottom.
            var work = System.Windows.Forms.Screen.FromRectangle(area).WorkingArea;
            int barPx = (int)Math.Ceiling(64 * scale);
            bool below = area.Bottom + barPx <= work.Bottom;
            bool above = !below && area.Top - barPx >= work.Top;
            var bar = Toolbar();
            var grid = new Grid();
            if (below || above)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetRow(frame, below ? 0 : 1);
                Grid.SetRow(bar, below ? 1 : 0);
                bar.Margin = below ? new Thickness(0, 10, 0, 8) : new Thickness(0, 8, 0, 10);
            }
            else
            {
                bar.VerticalAlignment = VerticalAlignment.Bottom;
                bar.Margin = new Thickness(0, 0, 0, 12);
            }
            bar.HorizontalAlignment = HorizontalAlignment.Center;
            grid.Children.Add(frame);
            grid.Children.Add(bar);
            w.Content = grid;

            w.PreviewKeyDown += OnKey;
            w.Loaded += delegate
            {
                var h = new WindowInteropHelper(w).Handle;
                int y = above ? area.Top - barPx - 1 : area.Top - 1;
                SetWindowPos(h, IntPtr.Zero, area.Left - 1, y, 0, 0, 0x0001 | 0x0004);
                w.Activate();
                Keyboard.Focus(w);
                // After layout (and a possible DPI change on another monitor) move the window so the
                // image sits exactly on the captured pixels.
                w.Dispatcher.BeginInvoke(new Action(() => AlignToArea(h)), System.Windows.Threading.DispatcherPriority.Background);
            };
        }

        void AlignToArea(IntPtr h)
        {
            if (PresentationSource.FromVisual(canvas) == null) return;
            Point pt = canvas.PointToScreen(new Point(0, 0));
            int dx = area.Left - (int)Math.Round(pt.X), dy = area.Top - (int)Math.Round(pt.Y);
            if (dx == 0 && dy == 0) return;
            RECT r; GetWindowRect(h, out r);
            SetWindowPos(h, IntPtr.Zero, r.Left + dx, r.Top + dy, 0, 0, 0x0001 | 0x0004 | 0x0010);
        }

        // ---------- toolbar ----------

        Border Toolbar()
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4) };
            panel.Children.Add(ToolButton("▭", Strings.T("anno.rect"), Tool.Rect));
            panel.Children.Add(ToolButton("↗", Strings.T("anno.arrow"), Tool.Arrow));
            panel.Children.Add(ToolButton("▬", Strings.T("anno.marker"), Tool.Marker));
            panel.Children.Add(ToolButton("T", Strings.T("anno.text"), Tool.Text));
            panel.Children.Add(Separator());
            foreach (var c in Colors) panel.Children.Add(ColorButton(c));
            panel.Children.Add(Separator());
            panel.Children.Add(Button("↶", Strings.T("anno.undo"), Undo));

            var next = new Border
            {
                Background = p.Accent, CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 0, 10, 0), Height = 32,
                Margin = new Thickness(6, 0, 0, 0), Cursor = Cursors.Hand
            };
            var np = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            np.Children.Add(new TextBlock { Text = Strings.T("anno.next"), Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
            np.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)), CornerRadius = new CornerRadius(4), Padding = new Thickness(5, 0, 5, 1),
                Child = new TextBlock { Text = "Enter", FontSize = 11, Foreground = Brushes.White }
            });
            next.Child = np;
            next.MouseLeftButtonUp += delegate { Finish(); };
            panel.Children.Add(next);

            SetTool(Tool.Rect);
            SetColor(Colors[0]);
            return new Border
            {
                Background = p.Win, BorderBrush = p.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10),
                Effect = new DropShadowEffect { Color = p.ShadowColor, BlurRadius = 24, ShadowDepth = 4, Direction = 270, Opacity = p.Dark ? 0.5 : 0.22 },
                Child = panel
            };
        }

        Border Button(string glyph, string tip, Action click)
        {
            var b = new Border
            {
                Width = 34, Height = 32, CornerRadius = new CornerRadius(6), Background = Brushes.Transparent, Cursor = Cursors.Hand, ToolTip = tip,
                Child = new TextBlock { Text = glyph, FontSize = 15, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = p.Text }
            };
            b.MouseEnter += delegate { if (b.Tag == null) b.Background = p.Hover; };
            b.MouseLeave += delegate { if (b.Tag == null) b.Background = Brushes.Transparent; };
            b.MouseLeftButtonUp += delegate { click(); };
            return b;
        }

        Border ToolButton(string glyph, string tip, Tool t)
        {
            var b = Button(glyph, tip, () => SetTool(t));
            b.DataContext = t;
            toolButtons.Add(b);
            return b;
        }

        Border ColorButton(Color c)
        {
            var dot = new Ellipse { Width = 16, Height = 16, Fill = new SolidColorBrush(c), StrokeThickness = 2 };
            var b = new Border
            {
                Width = 28, Height = 32, CornerRadius = new CornerRadius(6), Background = Brushes.Transparent, Cursor = Cursors.Hand,
                ToolTip = Strings.T("anno.color"), DataContext = c, Child = dot
            };
            b.MouseLeftButtonUp += delegate { SetColor(c); };
            colorButtons.Add(b);
            return b;
        }

        UIElement Separator()
        {
            return new Border { Width = 1, Height = 20, Background = p.Line, Margin = new Thickness(5, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center };
        }

        void SetTool(Tool t)
        {
            CommitText();
            tool = t;
            foreach (var b in toolButtons)
            {
                bool on = (Tool)b.DataContext == t;
                b.Tag = on ? (object)true : null;
                b.Background = on ? p.AccentSoft : Brushes.Transparent;
                ((TextBlock)b.Child).Foreground = on ? p.Accent : p.Text;
            }
            canvas.Cursor = t == Tool.Text ? Cursors.IBeam : Cursors.Cross;
        }

        void SetColor(Color c)
        {
            color = c;
            foreach (var b in colorButtons)
                ((Ellipse)b.Child).Stroke = (Color)b.DataContext == c ? p.Text : Brushes.Transparent;
        }

        // ---------- drawing ----------

        Brush ColorBrush { get { return new SolidColorBrush(color); } }

        void OnDown(object sender, MouseButtonEventArgs e)
        {
            Point pt = e.GetPosition(canvas);
            if (tool == Tool.Text) { CommitText(); StartText(pt); e.Handled = true; return; }
            CommitText();
            dragStart = pt;
            canvas.CaptureMouse();
            if (tool == Tool.Rect)
            {
                current = new Rectangle { Stroke = ColorBrush, StrokeThickness = 3, RadiusX = 3, RadiusY = 3 };
                Canvas.SetLeft(current, pt.X); Canvas.SetTop(current, pt.Y);
            }
            else if (tool == Tool.Arrow)
            {
                current = new System.Windows.Shapes.Path { Stroke = ColorBrush, StrokeThickness = 3, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round };
            }
            else
            {
                stroke = new Polyline
                {
                    Stroke = new SolidColorBrush(MarkerColor), Opacity = 0.42, StrokeThickness = 16,
                    StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round
                };
                stroke.Points.Add(pt);
                current = stroke;
            }
            canvas.Children.Add(current);
            marks.Add(current);
        }

        void OnMove(object sender, MouseEventArgs e)
        {
            if (current == null || !canvas.IsMouseCaptured) return;
            Point pt = e.GetPosition(canvas);
            var rect = current as Rectangle;
            if (rect != null)
            {
                Canvas.SetLeft(rect, Math.Min(pt.X, dragStart.X)); Canvas.SetTop(rect, Math.Min(pt.Y, dragStart.Y));
                rect.Width = Math.Abs(pt.X - dragStart.X); rect.Height = Math.Abs(pt.Y - dragStart.Y);
            }
            var path = current as System.Windows.Shapes.Path;
            if (path != null) path.Data = ArrowGeometry(dragStart, pt);
            if (stroke != null) stroke.Points.Add(pt);
        }

        void OnUp(object sender, MouseButtonEventArgs e)
        {
            if (current == null) return;
            canvas.ReleaseMouseCapture();
            Point pt = e.GetPosition(canvas);
            if ((pt - dragStart).Length < 3 && stroke == null) { canvas.Children.Remove(current); marks.Remove(current); }
            current = null;
            stroke = null;
        }

        static Geometry ArrowGeometry(Point from, Point to)
        {
            var g = new StreamGeometry();
            Vector v = to - from;
            double len = v.Length;
            using (var c = g.Open())
            {
                c.BeginFigure(from, false, false);
                c.LineTo(to, true, true);
                if (len > 4)
                {
                    v.Normalize();
                    double head = Math.Min(16, len * 0.5);
                    var n = new Vector(-v.Y, v.X);
                    c.BeginFigure(to - v * head + n * head * 0.6, false, false);
                    c.LineTo(to, true, true);
                    c.LineTo(to - v * head - n * head * 0.6, true, true);
                }
            }
            g.Freeze();
            return g;
        }

        void StartText(Point pt)
        {
            bool light = color == Colors[1];
            editing = new TextBox
            {
                MinWidth = 80, FontSize = 14, Padding = new Thickness(6, 3, 6, 3), BorderThickness = new Thickness(0),
                Background = ColorBrush, Foreground = light ? Brushes.Black : Brushes.White, CaretBrush = light ? Brushes.Black : Brushes.White
            };
            Canvas.SetLeft(editing, pt.X); Canvas.SetTop(editing, pt.Y - 12);
            canvas.Children.Add(editing);
            editing.Focus();
        }

        void CommitText()
        {
            if (editing == null) return;
            var box = editing;
            editing = null;
            canvas.Children.Remove(box);
            string text = Settings.CleanTemplate(box.Text);
            if (text == null) return;
            var label = new Border
            {
                Background = box.Background, CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 3, 6, 3),
                Child = new TextBlock { Text = text, FontSize = 14, Foreground = box.Foreground, FontFamily = Theme.Font }
            };
            Canvas.SetLeft(label, Canvas.GetLeft(box)); Canvas.SetTop(label, Canvas.GetTop(box));
            canvas.Children.Add(label);
            marks.Add(label);
            Keyboard.Focus(w);
        }

        void Undo()
        {
            if (editing != null) { canvas.Children.Remove(editing); editing = null; Keyboard.Focus(w); return; }
            if (marks.Count == 0) return;
            var last = marks[marks.Count - 1];
            marks.RemoveAt(marks.Count - 1);
            canvas.Children.Remove(last);
        }

        void OnKey(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (editing != null) { canvas.Children.Remove(editing); editing = null; Keyboard.Focus(w); }
                else w.Close();
                e.Handled = true; return;
            }
            if (e.Key == Key.Enter)
            {
                if (editing != null) CommitText(); else Finish();
                e.Handled = true; return;
            }
            if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) != 0 && editing == null) { Undo(); e.Handled = true; }
        }

        void Finish()
        {
            CommitText();
            // Render at device resolution so the result keeps the original pixel size.
            // Drawn through a VisualBrush so the canvas' offset inside the window does not shift the result.
            var rtb = new RenderTargetBitmap(source.Width, source.Height, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
                dc.DrawRectangle(new VisualBrush(canvas) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top },
                    null, new Rect(0, 0, canvas.Width, canvas.Height));
            rtb.Render(dv);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            var ms = new MemoryStream();
            enc.Save(ms);
            ms.Position = 0;
            result = new Drawing.Bitmap(ms);
            w.Close();
        }

        static BitmapSource ToSource(Drawing.Bitmap img)
        {
            using (var ms = new MemoryStream())
            {
                img.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Position = 0;
                var bi = new BitmapImage();
                bi.BeginInit(); bi.CacheOption = BitmapCacheOption.OnLoad; bi.StreamSource = ms; bi.EndInit(); bi.Freeze();
                return bi;
            }
        }

        /// DPI scale of the monitor that holds the capture (1.0 = 100 %).
        static double MonitorScale(Drawing.Rectangle area)
        {
            try
            {
                var center = new POINT { X = area.Left + area.Width / 2, Y = area.Top + area.Height / 2 };
                IntPtr mon = MonitorFromPoint(center, 2);
                uint dx, dy;
                if (GetDpiForMonitor(mon, 0, out dx, out dy) == 0) return dx / 96.0;
            }
            catch (Exception) { }
            return 1.0;
        }

        [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out RECT r);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(POINT pt, uint flags);
        [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr mon, int type, out uint dx, out uint dy);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    }
}
