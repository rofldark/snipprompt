using System;
using System.Collections.Generic;
using System.Drawing;
using SnipPrompt;

static class LogicTests
{
    static int failures = 0;

    public static void Check(bool ok, string name)
    {
        Console.WriteLine((ok ? "PASS " : "FAIL ") + name);
        if (!ok) failures++;
    }

    static string Join(List<int> l) { return string.Join(",", l.ConvertAll(i => i.ToString()).ToArray()); }

    static int Main()
    {
        Check(Logic.IsClaudeTitle("✳ Claude Code"), "idle marker");
        Check(Logic.IsClaudeTitle("◑ Thema"), "spinner circle");
        Check(Logic.IsClaudeTitle("⠐ x"), "braille spinner");
        Check(!Logic.IsClaudeTitle("Windows PowerShell"), "plain shell");
        Check(!Logic.IsClaudeTitle(""), "empty");
        Check(!Logic.IsClaudeTitle(null), "null");
        Check(!Logic.IsClaudeTitle("   "), "whitespace only");

        // Picker rule: a single marked Claude tab goes direct; unmarked fallback tabs always need confirmation.
        Check(!Logic.NeedsPicker(new List<string> { "PowerShell", "✳ A" }), "one claude tab -> direct");
        Check(Logic.NeedsPicker(new List<string> { "✳ A", "◐ B" }), "two claude tabs -> picker");
        Check(Logic.NeedsPicker(new List<string> { "PowerShell" }), "single unmarked tab -> picker");
        Check(Logic.NeedsPicker(new List<string> { "PowerShell", "vim" }), "unmarked tabs -> picker");

        Check(Join(Logic.CandidateIndices(new List<string> { "PowerShell", "✳ A", "◐ B" })) == "1,2", "candidates marked");
        Check(Join(Logic.CandidateIndices(new List<string> { "PowerShell", "cmd" })) == "0,1", "candidates fallback all");
        Check(Logic.CandidateIndices(new List<string>()).Count == 0, "candidates empty");

        Check(Logic.Normalize(new Point(10, 10), new Point(2, 5)) == new Rectangle(2, 5, 8, 5), "normalize");
        Check(Logic.Normalize(new Point(2, 10), new Point(10, 5)) == new Rectangle(2, 5, 8, 5), "normalize mixed directions");
        Check(Logic.Normalize(new Point(10, 5), new Point(2, 10)) == new Rectangle(2, 5, 8, 5), "normalize mixed directions reversed");
        Check(Logic.IsTooSmall(new Rectangle(0, 0, 3, 100)), "too small");
        Check(!Logic.IsTooSmall(new Rectangle(0, 0, 4, 4)), "not too small");

        var names = new List<string>();
        for (int i = 21; i >= 0; i--) names.Add("capture-" + i.ToString("D2") + ".png");
        var del = Logic.FilesToDelete(names, 20);
        Check(del.Count == 2 && del[0] == "capture-01.png" && del[1] == "capture-00.png", "files to delete");
        Check(Logic.FilesToDelete(names.GetRange(0, 5), 20).Count == 0, "nothing to delete");

        // Session display name and status
        Check(Logic.DisplayName("◑ Thema") == "Thema", "display name spinner");
        Check(Logic.DisplayName("✳ Claude Code") == "Claude Code", "display name idle");
        Check(Logic.DisplayName("PowerShell") == "PowerShell", "display name plain");
        Check(Logic.IsBusy("◐ x") && Logic.IsBusy("⠐ x"), "busy spinner");
        Check(!Logic.IsBusy("✳ x") && !Logic.IsBusy("PowerShell"), "not busy");

        // Last used session
        var lt = new List<string> { "✳ A", "◐ B" };
        var lh = new List<long> { 1, 2 };
        Check(Logic.PreferredIndex(lt, lh, "B", 2) == 1, "preferred same window + name");
        Check(Logic.PreferredIndex(lt, lh, "B", 9) == 1, "preferred same name other window");
        Check(Logic.PreferredIndex(lt, lh, "Z", 9) == 0, "preferred fallback first");
        var dup = new List<string> { "✳ A", "✳ A" };
        Check(Logic.PreferredIndex(dup, lh, "A", 2) == 1, "preferred window decides between equal names");

        // Hotkeys
        uint mods; int vk;
        Check(Logic.TryParseHotkey("Ctrl+Alt+S", out mods, out vk) && mods == 3 && vk == 0x53, "parse ctrl+alt+s");
        Check(Logic.FormatHotkey(3, 0x53) == "Ctrl+Alt+S", "format ctrl+alt+s");
        Check(!Logic.TryParseHotkey("S", out mods, out vk), "no modifier rejected");
        Check(!Logic.TryParseHotkey("Ctrl+Alt+Blub", out mods, out vk), "unknown key rejected");
        Check(Logic.TryParseHotkey("Ctrl+Alt+F5", out mods, out vk) && vk == 0x74, "parse F5");
        Check(Logic.TryParseHotkey("shift+win+2", out mods, out vk) && mods == 12 && vk == 0x32, "parse case-insensitive digit");
        Check(Logic.FormatHotkey(12, 0x32) == "Shift+Win+2", "format order");
        Check(!Logic.TryParseHotkey("Ctrl+A+B", out mods, out vk), "two keys rejected");
        Check(!Logic.TryParseHotkey("Shift+A", out mods, out vk), "shift only rejected");
        Check(!Logic.TryParseHotkey("Alt+F4", out mods, out vk), "alt+f4 rejected");
        Check(Logic.TryParseHotkey("Shift+Alt+A", out mods, out vk), "shift+alt allowed");
        Check(Logic.TryParseHotkey("ctrl+alt+s", out mods, out vk) && vk == 0x53, "lowercase letter");

        SettingsTests.Run();

        Console.WriteLine(failures == 0 ? "ALL PASS" : failures + " FAILED");
        return failures == 0 ? 0 : 1;
    }
}
