using System.Collections.Generic;
using System.Linq;
using SnipPrompt;

static class SettingsTests
{
    static void Check(bool ok, string name) { LogicTests.Check(ok, name); }

    public static void Run()
    {
        var de = Settings.Defaults("de-DE");
        Check(de.Templates.Contains("Was ist das?"), "defaults de templates");
        Check(Settings.Defaults("en-US").Templates.Contains("What is this?"), "defaults en templates");
        Check(de.Hotkey == "Ctrl+Alt+S" && de.Theme == "system" && de.Language == "auto" && de.CaptureMode == "frame", "defaults basics");
        Check(de.DelaySeconds == 0 && de.KeepCopies == 20 && de.ShiftAnnotates && de.ShowPreview && de.PreselectLast && de.DirectWhenSingle && de.ShowTemplates && !de.Autostart, "defaults switches");

        Check(Settings.FromJson("{", "de-DE").Hotkey == "Ctrl+Alt+S", "broken json -> defaults");
        Check(Settings.FromJson(null, "de-DE").Templates.Count == 3, "null json -> defaults");
        Check(Settings.FromJson("{\"DelaySeconds\":4}", "de-DE").DelaySeconds == 0, "invalid delay -> 0");
        Check(Settings.FromJson("{\"DelaySeconds\":3}", "de-DE").DelaySeconds == 3, "valid delay kept");
        Check(Settings.FromJson("{\"Theme\":\"blau\"}", "de-DE").Theme == "system", "invalid theme -> system");
        Check(Settings.FromJson("{\"KeepCopies\":7}", "de-DE").KeepCopies == 20, "invalid keep -> 20");
        Check(Settings.FromJson("{\"Hotkey\":\"S\"}", "de-DE").Hotkey == "Ctrl+Alt+S", "invalid hotkey -> default");
        Check(Settings.FromJson("{\"ShowPreview\":false}", "de-DE").ShowPreview == false, "bool kept");
        Check(Settings.FromJson("{\"ShowPreview\":false}", "de-DE").PreselectLast == true, "missing bool -> default");
        Check(Settings.FromJson("{\"Templates\":[\"a\\nb\",\"  \"]}", "de-DE").Templates.SequenceEqual(new[] { "a b" }), "templates cleaned");
        Check(Settings.FromJson("{\"Templates\":[]}", "de-DE").Templates.Count == 0, "empty template list kept");

        var s = Settings.Defaults("de-DE");
        s.Hotkey = "Ctrl+Shift+F5"; s.Language = "en"; s.Theme = "dark"; s.Autostart = true; s.CaptureMode = "window";
        s.DelaySeconds = 5; s.ShiftAnnotates = false; s.KeepCopies = 100; s.ShowPreview = false; s.PreselectLast = false;
        s.DirectWhenSingle = false; s.ShowTemplates = false; s.Templates = new List<string> { "Ä \"ö\" ✓" };
        s.LastName = "Thema"; s.LastHwnd = 123456;
        var r = Settings.FromJson(s.ToJson(), "de-DE");
        Check(r.Hotkey == s.Hotkey && r.Language == "en" && r.Theme == "dark" && r.Autostart && r.CaptureMode == "window"
            && r.DelaySeconds == 5 && !r.ShiftAnnotates && r.KeepCopies == 100 && !r.ShowPreview && !r.PreselectLast
            && !r.DirectWhenSingle && !r.ShowTemplates && r.Templates.SequenceEqual(s.Templates)
            && r.LastName == "Thema" && r.LastHwnd == 123456, "roundtrip");

        Check(Settings.CleanTemplate("a\r\nb ") == "a b", "clean newline");
        Check(Settings.CleanTemplate("  ") == null && Settings.CleanTemplate(null) == null, "clean empty");
        Check(Settings.CleanTemplate("a\u001bb\u0003c\u2028d\u2029e") == "a b c d e", "control and separator chars removed");

        // One field with a wrong type must not reset the others.
        var mixed = Settings.FromJson("{\"Templates\":\"x\",\"ShowPreview\":false,\"Autostart\":\"yes\",\"Hotkey\":\"Ctrl+Shift+K\",\"LastHwnd\":1.5,\"DelaySeconds\":2}", "de-DE");
        Check(!mixed.ShowPreview && mixed.Hotkey == "Ctrl+Shift+K" && mixed.DelaySeconds == 2, "valid fields kept next to wrong types");
        Check(mixed.Templates.Count == 3 && !mixed.Autostart && mixed.LastHwnd == 0, "wrong types -> defaults per field");
        Check(Settings.FromJson("[1,2]", "de-DE").Hotkey == "Ctrl+Alt+S", "json array -> defaults");
        Check(Settings.FromJson("{\"KeepCopies\":20.5}", "de-DE").KeepCopies == 20, "fraction -> default, not 0");

        Check(Strings.Resolve("auto", "de-AT") == "de" && Strings.Resolve("auto", "fr-FR") == "en" && Strings.Resolve("de", "en-US") == "de", "language resolve");
        var deKeys = Strings.Keys("de").ToList();
        var enKeys = Strings.Keys("en").ToList();
        Check(deKeys.Count > 20 && deKeys.OrderBy(k => k).SequenceEqual(enKeys.OrderBy(k => k)), "same keys in de and en");
        Check(deKeys.All(k => !string.IsNullOrEmpty(Strings.Get("de", k)) && !string.IsNullOrEmpty(Strings.Get("en", k))), "no empty texts");
        Check(Strings.Get("de", "tray.quit") == "Beenden" && Strings.Get("en", "tray.quit") == "Quit", "sample text");
        Check(Strings.Get("de", "does.not.exist") == "does.not.exist", "unknown key returns key");
    }
}
