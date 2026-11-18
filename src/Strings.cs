using System;
using System.Collections.Generic;
using System.Linq;

namespace SnipPrompt
{
    /// UI texts in German and English. One row per key keeps both languages side by side.
    static class Strings
    {
        static readonly string[,] Table =
        {
            // key                       German                                                    English
            { "app.name",                "SnipPrompt",                                             "SnipPrompt" },
            { "tray.tooltip",            "SnipPrompt – Klick für Screenshot",                      "SnipPrompt – click to capture" },
            { "tray.capture",            "Aufnahme",                                               "Capture" },
            { "tray.settings",           "Einstellungen",                                          "Settings" },
            { "tray.folder",             "Screenshot-Ordner öffnen",                               "Open screenshot folder" },
            { "tray.quit",               "Beenden",                                                "Quit" },

            { "picker.sendTo",           "SENDEN AN",                                              "SEND TO" },
            { "picker.onlyImage",        "Nur Bild",                                               "Image only" },
            { "picker.busy",             "arbeitet",                                               "working" },
            { "picker.idle",             "bereit",                                                 "ready" },
            { "picker.other",            "Terminal",                                               "terminal" },
            { "picker.closed",           "geschlossen",                                            "closed" },
            { "picker.last",             "zuletzt",                                                "last" },
            { "picker.window",           "Fenster {0}",                                            "window {0}" },
            { "picker.hintSelect",       "wählen",                                                 "select" },
            { "picker.hintDirect",       "direkt",                                                 "direct" },
            { "picker.hintTemplate",     "Vorlage",                                                "template" },
            { "picker.hintSend",         "senden",                                                 "send" },
            { "picker.send",             "Senden",                                                 "Send" },
            { "picker.hintCancel",       "abbrechen",                                              "cancel" },

            { "toast.noTab",             "Kein Windows-Terminal-Tab gefunden.",                    "No Windows Terminal tab found." },
            { "toast.clipboardBlocked",  "Die Zwischenablage ist gerade blockiert.",               "The clipboard is locked right now." },
            { "toast.notDelivered",      "Der Terminal-Tab ließ sich nicht sicher nach vorne holen.", "The terminal tab could not be brought to the front safely." },
            { "toast.onClipboard",       "Das Bild liegt in der Zwischenablage (Alt+V).",          "The image is on the clipboard (Alt+V)." },
            { "toast.notOnClipboard",    "Das Bild konnte nicht in die Zwischenablage gelegt werden.", "The image could not be put on the clipboard." },
            { "toast.openFolder",        "Ordner öffnen",                                          "Open folder" },
            { "toast.error",             "Unerwarteter Fehler: {0}",                               "Unexpected error: {0}" },
            { "toast.hotkeyTaken",       "Das Tastenkürzel {0} ist schon belegt. In den Einstellungen änderbar.", "The shortcut {0} is already in use. Change it in the settings." },
            { "toast.running",           "SnipPrompt läuft im Infobereich. Tastenkürzel: {0}",     "SnipPrompt is running in the tray. Shortcut: {0}" },

            { "countdown.cancel",        "Esc bricht ab",                                          "Esc cancels" },

            { "anno.rect",               "Rechteck",                                               "Rectangle" },
            { "anno.arrow",              "Pfeil",                                                  "Arrow" },
            { "anno.marker",             "Marker",                                                 "Highlighter" },
            { "anno.text",               "Text",                                                   "Text" },
            { "anno.color",              "Farbe",                                                  "Color" },
            { "anno.undo",               "Rückgängig (Strg+Z)",                                    "Undo (Ctrl+Z)" },
            { "anno.next",               "Weiter",                                                 "Next" },

            { "settings.title",          "SnipPrompt – Einstellungen",                             "SnipPrompt – Settings" },
            { "nav.general",             "Allgemein",                                              "General" },
            { "nav.capture",             "Aufnahme",                                               "Capture" },
            { "nav.picker",              "Auswahlfenster",                                         "Picker" },
            { "nav.templates",           "Vorlagen",                                               "Templates" },
            { "nav.about",               "Info",                                                   "About" },

            { "set.hotkey",              "Tastenkürzel",                                           "Shortcut" },
            { "set.hotkey.desc",         "Startet die Aufnahme von überall",                       "Starts a capture from anywhere" },
            { "set.hotkey.press",        "Tastenkombination drücken …",                            "Press a key combination …" },
            { "set.hotkey.taken",        "Schon belegt – bitte eine andere Kombination wählen",    "Already in use – please choose another one" },
            { "set.language",            "Sprache",                                                "Language" },
            { "set.language.desc",       "Sprache der Oberfläche",                                 "Language of the interface" },
            { "lang.auto",               "Automatisch",                                            "Automatic" },
            { "lang.de",                 "Deutsch",                                                "Deutsch" },
            { "lang.en",                 "English",                                                "English" },
            { "set.theme",               "Design",                                                 "Theme" },
            { "set.theme.desc",          "Hell, dunkel oder wie Windows",                          "Light, dark or like Windows" },
            { "theme.light",             "Hell",                                                   "Light" },
            { "theme.dark",              "Dunkel",                                                 "Dark" },
            { "theme.system",            "System",                                                 "System" },
            { "set.autostart",           "Mit Windows starten",                                    "Start with Windows" },
            { "set.autostart.desc",      "Läuft dann im Hintergrund neben der Uhr",                "Then runs in the background next to the clock" },

            { "set.mode",                "Art der Aufnahme",                                       "Capture mode" },
            { "set.mode.desc",           "Rahmen aufziehen oder Fenster anklicken",                "Drag a frame or click a window" },
            { "mode.frame",              "Rahmen",                                                 "Frame" },
            { "mode.window",             "Fenster",                                                "Window" },
            { "set.delay",               "Verzögerung",                                            "Delay" },
            { "set.delay.desc",          "Nach dem Rahmen: Zeit, um Menüs oder Tooltips zu öffnen", "After choosing the area: time to open menus or tooltips" },
            { "delay.off",               "Aus",                                                    "Off" },
            { "set.shift",               "Markieren mit Umschalt",                                 "Annotate with Shift" },
            { "set.shift.desc",          "Umschalt beim Aufziehen öffnet die Markier-Leiste",      "Holding Shift while capturing opens the annotation bar" },
            { "set.keep",                "Kopien behalten",                                        "Keep copies" },
            { "set.keep.desc",           "Im Screenshot-Ordner",                                   "In the screenshot folder" },
            { "keep.none",               "Keine",                                                  "None" },

            { "set.preview",             "Vorschau anzeigen",                                      "Show preview" },
            { "set.preview.desc",        "Miniatur des Screenshots oben im Fenster",               "Thumbnail of the screenshot at the top" },
            { "set.preselect",           "Zuletzt genutzte Session vorauswählen",                  "Preselect the last used session" },
            { "set.preselect.desc",      "Dann reicht meist Enter",                                "Then Enter is usually enough" },
            { "set.direct",              "Bei nur einer Session direkt senden",                    "Send directly when there is only one session" },
            { "set.direct.desc",         "Ohne Auswahlfenster",                                    "Without the picker" },
            { "set.showTemplates",       "Vorlagen anzeigen",                                      "Show templates" },
            { "set.showTemplates.desc",  "Knöpfe unter der Session-Liste",                         "Buttons below the session list" },

            { "tpl.desc",                "Text, der nach dem Bild ins Eingabefeld geschrieben wird – ohne Enter.", "Text typed into the prompt after the image – without Enter." },
            { "tpl.add",                 "+ Vorlage hinzufügen",                                   "+ Add template" },
            { "tpl.new",                 "Neue Vorlage",                                           "New template" },
            { "tpl.up",                  "Nach oben",                                              "Move up" },
            { "tpl.delete",              "Löschen",                                                "Delete" },

            { "about.version",           "Version",                                                "Version" },
            { "about.folder",            "Screenshot-Ordner",                                      "Screenshot folder" },
            { "about.open",              "Öffnen",                                                 "Open" },
        };

        static readonly Dictionary<string, int> Index = BuildIndex();

        static Dictionary<string, int> BuildIndex()
        {
            var d = new Dictionary<string, int>();
            for (int i = 0; i < Table.GetLength(0); i++) d[Table[i, 0]] = i;
            return d;
        }

        /// Current UI language ("de" or "en").
        public static string Lang = "en";

        /// "auto" follows the Windows UI language: German for de-*, English otherwise.
        public static string Resolve(string setting, string uiCulture)
        {
            if (setting == "de" || setting == "en") return setting;
            return uiCulture != null && uiCulture.StartsWith("de", StringComparison.OrdinalIgnoreCase) ? "de" : "en";
        }

        public static IEnumerable<string> Keys(string lang)
        {
            int col = lang == "de" ? 1 : 2;
            for (int i = 0; i < Table.GetLength(0); i++)
                if (!string.IsNullOrEmpty(Table[i, col])) yield return Table[i, 0];
        }

        public static string Get(string lang, string key)
        {
            int i;
            if (!Index.TryGetValue(key, out i)) return key;
            return Table[i, lang == "de" ? 1 : 2];
        }

        public static string T(string key) { return Get(Lang, key); }

        public static string T(string key, object arg) { return string.Format(Get(Lang, key), arg); }
    }
}
