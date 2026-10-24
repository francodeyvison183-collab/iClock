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


internal sealed class AppContext : ApplicationContext
{
    private static AppContext currentInstance;
    internal static AppContext Current { get { return currentInstance; } }

    // Resolve today's quote index: same-day reuse saved index, new day advance by one.
    // Persists immediately so the setting page and overlay stay in sync.
    internal static int ResolveTodayQuoteIndex(Settings s, int count)
    {
        if (count <= 0) return 0;
        string today = DateTime.Today.ToString("");
        int idx;
        if (s.QuoteDate == today && s.QuoteIndex >= 0)
        {
            idx = s.QuoteIndex % count;
        }
        else if (!string.IsNullOrEmpty(s.QuoteDate) && s.QuoteIndex >= 0)
        {
            idx = (s.QuoteIndex + 1) % count;
        }
        else
        {
            int seed = DateTime.Today.Year * 366 + DateTime.Today.DayOfYear;
            idx = Math.Abs(seed) % count;
        }
        s.QuoteIndex = idx;
        s.QuoteDate = today;
        s.Save();
        return idx;
    }

    internal static bool IsSelfCheckRunning = false;

    private const int HOTKEY_ID = 0x4A10;
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_NOREPEAT = 0x4000;
    private readonly Settings settings;
    private Overlay overlay;
    private AgentTaskStatusHud taskStatusHud;
    private readonly List<AgentTaskStatus> agentTaskStatuses = new List<AgentTaskStatus>();
    private Timer hudClearDeferTimer;
    private bool hudClearPending;
    private NotifyIcon tray;
    private ToolStripMenuItem menuStart, menuReset, menuMove, menuHistory, menuSettings, menuLanguage, menuLangZh, menuLangEn, menuAbout, menuExit, menuUpdate;
    private MenuSwitchItem menuStartup;
    private bool holdTrayMenuOpen;
    internal const string CURRENT_VERSION = "";
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
    private DailyQuoteDialog activeProjectedNote;
    private AskHumanDialog activeAskHumanDialog;
    private bool dailyQuoteSuspendedForProjection;
    private Timer projectedAgentMonitorTimer;
    private string projectedAgentName;
    private int projectedAgentPid;
    private long lastSeconds = -1;
    private long lastLiveFocusMinutes = -1;
    private long cachedLoggedFocusSecs = -1;
    private string cachedLoggedFocusDay;
    private MessageWindow messageWindow;
    private Icon appIcon;
    private Timer trayClickTimer;
    private int lastTrayDoubleClickTick;

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
    private UsageTracker usageTracker;
    private UsageStripItem menuUsageStrip;
    private bool openHistoryOnUsageTab;
    private Action<bool> historySetUsageTab;
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
            GC.Collect(2, GCCollectionMode.Forced);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced);
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
            bool en = settings.Language == "";
            string tip = en ? "" + settings.Minutes + "" : "" + settings.Minutes + "";
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
        ApplyPendingUpdateOnStartup();
        appIcon = CreateIcon();
        overlay = new Overlay(settings); overlay.PositionChanged += delegate(Point p) { settings.X = p.X; settings.Y = p.Y; settings.Save(); };
        taskStatusHud = new AgentTaskStatusHud(settings);
        overlay.LocationChanged += delegate { UpdateTaskStatusHudPosition(); };
        overlay.SizeChanged += delegate { UpdateTaskStatusHudPosition(); };
        overlay.VisibleChanged += delegate { UpdateTaskStatusHudPosition(); };
        overlay.TimeAdjustRequested += delegate(int delta) { AdjustMinutes(delta); };
        IntPtr forceOverlayHandle = overlay.Handle;
        messageWindow = new MessageWindow(this);
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
        tray = new NotifyIcon(); tray.Icon = appIcon; tray.Text = isOneShot ? (settings.Language == "" ? "" : "") : ""; tray.Visible = true; tray.ContextMenuStrip = MakeMenu();
        // The screensaver registration can be wiped by a cleanup tool or a
        // Windows reset; re-assert it while the toggle is still on.
        Program.EnsureSaverRegistry(settings);
        tray.MouseMove += delegate { if (!running && !sessionActive) { lastTrayMouseMove = Environment.TickCount; EnsureMouseHook(); } };
        tray.BalloonTipClicked += delegate { if (!string.IsNullOrEmpty(updateUrl)) { try { Process.Start(updateUrl); } catch { } } };
        tray.MouseClick += delegate(object sender, MouseEventArgs e)
        {
            UnhookMouse();
            if (e.Button == MouseButtons.Left)
            {
                if (e.Clicks > 1 || Math.Abs(Environment.TickCount - lastTrayDoubleClickTick) < 500)
                {
                    if (trayClickTimer != null) trayClickTimer.Stop();
                    return;
                }
                if (trayClickTimer == null)
                {
                    trayClickTimer = new Timer();
                    trayClickTimer.Interval = Math.Min(250, Math.Max(150, SystemInformation.DoubleClickTime - 50));
                    trayClickTimer.Tick += delegate
                    {
                        if (trayClickTimer != null) trayClickTimer.Stop();
                        if (Math.Abs(Environment.TickCount - lastTrayDoubleClickTick) >= 500)
                        {
                            Toggle();
                        }
                    };
                }
                trayClickTimer.Stop();
                trayClickTimer.Start();
            }
        };
        tray.DoubleClick += delegate
        {
            lastTrayDoubleClickTick = Environment.TickCount;
            if (trayClickTimer != null) trayClickTimer.Stop();
            ShowSettings();
        };
        timer = new Timer(); timer.Interval = 100; timer.Tick += Tick;
        Timer startupTrim = new Timer(); startupTrim.Interval = 3500;
        startupTrim.Tick += delegate { startupTrim.Stop(); startupTrim.Dispose(); TrimMemory(); };
        startupTrim.Start();
        ApplyStartup();
        EnsureStartMenuShortcut(false);
        ResetDisplay();
        UpdateDailyProgress();
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
            if (usageTracker != null) usageTracker.OnSessionSwitch(se.Reason);
        };
        SystemEvents.SessionSwitch += sessionSwitchHandler;
        usageTracker = new UsageTracker(overlay);
        if (settings.UsageTrackEnabled && !isOneShot && !IsSelfCheckRunning)
            usageTracker.Start();
        if (tray != null && tray.ContextMenuStrip != null)
        {
            RefreshUsageStrip(tray.ContextMenuStrip);
            FitTrayMenuWidth(tray.ContextMenuStrip);
        }
        if (fallbackUsed && !settings.FirstRun)
        {
            bool en = settings.Language == "";
            string oldKeyText = HotkeyText(originalMods, originalKey);
            string newKeyText = HotkeyText(settings.HotkeyModifiers, settings.HotkeyKey);
            string msg = en
                ? "" + oldKeyText + "" + newKeyText + ""
                : "" + oldKeyText + "" + newKeyText + "";
            overlay.BeginInvoke(new Action(delegate { MessageBox.Show(msg, "", MessageBoxButtons.OK, MessageBoxIcon.Information); }));
        }
        else if (!hotkeyRegistered && !settings.FirstRun)
        {
            bool en = settings.Language == "";
            string msg = en
                ? ""
                : "";
            overlay.BeginInvoke(new Action(delegate
            {
                if (MessageBox.Show(msg, "", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    ShowSettings();
                }
            }));
        }
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
                string tip = (settings.Language == "")
                    ? "" + hotkeyStr
                    : "" + hotkeyStr + "";
                ShowTrayNotification("", tip, 2500);
            }
            catch { }
        }
        if (initialArgs != null && initialArgs.Length > 0)
        {
            HandleIpcCommand(string.Join("", initialArgs));
        }
        else if (isOneShot)
        {
            HandleIpcCommand("");
        }
    }

    private void ShowWelcome(bool fallbackUsed = false)
    {
        if (!settings.FirstRun || IsSelfCheckRunning) return;
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
            m.SuspendLayout();
            try
            {
                m.Font = GetUiFont(settings.Language, 9.5f);
                if (menuStart != null) menuStart.Font = new Font(m.Font, FontStyle.Bold);
                UpdateMenuText();
            }
            finally { m.ResumeLayout(false); }
            FitTrayMenuWidth(m);
            RefreshUsageStrip(m);
        };
        m.Closing += delegate(object sender, ToolStripDropDownClosingEventArgs e)
        {
            // 点语言子项时，主菜单收到的往往是 AppClicked / AppFocusChange，不是 ItemClicked。
            // hold 只在开关和语言的 MouseDown 里置位，点外面时是 false，照常关掉。
            bool keep = holdTrayMenuOpen
                && e.CloseReason != ToolStripDropDownCloseReason.Keyboard
                && e.CloseReason != ToolStripDropDownCloseReason.CloseCalled;
            holdTrayMenuOpen = false;
            if (keep) e.Cancel = true;
        };

        menuStart = AddMenuItem(m, delegate { Toggle(); });
        menuStart.Font = new Font(m.Font, FontStyle.Bold);
        menuReset = AddMenuItem(m, delegate { Reset(); });
        menuMove = AddMenuItem(m, delegate { ToggleMove(); });
        m.Items.Add(new ToolStripSeparator());
        menuHistory = AddMenuItem(m, delegate { openHistoryOnUsageTab = false; ShowHistory(); });
        menuUsageStrip = new UsageStripItem();
        menuUsageStrip.Click += delegate { openHistoryOnUsageTab = true; ShowHistory(); };
        menuUsageStrip.Visible = false;
        m.Items.Add(menuUsageStrip);
        menuSettings = AddMenuItem(m, delegate { ShowSettings(); });
        m.Items.Add(new ToolStripSeparator());
        menuLanguage = AddMenuItem(m, null);
        menuLangZh = new ToolStripMenuItem("");
        menuLangZh.Padding = new Padding(32, 7, 32, 7);
        menuLangZh.ForeColor = Win11TextPrimary;
        menuLangZh.MouseDown += delegate { holdTrayMenuOpen = true; };
        menuLangZh.Click += delegate { SetInterfaceLanguage(false); };
        menuLangEn = new ToolStripMenuItem("");
        menuLangEn.Padding = new Padding(32, 7, 32, 7);
        menuLangEn.ForeColor = Win11TextPrimary;
        menuLangEn.MouseDown += delegate { holdTrayMenuOpen = true; };
        menuLangEn.Click += delegate { SetInterfaceLanguage(true); };
        menuLanguage.DropDownItems.Add(menuLangZh);
        menuLanguage.DropDownItems.Add(menuLangEn);
        menuStartup = new MenuSwitchItem();
        menuStartup.Padding = new Padding(32, 7, 32, 7);
        menuStartup.ForeColor = Win11TextPrimary;
        menuStartup.MouseDown += delegate { holdTrayMenuOpen = true; };
        menuStartup.Click += delegate { ToggleAutoStartFromMenu(); };
        m.Items.Add(menuStartup);
        m.Items.Add(new ToolStripSeparator());
        menuAbout = AddMenuItem(m, delegate { ShowAbout(); });
        m.Items.Add(new ToolStripSeparator());
        menuExit = AddMenuItem(m, delegate { Exit(); });
        UpdateMenuText();
        FitTrayMenuWidth(m);
        return m;
    }

    private int MeasureMenuRow(Font font, string text, string shortcut, bool hasSwitch, bool hasArrow)
    {
        int tw = TextRenderer.MeasureText(text ?? "", font).Width;
        int extra = 24;
        if (hasSwitch) extra = 24 + 40 + 8;
        else if (!string.IsNullOrEmpty(shortcut))
            extra = 28 + TextRenderer.MeasureText(shortcut, font).Width + 24;
        else if (hasArrow)
            extra = 24 + 16;
        return 32 + tw + extra;
    }

    // 中英文各测一遍，取更宽的那套，切换语言时菜单宽度不动。
    private void FitTrayMenuWidth(ContextMenuStrip m)
    {
        if (m == null) return;
        Font zh = GetUiFont("", 9.5f);
        Font en = GetUiFont("", 9.5f);
        Font zhBold = new Font(zh, FontStyle.Bold);
        Font enBold = new Font(en, FontStyle.Bold);
        int w = 260;
        try
        {
            string hotkey = hotkeyRegistered
                ? HotkeyText(settings.HotkeyModifiers, settings.HotkeyKey)
                : null;
            string[] starts = new string[]
            {
                "", "", "", "", "",
                "", "", "", "", ""
            };
            foreach (string s in starts)
            {
                w = Math.Max(w, MeasureMenuRow(enBold, s, hotkey ?? "", false, false));
                w = Math.Max(w, MeasureMenuRow(zhBold, s, hotkey ?? "", false, false));
            }
            w = Math.Max(w, MeasureMenuRow(en, "", null, false, false));
            w = Math.Max(w, MeasureMenuRow(zh, "", null, false, false));
            w = Math.Max(w, MeasureMenuRow(en, "", null, false, false));
            w = Math.Max(w, MeasureMenuRow(zh, "", null, false, false));
            w = Math.Max(w, MeasureMenuRow(en, "", "", false, false));
            w = Math.Max(w, MeasureMenuRow(zh, "", "", false, false));
            w = Math.Max(w, MeasureMenuRow(en, "", null, false, false));
            w = Math.Max(w, MeasureMenuRow(zh, "", null, false, false));
            w = Math.Max(w, MeasureMenuRow(en, "", "", false, false));
            w = Math.Max(w, MeasureMenuRow(zh, "", "", false, false));
            w = Math.Max(w, MeasureMenuRow(en, "", null, false, false));
            w = Math.Max(w, MeasureMenuRow(zh, "", null, false, false));
            w = Math.Max(w, MeasureMenuRow(en, "", "", false, true));
            w = Math.Max(w, MeasureMenuRow(zh, "", "", false, true));
            w = Math.Max(w, MeasureMenuRow(en, "", null, true, false));
            w = Math.Max(w, MeasureMenuRow(zh, "", null, true, false));
            w = Math.Max(w, MeasureMenuRow(en, "", null, false, false));
            w = Math.Max(w, MeasureMenuRow(zh, "", null, false, false));
            w = Math.Max(w, MeasureMenuRow(en, "", null, false, false));
            w = Math.Max(w, MeasureMenuRow(zh, "", null, false, false));
            if (!string.IsNullOrEmpty(latestVersion))
            {
                w = Math.Max(w, MeasureMenuRow(en, "" + latestVersion + "", null, false, false));
                w = Math.Max(w, MeasureMenuRow(zh, "" + latestVersion + "", null, false, false));
            }
        }
        finally
        {
            zhBold.Dispose();
            enBold.Dispose();
        }
        m.MinimumSize = new Size(w, 0);
        m.MaximumSize = new Size(w, 4096);
        if (m.Width != w) m.Width = w;
        m.PerformLayout();
    }

    private void SetInterfaceLanguage(bool english)
    {
        string next = english ? "" : "";
        if (settings.Language == next)
        {
            UpdateMenuText();
            return;
        }
        settings.Language = next;
        settings.Save();
        // 菜单还开着时改 Font 会把整份菜单收掉；下次 Opening 再换字体。
        if (overlay != null) overlay.RefreshSettings();
        SettingsDialog open = activeDialog as SettingsDialog;
        if (open != null && !open.IsDisposed) open.ApplyInterfaceLanguage(english);
        UpdateMenuText();
        if (tray != null && tray.ContextMenuStrip != null)
        {
            FitTrayMenuWidth(tray.ContextMenuStrip);
            tray.ContextMenuStrip.Invalidate();
        }
        UpdateDailyProgress();
    }

    private void ToggleAutoStartFromMenu()
    {
        bool next = !settings.AutoStart;
        if (next && IsInEphemeralFolder(Application.ExecutablePath))
        {
            holdTrayMenuOpen = false;
            if (tray != null && tray.ContextMenuStrip != null) tray.ContextMenuStrip.Close();
            bool en = settings.Language == "";
            string msg = en
                ? "" + Application.ExecutablePath + ""
                : "" + Application.ExecutablePath + "";
            string caption = en ? "" : "";
            if (MessageBox.Show(msg, caption, MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.No)
                next = false;
        }
        if (settings.AutoStart == next)
        {
            UpdateMenuText();
            if (menuStartup != null) menuStartup.Invalidate();
            return;
        }
        settings.AutoStart = next;
        settings.Save();
        ApplyStartup();
        UpdateMenuText();
        if (menuStartup != null) menuStartup.Invalidate();
    }

    internal void Reset()
    {
        if (overlay.IsPulsing) overlay.StopBreathingPulse();
        ClearAgentProgress();
        if (activeNotice != null && !activeNotice.IsDisposed) { activeNotice.Close(); activeNotice = null; }
        if (running) remaining = ReadRemaining();
        timer.Stop();
        if (sessionActive)
        {
            LogSession((state == ClockState.RestRunning || state == ClockState.RestPaused) ? "" : "");
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
        if (onClick != null) item.Click += onClick;
        m.Items.Add(item);
        return item;
    }

    private void UpdateMenuText()
    {
        bool en = settings.Language == "";
        if (menuStart == null) return;

        if (state == ClockState.RestRunning)
        {
            menuStart.Text = en ? "" : "";
            menuStart.ForeColor = Color.FromArgb(202, 80, 16);
        }
        else if (state == ClockState.RestPaused)
        {
            menuStart.Text = en ? "" : "";
            menuStart.ForeColor = Color.FromArgb(16, 124, 65);
        }
        else if (running)
        {
            menuStart.Text = en ? "" : "";
            menuStart.ForeColor = Color.FromArgb(202, 80, 16);
        }
        else if (sessionActive)
        {
            menuStart.Text = en ? "" : "";
            menuStart.ForeColor = Color.FromArgb(16, 124, 65);
        }
        else
        {
            menuStart.Text = en ? "" : "";
            menuStart.ForeColor = Color.FromArgb(0, 103, 192);
        }

        menuStart.ShortcutKeyDisplayString = hotkeyRegistered
            ? HotkeyText(settings.HotkeyModifiers, settings.HotkeyKey)
            : (en ? "" : "");
        menuReset.Text = (state == ClockState.RestRunning || state == ClockState.RestPaused) ? (en ? "" : "") : (en ? "" : "");
        menuReset.Enabled = running || sessionActive;
        menuMove.Text = overlay.MoveMode ? (en ? "" : "") : (en ? "" : "");
        menuMove.ShortcutKeyDisplayString = overlay.MoveMode ? "" : (en ? "" : "");
        menuHistory.Text = en ? "" : "";
        menuHistory.ShortcutKeyDisplayString = BuildHistoryMenuHint();
        menuSettings.Text = en ? "" : "";
        if (menuLanguage != null)
        {
            menuLanguage.Text = en ? "" : "";
            menuLanguage.ShortcutKeyDisplayString = en ? "" : "";
        }
        if (menuLangZh != null) menuLangZh.Text = (en ? "" : "") + (en ? "" : "");
        if (menuLangEn != null) menuLangEn.Text = (en ? "" : "") + "";
        if (menuStartup != null)
        {
            menuStartup.Text = en ? "" : "";
            menuStartup.On = settings.AutoStart;
        }
        menuAbout.Text = en ? "" : "";
        menuExit.Text = en ? "" : "";
        if (menuUpdate != null) menuUpdate.Text = en ? "" + latestVersion + "" : "" + latestVersion + "";
        string actionTip = running ? (en ? "" : "")
            : (sessionActive ? (en ? "" : "") : (en ? "" : ""));
        tray.Text = hotkeyRegistered
            ? (en ? "" + actionTip + "" : "" + actionTip + "")
            : (en ? "" + actionTip + "" : "" + actionTip + "");
    }

    private void SetUsageTracking(bool on)
    {
        settings.UsageTrackEnabled = on;
        settings.Save();
        if (usageTracker == null) return;
        if (on && !isOneShot && !IsSelfCheckRunning) usageTracker.Start();
        else usageTracker.Stop();
    }

    private void RefreshUsageStrip(ContextMenuStrip m)
    {
        if (menuUsageStrip == null) return;
        bool show = settings.UsageTrackEnabled && !isOneShot;
        menuUsageStrip.Available = show;
        menuUsageStrip.Visible = show;
        if (!show) return;
        string today = DateTime.Today.ToString("");
        List<UsageSegment> segs = usageTracker != null ? usageTracker.GetDayWithOpen(today) : UsageLog.LoadDay(today);
        DateTime start = UsageLog.EstimateBootOrMidnight();
        if (start.Date != DateTime.Today) start = DateTime.Today;
        DateTime end = DateTime.Now;
        if (segs.Count > 0)
        {
            if (segs[0].Start < start) start = segs[0].Start;
            DateTime last = segs[segs.Count - 1].End;
            if (last > end) end = last;
        }
        menuUsageStrip.SetSegments(segs, start, end);
    }

    // 左侧固定「今日记录」，右侧提示位写当天累计；有目标时带上 / 目标。连胜只在历史窗口里看。
    private string BuildHistoryMenuHint()
    {
        try
        {
            long today = TodayFocusSecs();
            string todayPart = FormatCompactDuration(today);
            if (settings.DailyGoalMinutes <= 0) return todayPart;
            return todayPart + "" + FormatCompactDuration(settings.DailyGoalMinutes * 60L);
        }
        catch
        {
            return "";
        }
    }

    // 已写入历史的今日时长 + 当前这段未结束的专注（休息不加）。
    private long TodayFocusSecs()
    {
        return LoggedTodayFocusSecs() + OpenWorkFocusSecs();
    }

    private long LoggedTodayFocusSecs()
    {
        string day = DateTime.Today.ToString("");
        if (cachedLoggedFocusSecs >= 0 && cachedLoggedFocusDay == day)
            return cachedLoggedFocusSecs;
        string dir = Path.GetDirectoryName(Settings.FilePath);
        cachedLoggedFocusSecs = AppContext.GetDayFocusSecs(dir, day);
        cachedLoggedFocusDay = day;
        return cachedLoggedFocusSecs;
    }

    private void InvalidateTodayFocusCache()
    {
        cachedLoggedFocusSecs = -1;
    }

    private long OpenWorkFocusSecs()
    {
        string start;
        string end;
        int minutes;
        long actual;
        bool isRest;
        if (!TryGetOpenHistoryRow(out start, out end, out minutes, out actual, out isRest) || isRest)
            return 0L;
        if (sessionStart.Date != DateTime.Today) return 0L;
        return actual;
    }

    private bool TryGetOpenHistoryRow(out string start, out string end, out int minutes, out long actualSecs, out bool isRest)
    {
        start = "";
        end = "";
        minutes = 0;
        actualSecs = 0L;
        isRest = false;
        if (!sessionActive) return false;
        if (state == ClockState.Idle || state == ClockState.MoveMode || state == ClockState.Alarming)
            return false;
        isRest = (state == ClockState.RestRunning || state == ClockState.RestPaused);
        start = sessionStart.ToString("");
        minutes = sessionMinutes;
        TimeSpan left = running ? ReadRemaining() : remaining;
        actualSecs = Math.Max(0L, Math.Min(sessionMinutes * 60L, (long)Math.Floor((TimeSpan.FromMinutes(sessionMinutes) - left).TotalSeconds)));
        return true;
    }

    // 重算今日进度并刷新悬浮条与托盘文案；进行中的专注按整分钟刷新。
    private void UpdateDailyProgress()
    {
        try
        {
            if (overlay == null || overlay.IsDisposed) return;
            if (settings.DailyGoalMinutes <= 0) { overlay.SetDailyProgress(-1f, false); UpdateMenuText(); return; }
            long today = TodayFocusSecs();
            long goal = settings.DailyGoalMinutes * 60L;
            bool full = today >= goal;
            overlay.SetDailyProgress(Math.Min(1f, (float)((double)today / goal)), full);
            UpdateMenuText();
        }
        catch { }
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
        UpdateDailyProgress();
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
        lastLiveFocusMinutes = 0;
        timer.Interval = 100;
        timer.Start();
        overlay.SetPaused(false);
        overlay.Show();
        Tick(null, EventArgs.Empty);
        UpdateDailyProgress();
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
        if (IsSelfCheckRunning)
        {
            // Headless test mode: toggle state only, skip UI side effects
            if (running) { Pause(); return; }
            if (sessionActive) { Resume(); return; }
            StartWork();
            return;
        }
        if (overlay.IsPulsing) { overlay.StopBreathingPulse(); SetState(ClockState.Idle); return; }
        if (running) { Pause(); return; }
        if (sessionActive) { Resume(); return; }
        StartWork();
    }

    internal bool HandleIpcCommand(string cmd)
    {
        if (string.IsNullOrEmpty(cmd)) return false;
        if (overlay != null && overlay.InvokeRequired)
        {
            try { return (bool)overlay.Invoke(new Func<bool>(delegate { return HandleIpcCommand(cmd); })); }
            catch { return false; }
        }
        cmd = cmd.Trim();
        string cliAgentName = null;
        const string agentNameArg = "";
        int agentNameIndex = cmd.LastIndexOf(agentNameArg, StringComparison.OrdinalIgnoreCase);
        if (agentNameIndex >= 0)
        {
            cliAgentName = cmd.Substring(agentNameIndex + agentNameArg.Length).Trim().Trim('"');
            cmd = cmd.Substring(0, agentNameIndex).Trim();
            if (cliAgentName.Length == 0) cliAgentName = null;
        }
        if (cmd.IndexOf("", StringComparison.OrdinalIgnoreCase) >= 0 || cmd.IndexOf("", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            cmd = System.Text.RegularExpressions.Regex.Replace(cmd, @"", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
        }
        string[] parts = cmd.Split(new char[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;
        string action = parts[0].ToLowerInvariant();
        string arg = parts.Length > 1 ? parts[1].Trim() : null;

        if (!settings.DesktopCollaborationEnabled)
        {
            bool collaborationCommand = action == "" || action == "" || action == ""
                || action.StartsWith("") || action == "" || action == "" || action == ""
                || action.StartsWith("") || action == "" || action == "" || action == ""
                || action.StartsWith("") || action == "" || action.StartsWith("");
            string payload = action.StartsWith("") ? cmd.Substring("".Length).Trim()
                : action.StartsWith("") ? cmd.Substring("".Length).Trim() : (arg ?? "");
            bool cleanup = (action == "" || action.StartsWith(""))
                || ((action == "" || action == "" || action == "" || action.StartsWith(""))
                    && (payload.Equals("", StringComparison.OrdinalIgnoreCase) || payload.Equals("", StringComparison.OrdinalIgnoreCase)
                        || payload.StartsWith("", StringComparison.OrdinalIgnoreCase) || payload.StartsWith("", StringComparison.OrdinalIgnoreCase)))
                || (action == "" || action == "" || action == "" || action.StartsWith(""))
                    && (payload.Equals("", StringComparison.OrdinalIgnoreCase) || payload.Equals("", StringComparison.OrdinalIgnoreCase)
                        || payload.Equals("", StringComparison.OrdinalIgnoreCase));
            if (collaborationCommand && !cleanup) return false;
        }

        if (action == "" || action == "" || action == "")
        {
            int mins = settings.Minutes;
            if (!string.IsNullOrEmpty(arg))
            {
                int parsed;
                if (int.TryParse(arg, out parsed) && parsed > 0) mins = parsed;
            }
            StartWorkWithMinutes(mins);
        }
        else if (action == "" || action == "" || action == "")
        {
            Pause();
        }
        else if (action == "" || action == "" || action == "")
        {
            Resume();
        }
        else if (action == "" || action == "" || action == "")
        {
            Toggle();
        }
        else if (action == "" || action == "")
        {
            Reset();
        }
        else if (action == "" || action == "")
        {
            int mins = 5;
            if (!string.IsNullOrEmpty(arg))
            {
                int parsed;
                if (int.TryParse(arg, out parsed) && parsed > 0) mins = parsed;
            }
            StartRest(mins);
        }
        else if (action == "" || action == "" || action == "" || action.StartsWith(""))
        {
            string raw = action.StartsWith("") ? cmd.Substring("".Length).Trim() : (arg ?? "");
            string snd = null;
            string agentName = null;
            if (raw.Contains(""))
            {
                string[] np = raw.Split(new char[] { '|' }, 3);
                snd = np[0];
                raw = np.Length > 1 ? np[1] : "";
                if (np.Length > 2 && !string.IsNullOrEmpty(np[2])) agentName = np[2];
            }
            if (!string.IsNullOrEmpty(snd)) PlaySoundStyle(snd);
            TriggerCliNotice(raw, string.IsNullOrEmpty(agentName) ? cliAgentName : agentName);
        }
        else if (action == "" || action == "" || action == "" || action.StartsWith(""))
        {
            string raw = action.StartsWith("") ? cmd.Substring("".Length).Trim() : (arg ?? "");
            if (!string.IsNullOrEmpty(raw))
            {
                if (raw.Equals("", StringComparison.OrdinalIgnoreCase) || raw.Equals("", StringComparison.OrdinalIgnoreCase))
                {
                    if (activeProjectedNote != null && !activeProjectedNote.IsDisposed)
                    {
                        try { activeProjectedNote.Close(); } catch { }
                        activeProjectedNote = null;
                    }
                }
                else
                {
                    string[] qp = raw.Split(new char[] { '|' }, 4);
                    string text = qp[0];
                    string author = qp.Length > 1 ? qp[1] : "";
                    string agentName = qp.Length > 2 && !string.IsNullOrEmpty(qp[2]) ? qp[2] : null;
                    int agentPid = 0;
                    if (qp.Length > 3) int.TryParse(qp[3], out agentPid);
                    ShowCustomQuote(text, author, agentName, agentPid);
                }
            }
            else
            {
                CheckDailyQuote(true);
            }
        }
        else if (action == "" || action.StartsWith(""))
        {
            string batchFile = (action.StartsWith("") ? cmd.Substring("".Length).Trim() : (arg ?? "")).Trim('"');
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
                    string resFile = batchFile + "";
                    File.WriteAllText(resFile, addedCount.ToString(), Encoding.UTF8);
                }
                catch { }
            }
        }
        else if (action == "" || action.StartsWith(""))
        {
            string payload = action.StartsWith("") ? cmd.Substring("".Length).Trim() : (arg ?? "");
            string[] p = payload.Split(new char[] { '|' }, 5);
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
                string agentName = (p.Length > 4 && !string.IsNullOrEmpty(p[4])) ? p[4] : cliAgentName;
                string resFile = Path.Combine(Path.GetTempPath(), "" + reqId + "");
                ShowAskHumanDialog(question, options, timeoutSec, resFile, agentName);
            }
        }
        else if (action == "" || action.StartsWith(""))
        {
            if (activeAskHumanDialog != null && !activeAskHumanDialog.IsDisposed)
            {
                try { activeAskHumanDialog.Close(); } catch { }
                activeAskHumanDialog = null;
            }
        }
        else if (action == "" || action == "" || action == "" || action.StartsWith(""))
        {
            string payload = action.StartsWith("") ? cmd.Substring("".Length).Trim() : (arg ?? "");
            HandleProgressCommand(payload, cliAgentName);
        }
        else if (action == "" || action == "" || action == "" || action == "" || action == "" || action == "")
        {
            ShowSettings();
        }
        else if (action == "" || action == "" || action == "" || action == "" || action == "")
        {
            Exit();
        }
        return true;
    }

    internal bool HasActiveDesktopCollaboration
    {
        get { return (activeAskHumanDialog != null && !activeAskHumanDialog.IsDisposed) || agentTaskStatuses.Count > 0; }
    }

    internal void CancelDesktopCollaboration()
    {
        if (activeAskHumanDialog != null && !activeAskHumanDialog.IsDisposed)
        {
            try { activeAskHumanDialog.Close(); } catch { }
            activeAskHumanDialog = null;
        }
        ClearAgentProgress();
    }

    internal void HandleProgressCommand(string payload, string agentName = null)
    {
        string statusAgent = GetTaskAgentName(agentName, settings.Language == "" ? "" : "");
        if (string.IsNullOrEmpty(payload) || payload.Equals("", StringComparison.OrdinalIgnoreCase) || payload.Equals("", StringComparison.OrdinalIgnoreCase) || payload.Equals("", StringComparison.OrdinalIgnoreCase))
        {
            ClearAgentProgress(statusAgent);
            return;
        }

        string valStr = payload;
        string msg = "";
        int pipeIdx = payload.IndexOf('|');
        if (pipeIdx >= 0)
        {
            valStr = payload.Substring(0, pipeIdx).Trim();
            msg = payload.Substring(pipeIdx + 1).Trim();
        }
        else
        {
            int spaceIdx = payload.IndexOf(' ');
            if (spaceIdx >= 0)
            {
                valStr = payload.Substring(0, spaceIdx).Trim();
                msg = payload.Substring(spaceIdx + 1).Trim();
            }
        }

        float pct = 0f;
        if (valStr.Contains(""))
        {
            string[] slashParts = valStr.Split('/');
            float num, den;
            if (slashParts.Length == 2 && float.TryParse(slashParts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out num) && float.TryParse(slashParts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out den) && den > 0f)
            {
                pct = num / den;
            }
        }
        else
        {
            string cleanVal = valStr.Replace("", "").Trim();
            float raw;
            if (float.TryParse(cleanVal, NumberStyles.Float, CultureInfo.InvariantCulture, out raw))
            {
                if (valStr.Contains("") || raw > 1.0f) pct = raw / 100.0f;
                else if (valStr.Contains("")) pct = raw;
                else pct = raw / 100.0f;
            }
        }

        SetAgentProgress(pct, msg, statusAgent);
    }

    private static string GetTaskAgentName(string agentName, string fallback)
    {
        return string.IsNullOrWhiteSpace(agentName) ? fallback : agentName.Trim();
    }

    private AgentTaskStatus FindTaskStatus(string agentName)
    {
        for (int i = 0; i < agentTaskStatuses.Count; i++)
        {
            if (string.Equals(agentTaskStatuses[i].AgentName, agentName, StringComparison.OrdinalIgnoreCase))
                return agentTaskStatuses[i];
        }
        return null;
    }

    private void RefreshTaskStatusHud()
    {
        if (taskStatusHud == null || taskStatusHud.IsDisposed) return;
        taskStatusHud.SetTasks(agentTaskStatuses);
        if (agentTaskStatuses.Count == 0)
        {
            taskStatusHud.Hide();
            return;
        }
        UpdateTaskStatusHudPosition();
        if (!taskStatusHud.Visible) taskStatusHud.Show();
        UpdateTaskStatusHudPosition();
    }

    private void UpdateTaskStatusHudPosition()
    {
        if (taskStatusHud == null || taskStatusHud.IsDisposed || overlay == null || agentTaskStatuses.Count == 0) return;
        // If the user dragged the HUD somewhere, keep that spot instead of chasing the clock.
        if (settings != null && (settings.TaskStatusX != -99999 || settings.TaskStatusY != -99999))
        {
            int x = settings.TaskStatusX != -99999 ? settings.TaskStatusX : taskStatusHud.Location.X;
            int y = settings.TaskStatusY != -99999 ? settings.TaskStatusY : taskStatusHud.Location.Y;
            taskStatusHud.Location = new Point(x, y);
            return;
        }
        taskStatusHud.UpdatePosition(overlay.Bounds);
    }

    internal void SetAgentProgress(float percent, string message, string agentName = null)
    {
        if (overlay != null && overlay.InvokeRequired)
        {
            overlay.BeginInvoke(new Action<float, string, string>(SetAgentProgress), percent, message, agentName);
            return;
        }
        string name = GetTaskAgentName(agentName, settings.Language == "" ? "" : "");
        AgentTaskStatus status = FindTaskStatus(name);
        if (status == null)
        {
            status = new AgentTaskStatus { AgentName = name };
            agentTaskStatuses.Add(status);
        }
        status.HasProgress = true;
        status.Progress = Math.Max(0f, Math.Min(1f, percent));
        if (!string.IsNullOrWhiteSpace(message)) status.Message = message;
        RefreshTaskStatusHud();
    }

    internal void SetAgentTaskMessage(string message, string agentName)
    {
        if (overlay != null && overlay.InvokeRequired)
        {
            overlay.BeginInvoke(new Action<string, string>(SetAgentTaskMessage), message, agentName);
            return;
        }
        string name = GetTaskAgentName(agentName, settings.Language == "" ? "" : "");
        AgentTaskStatus status = FindTaskStatus(name);
        if (status == null)
        {
            status = new AgentTaskStatus { AgentName = name };
            agentTaskStatuses.Add(status);
        }
        status.Message = message;
        RefreshTaskStatusHud();
    }

    internal void ClearAgentProgress(string agentName = null)
    {
        if (overlay != null && overlay.InvokeRequired)
        {
            overlay.BeginInvoke(new Action<string>(ClearAgentProgress), agentName);
            return;
        }

        // While Alt is held (user is tweaking HUD settings), defer the clear so the HUD stays visible.
        bool altHeld = (NativeMethods.GetAsyncKeyState(0x12) & 0x8000) != 0;
        if (altHeld)
        {
            hudClearPending = true;
            StartHudClearDeferTimer();
            return;
        }

        if (string.IsNullOrWhiteSpace(agentName))
        {
            agentTaskStatuses.Clear();
        }
        else
        {
            for (int i = agentTaskStatuses.Count - 1; i >= 0; i--)
            {
                if (string.Equals(agentTaskStatuses[i].AgentName, agentName.Trim(), StringComparison.OrdinalIgnoreCase))
                    agentTaskStatuses.RemoveAt(i);
            }
        }
        RefreshTaskStatusHud();
    }

    private void StartHudClearDeferTimer()
    {
        if (hudClearDeferTimer == null)
        {
            hudClearDeferTimer = new Timer();
            hudClearDeferTimer.Interval = 200;
            hudClearDeferTimer.Tick += OnHudClearDeferTick;
        }
        hudClearDeferTimer.Stop();
        hudClearDeferTimer.Start();
    }

    private DateTime altReleasedAt = DateTime.MinValue;
    private bool wasAltHeld;

    private void OnHudClearDeferTick(object sender, EventArgs e)
    {
        bool alt = (NativeMethods.GetAsyncKeyState(0x12) & 0x8000) != 0;
        if (alt)
        {
            wasAltHeld = true;
            altReleasedAt = DateTime.MinValue;
            return;
        }
        if (wasAltHeld && altReleasedAt == DateTime.MinValue)
        {
            altReleasedAt = DateTime.UtcNow;
            wasAltHeld = false;
        }
        if (!hudClearPending)
        {
            hudClearDeferTimer.Stop();
            return;
        }
        if (altReleasedAt == DateTime.MinValue) return;
        if ((DateTime.UtcNow - altReleasedAt).TotalSeconds >= 60)
        {
            hudClearPending = false;
            hudClearDeferTimer.Stop();
            agentTaskStatuses.Clear();
            RefreshTaskStatusHud();
        }
    }

    internal void TriggerCliNotice(string message, string agentName = null)
    {
        if (string.IsNullOrEmpty(message)) message = settings.Language == "" ? "" : "";
        if (activeNotice != null && !activeNotice.IsDisposed) { activeNotice.Close(); activeNotice = null; }
        // Only interrupt (force topmost + chime) when the agent is in the background;
        // if the user is already watching the agent window, show quietly without stealing focus.
        bool isFg = AppContext.IsAgentInForeground();
        if (settings.EndSound && !isFg) PlaySoundStyle(settings.EndSoundStyle);
        CliNoticeDialog dlg = new CliNoticeDialog(settings, message, agentName);
        activeNotice = dlg;
        dlg.FormClosed += delegate
        {
            if (activeNotice == dlg) activeNotice = null;
            if (isOneShot) Exit();
        };
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

    internal void ShowAskHumanDialog(string question, string[] options, int timeoutSec, string resFile, string agentName = null, Action<string> resultCallback = null)
    {
        if (activeAskHumanDialog != null && !activeAskHumanDialog.IsDisposed)
        {
            try { activeAskHumanDialog.Close(); } catch { }
            activeAskHumanDialog = null;
        }

        bool isFg = AppContext.IsAgentInForeground();
        if (string.IsNullOrEmpty(agentName))
        {
            List<string> agents = AppContext.DetectAgents();
            if (agents != null && agents.Count > 0) agentName = agents[0];
        }
        PlayAiSound();
        AskHumanDialog dlg = new AskHumanDialog(settings, question, options, timeoutSec, resFile, isFg, agentName, resultCallback);
        activeAskHumanDialog = dlg;
        dlg.FormClosed += delegate
        {
            if (activeAskHumanDialog == dlg) activeAskHumanDialog = null;
        };
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

    internal void ClearProjectedNote()
    {
        if (activeProjectedNote != null && !activeProjectedNote.IsDisposed)
        {
            try { activeProjectedNote.Close(); } catch { }
        }
    }

    internal void ShowCustomQuote(string text, string author, string agentName = null, int agentPid = 0)
    {
        // 1. Temporarily hide daily quote if active
        if (activeQuoteDialog != null && !activeQuoteDialog.IsDisposed)
        {
            dailyQuoteSuspendedForProjection = true;
            activeQuoteDialog.Hide();
        }
        else if (settings != null && settings.DailyQuote)
        {
            dailyQuoteSuspendedForProjection = true;
        }

        // 2. Mutual exclusion: at any time, only ONE desktop note projection is allowed
        if (activeProjectedNote != null && !activeProjectedNote.IsDisposed)
        {
            DailyQuoteDialog oldNote = activeProjectedNote;
            activeProjectedNote = null;
            try { oldNote.Close(); oldNote.Dispose(); } catch { }
        }
        StopProjectedAgentMonitor();

        // 3. Resolve agent info
        if (string.IsNullOrEmpty(agentName))
        {
            List<string> detected = DetectAgents();
            if (detected != null && detected.Count > 0) agentName = detected[0];
        }
        projectedAgentName = agentName;
        projectedAgentPid = agentPid;

        // 4. Create single projected note
        DailyQuoteDialog dlg = new DailyQuoteDialog(settings, text, author, isProjected: true);
        activeProjectedNote = dlg;

        dlg.FormClosed += delegate
        {
            if (activeProjectedNote == dlg)
            {
                activeProjectedNote = null;
                StopProjectedAgentMonitor();

                // Restore daily quote if it was suspended for projection
                if (dailyQuoteSuspendedForProjection)
                {
                    dailyQuoteSuspendedForProjection = false;
                    try
                    {
                        if (activeQuoteDialog != null && !activeQuoteDialog.IsDisposed)
                        {
                            activeQuoteDialog.Show();
                            activeQuoteDialog.EnsureVisible();
                            activeQuoteDialog.UpdateTopMostState();
                        }
                        else if (settings != null && settings.DailyQuote)
                        {
                            CheckDailyQuote(forceShow: false);
                        }
                    }
                    catch { }
                }
            }
        };

        dlg.Show();
        ForceTopMost(dlg);
        StartProjectedAgentMonitor();
    }

    private void StartProjectedAgentMonitor()
    {
        StopProjectedAgentMonitor();
        bool hasPid = projectedAgentPid > 0;
        bool hasAgent = !string.IsNullOrEmpty(projectedAgentName);
        if (!hasPid && !hasAgent) return;

        bool agentWasRunning = false;
        if (hasAgent)
        {
            try
            {
                HashSet<string> currentRunning = GetRunningAgents();
                agentWasRunning = currentRunning.Contains(projectedAgentName);
            }
            catch { }
        }

        projectedAgentMonitorTimer = new Timer();
        projectedAgentMonitorTimer.Interval = 1000;
        projectedAgentMonitorTimer.Tick += delegate
        {
            if (activeProjectedNote == null || activeProjectedNote.IsDisposed)
            {
                StopProjectedAgentMonitor();
                return;
            }

            bool agentExited = false;
            if (hasPid)
            {
                try
                {
                    using (Process p = Process.GetProcessById(projectedAgentPid))
                    {
                        if (p.HasExited) agentExited = true;
                    }
                }
                catch
                {
                    agentExited = true;
                }
            }
            else if (hasAgent && agentWasRunning)
            {
                try
                {
                    HashSet<string> running = GetRunningAgents();
                    if (!running.Contains(projectedAgentName))
                    {
                        agentExited = true;
                    }
                }
                catch { }
            }

            if (agentExited)
            {
                StopProjectedAgentMonitor();
                if (activeProjectedNote != null && !activeProjectedNote.IsDisposed)
                {
                    activeProjectedNote.Dismiss();
                }
            }
        };
        projectedAgentMonitorTimer.Start();
    }

    private void StopProjectedAgentMonitor()
    {
        if (projectedAgentMonitorTimer != null)
        {
            try { projectedAgentMonitorTimer.Stop(); projectedAgentMonitorTimer.Dispose(); } catch { }
            projectedAgentMonitorTimer = null;
        }
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
            if (sessionActive) LogSession(wasRest ? "" : "");
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
            if (state == ClockState.Running)
            {
                long liveMin = OpenWorkFocusSecs() / 60;
                if (liveMin != lastLiveFocusMinutes)
                {
                    lastLiveFocusMinutes = liveMin;
                    UpdateDailyProgress();
                }
            }
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
            string prefix = s.Language == "" ? "" : "";
            return prefix + (total / 60).ToString("") + "" + sSec.ToString("");
        }
        return s.Format == "" ? (total / 60).ToString("") + "" + sSec.ToString("") :
            s.Format == "" ? (s.Language == "" ? h.ToString("") + "" + m.ToString("") + "" + sSec.ToString("") + "" : h.ToString("") + "" + m.ToString("") + "" + sSec.ToString("") + "") : h.ToString("") + "" + m.ToString("") + "" + sSec.ToString("");
    }

    private void FormatAndDisplay(long total)
    {
        string value = GetFormattedTime(settings, total);
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
            if (settings.NoticeStyle == "" || (settings.FullscreenDnd && IsForegroundFullScreen()))
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
            Action onRest = wasRest ? null : new Action(delegate { StartRest(settings.RestMinutes > 0 ? settings.RestMinutes : 10); });
            try
            {
                activeNotice = new NoticeDialog(settings, DateTime.Now, wasRest ? (settings.Language == "" ? "" : "") : null, IntPtr.Zero, onRest);
                activeNotice.FormClosed += delegate
                {
                    activeNotice = null;
                    if (state == ClockState.Alarming) SetState(ClockState.Idle);
                    if (isOneShot) Exit();
                };
                activeNotice.Show();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("" + ex.Message);
            }
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
        bool en = settings.Language == "";
        ShowTrayNotification("", enabled ? (en ? "" : "") : (en ? "" : ""), 2000);
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
        if (IsSelfCheckRunning) return false;
        try
        {
            IntPtr fg = NativeMethods.GetForegroundWindow();
            if (fg == IntPtr.Zero) return false;
            if (fg == NativeMethods.GetDesktopWindow() || fg == NativeMethods.GetShellWindow()) return false;

            StringBuilder sb = new StringBuilder(256);
            if (NativeMethods.GetClassName(fg, sb, sb.Capacity) > 0)
            {
                string cls = sb.ToString();
                if (cls == "" || cls == "" || cls == "") return false;
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

    private static byte[] doorbellWavData;
    private static byte[] qqWavData;
    private static byte[] phoneWavData;
    private static byte[] knockWavData;
    private static byte[] aiChimeWavData;
    private static SoundPlayer activeSoundPlayer;

    private static byte[] GenerateKnockWav()
    {
        int sampleRate = 22050;
        List<short> samples = new List<short>();
        Random rnd = new Random(42);

        // 3 bursts of ""
        for (int burst = 0; burst < 3; burst++)
        {
            // Each burst has 3 knocks: ""
            for (int knock = 0; knock < 3; knock++)
            {
                int knockSamples = (int)(sampleRate * 0.090); // 90ms total per knock
                for (int i = 0; i < knockSamples; i++)
                {
                    double t = (double)i / sampleRate;

                    // Wood resonance: fundamental around 200 Hz with slight pitch drop from 240 to 195
                    double freq = 195.0 + 45.0 * Math.Exp(-t / 0.015);
                    double phase = 2.0 * Math.PI * freq * t;

                    // Fast exponential decay of solid wood resonance
                    double decay = Math.Exp(-t / 0.022);

                    // Attack envelope: fast ramp in 1.5ms
                    double attack = i < (sampleRate * 0.0015) ? (double)i / (sampleRate * 0.0015) : 1.0;

                    // Fundamental + subtle wood overtones (2.1x, 3.4x)
                    double s0 = Math.Sin(phase);
                    double s1 = 0.35 * Math.Sin(phase * 2.15) * Math.Exp(-t / 0.012);
                    double s2 = 0.15 * Math.Sin(phase * 3.42) * Math.Exp(-t / 0.006);

                    // Knuckle impact noise burst in first 4ms
                    double noise = (t < 0.004) ? (rnd.NextDouble() * 2.0 - 1.0) * Math.Exp(-t / 0.002) * 0.30 : 0.0;

                    double val = (s0 + s1 + s2 + noise) * decay * attack * 22000.0;
                    samples.Add((short)Math.Max(-32767, Math.Min(32767, val)));
                }

                // Gap between knocks within the same burst: ~110ms
                if (knock < 2)
                {
                    int knockGap = (int)(sampleRate * 0.110);
                    for (int i = 0; i < knockGap; i++) samples.Add(0);
                }
            }

            // Pause between the 3 bursts: ~380ms
            if (burst < 2)
            {
                int burstPause = (int)(sampleRate * 0.380);
                for (int i = 0; i < burstPause; i++) samples.Add(0);
            }
        }

        return BuildPcmWav(sampleRate, samples);
    }

    private static byte[] GenerateDoorbellWav()
    {
        int sampleRate = 22050;
        List<short> samples = new List<short>();
        double fDing = 783.99; // G5 (叮)
        double fDong = 659.25; // E5 (咚)

        for (int cycle = 0; cycle < 3; cycle++)
        {
            int pairSamples = (int)(sampleRate * 1.05);
            int dongOffset = (int)(sampleRate * 0.38);

            for (int i = 0; i < pairSamples; i++)
            {
                double t = (double)i / sampleRate;
                double val = 0;

                // Ding component (叮)
                if (t < 0.65)
                {
                    double attack = i < (sampleRate * 0.003) ? (double)i / (sampleRate * 0.003) : 1.0;
                    double decay = Math.Exp(-t / 0.22);
                    double over1 = Math.Exp(-t / 0.06);
                    double over2 = Math.Exp(-t / 0.025);

                    double s0 = Math.Sin(2.0 * Math.PI * fDing * t);
                    double s1 = 0.25 * Math.Sin(2.0 * Math.PI * fDing * 2.756 * t) * over1;
                    double s2 = 0.10 * Math.Sin(2.0 * Math.PI * fDing * 5.404 * t) * over2;

                    val += (s0 + s1 + s2) * decay * attack * 16000.0;
                }

                // Dong component (咚) - starts at dongOffset (0.38s)
                if (i >= dongOffset)
                {
                    int iDong = i - dongOffset;
                    double tDong = (double)iDong / sampleRate;
                    if (tDong < 0.70)
                    {
                        double attack = iDong < (sampleRate * 0.003) ? (double)iDong / (sampleRate * 0.003) : 1.0;
                        double decay = Math.Exp(-tDong / 0.32);
                        double over1 = Math.Exp(-tDong / 0.08);
                        double over2 = Math.Exp(-tDong / 0.03);

                        double s0 = Math.Sin(2.0 * Math.PI * fDong * tDong);
                        double s1 = 0.25 * Math.Sin(2.0 * Math.PI * fDong * 2.756 * tDong) * over1;
                        double s2 = 0.10 * Math.Sin(2.0 * Math.PI * fDong * 5.404 * tDong) * over2;

                        val += (s0 + s1 + s2) * decay * attack * 16000.0;
                    }
                }

                samples.Add((short)Math.Max(-32767, Math.Min(32767, val)));
            }

            if (cycle < 2)
            {
                int pauseSamples = (int)(sampleRate * 0.20);
                for (int i = 0; i < pauseSamples; i++) samples.Add(0);
            }
        }

        return BuildPcmWav(sampleRate, samples);
    }

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

            bw.Write(System.Text.Encoding.ASCII.GetBytes(""));
            bw.Write(chunkSize);
            bw.Write(System.Text.Encoding.ASCII.GetBytes(""));

            bw.Write(System.Text.Encoding.ASCII.GetBytes(""));
            bw.Write(16);
            bw.Write((short)1); // PCM
            bw.Write((short)1); // Mono
            bw.Write(sampleRate);
            bw.Write(byteRate);
            bw.Write((short)2); // BlockAlign
            bw.Write((short)16); // BitsPerSample

            bw.Write(System.Text.Encoding.ASCII.GetBytes(""));
            bw.Write(dataLen);
            for (int i = 0; i < numSamples; i++)
            {
                bw.Write(samples[i]);
            }
            bw.Flush();
            return ms.ToArray();
        }
    }

    private static byte[] GenerateAiChimeWav()
    {
        int sampleRate = 22050;
        List<short> samples = new List<short>();

        // Option 2: Dedicated "" (灵感双音轻升调)
        // Note 1: C6 (1046.5 Hz, ~110ms)
        int n1Samples = (int)(sampleRate * 0.110);
        for (int i = 0; i < n1Samples; i++)
        {
            double t = (double)i / sampleRate;
            double attack = i < (sampleRate * 0.0025) ? (double)i / (sampleRate * 0.0025) : 1.0;
            double decay = Math.Exp(-t / 0.045);
            double phase = 2.0 * Math.PI * 1046.5 * t;
            double s0 = Math.Sin(phase);
            double s1 = 0.22 * Math.Sin(phase * 2.0);
            double s2 = 0.08 * Math.Sin(phase * 3.0);
            double val = (s0 + s1 + s2) * decay * attack * 18000.0;
            samples.Add((short)Math.Max(-32767, Math.Min(32767, val)));
        }

        // Note 2: G6 (1567.98 Hz, ~170ms)
        int n2Samples = (int)(sampleRate * 0.170);
        for (int i = 0; i < n2Samples; i++)
        {
            double t = (double)i / sampleRate;
            double attack = i < (sampleRate * 0.0025) ? (double)i / (sampleRate * 0.0025) : 1.0;
            double decay = Math.Exp(-t / 0.065);
            double phase = 2.0 * Math.PI * 1567.98 * t;
            double s0 = Math.Sin(phase);
            double s1 = 0.20 * Math.Sin(phase * 2.0);
            double s2 = 0.06 * Math.Sin(phase * 3.0);
            double val = (s0 + s1 + s2) * decay * attack * 19000.0;
            samples.Add((short)Math.Max(-32767, Math.Min(32767, val)));
        }

        return BuildPcmWav(sampleRate, samples);
    }

    internal static void PlayAiSound()
    {
        try
        {
            StopSound();

            // Priority: Option 2 (Pure Math PCM Synthesized Inspiration Chime)
            if (aiChimeWavData == null) aiChimeWavData = GenerateAiChimeWav();
            if (aiChimeWavData != null && aiChimeWavData.Length > 0)
            {
                MemoryStream ms = new MemoryStream(aiChimeWavData);
                activeSoundPlayer = new SoundPlayer(ms);
                activeSoundPlayer.Play();
                return;
            }
        }
        catch { }

        // Fallback: Option 3 (Windows Media Speech On.wav / Windows Background.wav / System Sound)
        try
        {
            string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string speechWav = Path.Combine(winDir, "", "");
            if (File.Exists(speechWav))
            {
                activeSoundPlayer = new SoundPlayer(speechWav);
                activeSoundPlayer.Play();
                return;
            }
            string notifWav = Path.Combine(winDir, "", "");
            if (File.Exists(notifWav))
            {
                activeSoundPlayer = new SoundPlayer(notifWav);
                activeSoundPlayer.Play();
                return;
            }
        }
        catch { }

        try
        {
            SystemSounds.Asterisk.Play();
        }
        catch { }
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

            if (style == "" || style == "" || style == "" || style == "")
            {
                PlayAiSound();
                return;
            }

            if (style == "")
            {
                string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                string p = Path.Combine(winDir, "");
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
            else if (style == "" || style == "")
            {
                if (knockWavData == null) knockWavData = GenerateKnockWav();
                MemoryStream ms = new MemoryStream(knockWavData);
                activeSoundPlayer = new SoundPlayer(ms);
                activeSoundPlayer.Play();
            }
            else if (style == "" || style == "" || style == "" || style == "")
            {
                if (phoneWavData == null) phoneWavData = GeneratePhoneWav();
                MemoryStream ms = new MemoryStream(phoneWavData);
                activeSoundPlayer = new SoundPlayer(ms);
                activeSoundPlayer.Play();
            }
            else if (style == "" || style == "")
            {
                if (qqWavData == null) qqWavData = GenerateQQWav();
                MemoryStream ms = new MemoryStream(qqWavData);
                activeSoundPlayer = new SoundPlayer(ms);
                activeSoundPlayer.Play();
            }
            else // "", "", "", or default
            {
                if (doorbellWavData == null) doorbellWavData = GenerateDoorbellWav();
                MemoryStream ms = new MemoryStream(doorbellWavData);
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
        if (lower.Contains("")) return "";
        if (lower.Contains("")) return "";
        if (lower.Contains("") || lower.Contains("")) return "";
        if (lower.Contains("")) return "";
        if (lower.Contains("")) return "";
        if (lower.Contains("") || lower.Contains("")) return "";
        if (lower.Contains("") || lower.Contains("")) return "";
        if (lower.Contains("")) return "";
        if (lower.Contains("")) return "";
        if (lower.Contains("") || lower.Contains("") || lower.Contains("")) return "";
        if (lower.Contains("")) return "";
        if (lower.Contains("")) return "";
        if (lower.Contains("")) return "";
        if (lower.Contains("")) return "";
        if (lower.Contains("")) return "";
        if (lower.Contains("")) return "";
        if (lower.Contains("")) return "";
        if (lower.Contains("")) return "";
        if (lower.Contains("")) return "";
        if (lower.Contains("") || lower.Contains("") || lower.Contains("")) return "";
        if (lower.Contains("")) return "";
        if (lower.Contains("")) return "";
        if (lower.Contains("")) return "";
        if (lower.Contains("") || lower.Contains("") || lower.Contains("")) return "";
        if (lower.Contains("") || lower.Contains("")) return "";
        if (lower.Contains("")) return "";
        if (lower.Contains("") || lower.Contains("")) return "";
        if (lower.Contains("") || lower.Contains("")) return "";
        if (lower.Contains("")) return "";
        if (lower.Contains("") || lower.Contains("") || lower.Contains("")) return "";
        if (lower.Contains("") || lower.Contains("")) return "";
        if (lower == "" || lower.StartsWith("") || lower.StartsWith("") || lower.StartsWith("")) return "";
        return null;
    }

    internal static string GetProcessNameByPid(int pid)
    {
        IntPtr hProc = NativeMethods.OpenProcess(0x1000, false, pid);
        if (hProc == IntPtr.Zero) return null;
        try
        {
            StringBuilder sb = new StringBuilder(1024);
            int size = sb.Capacity;
            if (NativeMethods.QueryFullProcessImageName(hProc, 0, sb, ref size))
            {
                return Path.GetFileName(sb.ToString());
            }
        }
        catch { }
        finally
        {
            NativeMethods.CloseHandle(hProc);
        }
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
            string procName = GetProcessNameByPid((int)pid);
            if (procName != null && MatchAgent(procName) != null) return true;
            string title = GetAgentWindowTitle(fg);
            if (!string.IsNullOrEmpty(title) && MatchAgent(title) != null) return true;
        }
        catch { }
        return false;
    }

    internal static void EnumerateSystemProcesses(NativeMethods.ProcessEnumCallback callback)
    {
        IntPtr hSnap = NativeMethods.CreateToolhelp32Snapshot(0x00000002, 0); // TH32CS_SNAPPROCESS
        if (hSnap == IntPtr.Zero || hSnap == (IntPtr)(-1)) return;
        try
        {
            NativeMethods.PROCESSENTRY32 pe = new NativeMethods.PROCESSENTRY32();
            pe.dwSize = (uint)Marshal.SizeOf(typeof(NativeMethods.PROCESSENTRY32));
            if (NativeMethods.Process32First(hSnap, ref pe))
            {
                do
                {
                    callback((int)pe.th32ProcessID, pe.szExeFile);
                } while (NativeMethods.Process32Next(hSnap, ref pe));
            }
        }
        catch { }
        finally
        {
            NativeMethods.CloseHandle(hSnap);
        }
    }

    internal static TimeSpan GetProcessCpuTime(int pid)
    {
        IntPtr hProc = NativeMethods.OpenProcess(0x1000 /* PROCESS_QUERY_LIMITED_INFORMATION */, false, pid);
        if (hProc == IntPtr.Zero) return TimeSpan.Zero;
        try
        {
            long c, e, k, u;
            if (NativeMethods.GetProcessTimes(hProc, out c, out e, out k, out u))
            {
                return TimeSpan.FromTicks(k + u);
            }
        }
        catch { }
        finally
        {
            NativeMethods.CloseHandle(hProc);
        }
        return TimeSpan.Zero;
    }

    private static HashSet<string> cachedRunningAgents = null;
    private static DateTime lastRunningAgentsTime = DateTime.MinValue;

    internal static HashSet<string> GetRunningAgents()
    {
        if (cachedRunningAgents != null && (DateTime.Now - lastRunningAgentsTime).TotalSeconds < 2)
        {
            return new HashSet<string>(cachedRunningAgents, StringComparer.OrdinalIgnoreCase);
        }
        HashSet<string> running = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            EnumerateSystemProcesses(delegate(int pid, string exeName)
            {
                string name = MatchAgent(exeName);
                if (name != null) running.Add(name);
            });
            cachedRunningAgents = running;
            lastRunningAgentsTime = DateTime.Now;
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
            foreach (string name in GetRunningAgents())
            {
                if (!seen.Contains(name))
                {
                    seen.Add(name);
                    result.Add(name);
                }
            }
        }
        catch { }
        return result;
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

    private void RefreshTimerVisual()
    {
        long total = running
            ? (lastSeconds >= 0 ? lastSeconds : (long)Math.Ceiling(remaining.TotalSeconds))
            : (sessionActive ? (long)Math.Ceiling(remaining.TotalSeconds) : (settings.Minutes * 60));
        string value = GetFormattedTime(settings, total);
        overlay.SetVisualPreview(value);
    }

    private void SetQuotePreview(bool visible)
    {
        if (activeProjectedNote != null && !activeProjectedNote.IsDisposed)
        {
            dailyQuoteSuspendedForProjection = true;
            if (activeQuoteDialog != null && !activeQuoteDialog.IsDisposed)
            {
                activeQuoteDialog.ApplySettings();
                activeQuoteDialog.Hide();
            }
            return;
        }

        if (!visible)
        {
            if (activeQuoteDialog != null && !activeQuoteDialog.IsDisposed)
            {
                if (activeDialog is SettingsDialog)
                {
                    SettingsDialog sd = (SettingsDialog)activeDialog;
                    if (!sd.IsDailyQuoteChecked)
                    {
                        activeQuoteDialog.Close();
                        activeQuoteDialog = null;
                        return;
                    }
                }
                if (settings.DailyQuote || (activeDialog is SettingsDialog && ((SettingsDialog)activeDialog).IsDailyQuoteChecked))
                {
                    activeQuoteDialog.IsPreviewMode = false;
                    return;
                }
                activeQuoteDialog.Close();
                activeQuoteDialog = null;
            }
            return;
        }

        if (activeQuoteDialog == null || activeQuoteDialog.IsDisposed)
        {
            activeQuoteDialog = new DailyQuoteDialog(settings);
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
                try { if (activeDialog is SettingsDialog) ((SettingsDialog)activeDialog).SyncQuotePin(pinned); } catch { }
            };
            activeQuoteDialog.FormClosed += delegate
            {
                try
                {
                    if (activeQuoteDialog != null && activeQuoteDialog.WasDismissedByUser)
                    {
                        settings.DailyQuote = false;
                        settings.Save();
                    }
                    if (activeDialog is SettingsDialog) ((SettingsDialog)activeDialog).SetDailyQuoteChecked(settings.DailyQuote);
                }
                catch { }
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
                if (sd.DialogQuoteIndex != activeQuoteDialog.CurrentQuoteIndex)
                {
                    sd.SyncQuoteIndex(activeQuoteDialog.CurrentQuoteIndex);
                }
            }
            activeQuoteDialog.ApplySettings();
        }
    }

    private void ShowSettings()
    {
        ShowSettingsTab(-1);
    }

    internal void ShowSettingsTab(int initialTab)
    {
        if (trayClickTimer != null) trayClickTimer.Stop();
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
            d.SyncQuoteIndex(activeQuoteDialog.CurrentQuoteIndex);
            activeQuoteDialog.FontSizeChanged += quoteFontSync;
            activeQuoteDialog.QuoteChanged += quoteChangeSync;
        }

        RefreshTimerVisual();

        d.PreviewRequested += delegate
        {
            RefreshTimerVisual();
            UpdateDailyProgress();
        };
        d.SettingsCommitted += delegate
        {
            ApplySavedSettings();
        };

        d.QuotePreviewRequested += delegate(bool visible)
        {
            SetQuotePreview(visible);
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
            RefreshTimerVisual();
            SetQuotePreview(false);
            if (settings.DailyQuote)
            {
                if (activeProjectedNote != null && !activeProjectedNote.IsDisposed)
                {
                    dailyQuoteSuspendedForProjection = true;
                    if (activeQuoteDialog != null && !activeQuoteDialog.IsDisposed)
                    {
                        activeQuoteDialog.IsPreviewMode = false;
                        activeQuoteDialog.ApplySettings();
                        activeQuoteDialog.Hide();
                    }
                }
                else if (activeQuoteDialog != null && !activeQuoteDialog.IsDisposed)
                {
                    activeQuoteDialog.IsPreviewMode = false;
                    activeQuoteDialog.ApplySettings();
                    activeQuoteDialog.Show();
                    activeQuoteDialog.EnsureVisible();
                }
                else
                {
                    CheckDailyQuote(true);
                }
            }
            else
            {
                if (activeQuoteDialog != null && !activeQuoteDialog.IsDisposed)
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

    private void ApplySavedSettings()
    {
        bool hotkeyFailed = false;
        if ((settings.HotkeyKey == (int)Keys.Space && (settings.HotkeyModifiers & 3) != 0) || !SetHotkey(settings.HotkeyModifiers, settings.HotkeyKey))
        {
            settings.HotkeyModifiers = activeHotkeyModifiers; settings.HotkeyKey = activeHotkeyKey;
            hotkeyFailed = true;
        }
        settings.Save();
        ApplyStartup();
        if (overlay != null) overlay.RefreshSettings();
        if (tray != null && tray.ContextMenuStrip != null)
        {
            tray.ContextMenuStrip.Font = GetUiFont(settings.Language, 9.5f);
            if (menuStart != null) menuStart.Font = new Font(tray.ContextMenuStrip.Font, FontStyle.Bold);
        }
        UpdateMenuText();
        lastSeconds = -1;
        if (!running && !sessionActive) { ResetDisplay(); overlay.SetPaused(false); overlay.Hide(); }
        else if (running) { Tick(null, EventArgs.Empty); }
        else { overlay.SetPaused(true); Display(remaining); }

        if (hotkeyFailed)
        {
            bool en = settings.Language == "";
            MessageBox.Show(en ? "" : "", "", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            if (res == "" || res == "") s = m * 60L;
            else
            {
                DateTime t1, t2;
                if (DateTime.TryParse(start, out t1) && DateTime.TryParse(end, out t2))
                    s = Math.Max(0L, Math.Min(m * 60L, (long)(t2 - t1).TotalSeconds));
            }
        }
        isDone = res == "" || res == "";
        return true;
    }

    private static void GetDayStats(string dir, string dateKey, out int total, out int completed, out long actualSecs)
    {
        total = 0; completed = 0; actualSecs = 0;
        string path = Path.Combine(dir, "" + dateKey + "");
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
        for (int i = 0; i < 7; i++) dates.Add(DateTime.Today.AddDays(-i).ToString(""));
        try
        {
            if (Directory.Exists(dir))
            {
                foreach (string file in Directory.GetFiles(dir, ""))
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    if (name.StartsWith("") && name.Length == 16)
                    {
                        string k = name.Substring(8);
                        if (!dates.Contains(k)) dates.Add(k);
                    }
                }
                foreach (string file in Directory.GetFiles(dir, ""))
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    if (name.StartsWith("") && name.Length == 14)
                    {
                        string k = name.Substring(6);
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
            if (activeDialog.Tag as string == "")
            {
                if (openHistoryOnUsageTab && historySetUsageTab != null) historySetUsageTab(true);
                openHistoryOnUsageTab = false;
                ActivateWindow(activeDialog);
                return;
            }
            activeDialog.Close();
        }
        bool en = settings.Language == "";
        string historyDir = Path.GetDirectoryName(Settings.FilePath);
        Form f = new Form();
        f.Tag = "";
        activeDialog = f;
        f.Font = GetUiFont(settings.Language, 9.5f);
        f.BackColor = Win11Bg;
        f.Text = en ? "" : "";
        f.StartPosition = FormStartPosition.CenterScreen;
        f.ClientSize = new Size(760, 500);
        f.MinimumSize = new Size(720, 440);
        f.MinimizeBox = false; f.MaximizeBox = false;
        f.ShowInTaskbar = false;
        f.TopMost = true;
        ApplyModernWindowStyle(f);
        f.Shown += delegate { ActivateWindow(f); };

        Font headerFont = new Font(f.Font, FontStyle.Bold);
        Font boldFont = new Font(f.Font, FontStyle.Bold);
        ToolTip tip = new ToolTip();
        ToolTip axisTip = new ToolTip();
        f.FormClosed += delegate
        {
            headerFont.Dispose();
            boldFont.Dispose();
            tip.Dispose();
            axisTip.Dispose();
            if (activeDialog == f) activeDialog = null;
            historySetUsageTab = null;
        };
        f.KeyPreview = true;

        int currentViewMode = 0; // 0: Day View, 1: Overview View
        int currentDaySubMode = openHistoryOnUsageTab ? 1 : 0; // 0: countdown, 1: usage
        openHistoryOnUsageTab = false;
        string usageHighlight = "";
        StatSpan[] currentSpans = new StatSpan[0];

        Button btnTabDay = new Button();
        btnTabDay.Text = en ? "" : "";
        btnTabDay.SetBounds(12, 10, en ? 100 : 84, 28);
        btnTabDay.FlatStyle = FlatStyle.Flat;
        btnTabDay.FlatAppearance.BorderSize = 0;
        btnTabDay.Font = boldFont;
        btnTabDay.Cursor = Cursors.Hand;
        btnTabDay.MouseEnter += delegate { if (currentViewMode != 0) btnTabDay.BackColor = Color.FromArgb(228, 232, 238); };
        btnTabDay.MouseLeave += delegate { if (currentViewMode != 0) btnTabDay.BackColor = Color.FromArgb(240, 242, 245); };

        Button btnTabOverview = new Button();
        btnTabOverview.Text = en ? "" : "";
        btnTabOverview.SetBounds(btnTabDay.Right + 6, 10, en ? 105 : 84, 28);
        btnTabOverview.FlatStyle = FlatStyle.Flat;
        btnTabOverview.FlatAppearance.BorderSize = 0;
        btnTabOverview.Font = boldFont;
        btnTabOverview.Cursor = Cursors.Hand;
        btnTabOverview.MouseEnter += delegate { if (currentViewMode != 1) btnTabOverview.BackColor = Color.FromArgb(228, 232, 238); };
        btnTabOverview.MouseLeave += delegate { if (currentViewMode != 1) btnTabOverview.BackColor = Color.FromArgb(240, 242, 245); };

        ToggleSwitch usageSwitch = new ToggleSwitch();
        usageSwitch.Size = new Size(40, 22);
        usageSwitch.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        usageSwitch.Checked = settings.UsageTrackEnabled;
        usageSwitch.TabStop = true;

        Label lblUsage = new Label();
        lblUsage.AutoSize = true;
        lblUsage.UseMnemonic = false;
        lblUsage.TextAlign = ContentAlignment.MiddleRight;
        lblUsage.Text = en ? "" : "";
        lblUsage.Font = f.Font;
        lblUsage.ForeColor = Win11TextSecondary;
        lblUsage.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        usageSwitch.Location = new Point(f.ClientSize.Width - 12 - usageSwitch.Width, 13);
        // 按文字本身的宽度再留出一个字，避免最后一个字换行后被裁掉。
        int usageLabelW = lblUsage.PreferredSize.Width + lblUsage.Font.Height;
        lblUsage.AutoSize = false;
        lblUsage.SetBounds(usageSwitch.Left - 6 - usageLabelW, 12, usageLabelW, 24);

        Button btnNext = new Button();
        btnNext.Text = "";
        btnNext.Size = new Size(28, 28);
        btnNext.Location = new Point(lblUsage.Left - 8 - btnNext.Width, 10);
        btnNext.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnNext.FlatStyle = FlatStyle.Flat;
        btnNext.BackColor = Color.FromArgb(240, 242, 245);
        btnNext.ForeColor = Win11TextPrimary;
        btnNext.FlatAppearance.BorderColor = Color.FromArgb(215, 220, 228);
        btnNext.FlatAppearance.MouseOverBackColor = Color.FromArgb(228, 232, 238);
        btnNext.Cursor = Cursors.Hand;
        tip.SetToolTip(btnNext, en ? "" : "");

        ComboBox cboDate = new ComboBox();
        cboDate.DropDownStyle = ComboBoxStyle.DropDownList;
        cboDate.Width = en ? 180 : 165;
        cboDate.Font = f.Font;
        cboDate.Location = new Point(btnNext.Left - cboDate.Width - 4, 10);
        cboDate.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        tip.SetToolTip(cboDate, en ? "" : "");

        Button btnPrev = new Button();
        btnPrev.Text = "";
        btnPrev.Size = new Size(28, 28);
        btnPrev.Location = new Point(cboDate.Left - 32, 10);
        btnPrev.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnPrev.FlatStyle = FlatStyle.Flat;
        btnPrev.BackColor = Color.FromArgb(240, 242, 245);
        btnPrev.ForeColor = Win11TextPrimary;
        btnPrev.FlatAppearance.BorderColor = Color.FromArgb(215, 220, 228);
        btnPrev.FlatAppearance.MouseOverBackColor = Color.FromArgb(228, 232, 238);
        btnPrev.Cursor = Cursors.Hand;
        tip.SetToolTip(btnPrev, en ? "" : "");

        List<string> availDates = GetAvailableHistoryDates(historyDir);
        string todayStr = DateTime.Today.ToString("");
        string yesterdayStr = DateTime.Today.AddDays(-1).ToString("");
        string openStart, openEnd; int openMinutes; long openSecs; bool openRest;
        bool hasOpenToday = TryGetOpenHistoryRow(out openStart, out openEnd, out openMinutes, out openSecs, out openRest)
            && sessionStart.ToString("") == todayStr;
        if (hasOpenToday && !availDates.Contains(todayStr))
            availDates.Insert(0, todayStr);
        foreach (string dStr in availDates)
        {
            DateTime dt;
            DateTime.TryParseExact(dStr, "", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt);
            string dFmt = dt.ToString("");
            string label = (dStr == todayStr) ? dFmt + (en ? "" : "") :
                           (dStr == yesterdayStr) ? dFmt + (en ? "" : "") :
                           dFmt + "" + dt.ToString("", en ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo("")) + "";
            cboDate.Items.Add(label);
        }
        if (cboDate.Items.Count > 0) cboDate.SelectedIndex = 0;

        ComboBox cboRange = new ComboBox();
        cboRange.DropDownStyle = ComboBoxStyle.DropDownList;
        cboRange.Width = en ? 140 : 120;
        cboRange.Font = f.Font;
        cboRange.Location = new Point(lblUsage.Left - 8 - cboRange.Width, 10);
        cboRange.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        cboRange.Items.AddRange(en ? new object[] { "", "", "", "" }
                                   : new object[] { "", "", "", "" });
        cboRange.SelectedIndex = 0;
        tip.SetToolTip(cboRange, en ? "" : "");

        ListView list = new ListView();
        list.Font = f.Font;
        list.BackColor = Color.White;
        list.ForeColor = Win11TextPrimary;
        list.BorderStyle = BorderStyle.FixedSingle;
        list.View = View.Details; list.FullRowSelect = true; list.GridLines = true;
        UsageAxisPanel usageAxis = new UsageAxisPanel();
        int contentW = f.ClientSize.Width - 24;
        usageAxis.SetBounds(12, 44, contentW, 38);
        usageAxis.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        usageAxis.Visible = false;
        usageAxis.MouseMove += delegate(object s, MouseEventArgs e)
        {
            UsageSegment hit = usageAxis.HitTest(e.X);
            if (hit == null) { axisTip.SetToolTip(usageAxis, ""); return; }
            axisTip.SetToolTip(usageAxis, UsageLog.LabelFor(hit.State, hit.Exe, en) + ""
                + hit.Start.ToString("") + "" + hit.End.ToString("") + ""
                + FormatDuration(hit.Seconds, en));
        };

        Button btnSegCountdown = new Button();
        btnSegCountdown.FlatStyle = FlatStyle.Flat;
        btnSegCountdown.FlatAppearance.BorderSize = 0;
        btnSegCountdown.Font = f.Font;
        btnSegCountdown.Cursor = Cursors.Hand;
        btnSegCountdown.Text = en ? "" : "";
        btnSegCountdown.SetBounds(12, 86, en ? 96 : 84, 26);
        btnSegCountdown.Visible = false;

        Button btnSegUsage = new Button();
        btnSegUsage.FlatStyle = FlatStyle.Flat;
        btnSegUsage.FlatAppearance.BorderSize = 0;
        btnSegUsage.Font = f.Font;
        btnSegUsage.Cursor = Cursors.Hand;
        btnSegUsage.Text = en ? "" : "";
        btnSegUsage.SetBounds(btnSegCountdown.Right + 6, 86, en ? 110 : 100, 26);
        btnSegUsage.Visible = false;

        list.SetBounds(12, 48, contentW, 394);
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
            if (currentViewMode == 0 && currentDaySubMode == 1)
            {
                int c0 = 28, c1 = 160, c2 = 110, c3 = 70, c4 = 120;
                list.Columns[0].Width = c0; list.Columns[1].Width = c1; list.Columns[2].Width = c2;
                list.Columns[3].Width = c3; list.Columns[4].Width = c4;
                list.Columns[5].Width = Math.Max(40, w - c0 - c1 - c2 - c3 - c4);
            }
            else if (currentViewMode == 0)
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
            bool centerCol0 = (currentViewMode == 0 && currentDaySubMode == 0 && e.ColumnIndex == 0);
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
            if (currentViewMode == 0 && currentDaySubMode == 1)
            {
                e.DrawBackground();
                Color fg = isSel ? SystemColors.HighlightText : Win11TextPrimary;
                if (e.ColumnIndex == 0)
                {
                    Color dot = e.Item.Tag is Color ? (Color)e.Item.Tag : Win11ShortcutGray;
                    int d = 8;
                    int dx = e.Bounds.X + (e.Bounds.Width - d) / 2;
                    int dy = e.Bounds.Y + (e.Bounds.Height - d) / 2;
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using (SolidBrush br = new SolidBrush(dot))
                        e.Graphics.FillEllipse(br, dx, dy, d, d);
                }
                else if (e.ColumnIndex == 4)
                {
                    string pctTxt = e.Item.SubItems.Count > 3 ? e.Item.SubItems[3].Text : "";
                    int pct = 0;
                    if (pctTxt.EndsWith("")) int.TryParse(pctTxt.Substring(0, pctTxt.Length - 1), out pct);
                    Rectangle bar = new Rectangle(e.Bounds.X + 6, e.Bounds.Y + e.Bounds.Height / 2 - 4, Math.Max(0, e.Bounds.Width - 14), 8);
                    using (SolidBrush bg = new SolidBrush(Color.FromArgb(230, 232, 236)))
                        e.Graphics.FillRectangle(bg, bar);
                    int fw = (int)Math.Round(bar.Width * Math.Min(100, Math.Max(0, pct)) / 100.0);
                    if (fw > 0)
                    {
                        Color barc = e.Item.Tag is Color ? (Color)e.Item.Tag : Win11Accent;
                        using (SolidBrush br = new SolidBrush(barc))
                            e.Graphics.FillRectangle(br, bar.X, bar.Y, fw, bar.Height);
                    }
                }
                else
                {
                    Rectangle r = new Rectangle(e.Bounds.X + 8, e.Bounds.Y, Math.Max(0, e.Bounds.Width - 16), e.Bounds.Height);
                    TextRenderer.DrawText(e.Graphics, e.SubItem.Text, e.Item.Font, r, fg, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.WordEllipsis);
                }
            }
            else if (currentViewMode == 0 && e.ColumnIndex == 0)
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
                if (txt.Contains("") || txt.Contains("") || txt == "" || txt == "")
                    fg = Color.FromArgb(16, 124, 65);
                else if (txt.Contains("") || txt.Contains(""))
                    fg = Win11Accent;
                else if (txt == "" || txt == "")
                    fg = Win11Accent;
                else if (txt.Contains("") || txt.Contains("") || txt == "" || txt == "")
                    fg = Color.FromArgb(202, 80, 16);
                else if (txt.Contains("") || txt.Contains("") || txt == "" || txt == "" || txt == "")
                    fg = Win11ShortcutGray;
                Rectangle r = new Rectangle(e.Bounds.X + 8, e.Bounds.Y, Math.Max(0, e.Bounds.Width - 16), e.Bounds.Height);
                TextRenderer.DrawText(e.Graphics, txt, e.Item.Font, r, fg, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
            else e.DrawDefault = true;
        };

        Panel summaryPanel = new Panel();
        summaryPanel.SetBounds(12, 450, contentW, 36);
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
                string selText = cboDate.SelectedItem != null ? cboDate.SelectedItem.ToString() : "";
                string selKey = selText.Length >= 10 ? selText.Substring(0, 10).Replace("", "") : todayStr;
                string selDisplay = selText.Length >= 10 ? selText.Substring(0, 10) : DateTime.Today.ToString("");

                btnPrev.Enabled = (cboDate.SelectedIndex < cboDate.Items.Count - 1);
                btnNext.Enabled = (cboDate.SelectedIndex > 0);
                btnPrev.ForeColor = btnPrev.Enabled ? Win11TextPrimary : Win11ShortcutGray;
                btnNext.ForeColor = btnNext.Enabled ? Win11TextPrimary : Win11ShortcutGray;

                List<UsageSegment> daySegs = (usageTracker != null && selKey == todayStr)
                    ? usageTracker.GetDayWithOpen(selKey) : UsageLog.LoadDay(selKey);
                DateTime axisStart = selKey == todayStr ? UsageLog.EstimateBootOrMidnight() : DateTime.Today;
                DateTime axisEnd = DateTime.Now;
                DateTime selDay;
                if (DateTime.TryParseExact(selKey, "", CultureInfo.InvariantCulture, DateTimeStyles.None, out selDay))
                {
                    if (selKey != todayStr)
                    {
                        axisStart = selDay.Date;
                        axisEnd = selDay.Date.AddDays(1);
                    }
                    else if (axisStart.Date != DateTime.Today)
                        axisStart = DateTime.Today;
                }
                if (daySegs.Count > 0)
                {
                    if (daySegs[0].Start < axisStart) axisStart = daySegs[0].Start;
                    DateTime last = daySegs[daySegs.Count - 1].End;
                    if (last > axisEnd) axisEnd = last;
                }
                usageAxis.SetData(daySegs, axisStart, axisEnd, usageHighlight, en);

                if (currentDaySubMode == 1 && settings.UsageTrackEnabled)
                {
                    list.Columns[0].TextAlign = HorizontalAlignment.Center;
                    list.Columns[0].Text = "";
                    list.Columns[1].Text = en ? "" : "";
                    list.Columns[2].Text = en ? "" : "";
                    list.Columns[3].Text = en ? "" : "";
                    list.Columns[4].Text = "";
                    list.Columns[5].Text = "";

                    List<UsageSummary> rows = UsageLog.Summarize(daySegs);
                    long total = 0;
                    long activeSecs = 0, idleSecs = 0;
                    for (int i = 0; i < rows.Count; i++)
                    {
                        total += rows[i].Seconds;
                        if (rows[i].State == UsageLog.Active) activeSecs += rows[i].Seconds;
                        else if (rows[i].State == UsageLog.Idle) idleSecs += rows[i].Seconds;
                    }
                    if (rows.Count == 0)
                    {
                        list.Items.Add(new ListViewItem(new string[] { "", en ? "" : "", "", "", "", "" }));
                        currentSpans = new StatSpan[]
                        {
                            new StatSpan(en ? "" : "", f.Font, Win11TextSecondary)
                        };
                    }
                    else
                    {
                        for (int i = 0; i < rows.Count; i++)
                        {
                            int pct = total > 0 ? (int)Math.Round(rows[i].Seconds * 100.0 / total) : 0;
                            ListViewItem lvi = new ListViewItem(new string[] {
                                "",
                                UsageLog.LabelFor(rows[i].State, rows[i].Exe, en),
                                FormatDuration(rows[i].Seconds, en),
                                pct + "",
                                "",
                                ""
                            });
                            lvi.Tag = UsageLog.ColorFor(rows[i].State, rows[i].Exe);
                            lvi.Name = rows[i].State == UsageLog.Active ? ("" + (rows[i].Exe ?? "")) : rows[i].State;
                            list.Items.Add(lvi);
                        }
                        currentSpans = new StatSpan[]
                        {
                            new StatSpan(en ? rows.Count + "" : rows.Count + "", f.Font, Win11TextPrimary),
                            new StatSpan(FormatDuration(activeSecs, en), boldFont, Win11Accent),
                            new StatSpan(en ? "" : "", f.Font, Win11TextSecondary),
                            new StatSpan(FormatDuration(idleSecs, en), boldFont, Win11TextPrimary)
                        };
                    }
                }
                else
                {
                list.Columns[0].TextAlign = HorizontalAlignment.Center;
                list.Columns[0].Text = "";
                list.Columns[1].Text = en ? "" : "";
                list.Columns[2].Text = en ? "" : "";
                list.Columns[3].Text = en ? "" : "";
                list.Columns[4].Text = en ? "" : "";
                list.Columns[5].Text = en ? "" : "";

                string path = Path.Combine(historyDir, "" + selKey + "");
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
                                res = en ? (isDone ? "" : (res == "" || res == "" ? "" : (res == "" || res == "" ? "" : res)))
                                         : (isDone ? "" : (res == "" ? "" : (res == "" ? "" : res)));
                                list.Items.Add(new ListViewItem(new string[] { totalSessions.ToString(), start, end, dur + (en ? "" : ""), FormatDuration(s, en), res }));
                            }
                        }
                    }
                    catch { }
                }

                if (selKey == todayStr && TryGetOpenHistoryRow(out openStart, out openEnd, out openMinutes, out openSecs, out openRest))
                {
                    totalSessions++;
                    totalActualSecs += openSecs;
                    list.Items.Add(new ListViewItem(new string[] {
                        totalSessions.ToString(),
                        openStart,
                        openEnd,
                        openMinutes + (en ? "" : ""),
                        FormatDuration(openSecs, en),
                        en ? "" : ""
                    }));
                }

                if (totalSessions == 0)
                {
                    list.Items.Add(new ListViewItem(new string[] { "", en ? "" : "", "", "", "", "" }));
                    currentSpans = new StatSpan[]
                    {
                        new StatSpan(en ? "" + selDisplay + "" : "" + selDisplay + "", f.Font, Win11TextPrimary),
                        new StatSpan(en ? "" : "", boldFont, Win11ShortcutGray),
                        new StatSpan(en ? "" : "", f.Font, Win11TextSecondary)
                    };
                }
                else
                {
                    int rate = (int)Math.Round((double)completedSessions * 100.0 / totalSessions);
                    Color rateColor = (rate >= 80) ? Color.FromArgb(16, 124, 65) : Win11Accent;
                    currentSpans = new StatSpan[]
                    {
                        new StatSpan(en ? "" + selDisplay + "" : "" + selDisplay + "", f.Font, Win11TextPrimary),
                        new StatSpan(FormatDuration(totalActualSecs, en), boldFont, Win11Accent),
                        new StatSpan(en ? "" : "", f.Font, Win11TextSecondary),
                        new StatSpan(completedSessions + "" + totalSessions + (en ? "" : ""), boldFont, Win11TextPrimary),
                        new StatSpan(en ? "" : "", f.Font, Win11TextSecondary),
                        new StatSpan(rate + "", boldFont, rateColor)
                    };
                }
                }
            }
            else
            {
                list.Columns[0].TextAlign = HorizontalAlignment.Left;
                list.Columns[0].Text = en ? "" : "";
                list.Columns[1].Text = en ? "" : "";
                list.Columns[2].Text = en ? "" : "";
                list.Columns[3].Text = en ? "" : "";
                list.Columns[4].Text = en ? "" : "";
                list.Columns[5].Text = en ? "" : "";

                int rangeDays = cboRange.SelectedIndex == 0 ? 7 : (cboRange.SelectedIndex == 1 ? 14 : (cboRange.SelectedIndex == 2 ? 30 : 0));
                List<string> dateKeys = new List<string>();
                if (rangeDays > 0)
                {
                    for (int i = 0; i < rangeDays; i++) dateKeys.Add(DateTime.Today.AddDays(-i).ToString(""));
                }
                else dateKeys = GetAvailableHistoryDates(historyDir);

                int activeDays = 0, totalCycles = 0, totalDone = 0;
                long allActualSecs = 0;

                foreach (string dk in dateKeys)
                {
                    DateTime dt;
                    DateTime.TryParseExact(dk, "", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt);
                    int tot, done; long secs;
                    GetDayStats(historyDir, dk, out tot, out done, out secs);
                    if (dk == todayStr && TryGetOpenHistoryRow(out openStart, out openEnd, out openMinutes, out openSecs, out openRest))
                    {
                        tot++;
                        secs += openSecs;
                    }
                    string dStr = dt.ToString("");
                    string weekday = dt.ToString("", en ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(""));

                    if (tot > 0)
                    {
                        activeDays++; totalCycles += tot; totalDone += done; allActualSecs += secs;
                        int r = (int)Math.Round((double)done * 100.0 / tot);
                        string eval = r >= 90 ? (en ? "" : "") : (r >= 70 ? (en ? "" : "") : (r >= 50 ? (en ? "" : "") : (en ? "" : "")));
                        ListViewItem lvi = new ListViewItem(new string[] { dStr, weekday, FormatDurationMinutes(secs, en), done + "" + tot, r + "", eval });
                        lvi.Tag = dStr;
                        list.Items.Add(lvi);
                    }
                    else
                    {
                        ListViewItem lvi = new ListViewItem(new string[] { dStr, weekday, en ? "" : "", "", "", "" });
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
                        new StatSpan(en ? "" + totalDaysCount + "" : "" + totalDaysCount + "", f.Font, Win11TextSecondary)
                    };
                }
                else
                {
                    currentSpans = new StatSpan[]
                    {
                        new StatSpan(en ? "" + totalDaysCount + "" : "" + totalDaysCount + "", f.Font, Win11TextPrimary),
                        new StatSpan(FormatDurationMinutes(allActualSecs, en), boldFont, Win11Accent),
                        new StatSpan(en ? "" : "", f.Font, Win11TextSecondary),
                        new StatSpan(activeDays + "" + totalDaysCount + (en ? "" : ""), boldFont, Win11TextPrimary),
                        new StatSpan(en ? "" : "", f.Font, Win11TextSecondary),
                        new StatSpan(totalDone + "" + totalCycles + "" + overallRate + "", boldFont, rateColor),
                        new StatSpan(en ? "" : "", f.Font, Win11ShortcutGray)
                    };
                }
            }

            list.EndUpdate();
            updateColumnWidths();
            summaryPanel.Invalidate();
        };

        MethodInvoker applyUsageLayout = null;
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

            applyUsageLayout();
            refreshData();
        };

        applyUsageLayout = delegate
        {
            bool show = settings.UsageTrackEnabled && currentViewMode == 0;
            usageAxis.Visible = show;
            btnSegCountdown.Visible = show;
            btnSegUsage.Visible = show;
            btnSegCountdown.BackColor = currentDaySubMode == 0 ? Win11Accent : Color.FromArgb(235, 238, 242);
            btnSegCountdown.ForeColor = currentDaySubMode == 0 ? Color.White : Win11TextPrimary;
            btnSegUsage.BackColor = currentDaySubMode == 1 ? Win11Accent : Color.FromArgb(235, 238, 242);
            btnSegUsage.ForeColor = currentDaySubMode == 1 ? Color.White : Win11TextPrimary;
            int top = show ? 118 : 48;
            list.Top = top;
            list.Height = Math.Max(80, summaryPanel.Top - top - 8);
        };

        btnTabDay.Click += delegate { switchView(0); };
        btnTabOverview.Click += delegate { switchView(1); };
        btnSegCountdown.Click += delegate
        {
            currentDaySubMode = 0;
            usageHighlight = "";
            applyUsageLayout();
            refreshData();
        };
        btnSegUsage.Click += delegate
        {
            currentDaySubMode = 1;
            applyUsageLayout();
            refreshData();
        };
        usageSwitch.CheckedChanged += delegate
        {
            SetUsageTracking(usageSwitch.Checked);
            if (!usageSwitch.Checked) { currentDaySubMode = 0; usageHighlight = ""; }
            applyUsageLayout();
            refreshData();
        };
        historySetUsageTab = delegate(bool usage)
        {
            if (!settings.UsageTrackEnabled) return;
            currentDaySubMode = usage ? 1 : 0;
            if (currentViewMode != 0) switchView(0);
            else { applyUsageLayout(); refreshData(); }
        };
        list.SelectedIndexChanged += delegate
        {
            string next = "";
            if (currentViewMode == 0 && currentDaySubMode == 1 && list.SelectedItems.Count > 0)
                next = list.SelectedItems[0].Name ?? "";
            if (next == usageHighlight) return;
            usageHighlight = next;
            usageAxis.SetHighlight(usageHighlight);
        };

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
        f.Controls.Add(lblUsage);
        f.Controls.Add(usageSwitch);
        f.Controls.Add(btnPrev);
        f.Controls.Add(cboDate);
        f.Controls.Add(btnNext);
        f.Controls.Add(cboRange);
        f.Controls.Add(usageAxis);
        f.Controls.Add(btnSegCountdown);
        f.Controls.Add(btnSegUsage);
        f.Controls.Add(list);
        f.Controls.Add(summaryPanel);

        Action onUsageChanged = null;
        onUsageChanged = delegate
        {
            if (f.IsDisposed) return;
            if (f.InvokeRequired) { try { f.BeginInvoke(onUsageChanged); } catch { } return; }
            if (currentViewMode == 0 && settings.UsageTrackEnabled) refreshData();
        };
        if (usageTracker != null) usageTracker.Changed += onUsageChanged;
        f.FormClosed += delegate { if (usageTracker != null) usageTracker.Changed -= onUsageChanged; };
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
                        sb.AppendLine(string.Join("", parts.ToArray()));
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

    internal static void EnsureQuotesFileExists()
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
                sb.AppendLine("");
                sb.AppendLine("");
                sb.AppendLine("");
                sb.AppendLine("");
                sb.AppendLine("");
                sb.AppendLine();
                int qIdx = 1;
                foreach (QuoteItem q in DailyQuoteDialog.DefaultQuotes)
                {
                    string prefix = qIdx + "";
                    if (!string.IsNullOrEmpty(q.Author))
                        sb.AppendLine(prefix + q.Text + "" + q.Author);
                    else
                        sb.AppendLine(prefix + q.Text);
                    qIdx++;
                }
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            }
        }
        catch { }
    }

    internal static void OpenQuotesFile()
    {
        try
        {
            EnsureQuotesFileExists();
            Process.Start("", Settings.QuotesFilePath);
        }
        catch (Exception ex)
        {
            MessageBox.Show("" + ex.Message, "", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    internal void CheckDailyQuote(bool forceShow = false)
    {
        try
        {
            if (activeProjectedNote != null && !activeProjectedNote.IsDisposed)
            {
                dailyQuoteSuspendedForProjection = true;
                if (activeQuoteDialog != null && !activeQuoteDialog.IsDisposed)
                {
                    activeQuoteDialog.Hide();
                }
                return;
            }

            if (activeQuoteDialog != null && !activeQuoteDialog.IsDisposed)
            {
                if (!forceShow)
                {
                    // A hidden instance can survive projection/settings transitions.
                    // Do not treat its existence as proof that the quote is visible.
                    if (!activeQuoteDialog.Visible)
                    {
                        activeQuoteDialog.Show();
                        activeQuoteDialog.EnsureVisible();
                        activeQuoteDialog.UpdateTopMostState();
                    }
                    return;
                }
                if (forceShow)
                {
                    activeQuoteDialog.ApplySettings();
                    activeQuoteDialog.IsPreviewMode = false;
                    activeQuoteDialog.Show();
                    activeQuoteDialog.EnsureVisible();
                    activeQuoteDialog.UpdateTopMostState();
                    return;
                }
            }

            if (!forceShow)
            {
                if (!settings.DailyQuote) return;
            }

            activeQuoteDialog = new DailyQuoteDialog(settings);
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
                try { if (activeDialog is SettingsDialog) ((SettingsDialog)activeDialog).SyncQuotePin(pinned); } catch { }
            };
            activeQuoteDialog.FormClosed += delegate
            {
                try
                {
                    if (activeQuoteDialog != null && activeQuoteDialog.WasDismissedByUser)
                    {
                        settings.DailyQuote = false;
                        settings.Save();
                    }
                    if (activeDialog is SettingsDialog) ((SettingsDialog)activeDialog).SetDailyQuoteChecked(settings.DailyQuote);
                }
                catch { }
                activeQuoteDialog = null;
                if (isOneShot) Exit();
            };
            if (overlay.MoveMode) activeQuoteDialog.SetMoveMode(true);
            activeQuoteDialog.Show();
            activeQuoteDialog.EnsureVisible();
            activeQuoteDialog.UpdateTopMostState();
        }
        catch { }
    }

    private void LogSession(string result)
    {
        try
        {
            long actualSecs = (result == "")
                ? sessionMinutes * 60L
                : Math.Max(0L, Math.Min(sessionMinutes * 60L, (long)(TimeSpan.FromMinutes(sessionMinutes) - remaining).TotalSeconds));
            string dir = Path.GetDirectoryName(Settings.FilePath); Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "" + sessionStart.ToString("") + "");
            string row = sessionStart.ToString("") + "" + DateTime.Now.ToString("") + "" + sessionMinutes + "" + actualSecs + "" + result + Environment.NewLine;
            File.AppendAllText(path, row, new UTF8Encoding(false));
        }
        catch { }
        InvalidateTodayFocusCache();
        UpdateDailyProgress();
    }
    private void ApplyStartup()
    {
        bool enable = settings != null && settings.AutoStart;
        string exe = Application.ExecutablePath;
        bool registered = false;
        try { registered = TrySetRunKey(enable, exe); }
        catch { registered = false; }
        // 注册表写成功时只保留 Run 一项。写失败才放到「启动」文件夹，避免登录时拉起两次。
        try { SetStartupShortcut(enable && !registered, exe); }
        catch { }
    }

    // 写入 64 位视图，供 64 位 Explorer 在登录时读取。StartupApproved 的首字节 0x02 表示启用；
    // 若用户曾在「启动应用」里关掉 iClock，只重写 Run 而留下 0x03，Windows 仍不会启动。
    private static bool TrySetRunKey(bool enable, string exePath)
    {
        const string runSub = @"";
        const string approvedSub = @"";
        const string name = "";
        string command = "" + exePath + "";
        using (RegistryKey hive = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64))
        using (RegistryKey run = hive.CreateSubKey(runSub))
        {
            if (run == null) return false;
            if (enable) run.SetValue(name, command, RegistryValueKind.String);
            else run.DeleteValue(name, false);
            if (enable)
            {
                object written = run.GetValue(name);
                if (!(written is string) || ((string)written).IndexOf(exePath, StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            }
        }
        try
        {
            using (RegistryKey hive = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64))
            using (RegistryKey approved = hive.CreateSubKey(approvedSub))
            {
                if (approved != null)
                {
                    if (enable)
                        approved.SetValue(name, new byte[] { 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, RegistryValueKind.Binary);
                    else
                        approved.DeleteValue(name, false);
                }
            }
        }
        catch { }
        return true;
    }

    private static void SetStartupShortcut(bool enable, string exePath)
    {
        string startupDir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        if (string.IsNullOrEmpty(startupDir)) return;
        string link = Path.Combine(startupDir, "");
        if (!enable)
        {
            if (File.Exists(link)) File.Delete(link);
            return;
        }
        EnsureStartMenuShortcut(true, link, exePath);
    }
    private void Exit()
    {
        if (activeQuoteDialog != null && !activeQuoteDialog.IsDisposed && settings != null)
        {
            // Persist the actual on-screen state before closing any preview dialog,
            // whose close handler may otherwise save an older settings draft.
            activeQuoteDialog.CapturePersistentState();
            settings.Save();
        }
        if (activeNotice != null && !activeNotice.IsDisposed) { activeNotice.Close(); activeNotice = null; }
        if (activeDialog != null && !activeDialog.IsDisposed) { activeDialog.Close(); activeDialog = null; }
        if (activeQuoteDialog != null && !activeQuoteDialog.IsDisposed) { activeQuoteDialog.Close(); activeQuoteDialog = null; }
        if (activeProjectedNote != null && !activeProjectedNote.IsDisposed) { activeProjectedNote.Close(); activeProjectedNote = null; }
        if (activeAskHumanDialog != null && !activeAskHumanDialog.IsDisposed) { activeAskHumanDialog.Close(); activeAskHumanDialog = null; }
        StopProjectedAgentMonitor();
        if (running) remaining = ReadRemaining();
        if (sessionActive) { LogSession(""); sessionActive = false; }
        timer.Stop(); tray.Visible = false; if (hotkeyRegistered) NativeMethods.UnregisterHotKey(messageWindow.Handle, HOTKEY_ID);
        if (mouseHookHandle != IntPtr.Zero) { try { NativeMethods.UnhookWindowsHookEx(mouseHookHandle); } catch { } mouseHookHandle = IntPtr.Zero; }
        if (displaySettingsHandler != null) { try { SystemEvents.DisplaySettingsChanged -= displaySettingsHandler; } catch { } displaySettingsHandler = null; }
        if (sessionSwitchHandler != null) { try { SystemEvents.SessionSwitch -= sessionSwitchHandler; } catch { } sessionSwitchHandler = null; }
        if (usageTracker != null) { try { usageTracker.Stop(); } catch { } usageTracker = null; }
        if (trayClickTimer != null) { try { trayClickTimer.Stop(); trayClickTimer.Dispose(); } catch { } trayClickTimer = null; }
        if (taskStatusHud != null && !taskStatusHud.IsDisposed) { taskStatusHud.Close(); taskStatusHud.Dispose(); taskStatusHud = null; }
        overlay.Close(); overlay.Dispose(); messageWindow.DestroyHandle(); tray.Dispose(); appIcon.Dispose(); timer.Dispose(); ExitThread();
    }
    internal static string FormatDuration(long secs, bool en)
    {
        if (secs < 60) return secs + (en ? "" : "");
        long m = secs / 60;
        long s = secs % 60;
        if (m < 60)
        {
            if (s == 0) return m + (en ? "" : "");
            return en ? m + "" + s + "" : m + "" + s + "";
        }
        long h = m / 60;
        m = m % 60;
        if (s == 0 && m == 0) return h + (en ? "" : "");
        if (s == 0) return en ? h + "" + m + "" : h + "" + m + "";
        return en ? h + "" + m + "" + s + "" : h + "" + m + "" + s + "";
    }
    internal static string FormatCompactDuration(long secs)
    {
        if (secs <= 0) return "";
        if (secs < 60) return "";
        long m = (secs + 30) / 60;
        if (m < 60) return m + "";
        long h = m / 60;
        m = m % 60;
        if (m == 0) return h + "";
        return h + "" + m + "";
    }

    internal static string FormatDurationMinutes(long secs, bool en)
    {
        if (secs <= 0) return en ? "" : "";
        if (secs < 60) return en ? "" : "";
        long m = (secs + 30) / 60;
        if (m < 60) return m + (en ? "" : "");
        long h = m / 60;
        m = m % 60;
        if (m == 0) return h + (en ? "" : "");
        return en ? h + "" + m + "" : h + "" + m + "";
    }
    public void HandleHotkey() { Toggle(); }
    internal static string HotkeyText(int mods, int key)
    {
        string value = "";
        if ((mods & 2) != 0) value += "";
        if ((mods & 1) != 0) value += "";
        if ((mods & 4) != 0) value += "";
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
        string family = (lang == "") ? "" : "";
        try
        {
            return new Font(family, sizePt, style, GraphicsUnit.Point);
        }
        catch
        {
            try { return new Font("", sizePt, style, GraphicsUnit.Point); }
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
        Screen scr = IsSelfCheckRunning ? Screen.PrimaryScreen : (Screen.FromPoint(Cursor.Position) ?? Screen.PrimaryScreen);
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
                string downloads = Path.Combine(userProfile, "");
                if (full.StartsWith(downloads + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(full, downloads, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            string lower = full.ToLowerInvariant();
            if (lower.Contains(@""\downloads\"))
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
                linkPath = Path.Combine(programsDir, "");
            }
            bool exists = File.Exists(linkPath);
            if (!exists && !createIfNotExists) return false;

            if (string.IsNullOrEmpty(targetExe)) targetExe = Application.ExecutablePath;

            string dir = Path.GetDirectoryName(linkPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            Type shellType = Type.GetTypeFromProgID("");
            if (shellType == null) return false;
            object shell = Activator.CreateInstance(shellType);
            object sc = shellType.InvokeMember("", BindingFlags.InvokeMethod, null, shell, new object[] { linkPath });
            if (sc == null) return false;
            Type scType = sc.GetType();

            if (exists)
            {
                string currentTarget = scType.InvokeMember("", BindingFlags.GetProperty, null, sc, null) as string;
                string currentIcon = scType.InvokeMember("", BindingFlags.GetProperty, null, sc, null) as string;
                string expIcon = targetExe + "";
                if (string.Equals(currentTarget, targetExe, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(currentIcon, expIcon, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            scType.InvokeMember("", BindingFlags.SetProperty, null, sc, new object[] { targetExe });
            scType.InvokeMember("", BindingFlags.SetProperty, null, sc, new object[] { Path.GetDirectoryName(targetExe) });
            scType.InvokeMember("", BindingFlags.SetProperty, null, sc, new object[] { "" });
            scType.InvokeMember("", BindingFlags.SetProperty, null, sc, new object[] { targetExe + "" });
            scType.InvokeMember("", BindingFlags.InvokeMethod, null, sc, null);
            return true;
        }
        catch { return false; }
    }

    internal static Icon CreateIcon()
    {
        Size targetSize = SystemInformation.SmallIconSize;
        try
        {
            using (Stream s = typeof(AppContext).Assembly.GetManifestResourceStream(""))
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
                Path.Combine(baseDir, ""),
                Path.Combine(baseDir, ""),
                Path.Combine(baseDir, ""),
                Path.Combine(baseDir, ""),
                Path.Combine(baseDir, "")
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
        if (IsSelfCheckRunning) return;
        System.Threading.ThreadPool.QueueUserWorkItem(delegate
        {
            string latest, url;
            if (QueryLatestVersion("", out latest, out url))
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

    // The last known release is kept for a while. GitHub throttles repeated
    // requests from one address, and the launch check followed by a manual
    // check would otherwise ask twice in a row and get a 403 for the second
    // one - which the user would read as "".
    private static string cachedLatest;
    private static string cachedUrl;
    private static DateTime cachedAt = DateTime.MinValue;
    private static readonly TimeSpan FreshVersionWindow = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan KnownVersionWindow = TimeSpan.FromMinutes(30);

    internal static bool QueryLatestVersion(string eventType, out string latest, out string url)
    {
        // A very recent answer is reused instead of asking again.
        if (!string.IsNullOrEmpty(cachedLatest) && (DateTime.Now - cachedAt) < FreshVersionWindow)
        {
            latest = cachedLatest;
            url = cachedUrl;
            return true;
        }
        latest = null;
        url = null;
        try
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
            byte[] data = Encoding.UTF8.GetBytes("" + (eventType ?? "") + "" + CURRENT_VERSION + "");
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create("");
            req.Method = "";
            req.ContentType = "";
            req.ContentLength = data.Length;
            req.Timeout = 5000;
            req.ReadWriteTimeout = 5000;
            using (Stream s = req.GetRequestStream()) s.Write(data, 0, data.Length);
            using (WebResponse resp = req.GetResponse())
            using (StreamReader r = new StreamReader(resp.GetResponseStream()))
            {
                string body = r.ReadToEnd();
                latest = ExtractJsonValue(body, "");
                url = ExtractJsonValue(body, "");
            }
        }
        catch { }

        if (string.IsNullOrEmpty(latest))
        {
            // The release page redirects to the newest release, so the tag can
            // be read from the final url. Unlike the api this is not rate
            // limited: the anonymous api allows 60 calls an hour per ip and
            // answers 403 as soon as that is used up.
            try
            {
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create("");
                req.Method = "";
                req.UserAgent = "" + CURRENT_VERSION;
                req.Timeout = 6000;
                req.ReadWriteTimeout = 6000;
                req.AllowAutoRedirect = true;
                using (WebResponse resp = req.GetResponse())
                {
                    string finalUrl = resp.ResponseUri == null ? "" : resp.ResponseUri.AbsoluteUri;
                    int p = finalUrl.IndexOf("");
                    if (p >= 0)
                    {
                        string tag = finalUrl.Substring(p + "".Length);
                        int q = tag.IndexOf('?');
                        if (q >= 0) tag = tag.Substring(0, q);
                        tag = tag.TrimEnd('/');
                        if (tag.Length > 0)
                        {
                            latest = tag;
                            url = "" + tag;
                        }
                    }
                }
            }
            catch { }
        }

        if (string.IsNullOrEmpty(latest))
        {
            try
            {
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create("");
                req.Method = "";
                req.UserAgent = "" + CURRENT_VERSION;
                req.Timeout = 5000;
                req.ReadWriteTimeout = 5000;
                using (WebResponse resp = req.GetResponse())
                using (StreamReader r = new StreamReader(resp.GetResponseStream()))
                {
                    string body = r.ReadToEnd();
                    latest = ExtractJsonValue(body, "");
                    url = ExtractJsonValue(body, "");
                }
            }
            catch { }
        }
        if (!string.IsNullOrEmpty(latest))
        {
            cachedLatest = latest;
            cachedUrl = url;
            cachedAt = DateTime.Now;
            return true;
        }
        // The network may be throttling us. A version we already know is a
        // better answer than a failure the user has to retry.
        if (!string.IsNullOrEmpty(cachedLatest) && (DateTime.Now - cachedAt) < KnownVersionWindow)
        {
            latest = cachedLatest;
            url = cachedUrl;
            return true;
        }
        return false;
    }

    internal void NotifyUpdateFound(string newVersion, string url)
    {
        OnUpdateFound(newVersion, url);
    }

    // ---- Auto-update: pending-update marker ---------------------------------
    // Written after a successful download so the next launch can swap the exe.
    private const string PendingUpdateIni = "";

    private string AppDir
    {
        get
        {
            try { return Path.GetDirectoryName(Application.ExecutablePath); } catch { return ""; }
        }
    }

    /// <summary>Persist a downloaded update so it can be applied on restart.</summary>
    internal static void MarkPendingUpdate(string version, string exePath)
    {
        try
        {
            string appDir = Path.GetDirectoryName(Application.ExecutablePath);
            if (string.IsNullOrEmpty(appDir)) return;
            MarkPendingUpdateFile(Path.Combine(appDir, PendingUpdateIni), version, exePath);
        }
        catch { }
    }

    /// <summary>Write the pending-update marker to an explicit path.</summary>
    internal static void MarkPendingUpdateFile(string iniPath, string version, string exePath)
    {
        try
        {
            if (string.IsNullOrEmpty(iniPath)) return;
            File.WriteAllText(iniPath, "" + version + "" + exePath + "", Encoding.UTF8);
        }
        catch { }
    }

    /// <summary>The download url of a release's Windows executable.</summary>
    internal static string BuildUpdateExeUrl(string version)
    {
        string ver = (version == null ? "" : version).TrimStart('v', 'V');
        if (ver.Length == 0) return null;
        return "" + ver + "" + ver + "";
    }

    /// <summary>Put a downloaded executable in place of the running one. Windows
    /// will not let a running exe be overwritten, so the current file is renamed
    /// aside first and moved back if the second step fails: the app is never
    /// left without its executable.</summary>
    internal static bool SwapUpdateInPlace(string currentExe, string newExe, out string error)
    {
        error = null;
        if (string.IsNullOrEmpty(currentExe) || string.IsNullOrEmpty(newExe) || !File.Exists(newExe))
        {
            error = "";
            return false;
        }
        string oldExe = currentExe + "";
        try
        {
            if (File.Exists(oldExe)) { try { File.Delete(oldExe); } catch { } }
            File.Move(currentExe, oldExe);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
        try
        {
            File.Move(newExe, currentExe);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            try { File.Move(oldExe, currentExe); } catch { }
            return false;
        }
        // The old file may still be locked (it was the running image); the next
        // launch cleans it up.
        try { File.Delete(oldExe); } catch { }
        return true;
    }

    /// <summary>Parse one history-TSV session row; mirrors Main.ParseSessionRow
    /// (kept separate so AppContext stays UI-free).</summary>
    internal static bool TryParseSessionRow(string row, out long secs)
    {
        secs = 0L;
        if (string.IsNullOrEmpty(row)) return false;
        string[] cols = row.Split('\t');
        if (cols.Length < 4) return false;
        string start = cols[0], end = cols[1], dur = cols[2];
        if (cols.Length >= 5)
        {
            long.TryParse(cols[3], out secs);
        }
        else
        {
            long m;
            long.TryParse(dur, out m);
            string res = cols[3];
            if (res == "" || res == "") secs = m * 60L;
            else
            {
                DateTime t1, t2;
                if (DateTime.TryParse(start, out t1) && DateTime.TryParse(end, out t2))
                    secs = Math.Max(0L, Math.Min(m * 60L, (long)(t2 - t1).TotalSeconds));
            }
        }
        return true;
    }

    /// <summary>Total actual focus seconds logged for one yyyyMMdd date.
    /// Reads the same history TSV the history dialog uses, so the daily-goal
    /// progress always agrees with what the user sees under 查看记录.</summary>
    internal static long GetDayFocusSecs(string dir, string dateKey)
    {
        if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(dateKey)) return 0L;
        try
        {
            string path = Path.Combine(dir, "" + dateKey + "");
            if (!File.Exists(path)) return 0L;
            long sum = 0L;
            foreach (string row in File.ReadAllLines(path, Encoding.UTF8))
            {
                long s;
                if (TryParseSessionRow(row, out s)) sum += Math.Max(0L, s);
            }
            return sum;
        }
        catch { return 0L; }
    }

    /// <summary>Consecutive days (counting back from today, locale dates in
    /// yyyyMMdd) whose actual focus reached the goal. Today is allowed to fail
    /// — the day is still in progress — everything behind today has to hold.
    /// goalMinutes &lt;= 0 means the feature is off: streak 0.</summary>
    internal static int ComputeStreak(Func<string, long> dayFocus, int goalMinutes, string todayKey)
    {
        if (goalMinutes <= 0 || string.IsNullOrEmpty(todayKey)) return 0;
        long goal = goalMinutes * 60L;
        DateTime day;
        if (!DateTime.TryParseExact(todayKey, "", CultureInfo.InvariantCulture, DateTimeStyles.None, out day)) return 0;
        // Today not meeting the goal does not break the streak (day in progress).
        if (dayFocus(todayKey) < goal) day = day.AddDays(-1);
        int streak = 0;
        while (true)
        {
            if (dayFocus(day.ToString("")) < goal) break;
            streak++;
            day = day.AddDays(-1);
            if (streak >= 3650) break; // ~10 years of perfect days: guard the loop
        }
        return streak;
    }

    /// <summary>Remove a leftover iClock.exe.old from an earlier update.</summary>
    internal static void CleanupUpdateBackup(string appDir)
    {
        try
        {
            if (string.IsNullOrEmpty(appDir)) return;
            string oldExe = Path.Combine(appDir, "");
            if (File.Exists(oldExe)) { try { File.Delete(oldExe); } catch { } }
        }
        catch { }
    }

    /// <summary>Remove the pending-update marker.</summary>
    internal static void ClearPendingUpdate()
    {
        try
        {
            string appDir = Path.GetDirectoryName(Application.ExecutablePath);
            if (string.IsNullOrEmpty(appDir)) return;
            File.Delete(Path.Combine(appDir, PendingUpdateIni));
        }
        catch { }
    }

    /// <summary>Called early at startup: if a downloaded update is pending, swap
    /// the running exe out from under itself so the next launch runs the new one.</summary>
    internal void ApplyPendingUpdateOnStartup()
    {
        try
        {
            string appDir = AppDir;
            if (string.IsNullOrEmpty(appDir)) return;
            // A previous update may have left its renamed original behind.
            CleanupUpdateBackup(appDir);
            string iniPath = Path.Combine(appDir, PendingUpdateIni);
            if (!File.Exists(iniPath)) return;
            string version = null, exePath = null;
            foreach (string line in File.ReadAllLines(iniPath))
            {
                int p = line.IndexOf('=');
                if (p < 1) continue;
                string k = line.Substring(0, p).Trim();
                string v = line.Substring(p + 1).Trim();
                if (k == "") version = v;
                else if (k == "") exePath = v;
            }
            if (string.IsNullOrEmpty(version) || string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                try { File.Delete(iniPath); } catch { }
                return;
            }
            if (!IsNewer(version, CURRENT_VERSION))
            {
                try { File.Delete(iniPath); } catch { }
                return;
            }
            string currentExe = Application.ExecutablePath;
            string error;
            if (SwapUpdateInPlace(currentExe, exePath, out error))
            {
                try { File.Delete(iniPath); } catch { }
            }
            // Otherwise the file was locked: keep the marker and try again on
            // the next launch.
        }
        catch { }
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
            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "");
            if (!Directory.Exists(appData)) Directory.CreateDirectory(appData);
            string logoPath = Path.Combine(appData, "");
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
            Type toastManagerType = Type.GetType("");
            Type xmlDocType = Type.GetType("");
            Type toastNotifType = Type.GetType("");
            if (toastManagerType == null || xmlDocType == null || toastNotifType == null) return false;

            string safeTitle = System.Security.SecurityElement.Escape(title ?? "");
            string safeMessage = System.Security.SecurityElement.Escape(message ?? "");

            string xml = string.Format(
                "" +
                "" +
                "" +
                "" +
                "" +
                "" +
                "" +
                "" +
                "", safeTitle, safeMessage, logoUri);

            object xmlDoc = Activator.CreateInstance(xmlDocType);
            xmlDocType.GetMethod("", new Type[] { typeof(string) }).Invoke(xmlDoc, new object[] { xml });
            object notif = Activator.CreateInstance(toastNotifType, new object[] { xmlDoc });

            MethodInfo createNotifier = toastManagerType.GetMethod("", new Type[] { typeof(string) });
            object notifier = createNotifier.Invoke(null, new object[] { "" });
            MethodInfo showMethod = notifier.GetType().GetMethod("", new Type[] { toastNotifType });
            showMethod.Invoke(notifier, new object[] { notif });
            return true;
        }
        catch { return false; }
    }

    private void OnUpdateFound(string newVersion, string url)
    {
        if (string.IsNullOrEmpty(url)) url = "";
        latestVersion = newVersion;
        updateUrl = url;
        bool en = settings.Language == "";
        string title = en ? "" : "";
        string text = en ? "" + newVersion + "" : "" + newVersion + "";

        try { ShowTrayNotification(title, text, 6000); } catch { }

        if (menuUpdate == null && tray.ContextMenuStrip != null)
        {
            menuUpdate = new ToolStripMenuItem(en ? "" + newVersion + "" : "" + newVersion + "");
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
        int k = json.IndexOf("" + key + "", StringComparison.OrdinalIgnoreCase);
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
            cp.Caption = "";
            CreateHandle(cp);
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY) owner.HandleHotkey();
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
                            m.Result = owner.HandleIpcCommand(cmd) ? (IntPtr)1 : IntPtr.Zero;
                            return;
                        }
                    }
                    m.Result = IntPtr.Zero;
                    return;
                }
                catch { }
            }
            base.WndProc(ref m);
        }
    }

    [DllImport("")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("")]
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

    private static void WriteCliJson(bool ok, string action, string agentName, object result, string error)
    {
        Dictionary<string, object> response = new Dictionary<string, object>
        {
            { "", ok },
            { "", action },
            { "", agentName ?? "" }
        };
        if (result != null) response[""] = result;
        if (!string.IsNullOrEmpty(error)) response[""] = error;
        WriteToConsole(new JavaScriptSerializer().Serialize(response));
    }

    internal static bool IsCliConfiguredInPath()
    {
        try
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
            string userPath = Environment.GetEnvironmentVariable("", EnvironmentVariableTarget.User) ?? "";
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
                message = "";
                return true;
            }
            string dir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
            string userPath = Environment.GetEnvironmentVariable("", EnvironmentVariableTarget.User) ?? "";
            string newPath = string.IsNullOrEmpty(userPath) ? dir : (userPath.TrimEnd(';') + "" + dir);
            Environment.SetEnvironmentVariable("", newPath, EnvironmentVariableTarget.User);
            message = "";
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
            if (msg.StartsWith("", StringComparison.OrdinalIgnoreCase))
                WriteToConsole("" + dir);
            else
                WriteToConsole("");
        }
        else
        {
            WriteToConsole("" + msg);
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
  --json                          Return structured JSON for collaboration commands
  -a, --ask <question>            Flash Approval HUD: request human decision
                                   (options: --options ""A,B"", timeout: -t <sec>)
  --agent-name <name>             Name this agent's collaboration status row
  -pr, --progress <val|clear>     Show hairline agent progress on HUD (e.g. 60, 3/5, clear)
  -q, --quote                     Show daily quote popup
  --once                          Single-run mode: exit automatically after completion
                                  (ignored if desktop instance is already running)
  --quit, --exit                  Gracefully exit running iClock instance
  --install-cli                   Add iClock directory to User PATH
  --install-screensaver           Register iClock as the Windows screensaver
  -h, --help                      Show this help information

Examples:
  iclock --start 25               Start a 25-minute Pomodoro timer
  iclock -s 25 --once             Run a 25-minute timer and exit upon completion
  iclock -a ""Deploy to prod?"" --options ""Approve,Reject"" --agent-name ""Codex""
  iclock -pr 60 ""Building..."" --agent-name ""Codex""  Update task progress
  iclock -n ""Build finished!"" --agent-name ""Codex"" Trigger completion notice
  iclock --progress 3/5 ""Building..."" --agent-name ""Codex"" --json
  iclock --quit                   Close running iClock instance";
        WriteToConsole(help);
    }

    private static bool SendIpcMessage(string cmd)
    {
        IntPtr hWnd = NativeMethods.FindWindow(null, "");
        if (hWnd == IntPtr.Zero) return false;
        byte[] bytes = Encoding.Unicode.GetBytes(cmd + "");
        IntPtr ptr = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            NativeMethods.COPYDATASTRUCT cds;
            cds.dwData = IntPtr.Zero;
            cds.cbData = bytes.Length;
            cds.lpData = ptr;
            return NativeMethods.SendMessage(hWnd, 0x004A, IntPtr.Zero, ref cds) != IntPtr.Zero;
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    private static IntPtr EnsureIpcWindow()
    {
        IntPtr hWnd = NativeMethods.FindWindow(null, "");
        if (hWnd != IntPtr.Zero) return hWnd;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ""),
                UseShellExecute = true
            });
        }
        catch { }

        for (int i = 0; i < 40; i++)
        {
            System.Threading.Thread.Sleep(100);
            hWnd = NativeMethods.FindWindow(null, "");
            if (hWnd != IntPtr.Zero) return hWnd;
        }
        return IntPtr.Zero;
    }

    private static int RunAskCommand(string[] args, bool json)
    {
        string question = null;
        List<string> options = new List<string>();
        int timeoutSec = 0;
        string agentName = null;

        for (int i = 1; i < args.Length; i++)
        {
            string a = args[i];
            if (a == "" || a == "" || a == "")
            {
                if (i + 1 < args.Length)
                {
                    string[] parts = args[++i].Split(new char[] { ',', '，', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string part in parts)
                    {
                        string option = part.Trim();
                        if (option.Length > 0) options.Add(option);
                    }
                }
            }
            else if (a == "" || a == "")
            {
                int parsed;
                if (i + 1 < args.Length && int.TryParse(args[++i], out parsed) && parsed > 0) timeoutSec = parsed;
            }
            else if (a == "" && i + 1 < args.Length)
            {
                agentName = args[++i].Replace("", "").Replace("", "").Trim();
            }
            else if (question == null && !a.StartsWith(""))
            {
                question = a;
            }
        }

        if (string.IsNullOrEmpty(question))
        {
            string error = "";
            if (json) WriteCliJson(false, "", agentName, null, error);
            else Console.Error.WriteLine("" + error);
            return 1;
        }

        if (!Settings.Load().DesktopCollaborationEnabled)
        {
            string error = "";
            if (json) WriteCliJson(false, "", agentName, null, error);
            else Console.Error.WriteLine("" + error);
            return 1;
        }

        if (EnsureIpcWindow() == IntPtr.Zero)
        {
            string error = "";
            if (json) WriteCliJson(false, "", agentName, null, error);
            else Console.Error.WriteLine("" + error);
            return 1;
        }

        string requestId = Guid.NewGuid().ToString("").Substring(0, 8);
        string resultFile = Path.Combine(Path.GetTempPath(), "" + requestId + "");
        string command = "" + requestId + "" + timeoutSec + "" + question + "" + string.Join("", options.ToArray()) + "" + agentName;
        if (!SendIpcMessage(command))
        {
            string error = "";
            if (json) WriteCliJson(false, "", agentName, null, error);
            else Console.Error.WriteLine("" + error);
            return 1;
        }

        int maxWait = timeoutSec > 0 ? (timeoutSec + 5) * 1000 : -1;
        int waited = 0;
        while (maxWait < 0 || waited < maxWait)
        {
            System.Threading.Thread.Sleep(100);
            waited += 100;
            if (!File.Exists(resultFile)) continue;
            try
            {
                string result = File.ReadAllText(resultFile, Encoding.UTF8).Trim();
                File.Delete(resultFile);
                if (result == "" || result == "" || result == "")
                {
                    if (json) WriteCliJson(false, "", agentName, null, result);
                    else Console.Error.WriteLine(result);
                    return 1;
                }
                if (result == "")
                {
                    if (json) WriteCliJson(false, "", agentName, null, result);
                    else Console.Error.WriteLine(result);
                    return 2;
                }
                if (json) WriteCliJson(true, "", agentName, result, null);
                else WriteToConsole(result);
                return 0;
            }
            catch { }
        }

        if (json) WriteCliJson(false, "", agentName, null, "");
        else Console.Error.WriteLine("");
        return 2;
    }

    private static int RunCliDesktopCommand(string[] args, string action, bool json)
    {
        string agentName = null;
        for (int i = 1; i + 1 < args.Length; i++)
            if (args[i] == "") agentName = args[i + 1].Trim('"');

        bool cleanup = action == "" && args.Length > 1
            && (args[1].Equals("", StringComparison.OrdinalIgnoreCase)
                || args[1].Equals("", StringComparison.OrdinalIgnoreCase)
                || args[1].Equals("", StringComparison.OrdinalIgnoreCase));
        if (!Settings.Load().DesktopCollaborationEnabled && !cleanup)
        {
            string error = "";
            if (json) WriteCliJson(false, action, agentName, null, error);
            else Console.Error.WriteLine("" + error);
            return 1;
        }

        if (EnsureIpcWindow() == IntPtr.Zero || !SendIpcMessage(string.Join("", args)))
        {
            string error = "";
            if (json) WriteCliJson(false, action, agentName, null, error);
            else Console.Error.WriteLine("" + error);
            return 1;
        }

        if (json) WriteCliJson(true, action, agentName, "", null);
        else WriteToConsole("");
        return 0;
    }

    private static int RunQuoteImportCommand(string[] args, bool json)
    {
        string batchFile = args.Length > 1 ? args[1].Trim('""";
        if (string.IsNullOrWhiteSpace(batchFile) || !File.Exists(batchFile))
        {
            string error = "";
            if (json) WriteCliJson(false, "", null, null, error);
            else Console.Error.WriteLine("" + error);
            return 1;
        }

        batchFile = Path.GetFullPath(batchFile);
        string resultFile = batchFile + "";
        try { if (File.Exists(resultFile)) File.Delete(resultFile); } catch { }
        if (EnsureIpcWindow() == IntPtr.Zero || !SendIpcMessage("" + batchFile.Replace("", "") + ""))
        {
            string error = "";
            if (json) WriteCliJson(false, "", null, null, error);
            else Console.Error.WriteLine("" + error);
            return 1;
        }

        for (int i = 0; i < 100 && !File.Exists(resultFile); i++) System.Threading.Thread.Sleep(50);
        int addedCount;
        try
        {
            if (!File.Exists(resultFile) || !int.TryParse(File.ReadAllText(resultFile, Encoding.UTF8).Trim(), out addedCount))
            {
                string error = "";
                if (json) WriteCliJson(false, "", null, null, error);
                else Console.Error.WriteLine("" + error);
                return 1;
            }
        }
        catch (Exception ex)
        {
            if (json) WriteCliJson(false, "", null, null, ex.Message);
            else Console.Error.WriteLine("" + ex.Message);
            return 1;
        }
        try { File.Delete(resultFile); } catch { }
        object result = new Dictionary<string, object> { { "", addedCount } };
        if (json) WriteCliJson(true, "", null, result, null);
        else WriteToConsole("" + addedCount + "");
        return 0;
    }

    [STAThread]
    private static void Main(string[] args)
    {
        // Windows screensaver dispatch: explorer launches the .scr copy with /s (run),
        // /p <hwnd> (preview) or /c (configure). Handle these before CLI parsing and
        // before the single-instance mutex, so the saver never hijacks the tray app.
        if (args != null && args.Length > 0)
        {
            string firstRaw = args[0];
            if (string.Equals(firstRaw, "", StringComparison.OrdinalIgnoreCase))
            {
                LogSaverEvent("" + firstRaw + "" + Assembly.GetExecutingAssembly().Location);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new QuoteScreensaverForm());
                LogSaverEvent("");
                return;
            }
            if (string.Equals(firstRaw, "", StringComparison.OrdinalIgnoreCase))
            {
                RunSaverConfigure();
                return;
            }
            if (string.Equals(firstRaw, "", StringComparison.OrdinalIgnoreCase))
            {
                // Mini preview inside Display Properties: skip (tiny child window
                // painting has no value for a full-screen text saver).
                return;
            }
        }
        if (args != null && args.Length > 0)
        {
            bool json = false;
            List<string> commandArgs = new List<string>();
            foreach (string arg in args)
            {
                if (string.Equals(arg, "", StringComparison.OrdinalIgnoreCase)) json = true;
                else commandArgs.Add(arg);
            }
            if (json) args = commandArgs.ToArray();
            if (args.Length == 0)
            {
                if (json) WriteCliJson(false, "", null, null, "");
                else PrintHelp();
                return;
            }
            string first = args[0].ToLowerInvariant();
            if (first == "" || first == "" || first == "" || first == "" || first == "" || first == "")
            {
                Console.Error.WriteLine("");
                Environment.ExitCode = 2;
                return;
            }
            if (first == "" || first == "" || first == "")
            {
                Environment.ExitCode = RunAskCommand(args, json);
                return;
            }
            if (first == "" || first == "" || first == "" || first == "")
            {
                PrintHelp();
                return;
            }
            if (first == "" || first == "")
            {
                InstallCli();
                return;
            }
            if (first == "" || first == "")
            {
                Settings regSettings = Settings.Load();
                if (ApplySaverRegistry(regSettings))
                {
                    regSettings.QuoteSaverEnabled = true;
                    regSettings.Save();
                    WriteToConsole("");
                }
                else
                {
                    WriteToConsole("");
                }
                return;
            }
            if (first == "" || first == "")
            {
                Environment.ExitCode = RunQuoteImportCommand(args, json);
                return;
            }
            if (json && (first == "" || first == "" || first == "" || first.StartsWith("")))
            {
                Environment.ExitCode = RunCliDesktopCommand(args, "", true);
                return;
            }
            if (json && (first == "" || first == "" || first == "" || first.StartsWith("")))
            {
                Environment.ExitCode = RunCliDesktopCommand(args, "", true);
                return;
            }
            bool isProgressCommand = first == "" || first == "" || first == "" || first.StartsWith("");
            string progressArg = first.StartsWith("") ? first.Substring("".Length)
                : (args.Length > 1 ? args[1] : "");
            bool isProgressCleanup = isProgressCommand && (progressArg.Equals("", StringComparison.OrdinalIgnoreCase)
                || progressArg.Equals("", StringComparison.OrdinalIgnoreCase) || progressArg.Equals("", StringComparison.OrdinalIgnoreCase));
            if ((first == "" || first == "" || first == "" || first.StartsWith("")
                || (isProgressCommand && !isProgressCleanup)) && !Settings.Load().DesktopCollaborationEnabled)
            {
                Console.Error.WriteLine("");
                Environment.ExitCode = 1;
                return;
            }
            if (first == "" || first == "" || first == "" || first == "" || first == "")
            {
                if (SendIpcMessage(""))
                    WriteToConsole("");
                else
                    WriteToConsole("");
                return;
            }
        }

        bool created;
        using (System.Threading.Mutex m = new System.Threading.Mutex(true, "", out created))
        {
            if (!created)
            {
                bool hasOnce = false;
                List<string> cleanArgs = new List<string>();
                if (args != null && args.Length > 0)
                {
                    foreach (string a in args)
                    {
                        if (string.Equals(a, "", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(a, "", StringComparison.OrdinalIgnoreCase))
                            hasOnce = true;
                        else
                            cleanArgs.Add(a);
                    }
                }

                if (cleanArgs.Count == 0) cleanArgs.Add(hasOnce ? "" : "");
                string cmd = string.Join("", cleanArgs.ToArray());
                if (SendIpcMessage(cmd))
                {
                    if (hasOnce) WriteToConsole("");
                    else if (cmd == "") WriteToConsole("");
                    else WriteToConsole("" + cmd);
                }
                else
                {
                    WriteToConsole("");
                }
                return;
            }

            bool isOneShot = false;
            List<string> runArgs = new List<string>();
            if (args != null && args.Length > 0)
            {
                foreach (string a in args)
                {
                    if (string.Equals(a, "", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(a, "", StringComparison.OrdinalIgnoreCase))
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
                    runArgs.Add("");
                }
                WriteToConsole("");
            }
            else if (runArgs.Count > 0)
            {
                WriteToConsole("" + string.Join("", runArgs.ToArray()));
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new AppContext(runArgs.ToArray(), isOneShot));
        }
    }

    // ---- Screensaver (.scr) support -----------------------------------------

    // Local per-user location for the .scr copy that Windows looks up. It is
    // writable without elevation and survives updates of the portable exe.
    internal static string SaverScrPath
    {
        get
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "", "");
        }
    }

    // Copy the running exe into LocalAppData as a .scr so Windows can launch it.
    // Returns true when the copy at SaverScrPath is current.
    internal static bool InstallSaverScr()
    {
        try
        {
            string scr = SaverScrPath;
            string dir = Path.GetDirectoryName(scr);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string self = Assembly.GetExecutingAssembly().Location;
            if (string.IsNullOrEmpty(self)) return false;
            if (File.Exists(scr) && FilesEqual(self, scr)) return true;
            // If the target is locked (saver currently running), retrying later is fine.
            if (File.Exists(scr))
            {
                try { File.Delete(scr); } catch { return false; }
            }
            File.Copy(self, scr, true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // Remove the .scr copy on opt-out so the saver list does not pile up entries.
    internal static void UninstallSaverScr()
    {
        try
        {
            string scr = SaverScrPath;
            if (File.Exists(scr)) File.Delete(scr);
        }
        catch { }
    }

    private const string SaverRegKey = @"";
    // Windows launches whatever SCRNSAVE.EXE names. "" is written alongside it
    // because some tools (and older Windows builds) read that name instead — writing
    // only SCR is what silently disabled the saver here: the idle timer fired, found
    // no screensaver, and did nothing.
    private const string SaverScrValue = "";
    private const string SaverScrLegacyValue = "";
    private const string SaverActiveValue = "";
    private const string SaverTimeoutValue = "";

    // What Windows is actually configured to run, tolerating either value name.
    private static string ReadSaverScr()
    {
        string current = ReadSaverRegValue(SaverScrValue);
        if (string.IsNullOrEmpty(current)) current = ReadSaverRegValue(SaverScrLegacyValue);
        return current ?? "";
    }

    private static bool SaverScrIsOurs()
    {
        return string.Equals(ReadSaverScr(), SaverScrPath, StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteSaverScr(string path)
    {
        WriteSaverRegValue(SaverScrValue, path);
        WriteSaverRegValue(SaverScrLegacyValue, path);
    }

    private static string ReadSaverRegValue(string name)
    {
        try { return Registry.GetValue("" + SaverRegKey, name, "") as string ?? ""; }
        catch { return ""; }
    }

    private static void WriteSaverRegValue(string name, object value)
    {
        using (RegistryKey k = Registry.CurrentUser.CreateSubKey(SaverRegKey))
        {
            if (k != null) k.SetValue(name, value, RegistryValueKind.String);
        }
    }

    private static void DeleteSaverRegValue(string name)
    {
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(SaverRegKey, true))
            {
                if (k != null) k.DeleteValue(name, false);
            }
        }
        catch { }
    }

    // Windows caches these for the Settings UI; this broadcast makes the change
    // take effect immediately instead of after a sign-out.
    private static void BroadcastSaverRegChange()
    {
        try
        {
            IntPtr broadcastResult;
            NativeMethods.SendMessageTimeout(
                new IntPtr(0xffff), 0x001A, IntPtr.Zero, "",
                0x0002 /* SMTO_ABORTIFHUNG */, 2000, out broadcastResult);
        }
        catch { }
    }

    // Startup self-heal, in both directions. With the toggle on, re-register when
    // the registration was lost (a cleaner reset HKCU, or Windows fell back to
    // "") — unless the user picked a different screensaver in Windows
    // meanwhile. With the toggle off, tear down a registration that is still ours
    // (settings.ini deleted, or copied over from another machine), because ""
    // has to mean Windows is not running iClock.scr.
    internal static void EnsureSaverRegistry(Settings settings)
    {
        try
        {
            if (settings == null) return;
            if (!settings.QuoteSaverEnabled)
            {
                if (SaverScrIsOurs()) RestoreSaverRegistry(settings);
                return;
            }
            string current = ReadSaverScr();
            if (!string.IsNullOrEmpty(current) &&
                !string.Equals(current, SaverScrPath, StringComparison.OrdinalIgnoreCase)) return;
            ApplySaverRegistry(settings);
        }
        catch { }
    }

    // Enable: point the Windows screensaver at iClock.scr and turn it on. The
    // user's previous values are captured once so RestoreSaverRegistry can put
    // them back on opt-out. Backup format: "";
    // older installs still hold the pre-fix 3-part "".
    internal static bool ApplySaverRegistry(Settings settings)
    {
        if (!InstallSaverScr()) return false;
        try
        {
            string currentScr = ReadSaverScr();
            // Capture the user's own screensaver settings the first time iClock
            // takes the registration over, so opting out can put them back.
            if (!string.Equals(currentScr, SaverScrPath, StringComparison.OrdinalIgnoreCase) &&
                settings != null && string.IsNullOrEmpty(settings.SaverRegBackup))
            {
                settings.SaverRegBackup = string.Join("",
                    "",
                    ReadSaverRegValue(SaverScrValue) ?? "",
                    ReadSaverRegValue(SaverScrLegacyValue) ?? "",
                    ReadSaverRegValue(SaverActiveValue) ?? "",
                    ReadSaverRegValue(SaverTimeoutValue) ?? "");
                settings.Save();
            }
            WriteSaverScr(SaverScrPath);
            WriteSaverRegValue(SaverActiveValue, "");
            WriteSaverWaitSeconds(settings);
            PushSaverActiveToSession(true);
            BroadcastSaverRegChange();
            return true;
        }
        catch { return false; }
    }

    // Diagnostics for the saver path: Windows launching a .scr is invisible from
    // the tray app, and "" cannot be told apart from the
    // display powering down without a trace. Appends to saver.log next to iClock.scr.
    internal static void LogSaverEvent(string message)
    {
        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, ""),
                DateTime.Now.ToString("") + "" + Process.GetCurrentProcess().Id + "" + message + "");
        }
        catch { }
    }

    // The idle wait belongs to iClock, not to Windows: Windows' screen saver dialog
    // only lists System32 .scr files, so it cannot show or edit iClock's entry.
    private static void WriteSaverWaitSeconds(Settings settings)
    {
        int mins = settings != null && settings.SaverMinutes > 0 ? settings.SaverMinutes : 10;
        mins = Math.Max(1, Math.Min(60, mins));
        WriteSaverRegValue(SaverTimeoutValue, (mins * 60).ToString(CultureInfo.InvariantCulture));
        PushSaverTimeoutToSession(mins * 60);
    }

    // Writing ScreenSaveTimeOut to the registry does not arm the idle timer: the
    // session keeps its own copy (SPI_GETSCREENSAVETIMEOUT still reported 1800 while
    // the registry said 60, so the saver never fired). desk.cpl goes through
    // SystemParametersInfo, and so must iClock.
    private static void PushSaverTimeoutToSession(int seconds)
    {
        try
        {
            NativeMethods.SystemParametersInfo(0x000F /* SPI_SETSCREENSAVETIMEOUT */,
                (uint)Math.Max(0, seconds), IntPtr.Zero, 0x0001 | 0x0002 /* UPDATEINIFILE | SENDCHANGE */);
        }
        catch { }
    }

    private static void PushSaverActiveToSession(bool active)
    {
        try
        {
            NativeMethods.SystemParametersInfo(0x0011 /* SPI_SETSCREENSAVEACTIVE */,
                active ? 1u : 0u, IntPtr.Zero, 0x0001 | 0x0002 /* UPDATEINIFILE | SENDCHANGE */);
        }
        catch { }
    }

    // Called when the wait time is changed from the toggle's menu: persist it, and
    // push it to Windows straight away when the saver is actually enabled.
    internal static void ApplySaverWait(Settings settings)
    {
        try
        {
            if (settings == null) return;
            settings.Save();
            if (!settings.QuoteSaverEnabled) return;
            WriteSaverWaitSeconds(settings);
            BroadcastSaverRegChange();
        }
        catch { }
    }

    // Opt-out: put the captured values back (or remove ours when the user had
    // none), and drop the .scr copy so the saver list does not pile up entries.
    internal static void RestoreSaverRegistry(Settings settings)
    {
        try
        {
            string backup = settings != null ? settings.SaverRegBackup ?? "" : "";
            string[] parts = backup.Split('\x1f');
            if (parts.Length == 5 && parts[0] == "")
            {
                RestoreOne(SaverScrValue, parts[1]);
                RestoreOne(SaverScrLegacyValue, parts[2]);
                RestoreOne(SaverActiveValue, parts[3]);
                RestoreOne(SaverTimeoutValue, parts[4]);
            }
            else if (parts.Length == 3)
            {
                // Backup written before iClock knew Windows reads SCRNSAVE.EXE:
                // only the legacy name was captured, so put that back and drop our
                // own SCRNSAVE.EXE entry.
                RestoreOne(SaverScrLegacyValue, parts[0]);
                RestoreOne(SaverActiveValue, parts[1]);
                RestoreOne(SaverTimeoutValue, parts[2]);
                if (SaverScrIsOurs()) DeleteSaverRegValue(SaverScrValue);
            }
            else
            {
                // No backup captured: only clear a selection that is still ours.
                if (SaverScrIsOurs())
                {
                    DeleteSaverRegValue(SaverScrValue);
                    DeleteSaverRegValue(SaverScrLegacyValue);
                }
            }
            // The session's own copy has to go back too, or Windows keeps waiting
            // for iClock's old timeout after the user switched it off.
            PushSaverActiveToSession(string.Equals(ReadSaverRegValue(SaverActiveValue), "", StringComparison.Ordinal));
            int restoredTimeout;
            if (int.TryParse(ReadSaverRegValue(SaverTimeoutValue), out restoredTimeout))
                PushSaverTimeoutToSession(restoredTimeout);
            if (settings != null) { settings.SaverRegBackup = ""; settings.Save(); }
            BroadcastSaverRegChange();
        }
        catch { }
        UninstallSaverScr();
    }

    private static void RestoreOne(string name, string oldValue)
    {
        if (string.IsNullOrEmpty(oldValue)) DeleteSaverRegValue(name);
        else WriteSaverRegValue(name, oldValue);
    }

    internal static bool FilesEqual(string a, string b)
    {
        try
        {
            if (!File.Exists(a) || !File.Exists(b)) return false;
            FileInfo fa = new FileInfo(a), fb = new FileInfo(b);
            if (fa.Length != fb.Length) return false;
            if (Math.Abs((fa.LastWriteTimeUtc - fb.LastWriteTimeUtc).TotalSeconds) < 2) return true;
            using (FileStream fa1 = File.OpenRead(a), fb1 = File.OpenRead(b))
            {
                byte[] bufA = new byte[64 * 1024], bufB = new byte[64 * 1024];
                int n;
                while ((n = fa1.Read(bufA, 0, bufA.Length)) > 0)
                {
                    if (fb1.Read(bufB, 0, bufB.Length) != n) return false;
                    for (int i = 0; i < n; i++) if (bufA[i] != bufB[i]) return false;
                }
                return true;
            }
        }
        catch { return false; }
    }

    // /c (configure) handler: open the main app (or its settings dialog) so the
    // user can switch the saver mode. The running tray instance owns settings,
    // so activate it via the existing single-instance IPC.
    private static void RunSaverConfigure()
    {
        if (SendIpcMessage(""))
        {
            WriteToConsole("");
        }
        else
        {
            // No running instance: start the app normally; it opens the tray and
            // settings, which is the closest equivalent for a non-GUI user flow.
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new AppContext(new string[0], false));
        }
    }
}

// Full-screen black window that shows today's quote when the Windows screensaver
// runs. Explorer launches the exe with /s; any mouse or keyboard input closes it
// (WM_CLOSE), matching standard screensaver behavior.
