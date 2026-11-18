using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SnipPrompt
{
    /// Settings window. Options are declared as data (one entry per option), so adding an option
    /// means adding one line to BuildOptions. Every change is saved and applied immediately.
    sealed class SettingsWindow
    {
        enum Kind { Switch, Choice, Hotkey }

        sealed class Option
        {
            public string Section, Label, Desc;
            public Kind Kind;
            public string[] Values, ValueLabels; // Choice: stored values and their text keys
            public Func<string> Get;
            public Action<string> Set;
        }

        static SettingsWindow open;

        readonly Window w;
        Palette p;
        string section = "nav.general";
        Border hotkeyBox;
        TextBlock hotkeyText;
        TextBlock hotkeyError;
        bool recording;

        static readonly string[] Sections = { "nav.general", "nav.capture", "nav.picker", "nav.templates", "nav.about" };

        public static void ShowSingle()
        {
            if (open != null) { open.w.Activate(); return; }
            open = new SettingsWindow();
            open.w.Show();
            open.w.Activate();
        }

        /// For --snapshots: one settings page without showing a window.
        public static FrameworkElement Demo(string sec)
        {
            var sw = new SettingsWindow(sec);
            var content = (FrameworkElement)sw.w.Content;
            sw.w.Content = null;
            var host = new System.Windows.Controls.Border { Width = 760, Height = 520, Background = sw.w.Background, Child = content };
            System.Windows.Documents.TextElement.SetForeground(host, sw.p.Text);
            System.Windows.Documents.TextElement.SetFontFamily(host, Theme.Font);
            return host;
        }

        SettingsWindow(string sec) : this() { section = sec; Build(); }

        SettingsWindow()
        {
            w = new Window
            {
                Width = 760, Height = 520, MinWidth = 640, MinHeight = 420, WindowStartupLocation = WindowStartupLocation.CenterScreen,
                FontFamily = Theme.Font, FontSize = 13, UseLayoutRounding = true, ResizeMode = ResizeMode.CanResize
            };
            TextOptions.SetTextFormattingMode(w, TextFormattingMode.Display);
            try
            {
                using (var s = typeof(App).Assembly.GetManifestResourceStream("SnipPrompt.icon.ico"))
                    w.Icon = System.Windows.Media.Imaging.BitmapFrame.Create(s, System.Windows.Media.Imaging.BitmapCreateOptions.None, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
            }
            catch (Exception) { }
            w.SourceInitialized += delegate { ApplyTitleBar(); };
            w.PreviewKeyDown += OnKey;
            w.Closed += delegate { if (recording) StopRecording(false); open = null; };
            w.Deactivated += delegate { if (recording) StopRecording(false); };
            Build();
        }

        // ---------- options as data ----------

        List<Option> BuildOptions()
        {
            var s = App.Settings;
            var o = new List<Option>();
            o.Add(new Option { Section = "nav.general", Label = "set.hotkey", Desc = "set.hotkey.desc", Kind = Kind.Hotkey });
            o.Add(new Option
            {
                Section = "nav.general", Label = "set.language", Desc = "set.language.desc", Kind = Kind.Choice,
                Values = Settings.Languages, ValueLabels = new[] { "lang.auto", "lang.de", "lang.en" },
                Get = () => s.Language, Set = v => { s.Language = v; App.ApplyLanguage(); App.RefreshTrayTexts(); Build(); }
            });
            o.Add(new Option
            {
                Section = "nav.general", Label = "set.theme", Desc = "set.theme.desc", Kind = Kind.Choice,
                Values = new[] { "light", "dark", "system" }, ValueLabels = new[] { "theme.light", "theme.dark", "theme.system" },
                Get = () => s.Theme, Set = v => { s.Theme = v; Build(); ApplyTitleBar(); }
            });
            o.Add(new Option
            {
                Section = "nav.general", Label = "set.autostart", Desc = "set.autostart.desc", Kind = Kind.Switch,
                Get = () => B(s.Autostart), Set = v => { s.Autostart = v == "1"; Autostart.Set(s.Autostart); }
            });

            o.Add(new Option
            {
                Section = "nav.capture", Label = "set.mode", Desc = "set.mode.desc", Kind = Kind.Choice,
                Values = Settings.Modes, ValueLabels = new[] { "mode.frame", "mode.window" },
                Get = () => s.CaptureMode, Set = v => s.CaptureMode = v
            });
            o.Add(new Option
            {
                Section = "nav.capture", Label = "set.delay", Desc = "set.delay.desc", Kind = Kind.Choice,
                Values = Settings.Delays.Select(d => d.ToString()).ToArray(), ValueLabels = new[] { "delay.off", "1 s", "2 s", "3 s", "5 s" },
                Get = () => s.DelaySeconds.ToString(), Set = v => s.DelaySeconds = int.Parse(v)
            });
            o.Add(new Option
            {
                Section = "nav.capture", Label = "set.shift", Desc = "set.shift.desc", Kind = Kind.Switch,
                Get = () => B(s.ShiftAnnotates), Set = v => s.ShiftAnnotates = v == "1"
            });
            o.Add(new Option
            {
                Section = "nav.capture", Label = "set.keep", Desc = "set.keep.desc", Kind = Kind.Choice,
                Values = Settings.KeepOptions.Select(d => d.ToString()).ToArray(), ValueLabels = new[] { "keep.none", "20", "100" },
                Get = () => s.KeepCopies.ToString(), Set = v => s.KeepCopies = int.Parse(v)
            });

            o.Add(new Option { Section = "nav.picker", Label = "set.preview", Desc = "set.preview.desc", Kind = Kind.Switch, Get = () => B(s.ShowPreview), Set = v => s.ShowPreview = v == "1" });
            o.Add(new Option { Section = "nav.picker", Label = "set.preselect", Desc = "set.preselect.desc", Kind = Kind.Switch, Get = () => B(s.PreselectLast), Set = v => s.PreselectLast = v == "1" });
            o.Add(new Option { Section = "nav.picker", Label = "set.direct", Desc = "set.direct.desc", Kind = Kind.Switch, Get = () => B(s.DirectWhenSingle), Set = v => s.DirectWhenSingle = v == "1" });
            o.Add(new Option { Section = "nav.picker", Label = "set.showTemplates", Desc = "set.showTemplates.desc", Kind = Kind.Switch, Get = () => B(s.ShowTemplates), Set = v => s.ShowTemplates = v == "1" });
            return o;
        }

        static string B(bool b) { return b ? "1" : "0"; }

        // ---------- layout ----------

        int generation;

        void Build()
        {
            generation++;
            p = Theme.Current();
            w.Title = Strings.T("settings.title");
            w.Background = p.Win;
            w.Foreground = p.Text;

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var nav = new StackPanel { Margin = new Thickness(8, 16, 8, 16) };
            var brand = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 0, 0, 18) };
            try
            {
                brand.Children.Add(new Image { Source = w.Icon, Width = 20, Height = 20, Margin = new Thickness(0, 0, 8, 0) });
            }
            catch (Exception) { }
            brand.Children.Add(new TextBlock { Text = Strings.T("app.name"), FontWeight = FontWeights.SemiBold, FontSize = 14, VerticalAlignment = VerticalAlignment.Center });
            nav.Children.Add(brand);
            foreach (var sec in Sections) nav.Children.Add(NavItem(sec));
            grid.Children.Add(new Border { Background = p.Win2, BorderBrush = p.Line, BorderThickness = new Thickness(0, 0, 1, 0), Child = nav });

            var pane = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };
            pane.Children.Add(new TextBlock { Text = Strings.T(section), FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 14) });
            if (section == "nav.templates") BuildTemplates(pane);
            else if (section == "nav.about") BuildAbout(pane);
            else foreach (var opt in BuildOptions().Where(x => x.Section == section)) pane.Children.Add(OptionRow(opt));

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = pane };
            Grid.SetColumn(scroll, 1);
            grid.Children.Add(scroll);
            w.Content = grid;
        }

        UIElement NavItem(string sec)
        {
            bool on = sec == section;
            var bar = new Border { Width = 3, CornerRadius = new CornerRadius(2), Background = p.Accent, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 8), Visibility = on ? Visibility.Visible : Visibility.Hidden };
            var text = new TextBlock { Text = Strings.T(sec), Margin = new Thickness(12, 8, 12, 8), FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal };
            var g = new Grid();
            g.Children.Add(text);
            g.Children.Add(bar);
            var b = new Border { CornerRadius = new CornerRadius(6), Background = on ? p.Hover : Brushes.Transparent, Child = g, Cursor = Cursors.Hand, Margin = new Thickness(0, 1, 0, 1) };
            b.MouseEnter += delegate { if (!on) b.Background = p.Hover; };
            b.MouseLeave += delegate { if (!on) b.Background = Brushes.Transparent; };
            b.MouseLeftButtonUp += delegate { if (recording) StopRecording(false); section = sec; Build(); };
            return b;
        }

        Border Card(UIElement left, UIElement right)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(left);
            if (right != null) { Grid.SetColumn(right, 1); g.Children.Add(right); }
            return new Border
            {
                BorderBrush = p.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Background = p.Win,
                Padding = new Thickness(14, 11, 14, 11), Margin = new Thickness(0, 0, 0, 8), Child = g
            };
        }

        UIElement Label(string label, string desc)
        {
            var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
            sp.Children.Add(new TextBlock { Text = Strings.T(label), FontWeight = FontWeights.SemiBold });
            if (desc != null) sp.Children.Add(new TextBlock { Text = Strings.T(desc), FontSize = 12, Foreground = p.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
            return sp;
        }

        UIElement OptionRow(Option o)
        {
            UIElement control;
            if (o.Kind == Kind.Switch) control = Switch(o);
            else if (o.Kind == Kind.Choice) control = Segmented(o);
            else control = HotkeyControl();
            var card = Card(Label(o.Label, o.Desc), control);
            if (o.Kind == Kind.Hotkey)
            {
                var sp = new StackPanel();
                sp.Children.Add(card);
                return sp;
            }
            return card;
        }

        UIElement Switch(Option o)
        {
            bool on = o.Get() == "1";
            var knobShift = new TranslateTransform(on ? 20 : 0, 0);
            var knob = new Border { Width = 14, Height = 14, CornerRadius = new CornerRadius(7), Background = Brushes.White, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(3, 0, 0, 0), RenderTransform = knobShift };
            var track = new Border { Width = 40, Height = 20, CornerRadius = new CornerRadius(10), Background = on ? p.Accent : p.Line, Child = knob, Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center };
            track.MouseLeftButtonUp += delegate
            {
                on = !on;
                track.Background = on ? p.Accent : p.Line;
                knobShift.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(on ? 20 : 0, new Duration(TimeSpan.FromMilliseconds(120))));
                o.Set(on ? "1" : "0");
                App.Settings.Save();
            };
            return track;
        }

        UIElement Segmented(Option o)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2) };
            var outer = new Border { Background = p.Win2, BorderBrush = p.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Child = panel, VerticalAlignment = VerticalAlignment.Center };
            var items = new List<Border>();
            Action<string> paint = cur =>
            {
                for (int i = 0; i < items.Count; i++)
                {
                    bool on = o.Values[i] == cur;
                    items[i].Background = on ? (p.Dark ? p.Hover : Brushes.White) : Brushes.Transparent;
                    items[i].Effect = on ? new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 3, ShadowDepth = 1, Opacity = 0.15 } : null;
                    ((TextBlock)items[i].Child).FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal;
                }
            };
            for (int i = 0; i < o.Values.Length; i++)
            {
                string label = o.ValueLabels[i].Contains(".") ? Strings.T(o.ValueLabels[i]) : o.ValueLabels[i];
                var b = new Border { CornerRadius = new CornerRadius(4), Padding = new Thickness(10, 4, 10, 5), Cursor = Cursors.Hand, Child = new TextBlock { Text = label, FontSize = 12 } };
                string value = o.Values[i];
                b.MouseLeftButtonUp += delegate
                {
                    if (o.Get() == value) return;
                    o.Set(value);
                    App.Settings.Save();
                    paint(value);
                };
                items.Add(b);
                panel.Children.Add(b);
            }
            paint(o.Get());
            return outer;
        }

        // ---------- hotkey ----------

        UIElement HotkeyControl()
        {
            hotkeyText = new TextBlock { Text = App.Settings.Hotkey, FontSize = 12 };
            hotkeyBox = new Border
            {
                BorderBrush = p.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Background = p.Win2,
                Padding = new Thickness(10, 5, 10, 6), Cursor = Cursors.Hand, Child = hotkeyText, HorizontalAlignment = HorizontalAlignment.Right
            };
            hotkeyBox.MouseLeftButtonUp += delegate { StartRecording(); };
            hotkeyError = new TextBlock { FontSize = 11, Foreground = p.Busy, Margin = new Thickness(0, 4, 0, 0), HorizontalAlignment = HorizontalAlignment.Right, Visibility = App.HotkeyOk ? Visibility.Collapsed : Visibility.Visible, Text = Strings.T("set.hotkey.taken") };
            var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(hotkeyBox);
            sp.Children.Add(hotkeyError);
            return sp;
        }

        void StartRecording()
        {
            recording = true;
            App.Hotkey.Unregister(); // otherwise pressing the current combination would start a capture
            hotkeyText.Text = Strings.T("set.hotkey.press");
            hotkeyBox.BorderBrush = p.Accent;
            hotkeyError.Visibility = Visibility.Collapsed;
        }

        void StopRecording(bool taken)
        {
            recording = false;
            App.HotkeyOk = App.Hotkey.Register(App.Settings.Hotkey);
            if (!App.HotkeyOk) taken = true;
            if (hotkeyText == null) return;
            hotkeyText.Text = App.Settings.Hotkey;
            hotkeyBox.BorderBrush = p.Line;
            hotkeyError.Visibility = taken ? Visibility.Visible : Visibility.Collapsed;
        }

        void OnKey(object sender, KeyEventArgs e)
        {
            if (!recording) return;
            e.Handled = true;
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.Escape) { StopRecording(false); return; }
            if (key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift || key == Key.LWin || key == Key.RWin) return;

            uint mods = 0;
            var m = Keyboard.Modifiers;
            if ((m & ModifierKeys.Control) != 0) mods |= Logic.ModCtrl;
            if ((m & ModifierKeys.Alt) != 0) mods |= Logic.ModAlt;
            if ((m & ModifierKeys.Shift) != 0) mods |= Logic.ModShift;
            if ((m & ModifierKeys.Windows) != 0) mods |= Logic.ModWin;
            string text = Logic.FormatHotkey(mods, KeyInterop.VirtualKeyFromKey(key));
            uint pm; int pvk;
            if (!Logic.TryParseHotkey(text, out pm, out pvk)) return; // no modifier or unsupported key: keep waiting

            if (!App.Hotkey.IsFree(text)) { StopRecording(true); return; }
            App.Settings.Hotkey = text;
            App.Settings.Save();
            StopRecording(false);
        }

        // ---------- templates ----------

        void BuildTemplates(StackPanel pane)
        {
            var s = App.Settings;
            pane.Children.Add(new TextBlock { Text = Strings.T("tpl.desc"), Foreground = p.Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });
            for (int i = 0; i < s.Templates.Count; i++)
            {
                int idx = i;
                var box = new TextBox
                {
                    Text = s.Templates[i], BorderThickness = new Thickness(0), Background = Brushes.Transparent, Foreground = p.Text,
                    CaretBrush = p.Text, VerticalAlignment = VerticalAlignment.Center, AcceptsReturn = false, FontSize = 13
                };
                int gen = generation;
                box.TextChanged += delegate
                {
                    if (gen != generation || idx >= s.Templates.Count) return;
                    string clean = Settings.CleanTemplate(box.Text);
                    if (clean != null && clean != s.Templates[idx]) { s.Templates[idx] = clean; s.Save(); }
                };
                box.LostKeyboardFocus += delegate
                {
                    if (gen != generation || idx >= s.Templates.Count) return; // list was rebuilt meanwhile
                    string clean = Settings.CleanTemplate(box.Text);
                    if (clean == null) { s.Templates.RemoveAt(idx); s.Save(); w.Dispatcher.BeginInvoke(new Action(Build)); return; }
                    box.Text = clean;
                };
                var tools = new StackPanel { Orientation = Orientation.Horizontal };
                if (i > 0) tools.Children.Add(IconButton("↑", Strings.T("tpl.up"), () => { var t = s.Templates[idx]; s.Templates.RemoveAt(idx); s.Templates.Insert(idx - 1, t); s.Save(); Build(); }));
                tools.Children.Add(IconButton("✕", Strings.T("tpl.delete"), () => { s.Templates.RemoveAt(idx); s.Save(); Build(); }));
                pane.Children.Add(Card(box, tools));
            }
            var add = new TextBlock { Text = Strings.T("tpl.add"), Foreground = p.Accent, Cursor = Cursors.Hand, Margin = new Thickness(2, 6, 0, 0) };
            add.MouseLeftButtonUp += delegate { s.Templates.Add(Strings.T("tpl.new")); s.Save(); Build(); };
            pane.Children.Add(add);
        }

        Border IconButton(string glyph, string tip, Action click)
        {
            var b = new Border
            {
                Width = 28, Height = 26, CornerRadius = new CornerRadius(5), Background = Brushes.Transparent, Cursor = Cursors.Hand, ToolTip = tip,
                Child = new TextBlock { Text = glyph, Foreground = p.Muted, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            };
            b.MouseEnter += delegate { b.Background = p.Hover; };
            b.MouseLeave += delegate { b.Background = Brushes.Transparent; };
            b.MouseLeftButtonUp += delegate { click(); };
            return b;
        }

        // ---------- about ----------

        void BuildAbout(StackPanel pane)
        {
            var v = typeof(App).Assembly.GetName().Version;
            pane.Children.Add(Card(Label("about.version", null), new TextBlock { Text = v.Major + "." + v.Minor + "." + v.Build, VerticalAlignment = VerticalAlignment.Center }));
            var folder = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
            folder.Children.Add(new TextBlock { Text = Strings.T("about.folder"), FontWeight = FontWeights.SemiBold });
            folder.Children.Add(new TextBlock { Text = Storage.Folder, FontSize = 12, Foreground = p.Muted, TextTrimming = TextTrimming.CharacterEllipsis });
            var open = new Border
            {
                BorderBrush = p.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Background = p.Win2, Padding = new Thickness(12, 5, 12, 6),
                Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center, Child = new TextBlock { Text = Strings.T("about.open"), FontSize = 12 }
            };
            open.MouseLeftButtonUp += delegate { Storage.OpenFolder(); };
            pane.Children.Add(Card(folder, open));
        }

        // ---------- title bar ----------

        void ApplyTitleBar()
        {
            var h = new WindowInteropHelper(w).Handle;
            if (h == IntPtr.Zero) return;
            int dark = Theme.Current().Dark ? 1 : 0;
            DwmSetWindowAttribute(h, 20, ref dark, 4); // DWMWA_USE_IMMERSIVE_DARK_MODE
        }

        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    }
}
