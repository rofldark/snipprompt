using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;

namespace SnipPrompt
{
    /// Developer helper (--snapshots <dir>): renders the picker and the settings pages to PNG files
    /// off-screen, light and dark, so the look can be checked without taking over the screen.
    static class Snapshots
    {
        public static int Run(string dir)
        {
            Directory.CreateDirectory(dir);
            var keepTheme = App.Settings.Theme;
            foreach (var theme in new[] { "light", "dark" })
            {
                App.Settings.Theme = theme;
                Save(PickerWindow.Demo(DemoTabs(), DemoImage(), App.Settings), Path.Combine(dir, "picker-" + theme + ".png"));
                foreach (var sec in new[] { "nav.general", "nav.capture", "nav.picker", "nav.templates", "nav.about" })
                    Save(SettingsWindow.Demo(sec), Path.Combine(dir, "settings-" + sec.Substring(4) + "-" + theme + ".png"));
            }
            App.Settings.Theme = keepTheme;
            return 0;
        }

        static List<TabRef> DemoTabs()
        {
            return new List<TabRef>
            {
                new TabRef { Title = "◑ api-server – fix login bug", Hwnd = new IntPtr(1) },
                new TabRef { Title = "✳ landing-page", Hwnd = new IntPtr(1) },
                new TabRef { Title = "✳ data-pipeline", Hwnd = new IntPtr(2) }
            };
        }

        static Drawing.Bitmap DemoImage()
        {
            var b = new Drawing.Bitmap(812, 508);
            using (var g = Drawing.Graphics.FromImage(b))
            {
                g.Clear(Drawing.Color.FromArgb(243, 243, 243));
                g.FillRectangle(Drawing.Brushes.White, 0, 0, 812, 40);
                g.FillRectangle(new Drawing.SolidBrush(Drawing.Color.FromArgb(234, 234, 234)), 0, 40, 200, 468);
                g.FillRectangle(Drawing.Brushes.White, 240, 80, 520, 360);
                g.FillRectangle(new Drawing.SolidBrush(Drawing.Color.FromArgb(253, 232, 232)), 240, 80, 520, 50);
                using (var f = new Drawing.Font("Segoe UI", 14f))
                {
                    g.DrawString("Login failed: 401 Unauthorized (token expired)", f, new Drawing.SolidBrush(Drawing.Color.FromArgb(153, 27, 27)), 260, 92);
                    g.DrawString("My App – Sign in", f, Drawing.Brushes.Black, 16, 8);
                }
                g.FillRectangle(new Drawing.SolidBrush(Drawing.Color.FromArgb(37, 99, 235)), 260, 380, 130, 38);
            }
            return b;
        }

        static void Save(FrameworkElement el, string path)
        {
            el.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var size = el.DesiredSize;
            if (el.Width > 0 && !double.IsNaN(el.Width)) size = new Size(el.Width, el.Height);
            el.Arrange(new Rect(size));
            el.UpdateLayout();
            var rtb = new RenderTargetBitmap((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height), 96, 96, PixelFormats.Pbgra32);
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRectangle(new VisualBrush(el), null, new Rect(size));
            }
            rtb.Render(dv);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using (var fs = File.Create(path)) enc.Save(fs);
        }
    }
}
