using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace SnipPrompt
{
    /// Puts the image on the clipboard, brings the terminal tab to the front and sends Alt+V.
    static class Paster
    {
        /// Returns false when the clipboard stayed locked by another program.
        public static bool ToClipboard(Bitmap img)
        {
            var data = new DataObject();
            data.SetImage(img);
            var png = new MemoryStream();
            img.Save(png, ImageFormat.Png);
            data.SetData("PNG", false, png);
            for (int i = 0; i < 10; i++)
            {
                try { Clipboard.SetDataObject(data, true); return true; }
                catch (ExternalException) { Thread.Sleep(100); }
            }
            return false;
        }

        public enum Result { Pasted, ClipboardFailed, NotDelivered }

        /// Never sends a key unless the right window is in front and the right tab is selected.
        /// Pastes the image and then types the template text (if any). Never presses Enter.
        public static Result Paste(TabRef tab, Bitmap img, string template)
        {
            var r = Paste(tab, img);
            if (r != Result.Pasted || string.IsNullOrEmpty(template)) return r;
            Thread.Sleep(400); // let Claude Code read the image from the clipboard first
            // Same rules as for the image: right window, right tab, no keys held. Otherwise skip the text.
            if (!WaitForKeysReleased()) return r;
            if (GetForegroundWindow() != tab.Hwnd || !TerminalTabs.IsSelected(tab)) return r;
            TypeText(" " + template);
            return r;
        }

        /// Types text as Unicode characters (umlauts, emoji as surrogate pairs). Line breaks are
        /// removed beforehand by Settings.CleanTemplate, so this can never submit the prompt.
        static void TypeText(string text)
        {
            var inputs = new List<INPUT>();
            foreach (char c in text)
            {
                if (char.IsControl(c) || c == '\u2028' || c == '\u2029') continue; // never Enter, Esc, Ctrl+C ...
                inputs.Add(Unicode(c, false));
                inputs.Add(Unicode(c, true));
            }
            if (inputs.Count > 0) SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf(typeof(INPUT)));
        }

        static INPUT Unicode(char c, bool up)
        {
            var i = new INPUT { type = INPUT_KEYBOARD };
            i.u.ki.wVk = 0;
            i.u.ki.wScan = c;
            i.u.ki.dwFlags = KEYEVENTF_UNICODE | (up ? KEYEVENTF_KEYUP : 0);
            return i;
        }

        const uint KEYEVENTF_UNICODE = 0x0004;

        static Result Paste(TabRef tab, Bitmap img)
        {
            if (!ToClipboard(img)) return Result.ClipboardFailed;
            if (!BringToFront(tab.Hwnd)) return Result.NotDelivered;
            if (!TerminalTabs.Select(tab)) return Result.NotDelivered;
            // Selection may switch asynchronously: wait until the tab really is the active one.
            bool selected = false;
            for (int i = 0; i < 20 && !selected; i++)
            {
                Thread.Sleep(25);
                selected = TerminalTabs.IsSelected(tab);
            }
            if (!selected) return Result.NotDelivered;
            Thread.Sleep(120); // let the terminal move keyboard focus into the tab's pane
            if (!WaitForKeysReleased()) return Result.NotDelivered;
            if (GetForegroundWindow() != tab.Hwnd || !TerminalTabs.IsSelected(tab)) return Result.NotDelivered;
            return SendAltV() ? Result.Pasted : Result.NotDelivered;
        }

        static bool BringToFront(IntPtr hwnd)
        {
            if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                if (attempt == 1)
                {
                    // Windows only lets the process that received the last input switch the foreground.
                    // Holding Alt while switching counts as input; releasing it only after the switch
                    // keeps the previous window from entering menu mode.
                    keybd_event((byte)VK_MENU, 0, 0, UIntPtr.Zero);
                    SetForegroundWindow(hwnd);
                    keybd_event((byte)VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                }
                else SetForegroundWindow(hwnd);
                for (int i = 0; i < 20; i++)
                {
                    if (GetForegroundWindow() == hwnd) return true;
                    Thread.Sleep(25);
                }
            }
            return false;
        }

        /// Waits (max. 3 s) until Enter, Shift, Ctrl, Alt and Win are released, so a held key
        /// (e.g. Enter from the picker) cannot repeat into the terminal and send the prompt.
        /// Returns false on timeout: then nothing is sent at all.
        static bool WaitForKeysReleased()
        {
            int[] keys = { 0x0D, 0x10, 0x11, 0x12, 0x5B, 0x5C };
            for (int i = 0; i < 120; i++)
            {
                bool down = false;
                foreach (int k in keys) if ((GetAsyncKeyState(k) & 0x8000) != 0) down = true;
                if (!down) return true;
                Thread.Sleep(25);
            }
            return false;
        }

        static bool SendAltV()
        {
            var inputs = new INPUT[]
            {
                Key(VK_MENU, false), Key(VK_V, false), Key(VK_V, true), Key(VK_MENU, true)
            };
            uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
            if (sent == inputs.Length) return true;
            // Partially blocked (e.g. terminal runs elevated): make sure Alt is not left pressed.
            var up = new INPUT[] { Key(VK_V, true), Key(VK_MENU, true) };
            SendInput((uint)up.Length, up, Marshal.SizeOf(typeof(INPUT)));
            return false;
        }

        static INPUT Key(ushort vk, bool up)
        {
            var i = new INPUT { type = INPUT_KEYBOARD };
            i.u.ki.wVk = vk;
            i.u.ki.dwFlags = up ? KEYEVENTF_KEYUP : 0;
            return i;
        }

        const int SW_RESTORE = 9;
        const uint INPUT_KEYBOARD = 1;
        const uint KEYEVENTF_KEYUP = 0x0002;
        const ushort VK_MENU = 0x12;
        const ushort VK_V = 0x56;

        [StructLayout(LayoutKind.Sequential)]
        struct INPUT { public uint type; public InputUnion u; }

        [StructLayout(LayoutKind.Explicit)]
        struct InputUnion
        {
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public MOUSEINPUT mi;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

        [DllImport("user32.dll")] static extern uint SendInput(uint n, INPUT[] inputs, int size);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hwnd, int cmd);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);
        [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    }
}
