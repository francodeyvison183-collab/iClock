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
using System.Security.AccessControl;
using System.Security.Principal;
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
    public int RestMinutes = 10;
    public string EndSoundStyle = "Doorbell";
    public bool EndNotice = true;
    public bool FirstRun;
    public string NoticeStyle = "Popup";
    public bool FullscreenDnd = true;
    public bool AgentProgress;
    // 旧总开关。只在设置文件还没有下面两个键时，用来一次性迁到两边。
    public bool DesktopCollaborationEnabled = true;
    // 自动感知智能体，并把状态置顶到桌面。
    public bool AgentStatusPinEnabled = true;
    // 本机是否接收决策请示和完成提醒。智能体会不会来叫，仍由技能或提示词决定。
    public bool AgentDeepCollabEnabled = true;
    public bool DailyQuote = true;
    public int QuoteX = -99999;
    public int QuoteY = -99999;
    public int QuoteWidth = -1;
    public float QuoteFontSize = 18.0f;
    public string QuoteColor = "";
    public string QuoteFont = "";
    public bool QuotePinTopMost;
    public int QuoteIndex = -1;
    public string QuoteDate = "";
    // Use today's quote as the Windows screensaver (installed as iClock.scr).
    // Off by default: taking over the user's Windows screensaver is opt-in.
    public bool QuoteSaverEnabled;
    // Previous HKCU\Control Panel\Desktop screensaver values (SCR / ScreenSaveActive /
    // ScreenSaveTimeOut), captured before iClock takes them over so opting out can put
    // the user's own screensaver back. Empty when nothing was captured yet.
    public string SaverRegBackup = "";
    // Idle wait before Windows starts the quote screensaver, in minutes. Windows' own
    // screen saver dialog cannot list iClock (it only enumerates System32 .scr files),
    // so iClock owns this value and writes ScreenSaveTimeOut itself.
    public int SaverMinutes = 10;
    public int TaskStatusX = -99999;
    public int TaskStatusY = -99999;
    public float TaskStatusFontSize = 9.5f;
    public string TaskStatusTextColor = "";
    public string TaskStatusBarColor = "";
    public int TaskStatusWidth = 300;
    public int TaskStatusHeight = 28;
    public string AiQuoteKeywords = "";
    public int AiQuoteCount = 30;
    // 每日专注目标（分钟）。0 = 关闭：不画进度条、托盘不显示打卡文案。
    public int DailyGoalMinutes = 0;
    // 电脑使用时间跟踪。默认关闭，避免未同意时后台装钩。
    // 用时统计始终开启（只记进程名与时长,不碰内容）;字段仅为兼容旧代码保留,恒为 true。
    public bool UsageTrackEnabled = true;
    // 护眼提醒：连续盯屏满间隔后弹轻提醒卡，可一键进入休息倒计时。
    public bool EyeCareEnabled = true;
    // 提醒间隔（分钟），可选 20/30/45/60。
    public int EyeCareIntervalMinutes = 45;

    public void CopyFrom(Settings src)
    {
        if (src == null) return;
        Minutes = src.Minutes; FontSize = src.FontSize; X = src.X; Y = src.Y;
        HotkeyModifiers = src.HotkeyModifiers; HotkeyKey = src.HotkeyKey;
        Color = src.Color; FontFamily = src.FontFamily; Format = src.Format;
        EndMessage = src.EndMessage; Language = src.Language; AutoStart = src.AutoStart;
        EndSound = src.EndSound; RestMinutes = src.RestMinutes; EndSoundStyle = src.EndSoundStyle; EndNotice = src.EndNotice;
        FirstRun = src.FirstRun; NoticeStyle = src.NoticeStyle; FullscreenDnd = src.FullscreenDnd;
        AgentProgress = src.AgentProgress; DesktopCollaborationEnabled = src.DesktopCollaborationEnabled;
        AgentStatusPinEnabled = src.AgentStatusPinEnabled; AgentDeepCollabEnabled = src.AgentDeepCollabEnabled; DailyQuote = src.DailyQuote;
        QuoteX = src.QuoteX; QuoteY = src.QuoteY; QuoteWidth = src.QuoteWidth;
        QuoteFontSize = src.QuoteFontSize; QuoteColor = src.QuoteColor; QuoteFont = src.QuoteFont;
        QuotePinTopMost = src.QuotePinTopMost;
        QuoteIndex = src.QuoteIndex; QuoteDate = src.QuoteDate;
        QuoteSaverEnabled = src.QuoteSaverEnabled;
        SaverRegBackup = src.SaverRegBackup;
        SaverMinutes = src.SaverMinutes > 0 ? src.SaverMinutes : 10;
        TaskStatusX = src.TaskStatusX; TaskStatusY = src.TaskStatusY;
        TaskStatusFontSize = src.TaskStatusFontSize > 0 ? src.TaskStatusFontSize : 9.5f;
        TaskStatusTextColor = src.TaskStatusTextColor; TaskStatusBarColor = src.TaskStatusBarColor;
        TaskStatusWidth = src.TaskStatusWidth > 0 ? Math.Max(160, Math.Min(800, src.TaskStatusWidth)) : 300;
        TaskStatusHeight = src.TaskStatusHeight > 0 ? Math.Max(28, Math.Min(60, src.TaskStatusHeight)) : 28;
        AiQuoteKeywords = src.AiQuoteKeywords;
        AiQuoteCount = src.AiQuoteCount > 0 ? src.AiQuoteCount : 30;
        DailyGoalMinutes = src.DailyGoalMinutes;
        UsageTrackEnabled = src.UsageTrackEnabled;
        EyeCareEnabled = src.EyeCareEnabled;
        EyeCareIntervalMinutes = src.EyeCareIntervalMinutes > 0 ? src.EyeCareIntervalMinutes : 45;
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
        bool sawStatusPin = false;
        bool sawDeepCollab = false;
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
                else if (k == "EndSoundStyle" && (v == "Doorbell" || v == "Chime" || v == "QQ" || v == "Phone" || v == "Pager" || v == "Knock" || v == "Alarm" || v == "Classic")) s.EndSoundStyle = (v == "Chime" || v == "Doorbell") ? "Doorbell" : ((v == "Classic" || v == "Pager") ? "Phone" : v);
                else if (k == "RestMinutes" && int.TryParse(v, out n)) s.RestMinutes = (n >= 1 && n <= 60) ? n : 10;
                else if (k == "EndNotice" && bool.TryParse(v, out b)) s.EndNotice = b;
                else if (k == "FirstRun" && bool.TryParse(v, out b)) s.FirstRun = b;
                else if (k == "NoticeStyle" && (v == "Popup" || v == "Pulse")) s.NoticeStyle = v;
                else if (k == "FullscreenDnd" && bool.TryParse(v, out b)) s.FullscreenDnd = b;
                else if (k == "AgentProgress" && bool.TryParse(v, out b)) s.AgentProgress = b;
                else if (k == "DesktopCollaborationEnabled" && bool.TryParse(v, out b)) s.DesktopCollaborationEnabled = b;
                else if (k == "AgentStatusPinEnabled" && bool.TryParse(v, out b)) { s.AgentStatusPinEnabled = b; sawStatusPin = true; }
                else if (k == "AgentDeepCollabEnabled" && bool.TryParse(v, out b)) { s.AgentDeepCollabEnabled = b; sawDeepCollab = true; }
                else if (k == "DailyQuote" && bool.TryParse(v, out b)) s.DailyQuote = b;
                else if (k == "QuoteX" && int.TryParse(v, out n)) s.QuoteX = n;
                else if (k == "QuoteY" && int.TryParse(v, out n)) s.QuoteY = n;
                else if (k == "QuoteWidth" && int.TryParse(v, out n)) s.QuoteWidth = n;
                else if (k == "QuoteFontSize" && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out f)) s.QuoteFontSize = f > 0 ? Math.Max(8.0f, Math.Min(24.0f, f)) : 18.0f;
                else if (k == "QuoteColor") s.QuoteColor = v;
                else if (k == "QuoteFont") s.QuoteFont = v;
                else if (k == "QuotePinTopMost" && bool.TryParse(v, out b)) s.QuotePinTopMost = b;
                else if (k == "QuoteIndex" && int.TryParse(v, out n)) s.QuoteIndex = n;
                else if (k == "QuoteDate") s.QuoteDate = v;
                else if (k == "QuoteSaverEnabled" && bool.TryParse(v, out b)) s.QuoteSaverEnabled = b;
                else if (k == "SaverRegBackup") s.SaverRegBackup = v;
                else if (k == "SaverMinutes" && int.TryParse(v, out n)) s.SaverMinutes = (n >= 1 && n <= 60) ? n : 10;
                else if (k == "QuoteSaverMode" && int.TryParse(v, out n)) s.QuoteSaverEnabled = n >= 1; // v1.11 迁移:旧三态 >=1 视为开启
                else if (k == "TaskStatusX" && int.TryParse(v, out n)) s.TaskStatusX = n;
                else if (k == "TaskStatusY" && int.TryParse(v, out n)) s.TaskStatusY = n;
                else if (k == "TaskStatusFontSize" && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out f)) s.TaskStatusFontSize = f > 0 ? Math.Max(7.0f, Math.Min(16.0f, f)) : 9.5f;
                else if (k == "TaskStatusTextColor") s.TaskStatusTextColor = v;
                else if (k == "TaskStatusBarColor") s.TaskStatusBarColor = v;
                else if (k == "TaskStatusWidth" && int.TryParse(v, out n)) s.TaskStatusWidth = n > 0 ? Math.Max(160, Math.Min(800, n)) : 300;
                else if (k == "TaskStatusHeight" && int.TryParse(v, out n)) s.TaskStatusHeight = n > 0 ? Math.Max(28, Math.Min(60, n)) : 28;
                else if (k == "AiQuoteKeywords") s.AiQuoteKeywords = v;
                else if (k == "AiQuoteCount" && int.TryParse(v, out n)) s.AiQuoteCount = n > 0 ? n : 30;
                else if (k == "DailyGoalMinutes" && int.TryParse(v, out n)) s.DailyGoalMinutes = Math.Max(0, Math.Min(1440, n));
                else if (k == "EyeCareEnabled" && bool.TryParse(v, out b)) s.EyeCareEnabled = b;
                else if (k == "EyeCareIntervalMinutes" && int.TryParse(v, out n)) s.EyeCareIntervalMinutes = (n >= 5 && n <= 180) ? n : 45;
            }
            if (!sawStatusPin) s.AgentStatusPinEnabled = s.DesktopCollaborationEnabled;
            if (!sawDeepCollab) s.AgentDeepCollabEnabled = s.DesktopCollaborationEnabled;
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
            "EndSound=" + EndSound, "EndSoundStyle=" + EndSoundStyle, "EndNotice=" + EndNotice, "FirstRun=" + FirstRun, "NoticeStyle=" + NoticeStyle, "FullscreenDnd=" + FullscreenDnd,
            "RestMinutes=" + RestMinutes,
            "AgentProgress=" + AgentProgress,
            "DesktopCollaborationEnabled=" + DesktopCollaborationEnabled,
            "AgentStatusPinEnabled=" + AgentStatusPinEnabled,
            "AgentDeepCollabEnabled=" + AgentDeepCollabEnabled,
            "DailyQuote=" + DailyQuote, "QuoteX=" + QuoteX, "QuoteY=" + QuoteY, "QuoteWidth=" + QuoteWidth, "QuoteFontSize=" + (QuoteFontSize > 0 ? QuoteFontSize.ToString("0.0", CultureInfo.InvariantCulture) : "18.0"),
            "QuoteColor=" + QuoteColor, "QuoteFont=" + QuoteFont, "QuotePinTopMost=" + QuotePinTopMost,
            "QuoteIndex=" + QuoteIndex, "QuoteDate=" + QuoteDate, "QuoteSaverEnabled=" + QuoteSaverEnabled,
            "SaverRegBackup=" + SaverRegBackup,
            "SaverMinutes=" + SaverMinutes,
            "TaskStatusX=" + TaskStatusX, "TaskStatusY=" + TaskStatusY,
            "TaskStatusFontSize=" + (TaskStatusFontSize > 0 ? TaskStatusFontSize.ToString("0.0", CultureInfo.InvariantCulture) : "9.5"),
            "TaskStatusTextColor=" + TaskStatusTextColor, "TaskStatusBarColor=" + TaskStatusBarColor,
            "TaskStatusWidth=" + TaskStatusWidth,
            "TaskStatusHeight=" + TaskStatusHeight,
            "AiQuoteKeywords=" + AiQuoteKeywords
            , "AiQuoteCount=" + AiQuoteCount
            , "DailyGoalMinutes=" + DailyGoalMinutes
            , "EyeCareEnabled=" + EyeCareEnabled
            , "EyeCareIntervalMinutes=" + EyeCareIntervalMinutes
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
[StructLayout(LayoutKind.Sequential)] internal struct WINDOWPLACEMENT
{
    public int length;
    public int flags;
    public int showCmd;
    public POINT ptMinPosition;
    public POINT ptMaxPosition;
    public RECT rcNormalPosition;
}

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
    [DllImport("user32.dll")] public static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hWnd);
    public const uint GA_ROOT = 2;
    [DllImport("user32.dll")] public static extern IntPtr GetDesktopWindow();
    [DllImport("user32.dll")] public static extern IntPtr GetShellWindow();
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
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
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr SendMessageTimeout(IntPtr hWnd, int msg, IntPtr wParam, string lParam, int flags, int timeout, out IntPtr result);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

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
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumDesktopWindows(IntPtr hDesktop, EnumWindowsProc lpfn, IntPtr lParam);
    [DllImport("user32.dll")] public static extern IntPtr OpenInputDesktop(uint dwFlags, bool fInherit, uint dwDesiredAccess);
    [DllImport("user32.dll")] public static extern bool CloseDesktop(IntPtr hDesktop);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)] public static extern int GetWindowTextLength(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct PROCESSENTRY32
    {
        const int MAX_PATH = 260;
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
        public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern bool QueryFullProcessImageName(IntPtr hProcess, int flags, StringBuilder text, ref int size);
    [DllImport("kernel32.dll")]
    public static extern ulong GetTickCount64();
    [DllImport("kernel32.dll")]
    public static extern IntPtr GetCurrentProcess();

    // PEB-based current directory resolution (ProcessProjectRootResolver)
    [DllImport("ntdll.dll")]
    public static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass,
        out PROCESS_BASIC_INFORMATION processInformation, int processInformationLength, out int returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress,
        byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_BASIC_INFORMATION
    {
        public IntPtr Reserved1;
        public IntPtr PebBaseAddress;
        public IntPtr Reserved2_0;
        public IntPtr Reserved2_1;
        public IntPtr UniqueProcessId;
        public IntPtr Reserved3;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }
    [DllImport("user32.dll")]
    public static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    public const uint EVENT_SYSTEM_MINIMIZESTART = 0x0016;
    public const uint EVENT_SYSTEM_MINIMIZEEND = 0x0017;
    public const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    public const uint WINEVENT_SKIPOWNPROCESS = 0x0002;
    public delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);
    [DllImport("user32.dll")]
    public static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);
    [DllImport("user32.dll")]
    public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    public delegate void ProcessEnumCallback(int pid, string exeName);
}

