using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Drawing.Drawing2D;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Windows.Forms;
using System.Media;
using System.Net;
using System.Drawing.Imaging;
using System.Globalization;
using System.Collections;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Microsoft.Win32;

internal sealed class Settings
{
    public int Minutes = 25;
    public int FontSize = 20;
    public int X = -1;
    public int Y = 60;
    public int HotkeyModifiers = 6;
    public int HotkeyKey = (int)Keys.T;
    public string Color = "#FF0000";
    public string FontFamily = "Segoe UI";
    public string Format = "MM:SS";
    public string EndMessage = "倒计时结束";
    public string Language = "zh";
    public bool AutoStart;
    public bool EndSound = true;
    public string EndSoundStyle = "QQ";
    public bool EndNotice = true;
    public bool FirstRun;
    public string NoticeStyle = "Popup";
    public bool FullscreenDnd = true;
    public bool AgentNotice;

    public static string FilePath
    {
        get
        {
            try
            {
                string localIni = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.ini");
                if (File.Exists(localIni)) return localIni;
            }
            catch { }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "iClock", "settings.ini");
        }
    }

    public static Settings Load()
    {
        Settings s = new Settings();
        try
        {
            if (!File.Exists(FilePath)) { s.FirstRun = true; return s; }
            foreach (string line in File.ReadAllLines(FilePath))
            {
                int p = line.IndexOf('=');
                if (p < 1) continue;
                string k = line.Substring(0, p).Trim();
                string v = line.Substring(p + 1).Trim();
                int n;
                bool b;
                if (k == "Minutes" && int.TryParse(v, out n)) s.Minutes = Math.Max(1, Math.Min(1440, n));
                else if (k == "FontSize" && int.TryParse(v, out n)) s.FontSize = Math.Max(12, Math.Min(120, n));
                else if (k == "X" && int.TryParse(v, out n)) s.X = n;
                else if (k == "Y" && int.TryParse(v, out n)) s.Y = n;
                else if (k == "HotkeyModifiers" && int.TryParse(v, out n)) s.HotkeyModifiers = n & 7;
                else if (k == "HotkeyKey" && int.TryParse(v, out n) && n > 0) s.HotkeyKey = n;
                else if (k == "Color") s.Color = v;
                else if (k == "FontFamily" && !string.IsNullOrEmpty(v)) s.FontFamily = v;
                else if (k == "Format" && (v == "HH:MM:SS" || v == "MM:SS" || v == "Chinese")) s.Format = v;
                else if (k == "EndMessage") s.EndMessage = v;
                else if (k == "Language" && (v == "zh" || v == "en")) s.Language = v;
                else if (k == "AutoStart" && bool.TryParse(v, out b)) s.AutoStart = b;
                else if (k == "EndSound" && bool.TryParse(v, out b)) s.EndSound = b;
                else if (k == "EndSoundStyle" && (v == "QQ" || v == "Pager" || v == "Alarm" || v == "Chime" || v == "Classic")) s.EndSoundStyle = (v == "Chime") ? "QQ" : ((v == "Classic") ? "Pager" : v);
                else if (k == "EndNotice" && bool.TryParse(v, out b)) s.EndNotice = b;
                else if (k == "FirstRun" && bool.TryParse(v, out b)) s.FirstRun = b;
                else if (k == "NoticeStyle" && (v == "Popup" || v == "Pulse")) s.NoticeStyle = v;
                else if (k == "FullscreenDnd" && bool.TryParse(v, out b)) s.FullscreenDnd = b;
                else if (k == "AgentNotice" && bool.TryParse(v, out b)) s.AgentNotice = b;
            }
            if ((s.X == 80 && s.Y == 80) || (s.X == -1 && s.Y == 90)) { s.X = -1; s.Y = 60; }
        }
        catch { }
        return s;
    }

    public void Save()
    {
        string dir = Path.GetDirectoryName(FilePath);
        Directory.CreateDirectory(dir);
        string[] lines = new string[] {
            "Minutes=" + Minutes, "FontSize=" + FontSize, "X=" + X, "Y=" + Y,
            "HotkeyModifiers=" + HotkeyModifiers, "HotkeyKey=" + HotkeyKey,
            "Color=" + Color, "FontFamily=" + FontFamily, "Format=" + Format, "EndMessage=" + EndMessage, "Language=" + Language, "AutoStart=" + AutoStart,
            "EndSound=" + EndSound, "EndSoundStyle=" + EndSoundStyle, "EndNotice=" + EndNotice, "FirstRun=" + FirstRun, "NoticeStyle=" + NoticeStyle, "FullscreenDnd=" + FullscreenDnd, "AgentNotice=" + AgentNotice
        };
        string tmp = Path.Combine(dir, "settings.ini.tmp");
        try
        {
            File.WriteAllLines(tmp, lines);
            if (File.Exists(FilePath))
            {
                File.Replace(tmp, FilePath, null);
            }
            else
            {
                File.Move(tmp, FilePath);
            }
        }
        catch
        {
            try { File.WriteAllLines(FilePath, lines); } catch { }
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
        }
    }
}

[StructLayout(LayoutKind.Sequential)] internal struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }
[StructLayout(LayoutKind.Sequential)] internal struct SIZE { public int cx, cy; public SIZE(int cx, int cy) { this.cx = cx; this.cy = cy; } }
[StructLayout(LayoutKind.Sequential, Pack = 1)] internal struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
[StructLayout(LayoutKind.Sequential)] internal struct RECT { public int Left, Top, Right, Bottom; }

internal delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

internal static class NativeMethods
{
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, uint crKey, [In] ref BLENDFUNCTION pblend, uint dwFlags);

    [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)] public static extern IntPtr CreateCompatibleDC(IntPtr hDC);
    [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)] public static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll", ExactSpelling = true)] public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
    [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)] public static extern bool DeleteObject(IntPtr hObject);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr GetDesktopWindow();
    [DllImport("user32.dll")] public static extern IntPtr GetShellWindow();
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    public struct COPYDATASTRUCT
    {
        public IntPtr dwData;
        public int cbData;
        public IntPtr lpData;
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref RECT lParam);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref COPYDATASTRUCT lParam);

    [DllImport("kernel32.dll", SetLastError = true)] public static extern bool AttachConsole(int dwProcessId);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern bool FreeConsole();
    [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr GetStdHandle(int nStdHandle);

    [StructLayout(LayoutKind.Explicit)]
    public struct INPUT_RECORD
    {
        [FieldOffset(0)] public ushort EventType;
        [FieldOffset(4)] public KEY_EVENT_RECORD KeyEvent;
    }

    [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
    public struct KEY_EVENT_RECORD
    {
        [FieldOffset(0)] public bool bKeyDown;
        [FieldOffset(4)] public ushort wRepeatCount;
        [FieldOffset(6)] public ushort wVirtualKeyCode;
        [FieldOffset(8)] public ushort wVirtualScanCode;
        [FieldOffset(10)] public char UnicodeChar;
        [FieldOffset(12)] public uint dwControlKeyState;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool WriteConsoleInput(IntPtr hConsoleInput, INPUT_RECORD[] lpBuffer, uint nLength, out uint lpNumberOfEventsWritten);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnhookWindowsHookEx(IntPtr hHook);
    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hHook, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr GetModuleHandle(string lpModuleName);
    [DllImport("psapi.dll")]
    public static extern int EmptyWorkingSet(IntPtr hProcess);

    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr hIcon);
    [DllImport("user32.dll")] public static extern bool RegisterShellHookWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool DeregisterShellHookWindow(IntPtr hWnd);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern uint RegisterWindowMessage(string lpString);

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumDesktopWindows(IntPtr hDesktop, EnumWindowsProc lpfn, IntPtr lParam);
    [DllImport("user32.dll")] public static extern IntPtr OpenInputDesktop(uint dwFlags, bool fInherit, uint dwDesiredAccess);
    [DllImport("user32.dll")] public static extern bool CloseDesktop(IntPtr hDesktop);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)] public static extern int GetWindowTextLength(IntPtr hWnd);
}

internal sealed class Overlay : Form
{
    private const int WS_EX_TRANSPARENT = 0x20;
    private const int WS_EX_LAYERED = 0x80000;
    private const int WS_EX_TOOLWINDOW = 0x80;
    private const int WM_NCHITTEST = 0x84;
    private const int WM_SETCURSOR = 0x0020;
    private const int WM_ERASEBKGND = 0x0014;
    private const int HTTRANSPARENT = -1;
    private const int HTCLIENT = 1;
    private const int VK_MENU = 0x12;
    private const int WM_DISPLAYCHANGE = 0x007E;

    private const byte AC_SRC_OVER = 0x00;
    private const byte AC_SRC_ALPHA = 0x01;
    private const uint ULW_ALPHA = 0x02;

    private Bitmap renderBitmap;
    private Graphics renderGraphics;
    private byte[] premulBuffer;

    private Settings settings;
    private string text;
    private bool moving;
    private Point dragStart;
    private Point formStart;
    private Font cachedFont;
    private SolidBrush cachedBrush;
    private SolidBrush cachedShadowBrush;
    private StringFormat cachedFormat;
    private float cachedDigitCenterY;
    private float cachedDigitHeight;
    public bool MoveMode { get; private set; }
    public event Action<Point> PositionChanged;
    public event Action<int> TimeAdjustRequested;

    private double currentOpacity = 1.0;
    public new double Opacity
    {
        get { return currentOpacity; }
        set { currentOpacity = Math.Max(0.0, Math.Min(1.0, value)); }
    }

    private Timer pulseTimer;
    private int pulseStep;
    private Action pulseDismissAction;
    public bool IsPulsing { get; private set; }

    public Overlay(Settings s)
    {
        settings = s;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Fuchsia;
        TopMost = true;
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Opaque, true);
        RebuildGdiResources();
        UpdateLocation();
        UpdateText("25:00");
        MouseDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseUp += OnMouseUp;
    }

    private void UpdateLocation()
    {
        if (settings.X < 0)
        {
            Rectangle screen = Screen.PrimaryScreen.Bounds;
            Location = new Point(screen.Left + Math.Max(0, (screen.Width - Width) / 2), screen.Top + settings.Y);
        }
        else
        {
            Location = new Point(settings.X, settings.Y);
        }
        EnsureVisible();
    }

    public void EnsureVisible()
    {
        if (settings == null) return;
        if (settings.X < 0)
        {
            Rectangle screen = Screen.PrimaryScreen.Bounds;
            int y = Math.Max(screen.Top, Math.Min(screen.Bottom - Height, screen.Top + settings.Y));
            Location = new Point(screen.Left + Math.Max(0, (screen.Width - Width) / 2), y);
            RenderLayeredWindow();
            return;
        }

        Rectangle currentRect = new Rectangle(Location, Size);
        bool isVisible = false;
        foreach (Screen s in Screen.AllScreens)
        {
            Rectangle inter = Rectangle.Intersect(s.Bounds, currentRect);
            if (inter.Width >= 32 && inter.Height >= 20)
            {
                isVisible = true;
                break;
            }
        }

        if (!isVisible)
        {
            Rectangle screen = Screen.PrimaryScreen.Bounds;
            int safeX = screen.Left + Math.Max(0, (screen.Width - Width) / 2);
            int safeY = screen.Top + 60;
            Location = new Point(safeX, safeY);
            settings.X = safeX;
            settings.Y = safeY;
            settings.Save();
        }
        else
        {
            Screen host = Screen.FromRectangle(currentRect);
            Rectangle b = host.Bounds;
            int x = Math.Max(b.Left, Math.Min(b.Right - Width, Location.X));
            int y = Math.Max(b.Top, Math.Min(b.Bottom - Height, Location.Y));
            if (x != Location.X || y != Location.Y)
            {
                Location = new Point(x, y);
                settings.X = x;
                settings.Y = y;
                settings.Save();
            }
        }
        RenderLayeredWindow();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= WS_EX_LAYERED | WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_ERASEBKGND) { m.Result = (IntPtr)1; return; }
        if (m.Msg == WM_DISPLAYCHANGE) { EnsureVisible(); m.Result = IntPtr.Zero; return; }
        if (m.Msg == WM_NCHITTEST)
        {
            if (MoveMode) { m.Result = (IntPtr)HTCLIENT; return; }
            bool alt = (NativeMethods.GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
            if (alt) { m.Result = (IntPtr)HTCLIENT; return; }
            m.Result = (IntPtr)HTTRANSPARENT;
            return;
        }
        if (m.Msg == WM_SETCURSOR)
        {
            bool alt = (NativeMethods.GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
            if (alt || MoveMode)
            {
                Cursor.Current = Cursors.SizeAll;
                m.Result = (IntPtr)1;
                return;
            }
        }
        base.WndProc(ref m);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        bool alt = (NativeMethods.GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
        if (alt || MoveMode)
        {
            if (TimeAdjustRequested != null)
            {
                int step = (e.Delta > 0 ? 1 : -1) * ((ModifierKeys & Keys.Shift) != 0 ? 5 : 1);
                TimeAdjustRequested(step);
            }
        }
    }

    public void SetSettings(Settings s)
    {
        settings = s;
        RebuildGdiResources();
        UpdateLocation();
        RenderLayeredWindow();
    }

    public void SetVisualPreview(Settings s, string previewText)
    {
        settings = s;
        if (previewText != null) text = previewText;
        RebuildGdiResources();
        UpdateLocation();
        RenderLayeredWindow();
    }

    private void RebuildGdiResources()
    {
        if (renderGraphics != null) { renderGraphics.Dispose(); renderGraphics = null; }
        if (renderBitmap != null) { renderBitmap.Dispose(); renderBitmap = null; }
        if (cachedFont != null) cachedFont.Dispose();
        if (cachedBrush != null) cachedBrush.Dispose();
        if (cachedShadowBrush != null) { cachedShadowBrush.Dispose(); cachedShadowBrush = null; }
        if (cachedFormat == null)
        {
            cachedFormat = new StringFormat();
            cachedFormat.Alignment = StringAlignment.Center;
            cachedFormat.LineAlignment = StringAlignment.Center;
        }

        Color c;
        try { c = ColorTranslator.FromHtml(settings.Color); } catch { c = Color.Red; }
        string family = string.IsNullOrEmpty(settings.FontFamily) ? "Segoe UI" : settings.FontFamily;
        try { cachedFont = new Font(family, settings.FontSize, FontStyle.Bold, GraphicsUnit.Pixel); }
        catch { cachedFont = new Font("Segoe UI", settings.FontSize, FontStyle.Bold, GraphicsUnit.Pixel); }
        cachedBrush = new SolidBrush(c);
        float brightness = c.GetBrightness();
        int shadowAlpha = 110;
        Color shadowColor = (brightness < 0.25f)
            ? Color.FromArgb(shadowAlpha, 255, 255, 255)
            : Color.FromArgb(shadowAlpha, 0, 0, 0);
        cachedShadowBrush = new SolidBrush(shadowColor);

        string template = settings.Format == "Chinese" ? (settings.Language == "en" ? "00h 00m 00s" : "00时00分00秒") : (settings.Format == "MM:SS" ? "00:00" : "00:00:00");
        using (Bitmap bmp = new Bitmap(1, 1))
        using (Graphics g = Graphics.FromImage(bmp))
        {
            SizeF size = g.MeasureString(template, cachedFont);
            float extraPauseWidth = (float)Math.Ceiling(cachedFont.Size * 0.75f);
            Size = new Size(Math.Max(100, (int)Math.Ceiling(size.Width + extraPauseWidth) + 16), Math.Max(50, (int)Math.Ceiling(size.Height) + 10));
        }

        using (GraphicsPath path = new GraphicsPath())
        using (StringFormat sf = new StringFormat(StringFormat.GenericTypographic))
        {
            sf.Alignment = StringAlignment.Center;
            sf.LineAlignment = StringAlignment.Center;
            RectangleF fullRect = new RectangleF(0, 0, Width, Height);
            path.AddString(template, cachedFont.FontFamily, (int)cachedFont.Style, cachedFont.Size, fullRect, sf);
            RectangleF b = path.GetBounds();
            cachedDigitCenterY = (b.Top + b.Bottom) / 2.0f;
            cachedDigitHeight = Math.Max(10f, b.Height);
        }
    }

    private static void FillRoundedBar(Graphics g, Brush brush, float x, float y, float w, float h)
    {
        if (h <= w) { g.FillEllipse(brush, x, y, w, h); return; }
        using (GraphicsPath path = new GraphicsPath())
        {
            path.AddArc(x, y, w, w, 180, 180);
            path.AddArc(x, y + h - w, w, w, 0, 180);
            path.CloseFigure();
            g.FillPath(brush, path);
        }
    }

    public void UpdateText(string value)
    {
        if (text == value) return;
        text = value;
        RenderLayeredWindow();
    }

    public bool IsPaused { get; private set; }

    public void SetPaused(bool paused)
    {
        IsPaused = paused;
        if (!MoveMode)
        {
            Opacity = paused ? 0.50 : 1.0;
        }
        RenderLayeredWindow();
    }

    public void SetMoveMode(bool enabled, bool isRunning = true)
    {
        MoveMode = enabled;
        if (enabled)
        {
            BackColor = Color.White; Opacity = 0.50; TopMost = true;
        }
        else
        {
            BackColor = Color.Fuchsia; Opacity = isRunning ? 1.0 : 0.50;
        }
        RenderLayeredWindow();
    }

    public void RenderLayeredWindow()
    {
        if (!IsHandleCreated || IsDisposed || Width <= 0 || Height <= 0) return;

        if (renderBitmap == null || renderBitmap.Width != Width || renderBitmap.Height != Height)
        {
            if (renderGraphics != null) { renderGraphics.Dispose(); renderGraphics = null; }
            if (renderBitmap != null) { renderBitmap.Dispose(); renderBitmap = null; }
            renderBitmap = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
            renderGraphics = Graphics.FromImage(renderBitmap);
            renderGraphics.SmoothingMode = SmoothingMode.AntiAlias;
            renderGraphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        }

        renderGraphics.Clear(Color.FromArgb(0, 0, 0, 0));

        if (MoveMode)
        {
            using (GraphicsPath path = AppContext.GetRoundedRectPath(new Rectangle(2, 2, Width - 5, Height - 5), 8))
            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(190, 255, 255, 255)))
            using (Pen borderPen = new Pen(AppContext.Win11Accent, 2.0f))
            {
                renderGraphics.FillPath(bgBrush, path);
                renderGraphics.DrawPath(borderPen, path);
            }
        }

        if (!string.IsNullOrEmpty(text) && cachedFont != null && cachedBrush != null)
        {
            Rectangle rect = new Rectangle(0, 0, Width, Height);
            if (MoveMode)
            {
                renderGraphics.DrawString(text, cachedFont, cachedBrush, rect, cachedFormat);
            }
            else
            {
                SolidBrush shadowBrush = cachedShadowBrush ?? cachedBrush;

                if (IsPaused)
                {
                    float fontSize = cachedFont.Size;
                    float baseDigitH = cachedDigitHeight > 0 ? cachedDigitHeight : (fontSize * 0.70f);
                    float barHeight = Math.Max(8f, (float)Math.Round(baseDigitH * 0.72f));
                    float barWidth = Math.Max(2.5f, (float)Math.Round(fontSize * 0.11f));
                    float barGap = Math.Max(2f, (float)Math.Round(fontSize * 0.08f));
                    float iconWidth = barWidth * 2f + barGap;
                    float spacing = Math.Max(4f, (float)Math.Round(fontSize * 0.25f));

                    SizeF textSize = renderGraphics.MeasureString(text, cachedFont, PointF.Empty, StringFormat.GenericTypographic);
                    float totalWidth = iconWidth + spacing + textSize.Width;
                    float startX = (Width - totalWidth) / 2f;
                    float centerY = cachedDigitCenterY > 0 ? cachedDigitCenterY : (Height / 2f);
                    float iconY = centerY - barHeight / 2f;
                    float textX = startX + iconWidth + spacing;
                    RectangleF textRect = new RectangleF(textX, 0, textSize.Width, Height);

                    using (StringFormat sf = new StringFormat(StringFormat.GenericTypographic))
                    {
                        sf.Alignment = StringAlignment.Center;
                        sf.LineAlignment = StringAlignment.Center;

                        FillRoundedBar(renderGraphics, shadowBrush, startX + 1.5f, iconY + 2.0f, barWidth, barHeight);
                        FillRoundedBar(renderGraphics, shadowBrush, startX + barWidth + barGap + 1.5f, iconY + 2.0f, barWidth, barHeight);
                        RectangleF shadowTextRect = new RectangleF(textX + 1.5f, 2.0f, textSize.Width, Height);
                        renderGraphics.DrawString(text, cachedFont, shadowBrush, shadowTextRect, sf);

                        FillRoundedBar(renderGraphics, cachedBrush, startX, iconY, barWidth, barHeight);
                        FillRoundedBar(renderGraphics, cachedBrush, startX + barWidth + barGap, iconY, barWidth, barHeight);
                        renderGraphics.DrawString(text, cachedFont, cachedBrush, textRect, sf);
                    }
                }
                else
                {
                    RectangleF shadowRect = new RectangleF(1.5f, 2.0f, Width, Height);
                    renderGraphics.DrawString(text, cachedFont, shadowBrush, shadowRect, cachedFormat);
                    renderGraphics.DrawString(text, cachedFont, cachedBrush, rect, cachedFormat);
                }
            }
        }

        BitmapData data = renderBitmap.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        int byteCount = data.Stride * data.Height;
        if (premulBuffer == null || premulBuffer.Length < byteCount)
        {
            premulBuffer = new byte[byteCount];
        }
        Marshal.Copy(data.Scan0, premulBuffer, 0, byteCount);
        for (int i = 0; i < byteCount; i += 4)
        {
            byte a = premulBuffer[i + 3];
            if (a > 0)
            {
                premulBuffer[i] = (byte)(premulBuffer[i] * a / 255);
                premulBuffer[i + 1] = (byte)(premulBuffer[i + 1] * a / 255);
                premulBuffer[i + 2] = (byte)(premulBuffer[i + 2] * a / 255);
            }
        }
        Marshal.Copy(premulBuffer, 0, data.Scan0, byteCount);
        renderBitmap.UnlockBits(data);

        IntPtr screenDc = IntPtr.Zero;
        IntPtr memDc = NativeMethods.CreateCompatibleDC(screenDc);
        IntPtr hBitmap = renderBitmap.GetHbitmap(Color.FromArgb(0));
        IntPtr oldBitmap = NativeMethods.SelectObject(memDc, hBitmap);

        POINT ptDst = new POINT(Left, Top);
        SIZE size = new SIZE(Width, Height);
        POINT ptSrc = new POINT(0, 0);
        byte alpha = (byte)Math.Max(0, Math.Min(255, (int)(Opacity * 255)));
        BLENDFUNCTION blend = new BLENDFUNCTION { BlendOp = AC_SRC_OVER, BlendFlags = 0, SourceConstantAlpha = alpha, AlphaFormat = AC_SRC_ALPHA };

        NativeMethods.UpdateLayeredWindow(Handle, screenDc, ref ptDst, ref size, memDc, ref ptSrc, 0, ref blend, ULW_ALPHA);

        NativeMethods.SelectObject(memDc, oldBitmap);
        NativeMethods.DeleteObject(hBitmap);
        NativeMethods.DeleteDC(memDc);
    }

    public void StartBreathingPulse(Action onDismiss)
    {
        StopBreathingPulse();
        IsPulsing = true;
        pulseDismissAction = onDismiss;
        pulseStep = 0;
        text = (settings != null && settings.Format == "Chinese")
            ? (settings.Language == "en" ? "00h 00m 00s" : "00时00分00秒")
            : (settings != null && settings.Format == "HH:MM:SS" ? "00:00:00" : "00:00");
        Show();
        BringToFront();
        if (pulseTimer == null)
        {
            pulseTimer = new Timer();
            pulseTimer.Interval = 75;
            pulseTimer.Tick += OnPulseTick;
        }
        pulseTimer.Start();
        RenderLayeredWindow();
    }

    public void StopBreathingPulse()
    {
        if (!IsPulsing) return;
        IsPulsing = false;
        if (pulseTimer != null) pulseTimer.Stop();
        Opacity = 1.0;
        Action act = pulseDismissAction;
        pulseDismissAction = null;
        if (act != null) act();
        RenderLayeredWindow();
    }

    private void OnPulseTick(object sender, EventArgs e)
    {
        if (!IsPulsing) return;
        pulseStep++;
        if (pulseStep > 80)
        {
            StopBreathingPulse();
            return;
        }
        double cycle = (pulseStep % 20) / 20.0 * 2.0 * Math.PI;
        double wave = (Math.Sin(cycle) + 1.0) / 2.0;
        Opacity = 0.35 + 0.65 * wave;
        RenderLayeredWindow();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        RenderLayeredWindow();
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        RenderLayeredWindow();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        RenderLayeredWindow();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible) EnsureVisible();
    }

    protected override void OnPaintBackground(PaintEventArgs e) { }
    protected override void OnPaint(PaintEventArgs e) { }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (renderGraphics != null) { renderGraphics.Dispose(); renderGraphics = null; }
            if (renderBitmap != null) { renderBitmap.Dispose(); renderBitmap = null; }
            if (pulseTimer != null) { pulseTimer.Stop(); pulseTimer.Dispose(); pulseTimer = null; }
            if (cachedFont != null) { cachedFont.Dispose(); cachedFont = null; }
            if (cachedBrush != null) { cachedBrush.Dispose(); cachedBrush = null; }
            if (cachedShadowBrush != null) { cachedShadowBrush.Dispose(); cachedShadowBrush = null; }
            if (cachedFormat != null) { cachedFormat.Dispose(); cachedFormat = null; }
        }
        base.Dispose(disposing);
    }

    private void OnMouseDown(object sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        if (IsPulsing) { StopBreathingPulse(); return; }
        bool alt = (NativeMethods.GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
        if (!MoveMode && !alt) return;
        moving = true; dragStart = Cursor.Position; formStart = Location;
    }
    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (moving)
        {
            Location = new Point(formStart.X + Cursor.Position.X - dragStart.X, formStart.Y + Cursor.Position.Y - dragStart.Y);
        }
    }
    private void OnMouseUp(object sender, MouseEventArgs e)
    {
        if (!moving) return;
        moving = false;
        EnsureVisible();
        if (PositionChanged != null) PositionChanged(Location);
    }
}

internal sealed class SettingsDialog : Form
{
    private NumericUpDown minutes, size;
    private Button colorButton;
    private Panel colorPreview;
    private string selectedColor;
    private TextBox endMessage;
    private ComboBox format, language, fontCombo;
    private CheckBox startup, sound, notice, agentNotice;
    private ComboBox soundStyle, noticeStyle;
    private TextBox hotkeyBox;
    private int hotkeyModifiers, hotkeyKey, candidateMods, candidateKey;
    private bool hotkeyActive;
    private Label lblMinutes, lblSize, lblFont, lblColor, lblFormat, lblEndMsg, lblHotkey, lblLanguage;
    private Button saveBtn, cancelBtn, btnTestAlert;
    public Settings Value { get; private set; }
    public event Action<Settings> PreviewRequested;
    public event Action<Settings, bool> TestAlertRequested;
    private static readonly string[] fontFamilies = { "Segoe UI", "Consolas", "Arial", "Impact", "Microsoft YaHei" };

    private int activeTab = 0;
    private Button btnTabTimer, btnTabAgent;
    private Label lblAgentDesc;
    private Label lblDetectedHeader;
    private Button btnRefreshAgents;
    private Panel panelAgentBox, panelAgentCards;
    private Label lblAgentTip;

    private const int EM_SETMARGINS = 0x00D3;
    private const int EM_SETRECTNP = 0x00B4;
    private const int EC_LEFTMARGIN = 0x0001;
    private const int EC_RIGHTMARGIN = 0x0002;

    private static void UpdateInputPadding(Control ctrl)
    {
        if (ctrl == null) return;
        if (ctrl is TextBox)
        {
            TextBox tb = (TextBox)ctrl;
            if (tb.IsHandleCreated)
            {
                NativeMethods.SendMessage(tb.Handle, EM_SETMARGINS, (IntPtr)(EC_LEFTMARGIN | EC_RIGHTMARGIN), (IntPtr)(4 | (4 << 16)));
                if (tb.Multiline)
                {
                    RECT r = new RECT { Left = 4, Top = 4, Right = Math.Max(0, tb.ClientSize.Width - 4), Bottom = Math.Max(0, tb.ClientSize.Height - 4) };
                    NativeMethods.SendMessage(tb.Handle, EM_SETRECTNP, IntPtr.Zero, ref r);
                }
            }
        }
        else if (ctrl is NumericUpDown)
        {
            foreach (Control child in ctrl.Controls)
            {
                if (child is TextBox) UpdateInputPadding(child);
            }
        }
    }

    private static void ApplyInputPadding(Control ctrl)
    {
        if (ctrl == null) return;
        IntPtr forceHandle = ctrl.Handle;
        if (ctrl is TextBox)
        {
            TextBox tb = (TextBox)ctrl;
            UpdateInputPadding(tb);
            tb.HandleCreated += (s, e) => UpdateInputPadding(tb);
            tb.FontChanged += (s, e) => UpdateInputPadding(tb);
            tb.SizeChanged += (s, e) => UpdateInputPadding(tb);
        }
        else if (ctrl is NumericUpDown)
        {
            NumericUpDown nud = (NumericUpDown)ctrl;
            foreach (Control child in nud.Controls)
            {
                if (child is TextBox) ApplyInputPadding(child);
            }
            nud.ControlAdded += (s, e) => { if (e.Control is TextBox) ApplyInputPadding(e.Control); };
        }
    }

    public SettingsDialog(Settings s) : this(s, true) { }

    public SettingsDialog(Settings s, bool hotkeyActive)
    {
        this.hotkeyActive = hotkeyActive;
        Value = new Settings();
        Value.Minutes = s.Minutes; Value.FontSize = s.FontSize; Value.X = s.X; Value.Y = s.Y;
        Value.HotkeyModifiers = s.HotkeyModifiers; Value.HotkeyKey = s.HotkeyKey;
        Value.Color = s.Color; Value.FontFamily = s.FontFamily; Value.Format = s.Format; Value.EndMessage = s.EndMessage;
        Value.Language = s.Language;
        Value.AutoStart = s.AutoStart; Value.EndSound = s.EndSound; Value.EndSoundStyle = s.EndSoundStyle; Value.EndNotice = s.EndNotice;
        Value.NoticeStyle = s.NoticeStyle; Value.FullscreenDnd = s.FullscreenDnd; Value.AgentNotice = s.AgentNotice;
        bool en = Value.Language == "en";
        Font = AppContext.GetUiFont(Value.Language, 9.5f);
        BackColor = AppContext.Win11Bg;
        AppContext.ApplyModernWindowStyle(this);
        Text = en ? "iClock Settings" : "iClock 设置"; FormBorderStyle = FormBorderStyle.FixedDialog; StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false; ClientSize = new Size(380, 484);

        btnTabTimer = new Button();
        btnTabTimer.Text = en ? "Timer" : "倒计时";
        btnTabTimer.SetBounds(18, 10, 169, 30);
        btnTabTimer.FlatStyle = FlatStyle.Flat;
        btnTabTimer.FlatAppearance.BorderSize = 0;
        btnTabTimer.Cursor = Cursors.Hand;
        btnTabTimer.Click += delegate { SetActiveTab(0); };
        btnTabTimer.MouseEnter += delegate { if (activeTab != 0) btnTabTimer.BackColor = Color.FromArgb(224, 228, 235); };
        btnTabTimer.MouseLeave += delegate { if (activeTab != 0) btnTabTimer.BackColor = Color.FromArgb(235, 238, 242); };
        Controls.Add(btnTabTimer);

        btnTabAgent = new Button();
        btnTabAgent.Text = en ? "AI Agents" : "AI 智能体协作";
        btnTabAgent.SetBounds(193, 10, 169, 30);
        btnTabAgent.FlatStyle = FlatStyle.Flat;
        btnTabAgent.FlatAppearance.BorderSize = 0;
        btnTabAgent.Cursor = Cursors.Hand;
        btnTabAgent.Click += delegate { SetActiveTab(1); };
        btnTabAgent.MouseEnter += delegate { if (activeTab != 1) btnTabAgent.BackColor = Color.FromArgb(224, 228, 235); };
        btnTabAgent.MouseLeave += delegate { if (activeTab != 1) btnTabAgent.BackColor = Color.FromArgb(235, 238, 242); };
        Controls.Add(btnTabAgent);

        KeyPreview = true;
        KeyDown += delegate(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.Tab)
            {
                SetActiveTab(activeTab == 0 ? 1 : 0);
                e.SuppressKeyPress = true;
            }
        };

        lblMinutes = AddLabel(en ? "Duration (minutes)" : "倒计时（分钟）", 18, 52); minutes = AddNumber(Value.Minutes, 1, 1440, 172, 48);
        minutes.ValueChanged += delegate { TriggerPreview(); };
        lblSize = AddLabel(en ? "Text size" : "文字大小", 18, 86); size = AddNumber(Value.FontSize, 12, 120, 172, 82);
        size.ValueChanged += delegate { TriggerPreview(); };
        lblFont = AddLabel(en ? "Font style" : "字体样式", 18, 120);
        fontCombo = new ComboBox(); fontCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        fontCombo.DrawMode = DrawMode.OwnerDrawFixed; fontCombo.ItemHeight = 22;
        fontCombo.SetBounds(172, 116, 188, 24);
        fontCombo.Items.AddRange(en
            ? new object[] { "Segoe UI (Default)", "Consolas (Monospace)", "Arial", "Impact", "Microsoft YaHei" }
            : new object[] { "Segoe UI (默认)", "Consolas (极客等宽)", "Arial", "Impact (醒目粗黑)", "微软雅黑" });
        fontCombo.SelectedIndex = Value.FontFamily == "Consolas" ? 1 :
            (Value.FontFamily == "Arial" ? 2 :
            (Value.FontFamily == "Impact" ? 3 :
            (Value.FontFamily == "Microsoft YaHei" ? 4 : 0)));
        fontCombo.DrawItem += OnDrawFontItem;
        fontCombo.SelectedIndexChanged += delegate { TriggerPreview(); };
        Controls.Add(fontCombo);
        lblColor = AddLabel(en ? "Text color" : "文字颜色", 18, 154); selectedColor = Value.Color;
        colorButton = new Button(); colorButton.Text = en ? "Choose…" : "点击选取…"; colorButton.SetBounds(172, 150, 128, 26);
        AppContext.StyleSecondaryButton(colorButton);
        colorButton.Click += ChooseColor; Controls.Add(colorButton);
        colorPreview = new Panel(); colorPreview.SetBounds(308, 152, 52, 22); colorPreview.BorderStyle = BorderStyle.None;
        colorPreview.Paint += delegate(object sender, PaintEventArgs pe)
        {
            using (Pen p = new Pen(AppContext.Win11Border, 1f))
            {
                pe.Graphics.DrawRectangle(p, 0, 0, colorPreview.Width - 1, colorPreview.Height - 1);
            }
        };
        UpdateColorPreview(); Controls.Add(colorPreview);
        lblFormat = AddLabel(en ? "Display format" : "显示格式", 18, 188);
        format = new ComboBox();
        format.DropDownStyle = ComboBoxStyle.DropDownList;
        format.DrawMode = DrawMode.OwnerDrawFixed;
        format.ItemHeight = 22;
        format.SetBounds(172, 184, 188, 24);
        format.Items.AddRange(en ? new object[] { "HH:MM:SS", "MM:SS", "H:M:S units (00h 25m 00s)" } : new object[] { "HH:MM:SS", "MM:SS", "中文单位 (00时25分00秒)" });
        format.SelectedIndex = Value.Format == "MM:SS" ? 1 : (Value.Format == "Chinese" ? 2 : 0);
        format.DrawItem += OnDrawComboItem;
        format.SelectedIndexChanged += delegate { TriggerPreview(); };
        Controls.Add(format);
        hotkeyModifiers = Value.HotkeyModifiers; hotkeyKey = Value.HotkeyKey;
        candidateMods = hotkeyModifiers; candidateKey = hotkeyKey;
        lblEndMsg = AddLabel(en ? "End message" : "结束时弹出消息", 18, 222);
        endMessage = new TextBox();
        endMessage.SetBounds(172, 218, 188, 36);
        endMessage.Multiline = true;
        endMessage.MaxLength = 200;
        endMessage.Text = Value.EndMessage;
        ApplyInputPadding(endMessage);
        Controls.Add(endMessage);
        lblHotkey = AddLabel(en ? "Start/pause hotkey" : "启动/暂停快捷键", 18, 266);
        hotkeyBox = new TextBox();
        hotkeyBox.ReadOnly = true;
        hotkeyBox.SetBounds(172, 262, 188, 24);
        if (!hotkeyActive)
        {
            hotkeyBox.Text = AppContext.HotkeyText(hotkeyModifiers, hotkeyKey) + (en ? " (Occupied)" : " (已被占用)");
            hotkeyBox.ForeColor = Color.FromArgb(202, 80, 16);
        }
        else if (Value.HotkeyKey == (int)Keys.Space && (Value.HotkeyModifiers & 3) != 0)
        {
            hotkeyBox.Text = AppContext.HotkeyText(hotkeyModifiers, hotkeyKey) + (en ? " (IME Conflict)" : " (与输入法冲突)");
            hotkeyBox.ForeColor = Color.FromArgb(202, 80, 16);
        }
        else
        {
            hotkeyBox.Text = AppContext.HotkeyText(hotkeyModifiers, hotkeyKey);
        }
        hotkeyBox.KeyDown += CaptureHotkey;
        ApplyInputPadding(hotkeyBox);
        Controls.Add(hotkeyBox);
        lblLanguage = AddLabel(en ? "Interface language" : "界面语言", 18, 300);
        language = new ComboBox();
        language.DropDownStyle = ComboBoxStyle.DropDownList;
        language.DrawMode = DrawMode.OwnerDrawFixed;
        language.ItemHeight = 22;
        language.SetBounds(172, 296, 188, 24);
        language.Items.AddRange(new object[] { "简体中文", "English" });
        language.SelectedIndex = Value.Language == "en" ? 1 : 0;
        language.SelectedIndexChanged += OnLanguageChanged;
        language.DrawItem += OnDrawComboItem;
        Controls.Add(language);
        sound = AddCheck(en ? "Play sound at end" : "结束时声音提醒", Value.EndSound, 18, 328);
        sound.SetBounds(18, 328, 150, 24);

        soundStyle = new ComboBox();
        soundStyle.DropDownStyle = ComboBoxStyle.DropDownList;
        soundStyle.DrawMode = DrawMode.OwnerDrawFixed;
        soundStyle.ItemHeight = 22;
        soundStyle.SetBounds(172, 326, 188, 24);
        soundStyle.Items.AddRange(en
            ? new object[] { "QQ Message (Classic)", "Pager Beep (Retro)", "Alarm Ring (Strong)" }
            : new object[] { "QQ 经典提示 (嘀嘀)", "寻呼机提示 (BP机)", "醒目闹铃 (强力)" });
        soundStyle.SelectedIndex = (Value.EndSoundStyle == "Pager" || Value.EndSoundStyle == "Classic") ? 1 : (Value.EndSoundStyle == "Alarm" ? 2 : 0);
        soundStyle.Enabled = sound.Checked;
        soundStyle.DrawItem += OnDrawComboItem;
        sound.CheckedChanged += delegate { soundStyle.Enabled = sound.Checked; };
        soundStyle.SelectionChangeCommitted += delegate
        {
            if (sound.Checked)
            {
                string st = soundStyle.SelectedIndex == 1 ? "Pager" : (soundStyle.SelectedIndex == 2 ? "Alarm" : "QQ");
                AppContext.PlaySoundStyle(st);
            }
        };
        Controls.Add(soundStyle);

        notice = AddCheck(en ? "End notification" : "结束时系统通知", Value.EndNotice, 18, 356);
        notice.SetBounds(18, 356, 150, 24);

        noticeStyle = new ComboBox();
        noticeStyle.DropDownStyle = ComboBoxStyle.DropDownList;
        noticeStyle.DrawMode = DrawMode.OwnerDrawFixed;
        noticeStyle.ItemHeight = 22;
        noticeStyle.SetBounds(172, 354, 188, 24);
        noticeStyle.Items.AddRange(en
            ? new object[] { "Popup (Auto DND Fullscreen)", "In-Place Pulse" }
            : new object[] { "弹窗强提醒 (全屏免打扰)", "原位呼吸轻提醒" });
        noticeStyle.SelectedIndex = Value.NoticeStyle == "Pulse" ? 1 : 0;
        noticeStyle.Enabled = notice.Checked;
        noticeStyle.DrawItem += OnDrawComboItem;
        notice.CheckedChanged += delegate { noticeStyle.Enabled = notice.Checked; };
        Controls.Add(noticeStyle);

        startup = AddCheck(en ? "Start with Windows" : "开机自启动", Value.AutoStart, 18, 388);

        agentNotice = AddCheck(en ? "Enable AI agent task completion alert" : "启用智能体后台任务完成提醒", Value.AgentNotice, 18, 52);
        agentNotice.CheckedChanged += delegate { RefreshAgentCards(); };

        lblAgentDesc = new Label();
        lblAgentDesc.Text = en
            ? "Alerts when background or minimized AI agents finish long tasks; click to switch back."
            : "智能体在后台或最小化完成代码、构建等耗时任务时，\n自动弹出浮窗提醒，点击可直接切回对应工作窗口。";
        lblAgentDesc.ForeColor = AppContext.Win11TextSecondary;
        lblAgentDesc.Font = AppContext.GetUiFont(en ? "en" : "zh", 8.5f);
        lblAgentDesc.SetBounds(18, 80, 344, 38);
        Controls.Add(lblAgentDesc);

        lblDetectedHeader = new Label();
        lblDetectedHeader.Font = new Font(Font.FontFamily, 9.5f, FontStyle.Bold);
        lblDetectedHeader.ForeColor = AppContext.Win11TextPrimary;
        lblDetectedHeader.TextAlign = ContentAlignment.MiddleLeft;
        lblDetectedHeader.SetBounds(18, 124, 260, 26);
        Controls.Add(lblDetectedHeader);

        btnRefreshAgents = new Button();
        btnRefreshAgents.Text = en ? "Re-scan" : "重新检测";
        btnRefreshAgents.SetBounds(282, 124, 80, 26);
        AppContext.StyleSecondaryButton(btnRefreshAgents);
        btnRefreshAgents.Click += delegate { RefreshAgentCards(); };
        Controls.Add(btnRefreshAgents);

        panelAgentBox = new Panel();
        panelAgentBox.SetBounds(18, 154, 344, 218);
        panelAgentBox.BackColor = Color.FromArgb(246, 248, 250);
        panelAgentBox.Paint += delegate(object sender, PaintEventArgs pe)
        {
            using (Pen p = new Pen(AppContext.Win11Border, 1f))
            {
                pe.Graphics.DrawRectangle(p, 0, 0, panelAgentBox.Width - 1, panelAgentBox.Height - 1);
            }
        };
        Controls.Add(panelAgentBox);

        panelAgentCards = new Panel();
        panelAgentCards.SetBounds(1, 1, 342, 216);
        panelAgentCards.AutoScroll = true;
        panelAgentCards.BackColor = Color.FromArgb(246, 248, 250);
        panelAgentCards.BorderStyle = BorderStyle.None;
        panelAgentBox.Controls.Add(panelAgentCards);

        lblAgentTip = new Label();
        lblAgentTip.Text = en
            ? "Tip: Click 'View' on the alert popup to restore and focus the window."
            : "提示：提醒浮窗弹出后，点击「查看」可还原并置顶对应窗口。";
        lblAgentTip.ForeColor = AppContext.Win11TextSecondary;
        lblAgentTip.Font = AppContext.GetUiFont(en ? "en" : "zh", 8.5f);
        lblAgentTip.SetBounds(18, 384, 344, 36);
        Controls.Add(lblAgentTip);

        Label footerLine = new Label(); footerLine.BackColor = AppContext.Win11Border; footerLine.BorderStyle = BorderStyle.None; footerLine.SetBounds(18, 432, 344, 1); Controls.Add(footerLine);
        btnTestAlert = new Button();
        btnTestAlert.Text = en ? "Test Alert" : "测试提醒";
        btnTestAlert.SetBounds(18, 442, en ? 92 : 88, 28);
        AppContext.StyleSecondaryButton(btnTestAlert);
        btnTestAlert.Click += delegate
        {
            if (TestAlertRequested != null)
            {
                Settings draft = GetCurrentDraftSettings();
                TestAlertRequested(draft, activeTab == 1);
            }
        };
        Controls.Add(btnTestAlert);
        saveBtn = new Button(); saveBtn.Text = en ? "Save" : "保存"; saveBtn.SetBounds(196, 442, 76, 28);
        AppContext.StylePrimaryButton(saveBtn);
        if (!hotkeyActive || (Value.HotkeyKey == (int)Keys.Space && (Value.HotkeyModifiers & 3) != 0)) saveBtn.Enabled = false;
        saveBtn.Click += SaveClick; Controls.Add(saveBtn);
        cancelBtn = new Button(); cancelBtn.Text = en ? "Cancel" : "取消"; cancelBtn.SetBounds(284, 442, 76, 28);
        AppContext.StyleSecondaryButton(cancelBtn);
        cancelBtn.DialogResult = DialogResult.Cancel;
        cancelBtn.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
        Controls.Add(cancelBtn);
        AcceptButton = saveBtn; CancelButton = cancelBtn;

        AppContext.CenterFormOnScreen(this);
        SetActiveTab(0);
    }

    private void SetActiveTab(int tabIndex)
    {
        activeTab = tabIndex;
        bool isTimer = (tabIndex == 0);

        lblMinutes.Visible = isTimer;
        minutes.Visible = isTimer;
        lblSize.Visible = isTimer;
        size.Visible = isTimer;
        lblFont.Visible = isTimer;
        fontCombo.Visible = isTimer;
        lblColor.Visible = isTimer;
        colorButton.Visible = isTimer;
        colorPreview.Visible = isTimer;
        lblFormat.Visible = isTimer;
        format.Visible = isTimer;
        lblEndMsg.Visible = isTimer;
        endMessage.Visible = isTimer;
        lblHotkey.Visible = isTimer;
        hotkeyBox.Visible = isTimer;
        lblLanguage.Visible = isTimer;
        language.Visible = isTimer;
        sound.Visible = isTimer;
        soundStyle.Visible = isTimer;
        notice.Visible = isTimer;
        noticeStyle.Visible = isTimer;
        startup.Visible = isTimer;

        agentNotice.Visible = !isTimer;
        lblAgentDesc.Visible = !isTimer;
        lblDetectedHeader.Visible = !isTimer;
        btnRefreshAgents.Visible = !isTimer;
        panelAgentBox.Visible = !isTimer;
        panelAgentCards.Visible = !isTimer;
        lblAgentTip.Visible = !isTimer;

        UpdateTabButtons();
        UpdateTestAlertButtonText();
        if (!isTimer)
        {
            RefreshAgentCards();
        }
    }

    private void UpdateTabButtons()
    {
        bool isTimer = (activeTab == 0);
        btnTabTimer.BackColor = isTimer ? AppContext.Win11Accent : Color.FromArgb(235, 238, 242);
        btnTabTimer.ForeColor = isTimer ? Color.White : AppContext.Win11TextPrimary;
        btnTabTimer.Font = new Font(Font.FontFamily, 9.5f, FontStyle.Bold);

        btnTabAgent.BackColor = isTimer ? Color.FromArgb(235, 238, 242) : AppContext.Win11Accent;
        btnTabAgent.ForeColor = isTimer ? AppContext.Win11TextPrimary : Color.White;
        btnTabAgent.Font = new Font(Font.FontFamily, 9.5f, FontStyle.Bold);
    }

    private void RefreshAgentCards()
    {
        if (panelAgentCards == null) return;
        panelAgentCards.SuspendLayout();
        while (panelAgentCards.Controls.Count > 0)
        {
            Control c = panelAgentCards.Controls[0];
            panelAgentCards.Controls.RemoveAt(0);
            c.Dispose();
        }

        bool en = language.SelectedIndex == 1;
        bool alertEnabled = agentNotice.Checked;
        List<string> agents = AppContext.DetectAgents();
        HashSet<string> running = AppContext.GetRunningAgents();
        agents.Sort((a, b) =>
        {
            bool aRun = running.Contains(a);
            bool bRun = running.Contains(b);
            if (aRun != bRun) return aRun ? -1 : 1;
            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        });

        if (alertEnabled)
        {
            lblDetectedHeader.Text = en
                ? string.Format("Detected Agents ({0})", agents.Count)
                : string.Format("已检测到的智能体 ({0} 款)", agents.Count);
            lblDetectedHeader.ForeColor = AppContext.Win11TextPrimary;
        }
        else
        {
            lblDetectedHeader.Text = en
                ? string.Format("Detected Agents ({0}) · Off", agents.Count)
                : string.Format("已检测到的智能体 ({0} 款 · 未启用)", agents.Count);
            lblDetectedHeader.ForeColor = AppContext.Win11TextSecondary;
        }

        if (agents.Count == 0)
        {
            Label empty = new Label();
            empty.Text = en
                ? "No supported AI agents detected.\n(Supports Antigravity, Cursor, Windsurf, Trae, Claude, Copilot, Tongyi Lingma, Comate, CodeGeeX, MiniMax, Kimi, etc.)"
                : "未检测到支持的智能体。\n（支持 Antigravity、Cursor、Windsurf、Trae、Claude、Copilot、通义灵码、文心快码、CodeGeeX、MiniMax、Kimi 等）";
            empty.ForeColor = AppContext.Win11TextSecondary;
            empty.Font = AppContext.GetUiFont(en ? "en" : "zh", 9.0f);
            empty.TextAlign = ContentAlignment.MiddleCenter;
            empty.Dock = DockStyle.Fill;
            panelAgentCards.Controls.Add(empty);
        }
        else
        {
            int cardY = 4;
            int cardPad = 8;
            bool needVScroll = (agents.Count * 36 + 8) > panelAgentCards.Height;
            int vScrollReserve = needVScroll ? (SystemInformation.VerticalScrollBarWidth + 4) : 0;
            int cardW = panelAgentCards.Width - (cardPad * 2) - vScrollReserve;
            for (int i = 0; i < agents.Count; i++)
            {
                string name = agents[i];
                bool isRunning = running.Contains(name);
                Panel card = CreateAgentCard(name, isRunning, alertEnabled, cardW, en);
                card.Location = new Point(cardPad, cardY);
                panelAgentCards.Controls.Add(card);
                cardY += card.Height + 4;
            }
        }
        panelAgentCards.ResumeLayout(true);
    }

    private Panel CreateAgentCard(string name, bool isRunning, bool alertEnabled, int width, bool en)
    {
        Panel card = new Panel();
        card.Size = new Size(width, 32);
        card.BackColor = Color.White;
        card.Paint += delegate(object sender, PaintEventArgs pe)
        {
            using (Pen p = new Pen(AppContext.Win11Border, 1f))
            {
                pe.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
            }
        };

        Label lblDot = new Label();
        lblDot.Text = "●";
        lblDot.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        lblDot.ForeColor = !alertEnabled
            ? Color.FromArgb(180, 180, 180)
            : (isRunning ? Color.FromArgb(16, 124, 65) : Color.FromArgb(140, 145, 155));
        lblDot.Location = new Point(8, 7);
        lblDot.Size = new Size(16, 18);
        card.Controls.Add(lblDot);

        Label lblName = new Label();
        lblName.Text = name;
        lblName.Font = new Font(Font.FontFamily, 9.5f, FontStyle.Bold);
        lblName.ForeColor = alertEnabled ? AppContext.Win11TextPrimary : Color.FromArgb(130, 130, 130);
        lblName.Location = new Point(26, 6);
        lblName.AutoSize = true;
        card.Controls.Add(lblName);

        Label lblBadge = new Label();
        if (!alertEnabled)
        {
            lblBadge.Text = isRunning ? (en ? "Running" : "运行中") : (en ? "Ready" : "已就绪");
            lblBadge.ForeColor = Color.FromArgb(140, 140, 140);
            lblBadge.BackColor = Color.FromArgb(242, 242, 242);
        }
        else if (isRunning)
        {
            lblBadge.Text = en ? "Running" : "运行中";
            lblBadge.ForeColor = Color.FromArgb(27, 94, 32);
            lblBadge.BackColor = Color.FromArgb(232, 245, 233);
        }
        else
        {
            lblBadge.Text = en ? "Ready" : "已就绪";
            lblBadge.ForeColor = Color.FromArgb(90, 90, 90);
            lblBadge.BackColor = Color.FromArgb(243, 244, 246);
        }
        lblBadge.Font = AppContext.GetUiFont(en ? "en" : "zh", 8.5f);
        lblBadge.TextAlign = ContentAlignment.MiddleCenter;
        lblBadge.Size = new Size(en ? 66 : 56, 20);
        lblBadge.Location = new Point(card.Width - lblBadge.Width - 8, 6);
        card.Controls.Add(lblBadge);

        return card;
    }

    private void OnLanguageChanged(object sender, EventArgs e)
    {
        bool toEnglish = language.SelectedIndex == 1;
        Font = AppContext.GetUiFont(toEnglish ? "en" : "zh", 9.5f);
        Text = toEnglish ? "iClock Settings" : "iClock 设置";
        if (toEnglish && endMessage.Text == "倒计时结束") endMessage.Text = "Countdown finished";
        else if (!toEnglish && endMessage.Text == "Countdown finished") endMessage.Text = "倒计时结束";

        btnTabTimer.Text = toEnglish ? "Timer" : "倒计时";
        btnTabAgent.Text = toEnglish ? "AI Agents" : "AI 智能体协作";

        lblMinutes.Text = toEnglish ? "Duration (minutes)" : "倒计时（分钟）";
        lblSize.Text = toEnglish ? "Text size" : "文字大小";
        lblFont.Text = toEnglish ? "Font style" : "字体样式";
        lblColor.Text = toEnglish ? "Text color" : "文字颜色";
        colorButton.Text = toEnglish ? "Choose…" : "点击选取…";
        lblFormat.Text = toEnglish ? "Display format" : "显示格式";
        lblEndMsg.Text = toEnglish ? "End message" : "结束时弹出消息";
        lblHotkey.Text = toEnglish ? "Start/pause hotkey" : "启动/暂停快捷键";
        lblLanguage.Text = toEnglish ? "Interface language" : "界面语言";
        startup.Text = toEnglish ? "Start with Windows" : "开机自启动";
        sound.Text = toEnglish ? "Play sound at end" : "结束时声音提醒";
        notice.Text = toEnglish ? "End notification" : "结束时系统通知";

        agentNotice.Text = toEnglish ? "Enable AI agent task completion alert" : "启用智能体后台任务完成提醒";
        lblAgentDesc.Text = toEnglish
            ? "Alerts when background or minimized AI agents finish long tasks; click to switch back."
            : "智能体在后台或最小化完成代码、构建等耗时任务时，\n自动弹出浮窗提醒，点击可直接切回对应工作窗口。";
        btnRefreshAgents.Text = toEnglish ? "Re-scan" : "重新检测";
        lblAgentTip.Text = toEnglish
            ? "Tip: Click 'View' on the alert popup to restore and focus the window."
            : "提示：提醒浮窗弹出后，点击「查看」可还原并置顶对应窗口。";

        saveBtn.Text = toEnglish ? "Save" : "保存";
        cancelBtn.Text = toEnglish ? "Cancel" : "取消";

        UpdateTabButtons();
        RefreshAgentCards();

        int sel = format.SelectedIndex;
        format.Items.Clear();
        format.Items.AddRange(toEnglish
            ? new object[] { "HH:MM:SS", "MM:SS", "H:M:S units (00h 25m 00s)" }
            : new object[] { "HH:MM:SS", "MM:SS", "中文单位 (00时25分00秒)" });
        format.SelectedIndex = sel >= 0 ? sel : 0;
        int fontSel = fontCombo.SelectedIndex;
        fontCombo.Items.Clear();
        fontCombo.Items.AddRange(toEnglish
            ? new object[] { "Segoe UI (Default)", "Consolas (Monospace)", "Arial", "Impact", "Microsoft YaHei" }
            : new object[] { "Segoe UI (默认)", "Consolas (极客等宽)", "Arial", "Impact (醒目粗黑)", "微软雅黑" });
        fontCombo.SelectedIndex = fontSel >= 0 ? fontSel : 0;
        int ssSel = soundStyle.SelectedIndex;
        soundStyle.Items.Clear();
        soundStyle.Items.AddRange(toEnglish
            ? new object[] { "QQ Message (Classic)", "Pager Beep (Retro)", "Alarm Ring (Strong)" }
            : new object[] { "QQ 经典提示 (嘀嘀)", "寻呼机提示 (BP机)", "醒目闹铃 (强力)" });
        soundStyle.SelectedIndex = ssSel >= 0 ? ssSel : 0;

        int nsSel = noticeStyle.SelectedIndex;
        noticeStyle.Items.Clear();
        noticeStyle.Items.AddRange(toEnglish
            ? new object[] { "Popup (Auto DND Fullscreen)", "In-Place Pulse" }
            : new object[] { "弹窗强提醒 (全屏免打扰)", "原位呼吸轻提醒" });
        noticeStyle.SelectedIndex = nsSel >= 0 ? nsSel : 0;
        if (saveBtn.Enabled)
        {
            hotkeyBox.Text = AppContext.HotkeyText(hotkeyModifiers, hotkeyKey);
            hotkeyBox.ForeColor = AppContext.Win11TextPrimary;
        }
        else if (candidateMods == 0 && !((candidateKey >= (int)Keys.F1 && candidateKey <= (int)Keys.F12) || candidateKey == (int)Keys.Pause || candidateKey == (int)Keys.Scroll))
        {
            hotkeyBox.Text = toEnglish ? "Use a modifier + key" : "请按修饰键 + 按键";
            hotkeyBox.ForeColor = Color.FromArgb(202, 80, 16);
        }
        else
        {
            hotkeyBox.Text = AppContext.HotkeyText(candidateMods, candidateKey) + (toEnglish ? " (Occupied)" : " (已被占用)");
            hotkeyBox.ForeColor = Color.FromArgb(202, 80, 16);
        }

        UpdateInputPadding(minutes);
        UpdateInputPadding(size);
        UpdateInputPadding(endMessage);
        UpdateInputPadding(hotkeyBox);
        UpdateTestAlertButtonText();
        TriggerPreview();
    }

    public Settings GetCurrentDraftSettings()
    {
        Settings draft = new Settings();
        draft.Minutes = minutes != null ? (int)minutes.Value : Value.Minutes;
        draft.FontSize = size != null ? (int)size.Value : Value.FontSize;
        draft.Color = !string.IsNullOrEmpty(selectedColor) ? selectedColor : Value.Color;
        draft.FontFamily = (fontCombo != null && fontCombo.SelectedIndex >= 0 && fontCombo.SelectedIndex < fontFamilies.Length)
            ? fontFamilies[fontCombo.SelectedIndex]
            : (Value.FontFamily ?? "Segoe UI");
        draft.Format = (format != null && format.SelectedIndex == 1) ? "MM:SS" : ((format != null && format.SelectedIndex == 2) ? "Chinese" : "HH:MM:SS");
        draft.Language = (language != null && language.SelectedIndex == 1) ? "en" : "zh";
        draft.EndSound = sound != null ? sound.Checked : Value.EndSound;
        draft.EndSoundStyle = (soundStyle != null && soundStyle.SelectedIndex == 1) ? "Pager" : ((soundStyle != null && soundStyle.SelectedIndex == 2) ? "Alarm" : "QQ");
        draft.EndNotice = notice != null ? notice.Checked : Value.EndNotice;
        draft.NoticeStyle = (noticeStyle != null && noticeStyle.SelectedIndex == 1) ? "Pulse" : "Popup";
        draft.EndMessage = endMessage != null ? endMessage.Text.Trim() : Value.EndMessage;
        draft.FullscreenDnd = Value.FullscreenDnd;
        draft.AgentNotice = agentNotice != null ? agentNotice.Checked : Value.AgentNotice;
        draft.X = Value.X;
        draft.Y = Value.Y;
        return draft;
    }

    private void UpdateTestAlertButtonText()
    {
        if (btnTestAlert == null) return;
        bool en = (language != null && language.SelectedIndex == 1);
        if (activeTab == 1)
        {
            btnTestAlert.Text = en ? "Test Agent" : "测试智能体";
            btnTestAlert.Width = en ? 96 : 94;
        }
        else
        {
            btnTestAlert.Text = en ? "Test Alert" : "测试提醒";
            btnTestAlert.Width = en ? 92 : 88;
        }
    }

    private void TriggerPreview()
    {
        if (PreviewRequested != null && minutes != null && size != null && fontCombo != null && format != null && language != null)
        {
            PreviewRequested(GetCurrentDraftSettings());
        }
    }

    private Label AddLabel(string t, int x, int y) { Label l = new Label(); l.Text = t; l.ForeColor = AppContext.Win11TextPrimary; l.SetBounds(x, y, 148, 24); l.TextAlign = ContentAlignment.MiddleLeft; Controls.Add(l); return l; }
    private NumericUpDown AddNumber(int v, int min, int max, int x, int y) { NumericUpDown n = new NumericUpDown(); n.Minimum = min; n.Maximum = max; n.Value = Math.Min(max, Math.Max(min, v)); n.SetBounds(x, y, 188, 24); ApplyInputPadding(n); Controls.Add(n); return n; }
    private CheckBox AddCheck(string t, bool v, int x, int y) { CheckBox c = new CheckBox(); c.Text = t; c.Checked = v; c.ForeColor = AppContext.Win11TextPrimary; c.SetBounds(x, y, 344, 24); Controls.Add(c); return c; }
    private void ChooseColor(object sender, EventArgs e)
    {
        Color initial; try { initial = ColorTranslator.FromHtml(selectedColor); } catch { initial = Color.Red; }
        using (ColorDialog d = new ColorDialog())
        {
            d.Color = initial; d.AllowFullOpen = true; d.FullOpen = true; d.AnyColor = true;
            if (d.ShowDialog(this) == DialogResult.OK) { selectedColor = ColorTranslator.ToHtml(d.Color); UpdateColorPreview(); TriggerPreview(); }
        }
    }
    private void UpdateColorPreview()
    {
        try { colorPreview.BackColor = ColorTranslator.FromHtml(selectedColor); } catch { colorPreview.BackColor = Color.Red; }
    }
    private void CaptureHotkey(object sender, KeyEventArgs e)
    {
        Keys key = e.KeyCode;
        if (key == Keys.Escape && (e.Modifiers & (Keys.Control | Keys.Alt | Keys.Shift)) == 0) return;
        e.SuppressKeyPress = true; e.Handled = true;
        if (key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu || key == Keys.LWin || key == Keys.RWin) return;
        int mods = 0;
        if ((e.Modifiers & Keys.Control) != 0) mods |= 2;
        if ((e.Modifiers & Keys.Alt) != 0) mods |= 1;
        if ((e.Modifiers & Keys.Shift) != 0) mods |= 4;
        bool isFunctionKey = (key >= Keys.F1 && key <= Keys.F12) || key == Keys.Pause || key == Keys.Scroll;
        candidateMods = mods; candidateKey = (int)key;
        if (mods == 0 && !isFunctionKey)
        {
            hotkeyBox.Text = language.SelectedIndex == 1 ? "Use a modifier + key" : "请按修饰键 + 按键";
            hotkeyBox.ForeColor = Color.FromArgb(202, 80, 16);
            saveBtn.Enabled = false;
            return;
        }
        string text = AppContext.HotkeyText(mods, (int)key);
        if (key == Keys.Space && (mods & 3) != 0)
        {
            hotkeyBox.Text = text + (language.SelectedIndex == 1 ? " (IME Conflict)" : " (与输入法冲突)");
            hotkeyBox.ForeColor = Color.FromArgb(202, 80, 16);
            saveBtn.Enabled = false;
            return;
        }
        bool isCurrent = hotkeyActive && (mods == Value.HotkeyModifiers && (int)key == Value.HotkeyKey);
        bool available = isCurrent || AppContext.ProbeHotkey(mods, (int)key);
        if (!available)
        {
            hotkeyBox.Text = text + (language.SelectedIndex == 1 ? " (Occupied)" : " (已被占用)");
            hotkeyBox.ForeColor = Color.FromArgb(202, 80, 16);
            saveBtn.Enabled = false;
        }
        else
        {
            hotkeyModifiers = mods; hotkeyKey = (int)key;
            hotkeyBox.Text = text;
            hotkeyBox.ForeColor = AppContext.Win11TextPrimary;
            saveBtn.Enabled = true;
        }
    }
    private void OnDrawFontItem(object sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= fontCombo.Items.Count) return;
        string family = e.Index < fontFamilies.Length ? fontFamilies[e.Index] : "Segoe UI";
        Font f = null;
        try { f = new Font(family, e.Font.Size, FontStyle.Regular, e.Font.Unit); } catch { }
        DrawComboItem(e, fontCombo.Items[e.Index].ToString(), f ?? e.Font);
        if (f != null) f.Dispose();
    }
    private void OnDrawComboItem(object sender, DrawItemEventArgs e)
    {
        ComboBox cb = sender as ComboBox;
        if (cb == null || e.Index < 0 || e.Index >= cb.Items.Count) return;
        DrawComboItem(e, cb.Items[e.Index].ToString(), e.Font);
    }
    private static void DrawComboItem(DrawItemEventArgs e, string text, Font font)
    {
        e.DrawBackground();
        e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        using (SolidBrush b = new SolidBrush(e.ForeColor))
        using (StringFormat sf = new StringFormat { LineAlignment = StringAlignment.Center })
        {
            Rectangle r = new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height);
            e.Graphics.DrawString(text, font, b, r, sf);
        }
        e.DrawFocusRectangle();
    }
    private void SaveClick(object sender, EventArgs e)
    {
        if (startup.Checked && !Value.AutoStart && AppContext.IsInEphemeralFolder(Application.ExecutablePath))
        {
            bool toEnglish = language.SelectedIndex == 1;
            string msg = toEnglish
                ? "iClock is currently located in a temporary or download folder:\r\n" + Application.ExecutablePath + "\r\n\r\nIt is recommended to move iClock.exe to a permanent folder (such as Documents or Tools) before enabling auto-start, to prevent accidental deletion during cleanup.\r\n\r\nDo you still want to enable auto-start?"
                : "检测到当前程序位于临时或下载目录：\r\n" + Application.ExecutablePath + "\r\n\r\n建议将 iClock.exe 移动到固定文件夹（如个人工具目录或文档）后再开启自启，以防清理下载文件时误删。\r\n\r\n是否仍要开启开机自启？";
            string caption = toEnglish ? "iClock Auto-Start Tip" : "iClock 开机自启提示";
            if (MessageBox.Show(this, msg, caption, MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.No)
            {
                startup.Checked = false;
                return;
            }
        }
        Value.Minutes = (int)minutes.Value; Value.FontSize = (int)size.Value; Value.Color = selectedColor;
        Value.FontFamily = fontCombo.SelectedIndex >= 0 && fontCombo.SelectedIndex < fontFamilies.Length
            ? fontFamilies[fontCombo.SelectedIndex]
            : "Segoe UI";
        Value.HotkeyModifiers = hotkeyModifiers; Value.HotkeyKey = hotkeyKey;
        Value.EndMessage = endMessage.Text.Trim();
        Value.Format = format.SelectedIndex == 1 ? "MM:SS" : (format.SelectedIndex == 2 ? "Chinese" : "HH:MM:SS");
        Value.Language = language.SelectedIndex == 1 ? "en" : "zh";
        Value.AutoStart = startup.Checked; Value.EndSound = sound.Checked;
        Value.EndSoundStyle = soundStyle.SelectedIndex == 1 ? "Pager" : (soundStyle.SelectedIndex == 2 ? "Alarm" : "QQ");
        Value.EndNotice = notice.Checked;
        Value.AgentNotice = agentNotice.Checked;
        Value.NoticeStyle = (noticeStyle.SelectedIndex == 1) ? "Pulse" : "Popup";
        DialogResult = DialogResult.OK; Close();
    }
}

internal sealed class NoticeDialog : Form
{
    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private const uint SWP_FLAGS = 0x0043;
    private Settings settings;
    private SoundPlayer player;
    private Timer timer;
    private DateTime finishTime;
    private Label finishLabel;

    private IntPtr targetHwnd;

    public NoticeDialog(Settings s, DateTime finishedAt) : this(s, finishedAt, null, IntPtr.Zero, null) { }

    public NoticeDialog(Settings s, DateTime finishedAt, string agentName, IntPtr targetWindow) : this(s, finishedAt, agentName, targetWindow, null, null) { }

    public NoticeDialog(Settings s, DateTime finishedAt, string agentName, IntPtr targetWindow, Action onStartRest) : this(s, finishedAt, agentName, targetWindow, onStartRest, null) { }

    public NoticeDialog(Settings s, DateTime finishedAt, string agentName, IntPtr targetWindow, Action onStartRest, string detailMessage)
    {
        settings = s;
        finishTime = finishedAt;
        targetHwnd = targetWindow;
        bool isAgent = !string.IsNullOrEmpty(agentName);
        bool en = s.Language == "en";
        Font = AppContext.GetUiFont(s.Language, 9.5f);
        BackColor = AppContext.Win11Bg;
        AppContext.ApplyModernWindowStyle(this);
        Text = isAgent ? (en ? agentName + " Ready" : agentName + " 已就绪") : "iClock";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        TopMost = true;
        ClientSize = new Size(350, 215);

        string titleText = isAgent
            ? (en ? agentName + " Task Completed" : agentName + " 任务已完成")
            : (String.IsNullOrEmpty(s.EndMessage) ? (en ? "Countdown Finished" : "倒计时结束") : s.EndMessage);
        Label titleLabel = new Label();
        titleLabel.Text = titleText;
        titleLabel.Font = AppContext.GetUiFont(s.Language, 13f, FontStyle.Bold);
        titleLabel.ForeColor = AppContext.Win11TextPrimary;
        titleLabel.TextAlign = ContentAlignment.MiddleCenter;
        titleLabel.SetBounds(20, 18, 310, 28);
        Controls.Add(titleLabel);

        string timeText;
        if (isAgent)
        {
            timeText = !string.IsNullOrEmpty(detailMessage) ? detailMessage : (en ? "Ready for review" : "可返回查看结果");
        }
        else
        {
            timeText = s.Format == "MM:SS" ? "00:00" :
                (s.Format == "Chinese" ? (en ? "00h 00m 00s" : "00时00分00秒") : "00:00:00");
        }
        Label timeLabel = new Label();
        timeLabel.Text = timeText;
        timeLabel.Font = isAgent
            ? (!string.IsNullOrEmpty(detailMessage) ? AppContext.GetUiFont(s.Language, 11f, FontStyle.Bold) : AppContext.GetUiFont(s.Language, 16f, FontStyle.Bold))
            : AppContext.GetUiFont(s.Language, 22f, FontStyle.Bold);
        timeLabel.ForeColor = AppContext.Win11Accent;
        timeLabel.TextAlign = ContentAlignment.MiddleCenter;
        timeLabel.SetBounds(20, 52, 310, 42);
        Controls.Add(timeLabel);

        finishLabel = new Label();
        finishLabel.Font = AppContext.GetUiFont(s.Language, 9.5f, FontStyle.Regular);
        finishLabel.ForeColor = AppContext.Win11TextSecondary;
        finishLabel.TextAlign = ContentAlignment.MiddleCenter;
        finishLabel.SetBounds(20, 102, 310, 22);
        Controls.Add(finishLabel);
        UpdateFinishLabel();

        if (onStartRest != null)
        {
            Button btnRest = new Button();
            btnRest.Text = en ? "Rest (5 min)" : "休息 5 分钟";
            btnRest.Font = AppContext.GetUiFont(s.Language, 9.5f, FontStyle.Regular);
            btnRest.SetBounds(35, 150, 130, 36);
            AppContext.StylePrimaryButton(btnRest);
            btnRest.Click += delegate
            {
                Close();
                onStartRest();
            };
            Controls.Add(btnRest);

            Button btn = new Button();
            btn.Text = en ? "OK" : "确定";
            btn.Font = AppContext.GetUiFont(s.Language, 9.5f, FontStyle.Regular);
            btn.SetBounds(185, 150, 130, 36);
            AppContext.StyleSecondaryButton(btn);
            btn.Click += delegate { Close(); };
            Controls.Add(btn);

            AcceptButton = btnRest;
            CancelButton = btn;
        }
        else
        {
            Button btn = new Button();
            btn.Text = isAgent ? (en ? "View" : "查看") : (en ? "OK" : "确定");
            btn.Font = AppContext.GetUiFont(s.Language, 9.5f, FontStyle.Regular);
            btn.SetBounds(120, 150, 110, 36);
            AppContext.StylePrimaryButton(btn);
            btn.Click += delegate
            {
                if (targetHwnd != IntPtr.Zero)
                {
                    try
                    {
                        NativeMethods.ShowWindow(targetHwnd, 9);
                        NativeMethods.SetForegroundWindow(targetHwnd);
                    }
                    catch { }
                }
                Close();
            };
            Controls.Add(btn);
            AcceptButton = btn;
            CancelButton = btn;
        }

        timer = new Timer();
        timer.Interval = 1000;
        timer.Tick += OnTimerTick;

        AppContext.CenterFormOnScreen(this);
    }

    private void UpdateFinishLabel()
    {
        bool en = settings.Language == "en";
        int secs = (int)Math.Max(0, (DateTime.Now - finishTime).TotalSeconds);
        string elapsed = (secs / 60).ToString("00") + ":" + (secs % 60).ToString("00");
        string prefix = (targetHwnd != IntPtr.Zero) ? (en ? "Completed at: " : "完成时间: ") : (en ? "Finished at: " : "结束时间: ");
        finishLabel.Text = prefix + finishTime.ToString("HH:mm:ss") + (secs > 0 ? " (+" + elapsed + ")" : "");
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        AppContext.CenterFormOnScreen(this);
        ForceTopMost();
        StartAlarm();
        if (timer != null) timer.Start();
    }

    public void ForceTopMost()
    {
        AppContext.ForceTopMost(this);
    }

    private void OnTimerTick(object sender, EventArgs e)
    {
        UpdateFinishLabel();
        ForceTopMost();
    }

    private void StartAlarm()
    {
        if (!settings.EndSound) return;
        AppContext.PlaySoundStyle(settings.EndSoundStyle);
    }

    private void StopAlarm()
    {
        try
        {
            AppContext.StopSound();
            if (player != null) { player.Stop(); player.Dispose(); player = null; }
            if (timer != null) { timer.Stop(); timer.Dispose(); timer = null; }
        }
        catch { }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        StopAlarm();
    }
}

internal sealed class CliNoticeDialog : Form
{
    private Settings settings;
    private Font msgFont;

    public CliNoticeDialog(Settings s, string message)
    {
        settings = s;
        bool en = s != null && s.Language == "en";
        string lang = s != null ? s.Language : "zh";
        Font = AppContext.GetUiFont(lang, 9.5f);
        BackColor = AppContext.Win11Bg;
        AppContext.ApplyModernWindowStyle(this);
        Text = en ? "iClock Notice" : "iClock 终端提醒";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        ShowIcon = false;
        try { Icon = AppContext.CreateIcon(); } catch { }
        TopMost = true;
        DoubleBuffered = true;
        ClientSize = new Size(350, 160);

        // Resolve user configured color
        Color msgColor;
        try { msgColor = ColorTranslator.FromHtml(s != null ? s.Color : null); }
        catch { msgColor = AppContext.Win11TextPrimary; }
        if (msgColor.IsEmpty) msgColor = AppContext.Win11TextPrimary;
        if (msgColor.R > 230 && msgColor.G > 230 && msgColor.B > 230) msgColor = AppContext.Win11TextPrimary;

        // Resolve user configured font family and font size
        string family = (s != null && !string.IsNullOrEmpty(s.FontFamily)) ? s.FontFamily : "Segoe UI";
        float userSize = (s != null && s.FontSize > 0) ? (float)s.FontSize : 20f;
        float targetSize = Math.Max(12f, Math.Min(32f, userSize));
        while (targetSize >= 12f)
        {
            try { msgFont = new Font(family, targetSize, FontStyle.Bold, GraphicsUnit.Pixel); }
            catch { msgFont = new Font("Segoe UI", targetSize, FontStyle.Bold, GraphicsUnit.Pixel); }

            Size measured = TextRenderer.MeasureText(message, msgFont, new Size(318, 0), TextFormatFlags.WordBreak);
            if (measured.Height <= 72 || targetSize <= 12f)
            {
                break;
            }
            msgFont.Dispose();
            targetSize -= 2f;
        }

        Label lblMsg = new Label();
        lblMsg.Text = message;
        lblMsg.Font = msgFont;
        lblMsg.ForeColor = msgColor;
        lblMsg.TextAlign = ContentAlignment.MiddleCenter;
        lblMsg.SetBounds(16, 18, 318, 78);
        lblMsg.AutoEllipsis = true;
        lblMsg.BackColor = Color.Transparent;
        Controls.Add(lblMsg);

        Button btnOk = new Button();
        btnOk.Text = en ? "Got it" : "我知道了";
        btnOk.Font = AppContext.GetUiFont(lang, 9.5f, FontStyle.Regular);
        btnOk.SetBounds(120, 108, 110, 34);
        AppContext.StylePrimaryButton(btnOk);
        btnOk.Click += delegate { Close(); };
        Controls.Add(btnOk);

        AcceptButton = btnOk;
        CancelButton = btnOk;
        KeyPreview = true;

        FormClosed += delegate
        {
            if (msgFont != null)
            {
                try { msgFont.Dispose(); } catch { }
                msgFont = null;
            }
        };

        AppContext.CenterFormOnScreen(this);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        AppContext.CenterFormOnScreen(this);
        ForceTopMost();
    }

    public void ForceTopMost()
    {
        AppContext.ForceTopMost(this);
    }
}

internal sealed class CliHelpDialog : Form
{
    private Settings settings;

    public CliHelpDialog(Settings s)
    {
        settings = s;
        bool en = s != null && s.Language == "en";
        string lang = s != null ? s.Language : "zh";
        Font = AppContext.GetUiFont(lang, 9.5f);
        BackColor = AppContext.Win11Bg;
        AppContext.ApplyModernWindowStyle(this);
        Text = en ? "iClock - Command Line (CLI)" : "iClock - 命令行 (CLI)";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ShowIcon = false;
        try { Icon = AppContext.CreateIcon(); } catch { }
        ClientSize = new Size(420, 364);

        // 1. Brief Intro Header
        Label lblIntro = new Label();
        lblIntro.Text = en
            ? "Control countdowns and notices directly from terminal or scripts."
            : "在终端或脚本中直接控制倒计时与任务提醒。";
        lblIntro.ForeColor = Color.FromArgb(107, 114, 128);
        lblIntro.Font = AppContext.GetUiFont(lang, 9f, FontStyle.Regular);
        lblIntro.TextAlign = ContentAlignment.MiddleLeft;
        lblIntro.SetBounds(18, 12, 384, 20);
        Controls.Add(lblIntro);

        // 2. PATH Status & Button
        bool inPath = Program.IsCliConfiguredInPath();
        Label lblStatus = new Label();
        lblStatus.Text = inPath
            ? (en ? "✓ Terminal environment ready (type 'iclock' anywhere)" : "✓ 终端环境已就绪（可在任意终端直接输入 iclock 运行）")
            : (en ? "● Status: Not yet added to User PATH" : "● 终端环境：尚未配置到 PATH 环境变量");
        lblStatus.ForeColor = inPath ? Color.FromArgb(16, 124, 65) : Color.FromArgb(202, 80, 16);
        lblStatus.Font = AppContext.GetUiFont(lang, 9f, FontStyle.Regular);
        lblStatus.TextAlign = ContentAlignment.MiddleLeft;
        lblStatus.SetBounds(18, 38, inPath ? 384 : 255, 26);
        Controls.Add(lblStatus);

        if (!inPath)
        {
            Button btnPath = new Button();
            btnPath.Text = en ? "Add to PATH" : "写入 PATH 环境变量";
            btnPath.Font = AppContext.GetUiFont(lang, 9f, FontStyle.Regular);
            btnPath.SetBounds(276, 37, 126, 28);
            AppContext.StyleSecondaryButton(btnPath);
            btnPath.Click += delegate
            {
                string msg;
                if (Program.ConfigureCliPath(out msg))
                {
                    lblStatus.Text = en ? "✓ Terminal environment ready (type 'iclock' anywhere)" : "✓ 终端环境已就绪（可在任意终端直接输入 iclock 运行）";
                    lblStatus.ForeColor = Color.FromArgb(16, 124, 65);
                    lblStatus.SetBounds(18, 38, 384, 26);
                    btnPath.Visible = false;
                }
                else
                {
                    MessageBox.Show(this, (en ? "Failed to configure PATH: " : "配置环境变量失败：") + msg, "iClock", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            Controls.Add(btnPath);
        }

        // 3. Command Quick Reference Title
        Label lblSec1 = new Label();
        lblSec1.Text = en ? "Command Quick Reference" : "常用命令速查";
        lblSec1.Font = AppContext.GetUiFont(lang, 9.5f, FontStyle.Bold);
        lblSec1.ForeColor = AppContext.Win11TextPrimary;
        lblSec1.SetBounds(18, 72, 384, 20);
        Controls.Add(lblSec1);

        string[,] cmds = en ? new string[,]
        {
            { "iclock -s [mins]", "Start countdown timer" },
            { "iclock -p / -r",   "Pause or resume countdown" },
            { "iclock -t",        "Toggle pause / resume" },
            { "iclock --reset",   "Reset timer and hide overlay" },
            { "iclock -n <msg>",  "Popup custom notification & chime" }
        } : new string[,]
        {
            { "iclock -s [mins]", "开启指定时长倒计时（默认配置分钟）" },
            { "iclock -p / -r",   "暂停或继续当前倒计时" },
            { "iclock -t",        "一键切换 暂停 / 继续" },
            { "iclock --reset",   "重置倒计时并隐藏桌面悬浮窗" },
            { "iclock -n <内容>", "弹出专属提醒卡片并播放提示音" }
        };

        int curY = 96;
        Font codeFont = new Font("Consolas", 9f, FontStyle.Bold);
        for (int i = 0; i < 5; i++)
        {
            Label lblCmd = new Label();
            lblCmd.Text = cmds[i, 0];
            lblCmd.Font = codeFont;
            lblCmd.ForeColor = AppContext.Win11Accent;
            lblCmd.TextAlign = ContentAlignment.MiddleLeft;
            lblCmd.SetBounds(22, curY, 136, 20);
            Controls.Add(lblCmd);

            Label lblDesc = new Label();
            lblDesc.Text = cmds[i, 1];
            lblDesc.Font = AppContext.GetUiFont(lang, 9f, FontStyle.Regular);
            lblDesc.ForeColor = AppContext.Win11TextPrimary;
            lblDesc.TextAlign = ContentAlignment.MiddleLeft;
            lblDesc.SetBounds(160, curY, 242, 20);
            Controls.Add(lblDesc);

            curY += 22;
        }

        // 4. Workflow Examples Title
        Label lblSec2 = new Label();
        lblSec2.Text = en ? "Typical Workflow Examples" : "典型脚本联动示例";
        lblSec2.Font = AppContext.GetUiFont(lang, 9.5f, FontStyle.Bold);
        lblSec2.ForeColor = AppContext.Win11TextPrimary;
        lblSec2.SetBounds(18, 214, 384, 20);
        Controls.Add(lblSec2);

        // Code Example Box
        Panel pnlBox = new Panel();
        pnlBox.SetBounds(18, 238, 384, 60);
        pnlBox.BackColor = Color.FromArgb(243, 244, 246);
        pnlBox.Paint += delegate(object sender, PaintEventArgs e)
        {
            using (Pen borderPen = new Pen(Color.FromArgb(229, 231, 235)))
            {
                e.Graphics.DrawRectangle(borderPen, 0, 0, pnlBox.Width - 1, pnlBox.Height - 1);
            }
        };
        Controls.Add(pnlBox);

        Label lblEx1 = new Label();
        lblEx1.Text = en ? "npm run build && iclock -n \"Build finished!\"" : "npm run build && iclock -n \"前端构建完成！\"";
        lblEx1.Font = codeFont;
        lblEx1.ForeColor = Color.FromArgb(31, 41, 55);
        lblEx1.SetBounds(10, 8, 364, 20);
        pnlBox.Controls.Add(lblEx1);

        Label lblEx2 = new Label();
        lblEx2.Text = en ? "pytest && iclock -n \"All tests passed\"" : "pytest && iclock -n \"单元测试全部通过\"";
        lblEx2.Font = codeFont;
        lblEx2.ForeColor = Color.FromArgb(31, 41, 55);
        lblEx2.SetBounds(10, 32, 364, 20);
        pnlBox.Controls.Add(lblEx2);

        // 5. Footer Button
        Button btnOk = new Button();
        btnOk.Text = en ? "Got it" : "我知道了";
        btnOk.Font = AppContext.GetUiFont(lang, 9.5f, FontStyle.Regular);
        btnOk.SetBounds(292, 314, 110, 34);
        AppContext.StylePrimaryButton(btnOk);
        btnOk.Click += delegate { Close(); };
        Controls.Add(btnOk);

        AcceptButton = btnOk;
        CancelButton = btnOk;
        KeyPreview = true;

        FormClosed += delegate
        {
            codeFont.Dispose();
        };

        AppContext.CenterFormOnScreen(this);
    }
}

internal sealed class WelcomeDialog : Form
{
    public bool StartRequested { get; private set; }

    public WelcomeDialog(Settings s) : this(s, true, false) { }

    public WelcomeDialog(Settings s, bool hotkeyActive, bool fallbackUsed = false)
    {
        bool en = s.Language == "en";
        Font = AppContext.GetUiFont(s.Language, 9.5f);
        BackColor = AppContext.Win11Bg;
        AppContext.ApplyModernWindowStyle(this);
        Text = "iClock";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        ShowIcon = false;
        try { Icon = AppContext.CreateIcon(); } catch { }
        ClientSize = new Size(380, 208);

        Label lblTitle = new Label();
        lblTitle.Text = en ? "Welcome to iClock" : "欢迎使用 iClock";
        lblTitle.Font = AppContext.GetUiFont(s.Language, 13.5f, FontStyle.Bold);
        lblTitle.ForeColor = AppContext.Win11Accent;
        lblTitle.TextAlign = ContentAlignment.MiddleCenter;
        lblTitle.SetBounds(0, 16, ClientSize.Width, 26);
        Controls.Add(lblTitle);

        Label lblPrompt = new Label();
        if (!hotkeyActive)
        {
            lblPrompt.Text = en ? "Candidate hotkeys occupied. Click Start below:" : "候选快捷键均被占用，可点击下方按钮开始：";
            lblPrompt.ForeColor = Color.FromArgb(202, 80, 16);
        }
        else if (fallbackUsed)
        {
            lblPrompt.Text = en ? "Default hotkey occupied. Switched to backup:" : "默认快捷键已被占用，已自动启用备用键：";
            lblPrompt.ForeColor = Color.FromArgb(202, 80, 16);
        }
        else
        {
            lblPrompt.Text = en ? "Press shortcut to start or pause:" : "按下快捷键启动或暂停倒计时：";
            lblPrompt.ForeColor = AppContext.Win11TextPrimary;
        }
        lblPrompt.Font = AppContext.GetUiFont(s.Language, 9.5f, FontStyle.Regular);
        lblPrompt.TextAlign = ContentAlignment.MiddleCenter;
        lblPrompt.SetBounds(0, 52, ClientSize.Width, 20);
        Controls.Add(lblPrompt);

        string hotkey = hotkeyActive ? AppContext.HotkeyText(s.HotkeyModifiers, s.HotkeyKey) : (en ? "(No Hotkey)" : "(快捷键未生效)");
        Label badge = new Label();
        badge.Text = hotkey;
        badge.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
        badge.ForeColor = hotkeyActive ? (fallbackUsed ? Color.FromArgb(202, 80, 16) : AppContext.Win11Accent) : Color.FromArgb(202, 80, 16);
        badge.BackColor = Color.White;
        badge.TextAlign = ContentAlignment.MiddleCenter;
        int badgeWidth = 120;
        using (Graphics g = CreateGraphics())
        {
            SizeF sz = g.MeasureString(hotkey, badge.Font);
            badgeWidth = Math.Max(70, (int)Math.Ceiling(sz.Width) + 24);
        }
        badge.SetBounds((ClientSize.Width - badgeWidth) / 2, 78, badgeWidth, 28);
        badge.Paint += delegate(object sender, PaintEventArgs pe)
        {
            pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen p = new Pen(Color.FromArgb(209, 213, 219), 1.2f))
            {
                pe.Graphics.DrawRectangle(p, 0, 0, badge.Width - 1, badge.Height - 1);
            }
        };
        Controls.Add(badge);

        Panel trayPanel = new Panel();
        trayPanel.SetBounds(0, 116, ClientSize.Width, 22);
        trayPanel.BackColor = Color.Transparent;

        string t1 = en ? "Right-click system tray icon" : "右键系统托盘图标";
        string t2 = en ? "for settings" : "可进行设置";
        Font trayFont = AppContext.GetUiFont(s.Language, 8.5f, FontStyle.Regular);

        Label lblT1 = new Label();
        lblT1.AutoSize = true;
        lblT1.Text = t1;
        lblT1.Font = trayFont;
        lblT1.ForeColor = AppContext.Win11TextSecondary;

        Label lblT2 = new Label();
        lblT2.AutoSize = true;
        lblT2.Text = t2;
        lblT2.Font = trayFont;
        lblT2.ForeColor = AppContext.Win11TextSecondary;

        int iconSize = Math.Max(16, SystemInformation.SmallIconSize.Width);
        int gap = 4;
        int totalTrayW = lblT1.PreferredWidth + gap + iconSize + gap + lblT2.PreferredWidth;
        int trayStartX = (ClientSize.Width - totalTrayW) / 2;

        lblT1.Location = new Point(trayStartX, (trayPanel.Height - lblT1.PreferredHeight) / 2);
        trayPanel.Controls.Add(lblT1);

        PictureBox trayIcon = new PictureBox();
        trayIcon.SetBounds(trayStartX + lblT1.PreferredWidth + gap, (trayPanel.Height - iconSize) / 2, iconSize, iconSize);
        trayIcon.SizeMode = PictureBoxSizeMode.Zoom;
        try
        {
            Icon appIcon = AppContext.CreateIcon();
            if (appIcon != null)
            {
                using (Icon targetIco = new Icon(appIcon, iconSize, iconSize))
                {
                    trayIcon.Image = targetIco.ToBitmap();
                }
            }
        }
        catch { }
        trayPanel.Controls.Add(trayIcon);

        lblT2.Location = new Point(trayStartX + lblT1.PreferredWidth + gap + iconSize + gap, (trayPanel.Height - lblT2.PreferredHeight) / 2);
        trayPanel.Controls.Add(lblT2);

        Controls.Add(trayPanel);

        int totalBtnWidth = 126 + 12 + 86;
        int startX = (ClientSize.Width - totalBtnWidth) / 2;

        Button btnStart = new Button();
        btnStart.Text = en ? "Start Countdown" : "开始倒计时";
        btnStart.Font = AppContext.GetUiFont(s.Language, 9.5f, FontStyle.Regular);
        btnStart.SetBounds(startX, 154, 126, 34);
        AppContext.StylePrimaryButton(btnStart);
        btnStart.Click += delegate { StartRequested = true; Close(); };
        Controls.Add(btnStart);

        Button btnOk = new Button();
        btnOk.Text = en ? "Got it" : "知道了";
        btnOk.Font = AppContext.GetUiFont(s.Language, 9.5f, FontStyle.Regular);
        btnOk.SetBounds(startX + 126 + 12, 154, 86, 34);
        AppContext.StyleSecondaryButton(btnOk);
        btnOk.Click += delegate { Close(); };
        Controls.Add(btnOk);

        AcceptButton = btnStart;
        CancelButton = btnOk;

        KeyPreview = true;
        KeyDown += delegate(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space)
            {
                e.Handled = true;
                StartRequested = true;
                Close();
            }
        };

        AppContext.CenterFormOnScreen(this);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        AppContext.CenterFormOnScreen(this);
    }
}

internal class AboutDialog : Form
{
    private AppContext app;
    private string language;
    private Button btnCheck;
    private Label lblVersion;

    private sealed class HqPictureBox : PictureBox
    {
        protected override void OnPaint(PaintEventArgs pe)
        {
            pe.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            pe.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            base.OnPaint(pe);
        }
    }

    public AboutDialog(string lang) : this(lang, null) { }

    public AboutDialog(string lang, AppContext appContext)
    {
        language = lang;
        app = appContext;
        bool en = language == "en";
        Font = AppContext.GetUiFont(language, 9.5f);
        BackColor = AppContext.Win11Bg;
        AppContext.ApplyModernWindowStyle(this);
        Text = en ? "About iClock" : "关于 iClock";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(350, 508);

        Label lblTitle = new Label();
        lblTitle.Text = "iClock";
        lblTitle.Font = AppContext.GetUiFont(language, 14f, FontStyle.Bold);
        lblTitle.ForeColor = AppContext.Win11Accent;
        lblTitle.SetBounds(20, 16, 140, 26);
        Controls.Add(lblTitle);

        Label lblDesc = new Label();
        lblDesc.Text = en ? "Desktop Floating Countdown Timer" : "桌面极简悬浮倒计时工具";
        lblDesc.Font = AppContext.GetUiFont(language, 9f, FontStyle.Regular);
        lblDesc.ForeColor = AppContext.Win11TextSecondary;
        lblDesc.SetBounds(20, 44, 310, 20);
        Controls.Add(lblDesc);

        lblVersion = new Label();
        lblVersion.Text = (en ? "Version: v" : "当前版本: v") + AppContext.CURRENT_VERSION;
        lblVersion.Font = AppContext.GetUiFont(language, 9.5f, FontStyle.Regular);
        lblVersion.ForeColor = AppContext.Win11TextPrimary;
        lblVersion.SetBounds(20, 72, 180, 24);
        lblVersion.TextAlign = ContentAlignment.MiddleLeft;
        Controls.Add(lblVersion);

        btnCheck = new Button();
        btnCheck.Text = en ? "Check Updates" : "检查更新";
        btnCheck.Font = AppContext.GetUiFont(language, 9f, FontStyle.Regular);
        btnCheck.SetBounds(210, 70, 120, 26);
        AppContext.StyleSecondaryButton(btnCheck);
        btnCheck.Click += OnCheckUpdates;
        Controls.Add(btnCheck);

        LinkLabel link = new LinkLabel();
        link.Text = "GitHub: francodeyvison183-collab/iClock";
        link.Font = AppContext.GetUiFont(language, 9f, FontStyle.Regular);
        link.LinkColor = AppContext.Win11Accent;
        link.SetBounds(20, 102, 310, 20);
        link.LinkClicked += delegate { try { Process.Start("https://github.com/francodeyvison183-collab/iClock"); } catch { } };
        Controls.Add(link);

        Label line = new Label();
        line.BackColor = AppContext.Win11Border;
        line.BorderStyle = BorderStyle.None;
        line.SetBounds(20, 128, 310, 1);
        Controls.Add(line);

        Label lblSponsor = new Label();
        lblSponsor.Text = en ? "If iClock helps you, thank you for supporting!" : "如果 iClock 对你有帮助，欢迎赞赏支持！";
        lblSponsor.Font = AppContext.GetUiFont(language, 9.5f, FontStyle.Regular);
        lblSponsor.ForeColor = AppContext.Win11TextPrimary;
        lblSponsor.TextAlign = ContentAlignment.MiddleCenter;
        lblSponsor.SetBounds(20, 138, 310, 24);
        Controls.Add(lblSponsor);

        HqPictureBox pic = new HqPictureBox();
        pic.SetBounds(25, 166, 300, 300);
        pic.SizeMode = PictureBoxSizeMode.Zoom;
        pic.BorderStyle = BorderStyle.None;
        pic.Image = LoadSponsorImage();
        Controls.Add(pic);

        Label sub = new Label();
        sub.Text = en ? "WeChat Pay" : "微信扫一扫 赞赏码";
        sub.Font = AppContext.GetUiFont(language, 9.5f, FontStyle.Bold);
        sub.ForeColor = AppContext.Win11Accent;
        sub.TextAlign = ContentAlignment.MiddleCenter;
        sub.SetBounds(20, 472, 310, 20);
        Controls.Add(sub);

        KeyPreview = true;
        KeyDown += delegate(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                Close();
            }
        };
        AppContext.CenterFormOnScreen(this);
    }

    private void OnCheckUpdates(object sender, EventArgs e)
    {
        bool en = language == "en";
        btnCheck.Enabled = false;
        btnCheck.Text = en ? "Checking..." : "检查中...";

        System.Threading.ThreadPool.QueueUserWorkItem(delegate
        {
            string latest, url;
            bool success = AppContext.QueryLatestVersion("check", out latest, out url);

            try
            {
                if (IsDisposed || !IsHandleCreated) return;
                BeginInvoke(new MethodInvoker(delegate
                {
                    btnCheck.Enabled = true;
                    btnCheck.Text = en ? "Check Updates" : "检查更新";

                    if (!success || string.IsNullOrEmpty(latest))
                    {
                        MessageBox.Show(this, en ? "Failed to check for updates. Please check your network." : "检查更新失败，请检查网络连接。", "iClock", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    if (AppContext.IsNewer(latest, AppContext.CURRENT_VERSION))
                    {
                        string targetUrl = string.IsNullOrEmpty(url) ? "https://github.com/francodeyvison183-collab/iClock/releases/latest" : url;
                        if (app != null) app.NotifyUpdateFound(latest, targetUrl);
                        string msg = en ? "New version " + latest + " is available! Do you want to download it now?" : "发现新版本 " + latest + "！是否立即前往下载？";
                        if (MessageBox.Show(this, msg, "iClock", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                        {
                            try { Process.Start(targetUrl); } catch { }
                        }
                    }
                    else
                    {
                        MessageBox.Show(this, en ? "You are using the latest version (v" + AppContext.CURRENT_VERSION + ")." : "当前已是最新版本 (v" + AppContext.CURRENT_VERSION + ")。", "iClock", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }));
            }
            catch { }
        });
    }

    private static Image LoadSponsorImage()
    {
        try
        {
            Stream stream = typeof(AppContext).Assembly.GetManifestResourceStream("zan.jpg");
            if (stream != null)
            {
                using (stream)
                using (Image temp = Image.FromStream(stream))
                {
                    return new Bitmap(temp);
                }
            }
        }
        catch { }
        try
        {
            string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "src", "zan.jpg");
            if (File.Exists(localPath))
            {
                using (Image temp = Image.FromFile(localPath))
                {
                    return new Bitmap(temp);
                }
            }
            localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "zan.jpg");
            if (File.Exists(localPath))
            {
                using (Image temp = Image.FromFile(localPath))
                {
                    return new Bitmap(temp);
                }
            }
        }
        catch { }
        return null;
    }
}

internal sealed class ModernContextMenu : ContextMenuStrip
{
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int w = ClientSize.Width;
        foreach (ToolStripItem item in Items)
        {
            if (item != null && item.Width != w)
            {
                item.Size = new Size(w, item.Height);
            }
        }
    }
}

internal sealed class ModernMenuRenderer : ToolStripProfessionalRenderer
{
    public ModernMenuRenderer() : base(new ModernMenuColorTable()) { }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Enabled) return;
        if (e.Item.Selected)
        {
            Rectangle rc = new Rectangle(4, 2, e.Item.Width - 8, e.Item.Height - 4);
            using (GraphicsPath path = AppContext.GetRoundedRectPath(rc, 4))
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(238, 238, 238)))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillPath(brush, path);
            }
        }
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        int left = 32;
        int right = e.Item.Width - 24;
        int width = Math.Max(0, right - left);
        e.TextRectangle = new Rectangle(left, 0, width, e.Item.Height);

        ToolStripMenuItem mi = e.Item as ToolStripMenuItem;
        bool isShortcut = mi != null && !string.IsNullOrEmpty(mi.ShortcutKeyDisplayString) && e.Text == mi.ShortcutKeyDisplayString;

        if (isShortcut)
        {
            e.TextColor = (e.Text.Contains("未生效") || e.Text.Contains("Disabled"))
                ? Color.FromArgb(202, 80, 16)
                : AppContext.Win11ShortcutGray;
            e.TextFormat = TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
        }
        else
        {
            e.TextFormat = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
            if (e.Item.Enabled && !e.Item.ForeColor.IsEmpty && e.Item.ForeColor != SystemColors.ControlText && e.Item.ForeColor != AppContext.Win11TextPrimary)
                e.TextColor = e.Item.ForeColor;
            else if (e.Item.Enabled)
                e.TextColor = AppContext.Win11TextPrimary;
        }
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        int y = e.Item.Height / 2;
        using (Pen p = new Pen(AppContext.Win11Border, 1f))
        {
            e.Graphics.DrawLine(p, 32, y, e.Item.Width - 32, y);
        }
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using (Pen p = new Pen(AppContext.Win11Border, 1f))
        {
            e.Graphics.DrawRectangle(p, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }
    }
}

internal sealed class ModernMenuColorTable : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground { get { return AppContext.Win11Bg; } }
    public override Color ImageMarginGradientBegin { get { return AppContext.Win11Bg; } }
    public override Color ImageMarginGradientMiddle { get { return AppContext.Win11Bg; } }
    public override Color ImageMarginGradientEnd { get { return AppContext.Win11Bg; } }
    public override Color MenuBorder { get { return AppContext.Win11Border; } }
    public override Color MenuItemBorder { get { return Color.Transparent; } }
}

internal enum ClockState
{
    Idle,
    Running,
    Paused,
    RestRunning,
    RestPaused,
    Alarming,
    MoveMode
}

internal sealed class AppContext : ApplicationContext
{
    private const int HOTKEY_ID = 0x4A10;
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_NOREPEAT = 0x4000;
    private Settings settings;
    private Overlay overlay;
    private NotifyIcon tray;
    private ToolStripMenuItem menuStart, menuReset, menuMove, menuHistory, menuSettings, menuCli, menuAbout, menuExit, menuUpdate;
    internal const string CURRENT_VERSION = "1.05";
    private string updateUrl;
    private string latestVersion;
    private Timer timer;
    private TimeSpan remaining;
    private long deadlineTimestamp;
    private DateTime sessionStart;
    private int sessionMinutes;
    private bool sessionActive;
    private bool running;
    private ClockState state = ClockState.Idle;
    internal ClockState State { get { return state; } }
    private ClockState preMoveState = ClockState.Idle;

    private void SetState(ClockState newState)
    {
        state = newState;
        running = (state == ClockState.Running || state == ClockState.RestRunning);
        sessionActive = (state != ClockState.Idle && state != ClockState.MoveMode);
    }
    private bool hotkeyRegistered;
    private int activeHotkeyModifiers, activeHotkeyKey;
    private Form activeNotice;
    private Form activeDialog;
    private long lastSeconds = -1;
    private MessageWindow messageWindow;
    internal uint shellHookMsg;
    private const int HSHELL_FLASH = 0x8006;
    private string exePath = Application.ExecutablePath;
    private Icon appIcon;

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public Point pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private const int WH_MOUSE_LL = 14;
    private const int WM_MOUSEWHEEL = 0x020A;
    private LowLevelMouseProc mouseHookProc;
    private IntPtr mouseHookHandle = IntPtr.Zero;
    private static int lastTrayMouseMove;
    private EventHandler displaySettingsHandler;
    private SessionSwitchEventHandler sessionSwitchHandler;
    private bool pausedByLock;

    private void EnsureMouseHook()
    {
        if (mouseHookHandle == IntPtr.Zero)
        {
            if (mouseHookProc == null) mouseHookProc = MouseHookCallback;
            try { mouseHookHandle = NativeMethods.SetWindowsHookEx(WH_MOUSE_LL, mouseHookProc, NativeMethods.GetModuleHandle(null), 0); } catch { }
        }
    }

    private void UnhookMouse()
    {
        if (mouseHookHandle != IntPtr.Zero)
        {
            try { NativeMethods.UnhookWindowsHookEx(mouseHookHandle); } catch { }
            mouseHookHandle = IntPtr.Zero;
        }
    }

    internal static void TrimMemory()
    {
        try
        {
            GC.Collect(1, GCCollectionMode.Optimized);
            NativeMethods.EmptyWorkingSet(Process.GetCurrentProcess().Handle);
        }
        catch { }
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        int now = Environment.TickCount;
        if (Math.Abs(now - lastTrayMouseMove) > 2000)
        {
            UnhookMouse();
            return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }
        if (nCode >= 0 && wParam == (IntPtr)WM_MOUSEWHEEL)
        {
            MSLLHOOKSTRUCT hs = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
            short wheelDelta = (short)((hs.mouseData >> 16) & 0xFFFF);
            if (wheelDelta != 0)
            {
                lastTrayMouseMove = now;
                int step = (wheelDelta > 0 ? 1 : -1) * ((Control.ModifierKeys & Keys.Shift) != 0 ? 5 : 1);
                AdjustMinutes(step);
                return (IntPtr)1;
            }
        }
        return NativeMethods.CallNextHookEx(mouseHookHandle, nCode, wParam, lParam);
    }

    internal void AdjustMinutes(int delta)
    {
        if (delta == 0) return;
        if (!running)
        {
            int newMin = Math.Max(1, Math.Min(1440, settings.Minutes + delta));
            if (newMin == settings.Minutes) return;
            settings.Minutes = newMin;
            settings.Save();
            remaining = TimeSpan.FromMinutes(settings.Minutes);
            ResetDisplay();
            bool en = settings.Language == "en";
            string tip = en ? "Duration: " + settings.Minutes + " min" : "倒计时时长：" + settings.Minutes + " 分钟";
            try { if (tip.Length < 64) tray.Text = tip; } catch { }
        }
        else
        {
            deadlineTimestamp += (long)(delta * 60L * Stopwatch.Frequency);
            remaining = ReadRemaining();
            lastSeconds = -1;
            if (remaining <= TimeSpan.Zero)
            {
                Tick(null, EventArgs.Empty);
            }
            else
            {
                Tick(null, EventArgs.Empty);
            }
        }
    }

    public AppContext() : this(null) { }

    public AppContext(string[] initialArgs)
    {
        settings = Settings.Load();
        appIcon = CreateIcon();
        overlay = new Overlay(settings); overlay.PositionChanged += delegate(Point p) { settings.X = p.X; settings.Y = p.Y; settings.Save(); };
        overlay.TimeAdjustRequested += delegate(int delta) { AdjustMinutes(delta); };
        IntPtr forceOverlayHandle = overlay.Handle;
        messageWindow = new MessageWindow(this);
        shellHookMsg = NativeMethods.RegisterWindowMessage("SHELLHOOK");
        if (shellHookMsg != 0)
        {
            try { NativeMethods.RegisterShellHookWindow(messageWindow.Handle); } catch { }
        }
        activeHotkeyModifiers = settings.HotkeyModifiers; activeHotkeyKey = settings.HotkeyKey;
        int originalMods = settings.HotkeyModifiers, originalKey = settings.HotkeyKey;
        bool fallbackUsed = false;
        if (settings.HotkeyKey == (int)Keys.Space && (settings.HotkeyModifiers & 3) != 0)
        {
            settings.HotkeyModifiers = 6;
            settings.HotkeyKey = (int)Keys.T;
            fallbackUsed = true;
        }
        if (!SetHotkey(settings.HotkeyModifiers, settings.HotkeyKey))
        {
            int[][] fallbacks = new int[][]
            {
                new int[] { 6, (int)Keys.T },     // Ctrl + Shift + T
                new int[] { 3, (int)Keys.T },     // Ctrl + Alt + T
                new int[] { 5, (int)Keys.C },     // Alt + Shift + C
                new int[] { 0, (int)Keys.F8 }     // F8
            };
            for (int i = 0; i < fallbacks.Length; i++)
            {
                int m = fallbacks[i][0], k = fallbacks[i][1];
                if (m == originalMods && k == originalKey) continue;
                if (SetHotkey(m, k))
                {
                    settings.HotkeyModifiers = m;
                    settings.HotkeyKey = k;
                    settings.Save();
                    fallbackUsed = true;
                    break;
                }
            }
        }
        else if (fallbackUsed)
        {
            settings.Save();
        }
        tray = new NotifyIcon(); tray.Icon = appIcon; tray.Text = "iClock"; tray.Visible = true; tray.ContextMenuStrip = MakeMenu();
        tray.MouseMove += delegate { lastTrayMouseMove = Environment.TickCount; EnsureMouseHook(); };
        tray.BalloonTipClicked += delegate { if (!string.IsNullOrEmpty(updateUrl)) { try { Process.Start(updateUrl); } catch { } } };
        tray.MouseClick += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) { Toggle(); } };
        timer = new Timer(); timer.Interval = 100; timer.Tick += Tick;
        Timer startupTrim = new Timer(); startupTrim.Interval = 3500;
        startupTrim.Tick += delegate { startupTrim.Stop(); startupTrim.Dispose(); TrimMemory(); };
        startupTrim.Start();
        ApplyStartup();
        EnsureStartMenuShortcut(false);
        ResetDisplay();
        TrackAndCheckUpdates();
        displaySettingsHandler = delegate
        {
            if (overlay == null || overlay.IsDisposed) return;
            if (overlay.InvokeRequired) overlay.BeginInvoke(new Action(delegate { overlay.EnsureVisible(); }));
            else overlay.EnsureVisible();
        };
        SystemEvents.DisplaySettingsChanged += displaySettingsHandler;
        sessionSwitchHandler = delegate(object ss, SessionSwitchEventArgs se)
        {
            if (se.Reason == SessionSwitchReason.SessionLock)
            {
                if (running)
                {
                    if (overlay.InvokeRequired) overlay.BeginInvoke(new Action(delegate { Pause(); }));
                    else Pause();
                    pausedByLock = true;
                }
            }
            else if (se.Reason == SessionSwitchReason.SessionUnlock)
            {
                if (pausedByLock && sessionActive && !running)
                {
                    pausedByLock = false;
                    // Don't auto-resume — just leave paused so user sees remaining time and clicks to continue
                }
            }
        };
        SystemEvents.SessionSwitch += sessionSwitchHandler;
        if (fallbackUsed && !settings.FirstRun)
        {
            bool en = settings.Language == "en";
            string oldKeyText = HotkeyText(originalMods, originalKey);
            string newKeyText = HotkeyText(settings.HotkeyModifiers, settings.HotkeyKey);
            string msg = en
                ? "The hotkey (" + oldKeyText + ") is unavailable or conflicts with Windows IME.\r\n\r\niClock has automatically switched to a reliable backup: " + newKeyText + "\r\n\r\nYou can customize it at any time in Settings."
                : "快捷键 " + oldKeyText + " 在当前系统下不可用（已被占用或与系统输入法冲突）。\r\n\r\niClock 已自动为您切换至稳定快捷键：" + newKeyText + "\r\n\r\n您随时可在“右键托盘 -> 设置”中自定义快捷键。";
            overlay.BeginInvoke(new Action(delegate { MessageBox.Show(msg, "iClock", MessageBoxButtons.OK, MessageBoxIcon.Information); }));
        }
        else if (!hotkeyRegistered && !settings.FirstRun)
        {
            bool en = settings.Language == "en";
            string msg = en
                ? "All candidate hotkeys are occupied by other applications. Hotkey is currently disabled.\r\n\r\nWould you like to open Settings now to configure a custom hotkey?"
                : "所有备选快捷键均已被其他程序占用，当前快捷键未生效。\r\n\r\n是否立即打开“设置”手动指定新的快捷键？";
            overlay.BeginInvoke(new Action(delegate
            {
                if (MessageBox.Show(msg, "iClock", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    ShowSettings();
                }
            }));
        }
        UpdateAgentMonitoring();
        if (settings.FirstRun)
        {
            overlay.BeginInvoke(new Action(delegate { ShowWelcome(fallbackUsed); }));
        }
        if (initialArgs != null && initialArgs.Length > 0)
        {
            HandleIpcCommand(string.Join(" ", initialArgs));
        }
    }

    private void ShowWelcome(bool fallbackUsed = false)
    {
        if (!settings.FirstRun) return;
        settings.FirstRun = false;
        settings.Save();
        EnsureStartMenuShortcut(true);
        using (WelcomeDialog dlg = new WelcomeDialog(settings, hotkeyRegistered, fallbackUsed))
        {
            dlg.ShowDialog();
            if (dlg.StartRequested)
            {
                Toggle();
            }
        }
    }

    private ContextMenuStrip MakeMenu()
    {
        ContextMenuStrip m = new ModernContextMenu();
        m.Font = GetUiFont(settings.Language, 9.5f);
        m.Renderer = new ModernMenuRenderer();
        m.ShowImageMargin = false;
        m.ShowCheckMargin = false;
        m.MinimumSize = new Size(260, 0);
        m.Padding = new Padding(2, 14, 2, 6);
        m.Opened += delegate { ApplyModernWindowStyle(m); };
        m.Opening += delegate
        {
            UpdateMenuText();
            int maxNeeded = 260;
            foreach (ToolStripItem it in m.Items)
            {
                ToolStripMenuItem mi = it as ToolStripMenuItem;
                if (mi == null) continue;
                int tw = TextRenderer.MeasureText(mi.Text, mi.Font).Width;
                int scw = string.IsNullOrEmpty(mi.ShortcutKeyDisplayString) ? 0 : TextRenderer.MeasureText(mi.ShortcutKeyDisplayString, m.Font).Width;
                int needed = 32 + tw + (scw > 0 ? 28 + scw : 0) + 24;
                if (needed > maxNeeded) maxNeeded = needed;
            }
            m.MinimumSize = new Size(maxNeeded, 0);
        };

        menuStart = AddMenuItem(m, delegate { Toggle(); });
        menuStart.Font = new Font(m.Font, FontStyle.Bold);
        menuReset = AddMenuItem(m, delegate { Reset(); });
        menuMove = AddMenuItem(m, delegate { ToggleMove(); });
        m.Items.Add(new ToolStripSeparator());
        menuHistory = AddMenuItem(m, delegate { ShowHistory(); });
        menuSettings = AddMenuItem(m, delegate { ShowSettings(); });
        menuCli = AddMenuItem(m, delegate { ShowCliHelp(); });
        menuAbout = AddMenuItem(m, delegate { ShowAbout(); });
        m.Items.Add(new ToolStripSeparator());
        menuExit = AddMenuItem(m, delegate { Exit(); });
        UpdateMenuText();
        return m;
    }

    internal void Reset()
    {
        if (overlay.IsPulsing) overlay.StopBreathingPulse();
        if (activeNotice != null && !activeNotice.IsDisposed) { activeNotice.Close(); activeNotice = null; }
        if (running) remaining = ReadRemaining();
        timer.Stop();
        if (sessionActive)
        {
            LogSession((state == ClockState.RestRunning || state == ClockState.RestPaused) ? "RestReset" : "Reset");
        }
        SetState(ClockState.Idle);
        ResetDisplay();
        overlay.SetPaused(false);
        overlay.Hide();
        UpdateMenuText();
        TrimMemory();
    }

    private ToolStripMenuItem AddMenuItem(ContextMenuStrip m, EventHandler onClick)
    {
        ToolStripMenuItem item = new ToolStripMenuItem();
        item.Padding = new Padding(32, 7, 32, 7);
        item.ForeColor = Win11TextPrimary;
        item.Click += onClick;
        m.Items.Add(item);
        return item;
    }

    private void UpdateMenuText()
    {
        bool en = settings.Language == "en";
        if (menuStart == null) return;

        if (state == ClockState.RestRunning)
        {
            menuStart.Text = en ? "Pause break" : "暂停休息";
            menuStart.ForeColor = Color.FromArgb(202, 80, 16);
        }
        else if (state == ClockState.RestPaused)
        {
            menuStart.Text = en ? "Resume break" : "继续休息";
            menuStart.ForeColor = Color.FromArgb(16, 124, 65);
        }
        else if (running)
        {
            menuStart.Text = en ? "Pause countdown" : "暂停倒计时";
            menuStart.ForeColor = Color.FromArgb(202, 80, 16);
        }
        else if (sessionActive)
        {
            menuStart.Text = en ? "Resume countdown" : "继续倒计时";
            menuStart.ForeColor = Color.FromArgb(16, 124, 65);
        }
        else
        {
            menuStart.Text = en ? "Start countdown" : "开始倒计时";
            menuStart.ForeColor = Color.FromArgb(0, 103, 192);
        }

        menuStart.ShortcutKeyDisplayString = hotkeyRegistered
            ? HotkeyText(settings.HotkeyModifiers, settings.HotkeyKey)
            : (en ? "(Disabled)" : "(未生效)");
        menuReset.Text = (state == ClockState.RestRunning || state == ClockState.RestPaused) ? (en ? "Reset break" : "重置休息") : (en ? "Reset countdown" : "重置倒计时");
        menuReset.Enabled = running || sessionActive;
        menuMove.Text = overlay.MoveMode ? (en ? "✓ Finish position adjustment" : "✓ 完成位置调整") : (en ? "Adjust text position" : "调整文字位置");
        menuMove.ShortcutKeyDisplayString = overlay.MoveMode ? "" : (en ? "Alt + Drag" : "Alt + 拖拽");
        menuHistory.Text = en ? "View today's history" : "查看今日记录";
        menuSettings.Text = en ? "Settings…" : "设置…";
        menuCli.Text = en ? "Command Line (CLI)…" : "命令行 (CLI)…";
        menuCli.ShortcutKeyDisplayString = "iclock -h";
        menuAbout.Text = en ? "About iClock…" : "关于 iClock…";
        menuExit.Text = en ? "Exit" : "退出";
        if (menuUpdate != null) menuUpdate.Text = en ? "⭐ Update available (" + latestVersion + ")…" : "⭐ 发现新版本 (" + latestVersion + ")…";
        string actionTip = running ? (en ? "Click to pause" : "单击暂停")
            : (sessionActive ? (en ? "Click to resume" : "单击继续") : (en ? "Click to start" : "单击开始"));
        tray.Text = hotkeyRegistered
            ? (en ? "iClock (" + actionTip + ")" : "iClock（" + actionTip + "）")
            : (en ? "iClock (" + actionTip + " · Hotkey conflict)" : "iClock（" + actionTip + " · 快捷键未生效）");
    }

    internal void Pause()
    {
        if (!running) return;
        remaining = ReadRemaining();
        timer.Stop();
        lastSeconds = -1;
        if (state == ClockState.RestRunning)
            SetState(ClockState.RestPaused);
        else
            SetState(ClockState.Paused);
        overlay.SetPaused(true);
        Display(remaining);
        UpdateMenuText();
    }

    internal void StartWork()
    {
        StartWorkWithMinutes(settings.Minutes);
    }

    internal void StartWorkWithMinutes(int minutes)
    {
        pausedByLock = false;
        if (activeNotice != null && !activeNotice.IsDisposed) { activeNotice.Close(); activeNotice = null; }
        int m = minutes > 0 ? Math.Min(1440, minutes) : settings.Minutes;
        remaining = TimeSpan.FromMinutes(m);
        sessionStart = DateTime.Now;
        sessionMinutes = m;
        deadlineTimestamp = Stopwatch.GetTimestamp() + (long)(remaining.TotalSeconds * Stopwatch.Frequency);
        SetState(ClockState.Running);
        lastSeconds = -1;
        timer.Interval = 100;
        timer.Start();
        overlay.SetPaused(false);
        overlay.Show();
        Tick(null, EventArgs.Empty);
        UpdateMenuText();
    }

    internal void StartRest(int minutes = 5)
    {
        if (overlay.IsPulsing) overlay.StopBreathingPulse();
        if (activeNotice != null && !activeNotice.IsDisposed) { activeNotice.Close(); activeNotice = null; }
        remaining = TimeSpan.FromMinutes(minutes);
        sessionStart = DateTime.Now;
        sessionMinutes = minutes;
        deadlineTimestamp = Stopwatch.GetTimestamp() + (long)(remaining.TotalSeconds * Stopwatch.Frequency);
        SetState(ClockState.RestRunning);
        lastSeconds = -1;
        timer.Interval = 100;
        timer.Start();
        overlay.SetPaused(false);
        overlay.Show();
        Tick(null, EventArgs.Empty);
        UpdateMenuText();
    }

    internal void Resume()
    {
        if (running) return;
        pausedByLock = false;
        deadlineTimestamp = Stopwatch.GetTimestamp() + (long)(remaining.TotalSeconds * Stopwatch.Frequency);
        if (state == ClockState.RestPaused)
            SetState(ClockState.RestRunning);
        else
            SetState(ClockState.Running);
        lastSeconds = -1;
        timer.Interval = 100;
        timer.Start();
        overlay.SetPaused(false);
        overlay.Show();
        Tick(null, EventArgs.Empty);
        UpdateMenuText();
    }

    private void Toggle()
    {
        if (overlay.IsPulsing) { overlay.StopBreathingPulse(); SetState(ClockState.Idle); return; }
        if (running) { Pause(); return; }
        if (sessionActive) { Resume(); return; }
        StartWork();
    }

    internal void HandleIpcCommand(string cmd)
    {
        if (string.IsNullOrEmpty(cmd)) return;
        if (overlay != null && overlay.InvokeRequired)
        {
            overlay.BeginInvoke(new Action(delegate { HandleIpcCommand(cmd); }));
            return;
        }
        cmd = cmd.Trim();
        string[] parts = cmd.Split(new char[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;
        string action = parts[0].ToLowerInvariant();
        string arg = parts.Length > 1 ? parts[1].Trim() : null;

        if (action == "start" || action == "-s" || action == "--start")
        {
            int mins = settings.Minutes;
            if (!string.IsNullOrEmpty(arg))
            {
                int parsed;
                if (int.TryParse(arg, out parsed) && parsed > 0) mins = parsed;
            }
            StartWorkWithMinutes(mins);
        }
        else if (action == "pause" || action == "-p" || action == "--pause")
        {
            Pause();
        }
        else if (action == "resume" || action == "-r" || action == "--resume")
        {
            Resume();
        }
        else if (action == "toggle" || action == "-t" || action == "--toggle")
        {
            Toggle();
        }
        else if (action == "reset" || action == "--reset")
        {
            Reset();
        }
        else if (action == "rest" || action == "--rest")
        {
            int mins = 5;
            if (!string.IsNullOrEmpty(arg))
            {
                int parsed;
                if (int.TryParse(arg, out parsed) && parsed > 0) mins = parsed;
            }
            StartRest(mins);
        }
        else if (action == "notice" || action == "-n" || action == "--notice")
        {
            TriggerCliNotice(arg);
        }
    }

    internal void TriggerCliNotice(string message)
    {
        if (string.IsNullOrEmpty(message)) message = settings.Language == "en" ? "Task completed!" : "任务已完成！";
        if (activeNotice != null && !activeNotice.IsDisposed) { activeNotice.Close(); activeNotice = null; }
        if (settings.EndSound) PlaySoundStyle(settings.EndSoundStyle);
        CliNoticeDialog dlg = new CliNoticeDialog(settings, message);
        activeNotice = dlg;
        dlg.FormClosed += delegate { if (activeNotice == dlg) activeNotice = null; };
        dlg.Show();
        ForceTopMost(dlg);
    }

    private TimeSpan ReadRemaining()
    {
        long ticks = deadlineTimestamp - Stopwatch.GetTimestamp();
        return ticks <= 0 ? TimeSpan.Zero : TimeSpan.FromSeconds((double)ticks / Stopwatch.Frequency);
    }

    private void Tick(object sender, EventArgs e)
    {
        if (!running) return;
        long ticksLeft = deadlineTimestamp - Stopwatch.GetTimestamp();
        if (ticksLeft <= 0) {
            remaining = TimeSpan.Zero;
            timer.Stop();
            overlay.SetPaused(false);
            overlay.Hide();
            bool wasRest = (state == ClockState.RestRunning);
            if (sessionActive) LogSession(wasRest ? "RestCompleted" : "Completed");
            SetState(ClockState.Alarming);
            if (wasRest) FinishRest(); else Finish();
            return;
        }
        double secLeft = (double)ticksLeft / Stopwatch.Frequency;
        long totalSecs = Math.Max(0, (long)Math.Ceiling(secLeft));
        remaining = TimeSpan.FromSeconds(secLeft);
        if (totalSecs != lastSeconds)
        {
            lastSeconds = totalSecs;
            FormatAndDisplay(totalSecs);
        }
        double frac = secLeft - Math.Floor(secLeft);
        if (frac <= 0.001) frac = 1.0;
        int nextMs = (int)(frac * 1000) + 15;
        timer.Interval = Math.Max(80, Math.Min(1000, nextMs));
    }

    private string GetFormattedTime(Settings s, long total)
    {
        long h = total / 3600, m = (total / 60) % 60, sSec = total % 60;
        if (state == ClockState.RestRunning || state == ClockState.RestPaused)
        {
            string prefix = s.Language == "en" ? "Rest " : "休息 ";
            return prefix + (total / 60).ToString("00") + ":" + sSec.ToString("00");
        }
        return s.Format == "MM:SS" ? (total / 60).ToString("00") + ":" + sSec.ToString("00") :
            s.Format == "Chinese" ? (s.Language == "en" ? h.ToString("00") + "h " + m.ToString("00") + "m " + sSec.ToString("00") + "s" : h.ToString("00") + "时" + m.ToString("00") + "分" + sSec.ToString("00") + "秒") : h.ToString("00") + ":" + m.ToString("00") + ":" + sSec.ToString("00");
    }

    private void FormatAndDisplay(long total)
    {
        Settings current = previewSettings ?? settings;
        string value = GetFormattedTime(current, total);
        overlay.UpdateText(value);
    }

    private void Display(TimeSpan t)
    {
        long total = Math.Max(0, (long)Math.Ceiling(t.TotalSeconds));
        lastSeconds = total;
        FormatAndDisplay(total);
    }

    private void ResetDisplay() { remaining = TimeSpan.FromMinutes(settings.Minutes); lastSeconds = -1; Display(remaining); }

    private void Finish()
    {
        FinishCore(false);
    }

    private void FinishRest()
    {
        FinishCore(true);
    }

    private void FinishCore(bool wasRest)
    {
        overlay.SetPaused(false);
        UpdateMenuText();
        if (settings.EndNotice)
        {
            if (settings.NoticeStyle == "Pulse" || (settings.FullscreenDnd && IsForegroundFullScreen()))
            {
                if (activeNotice != null && !activeNotice.IsDisposed) { activeNotice.Close(); activeNotice = null; }
                if (settings.EndSound)
                {
                    PlaySoundStyle(settings.EndSoundStyle);
                }
                overlay.StartBreathingPulse(delegate
                {
                    ResetDisplay();
                    overlay.Hide();
                    SetState(ClockState.Idle);
                });
                return;
            }
            overlay.Hide();
            if (activeNotice != null && !activeNotice.IsDisposed)
            {
                ForceTopMost(activeNotice);
                return;
            }
            Action onRest = wasRest ? null : new Action(delegate { StartRest(5); });
            activeNotice = new NoticeDialog(settings, DateTime.Now, wasRest ? (settings.Language == "en" ? "Break" : "休息") : null, IntPtr.Zero, onRest);
            activeNotice.FormClosed += delegate { activeNotice = null; if (state == ClockState.Alarming) SetState(ClockState.Idle); };
            activeNotice.Show();
        }
        else
        {
            overlay.Hide();
            if (settings.EndSound)
            {
                PlaySoundStyle(settings.EndSoundStyle);
            }
            SetState(ClockState.Idle);
        }
    }

    private void ToggleMove()
    {
        bool enabled = !overlay.MoveMode;
        overlay.SetMoveMode(enabled, running);
        if (enabled)
        {
            preMoveState = state;
            SetState(ClockState.MoveMode);
            if (preMoveState == ClockState.Idle) ResetDisplay();
            overlay.Show();
        }
        else
        {
            SetState(preMoveState);
            if (state == ClockState.Idle) overlay.Hide();
        }
        bool en = settings.Language == "en";
        tray.ShowBalloonTip(2000, "iClock", enabled ? (en ? "Drag the translucent area to move the text. Use the tray menu to finish." : "拖动半透明区域调整文字位置，再次从托盘菜单退出调整模式。") : (en ? "Text position saved." : "文字位置已保存。"), ToolTipIcon.Info);
        UpdateMenuText();
    }
    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private const uint SWP_FLAGS = 0x0043;

    private static void ActivateWindow(Form f)
    {
        if (f == null || f.IsDisposed) return;
        if (f.WindowState == FormWindowState.Minimized) f.WindowState = FormWindowState.Normal;
        f.BringToFront();
        f.Activate();
        try
        {
            if (f.TopMost) NativeMethods.SetWindowPos(f.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_FLAGS);
            NativeMethods.SetForegroundWindow(f.Handle);
            NativeMethods.BringWindowToTop(f.Handle);
        }
        catch { }
    }

    internal static bool IsForegroundFullScreen()
    {
        try
        {
            IntPtr fg = NativeMethods.GetForegroundWindow();
            if (fg == IntPtr.Zero) return false;
            if (fg == NativeMethods.GetDesktopWindow() || fg == NativeMethods.GetShellWindow()) return false;

            StringBuilder sb = new StringBuilder(256);
            if (NativeMethods.GetClassName(fg, sb, sb.Capacity) > 0)
            {
                string cls = sb.ToString();
                if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd") return false;
            }

            RECT r;
            if (!NativeMethods.GetWindowRect(fg, out r)) return false;

            Screen scr = Screen.FromHandle(fg);
            if (scr == null) return false;

            return r.Left <= scr.Bounds.Left &&
                   r.Top <= scr.Bounds.Top &&
                   r.Right >= scr.Bounds.Right &&
                   r.Bottom >= scr.Bounds.Bottom;
        }
        catch { return false; }
    }

    private static byte[] qqWavData;
    private static byte[] pagerWavData;
    private static SoundPlayer activeSoundPlayer;

    private static byte[] GenerateQQWav()
    {
        int sampleRate = 22050;
        List<short> samples = new List<short>();
        double freq = 2150.0;
        for (int burst = 0; burst < 5; burst++)
        {
            for (int beep = 0; beep < 4; beep++)
            {
                int toneSamples = (int)(sampleRate * 0.042);
                int ramp = (int)(sampleRate * 0.005);
                for (int i = 0; i < toneSamples; i++)
                {
                    double t = (double)i / sampleRate;
                    double env = 1.0;
                    if (i < ramp) env = (double)i / ramp;
                    else if (i > toneSamples - ramp) env = (double)(toneSamples - i) / ramp;

                    double sine = Math.Sin(2.0 * Math.PI * freq * t);
                    double sq = (sine >= 0) ? 1.0 : -1.0;
                    short val = (short)((sq * 0.65 + sine * 0.35) * env * 18000.0);
                    samples.Add(val);
                }
                if (beep < 3)
                {
                    int gap = (int)(sampleRate * 0.042);
                    for (int i = 0; i < gap; i++) samples.Add(0);
                }
            }
            if (burst < 4)
            {
                int pause = (int)(sampleRate * 0.420);
                for (int i = 0; i < pause; i++) samples.Add(0);
            }
            else
            {
                int end = (int)(sampleRate * 0.100);
                for (int i = 0; i < end; i++) samples.Add(0);
            }
        }
        return BuildPcmWav(sampleRate, samples);
    }

    private static byte[] GeneratePagerWav()
    {
        int sampleRate = 22050;
        List<short> samples = new List<short>();
        double freq = 2700.0;
        for (int cycle = 0; cycle < 5; cycle++)
        {
            for (int beep = 0; beep < 3; beep++)
            {
                int toneSamples = (int)(sampleRate * 0.070);
                int ramp = (int)(sampleRate * 0.004);
                for (int i = 0; i < toneSamples; i++)
                {
                    double t = (double)i / sampleRate;
                    double env = 1.0;
                    if (i < ramp) env = (double)i / ramp;
                    else if (i > toneSamples - ramp) env = (double)(toneSamples - i) / ramp;

                    double sine = Math.Sin(2.0 * Math.PI * freq * t);
                    double sq = (sine >= 0) ? 1.0 : -1.0;
                    short val = (short)(sq * env * 16000.0);
                    samples.Add(val);
                }
                if (beep < 2)
                {
                    int gap = (int)(sampleRate * 0.045);
                    for (int i = 0; i < gap; i++) samples.Add(0);
                }
            }
            if (cycle < 4)
            {
                int pause = (int)(sampleRate * 0.400);
                for (int i = 0; i < pause; i++) samples.Add(0);
            }
            else
            {
                int end = (int)(sampleRate * 0.100);
                for (int i = 0; i < end; i++) samples.Add(0);
            }
        }
        return BuildPcmWav(sampleRate, samples);
    }

    private static byte[] BuildPcmWav(int sampleRate, List<short> samples)
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(ms))
        {
            int numSamples = samples.Count;
            int dataLen = numSamples * 2;
            int chunkSize = 36 + dataLen;
            int byteRate = sampleRate * 2;

            bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(chunkSize);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

            bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);
            bw.Write((short)1); // PCM
            bw.Write((short)1); // Mono
            bw.Write(sampleRate);
            bw.Write(byteRate);
            bw.Write((short)2); // BlockAlign
            bw.Write((short)16); // BitsPerSample

            bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            bw.Write(dataLen);
            for (int i = 0; i < numSamples; i++)
            {
                bw.Write(samples[i]);
            }
            bw.Flush();
            return ms.ToArray();
        }
    }

    internal static void StopSound()
    {
        try
        {
            if (activeSoundPlayer != null)
            {
                activeSoundPlayer.Stop();
                activeSoundPlayer.Dispose();
                activeSoundPlayer = null;
            }
        }
        catch { }
    }

    internal static void PlaySoundStyle(string style)
    {
        try
        {
            StopSound();

            if (style == "Alarm")
            {
                string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                string p = Path.Combine(winDir, "Media\\Alarm01.wav");
                if (File.Exists(p))
                {
                    activeSoundPlayer = new SoundPlayer(p);
                    activeSoundPlayer.Play();
                }
                else
                {
                    SystemSounds.Exclamation.Play();
                }
            }
            else if (style == "Pager" || style == "Classic")
            {
                if (pagerWavData == null) pagerWavData = GeneratePagerWav();
                MemoryStream ms = new MemoryStream(pagerWavData);
                activeSoundPlayer = new SoundPlayer(ms);
                activeSoundPlayer.Play();
            }
            else // "QQ" or "Chime"
            {
                if (qqWavData == null) qqWavData = GenerateQQWav();
                MemoryStream ms = new MemoryStream(qqWavData);
                activeSoundPlayer = new SoundPlayer(ms);
                activeSoundPlayer.Play();
            }
        }
        catch { }
    }

    internal static string MatchAgent(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        string lower = text.ToLowerInvariant();
        if (lower.Contains("antigravity")) return "Antigravity";
        if (lower.Contains("cursor")) return "Cursor";
        if (lower.Contains("windsurf") || lower.Contains("codeium")) return "Windsurf";
        if (lower.Contains("trae")) return "Trae";
        if (lower.Contains("marscode")) return "MarsCode";
        if (lower.Contains("lingma") || lower.Contains("tongyi")) return "通义灵码 (Lingma)";
        if (lower.Contains("codegeex") || lower.Contains("zcode")) return "CodeGeeX";
        if (lower.Contains("comate")) return "文心快码 (Comate)";
        if (lower.Contains("minimax")) return "MiniMax Code";
        if (lower.Contains("codebuddy") || lower.Contains("tencentcloud") || lower.Contains("tce-code")) return "CodeBuddy";
        if (lower.Contains("fitten")) return "Fitten Code";
        if (lower.Contains("codearts")) return "CodeArts Snap";
        if (lower.Contains("raccoon")) return "代码小浣熊";
        if (lower.Contains("copilot")) return "GitHub Copilot";
        if (lower.Contains("claude")) return "Claude Code";
        if (lower.Contains("gemini")) return "Gemini";
        if (lower.Contains("codex")) return "Codex";
        if (lower.Contains("kimi")) return "Kimi Code";
        if (lower.Contains("opencode")) return "OpenCode";
        if (lower.Contains("workbuddy") || lower.Contains("work_buddy") || lower.Contains("work-buddy")) return "WorkBuddy";
        if (lower.Contains("sheetagent")) return "SheetAgent";
        if (lower.Contains("aider")) return "Aider";
        if (lower.Contains("cline")) return "Cline";
        if (lower.Contains("roo-code") || lower.Contains("roocode") || lower.Contains("roo-cline")) return "Roo Code";
        if (lower.Contains("continue") || lower.Contains("continuedev")) return "Continue";
        if (lower.Contains("tabnine")) return "Tabnine";
        if (lower.Contains("cody") || lower.Contains("sourcegraph")) return "Cody";
        if (lower.Contains("amazonq") || lower.Contains("codewhisperer")) return "Amazon Q";
        if (lower.Contains("supermaven")) return "Supermaven";
        if (lower.Contains("devin") || lower.Contains("opendevin") || lower.Contains("all-hands")) return "Devin";
        if (lower.Contains("grokbot") || lower.Contains("grok")) return "Grok";
        if (lower == "pi" || lower.StartsWith("pi ") || lower.StartsWith("pi-") || lower.StartsWith("pi.")) return "Pi";
        return null;
    }

    internal static HashSet<string> GetRunningAgents()
    {
        HashSet<string> running = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (Process p in Process.GetProcesses())
            {
                using (p)
                {
                    string name = MatchAgent(p.ProcessName);
                    if (name != null) running.Add(name);
                }
            }
        }
        catch { }
        return running;
    }

    internal static List<string> DetectAgents()
    {
        List<string> result = new List<string>();
        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (Process p in Process.GetProcesses())
            {
                using (p)
                {
                    string name = MatchAgent(p.ProcessName);
                    if (name != null && !seen.Contains(name))
                    {
                        seen.Add(name);
                        result.Add(name);
                    }
                }
            }
        }
        catch { }

        List<string> dirsToCheck = new List<string>();
        try
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            dirsToCheck.Add(Path.Combine(local, "Programs"));
            dirsToCheck.Add(Path.Combine(local, "Microsoft", "WindowsApps"));
            dirsToCheck.Add(Path.Combine(local, "OpenAI"));
            dirsToCheck.Add(Path.Combine(local, "Programs", "@opencode-aidesktop"));

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrEmpty(appData))
            {
                dirsToCheck.Add(appData);
                dirsToCheck.Add(Path.Combine(appData, "Programs"));
            }

            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrEmpty(pf)) dirsToCheck.Add(pf);

            string pfx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (!string.IsNullOrEmpty(pfx86) && pfx86 != pf) dirsToCheck.Add(pfx86);

            string pd = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (!string.IsNullOrEmpty(pd)) dirsToCheck.Add(pd);

            string prog = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            if (!string.IsNullOrEmpty(prog)) dirsToCheck.Add(prog);

            string cprog = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
            if (!string.IsNullOrEmpty(cprog)) dirsToCheck.Add(cprog);

            string userProf = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(userProf))
            {
                dirsToCheck.Add(userProf);
                dirsToCheck.Add(Path.Combine(userProf, ".vscode", "extensions"));
                dirsToCheck.Add(Path.Combine(userProf, ".cursor", "extensions"));
                dirsToCheck.Add(Path.Combine(userProf, ".windsurf", "extensions"));
                dirsToCheck.Add(Path.Combine(userProf, ".trae", "extensions"));
                dirsToCheck.Add(Path.Combine(userProf, ".antigravity", "extensions"));
            }
        }
        catch { }

        foreach (string baseDir in dirsToCheck)
        {
            try
            {
                if (!Directory.Exists(baseDir)) continue;
                foreach (string entry in Directory.GetFileSystemEntries(baseDir))
                {
                    string fname = Path.GetFileName(entry);
                    string name = MatchAgent(fname);
                    if (name != null && !seen.Contains(name))
                    {
                        seen.Add(name);
                        result.Add(name);
                    }
                }
            }
            catch { }
        }

        return result;
    }

    internal static class ToastAgentMonitor
    {
        private static object listener;
        private static MethodInfo getNotifsMethod;
        private static MethodInfo asTaskMethod;
        private static MethodInfo cachedGenericAsTask;
        private static Type genericArg;
        private static object toastKind;
        private static uint lastSeenId = 0;
        private static bool initialized = false;
        private static bool initAttempted = false;

        internal static uint LastSeenId { get { return lastSeenId; } set { lastSeenId = value; } }

        public static bool IsAvailable
        {
            get
            {
                if (!initAttempted) Init();
                return initialized;
            }
        }

        public static bool Init()
        {
            initAttempted = true;
            try
            {
                Type listenerType = Type.GetType("Windows.UI.Notifications.Management.UserNotificationListener, Windows, ContentType=WindowsRuntime");
                if (listenerType == null) return false;

                object current = listenerType.GetProperty("Current", BindingFlags.Public | BindingFlags.Static).GetValue(null, null);
                MethodInfo getStatus = listenerType.GetMethod("GetAccessStatus");
                object status = getStatus.Invoke(current, null);
                if (status == null || status.ToString() != "Allowed") return false;

                Type kindType = Type.GetType("Windows.UI.Notifications.NotificationKinds, Windows, ContentType=WindowsRuntime");
                toastKind = Enum.Parse(kindType, "Toast");

                getNotifsMethod = listenerType.GetMethod("GetNotificationsAsync", new Type[] { kindType });
                genericArg = getNotifsMethod.ReturnType.GetGenericArguments()[0];

                Type extType = typeof(System.WindowsRuntimeSystemExtensions);
                foreach (MethodInfo m in extType.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (m.Name == "AsTask" && m.IsGenericMethod && m.GetParameters().Length == 1)
                    {
                        asTaskMethod = m;
                        break;
                    }
                }
                if (asTaskMethod == null) return false;

                try { cachedGenericAsTask = asTaskMethod.MakeGenericMethod(genericArg); } catch { }

                listener = current;
                initialized = true;

                uint maxId = 0;
                IEnumerable initialList = FetchToastNotifications();
                if (initialList != null)
                {
                    foreach (object item in initialList)
                    {
                        uint id = (uint)item.GetType().GetProperty("Id").GetValue(item, null);
                        if (id > maxId) maxId = id;
                    }
                }
                lastSeenId = maxId;
                return true;
            }
            catch
            {
                initialized = false;
                return false;
            }
        }

        public static IEnumerable FetchToastNotifications()
        {
            if (!initialized || listener == null || getNotifsMethod == null || asTaskMethod == null) return null;
            try
            {
                object asyncOp = getNotifsMethod.Invoke(listener, new object[] { toastKind });
                MethodInfo m = cachedGenericAsTask ?? asTaskMethod.MakeGenericMethod(genericArg);
                Task task = (Task)m.Invoke(null, new object[] { asyncOp });
                if (task.Wait(1000))
                {
                    return (IEnumerable)task.GetType().GetProperty("Result").GetValue(task, null);
                }
                return null;
            }
            catch { return null; }
        }

        public static void CheckNewAgentNotifications(Action<string, string> onAgentNotice)
        {
            if (!IsAvailable || onAgentNotice == null) return;
            IEnumerable list = FetchToastNotifications();
            if (list == null) return;

            uint newMaxId = lastSeenId;
            foreach (object item in list)
            {
                try
                {
                    Type itemType = item.GetType();
                    uint id = (uint)itemType.GetProperty("Id").GetValue(item, null);
                    if (id <= lastSeenId) continue;
                    if (id > newMaxId) newMaxId = id;

                    object appInfo = itemType.GetProperty("AppInfo").GetValue(item, null);
                    if (appInfo == null) continue;
                    object displayInfo = appInfo.GetType().GetProperty("DisplayInfo").GetValue(appInfo, null);
                    if (displayInfo == null) continue;
                    string appName = (string)displayInfo.GetType().GetProperty("DisplayName").GetValue(displayInfo, null);

                    string agentName = MatchAgent(appName);
                    if (agentName != null)
                    {
                        string text = "";
                        try
                        {
                            object notifObj = itemType.GetProperty("Notification").GetValue(item, null);
                            if (notifObj != null)
                            {
                                object visual = notifObj.GetType().GetProperty("Visual").GetValue(notifObj, null);
                                if (visual != null)
                                {
                                    MethodInfo getBinding = visual.GetType().GetMethod("GetBinding");
                                    object binding = getBinding.Invoke(visual, new object[] { "ToastGeneric" });
                                    if (binding != null)
                                    {
                                        MethodInfo getText = binding.GetType().GetMethod("GetTextElements");
                                        IEnumerable texts = (IEnumerable)getText.Invoke(binding, null);
                                        if (texts != null)
                                        {
                                            foreach (object t in texts)
                                            {
                                                string s = (string)t.GetType().GetProperty("Text").GetValue(t, null);
                                                if (!string.IsNullOrEmpty(s))
                                                {
                                                    text += (text.Length > 0 ? " • " : "") + s;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch { }

                        onAgentNotice(agentName, text);
                    }
                }
                catch { }
            }
            lastSeenId = newMaxId;
        }
    }

    internal const int AgentCpuBusyMinTicks = 5;       // 算力需持续活跃 >= 5 秒才认定进入后台任务（大幅提高启动门槛）
    internal const int AgentCpuIdleMinTicks = 6;       // 算力需持续平稳 >= 6 秒才触发完成提醒（大幅提高触发门槛，防工具调用间隙误报）
    internal const double AgentCpuSecsMin = 0.40;      // 集群单秒实际消耗 CPU 核心时间 >= 0.40 秒（原 0.15s）
    internal const double AgentCpuUsageMin = 0.08;     // 系统总算力占用比 >= 8%（原 3%）
    internal const int AgentNoticeCooldownSecs = 30;   // 提醒冷却周期 >= 30 秒（原 15s）

    internal class AgentTrack
    {
        public string DisplayName;
        public HashSet<int> Pids = new HashSet<int>();
        public IntPtr MainWindow;
        public TimeSpan LastCpu;
        public DateTime LastSample;
        public int BusyTicks;
        public int IdleTicks;
        public bool WasBusy;
        public DateTime LastNoticeTime;
        public string LastTitle;
        public bool TitleWasBusy;
    }

    internal static string GetAgentWindowTitle(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero || !NativeMethods.IsWindow(hWnd)) return string.Empty;
        try
        {
            int len = NativeMethods.GetWindowTextLength(hWnd);
            if (len <= 0) return string.Empty;
            StringBuilder sb = new StringBuilder(len + 1);
            NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }
        catch { return string.Empty; }
    }

    internal static bool IsBusyAgentTitle(string title)
    {
        if (string.IsNullOrEmpty(title)) return false;
        string lower = title.ToLowerInvariant();
        return lower.Contains("[running]") || lower.Contains("(running)") ||
               lower.Contains("●") || lower.Contains("thinking...") ||
               lower.Contains("generating...") || lower.Contains("[busy]") ||
               lower.Contains("(busy)") || lower.Contains("[building]") ||
               lower.Contains("(building)");
    }

    internal static IntPtr FindAgentMainWindow(HashSet<int> pids)
    {
        if (pids == null || pids.Count == 0) return IntPtr.Zero;
        IntPtr best = IntPtr.Zero;
        int maxArea = 0;
        IntPtr desk = IntPtr.Zero;
        try
        {
            desk = NativeMethods.OpenInputDesktop(0, false, 0x0100 | 0x0001);
            NativeMethods.EnumWindowsProc proc = delegate(IntPtr hWnd, IntPtr lParam)
            {
                if (!NativeMethods.IsWindowVisible(hWnd)) return true;
                uint pid;
                NativeMethods.GetWindowThreadProcessId(hWnd, out pid);
                if (pids.Contains((int)pid))
                {
                    if (NativeMethods.GetWindowTextLength(hWnd) > 0)
                    {
                        RECT r;
                        if (NativeMethods.GetWindowRect(hWnd, out r))
                        {
                            int area = (r.Right - r.Left) * (r.Bottom - r.Top);
                            if (area > maxArea)
                            {
                                maxArea = area;
                                best = hWnd;
                            }
                        }
                    }
                }
                return true;
            };

            if (desk != IntPtr.Zero)
            {
                NativeMethods.EnumDesktopWindows(desk, proc, IntPtr.Zero);
            }
            if (best == IntPtr.Zero)
            {
                NativeMethods.EnumWindows(proc, IntPtr.Zero);
            }
        }
        catch { }
        finally
        {
            if (desk != IntPtr.Zero)
            {
                try { NativeMethods.CloseDesktop(desk); } catch { }
            }
        }
        return best;
    }

    private Timer agentTimer;
    internal Dictionary<string, AgentTrack> trackedAgents = new Dictionary<string, AgentTrack>(StringComparer.OrdinalIgnoreCase);
    private int agentScanCooldown;
    private int toastScanCooldown;

    private void UpdateAgentMonitoring()
    {
        if (settings.AgentNotice)
        {
            if (agentTimer == null)
            {
                agentTimer = new Timer();
                agentTimer.Interval = 1000;
                agentTimer.Tick += AgentTimerTick;
            }
            agentTimer.Start();
        }
        else
        {
            if (agentTimer != null) agentTimer.Stop();
            trackedAgents.Clear();
        }
    }

    private void AgentTimerTick(object sender, EventArgs e)
    {
        if (!settings.AgentNotice) return;
        if (mouseHookHandle != IntPtr.Zero && Math.Abs(Environment.TickCount - lastTrayMouseMove) > 2500)
        {
            UnhookMouse();
        }
        IntPtr fg = NativeMethods.GetForegroundWindow();
        uint fgPid = 0;
        if (fg != IntPtr.Zero) NativeMethods.GetWindowThreadProcessId(fg, out fgPid);
        ProcessAgentTick(DateTime.Now, fg, fgPid);
    }

    internal void ProcessAgentTick(DateTime now, IntPtr fg, uint fgPid)
    {
        if (ToastAgentMonitor.IsAvailable)
        {
            toastScanCooldown--;
            if (toastScanCooldown <= 0)
            {
                toastScanCooldown = (trackedAgents.Count > 0) ? 3 : 10;
                ToastAgentMonitor.CheckNewAgentNotifications(delegate(string agentName, string detailText)
                {
                AgentTrack t = null;
                IntPtr targetHwnd = IntPtr.Zero;
                if (trackedAgents.TryGetValue(agentName, out t))
                {
                    if (t.MainWindow == IntPtr.Zero || !NativeMethods.IsWindow(t.MainWindow))
                    {
                        t.MainWindow = FindAgentMainWindow(t.Pids);
                    }
                    targetHwnd = t.MainWindow;
                    t.LastNoticeTime = now;
                    t.WasBusy = false;
                    t.TitleWasBusy = false;
                    t.BusyTicks = 0;
                    t.IdleTicks = 0;
                }
                else
                {
                    try
                    {
                        foreach (Process p in Process.GetProcesses())
                        {
                            using (p)
                            {
                                if (string.Equals(MatchAgent(p.ProcessName), agentName, StringComparison.OrdinalIgnoreCase))
                                {
                                    HashSet<int> pids = new HashSet<int>();
                                    pids.Add(p.Id);
                                    targetHwnd = FindAgentMainWindow(pids);
                                    break;
                                }
                            }
                        }
                    }
                    catch { }
                }

                bool isForeground = false;
                if (t != null)
                {
                    bool isMinimized = t.MainWindow != IntPtr.Zero && NativeMethods.IsIconic(t.MainWindow);
                    isForeground = !isMinimized && ((t.MainWindow != IntPtr.Zero && t.MainWindow == fg) || (t.Pids != null && t.Pids.Contains((int)fgPid)));
                }
                else if (targetHwnd != IntPtr.Zero && fg != IntPtr.Zero)
                {
                    isForeground = (targetHwnd == fg);
                }

                if (!isForeground)
                {
                    TriggerAgentNotice(agentName, targetHwnd, detailText);
                }
            });
            }
        }

        agentScanCooldown--;
        if (agentScanCooldown <= 0 || trackedAgents.Count == 0)
        {
            agentScanCooldown = (trackedAgents.Count > 0) ? 15 : 6;
            try
            {
                Dictionary<string, HashSet<int>> currentAgentPids = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
                foreach (Process p in Process.GetProcesses())
                {
                    using (p)
                    {
                        string displayName = MatchAgent(p.ProcessName);
                        if (displayName != null)
                        {
                            HashSet<int> pids;
                            if (!currentAgentPids.TryGetValue(displayName, out pids))
                            {
                                pids = new HashSet<int>();
                                currentAgentPids[displayName] = pids;
                            }
                            pids.Add(p.Id);
                        }
                    }
                }

                foreach (KeyValuePair<string, HashSet<int>> kvp in currentAgentPids)
                {
                    string name = kvp.Key;
                    HashSet<int> pids = kvp.Value;
                    AgentTrack track;
                    if (!trackedAgents.TryGetValue(name, out track))
                    {
                        TimeSpan totalCpu = TimeSpan.Zero;
                        foreach (int pid in pids)
                        {
                            try
                            {
                                using (Process p = Process.GetProcessById(pid))
                                {
                                    if (!p.HasExited) totalCpu += p.TotalProcessorTime;
                                }
                            }
                            catch { }
                        }

                        track = new AgentTrack
                        {
                            DisplayName = name,
                            Pids = pids,
                            MainWindow = FindAgentMainWindow(pids),
                            LastCpu = totalCpu,
                            LastSample = now,
                            LastNoticeTime = DateTime.MinValue
                        };
                        trackedAgents[name] = track;
                    }
                    else
                    {
                        track.Pids = pids;
                        if (track.MainWindow == IntPtr.Zero || !NativeMethods.IsWindow(track.MainWindow))
                        {
                            track.MainWindow = FindAgentMainWindow(pids);
                        }
                    }
                }

                List<string> toRemove = new List<string>();
                foreach (string name in trackedAgents.Keys)
                {
                    if (!currentAgentPids.ContainsKey(name)) toRemove.Add(name);
                }
                foreach (string name in toRemove) trackedAgents.Remove(name);
            }
            catch { }
        }

        foreach (AgentTrack track in new List<AgentTrack>(trackedAgents.Values))
        {
            try
            {
                if (track.Pids == null || track.Pids.Count == 0)
                {
                    trackedAgents.Remove(track.DisplayName);
                    continue;
                }

                if (track.MainWindow == IntPtr.Zero || !NativeMethods.IsWindow(track.MainWindow))
                {
                    track.MainWindow = FindAgentMainWindow(track.Pids);
                }

                bool isMinimized = track.MainWindow != IntPtr.Zero && NativeMethods.IsIconic(track.MainWindow);
                bool isForeground = !isMinimized && ((track.MainWindow != IntPtr.Zero && track.MainWindow == fg) || track.Pids.Contains((int)fgPid));

                string curTitle = GetAgentWindowTitle(track.MainWindow);
                bool isTitleBusy = IsBusyAgentTitle(curTitle);
                bool titleJustFinished = track.TitleWasBusy && !isTitleBusy;

                TimeSpan curCpu = TimeSpan.Zero;
                List<int> deadPids = null;
                foreach (int pid in track.Pids)
                {
                    try
                    {
                        using (Process p = Process.GetProcessById(pid))
                        {
                            if (!p.HasExited) curCpu += p.TotalProcessorTime;
                            else { if (deadPids == null) deadPids = new List<int>(); deadPids.Add(pid); }
                        }
                    }
                    catch { if (deadPids == null) deadPids = new List<int>(); deadPids.Add(pid); }
                }
                if (deadPids != null)
                {
                    foreach (int d in deadPids) track.Pids.Remove(d);
                }

                if (isForeground)
                {
                    track.WasBusy = false;
                    track.TitleWasBusy = false;
                    track.BusyTicks = 0;
                    track.IdleTicks = 0;
                    track.LastCpu = curCpu;
                    track.LastSample = now;
                    track.LastTitle = curTitle;
                    continue;
                }

                double elapsed = (now - track.LastSample).TotalSeconds;
                if (elapsed <= 0.2) continue;

                double cpuSecs = (curCpu - track.LastCpu).TotalSeconds;
                track.LastCpu = curCpu;
                track.LastSample = now;
                track.LastTitle = curTitle;
                if (cpuSecs < 0) cpuSecs = 0;

                double usage = cpuSecs / (elapsed * Environment.ProcessorCount);
                bool isCpuBusy = (usage >= AgentCpuUsageMin || cpuSecs >= AgentCpuSecsMin);

                bool isToastAgent = ToastAgentMonitor.IsAvailable && (
                    string.Equals(track.DisplayName, "Antigravity", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(track.DisplayName, "Cursor", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(track.DisplayName, "Windsurf", StringComparison.OrdinalIgnoreCase)
                );

                if (isTitleBusy)
                {
                    track.TitleWasBusy = true;
                    track.WasBusy = true;
                    track.BusyTicks++;
                    track.IdleTicks = 0;
                }
                else if (titleJustFinished)
                {
                    toastScanCooldown = 0;
                    track.TitleWasBusy = false;
                    track.WasBusy = false;
                    track.BusyTicks = 0;
                    track.IdleTicks = 0;
                    if ((now - track.LastNoticeTime).TotalSeconds >= AgentNoticeCooldownSecs)
                    {
                        track.LastNoticeTime = now;
                        TriggerAgentNotice(track.DisplayName, track.MainWindow);
                    }
                }
                else if (!isToastAgent && isCpuBusy)
                {
                    track.BusyTicks++;
                    track.IdleTicks = 0;
                    if (track.BusyTicks >= AgentCpuBusyMinTicks) track.WasBusy = true;
                }
                else if (!isToastAgent && track.WasBusy)
                {
                    track.IdleTicks++;
                    if (track.IdleTicks >= AgentCpuIdleMinTicks)
                    {
                        track.WasBusy = false;
                        track.TitleWasBusy = false;
                        track.BusyTicks = 0;
                        track.IdleTicks = 0;
                        if ((now - track.LastNoticeTime).TotalSeconds >= AgentNoticeCooldownSecs)
                        {
                            track.LastNoticeTime = now;
                            if (track.MainWindow == IntPtr.Zero || !NativeMethods.IsWindow(track.MainWindow))
                            {
                                track.MainWindow = FindAgentMainWindow(track.Pids);
                            }
                            TriggerAgentNotice(track.DisplayName, track.MainWindow);
                        }
                    }
                }
                else
                {
                    track.BusyTicks = 0;
                }
            }
            catch { }
        }
    }

    private void TriggerAgentNotice(string agentName, IntPtr targetHwnd, string detailText = null)
    {
        if (settings.NoticeStyle == "Pulse" || (settings.FullscreenDnd && IsForegroundFullScreen()))
        {
            overlay.StartBreathingPulse(delegate
            {
                ResetDisplay();
                overlay.Hide();
            });
            if (settings.EndSound)
            {
                PlaySoundStyle(settings.EndSoundStyle);
            }
        }
        else
        {
            if (activeNotice != null && !activeNotice.IsDisposed)
            {
                activeNotice.Close();
                activeNotice = null;
            }
            activeNotice = new NoticeDialog(settings, DateTime.Now, agentName, targetHwnd, null, detailText);
            activeNotice.FormClosed += delegate { activeNotice = null; };
            activeNotice.Show();
        }
    }

    internal void HandleShellHook(int code, IntPtr targetHwnd)
    {
        if (targetHwnd == IntPtr.Zero || !settings.AgentNotice) return;
        if (code != HSHELL_FLASH) return;

        try
        {
            IntPtr fg = NativeMethods.GetForegroundWindow();
            if (fg != IntPtr.Zero && fg == targetHwnd) return;

            uint pid = 0;
            NativeMethods.GetWindowThreadProcessId(targetHwnd, out pid);

            string agentName = null;
            foreach (KeyValuePair<string, AgentTrack> kvp in trackedAgents)
            {
                if (kvp.Value.MainWindow == targetHwnd || (pid != 0 && kvp.Value.Pids.Contains((int)pid)))
                {
                    agentName = kvp.Key;
                    break;
                }
            }

            if (agentName == null && pid != 0)
            {
                try
                {
                    using (Process p = Process.GetProcessById((int)pid))
                    {
                        agentName = MatchAgent(p.ProcessName);
                    }
                }
                catch { }
            }

            if (agentName == null)
            {
                string title = GetAgentWindowTitle(targetHwnd);
                agentName = MatchAgent(title);
            }

            if (agentName != null)
            {
                DateTime now = DateTime.Now;
                AgentTrack track;
                if (trackedAgents.TryGetValue(agentName, out track))
                {
                    if ((now - track.LastNoticeTime).TotalSeconds < AgentNoticeCooldownSecs) return;
                    track.LastNoticeTime = now;
                    track.WasBusy = false;
                    track.TitleWasBusy = false;
                    track.BusyTicks = 0;
                    track.IdleTicks = 0;
                    if (track.MainWindow == IntPtr.Zero || !NativeMethods.IsWindow(track.MainWindow))
                    {
                        track.MainWindow = targetHwnd;
                    }
                }
                else
                {
                    track = new AgentTrack
                    {
                        DisplayName = agentName,
                        MainWindow = targetHwnd,
                        LastNoticeTime = now,
                        LastSample = now
                    };
                    if (pid != 0) track.Pids.Add((int)pid);
                    trackedAgents[agentName] = track;
                }

                TriggerAgentNotice(agentName, targetHwnd, null);
            }
        }
        catch { }
    }

    private Settings previewSettings;

    private void SetPreviewSettings(Settings preview)
    {
        previewSettings = preview;
        if (preview != null)
        {
            long total = running
                ? (lastSeconds >= 0 ? lastSeconds : (long)Math.Ceiling(remaining.TotalSeconds))
                : (sessionActive ? (long)Math.Ceiling(remaining.TotalSeconds) : (preview.Minutes * 60));
            string value = GetFormattedTime(preview, total);
            overlay.SetVisualPreview(preview, value);
        }
        else
        {
            long total = (running || sessionActive)
                ? (lastSeconds >= 0 ? lastSeconds : (long)Math.Ceiling(remaining.TotalSeconds))
                : (settings.Minutes * 60);
            string value = GetFormattedTime(settings, total);
            overlay.SetVisualPreview(settings, value);
        }
    }

    private void ExecuteTestAlert(Settings draft, bool isAgentTab)
    {
        if (isAgentTab)
        {
            if (draft.NoticeStyle == "Pulse")
            {
                if (draft.EndSound) PlaySoundStyle(draft.EndSoundStyle);
                overlay.StartBreathingPulse(null);
            }
            else
            {
                string demoAgent = "Cursor";
                List<string> agents = DetectAgents();
                if (agents != null && agents.Count > 0) demoAgent = agents[0];
                string demoDetail = draft.Language == "en"
                    ? "Demo: Task finished in 45s (3 files changed)"
                    : "演示：耗时 45 秒完成长任务（修改 3 个文件）";
                if (activeNotice != null && !activeNotice.IsDisposed) { activeNotice.Close(); activeNotice = null; }
                activeNotice = new NoticeDialog(draft, DateTime.Now, demoAgent, activeDialog != null ? activeDialog.Handle : IntPtr.Zero, null, demoDetail);
                activeNotice.Show();
            }
        }
        else
        {
            if (!draft.EndSound && !draft.EndNotice)
            {
                bool en = draft.Language == "en";
                MessageBox.Show(activeDialog,
                    en ? "Please check 'Play sound at end' or 'End notification' first to test."
                       : "请先勾选「结束时声音提醒」或「结束时系统通知」以测试效果。",
                    "iClock", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (draft.NoticeStyle == "Pulse" || !draft.EndNotice)
            {
                if (draft.EndSound) PlaySoundStyle(draft.EndSoundStyle);
                if (draft.EndNotice) overlay.StartBreathingPulse(null);
            }
            else
            {
                if (activeNotice != null && !activeNotice.IsDisposed) { activeNotice.Close(); activeNotice = null; }
                activeNotice = new NoticeDialog(draft, DateTime.Now, null, IntPtr.Zero, delegate { StartRest(5); });
                activeNotice.Show();
            }
        }
    }

    private void ShowSettings()
    {
        if (activeDialog != null && !activeDialog.IsDisposed)
        {
            if (activeDialog is SettingsDialog) { ActivateWindow(activeDialog); return; }
            activeDialog.Close();
        }

        bool wasOverlayVisible = overlay.Visible;
        if (!wasOverlayVisible)
        {
            overlay.Show();
        }

        SettingsDialog d = new SettingsDialog(settings, hotkeyRegistered);
        activeDialog = d;

        SetPreviewSettings(d.GetCurrentDraftSettings());

        d.PreviewRequested += delegate(Settings preview)
        {
            SetPreviewSettings(preview);
        };

        d.TestAlertRequested += delegate(Settings draft, bool isAgentTab)
        {
            ExecuteTestAlert(draft, isAgentTab);
        };

        d.FormClosed += delegate
        {
            SetPreviewSettings(null);
            if (d.DialogResult == DialogResult.OK)
            {
                ApplySavedSettings(d.Value);
            }
            else
            {
                if (!wasOverlayVisible)
                {
                    overlay.Hide();
                }
            }
            if (activeDialog == d) activeDialog = null;
            TrimMemory();
        };
        d.Show();
        ActivateWindow(d);
    }

    private void ApplySavedSettings(Settings newSettings)
    {
        bool hotkeyFailed = false;
        if ((newSettings.HotkeyKey == (int)Keys.Space && (newSettings.HotkeyModifiers & 3) != 0) || !SetHotkey(newSettings.HotkeyModifiers, newSettings.HotkeyKey))
        {
            newSettings.HotkeyModifiers = activeHotkeyModifiers; newSettings.HotkeyKey = activeHotkeyKey;
            hotkeyFailed = true;
        }
        settings = newSettings; settings.Save(); overlay.SetSettings(settings); ApplyStartup();
        if (tray != null && tray.ContextMenuStrip != null)
        {
            tray.ContextMenuStrip.Font = GetUiFont(settings.Language, 9.5f);
            if (menuStart != null) menuStart.Font = new Font(tray.ContextMenuStrip.Font, FontStyle.Bold);
        }
        UpdateMenuText();
        UpdateAgentMonitoring();
        lastSeconds = -1;
        if (!running && !sessionActive) { ResetDisplay(); overlay.SetPaused(false); overlay.Hide(); }
        else if (running) { Tick(null, EventArgs.Empty); }
        else { overlay.SetPaused(true); Display(remaining); }

        if (hotkeyFailed)
        {
            bool en = settings.Language == "en";
            MessageBox.Show(en ? "That hotkey conflicts with system input or is occupied. The previous hotkey was kept, but other settings were saved." : "该快捷键已被占用或与系统输入法冲突，快捷键已保留为原设置，其他设置已成功保存。", "iClock", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
    private bool SetHotkey(int modifiers, int key)
    {
        bool hadHotkey = hotkeyRegistered;
        int oldModifiers = activeHotkeyModifiers, oldKey = activeHotkeyKey;
        if (hadHotkey) UnregisterHotKey(messageWindow.Handle, HOTKEY_ID);
        bool ok = RegisterHotKey(messageWindow.Handle, HOTKEY_ID, (uint)(modifiers | MOD_NOREPEAT), (uint)key);
        if (ok) { activeHotkeyModifiers = modifiers; activeHotkeyKey = key; hotkeyRegistered = true; return true; }
        hotkeyRegistered = false;
        if (hadHotkey && RegisterHotKey(messageWindow.Handle, HOTKEY_ID, (uint)(oldModifiers | MOD_NOREPEAT), (uint)oldKey)) hotkeyRegistered = true;
        return false;
    }
    private struct StatSpan
    {
        public string Text;
        public Font Font;
        public Color Color;
        public StatSpan(string text, Font font, Color color) { Text = text; Font = font; Color = color; }
    }
    private static bool ParseSessionRow(string row, out string start, out string end, out string dur, out long s, out string res, out bool isDone)
    {
        start = ""; end = ""; dur = ""; s = 0; res = ""; isDone = false;
        if (string.IsNullOrEmpty(row)) return false;
        string[] cols = row.Split('\t'); if (cols.Length < 4) return false;
        start = cols[0]; end = cols[1]; dur = cols[2];
        if (cols.Length >= 5) { long.TryParse(cols[3], out s); res = cols[4]; }
        else
        {
            res = cols[3]; int m; int.TryParse(dur, out m);
            if (res == "Completed" || res == "已完成") s = m * 60L;
            else
            {
                DateTime t1, t2;
                if (DateTime.TryParse(start, out t1) && DateTime.TryParse(end, out t2))
                    s = Math.Max(0L, Math.Min(m * 60L, (long)(t2 - t1).TotalSeconds));
            }
        }
        isDone = res == "Completed" || res == "已完成";
        return true;
    }

    private static void GetDayStats(string dir, string dateKey, out int total, out int completed, out long actualSecs)
    {
        total = 0; completed = 0; actualSecs = 0;
        string path = Path.Combine(dir, "history-" + dateKey + ".tsv");
        if (!File.Exists(path)) return;
        try
        {
            foreach (string row in File.ReadAllLines(path, Encoding.UTF8))
            {
                string start, end, dur, res; long s; bool isDone;
                if (ParseSessionRow(row, out start, out end, out dur, out s, out res, out isDone))
                {
                    total++; if (isDone) completed++; actualSecs += s;
                }
            }
        }
        catch { }
    }

    private static List<string> GetAvailableHistoryDates(string dir)
    {
        List<string> dates = new List<string>();
        for (int i = 0; i < 7; i++) dates.Add(DateTime.Today.AddDays(-i).ToString("yyyyMMdd"));
        try
        {
            if (Directory.Exists(dir))
            {
                foreach (string file in Directory.GetFiles(dir, "history-*.tsv"))
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    if (name.StartsWith("history-") && name.Length == 16)
                    {
                        string k = name.Substring(8);
                        if (!dates.Contains(k)) dates.Add(k);
                    }
                }
            }
        }
        catch { }
        dates.Sort();
        dates.Reverse();
        return dates;
    }

    private void ShowHistory()
    {
        if (activeDialog != null && !activeDialog.IsDisposed)
        {
            if (activeDialog.Tag as string == "history") { ActivateWindow(activeDialog); return; }
            activeDialog.Close();
        }
        bool en = settings.Language == "en";
        string historyDir = Path.GetDirectoryName(Settings.FilePath);
        Form f = new Form();
        f.Tag = "history";
        activeDialog = f;
        f.Font = GetUiFont(settings.Language, 9.5f);
        f.BackColor = Win11Bg;
        f.Text = en ? "iClock - Countdown History & Statistics" : "iClock - 专注历史记录与统计";
        f.StartPosition = FormStartPosition.CenterScreen;
        f.ClientSize = new Size(680, 440);
        f.MinimumSize = new Size(620, 380);
        f.MinimizeBox = false; f.MaximizeBox = false;
        f.ShowInTaskbar = false;
        f.TopMost = true;
        ApplyModernWindowStyle(f);
        f.Shown += delegate { ActivateWindow(f); };

        Font headerFont = new Font(f.Font, FontStyle.Bold);
        Font boldFont = new Font(f.Font, FontStyle.Bold);
        ToolTip tip = new ToolTip();
        f.FormClosed += delegate
        {
            headerFont.Dispose();
            boldFont.Dispose();
            tip.Dispose();
            if (activeDialog == f) activeDialog = null;
        };
        f.KeyPreview = true;

        int currentViewMode = 0; // 0: Day View, 1: Overview View
        StatSpan[] currentSpans = new StatSpan[0];

        Button btnTabDay = new Button();
        btnTabDay.Text = en ? "Daily Details" : "单日明细";
        btnTabDay.SetBounds(12, 10, en ? 100 : 84, 28);
        btnTabDay.FlatStyle = FlatStyle.Flat;
        btnTabDay.FlatAppearance.BorderSize = 0;
        btnTabDay.Font = boldFont;
        btnTabDay.Cursor = Cursors.Hand;
        btnTabDay.MouseEnter += delegate { if (currentViewMode != 0) btnTabDay.BackColor = Color.FromArgb(228, 232, 238); };
        btnTabDay.MouseLeave += delegate { if (currentViewMode != 0) btnTabDay.BackColor = Color.FromArgb(240, 242, 245); };

        Button btnTabOverview = new Button();
        btnTabOverview.Text = en ? "Days Summary" : "按天汇总";
        btnTabOverview.SetBounds(btnTabDay.Right + 6, 10, en ? 105 : 84, 28);
        btnTabOverview.FlatStyle = FlatStyle.Flat;
        btnTabOverview.FlatAppearance.BorderSize = 0;
        btnTabOverview.Font = boldFont;
        btnTabOverview.Cursor = Cursors.Hand;
        btnTabOverview.MouseEnter += delegate { if (currentViewMode != 1) btnTabOverview.BackColor = Color.FromArgb(228, 232, 238); };
        btnTabOverview.MouseLeave += delegate { if (currentViewMode != 1) btnTabOverview.BackColor = Color.FromArgb(240, 242, 245); };

        Button btnNext = new Button();
        btnNext.Text = "▶";
        btnNext.Size = new Size(28, 28);
        btnNext.Location = new Point(f.ClientSize.Width - 12 - btnNext.Width, 10);
        btnNext.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnNext.FlatStyle = FlatStyle.Flat;
        btnNext.BackColor = Color.FromArgb(240, 242, 245);
        btnNext.ForeColor = Win11TextPrimary;
        btnNext.FlatAppearance.BorderColor = Color.FromArgb(215, 220, 228);
        btnNext.Cursor = Cursors.Hand;
        tip.SetToolTip(btnNext, en ? "Next Day (Right Arrow)" : "后一天（支持 → 快捷键）");
        btnNext.MouseEnter += delegate { if (btnNext.Enabled) btnNext.BackColor = Color.FromArgb(228, 232, 238); };
        btnNext.MouseLeave += delegate { if (btnNext.Enabled) btnNext.BackColor = Color.FromArgb(240, 242, 245); };

        ComboBox cboDate = new ComboBox();
        cboDate.DropDownStyle = ComboBoxStyle.DropDownList;
        cboDate.Width = en ? 180 : 165;
        cboDate.Font = f.Font;
        cboDate.Location = new Point(btnNext.Left - cboDate.Width - 4, 10);
        cboDate.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        tip.SetToolTip(cboDate, en ? "Select history date" : "选择查看历史日期");

        Button btnPrev = new Button();
        btnPrev.Text = "◀";
        btnPrev.Size = new Size(28, 28);
        btnPrev.Location = new Point(cboDate.Left - 32, 10);
        btnPrev.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnPrev.FlatStyle = FlatStyle.Flat;
        btnPrev.BackColor = Color.FromArgb(240, 242, 245);
        btnPrev.ForeColor = Win11TextPrimary;
        btnPrev.FlatAppearance.BorderColor = Color.FromArgb(215, 220, 228);
        btnPrev.Cursor = Cursors.Hand;
        tip.SetToolTip(btnPrev, en ? "Previous Day (Left Arrow)" : "前一天（支持 ← 快捷键）");
        btnPrev.MouseEnter += delegate { if (btnPrev.Enabled) btnPrev.BackColor = Color.FromArgb(228, 232, 238); };
        btnPrev.MouseLeave += delegate { if (btnPrev.Enabled) btnPrev.BackColor = Color.FromArgb(240, 242, 245); };

        List<string> availDates = GetAvailableHistoryDates(historyDir);
        string todayStr = DateTime.Today.ToString("yyyyMMdd");
        string yesterdayStr = DateTime.Today.AddDays(-1).ToString("yyyyMMdd");
        foreach (string dStr in availDates)
        {
            DateTime dt;
            DateTime.TryParseExact(dStr, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt);
            string dFmt = dt.ToString("yyyy-MM-dd");
            string label = (dStr == todayStr) ? dFmt + (en ? " (Today)" : " (今天)") :
                           (dStr == yesterdayStr) ? dFmt + (en ? " (Yesterday)" : " (昨天)") :
                           dFmt + " (" + dt.ToString("ddd", en ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo("zh-CN")) + ")";
            cboDate.Items.Add(label);
        }
        if (cboDate.Items.Count > 0) cboDate.SelectedIndex = 0;

        ComboBox cboRange = new ComboBox();
        cboRange.DropDownStyle = ComboBoxStyle.DropDownList;
        cboRange.Width = en ? 140 : 120;
        cboRange.Font = f.Font;
        cboRange.Location = new Point(f.ClientSize.Width - 12 - cboRange.Width, 10);
        cboRange.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        cboRange.Items.AddRange(en ? new object[] { "Last 7 Days", "Last 14 Days", "Last 30 Days", "All History" }
                                   : new object[] { "最近 7 天", "最近 14 天", "最近 30 天", "全部历史" });
        cboRange.SelectedIndex = 0;
        tip.SetToolTip(cboRange, en ? "Select statistics time range" : "选择统计时间范围");

        ListView list = new ListView();
        list.Font = f.Font;
        list.BackColor = Color.White;
        list.ForeColor = Win11TextPrimary;
        list.BorderStyle = BorderStyle.FixedSingle;
        list.View = View.Details; list.FullRowSelect = true; list.GridLines = true;
        list.SetBounds(12, 48, 656, 334);
        list.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        list.OwnerDraw = true;

        list.Columns.Add("", 50);
        list.Columns.Add("", 115);
        list.Columns.Add("", 115);
        list.Columns.Add("", 85);
        list.Columns.Add("", 95);
        list.Columns.Add("", 120);

        MethodInvoker updateColumnWidths = delegate
        {
            int w = list.ClientSize.Width;
            if (w <= 0 || list.Columns.Count < 6) return;
            list.Columns[5].Width = 10;
            if (currentViewMode == 0)
            {
                int c0 = 45, c1 = 110, c2 = 110, c3 = 85, c4 = 95;
                list.Columns[0].Width = c0; list.Columns[1].Width = c1; list.Columns[2].Width = c2;
                list.Columns[3].Width = c3; list.Columns[4].Width = c4;
                list.Columns[5].Width = Math.Max(90, w - c0 - c1 - c2 - c3 - c4);
            }
            else
            {
                int c0 = 105, c1 = 60, c2 = 140, c3 = 90, c4 = 70;
                list.Columns[0].Width = c0; list.Columns[1].Width = c1; list.Columns[2].Width = c2;
                list.Columns[3].Width = c3; list.Columns[4].Width = c4;
                list.Columns[5].Width = Math.Max(90, w - c0 - c1 - c2 - c3 - c4);
            }
        };
        list.Resize += delegate { updateColumnWidths(); };

        list.DrawColumnHeader += delegate(object s, DrawListViewColumnHeaderEventArgs e)
        {
            using (SolidBrush b = new SolidBrush(Color.FromArgb(243, 244, 246))) e.Graphics.FillRectangle(b, e.Bounds);
            using (Pen p = new Pen(Color.FromArgb(226, 230, 236)))
            {
                e.Graphics.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
                e.Graphics.DrawLine(p, e.Bounds.Right - 1, e.Bounds.Top + 3, e.Bounds.Right - 1, e.Bounds.Bottom - 4);
            }
            bool centerCol0 = (currentViewMode == 0 && e.ColumnIndex == 0);
            TextFormatFlags flags = centerCol0
                ? (TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine)
                : (TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.WordEllipsis);
            Rectangle textRect = centerCol0 ? e.Bounds : new Rectangle(e.Bounds.X + 8, e.Bounds.Y, Math.Max(0, e.Bounds.Width - 16), e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, e.Header.Text, headerFont, textRect, centerCol0 ? Win11ShortcutGray : Color.FromArgb(60, 60, 60), flags);
        };
        list.DrawItem += delegate(object s, DrawListViewItemEventArgs e) { };
        list.DrawSubItem += delegate(object s, DrawListViewSubItemEventArgs e)
        {
            bool isSel = (e.ItemState & ListViewItemStates.Selected) != 0;
            if (currentViewMode == 0 && e.ColumnIndex == 0)
            {
                e.DrawBackground();
                Color fg = isSel ? SystemColors.HighlightText : Win11ShortcutGray;
                TextRenderer.DrawText(e.Graphics, e.SubItem.Text, e.Item.Font, e.Bounds, fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
            else if (!isSel && e.ColumnIndex == 5)
            {
                e.DrawBackground();
                string txt = e.SubItem.Text;
                Color fg = Win11TextPrimary;
                if (txt.Contains("卓越") || txt.Contains("Excellent") || txt == "已完成" || txt == "Completed")
                    fg = Color.FromArgb(16, 124, 65);
                else if (txt.Contains("良好") || txt.Contains("Good"))
                    fg = Win11Accent;
                else if (txt.Contains("及格") || txt.Contains("Fair") || txt == "已中断" || txt == "Interrupted")
                    fg = Color.FromArgb(202, 80, 16);
                else if (txt.Contains("待提升") || txt.Contains("Needs work") || txt == "已重置" || txt == "Reset" || txt == "-")
                    fg = Win11ShortcutGray;
                Rectangle r = new Rectangle(e.Bounds.X + 8, e.Bounds.Y, Math.Max(0, e.Bounds.Width - 16), e.Bounds.Height);
                TextRenderer.DrawText(e.Graphics, txt, e.Item.Font, r, fg, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
            else e.DrawDefault = true;
        };

        Panel summaryPanel = new Panel();
        summaryPanel.SetBounds(12, 390, 656, 36);
        summaryPanel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        summaryPanel.BackColor = Color.FromArgb(243, 244, 246);
        summaryPanel.Paint += delegate(object s, PaintEventArgs e)
        {
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(243, 244, 246))) e.Graphics.FillRectangle(bg, summaryPanel.ClientRectangle);
            using (Pen p = new Pen(Color.FromArgb(226, 230, 236), 1f)) e.Graphics.DrawRectangle(p, 0, 0, summaryPanel.Width - 1, summaryPanel.Height - 1);
            if (currentSpans == null || currentSpans.Length == 0) return;
            TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            int totalW = 0;
            int[] widths = new int[currentSpans.Length];
            for (int i = 0; i < currentSpans.Length; i++)
            {
                Size sz = TextRenderer.MeasureText(e.Graphics, currentSpans[i].Text, currentSpans[i].Font, Size.Empty, flags);
                widths[i] = sz.Width; totalW += sz.Width;
            }
            int curX = Math.Max(8, (summaryPanel.Width - totalW) / 2);
            int y = (summaryPanel.Height - f.Font.Height) / 2;
            for (int i = 0; i < currentSpans.Length; i++)
            {
                TextRenderer.DrawText(e.Graphics, currentSpans[i].Text, currentSpans[i].Font, new Point(curX, y), currentSpans[i].Color, flags);
                curX += widths[i];
            }
        };
        summaryPanel.Resize += delegate { summaryPanel.Invalidate(); };

        MethodInvoker refreshData = delegate
        {
            list.BeginUpdate();
            list.Items.Clear();

            if (currentViewMode == 0)
            {
                list.Columns[0].TextAlign = HorizontalAlignment.Center;
                list.Columns[0].Text = "#";
                list.Columns[1].Text = en ? "Start time" : "开始时间";
                list.Columns[2].Text = en ? "End time" : "结束时间";
                list.Columns[3].Text = en ? "Duration" : "设定时长";
                list.Columns[4].Text = en ? "Actual duration" : "实际时长";
                list.Columns[5].Text = en ? "Result" : "结果";

                string selText = cboDate.SelectedItem != null ? cboDate.SelectedItem.ToString() : "";
                string selKey = selText.Length >= 10 ? selText.Substring(0, 10).Replace("-", "") : todayStr;
                string selDisplay = selText.Length >= 10 ? selText.Substring(0, 10) : DateTime.Today.ToString("yyyy-MM-dd");

                btnPrev.Enabled = (cboDate.SelectedIndex < cboDate.Items.Count - 1);
                btnNext.Enabled = (cboDate.SelectedIndex > 0);
                btnPrev.ForeColor = btnPrev.Enabled ? Win11TextPrimary : Win11ShortcutGray;
                btnNext.ForeColor = btnNext.Enabled ? Win11TextPrimary : Win11ShortcutGray;

                string path = Path.Combine(historyDir, "history-" + selKey + ".tsv");
                int totalSessions = 0, completedSessions = 0;
                long totalActualSecs = 0;

                if (File.Exists(path))
                {
                    try
                    {
                        foreach (string row in File.ReadAllLines(path, Encoding.UTF8))
                        {
                            string start, end, dur, res; long s; bool isDone;
                            if (ParseSessionRow(row, out start, out end, out dur, out s, out res, out isDone))
                            {
                                totalSessions++; if (isDone) completedSessions++; totalActualSecs += s;
                                res = en ? (isDone ? "Completed" : (res == "Reset" || res == "已重置" ? "Reset" : (res == "Interrupted" || res == "已中断" ? "Interrupted" : res)))
                                         : (isDone ? "已完成" : (res == "Reset" ? "已重置" : (res == "Interrupted" ? "已中断" : res)));
                                list.Items.Add(new ListViewItem(new string[] { totalSessions.ToString(), start, end, dur + (en ? " min" : " 分钟"), FormatDuration(s, en), res }));
                            }
                        }
                    }
                    catch { }
                }

                if (totalSessions == 0)
                {
                    list.Items.Add(new ListViewItem(new string[] { "-", en ? "No countdown records for this day" : "该日期暂无倒计时记录", "", "", "", "" }));
                    currentSpans = new StatSpan[]
                    {
                        new StatSpan(en ? "⏱️ " + selDisplay + " Focus Summary: " : "⏱️ " + selDisplay + " 累计专注汇总：", f.Font, Win11TextPrimary),
                        new StatSpan(en ? "0s" : "0 秒", boldFont, Win11ShortcutGray),
                        new StatSpan(en ? " (No records)" : "（暂无倒计时记录）", f.Font, Win11TextSecondary)
                    };
                }
                else
                {
                    int rate = (int)Math.Round((double)completedSessions * 100.0 / totalSessions);
                    Color rateColor = (rate >= 80) ? Color.FromArgb(16, 124, 65) : Win11Accent;
                    currentSpans = new StatSpan[]
                    {
                        new StatSpan(en ? "⏱️ " + selDisplay + " Focus: " : "⏱️ " + selDisplay + " 累计专注：", f.Font, Win11TextPrimary),
                        new StatSpan(FormatDuration(totalActualSecs, en), boldFont, Win11Accent),
                        new StatSpan(en ? "   |   Completed " : "   ｜   完成 ", f.Font, Win11TextSecondary),
                        new StatSpan(completedSessions + "/" + totalSessions + (en ? " sessions" : " 个周期"), boldFont, Win11TextPrimary),
                        new StatSpan(en ? "   |   Rate " : "   ｜   完成率 ", f.Font, Win11TextSecondary),
                        new StatSpan(rate + "%", boldFont, rateColor)
                    };
                }
            }
            else
            {
                list.Columns[0].TextAlign = HorizontalAlignment.Left;
                list.Columns[0].Text = en ? "Date" : "日期";
                list.Columns[1].Text = en ? "Day" : "星期";
                list.Columns[2].Text = en ? "Total focus" : "专注总时长";
                list.Columns[3].Text = en ? "Completed" : "完成/总数";
                list.Columns[4].Text = en ? "Rate" : "完成率";
                list.Columns[5].Text = en ? "Evaluation" : "达成评价";

                int rangeDays = cboRange.SelectedIndex == 0 ? 7 : (cboRange.SelectedIndex == 1 ? 14 : (cboRange.SelectedIndex == 2 ? 30 : 0));
                List<string> dateKeys = new List<string>();
                if (rangeDays > 0)
                {
                    for (int i = 0; i < rangeDays; i++) dateKeys.Add(DateTime.Today.AddDays(-i).ToString("yyyyMMdd"));
                }
                else dateKeys = GetAvailableHistoryDates(historyDir);

                int activeDays = 0, totalCycles = 0, totalDone = 0;
                long allActualSecs = 0;

                foreach (string dk in dateKeys)
                {
                    DateTime dt;
                    DateTime.TryParseExact(dk, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt);
                    int tot, done; long secs;
                    GetDayStats(historyDir, dk, out tot, out done, out secs);
                    string dStr = dt.ToString("yyyy-MM-dd");
                    string weekday = dt.ToString("ddd", en ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo("zh-CN"));

                    if (tot > 0)
                    {
                        activeDays++; totalCycles += tot; totalDone += done; allActualSecs += secs;
                        int r = (int)Math.Round((double)done * 100.0 / tot);
                        string eval = r >= 90 ? (en ? "● Excellent" : "● 卓越") : (r >= 70 ? (en ? "● Good" : "● 良好") : (r >= 50 ? (en ? "● Fair" : "● 及格") : (en ? "● Needs work" : "● 待提升")));
                        ListViewItem lvi = new ListViewItem(new string[] { dStr, weekday, FormatDurationMinutes(secs, en), done + " / " + tot, r + "%", eval });
                        lvi.Tag = dStr;
                        list.Items.Add(lvi);
                    }
                    else
                    {
                        ListViewItem lvi = new ListViewItem(new string[] { dStr, weekday, en ? "0 min" : "0 分钟", "-", "-", "-" });
                        lvi.Tag = dStr;
                        lvi.ForeColor = Win11ShortcutGray;
                        list.Items.Add(lvi);
                    }
                }

                int overallRate = totalCycles == 0 ? 0 : (int)Math.Round((double)totalDone * 100.0 / totalCycles);
                Color rateColor = (overallRate >= 80) ? Color.FromArgb(16, 124, 65) : Win11Accent;
                int totalDaysCount = dateKeys.Count;

                if (totalCycles == 0)
                {
                    currentSpans = new StatSpan[]
                    {
                        new StatSpan(en ? "📊 Summary (" + totalDaysCount + " days): No countdown sessions" : "📊 统计周期：" + totalDaysCount + " 天 ｜ 暂无专注记录 ｜ 💡 双击行查看明细", f.Font, Win11TextSecondary)
                    };
                }
                else
                {
                    currentSpans = new StatSpan[]
                    {
                        new StatSpan(en ? "📊 " + totalDaysCount + "-day Focus: " : "📊 " + totalDaysCount + " 天累计专注：", f.Font, Win11TextPrimary),
                        new StatSpan(FormatDurationMinutes(allActualSecs, en), boldFont, Win11Accent),
                        new StatSpan(en ? "   |   Active " : "   ｜   活跃 ", f.Font, Win11TextSecondary),
                        new StatSpan(activeDays + "/" + totalDaysCount + (en ? " days" : " 天"), boldFont, Win11TextPrimary),
                        new StatSpan(en ? "   |   Sessions " : "   ｜   周期 ", f.Font, Win11TextSecondary),
                        new StatSpan(totalDone + "/" + totalCycles + " (" + overallRate + "%)", boldFont, rateColor),
                        new StatSpan(en ? "   |   💡 Double-click row for details" : "   ｜   💡 双击行查看明细", f.Font, Win11ShortcutGray)
                    };
                }
            }

            list.EndUpdate();
            updateColumnWidths();
            summaryPanel.Invalidate();
        };

        Action<int> switchView = delegate(int mode)
        {
            currentViewMode = mode;
            bool isDay = currentViewMode == 0;
            btnTabDay.BackColor = isDay ? Win11Accent : Color.FromArgb(235, 238, 242);
            btnTabDay.ForeColor = isDay ? Color.White : Win11TextPrimary;
            btnTabOverview.BackColor = isDay ? Color.FromArgb(235, 238, 242) : Win11Accent;
            btnTabOverview.ForeColor = isDay ? Win11TextPrimary : Color.White;

            btnPrev.Visible = isDay;
            cboDate.Visible = isDay;
            btnNext.Visible = isDay;
            cboRange.Visible = !isDay;

            refreshData();
        };

        btnTabDay.Click += delegate { switchView(0); };
        btnTabOverview.Click += delegate { switchView(1); };

        cboDate.SelectedIndexChanged += delegate { if (currentViewMode == 0) refreshData(); };
        btnPrev.Click += delegate { if (cboDate.SelectedIndex < cboDate.Items.Count - 1) cboDate.SelectedIndex++; };
        btnNext.Click += delegate { if (cboDate.SelectedIndex > 0) cboDate.SelectedIndex--; };
        cboRange.SelectedIndexChanged += delegate { if (currentViewMode == 1) refreshData(); };

        list.DoubleClick += delegate
        {
            if (currentViewMode == 1 && list.SelectedItems.Count > 0)
            {
                string targetDate = list.SelectedItems[0].Tag as string;
                if (!string.IsNullOrEmpty(targetDate))
                {
                    for (int i = 0; i < cboDate.Items.Count; i++)
                    {
                        if (cboDate.Items[i].ToString().StartsWith(targetDate))
                        {
                            cboDate.SelectedIndex = i;
                            break;
                        }
                    }
                    switchView(0);
                }
            }
        };

        f.Controls.Add(btnTabDay);
        f.Controls.Add(btnTabOverview);
        f.Controls.Add(btnPrev);
        f.Controls.Add(cboDate);
        f.Controls.Add(btnNext);
        f.Controls.Add(cboRange);
        f.Controls.Add(list);
        f.Controls.Add(summaryPanel);
        f.KeyDown += delegate(object s, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) f.Close();
            else if (e.KeyCode == Keys.Left && currentViewMode == 0)
            {
                if (cboDate.SelectedIndex < cboDate.Items.Count - 1) cboDate.SelectedIndex++;
            }
            else if (e.KeyCode == Keys.Right && currentViewMode == 0)
            {
                if (cboDate.SelectedIndex > 0) cboDate.SelectedIndex--;
            }
            else if (e.Control && e.KeyCode == Keys.Tab)
            {
                switchView(currentViewMode == 0 ? 1 : 0);
            }
            else if (e.Control && e.KeyCode == Keys.C && list.SelectedItems.Count > 0)
            {
                try
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (ListViewItem lvi in list.SelectedItems)
                    {
                        List<string> parts = new List<string>();
                        foreach (ListViewItem.ListViewSubItem sub in lvi.SubItems) parts.Add(sub.Text);
                        sb.AppendLine(string.Join("\t", parts.ToArray()));
                    }
                    Clipboard.SetText(sb.ToString());
                }
                catch { }
            }
        };

        switchView(0);
        CenterFormOnScreen(f);
        f.FormClosed += delegate { TrimMemory(); };
        f.Show();
        ActivateWindow(f);
    }

    internal void ShowCliHelp()
    {
        if (activeDialog != null && !activeDialog.IsDisposed)
        {
            if (activeDialog is CliHelpDialog) { ActivateWindow(activeDialog); return; }
            activeDialog.Close();
        }
        CliHelpDialog d = new CliHelpDialog(settings);
        activeDialog = d;
        d.FormClosed += delegate
        {
            if (activeDialog == d) activeDialog = null;
            TrimMemory();
        };
        d.Show();
        ActivateWindow(d);
    }
    private void ShowAbout()
    {
        if (activeDialog != null && !activeDialog.IsDisposed)
        {
            if (activeDialog is AboutDialog) { ActivateWindow(activeDialog); return; }
            activeDialog.Close();
        }
        AboutDialog d = new AboutDialog(settings.Language, this);
        activeDialog = d;
        d.FormClosed += delegate
        {
            if (activeDialog == d) activeDialog = null;
            TrimMemory();
        };
        d.Show();
        ActivateWindow(d);
    }
    private void LogSession(string result)
    {
        try
        {
            long actualSecs = (result == "Completed")
                ? sessionMinutes * 60L
                : Math.Max(0L, Math.Min(sessionMinutes * 60L, (long)(TimeSpan.FromMinutes(sessionMinutes) - remaining).TotalSeconds));
            string dir = Path.GetDirectoryName(Settings.FilePath); Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "history-" + sessionStart.ToString("yyyyMMdd") + ".tsv");
            string row = sessionStart.ToString("HH:mm:ss") + "\t" + DateTime.Now.ToString("HH:mm:ss") + "\t" + sessionMinutes + "\t" + actualSecs + "\t" + result + Environment.NewLine;
            File.AppendAllText(path, row, new UTF8Encoding(false));
        }
        catch { }
    }
    private void ApplyStartup()
    {
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", true))
            {
                if (k == null) return;
                if (settings.AutoStart) k.SetValue("iClock", "\"" + exePath + "\""); else k.DeleteValue("iClock", false);
            }
        }
        catch { }
    }
    private void Exit()
    {
        if (activeNotice != null && !activeNotice.IsDisposed) { activeNotice.Close(); activeNotice = null; }
        if (activeDialog != null && !activeDialog.IsDisposed) { activeDialog.Close(); activeDialog = null; }
        if (running) remaining = ReadRemaining();
        if (sessionActive) { LogSession("Interrupted"); sessionActive = false; }
        timer.Stop(); tray.Visible = false; if (hotkeyRegistered) UnregisterHotKey(messageWindow.Handle, HOTKEY_ID);
        if (mouseHookHandle != IntPtr.Zero) { try { NativeMethods.UnhookWindowsHookEx(mouseHookHandle); } catch { } mouseHookHandle = IntPtr.Zero; }
        if (displaySettingsHandler != null) { try { SystemEvents.DisplaySettingsChanged -= displaySettingsHandler; } catch { } displaySettingsHandler = null; }
        if (sessionSwitchHandler != null) { try { SystemEvents.SessionSwitch -= sessionSwitchHandler; } catch { } sessionSwitchHandler = null; }
        if (agentTimer != null) { try { agentTimer.Stop(); agentTimer.Dispose(); } catch { } agentTimer = null; }
        if (shellHookMsg != 0 && messageWindow != null && messageWindow.Handle != IntPtr.Zero) { try { NativeMethods.DeregisterShellHookWindow(messageWindow.Handle); } catch { } }
        overlay.Close(); overlay.Dispose(); messageWindow.DestroyHandle(); tray.Dispose(); appIcon.Dispose(); timer.Dispose(); ExitThread();
    }
    internal static string FormatDuration(long secs, bool en)
    {
        if (secs < 60) return secs + (en ? "s" : " 秒");
        long m = secs / 60;
        long s = secs % 60;
        if (m < 60)
        {
            if (s == 0) return m + (en ? " min" : " 分钟");
            return en ? m + "m " + s + "s" : m + " 分 " + s + " 秒";
        }
        long h = m / 60;
        m = m % 60;
        if (s == 0 && m == 0) return h + (en ? "h" : " 小时");
        if (s == 0) return en ? h + "h " + m + "m" : h + " 小时 " + m + " 分钟";
        return en ? h + "h " + m + "m " + s + "s" : h + " 小时 " + m + " 分 " + s + " 秒";
    }
    internal static string FormatDurationMinutes(long secs, bool en)
    {
        if (secs <= 0) return en ? "0 min" : "0 分钟";
        if (secs < 60) return en ? "< 1 min" : "< 1 分钟";
        long m = (secs + 30) / 60;
        if (m < 60) return m + (en ? " min" : " 分钟");
        long h = m / 60;
        m = m % 60;
        if (m == 0) return h + (en ? "h" : " 小时");
        return en ? h + "h " + m + "m" : h + " 小时 " + m + " 分钟";
    }
    public void HandleHotkey() { Toggle(); }
    internal static string HotkeyText(int mods, int key)
    {
        string value = "";
        if ((mods & 2) != 0) value += "Ctrl+";
        if ((mods & 1) != 0) value += "Alt+";
        if ((mods & 4) != 0) value += "Shift+";
        return value + ((Keys)key).ToString();
    }

    internal static bool ProbeHotkey(int modifiers, int key)
    {
        NativeWindow nw = new NativeWindow();
        try
        {
            nw.CreateHandle(new CreateParams());
            int testId = 0x55AA;
            bool ok = RegisterHotKey(nw.Handle, testId, (uint)(modifiers | MOD_NOREPEAT), (uint)key);
            if (ok) UnregisterHotKey(nw.Handle, testId);
            return ok;
        }
        catch { return false; }
        finally
        {
            try { nw.DestroyHandle(); } catch { }
        }
    }

    internal static Font GetUiFont(string lang, float sizePt = 9.5f, FontStyle style = FontStyle.Regular)
    {
        string family = (lang == "en") ? "Segoe UI" : "Microsoft YaHei UI";
        try
        {
            return new Font(family, sizePt, style, GraphicsUnit.Point);
        }
        catch
        {
            try { return new Font("Microsoft YaHei", sizePt, style, GraphicsUnit.Point); }
            catch { return SystemFonts.MessageBoxFont; }
        }
    }

    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    internal static readonly Color Win11Bg = Color.FromArgb(250, 250, 250);
    internal static readonly Color Win11Accent = Color.FromArgb(0, 103, 192);
    internal static readonly Color Win11TextPrimary = Color.FromArgb(31, 31, 31);
    internal static readonly Color Win11TextSecondary = Color.FromArgb(95, 95, 95);
    internal static readonly Color Win11ShortcutGray = Color.FromArgb(130, 130, 130);
    internal static readonly Color Win11Border = Color.FromArgb(228, 228, 228);

    internal static void ApplyModernWindowStyle(Control ctrl)
    {
        if (ctrl == null) return;
        try
        {
            int preference = DWMWCP_ROUND;
            NativeMethods.DwmSetWindowAttribute(ctrl.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
        }
        catch { }
    }

    internal static void CenterFormOnScreen(Form f)
    {
        if (f == null) return;
        Screen scr = Screen.FromPoint(Cursor.Position) ?? Screen.PrimaryScreen;
        Rectangle wa = scr.WorkingArea;
        f.StartPosition = FormStartPosition.Manual;
        f.Location = new Point(wa.Left + Math.Max(0, (wa.Width - f.Width) / 2), wa.Top + Math.Max(0, (wa.Height - f.Height) / 2));
    }

    internal static void StylePrimaryButton(Button btn)
    {
        if (btn == null) return;
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 0;
        btn.BackColor = Win11Accent;
        btn.ForeColor = Color.White;
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(25, 117, 197);
        btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(0, 90, 168);
        btn.Cursor = Cursors.Hand;
    }

    internal static void StyleSecondaryButton(Button btn)
    {
        if (btn == null) return;
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderColor = Color.FromArgb(209, 209, 209);
        btn.FlatAppearance.BorderSize = 1;
        btn.BackColor = Color.White;
        btn.ForeColor = Win11TextPrimary;
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(245, 245, 245);
        btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(235, 235, 235);
        btn.Cursor = Cursors.Hand;
    }

    internal static void ForceTopMost(Form f)
    {
        if (f == null || f.IsDisposed) return;
        try
        {
            f.TopMost = true;
            f.BringToFront();
            f.Activate();
            NativeMethods.SetWindowPos(f.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0043);
            NativeMethods.SetForegroundWindow(f.Handle);
            NativeMethods.BringWindowToTop(f.Handle);
        }
        catch { }
    }

    internal static void DrawLogoVector(Graphics g, float x, float y, float sz)
    {
        using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(19, 28, 67)))
        using (SolidBrush whiteBrush = new SolidBrush(Color.White))
        using (Pen slashPen = new Pen(Color.FromArgb(255, 87, 56), Math.Max(1.8f, sz * 0.16f)))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float radius = Math.Max(2f, sz * 0.22f);
            using (GraphicsPath path = new GraphicsPath())
            {
                float bx = x + 0.5f, by = y + 0.5f, w = sz - 1f, sqH = sz - 1f;
                path.AddArc(bx, by, radius * 2, radius * 2, 180, 90);
                path.AddArc(bx + w - radius * 2, by, radius * 2, radius * 2, 270, 90);
                path.AddArc(bx + w - radius * 2, by + sqH - radius * 2, radius * 2, radius * 2, 0, 90);
                path.AddArc(bx, by + sqH - radius * 2, radius * 2, radius * 2, 90, 90);
                path.CloseFigure();
                g.FillPath(bgBrush, path);
            }

            float dotR = Math.Max(1.0f, sz * 0.10f);
            float dotCx = x + sz * 0.44f;
            float dotCy = y + sz * 0.30f;
            g.FillEllipse(whiteBrush, dotCx - dotR, dotCy - dotR, dotR * 2, dotR * 2);

            float stemW = Math.Max(1.5f, sz * 0.18f);
            float stemH = Math.Max(3f, sz * 0.36f);
            float stemX = dotCx - stemW / 2.0f;
            float stemY = y + sz * 0.46f;
            using (GraphicsPath stemPath = new GraphicsPath())
            {
                float r = Math.Max(0.5f, stemW * 0.3f);
                stemPath.AddArc(stemX, stemY, r * 2, r * 2, 180, 90);
                stemPath.AddArc(stemX + stemW - r * 2, stemY, r * 2, r * 2, 270, 90);
                stemPath.AddArc(stemX + stemW - r * 2, stemY + stemH - r * 2, r * 2, r * 2, 0, 90);
                stemPath.AddArc(stemX, stemY + stemH - r * 2, r * 2, r * 2, 90, 90);
                stemPath.CloseFigure();
                g.FillPath(whiteBrush, stemPath);
            }

            slashPen.StartCap = LineCap.Round;
            slashPen.EndCap = LineCap.Round;
            g.DrawLine(slashPen, x + sz * 0.40f, y + sz * 0.62f, x + sz * 0.82f, y + sz * 0.26f);
        }
    }

    internal static GraphicsPath GetRoundedRectPath(Rectangle rect, int radius)
    {
        GraphicsPath path = new GraphicsPath();
        int d = radius * 2;
        if (d > rect.Width) d = rect.Width;
        if (d > rect.Height) d = rect.Height;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    internal static bool IsInEphemeralFolder(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        try
        {
            string full = Path.GetFullPath(path);
            string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd('\\', '/');
            if (full.StartsWith(temp + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(full, temp, StringComparison.OrdinalIgnoreCase))
                return true;

            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(userProfile))
            {
                string downloads = Path.Combine(userProfile, "Downloads");
                if (full.StartsWith(downloads + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(full, downloads, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            string lower = full.ToLowerInvariant();
            if (lower.Contains(@"\appdata\local\temp\") || lower.Contains(@"\downloads\"))
                return true;
        }
        catch { }
        return false;
    }

    internal static bool EnsureStartMenuShortcut(bool createIfNotExists, string customLinkPath = null, string targetExe = null)
    {
        try
        {
            string linkPath = customLinkPath;
            if (string.IsNullOrEmpty(linkPath))
            {
                string programsDir = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                if (string.IsNullOrEmpty(programsDir)) return false;
                linkPath = Path.Combine(programsDir, "iClock.lnk");
            }
            bool exists = File.Exists(linkPath);
            if (!exists && !createIfNotExists) return false;

            if (string.IsNullOrEmpty(targetExe)) targetExe = Application.ExecutablePath;

            string dir = Path.GetDirectoryName(linkPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return false;
            object shell = Activator.CreateInstance(shellType);
            object sc = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { linkPath });
            if (sc == null) return false;
            Type scType = sc.GetType();

            if (exists)
            {
                string currentTarget = scType.InvokeMember("TargetPath", BindingFlags.GetProperty, null, sc, null) as string;
                if (string.Equals(currentTarget, targetExe, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            scType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { targetExe });
            scType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { Path.GetDirectoryName(targetExe) });
            scType.InvokeMember("Description", BindingFlags.SetProperty, null, sc, new object[] { "iClock Desktop Floating Countdown Timer" });
            scType.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
            return true;
        }
        catch { return false; }
    }

    internal static Icon CreateIcon()
    {
        Size targetSize = SystemInformation.SmallIconSize;
        try
        {
            using (Stream s = typeof(AppContext).Assembly.GetManifestResourceStream("app.ico"))
            {
                if (s != null) return new Icon(s, targetSize.Width, targetSize.Height);
            }
        }
        catch { }
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates = new string[]
            {
                Path.Combine(baseDir, "src\\app.ico"),
                Path.Combine(baseDir, "app.ico"),
                Path.Combine(baseDir, "..\\src\\app.ico"),
                Path.Combine(baseDir, "..\\src\\icons\\app.ico"),
                Path.Combine(baseDir, "icons\\app.ico")
            };
            foreach (string c in candidates)
            {
                if (File.Exists(c))
                {
                    return new Icon(c, targetSize.Width, targetSize.Height);
                }
            }
        }
        catch { }
        try
        {
            Icon exeIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (exeIcon != null) return new Icon(exeIcon, targetSize.Width, targetSize.Height);
        }
        catch { }
        int sz = Math.Max(16, targetSize.Width);
        Bitmap b = new Bitmap(sz, sz, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(b))
        {
            g.Clear(Color.Transparent);
            DrawLogoVector(g, 0, 0, sz);
        }
        IntPtr h = b.GetHicon(); Icon icon = (Icon)Icon.FromHandle(h).Clone(); NativeMethods.DestroyIcon(h); b.Dispose(); return icon;
    }

    private static bool launchReported;
    private void TrackAndCheckUpdates()
    {
        if (launchReported) return;
        launchReported = true;
        System.Threading.ThreadPool.QueueUserWorkItem(delegate
        {
            string latest, url;
            if (QueryLatestVersion("launch", out latest, out url))
            {
                if (IsNewer(latest, CURRENT_VERSION))
                {
                    string foundVer = latest;
                    string targetUrl = url;
                    try
                    {
                        if (overlay != null && overlay.IsHandleCreated)
                        {
                            overlay.BeginInvoke(new MethodInvoker(delegate { OnUpdateFound(foundVer, targetUrl); }));
                        }
                    }
                    catch { }
                }
            }
        });
    }

    internal static bool QueryLatestVersion(string eventType, out string latest, out string url)
    {
        latest = null;
        url = null;
        try
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
            byte[] data = Encoding.UTF8.GetBytes("{\"event\":\"" + (eventType ?? "launch") + "\",\"page\":\"main\",\"ua\":\"iClock Desktop v" + CURRENT_VERSION + "\"}");
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create("https://green-scene-5a6d.francodeyvison183.workers.dev/track");
            req.Method = "POST";
            req.ContentType = "application/json";
            req.ContentLength = data.Length;
            req.Timeout = 5000;
            req.ReadWriteTimeout = 5000;
            using (Stream s = req.GetRequestStream()) s.Write(data, 0, data.Length);
            using (WebResponse resp = req.GetResponse())
            using (StreamReader r = new StreamReader(resp.GetResponseStream()))
            {
                string body = r.ReadToEnd();
                latest = ExtractJsonValue(body, "latest");
                url = ExtractJsonValue(body, "url");
            }
        }
        catch { }

        if (string.IsNullOrEmpty(latest))
        {
            try
            {
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create("https://api.github.com/repos/francodeyvison183-collab/iClock/releases/latest");
                req.Method = "GET";
                req.UserAgent = "iClock-Desktop-v" + CURRENT_VERSION;
                req.Timeout = 5000;
                req.ReadWriteTimeout = 5000;
                using (WebResponse resp = req.GetResponse())
                using (StreamReader r = new StreamReader(resp.GetResponseStream()))
                {
                    string body = r.ReadToEnd();
                    latest = ExtractJsonValue(body, "tag_name");
                    url = ExtractJsonValue(body, "html_url");
                }
            }
            catch { }
        }
        return !string.IsNullOrEmpty(latest);
    }

    internal void NotifyUpdateFound(string newVersion, string url)
    {
        OnUpdateFound(newVersion, url);
    }

    private void OnUpdateFound(string newVersion, string url)
    {
        if (string.IsNullOrEmpty(url)) url = "https://github.com/francodeyvison183-collab/iClock/releases/latest";
        latestVersion = newVersion;
        updateUrl = url;
        bool en = settings.Language == "en";
        string title = en ? "iClock Update Available" : "iClock 发现新版本";
        string text = en ? "New version " + newVersion + " is available. Click to download." : "发现新版本 " + newVersion + "，点击前往下载更新。";

        try { tray.ShowBalloonTip(6000, title, text, ToolTipIcon.Info); } catch { }

        if (menuUpdate == null && tray.ContextMenuStrip != null)
        {
            menuUpdate = new ToolStripMenuItem(en ? "⭐ Update available (" + newVersion + ")…" : "⭐ 发现新版本 (" + newVersion + ")…");
            menuUpdate.ForeColor = Win11Accent;
            menuUpdate.Padding = new Padding(32, 7, 32, 7);
            menuUpdate.Font = new Font(tray.ContextMenuStrip.Font, FontStyle.Bold);
            menuUpdate.Click += delegate
            {
                try { Process.Start(updateUrl); } catch { }
            };
            tray.ContextMenuStrip.Items.Insert(0, menuUpdate);
            tray.ContextMenuStrip.Items.Insert(1, new ToolStripSeparator());
        }
    }

    internal static bool IsNewer(string latest, string current)
    {
        try
        {
            Version v1 = ParseVer(latest);
            Version v2 = ParseVer(current);
            return v1 > v2;
        }
        catch { return false; }
    }

    private static Version ParseVer(string s)
    {
        if (string.IsNullOrEmpty(s)) return new Version(0, 0, 0);
        s = s.TrimStart('v', 'V').Trim();
        string[] parts = s.Split('.');
        int major = parts.Length > 0 ? int.Parse(parts[0]) : 0;
        int minor = parts.Length > 1 ? int.Parse(parts[1]) : 0;
        int build = parts.Length > 2 ? int.Parse(parts[2]) : 0;
        return new Version(major, minor, build);
    }

    internal static string ExtractJsonValue(string json, string key)
    {
        if (string.IsNullOrEmpty(json)) return null;
        int k = json.IndexOf("\"" + key + "\"", StringComparison.OrdinalIgnoreCase);
        if (k < 0) return null;
        int colon = json.IndexOf(':', k + key.Length + 2);
        if (colon < 0) return null;
        int q1 = json.IndexOf('"', colon + 1);
        if (q1 < 0) return null;
        int q2 = json.IndexOf('"', q1 + 1);
        if (q2 < 0) return null;
        return json.Substring(q1 + 1, q2 - q1 - 1);
    }

    private sealed class MessageWindow : NativeWindow
    {
        private AppContext owner;
        public MessageWindow(AppContext a)
        {
            owner = a;
            CreateParams cp = new CreateParams();
            cp.Caption = "iClock.IpcWindow";
            CreateHandle(cp);
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY) owner.HandleHotkey();
            else if (owner.shellHookMsg != 0 && (uint)m.Msg == owner.shellHookMsg)
            {
                owner.HandleShellHook(m.WParam.ToInt32(), m.LParam);
            }
            else if (m.Msg == 0x004A) // WM_COPYDATA
            {
                try
                {
                    NativeMethods.COPYDATASTRUCT cds = (NativeMethods.COPYDATASTRUCT)Marshal.PtrToStructure(m.LParam, typeof(NativeMethods.COPYDATASTRUCT));
                    if (cds.lpData != IntPtr.Zero && cds.cbData > 0)
                    {
                        string cmd = Marshal.PtrToStringUni(cds.lpData);
                        if (!string.IsNullOrEmpty(cmd))
                        {
                            owner.HandleIpcCommand(cmd);
                        }
                    }
                    m.Result = (IntPtr)1;
                    return;
                }
                catch { }
            }
            base.WndProc(ref m);
        }
    }

    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}


internal static class Program
{
    private static void RestoreConsolePrompt()
    {
        try
        {
            bool needClose = false;
            IntPtr hIn = NativeMethods.GetStdHandle(-10); // STD_INPUT_HANDLE = -10
            if (hIn == IntPtr.Zero || hIn == (IntPtr)(-1))
            {
                hIn = NativeMethods.CreateFile("CONIN$", 0xC0000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
                needClose = (hIn != IntPtr.Zero && hIn != (IntPtr)(-1));
            }
            if (hIn != IntPtr.Zero && hIn != (IntPtr)(-1))
            {
                NativeMethods.INPUT_RECORD[] records = new NativeMethods.INPUT_RECORD[2];

                records[0].EventType = 1; // KEY_EVENT
                records[0].KeyEvent.bKeyDown = true;
                records[0].KeyEvent.wRepeatCount = 1;
                records[0].KeyEvent.wVirtualKeyCode = 0x0D; // VK_RETURN
                records[0].KeyEvent.wVirtualScanCode = 0x1C;
                records[0].KeyEvent.UnicodeChar = '\r';

                records[1].EventType = 1;
                records[1].KeyEvent.bKeyDown = false;
                records[1].KeyEvent.wRepeatCount = 1;
                records[1].KeyEvent.wVirtualKeyCode = 0x0D;
                records[1].KeyEvent.wVirtualScanCode = 0x1C;
                records[1].KeyEvent.UnicodeChar = '\r';

                uint written;
                NativeMethods.WriteConsoleInput(hIn, records, 2, out written);
                if (needClose) NativeMethods.CloseHandle(hIn);
            }
        }
        catch { }
    }

    private static void WriteToConsole(string text)
    {
        bool attached = false;
        IntPtr stdout = NativeMethods.GetStdHandle(-11);
        if (stdout == IntPtr.Zero || stdout == (IntPtr)(-1))
        {
            if (NativeMethods.AttachConsole(-1))
            {
                attached = true;
                stdout = NativeMethods.GetStdHandle(-11);
            }
        }
        else
        {
            if (NativeMethods.AttachConsole(-1)) attached = true;
        }

        if (stdout != IntPtr.Zero && stdout != (IntPtr)(-1))
        {
            try
            {
                using (FileStream fs = new FileStream(new Microsoft.Win32.SafeHandles.SafeFileHandle(stdout, false), FileAccess.Write))
                using (StreamWriter sw = new StreamWriter(fs, Console.OutputEncoding ?? Encoding.Default))
                {
                    sw.WriteLine(text);
                    sw.Flush();
                }
            }
            catch { }
        }
        if (attached)
        {
            RestoreConsolePrompt();
            NativeMethods.FreeConsole();
        }
    }

    internal static bool IsCliConfiguredInPath()
    {
        try
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
            string userPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
            string[] paths = userPath.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string p in paths)
            {
                if (string.Equals(p.Trim().TrimEnd('\\', '/'), dir, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch { }
        return false;
    }

    internal static bool ConfigureCliPath(out string message)
    {
        try
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
            string userPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
            string[] paths = userPath.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            bool found = false;
            foreach (string p in paths)
            {
                if (string.Equals(p.Trim().TrimEnd('\\', '/'), dir, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                string newPath = string.IsNullOrEmpty(userPath) ? dir : (userPath.TrimEnd(';') + ";" + dir);
                Environment.SetEnvironmentVariable("PATH", newPath, EnvironmentVariableTarget.User);
                message = "CLI environment configured successfully.";
                return true;
            }
            message = "CLI path is already configured in User PATH.";
            return true;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return false;
        }
    }

    private static void InstallCli()
    {
        string dir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
        string msg;
        if (ConfigureCliPath(out msg))
        {
            if (msg.StartsWith("CLI path is already", StringComparison.OrdinalIgnoreCase))
                WriteToConsole("[iClock] CLI path is already configured in User PATH:\r\n  " + dir);
            else
                WriteToConsole("[iClock] CLI environment configured successfully!\r\n[iClock] You can now type 'iclock' directly in any terminal.");
        }
        else
        {
            WriteToConsole("[iClock] Failed to configure PATH: " + msg);
        }
    }

    private static void PrintHelp()
    {
        string help = 
@"iClock - Desktop Focus Clock & CLI Pipeline

Usage:
  iclock                          Start iClock GUI (if not running)
  iclock [options]                Send command to running iClock instance

Options:
  -s, --start [mins]              Start countdown timer (default: settings minutes)
  -p, --pause                     Pause countdown timer
  -r, --resume                    Resume countdown timer
  -t, --toggle                    Toggle between pause and resume
  --reset                         Reset countdown timer to idle
  --rest [mins]                   Start break timer (default: 5 minutes)
  -n, --notice <message>          Display popup notification and sound
  --install-cli                   Add iClock directory to User PATH
  -h, --help                      Show this help information

Examples:
  iclock --start 25               Start a 25-minute Pomodoro timer
  iclock -s 45                    Start a 45-minute countdown
  iclock -p                       Pause current timer
  iclock -r                       Resume paused timer
  iclock -n ""Build finished!""     Trigger completion notice from script";
        WriteToConsole(help);
    }

    [STAThread]
    private static void Main(string[] args)
    {
        if (args != null && args.Length > 0)
        {
            string first = args[0].ToLowerInvariant();
            if (first == "-h" || first == "--help" || first == "/?" || first == "help")
            {
                PrintHelp();
                return;
            }
            if (first == "--install-cli" || first == "install-cli")
            {
                InstallCli();
                return;
            }
        }

        bool created;
        using (System.Threading.Mutex m = new System.Threading.Mutex(true, "iClock.SingleInstance", out created))
        {
            if (!created)
            {
                if (args != null && args.Length > 0)
                {
                    IntPtr hWnd = NativeMethods.FindWindow(null, "iClock.IpcWindow");
                    if (hWnd != IntPtr.Zero)
                    {
                        string cmd = string.Join(" ", args);
                        byte[] bytes = Encoding.Unicode.GetBytes(cmd + "\0");
                        IntPtr ptr = Marshal.AllocHGlobal(bytes.Length);
                        try
                        {
                            Marshal.Copy(bytes, 0, ptr, bytes.Length);
                            NativeMethods.COPYDATASTRUCT cds;
                            cds.dwData = IntPtr.Zero;
                            cds.cbData = bytes.Length;
                            cds.lpData = ptr;
                            NativeMethods.SendMessage(hWnd, 0x004A, IntPtr.Zero, ref cds);
                            WriteToConsole("[iClock] Command sent: " + cmd);
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(ptr);
                        }
                    }
                    else
                    {
                        WriteToConsole("[iClock] Error: Running instance IPC window not found.");
                    }
                }
                else
                {
                    WriteToConsole("[iClock] Already running in background tray.");
                }
                return;
            }

            if (args != null && args.Length > 0)
            {
                WriteToConsole("[iClock] Started in background with: " + string.Join(" ", args));
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new AppContext(args));
        }
    }
}
