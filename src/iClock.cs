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
using System.Web.Script.Serialization;

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
    public bool DailyQuote = true;
    public int QuoteDuration = 10;
    public int QuoteX = -99999;
    public int QuoteY = -99999;
    public int QuoteWidth = -1;
    public float QuoteFontSize = 18.0f;
    public string QuoteColor = "";
    public string QuoteFont = "";
    public bool QuoteDesktopVisible = true;
    public bool QuotePinTopMost;
    public string LastQuoteDate = "";
    public string AiAgentType = "Auto";
    public string AiAgentCustomCmd = "";
    public string AiQuoteKeywords = "";

    public void CopyFrom(Settings src)
    {
        if (src == null) return;
        Minutes = src.Minutes; FontSize = src.FontSize; X = src.X; Y = src.Y;
        HotkeyModifiers = src.HotkeyModifiers; HotkeyKey = src.HotkeyKey;
        Color = src.Color; FontFamily = src.FontFamily; Format = src.Format;
        EndMessage = src.EndMessage; Language = src.Language; AutoStart = src.AutoStart;
        EndSound = src.EndSound; EndSoundStyle = src.EndSoundStyle; EndNotice = src.EndNotice;
        FirstRun = src.FirstRun; NoticeStyle = src.NoticeStyle; FullscreenDnd = src.FullscreenDnd;
        AgentNotice = src.AgentNotice; DailyQuote = src.DailyQuote; QuoteDuration = src.QuoteDuration;
        QuoteX = src.QuoteX; QuoteY = src.QuoteY; QuoteWidth = src.QuoteWidth;
        QuoteFontSize = src.QuoteFontSize; QuoteColor = src.QuoteColor; QuoteFont = src.QuoteFont;
        QuoteDesktopVisible = src.QuoteDesktopVisible; QuotePinTopMost = src.QuotePinTopMost;
        LastQuoteDate = src.LastQuoteDate;
        AiAgentType = src.AiAgentType; AiAgentCustomCmd = src.AiAgentCustomCmd;
        AiQuoteKeywords = src.AiQuoteKeywords;
    }

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

    public static string QuotesFilePath
    {
        get
        {
            try
            {
                string localTxt = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "quotes.txt");
                if (File.Exists(localTxt)) return localTxt;
            }
            catch { }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "iClock", "quotes.txt");
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
                float f;
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
                else if (k == "EndSoundStyle" && (v == "QQ" || v == "Phone" || v == "Pager" || v == "Alarm" || v == "Chime" || v == "Classic")) s.EndSoundStyle = (v == "Chime") ? "QQ" : ((v == "Classic" || v == "Pager") ? "Phone" : v);
                else if (k == "EndNotice" && bool.TryParse(v, out b)) s.EndNotice = b;
                else if (k == "FirstRun" && bool.TryParse(v, out b)) s.FirstRun = b;
                else if (k == "NoticeStyle" && (v == "Popup" || v == "Pulse")) s.NoticeStyle = v;
                else if (k == "FullscreenDnd" && bool.TryParse(v, out b)) s.FullscreenDnd = b;
                else if (k == "AgentNotice" && bool.TryParse(v, out b)) s.AgentNotice = b;
                else if (k == "DailyQuote" && bool.TryParse(v, out b)) s.DailyQuote = b;
                else if (k == "QuoteDuration" && int.TryParse(v, out n)) s.QuoteDuration = Math.Max(3, Math.Min(60, n));
                else if (k == "QuoteX" && int.TryParse(v, out n)) s.QuoteX = n;
                else if (k == "QuoteY" && int.TryParse(v, out n)) s.QuoteY = n;
                else if (k == "QuoteWidth" && int.TryParse(v, out n)) s.QuoteWidth = n;
                else if (k == "QuoteFontSize" && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out f)) s.QuoteFontSize = f > 0 ? Math.Max(8.0f, Math.Min(24.0f, f)) : 18.0f;
                else if (k == "QuoteColor") s.QuoteColor = v;
                else if (k == "QuoteFont") s.QuoteFont = v;
                else if (k == "QuoteDesktopVisible" && bool.TryParse(v, out b)) s.QuoteDesktopVisible = b;
                else if (k == "QuotePinTopMost" && bool.TryParse(v, out b)) s.QuotePinTopMost = b;
                else if (k == "LastQuoteDate") s.LastQuoteDate = v;
                else if (k == "AiAgentType") s.AiAgentType = v;
                else if (k == "AiAgentCustomCmd") s.AiAgentCustomCmd = v;
                else if (k == "AiQuoteKeywords") s.AiQuoteKeywords = v;
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
            "EndSound=" + EndSound, "EndSoundStyle=" + EndSoundStyle, "EndNotice=" + EndNotice, "FirstRun=" + FirstRun, "NoticeStyle=" + NoticeStyle, "FullscreenDnd=" + FullscreenDnd, "AgentNotice=" + AgentNotice,
            "DailyQuote=" + DailyQuote, "QuoteDuration=" + QuoteDuration, "QuoteX=" + QuoteX, "QuoteY=" + QuoteY, "QuoteWidth=" + QuoteWidth, "QuoteFontSize=" + (QuoteFontSize > 0 ? QuoteFontSize.ToString("0.0", CultureInfo.InvariantCulture) : "18.0"),
            "QuoteColor=" + QuoteColor, "QuoteFont=" + QuoteFont, "QuoteDesktopVisible=" + QuoteDesktopVisible, "QuotePinTopMost=" + QuotePinTopMost, "LastQuoteDate=" + LastQuoteDate,
            "AiAgentType=" + AiAgentType,
            "AiAgentCustomCmd=" + AiAgentCustomCmd,
            "AiQuoteKeywords=" + AiQuoteKeywords
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

internal sealed class DoubleBufferedPanel : Panel
{
    public DoubleBufferedPanel()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        UpdateStyles();
    }
}

internal sealed class SlimScrollBar : Control
{
    private int maximum = 100;
    private int largeChange = 10;
    private int val = 0;
    private bool isHovered = false;
    private bool isDragging = false;
    private int dragStartY = 0;
    private int dragStartVal = 0;

    public event EventHandler ValueChanged;

    public int Maximum
    {
        get { return maximum; }
        set { maximum = Math.Max(0, value); Invalidate(); }
    }

    public int LargeChange
    {
        get { return largeChange; }
        set { largeChange = Math.Max(1, value); Invalidate(); }
    }

    public int Value
    {
        get { return val; }
        set
        {
            int maxVal = Math.Max(0, maximum - largeChange);
            int clamped = Math.Max(0, Math.Min(maxVal, value));
            if (this.val != clamped)
            {
                this.val = clamped;
                Invalidate();
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }
    }

    public SlimScrollBar()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Color.FromArgb(246, 248, 250);
        Cursor = Cursors.Default;
    }

    private Rectangle GetThumbRect()
    {
        int maxVal = maximum - largeChange;
        if (maxVal <= 0 || Height <= 0) return Rectangle.Empty;
        int trackH = Height;
        int thumbH = Math.Max(24, (int)((float)largeChange / maximum * trackH));
        int travel = trackH - thumbH;
        int thumbY = (int)((float)val / maxVal * travel);
        return new Rectangle(1, thumbY, Math.Max(2, Width - 2), thumbH);
    }

    protected override void OnPaint(PaintEventArgs pe)
    {
        pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle thumb = GetThumbRect();
        if (thumb.IsEmpty) return;
        Color c = isDragging ? Color.FromArgb(115, 122, 133) : (isHovered ? Color.FromArgb(156, 163, 175) : Color.FromArgb(200, 205, 212));
        using (Brush b = new SolidBrush(c))
        using (GraphicsPath gp = AppContext.GetRoundedRectPath(thumb, 2))
        {
            pe.Graphics.FillPath(b, gp);
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        isHovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        isHovered = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            Rectangle thumb = GetThumbRect();
            if (thumb.Contains(e.Location))
            {
                isDragging = true;
                dragStartY = e.Y;
                dragStartVal = val;
                Capture = true;
                Invalidate();
            }
            else if (!thumb.IsEmpty)
            {
                if (e.Y < thumb.Top) Value -= largeChange;
                else if (e.Y > thumb.Bottom) Value += largeChange;
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (isDragging)
        {
            int maxVal = maximum - largeChange;
            if (maxVal > 0)
            {
                int trackH = Height;
                int thumbH = Math.Max(24, (int)((float)largeChange / maximum * trackH));
                int travel = trackH - thumbH;
                if (travel > 0)
                {
                    int deltaY = e.Y - dragStartY;
                    Value = dragStartVal + (int)((float)deltaY / travel * maxVal);
                }
            }
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (isDragging)
        {
            isDragging = false;
            Capture = false;
            Invalidate();
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        Value += (e.Delta < 0 ? 36 : -36);
    }
}

internal sealed class StyledTipLabel : Label
{
    private string[] highlightWords;
    public string[] HighlightWords
    {
        get { return highlightWords; }
        set { highlightWords = value; Invalidate(); }
    }

    public StyledTipLabel(params string[] highlightWords)
    {
        this.highlightWords = highlightWords;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        string text = this.Text;
        if (string.IsNullOrEmpty(text)) return;

        using (Font codeFont = new Font(Font.FontFamily, Font.Size, FontStyle.Italic))
        using (SolidBrush regBrush = new SolidBrush(ForeColor))
        using (SolidBrush codeBrush = new SolidBrush(AppContext.Win11Accent))
        using (StringFormat sf = new StringFormat(StringFormat.GenericTypographic))
        {
            sf.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;

            List<Token> tokens = new List<Token>();
            int cur = 0;
            string[] words = highlightWords;

            while (cur < text.Length)
            {
                int matchIdx = -1;
                if (words != null)
                {
                    for (int c = 0; c < words.Length; c++)
                    {
                        string w = words[c];
                        if (!string.IsNullOrEmpty(w) && cur + w.Length <= text.Length && text.Substring(cur, w.Length) == w)
                        {
                            matchIdx = c;
                            break;
                        }
                    }
                }

                if (matchIdx >= 0)
                {
                    tokens.Add(new Token(words[matchIdx], true));
                    cur += words[matchIdx].Length;
                    continue;
                }

                char ch = text[cur];
                bool isCjk = (ch >= 0x4E00 && ch <= 0x9FFF) || ch == '，' || ch == '。' || ch == '；' || ch == '：' || ch == '、';
                if (isCjk)
                {
                    tokens.Add(new Token(ch.ToString(), false));
                    cur++;
                }
                else
                {
                    int start = cur;
                    while (cur < text.Length)
                    {
                        bool nextIsWord = false;
                        if (words != null)
                        {
                            for (int c = 0; c < words.Length; c++)
                            {
                                string w = words[c];
                                if (!string.IsNullOrEmpty(w) && cur + w.Length <= text.Length && text.Substring(cur, w.Length) == w)
                                {
                                    nextIsWord = true;
                                    break;
                                }
                            }
                        }
                        if (nextIsWord) break;

                        char cChar = text[cur];
                        bool nextIsCjk = (cChar >= 0x4E00 && cChar <= 0x9FFF) || cChar == '，' || cChar == '。' || cChar == '；' || cChar == '：' || cChar == '、';
                        if (nextIsCjk) break;

                        cur++;
                        if (cChar == ' ') break;
                    }
                    tokens.Add(new Token(text.Substring(start, cur - start), false));
                }
            }

            float x = ClientRectangle.Left;
            float y = ClientRectangle.Top;
            float lineHeight = Font.GetHeight(g) + 3f;
            float maxX = ClientRectangle.Right;

            for (int i = 0; i < tokens.Count; i++)
            {
                Token tok = tokens[i];
                Font f = tok.IsCode ? codeFont : Font;
                SolidBrush b = tok.IsCode ? codeBrush : regBrush;

                SizeF sz = g.MeasureString(tok.Text, f, PointF.Empty, sf);
                float neededWidth = sz.Width;
                if (i + 1 < tokens.Count)
                {
                    string nextText = tokens[i + 1].Text;
                    if (nextText == "，" || nextText == "。" || nextText == "、" || nextText == "；" || nextText == "：")
                    {
                        neededWidth += g.MeasureString(nextText, tokens[i + 1].IsCode ? codeFont : Font, PointF.Empty, sf).Width;
                    }
                }

                if (x + neededWidth > maxX && x > ClientRectangle.Left)
                {
                    x = ClientRectangle.Left;
                    y += lineHeight;
                }

                g.DrawString(tok.Text, f, b, new PointF(x, y), sf);
                x += sz.Width + (tok.IsCode && !tok.Text.EndsWith(" ") ? 2.5f : 0f);
            }
        }
    }

    private struct Token
    {
        public string Text;
        public bool IsCode;
        public Token(string text, bool isCode) { Text = text; IsCode = isCode; }
    }
}

internal sealed class SettingsDialog : Form
{
    private NumericUpDown minutes, size;
    private ComboBox timerColorCombo;
    private bool updatingTimerColorCombo;
    private string selectedColor;
    private TextBox endMessage;
    private ComboBox format, language, fontCombo;
    private CheckBox startup, sound, notice, agentNotice, dailyQuote, quoteDesktopVisible;
    private Button btnEditQuotes, btnAiQuotes;
    private ComboBox soundStyle, noticeStyle;
    private TextBox hotkeyBox;
    private int hotkeyModifiers, hotkeyKey, candidateMods, candidateKey;
    private bool hotkeyActive;
    private Label lblMinutes, lblSize, lblFont, lblColor, lblFormat, lblEndMsg, lblHotkey, lblLanguage;
    private Button saveBtn, cancelBtn;
    public Settings Value { get; private set; }
    public event Action<Settings> PreviewRequested;
    public event Action<Settings, bool> QuotePreviewRequested;
    public event Action QuoteNextRequested;
    public bool IsQuoteDesktopVisible { get { return quoteDesktopVisible != null && quoteDesktopVisible.Checked; } }
    public int DialogQuoteIndex { get { return dialogQuoteIndex; } }
    public int ActiveTab { get { return activeTab; } }
    private static readonly string[] fontFamilies = { "Segoe UI", "Consolas", "Arial", "Impact", "Microsoft YaHei" };

    private int activeTab = 0;
    private int hoveredTab = -1;
    private Button btnTabTimer, btnTabQuote, btnTabAgent;
    private Label lblAgentDesc;
    private Label lblDetectedHeader;
    private Button btnRefreshAgents, btnAgentNoticeToggle, btnMcpCopyJson, btnMcpDemo;
    private Button btnMcpDemoNotify, btnMcpDemoQuote;
    private Panel panelAgentBox, panelAgentCards, panelMcpBox;
    private SlimScrollBar agentScrollBar;
    private Label lblMcpTitle;
    private Label lblTool1Name, lblTool1Desc, lblTool2Name, lblTool2Desc, lblTool3Name, lblTool3Desc;
    private StyledTipLabel lblAgentCliTip;

    private NumericUpDown quoteDuration, quoteFontSize;
    private bool isSyncingQuoteFontSize;
    private Label lblQuoteDuration, lblQuoteSize, lblQuoteFont, lblQuoteColor, lblQuoteLayer, lblQuoteQuotes;
    private StyledTipLabel lblQuoteTip;
    private ComboBox quoteFontCombo, quoteColorCombo, quoteLayerCombo;
    private bool updatingQuoteColorCombo;
    private string selectedQuoteColor = "";
    private string selectedQuoteFont = "";
    private Panel panelQuoteCard;
    private Label lblCardQuoteText, lblCardQuoteAuthor;
    private Button btnNextQuote;
    private List<QuoteItem> dialogQuoteList;
    private int dialogQuoteIndex;
    private ToolTip toolTip;
    private static readonly string[] quoteFontFamilies = { "", "Microsoft YaHei", "KaiTi", "SimSun", "Segoe UI" };
    private static readonly string[] TimerPresetColors = { "#FF0000", "#FFFFFF", "#FFD166", "#06D6A0", "#4CC9F0", "#F72585", "#52B788", "#7209B7", "#F77F00", "custom" };
    private static readonly string[] QuotePresetColors = { "", "#FFFFFF", "#FFD166", "#06D6A0", "#4CC9F0", "#F72585", "#52B788", "#7209B7", "#F77F00", "custom" };
    private static readonly string[] sharedColorNamesZh = { "柔白", "暖金", "薄荷", "淡青", "樱粉", "竹青", "暮紫", "暖橙", "自选…" };
    private static readonly string[] sharedColorNamesEn = { "White", "Gold", "Mint", "Cyan", "Pink", "Green", "Purple", "Orange", "Custom…" };

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

    private void SetupTabButton(Button btn, string text, int x, int tabIdx)
    {
        btn.Text = text;
        btn.SetBounds(x, 8, 114, 34);
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 0;
        btn.BackColor = Color.Transparent;
        btn.ForeColor = Color.Transparent;
        btn.Cursor = Cursors.Hand;
        btn.Tag = tabIdx;
        btn.Click += OnTabBtnClick;
        btn.MouseEnter += OnTabBtnMouseEnter;
        btn.MouseLeave += OnTabBtnMouseLeave;
        btn.Paint += OnTabBtnPaint;
        Controls.Add(btn);
    }
    private void OnTabBtnClick(object sender, EventArgs e) { SetActiveTab((int)((Control)sender).Tag); }
    private void OnTabBtnMouseEnter(object sender, EventArgs e) { hoveredTab = (int)((Control)sender).Tag; ((Control)sender).Invalidate(); }
    private void OnTabBtnMouseLeave(object sender, EventArgs e) { hoveredTab = -1; ((Control)sender).Invalidate(); }
    private void OnTabBtnPaint(object sender, PaintEventArgs pe) { Button btn = (Button)sender; DrawWin11Tab(pe.Graphics, btn, (int)btn.Tag); }

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
        Value.DailyQuote = s.DailyQuote; Value.QuoteDuration = s.QuoteDuration; Value.QuoteX = s.QuoteX; Value.QuoteY = s.QuoteY; Value.QuoteWidth = s.QuoteWidth; Value.QuoteFontSize = s.QuoteFontSize;
        Value.QuoteColor = s.QuoteColor ?? ""; Value.QuoteFont = s.QuoteFont ?? ""; Value.QuoteDesktopVisible = s.QuoteDesktopVisible; Value.QuotePinTopMost = s.QuotePinTopMost; Value.LastQuoteDate = s.LastQuoteDate;
        selectedQuoteColor = Value.QuoteColor;
        selectedQuoteFont = Value.QuoteFont;
        bool en = Value.Language == "en";
        Font = AppContext.GetUiFont(Value.Language, 9.5f);
        BackColor = AppContext.Win11Bg;
        AppContext.ApplyModernWindowStyle(this);
        Text = en ? "iClock Settings" : "iClock 设置"; FormBorderStyle = FormBorderStyle.FixedDialog; StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false; ClientSize = new Size(380, 468);

        btnTabTimer = new Button(); SetupTabButton(btnTabTimer, en ? "Timer" : "倒计时", 18, 0);
        btnTabQuote = new Button(); SetupTabButton(btnTabQuote, en ? "Daily Quotes" : "每日格言", 133, 1);
        btnTabAgent = new Button(); SetupTabButton(btnTabAgent, en ? "AI Collaboration" : "AI 协作", 248, 2);

        KeyPreview = true;
        KeyDown += delegate(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.Tab)
            {
                SetActiveTab(e.Shift ? (activeTab + 2) % 3 : (activeTab + 1) % 3);
                e.SuppressKeyPress = true;
            }
        };

        lblMinutes = AddLabel(en ? "Duration (minutes)" : "倒计时（分钟）", 18, 66); minutes = AddNumber(Value.Minutes, 1, 1440, 172, 62);
        minutes.ValueChanged += OnPreviewChanged;
        lblSize = AddLabel(en ? "Text size" : "文字大小", 18, 100); size = AddNumber(Value.FontSize, 12, 120, 172, 96);
        size.ValueChanged += OnPreviewChanged;
        lblFont = AddLabel(en ? "Font style" : "字体样式", 18, 134);
        fontCombo = AddCombo(130);
        fontCombo.Items.AddRange(en
            ? new object[] { "Segoe UI (Default)", "Consolas (Monospace)", "Arial", "Impact", "Microsoft YaHei" }
            : new object[] { "Segoe UI (默认)", "Consolas (极客等宽)", "Arial", "Impact (醒目粗黑)", "微软雅黑" });
        fontCombo.SelectedIndex = Value.FontFamily == "Consolas" ? 1 :
            (Value.FontFamily == "Arial" ? 2 :
            (Value.FontFamily == "Impact" ? 3 :
            (Value.FontFamily == "Microsoft YaHei" ? 4 : 0)));
        fontCombo.DrawItem += OnDrawFontItem;
        fontCombo.SelectedIndexChanged += OnQuoteFontComboChanged;
        lblColor = AddLabel(en ? "Text color" : "文字颜色", 18, 168); selectedColor = Value.Color;
        timerColorCombo = AddCombo(164);
        PopulateTimerColorCombo(en);
        timerColorCombo.SelectedIndex = GetTimerColorIndex(selectedColor);
        timerColorCombo.DrawItem += OnDrawTimerColorItem;
        timerColorCombo.SelectedIndexChanged += OnTimerColorComboChanged;
        lblFormat = AddLabel(en ? "Display format" : "显示格式", 18, 202);
        format = AddCombo(198);
        format.Items.AddRange(en ? new object[] { "HH:MM:SS", "MM:SS", "H:M:S units (00h 25m 00s)" } : new object[] { "HH:MM:SS", "MM:SS", "中文单位 (00时25分00秒)" });
        format.SelectedIndex = Value.Format == "MM:SS" ? 1 : (Value.Format == "Chinese" ? 2 : 0);
        format.DrawItem += OnDrawComboItem;
        format.SelectedIndexChanged += OnPreviewChanged;
        hotkeyModifiers = Value.HotkeyModifiers; hotkeyKey = Value.HotkeyKey;
        candidateMods = hotkeyModifiers; candidateKey = hotkeyKey;
        lblEndMsg = AddLabel(en ? "End message" : "结束时弹出消息", 18, 236);
        endMessage = new TextBox();
        endMessage.SetBounds(172, 232, 188, 28);
        endMessage.Multiline = true;
        endMessage.MaxLength = 200;
        endMessage.Text = Value.EndMessage;
        ApplyInputPadding(endMessage);
        Controls.Add(endMessage);
        lblHotkey = AddLabel(en ? "Start/pause hotkey" : "启动/暂停快捷键", 18, 270);
        hotkeyBox = new TextBox();
        hotkeyBox.ReadOnly = true;
        hotkeyBox.Multiline = true;
        hotkeyBox.SetBounds(172, 266, 188, 28);
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
        lblLanguage = AddLabel(en ? "Interface language" : "界面语言", 18, 304);
        language = AddCombo(300);
        language.Items.AddRange(new object[] { "简体中文", "English" });
        language.SelectedIndex = Value.Language == "en" ? 1 : 0;
        language.SelectedIndexChanged += OnLanguageChanged;
        language.DrawItem += OnDrawComboItem;
        sound = AddCheck(en ? "Play sound at end" : "结束时声音提醒", Value.EndSound, 18, 334);
        sound.SetBounds(18, 334, 150, 24);

        soundStyle = AddCombo(334);
        soundStyle.Items.AddRange(en
            ? new object[] { "QQ Message (Classic)", "Telephone Ring (Classic)", "Alarm Ring (Strong)" }
            : new object[] { "QQ 经典提示 (嘀嘀)", "经典电话铃 (叮铃铃)", "醒目闹铃 (强力)" });
        soundStyle.SelectedIndex = (Value.EndSoundStyle == "Phone" || Value.EndSoundStyle == "Pager" || Value.EndSoundStyle == "Classic") ? 1 : (Value.EndSoundStyle == "Alarm" ? 2 : 0);
        soundStyle.Enabled = sound.Checked;
        soundStyle.DrawItem += OnDrawComboItem;
        sound.CheckedChanged += OnSoundCheckChanged;
        soundStyle.SelectionChangeCommitted += OnSoundStyleCommitted;

        notice = AddCheck(en ? "End notification" : "结束时系统通知", Value.EndNotice, 18, 368);
        notice.SetBounds(18, 368, 150, 24);

        noticeStyle = AddCombo(368);
        noticeStyle.Items.AddRange(en
            ? new object[] { "Popup (Auto DND Fullscreen)", "In-Place Pulse" }
            : new object[] { "弹窗强提醒 (全屏免打扰)", "原位呼吸轻提醒" });
        noticeStyle.SelectedIndex = Value.NoticeStyle == "Pulse" ? 1 : 0;
        noticeStyle.Enabled = notice.Checked;
        noticeStyle.DrawItem += OnDrawComboItem;
        notice.CheckedChanged += OnNoticeCheckChanged;

        startup = AddCheck(en ? "Start with Windows" : "开机自启动", Value.AutoStart, 18, 402);
        startup.SetBounds(18, 402, 344, 24);
        startup.CheckedChanged += OnStartupCheckedChanged;


        dialogQuoteList = DailyQuoteDialog.LoadQuotes();
        if (dialogQuoteList == null || dialogQuoteList.Count == 0) dialogQuoteList = new List<QuoteItem>(DailyQuoteDialog.DefaultQuotes);
        int seed = DateTime.Today.Year * 366 + DateTime.Today.DayOfYear;
        dialogQuoteIndex = Math.Abs(seed) % dialogQuoteList.Count;

        // Section 1 Hero Banner: panelQuoteCard (Top = 62, Height = 98px, vertically centered content)
        panelQuoteCard = new Panel();
        panelQuoteCard.SetBounds(18, 62, 344, 98);
        panelQuoteCard.BackColor = Color.FromArgb(246, 248, 250);
        panelQuoteCard.Paint += delegate(object sender, PaintEventArgs pe)
        {
            pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            pe.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // Top-left "今日" / "TODAY" corner triangle ribbon badge (Proposal A)
            int badgeSize = 36;
            PointF[] pts = new PointF[] {
                new PointF(1, 1),
                new PointF(badgeSize, 1),
                new PointF(1, badgeSize)
            };
            using (SolidBrush b = new SolidBrush(AppContext.Win11Accent))
            {
                pe.Graphics.FillPolygon(b, pts);
            }

            GraphicsState st = pe.Graphics.Save();
            pe.Graphics.TranslateTransform(11.5f, 11.5f);
            pe.Graphics.RotateTransform(-45);
            bool isEn = (language != null && language.SelectedIndex == 1) || (language == null && Value != null && Value.Language == "en");
            string tagText = isEn ? "TODAY" : "今日";
            using (Font f = AppContext.GetUiFont(isEn ? "en" : "zh", isEn ? 6.5f : 7.5f, FontStyle.Bold))
            using (SolidBrush tb = new SolidBrush(Color.White))
            {
                SizeF sz = pe.Graphics.MeasureString(tagText, f);
                pe.Graphics.DrawString(tagText, f, tb, -sz.Width / 2.0f + 0.5f, -sz.Height / 2.0f + 0.5f);
            }
            pe.Graphics.Restore(st);

            using (Pen p = new Pen(AppContext.Win11Border, 1f))
            {
                pe.Graphics.DrawRectangle(p, 0, 0, panelQuoteCard.Width - 1, panelQuoteCard.Height - 1);
            }
        };
        Controls.Add(panelQuoteCard);

        lblCardQuoteText = new Label();
        lblCardQuoteText.BackColor = Color.Transparent;
        lblCardQuoteText.SetBounds(38, 12, 268, 48);
        lblCardQuoteText.Font = AppContext.GetUiFont(en ? "en" : "zh", 9.5f, FontStyle.Regular);
        lblCardQuoteText.ForeColor = Color.FromArgb(31, 41, 55);
        lblCardQuoteText.TextAlign = ContentAlignment.MiddleCenter;
        lblCardQuoteText.AutoEllipsis = true;
        panelQuoteCard.Controls.Add(lblCardQuoteText);

        btnNextQuote = new Button();
        btnNextQuote.Text = "";
        btnNextQuote.SetBounds(12, 64, 26, 24);
        btnNextQuote.FlatStyle = FlatStyle.Flat;
        btnNextQuote.FlatAppearance.BorderSize = 0;
        btnNextQuote.FlatAppearance.MouseDownBackColor = Color.FromArgb(220, 224, 230);
        btnNextQuote.FlatAppearance.MouseOverBackColor = Color.FromArgb(235, 238, 242);
        btnNextQuote.BackColor = Color.Transparent;
        btnNextQuote.Cursor = Cursors.Hand;
        btnNextQuote.Paint += delegate(object sender, PaintEventArgs pe)
        {
            pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            bool hov = btnNextQuote.ClientRectangle.Contains(btnNextQuote.PointToClient(Cursor.Position));
            Color iconCol = hov ? AppContext.Win11Accent : Color.FromArgb(120, 125, 135);
            float cx = btnNextQuote.Width / 2.0f;
            float cy = btnNextQuote.Height / 2.0f;
            using (Pen p = new Pen(iconCol, 1.5f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                pe.Graphics.DrawArc(p, cx - 5.0f, cy - 5.0f, 10f, 10f, 45, 270);
                float ax = cx + 3.5f;
                float ay = cy - 3.5f;
                pe.Graphics.DrawLine(p, ax, ay, ax + 3.0f, ay);
                pe.Graphics.DrawLine(p, ax, ay, ax, ay - 3.0f);
            }
        };
        btnNextQuote.MouseEnter += delegate { btnNextQuote.Invalidate(); };
        btnNextQuote.MouseLeave += delegate { btnNextQuote.Invalidate(); };
        if (toolTip == null) toolTip = new ToolTip();
        toolTip.SetToolTip(btnNextQuote, en ? "Next quote (↻)" : "换一句 (↻)");
        btnNextQuote.Click += delegate
        {
            if (dialogQuoteList != null && dialogQuoteList.Count > 0)
            {
                dialogQuoteIndex = (dialogQuoteIndex + 1) % dialogQuoteList.Count;
                UpdateDialogQuoteCard();
                if (QuoteNextRequested != null) QuoteNextRequested();
                TriggerQuotePreview();
            }
        };
        panelQuoteCard.Controls.Add(btnNextQuote);

        lblCardQuoteAuthor = new Label();
        lblCardQuoteAuthor.SetBounds(44, 64, 286, 24);
        lblCardQuoteAuthor.Font = AppContext.GetUiFont(en ? "en" : "zh", 8.5f, FontStyle.Italic);
        lblCardQuoteAuthor.ForeColor = AppContext.Win11TextSecondary;
        lblCardQuoteAuthor.TextAlign = ContentAlignment.MiddleRight;
        lblCardQuoteAuthor.AutoEllipsis = true;
        panelQuoteCard.Controls.Add(lblCardQuoteAuthor);
        UpdateDialogQuoteCard();

        // Section 2: Dual Switches Row (Y = 172)
        dailyQuote = AddCheck(en ? "Enable daily quotes" : "开启每日格言", Value.DailyQuote, 18, 172);
        dailyQuote.SetBounds(18, 172, 180, 24);
        dailyQuote.CheckedChanged += OnDailyQuoteCheckChanged;

        quoteDesktopVisible = AddCheck(en ? "Show on desktop" : "桌面悬浮显示", Value.QuoteDesktopVisible, 210, 172);
        int chkW = Math.Min(152, TextRenderer.MeasureText(quoteDesktopVisible.Text, quoteDesktopVisible.Font).Width + 28);
        quoteDesktopVisible.SetBounds(362 - chkW, 172, chkW, 24);
        quoteDesktopVisible.Enabled = dailyQuote.Checked;
        quoteDesktopVisible.CheckedChanged += OnQuoteDurationChanged;

        // Section 3: Unified 2-column key-value grid (Y = 204, 238, 272, 306, 340, 374)
        // Row 1: Duration
        lblQuoteDuration = AddLabel(en ? "Duration (seconds)" : "显示时长（秒）", 18, 208);
        quoteDuration = AddNumber(Value.QuoteDuration, 3, 60, 172, 204);
        quoteDuration.ValueChanged += OnQuoteDurationChanged;

        // Row 2: Font Size
        lblQuoteSize = AddLabel(en ? "Text size" : "文字大小", 18, 242);
        quoteFontSize = new NumericUpDown();
        quoteFontSize.DecimalPlaces = 1;
        quoteFontSize.Increment = 0.5M;
        quoteFontSize.Minimum = 8.0M;
        quoteFontSize.Maximum = 24.0M;
        float initQuoteSize = (Value.QuoteFontSize >= 8.0f && Value.QuoteFontSize <= 24.0f) ? Value.QuoteFontSize : 18.0f;
        quoteFontSize.Value = (decimal)initQuoteSize;
        quoteFontSize.SetBounds(172, 238, 188, 28);
        ApplyInputPadding(quoteFontSize);
        quoteFontSize.ValueChanged += OnQuoteFontSizeChanged;
        Controls.Add(quoteFontSize);

        // Row 3: Font Style
        lblQuoteFont = AddLabel(en ? "Font style" : "字体样式", 18, 276);
        quoteFontCombo = AddCombo(272);
        quoteFontCombo.Items.AddRange(en
            ? new object[] { "Follow Timer (Default)", "Microsoft YaHei", "KaiTi", "SimSun", "Segoe UI" }
            : new object[] { "跟随倒计时 (默认)", "微软雅黑", "楷体", "宋体", "Segoe UI" });
        quoteFontCombo.SelectedIndex = GetQuoteFontIndex(selectedQuoteFont);
        quoteFontCombo.DrawItem += OnDrawQuoteFontItem;
        quoteFontCombo.SelectedIndexChanged += OnQuoteFontChanged;

        // Row 4: Color (Full width 188px, self-contained color chips)
        lblQuoteColor = AddLabel(en ? "Text color" : "文字颜色", 18, 310);
        quoteColorCombo = AddCombo(306);
        PopulateQuoteColorCombo(en);
        quoteColorCombo.SelectedIndex = GetQuoteColorIndex(selectedQuoteColor);
        quoteColorCombo.DrawItem += OnDrawQuoteColorItem;
        quoteColorCombo.SelectedIndexChanged += OnQuoteColorComboChanged;

        // Row 5: Window Layer (常驻层级)
        lblQuoteLayer = AddLabel(en ? "Window level" : "常驻层级", 18, 344);
        quoteLayerCombo = AddCombo(340);
        quoteLayerCombo.Items.AddRange(en
            ? new object[] { "Desktop Layer (Default)", "Always on Top" }
            : new object[] { "桌面背景置底 (默认)", "始终置顶显示" });
        quoteLayerCombo.SelectedIndex = Value.QuotePinTopMost ? 1 : 0;
        quoteLayerCombo.DrawItem += OnDrawComboItem;
        quoteLayerCombo.SelectedIndexChanged += OnQuoteLayerChanged;

        // Row 6: Quote Library
        lblQuoteQuotes = AddLabel(en ? "Quote library" : "名言词库", 18, 378);
        btnEditQuotes = new Button();
        btnEditQuotes.Text = en ? "Edit Quotes" : "自定义…";
        btnEditQuotes.Font = AppContext.GetUiFont(en ? "en" : "zh", 9f);
        btnEditQuotes.SetBounds(172, 374, 90, 28);
        AppContext.StyleSecondaryButton(btnEditQuotes);
        btnEditQuotes.Click += OnEditQuotesClick;
        if (toolTip == null) toolTip = new ToolTip();
        toolTip.SetToolTip(btnEditQuotes, en ? "Open and edit quotes.txt in notepad" : "在记事本中自定义编辑名言 (quotes.txt)");
        Controls.Add(btnEditQuotes);

        btnAiQuotes = new Button();
        btnAiQuotes.Text = en ? "✨ AI Quotes" : "✨ AI 扩充";
        btnAiQuotes.Font = AppContext.GetUiFont(en ? "en" : "zh", 9f);
        btnAiQuotes.SetBounds(270, 374, 90, 28);
        AppContext.StyleSecondaryButton(btnAiQuotes);
        btnAiQuotes.Click += OnAiQuotesClick;
        toolTip.SetToolTip(btnAiQuotes, en ? "Generate quotes with AI Agent / MCP" : "通过 AI 智能体 / MCP 批量创作并扩充词库");
        Controls.Add(btnAiQuotes);

        // Section 4: Tip
        lblQuoteTip = new StyledTipLabel(en
            ? new string[] { "Alt + Ctrl + wheel", "Alt + drag" }
            : new string[] { "Alt + Ctrl + 滚轮", "Alt + 拖拽" });
        lblQuoteTip.UseMnemonic = false;
        lblQuoteTip.Text = en
            ? "Tip: Hold Alt + drag to move quote, Alt + Ctrl + wheel to scale font."
            : "提示：在桌面格言文字上按住 Alt + 拖拽，Alt + Ctrl + 滚轮缩放字号。";
        lblQuoteTip.ForeColor = AppContext.Win11TextSecondary;
        lblQuoteTip.Font = AppContext.GetUiFont(en ? "en" : "zh", 8f);
        lblQuoteTip.SetBounds(16, 416, 348, 22);
        Controls.Add(lblQuoteTip);
        if (toolTip == null) toolTip = new ToolTip();
        toolTip.SetToolTip(quoteDesktopVisible, en
            ? "Display quote permanently on desktop; hold Alt + drag to move, drag edge to resize"
            : "在桌面上常驻显示格言；在桌面格言文字上按住 Alt + 拖拽调整位置，拖动右边缘调节宽度");


        agentNotice = new CheckBox();
        agentNotice.Checked = Value.AgentNotice;
        agentNotice.SetBounds(-100, -100, 10, 10);
        agentNotice.TabStop = false;
        agentNotice.Visible = false;
        Controls.Add(agentNotice);

        lblDetectedHeader = new Label();
        lblDetectedHeader.Font = AppContext.GetUiFont(en ? "en" : "zh", 9f, FontStyle.Bold);
        lblDetectedHeader.ForeColor = AppContext.Win11TextPrimary;
        lblDetectedHeader.TextAlign = ContentAlignment.MiddleLeft;
        lblDetectedHeader.SetBounds(18, 56, 185, 20);
        Controls.Add(lblDetectedHeader);

        lblAgentDesc = new Label();
        lblAgentDesc.Font = AppContext.GetUiFont(en ? "en" : "zh", 8f);
        lblAgentDesc.ForeColor = AppContext.Win11TextSecondary;
        lblAgentDesc.TextAlign = ContentAlignment.MiddleLeft;
        lblAgentDesc.SetBounds(18, 76, 185, 18);
        Controls.Add(lblAgentDesc);

        btnRefreshAgents = new Button();
        btnRefreshAgents.Text = en ? "Re-scan" : "重新检测";
        btnRefreshAgents.Font = AppContext.GetUiFont(en ? "en" : "zh", 8.5f);
        btnRefreshAgents.SetBounds(208, 63, 72, 26);
        AppContext.StyleSecondaryButton(btnRefreshAgents);
        btnRefreshAgents.Click += OnRefreshAgentsClick;
        Controls.Add(btnRefreshAgents);

        btnAgentNoticeToggle = new Button();
        btnAgentNoticeToggle.SetBounds(286, 63, 76, 26);
        btnAgentNoticeToggle.FlatStyle = FlatStyle.Flat;
        btnAgentNoticeToggle.FlatAppearance.BorderSize = 1;
        btnAgentNoticeToggle.Font = AppContext.GetUiFont(en ? "en" : "zh", 8.5f, FontStyle.Regular);
        btnAgentNoticeToggle.Cursor = Cursors.Hand;
        btnAgentNoticeToggle.Click += OnAgentNoticeToggleClick;
        Controls.Add(btnAgentNoticeToggle);

        agentNotice.CheckedChanged += OnAgentNoticeCheckedChanged;
        UpdateAgentNoticeToggleState(en);

        panelAgentBox = new Panel();
        panelAgentBox.SetBounds(18, 96, 344, 114);
        panelAgentBox.BackColor = Color.FromArgb(246, 248, 250);
        panelAgentBox.Paint += OnCardPaint;
        Controls.Add(panelAgentBox);

        panelAgentCards = new DoubleBufferedPanel();
        panelAgentCards.SetBounds(1, 1, 333, 112);
        panelAgentCards.AutoScroll = false;
        panelAgentCards.BackColor = Color.FromArgb(246, 248, 250);
        panelAgentCards.BorderStyle = BorderStyle.None;
        panelAgentBox.Controls.Add(panelAgentCards);

        agentScrollBar = new SlimScrollBar();
        agentScrollBar.SetBounds(334, 3, 7, 108);
        agentScrollBar.Visible = false;
        agentScrollBar.ValueChanged += OnAgentScrollBarValueChanged;
        panelAgentBox.Controls.Add(agentScrollBar);

        panelAgentCards.MouseWheel += OnAgentCardMouseWheel;
        panelAgentBox.MouseWheel += OnAgentCardMouseWheel;

        lblMcpTitle = new Label();
        lblMcpTitle.Text = en ? "MCP Direct Connection (3 Tools)" : "MCP 协议直连 (3项核心能力)";
        lblMcpTitle.Font = AppContext.GetUiFont(en ? "en" : "zh", 9f, FontStyle.Bold);
        lblMcpTitle.ForeColor = AppContext.Win11TextPrimary;
        lblMcpTitle.TextAlign = ContentAlignment.MiddleLeft;
        lblMcpTitle.SetBounds(18, 224, 226, 24);
        Controls.Add(lblMcpTitle);

        btnMcpCopyJson = new Button();
        btnMcpCopyJson.Text = en ? "Copy Prompt" : "复制接入提示词";
        btnMcpCopyJson.Font = AppContext.GetUiFont(en ? "en" : "zh", 8.5f);
        btnMcpCopyJson.SetBounds(244, 222, 118, 26);
        AppContext.StyleSecondaryButton(btnMcpCopyJson);
        btnMcpCopyJson.Click += OnMcpCopyPromptClick;
        Controls.Add(btnMcpCopyJson);

        panelMcpBox = new Panel();
        panelMcpBox.SetBounds(18, 252, 344, 142);
        panelMcpBox.BackColor = Color.White;
        panelMcpBox.Paint += OnMcpBoxPaint;
        Controls.Add(panelMcpBox);

        // Row 1: Decision Gate (iclock_ask_human)
        lblTool1Name = new Label();
        lblTool1Name.Text = en ? "Flash Approval HUD (ask_human)" : "极速桌面授权门禁 (Flash Approval HUD)";
        lblTool1Name.Font = AppContext.GetUiFont(en ? "en" : "zh", 9f, FontStyle.Regular);
        lblTool1Name.ForeColor = AppContext.Win11TextPrimary;
        lblTool1Name.SetBounds(12, 7, 240, 18);
        panelMcpBox.Controls.Add(lblTool1Name);

        lblTool1Desc = new Label();
        lblTool1Desc.Text = en ? "Human-in-the-loop approval HUD for critical actions" : "关键高危操作置顶人机决策门禁 (支持 1/2/Enter/Esc 盲打)";
        lblTool1Desc.Font = AppContext.GetUiFont(en ? "en" : "zh", 8.2f);
        lblTool1Desc.ForeColor = AppContext.Win11TextSecondary;
        lblTool1Desc.SetBounds(12, 26, 246, 16);
        panelMcpBox.Controls.Add(lblTool1Desc);

        btnMcpDemo = new Button();
        btnMcpDemo.Text = en ? "Demo" : "演示";
        btnMcpDemo.Font = AppContext.GetUiFont(en ? "en" : "zh", 8.5f);
        btnMcpDemo.SetBounds(264, 11, 68, 26);
        AppContext.StyleSecondaryButton(btnMcpDemo);
        btnMcpDemo.Click += OnMcpDemoClick;
        panelMcpBox.Controls.Add(btnMcpDemo);

        // Row 2: Task Notification (iclock_notify)
        lblTool2Name = new Label();
        lblTool2Name.Text = en ? "Task Notification (notify)" : "任务完成提醒";
        lblTool2Name.Font = AppContext.GetUiFont(en ? "en" : "zh", 9f, FontStyle.Regular);
        lblTool2Name.ForeColor = AppContext.Win11TextPrimary;
        lblTool2Name.SetBounds(12, 54, 240, 18);
        panelMcpBox.Controls.Add(lblTool2Name);

        lblTool2Desc = new Label();
        lblTool2Desc.Text = en ? "TopMost modal alert & sound on background task done" : "后台耗时任务完成时全向弹窗与声音唤醒";
        lblTool2Desc.Font = AppContext.GetUiFont(en ? "en" : "zh", 8.2f);
        lblTool2Desc.ForeColor = AppContext.Win11TextSecondary;
        lblTool2Desc.SetBounds(12, 73, 246, 16);
        panelMcpBox.Controls.Add(lblTool2Desc);

        btnMcpDemoNotify = new Button();
        btnMcpDemoNotify.Text = en ? "Demo" : "演示";
        btnMcpDemoNotify.Font = AppContext.GetUiFont(en ? "en" : "zh", 8.5f);
        btnMcpDemoNotify.SetBounds(264, 58, 68, 26);
        AppContext.StyleSecondaryButton(btnMcpDemoNotify);
        btnMcpDemoNotify.Click += OnMcpDemoNotifyClick;
        panelMcpBox.Controls.Add(btnMcpDemoNotify);

        // Row 3: Desktop Status (iclock_quote)
        lblTool3Name = new Label();
        lblTool3Name.Text = en ? "Desktop Status (quote)" : "桌面状态投射";
        lblTool3Name.Font = AppContext.GetUiFont(en ? "en" : "zh", 9f, FontStyle.Regular);
        lblTool3Name.ForeColor = AppContext.Win11TextPrimary;
        lblTool3Name.SetBounds(12, 101, 240, 18);
        panelMcpBox.Controls.Add(lblTool3Name);

        lblTool3Desc = new Label();
        lblTool3Desc.Text = en ? "Project task goals, thoughts or working status onto desktop" : "智能体在屏幕投射当前进展与工作便签";
        lblTool3Desc.Font = AppContext.GetUiFont(en ? "en" : "zh", 8.2f);
        lblTool3Desc.ForeColor = AppContext.Win11TextSecondary;
        lblTool3Desc.SetBounds(12, 120, 246, 16);
        panelMcpBox.Controls.Add(lblTool3Desc);

        btnMcpDemoQuote = new Button();
        btnMcpDemoQuote.Text = en ? "Demo" : "演示";
        btnMcpDemoQuote.Font = AppContext.GetUiFont(en ? "en" : "zh", 8.5f);
        btnMcpDemoQuote.SetBounds(264, 105, 68, 26);
        AppContext.StyleSecondaryButton(btnMcpDemoQuote);
        btnMcpDemoQuote.Click += OnMcpDemoQuoteClick;
        panelMcpBox.Controls.Add(btnMcpDemoQuote);

        lblAgentCliTip = new StyledTipLabel("iclock --install-cli", "iclock -h");
        lblAgentCliTip.UseMnemonic = false;
        lblAgentCliTip.Text = en
            ? "Tip: Run iclock -h for commands, iclock --install-cli to add to PATH."
            : "提示：终端运行 iclock -h 查看指令，iclock --install-cli 写入 PATH。";
        lblAgentCliTip.ForeColor = AppContext.Win11TextSecondary;
        lblAgentCliTip.Font = AppContext.GetUiFont(en ? "en" : "zh", 8f);
        lblAgentCliTip.SetBounds(16, 416, 348, 22);
        Controls.Add(lblAgentCliTip);

        saveBtn = new Button(); saveBtn.Text = en ? "Save" : "保存";
        saveBtn.SetBounds(-100, -100, 10, 10);
        saveBtn.TabStop = false;
        if (!hotkeyActive || (Value.HotkeyKey == (int)Keys.Space && (Value.HotkeyModifiers & 3) != 0)) saveBtn.Enabled = false;
        saveBtn.Click += SaveClick;
        Controls.Add(saveBtn);

        cancelBtn = new Button(); cancelBtn.Text = en ? "Cancel" : "取消";
        cancelBtn.SetBounds(-100, -100, 10, 10);
        cancelBtn.TabStop = false;
        cancelBtn.DialogResult = DialogResult.Cancel;
        cancelBtn.Click += OnCancelClick;
        CancelButton = cancelBtn;
        Controls.Add(cancelBtn);

        AppContext.CenterFormOnScreen(this);
        SetActiveTab(0);
    }

    internal void SetActiveTab(int tabIndex)
    {
        activeTab = tabIndex;
        bool isTimer = (tabIndex == 0);
        bool isQuote = (tabIndex == 1);
        bool isAgent = (tabIndex == 2);

        lblMinutes.Visible = isTimer;
        minutes.Visible = isTimer;
        lblSize.Visible = isTimer;
        size.Visible = isTimer;
        lblFont.Visible = isTimer;
        fontCombo.Visible = isTimer;
        lblColor.Visible = isTimer;
        timerColorCombo.Visible = isTimer;
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

        dailyQuote.Visible = isQuote;
        quoteDesktopVisible.Visible = isQuote;
        panelQuoteCard.Visible = isQuote;
        lblQuoteDuration.Visible = isQuote;
        quoteDuration.Visible = isQuote;
        lblQuoteSize.Visible = isQuote;
        quoteFontSize.Visible = isQuote;
        lblQuoteFont.Visible = isQuote;
        quoteFontCombo.Visible = isQuote;
        lblQuoteColor.Visible = isQuote;
        quoteColorCombo.Visible = isQuote;
        lblQuoteLayer.Visible = isQuote;
        quoteLayerCombo.Visible = isQuote;
        lblQuoteQuotes.Visible = isQuote;
        btnEditQuotes.Visible = isQuote;
        if (btnAiQuotes != null) btnAiQuotes.Visible = isQuote;
        lblQuoteTip.Visible = isQuote;

        if (agentNotice != null) agentNotice.Visible = isAgent;
        if (lblDetectedHeader != null) lblDetectedHeader.Visible = isAgent;
        if (lblAgentDesc != null) lblAgentDesc.Visible = isAgent;
        if (btnRefreshAgents != null) btnRefreshAgents.Visible = isAgent;
        if (btnAgentNoticeToggle != null) btnAgentNoticeToggle.Visible = isAgent;
        if (panelAgentBox != null) panelAgentBox.Visible = isAgent;
        if (panelAgentCards != null) panelAgentCards.Visible = isAgent;
        if (lblMcpTitle != null) lblMcpTitle.Visible = isAgent;
        if (btnMcpCopyJson != null) btnMcpCopyJson.Visible = isAgent;
        if (panelMcpBox != null) panelMcpBox.Visible = isAgent;
        if (lblAgentCliTip != null) lblAgentCliTip.Visible = isAgent;

        UpdateTabButtons();
        TriggerQuotePreview();
        if (isAgent)
        {
            RefreshAgentCards();
        }
    }

    private void UpdateTabButtons()
    {
        if (btnTabTimer != null) btnTabTimer.Invalidate();
        if (btnTabQuote != null) btnTabQuote.Invalidate();
        if (btnTabAgent != null) btnTabAgent.Invalidate();
    }

    private void DrawWin11Tab(Graphics g, Button btn, int tabIdx)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        bool isActive = (activeTab == tabIdx);
        bool isHov = (hoveredTab == tabIdx && !isActive);

        g.Clear(BackColor);

        // Continuous baseline divider running across inactive tabs only
        if (!isActive)
        {
            using (Pen basePen = new Pen(Color.FromArgb(226, 230, 236), 1f))
            {
                g.DrawLine(basePen, 0, btn.Height - 1, btn.Width, btn.Height - 1);
            }
        }

        if (isActive)
        {
            // Windows 11 File Explorer Active Tab: pure white card with 6px top rounded corners and flat/seamless bottom
            Rectangle cardRect = new Rectangle(0, 2, btn.Width - 1, btn.Height - 2);
            using (GraphicsPath path = new GraphicsPath())
            {
                int d = 6 * 2;
                path.AddArc(cardRect.X, cardRect.Y, d, d, 180, 90);
                path.AddArc(cardRect.Right - d, cardRect.Y, d, d, 270, 90);
                path.AddLine(cardRect.Right, cardRect.Bottom, cardRect.X, cardRect.Bottom);
                path.CloseFigure();

                using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(10, 0, 0, 0)))
                using (SolidBrush bgBrush = new SolidBrush(Color.White))
                using (Pen borderPen = new Pen(Color.FromArgb(216, 220, 228), 1f))
                {
                    // Subtle top shadow
                    using (GraphicsPath shadowPath = new GraphicsPath())
                    {
                        shadowPath.AddArc(cardRect.X, cardRect.Y + 1, d, d, 180, 90);
                        shadowPath.AddArc(cardRect.Right - d, cardRect.Y + 1, d, d, 270, 90);
                        shadowPath.AddLine(cardRect.Right, cardRect.Bottom, cardRect.X, cardRect.Bottom);
                        shadowPath.CloseFigure();
                        g.FillPath(shadowBrush, shadowPath);
                    }
                    g.FillPath(bgBrush, path);

                    // Draw U-shaped border (left, top, right) leaving the bottom seamlessly open
                    using (GraphicsPath borderPath = new GraphicsPath())
                    {
                        borderPath.AddLine(cardRect.X, cardRect.Bottom, cardRect.X, cardRect.Y + 6);
                        borderPath.AddArc(cardRect.X, cardRect.Y, d, d, 180, 90);
                        borderPath.AddLine(cardRect.X + 6, cardRect.Y, cardRect.Right - 6, cardRect.Y);
                        borderPath.AddArc(cardRect.Right - d, cardRect.Y, d, d, 270, 90);
                        borderPath.AddLine(cardRect.Right, cardRect.Y + 6, cardRect.Right, cardRect.Bottom);
                        g.DrawPath(borderPen, borderPath);
                    }
                }
            }
        }
        else if (isHov)
        {
            // Inactive Tab hover: soft rounded preview capsule
            Rectangle hovRect = new Rectangle(3, 4, btn.Width - 7, btn.Height - 8);
            using (GraphicsPath hovPath = AppContext.GetRoundedRectPath(hovRect, 5))
            using (SolidBrush hovBrush = new SolidBrush(Color.FromArgb(235, 239, 245)))
            {
                g.FillPath(hovBrush, hovPath);
            }
        }

        // Win11 File Explorer vertical divider between inactive tabs
        if (!isActive && tabIdx < 2 && activeTab != tabIdx + 1)
        {
            using (Pen divPen = new Pen(Color.FromArgb(212, 216, 222), 1f))
            {
                int divH = 14;
                int divY = (btn.Height - divH) / 2;
                g.DrawLine(divPen, btn.Width - 1, divY, btn.Width - 1, divY + divH);
            }
        }

        // Pure clean text without emoji clutter
        string label = btn.Text ?? "";

        Color textCol = isActive ? Color.FromArgb(24, 28, 36) : (isHov ? Color.FromArgb(32, 36, 44) : Color.FromArgb(95, 107, 124));
        Font tabFont = AppContext.GetUiFont(language != null && language.SelectedIndex == 1 ? "en" : "zh", 9.5f, isActive ? FontStyle.Bold : FontStyle.Regular);
        using (tabFont)
        using (SolidBrush textBrush = new SolidBrush(textCol))
        using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        {
            g.DrawString(label, tabFont, textBrush, new RectangleF(0, isActive ? 1 : 0, btn.Width, btn.Height), sf);
        }
    }

    private Color GetEffectiveQuoteColor()
    {
        string col = !string.IsNullOrEmpty(selectedQuoteColor) ? selectedQuoteColor : selectedColor;
        try { return ColorTranslator.FromHtml(col); }
        catch { return Color.White; }
    }

    private static int GetQuoteFontIndex(string font)
    {
        if (string.IsNullOrEmpty(font)) return 0;
        for (int i = 1; i < quoteFontFamilies.Length; i++)
        {
            if (string.Equals(quoteFontFamilies[i], font, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return 0;
    }

    private static int GetTimerColorIndex(string colorHex)
    {
        if (string.IsNullOrEmpty(colorHex)) return 0;
        for (int i = 0; i < 9; i++)
            if (string.Equals(TimerPresetColors[i], colorHex, StringComparison.OrdinalIgnoreCase)) return i;
        return 9;
    }

    private static int GetQuoteColorIndex(string colorHex)
    {
        if (string.IsNullOrEmpty(colorHex)) return 0;
        for (int i = 1; i < 9; i++)
            if (string.Equals(QuotePresetColors[i], colorHex, StringComparison.OrdinalIgnoreCase)) return i;
        return 9;
    }

    private static void PopulateColorCombo(ComboBox cb, string firstItem, bool en)
    {
        if (cb == null) return;
        cb.Items.Clear();
        cb.Items.Add(firstItem);
        for (int i = 0; i < sharedColorNamesEn.Length; i++)
            cb.Items.Add(en ? sharedColorNamesEn[i] : sharedColorNamesZh[i]);
    }
    private void PopulateTimerColorCombo(bool en)
    {
        PopulateColorCombo(timerColorCombo, en ? "Classic Red (Default)" : "经典红 (默认)", en);
    }
    private void PopulateQuoteColorCombo(bool en)
    {
        PopulateColorCombo(quoteColorCombo, en ? "Follow Timer" : "跟随倒计时", en);
    }

    private void DrawColorItem(DrawItemEventArgs e, ComboBox cb, string[] presets, string currentHex, bool isQuote)
    {
        if (cb == null || e.Index < 0 || e.Index >= cb.Items.Count) return;
        e.DrawBackground();
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        Color chipCol;
        if (e.Index == 0)
        {
            if (isQuote)
            {
                try { chipCol = ColorTranslator.FromHtml(selectedColor); } catch { chipCol = Color.Red; }
            }
            else
            {
                chipCol = Color.Red;
            }
        }
        else if (e.Index >= 1 && e.Index <= 8)
        {
            chipCol = ColorTranslator.FromHtml(presets[e.Index]);
        }
        else
        {
            bool isCustom = isQuote
                ? (!string.IsNullOrEmpty(selectedQuoteColor) && GetQuoteColorIndex(selectedQuoteColor) == 9)
                : (!string.IsNullOrEmpty(selectedColor) && GetTimerColorIndex(selectedColor) == 9);
            if (isCustom)
            {
                try { chipCol = ColorTranslator.FromHtml(currentHex); } catch { chipCol = Color.White; }
            }
            else
            {
                chipCol = AppContext.Win11Accent;
            }
        }

        int circleSize = 10;
        int circleX = e.Bounds.X + 6;
        int circleY = e.Bounds.Y + (e.Bounds.Height - circleSize) / 2;

        using (SolidBrush sb = new SolidBrush(chipCol))
        using (Pen p = new Pen(Color.FromArgb(140, 140, 140), 1f))
        {
            e.Graphics.FillEllipse(sb, circleX, circleY, circleSize, circleSize);
            e.Graphics.DrawEllipse(p, circleX, circleY, circleSize, circleSize);
        }

        string text = cb.Items[e.Index].ToString();
        using (SolidBrush tb = new SolidBrush(e.ForeColor))
        using (StringFormat sf = new StringFormat { LineAlignment = StringAlignment.Center })
        {
            Rectangle textRect = new Rectangle(circleX + circleSize + 6, e.Bounds.Y, e.Bounds.Width - (circleSize + 12), e.Bounds.Height);
            e.Graphics.DrawString(text, e.Font, tb, textRect, sf);
        }
        e.DrawFocusRectangle();
    }

    private void OnDrawTimerColorItem(object sender, DrawItemEventArgs e)
    {
        DrawColorItem(e, timerColorCombo, TimerPresetColors, selectedColor, false);
    }

    private void OnDrawQuoteColorItem(object sender, DrawItemEventArgs e)
    {
        DrawColorItem(e, quoteColorCombo, QuotePresetColors, selectedQuoteColor, true);
    }

    private void ChooseCustomColor(bool isQuote)
    {
        Color initCol;
        try { initCol = isQuote ? GetEffectiveQuoteColor() : ColorTranslator.FromHtml(selectedColor); }
        catch { initCol = Color.Red; }
        using (ColorDialog cd = new ColorDialog())
        {
            cd.Color = initCol;
            cd.AllowFullOpen = true;
            cd.FullOpen = true;
            cd.AnyColor = true;
            if (cd.ShowDialog(this) == DialogResult.OK)
            {
                if (isQuote) selectedQuoteColor = ColorTranslator.ToHtml(cd.Color);
                else selectedColor = ColorTranslator.ToHtml(cd.Color);
                UpdateTimerColorPreview();
                UpdateQuoteColorPreview();
                TriggerQuotePreview();
                if (!isQuote) TriggerPreview();
            }
            else
            {
                if (isQuote) UpdateQuoteColorPreview();
                else UpdateTimerColorPreview();
            }
        }
    }

    private void OnTimerColorComboChanged(object sender, EventArgs e)
    {
        if (updatingTimerColorCombo || timerColorCombo == null) return;
        int idx = timerColorCombo.SelectedIndex;
        if (idx >= 0 && idx <= 8)
        {
            selectedColor = TimerPresetColors[idx];
            UpdateTimerColorPreview();
            UpdateQuoteColorPreview();
            TriggerQuotePreview();
            TriggerPreview();
        }
        else if (idx == 9)
        {
            ChooseCustomColor(false);
        }
    }

    private void UpdateTimerColorPreview()
    {
        if (timerColorCombo != null)
        {
            updatingTimerColorCombo = true;
            try
            {
                timerColorCombo.SelectedIndex = GetTimerColorIndex(selectedColor);
            }
            finally
            {
                updatingTimerColorCombo = false;
            }
            timerColorCombo.Invalidate();
        }
    }

    private void OnQuoteColorComboChanged(object sender, EventArgs e)
    {
        if (updatingQuoteColorCombo || quoteColorCombo == null) return;
        int idx = quoteColorCombo.SelectedIndex;
        if (idx == 0)
        {
            selectedQuoteColor = "";
            UpdateQuoteColorPreview();
            TriggerQuotePreview();
        }
        else if (idx >= 1 && idx <= 8)
        {
            selectedQuoteColor = QuotePresetColors[idx];
            UpdateQuoteColorPreview();
            TriggerQuotePreview();
        }
        else if (idx == 9)
        {
            ChooseCustomColor(true);
        }
    }

    private void UpdateQuoteColorPreview()
    {
        if (quoteColorCombo != null)
        {
            updatingQuoteColorCombo = true;
            try
            {
                quoteColorCombo.SelectedIndex = GetQuoteColorIndex(selectedQuoteColor);
            }
            finally
            {
                updatingQuoteColorCombo = false;
            }
            quoteColorCombo.Invalidate();
        }
    }

    private void UpdateDialogQuoteCard()
    {
        if (dialogQuoteList == null || dialogQuoteList.Count == 0) return;
        QuoteItem q = dialogQuoteList[dialogQuoteIndex % dialogQuoteList.Count];
        if (lblCardQuoteText != null)
        {
            lblCardQuoteText.Text = "“ " + q.Text + " ”";
        }
        if (lblCardQuoteAuthor != null)
        {
            lblCardQuoteAuthor.Text = !string.IsNullOrEmpty(q.Author) ? "—— " + q.Author : "";
        }
    }

    public void SyncNextQuote()
    {
        if (dialogQuoteList != null && dialogQuoteList.Count > 0)
        {
            dialogQuoteIndex = (dialogQuoteIndex + 1) % dialogQuoteList.Count;
            UpdateDialogQuoteCard();
        }
    }


    private void UpdateAgentNoticeToggleState(bool en)
    {
        if (btnAgentNoticeToggle == null) return;
        bool enabled = (agentNotice != null && agentNotice.Checked);
        if (enabled)
        {
            btnAgentNoticeToggle.Text = en ? "● Enabled" : "● 已开启";
            btnAgentNoticeToggle.BackColor = Color.FromArgb(232, 245, 233);
            btnAgentNoticeToggle.FlatAppearance.BorderColor = Color.FromArgb(165, 214, 167);
            btnAgentNoticeToggle.ForeColor = Color.FromArgb(27, 94, 32);
            if (lblDetectedHeader != null) lblDetectedHeader.ForeColor = AppContext.Win11TextPrimary;
            if (lblAgentDesc != null)
            {
                lblAgentDesc.Text = en ? "Alerts on background task done; click to switch back." : "智能体后台任务完成时自动弹窗提醒";
                lblAgentDesc.ForeColor = AppContext.Win11TextSecondary;
            }
        }
        else
        {
            btnAgentNoticeToggle.Text = en ? "○ Disabled" : "○ 已停用";
            btnAgentNoticeToggle.BackColor = Color.FromArgb(245, 245, 245);
            btnAgentNoticeToggle.FlatAppearance.BorderColor = Color.FromArgb(217, 217, 217);
            btnAgentNoticeToggle.ForeColor = Color.FromArgb(115, 115, 115);
            if (lblDetectedHeader != null) lblDetectedHeader.ForeColor = Color.FromArgb(120, 120, 120);
            if (lblAgentDesc != null)
            {
                lblAgentDesc.Text = en ? "Alerts on background task done; click to switch back." : "智能体后台任务完成时自动弹窗提醒";
                lblAgentDesc.ForeColor = Color.FromArgb(145, 145, 145);
            }
        }
    }

    internal void RefreshAgentCards()
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
        bool alertEnabled = agentNotice != null && agentNotice.Checked;
        List<string> agents = AppContext.DetectAgents();
        HashSet<string> running = AppContext.GetRunningAgents();
        agents.Sort((a, b) =>
        {
            bool aRun = running.Contains(a);
            bool bRun = running.Contains(b);
            if (aRun != bRun) return aRun ? -1 : 1;
            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        });

        if (lblDetectedHeader != null)
        {
            lblDetectedHeader.Text = en
                ? string.Format("AI Agent Alerts ({0})", agents.Count)
                : string.Format("智能体后台任务提醒 ({0})", agents.Count);
            lblDetectedHeader.ForeColor = alertEnabled ? AppContext.Win11TextPrimary : Color.FromArgb(120, 120, 120);
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
            if (agentScrollBar != null) agentScrollBar.Visible = false;
        }
        else
        {
            int cardY = 4;
            int cardPad = 7;
            int cardW = panelAgentCards.Width - (cardPad * 2);
            for (int i = 0; i < agents.Count; i++)
            {
                string name = agents[i];
                bool isRunning = running.Contains(name);
                Panel card = CreateAgentCard(name, isRunning, alertEnabled, cardW, en);
                card.Location = new Point(cardPad, cardY);
                AttachAgentWheel(card);
                panelAgentCards.Controls.Add(card);
                cardY += card.Height + 4;
            }

            int totalH = cardY;
            int viewportH = panelAgentCards.Height;
            if (agentScrollBar != null)
            {
                if (totalH > viewportH)
                {
                    agentScrollBar.Maximum = totalH;
                    agentScrollBar.LargeChange = viewportH;
                    agentScrollBar.Visible = true;
                    agentScrollBar.Value = Math.Min(agentScrollBar.Value, totalH - viewportH);
                }
                else
                {
                    agentScrollBar.Visible = false;
                    agentScrollBar.Value = 0;
                }
            }
        }
        UpdateCardsScroll();
        panelAgentCards.ResumeLayout(true);
    }

    private void OnAgentCardMouseWheel(object s, MouseEventArgs e)
    {
        if (agentScrollBar != null && agentScrollBar.Visible)
        {
            agentScrollBar.Value += (e.Delta < 0 ? 36 : -36);
        }
    }

    private void AttachAgentWheel(Control c)
    {
        c.MouseWheel += OnAgentCardMouseWheel;
        foreach (Control child in c.Controls)
        {
            AttachAgentWheel(child);
        }
    }

    private void UpdateCardsScroll()
    {
        if (panelAgentCards == null) return;
        int scrollOffset = agentScrollBar != null ? agentScrollBar.Value : 0;
        panelAgentCards.SuspendLayout();
        int y = 4 - scrollOffset;
        for (int i = 0; i < panelAgentCards.Controls.Count; i++)
        {
            Control c = panelAgentCards.Controls[i];
            if (c is Panel)
            {
                c.Top = y;
                y += c.Height + 4;
            }
        }
        panelAgentCards.ResumeLayout(true);
    }

    private static void OnCardPaint(object sender, PaintEventArgs pe)
    {
        Control card = (Control)sender;
        using (Pen p = new Pen(AppContext.Win11Border, 1f))
        {
            pe.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
        }
    }

    private static void OnBadgePaint(object sender, PaintEventArgs pe)
    {
        Label lbl = (Label)sender;
        object[] tag = lbl.Tag as object[];
        if (tag == null) return;
        pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        pe.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        using (SolidBrush bg = new SolidBrush((Color)tag[0]))
        using (GraphicsPath path = AppContext.GetRoundedRectPath(new Rectangle(0, 0, lbl.Width - 1, lbl.Height - 1), 3))
        {
            pe.Graphics.FillPath(bg, path);
        }
        using (SolidBrush fg = new SolidBrush((Color)tag[1]))
        using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
        {
            pe.Graphics.DrawString((string)tag[2], lbl.Font, fg, new Rectangle(0, 0, lbl.Width, lbl.Height), sf);
        }
    }

    private void OnPreviewChanged(object sender, EventArgs e) { TriggerPreview(); }
    private void OnQuoteFontComboChanged(object sender, EventArgs e) { TriggerQuotePreview(); TriggerPreview(); }
    private void OnSoundCheckChanged(object sender, EventArgs e) { soundStyle.Enabled = sound.Checked; }
    private void OnSoundStyleCommitted(object sender, EventArgs e)
    {
        if (sound.Checked)
        {
            string st = soundStyle.SelectedIndex == 1 ? "Phone" : (soundStyle.SelectedIndex == 2 ? "Alarm" : "QQ");
            AppContext.PlaySoundStyle(st);
        }
    }
    private void OnNoticeCheckChanged(object sender, EventArgs e) { noticeStyle.Enabled = notice.Checked; }
    private void OnStartupCheckedChanged(object sender, EventArgs e)
    {
        if (startup.Checked && !Value.AutoStart && AppContext.IsInEphemeralFolder(Application.ExecutablePath))
        {
            bool toEnglish = language != null && language.SelectedIndex == 1;
            string msg = toEnglish
                ? "iClock is currently located in a temporary or download folder:\r\n" + Application.ExecutablePath + "\r\n\r\nIt is recommended to move iClock.exe to a permanent folder (such as Documents or Tools) before enabling auto-start, to prevent accidental deletion during cleanup.\r\n\r\nDo you still want to enable auto-start?"
                : "检测到当前程序位于临时或下载目录：\r\n" + Application.ExecutablePath + "\r\n\r\n建议将 iClock.exe 移动到固定文件夹（如个人工具目录或文档）后再开启自启，以防清理下载文件时误删。\r\n\r\n是否仍要开启开机自启？";
            string caption = toEnglish ? "iClock Auto-Start Tip" : "iClock 开机自启提示";
            if (MessageBox.Show(this, msg, caption, MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.No)
            {
                startup.Checked = false;
            }
        }
    }
    private void OnDailyQuoteCheckChanged(object sender, EventArgs e)
    {
        quoteDesktopVisible.Enabled = dailyQuote.Checked;
        if (!dailyQuote.Checked && quoteDesktopVisible.Checked)
        {
            quoteDesktopVisible.Checked = false;
        }
        TriggerQuotePreview();
    }
    private void OnQuoteDurationChanged(object sender, EventArgs e) { TriggerQuotePreview(); }
    private void OnQuoteFontSizeChanged(object sender, EventArgs e) { if (!isSyncingQuoteFontSize) TriggerQuotePreview(); }
    private void OnQuoteFontChanged(object sender, EventArgs e)
    {
        int fi = quoteFontCombo.SelectedIndex;
        selectedQuoteFont = (fi > 0 && fi < quoteFontFamilies.Length) ? quoteFontFamilies[fi] : "";
        TriggerQuotePreview();
    }
    private void OnQuoteLayerChanged(object sender, EventArgs e) { TriggerQuotePreview(); }
    private void OnEditQuotesClick(object sender, EventArgs e) { AppContext.OpenQuotesFile(); }
    private void OnAiQuotesClick(object sender, EventArgs e)
    {
        using (AiQuoteDialog dlg = new AiQuoteDialog(Value))
        {
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                TriggerQuotePreview();
            }
        }
    }
    private void OnCancelClick(object sender, EventArgs e) { DialogResult = DialogResult.Cancel; Close(); }
    private void OnRefreshAgentsClick(object sender, EventArgs e) { RefreshAgentCards(); }
    private void OnAgentNoticeToggleClick(object sender, EventArgs e) { if (agentNotice != null) agentNotice.Checked = !agentNotice.Checked; }
    private void OnAgentNoticeCheckedChanged(object sender, EventArgs e)
    {
        UpdateAgentNoticeToggleState(language != null && language.SelectedIndex == 1);
        RefreshAgentCards();
    }
    private void OnAgentScrollBarValueChanged(object sender, EventArgs e) { UpdateCardsScroll(); }
    private void OnMcpCopyPromptClick(object sender, EventArgs e)
    {
        try
        {
            bool en = Value != null && Value.Language == "en";
            string prompt = McpConfigHelper.GenerateAgentPrompt(en);
            Clipboard.SetText(prompt);
            btnMcpCopyJson.Text = en ? "✓ Copied!" : "✓ 已复制!";
            System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
            t.Interval = 2000;
            t.Tick += delegate
            {
                t.Stop(); t.Dispose();
                if (!btnMcpCopyJson.IsDisposed)
                    btnMcpCopyJson.Text = (Value != null && Value.Language == "en") ? "Copy Prompt" : "复制接入提示词";
            };
            t.Start();
            MessageBox.Show(this, en
                ? "Agent MCP setup prompt copied to clipboard!\n\nSend this prompt directly to any AI agent (Antigravity, Cursor, Claude, Windsurf, Cline, etc.) to let it auto-configure MCP for you."
                : "已将智能体 MCP 接入提示词复制到剪贴板！\n\n直接将这段提示词发送给任意 AI 智能体（如 Antigravity、Cursor、Claude、Windsurf、Cline 等），智能体即可为您自动配置接入。",
                "iClock", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ((Value != null && Value.Language == "en") ? "Failed to copy to clipboard: " : "复制到剪贴板失败：") + ex.Message, "iClock", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
    private void OnMcpDemoClick(object sender, EventArgs e)
    {
        bool en = Value != null && Value.Language == "en";
        using (AskHumanDialog demo = new AskHumanDialog(
            Value,
            en ? "Critical action detected: Allow pushing code and triggering auto-deployment?" : "检测到即将执行关键操作：是否允许覆盖 main 分支并触发部署？",
            en ? new string[] { "Allow & Deploy", "Reject / Cancel" } : new string[] { "允许并部署", "拒绝操作" },
            0, null))
        {
            demo.ShowDialog(this);
        }
    }
    private void OnMcpDemoNotifyClick(object sender, EventArgs e)
    {
        bool en = Value != null && Value.Language == "en";
        NoticeDialog notifyDemo = new NoticeDialog(
            Value, DateTime.Now, "Cursor", IntPtr.Zero, null,
            en ? "Background code refactoring completed (elapsed 2m 45s)" : "后台代码重构任务已完成（耗时 2分45秒）"
        );
        notifyDemo.Show();
    }
    private void OnMcpDemoQuoteClick(object sender, EventArgs e)
    {
        bool en = Value != null && Value.Language == "en";
        DailyQuoteDialog quoteDemo = new DailyQuoteDialog(
            Value,
            en ? "Refactoring modules & running test suite, progress 85%..." : "正在执行代码重构与模块自检，当前进度 85%...",
            "Cursor Agent"
        );
        quoteDemo.Show();
        AppContext.ForceTopMost(quoteDemo);
    }
    private static void OnMcpBoxPaint(object sender, PaintEventArgs pe)
    {
        Control pnl = (Control)sender;
        using (Pen p = new Pen(AppContext.Win11Border, 1f))
            pe.Graphics.DrawRectangle(p, 0, 0, pnl.Width - 1, pnl.Height - 1);
        using (Pen divPen = new Pen(Color.FromArgb(238, 240, 244), 1f))
        {
            pe.Graphics.DrawLine(divPen, 10, 47, pnl.Width - 10, 47);
            pe.Graphics.DrawLine(divPen, 10, 94, pnl.Width - 10, 94);
        }
    }

    private static void OnMcpBadgePaint(object sender, PaintEventArgs pe)
    {
        Label lbl = (Label)sender;
        AppContext.PaintMcpBadge(pe.Graphics, lbl.ClientRectangle, lbl.Text);
    }

    private Panel CreateAgentCard(string name, bool isRunning, bool alertEnabled, int width, bool en)
    {
        Panel card = new Panel();
        card.Size = new Size(width, 32);
        card.BackColor = Color.White;
        card.Paint += OnCardPaint;

        Label lblDot = new Label();
        lblDot.Text = "●";
        lblDot.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
        lblDot.ForeColor = !alertEnabled
            ? Color.FromArgb(180, 180, 180)
            : (isRunning ? Color.FromArgb(16, 124, 65) : Color.FromArgb(140, 145, 155));
        lblDot.Location = new Point(8, 7);
        lblDot.Size = new Size(16, 18);
        card.Controls.Add(lblDot);

        Label lblBadge = new Label();
        string badgeText;
        Color bBg, bFg;
        if (!alertEnabled)
        {
            badgeText = en ? "Paused" : "未监听";
            bFg = Color.FromArgb(140, 140, 140);
            bBg = Color.FromArgb(242, 242, 242);
        }
        else if (isRunning)
        {
            badgeText = en ? "Running" : "运行中";
            bFg = Color.FromArgb(27, 94, 32);
            bBg = Color.FromArgb(232, 245, 233);
        }
        else
        {
            badgeText = en ? "Ready" : "已就绪";
            bFg = Color.FromArgb(90, 90, 90);
            bBg = Color.FromArgb(243, 244, 246);
        }
        lblBadge.Text = badgeText;
        lblBadge.Tag = new object[] { bBg, bFg, badgeText };
        lblBadge.Font = AppContext.GetUiFont(en ? "en" : "zh", 8.5f);
        lblBadge.Size = new Size(en ? 66 : 56, 20);
        lblBadge.Location = new Point(card.Width - lblBadge.Width - 8, 6);
        lblBadge.Paint += OnBadgePaint;
        card.Controls.Add(lblBadge);

        int leftLimit = lblBadge.Left;
        bool isMcp = McpConfigHelper.IsAgentMcpConfigured(name);
        if (isMcp)
        {
            Label lblMcp = new Label();
            lblMcp.Text = "⚡ MCP";
            lblMcp.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
            lblMcp.Size = new Size(54, 20);
            lblMcp.Location = new Point(lblBadge.Left - lblMcp.Width - 6, 6);
            lblMcp.Paint += OnMcpBadgePaint;
            if (toolTip == null) toolTip = new ToolTip();
            toolTip.SetToolTip(lblMcp, en ? "MCP connected: Agent can call desktop decisions & timers" : "已接入 iClock MCP 协议，支持桌面决策门禁与状态投射");
            card.Controls.Add(lblMcp);
            leftLimit = lblMcp.Left;
        }

        Label lblName = new Label();
        lblName.Text = name;
        lblName.Font = AppContext.GetUiFont(en ? "en" : "zh", 9f, FontStyle.Regular);
        lblName.ForeColor = alertEnabled ? AppContext.Win11TextPrimary : AppContext.Win11TextSecondary;
        lblName.Location = new Point(26, 6);
        lblName.AutoEllipsis = true;
        lblName.MaximumSize = new Size(Math.Max(50, leftLimit - 26 - 6), 20);
        lblName.AutoSize = true;
        card.Controls.Add(lblName);

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
        btnTabQuote.Text = toEnglish ? "Daily Quotes" : "每日格言";
        btnTabAgent.Text = toEnglish ? "AI Collaboration" : "AI 协作";
        UpdateTabButtons();

        lblMinutes.Text = toEnglish ? "Duration (minutes)" : "倒计时（分钟）";
        lblSize.Text = toEnglish ? "Text size" : "文字大小";
        lblFont.Text = toEnglish ? "Font style" : "字体样式";
        lblColor.Text = toEnglish ? "Text color" : "文字颜色";
        lblFormat.Text = toEnglish ? "Display format" : "显示格式";
        lblEndMsg.Text = toEnglish ? "End message" : "结束时弹出消息";
        lblHotkey.Text = toEnglish ? "Start/pause hotkey" : "启动/暂停快捷键";
        lblLanguage.Text = toEnglish ? "Interface language" : "界面语言";
        startup.Text = toEnglish ? "Start with Windows" : "开机自启动";
        sound.Text = toEnglish ? "Play sound at end" : "结束时声音提醒";
        notice.Text = toEnglish ? "End notification" : "结束时系统通知";

        dailyQuote.Text = toEnglish ? "Enable daily quotes" : "开启每日格言";
        quoteDesktopVisible.Text = toEnglish ? "Show on desktop" : "桌面悬浮显示";
        int quoteChkW = Math.Min(152, TextRenderer.MeasureText(quoteDesktopVisible.Text, quoteDesktopVisible.Font).Width + 28);
        quoteDesktopVisible.SetBounds(362 - quoteChkW, 172, quoteChkW, 24);
        lblQuoteDuration.Text = toEnglish ? "Duration (seconds)" : "显示时长（秒）";
        lblQuoteSize.Text = toEnglish ? "Text size" : "文字大小";
        lblQuoteFont.Text = toEnglish ? "Font style" : "字体样式";
        lblQuoteColor.Text = toEnglish ? "Text color" : "文字颜色";
        lblQuoteLayer.Text = toEnglish ? "Window level" : "常驻层级";
        int qlSel = quoteLayerCombo != null ? quoteLayerCombo.SelectedIndex : 0;
        if (quoteLayerCombo != null)
        {
            quoteLayerCombo.Items.Clear();
            quoteLayerCombo.Items.AddRange(toEnglish
                ? new object[] { "Desktop Layer (Default)", "Always on Top" }
                : new object[] { "桌面背景置底 (默认)", "始终置顶显示" });
            quoteLayerCombo.SelectedIndex = qlSel >= 0 ? qlSel : 0;
        }
        lblQuoteQuotes.Text = toEnglish ? "Quote library" : "名言词库";
        btnEditQuotes.Text = toEnglish ? "Edit Quotes" : "自定义…";
        btnEditQuotes.Font = AppContext.GetUiFont(toEnglish ? "en" : "zh", 9f);
        if (btnAiQuotes != null)
        {
            btnAiQuotes.Text = toEnglish ? "✨ AI Quotes" : "✨ AI 扩充";
            btnAiQuotes.Font = AppContext.GetUiFont(toEnglish ? "en" : "zh", 9f);
        }
        if (lblQuoteTip != null)
        {
            lblQuoteTip.HighlightWords = toEnglish
                ? new string[] { "Alt + Ctrl + wheel", "Alt + drag" }
                : new string[] { "Alt + Ctrl + 滚轮", "Alt + 拖拽" };
            lblQuoteTip.Font = AppContext.GetUiFont(toEnglish ? "en" : "zh", 8f);
            lblQuoteTip.Text = toEnglish
                ? "Tip: Hold Alt + drag to move quote, Alt + Ctrl + wheel to scale font."
                : "提示：在桌面格言文字上按住 Alt + 拖拽，Alt + Ctrl + 滚轮缩放字号。";
        }
        if (toolTip != null)
        {
            if (btnNextQuote != null) toolTip.SetToolTip(btnNextQuote, toEnglish ? "Next quote (↻)" : "换一句 (↻)");
            toolTip.SetToolTip(btnEditQuotes, toEnglish ? "Open and edit quotes.txt in notepad" : "在记事本中自定义编辑名言 (quotes.txt)");
            if (btnAiQuotes != null) toolTip.SetToolTip(btnAiQuotes, toEnglish ? "Generate quotes with AI Agent / MCP" : "通过 AI 智能体 / MCP 批量创作并扩充词库");
            if (quoteDesktopVisible != null) toolTip.SetToolTip(quoteDesktopVisible, toEnglish ? "Display quote permanently on desktop; hold Alt + drag to move, drag edge to resize" : "在桌面上常驻显示格言；在桌面格言文字上按住 Alt + 拖拽调整位置，拖动右边缘调节宽度");
        }
        if (lblCardQuoteText != null)
        {
            lblCardQuoteText.Font = AppContext.GetUiFont(toEnglish ? "en" : "zh", 9.5f, FontStyle.Regular);
        }
        if (lblCardQuoteAuthor != null)
        {
            lblCardQuoteAuthor.Font = AppContext.GetUiFont(toEnglish ? "en" : "zh", 8.5f, FontStyle.Italic);
        }

        UpdateAgentNoticeToggleState(toEnglish);
        if (btnRefreshAgents != null) btnRefreshAgents.Text = toEnglish ? "Re-scan" : "重新检测";
        if (lblMcpTitle != null) lblMcpTitle.Text = toEnglish ? "MCP Direct Connection (3 Tools)" : "MCP 协议直连 (3项核心能力)";
        if (btnMcpCopyJson != null) btnMcpCopyJson.Text = toEnglish ? "Copy Prompt" : "复制接入提示词";
        if (btnMcpDemo != null) btnMcpDemo.Text = toEnglish ? "Demo" : "演示";
        if (btnMcpDemoNotify != null) btnMcpDemoNotify.Text = toEnglish ? "Demo" : "演示";
        if (btnMcpDemoQuote != null) btnMcpDemoQuote.Text = toEnglish ? "Demo" : "演示";
        if (lblTool1Name != null) { lblTool1Name.Text = toEnglish ? "Flash Approval HUD (ask_human)" : "极速桌面授权门禁 (Flash Approval HUD)"; lblTool1Name.Font = AppContext.GetUiFont(toEnglish ? "en" : "zh", 9f, FontStyle.Regular); }
        if (lblTool1Desc != null) { lblTool1Desc.Text = toEnglish ? "Human-in-the-loop approval HUD for critical actions" : "关键高危操作置顶人机决策门禁 (支持 1/2/Enter/Esc 盲打)"; lblTool1Desc.Font = AppContext.GetUiFont(toEnglish ? "en" : "zh", 8.2f); }
        if (lblTool2Name != null) { lblTool2Name.Text = toEnglish ? "Task Notification (notify)" : "任务完成提醒"; lblTool2Name.Font = AppContext.GetUiFont(toEnglish ? "en" : "zh", 9f, FontStyle.Regular); }
        if (lblTool2Desc != null) { lblTool2Desc.Text = toEnglish ? "TopMost modal alert & sound on background task done" : "后台耗时任务完成时全向弹窗与声音唤醒"; lblTool2Desc.Font = AppContext.GetUiFont(toEnglish ? "en" : "zh", 8.2f); }
        if (lblTool3Name != null) { lblTool3Name.Text = toEnglish ? "Desktop Status (quote)" : "桌面状态投射"; lblTool3Name.Font = AppContext.GetUiFont(toEnglish ? "en" : "zh", 9f, FontStyle.Regular); }
        if (lblTool3Desc != null) { lblTool3Desc.Text = toEnglish ? "Project task goals, thoughts or working status onto desktop" : "智能体在屏幕投射当前进展与工作便签"; lblTool3Desc.Font = AppContext.GetUiFont(toEnglish ? "en" : "zh", 8.2f); }
        if (lblAgentCliTip != null)
        {
            lblAgentCliTip.HighlightWords = new string[] { "iclock --install-cli", "iclock -h" };
            lblAgentCliTip.Font = AppContext.GetUiFont(toEnglish ? "en" : "zh", 8f);
            lblAgentCliTip.Text = toEnglish
                ? "Tip: Run iclock -h for commands, iclock --install-cli to add to PATH."
                : "提示：终端运行 iclock -h 查看指令，iclock --install-cli 写入 PATH。";
        }
        if (panelQuoteCard != null) panelQuoteCard.Invalidate();

        saveBtn.Text = toEnglish ? "Save" : "保存";
        cancelBtn.Text = toEnglish ? "Cancel" : "取消";

        UpdateTabButtons();
        updatingTimerColorCombo = true;
        try
        {
            int tcSel = timerColorCombo != null ? timerColorCombo.SelectedIndex : 0;
            PopulateTimerColorCombo(toEnglish);
            if (timerColorCombo != null) timerColorCombo.SelectedIndex = tcSel >= 0 ? tcSel : 0;
        }
        finally
        {
            updatingTimerColorCombo = false;
        }
        UpdateTimerColorPreview();

        updatingQuoteColorCombo = true;
        try
        {
            int qcSel = quoteColorCombo != null ? quoteColorCombo.SelectedIndex : 0;
            PopulateQuoteColorCombo(toEnglish);
            if (quoteColorCombo != null) quoteColorCombo.SelectedIndex = qcSel >= 0 ? qcSel : 0;
        }
        finally
        {
            updatingQuoteColorCombo = false;
        }
        UpdateQuoteColorPreview();
        if (activeTab == 2) RefreshAgentCards();
        TriggerQuotePreview();

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

        int qfSel = quoteFontCombo.SelectedIndex;
        quoteFontCombo.Items.Clear();
        quoteFontCombo.Items.AddRange(toEnglish
            ? new object[] { "Follow Timer (Default)", "Microsoft YaHei", "KaiTi", "SimSun", "Segoe UI" }
            : new object[] { "跟随倒计时 (默认)", "微软雅黑", "楷体", "宋体", "Segoe UI" });
        quoteFontCombo.SelectedIndex = qfSel >= 0 ? qfSel : 0;

        int ssSel = soundStyle.SelectedIndex;
        soundStyle.Items.Clear();
        soundStyle.Items.AddRange(toEnglish
            ? new object[] { "QQ Message (Classic)", "Telephone Ring (Classic)", "Alarm Ring (Strong)" }
            : new object[] { "QQ 经典提示 (嘀嘀)", "经典电话铃 (叮铃铃)", "醒目闹铃 (强力)" });
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
        UpdateInputPadding(quoteDuration);
        UpdateInputPadding(quoteFontSize);
        UpdateInputPadding(endMessage);
        UpdateInputPadding(hotkeyBox);
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
        draft.HotkeyModifiers = hotkeyModifiers;
        draft.HotkeyKey = hotkeyKey;
        draft.AutoStart = startup != null ? startup.Checked : Value.AutoStart;
        draft.Format = (format != null && format.SelectedIndex == 1) ? "MM:SS" : ((format != null && format.SelectedIndex == 2) ? "Chinese" : "HH:MM:SS");
        draft.Language = (language != null && language.SelectedIndex == 1) ? "en" : "zh";
        draft.EndSound = sound != null ? sound.Checked : Value.EndSound;
        draft.EndSoundStyle = (soundStyle != null && soundStyle.SelectedIndex == 1) ? "Phone" : ((soundStyle != null && soundStyle.SelectedIndex == 2) ? "Alarm" : "QQ");
        draft.EndNotice = notice != null ? notice.Checked : Value.EndNotice;
        draft.NoticeStyle = (noticeStyle != null && noticeStyle.SelectedIndex == 1) ? "Pulse" : "Popup";
        draft.EndMessage = endMessage != null ? endMessage.Text.Trim() : Value.EndMessage;
        draft.FullscreenDnd = Value.FullscreenDnd;
        draft.AgentNotice = agentNotice != null ? agentNotice.Checked : Value.AgentNotice;
        draft.DailyQuote = dailyQuote != null ? dailyQuote.Checked : Value.DailyQuote;
        draft.QuoteDuration = quoteDuration != null ? (int)quoteDuration.Value : Value.QuoteDuration;
        draft.QuoteColor = selectedQuoteColor;
        draft.QuoteFont = selectedQuoteFont;
        draft.QuoteX = Value.QuoteX;
        draft.QuoteY = Value.QuoteY;
        draft.QuoteWidth = Value.QuoteWidth;
        draft.QuoteFontSize = quoteFontSize != null ? (float)quoteFontSize.Value : Value.QuoteFontSize;
        draft.QuoteDesktopVisible = quoteDesktopVisible != null ? quoteDesktopVisible.Checked : Value.QuoteDesktopVisible;
        draft.QuotePinTopMost = quoteLayerCombo != null && quoteLayerCombo.SelectedIndex == 1;
        draft.LastQuoteDate = Value.LastQuoteDate;
        draft.X = Value.X;
        draft.Y = Value.Y;
        return draft;
    }

    private void TriggerPreview()
    {
        if (PreviewRequested != null && minutes != null && size != null && fontCombo != null && format != null && language != null && timerColorCombo != null)
        {
            PreviewRequested(GetCurrentDraftSettings());
        }
    }

    private void TriggerQuotePreview()
    {
        if (QuotePreviewRequested != null && quoteDesktopVisible != null)
        {
            QuotePreviewRequested(GetCurrentDraftSettings(), activeTab == 1 && quoteDesktopVisible.Checked);
        }
    }

    private Label AddLabel(string t, int x, int y) { Label l = new Label(); l.Text = t; l.ForeColor = AppContext.Win11TextPrimary; l.SetBounds(x, y, 148, 24); l.TextAlign = ContentAlignment.MiddleLeft; Controls.Add(l); return l; }
    private NumericUpDown AddNumber(int v, int min, int max, int x, int y) { NumericUpDown n = new NumericUpDown(); n.Minimum = min; n.Maximum = max; n.Value = Math.Min(max, Math.Max(min, v)); n.SetBounds(x, y, 188, 28); ApplyInputPadding(n); Controls.Add(n); return n; }
    private CheckBox AddCheck(string t, bool v, int x, int y) { CheckBox c = new CheckBox(); c.Text = t; c.Checked = v; c.ForeColor = AppContext.Win11TextPrimary; c.SetBounds(x, y, 344, 24); Controls.Add(c); return c; }
    private ComboBox AddCombo(int y) { ComboBox c = new ComboBox(); c.DropDownStyle = ComboBoxStyle.DropDownList; c.DrawMode = DrawMode.OwnerDrawFixed; c.ItemHeight = 22; c.SetBounds(172, y, 188, 28); Controls.Add(c); return c; }
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
    private void OnDrawQuoteFontItem(object sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || quoteFontCombo == null || e.Index >= quoteFontCombo.Items.Count) return;
        string family = (e.Index > 0 && e.Index < quoteFontFamilies.Length) ? quoteFontFamilies[e.Index] : "Segoe UI";
        Font f = null;
        try { f = new Font(family, e.Font.Size, FontStyle.Regular, e.Font.Unit); } catch { }
        DrawComboItem(e, quoteFontCombo.Items[e.Index].ToString(), f ?? e.Font);
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
        Value = GetCurrentDraftSettings();
        DialogResult = DialogResult.OK; Close();
    }

    public void SetDesktopQuoteChecked(bool chk)
    {
        if (quoteDesktopVisible != null && quoteDesktopVisible.Checked != chk)
        {
            quoteDesktopVisible.Checked = chk;
        }
    }

    public void SyncQuoteFontSize(float sz)
    {
        if (quoteFontSize != null && sz >= 8.0f && sz <= 24.0f)
        {
            decimal dec = (decimal)sz;
            if (quoteFontSize.Value != dec)
            {
                isSyncingQuoteFontSize = true;
                try
                {
                    quoteFontSize.Value = dec;
                    Value.QuoteFontSize = sz;
                }
                finally
                {
                    isSyncingQuoteFontSize = false;
                }
            }
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (activeTab == 2 && panelAgentBox != null && panelAgentBox.Visible && agentScrollBar != null && agentScrollBar.Visible)
        {
            Point pt = panelAgentBox.PointToClient(Cursor.Position);
            if (panelAgentBox.ClientRectangle.Contains(pt))
            {
                agentScrollBar.Value += (e.Delta < 0 ? 36 : -36);
                return;
            }
        }
        base.OnMouseWheel(e);
    }
}

internal sealed class NoticeDialog : Form
{
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
    private Font msgFont;

    public CliNoticeDialog(Settings s, string message)
    {
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


internal static class McpConfigHelper
{
    public static string GetMcpExePath()
    {
        string dir = "";
        try { dir = Path.GetDirectoryName(typeof(McpConfigHelper).Assembly.Location); } catch { }
        if (string.IsNullOrEmpty(dir)) dir = AppDomain.CurrentDomain.BaseDirectory;
        string comPath = Path.Combine(dir, "iclock.com");
        if (File.Exists(comPath)) return comPath;
        string exePath = Path.Combine(dir, "iClock.exe");
        if (File.Exists(exePath)) return exePath;
        return "iclock";
    }

    public static string GetCursorConfigPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cursor", "mcp.json");
    }

    public static string GetClaudeConfigPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Claude", "claude_desktop_config.json");
    }

    public static string GetWindsurfConfigPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codeium", "windsurf", "mcp_config.json");
    }

    public static string GetAntigravityConfigPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gemini", "config", "mcp_config.json");
    }

    public static string GetClineConfigPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Code", "User", "globalStorage", "saoudrizwan.claude-dev", "settings", "cline_mcp_settings.json");
    }

    public static string GetRooConfigPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Code", "User", "globalStorage", "rooveterinaryinc.roo-cline", "settings", "cline_mcp_settings.json");
    }

    public static string GetConfigPathForAgent(string agentName)
    {
        if (string.IsNullOrEmpty(agentName)) return null;
        string lower = agentName.ToLowerInvariant();
        if (lower.Contains("cursor")) return GetCursorConfigPath();
        if (lower.Contains("claude")) return GetClaudeConfigPath();
        if (lower.Contains("windsurf")) return GetWindsurfConfigPath();
        if (lower.Contains("antigravity")) return GetAntigravityConfigPath();
        if (lower.Contains("cline")) return GetClineConfigPath();
        if (lower.Contains("roo")) return GetRooConfigPath();
        return null;
    }

    public static bool IsAgentMcpConfigured(string agentName)
    {
        string path = GetConfigPathForAgent(agentName);
        return IsConfigured(path);
    }

    public static bool IsConfigured(string configFile)
    {
        if (string.IsNullOrEmpty(configFile) || !File.Exists(configFile)) return false;
        try
        {
            string text = File.ReadAllText(configFile, Encoding.UTF8);
            if (string.IsNullOrEmpty(text)) return false;
            JavaScriptSerializer jss = new JavaScriptSerializer();
            Dictionary<string, object> root = jss.Deserialize<Dictionary<string, object>>(text);
            if (root != null && root.ContainsKey("mcpServers"))
            {
                Dictionary<string, object> servers = root["mcpServers"] as Dictionary<string, object>;
                if (servers != null && servers.ContainsKey("iclock")) return true;
            }
        }
        catch { }
        return false;
    }

    public static string GenerateAgentPrompt(bool en)
    {
        string exePath = GetMcpExePath();
        if (en)
        {
            return "Please configure iClock (desktop clock & status collaboration tool) as an MCP server for this environment.\r\n\r\n"
                + "[Configuration]\r\n"
                + "- Server Name: iclock\r\n"
                + "- Command: " + exePath + "\r\n"
                + "- Args: [\"--mcp\"]\r\n\r\n"
                + "[Core Capabilities]\r\n"
                + "Once connected, you will have access to iClock desktop tools:\r\n"
                + "1. iclock_ask_human: Trigger a top-most modal gate for human-in-the-loop approvals on critical decisions;\r\n"
                + "2. iclock_notify: Pop up a desktop alert and sound notification when background tasks complete;\r\n"
                + "3. iclock_quote: Project your current progress, task goal, or status sticky note directly onto my desktop.\r\n\r\n"
                + "[Instructions]\r\n"
                + "1. Check current environment's MCP config file (e.g. Cursor's mcp.json, Claude Desktop config, or current workspace MCP config) and merge this server (preserve existing servers).\r\n"
                + "2. Once saved, prompt me to reload the window (e.g. 'Developer: Reload Window' in Cursor) so the MCP server takes effect.\r\n"
                + "3. After reload, immediately call the iclock_notify tool to send a desktop greeting (e.g. 'iClock MCP connected successfully!') to verify connectivity.";
        }
        else
        {
            return "请帮我将本机的 iClock (桌面时钟与工作状态协同工具) 接入为当前环境的 MCP Server。\r\n\r\n"
                + "【配置参数】\r\n"
                + "- 服务名称：iclock\r\n"
                + "- 启动命令：" + exePath + "\r\n"
                + "- 启动参数：[\"--mcp\"]\r\n\r\n"
                + "【能力说明】\r\n"
                + "接入后你将拥有 iClock 提供的桌面协作工具：\r\n"
                + "1. iclock_ask_human: 遇到关键决策、高危操作或多选方案时，呼出桌面置顶阻断式人工审批卡片；\r\n"
                + "2. iclock_notify: 耗时或后台任务完成时，呼出桌面模态弹窗与提示音唤醒我；\r\n"
                + "3. iclock_quote: 在我的桌面实时投射你当前的任务进展、思考要点或状态便签。\r\n\r\n"
                + "【操作步骤】\r\n"
                + "1. 请检查当前环境的 MCP 配置文件（例如 Claude Desktop 的 claude_desktop_config.json、Cursor 的 mcp.json 或当前项目/全局 MCP 配置），将上述 iclock 服务合并进去（保留我已有的其他服务）。\r\n"
                + "2. 写入配置后，请提醒我重载窗口生效（如 Cursor 执行 Reload Window 或重启客户端）。\r\n"
                + "3. 重载后，请立即调用 iclock_notify 工具向我发送一条测试通知（例如：'iClock MCP 协同接入成功！'），以验证桌面联通性。";
        }
    }

    public static string GenerateJsonSnippet(bool absolutePath)
    {
        string cmd = absolutePath ? GetMcpExePath() : "iclock";
        string escapedCmd = cmd.Replace("\\", "\\\\");
        return "{\r\n  \"mcpServers\": {\r\n    \"iclock\": {\r\n      \"command\": \"" + escapedCmd + "\",\r\n      \"args\": [\r\n        \"--mcp\"\r\n      ]\r\n    }\r\n  }\r\n}";
    }
}


internal sealed class AskHumanDialog : Form
{
    private string resultFilePath;
    private bool resultWritten;
    private int timeoutRemaining;
    private System.Windows.Forms.Timer timeoutTimer;
    private Label lblTimeout;
    private bool nonIntrusive;

    protected override bool ShowWithoutActivation
    {
        get { return nonIntrusive; }
    }

    public AskHumanDialog(Settings s, string question, string[] options, int timeoutSeconds, string resultFile)
        : this(s, question, options, timeoutSeconds, resultFile, false)
    {
    }

    public AskHumanDialog(Settings s, string question, string[] options, int timeoutSeconds, string resultFile, bool nonIntrusiveMode)
    {
        resultFilePath = resultFile;
        nonIntrusive = nonIntrusiveMode;
        timeoutRemaining = timeoutSeconds > 0 ? timeoutSeconds : 0;
        bool hasTimeout = timeoutSeconds > 0;
        string lang = s != null ? s.Language : "zh";
        bool en = lang == "en";

        Font = AppContext.GetUiFont(lang, 9.5f);
        BackColor = AppContext.Win11Bg;
        AppContext.ApplyModernWindowStyle(this);
        Text = en ? "Flash Approval HUD (AI Agent Request) - iClock" : "极速桌面授权门禁 (Flash Approval HUD) - AI 智能体请示";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        TopMost = true;

        bool hasOptions = options != null && options.Length > 0;
        int optCount = hasOptions ? options.Length : 0;
        int dialogW = 410;

        int qHeight = 44;
        try
        {
            Size measured = TextRenderer.MeasureText(question ?? "", AppContext.GetUiFont(lang, 9.5f), new Size(dialogW - 40, int.MaxValue), TextFormatFlags.WordBreak);
            if (measured.Height > 40) qHeight = Math.Min(120, measured.Height + 6);
        }
        catch { }

        Label lblBadge = new Label();
        lblBadge.Text = en ? "⚡ Flash Approval HUD" : "⚡ 极速桌面授权门禁 (Flash Approval HUD)";
        lblBadge.Font = AppContext.GetUiFont(lang, 10.5f, FontStyle.Bold);
        lblBadge.ForeColor = AppContext.Win11Accent;
        lblBadge.SetBounds(20, 14, dialogW - 40, 22);
        Controls.Add(lblBadge);

        Label lblQuestion = new Label();
        lblQuestion.Text = question ?? "";
        lblQuestion.Font = AppContext.GetUiFont(lang, 9.5f, FontStyle.Regular);
        lblQuestion.ForeColor = AppContext.Win11TextPrimary;
        lblQuestion.SetBounds(20, 38, dialogW - 40, qHeight);
        Controls.Add(lblQuestion);

        int currentY = 40 + qHeight + 8;
        Button firstOptionBtn = null;

        if (hasOptions)
        {
            for (int i = 0; i < options.Length; i++)
            {
                int optIdx = i;
                string optText = options[i];
                string displayOpt = optText;
                string prefix = (i + 1).ToString();
                if (!displayOpt.StartsWith(prefix + ".") && !displayOpt.StartsWith(prefix + "、") && !displayOpt.StartsWith(prefix + " "))
                {
                    displayOpt = prefix + ". " + displayOpt;
                }

                Button btn = new Button();
                btn.Text = displayOpt;
                btn.Font = AppContext.GetUiFont(lang, 9f);
                btn.SetBounds(20, currentY, dialogW - 40, 32);
                if (i == 0)
                {
                    AppContext.StylePrimaryButton(btn);
                    firstOptionBtn = btn;
                }
                else
                {
                    AppContext.StyleSecondaryButton(btn);
                }
                btn.Click += delegate
                {
                    WriteResult(optText);
                    Close();
                };
                Controls.Add(btn);
                currentY += 38;
            }

            if (firstOptionBtn != null)
            {
                AcceptButton = firstOptionBtn;
            }
        }
        else
        {
            TextBox txtInput = new TextBox();
            txtInput.Font = AppContext.GetUiFont(lang, 9.5f);
            txtInput.SetBounds(20, currentY, dialogW - 40, 26);
            Controls.Add(txtInput);
            currentY += 34;

            Button btnSubmit = new Button();
            btnSubmit.Text = en ? "Submit (Enter)" : "提交 (Enter)";
            btnSubmit.SetBounds(dialogW - 130, currentY, 110, 28);
            AppContext.StylePrimaryButton(btnSubmit);
            btnSubmit.Click += delegate
            {
                WriteResult(txtInput.Text);
                Close();
            };
            Controls.Add(btnSubmit);

            Button btnCancel = new Button();
            btnCancel.Text = en ? "Cancel (Esc)" : "取消 (Esc)";
            btnCancel.SetBounds(dialogW - 220, currentY, 80, 28);
            AppContext.StyleSecondaryButton(btnCancel);
            btnCancel.Click += delegate
            {
                WriteResult("[用户取消 / Cancelled]");
                Close();
            };
            Controls.Add(btnCancel);

            AcceptButton = btnSubmit;
            CancelButton = btnCancel;
            currentY += 34;
        }

        Label lblShortcut = new Label();
        lblShortcut.Text = en 
            ? (hasOptions ? "Shortcuts: Enter / 1-9 to select, Esc to cancel" : "Shortcuts: Enter to submit, Esc to cancel")
            : (hasOptions ? "快捷键：Enter / 1-9 极速选择，Esc 取消" : "快捷键：Enter 提交，Esc 取消");
        lblShortcut.Font = AppContext.GetUiFont(lang, 8f);
        lblShortcut.ForeColor = AppContext.Win11TextSecondary;
        lblShortcut.TextAlign = ContentAlignment.MiddleCenter;
        lblShortcut.SetBounds(20, currentY + 2, dialogW - 40, 18);
        Controls.Add(lblShortcut);
        currentY += 22;

        if (hasTimeout)
        {
            lblTimeout = new Label();
            lblTimeout.Text = en ? "Auto-timeout in " + timeoutRemaining + "s" : "自动超时：" + timeoutRemaining + " 秒";
            lblTimeout.Font = AppContext.GetUiFont(lang, 8f);
            lblTimeout.ForeColor = AppContext.Win11TextSecondary;
            lblTimeout.TextAlign = ContentAlignment.MiddleRight;
            lblTimeout.SetBounds(20, currentY, dialogW - 40, 18);
            Controls.Add(lblTimeout);
            currentY += 20;

            timeoutTimer = new System.Windows.Forms.Timer();
            timeoutTimer.Interval = 1000;
            timeoutTimer.Tick += delegate
            {
                timeoutRemaining--;
                if (lblTimeout != null && !lblTimeout.IsDisposed)
                {
                    lblTimeout.Text = en ? "Auto-timeout in " + timeoutRemaining + "s" : "自动超时：" + timeoutRemaining + " 秒";
                }
                if (timeoutRemaining <= 0)
                {
                    timeoutTimer.Stop();
                    WriteResult("[超时未响应 / Timeout]");
                    Close();
                }
            };
            timeoutTimer.Start();
        }

        if (nonIntrusive)
        {
            Label lblHint = new Label();
            lblHint.Text = en ? "• Host active: Floating without focus interruption" : "• 宿主前台活跃中：温和置顶呈现，不中断代码输入";
            lblHint.Font = AppContext.GetUiFont(lang, 8f);
            lblHint.ForeColor = AppContext.Win11TextSecondary;
            lblHint.TextAlign = ContentAlignment.MiddleCenter;
            lblHint.SetBounds(20, currentY, dialogW - 40, 16);
            Controls.Add(lblHint);
            currentY += 18;
        }

        ClientSize = new Size(dialogW, currentY + 12);

        KeyPreview = true;
        KeyDown += delegate(object sender, KeyEventArgs ke)
        {
            if (hasOptions)
            {
                if (ke.KeyCode == Keys.Enter && options.Length > 0)
                {
                    WriteResult(options[0]);
                    Close();
                    ke.Handled = true;
                    return;
                }
                if ((ke.KeyCode >= Keys.D1 && ke.KeyCode <= Keys.D9) ||
                    (ke.KeyCode >= Keys.NumPad1 && ke.KeyCode <= Keys.NumPad9))
                {
                    int k = (ke.KeyCode >= Keys.NumPad1) 
                        ? (ke.KeyCode - Keys.NumPad1) 
                        : (ke.KeyCode - Keys.D1);
                    if (k >= 0 && k < options.Length)
                    {
                        WriteResult(options[k]);
                        Close();
                        ke.Handled = true;
                        return;
                    }
                }
            }
            if (ke.KeyCode == Keys.Escape)
            {
                WriteResult("[用户取消 / Cancelled]");
                Close();
                ke.Handled = true;
                return;
            }
        };

        FormClosed += delegate
        {
            if (timeoutTimer != null) { timeoutTimer.Stop(); timeoutTimer.Dispose(); timeoutTimer = null; }
            if (!resultWritten)
            {
                WriteResult("[用户取消 / Cancelled]");
            }
        };
    }

    private void WriteResult(string result)
    {
        if (resultWritten) return;
        resultWritten = true;
        try
        {
            if (!string.IsNullOrEmpty(resultFilePath))
            {
                File.WriteAllText(resultFilePath, result ?? "", Encoding.UTF8);
            }
        }
        catch { }
    }
}

internal sealed class QuoteItem
{
    public string Text { get; set; }
    public string Author { get; set; }

    public QuoteItem(string text, string author)
    {
        Text = StripLeadingNumber(text);
        Author = author ?? "";
    }

    public static string StripLeadingNumber(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        string trimmed = text.Trim();
        string cleaned = System.Text.RegularExpressions.Regex.Replace(
            trimmed,
            @"^(?:(?:No\.?|#)\s*[\d\uFF10-\uFF19]+\s*[:：\.．\-、]?|[\d\uFF10-\uFF19]+[\.．](?![0-9\uFF10-\uFF19])|[\d\uFF10-\uFF19]+[、:：\)）]|(?:\([\d\uFF10-\uFF19]+\)|（[\d\uFF10-\uFF19]+）|\[[\d\uFF10-\uFF19]+\]|【[\d\uFF10-\uFF19]+】)|[\d\uFF10-\uFF19]+\s*[-–—]\s+|[\u2460-\u2473])\s*",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );
        return string.IsNullOrEmpty(cleaned) ? trimmed : cleaned.Trim();
    }
}

internal sealed class DailyQuoteDialog : Form
{
    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
    private const uint SWP_FLAGS = 0x0043;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const byte AC_SRC_OVER = 0x00;
    private const byte AC_SRC_ALPHA = 0x01;
    private const uint ULW_ALPHA = 0x02;
    private const int WM_ERASEBKGND = 0x0014;
    private const int WM_NCHITTEST = 0x84;
    private const int HTTRANSPARENT = -1;
    private const int HTCLIENT = 1;
    private const int WM_MOUSELEAVE = 0x02A3;
    private const int VK_MENU = 0x12;

    public static readonly QuoteItem[] DefaultQuotes = new QuoteItem[]
    {
        new QuoteItem("种一棵树最好的时间是十年前，其次是现在。", "丹比萨·莫约"),
        new QuoteItem("流水不争先，争的是滔滔不绝。", "《道德经》"),
        new QuoteItem("博学之，审问之，慎思之，明辨之，笃行之。", "《礼记·中庸》"),
        new QuoteItem("专注是现代人最稀缺的超能力。", "卡尔·纽波特"),
        new QuoteItem("耐心不是消极等待，而是带着远见的持续行动。", "歌德"),
        new QuoteItem("The only way to do great work is to love what you do.", "Steve Jobs"),
        new QuoteItem("Stay hungry, stay foolish.", "Whole Earth Catalog"),
        new QuoteItem("Simplicity is prerequisite for reliability.", "Edsger W. Dijkstra"),
        new QuoteItem("知者不惑，仁者不忧，勇者不惧。", "《论语·子罕》"),
        new QuoteItem("日日行，不怕千万里；常常做，不怕千万事。", "《格言联璧》")
    };

    private Settings settings;
    private List<QuoteItem> quoteList;
    private int currentQuoteIndex;
    private bool isPinned;
    private bool isHovered;
    private bool isDragging;
    private Point dragStart;
    private Point formStart;

    private bool isResizingWidth;
    private int dragStartX;
    private int resizeStartWidth;
    private int customWidth = -1;

    private Timer countdownTimer;
    private Timer fadeTimer;
    private int totalDurationMs = 10000;
    private int elapsedMs = 0;
    private float currentFontSize = 18.0f;
    public float CurrentFontSize { get { return currentFontSize; } }
    public event Action<float> FontSizeChanged;
    public event Action QuoteChanged;
    public event Action<bool> PinChanged;
    public bool MoveMode { get; private set; }
    public void SetMoveMode(bool enabled)
    {
        MoveMode = enabled;
        if (enabled) EnterHoverState();
        else ExitHoverState();
    }
    public bool IsPinned
    {
        get { return isPinned; }
        set
        {
            if (isPinned != value)
            {
                isPinned = value;
                if (isPinned)
                {
                    if (countdownTimer != null) countdownTimer.Stop();
                }
                else if (!isPreviewMode && countdownTimer != null)
                {
                    countdownTimer.Start();
                }
                if (PinChanged != null) PinChanged(isPinned);
                UpdateTopMostState();
                RenderLayeredQuoteWindow();
            }
        }
    }
    private bool isPreviewMode;
    public bool IsPreviewMode
    {
        get { return isPreviewMode; }
        set
        {
            isPreviewMode = value;
            if (isPreviewMode)
            {
                if (countdownTimer != null) countdownTimer.Stop();
            }
            else if (!isPinned && countdownTimer != null)
            {
                countdownTimer.Start();
            }
            UpdateTopMostState();
        }
    }

    public int CurrentQuoteIndex
    {
        get { return currentQuoteIndex; }
        set { SetQuoteIndex(value); }
    }

    public void SetQuoteIndex(int idx)
    {
        if (quoteList != null && quoteList.Count > 0)
        {
            currentQuoteIndex = Math.Abs(idx) % quoteList.Count;
            elapsedMs = 0;
            UpdateCardLayout(keepLeft: true);
        }
    }

    public void NextQuote()
    {
        if (quoteList != null && quoteList.Count > 0)
        {
            currentQuoteIndex = (currentQuoteIndex + 1) % quoteList.Count;
            elapsedMs = 0;
            UpdateCardLayout(keepLeft: true);
            if (QuoteChanged != null) QuoteChanged();
        }
    }

    public void ReloadQuotes()
    {
        quoteList = LoadQuotes();
        if (quoteList != null && quoteList.Count > 0)
        {
            currentQuoteIndex = quoteList.Count - 1;
            elapsedMs = 0;
            UpdateCardLayout(keepLeft: false);
            if (QuoteChanged != null) QuoteChanged();
        }
    }

    public void UpdateTopMostState()
    {
        if (IsDisposed) return;
        try
        {
            bool wantTopMost = !isPinned || isPreviewMode || (settings != null && settings.QuotePinTopMost);
            TopMost = wantTopMost;
            if (IsHandleCreated)
            {
                IntPtr insertAfter = wantTopMost ? HWND_TOPMOST : HWND_BOTTOM;
                NativeMethods.SetWindowPos(Handle, insertAfter, 0, 0, 0, 0, SWP_FLAGS);
            }
        }
        catch { }
    }

    public void ApplyDraftSettings(Settings draft)
    {
        if (draft == null) return;
        settings = draft;
        totalDurationMs = Math.Max(3, settings.QuoteDuration) * 1000;
        if (draft.QuoteFontSize >= 8.0f && draft.QuoteFontSize <= 24.0f)
        {
            currentFontSize = draft.QuoteFontSize;
        }
        UpdateTopMostState();
        UpdateCardLayout(keepLeft: true);
    }
    private double currentOpacity = 1.0;

    private Bitmap renderBitmap;
    private Graphics renderGraphics;
    private byte[] premulBuffer;

    private Rectangle btnRefreshRect;
    private Rectangle btnPinRect;
    private Rectangle btnCloseRect;
    private int hoveredBtn = 0;

    public DailyQuoteDialog(Settings s) : this(s, null, null) { }

    public DailyQuoteDialog(Settings s, string customText, string customAuthor)
    {
        settings = s;
        totalDurationMs = Math.Max(3, (settings != null ? settings.QuoteDuration : 10)) * 1000;
        customWidth = (settings != null && settings.QuoteWidth > 0) ? settings.QuoteWidth : -1;
        if (settings != null && settings.QuoteFontSize >= 8.0f && settings.QuoteFontSize <= 24.0f)
        {
            currentFontSize = settings.QuoteFontSize;
        }
        else
        {
            currentFontSize = 18.0f;
        }

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        DoubleBuffered = true;
        TopMost = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Opaque, true);
        KeyPreview = true;
        KeyDown += delegate(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Dismiss();
        };

        if (!string.IsNullOrEmpty(customText))
        {
            quoteList = new List<QuoteItem> { new QuoteItem(customText, customAuthor ?? "") };
            currentQuoteIndex = 0;
        }
        else
        {
            quoteList = LoadQuotes();
            if (quoteList == null || quoteList.Count == 0) quoteList = new List<QuoteItem>(DefaultQuotes);
            int seed = DateTime.Today.Year * 366 + DateTime.Today.DayOfYear;
            currentQuoteIndex = Math.Abs(seed) % quoteList.Count;
        }

        UpdateCardLayout(keepLeft: false);

        countdownTimer = new Timer();
        countdownTimer.Interval = 500;
        countdownTimer.Tick += OnCountdownTick;
        if (!isPinned && !isPreviewMode)
        {
            countdownTimer.Start();
        }
    }

    protected override bool ShowWithoutActivation
    {
        get { return true; }
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_ERASEBKGND)
        {
            m.Result = (IntPtr)1;
            return;
        }
        if (m.Msg == WM_NCHITTEST)
        {
            if (MoveMode || isDragging || isResizingWidth)
            {
                m.Result = (IntPtr)HTCLIENT;
                return;
            }
            bool alt = (NativeMethods.GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
            if (alt)
            {
                m.Result = (IntPtr)HTCLIENT;
                return;
            }
            if (isHovered)
            {
                ExitHoverState();
            }
            m.Result = (IntPtr)HTTRANSPARENT;
            return;
        }
        if (m.Msg == WM_MOUSELEAVE)
        {
            OnMouseLeaveNative();
        }
        base.WndProc(ref m);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        UpdateTopMostState();
        RenderLayeredQuoteWindow();
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        RenderLayeredQuoteWindow();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        RenderLayeredQuoteWindow();
    }

    protected override void OnPaintBackground(PaintEventArgs e) { }
    protected override void OnPaint(PaintEventArgs e) { }

    private QuoteItem GetCurrentQuote()
    {
        if (quoteList == null || quoteList.Count == 0) return DefaultQuotes[0];
        return quoteList[currentQuoteIndex % quoteList.Count];
    }

    private void UpdateCardLayout(bool keepLeft = false)
    {
        QuoteItem q = GetCurrentQuote();
        string family = (settings != null && !string.IsNullOrEmpty(settings.QuoteFont))
            ? settings.QuoteFont
            : ((settings != null && !string.IsNullOrEmpty(settings.FontFamily)) ? settings.FontFamily : "Segoe UI");
        string lang = settings != null ? settings.Language : "zh";
        Font testFont = null;
        try { testFont = new Font(family, currentFontSize, FontStyle.Regular, GraphicsUnit.Point); }
        catch { testFont = AppContext.GetUiFont(lang, currentFontSize); }

        Font authorFont = null;
        try { authorFont = new Font(family, Math.Max(8.0f, currentFontSize - 1.5f), FontStyle.Italic, GraphicsUnit.Point); }
        catch
        {
            try { authorFont = new Font(family, Math.Max(8.0f, currentFontSize - 1.5f), FontStyle.Regular, GraphicsUnit.Point); }
            catch { authorFont = AppContext.GetUiFont(lang, Math.Max(8.0f, currentFontSize - 1.5f)); }
        }

        using (testFont)
        using (authorFont)
        using (Bitmap bmp = new Bitmap(1, 1))
        using (Graphics g = Graphics.FromImage(bmp))
        {
            string text = "“ " + q.Text + " ”";
            int padX = 18;
            int padTop = Math.Max(26, (int)Math.Round(currentFontSize * 1.0f) + 8);
            float gap = Math.Max(6f, currentFontSize * 0.35f);
            float padBottom = Math.Max(14f, currentFontSize * 0.6f);

            int cardW;
            if (customWidth > 0)
            {
                cardW = Math.Max(260, Math.Min(Screen.PrimaryScreen.WorkingArea.Width - 40, customWidth));
            }
            else
            {
                int defaultW = (int)Math.Round(Screen.PrimaryScreen.WorkingArea.Width * 0.40);
                cardW = Math.Max(320, Math.Min(Screen.PrimaryScreen.WorkingArea.Width - 40, defaultW));
            }

            int contentW = cardW - padX * 2;
            SizeF szText = g.MeasureString(text, testFont, contentW);
            int cardH = padTop + (int)Math.Ceiling(szText.Height);
            if (!string.IsNullOrEmpty(q.Author))
            {
                SizeF szAuthor = g.MeasureString("—— " + q.Author, authorFont, contentW);
                cardH += (int)Math.Ceiling(gap + szAuthor.Height);
            }
            cardH += (int)Math.Ceiling(padBottom);
            cardH = Math.Max(56, cardH);

            Size = new Size(cardW, cardH);

            int btnY = 6;
            int btnSz = 18;
            btnCloseRect = new Rectangle(cardW - 24, btnY, btnSz, btnSz);
            btnPinRect = new Rectangle(cardW - 46, btnY, btnSz, btnSz);
            btnRefreshRect = new Rectangle(cardW - 68, btnY, btnSz, btnSz);
        }

        if (!keepLeft || settings == null || settings.QuoteX < 0)
        {
            UpdatePosition();
        }
        RenderLayeredQuoteWindow();
    }

    private void UpdatePosition()
    {
        Rectangle scr = Screen.PrimaryScreen.WorkingArea;
        int x;
        if (settings != null && settings.QuoteX >= 0)
        {
            x = settings.QuoteX;
        }
        else
        {
            x = scr.Left + Math.Max(0, (scr.Width - Width) / 2);
        }

        int y;
        if (settings != null && settings.QuoteY >= 0)
        {
            y = settings.QuoteY;
        }
        else
        {
            y = scr.Top + 120;
            foreach (Form f in Application.OpenForms)
            {
                if (f != this && f.Visible && f.GetType().Name == "SettingsDialog")
                {
                    if (y + Height >= f.Top - 8)
                    {
                        // On compact displays or long multi-line quotes, fit snugly between countdown bottom (~98px) and settings top
                        y = Math.Max(scr.Top + 98, f.Top - Height - 12);
                    }
                    break;
                }
            }
        }

        Location = new Point(x, y);
        EnsureVisible();
    }

    public void EnsureVisible()
    {
        Rectangle currentRect = new Rectangle(Location, Size);
        bool visible = false;
        foreach (Screen s in Screen.AllScreens)
        {
            Rectangle intersect = Rectangle.Intersect(s.WorkingArea, currentRect);
            if (intersect.Width >= 40 && intersect.Height >= 30)
            {
                visible = true;
                break;
            }
        }
        if (!visible)
        {
            Rectangle scr = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(scr.Left + Math.Max(0, (scr.Width - Width) / 2), scr.Top + 120);
        }
    }

    private void OnCountdownTick(object sender, EventArgs e)
    {
        if (isPinned || isPreviewMode || isDragging || isResizingWidth || isHovered)
        {
            if (countdownTimer != null) countdownTimer.Stop();
            return;
        }

        elapsedMs += countdownTimer.Interval;
        if (elapsedMs >= totalDurationMs)
        {
            countdownTimer.Stop();
            StartFadeOut();
        }
    }

    private void EnterHoverState()
    {
        isHovered = true;
        if (countdownTimer != null) countdownTimer.Stop();
        RenderLayeredQuoteWindow();
    }

    private void ExitHoverState()
    {
        isHovered = false;
        hoveredBtn = 0;
        isResizingWidth = false;
        RenderLayeredQuoteWindow();
        UpdateTopMostState();
        if (!isPinned && !isPreviewMode && countdownTimer != null)
        {
            countdownTimer.Interval = 500;
            countdownTimer.Start();
        }
    }

    private void OnMouseLeaveNative()
    {
        if (isDragging || isResizingWidth) return;
        Point cur = Cursor.Position;
        Rectangle screenRect = new Rectangle(Location, Size);
        if (!screenRect.Contains(cur))
        {
            ExitHoverState();
        }
    }

    private void StartFadeOut()
    {
        if (fadeTimer != null) return;
        fadeTimer = new Timer();
        fadeTimer.Interval = 25;
        fadeTimer.Tick += delegate
        {
            currentOpacity -= 0.1;
            if (currentOpacity <= 0.05)
            {
                fadeTimer.Stop();
                fadeTimer.Dispose();
                fadeTimer = null;
                Close();
            }
            else
            {
                RenderLayeredQuoteWindow();
            }
        };
        fadeTimer.Start();
    }

    public void Dismiss()
    {
        if (countdownTimer != null) countdownTimer.Stop();
        StartFadeOut();
    }

    public void RenderLayeredQuoteWindow()
    {
        if (!IsHandleCreated || IsDisposed || Width <= 0 || Height <= 0) return;

        if (renderBitmap == null || renderBitmap.Width != Width || renderBitmap.Height != Height)
        {
            if (renderGraphics != null) { renderGraphics.Dispose(); renderGraphics = null; }
            if (renderBitmap != null) { renderBitmap.Dispose(); renderBitmap = null; }
            renderBitmap = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
            renderGraphics = Graphics.FromImage(renderBitmap);
            renderGraphics.SmoothingMode = SmoothingMode.AntiAlias;
            renderGraphics.TextRenderingHint = TextRenderingHint.AntiAlias;
        }

        renderGraphics.Clear(Color.FromArgb(0, 0, 0, 0));

        QuoteItem q = GetCurrentQuote();
        Color mainColor;
        string colStr = (settings != null && !string.IsNullOrEmpty(settings.QuoteColor)) ? settings.QuoteColor : (settings != null ? settings.Color : "#FF0000");
        try { mainColor = ColorTranslator.FromHtml(colStr); }
        catch { mainColor = Color.FromArgb(245, 247, 250); }

        string family = (settings != null && !string.IsNullOrEmpty(settings.QuoteFont))
            ? settings.QuoteFont
            : ((settings != null && !string.IsNullOrEmpty(settings.FontFamily)) ? settings.FontFamily : "Segoe UI");
        string lang = settings != null ? settings.Language : "zh";

        if (isHovered)
        {
            Rectangle cardRect = new Rectangle(0, 0, Width - 1, Height - 1);
            // 80% transparency -> 20% opacity -> alpha = 51 (255 * 0.20)
            int cardAlpha = 51;
            using (GraphicsPath path = AppContext.GetRoundedRectPath(cardRect, 10))
            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(cardAlpha, 20, 22, 28)))
            using (Pen borderPen = new Pen(Color.FromArgb(60, 255, 255, 255), 1.0f))
            {
                renderGraphics.FillPath(bgBrush, path);
                renderGraphics.DrawPath(borderPen, path);
            }

            // Right-edge resize grip handle (3 subtle vertical dots)
            using (SolidBrush gripBrush = new SolidBrush(Color.FromArgb(100, 255, 255, 255)))
            {
                float gx = Width - 5.5f;
                float gy = Height / 2.0f;
                renderGraphics.FillEllipse(gripBrush, gx - 1f, gy - 7f, 2.5f, 2.5f);
                renderGraphics.FillEllipse(gripBrush, gx - 1f, gy, 2.5f, 2.5f);
                renderGraphics.FillEllipse(gripBrush, gx - 1f, gy + 7f, 2.5f, 2.5f);
            }

            DrawActionButtons(renderGraphics, mainColor);

            if (!isPinned)
            {
                float ratio = Math.Max(0f, Math.Min(1f, 1f - (float)elapsedMs / totalDurationMs));
                int barW = (int)((Width - 36) * ratio);
                if (barW > 0)
                {
                    using (Pen barPen = new Pen(mainColor, 1.8f))
                    {
                        barPen.StartCap = LineCap.Round;
                        barPen.EndCap = LineCap.Round;
                        renderGraphics.DrawLine(barPen, 18, Height - 4, 18 + barW, Height - 4);
                    }
                }
            }
        }

        renderGraphics.TextRenderingHint = TextRenderingHint.AntiAlias;

        Font textFont = null;
        try { textFont = new Font(family, currentFontSize, FontStyle.Regular, GraphicsUnit.Point); }
        catch { textFont = AppContext.GetUiFont(lang, currentFontSize); }

        Font authorFont = null;
        try { authorFont = new Font(family, Math.Max(8.0f, currentFontSize - 1.5f), FontStyle.Italic, GraphicsUnit.Point); }
        catch
        {
            try { authorFont = new Font(family, Math.Max(8.0f, currentFontSize - 1.5f), FontStyle.Regular, GraphicsUnit.Point); }
            catch { authorFont = AppContext.GetUiFont(lang, Math.Max(8.0f, currentFontSize - 1.5f)); }
        }

        using (textFont)
        using (authorFont)
        {
            string text = "“ " + q.Text + " ”";
            int padX = 18;
            int padTop = Math.Max(26, (int)Math.Round(currentFontSize * 1.0f) + 8);
            float gap = Math.Max(6f, currentFontSize * 0.35f);
            int contentW = Width - padX * 2;

            using (StringFormat sf = new StringFormat { Trimming = StringTrimming.Word, Alignment = StringAlignment.Center })
            {
                SizeF sz = renderGraphics.MeasureString(text, textFont, contentW);
                RectangleF textBounds = new RectangleF(padX, padTop, contentW, sz.Height);
                float authorY = padTop + sz.Height + gap;

                SizeF szAuthor = SizeF.Empty;
                if (!string.IsNullOrEmpty(q.Author))
                {
                    szAuthor = renderGraphics.MeasureString("—— " + q.Author, authorFont, contentW);
                }
                RectangleF authorBounds = new RectangleF(padX, authorY, contentW, szAuthor.Height + 2);

                float brightness = mainColor.GetBrightness();
                int shadowAlpha = 110;
                Color shadowColor = (brightness < 0.32f)
                    ? Color.FromArgb(90, 255, 255, 255)
                    : Color.FromArgb(shadowAlpha, 0, 0, 0);

                float shadowOffset = currentFontSize >= 18f ? 2f : 1f;

                using (StringFormat asf = new StringFormat { Alignment = StringAlignment.Center })
                using (SolidBrush shadowBrush = new SolidBrush(shadowColor))
                {
                    RectangleF shadowText = new RectangleF(textBounds.X + shadowOffset, textBounds.Y + shadowOffset, textBounds.Width, textBounds.Height);
                    renderGraphics.DrawString(text, textFont, shadowBrush, shadowText, sf);
                    if (!string.IsNullOrEmpty(q.Author))
                    {
                        RectangleF shadowAuthor = new RectangleF(authorBounds.X + shadowOffset, authorBounds.Y + shadowOffset, authorBounds.Width, authorBounds.Height);
                        renderGraphics.DrawString("—— " + q.Author, authorFont, shadowBrush, shadowAuthor, asf);
                    }
                }

                Color authorColor = Color.FromArgb(190, mainColor.R, mainColor.G, mainColor.B);
                using (StringFormat asf = new StringFormat { Alignment = StringAlignment.Center })
                using (SolidBrush textBrush = new SolidBrush(mainColor))
                using (SolidBrush authorBrush = new SolidBrush(authorColor))
                {
                    renderGraphics.DrawString(text, textFont, textBrush, textBounds, sf);
                    if (!string.IsNullOrEmpty(q.Author))
                    {
                        renderGraphics.DrawString("—— " + q.Author, authorFont, authorBrush, authorBounds, asf);
                    }
                }
            }
        }

        DrawButtonTooltip(renderGraphics, lang);

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
        byte alpha = (byte)Math.Max(0, Math.Min(255, (int)(currentOpacity * 255)));
        BLENDFUNCTION blend = new BLENDFUNCTION { BlendOp = AC_SRC_OVER, BlendFlags = 0, SourceConstantAlpha = alpha, AlphaFormat = AC_SRC_ALPHA };

        NativeMethods.UpdateLayeredWindow(Handle, screenDc, ref ptDst, ref size, memDc, ref ptSrc, 0, ref blend, ULW_ALPHA);

        NativeMethods.SelectObject(memDc, oldBitmap);
        NativeMethods.DeleteObject(hBitmap);
        NativeMethods.DeleteDC(memDc);
    }

    private void DrawActionButtons(Graphics g, Color accentColor)
    {
        DrawMicroButton(g, btnRefreshRect, 1, accentColor);
        DrawMicroButton(g, btnPinRect, 2, accentColor);
        DrawMicroButton(g, btnCloseRect, 3, accentColor);
    }

    private void DrawMicroButton(Graphics g, Rectangle r, int btnId, Color accentColor)
    {
        bool hov = (hoveredBtn == btnId);
        if (hov)
        {
            using (GraphicsPath bp = AppContext.GetRoundedRectPath(r, 4))
            using (SolidBrush hb = new SolidBrush(Color.FromArgb(48, 255, 255, 255)))
            {
                g.FillPath(hb, bp);
            }
        }

        Color iconCol = hov ? Color.White : Color.FromArgb(180, 184, 196);
        float cx = r.X + r.Width / 2.0f;
        float cy = r.Y + r.Height / 2.0f;

        if (btnId == 1)
        {
            using (Pen p = new Pen(iconCol, 1.4f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                g.DrawArc(p, cx - 4.5f, cy - 4.5f, 9f, 9f, 45, 270);
                float ax = cx + 3.0f;
                float ay = cy - 3.0f;
                g.DrawLine(p, ax, ay, ax + 2.5f, ay);
                g.DrawLine(p, ax, ay, ax, ay - 2.5f);
            }
        }
        else if (btnId == 2)
        {
            Color pinCol = isPinned ? accentColor : iconCol;
            using (Pen p = new Pen(pinCol, 1.4f))
            using (SolidBrush b = new SolidBrush(pinCol))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                g.FillEllipse(b, cx - 1.8f, cy - 5.0f, 3.6f, 3.6f);
                g.DrawLine(p, cx - 3.2f, cy - 1.5f, cx + 3.2f, cy - 1.5f);
                g.DrawLine(p, cx, cy - 1.5f, cx, cy + 1.8f);
                g.DrawLine(p, cx, cy + 1.8f, cx, cy + 5.0f);
            }
        }
        else if (btnId == 3)
        {
            Color closeCol = hov ? Color.FromArgb(255, 110, 110) : iconCol;
            using (Pen p = new Pen(closeCol, 1.4f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                float off = 3.2f;
                g.DrawLine(p, cx - off, cy - off, cx + off, cy + off);
                g.DrawLine(p, cx + off, cy - off, cx - off, cy + off);
            }
        }
    }

    private void DrawButtonTooltip(Graphics g, string lang)
    {
        if (!isHovered || hoveredBtn <= 0) return;

        string tipText;

        if (hoveredBtn == 1)
        {
            tipText = (lang == "en") ? "Next quote" : "换一句";
        }
        else if (hoveredBtn == 2)
        {
            tipText = isPinned
                ? ((lang == "en") ? "Unpin" : "取消固定")
                : ((lang == "en") ? "Pin to desktop" : "钉在桌面");
        }
        else if (hoveredBtn == 3)
        {
            tipText = (lang == "en") ? "Close" : "关闭";
        }
        else
        {
            return;
        }

        using (Font tipFont = AppContext.GetUiFont(lang, 8.5f, FontStyle.Regular))
        {
            SizeF textSz = g.MeasureString(tipText, tipFont);
            int badgeW = (int)Math.Ceiling(textSz.Width) + 12;
            int badgeH = 18;
            int badgeY = btnRefreshRect.Y;

            int badgeX = btnRefreshRect.Left - 6 - badgeW;
            if (badgeX < 6) badgeX = 6;

            Rectangle badgeRect = new Rectangle(badgeX, badgeY, badgeW, badgeH);

            Rectangle shadowRect = new Rectangle(badgeRect.X, badgeRect.Y + 1, badgeRect.Width, badgeRect.Height);
            using (GraphicsPath sp = AppContext.GetRoundedRectPath(shadowRect, 4))
            using (SolidBrush sb = new SolidBrush(Color.FromArgb(70, 0, 0, 0)))
            {
                g.FillPath(sb, sp);
            }

            using (GraphicsPath path = AppContext.GetRoundedRectPath(badgeRect, 4))
            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(245, 26, 28, 34)))
            using (Pen borderPen = new Pen(Color.FromArgb(65, 255, 255, 255), 1.0f))
            using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(245, 245, 245)))
            using (StringFormat sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap
            })
            {
                g.FillPath(bgBrush, path);
                g.DrawPath(borderPen, path);
                g.DrawString(tipText, tipFont, textBrush, new RectangleF(badgeRect.X, badgeRect.Y + 0.5f, badgeRect.Width, badgeRect.Height), sf);
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (isDragging)
        {
            Location = new Point(formStart.X + (Cursor.Position.X - dragStart.X), formStart.Y + (Cursor.Position.Y - dragStart.Y));
            return;
        }

        if (isResizingWidth)
        {
            int deltaX = Cursor.Position.X - dragStartX;
            int minW = 260;
            int maxW = Screen.PrimaryScreen.WorkingArea.Width - 40;
            int targetW = Math.Max(minW, Math.Min(maxW, resizeStartWidth + deltaX));
            if (targetW != customWidth)
            {
                customWidth = targetW;
                UpdateCardLayout(keepLeft: true);
            }
            return;
        }

        bool alt = (NativeMethods.GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
        if (!alt && !MoveMode)
        {
            if (isHovered) ExitHoverState();
            return;
        }

        if (!isHovered)
        {
            EnterHoverState();
        }

        if (!ClientRectangle.Contains(e.Location))
        {
            OnMouseLeaveNative();
            return;
        }

        int oldHovBtn = hoveredBtn;
        if (btnRefreshRect.Contains(e.Location))
        {
            hoveredBtn = 1;
        }
        else if (btnPinRect.Contains(e.Location))
        {
            hoveredBtn = 2;
        }
        else if (btnCloseRect.Contains(e.Location))
        {
            hoveredBtn = 3;
        }
        else
        {
            hoveredBtn = 0;
        }

        if (hoveredBtn != oldHovBtn) RenderLayeredQuoteWindow();

        bool isOverButtons = hoveredBtn > 0;
        bool isNearRightEdge = (e.X >= Width - 10) && (!isOverButtons || e.Y >= 26);
        if (hoveredBtn > 0)
        {
            Cursor = Cursors.Hand;
        }
        else if (isNearRightEdge)
        {
            Cursor = Cursors.SizeWE;
        }
        else
        {
            Cursor = Cursors.SizeAll;
        }
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        bool alt = (NativeMethods.GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
        if (!alt && !MoveMode) return;
        if (e.Button == MouseButtons.Left && hoveredBtn == 0 && !isResizingWidth)
        {
            NextQuote();
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;

        bool alt = (NativeMethods.GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
        if (!alt && !MoveMode) return;

        if (btnRefreshRect.Contains(e.Location))
        {
            NextQuote();
            return;
        }
        if (btnPinRect.Contains(e.Location))
        {
            IsPinned = !IsPinned;
            return;
        }
        if (btnCloseRect.Contains(e.Location))
        {
            Dismiss();
            return;
        }

        bool isOverButtons = btnCloseRect.Contains(e.Location) || btnPinRect.Contains(e.Location) || btnRefreshRect.Contains(e.Location);
        bool isNearRightEdge = (e.X >= Width - 10) && (!isOverButtons || e.Y >= 26);

        if (isNearRightEdge)
        {
            isResizingWidth = true;
            dragStartX = Cursor.Position.X;
            resizeStartWidth = Width;
            Capture = true;
            return;
        }

        isDragging = true;
        dragStart = Cursor.Position;
        formStart = Location;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (isResizingWidth)
        {
            isResizingWidth = false;
            Capture = false;
            if (settings != null)
            {
                settings.QuoteWidth = customWidth;
                if (settings.QuoteX >= 0) settings.QuoteX = Location.X;
                if (settings.QuoteY >= 0) settings.QuoteY = Location.Y;
                settings.Save();
            }
            UpdateTopMostState();
            bool alt = (NativeMethods.GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
            if (!alt && !MoveMode) ExitHoverState();
            return;
        }

        if (isDragging)
        {
            isDragging = false;
            if (settings != null)
            {
                Rectangle scr = Screen.PrimaryScreen.WorkingArea;
                int centerX = scr.Left + Math.Max(0, (scr.Width - Width) / 2);
                if (Math.Abs(Location.X - centerX) <= 24)
                {
                    settings.QuoteX = -1;
                    Location = new Point(centerX, Location.Y);
                }
                else
                {
                    settings.QuoteX = Location.X;
                }
                settings.QuoteY = Location.Y;
                settings.Save();
            }
            UpdateTopMostState();
            bool alt = (NativeMethods.GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
            if (!alt && !MoveMode) ExitHoverState();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        OnMouseLeaveNative();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        bool alt = (NativeMethods.GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
        if (!alt && !MoveMode) return;
        if ((Control.ModifierKeys & Keys.Control) != 0)
        {
            float delta = e.Delta > 0 ? 0.5f : -0.5f;
            float newSize = Math.Max(8.0f, Math.Min(24.0f, currentFontSize + delta));
            if (Math.Abs(newSize - currentFontSize) > 0.05f)
            {
                currentFontSize = newSize;
                if (settings != null)
                {
                    settings.QuoteFontSize = currentFontSize;
                    settings.Save();
                }
                if (FontSizeChanged != null) FontSizeChanged(currentFontSize);
                UpdateCardLayout(keepLeft: true);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (countdownTimer != null) { countdownTimer.Stop(); countdownTimer.Dispose(); countdownTimer = null; }
            if (fadeTimer != null) { fadeTimer.Stop(); fadeTimer.Dispose(); fadeTimer = null; }
            if (renderGraphics != null) { renderGraphics.Dispose(); renderGraphics = null; }
            if (renderBitmap != null) { renderBitmap.Dispose(); renderBitmap = null; }

            if (settings != null)
            {
                bool needSave = false;
                if (customWidth > 0 && settings.QuoteWidth != customWidth)
                {
                    settings.QuoteWidth = customWidth;
                    needSave = true;
                }
                if (currentFontSize > 0 && Math.Abs(settings.QuoteFontSize - currentFontSize) > 0.05f)
                {
                    settings.QuoteFontSize = currentFontSize;
                    needSave = true;
                }
                if (needSave)
                {
                    settings.Save();
                }
            }
        }
        base.Dispose(disposing);
    }

    public static List<QuoteItem> LoadQuotes()
    {
        List<QuoteItem> list = new List<QuoteItem>();
        string path = Settings.QuotesFilePath;
        try
        {
            if (File.Exists(path))
            {
                string[] lines = ReadAllLinesAdaptive(path);
                foreach (string rawLine in lines)
                {
                    if (string.IsNullOrEmpty(rawLine)) continue;
                    string line = rawLine.Trim();
                    if (line.StartsWith("#") || line.StartsWith("//") || line.Length < 2) continue;

                    string[] parts = line.Split(new string[] { " | ", "|", " —— ", "——", " — ", "—", " -- " }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        list.Add(new QuoteItem(parts[0].Trim(), parts[1].Trim()));
                    }
                    else
                    {
                        list.Add(new QuoteItem(line, ""));
                    }
                }
            }
        }
        catch { }

        if (list.Count == 0)
        {
            list.AddRange(DefaultQuotes);
        }
        return list;
    }

    private static string[] ReadAllLinesAdaptive(string path)
    {
        try { return File.ReadAllLines(path, new UTF8Encoding(false, true)); }
        catch { try { return File.ReadAllLines(path, Encoding.Default); } catch { return new string[0]; } }
    }
}

internal static class AiAgentQuoteHelper
{
    private static readonly Dictionary<string, bool> commandCache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

    public static event Action<int, QuoteItem> OnQuotesAdded;

    internal static void NotifyQuotesAdded(int count, QuoteItem sample)
    {
        try
        {
            if (OnQuotesAdded != null)
            {
                OnQuotesAdded(count, sample);
            }
        }
        catch { }
    }

    internal static bool HasCliSupport(string agentName)
    {
        string exe, args;
        return TryGetCliCommand(agentName, "test", out exe, out args);
    }

    internal static bool IsCommandInPath(string cmd)
    {
        if (string.IsNullOrEmpty(cmd)) return false;
        lock (commandCache)
        {
            bool cached;
            if (commandCache.TryGetValue(cmd, out cached)) return cached;
        }
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "where.exe",
                Arguments = cmd,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (Process p = Process.Start(psi))
            {
                p.WaitForExit(1000);
                bool found = (p.ExitCode == 0);
                lock (commandCache) { commandCache[cmd] = found; }
                return found;
            }
        }
        catch { return false; }
    }

    internal static List<string> GetKeywords(Settings s)
    {
        string raw = s != null ? s.AiQuoteKeywords : null;
        if (string.IsNullOrEmpty(raw))
        {
            return s != null && s.Language == "en"
                ? new List<string> { "Geek Flow", "Stoicism", "Wisdom", "Balance" }
                : new List<string> { "极客心流", "斯多葛", "东方智慧", "从容反内卷" };
        }
        List<string> list = new List<string>();
        string[] parts = raw.Split(new char[] { ',', '，', '、', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string part in parts)
        {
            string t = part.Trim();
            if (t.Length > 0 && !list.Contains(t)) list.Add(t);
        }
        if (list.Count == 0)
        {
            list.AddRange(s != null && s.Language == "en"
                ? new string[] { "Geek Flow", "Stoicism", "Wisdom", "Balance" }
                : new string[] { "极客心流", "斯多葛", "东方智慧", "从容反内卷" });
        }
        return list;
    }

    internal static void SaveKeywords(Settings s, List<string> keywords)
    {
        if (s == null) return;
        s.AiQuoteKeywords = (keywords != null && keywords.Count > 0)
            ? string.Join(",", keywords.ToArray())
            : "";
        s.Save();
    }

    internal static string BuildMcpPrompt(List<string> keywords, string lang)
    {
        bool en = (lang == "en");
        string t = (keywords != null && keywords.Count > 0)
            ? string.Join(en ? ", " : "、", keywords.ToArray())
            : (en ? "Deep focus, craftsmanship and tranquility" : "极客心流、斯多葛与深度专注");
        if (en)
        {
            return "Please curate and compose 30 inspiring desk quotes based on the preference topics: [" + t + "].\r\n" +
                   "After creating them, please call iClock's MCP tool `iclock_quote` to add them in batch into my quotes library and project onto desktop:\r\n" +
                   "Tool call parameters:\r\n" +
                   "- action: \"add\"\r\n" +
                   "- quotes: [\r\n" +
                   "    \"Quote text 1 | Author 1\",\r\n" +
                   "    \"Quote text 2 | Author 2\",\r\n" +
                   "    ...\r\n" +
                   "  ]\r\n\r\n" +
                   "STRICT RULES:\r\n" +
                   "1. Maximum 35 words per quote, inspiring, timeless and concise.\r\n" +
                   "2. Every quote MUST include authentic author or source attribution.\r\n" +
                   "3. Call the `iclock_quote` tool with action: 'add' directly. No introductory explanations.";
        }
        else
        {
            return "请围绕偏好标签【" + t + "】精选创作 30 条高质量桌面格言短句。\r\n" +
                   "创作完成后，请直接调用 iClock 的 MCP 工具 `iclock_quote` 将这 30 条格言批量收录至名言词库并投射到桌面：\r\n" +
                   "工具调用参数：\r\n" +
                   "- action: \"add\"\r\n" +
                   "- quotes: [\r\n" +
                   "    \"格言内容 1 | 作者或出处 1\",\r\n" +
                   "    \"格言内容 2 | 作者或出处 2\",\r\n" +
                   "    ...\r\n" +
                   "  ]\r\n\r\n" +
                   "严格规范：\r\n" +
                   "1. 每条格言控制在 35 字以内，语言精练有力，富有启发与哲理；\r\n" +
                   "2. 每条格言必须附带真实作者或典籍出处（若佚名请注明佚名）；\r\n" +
                   "3. 请直接调用 `iclock_quote` 工具（action 设为 'add'），严禁多余的打招呼或寒暄废话。";
        }
    }

    internal static string BuildPrompt(List<string> keywords, string lang)
    {
        bool en = (lang == "en");
        string t = (keywords != null && keywords.Count > 0)
            ? string.Join(en ? ", " : "、", keywords.ToArray())
            : (en ? "Deep focus and craftsmanship" : "极客心流与深度专注");
        if (en)
        {
            return "Please create 30 inspiring desk quotes based on the topics: [" + t + "]. " +
                   "STRICT OUTPUT RULES: " +
                   "1. Each quote on its own line in the exact format: 'Quote text | Author or Source'. " +
                   "2. Maximum 35 words per quote. " +
                   "3. NEVER output numbers, bullets, greetings, markdown quotes, or any explanations.";
        }
        else
        {
            return "请围绕主题【" + t + "】精选创作30条桌面格言短句。严格规范：1.每行严格格式为'格言内容 | 作者或出处'；2.每条不超过35字，语言精炼有力；3.严禁输出序号、引号、打招呼、前言或任何多余废话。";
        }
    }

    internal static List<QuoteItem> ParseQuotes(string rawOutput)
    {
        List<QuoteItem> list = new List<QuoteItem>();
        if (string.IsNullOrEmpty(rawOutput)) return list;
        string[] lines = rawOutput.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (string.IsNullOrEmpty(line)) continue;
            if (line.StartsWith("```") || line.StartsWith("#") || line.StartsWith("//")) continue;
            if (line.EndsWith(":") || line.EndsWith("：")) continue;
            if (line == "{" || line == "}" || line == "[" || line == "]") continue;

            // Reject JSON properties, JSON errors, or command line error lines
            if (line.Contains("\": \"") || line.Contains("\":\"") || line.StartsWith("\"error\"")) continue;
            string lowerLine = line.ToLowerInvariant();
            if (lowerLine.StartsWith("error:") || lowerLine.StartsWith("fatal:") || lowerLine.StartsWith("exception:") || lowerLine.StartsWith("usage:")) continue;

            string clean = QuoteItem.StripLeadingNumber(line).Trim();
            bool hasOuterQuotes = false;
            if ((clean.StartsWith("\"") && clean.EndsWith("\"") && clean.Length > 2) ||
                (clean.StartsWith("“") && clean.EndsWith("”") && clean.Length > 2) ||
                (clean.StartsWith("「") && clean.EndsWith("」") && clean.Length > 2))
            {
                hasOuterQuotes = true;
                clean = clean.Substring(1, clean.Length - 2).Trim();
            }
            if (clean.Length < 3) continue;

            // If clean still contains JSON syntax or brackets, skip
            if (clean.Contains("\":") || clean.Contains("{") || clean.Contains("}")) continue;

            string[] parts = clean.Split(new string[] { " | ", "|", " —— ", "——", " — ", "—", " -- " }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                string text = parts[0].Trim();
                if ((text.StartsWith("\"") && text.EndsWith("\"") && text.Length > 2) ||
                    (text.StartsWith("“") && text.EndsWith("”") && text.Length > 2))
                {
                    text = text.Substring(1, text.Length - 2).Trim();
                }
                string author = parts[1].Trim();
                if (text.Length >= 2 && !author.Contains("\"") && !author.Contains(":") && !author.Contains("{"))
                {
                    list.Add(new QuoteItem(text, author));
                }
            }
            else if (hasOuterQuotes && clean.Length >= 4)
            {
                // Standalone quote without author: must have no leftover quotes, colons, or syntax characters
                if (!clean.Contains("\"") && !clean.Contains(":") && !clean.Contains("="))
                {
                    list.Add(new QuoteItem(clean, "佚名"));
                }
            }
        }
        return list;
    }

    internal static bool AppendQuotesToFile(List<QuoteItem> newQuotes, out int appendedCount, out string error)
    {
        appendedCount = 0;
        error = null;
        if (newQuotes == null || newQuotes.Count == 0)
        {
            error = "未解析到有效的新格言";
            return false;
        }

        string filePath = Settings.QuotesFilePath;
        try
        {
            string dir = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            HashSet<string> existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(filePath))
            {
                foreach (string line in File.ReadAllLines(filePath, Encoding.UTF8))
                {
                    string t = QuoteItem.StripLeadingNumber(line).Trim();
                    int p = t.IndexOf('|');
                    string text = (p >= 0 ? t.Substring(0, p) : t).Trim();
                    if (!string.IsNullOrEmpty(text)) existing.Add(text);
                }

                try
                {
                    string bak = filePath + ".bak";
                    File.Copy(filePath, bak, true);
                }
                catch { }
            }

            List<string> linesToAppend = new List<string>();
            foreach (QuoteItem qi in newQuotes)
            {
                if (string.IsNullOrEmpty(qi.Text)) continue;
                if (qi.Text.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0 && qi.Text.IndexOfAny(new char[] { '"', ':', '{', '}' }) >= 0) continue;
                if (existing.Contains(qi.Text)) continue;

                string line = string.IsNullOrEmpty(qi.Author)
                    ? qi.Text
                    : qi.Text + " | " + qi.Author;
                linesToAppend.Add(line);
                existing.Add(qi.Text);
            }

            if (linesToAppend.Count == 0)
            {
                error = "所生成的格言已存在于词库中";
                return false;
            }

            if (File.Exists(filePath))
            {
                File.AppendAllLines(filePath, linesToAppend, Encoding.UTF8);
            }
            else
            {
                File.WriteAllLines(filePath, linesToAppend, Encoding.UTF8);
            }

            appendedCount = linesToAppend.Count;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    internal static bool TryGetCliCommand(string agentName, string prompt, out string exe, out string args)
    {
        exe = "";
        args = "";
        if (string.IsNullOrEmpty(agentName)) return false;
        string lower = agentName.ToLowerInvariant();

        if (lower.Contains("claude"))
        {
            if (IsCommandInPath("claude.cmd")) { exe = "claude.cmd"; args = "-p \"" + prompt.Replace("\"", "\\\"") + "\""; return true; }
            if (IsCommandInPath("claude")) { exe = "claude"; args = "-p \"" + prompt.Replace("\"", "\\\"") + "\""; return true; }
        }
        else if (lower.Contains("ollama"))
        {
            if (IsCommandInPath("ollama.exe")) { exe = "ollama.exe"; args = "run qwen2.5 \"" + prompt.Replace("\"", "\\\"") + "\""; return true; }
            if (IsCommandInPath("ollama")) { exe = "ollama"; args = "run qwen2.5 \"" + prompt.Replace("\"", "\\\"") + "\""; return true; }
        }
        return false;
    }

    internal static bool RunAgentCli(string agentName, Settings settings, List<string> keywords, out string stdout, out string error)
    {
        stdout = null;
        error = null;
        string actualAgent = !string.IsNullOrEmpty(agentName) ? agentName : (settings != null ? settings.AiAgentType : "Auto");

        string lang = settings != null ? settings.Language : "zh";
        string prompt = BuildPrompt(keywords, lang);

        string exe, args;
        if (!TryGetCliCommand(actualAgent, prompt, out exe, out args))
        {
            error = (lang == "en") ? "Agent CLI not found or not supported for direct headless execution." : "未找到该智能体的命令行执行工具或不支持直接后台生成。";
            return false;
        }

        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            using (Process p = Process.Start(psi))
            {
                string outText = p.StandardOutput.ReadToEnd();
                string errText = p.StandardError.ReadToEnd();
                if (!p.WaitForExit(35000))
                {
                    try { p.Kill(); } catch { }
                    error = (lang == "en") ? "Agent execution timed out (35s)" : "智能体执行超时 (35秒)";
                    return false;
                }

                if (p.ExitCode != 0)
                {
                    string msg = !string.IsNullOrEmpty(errText) ? errText : outText;
                    error = !string.IsNullOrEmpty(msg) ? msg.Trim() : ("智能体退出码: " + p.ExitCode);
                    return false;
                }

                stdout = outText;
                return true;
            }
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}

internal sealed class AiQuoteDialog : Form
{
    private Settings settings;
    private List<string> currentKeywords;
    private List<string> detectedAgents;
    private string selectedAgent;
    private string currentPrompt;

    // View panels
    private Panel panelSetup;
    private FlowLayoutPanel panelTags;
    private TextBox txtNewTag;
    private Button btnAddTag;
    private ComboBox cboAgent;
    private Label lblAgentHint;
    private Button btnCancel;
    private Button btnGenerate;

    private Panel panelStatus;
    private Label lblStatusTitle;
    private Panel cardContainer;
    private Label lblCardMain;
    private Label lblCardSub;
    private Label lblCardBadge;

    private Button btnStatusAction1; // Bring window to front / Open library / Fallback to chat
    private Button btnStatusAction2; // Copy again / Retry
    private Button btnStatusBack;    // Back to setup
    private Button btnStatusDone;    // Close / Done / Cancel

    private System.Windows.Forms.Timer timerStatus;
    private int elapsedSeconds = 0;
    private int waitingDots = 0;

    private enum ViewMode { Setup, WaitingMcp, RunningCli, Completed, Error }
    private ViewMode currentMode = ViewMode.Setup;
    private bool isExecutingCli = false;

    public AiQuoteDialog(Settings s)
    {
        settings = s;
        string lang = s != null ? s.Language : "zh";
        bool en = (lang == "en");

        currentKeywords = AiAgentQuoteHelper.GetKeywords(s);

        Font = AppContext.GetUiFont(lang, 9.5f);
        BackColor = AppContext.Win11Bg;
        AppContext.ApplyModernWindowStyle(this);
        Text = en ? "AI Quote Library Expansion" : "AI 扩充名言词库";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ShowIcon = false;
        ClientSize = new Size(410, 265);

        // Register event for realtime quote expansion callback
        AiAgentQuoteHelper.OnQuotesAdded += HandleQuotesAddedCallback;

        timerStatus = new System.Windows.Forms.Timer();
        timerStatus.Interval = 1000;
        timerStatus.Tick += OnTimerStatusTick;

        BuildSetupPanel(lang, en);
        BuildStatusPanel(lang, en);

        ShowSetupView();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        AiAgentQuoteHelper.OnQuotesAdded -= HandleQuotesAddedCallback;
        if (timerStatus != null)
        {
            timerStatus.Stop();
            timerStatus.Dispose();
            timerStatus = null;
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            if (currentMode == ViewMode.WaitingMcp || currentMode == ViewMode.Error)
            {
                ShowSetupView();
                return true;
            }
            Close();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void BuildSetupPanel(string lang, bool en)
    {
        panelSetup = new Panel();
        panelSetup.SetBounds(0, 0, ClientSize.Width, ClientSize.Height);
        panelSetup.BackColor = AppContext.Win11Bg;
        Controls.Add(panelSetup);

        Label lblHeader = new Label();
        lblHeader.Text = en ? "✨ Expand Daily Quotes with AI" : "✨ AI 扩充名言词库";
        lblHeader.Font = AppContext.GetUiFont(lang, 11f, FontStyle.Bold);
        lblHeader.ForeColor = AppContext.Win11Accent;
        lblHeader.SetBounds(18, 12, 374, 22);
        panelSetup.Controls.Add(lblHeader);

        Label lblTags = new Label();
        lblTags.Text = en ? "Quote preference tags:" : "格言偏好标签：";
        lblTags.Font = AppContext.GetUiFont(lang, 9f);
        lblTags.ForeColor = AppContext.Win11TextPrimary;
        lblTags.SetBounds(18, 36, 374, 18);
        panelSetup.Controls.Add(lblTags);

        panelTags = new FlowLayoutPanel();
        panelTags.SetBounds(18, 56, 374, 56);
        panelTags.AutoScroll = true;
        panelTags.BackColor = Color.FromArgb(246, 248, 250);
        panelTags.BorderStyle = BorderStyle.None;
        panelTags.WrapContents = true;
        panelTags.Padding = new Padding(3, 3, 3, 3);
        panelSetup.Controls.Add(panelTags);

        txtNewTag = new TextBox();
        txtNewTag.Font = AppContext.GetUiFont(lang, 9f);
        txtNewTag.SetBounds(18, 118, 286, 25);
        txtNewTag.KeyDown += delegate(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                AddTagFromInput();
            }
        };
        panelSetup.Controls.Add(txtNewTag);

        btnAddTag = new Button();
        btnAddTag.Text = en ? "➕ Add" : "➕ 添加";
        btnAddTag.Font = AppContext.GetUiFont(lang, 9f);
        btnAddTag.SetBounds(310, 117, 82, 27);
        AppContext.StyleSecondaryButton(btnAddTag);
        btnAddTag.Click += delegate { AddTagFromInput(); };
        panelSetup.Controls.Add(btnAddTag);

        Label lblAgent = new Label();
        lblAgent.Text = en ? "AI Collaboration Agent (MCP):" : "AI 协作智能体（基于 MCP）：";
        lblAgent.Font = AppContext.GetUiFont(lang, 9f);
        lblAgent.ForeColor = AppContext.Win11TextPrimary;
        lblAgent.SetBounds(18, 150, 374, 18);
        panelSetup.Controls.Add(lblAgent);

        cboAgent = new ComboBox();
        cboAgent.DropDownStyle = ComboBoxStyle.DropDownList;
        cboAgent.Font = AppContext.GetUiFont(lang, 9f);
        cboAgent.SetBounds(18, 170, 374, 25);
        panelSetup.Controls.Add(cboAgent);

        // 100% Only Show Agents Configured with MCP
        List<string> allDetected = AppContext.DetectAgents();
        HashSet<string> running = AppContext.GetRunningAgents();
        detectedAgents = new List<string>();
        foreach (string name in allDetected)
        {
            if (McpConfigHelper.IsAgentMcpConfigured(name))
            {
                detectedAgents.Add(name);
            }
        }
        detectedAgents.Sort(delegate(string a, string b)
        {
            bool aRun = running.Contains(a);
            bool bRun = running.Contains(b);
            if (aRun != bRun) return aRun ? -1 : 1;
            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        });

        int defaultIdx = 0;
        string savedAgent = settings != null ? settings.AiAgentType : "";
        for (int i = 0; i < detectedAgents.Count; i++)
        {
            string name = detectedAgents[i];
            bool isRun = running.Contains(name);
            bool hasCli = AiAgentQuoteHelper.HasCliSupport(name);
            string modeStr = hasCli
                ? (en ? "CLI Direct" : "支持后台直接扩充")
                : (en ? "Chat Collab" : "对话框协作");
            string stateStr = isRun
                ? (en ? "Running" : "运行中")
                : (en ? "Ready" : "已就绪");
            string display = en
                ? string.Format("⚡ {0} (MCP Connected · {1} · {2})", name, stateStr, modeStr)
                : string.Format("⚡ {0} (已接入 MCP · {1} · {2})", name, stateStr, modeStr);
            cboAgent.Items.Add(display);
            if (!string.IsNullOrEmpty(savedAgent) && name.Equals(savedAgent, StringComparison.OrdinalIgnoreCase))
            {
                defaultIdx = i;
            }
        }

        lblAgentHint = new Label();
        lblAgentHint.Text = "";
        lblAgentHint.ForeColor = AppContext.Win11TextSecondary;
        lblAgentHint.Font = AppContext.GetUiFont(lang, 8.5f);
        lblAgentHint.SetBounds(18, 198, 374, 20);
        lblAgentHint.AutoEllipsis = true;
        panelSetup.Controls.Add(lblAgentHint);

        btnCancel = new Button();
        btnCancel.Text = en ? "Cancel" : "取消";
        btnCancel.SetBounds(210, 222, 80, 30);
        AppContext.StyleSecondaryButton(btnCancel);
        btnCancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
        panelSetup.Controls.Add(btnCancel);

        btnGenerate = new Button();
        btnGenerate.Text = en ? "✨ Expand" : "✨ 立即扩充";
        btnGenerate.SetBounds(296, 222, 96, 30);
        AppContext.StylePrimaryButton(btnGenerate);
        btnGenerate.Click += OnGenerateClick;
        panelSetup.Controls.Add(btnGenerate);

        if (detectedAgents.Count == 0)
        {
            cboAgent.Items.Add(en ? "⚪ No MCP agents connected" : "⚪ 未检测到已接入 MCP 的智能体");
            cboAgent.SelectedIndex = 0;
            lblAgentHint.Text = en
                ? "Tip: Go to [Settings ➔ AI Collaboration] to connect an agent via MCP."
                : "提示：请前往【设置 ➔ AI 协作】中为智能体一键接入 MCP。";
            lblAgentHint.ForeColor = Color.FromArgb(180, 100, 20);
            btnGenerate.Enabled = false;
        }
        else
        {
            cboAgent.SelectedIndex = defaultIdx;
            cboAgent.SelectedIndexChanged += delegate { UpdateAgentSelection(); };
            UpdateAgentSelection();
        }

        RenderTags();
    }

    private void UpdateAgentSelection()
    {
        bool en = (settings != null && settings.Language == "en");
        int idx = cboAgent != null ? cboAgent.SelectedIndex : -1;
        if (idx >= 0 && idx < detectedAgents.Count)
        {
            selectedAgent = detectedAgents[idx];
            if (settings != null)
            {
                settings.AiAgentType = selectedAgent;
                settings.Save();
            }
            bool hasCli = AiAgentQuoteHelper.HasCliSupport(selectedAgent);
            if (hasCli)
            {
                lblAgentHint.Text = en
                    ? "✨ Supports direct headless generation. Click below to expand automatically."
                    : "✨ 该智能体支持后台直接生成，点击下方扩充将在后台自动执行并在此汇报。";
                lblAgentHint.ForeColor = AppContext.Win11TextSecondary;
                btnGenerate.Text = en ? "⚡ Direct Expand" : "⚡ 直接扩充";
            }
            else
            {
                lblAgentHint.Text = en
                    ? "✨ Chat collaboration: Click below to copy prompt, then press Ctrl+V in agent chat."
                    : "✨ 对话框协作：点击下方扩充将自动复制指令，在智能体对话框按 Ctrl+V 发送即可自动入库。";
                lblAgentHint.ForeColor = AppContext.Win11TextSecondary;
                btnGenerate.Text = en ? "✨ Expand via MCP" : "✨ 立即扩充";
            }
            btnGenerate.Enabled = true;
        }
    }

    private void BuildStatusPanel(string lang, bool en)
    {
        panelStatus = new Panel();
        panelStatus.SetBounds(0, 0, ClientSize.Width, ClientSize.Height);
        panelStatus.BackColor = AppContext.Win11Bg;
        panelStatus.Visible = false;
        Controls.Add(panelStatus);

        lblStatusTitle = new Label();
        lblStatusTitle.Text = "";
        lblStatusTitle.Font = AppContext.GetUiFont(lang, 11f, FontStyle.Bold);
        lblStatusTitle.ForeColor = AppContext.Win11Accent;
        lblStatusTitle.SetBounds(18, 12, 374, 22);
        panelStatus.Controls.Add(lblStatusTitle);

        cardContainer = new Panel();
        cardContainer.SetBounds(18, 38, 374, 172);
        cardContainer.BackColor = Color.FromArgb(246, 248, 250);
        cardContainer.Paint += delegate(object sender, PaintEventArgs pe)
        {
            using (Pen p = new Pen(Color.FromArgb(228, 232, 236)))
            {
                pe.Graphics.DrawRectangle(p, 0, 0, cardContainer.Width - 1, cardContainer.Height - 1);
            }
        };
        panelStatus.Controls.Add(cardContainer);

        lblCardMain = new Label();
        lblCardMain.Text = "";
        lblCardMain.Font = AppContext.GetUiFont(lang, 9.5f, FontStyle.Bold);
        lblCardMain.ForeColor = AppContext.Win11TextPrimary;
        lblCardMain.SetBounds(14, 12, 346, 24);
        cardContainer.Controls.Add(lblCardMain);

        lblCardSub = new Label();
        lblCardSub.Text = "";
        lblCardSub.Font = AppContext.GetUiFont(lang, 9f);
        lblCardSub.ForeColor = AppContext.Win11TextSecondary;
        lblCardSub.SetBounds(14, 38, 346, 96);
        cardContainer.Controls.Add(lblCardSub);

        lblCardBadge = new Label();
        lblCardBadge.Text = "";
        lblCardBadge.Font = AppContext.GetUiFont(lang, 8.5f);
        lblCardBadge.ForeColor = AppContext.Win11Accent;
        lblCardBadge.SetBounds(14, 138, 346, 22);
        lblCardBadge.AutoEllipsis = true;
        cardContainer.Controls.Add(lblCardBadge);

        btnStatusAction1 = new Button();
        btnStatusAction1.Font = AppContext.GetUiFont(lang, 9f);
        AppContext.StyleSecondaryButton(btnStatusAction1);
        btnStatusAction1.Click += OnStatusAction1Click;
        panelStatus.Controls.Add(btnStatusAction1);

        btnStatusAction2 = new Button();
        btnStatusAction2.Font = AppContext.GetUiFont(lang, 9f);
        AppContext.StyleSecondaryButton(btnStatusAction2);
        btnStatusAction2.Click += OnStatusAction2Click;
        panelStatus.Controls.Add(btnStatusAction2);

        btnStatusBack = new Button();
        btnStatusBack.Font = AppContext.GetUiFont(lang, 9f);
        AppContext.StyleSecondaryButton(btnStatusBack);
        btnStatusBack.Click += OnStatusBackClick;
        panelStatus.Controls.Add(btnStatusBack);

        btnStatusDone = new Button();
        btnStatusDone.Font = AppContext.GetUiFont(lang, 9f);
        AppContext.StylePrimaryButton(btnStatusDone);
        btnStatusDone.Click += OnStatusDoneClick;
        panelStatus.Controls.Add(btnStatusDone);
    }

    private void RenderTags()
    {
        if (panelTags == null) return;
        panelTags.SuspendLayout();
        while (panelTags.Controls.Count > 0)
        {
            Control c = panelTags.Controls[0];
            panelTags.Controls.RemoveAt(0);
            c.Dispose();
        }

        string lang = settings != null ? settings.Language : "zh";
        for (int i = 0; i < currentKeywords.Count; i++)
        {
            string tag = currentKeywords[i];
            Button pill = new Button();
            pill.Text = tag + "  ✕";
            pill.Font = AppContext.GetUiFont(lang, 8.5f);
            pill.Height = 24;
            pill.AutoSize = true;
            pill.Cursor = Cursors.Hand;
            pill.Margin = new Padding(2, 2, 4, 2);
            AppContext.StyleSecondaryButton(pill);
            pill.Click += delegate
            {
                currentKeywords.Remove(tag);
                AiAgentQuoteHelper.SaveKeywords(settings, currentKeywords);
                RenderTags();
            };
            panelTags.Controls.Add(pill);
        }

        panelTags.ResumeLayout(true);
    }

    private void AddTagFromInput()
    {
        if (txtNewTag == null) return;
        string text = txtNewTag.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        string[] parts = text.Split(new char[] { ',', '，', '、', ';' }, StringSplitOptions.RemoveEmptyEntries);
        bool changed = false;
        foreach (string part in parts)
        {
            string clean = part.Trim();
            if (clean.Length > 0 && !currentKeywords.Contains(clean))
            {
                currentKeywords.Add(clean);
                changed = true;
            }
        }
        if (changed)
        {
            AiAgentQuoteHelper.SaveKeywords(settings, currentKeywords);
            RenderTags();
        }
        txtNewTag.Clear();
        txtNewTag.Focus();
    }

    private void ShowSetupView()
    {
        currentMode = ViewMode.Setup;
        isExecutingCli = false;
        if (timerStatus != null) timerStatus.Stop();
        panelStatus.Visible = false;
        panelSetup.Visible = true;
        AcceptButton = btnGenerate;
        CancelButton = btnCancel;
    }

    private void ShowWaitingMcpView(string agentName)
    {
        bool en = (settings != null && settings.Language == "en");
        currentMode = ViewMode.WaitingMcp;
        isExecutingCli = false;
        panelSetup.Visible = false;
        panelStatus.Visible = true;

        lblStatusTitle.Text = en ? "✨ Collaborating with " + agentName : "✨ 正在与 " + agentName + " 协作扩充...";
        lblCardMain.Text = en ? "✓ 30-quote prompt copied to clipboard!" : "✓ 30 条格言扩充指令已复制到剪贴板！";
        lblCardMain.ForeColor = Color.FromArgb(16, 124, 65);
        lblCardSub.Text = en
            ? string.Format("Please switch to [{0}] chat and press Ctrl+V to send.\nThe agent will automatically save 30 quotes via MCP.", agentName)
            : string.Format("请切换至【{0}】对话框，按 Ctrl+V 发送。\n智能体创作完成后，将通过 MCP 自动将 30 条格言写入 iClock。", agentName);
        lblCardSub.ForeColor = AppContext.Win11TextSecondary;
        lblCardBadge.Text = en ? "• Waiting for agent to write quotes via MCP..." : "• 正在等待智能体通过 MCP 写入词库...";
        lblCardBadge.ForeColor = AppContext.Win11Accent;

        btnStatusAction1.Visible = true;
        btnStatusAction1.Text = en ? "Bring Window" : "唤起窗口";
        btnStatusAction1.SetBounds(18, 222, 96, 30);
        AppContext.StyleSecondaryButton(btnStatusAction1);

        btnStatusAction2.Visible = true;
        btnStatusAction2.Text = en ? "Copy Again" : "重新复制";
        btnStatusAction2.SetBounds(120, 222, 96, 30);
        AppContext.StyleSecondaryButton(btnStatusAction2);

        btnStatusBack.Visible = true;
        btnStatusBack.Text = en ? "❮ Back" : "❮ 返回修改";
        btnStatusBack.SetBounds(222, 222, 94, 30);
        AppContext.StyleSecondaryButton(btnStatusBack);

        btnStatusDone.Visible = true;
        btnStatusDone.Text = en ? "Close" : "关闭";
        btnStatusDone.SetBounds(322, 222, 70, 30);
        AppContext.StyleSecondaryButton(btnStatusDone);

        AcceptButton = null;
        CancelButton = btnStatusDone;

        elapsedSeconds = 0;
        waitingDots = 0;
        if (timerStatus != null) timerStatus.Start();
    }

    private void ShowRunningCliView(string agentName)
    {
        bool en = (settings != null && settings.Language == "en");
        currentMode = ViewMode.RunningCli;
        isExecutingCli = true;
        panelSetup.Visible = false;
        panelStatus.Visible = true;

        lblStatusTitle.Text = en ? "⚡ Generating via " + agentName + "..." : "⚡ 正在通过 " + agentName + " 后台直接生成...";
        lblCardMain.Text = en ? "⚡ Running agent CLI in background..." : "⚡ 正在调用智能体命令行直接生成 30 条格言...";
        lblCardMain.ForeColor = AppContext.Win11Accent;
        lblCardSub.Text = en
            ? "No manual action required. Generating 30 quotes in background...\nResults will be reported here once completed."
            : "无需手动操作，智能体正在后台创作 30 条格言。\n完成后将自动收录并在此当场汇报。";
        lblCardSub.ForeColor = AppContext.Win11TextSecondary;
        lblCardBadge.Text = en ? "• Generating in background..." : "• 正在后台生成中...";
        lblCardBadge.ForeColor = AppContext.Win11Accent;

        btnStatusAction1.Visible = false;
        btnStatusAction2.Visible = false;
        btnStatusBack.Visible = false;

        btnStatusDone.Visible = true;
        btnStatusDone.Text = en ? "Cancel" : "取消";
        btnStatusDone.SetBounds(318, 222, 74, 30);
        AppContext.StyleSecondaryButton(btnStatusDone);

        AcceptButton = null;
        CancelButton = btnStatusDone;

        elapsedSeconds = 0;
        if (timerStatus != null) timerStatus.Start();
    }

    private void ShowCompletedView(int count, QuoteItem sample)
    {
        bool en = (settings != null && settings.Language == "en");
        currentMode = ViewMode.Completed;
        isExecutingCli = false;
        if (timerStatus != null) timerStatus.Stop();

        panelSetup.Visible = false;
        panelStatus.Visible = true;

        lblStatusTitle.Text = en ? "✨ Quotes Expansion Completed!" : "✨ 格言扩充完成！";
        lblCardMain.Text = en
            ? string.Format("Successfully added {0} new quotes to library!", count)
            : string.Format("已成功将 {0} 条全新精选格言收录至名言词库！", count);
        lblCardMain.ForeColor = Color.FromArgb(16, 124, 65);

        string preview = (sample != null && !string.IsNullOrEmpty(sample.Text))
            ? (string.IsNullOrEmpty(sample.Author) ? ("“ " + sample.Text + " ”") : ("“ " + sample.Text + " ”\r\n—— " + sample.Author))
            : (en ? "New quotes have been added to the library." : "新格言已加入词库并刷新。");
        lblCardSub.Text = preview;
        lblCardSub.ForeColor = AppContext.Win11TextPrimary;

        lblCardBadge.Text = en ? "✨ Latest quote projected to desktop display" : "✨ 最新格言已同步投递至桌面每日格言展示";
        lblCardBadge.ForeColor = Color.FromArgb(16, 124, 65);

        btnStatusAction1.Visible = true;
        btnStatusAction1.Text = en ? "Open Library" : "查看词库";
        btnStatusAction1.SetBounds(18, 222, 120, 30);
        AppContext.StyleSecondaryButton(btnStatusAction1);

        btnStatusAction2.Visible = false;
        btnStatusBack.Visible = false;

        btnStatusDone.Visible = true;
        btnStatusDone.Text = en ? "Done" : "完成";
        btnStatusDone.SetBounds(288, 222, 104, 30);
        AppContext.StylePrimaryButton(btnStatusDone);

        AcceptButton = btnStatusDone;
        CancelButton = btnStatusDone;
    }

    private void ShowErrorView(string errorMsg)
    {
        bool en = (settings != null && settings.Language == "en");
        currentMode = ViewMode.Error;
        isExecutingCli = false;
        if (timerStatus != null) timerStatus.Stop();

        panelSetup.Visible = false;
        panelStatus.Visible = true;

        lblStatusTitle.Text = en ? "Notice: Expansion Incomplete" : "提示：后台生成未完成";
        lblCardMain.Text = en ? "Agent CLI execution failed or timed out:" : "智能体命令行执行异常或响应超时：";
        lblCardMain.ForeColor = Color.FromArgb(196, 43, 28);

        lblCardSub.Text = !string.IsNullOrEmpty(errorMsg) ? errorMsg : "Unknown error";
        lblCardSub.ForeColor = AppContext.Win11TextSecondary;

        lblCardBadge.Text = en ? "✨ Tip: You can fallback to chat collaboration." : "✨ 提示：您可以降级为复制指令并在智能体对话框中按 Ctrl+V 发送。";
        lblCardBadge.ForeColor = Color.FromArgb(180, 100, 20);

        btnStatusAction1.Visible = true;
        btnStatusAction1.Text = en ? "Fallback to Chat" : "降级为复制指令协作";
        btnStatusAction1.SetBounds(18, 222, 150, 30);
        AppContext.StylePrimaryButton(btnStatusAction1);

        btnStatusAction2.Visible = true;
        btnStatusAction2.Text = en ? "Retry" : "重试";
        btnStatusAction2.SetBounds(174, 222, 80, 30);
        AppContext.StyleSecondaryButton(btnStatusAction2);

        btnStatusBack.Visible = true;
        btnStatusBack.Text = en ? "❮ Back" : "❮ 返回";
        btnStatusBack.SetBounds(260, 222, 68, 30);
        AppContext.StyleSecondaryButton(btnStatusBack);

        btnStatusDone.Visible = true;
        btnStatusDone.Text = en ? "Close" : "关闭";
        btnStatusDone.SetBounds(334, 222, 58, 30);
        AppContext.StyleSecondaryButton(btnStatusDone);

        AcceptButton = btnStatusAction1;
        CancelButton = btnStatusDone;
    }

    private void OnTimerStatusTick(object sender, EventArgs e)
    {
        bool en = (settings != null && settings.Language == "en");
        if (currentMode == ViewMode.RunningCli)
        {
            elapsedSeconds++;
            lblCardBadge.Text = en
                ? string.Format("• Generating in background... ({0}s)", elapsedSeconds)
                : string.Format("• 正在后台生成中... (已耗时 {0} 秒)", elapsedSeconds);
        }
        else if (currentMode == ViewMode.WaitingMcp)
        {
            waitingDots = (waitingDots + 1) % 4;
            string dots = new string('.', waitingDots);
            lblCardBadge.Text = en
                ? ("• Waiting for agent to write quotes via MCP" + dots)
                : ("• 正在等待智能体通过 MCP 写入词库" + dots);
        }
    }

    private void OnStatusAction1Click(object sender, EventArgs e)
    {
        if (currentMode == ViewMode.WaitingMcp)
        {
            AppContext.BringAgentToFront(selectedAgent);
        }
        else if (currentMode == ViewMode.Completed)
        {
            AppContext.OpenQuotesFile();
        }
        else if (currentMode == ViewMode.Error)
        {
            StartMcpCollaboration(selectedAgent);
        }
    }

    private void OnStatusAction2Click(object sender, EventArgs e)
    {
        bool en = (settings != null && settings.Language == "en");
        if (currentMode == ViewMode.WaitingMcp)
        {
            try
            {
                Clipboard.SetText(currentPrompt);
                btnStatusAction2.Text = en ? "Copied ✓" : "已复制 ✓";
            }
            catch { }
        }
        else if (currentMode == ViewMode.Error)
        {
            StartCliGeneration(selectedAgent);
        }
    }

    private void OnStatusBackClick(object sender, EventArgs e)
    {
        ShowSetupView();
    }

    private void OnStatusDoneClick(object sender, EventArgs e)
    {
        if (currentMode == ViewMode.Completed)
        {
            DialogResult = DialogResult.OK;
            Close();
        }
        else
        {
            isExecutingCli = false;
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }

    private void OnGenerateClick(object sender, EventArgs e)
    {
        bool en = settings != null && settings.Language == "en";
        string lang = settings != null ? settings.Language : "zh";

        // Auto-add any pending text in txtNewTag
        if (txtNewTag != null && !string.IsNullOrEmpty(txtNewTag.Text.Trim()))
        {
            AddTagFromInput();
        }

        if (currentKeywords.Count == 0)
        {
            currentKeywords = AiAgentQuoteHelper.GetKeywords(settings);
            RenderTags();
        }

        int selIdx = cboAgent != null ? cboAgent.SelectedIndex : -1;
        if (selIdx < 0 || detectedAgents == null || selIdx >= detectedAgents.Count)
        {
            lblAgentHint.ForeColor = Color.FromArgb(202, 80, 16);
            lblAgentHint.Text = en ? "No available agent selected." : "未选择可用的智能体。";
            return;
        }

        selectedAgent = detectedAgents[selIdx];
        if (settings != null)
        {
            settings.AiAgentType = selectedAgent;
            settings.Save();
        }

        currentPrompt = AiAgentQuoteHelper.BuildMcpPrompt(currentKeywords, lang);

        bool hasCli = AiAgentQuoteHelper.HasCliSupport(selectedAgent);
        if (hasCli)
        {
            StartCliGeneration(selectedAgent);
        }
        else
        {
            StartMcpCollaboration(selectedAgent);
        }
    }

    private void StartMcpCollaboration(string agentName)
    {
        try
        {
            Clipboard.SetText(currentPrompt);
        }
        catch { }

        AppContext.BringAgentToFront(agentName);
        ShowWaitingMcpView(agentName);
    }

    private void StartCliGeneration(string agentName)
    {
        ShowRunningCliView(agentName);
        System.Threading.ThreadPool.QueueUserWorkItem(delegate
        {
            string stdout;
            string error;
            bool ok = AiAgentQuoteHelper.RunAgentCli(agentName, settings, currentKeywords, out stdout, out error);
            if (!isExecutingCli) return; // User cancelled or closed

            if (ok && !string.IsNullOrEmpty(stdout))
            {
                List<QuoteItem> parsed = AiAgentQuoteHelper.ParseQuotes(stdout);
                if (parsed.Count > 0)
                {
                    int addedCount;
                    string appendErr;
                    AiAgentQuoteHelper.AppendQuotesToFile(parsed, out addedCount, out appendErr);
                    if (addedCount > 0)
                    {
                        try
                        {
                            if (AppContext.Current != null)
                            {
                                AppContext.Current.ShowCustomQuote(parsed[0].Text, parsed[0].Author);
                            }
                        }
                        catch { }
                        AiAgentQuoteHelper.NotifyQuotesAdded(addedCount, parsed[0]);
                    }
                    else
                    {
                        // Deduplicated, all existed
                        AiAgentQuoteHelper.NotifyQuotesAdded(parsed.Count, parsed[0]);
                    }
                }
                else
                {
                    string failMsg = (settings != null && settings.Language == "en")
                        ? "Agent output did not contain valid quotes format."
                        : "智能体未按规范格式返回格言，建议使用对话框协作方式。";
                    try
                    {
                        BeginInvoke(new MethodInvoker(delegate
                        {
                            if (isExecutingCli) ShowErrorView(failMsg);
                        }));
                    }
                    catch { }
                }
            }
            else
            {
                string failMsg = !string.IsNullOrEmpty(error) ? error : "CLI execution returned no output.";
                try
                {
                    BeginInvoke(new MethodInvoker(delegate
                    {
                        if (isExecutingCli) ShowErrorView(failMsg);
                    }));
                }
                catch { }
            }
        });
    }

    private void HandleQuotesAddedCallback(int count, QuoteItem sample)
    {
        if (IsDisposed || !IsHandleCreated) return;
        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(new MethodInvoker(delegate { HandleQuotesAddedCallback(count, sample); }));
            }
            catch { }
            return;
        }
        ShowCompletedView(count, sample);
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
        lblDesc.UseMnemonic = false;
        lblDesc.Text = en ? "Minimalist & distraction-free desktop focus companion" : "极简通透、轻量无打扰的桌面专注与效率伴侣";
        lblDesc.Font = AppContext.GetUiFont(language, 9f, FontStyle.Regular);
        lblDesc.ForeColor = AppContext.Win11TextSecondary;
        lblDesc.SetBounds(20, 44, 310, 20);
        Controls.Add(lblDesc);

        Label lblVersion = new Label();
        lblVersion.Text = (en ? "Version: v" : "当前版本: v") + AppContext.CURRENT_VERSION;
        lblVersion.Font = AppContext.GetUiFont(language, 9.5f, FontStyle.Regular);
        lblVersion.ForeColor = AppContext.Win11TextPrimary;
        int verW = TextRenderer.MeasureText(lblVersion.Text, lblVersion.Font).Width;
        lblVersion.SetBounds(20, 72, verW, 24);
        lblVersion.TextAlign = ContentAlignment.MiddleLeft;
        Controls.Add(lblVersion);

        LinkLabel link = new LinkLabel();
        link.Text = "(GitHub ↗)";
        link.Font = AppContext.GetUiFont(language, 9f, FontStyle.Regular);
        link.LinkColor = AppContext.Win11Accent;
        link.LinkBehavior = LinkBehavior.HoverUnderline;
        int linkW = TextRenderer.MeasureText(link.Text, link.Font).Width;
        link.SetBounds(lblVersion.Right + 2, 74, linkW, 20);
        link.LinkClicked += delegate { try { Process.Start("https://github.com/francodeyvison183-collab/iClock"); } catch { } };
        Controls.Add(link);

        btnCheck = new Button();
        btnCheck.Text = en ? "Check Updates" : "检查更新";
        btnCheck.Font = AppContext.GetUiFont(language, 9f, FontStyle.Regular);
        btnCheck.SetBounds(220, 70, 110, 26);
        AppContext.StyleSecondaryButton(btnCheck);
        btnCheck.Click += OnCheckUpdates;
        Controls.Add(btnCheck);

        Label line = new Label();
        line.BackColor = AppContext.Win11Border;
        line.BorderStyle = BorderStyle.None;
        line.SetBounds(20, 106, 310, 1);
        Controls.Add(line);

        Label lblSponsor = new Label();
        lblSponsor.Text = en ? "If iClock helps, feel free to support via WeChat Pay!" : "如果 iClock 对你有帮助，欢迎微信扫码赞赏支持！";
        lblSponsor.Font = AppContext.GetUiFont(language, 9f, FontStyle.Regular);
        lblSponsor.ForeColor = AppContext.Win11TextPrimary;
        lblSponsor.TextAlign = ContentAlignment.MiddleCenter;
        lblSponsor.SetBounds(15, 118, 320, 24);
        Controls.Add(lblSponsor);

        HqPictureBox pic = new HqPictureBox();
        pic.SetBounds(25, 146, 300, 300);
        pic.SizeMode = PictureBoxSizeMode.Zoom;
        pic.BorderStyle = BorderStyle.None;
        pic.Image = LoadSponsorImage();
        Controls.Add(pic);

        Label lblAuthor = new Label();
        lblAuthor.UseMnemonic = false;
        lblAuthor.Text = en ? "Designed & Built by gujiangwen" : "作者：gujiangwen";
        lblAuthor.Font = AppContext.GetUiFont(language, 8.5f, FontStyle.Regular);
        lblAuthor.ForeColor = AppContext.Win11TextSecondary;
        lblAuthor.TextAlign = ContentAlignment.MiddleCenter;
        lblAuthor.SetBounds(20, 454, 310, 18);
        Controls.Add(lblAuthor);

        LinkLabel linkEmail = new LinkLabel();
        linkEmail.UseMnemonic = false;
        string emailPrefix = en ? "Feedback: " : "反馈交流：";
        string emailAddress = "francodeyvison183@gmail.com";
        linkEmail.Text = emailPrefix + emailAddress;
        linkEmail.Font = AppContext.GetUiFont(language, 8.5f, FontStyle.Regular);
        linkEmail.LinkColor = AppContext.Win11Accent;
        linkEmail.ActiveLinkColor = Color.FromArgb(0, 90, 158);
        linkEmail.VisitedLinkColor = AppContext.Win11Accent;
        linkEmail.LinkBehavior = LinkBehavior.HoverUnderline;
        linkEmail.LinkArea = new LinkArea(emailPrefix.Length, emailAddress.Length);
        linkEmail.TextAlign = ContentAlignment.MiddleCenter;
        linkEmail.SetBounds(20, 476, 310, 18);

        ToolTip tip = new ToolTip();
        tip.SetToolTip(linkEmail, en ? "Click to send email or copy address" : "点击发送邮件或复制邮箱地址");
        linkEmail.LinkClicked += delegate
        {
            try { Clipboard.SetText(emailAddress); } catch { }
            try { tip.Show(en ? "Email copied to clipboard!" : "邮箱地址已复制到剪贴板！", linkEmail, 150, 20, 2000); } catch { }
            try { Process.Start("mailto:" + emailAddress); } catch { }
        };
        Controls.Add(linkEmail);

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
    private static AppContext currentInstance;
    internal static AppContext Current { get { return currentInstance; } }

    private const int HOTKEY_ID = 0x4A10;
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_NOREPEAT = 0x4000;
    private Settings settings;
    private Overlay overlay;
    private NotifyIcon tray;
    private ToolStripMenuItem menuStart, menuReset, menuMove, menuHistory, menuSettings, menuAbout, menuExit, menuUpdate;
    internal const string CURRENT_VERSION = "1.07";
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
    private bool isOneShot = false;

    private void SetState(ClockState newState)
    {
        state = newState;
        running = (state == ClockState.Running || state == ClockState.RestRunning);
        sessionActive = (state != ClockState.Idle && state != ClockState.MoveMode);
        if (running || sessionActive)
        {
            UnhookMouse();
        }
    }
    private bool hotkeyRegistered;
    private int activeHotkeyModifiers, activeHotkeyKey;
    private Form activeNotice;
    private Form activeDialog;
    private DailyQuoteDialog activeQuoteDialog;
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
        if (running || sessionActive)
        {
            UnhookMouse();
            return NativeMethods.CallNextHookEx(mouseHookHandle, nCode, wParam, lParam);
        }

        int now = Environment.TickCount;
        if (Math.Abs(now - lastTrayMouseMove) > 800)
        {
            UnhookMouse();
            return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        if (nCode >= 0 && wParam == (IntPtr)WM_MOUSEWHEEL)
        {
            MSLLHOOKSTRUCT hs = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
            Point cursorPt = new Point(hs.pt.X, hs.pt.Y);
            Screen scr = Screen.FromPoint(cursorPt);
            if (scr != null && scr.WorkingArea.Contains(cursorPt))
            {
                UnhookMouse();
                return NativeMethods.CallNextHookEx(mouseHookHandle, nCode, wParam, lParam);
            }

            short wheelDelta = (short)((hs.mouseData >> 16) & 0xFFFF);
            if (wheelDelta != 0)
            {
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

    public AppContext() : this(null, false) { }

    public AppContext(string[] initialArgs) : this(initialArgs, false) { }

    public AppContext(string[] initialArgs, bool isOneShot)
    {
        currentInstance = this;
        this.isOneShot = isOneShot;
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
        tray = new NotifyIcon(); tray.Icon = appIcon; tray.Text = isOneShot ? (settings.Language == "en" ? "iClock (One-shot mode)" : "iClock (单次运行模式)") : "iClock"; tray.Visible = true; tray.ContextMenuStrip = MakeMenu();
        tray.MouseMove += delegate { if (!running && !sessionActive) { lastTrayMouseMove = Environment.TickCount; EnsureMouseHook(); } };
        tray.BalloonTipClicked += delegate { if (!string.IsNullOrEmpty(updateUrl)) { try { Process.Start(updateUrl); } catch { } } };
        tray.MouseClick += delegate(object sender, MouseEventArgs e) { UnhookMouse(); if (e.Button == MouseButtons.Left) { Toggle(); } };
        tray.DoubleClick += delegate { ShowSettings(); };
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
            if (overlay != null && !overlay.IsDisposed)
            {
                if (overlay.InvokeRequired) overlay.BeginInvoke(new Action(delegate { overlay.EnsureVisible(); }));
                else overlay.EnsureVisible();
            }
            if (activeQuoteDialog != null && !activeQuoteDialog.IsDisposed)
            {
                if (activeQuoteDialog.InvokeRequired) activeQuoteDialog.BeginInvoke(new Action(delegate { activeQuoteDialog.EnsureVisible(); }));
                else activeQuoteDialog.EnsureVisible();
            }
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
                if (settings.DailyQuote)
                {
                    if (overlay.InvokeRequired) overlay.BeginInvoke(new Action(delegate { CheckDailyQuote(false); }));
                    else CheckDailyQuote(false);
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
        if (!isOneShot && settings.FirstRun)
        {
            overlay.BeginInvoke(new Action(delegate { ShowWelcome(fallbackUsed); }));
        }
        else if (!isOneShot && settings.DailyQuote)
        {
            overlay.BeginInvoke(new Action(delegate { CheckDailyQuote(false); }));
        }
        if (!isOneShot && !settings.FirstRun && (initialArgs == null || initialArgs.Length == 0))
        {
            try
            {
                string hotkeyStr = HotkeyText(settings.HotkeyModifiers, settings.HotkeyKey);
                string tip = (settings.Language == "en")
                    ? "iClock is running in system tray. Hotkey: " + hotkeyStr
                    : "iClock 已在系统托盘运行中 (快捷键: " + hotkeyStr + ")";
                ShowTrayNotification("iClock", tip, 2500);
            }
            catch { }
        }
        if (initialArgs != null && initialArgs.Length > 0)
        {
            HandleIpcCommand(string.Join(" ", initialArgs));
        }
        else if (isOneShot)
        {
            HandleIpcCommand("--start");
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
        if (isOneShot) Exit();
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
        UnhookMouse();
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
        if (cmd.IndexOf("--once", StringComparison.OrdinalIgnoreCase) >= 0 || cmd.IndexOf("-once", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            cmd = System.Text.RegularExpressions.Regex.Replace(cmd, @"\s*--?once\b", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
        }
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
        else if (action == "notice" || action == "-n" || action == "--notice" || action.StartsWith("notice:"))
        {
            string raw = action.StartsWith("notice:") ? cmd.Substring("notice:".Length).Trim() : (arg ?? "");
            string snd = null;
            if (raw.Contains("|"))
            {
                string[] np = raw.Split(new char[] { '|' }, 2);
                snd = np[0];
                raw = np[1];
            }
            if (!string.IsNullOrEmpty(snd)) PlaySoundStyle(snd);
            TriggerCliNotice(raw);
        }
        else if (action == "quote" || action == "-q" || action == "--quote" || action.StartsWith("quote:"))
        {
            string raw = action.StartsWith("quote:") ? cmd.Substring("quote:".Length).Trim() : (arg ?? "");
            if (!string.IsNullOrEmpty(raw) && raw.Contains("|"))
            {
                string[] qp = raw.Split(new char[] { '|' }, 2);
                ShowCustomQuote(qp[0], qp[1]);
            }
            else
            {
                CheckDailyQuote(true);
            }
        }
        else if (action == "quote_add" || action.StartsWith("quote_add:"))
        {
            string batchFile = action.StartsWith("quote_add:") ? cmd.Substring("quote_add:".Length).Trim() : (arg ?? "");
            int addedCount = 0;
            if (!string.IsNullOrEmpty(batchFile) && File.Exists(batchFile))
            {
                try
                {
                    string content = File.ReadAllText(batchFile, Encoding.UTF8);
                    List<QuoteItem> parsed = AiAgentQuoteHelper.ParseQuotes(content);
                    if (parsed.Count > 0)
                    {
                        string err;
                        AiAgentQuoteHelper.AppendQuotesToFile(parsed, out addedCount, out err);
                        if (addedCount > 0)
                        {
                            AiAgentQuoteHelper.NotifyQuotesAdded(addedCount, parsed[0]);
                            if (activeQuoteDialog != null && !activeQuoteDialog.IsDisposed) activeQuoteDialog.ReloadQuotes();
                            ShowCustomQuote(parsed[0].Text, parsed[0].Author);
                        }
                    }
                    string resFile = batchFile + ".res";
                    File.WriteAllText(resFile, addedCount.ToString(), Encoding.UTF8);
                }
                catch { }
            }
        }
        else if (action == "ask_human" || action.StartsWith("ask_human:"))
        {
            string payload = action.StartsWith("ask_human:") ? cmd.Substring("ask_human:".Length).Trim() : (arg ?? "");
            string[] p = payload.Split(new char[] { '|' }, 4);
            if (p.Length >= 3)
            {
                string reqId = p[0];
                int timeoutSec = 0;
                if (p.Length > 1 && !string.IsNullOrEmpty(p[1])) int.TryParse(p[1], out timeoutSec);
                if (timeoutSec < 0) timeoutSec = 0;
                string question = p[2];
                string[] options = (p.Length > 3 && !string.IsNullOrEmpty(p[3]))
                    ? p[3].Split(new char[] { '~' }, StringSplitOptions.RemoveEmptyEntries)
                    : new string[0];
                string resFile = Path.Combine(Path.GetTempPath(), "iclock_ask_" + reqId + ".txt");
                ShowAskHumanDialog(question, options, timeoutSec, resFile);
            }
        }
        else if (action == "status" || action.StartsWith("status:"))
        {
            string statFile = action.StartsWith("status:") ? cmd.Substring("status:".Length).Trim() : (arg ?? "");
            if (!string.IsNullOrEmpty(statFile))
            {
                int rem = (int)ReadRemaining().TotalSeconds;
                string json = string.Format("{{\"running\":{0},\"remaining_seconds\":{1},\"total_minutes\":{2}}}",
                    running ? "true" : "false", rem, settings.Minutes);
                try { File.WriteAllText(statFile, json, Encoding.UTF8); } catch { }
            }
        }
        else if (action == "mcp-connected")
        {
            // Silent handshake compatibility
        }
        else if (action == "activate" || action == "settings" || action == "config" || action == "open" || action == "--settings" || action == "-c")
        {
            ShowSettings();
        }
        else if (action == "quit" || action == "exit" || action == "--quit" || action == "--exit" || action == "-x")
        {
            Exit();
        }
    }

    internal void TriggerCliNotice(string message)
    {
        if (string.IsNullOrEmpty(message)) message = settings.Language == "en" ? "Task completed!" : "任务已完成！";
        if (activeNotice != null && !activeNotice.IsDisposed) { activeNotice.Close(); activeNotice = null; }
        if (settings.EndSound) PlaySoundStyle(settings.EndSoundStyle);
        CliNoticeDialog dlg = new CliNoticeDialog(settings, message);
        activeNotice = dlg;
        dlg.FormClosed += delegate
        {
            if (activeNotice == dlg) activeNotice = null;
            if (isOneShot) Exit();
        };
        dlg.Show();
        ForceTopMost(dlg);
    }

    internal void ShowAskHumanDialog(string question, string[] options, int timeoutSec, string resFile)
    {
        bool isFg = AppContext.IsAgentInForeground();
        PlaySoundStyle(settings != null && !string.IsNullOrEmpty(settings.EndSoundStyle) ? settings.EndSoundStyle : "Phone");
        AskHumanDialog dlg = new AskHumanDialog(settings, question, options, timeoutSec, resFile, isFg);
        dlg.Show();
        if (isFg)
        {
            try
            {
                dlg.TopMost = true;
                NativeMethods.SetWindowPos(dlg.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0053);
            }
            catch { }
        }
        else
        {
            ForceTopMost(dlg);
        }
    }

    internal void ShowCustomQuote(string text, string author)
    {
        if (activeQuoteDialog != null && !activeQuoteDialog.IsDisposed)
        {
            activeQuoteDialog.Close();
            activeQuoteDialog = null;
        }
        DailyQuoteDialog dlg = new DailyQuoteDialog(settings, text, author);
        activeQuoteDialog = dlg;
        dlg.FormClosed += delegate { if (activeQuoteDialog == dlg) activeQuoteDialog = null; };
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
                    if (isOneShot) Exit();
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
            activeNotice.FormClosed += delegate
            {
                activeNotice = null;
                if (state == ClockState.Alarming) SetState(ClockState.Idle);
                if (isOneShot) Exit();
            };
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
            if (isOneShot) Exit();
        }
    }

    private void ToggleMove()
    {
        bool enabled = !overlay.MoveMode;
        overlay.SetMoveMode(enabled, running);
        if (activeQuoteDialog != null && !activeQuoteDialog.IsDisposed)
        {
            activeQuoteDialog.SetMoveMode(enabled);
        }
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
        ShowTrayNotification("iClock", enabled ? (en ? "Drag the translucent area to move countdown or quote. Use the tray menu to finish." : "拖动半透明区域调整倒计时或格言位置，再次从托盘菜单退出调整模式。") : (en ? "Position saved." : "文字位置已保存。"), 2000);
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
    private static byte[] phoneWavData;
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

    private static byte[] GeneratePhoneWav()
    {
        int sampleRate = 22050;
        List<short> samples = new List<short>();

        for (int ring = 0; ring < 2; ring++)
        {
            int ringSamples = (int)(sampleRate * 0.80);
            int strikePeriod = (int)(sampleRate * 0.050); // 20Hz -> 50ms (2 alternating strikes)
            int halfPeriod = strikePeriod / 2;             // 25ms per strike

            for (int i = 0; i < ringSamples; i++)
            {
                int strikePhase = i % strikePeriod;
                bool firstBell = (strikePhase < halfPeriod);
                int timeSinceStrike = firstBell ? strikePhase : (strikePhase - halfPeriod);
                double tStrike = (double)timeSinceStrike / sampleRate;

                // Bell frequencies: Gong 1 = 753 Hz, Gong 2 = 840 Hz (classic telephone brass bell)
                double f = firstBell ? 753.0 : 840.0;
                double decay = Math.Exp(-timeSinceStrike / (sampleRate * 0.024));

                double s1 = Math.Sin(2.0 * Math.PI * f * tStrike);
                double s2 = 0.20 * Math.Sin(2.0 * Math.PI * f * 2.76 * tStrike);

                double ringEnv = 1.0;
                int ramp = (int)(sampleRate * 0.02);
                if (i < ramp) ringEnv = (double)i / ramp;
                else if (i > ringSamples - ramp) ringEnv = (double)(ringSamples - i) / ramp;

                double sample = (s1 + s2) * decay * ringEnv * 18000.0;
                samples.Add((short)Math.Max(-32767, Math.Min(32767, sample)));
            }

            if (ring == 0)
            {
                int pauseSamples = (int)(sampleRate * 0.35);
                for (int i = 0; i < pauseSamples; i++) samples.Add(0);
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
            else if (style == "Phone" || style == "Pager" || style == "Classic")
            {
                if (phoneWavData == null) phoneWavData = GeneratePhoneWav();
                MemoryStream ms = new MemoryStream(phoneWavData);
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

    internal static bool IsAgentInForeground()
    {
        try
        {
            IntPtr fg = NativeMethods.GetForegroundWindow();
            if (fg == IntPtr.Zero) return false;
            uint pid = 0;
            NativeMethods.GetWindowThreadProcessId(fg, out pid);
            if (pid == 0) return false;
            using (Process p = Process.GetProcessById((int)pid))
            {
                string name = MatchAgent(p.ProcessName);
                if (name != null) return true;
                if (!string.IsNullOrEmpty(p.MainWindowTitle))
                {
                    string titleName = MatchAgent(p.MainWindowTitle);
                    if (titleName != null) return true;
                }
            }
        }
        catch { }
        return false;
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

    internal static bool BringAgentToFront(string agentName)
    {
        if (string.IsNullOrEmpty(agentName)) return false;
        try
        {
            foreach (Process p in Process.GetProcesses())
            {
                using (p)
                {
                    string match = MatchAgent(p.ProcessName);
                    if (match != null && match.Equals(agentName, StringComparison.OrdinalIgnoreCase))
                    {
                        IntPtr hwnd = p.MainWindowHandle;
                        if (hwnd != IntPtr.Zero && NativeMethods.IsWindow(hwnd))
                        {
                            NativeMethods.ShowWindow(hwnd, 9); // SW_RESTORE
                            NativeMethods.SetForegroundWindow(hwnd);
                            return true;
                        }
                    }
                }
            }
        }
        catch { }
        return false;
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

    private void SetQuotePreview(Settings draft, bool visible)
    {
        if (!visible)
        {
            if (activeQuoteDialog != null && !activeQuoteDialog.IsDisposed)
            {
                if (activeDialog is SettingsDialog)
                {
                    SettingsDialog sd = (SettingsDialog)activeDialog;
                    if (!sd.IsQuoteDesktopVisible)
                    {
                        activeQuoteDialog.IsPinned = false;
                        activeQuoteDialog.Close();
                        activeQuoteDialog = null;
                        return;
                    }
                }
                if (activeQuoteDialog.IsPinned || (activeDialog is SettingsDialog && ((SettingsDialog)activeDialog).IsQuoteDesktopVisible))
                {
                    activeQuoteDialog.IsPreviewMode = false;
                    if (draft != null)
                    {
                        if (activeDialog is SettingsDialog && ((SettingsDialog)activeDialog).ActiveTab != 1)
                        {
                            draft.QuoteFontSize = activeQuoteDialog.CurrentFontSize;
                        }
                        activeQuoteDialog.ApplyDraftSettings(draft);
                    }
                    return;
                }
                activeQuoteDialog.Close();
                activeQuoteDialog = null;
            }
            return;
        }

        if (activeQuoteDialog == null || activeQuoteDialog.IsDisposed)
        {
            activeQuoteDialog = new DailyQuoteDialog(draft ?? settings);
            activeQuoteDialog.IsPreviewMode = true;
            if (activeDialog is SettingsDialog)
            {
                SettingsDialog sd = (SettingsDialog)activeDialog;
                activeQuoteDialog.CurrentQuoteIndex = sd.DialogQuoteIndex;
            }
            activeQuoteDialog.FontSizeChanged += delegate(float sz)
            {
                try { if (activeDialog is SettingsDialog) ((SettingsDialog)activeDialog).SyncQuoteFontSize(sz); } catch { }
            };
            activeQuoteDialog.QuoteChanged += delegate
            {
                try { if (activeDialog is SettingsDialog) ((SettingsDialog)activeDialog).SyncNextQuote(); } catch { }
            };
            activeQuoteDialog.PinChanged += delegate(bool pinned)
            {
                try { if (pinned && activeDialog is SettingsDialog) ((SettingsDialog)activeDialog).SetDesktopQuoteChecked(true); } catch { }
            };
            activeQuoteDialog.FormClosed += delegate
            {
                try { if (activeDialog is SettingsDialog) ((SettingsDialog)activeDialog).SetDesktopQuoteChecked(false); } catch { }
                activeQuoteDialog = null;
            };
            if (overlay.MoveMode) activeQuoteDialog.SetMoveMode(true);
            activeQuoteDialog.Show();
        }
        else
        {
            activeQuoteDialog.IsPreviewMode = true;
            if (activeDialog is SettingsDialog)
            {
                SettingsDialog sd = (SettingsDialog)activeDialog;
                if (activeQuoteDialog.CurrentQuoteIndex != sd.DialogQuoteIndex)
                {
                    activeQuoteDialog.SetQuoteIndex(sd.DialogQuoteIndex);
                }
            }
            activeQuoteDialog.ApplyDraftSettings(draft ?? settings);
        }
    }

    private void ShowSettings()
    {
        ShowSettingsTab(0);
    }

    internal void ShowSettingsTab(int initialTab)
    {
        if (activeDialog != null && !activeDialog.IsDisposed)
        {
            if (activeDialog is SettingsDialog)
            {
                SettingsDialog sd = (SettingsDialog)activeDialog;
                if (initialTab >= 0) sd.SetActiveTab(initialTab);
                ActivateWindow(sd);
                return;
            }
            activeDialog.Close();
        }

        bool wasOverlayVisible = overlay.Visible;
        if (!wasOverlayVisible)
        {
            overlay.Show();
        }

        SettingsDialog d = new SettingsDialog(settings, hotkeyRegistered);
        if (initialTab > 0) d.SetActiveTab(initialTab);
        activeDialog = d;

        Action<float> quoteFontSync = delegate(float sz)
        {
            try { d.SyncQuoteFontSize(sz); } catch { }
        };
        Action quoteChangeSync = delegate
        {
            try { d.SyncNextQuote(); } catch { }
        };

        if (activeQuoteDialog != null && !activeQuoteDialog.IsDisposed)
        {
            d.SyncQuoteFontSize(activeQuoteDialog.CurrentFontSize);
            activeQuoteDialog.FontSizeChanged += quoteFontSync;
            activeQuoteDialog.QuoteChanged += quoteChangeSync;
        }

        SetPreviewSettings(d.GetCurrentDraftSettings());

        d.PreviewRequested += delegate(Settings preview)
        {
            SetPreviewSettings(preview);
        };

        d.QuotePreviewRequested += delegate(Settings preview, bool visible)
        {
            SetQuotePreview(preview, visible);
        };

        d.QuoteNextRequested += delegate
        {
            if (activeQuoteDialog != null && !activeQuoteDialog.IsDisposed)
            {
                activeQuoteDialog.NextQuote();
            }
        };

        d.FormClosed += delegate
        {
            if (activeQuoteDialog != null && !activeQuoteDialog.IsDisposed)
            {
                activeQuoteDialog.FontSizeChanged -= quoteFontSync;
                activeQuoteDialog.QuoteChanged -= quoteChangeSync;
            }
            SetPreviewSettings(null);
            bool quotePinned = activeQuoteDialog != null && !activeQuoteDialog.IsDisposed && activeQuoteDialog.IsPinned;
            SetQuotePreview(null, false);
            Settings draft = d.GetCurrentDraftSettings();
            ApplySavedSettings(draft);
            if (draft.DailyQuote && d.IsQuoteDesktopVisible)
            {
                if (activeQuoteDialog != null && !activeQuoteDialog.IsDisposed)
                {
                    activeQuoteDialog.IsPinned = true;
                    activeQuoteDialog.IsPreviewMode = false;
                    activeQuoteDialog.ApplyDraftSettings(draft);
                }
                else
                {
                    CheckDailyQuote(true);
                    if (activeQuoteDialog != null && !activeQuoteDialog.IsDisposed)
                    {
                        activeQuoteDialog.IsPinned = true;
                    }
                }
            }
            else if (quotePinned && activeQuoteDialog != null && !activeQuoteDialog.IsDisposed)
            {
                activeQuoteDialog.ApplyDraftSettings(draft);
            }
            else
            {
                if (activeQuoteDialog != null && !activeQuoteDialog.IsDisposed && !quotePinned)
                {
                    activeQuoteDialog.Close();
                    activeQuoteDialog = null;
                }
            }
            if (!wasOverlayVisible)
            {
                overlay.Hide();
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
        if (settings != null && newSettings != null && settings != newSettings)
        {
            settings.CopyFrom(newSettings);
        }
        else
        {
            settings = newSettings;
        }
        settings.Save(); overlay.SetSettings(settings); ApplyStartup();
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
        if (hadHotkey) NativeMethods.UnregisterHotKey(messageWindow.Handle, HOTKEY_ID);
        bool ok = NativeMethods.RegisterHotKey(messageWindow.Handle, HOTKEY_ID, (uint)(modifiers | MOD_NOREPEAT), (uint)key);
        if (ok) { activeHotkeyModifiers = modifiers; activeHotkeyKey = key; hotkeyRegistered = true; return true; }
        hotkeyRegistered = false;
        if (hadHotkey && NativeMethods.RegisterHotKey(messageWindow.Handle, HOTKEY_ID, (uint)(oldModifiers | MOD_NOREPEAT), (uint)oldKey)) hotkeyRegistered = true;
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
        btnNext.FlatAppearance.MouseOverBackColor = Color.FromArgb(228, 232, 238);
        btnNext.Cursor = Cursors.Hand;
        tip.SetToolTip(btnNext, en ? "Next Day (Right Arrow)" : "后一天（支持 → 快捷键）");

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
        btnPrev.FlatAppearance.MouseOverBackColor = Color.FromArgb(228, 232, 238);
        btnPrev.Cursor = Cursors.Hand;
        tip.SetToolTip(btnPrev, en ? "Previous Day (Left Arrow)" : "前一天（支持 ← 快捷键）");

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

    internal static void OpenQuotesFile()
    {
        try
        {
            string path = Settings.QuotesFilePath;
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            if (!File.Exists(path))
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# iClock 每日格言 / Daily Quotes");
                sb.AppendLine("# 格式：[序号. ]名言正文 —— 作者/出处 （支持使用 1. / 02、 / [3] 等序号，作者可省略）");
                sb.AppendLine("# Format: [No. ]Quote text -- Author (leading numbers like 1. / 02. are optional, author is optional)");
                sb.AppendLine("# 提示：序号仅用于在此文本中排版整理，桌面及卡片展示时会自动智能隐藏序号。");
                sb.AppendLine("# Tip: Numbers help you organize quotes here; they are automatically hidden when displayed.");
                sb.AppendLine();
                int qIdx = 1;
                foreach (QuoteItem q in DailyQuoteDialog.DefaultQuotes)
                {
                    string prefix = qIdx + ". ";
                    if (!string.IsNullOrEmpty(q.Author))
                        sb.AppendLine(prefix + q.Text + " —— " + q.Author);
                    else
                        sb.AppendLine(prefix + q.Text);
                    qIdx++;
                }
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            }
            Process.Start("notepad.exe", path);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Failed to open quotes file: " + ex.Message, "iClock", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    internal void CheckDailyQuote(bool forceShow = false, Settings customSettings = null)
    {
        try
        {
            if (activeQuoteDialog != null && !activeQuoteDialog.IsDisposed)
            {
                if (activeQuoteDialog.IsPinned && !forceShow) return;
                activeQuoteDialog.Close();
                activeQuoteDialog = null;
            }

            Settings effective = customSettings ?? settings;
            if (!forceShow)
            {
                if (!effective.DailyQuote || !effective.QuoteDesktopVisible) return;
                string today = DateTime.Today.ToString("yyyy-MM-dd");
                if (string.Equals(effective.LastQuoteDate, today, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
                effective.LastQuoteDate = today;
                effective.Save();
            }

            activeQuoteDialog = new DailyQuoteDialog(effective);
            activeQuoteDialog.FontSizeChanged += delegate(float sz)
            {
                try { if (activeDialog is SettingsDialog) ((SettingsDialog)activeDialog).SyncQuoteFontSize(sz); } catch { }
            };
            activeQuoteDialog.QuoteChanged += delegate
            {
                try { if (activeDialog is SettingsDialog) ((SettingsDialog)activeDialog).SyncNextQuote(); } catch { }
            };
            activeQuoteDialog.PinChanged += delegate(bool pinned)
            {
                try { if (pinned && activeDialog is SettingsDialog) ((SettingsDialog)activeDialog).SetDesktopQuoteChecked(true); } catch { }
            };
            activeQuoteDialog.FormClosed += delegate
            {
                try { if (activeDialog is SettingsDialog) ((SettingsDialog)activeDialog).SetDesktopQuoteChecked(false); } catch { }
                activeQuoteDialog = null;
                if (isOneShot) Exit();
            };
            if (overlay.MoveMode) activeQuoteDialog.SetMoveMode(true);
            activeQuoteDialog.Show();
        }
        catch { }
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
        if (activeQuoteDialog != null && !activeQuoteDialog.IsDisposed) { activeQuoteDialog.Close(); activeQuoteDialog = null; }
        if (running) remaining = ReadRemaining();
        if (sessionActive) { LogSession("Interrupted"); sessionActive = false; }
        timer.Stop(); tray.Visible = false; if (hotkeyRegistered) NativeMethods.UnregisterHotKey(messageWindow.Handle, HOTKEY_ID);
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
            bool ok = NativeMethods.RegisterHotKey(nw.Handle, testId, (uint)(modifiers | MOD_NOREPEAT), (uint)key);
            if (ok) NativeMethods.UnregisterHotKey(nw.Handle, testId);
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

    internal static void PaintMcpBadge(Graphics g, Rectangle r, string text)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        using (SolidBrush bg = new SolidBrush(Color.FromArgb(229, 241, 251)))
        using (GraphicsPath path = GetRoundedRectPath(new Rectangle(0, 0, r.Width - 1, r.Height - 1), 3))
        {
            g.FillPath(bg, path);
        }
        using (SolidBrush fg = new SolidBrush(Win11Accent))
        using (Font f = new Font("Segoe UI", 8f, FontStyle.Bold))
        using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
        {
            g.DrawString(text, f, fg, new Rectangle(0, 0, r.Width, r.Height), sf);
        }
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
                string currentIcon = scType.InvokeMember("IconLocation", BindingFlags.GetProperty, null, sc, null) as string;
                string expIcon = targetExe + ",0";
                if (string.Equals(currentTarget, targetExe, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(currentIcon, expIcon, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            scType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { targetExe });
            scType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { Path.GetDirectoryName(targetExe) });
            scType.InvokeMember("Description", BindingFlags.SetProperty, null, sc, new object[] { "iClock Desktop Floating Countdown Timer" });
            scType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, sc, new object[] { targetExe + ",0" });
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

    internal void ShowTrayNotification(string title, string text, int timeoutMs = 2500)
    {
        try
        {
            if (TryShowNativeToast(title, text)) return;
        }
        catch { }
        try
        {
            if (tray != null) tray.ShowBalloonTip(timeoutMs, title, text, ToolTipIcon.None);
        }
        catch { }
    }

    internal static bool TryShowNativeToast(string title, string message)
    {
        try
        {
            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "iClock");
            if (!Directory.Exists(appData)) Directory.CreateDirectory(appData);
            string logoPath = Path.Combine(appData, "app-logo.png");
            if (!File.Exists(logoPath))
            {
                using (Bitmap bmp = new Bitmap(128, 128, PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.Clear(Color.Transparent);
                        DrawLogoVector(g, 0, 0, 128);
                    }
                    bmp.Save(logoPath, ImageFormat.Png);
                }
            }

            string logoUri = new Uri(logoPath).AbsoluteUri;
            Type toastManagerType = Type.GetType("Windows.UI.Notifications.ToastNotificationManager, Windows, ContentType=WindowsRuntime");
            Type xmlDocType = Type.GetType("Windows.Data.Xml.Dom.XmlDocument, Windows, ContentType=WindowsRuntime");
            Type toastNotifType = Type.GetType("Windows.UI.Notifications.ToastNotification, Windows, ContentType=WindowsRuntime");
            if (toastManagerType == null || xmlDocType == null || toastNotifType == null) return false;

            string safeTitle = System.Security.SecurityElement.Escape(title ?? "iClock");
            string safeMessage = System.Security.SecurityElement.Escape(message ?? "");

            string xml = string.Format(
                "<toast>" +
                "  <visual>" +
                "    <binding template=\"ToastGeneric\">" +
                "      <text>{0}</text>" +
                "      <text>{1}</text>" +
                "      <image placement=\"appLogoOverride\" hint-crop=\"none\" src=\"{2}\"/>" +
                "    </binding>" +
                "  </visual>" +
                "</toast>", safeTitle, safeMessage, logoUri);

            object xmlDoc = Activator.CreateInstance(xmlDocType);
            xmlDocType.GetMethod("LoadXml", new Type[] { typeof(string) }).Invoke(xmlDoc, new object[] { xml });
            object notif = Activator.CreateInstance(toastNotifType, new object[] { xmlDoc });

            MethodInfo createNotifier = toastManagerType.GetMethod("CreateToastNotifier", new Type[] { typeof(string) });
            object notifier = createNotifier.Invoke(null, new object[] { "iClock" });
            MethodInfo showMethod = notifier.GetType().GetMethod("Show", new Type[] { toastNotifType });
            showMethod.Invoke(notifier, new object[] { notif });
            return true;
        }
        catch { return false; }
    }

    private void OnUpdateFound(string newVersion, string url)
    {
        if (string.IsNullOrEmpty(url)) url = "https://github.com/francodeyvison183-collab/iClock/releases/latest";
        latestVersion = newVersion;
        updateUrl = url;
        bool en = settings.Language == "en";
        string title = en ? "iClock Update Available" : "iClock 发现新版本";
        string text = en ? "New version " + newVersion + " is available. Click to download." : "发现新版本 " + newVersion + "，点击前往下载更新。";

        try { ShowTrayNotification(title, text, 6000); } catch { }

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

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}


internal static class Program
{
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
            if (IsCliConfiguredInPath())
            {
                message = "CLI path is already configured in User PATH.";
                return true;
            }
            string dir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
            string userPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
            string newPath = string.IsNullOrEmpty(userPath) ? dir : (userPath.TrimEnd(';') + ";" + dir);
            Environment.SetEnvironmentVariable("PATH", newPath, EnvironmentVariableTarget.User);
            message = "CLI environment configured successfully.";
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
  -a, --ask <question>            Flash Approval HUD: request human decision
                                  (options: --options ""A,B"", timeout: -t <sec>)
  -q, --quote                     Show daily quote popup
  --once                          Single-run mode: exit automatically after completion
                                  (ignored if desktop instance is already running)
  --quit, --exit                  Gracefully exit running iClock instance
  --mcp                           Start Model Context Protocol (MCP) stdio server
  --mcp-prompt                    Print AI agent MCP setup prompt
  --mcp-config                    Print MCP client JSON configuration snippet
  --install-cli                   Add iClock directory to User PATH
  -h, --help                      Show this help information

Examples:
  iclock --start 25               Start a 25-minute Pomodoro timer
  iclock -s 25 --once             Run a 25-minute timer and exit upon completion
  iclock -a ""Deploy to prod?"" --options ""Approve,Reject""
  iclock -n ""Build finished!""     Trigger completion notice from script
  iclock --mcp-prompt             Print setup prompt for AI agents
  iclock --mcp-config             Output MCP JSON configuration snippet
  iclock --quit                   Close running iClock instance";
        WriteToConsole(help);
    }

    private static bool SendIpcMessage(string cmd)
    {
        IntPtr hWnd = NativeMethods.FindWindow(null, "iClock.IpcWindow");
        if (hWnd == IntPtr.Zero) return false;
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
            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
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
            if (first == "--mcp-prompt" || first == "mcp-prompt")
            {
                string prompt = McpConfigHelper.GenerateAgentPrompt(false);
                WriteToConsole(prompt);
                return;
            }
            if (first == "--mcp-config" || first == "mcp-config")
            {
                string cfg = McpConfigHelper.GenerateJsonSnippet(false);
                WriteToConsole(cfg);
                return;
            }
            if (first == "--install-cli" || first == "install-cli")
            {
                InstallCli();
                return;
            }
            if (first == "quit" || first == "--quit" || first == "exit" || first == "--exit" || first == "-x")
            {
                if (SendIpcMessage("quit"))
                    WriteToConsole("[iClock] Exit command sent. iClock terminated.");
                else
                    WriteToConsole("[iClock] iClock is not running.");
                return;
            }
        }

        bool created;
        using (System.Threading.Mutex m = new System.Threading.Mutex(true, "iClock.SingleInstance", out created))
        {
            if (!created)
            {
                bool hasOnce = false;
                List<string> cleanArgs = new List<string>();
                if (args != null && args.Length > 0)
                {
                    foreach (string a in args)
                    {
                        if (string.Equals(a, "--once", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(a, "-once", StringComparison.OrdinalIgnoreCase))
                            hasOnce = true;
                        else
                            cleanArgs.Add(a);
                    }
                }

                if (cleanArgs.Count == 0) cleanArgs.Add(hasOnce ? "--start" : "activate");
                string cmd = string.Join(" ", cleanArgs.ToArray());
                if (SendIpcMessage(cmd))
                {
                    if (hasOnce) WriteToConsole("[iClock] Command sent. Desktop instance is already running (desktop instance will remain active).");
                    else if (cmd == "activate") WriteToConsole("[iClock] Already running. Activated settings dialog.");
                    else WriteToConsole("[iClock] Command sent: " + cmd);
                }
                else
                {
                    WriteToConsole("[iClock] Already running in background tray.");
                }
                return;
            }

            bool isOneShot = false;
            List<string> runArgs = new List<string>();
            if (args != null && args.Length > 0)
            {
                foreach (string a in args)
                {
                    if (string.Equals(a, "--once", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(a, "-once", StringComparison.OrdinalIgnoreCase))
                    {
                        isOneShot = true;
                    }
                    else
                    {
                        runArgs.Add(a);
                    }
                }
            }

            if (isOneShot)
            {
                if (runArgs.Count == 0)
                {
                    runArgs.Add("--start");
                }
                WriteToConsole("[iClock] Started in single-run mode (--once). Will exit after completion.");
            }
            else if (runArgs.Count > 0)
            {
                WriteToConsole("[iClock] Started in background with: " + string.Join(" ", runArgs.ToArray()));
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new AppContext(runArgs.ToArray(), isOneShot));
        }
    }
}
