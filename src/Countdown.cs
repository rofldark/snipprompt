using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;

namespace SnipPrompt
{
    /// Small countdown near the mouse before the screen is frozen. It never takes focus and lets
    /// clicks pass through, so an open menu or tooltip stays open. Esc cancels.
    static class Countdown
    {
        public static bool Run(int seconds)
        {
            var p = Theme.Current();
            Border card;
            var w = Theme.NewPopup(p, out card);
            w.ShowActivated = false;
            w.Focusable = false;

            var number = new TextBlock { FontSize = 30, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, Foreground = p.Accent };
            var stack = new StackPanel { Margin = new Thickness(18, 8, 18, 10) };
            stack.Children.Add(number);
            stack.Children.Add(new TextBlock { Text = Strings.T("countdown.cancel"), FontSize = 11, Foreground = p.Muted, HorizontalAlignment = HorizontalAlignment.Center });
            card.Child = stack;

            w.SourceInitialized += delegate
            {
                var h = new WindowInteropHelper(w).Handle;
                SetWindowLong(h, GWL_EXSTYLE, GetWindowLong(h, GWL_EXSTYLE) | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
            };
            w.Loaded += delegate
            {
                var h = new WindowInteropHelper(w).Handle;
                POINT c; GetCursorPos(out c);
                SetWindowPos(h, IntPtr.Zero, c.X + 24, c.Y + 24, 0, 0, 0x0001 | 0x0004 | 0x0010);
            };

            bool cancelled = false;
            var end = DateTime.UtcNow.AddSeconds(seconds);
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            timer.Tick += delegate
            {
                if ((GetAsyncKeyState(0x1B) & 0x8001) != 0) { cancelled = true; frame.Continue = false; return; }
                double left = (end - DateTime.UtcNow).TotalSeconds;
                if (left <= 0) { frame.Continue = false; return; }
                number.Text = Math.Ceiling(left).ToString();
            };
            GetAsyncKeyState(0x1B); // reset the "pressed since last call" bit
            number.Text = seconds.ToString();
            w.Show();
            timer.Start();
            Dispatcher.PushFrame(frame);
            timer.Stop();
            w.Close();
            // Give the compositor a moment to remove the countdown before the screen is frozen.
            var wait = new DispatcherFrame();
            var t2 = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            t2.Tick += delegate { t2.Stop(); wait.Continue = false; };
            t2.Start();
            Dispatcher.PushFrame(wait);
            return !cancelled;
        }

        const int GWL_EXSTYLE = -20;
        const int WS_EX_TRANSPARENT = 0x20, WS_EX_NOACTIVATE = 0x08000000, WS_EX_TOOLWINDOW = 0x80;
        [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int i, int v);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    }
}
