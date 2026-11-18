using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace SnipPrompt
{
    /// Global shortcut via RegisterHotKey on a hidden message-only window.
    sealed class HotkeyHost : IDisposable
    {
        const int WM_HOTKEY = 0x0312;
        const uint MOD_NOREPEAT = 0x4000;
        const int Id = 1;

        readonly HwndSource source;
        bool registered;

        public event Action Pressed;

        public HotkeyHost()
        {
            var p = new HwndSourceParameters("SnipPrompt.Hotkey") { ParentWindow = new IntPtr(-3), Width = 0, Height = 0, WindowStyle = 0 };
            source = new HwndSource(p);
            source.AddHook(Hook);
        }

        /// Returns false when the text is invalid or the combination is taken by another program.
        public bool Register(string hotkey)
        {
            Unregister();
            uint mods; int vk;
            if (!Logic.TryParseHotkey(hotkey, out mods, out vk)) return false;
            registered = RegisterHotKey(source.Handle, Id, mods | MOD_NOREPEAT, (uint)vk);
            return registered;
        }

        /// Temporarily frees the combination, e.g. while the settings window records a new one.
        public void Unregister()
        {
            if (registered) UnregisterHotKey(source.Handle, Id);
            registered = false;
        }

        /// Checks whether a combination could be registered right now, without keeping it.
        public bool IsFree(string hotkey)
        {
            uint mods; int vk;
            if (!Logic.TryParseHotkey(hotkey, out mods, out vk)) return false;
            if (!RegisterHotKey(source.Handle, 2, mods | MOD_NOREPEAT, (uint)vk)) return false;
            UnregisterHotKey(source.Handle, 2);
            return true;
        }

        IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == Id)
            {
                handled = true;
                var h = Pressed;
                if (h != null) h();
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            Unregister();
            source.Dispose();
        }

        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hwnd, int id, uint mods, uint vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    }
}
