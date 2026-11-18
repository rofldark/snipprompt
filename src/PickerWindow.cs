using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Drawing = System.Drawing;

namespace SnipPrompt
{
    sealed class PickResult
    {
        public TabRef Tab;
        public string Template; // null = image only
    }

    /// Modern session picker: preview, sessions with live status, optional templates.
    sealed class PickerWindow
    {
        sealed class Row
        {
            public TabRef Tab;
            public Border Box;
            public Border Bar;
            public Ellipse Dot;
            public TextBlock State;
            public bool Closed;
            public bool Claude;
        }

        readonly Palette p;
        readonly Settings s;
        readonly Window w;
        readonly List<Row> rows = new List<Row>();
        readonly List<Border> chips = new List<Border>();
        readonly List<string> chipTexts = new List<string>();
        int selected;
        int chip;
        PickResult result;

        public static PickResult Choose(List<TabRef> tabs, Drawing.Bitmap img, Drawing.Rectangle area, Settings s)
        {
            var pw = new PickerWindow(tabs, img, area, s);
            pw.w.ShowDialog();
            return pw.result;
        }

        /// For --snapshots: builds the content without showing a window.
        public static FrameworkElement Demo(List<TabRef> tabs, Drawing.Bitmap img, Settings s)
        {
            var pw = new PickerWindow(tabs, img, new Drawing.Rectangle(0, 0, 100, 100), s);
            var content = (FrameworkElement)pw.w.Content;
            pw.w.Content = null;
            content.RenderTransform = null;
            System.Windows.Documents.TextElement.SetForeground(content, pw.p.Text);
            System.Windows.Documents.TextElement.SetFontFamily(content, Theme.Font);
            return content;
        }

        PickerWindow(List<TabRef> tabs, Drawing.Bitmap img, Drawing.Rectangle area, Settings settings)
        {
            s = settings;
            p = Theme.Current();
            Border card;
            w = Theme.NewPopup(p, out card);
            w.Title = Strings.T("app.name");

            var root = new StackPanel { Width = 460 };
            if (s.ShowPreview) root.Children.Add(Preview(img));

            root.Children.Add(new TextBlock
            {
                Text = Strings.T("picker.sendTo"), FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = p.Muted,
                Margin = new Thickness(18, 14, 18, 6)
            });

            var list = new StackPanel { Margin = new Thickness(8, 0, 8, 0) };
            var windows = new List<IntPtr>();
            foreach (var t in tabs) if (!windows.Contains(t.Hwnd)) windows.Add(t.Hwnd);
            var titles = tabs.Select(t => t.Title).ToList();
            int preferred = s.PreselectLast ? Logic.PreferredIndex(titles, tabs.Select(t => t.Hwnd.ToInt64()).ToList(), s.LastName, s.LastHwnd) : 0;
            bool lastMatched = s.PreselectLast && !string.IsNullOrEmpty(s.LastName) && Logic.DisplayName(titles[preferred]) == s.LastName;
            for (int i = 0; i < tabs.Count; i++)
                list.Children.Add(MakeRow(i, tabs[i], windows.Count > 1 ? windows.IndexOf(tabs[i].Hwnd) + 1 : 0, lastMatched && i == preferred).Box);
            root.Children.Add(list);

            chipTexts.Add(null);
            if (s.ShowTemplates) chipTexts.AddRange(s.Templates);
            if (chipTexts.Count > 1) root.Children.Add(Chips());

            root.Children.Add(Footer());
            card.Child = root;

            Select(preferred);
            w.PreviewKeyDown += OnKey;
            w.Loaded += delegate { Place(area); w.Activate(); w.Focus(); Keyboard.Focus(w); };

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            timer.Tick += delegate { RefreshStatus(); };
            w.Loaded += delegate { timer.Start(); };
            w.Closed += delegate { timer.Stop(); };
        }

        // ---------- building blocks ----------

        UIElement Preview(Drawing.Bitmap img)
        {
            BitmapSource src = ToSource(img);
            const double maxW = 430, maxH = 240;
            double scale = Math.Min(1.0, Math.Min(maxW / img.Width, maxH / img.Height));
            var image = new Image
            {
                Source = src, Stretch = Stretch.Uniform,
                Width = Math.Max(1, img.Width * scale), Height = Math.Max(1, img.Height * scale),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

            var grid = new Grid { Height = Math.Max(120, Math.Min(maxH, img.Height * scale)) + 16 };
            grid.Children.Add(image);
            var dim = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)), CornerRadius = new CornerRadius(9),
                Padding = new Thickness(7, 1, 7, 2), Margin = new Thickness(8),
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom,
                Child = new TextBlock { Text = img.Width + " × " + img.Height, Foreground = Brushes.White, FontSize = 11 }
            };
            grid.Children.Add(dim);
            return new Border
            {
                Margin = new Thickness(14, 14, 14, 0), CornerRadius = new CornerRadius(8), BorderBrush = p.Line,
                BorderThickness = new Thickness(1), Background = p.Win2, ClipToBounds = true, Child = grid
            };
        }

        static BitmapSource ToSource(Drawing.Bitmap img)
        {
            using (var ms = new MemoryStream())
            {
                img.Save(ms, ImageFormat.Png);
                ms.Position = 0;
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.StreamSource = ms;
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
        }

        Row MakeRow(int index, TabRef tab, int windowNo, bool isLast)
        {
            var r = new Row { Tab = tab, Claude = Logic.IsClaudeTitle(tab.Title) };
            var g = new Grid { Margin = new Thickness(10, 8, 10, 8) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var num = new Border
            {
                Width = 22, Height = 22, CornerRadius = new CornerRadius(6), Background = p.Chip, Margin = new Thickness(0, 0, 12, 0),
                Child = new TextBlock
                {
                    Text = index < 9 ? (index + 1).ToString() : "", FontSize = 12, Foreground = p.Muted,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
                }
            };
            g.Children.Add(num);

            var name = new TextBlock { FontSize = 14, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            name.Inlines.Add(new System.Windows.Documents.Run(Logic.DisplayName(tab.Title)));
            if (windowNo > 0)
                name.Inlines.Add(new System.Windows.Documents.Run("  ·  " + Strings.T("picker.window", windowNo)) { Foreground = p.Muted, FontSize = 12 });
            var namePanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            namePanel.Children.Add(name);
            if (isLast)
                namePanel.Children.Add(new Border
                {
                    BorderBrush = p.Accent, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9),
                    Padding = new Thickness(6, 0, 6, 1), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock { Text = Strings.T("picker.last"), FontSize = 11, Foreground = p.Accent }
                });
            Grid.SetColumn(namePanel, 1);
            g.Children.Add(namePanel);

            var state = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            r.Dot = new Ellipse { Width = 8, Height = 8, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
            r.State = new TextBlock { FontSize = 12, Foreground = p.Muted };
            state.Children.Add(r.Dot);
            state.Children.Add(r.State);
            Grid.SetColumn(state, 2);
            g.Children.Add(state);

            r.Bar = new Border { Width = 3, CornerRadius = new CornerRadius(2), Background = p.Accent, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 9, 0, 9), Visibility = Visibility.Hidden };
            var layer = new Grid();
            layer.Children.Add(g);
            layer.Children.Add(r.Bar);
            r.Box = new Border { CornerRadius = new CornerRadius(8), Background = Brushes.Transparent, Child = layer, Cursor = Cursors.Hand };

            int i = index;
            r.Box.MouseEnter += delegate { if (i != selected && !r.Closed) r.Box.Background = p.Hover; };
            r.Box.MouseLeave += delegate { if (i != selected) r.Box.Background = Brushes.Transparent; };
            r.Box.MouseLeftButtonDown += (o, e) =>
            {
                if (r.Closed) return;
                Select(i);
                if (e.ClickCount >= 2) Accept();
            };
            rows.Add(r);
            UpdateState(r, tab.Title, true);
            return r;
        }

        void UpdateState(Row r, string title, bool alive)
        {
            if (!alive)
            {
                r.Closed = true;
                r.Box.Opacity = 0.45;
                r.Dot.Fill = p.Line;
                r.Dot.BeginAnimation(UIElement.OpacityProperty, null);
                r.State.Text = Strings.T("picker.closed");
                return;
            }
            if (!r.Claude)
            {
                r.Dot.Fill = p.Line;
                r.State.Text = Strings.T("picker.other");
                return;
            }
            bool busy = Logic.IsBusy(title);
            string text = Strings.T(busy ? "picker.busy" : "picker.idle");
            if (r.State.Text == text) return;
            r.State.Text = text;
            r.Dot.Fill = busy ? p.Busy : p.Ok;
            if (busy)
                r.Dot.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, 0.35, new Duration(TimeSpan.FromMilliseconds(600)))
                { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
            else
                r.Dot.BeginAnimation(UIElement.OpacityProperty, null);
        }

        /// Live status: only dot and text change; order and digits stay fixed.
        void RefreshStatus()
        {
            foreach (var r in rows)
            {
                if (r.Closed) continue;
                string title;
                bool alive = TerminalTabs.TryTitle(r.Tab, out title);
                if (alive) r.Tab.Title = title;
                UpdateState(r, title, alive);
            }
            if (rows[selected].Closed) Move(1);
        }

        UIElement Chips()
        {
            var wrap = new WrapPanel { Margin = new Thickness(18, 12, 18, 2) };
            for (int i = 0; i < chipTexts.Count; i++)
            {
                var c = new Border
                {
                    CornerRadius = new CornerRadius(14), Padding = new Thickness(11, 4, 11, 5), Margin = new Thickness(0, 0, 6, 6),
                    BorderThickness = new Thickness(1), Cursor = Cursors.Hand,
                    Child = new TextBlock { Text = chipTexts[i] ?? Strings.T("picker.onlyImage"), FontSize = 12, MaxWidth = 380, TextTrimming = TextTrimming.CharacterEllipsis }
                };
                int idx = i;
                c.MouseLeftButtonDown += delegate { SetChip(idx); };
                chips.Add(c);
                wrap.Children.Add(c);
            }
            SetChip(0);
            return wrap;
        }

        void SetChip(int i)
        {
            chip = i;
            for (int k = 0; k < chips.Count; k++)
            {
                bool on = k == i;
                chips[k].Background = on ? p.AccentSoft : p.Chip;
                chips[k].BorderBrush = on ? p.Accent : Brushes.Transparent;
                ((TextBlock)chips[k].Child).Foreground = on ? p.Accent : p.Text;
            }
        }

        UIElement Footer()
        {
            var f = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
            Action<string[], string> hint = (keys, text) =>
            {
                var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 11, 2) };
                foreach (var k in keys) sp.Children.Add(Theme.Kbd(p, k));
                sp.Children.Add(new TextBlock { Text = " " + text, FontSize = 12, Foreground = p.Muted, VerticalAlignment = VerticalAlignment.Center });
                f.Children.Add(sp);
            };
            hint(new[] { "↑", "↓" }, Strings.T("picker.hintSelect"));
            hint(new[] { "1–9" }, Strings.T("picker.hintDirect"));
            if (chipTexts.Count > 1) hint(new[] { "Tab" }, Strings.T("picker.hintTemplate"));
            hint(new[] { "Esc" }, Strings.T("picker.hintCancel"));

            var g = new Grid { Margin = new Thickness(18, 10, 14, 12) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(f);
            var send = SendButton();
            Grid.SetColumn(send, 1);
            g.Children.Add(send);
            return new Border { BorderBrush = p.Line, BorderThickness = new Thickness(0, 1, 0, 0), Margin = new Thickness(0, 10, 0, 0), Child = g };
        }

        /// Primary button, same as Enter.
        UIElement SendButton()
        {
            var b = new Border
            {
                Background = p.Accent, CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 0, 8, 0), Height = 32,
                Margin = new Thickness(10, 0, 0, 0), Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center
            };
            var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(new TextBlock { Text = Strings.T("picker.send"), Foreground = p.OnAccent, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
            sp.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)), CornerRadius = new CornerRadius(4), Padding = new Thickness(5, 0, 5, 1),
                Child = new TextBlock { Text = "Enter", FontSize = 11, Foreground = p.OnAccent }
            });
            b.Child = sp;
            b.MouseEnter += delegate { b.Opacity = 0.9; };
            b.MouseLeave += delegate { b.Opacity = 1; };
            b.MouseLeftButtonUp += delegate { Accept(); };
            return b;
        }

        // ---------- behaviour ----------

        void Select(int i)
        {
            if (i < 0 || i >= rows.Count) return;
            if (rows[i].Closed) { int first = rows.FindIndex(r => !r.Closed); if (first < 0) return; i = first; }
            selected = i;
            for (int k = 0; k < rows.Count; k++)
            {
                bool on = k == i;
                rows[k].Box.Background = on ? p.AccentSoft : Brushes.Transparent;
                rows[k].Bar.Visibility = on ? Visibility.Visible : Visibility.Hidden;
            }
        }

        void Move(int dir)
        {
            for (int k = 1; k <= rows.Count; k++)
            {
                int i = selected + dir * k;
                if (i < 0 || i >= rows.Count) return;
                if (!rows[i].Closed) { Select(i); return; }
            }
        }

        void OnKey(object sender, KeyEventArgs e)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            e.Handled = true;
            if (e.IsRepeat && key != Key.Up && key != Key.Down) return; // a held Enter from the previous step must not confirm
            if (key == Key.Escape) { w.Close(); return; }
            if (key == Key.Enter) { Accept(); return; }
            if (key == Key.Down) { Move(1); return; }
            if (key == Key.Up) { Move(-1); return; }
            if (key == Key.Tab && chips.Count > 1)
            {
                bool back = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
                SetChip((chip + (back ? chips.Count - 1 : 1)) % chips.Count);
                return;
            }
            int n = -1;
            if (key >= Key.D1 && key <= Key.D9) n = key - Key.D1;
            if (key >= Key.NumPad1 && key <= Key.NumPad9) n = key - Key.NumPad1;
            if (n >= 0 && n < rows.Count && !rows[n].Closed) { Select(n); Accept(); return; }
            e.Handled = false;
        }

        void Accept()
        {
            if (rows[selected].Closed) return;
            result = new PickResult { Tab = rows[selected].Tab, Template = chip > 0 ? chipTexts[chip] : null };
            w.Close();
        }

        /// Places the window next to the captured area (right, else left, else over it), inside its monitor.
        /// Works in physical pixels, so mixed-DPI setups are handled by Windows.
        void Place(Drawing.Rectangle area)
        {
            var hwnd = new WindowInteropHelper(w).Handle;
            RECT wr;
            GetWindowRect(hwnd, out wr);
            int width = wr.Right - wr.Left, height = wr.Bottom - wr.Top;
            var work = System.Windows.Forms.Screen.FromRectangle(area).WorkingArea;
            int x, y;
            if (area.Right + width <= work.Right) x = area.Right - 10;
            else if (area.Left - width >= work.Left) x = area.Left - width + 10;
            else x = area.Left + (area.Width - width) / 2;
            y = area.Top + (area.Height - height) / 2;
            x = Math.Max(work.Left, Math.Min(x, work.Right - width));
            y = Math.Max(work.Top, Math.Min(y, work.Bottom - height));
            SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, 0x0001 | 0x0004 | 0x0010); // NOSIZE | NOZORDER | NOACTIVATE
        }

        [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out RECT r);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    }
}
