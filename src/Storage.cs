using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace SnipPrompt
{
    /// Keeps copies of the captures in %LOCALAPPDATA%\SnipPrompt (newest N, N from the settings).
    static class Storage
    {
        public static string Folder
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SnipPrompt"); }
        }

        /// Returns the saved path, or null when copies are disabled or saving failed.
        public static string Save(Bitmap img, int keep)
        {
            if (keep <= 0) return null;
            try
            {
                Directory.CreateDirectory(Folder);
                string path = Path.Combine(Folder, "capture-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png");
                img.Save(path, ImageFormat.Png);
                Prune(keep);
                return path;
            }
            catch (Exception) { return null; }
        }

        static void Prune(int keep)
        {
            var newestFirst = Directory.GetFiles(Folder, "capture-*.png").OrderByDescending(p => p).ToList();
            foreach (var old in Logic.FilesToDelete(newestFirst, keep))
                try { File.Delete(old); } catch (Exception) { }
        }

        /// One-time move of captures made under the old project name (ClaudeDrop 0.1).
        public static void MigrateOld()
        {
            try
            {
                string old = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeDrop");
                if (!Directory.Exists(old)) return;
                Directory.CreateDirectory(Folder);
                foreach (var f in Directory.GetFiles(old, "capture-*.png"))
                {
                    string target = Path.Combine(Folder, Path.GetFileName(f));
                    if (!File.Exists(target)) File.Move(f, target);
                }
                if (!Directory.EnumerateFileSystemEntries(old).Any()) Directory.Delete(old);
            }
            catch (Exception) { }
        }

        public static void OpenFolder()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                System.Diagnostics.Process.Start("explorer.exe", "\"" + Folder + "\"");
            }
            catch (Exception) { }
        }
    }
}
