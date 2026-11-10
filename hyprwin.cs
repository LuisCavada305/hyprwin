// HyprWin: tiling estilo Hyprland usando apenas Win32 + .NET Framework (sem instalar nada).
// Compilado em tempo de execução por hyprwin.ps1 (C# 5, Windows PowerShell 5.1).
// Cada monitor mostra um workspace ativo; workspaces podem ser fixados a um monitor no hyprwin.conf.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace HyprWin
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int L, T, R, B;
        public RECT(int l, int t, int r, int b) { L = l; T = t; R = r; B = b; }
        public int W { get { return R - L; } }
        public int H { get { return B - T; } }
        public int CX { get { return (L + R) / 2; } }
        public int CY { get { return (T + B) / 2; } }
        public bool Contains(int x, int y) { return x >= L && x < R && y >= T && y < B; }
    }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public IntPtr extra; }
    [StructLayout(LayoutKind.Sequential)] public struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData, flags, time; public IntPtr extra; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct MONITORINFOEX
    {
        public int cbSize; public RECT rcMonitor, rcWork; public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szDevice;
    }
    [StructLayout(LayoutKind.Sequential)] public struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr extra; }
    [StructLayout(LayoutKind.Sequential)] public struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr extra; }
    [StructLayout(LayoutKind.Explicit)] public struct INPUTUNION { [FieldOffset(0)] public KEYBDINPUT ki; [FieldOffset(0)] public MOUSEINPUT mi; }
    [StructLayout(LayoutKind.Sequential)] public struct INPUT { public uint type; public INPUTUNION u; }
    [StructLayout(LayoutKind.Sequential)] public struct APPBARDATA { public int cbSize; public IntPtr hWnd; public uint uCallbackMessage, uEdge; public RECT rc; public IntPtr lParam; }
    [StructLayout(LayoutKind.Sequential)] public struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public POINT pt; }

    public delegate bool EnumWindowsProc(IntPtr h, IntPtr l);
    public delegate bool MonitorEnumProc(IntPtr hMon, IntPtr hdc, ref RECT r, IntPtr d);
    public delegate IntPtr HookProc(int code, IntPtr w, IntPtr l);
    public delegate void WinEventProc(IntPtr hook, uint ev, IntPtr hwnd, int idObj, int idChild, uint thread, uint time);

    public static class N
    {
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr l);
        [DllImport("user32.dll")] public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc cb, IntPtr d);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool GetMonitorInfo(IntPtr h, ref MONITORINFOEX mi);
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr h, uint f);
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromPoint(POINT p, uint f);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr h);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowTextLength(IntPtr h);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] public static extern int GetWindowLong(IntPtr h, int i);
        [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h, uint c);
        [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h, uint f);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);
        [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr h, int c);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
        [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool f);
        [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] public static extern bool PostThreadMessage(uint t, uint m, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT p);
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll", SetLastError = true)] public static extern IntPtr SetWindowsHookEx(int id, HookProc p, IntPtr mod, uint tid);
        [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr h, int c, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr h);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetModuleHandle(string n);
        [DllImport("user32.dll")] public static extern int GetMessage(out MSG m, IntPtr h, uint a, uint b);
        [DllImport("user32.dll")] public static extern bool TranslateMessage(ref MSG m);
        [DllImport("user32.dll")] public static extern IntPtr DispatchMessage(ref MSG m);
        [DllImport("user32.dll")] public static extern uint SendInput(uint n, INPUT[] i, int size);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vk);
        [DllImport("user32.dll")] public static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr mod, WinEventProc p, uint pid, uint tid, uint flags);
        [DllImport("user32.dll")] public static extern bool UnhookWinEvent(IntPtr h);
        [DllImport("user32.dll")] public static extern bool SystemParametersInfo(uint a, uint p, IntPtr pv, uint f);
        [DllImport("user32.dll")] public static extern bool SystemParametersInfo(uint a, uint p, ref int pv, uint f);
        [DllImport("shell32.dll")] public static extern IntPtr SHAppBarMessage(uint m, ref APPBARDATA d);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int size);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out int v, int size);
        [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr h, int a, ref uint v, int size);
        [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr v);
        [DllImport("user32.dll")] public static extern IntPtr GetShellWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string c, string t);
        [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint a, bool i, uint pid);
        [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern bool QueryFullProcessImageName(IntPtr h, uint f, StringBuilder s, ref int n);
        [DllImport("advapi32.dll")] public static extern bool OpenProcessToken(IntPtr p, uint a, out IntPtr t);
        [DllImport("advapi32.dll")] public static extern bool GetTokenInformation(IntPtr t, int c, out int v, int len, out int ret);
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr h);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr h);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        public static extern void SHCreateItemFromParsingName(string path, IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);
    }

    [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int cx, cy; }

    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellItemImageFactory { [PreserveSig] int GetImage(SIZE size, int flags, out IntPtr phbm); }

    public class Win
    {
        public IntPtr H;
        public int Ws;
        public bool Floating, Fullscreen, Flip;
        public double Ratio = 0.5;
        public string Exe = "", Cls = "";
        public RECT Node; public bool SplitVert;
        public RECT Target; public bool HasTarget; public int Retries;
        public DateTime Grace = DateTime.MinValue;
        public RECT FloatRect; public bool HasFloatRect;
        public uint Border = 0xFFFFFFFE;
        public bool PopIn; public int SlideDx;   // efeito da próxima animação
    }

    public class Anim { public Win W; public RECT From, To, Now; public int Start, Dl, Dt, Dr, Db; }

    public class Bind { public int Mods; public uint Key; public bool Mouse; public string Action = ""; public string Arg = ""; }
    public class Rule { public string Effect; public string Field; public Regex Re; }
    public class Mon { public string Name; public RECT Work, Bounds; public bool Primary; }

    public class Host : Form
    {
        protected override void WndProc(ref Message m)
        {
            if (m.Msg > App.WM_APP && m.Msg < App.WM_APP + 16) { App.OnAppMessage(m.Msg, m.WParam); return; }
            base.WndProc(ref m);
        }
    }

    public class Osd : Form
    {
        public int Cur = 1, Count = 10; public float DpiScale = 1f;
        public bool[] Occupied = new bool[11], OnScreen = new bool[11];
        public Osd()
        {
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
            StartPosition = FormStartPosition.Manual; BackColor = Color.FromArgb(30, 30, 46);
            DoubleBuffered = true; Opacity = 0.92; Text = "HyprWinOsd";
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x80 | 0x08000000 | 0x8 | 0x20; return cp; }
        }
        public Size Measure()
        {
            int box = (int)(30 * DpiScale), gap = (int)(6 * DpiScale), pad = (int)(10 * DpiScale);
            return new Size(pad * 2 + Count * box + (Count - 1) * gap, pad * 2 + box);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            int box = (int)(30 * DpiScale), gap = (int)(6 * DpiScale), pad = (int)(10 * DpiScale);
            using (Font f = new Font("Segoe UI", 11 * DpiScale, FontStyle.Bold, GraphicsUnit.Pixel))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                for (int i = 1; i <= Count; i++)
                {
                    Rectangle r = new Rectangle(pad + (i - 1) * (box + gap), pad, box, box);
                    Color fill, txt;
                    if (i == Cur) { fill = Color.FromArgb(51, 204, 255); txt = Color.FromArgb(30, 30, 46); }
                    else if (OnScreen[i]) { fill = Color.FromArgb(28, 92, 120); txt = Color.White; }
                    else if (Occupied[i]) { fill = Color.FromArgb(69, 71, 90); txt = Color.White; }
                    else { fill = Color.FromArgb(40, 40, 58); txt = Color.FromArgb(110, 110, 130); }
                    using (SolidBrush b = new SolidBrush(fill)) g.FillRectangle(b, r);
                    using (SolidBrush b = new SolidBrush(txt)) g.DrawString((i % 10).ToString(), f, b, r, sf);
                }
            }
        }
    }

    public class AppEntry { public string Name, Id; public Bitmap Icon; public bool IconTried, IsCommand; public int Score; }

    // Launcher no estilo rofi (drun + run): busca nos apps do menu Iniciar ou executa o texto como comando.
    public class Launcher : Form
    {
        public Func<List<AppEntry>> Source;
        public Func<string, int, Bitmap> IconLoader;
        public Action<AppEntry, bool> OnLaunch;   // (entrada, como administrador)
        public Dictionary<string, int> Uses = new Dictionary<string, int>();

        const int Rows = 8;
        static readonly Color Bg = Color.FromArgb(25, 23, 36), Overlay = Color.FromArgb(38, 35, 58),
            TextC = Color.FromArgb(224, 222, 244), Muted = Color.FromArgb(110, 106, 134),
            Iris = Color.FromArgb(196, 167, 231), Foam = Color.FromArgb(156, 207, 216);

        readonly TextBox box = new TextBox();
        readonly List<AppEntry> results = new List<AppEntry>();
        int sel, top;
        float s = 1f;
        Font font, glyphFont;
        string glyphSearch, glyphRun;

        int RowH { get { return (int)(38 * s); } }
        int HeadH { get { return (int)(56 * s); } }
        int Pad { get { return (int)(10 * s); } }
        int IconSize { get { return (int)(22 * s); } }

        public Launcher()
        {
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
            StartPosition = FormStartPosition.Manual; DoubleBuffered = true; BackColor = Bg; Text = "HyprWinLauncher";
            box.BorderStyle = BorderStyle.None; box.BackColor = Bg; box.ForeColor = TextC;
            Controls.Add(box);
            box.TextChanged += delegate { Filter(); };
            Deactivate += delegate { Hide(); };
            MouseMove += delegate(object o, MouseEventArgs e) { int i = RowAt(e.Y); if (i >= 0 && i != sel) { sel = i; Invalidate(); } };
            MouseDown += delegate(object o, MouseEventArgs e) { int i = RowAt(e.Y); if (e.Button == MouseButtons.Left && i >= 0) Launch(i, false); };
            MouseWheel += delegate(object o, MouseEventArgs e) { MoveSel(e.Delta > 0 ? -1 : 1); };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            uint round = 2, border = 0x00E7A7C4;   // cantos arredondados + borda iris (COLORREF)
            N.DwmSetWindowAttribute(Handle, 33, ref round, 4);
            N.DwmSetWindowAttribute(Handle, 34, ref border, 4);
        }

        static string PickFont(string[] names, string fallback)
        {
            foreach (FontFamily f in FontFamily.Families)
                foreach (string n in names) if (f.Name == n) return n;
            return fallback;
        }

        public void Open(RECT work, float scale)
        {
            s = scale;
            string nf = PickFont(new string[] { "JetBrainsMono NFM", "JetBrainsMono Nerd Font Mono", "JetBrainsMono Nerd Font" }, null);
            if (font != null) { font.Dispose(); glyphFont.Dispose(); }
            font = new Font(nf ?? "Segoe UI", 12.5f * s, FontStyle.Regular, GraphicsUnit.Pixel);
            if (nf != null) { glyphFont = new Font(nf, 15 * s, GraphicsUnit.Pixel); glyphSearch = ""; glyphRun = ""; }
            else { glyphFont = new Font("Segoe MDL2 Assets", 14 * s, GraphicsUnit.Pixel); glyphSearch = ""; glyphRun = ""; }

            int w = (int)(620 * s), h = HeadH + Rows * RowH + Pad * 2;
            Bounds = new Rectangle(work.CX - w / 2, work.T + work.H / 5, w, h);
            box.Font = font;
            box.Location = new Point((int)(48 * s), (HeadH - box.PreferredHeight) / 2 + 1);
            box.Width = w - (int)(48 * s) - Pad * 2;
            box.Text = "";
            Filter();
            Show();
            box.Focus();
        }

        int RowAt(int y)
        {
            int i = (y - HeadH - Pad) / Math.Max(1, RowH);
            if (y < HeadH + Pad || i >= Rows) return -1;
            i += top;
            return i < results.Count ? i : -1;
        }

        // Pontua o nome contra a busca: prefixo > início de palavra > substring > subsequência. -1 = sem match.
        static int Score(string name, string q)
        {
            if (q.Length == 0) return 0;
            string n = name.ToLowerInvariant();
            q = q.ToLowerInvariant();
            int idx = n.IndexOf(q, StringComparison.Ordinal);
            if (idx == 0) return 1000 - n.Length;
            if (idx > 0) return (IsWordStart(n, idx) ? 800 : 600) - idx;
            int qi = 0, score = 300, prev = -2;
            for (int i = 0; i < n.Length && qi < q.Length; i++)
            {
                if (n[i] != q[qi]) continue;
                score += 10;
                if (IsWordStart(n, i)) score += 15;
                if (prev == i - 1) score += 5;
                prev = i; qi++;
            }
            return qi == q.Length ? score - n.Length / 4 : -1;
        }

        static bool IsWordStart(string n, int i) { return i == 0 || " -_.(".IndexOf(n[i - 1]) >= 0; }

        void Filter()
        {
            string q = box.Text.Trim();
            results.Clear();
            if (q.StartsWith(">"))
            {
                string cmd = q.Substring(1).Trim();
                if (cmd.Length > 0) results.Add(new AppEntry { Name = cmd, IsCommand = true });
            }
            else
            {
                foreach (AppEntry e in Source())
                {
                    int sc = Score(e.Name, q);
                    if (sc < 0) continue;
                    int u;
                    Uses.TryGetValue(e.Id, out u);
                    e.Score = sc + Math.Min(u, 20) * (q.Length == 0 ? 1000 : 15);
                    results.Add(e);
                }
                results.Sort(delegate(AppEntry a, AppEntry b)
                {
                    int c = b.Score.CompareTo(a.Score);
                    return c != 0 ? c : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                });
                if (q.Length > 0) results.Add(new AppEntry { Name = q, IsCommand = true });
            }
            sel = 0; top = 0;
            Invalidate();
        }

        void MoveSel(int d)
        {
            if (results.Count == 0) return;
            sel = Math.Max(0, Math.Min(results.Count - 1, sel + d));
            if (sel < top) top = sel;
            if (sel >= top + Rows) top = sel - Rows + 1;
            Invalidate();
        }

        void Launch(int i, bool admin)
        {
            if (i < 0 || i >= results.Count) return;
            AppEntry e = results[i];
            Hide();
            OnLaunch(e, admin);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys k)
        {
            switch (k)
            {
                case Keys.Down: case Keys.Tab: case Keys.Control | Keys.J: case Keys.Control | Keys.N: MoveSel(1); return true;
                case Keys.Up: case Keys.Shift | Keys.Tab: case Keys.Control | Keys.K: case Keys.Control | Keys.P: MoveSel(-1); return true;
                case Keys.PageDown: MoveSel(Rows); return true;
                case Keys.PageUp: MoveSel(-Rows); return true;
                case Keys.Enter: Launch(sel, false); return true;
                case Keys.Control | Keys.Shift | Keys.Enter: Launch(sel, true); return true;
                case Keys.Escape: Hide(); return true;
            }
            return base.ProcessCmdKey(ref msg, k);
        }

        static GraphicsPath RoundRect(Rectangle r, int rad)
        {
            GraphicsPath p = new GraphicsPath();
            int d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            if (font == null) return;
            Graphics g = pe.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            TextFormatFlags left = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;

            TextRenderer.DrawText(g, glyphSearch, glyphFont, new Rectangle((int)(16 * s), 0, (int)(28 * s), HeadH), Iris, left);
            using (SolidBrush sep = new SolidBrush(Overlay)) g.FillRectangle(sep, Pad, HeadH - 1, Width - Pad * 2, 1);

            if (results.Count == 0)
            {
                TextRenderer.DrawText(g, "Nenhum resultado", font, new Rectangle(Pad * 2, HeadH + Pad, Width, RowH), Muted, left);
                return;
            }
            for (int i = 0; i < Rows; i++)
            {
                int idx = top + i;
                if (idx >= results.Count) break;
                AppEntry e = results[idx];
                Rectangle r = new Rectangle(Pad, HeadH + Pad + i * RowH, Width - Pad * 2, RowH);
                if (idx == sel)
                    using (GraphicsPath p = RoundRect(r, (int)(7 * s)))
                    using (SolidBrush b = new SolidBrush(Overlay)) g.FillPath(b, p);

                int ix = r.X + (int)(12 * s), iy = r.Y + (RowH - IconSize) / 2;
                int tx = ix + IconSize + (int)(14 * s);
                Color nameColor = idx == sel ? Iris : TextC;
                if (e.IsCommand)
                {
                    TextRenderer.DrawText(g, glyphRun, glyphFont, new Rectangle(ix, r.Y, IconSize + 4, RowH), Foam, left);
                    Size pre = TextRenderer.MeasureText(g, "Executar ", font, Size.Empty, left);
                    TextRenderer.DrawText(g, "Executar ", font, new Rectangle(tx, r.Y, pre.Width, RowH), Muted, left);
                    TextRenderer.DrawText(g, e.Name, font, new Rectangle(tx + pre.Width, r.Y, r.Right - tx - pre.Width - Pad, RowH), nameColor, left | TextFormatFlags.EndEllipsis);
                    continue;
                }
                if (!e.IconTried) { e.IconTried = true; try { e.Icon = IconLoader(e.Id, IconSize); } catch { } }
                if (e.Icon != null) g.DrawImage(e.Icon, ix, iy, IconSize, IconSize);
                TextRenderer.DrawText(g, e.Name, font, new Rectangle(tx, r.Y, r.Right - tx - Pad, RowH), nameColor, left | TextFormatFlags.EndEllipsis);
            }
        }
    }

    public static class App
    {
        public const int WM_APP = 0x8000;
        const int MSG_ACTION = WM_APP + 1, MSG_DRAGSTART = WM_APP + 2, MSG_DRAGMOVE = WM_APP + 3, MSG_DRAGEND = WM_APP + 4, MSG_RELOAD = WM_APP + 5, MSG_QUIT = WM_APP + 9;
        const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10, SWP_NOOWNERZORDER = 0x200, SWP_ASYNC = 0x4000;
        const int SW_HIDE = 0, SW_SHOWNOACTIVATE = 4, SW_SHOWNA = 8, SW_RESTORE = 9;
        const uint KEY_MOUSE_DOWN = 0x1001, KEY_MOUSE_UP = 0x1002, KEY_LMB = 0x2001, KEY_RMB = 0x2002;
        const int MOD_SHIFT = 1, MOD_CTRL = 2, MOD_ALT = 4, MOD_SUPER = 8;

        static readonly HashSet<string> ShellClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Windows.UI.Core.CoreWindow",
            "XamlExplorerHostIslandWindow", "TopLevelWindowForOverflowXamlIsland", "ForegroundStaging",
            "MultitaskingViewFrame", "NotifyIconOverflowWindow", "#32768", "ApplicationManager_ImmersiveShellWindow",
            "EdgeUiInputTopWndClass", "Shell_InputSwitchTopLevelWindow", "ThumbnailDeviceHelperWnd", "IME",
            "MSCTFIME UI", "Windows.Internal.Shell.TabProxyWindow", "SysShadow", "tooltips_class32",
            "DummyDWMListenerWindow", "PseudoConsoleWindow", "Xaml_WindowedPopupClass", "LivePreview"
        };

        static string Dir, ConfPath, StatePath, LogPath;
        static readonly List<Win> wins = new List<Win>();
        static readonly Dictionary<string, int> active = new Dictionary<string, int>();   // monitor -> workspace visível
        static readonly Dictionary<int, string> wsMon = new Dictionary<int, string>();    // workspace -> monitor
        static readonly Dictionary<int, IntPtr> lastFocus = new Dictionary<int, IntPtr>();
        static int trayWs = -1;
        static Host host; static IntPtr hostH; static Osd osd; static NotifyIcon tray; static IntPtr trayIcon;
        static System.Windows.Forms.Timer pollTimer, debounceTimer, osdTimer, reloadTimer;
        static FileSystemWatcher watcher;
        static uint myPid; static bool meElevated; static Mutex mutex;
        static readonly Dictionary<uint, string> exeCache = new Dictionary<uint, string>();
        static readonly Dictionary<uint, bool> elevCache = new Dictionary<uint, bool>();

        // config
        static int gapsIn = 5, gapsOut = 10, wsCount = 10, followDelay = 150;
        static uint colActive = 0xFFCC33, colInactive = 0x595959;
        static bool activeOn = true, inactiveOn = true, followMouse = true, autoHide = false, showOsd = true;
        static bool animOn = true; static int animMs = 220; static double animPopin = 0.85, animSlide = 0.12;
        static readonly Dictionary<IntPtr, Anim> anims = new Dictionary<IntPtr, Anim>();
        static System.Windows.Forms.Timer animTimer;

        // launcher
        static Launcher launcher;
        static List<AppEntry> apps = new List<AppEntry>();
        static DateTime appsAt = DateTime.MinValue;
        static readonly Dictionary<string, int> uses = new Dictionary<string, int>();
        static string HistPath;
        static System.Windows.Forms.Timer indexTimer;
        static volatile Bind[] binds = new Bind[0];
        static List<Rule> rules = new List<Rule>();
        static Dictionary<int, string> wsRule = new Dictionary<int, string>();       // workspace fixado -> monitor
        static Dictionary<string, int> monDefault = new Dictionary<string, int>();   // monitor -> workspace inicial

        // hook thread
        static uint hookThreadId; static IntPtr kbHook, msHook; static HookProc kbProc, msProc;
        static readonly HashSet<uint> swallowed = new HashSet<uint>();
        static volatile int dragMode; static uint dragUpMsg; static int movePending;

        // drag (main thread)
        static Win dragWin; static int dragKind, dragNode = -1; static POINT dragStart; static RECT dragStartRect;
        static Win nativeDrag; static RECT nativeStartRect;

        static WinEventProc winEvProc; static readonly List<IntPtr> evHooks = new List<IntPtr>();
        static int savedTracking = -1, savedTrkZ = -1, savedTrkTimeout = -1; static IntPtr savedAppbar = IntPtr.Zero; static bool appbarChanged;
        static bool cleaned;

        // ---------------------------------------------------------------- start

        public static void Run(string dir)
        {
            Dir = dir;
            ConfPath = Path.Combine(dir, "hyprwin.conf");
            StatePath = Path.Combine(dir, "hidden.state");
            LogPath = Path.Combine(dir, "hyprwin.log");
            try { if (File.Exists(LogPath) && new FileInfo(LogPath).Length > 1000000) File.Delete(LogPath); } catch { }

            bool created;
            mutex = new Mutex(true, "Local\\HyprWin", out created);
            if (!created) { Log("já está em execução"); return; }

            try { if (!N.SetProcessDpiAwarenessContext((IntPtr)(-4))) Log("DPI awareness não aplicado"); } catch { }
            myPid = (uint)Process.GetCurrentProcess().Id;
            meElevated = IsElevated(myPid);
            Log("start pid=" + myPid);

            RecoverState();
            LoadConfig();

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Log("ThreadException: " + e.Exception); };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { Log("Unhandled: " + e.ExceptionObject); Cleanup(); };
            AppDomain.CurrentDomain.ProcessExit += delegate { Cleanup(); };

            host = new Host(); host.Text = "HyprWinHost"; hostH = host.Handle;
            osd = new Osd();
            SetupTray();
            ApplySystemSettings();

            winEvProc = OnWinEvent;
            uint[][] ranges = { new uint[] { 0x0003, 0x0003 }, new uint[] { 0x000A, 0x000B }, new uint[] { 0x0016, 0x0017 },
                                new uint[] { 0x8001, 0x8003 }, new uint[] { 0x8017, 0x8018 } };
            foreach (uint[] r in ranges) evHooks.Add(N.SetWinEventHook(r[0], r[1], IntPtr.Zero, winEvProc, 0, 0, 2));

            debounceTimer = new System.Windows.Forms.Timer { Interval = 40 };
            debounceTimer.Tick += delegate { debounceTimer.Stop(); Guard(delegate { Refresh(false); Layout(); }); };
            pollTimer = new System.Windows.Forms.Timer { Interval = 500 };
            pollTimer.Tick += delegate { Guard(delegate { Refresh(false); Enforce(); }); };
            osdTimer = new System.Windows.Forms.Timer { Interval = 900 };
            osdTimer.Tick += delegate { osdTimer.Stop(); osd.Hide(); };
            reloadTimer = new System.Windows.Forms.Timer { Interval = 300 };
            reloadTimer.Tick += delegate { reloadTimer.Stop(); Guard(Reload); };
            animTimer = new System.Windows.Forms.Timer { Interval = 10 };
            animTimer.Tick += delegate { Guard(AnimTick); };
            HistPath = Path.Combine(dir, "launcher.history");
            LoadHistory();
            indexTimer = new System.Windows.Forms.Timer { Interval = 2000 };
            indexTimer.Tick += delegate { indexTimer.Stop(); indexTimer.Interval = 500; Guard(IndexApps); };
            indexTimer.Start();

            try
            {
                watcher = new FileSystemWatcher(dir, "hyprwin.conf");
                watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName;
                FileSystemEventHandler h = delegate { N.PostMessage(hostH, MSG_RELOAD, IntPtr.Zero, IntPtr.Zero); };
                watcher.Changed += h; watcher.Created += h; watcher.Renamed += delegate { N.PostMessage(hostH, MSG_RELOAD, IntPtr.Zero, IntPtr.Zero); };
                watcher.EnableRaisingEvents = true;
            }
            catch (Exception e) { Log("watcher: " + e.Message); }

            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += delegate { Guard(delegate { Layout(); UpdateTray(); }); };

            List<Mon> mons = Monitors();
            foreach (Mon m in mons) Log("monitor " + m.Name + (m.Primary ? " (principal)" : "") + " " + m.Bounds.W + "x" + m.Bounds.H);
            SyncMonitors(mons);
            Refresh(true);
            Layout();
            ApplyBorders();
            UpdateTray();
            pollTimer.Start();

            Thread t = new Thread(HookLoop); t.IsBackground = true; t.Start();
            Application.Run();
            Cleanup();
        }

        static void Guard(Action a) { try { a(); } catch (Exception e) { Log(e.ToString()); } }

        public static void Log(string s)
        {
            try { File.AppendAllText(LogPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + s + "\r\n"); } catch { }
        }

        // ---------------------------------------------------------------- config

        static string NormMon(string s) { return s.Trim().Replace("\\\\.\\", "").ToUpperInvariant(); }

        static void LoadConfig()
        {
            if (!File.Exists(ConfPath)) { Log("config não encontrada: " + ConfPath); return; }
            Dictionary<string, string> vars = new Dictionary<string, string>();
            List<Bind> nb = new List<Bind>(); List<Rule> nr = new List<Rule>();
            Dictionary<int, string> nws = new Dictionary<int, string>(); Dictionary<string, int> ndef = new Dictionary<string, int>();
            int ln = 0;
            foreach (string raw in File.ReadAllLines(ConfPath))
            {
                ln++;
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int eq = line.IndexOf('=');
                if (eq < 0) continue;
                string key = line.Substring(0, eq).Trim();
                string val = line.Substring(eq + 1).Trim();
                List<string> names = new List<string>(vars.Keys);
                names.Sort(delegate(string a, string b) { return b.Length.CompareTo(a.Length); });
                foreach (string n in names) val = val.Replace(n, vars[n]);
                if (key.StartsWith("$")) { vars[key] = val; continue; }
                key = key.Substring(key.LastIndexOf(':') + 1).ToLowerInvariant();
                try
                {
                    switch (key)
                    {
                        case "gaps_in": gapsIn = int.Parse(val); break;
                        case "gaps_out": gapsOut = int.Parse(val); break;
                        case "col.active_border": colActive = ParseColor(val, out activeOn); break;
                        case "col.inactive_border": colInactive = ParseColor(val, out inactiveOn); break;
                        case "follow_mouse": followMouse = val != "0" && val.ToLower() != "false"; break;
                        case "follow_mouse_delay": followDelay = int.Parse(val); break;
                        case "autohide_taskbar": autoHide = val != "0" && val.ToLower() != "false"; break;
                        case "osd": showOsd = val != "0" && val.ToLower() != "false"; break;
                        case "workspaces": wsCount = Math.Max(1, Math.Min(10, int.Parse(val))); break;
                        case "enabled": animOn = val != "0" && val.ToLower() != "false"; break;
                        case "duration": animMs = Math.Max(1, int.Parse(val)); break;
                        case "popin": animPopin = Math.Max(0.1, Math.Min(1.0, double.Parse(val.TrimEnd('%'), System.Globalization.CultureInfo.InvariantCulture) / (val.EndsWith("%") ? 100.0 : 1.0))); break;
                        case "workspace_slide": animSlide = double.Parse(val, System.Globalization.CultureInfo.InvariantCulture); break;
                        case "workspace":
                            {
                                // workspace = 6, monitor:DISPLAY2, default:true
                                string[] p = val.Split(',');
                                int n = int.Parse(p[0].Trim());
                                string mon = null; bool def = false;
                                for (int i = 1; i < p.Length; i++)
                                {
                                    int c = p[i].IndexOf(':');
                                    if (c < 0) continue;
                                    string k = p[i].Substring(0, c).Trim().ToLowerInvariant(), v = p[i].Substring(c + 1).Trim();
                                    if (k == "monitor") mon = NormMon(v);
                                    else if (k == "default") def = v.ToLowerInvariant() == "true" || v == "1";
                                }
                                if (mon == null) throw new Exception("workspace sem monitor:");
                                nws[n] = mon;
                                if (def) ndef[mon] = n;
                                break;
                            }
                        case "bind":
                        case "bindm":
                            {
                                string[] p = val.Split(new char[] { ',' }, 4);
                                if (p.Length < 3) throw new Exception("bind incompleto");
                                Bind b = new Bind();
                                b.Mods = ParseMods(p[0]);
                                b.Key = ParseKey(p[1].Trim());
                                b.Mouse = key == "bindm";
                                b.Action = p[2].Trim().ToLowerInvariant();
                                b.Arg = p.Length > 3 ? p[3].Trim() : "";
                                if (b.Key == 0) throw new Exception("tecla desconhecida: " + p[1]);
                                nb.Add(b);
                                break;
                            }
                        case "windowrule":
                        case "windowrulev2":
                            {
                                string[] p = val.Split(new char[] { ',' }, 2);
                                string cond = p[1].Trim();
                                int c = cond.IndexOf(':');
                                Rule r = new Rule();
                                r.Effect = p[0].Trim().ToLowerInvariant();
                                r.Field = cond.Substring(0, c).Trim().ToLowerInvariant();
                                r.Re = new Regex(cond.Substring(c + 1).Trim(), RegexOptions.IgnoreCase);
                                nr.Add(r);
                                break;
                            }
                    }
                }
                catch (Exception e) { Log("config linha " + ln + ": " + e.Message); }
            }
            binds = nb.ToArray();
            rules = nr;
            wsRule = nws;
            monDefault = ndef;
            Log("config: " + nb.Count + " binds, " + nr.Count + " regras, " + nws.Count + " workspaces fixados");
        }

        static void Reload()
        {
            LoadConfig();
            RestoreSystemSettings();
            ApplySystemSettings();
            foreach (Win w in wins) w.Border = 0xFFFFFFFE;
            Layout(); ApplyBorders(); UpdateTray();
        }

        static int ParseMods(string s)
        {
            int m = 0;
            foreach (string t in s.ToUpperInvariant().Split(new char[] { ' ', '_', '+' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (t == "SUPER" || t == "WIN" || t == "MOD4" || t == "LOGO") m |= MOD_SUPER;
                else if (t == "SHIFT") m |= MOD_SHIFT;
                else if (t == "CTRL" || t == "CONTROL") m |= MOD_CTRL;
                else if (t == "ALT" || t == "MOD1") m |= MOD_ALT;
            }
            return m;
        }

        static uint ParseKey(string s)
        {
            string k = s.ToUpperInvariant();
            if (k.Length == 1 && ((k[0] >= 'A' && k[0] <= 'Z') || (k[0] >= '0' && k[0] <= '9'))) return k[0];
            if (k.Length >= 2 && k[0] == 'F')
            {
                int f;
                if (int.TryParse(k.Substring(1), out f) && f >= 1 && f <= 24) return (uint)(0x6F + f);
            }
            switch (k)
            {
                case "RETURN": case "ENTER": return 0x0D;
                case "SPACE": return 0x20;
                case "TAB": return 0x09;
                case "ESCAPE": case "ESC": return 0x1B;
                case "BACKSPACE": return 0x08;
                case "DELETE": return 0x2E;
                case "INSERT": return 0x2D;
                case "HOME": return 0x24;
                case "END": return 0x23;
                case "PRIOR": case "PAGEUP": return 0x21;
                case "NEXT": case "PAGEDOWN": return 0x22;
                case "LEFT": return 0x25;
                case "UP": return 0x26;
                case "RIGHT": return 0x27;
                case "DOWN": return 0x28;
                case "MINUS": return 0xBD;
                case "EQUAL": return 0xBB;
                case "COMMA": return 0xBC;
                case "PERIOD": return 0xBE;
                case "SLASH": return 0xBF;
                case "GRAVE": return 0xC0;
                case "SEMICOLON": return 0xBA;
                case "BRACKETLEFT": return 0xDB;
                case "BRACKETRIGHT": return 0xDD;
                case "MOUSE_DOWN": return KEY_MOUSE_DOWN;
                case "MOUSE_UP": return KEY_MOUSE_UP;
                case "MOUSE:272": return KEY_LMB;
                case "MOUSE:273": return KEY_RMB;
            }
            return 0;
        }

        // Retorna COLORREF (0x00BBGGRR).
        static uint ParseColor(string v, out bool on)
        {
            string t = v.Trim().Split(' ')[0].ToLowerInvariant();
            on = t != "none";
            if (!on) return 0;
            bool argb = t.StartsWith("0x");
            t = t.Replace("rgba(", "").Replace("rgb(", "").Replace(")", "").Replace("0x", "").Replace("#", "");
            if (t.Length == 8) t = argb ? t.Substring(2) : t.Substring(0, 6);
            uint rgb = Convert.ToUInt32(t, 16);
            uint r = (rgb >> 16) & 0xFF, g = (rgb >> 8) & 0xFF, b = rgb & 0xFF;
            return (b << 16) | (g << 8) | r;
        }

        // ---------------------------------------------------------------- system settings

        static void ApplySystemSettings()
        {
            if (followMouse)
            {
                int v = 0; N.SystemParametersInfo(0x1000, 0, ref v, 0); savedTracking = v;
                int z = 0; N.SystemParametersInfo(0x100C, 0, ref z, 0); savedTrkZ = z;
                int t = 0; N.SystemParametersInfo(0x2002, 0, ref t, 0); savedTrkTimeout = t;
                N.SystemParametersInfo(0x1001, 0, (IntPtr)1, 2);
                N.SystemParametersInfo(0x100D, 0, IntPtr.Zero, 2);
                N.SystemParametersInfo(0x2003, 0, (IntPtr)followDelay, 2);
            }
            if (autoHide)
            {
                APPBARDATA a = new APPBARDATA(); a.cbSize = Marshal.SizeOf(a); a.hWnd = N.FindWindow("Shell_TrayWnd", null);
                savedAppbar = N.SHAppBarMessage(4, ref a);
                a.lParam = (IntPtr)1; N.SHAppBarMessage(10, ref a); appbarChanged = true;
            }
        }

        static void RestoreSystemSettings()
        {
            if (savedTracking >= 0) { N.SystemParametersInfo(0x1001, 0, (IntPtr)savedTracking, 2); savedTracking = -1; }
            if (savedTrkZ >= 0) { N.SystemParametersInfo(0x100D, 0, (IntPtr)savedTrkZ, 2); savedTrkZ = -1; }
            if (savedTrkTimeout >= 0) { N.SystemParametersInfo(0x2003, 0, (IntPtr)savedTrkTimeout, 2); savedTrkTimeout = -1; }
            if (appbarChanged)
            {
                APPBARDATA a = new APPBARDATA(); a.cbSize = Marshal.SizeOf(a); a.hWnd = N.FindWindow("Shell_TrayWnd", null);
                a.lParam = savedAppbar; N.SHAppBarMessage(10, ref a); appbarChanged = false;
            }
        }

        // ---------------------------------------------------------------- hooks (thread próprio)

        static void HookLoop()
        {
            hookThreadId = N.GetCurrentThreadId();
            kbProc = KbHook; msProc = MsHook;
            IntPtr mod = N.GetModuleHandle(null);
            kbHook = N.SetWindowsHookEx(13, kbProc, mod, 0);
            if (kbHook == IntPtr.Zero) Log("falha no hook de teclado: " + Marshal.GetLastWin32Error());
            msHook = N.SetWindowsHookEx(14, msProc, mod, 0);
            if (msHook == IntPtr.Zero) Log("falha no hook de mouse: " + Marshal.GetLastWin32Error());
            MSG m;
            while (N.GetMessage(out m, IntPtr.Zero, 0, 0) > 0) { N.TranslateMessage(ref m); N.DispatchMessage(ref m); }
            N.UnhookWindowsHookEx(kbHook); N.UnhookWindowsHookEx(msHook);
        }

        static bool Down(int vk) { return (N.GetAsyncKeyState(vk) & 0x8000) != 0; }

        static int CurMods()
        {
            int m = 0;
            if (Down(0x10)) m |= MOD_SHIFT;
            if (Down(0x11)) m |= MOD_CTRL;
            if (Down(0x12)) m |= MOD_ALT;
            if (Down(0x5B) || Down(0x5C)) m |= MOD_SUPER;
            return m;
        }

        static int FindBind(uint key, int mods, bool mouse)
        {
            Bind[] b = binds;
            for (int i = 0; i < b.Length; i++) if (b[i].Key == key && b[i].Mods == mods && b[i].Mouse == mouse) return i;
            return -1;
        }

        // Injeta uma tecla neutra (vk 0xE8) para o Windows não abrir o menu Iniciar ao soltar a tecla Win.
        static void MaskWin()
        {
            INPUT[] inp = new INPUT[2];
            inp[0].type = 1; inp[0].u.ki.wVk = 0xE8;
            inp[1].type = 1; inp[1].u.ki.wVk = 0xE8; inp[1].u.ki.dwFlags = 2;
            N.SendInput(2, inp, Marshal.SizeOf(typeof(INPUT)));
        }

        static IntPtr KbHook(int code, IntPtr w, IntPtr l)
        {
            if (code >= 0)
            {
                try
                {
                    KBDLLHOOKSTRUCT k = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(l, typeof(KBDLLHOOKSTRUCT));
                    if ((k.flags & 0x10) == 0)
                    {
                        int msg = w.ToInt32();
                        bool down = msg == 0x100 || msg == 0x104, up = msg == 0x101 || msg == 0x105;
                        uint vk = k.vkCode;
                        if (down && vk != 0x5B && vk != 0x5C)
                        {
                            if (swallowed.Contains(vk)) return (IntPtr)1;
                            int mods = CurMods();
                            int i = FindBind(vk, mods, false);
                            if (i >= 0)
                            {
                                swallowed.Add(vk);
                                if ((mods & MOD_SUPER) != 0) MaskWin();
                                N.PostMessage(hostH, MSG_ACTION, (IntPtr)i, IntPtr.Zero);
                                return (IntPtr)1;
                            }
                        }
                        else if (up && swallowed.Remove(vk)) return (IntPtr)1;
                    }
                }
                catch { }
            }
            return N.CallNextHookEx(IntPtr.Zero, code, w, l);
        }

        static IntPtr MsHook(int code, IntPtr w, IntPtr l)
        {
            if (code >= 0)
            {
                try
                {
                    int msg = w.ToInt32();
                    if (msg == 0x200)
                    {
                        if (dragMode != 0 && Interlocked.CompareExchange(ref movePending, 1, 0) == 0)
                            N.PostMessage(hostH, MSG_DRAGMOVE, IntPtr.Zero, IntPtr.Zero);
                    }
                    else
                    {
                        MSLLHOOKSTRUCT s = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(l, typeof(MSLLHOOKSTRUCT));
                        if ((s.flags & 1) == 0)
                        {
                            if ((msg == 0x201 || msg == 0x204) && dragMode == 0)
                            {
                                int mods = CurMods();
                                int i = FindBind(msg == 0x201 ? KEY_LMB : KEY_RMB, mods, true);
                                if (i >= 0)
                                {
                                    dragMode = 1; dragUpMsg = (uint)(msg + 1);
                                    if ((mods & MOD_SUPER) != 0) MaskWin();
                                    N.PostMessage(hostH, MSG_DRAGSTART, (IntPtr)i, IntPtr.Zero);
                                    return (IntPtr)1;
                                }
                            }
                            else if ((msg == 0x202 || msg == 0x205) && dragMode != 0 && (uint)msg == dragUpMsg)
                            {
                                dragMode = 0;
                                N.PostMessage(hostH, MSG_DRAGEND, IntPtr.Zero, IntPtr.Zero);
                                return (IntPtr)1;
                            }
                            else if (msg == 0x20A)
                            {
                                short delta = (short)(s.mouseData >> 16);
                                int mods = CurMods();
                                int i = FindBind(delta < 0 ? KEY_MOUSE_DOWN : KEY_MOUSE_UP, mods, false);
                                if (i >= 0)
                                {
                                    if ((mods & MOD_SUPER) != 0) MaskWin();
                                    N.PostMessage(hostH, MSG_ACTION, (IntPtr)i, IntPtr.Zero);
                                    return (IntPtr)1;
                                }
                            }
                        }
                    }
                }
                catch { }
            }
            return N.CallNextHookEx(IntPtr.Zero, code, w, l);
        }

        // ---------------------------------------------------------------- mensagens (thread principal)

        public static void OnAppMessage(int msg, IntPtr w)
        {
            try
            {
                Bind[] b = binds;
                int i = w.ToInt32();
                switch (msg)
                {
                    case MSG_ACTION: if (i < b.Length) Dispatch(b[i].Action, b[i].Arg); break;
                    case MSG_DRAGSTART: if (i < b.Length) DragStart(b[i].Action); break;
                    case MSG_DRAGMOVE: Interlocked.Exchange(ref movePending, 0); DragMove(); break;
                    case MSG_DRAGEND: DragEnd(); break;
                    case MSG_RELOAD: reloadTimer.Stop(); reloadTimer.Start(); break;
                    case MSG_QUIT: Quit(); break;
                }
            }
            catch (Exception e) { Log(e.ToString()); }
        }

        static void Dispatch(string action, string arg)
        {
            switch (action)
            {
                case "exec": Spawn(arg); break;
                case "killactive": Kill(); break;
                case "togglefloating": ToggleFloat(); break;
                case "fullscreen": ToggleFull(); break;
                case "togglesplit": ToggleSplit(); break;
                case "movefocus": MoveFocus(arg); break;
                case "movewindow": case "swapwindow": MoveWindowDir(arg); break;
                case "focusmonitor": FocusMonitorDir(arg); break;
                case "workspace": SwitchTo(ResolveWs(arg)); break;
                case "movetoworkspace": MoveToWs(ResolveWs(arg), true); break;
                case "movetoworkspacesilent": MoveToWs(ResolveWs(arg), false); break;
                case "exit": Quit(); break;
                case "reload": Reload(); break;
                case "launcher": ToggleLauncher(); break;
                case "sendshortcut": SendShortcut(arg); break;
                default: Log("ação desconhecida: " + action); break;
            }
        }

        // sendshortcut, MODS, TECLA[, campo:regex]: envia o atalho para a janela ativa.
        // Com filtro, envia só se a janela ativa casar com ele (campos: class, title, exe).
        static void SendShortcut(string arg)
        {
            string[] p = arg.Split(new char[] { ',' }, 3);
            if (p.Length < 2) throw new Exception("sendshortcut: use MODS, TECLA[, campo:regex]");
            int mods = ParseMods(p[0]);
            uint key = ParseKey(p[1].Trim());
            if (key == 0 || key > 0xFF) throw new Exception("sendshortcut: tecla desconhecida: " + p[1]);
            if (p.Length > 2 && p[2].Trim() != "")
            {
                string cond = p[2].Trim();
                int c = cond.IndexOf(':');
                if (c < 0) throw new Exception("sendshortcut: filtro sem campo: " + cond);
                Rule r = new Rule();
                r.Field = cond.Substring(0, c).Trim().ToLowerInvariant();
                r.Re = new Regex(cond.Substring(c + 1).Trim(), RegexOptions.IgnoreCase);
                IntPtr fg = N.GetForegroundWindow();
                uint pid; N.GetWindowThreadProcessId(fg, out pid);
                if (!MatchRule(r, ClassOf(fg), TitleOf(fg), ExeOf(pid))) return;
            }
            // Solta a tecla Win física para o app receber só o atalho. Depois pressiona Win de novo,
            // para outra bind funcionar enquanto Win continua presa, e mascara para não abrir o menu Iniciar.
            bool lwin = Down(0x5B), rwin = Down(0x5C);
            List<INPUT> l = new List<INPUT>();
            if (lwin) l.Add(KeyInput(0x5B, false));
            if (rwin) l.Add(KeyInput(0x5C, false));
            ushort[] mvk = { 0x11, 0x10, 0x12 };
            int[] mbit = { MOD_CTRL, MOD_SHIFT, MOD_ALT };
            for (int i = 0; i < 3; i++) if ((mods & mbit[i]) != 0) l.Add(KeyInput(mvk[i], true));
            l.Add(KeyInput((ushort)key, true));
            l.Add(KeyInput((ushort)key, false));
            for (int i = 2; i >= 0; i--) if ((mods & mbit[i]) != 0) l.Add(KeyInput(mvk[i], false));
            if (lwin) l.Add(KeyInput(0x5B, true));
            if (rwin) l.Add(KeyInput(0x5C, true));
            if (lwin || rwin) { l.Add(KeyInput(0xE8, true)); l.Add(KeyInput(0xE8, false)); }
            N.SendInput((uint)l.Count, l.ToArray(), Marshal.SizeOf(typeof(INPUT)));
        }

        static INPUT KeyInput(ushort vk, bool down)
        {
            INPUT i = new INPUT();
            i.type = 1;
            i.u.ki.wVk = vk;
            bool ext = (vk >= 0x21 && vk <= 0x28) || vk == 0x2D || vk == 0x2E || vk == 0x5B || vk == 0x5C;
            i.u.ki.dwFlags = (ext ? 1u : 0u) | (down ? 0u : 2u);
            return i;
        }

        // ---------------------------------------------------------------- launcher

        static void LoadHistory()
        {
            if (!File.Exists(HistPath)) return;
            foreach (string line in File.ReadAllLines(HistPath))
            {
                int tab = line.IndexOf('\t'), n;
                if (tab > 0 && int.TryParse(line.Substring(0, tab), out n)) uses[line.Substring(tab + 1)] = n;
            }
        }

        static void SaveHistory()
        {
            try
            {
                List<string> l = new List<string>();
                foreach (KeyValuePair<string, int> kv in uses) l.Add(kv.Value + "\t" + kv.Key);
                File.WriteAllLines(HistPath, l.ToArray());
            }
            catch (Exception e) { Log("history: " + e.Message); }
        }

        static readonly Regex NotAnApp = new Regex(@"uninstall|desinstal|readme|release notes|\bhelp\b|ajuda|documenta", RegexOptions.IgnoreCase);
        static readonly HashSet<string> DocExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".chm", ".html", ".htm", ".txt", ".pdf", ".url", ".md" };

        // Lista os apps do menu Iniciar (desktop e Store) via shell:AppsFolder.
        static void IndexApps()
        {
            Stopwatch sw = Stopwatch.StartNew();
            Dictionary<string, AppEntry> old = new Dictionary<string, AppEntry>();
            foreach (AppEntry e in apps) old[e.Id] = e;
            List<AppEntry> l = new List<AppEntry>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Type t = Type.GetTypeFromProgID("Shell.Application");
            object sh = Activator.CreateInstance(t);
            object folder = t.InvokeMember("NameSpace", BindingFlags.InvokeMethod, null, sh, new object[] { "shell:AppsFolder" });
            object items = folder.GetType().InvokeMember("Items", BindingFlags.InvokeMethod, null, folder, null);
            foreach (object it in (System.Collections.IEnumerable)items)
            {
                string name = it.GetType().InvokeMember("Name", BindingFlags.GetProperty, null, it, null) as string;
                string id = it.GetType().InvokeMember("Path", BindingFlags.GetProperty, null, it, null) as string;
                Marshal.ReleaseComObject(it);
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(id) || NotAnApp.IsMatch(name)) continue;
                string ext = "";
                try { ext = Path.GetExtension(id); } catch { }
                if (DocExt.Contains(ext) || !seen.Add(name)) continue;
                AppEntry prev;
                l.Add(old.TryGetValue(id, out prev) ? prev : new AppEntry { Name = name, Id = id });
            }
            Marshal.ReleaseComObject(items); Marshal.ReleaseComObject(folder); Marshal.ReleaseComObject(sh);
            apps = l;
            appsAt = DateTime.Now;
            Log("launcher: " + l.Count + " apps indexados em " + sw.ElapsedMilliseconds + " ms");
        }

        static bool IsFilePath(string id) { return id.Length > 2 && (id[1] == ':' || id.StartsWith("\\\\")); }

        static Bitmap LoadAppIcon(string id, int size)
        {
            IShellItemImageFactory f;
            N.SHCreateItemFromParsingName(IsFilePath(id) ? id : "shell:AppsFolder\\" + id, IntPtr.Zero, typeof(IShellItemImageFactory).GUID, out f);
            try
            {
                SIZE sz = new SIZE(); sz.cx = Math.Max(32, size); sz.cy = sz.cx;
                IntPtr hbm;
                if (f.GetImage(sz, 0x4, out hbm) != 0 || hbm == IntPtr.Zero) return null;
                try { return ToArgb(hbm); } finally { N.DeleteObject(hbm); }
            }
            finally { Marshal.ReleaseComObject(f); }
        }

        // Image.FromHbitmap descarta o alfa; copia os pixels como PArgb para manter a transparência.
        static Bitmap ToArgb(IntPtr hbm)
        {
            using (Bitmap src = Image.FromHbitmap(hbm))
            {
                if (Image.GetPixelFormatSize(src.PixelFormat) < 32) return new Bitmap(src);
                BitmapData d = src.LockBits(new Rectangle(0, 0, src.Width, src.Height), ImageLockMode.ReadOnly, src.PixelFormat);
                try
                {
                    using (Bitmap view = new Bitmap(d.Width, d.Height, d.Stride, PixelFormat.Format32bppPArgb, d.Scan0))
                        return new Bitmap(view);
                }
                finally { src.UnlockBits(d); }
            }
        }

        static void ToggleLauncher()
        {
            if (launcher == null)
            {
                launcher = new Launcher();
                launcher.Source = delegate { return apps; };
                launcher.IconLoader = LoadAppIcon;
                launcher.Uses = uses;
                launcher.OnLaunch = LaunchEntry;
                launcher.VisibleChanged += delegate { if (!launcher.Visible) LauncherClosed(); };
            }
            if (launcher.Visible) { launcher.Hide(); return; }
            if (apps.Count == 0) IndexApps();
            // O foco que segue o mouse fecharia o launcher ao mover o cursor; desliga enquanto ele está aberto.
            if (followMouse) N.SystemParametersInfo(0x1001, 0, IntPtr.Zero, 2);
            float sc;
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) sc = g.DpiX / 96f;
            Mon m = MonByName(Monitors(), FocusMon());
            launcher.Open(m != null ? m.Work : new RECT(0, 0, 1280, 720), sc);
            ForceForeground(launcher.Handle);
        }

        static void LauncherClosed()
        {
            if (followMouse) N.SystemParametersInfo(0x1001, 0, (IntPtr)1, 2);
            if ((DateTime.Now - appsAt).TotalMinutes > 10) indexTimer.Start();
        }

        static void ForceForeground(IntPtr h)
        {
            IntPtr fg = N.GetForegroundWindow();
            uint pid;
            uint fgT = N.GetWindowThreadProcessId(fg, out pid), me = N.GetCurrentThreadId();
            bool att = fgT != 0 && fgT != me && N.AttachThreadInput(me, fgT, true);
            N.SetForegroundWindow(h);
            N.BringWindowToTop(h);
            if (att) N.AttachThreadInput(me, fgT, false);
        }

        static void LaunchEntry(AppEntry e, bool admin)
        {
            if (!e.IsCommand)
            {
                int n;
                uses.TryGetValue(e.Id, out n);
                uses[e.Id] = n + 1;
                SaveHistory();
            }
            try
            {
                if (e.IsCommand) RunCommand(e.Name, admin);
                else if (IsFilePath(e.Id)) Process.Start(new ProcessStartInfo(e.Id) { UseShellExecute = true, Verb = admin ? "runas" : "" });
                else Process.Start("explorer.exe", "shell:AppsFolder\\" + e.Id);   // apps da Store não elevam por aqui
            }
            catch (Exception ex)
            {
                System.ComponentModel.Win32Exception w = ex as System.ComponentModel.Win32Exception;
                if (w == null || w.NativeErrorCode != 1223) Log("launcher '" + e.Name + "': " + ex.Message);   // 1223 = UAC cancelado
            }
        }

        // Igual à caixa Executar: expande %VARIAVEIS%, tenta ShellExecute e, se falhar
        // (ex.: .cmd no PATH), usa "cmd /c start". admin = Ctrl+Shift+Enter (pede UAC).
        static void RunCommand(string cmd, bool admin)
        {
            cmd = Environment.ExpandEnvironmentVariables(cmd);
            string file = cmd, args = "";
            if (cmd.StartsWith("\"")) { int q = cmd.IndexOf('"', 1); if (q > 0) { file = cmd.Substring(1, q - 1); args = cmd.Substring(q + 1).Trim(); } }
            else { int sp = cmd.IndexOf(' '); if (sp > 0) { file = cmd.Substring(0, sp); args = cmd.Substring(sp + 1).Trim(); } }
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(file, args);
                psi.UseShellExecute = true;
                if (admin) psi.Verb = "runas";
                psi.WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                Process.Start(psi);
            }
            catch
            {
                if (admin) throw;
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c start \"\" " + cmd);
                psi.UseShellExecute = false; psi.CreateNoWindow = true;
                psi.WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                Process.Start(psi);
            }
        }

        static void OnWinEvent(IntPtr hook, uint ev, IntPtr h, int idObj, int idChild, uint th, uint t)
        {
            if (idObj != 0 || idChild != 0) return;
            try
            {
                switch (ev)
                {
                    case 0x0003:
                        ApplyBorders();
                        { Win w = Find(h); if (w != null && IsActive(w.Ws)) lastFocus[w.Ws] = h; }
                        if (Cur() != trayWs) UpdateTray();
                        RequestRefresh();
                        break;
                    case 0x000A: MoveSizeStart(h); break;
                    case 0x000B: MoveSizeEnd(h); break;
                    default: RequestRefresh(); break;
                }
            }
            catch (Exception e) { Log(e.ToString()); }
        }

        static void RequestRefresh() { if (!debounceTimer.Enabled) debounceTimer.Start(); }

        // ---------------------------------------------------------------- monitores e workspaces

        static List<Mon> Monitors()
        {
            List<Mon> l = new List<Mon>();
            N.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, delegate(IntPtr hm, IntPtr dc, ref RECT rc, IntPtr d)
            {
                MONITORINFOEX mi = new MONITORINFOEX(); mi.cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));
                if (N.GetMonitorInfo(hm, ref mi))
                {
                    Mon m = new Mon(); m.Name = NormMon(mi.szDevice); m.Work = mi.rcWork; m.Bounds = mi.rcMonitor; m.Primary = (mi.dwFlags & 1) != 0;
                    if (m.Primary) l.Insert(0, m); else l.Add(m);
                }
                return true;
            }, IntPtr.Zero);
            return l;
        }

        static Mon MonByName(List<Mon> mons, string name)
        {
            if (name == null) return null;
            foreach (Mon m in mons) if (m.Name == name) return m;
            return null;
        }

        static string MonName(IntPtr hm)
        {
            MONITORINFOEX mi = new MONITORINFOEX(); mi.cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));
            return N.GetMonitorInfo(hm, ref mi) ? NormMon(mi.szDevice) : "";
        }

        static string MonAt(POINT p) { return MonName(N.MonitorFromPoint(p, 2)); }
        static string MonOfWindow(IntPtr h) { return MonName(N.MonitorFromWindow(h, 2)); }
        static string CursorMon() { POINT p; N.GetCursorPos(out p); return MonAt(p); }

        static bool IsActive(int ws) { return active.ContainsValue(ws); }

        static int ActiveOn(string mon, int fallback)
        {
            int ws;
            return mon != null && active.TryGetValue(mon, out ws) ? ws : fallback;
        }

        // Monitor em foco: o da janela ativa gerenciada; senão, o do cursor.
        static string FocusMon()
        {
            Win w = Focused();
            string m;
            if (w != null && IsActive(w.Ws) && wsMon.TryGetValue(w.Ws, out m)) return m;
            return CursorMon();
        }

        static int Cur()
        {
            string m = FocusMon();
            int ws;
            if (active.TryGetValue(m, out ws)) return ws;
            foreach (int v in active.Values) return v;
            return 1;
        }

        static bool WsAllowedOn(int ws, string mon)
        {
            string r;
            return !wsRule.TryGetValue(ws, out r) || r == mon;
        }

        static bool WsOccupied(int ws)
        {
            foreach (Win w in wins) if (w.Ws == ws) return true;
            return false;
        }

        // Garante que cada monitor conectado tem um workspace visível e que nenhum workspace aponta para monitor ausente.
        static void SyncMonitors(List<Mon> mons)
        {
            string primary = mons.Count > 0 ? mons[0].Name : "";
            DateTime g = DateTime.Now.AddMilliseconds(1500);
            foreach (string m in new List<string>(active.Keys))
            {
                if (MonByName(mons, m) != null) continue;
                int ws = active[m];
                active.Remove(m);
                foreach (Win w in wins) if (w.Ws == ws) { N.ShowWindowAsync(w.H, SW_HIDE); w.Grace = g; }
                Log("monitor " + m + " desconectado; workspace " + ws + " oculto");
            }
            foreach (int ws in new List<int>(wsMon.Keys))
            {
                if (MonByName(mons, wsMon[ws]) != null) continue;
                string r;
                wsMon[ws] = wsRule.TryGetValue(ws, out r) && MonByName(mons, r) != null ? r : primary;
            }
            foreach (Mon m in mons)
            {
                if (active.ContainsKey(m.Name)) continue;
                int pick = 0, def;
                if (monDefault.TryGetValue(m.Name, out def) && !IsActive(def)) pick = def;
                for (int pass = 0; pass < 2 && pick == 0; pass++)
                    for (int ws = 1; ws <= wsCount && pick == 0; ws++)
                    {
                        string owner;
                        if (IsActive(ws) || !WsAllowedOn(ws, m.Name)) continue;
                        if (wsMon.TryGetValue(ws, out owner) && owner != m.Name) continue;
                        if (pass == 0 && WsOccupied(ws)) continue;
                        pick = ws;
                    }
                if (pick == 0) continue;
                active[m.Name] = pick;
                wsMon[pick] = m.Name;
                foreach (Win w in wins) if (w.Ws == pick) { N.ShowWindowAsync(w.H, SW_SHOWNA); w.Grace = g; }
                Log("monitor " + m.Name + " mostra workspace " + pick);
            }
        }

        static void ForgetIfEmpty(int ws)
        {
            if (ws > 0 && !IsActive(ws) && !WsOccupied(ws)) wsMon.Remove(ws);
        }

        // ---------------------------------------------------------------- janelas

        static Win Find(IntPtr h)
        {
            for (int i = 0; i < wins.Count; i++) if (wins[i].H == h) return wins[i];
            return null;
        }

        static Win Focused()
        {
            IntPtr fg = N.GetForegroundWindow();
            Win w = Find(fg);
            if (w == null) w = Find(N.GetAncestor(fg, 3));
            return w;
        }

        static string ClassOf(IntPtr h) { StringBuilder sb = new StringBuilder(256); N.GetClassName(h, sb, 256); return sb.ToString(); }
        static string TitleOf(IntPtr h) { StringBuilder sb = new StringBuilder(512); N.GetWindowText(h, sb, 512); return sb.ToString(); }

        static string ExeOf(uint pid)
        {
            string s;
            if (exeCache.TryGetValue(pid, out s)) return s;
            s = "";
            IntPtr p = N.OpenProcess(0x1000, false, pid);
            if (p != IntPtr.Zero)
            {
                StringBuilder sb = new StringBuilder(1024); int n = 1024;
                if (N.QueryFullProcessImageName(p, 0, sb, ref n)) s = Path.GetFileName(sb.ToString());
                N.CloseHandle(p);
            }
            exeCache[pid] = s;
            return s;
        }

        static bool IsElevated(uint pid)
        {
            bool e;
            if (elevCache.TryGetValue(pid, out e)) return e;
            e = false;
            IntPtr p = N.OpenProcess(0x1000, false, pid);
            if (p != IntPtr.Zero)
            {
                IntPtr tok;
                if (N.OpenProcessToken(p, 0x8, out tok))
                {
                    int v, ret;
                    if (N.GetTokenInformation(tok, 20, out v, 4, out ret)) e = v != 0;
                    N.CloseHandle(tok);
                }
                N.CloseHandle(p);
            }
            elevCache[pid] = e;
            return e;
        }

        static bool IsCloaked(IntPtr h) { int v; return N.DwmGetWindowAttribute(h, 14, out v, 4) == 0 && v != 0; }

        static bool ExtRect(IntPtr h, out RECT r)
        {
            if (N.DwmGetWindowAttribute(h, 9, out r, Marshal.SizeOf(typeof(RECT))) == 0) return true;
            return N.GetWindowRect(h, out r);
        }

        static bool MatchRule(Rule r, string cls, string title, string exe)
        {
            string v = r.Field == "class" ? cls : r.Field == "title" ? title : (r.Field == "exe" || r.Field == "process") ? exe : null;
            return v != null && r.Re.IsMatch(v);
        }

        // Retorna a janela se ela deve ser gerenciada (Ws ainda não definido), ou null.
        static Win Classify(IntPtr h)
        {
            if (!N.IsWindowVisible(h)) return null;
            uint pid; N.GetWindowThreadProcessId(h, out pid);
            if (pid == myPid) return null;
            int st = N.GetWindowLong(h, -16), ex = N.GetWindowLong(h, -20);
            if ((st & 0x40000000) != 0) return null;
            if ((ex & 0x80) != 0 && (ex & 0x40000) == 0) return null;
            if ((ex & 0x08000000) != 0) return null;
            if (N.GetWindowTextLength(h) == 0) return null;
            if (IsCloaked(h)) return null;
            string cls = ClassOf(h);
            if (ShellClasses.Contains(cls)) return null;
            if (!N.IsIconic(h)) { RECT r; N.GetWindowRect(h, out r); if (r.W < 40 || r.H < 30) return null; }
            if (!meElevated && IsElevated(pid)) return null;
            string exe = ExeOf(pid), title = TitleOf(h);
            IntPtr owner = N.GetWindow(h, 4);
            bool floating = owner != IntPtr.Zero || (st & 0x00040000) == 0 || (ex & 0x8) != 0 || cls == "#32770";
            foreach (Rule r in rules)
            {
                if (!MatchRule(r, cls, title, exe)) continue;
                if (r.Effect == "ignore") return null;
                if (r.Effect == "float") floating = true;
                if (r.Effect == "tile") floating = false;
            }
            Win w = new Win();
            w.H = h; w.Exe = exe; w.Cls = cls; w.Floating = floating;
            return w;
        }

        static List<IntPtr> TopLevel()
        {
            List<IntPtr> l = new List<IntPtr>();
            N.EnumWindows(delegate(IntPtr h, IntPtr p) { l.Add(h); return true; }, IntPtr.Zero);
            return l;
        }

        static void Refresh(bool initial)
        {
            bool changed = false;
            DateTime now = DateTime.Now;
            for (int i = wins.Count - 1; i >= 0; i--)
            {
                Win w = wins[i];
                if (!N.IsWindow(w.H)) { wins.RemoveAt(i); changed = true; continue; }
                if (now < w.Grace) continue;
                bool vis = N.IsWindowVisible(w.H) && !IsCloaked(w.H);
                if (IsActive(w.Ws)) { if (!vis) { wins.RemoveAt(i); changed = true; } }
                else if (vis) { w.Ws = ActiveOn(MonOfWindow(w.H), Cur()); changed = true; }
            }
            if (changed) foreach (int ws in new List<int>(wsMon.Keys)) ForgetIfEmpty(ws);
            Win focused = Focused();
            string cursorMon = CursorMon();
            foreach (IntPtr h in TopLevel())
            {
                if (Find(h) != null) continue;
                Win w = Classify(h);
                if (w == null) continue;
                Win owner = N.GetWindow(h, 4) != IntPtr.Zero ? Find(N.GetAncestor(h, 3)) : null;
                if (owner != null) w.Ws = owner.Ws;
                else w.Ws = ActiveOn((initial || w.Floating) ? MonOfWindow(h) : cursorMon, Cur());
                w.PopIn = animOn && !initial && !w.Floating;
                int at = focused != null && focused.Ws == w.Ws ? wins.IndexOf(focused) : -1;
                if (at >= 0 && !initial) wins.Insert(at + 1, w); else wins.Add(w);
                changed = true;
                Log("gerenciar " + w.Exe + " [" + w.Cls + "] ws=" + w.Ws + " float=" + w.Floating);
            }
            if (changed) { ApplyBorders(); SaveState(); UpdateTray(); }
        }

        // ---------------------------------------------------------------- layout (dwindle)

        static List<Win> TiledOn(int ws)
        {
            List<Win> t = new List<Win>();
            foreach (Win w in wins) if (w.Ws == ws && !w.Floating && !N.IsIconic(w.H)) t.Add(w);
            return t;
        }

        static bool IsAppFullscreen(IntPtr h)
        {
            RECT wr; N.GetWindowRect(h, out wr);
            MONITORINFOEX mi = new MONITORINFOEX(); mi.cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));
            N.GetMonitorInfo(N.MonitorFromWindow(h, 2), ref mi);
            int st = N.GetWindowLong(h, -16);
            return wr.L <= mi.rcMonitor.L && wr.T <= mi.rcMonitor.T && wr.R >= mi.rcMonitor.R && wr.B >= mi.rcMonitor.B
                && (st & 0x00C00000) != 0x00C00000;
        }

        static void Layout()
        {
            List<Mon> mons = Monitors();
            SyncMonitors(mons);
            foreach (Mon m in mons)
            {
                int ws;
                if (!active.TryGetValue(m.Name, out ws)) continue;
                List<Win> t = TiledOn(ws);
                Win full = null;
                foreach (Win w in t) if (w.Fullscreen) full = w;
                if (full != null) { Place(full, m.Work, true); continue; }
                RECT area = new RECT(m.Work.L + gapsOut, m.Work.T + gapsOut, m.Work.R - gapsOut, m.Work.B - gapsOut);
                for (int i = 0; i < t.Count; i++)
                {
                    Win w = t[i];
                    w.Node = area;
                    if (i == t.Count - 1) { Place(w, area, false); break; }
                    bool vert = area.W >= area.H;
                    if (w.Flip) vert = !vert;
                    w.SplitVert = vert;
                    RECT a, b;
                    if (vert)
                    {
                        int s = area.L + (int)(area.W * w.Ratio);
                        a = new RECT(area.L, area.T, s - gapsIn, area.B);
                        b = new RECT(s + gapsIn, area.T, area.R, area.B);
                    }
                    else
                    {
                        int s = area.T + (int)(area.H * w.Ratio);
                        a = new RECT(area.L, area.T, area.R, s - gapsIn);
                        b = new RECT(area.L, s + gapsIn, area.R, area.B);
                    }
                    Place(w, a, false);
                    area = b;
                }
            }
        }

        static void Place(Win w, RECT r, bool raise)
        {
            if (w == nativeDrag || (w == dragWin && dragKind == 1)) return;
            if (!w.Floating && IsAppFullscreen(w.H)) return;
            if (!w.HasTarget || w.Target.L != r.L || w.Target.T != r.T || w.Target.R != r.R || w.Target.B != r.B) w.Retries = 0;
            w.Target = r; w.HasTarget = !w.Floating;
            bool zoomed = N.IsZoomed(w.H);
            RECT wr, fr;
            N.GetWindowRect(w.H, out wr);
            if (!ExtRect(w.H, out fr)) fr = wr;
            bool fx = w.PopIn || w.SlideDx != 0;
            Anim running;
            bool animating = anims.TryGetValue(w.H, out running);
            if (!raise && !zoomed && !fx && fr.L == r.L && fr.T == r.T && fr.R == r.R && fr.B == r.B)
            {
                if (animating && (running.To.L != r.L || running.To.T != r.T || running.To.R != r.R || running.To.B != r.B)) anims.Remove(w.H);
                return;
            }
            if (animating && running.To.L == r.L && running.To.T == r.T && running.To.R == r.R && running.To.B == r.B && !fx && !raise) return;
            int dl = fr.L - wr.L, dt = fr.T - wr.T, dr = wr.R - fr.R, db = wr.B - fr.B;
            if (dl < 0 || dl > 30 || dt < 0 || dt > 30 || dr < 0 || dr > 30 || db < 0 || db > 30) { dl = dt = dr = db = 0; }
            if (zoomed) N.ShowWindowAsync(w.H, SW_SHOWNOACTIVATE);
            if (raise) N.SetWindowPos(w.H, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER | SWP_ASYNC);

            RECT from = animating ? running.Now : fr;
            if (w.PopIn)
            {
                int pw = (int)(r.W * animPopin), ph = (int)(r.H * animPopin);
                from = new RECT(r.CX - pw / 2, r.CY - ph / 2, r.CX - pw / 2 + pw, r.CY - ph / 2 + ph);
            }
            if (w.SlideDx != 0) from = new RECT(r.L + w.SlideDx, r.T, r.R + w.SlideDx, r.B);
            w.PopIn = false; w.SlideDx = 0;

            if (!animOn || dragWin != null || zoomed || from.W <= 0 || from.H <= 0)
            {
                anims.Remove(w.H);
                MoveTo(w.H, r, dl, dt, dr, db);
                return;
            }
            Anim a = new Anim();
            a.W = w; a.From = from; a.To = r; a.Now = from; a.Start = Environment.TickCount;
            a.Dl = dl; a.Dt = dt; a.Dr = dr; a.Db = db;
            anims[w.H] = a;
            MoveTo(w.H, from, dl, dt, dr, db);
            if (!animTimer.Enabled) animTimer.Start();
        }

        static void MoveTo(IntPtr h, RECT r, int dl, int dt, int dr, int db)
        {
            N.SetWindowPos(h, IntPtr.Zero, r.L - dl, r.T - dt, r.W + dl + dr, r.H + dt + db,
                SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOOWNERZORDER | SWP_ASYNC);
        }

        static int Lerp(int a, int b, double e) { return a + (int)Math.Round((b - a) * e); }

        // Interpola posição e tamanho com ease-out cúbico.
        static void AnimTick()
        {
            int now = Environment.TickCount;
            List<IntPtr> done = new List<IntPtr>();
            foreach (KeyValuePair<IntPtr, Anim> kv in anims)
            {
                Anim a = kv.Value;
                if (!N.IsWindow(kv.Key)) { done.Add(kv.Key); continue; }
                double t = Math.Min(1.0, (now - a.Start) / (double)animMs);
                double e = 1 - Math.Pow(1 - t, 3);
                a.Now = new RECT(Lerp(a.From.L, a.To.L, e), Lerp(a.From.T, a.To.T, e), Lerp(a.From.R, a.To.R, e), Lerp(a.From.B, a.To.B, e));
                MoveTo(kv.Key, a.Now, a.Dl, a.Dt, a.Dr, a.Db);
                if (t >= 1.0) done.Add(kv.Key);
            }
            foreach (IntPtr h in done) anims.Remove(h);
            if (anims.Count == 0) animTimer.Stop();
        }

        // Reaplica o layout se um app mudou o próprio tamanho (no máximo 3 tentativas por alvo).
        static void Enforce()
        {
            if (dragWin != null || nativeDrag != null) return;
            foreach (Win w in wins)
            {
                if (!IsActive(w.Ws) || w.Floating || !w.HasTarget || w.Retries >= 3 || N.IsIconic(w.H) || anims.ContainsKey(w.H)) continue;
                RECT r;
                if (!ExtRect(w.H, out r)) continue;
                if (Math.Abs(r.L - w.Target.L) > 2 || Math.Abs(r.T - w.Target.T) > 2 || Math.Abs(r.R - w.Target.R) > 2 || Math.Abs(r.B - w.Target.B) > 2)
                {
                    if (IsAppFullscreen(w.H)) continue;
                    w.Retries++;
                    Place(w, w.Target, false);
                }
            }
        }

        static void ApplyBorders()
        {
            if (!activeOn && !inactiveOn) return;
            IntPtr fg = N.GetForegroundWindow();
            foreach (Win w in wins)
            {
                if (!IsActive(w.Ws)) continue;
                bool act = w.H == fg;
                uint c = act ? (activeOn ? colActive : 0xFFFFFFFF) : (inactiveOn ? colInactive : 0xFFFFFFFF);
                if (c == w.Border) continue;
                w.Border = c;
                N.DwmSetWindowAttribute(w.H, 34, ref c, 4);
            }
        }

        // ---------------------------------------------------------------- ações

        static void Spawn(string cmd)
        {
            cmd = cmd.Trim();
            if (cmd.Length == 0) return;
            string file, args = "";
            if (cmd.StartsWith("\""))
            {
                int e = cmd.IndexOf('"', 1);
                file = cmd.Substring(1, e - 1); args = cmd.Substring(e + 1).Trim();
            }
            else
            {
                int s = cmd.IndexOf(' ');
                file = s < 0 ? cmd : cmd.Substring(0, s); args = s < 0 ? "" : cmd.Substring(s + 1).Trim();
            }
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(file, args);
                psi.UseShellExecute = true;
                psi.WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                Process.Start(psi);
            }
            catch (Exception e) { Log("exec '" + cmd + "': " + e.Message); }
        }

        static void Kill()
        {
            IntPtr fg = N.GetForegroundWindow();
            if (fg == IntPtr.Zero || N.GetAncestor(fg, 2) != fg) return;
            uint pid; N.GetWindowThreadProcessId(fg, out pid);
            if (pid == myPid || ShellClasses.Contains(ClassOf(fg))) return;
            N.PostMessage(fg, 0x0010, IntPtr.Zero, IntPtr.Zero);
        }

        static void Focus(Win w, bool warp)
        {
            IntPtr fg = N.GetForegroundWindow();
            uint pid;
            uint fgT = N.GetWindowThreadProcessId(fg, out pid), me = N.GetCurrentThreadId();
            bool att = fgT != 0 && fgT != me && N.AttachThreadInput(me, fgT, true);
            if (N.IsIconic(w.H)) N.ShowWindowAsync(w.H, SW_RESTORE);
            N.SetForegroundWindow(w.H);
            N.BringWindowToTop(w.H);
            if (att) N.AttachThreadInput(me, fgT, false);
            if (warp) Warp(w);
        }

        static void Warp(Win w)
        {
            if (!followMouse) return;
            RECT r;
            if (w.HasTarget && !w.Floating) r = w.Target; else if (!ExtRect(w.H, out r)) return;
            POINT p; N.GetCursorPos(out p);
            if (!r.Contains(p.X, p.Y)) N.SetCursorPos(r.CX, r.CY);
        }

        // Foca um monitor vazio: tira o foco das janelas e leva o cursor ao centro dele.
        static void FocusEmptyMonitor(string mon)
        {
            IntPtr d = N.GetShellWindow();
            if (d != IntPtr.Zero) N.SetForegroundWindow(d);
            Mon m = MonByName(Monitors(), mon);
            if (m == null) return;
            POINT p; N.GetCursorPos(out p);
            if (!m.Bounds.Contains(p.X, p.Y)) N.SetCursorPos(m.Work.CX, m.Work.CY);
        }

        static RECT WorkOf(int ws)
        {
            string name;
            Mon m = wsMon.TryGetValue(ws, out name) ? MonByName(Monitors(), name) : null;
            return m != null ? m.Work : new RECT(0, 0, 1280, 720);
        }

        static void ToggleFloat()
        {
            Win w = Focused();
            if (w == null) return;
            w.Fullscreen = false;
            if (!w.Floating)
            {
                w.Floating = true; w.HasTarget = false;
                RECT r;
                if (w.HasFloatRect) r = w.FloatRect;
                else
                {
                    RECT wk = WorkOf(w.Ws);
                    int ww = wk.W * 6 / 10, hh = wk.H * 6 / 10;
                    r = new RECT(wk.CX - ww / 2, wk.CY - hh / 2, wk.CX - ww / 2 + ww, wk.CY - hh / 2 + hh);
                }
                Place(w, r, true);
            }
            else
            {
                RECT r;
                if (ExtRect(w.H, out r)) { w.FloatRect = r; w.HasFloatRect = true; }
                w.Floating = false;
                w.Ws = ActiveOn(MonOfWindow(w.H), w.Ws);
            }
            Layout();
        }

        static void ToggleFull()
        {
            Win w = Focused();
            if (w == null) return;
            if (w.Floating) { w.Floating = false; w.Ws = ActiveOn(MonOfWindow(w.H), w.Ws); }
            bool on = !w.Fullscreen;
            foreach (Win o in wins) if (o.Ws == w.Ws) o.Fullscreen = false;
            w.Fullscreen = on;
            w.HasTarget = false;
            Layout();
        }

        static void ToggleSplit()
        {
            Win w = Focused();
            if (w == null || w.Floating) return;
            List<Win> t = TiledOn(w.Ws);
            int i = t.IndexOf(w);
            if (i < 0) return;
            if (i < t.Count - 1) t[i].Flip = !t[i].Flip; else if (i > 0) t[i - 1].Flip = !t[i - 1].Flip;
            Layout();
        }

        static char Dir1(string arg)
        {
            string a = arg.Trim().ToLowerInvariant();
            return a.Length == 0 ? ' ' : a[0];
        }

        // Escolhe a janela mais próxima na direção, preferindo as que se sobrepõem no eixo ortogonal.
        static Win Neighbor(Win from, char d, bool tiledOnly)
        {
            RECT fr;
            if (from != null) { if (!ExtRect(from.H, out fr)) return null; }
            else { POINT p; N.GetCursorPos(out p); fr = new RECT(p.X, p.Y, p.X + 1, p.Y + 1); }
            Win best = null; long bestScore = long.MaxValue;
            foreach (Win o in wins)
            {
                if (o == from || !IsActive(o.Ws) || N.IsIconic(o.H) || (tiledOnly && o.Floating)) continue;
                RECT r;
                if (!ExtRect(o.H, out r)) continue;
                int prim, ortho; bool overlap;
                if (d == 'l' || d == 'r')
                {
                    prim = d == 'l' ? fr.CX - r.CX : r.CX - fr.CX;
                    ortho = Math.Abs(r.CY - fr.CY);
                    overlap = r.T < fr.B && r.B > fr.T;
                }
                else if (d == 'u' || d == 'd')
                {
                    prim = d == 'u' ? fr.CY - r.CY : r.CY - fr.CY;
                    ortho = Math.Abs(r.CX - fr.CX);
                    overlap = r.L < fr.R && r.R > fr.L;
                }
                else return null;
                if (prim <= 0) continue;
                long score = prim + ortho + (overlap ? 0 : 1000000L);
                if (score < bestScore) { bestScore = score; best = o; }
            }
            return best;
        }

        static Mon MonitorInDir(List<Mon> mons, Mon from, char d)
        {
            Mon best = null; int bestDist = int.MaxValue;
            foreach (Mon m in mons)
            {
                if (m.Name == from.Name) continue;
                int dx = m.Bounds.CX - from.Bounds.CX, dy = m.Bounds.CY - from.Bounds.CY;
                int prim = d == 'l' ? -dx : d == 'r' ? dx : d == 'u' ? -dy : dy;
                if (prim > 0 && prim < bestDist) { bestDist = prim; best = m; }
            }
            return best;
        }

        static void MoveFocus(string arg)
        {
            char d = Dir1(arg);
            Win n = Neighbor(Focused(), d, false);
            if (n != null) { Focus(n, true); return; }
            // Sem janela na direção: foca o monitor vizinho, mesmo vazio.
            FocusMonitorDir(arg);
        }

        static void FocusMonitorDir(string arg)
        {
            List<Mon> mons = Monitors();
            Mon me = MonByName(mons, FocusMon());
            if (me == null) return;
            Mon m = MonitorInDir(mons, me, Dir1(arg));
            if (m == null) return;
            int ws;
            if (active.TryGetValue(m.Name, out ws)) FocusWs(ws);
            UpdateTray();
        }

        static void SwapWins(Win a, Win b)
        {
            int ia = wins.IndexOf(a), ib = wins.IndexOf(b);
            wins[ia] = b; wins[ib] = a;
            int ws = a.Ws; a.Ws = b.Ws; b.Ws = ws;
            double r = a.Ratio; a.Ratio = b.Ratio; b.Ratio = r;
            bool f = a.Flip; a.Flip = b.Flip; b.Flip = f;
        }

        static void MoveWindowDir(string arg)
        {
            Win w = Focused();
            if (w == null || w.Floating) return;
            char d = Dir1(arg);
            Win n = Neighbor(w, d, true);
            if (n != null) SwapWins(w, n);
            else
            {
                List<Mon> mons = Monitors();
                string myMon;
                Mon me = wsMon.TryGetValue(w.Ws, out myMon) ? MonByName(mons, myMon) : null;
                if (me == null) return;
                Mon best = MonitorInDir(mons, me, d);
                int ws;
                if (best == null || !active.TryGetValue(best.Name, out ws)) return;
                w.Ws = ws; w.Fullscreen = false;
                wins.Remove(w); wins.Add(w);
            }
            Layout();
            Warp(w);
            UpdateTray();
        }

        static int ResolveWs(string arg)
        {
            string a = arg.Trim().ToLowerInvariant();
            int cur = Cur();
            if (a.StartsWith("e+") || a.StartsWith("e-"))
            {
                // Percorre os workspaces ocupados do monitor em foco.
                string mon = FocusMon();
                List<int> occ = new List<int>();
                occ.Add(cur);
                foreach (Win w in wins)
                {
                    string m;
                    if (!occ.Contains(w.Ws) && wsMon.TryGetValue(w.Ws, out m) && m == mon) occ.Add(w.Ws);
                }
                occ.Sort();
                int i = occ.IndexOf(cur), step = int.Parse(a.Substring(1));
                return occ[((i + step) % occ.Count + occ.Count) % occ.Count];
            }
            int n;
            if (a.StartsWith("+") || a.StartsWith("-")) n = cur + int.Parse(a);
            else n = int.Parse(a);
            return Math.Max(1, Math.Min(wsCount, n));
        }

        // Monitor onde o workspace n deve aparecer: regra fixa > monitor atual do workspace > monitor em foco.
        static string TargetMon(int n, List<Mon> mons)
        {
            string m;
            if (wsRule.TryGetValue(n, out m) && MonByName(mons, m) != null) return m;
            if (wsMon.TryGetValue(n, out m) && MonByName(mons, m) != null) return m;
            return FocusMon();
        }

        static void SwitchTo(int n)
        {
            if (n < 1) return;
            Win fw = Focused();
            if (fw != null && IsActive(fw.Ws)) lastFocus[fw.Ws] = fw.H;
            if (IsActive(n)) { FocusWs(n); UpdateTray(); ShowOsd(); return; }

            List<Mon> mons = Monitors();
            string target = TargetMon(n, mons);
            int old = ActiveOn(target, 0);
            active[target] = n;
            wsMon[n] = target;
            // Janelas que entram deslizam a partir da direção do workspace (como o "slide" do Hyprland).
            Mon tm = MonByName(mons, target);
            int dx = animOn && tm != null ? (int)(tm.Work.W * animSlide) * (n > old ? 1 : -1) : 0;
            DateTime g = DateTime.Now.AddMilliseconds(1500);
            foreach (Win w in wins) if (w.Ws == n) { w.Grace = g; w.Border = 0xFFFFFFFE; if (!w.Floating) w.SlideDx = dx; }
            Layout();   // posiciona o primeiro quadro ainda oculto
            foreach (Win w in wins) if (w.Ws == n) N.ShowWindowAsync(w.H, SW_SHOWNA);
            foreach (Win w in wins) if (w.Ws == old) { N.ShowWindowAsync(w.H, SW_HIDE); w.Grace = g; }
            ForgetIfEmpty(old);
            FocusWs(n);
            SaveState(); UpdateTray(); ShowOsd();
        }

        static void FocusWs(int n)
        {
            IntPtr h;
            Win w = null;
            if (lastFocus.TryGetValue(n, out h)) { w = Find(h); if (w != null && (w.Ws != n || N.IsIconic(w.H))) w = null; }
            if (w == null)
                foreach (Win o in wins)
                    if (o.Ws == n && !N.IsIconic(o.H)) { w = o; break; }
            if (w != null) Focus(w, true);
            else { string m; if (wsMon.TryGetValue(n, out m)) FocusEmptyMonitor(m); }
            ApplyBorders();
        }

        static void MoveToWs(int n, bool follow)
        {
            Win w = Focused();
            if (w == null || n == w.Ws) return;
            int from = w.Ws;
            w.Ws = n; w.Fullscreen = false;
            wins.Remove(w); wins.Add(w);
            if (!wsMon.ContainsKey(n))
            {
                string r, fm;
                wsMon[n] = wsRule.TryGetValue(n, out r) && MonByName(Monitors(), r) != null ? r
                         : wsMon.TryGetValue(from, out fm) ? fm : FocusMon();
            }
            if (!IsActive(n)) { N.ShowWindowAsync(w.H, SW_HIDE); w.Grace = DateTime.Now.AddMilliseconds(1500); }
            if (follow) { lastFocus[n] = w.H; SwitchTo(n); }
            else { Layout(); FocusWs(from); SaveState(); UpdateTray(); }
        }

        // ---------------------------------------------------------------- arrastar

        static void DragStart(string action)
        {
            POINT p; N.GetCursorPos(out p);
            IntPtr h = N.GetAncestor(N.WindowFromPoint(p), 2);
            Win w = Find(h);
            if (w == null || !IsActive(w.Ws)) { dragWin = null; return; }
            dragKind = action == "movewindow" ? 1 : 2;
            dragStart = p;
            N.GetWindowRect(h, out dragStartRect);
            dragNode = -1;
            N.SetWindowPos(h, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_ASYNC);
            if (dragKind == 2 && !w.Floating)
            {
                List<Win> t = TiledOn(w.Ws);
                int i = t.IndexOf(w);
                int best = int.MaxValue;
                if (i >= 0 && i < t.Count - 1)
                {
                    int line = t[i].SplitVert ? w.Target.R : w.Target.B;
                    int dist = Math.Abs((t[i].SplitVert ? p.X : p.Y) - line);
                    if (dist < best) { best = dist; dragNode = i; }
                }
                if (i > 0)
                {
                    int line = t[i - 1].SplitVert ? w.Target.L : w.Target.T;
                    int dist = Math.Abs((t[i - 1].SplitVert ? p.X : p.Y) - line);
                    if (dist < best) { best = dist; dragNode = i - 1; }
                }
            }
            dragWin = w;
        }

        static void SetSplit(Win node, int pos)
        {
            double r = node.SplitVert ? (pos - node.Node.L) / (double)Math.Max(1, node.Node.W) : (pos - node.Node.T) / (double)Math.Max(1, node.Node.H);
            node.Ratio = Math.Max(0.1, Math.Min(0.9, r));
        }

        static void DragMove()
        {
            Win w = dragWin;
            if (w == null) return;
            POINT p; N.GetCursorPos(out p);
            int dx = p.X - dragStart.X, dy = p.Y - dragStart.Y;
            RECT s = dragStartRect;
            if (dragKind == 1)
                N.SetWindowPos(w.H, IntPtr.Zero, s.L + dx, s.T + dy, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_ASYNC);
            else if (w.Floating)
                N.SetWindowPos(w.H, IntPtr.Zero, 0, 0, Math.Max(150, s.W + dx), Math.Max(100, s.H + dy), SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_ASYNC);
            else if (dragNode >= 0)
            {
                List<Win> t = TiledOn(w.Ws);
                if (dragNode < t.Count) { SetSplit(t[dragNode], t[dragNode].SplitVert ? p.X : p.Y); Layout(); }
            }
        }

        static void DragEnd()
        {
            Win w = dragWin;
            dragWin = null;
            if (w == null) return;
            POINT p; N.GetCursorPos(out p);
            if (w.Floating) FloatMoved(w);
            else if (dragKind == 1) DropTiled(w, p);
            Layout();
            UpdateTray();
        }

        // Janela flutuante solta em outro monitor passa para o workspace visível dele.
        static void FloatMoved(Win w)
        {
            RECT r;
            if (ExtRect(w.H, out r)) { w.FloatRect = r; w.HasFloatRect = true; }
            w.Ws = ActiveOn(MonOfWindow(w.H), w.Ws);
        }

        static void DropTiled(Win w, POINT p)
        {
            Win target = null;
            foreach (Win o in wins)
                if (o != w && IsActive(o.Ws) && !o.Floating && o.HasTarget && !N.IsIconic(o.H) && o.Target.Contains(p.X, p.Y)) target = o;
            if (target != null) SwapWins(w, target);
            else
            {
                int ws = ActiveOn(MonAt(p), w.Ws);
                if (ws != w.Ws) { w.Ws = ws; wins.Remove(w); wins.Add(w); }
            }
            w.HasTarget = false;
        }

        static void MoveSizeStart(IntPtr h)
        {
            Win w = Find(h);
            if (w == null || !IsActive(w.Ws)) return;
            nativeDrag = w;
            ExtRect(h, out nativeStartRect);
        }

        static void MoveSizeEnd(IntPtr h)
        {
            Win w = nativeDrag;
            nativeDrag = null;
            if (w == null || w.H != h) return;
            if (w.Floating) { FloatMoved(w); Layout(); UpdateTray(); return; }
            RECT now, s = nativeStartRect;
            ExtRect(h, out now);
            bool resized = Math.Abs(now.W - s.W) > 4 || Math.Abs(now.H - s.H) > 4;
            if (!resized) { POINT p; N.GetCursorPos(out p); DropTiled(w, p); }
            else
            {
                List<Win> t = TiledOn(w.Ws);
                int i = t.IndexOf(w);
                if (i >= 0 && i < t.Count - 1)
                {
                    if (t[i].SplitVert && now.R != s.R) SetSplit(t[i], now.R + gapsIn);
                    else if (!t[i].SplitVert && now.B != s.B) SetSplit(t[i], now.B + gapsIn);
                }
                if (i > 0)
                {
                    if (t[i - 1].SplitVert && now.L != s.L) SetSplit(t[i - 1], now.L - gapsIn);
                    else if (!t[i - 1].SplitVert && now.T != s.T) SetSplit(t[i - 1], now.T - gapsIn);
                }
                w.HasTarget = false;
            }
            Layout();
            UpdateTray();
        }

        // ---------------------------------------------------------------- estado, tray, OSD

        static void SaveState()
        {
            try
            {
                List<string> l = new List<string>();
                foreach (Win w in wins) if (!IsActive(w.Ws)) l.Add(w.H.ToInt64().ToString());
                if (l.Count == 0) { if (File.Exists(StatePath)) File.Delete(StatePath); }
                else File.WriteAllLines(StatePath, l.ToArray());
            }
            catch (Exception e) { Log("state: " + e.Message); }
        }

        static void RecoverState()
        {
            if (!File.Exists(StatePath)) return;
            int n = 0;
            foreach (string s in File.ReadAllLines(StatePath))
            {
                long v;
                if (long.TryParse(s, out v) && N.IsWindow((IntPtr)v)) { N.ShowWindowAsync((IntPtr)v, SW_SHOWNA); n++; }
            }
            File.Delete(StatePath);
            Log("recuperadas " + n + " janelas ocultas da sessão anterior");
        }

        static void SetupTray()
        {
            tray = new NotifyIcon();
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("Reorganizar janelas", null, delegate { Guard(delegate { foreach (Win w in wins) w.HasTarget = false; Layout(); }); });
            menu.Items.Add("Abrir config", null, delegate { Spawn("notepad.exe \"" + ConfPath + "\""); });
            menu.Items.Add("Recarregar config", null, delegate { Guard(Reload); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Sair (mostra todas as janelas)", null, delegate { Quit(); });
            tray.ContextMenuStrip = menu;
            tray.Visible = true;
        }

        static void UpdateTray()
        {
            if (tray == null) return;
            int cur = Cur();
            trayWs = cur;
            using (Bitmap bmp = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                using (SolidBrush bg = new SolidBrush(Color.FromArgb(51, 204, 255)))
                using (SolidBrush fg = new SolidBrush(Color.FromArgb(30, 30, 46)))
                using (Font f = new Font("Segoe UI", 20, FontStyle.Bold, GraphicsUnit.Pixel))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    g.FillRectangle(bg, 0, 0, 32, 32);
                    g.DrawString((cur % 10).ToString(), f, fg, new RectangleF(0, 1, 32, 32), sf);
                }
                IntPtr hi = bmp.GetHicon();
                tray.Icon = Icon.FromHandle(hi);
                if (trayIcon != IntPtr.Zero) N.DestroyIcon(trayIcon);
                trayIcon = hi;
            }
            StringBuilder sb = new StringBuilder("HyprWin");
            foreach (KeyValuePair<string, int> kv in active) sb.Append("\n" + kv.Key + ": " + kv.Value);
            string text = sb.ToString();
            tray.Text = text.Length > 63 ? text.Substring(0, 63) : text;
        }

        static void ShowOsd()
        {
            if (!showOsd) return;
            osd.Cur = Cur(); osd.Count = wsCount;
            for (int i = 0; i < osd.Occupied.Length; i++) { osd.Occupied[i] = false; osd.OnScreen[i] = false; }
            foreach (Win w in wins) if (w.Ws >= 1 && w.Ws <= 10) osd.Occupied[w.Ws] = true;
            foreach (int ws in active.Values) if (ws >= 1 && ws <= 10) osd.OnScreen[ws] = true;
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) osd.DpiScale = g.DpiX / 96f;
            string name;
            Mon m = wsMon.TryGetValue(osd.Cur, out name) ? MonByName(Monitors(), name) : null;
            RECT wk = m != null ? m.Work : new RECT(0, 0, 1280, 720);
            Size sz = osd.Measure();
            osd.Bounds = new Rectangle(wk.CX - sz.Width / 2, wk.B - sz.Height - (int)(60 * osd.DpiScale), sz.Width, sz.Height);
            osd.Invalidate();
            if (!osd.Visible) osd.Show();
            osdTimer.Stop(); osdTimer.Start();
        }

        // ---------------------------------------------------------------- saída

        static void Quit()
        {
            Cleanup();
            Application.Exit();
        }

        static void Cleanup()
        {
            if (cleaned) return;
            cleaned = true;
            try
            {
                foreach (Win w in wins)
                {
                    if (!N.IsWindow(w.H)) continue;
                    if (!IsActive(w.Ws)) N.ShowWindowAsync(w.H, SW_SHOWNA);
                    if (w.Border != 0xFFFFFFFE) { uint c = 0xFFFFFFFF; N.DwmSetWindowAttribute(w.H, 34, ref c, 4); }
                }
                RestoreSystemSettings();
                foreach (IntPtr h in evHooks) N.UnhookWinEvent(h);
                if (hookThreadId != 0) N.PostThreadMessage(hookThreadId, 0x0012, IntPtr.Zero, IntPtr.Zero);
                if (tray != null) { tray.Visible = false; tray.Dispose(); }
                if (File.Exists(StatePath)) File.Delete(StatePath);
                Log("stop");
            }
            catch (Exception e) { Log("cleanup: " + e); }
        }
    }
}
