using System;
using System.Collections.Generic;
using System.Drawing;

namespace SnipPrompt
{
    /// Pure logic without window or system access (tested in tests/LogicTests.cs).
    static class Logic
    {
        // Characters Claude Code puts in front of the terminal title (idle mark and working spinner).
        const string Markers = "✳✻✶✢◐◑◒◓";

        public static bool IsClaudeTitle(string title)
        {
            if (title == null) return false;
            string t = title.TrimStart();
            if (t.Length == 0) return false;
            char c = t[0];
            if (Markers.IndexOf(c) >= 0) return true;
            return c >= '⠀' && c <= '⣿'; // braille spinner
        }

        static bool AnyClaude(IList<string> titles)
        {
            foreach (var t in titles) if (IsClaudeTitle(t)) return true;
            return false;
        }

        /// Indices of the Claude tabs; when none is marked, all indices (fallback).
        public static List<int> CandidateIndices(IList<string> titles)
        {
            bool any = AnyClaude(titles);
            var result = new List<int>();
            for (int i = 0; i < titles.Count; i++)
                if (!any || IsClaudeTitle(titles[i])) result.Add(i);
            return result;
        }

        /// True unless there is exactly one marked Claude tab. Unmarked fallback tabs are
        /// never pasted into without the user confirming the target.
        public static bool NeedsPicker(IList<string> titles)
        {
            return !AnyClaude(titles) || CandidateIndices(titles).Count > 1;
        }

        static bool IsSpinner(char c)
        {
            return (c >= '◐' && c <= '◓') || (c >= '⠀' && c <= '⣿');
        }

        /// Tab title without the leading status marker.
        public static string DisplayName(string title)
        {
            if (title == null) return "";
            string t = title.TrimStart();
            if (IsClaudeTitle(t)) t = t.Substring(1).TrimStart();
            return t;
        }

        /// True while Claude is working (spinner marker), false when idle or not a Claude tab.
        public static bool IsBusy(string title)
        {
            if (!IsClaudeTitle(title)) return false;
            return IsSpinner(title.TrimStart()[0]);
        }

        /// Index of the last used session: same window and name, else same name, else 0.
        public static int PreferredIndex(IList<string> titles, IList<long> hwnds, string lastName, long lastHwnd)
        {
            if (string.IsNullOrEmpty(lastName)) return 0;
            int byName = -1;
            for (int i = 0; i < titles.Count; i++)
            {
                if (DisplayName(titles[i]) != lastName) continue;
                if (i < hwnds.Count && hwnds[i] == lastHwnd) return i;
                if (byName < 0) byName = i;
            }
            return byName >= 0 ? byName : 0;
        }

        public const uint ModAlt = 1, ModCtrl = 2, ModShift = 4, ModWin = 8;

        static readonly string[] ModNames = { "Ctrl", "Alt", "Shift", "Win" };
        static readonly uint[] ModValues = { ModCtrl, ModAlt, ModShift, ModWin };

        static string KeyName(int vk)
        {
            if ((vk >= 0x30 && vk <= 0x39) || (vk >= 0x41 && vk <= 0x5A)) return ((char)vk).ToString();
            if (vk >= 0x70 && vk <= 0x87) return "F" + (vk - 0x6F);
            switch (vk)
            {
                case 0x20: return "Space";
                case 0x2C: return "PrintScreen";
                case 0x2D: return "Insert";
                case 0x13: return "Pause";
            }
            return null;
        }

        static int KeyCode(string name)
        {
            for (int vk = 0x01; vk <= 0xFE; vk++)
            {
                string n = KeyName(vk);
                if (n != null && string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) return vk;
            }
            return -1;
        }

        /// Parses "Ctrl+Alt+S" style text. Needs at least one modifier and exactly one known key.
        public static bool TryParseHotkey(string text, out uint modifiers, out int vk)
        {
            modifiers = 0; vk = -1;
            if (string.IsNullOrEmpty(text)) return false;
            foreach (string raw in text.Split('+'))
            {
                string part = raw.Trim();
                int m = Array.FindIndex(ModNames, n => string.Equals(n, part, StringComparison.OrdinalIgnoreCase));
                if (m >= 0) { modifiers |= ModValues[m]; continue; }
                if (vk >= 0) return false;
                vk = KeyCode(part);
                if (vk < 0) return false;
            }
            if (vk < 0 || (modifiers & (ModCtrl | ModAlt | ModWin)) == 0) return false; // Shift alone would swallow typing
            if (modifiers == ModAlt && (vk == 0x73 || vk == 0x20)) return false;           // Alt+F4, Alt+Space belong to Windows
            return true;
        }

        public static string FormatHotkey(uint modifiers, int vk)
        {
            var parts = new List<string>();
            for (int i = 0; i < ModNames.Length; i++)
                if ((modifiers & ModValues[i]) != 0) parts.Add(ModNames[i]);
            parts.Add(KeyName(vk) ?? "?");
            return string.Join("+", parts.ToArray());
        }

        public static Rectangle Normalize(Point a, Point b)
        {
            return new Rectangle(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
        }

        public static bool IsTooSmall(Rectangle r)
        {
            return r.Width < 4 || r.Height < 4;
        }

        /// newestFirst: files, newest first. Returns everything beyond the first "keep".
        public static List<string> FilesToDelete(IList<string> newestFirst, int keep)
        {
            var result = new List<string>();
            for (int i = keep; i < newestFirst.Count; i++) result.Add(newestFirst[i]);
            return result;
        }
    }
}
