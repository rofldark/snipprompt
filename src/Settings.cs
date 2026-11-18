using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace SnipPrompt
{
    /// User settings, stored as JSON in %APPDATA%\SnipPrompt\settings.json.
    /// Every field is validated on load; invalid or missing values fall back to the default.
    [DataContract]
    sealed class Settings
    {
        [DataMember] public string Hotkey;
        [DataMember] public string Language;      // auto | de | en
        [DataMember] public string Theme;         // system | light | dark
        [DataMember] public bool Autostart;
        [DataMember] public string CaptureMode;   // frame | window
        [DataMember] public int DelaySeconds;     // 0, 1, 2, 3, 5
        [DataMember] public bool ShiftAnnotates;
        [DataMember] public int KeepCopies;       // 0, 20, 100
        [DataMember] public bool ShowPreview;
        [DataMember] public bool PreselectLast;
        [DataMember] public bool DirectWhenSingle;
        [DataMember] public bool ShowTemplates;
        [DataMember] public List<string> Templates;
        [DataMember] public string LastName;
        [DataMember] public long LastHwnd;

        public static readonly int[] Delays = { 0, 1, 2, 3, 5 };
        public static readonly int[] KeepOptions = { 0, 20, 100 };
        public static readonly string[] Languages = { "auto", "de", "en" };
        public static readonly string[] Themes = { "system", "light", "dark" };
        public static readonly string[] Modes = { "frame", "window" };

        public static Settings Defaults(string uiCulture)
        {
            bool de = Strings.Resolve("auto", uiCulture) == "de";
            return new Settings
            {
                Hotkey = "Ctrl+Alt+S",
                Language = "auto",
                Theme = "system",
                Autostart = false,
                CaptureMode = "frame",
                DelaySeconds = 0,
                ShiftAnnotates = true,
                KeepCopies = 20,
                ShowPreview = true,
                PreselectLast = true,
                DirectWhenSingle = true,
                ShowTemplates = true,
                Templates = de
                    ? new List<string> { "Was ist das?", "Behebe den Fehler", "Erkläre kurz, was hier passiert" }
                    : new List<string> { "What is this?", "Fix this error", "Briefly explain what is happening here" },
                LastName = "",
                LastHwnd = 0
            };
        }

        /// Reads the JSON field by field (via the XML view of the JSON reader), so one field with a
        /// wrong type only falls back to its own default instead of resetting everything.
        public static Settings FromJson(string json, string uiCulture)
        {
            var s = Defaults(uiCulture);
            if (string.IsNullOrEmpty(json)) return s;
            XElement root;
            try
            {
                using (var reader = JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(json), XmlDictionaryReaderQuotas.Max))
                    root = XElement.Load(reader);
            }
            catch (Exception) { return s; }
            if ((string)root.Attribute("type") != "object") return s;

            string str; bool b; long n; List<string> list;
            uint m; int vk;
            if (Str(root, "Hotkey", out str) && Logic.TryParseHotkey(str, out m, out vk)) s.Hotkey = Logic.FormatHotkey(m, vk);
            if (Str(root, "Language", out str) && Languages.Contains(str)) s.Language = str;
            if (Str(root, "Theme", out str) && Themes.Contains(str)) s.Theme = str;
            if (Str(root, "CaptureMode", out str) && Modes.Contains(str)) s.CaptureMode = str;
            if (Num(root, "DelaySeconds", out n) && Delays.Contains((int)n)) s.DelaySeconds = (int)n;
            if (Num(root, "KeepCopies", out n) && KeepOptions.Contains((int)n)) s.KeepCopies = (int)n;
            if (Bool(root, "Autostart", out b)) s.Autostart = b;
            if (Bool(root, "ShiftAnnotates", out b)) s.ShiftAnnotates = b;
            if (Bool(root, "ShowPreview", out b)) s.ShowPreview = b;
            if (Bool(root, "PreselectLast", out b)) s.PreselectLast = b;
            if (Bool(root, "DirectWhenSingle", out b)) s.DirectWhenSingle = b;
            if (Bool(root, "ShowTemplates", out b)) s.ShowTemplates = b;
            if (StrList(root, "Templates", out list)) s.Templates = list.Select(CleanTemplate).Where(t => t != null).ToList();
            if (Str(root, "LastName", out str)) s.LastName = str;
            if (Num(root, "LastHwnd", out n)) s.LastHwnd = n;
            return s;
        }

        static XElement Field(XElement root, string name, string type)
        {
            var e = root.Element(name);
            return e != null && (string)e.Attribute("type") == type ? e : null;
        }

        static bool Str(XElement root, string name, out string v)
        {
            var e = Field(root, name, "string");
            v = e == null ? null : e.Value;
            return e != null;
        }

        static bool Bool(XElement root, string name, out bool v)
        {
            var e = Field(root, name, "boolean");
            v = e != null && e.Value == "true";
            return e != null;
        }

        /// Whole numbers only; 1.5 or 1e3 count as wrong type.
        static bool Num(XElement root, string name, out long v)
        {
            var e = Field(root, name, "number");
            v = 0;
            return e != null && long.TryParse(e.Value, System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out v);
        }

        static bool StrList(XElement root, string name, out List<string> v)
        {
            var e = Field(root, name, "array");
            v = e == null ? null : e.Elements().Where(x => (string)x.Attribute("type") == "string").Select(x => x.Value).ToList();
            return e != null;
        }

        public string ToJson()
        {
            using (var ms = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(Settings)).WriteObject(ms, this);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        /// Templates are single-line: a line break would arrive in the terminal as Enter.
        public static string CleanTemplate(string s)
        {
            if (s == null) return null;
            var sb = new StringBuilder();
            foreach (char c in s) sb.Append(char.IsControl(c) || c == '\u2028' || c == '\u2029' ? ' ' : c); // never Enter, Esc, Ctrl+C ...
            string t = sb.ToString().Trim();
            while (t.Contains("  ")) t = t.Replace("  ", " ");
            return t.Length == 0 ? null : t;
        }

        public static string Folder
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SnipPrompt"); }
        }

        static string FilePath { get { return Path.Combine(Folder, "settings.json"); } }

        public static Settings Load()
        {
            string json = null;
            try { if (File.Exists(FilePath)) json = File.ReadAllText(FilePath, Encoding.UTF8); } catch (Exception) { }
            return FromJson(json, System.Globalization.CultureInfo.CurrentUICulture.Name);
        }

        /// Writes atomically via a temp file so a crash never leaves a half-written file.
        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, ToJson(), new UTF8Encoding(false));
                if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
                else File.Move(tmp, FilePath);
            }
            catch (Exception) { }
        }
    }
}