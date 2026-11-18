using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;

namespace SnipPrompt
{
    /// One capture: (delay) → overlay → (annotate) → find tabs → (picker) → paste.
    static class Flow
    {
        static bool running;

        public static void Start()
        {
            if (running) return; // a second shortcut press during a capture is ignored
            running = true;
            Bitmap img = null;
            string saved = null;
            try
            {
                Run(ref img, ref saved);
            }
            catch (Exception ex)
            {
                bool onClipboard = false;
                if (img != null) try { onClipboard = Paster.ToClipboard(img); } catch (Exception) { }
                Notify(onClipboard, saved, Strings.T("toast.error", ex.Message));
            }
            finally
            {
                if (img != null) img.Dispose(); // the clipboard holds its own copy
                running = false;
            }
        }

        static void Run(ref Bitmap img, ref string saved)
        {
            var s = App.Settings;

            // Look for terminal tabs in the background while the user selects the area.
            List<TabRef> tabs = null;
            var finder = new Thread(() => tabs = TerminalTabs.FindAll()) { IsBackground = true };
            finder.SetApartmentState(ApartmentState.MTA);
            finder.Start();

            Rectangle area;
            bool shift;
            img = CaptureOverlay.Run(s.CaptureMode == "window", out area, out shift);
            if (img == null) return;

            // Delay comes after choosing the area: time to open a menu or hover a tooltip inside it,
            // then the area is taken again, live.
            if (s.DelaySeconds > 0)
            {
                if (!Countdown.Run(s.DelaySeconds)) return;
                img.Dispose();
                img = CaptureOverlay.Grab(area);
            }

            if (shift && s.ShiftAnnotates)
            {
                var marked = AnnotateWindow.Run(img, area);
                if (marked == null) return;
                img.Dispose();
                img = marked;
            }
            saved = Storage.Save(img, s.KeepCopies);

            finder.Join(10000);
            var all = tabs ?? new List<TabRef>();
            var titles = all.Select(TerminalTabs.CurrentTitle).ToList();
            for (int i = 0; i < all.Count; i++) all[i].Title = titles[i];
            var candidates = Logic.CandidateIndices(titles).Select(i => all[i]).ToList();

            if (candidates.Count == 0)
            {
                Notify(Paster.ToClipboard(img), saved, Strings.T("toast.noTab"));
                return;
            }

            TabRef target;
            string template = null;
            bool direct = !Logic.NeedsPicker(titles) && s.DirectWhenSingle;
            if (direct) target = candidates[0];
            else
            {
                var pick = PickerWindow.Choose(candidates, img, area, s);
                if (pick == null) return;
                target = pick.Tab;
                template = pick.Template;
            }

            var result = Paster.Paste(target, img, template);
            if (result == Paster.Result.Pasted)
            {
                s.LastName = Logic.DisplayName(target.Title);
                s.LastHwnd = target.Hwnd.ToInt64();
                s.Save();
            }
            else if (result == Paster.Result.ClipboardFailed)
                Notify(false, saved, Strings.T("toast.clipboardBlocked"));
            else
                Notify(true, saved, Strings.T("toast.notDelivered"));
        }

        static void Notify(bool onClipboard, string savedPath, string reason)
        {
            string text = reason + "\n" + Strings.T(onClipboard ? "toast.onClipboard" : "toast.notOnClipboard");
            Toast.Show(text, savedPath != null);
        }
    }
}
