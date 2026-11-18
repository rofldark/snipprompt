using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Microsoft.Win32;

namespace SnipPrompt
{
    /// Colors of the current theme. Read fresh every time a window opens, so a change of the
    /// setting or of the Windows theme applies to the next window.
    sealed class Palette
    {
        public bool Dark;
        public Brush Win, Win2, Line, Text, Muted, Accent, AccentSoft, Hover, Chip, Ok, Busy, OnAccent;
        public Color ShadowColor;
    }

    static class Theme
    {
        static Brush B(string hex)
        {
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();
            return b;
        }

        public static bool SystemIsDark()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object v = k == null ? null : k.GetValue("AppsUseLightTheme");
                    return v is int && (int)v == 0;
                }
            }
            catch (Exception) { return false; }
        }

        public static Palette Current()
        {
            string t = App.Settings.Theme;
            bool dark = t == "dark" || (t == "system" && SystemIsDark());
            return dark
                ? new Palette
                {
                    Dark = true, Win = B("#23262c"), Win2 = B("#1c1f24"), Line = B("#343841"), Text = B("#e7e9ec"), Muted = B("#9aa3ad"),
                    Accent = B("#5b8def"), AccentSoft = B("#2a3550"), Hover = B("#2c3038"), Chip = B("#2c3038"),
                    Ok = B("#22c55e"), Busy = B("#f59e0b"), OnAccent = Brushes.White, ShadowColor = Colors.Black
                }
                : new Palette
                {
                    Dark = false, Win = B("#ffffff"), Win2 = B("#f6f7f9"), Line = B("#e3e6ea"), Text = B("#1b1f24"), Muted = B("#6b7280"),
                    Accent = B("#2563eb"), AccentSoft = B("#e8f0fe"), Hover = B("#f1f4f8"), Chip = B("#f1f3f6"),
                    Ok = B("#16a34a"), Busy = B("#d97706"), OnAccent = Brushes.White, ShadowColor = Color.FromRgb(15, 23, 42)
                };
        }

        public static readonly FontFamily Font = new FontFamily("Segoe UI Variable Text, Segoe UI");

        /// Borderless, transparent window that hosts a rounded card with a soft shadow.
        public static Window NewPopup(Palette p, out Border card)
        {
            var w = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                Topmost = true,
                SizeToContent = SizeToContent.WidthAndHeight,
                FontFamily = Font,
                FontSize = 13,
                Foreground = p.Text,
                UseLayoutRounding = true,
                SnapsToDevicePixels = true
            };
            TextOptions.SetTextFormattingMode(w, TextFormattingMode.Display);
            card = new Border
            {
                Background = p.Win,
                BorderBrush = p.Line,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(18),
                Effect = new DropShadowEffect { Color = p.ShadowColor, BlurRadius = 36, ShadowDepth = 8, Direction = 270, Opacity = p.Dark ? 0.55 : 0.22 }
            };
            w.Content = card;
            FadeIn(w, card);
            return w;
        }

        /// Short fade and lift when the window appears.
        public static void FadeIn(Window w, FrameworkElement card)
        {
            var shift = new TranslateTransform(0, 6);
            card.RenderTransform = shift;
            w.Opacity = 0;
            w.Loaded += delegate
            {
                var d = new Duration(TimeSpan.FromMilliseconds(160));
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                w.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, d) { EasingFunction = ease });
                shift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(6, 0, d) { EasingFunction = ease });
            };
        }

        /// Small keyboard key label like in the mockup.
        public static Border Kbd(Palette p, string text)
        {
            return new Border
            {
                Background = p.Chip,
                BorderBrush = p.Line,
                BorderThickness = new Thickness(1, 1, 1, 2),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(5, 0, 5, 0),
                Margin = new Thickness(0, 0, 3, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = text, FontSize = 11, Foreground = p.Text }
            };
        }
    }
}
