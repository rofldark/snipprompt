using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

[assembly: System.Reflection.AssemblyVersion("0.2.0.0")]
// Without this attribute WPF runs in legacy DPI mode and ignores the PerMonitorV2 manifest.
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
[assembly: System.Reflection.AssemblyTitle("SnipPrompt")]

namespace SnipPrompt
{
    /// Entry point: single instance, tray icon, global shortcut. The capture flow lives in Flow.
    static class App
    {
        public static Settings Settings;
        public static Dispatcher UI;
        public static HotkeyHost Hotkey;
        public static bool HotkeyOk = true;
        static Forms.NotifyIcon tray;
        static Application wpf;

        [STAThread]
        static int Main(string[] args)
        {
            if (args.Contains("--list")) return ListTabs();
            if (args.Contains("--grab-check")) return GrabCheck();
            if (args.Length >= 2 && args[0] == "--snapshots")
            {
                Settings = Settings.Defaults(args.Length >= 3 ? args[2] : CultureInfo.CurrentUICulture.Name);
                if (args.Length >= 3) Settings.Language = args[2].StartsWith("de") ? "de" : "en";
                ApplyLanguage();
                return Snapshots.Run(args[1]);
            }

            var instance = new SingleInstance();
            if (!instance.TryBecomePrimary())
            {
                SingleInstance.SignalPrimary();
                return 0;
            }

            Settings = Settings.Load();
            ApplyLanguage();
            Storage.MigrateOld();

            Forms.Application.EnableVisualStyles();
            Forms.Application.SetUnhandledExceptionMode(Forms.UnhandledExceptionMode.CatchException);
            Forms.Application.ThreadException += (s, e) => Toast.Show(Strings.T("toast.error", e.Exception.Message), false);
            wpf = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            UI = wpf.Dispatcher;
            wpf.DispatcherUnhandledException += (s, e) =>
            {
                e.Handled = true;
                Toast.Show(Strings.T("toast.error", e.Exception.Message), false);
            };

            instance.Listen(() => UI.BeginInvoke(new Action(Flow.Start)));
            CreateTray();
            Hotkey = new HotkeyHost();
            Hotkey.Pressed += Flow.Start;
            HotkeyOk = Hotkey.Register(Settings.Hotkey);
            bool hotkeyOk = HotkeyOk;

            bool background = args.Contains("--background");
            UI.BeginInvoke(new Action(() =>
            {
                if (!hotkeyOk) Toast.Show(Strings.T("toast.hotkeyTaken", Settings.Hotkey), false);
                if (!background) Flow.Start();
            }));

            wpf.Run();

            tray.Visible = false;
            tray.Dispose();
            Hotkey.Dispose();
            return 0;
        }

        public static void ApplyLanguage()
        {
            Strings.Lang = Strings.Resolve(Settings.Language, CultureInfo.CurrentUICulture.Name);
        }

        static void CreateTray()
        {
            tray = new Forms.NotifyIcon { Visible = true };
            try
            {
                using (var s = typeof(App).Assembly.GetManifestResourceStream("SnipPrompt.icon.ico"))
                    tray.Icon = new System.Drawing.Icon(s, Forms.SystemInformation.SmallIconSize);
            }
            catch (Exception) { tray.Icon = System.Drawing.SystemIcons.Application; }
            tray.MouseClick += (s, e) => { if (e.Button == Forms.MouseButtons.Left) UI.BeginInvoke(new Action(Flow.Start)); };
            RefreshTrayTexts();
        }

        /// Rebuilds tray texts, e.g. after the language changed.
        public static void RefreshTrayTexts()
        {
            tray.Text = Strings.T("tray.tooltip");
            var menu = new Forms.ContextMenuStrip { ShowImageMargin = false, Font = new System.Drawing.Font("Segoe UI", 9.5f) };
            menu.Items.Add(Strings.T("tray.capture"), null, delegate { UI.BeginInvoke(new Action(Flow.Start)); });
            menu.Items.Add(Strings.T("tray.settings"), null, delegate { UI.BeginInvoke(new Action(SettingsWindow.ShowSingle)); });
            menu.Items.Add(Strings.T("tray.folder"), null, delegate { Storage.OpenFolder(); });
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add(Strings.T("tray.quit"), null, delegate { UI.BeginInvoke(new Action(() => wpf.Shutdown())); });
            var old = tray.ContextMenuStrip;
            tray.ContextMenuStrip = menu;
            if (old != null) old.Dispose();
        }

        /// Debug helper: prints all found tabs to the calling console.
        static int ListTabs()
        {
            AttachConsole(-1);
            var tabs = TerminalTabs.FindAll();
            var titles = tabs.Select(t => t.Title).ToList();
            Console.WriteLine();
            for (int i = 0; i < tabs.Count; i++)
                Console.WriteLine((Logic.IsClaudeTitle(titles[i]) ? "* " : "  ") + tabs[i].Title + "  (hwnd " + tabs[i].Hwnd + ")");
            Console.WriteLine(tabs.Count + " tab(s), picker needed: " + (Logic.NeedsPicker(titles) ? "yes" : "no"));
            return 0;
        }

        /// Debug helper: takes a screenshot in memory only (nothing saved) and reports size and content.
        static int GrabCheck()
        {
            AttachConsole(-1);
            var vb = Forms.SystemInformation.VirtualScreen;
            using (var b = CaptureOverlay.Grab(vb))
            {
                int nonBlack = 0, samples = 0;
                for (int y = 0; y < b.Height; y += Math.Max(1, b.Height / 40))
                    for (int x = 0; x < b.Width; x += Math.Max(1, b.Width / 40))
                    {
                        samples++;
                        var c = b.GetPixel(x, y);
                        if (c.R + c.G + c.B > 30) nonBlack++;
                    }
                Console.WriteLine();
                Console.WriteLine("grab " + b.Width + "x" + b.Height + " (virtual screen " + vb + "), non-black samples " + nonBlack + "/" + samples);
            }
            return 0;
        }

        [DllImport("kernel32.dll")] static extern bool AttachConsole(int pid);
    }
}
