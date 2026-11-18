using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace SnipPrompt
{
    /// Small notice in the lower right corner, in the current theme. Replaces message boxes.
    static class Toast
    {
        static Window current;

        public static void Show(string text, bool offerFolder)
        {
            if (current != null) { try { current.Close(); } catch (Exception) { } }
            var p = Theme.Current();
            Border card;
            var w = Theme.NewPopup(p, out card);
            w.ShowActivated = false;

            var stack = new StackPanel { Margin = new Thickness(16, 12, 16, 12), MaxWidth = 340 };
            stack.Children.Add(new TextBlock { Text = Strings.T("app.name"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
            stack.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = p.Muted, LineHeight = 18 });
            if (offerFolder)
            {
                var link = new TextBlock { Text = Strings.T("toast.openFolder"), Foreground = p.Accent, Margin = new Thickness(0, 8, 0, 0), Cursor = Cursors.Hand };
                link.MouseLeftButtonUp += (s, e) => { Storage.OpenFolder(); w.Close(); e.Handled = true; };
                stack.Children.Add(link);
            }
            card.Child = stack;
            card.MouseLeftButtonUp += delegate { w.Close(); };

            w.Loaded += delegate
            {
                var area = SystemParameters.WorkArea;
                w.Left = area.Right - w.ActualWidth - 4;
                w.Top = area.Bottom - w.ActualHeight - 4;
            };
            w.Left = -10000; w.Top = -10000;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(7) };
            timer.Tick += delegate { timer.Stop(); w.Close(); };
            w.Closed += delegate { timer.Stop(); if (current == w) current = null; };
            current = w;
            w.Show();
            timer.Start();
        }
    }
}
