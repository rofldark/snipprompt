using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace SnipPrompt
{
    /// Full-screen overlay over all monitors: shows a frozen, dimmed screenshot.
    /// Frame mode: drag a rectangle. Window mode: the window under the mouse is highlighted, a click takes it.
    /// Esc or right click cancels. Shift held on release/click is reported (opens the annotation bar).
    sealed class CaptureOverlay : Form
    {
        readonly Bitmap screen;
        readonly Bitmap dimmed;
        readonly Rectangle virtualBounds;
        readonly bool windowMode;
        readonly List<Rectangle> windows; // overlay coordinates, top-most first
        Point start;
        Rectangle selection;
        bool dragging;
        bool shiftOnRelease;
        Bitmap result;

        CaptureOverlay(Bitmap screen, Rectangle virtualBounds, bool windowMode, List<Rectangle> windows)
        {
            this.screen = screen;
            this.virtualBounds = virtualBounds;
            this.windowMode = windowMode;
            this.windows = windows;
            dimmed = new Bitmap(screen.Width, screen.Height, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(dimmed))
            {
                g.DrawImageUnscaled(screen, 0, 0);
                using (var dim = new SolidBrush(Color.FromArgb(110, 0, 0, 0))) g.FillRectangle(dim, 0, 0, screen.Width, screen.Height);
            }
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = virtualBounds;
            TopMost = true;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            Cursor = windowMode ? Cursors.Hand : Cursors.Cross;
            KeyPreview = true;
        }

        /// Returns the selected area as a bitmap (null when cancelled), its screen rectangle
        /// and whether Shift was held when the selection was made.
        public static Bitmap Run(bool windowMode, out Rectangle screenRect, out bool shift)
        {
            Rectangle vb = SystemInformation.VirtualScreen;
            var windows = windowMode ? VisibleWindows(vb) : new List<Rectangle>();
            var shot = Grab(vb);

            using (var f = new CaptureOverlay(shot, vb, windowMode, windows))
            {
                f.Shown += delegate { f.Activate(); f.Focus(); };
                f.ShowDialog();
                Rectangle s = f.selection;
                screenRect = new Rectangle(s.X + vb.Left, s.Y + vb.Top, s.Width, s.Height);
                shift = f.shiftOnRelease;
                f.dimmed.Dispose();
                shot.Dispose();
                return f.result;
            }
        }

        /// Screenshot of the given screen area including layered windows (tooltips, popups).
        /// Graphics.CopyFromScreen rejects SourceCopy|CaptureBlt, so BitBlt is called directly.
        public static Bitmap Grab(Rectangle vb)
        {
            var shot = new Bitmap(vb.Width, vb.Height, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(shot))
            {
                IntPtr dst = g.GetHdc();
                IntPtr src = GetDC(IntPtr.Zero);
                try
                {
                    if (!BitBlt(dst, 0, 0, vb.Width, vb.Height, src, vb.Left, vb.Top, SRCCOPY | CAPTUREBLT))
                        throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                }
                finally
                {
                    ReleaseDC(IntPtr.Zero, src);
                    g.ReleaseHdc(dst);
                }
            }
            return shot;
        }

        const int SRCCOPY = 0x00CC0020, CAPTUREBLT = 0x40000000;
        [DllImport("gdi32.dll", SetLastError = true)] static extern bool BitBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, int rop);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Close();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right) { Close(); return; }
            if (e.Button != MouseButtons.Left) return;
            if (windowMode)
            {
                Rectangle w = WindowAt(e.Location);
                if (w.IsEmpty) return;
                selection = w;
                Finish();
                return;
            }
            dragging = true;
            start = e.Location;
            SetSelection(Rectangle.Empty);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (windowMode) { SetSelection(WindowAt(e.Location)); return; }
            if (!dragging) return;
            SetSelection(Logic.Normalize(start, e.Location));
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (windowMode || !dragging || e.Button != MouseButtons.Left) return;
            dragging = false;
            selection = Logic.Normalize(start, e.Location);
            if (Logic.IsTooSmall(selection)) { selection = Rectangle.Empty; Close(); return; }
            Finish();
        }

        void Finish()
        {
            shiftOnRelease = (Control.ModifierKeys & Keys.Shift) != 0;
            result = screen.Clone(selection, PixelFormat.Format32bppArgb);
            Close();
        }

        /// Repaints only the area that changed, so dragging stays smooth on large desktops.
        void SetSelection(Rectangle r)
        {
            if (r == selection) return;
            Rectangle old = selection;
            selection = r;
            Invalidate(Grow(old));
            Invalidate(Grow(r));
        }

        static Rectangle Grow(Rectangle r)
        {
            if (r.IsEmpty) return r;
            r.Inflate(4, 30); // border plus the size label above or below
            r.Width += 60;
            return r;
        }

        Rectangle WindowAt(Point p)
        {
            foreach (var w in windows) if (w.Contains(p)) return w;
            return Rectangle.Empty;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.DrawImageUnscaled(dimmed, 0, 0);
            if (selection.Width <= 0 || selection.Height <= 0) return;

            g.DrawImage(screen, selection, selection, GraphicsUnit.Pixel);
            Color border = windowMode ? Color.FromArgb(37, 99, 235) : Color.White;
            using (var pen = new Pen(border, windowMode ? 3 : 1))
                g.DrawRectangle(pen, selection.X, selection.Y, selection.Width - 1, selection.Height - 1);

            string label = selection.Width + " × " + selection.Height;
            using (var font = new Font("Segoe UI", 9f))
            {
                var size = g.MeasureString(label, font);
                float ly = selection.Y > size.Height + 4 ? selection.Y - size.Height - 4 : selection.Bottom + 4;
                using (var bg = new SolidBrush(Color.FromArgb(225, 255, 255, 255)))
                    g.FillRectangle(bg, selection.X, ly, size.Width + 6, size.Height + 2);
                g.DrawString(label, font, Brushes.Black, selection.X + 3, ly + 1);
            }
        }

        // ---- window mode: visible top-level windows in z-order (overlay coordinates) ----

        static List<Rectangle> VisibleWindows(Rectangle vb)
        {
            var list = new List<Rectangle>();
            int ownPid = Process.GetCurrentProcess().Id;
            EnumWindows((hwnd, _) =>
            {
                if (!IsWindowVisible(hwnd) || IsIconic(hwnd)) return true;
                uint pid; GetWindowThreadProcessId(hwnd, out pid);
                if (pid == ownPid) return true;
                int cloaked;
                if (DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out cloaked, 4) == 0 && cloaked != 0) return true;
                int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
                if ((ex & WS_EX_TRANSPARENT) != 0) return true;
                if ((ex & WS_EX_TOOLWINDOW) != 0 && GetWindowTextLength(hwnd) == 0) return true;
                if ((ex & WS_EX_LAYERED) != 0)
                {
                    uint key; byte alpha; uint flags;
                    if (GetLayeredWindowAttributes(hwnd, out key, out alpha, out flags) && (flags & LWA_ALPHA) != 0 && alpha == 0) return true;
                }
                var cls = new StringBuilder(64);
                GetClassName(hwnd, cls, 64);
                string c = cls.ToString();
                if (c == "Progman" || c == "WorkerW" || c == "Shell_TrayWnd" || c == "Shell_SecondaryTrayWnd") return true;
                RECT r;
                if (DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out r, Marshal.SizeOf(typeof(RECT))) != 0)
                    if (!GetWindowRect(hwnd, out r)) return true;
                var rect = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
                rect.Intersect(vb);
                if (rect.Width < 8 || rect.Height < 8) return true;
                rect.Offset(-vb.Left, -vb.Top);
                list.Add(rect);
                return true;
            }, IntPtr.Zero);
            // Desktop as last resort: whole monitor under the mouse.
            foreach (var s in Screen.AllScreens)
            {
                var b = s.Bounds;
                b.Offset(-vb.Left, -vb.Top);
                list.Add(b);
            }
            return list;
        }

        [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
        delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);
        const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
        const int DWMWA_CLOAKED = 14;
        const int GWL_EXSTYLE = -20, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_LAYERED = 0x80000;
        const uint LWA_ALPHA = 0x2;
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool GetLayeredWindowAttributes(IntPtr hwnd, out uint key, out byte alpha, out uint flags);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out RECT r);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, StringBuilder sb, int max);
        [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out RECT value, int size);
        [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out int value, int size);
    }
}
