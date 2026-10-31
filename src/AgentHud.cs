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

internal sealed class AgentTaskStatus
{
    internal string AgentName;
    internal string SessionKey;
    internal bool HasProgress;
    internal float Progress;
    internal string Message;
    internal bool IsThinking;
    internal string SubDetail;
    internal string StepProgress;
    internal DateTime TurnStartTime;
    internal DateTime StepStartTime;
    internal double ForegroundSeenSeconds;
    internal DateTime MessageTimestamp;
    internal int DetailShownTick;
    internal int DetailLevel;
    internal bool InterruptHold;
    internal readonly List<string> DetailQueue = new List<string>();
    internal int DetailQueueTick;
    internal const int DetailDwellMs = 700;

    // 0 interrupt, 1 complete, 2 write/run,
    // 3 search, 4 read, 5 thought. -1 = generic keep, not a new slot.
    internal static int ClassifyHudNode(string text, string message, out bool interrupt)
    {
        interrupt = false;
        string m = message ?? "";
        if (m.StartsWith("\u26a0") || m.IndexOf("\u5f02\u5e38", StringComparison.Ordinal) >= 0 || m.IndexOf("\u6b7b\u5faa\u73af", StringComparison.Ordinal) >= 0)
        {
            interrupt = true;
            return 0;
        }
        if (m.StartsWith("\u2753") || m.IndexOf("\u7b49\u5f85", StringComparison.Ordinal) >= 0 || m.IndexOf("Waiting", StringComparison.Ordinal) >= 0 || m.IndexOf("\u51b3\u7b56", StringComparison.Ordinal) >= 0)
        {
            interrupt = true;
            return 0;
        }
        if (m.StartsWith("\u2705") || m.IndexOf("\u5b8c\u6210", StringComparison.Ordinal) >= 0 || m.IndexOf("Completed", StringComparison.Ordinal) >= 0)
            return 1;

        string s = (text ?? "").Trim();
        if (s.Length == 0) return 2;
        if (s.StartsWith("\u2713") || s.StartsWith("\u2714")) return -1;
        if (IsThoughtDetail(s)) return 5;
        if (s.StartsWith("\u2753") || s.StartsWith("\u8bf7\u786e\u8ba4") || s.StartsWith("Confirm:"))
        {
            interrupt = true;
            return 0;
        }
        if (s.StartsWith("\u26a0"))
        {
            interrupt = true;
            return 0;
        }
        string sl = s.ToLowerInvariant();
        if (sl.IndexOf("\u9605\u8bfb", StringComparison.Ordinal) >= 0 || sl.StartsWith("read_file") ||
            sl.IndexOf("\u8bfb\u53d6", StringComparison.Ordinal) >= 0 || sl.StartsWith("read ") || sl.StartsWith("reading"))
            return 4;
        if (sl.StartsWith("search:") || sl.StartsWith("search ") || sl.IndexOf("\u641c\u7d22", StringComparison.Ordinal) >= 0 ||
            sl.IndexOf("grep", StringComparison.Ordinal) >= 0 || sl.IndexOf("glob", StringComparison.Ordinal) >= 0)
            return 3;
        if (sl.StartsWith("edit_file") || sl.StartsWith("write_file") || sl.StartsWith("delete_file") ||
            sl.IndexOf("\u4fee\u6539", StringComparison.Ordinal) >= 0 || sl.IndexOf("\u5199\u5165", StringComparison.Ordinal) >= 0 ||
            sl.StartsWith("\u26a1") || sl.StartsWith("\u8fd0\u884c") || sl.StartsWith("run:") || sl.StartsWith("run ") ||
            sl.IndexOf("build", StringComparison.Ordinal) >= 0 || sl.IndexOf("selfcheck", StringComparison.Ordinal) >= 0 ||
            sl.IndexOf("csc", StringComparison.Ordinal) >= 0)
            return 2;
        return 2;
    }

    internal void ResetDisplayQueue()
    {
        DetailShownTick = 0;
        DetailLevel = 0;
        InterruptHold = false;
        DetailQueue.Clear();
    }

    internal void QueueAfterCurrent(IList nodes)
    {
        DetailQueue.Clear();
        if (nodes == null) return;
        for (int i = 1; i < nodes.Count; i++)
        {
            string text = nodes[i] as string;
            if (string.IsNullOrEmpty(text)) continue;
            if (DetailQueue.Count == 0 && string.Equals(text, SubDetail, StringComparison.Ordinal)) continue;
            if (DetailQueue.Count > 0 && string.Equals(DetailQueue[DetailQueue.Count - 1], text, StringComparison.Ordinal)) continue;
            DetailQueue.Add(text);
        }
        DetailQueueTick = Environment.TickCount;
    }

    internal bool AdvanceDetailQueue(bool visible)
    {
        if (DetailQueue.Count == 0) return false;
        if (!visible)
        {
            DetailQueueTick = Environment.TickCount;
            return false;
        }
        int elapsed = unchecked(Environment.TickCount - DetailQueueTick);
        if (elapsed < DetailDwellMs) return false;
        string next = DetailQueue[0];
        DetailQueue.RemoveAt(0);
        SubDetail = next;
        bool interrupt;
        DetailLevel = ClassifyHudNode(next, Message, out interrupt);
        DetailShownTick = Environment.TickCount;
        DetailQueueTick = DetailShownTick;
        StepStartTime = DateTime.Now;
        return true;
    }

    internal void OfferDisplay(string text, string message)
    {
        bool interrupt;
        int level = ClassifyHudNode(text, message, out interrupt);
        DetailQueue.Clear();
        if (interrupt)
        {
            SubDetail = text;
            DetailLevel = 0;
            DetailShownTick = Environment.TickCount;
            InterruptHold = true;
            return;
        }
        InterruptHold = false;
        if (string.IsNullOrEmpty(text)) return;
        // A newer line replaces whatever is on screen, including a thought sentence.
        SubDetail = text;
        DetailLevel = level;
        DetailShownTick = Environment.TickCount;
    }

    private static bool IsThoughtDetail(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        if (text.StartsWith("\uD83D\uDCAD")) return true;
        // Same words the agent shows once a model call has started reasoning.
        if (text.StartsWith("\u6b63\u5728\u601d\u8003 \u00b7 ")) return true;
        if (text.StartsWith("Thinking \u00b7 ")) return true;
        return false;
    }
}

internal sealed class AgentTaskStatusHud : Form
{
    private const int MinHudWidth = 200;
    private const int MaxHudWidth = 1400;
    private const int CollapsedHeight = 28;   // minimum card height (and the default)
    private const int MaxCardHeight = 60;
    private int CardHeight
    {
        get
        {
            int h = settings != null ? settings.TaskStatusHeight : CollapsedHeight;
            return Math.Max(CollapsedHeight, Math.Min(MaxCardHeight, h));
        }
    }

    // Standardized typography & state color palette (Option A)
    private static readonly Color ColorBg = Color.FromArgb(235, 18, 20, 24);              // Deep acrylic glass #121418
    private static readonly Color ColorBorderNormal = Color.FromArgb(45, 255, 255, 255);      // 18% frosted border
    private static readonly Color ColorBorderHover = Color.FromArgb(90, 255, 255, 255);       // 35% hover border
    private static readonly Color ColorTitleText = Color.FromArgb(248, 250, 252);          // #F8FAFC crisp white
    private static readonly Color ColorRunning = Color.FromArgb(56, 189, 248);             // #38BDF8 Cyan Blue
    private static readonly Color ColorWaiting = Color.FromArgb(245, 158, 11);             // #F59E0B Amber
    private static readonly Color ColorError = Color.FromArgb(248, 113, 113);              // #F87171 Coral Red
    private static readonly Color ColorDone = Color.FromArgb(34, 197, 94);                 // #22C55E Emerald Green

    private readonly Settings settings;
    private List<AgentTaskStatus> tasks = new List<AgentTaskStatus>();
    private bool moving;
    private bool resizingWidth;
    private bool resizingHeight;
    private Form snapGuideForm;
    private bool snapGuideV;
    private string pinnedAgent;
    private Point dragStart;
    private Point formStart;
    private int dragStartWidth;
    private int dragStartHeight;
    private Bitmap renderBitmap;
    private byte[] premulBuffer;
    private Font cachedBadgeFont;
    private Font cachedPillFont;
    private float cachedBadgeSize = -1f;
    private float cachedPillSize = -1f;
    private Font cachedTitleFont;
    private float cachedTitleFontSize = -1f;
    private SolidBrush cachedTitleBrush;
    private Pen cachedBorderNormal;
    private Pen cachedBorderHover;
    private StringFormat cachedTitleFormat;
    private StringFormat cachedCenterFormat;

    private Timer heartbeatTimer;
    private bool isHovered;
    private int currentAgentIndex = 0;
    private AgentTaskStatus lastDisplayedTask;
    private int lastRotationTick = Environment.TickCount;
    private int manualLockUntilTick = 0;
    private int lastHoverPillIndex = -1;
    private bool hoverColorDot;
    private const int AutoRotationCadenceMs = 5000;

    private DateTime lastHoverTime;
    private DateTime lastFgCheckTime = DateTime.MinValue;
    private NativeMethods.WinEventDelegate foregroundHookProc;
    private IntPtr foregroundHook = IntPtr.Zero;
    private IntPtr minimizeHook = IntPtr.Zero;
    private int fadeStartTick = 0;
    private byte currentAlpha = 255;

    private int CurrentWidth
    {
        get
        {
            if (settings != null && settings.TaskStatusWidth > 0 && settings.TaskStatusWidth != 300)
                return settings.TaskStatusWidth;
            Screen screen = Screen.FromPoint(Location.X > -9000 ? Location : Cursor.Position);
            return Math.Max(MinHudWidth, Math.Min(MaxHudWidth, screen.WorkingArea.Width / 3));
        }
    }

    private List<AgentTaskStatus> GetActiveTasks()
    {
        List<AgentTaskStatus> active = new List<AgentTaskStatus>();
        if (tasks == null) return active;
        DateTime now = DateTime.Now;
        Dictionary<string, AgentTaskStatus> projectMap = new Dictionary<string, AgentTaskStatus>(StringComparer.OrdinalIgnoreCase);

        bool stayPinned = !string.IsNullOrEmpty(pinnedAgent);
        for (int i = 0; i < tasks.Count; i++)
        {
            AgentTaskStatus t = tasks[i];
            // A finished task leaves after 5 quiet minutes. Pinning keeps the bar
            // on screen; it does not bring those finished sessions back.
            if (IsFinishedTask(t) && t.MessageTimestamp > DateTime.MinValue && (now - t.MessageTimestamp).TotalMinutes >= 5.0) continue;

            string key = GetProjectKey(t);
            AgentTaskStatus existing;
            if (projectMap.TryGetValue(key, out existing))
            {
                // 同项目多会话协同合流：等待/错误 (3) > 运行中 (2) > 完成 (1)
                int pCurrent = GetTaskPriority(t);
                int pExisting = GetTaskPriority(existing);
                if (pCurrent > pExisting || (pCurrent == pExisting && t.MessageTimestamp > existing.MessageTimestamp))
                {
                    projectMap[key] = t;
                }
            }
            else
            {
                projectMap[key] = t;
            }
        }

        foreach (var v in projectMap.Values)
        {
            active.Add(v);
        }
        if (active.Count == 0 && stayPinned && lastDisplayedTask != null)
        {
            active.Add(lastDisplayedTask);
        }
        // Group same-agent sessions adjacently so multi-session pills are never
        // split by other agents. SessionCompareTask keeps the pill order and
        // the "#n" labels in sync.
        active.Sort(CompareTasksForDisplay);

        // After re-sorting, the previously displayed task may sit at a new
        // index. Track it by reference so the HUD doesn't visually "jump" to
        // a different agent when the order shifts.
        if (lastDisplayedTask != null)
        {
            int idx = -1;
            for (int i = 0; i < active.Count; i++)
            {
                if (object.ReferenceEquals(active[i], lastDisplayedTask)) { idx = i; break; }
            }
            if (idx >= 0)
            {
                // Same task still visible: pin the index to it so a sort shift
                // doesn't change which pill is currently selected.
                currentAgentIndex = idx;
            }
            else
            {
                // Task vanished (expired / cleared): drop the anchor so the
                // next frame picks a fresh one.
                lastDisplayedTask = null;
            }
        }
        if (lastDisplayedTask == null && active.Count > 0)
        {
            if (currentAgentIndex >= active.Count) currentAgentIndex = active.Count - 1;
            if (currentAgentIndex < 0) currentAgentIndex = 0;
            lastDisplayedTask = active[currentAgentIndex];
        }
        return active;
    }

    private static string BaseAgentName(AgentTaskStatus t)
    {
        if (t == null) return "";
        string n = t.AgentName ?? "";
        int at = n.IndexOf('@');
        if (at > 0) n = n.Substring(0, at).Trim();
        return n;
    }

    // Shared ordering for the pill row: agent name alphabetically, then keyed
    // sessions by key, with the legacy keyless session last. Using the same
    // comparer here and in GetPillTextForTask keeps "#1" left of "#2".
    private static int CompareTasksForDisplay(AgentTaskStatus a, AgentTaskStatus b)
    {
        int c = string.Compare(BaseAgentName(a), BaseAgentName(b), StringComparison.OrdinalIgnoreCase);
        if (c != 0) return c;
        bool ak = string.IsNullOrEmpty(a != null ? a.SessionKey : null);
        bool bk = string.IsNullOrEmpty(b != null ? b.SessionKey : null);
        if (ak != bk) return ak ? 1 : -1;
        return string.Compare(a != null ? (a.SessionKey ?? "") : "", b != null ? (b.SessionKey ?? "") : "", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetProjectKey(AgentTaskStatus t)
    {
        if (t == null) return "unknown";
        string name = t.AgentName ?? "";
        int at = name.IndexOf('@');
        if (at > 0)
        {
            name = name.Substring(0, at).Trim();
        }
        string key = "agent:" + name.ToLowerInvariant();
        if (!string.IsNullOrEmpty(t.SessionKey)) key += "|sess:" + t.SessionKey.ToLowerInvariant();
        return key;
    }

    private static bool HasRecentDynamic(AgentTaskStatus t)
    {
        if (t == null || t.MessageTimestamp <= DateTime.MinValue) return false;
        return (DateTime.Now - t.MessageTimestamp).TotalMinutes < 5.0;
    }

    private static bool IsFinishedTask(AgentTaskStatus t)
    {
        if (t == null || t.IsThinking || t.HasProgress) return false;
        string m = t.Message ?? "";
        return m.StartsWith("\u2705") || m.Contains("\u5b8c\u6210") || m.Contains("Completed");
    }

    private static int GetTaskPriority(AgentTaskStatus t)
    {
        if (t == null) return 0;
        string m = t.Message ?? "";
        if (m.StartsWith("\u26a0") || m.Contains("\u5f02\u5e38") || m.StartsWith("\u2753") || m.Contains("\u7b49\u5f85") || m.Contains("\u51b3\u7b56")) return 3;
        bool isDone = m.StartsWith("\u2705") || m.Contains("\u5b8c\u6210") || m.Contains("Completed");
        if (t.IsThinking || t.HasProgress || !isDone) return 2;
        return 1;
    }

    internal void OnAgentDynamicUpdated(string agentName, bool isHighPriority = false)
    {
        var active = GetActiveTasks();
        if (active.Count <= 1) return;

        // If pinned by user or mouse is currently hovering over HUD, keep current display locked
        if (!string.IsNullOrEmpty(pinnedAgent) || isHovered) return;

        // If user manually switched recently, don't interrupt unless high priority alert
        if (Environment.TickCount < manualLockUntilTick && !isHighPriority) return;

        for (int i = 0; i < active.Count; i++)
        {
            if (string.Equals(active[i].AgentName, agentName, StringComparison.OrdinalIgnoreCase))
            {
                if (currentAgentIndex != i)
                {
                    currentAgentIndex = i;
                    lastDisplayedTask = active[i];
                    lastRotationTick = Environment.TickCount;
                    RenderLayeredWindow();
                }
                break;
            }
        }
    }

    internal AgentTaskStatusHud(Settings settings)
    {
        this.settings = settings;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        Size = new Size(CurrentWidth, CardHeight);
        IntPtr forceHandle = Handle;

        MouseDown += OnHudMouseDown;
        MouseMove += OnHudMouseMove;
        MouseUp += OnHudMouseUp;
        MouseWheel += OnHudMouseWheel;
        LocationChanged += delegate { RenderLayeredWindow(); };
        SizeChanged += delegate { RenderLayeredWindow(); };

        heartbeatTimer = new Timer();
        heartbeatTimer.Interval = 60;
        heartbeatTimer.Tick += OnHeartbeatTick;
        heartbeatTimer.Start();
        InstallForegroundHook();
    }

    private void InstallForegroundHook()
    {
        if (AppContext.IsSelfCheckRunning || foregroundHook != IntPtr.Zero) return;
        foregroundHookProc = OnForegroundWinEvent;
        uint flags = NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS;
        try
        {
            foregroundHook = NativeMethods.SetWinEventHook(
                NativeMethods.EVENT_SYSTEM_FOREGROUND, NativeMethods.EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero, foregroundHookProc, 0, 0, flags);
            minimizeHook = NativeMethods.SetWinEventHook(
                NativeMethods.EVENT_SYSTEM_MINIMIZESTART, NativeMethods.EVENT_SYSTEM_MINIMIZEEND,
                IntPtr.Zero, foregroundHookProc, 0, 0, flags);
        }
        catch { foregroundHook = IntPtr.Zero; minimizeHook = IntPtr.Zero; }
    }

    private void OnForegroundWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (!IsHandleCreated || IsDisposed) return;
        try
        {
            if (eventType == NativeMethods.EVENT_SYSTEM_MINIMIZESTART)
            {
                // The window the user just minimized. Focus has often already moved,
                // so do not require it to still be GetForegroundWindow.
                IntPtr root = AppContext.RootWindow(hwnd);
                if (root == IntPtr.Zero) root = hwnd;
                bool agent = AppContext.WindowBelongsToAgent(root, null) || AppContext.WindowBelongsToAgent(hwnd, null);
                if (agent && (AppContext.HasUserWindowSize(root) || AppContext.HasUserWindowSize(hwnd)))
                {
                    AppContext.LeavingDesktopHwnd = root;
                    AppContext.LeavingDesktopTick = Environment.TickCount;
                }
            }
            else if (eventType != NativeMethods.EVENT_SYSTEM_MINIMIZEEND)
            {
                // Only the restored agent window itself clears the mark.
                // Another window coming forward must not.
                IntPtr leave = AppContext.LeavingDesktopHwnd;
                if (leave != IntPtr.Zero)
                {
                    IntPtr fgRoot = AppContext.RootWindow(hwnd);
                    IntPtr leaveRoot = AppContext.RootWindow(leave);
                    if (leaveRoot == IntPtr.Zero) leaveRoot = leave;
                    bool same = hwnd == leave || hwnd == leaveRoot || fgRoot == leave || fgRoot == leaveRoot;
                    int age = unchecked(Environment.TickCount - AppContext.LeavingDesktopTick);
                    if (same && age > 400 && AppContext.IsWindowRestored(leaveRoot))
                        AppContext.LeavingDesktopHwnd = IntPtr.Zero;
                }
            }
            if (InvokeRequired) BeginInvoke(new Action(SyncVisibilityToForeground));
            else SyncVisibilityToForeground();
        }
        catch { }
    }

    // Foreground loss shows the bar on this call. The heartbeat only backstops a missed event.
    internal void SyncVisibilityToForeground()
    {
        if (moving || resizingWidth) return;
        if (settings == null || !settings.AgentStatusPinEnabled) return;
        if (AppContext.Current != null) AppContext.Current.EnsureRunningStatusOnHud();
        if (tasks == null || tasks.Count == 0) return;
        lastFgCheckTime = DateTime.Now;
        bool shouldShow = ShouldBeVisible();
        if (shouldShow)
        {
            if (fadeStartTick > 0)
            {
                fadeStartTick = 0;
                if (currentAlpha != 255) { currentAlpha = 255; RenderLayeredWindow(); }
            }
            if (!Visible)
            {
                if (AppContext.Current != null) AppContext.Current.UpdateTaskStatusHudPosition();
                currentAlpha = 255;
                Show();
                RenderLayeredWindow();
            }
        }
        else if (Visible && fadeStartTick == 0)
        {
            fadeStartTick = Environment.TickCount;
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (minimizeHook != IntPtr.Zero)
        {
            try { NativeMethods.UnhookWinEvent(minimizeHook); } catch { }
            minimizeHook = IntPtr.Zero;
        }
        if (foregroundHook != IntPtr.Zero)
        {
            try { NativeMethods.UnhookWinEvent(foregroundHook); } catch { }
            foregroundHook = IntPtr.Zero;
            foregroundHookProc = null;
        }
        if (heartbeatTimer != null) { heartbeatTimer.Stop(); heartbeatTimer.Dispose(); heartbeatTimer = null; }
        if (renderBitmap != null) { renderBitmap.Dispose(); renderBitmap = null; }
        CleanupCachedGdiObjects();
        base.OnFormClosed(e);
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (!Visible) { fadeStartTick = 0; currentAlpha = 255; }
        else RenderLayeredWindow();
    }

    internal bool IsHudPinned
    {
        get { return !string.IsNullOrEmpty(pinnedAgent); }
    }

    internal void ReleaseStatusPin()
    {
        pinnedAgent = null;
    }

    internal bool ShouldBeVisible()
    {
        if (settings == null || !settings.AgentStatusPinEnabled) return false;
        // Pinned HUD stays on the desktop until the user unpins or closes it.
        if (!string.IsNullOrEmpty(pinnedAgent)) return true;
        var active = GetActiveTasks();
        if (active.Count == 0) return false;
        if (isHovered || moving || resizingWidth) return true;
        for (int i = 0; i < active.Count; i++)
        {
            AgentTaskStatus t = active[i];
            if (!AppContext.IsAgentWindowInForeground(t.AgentName))
            {
                return true;
            }
            // A minimized window can still be reported as the foreground window.
            // Recent activity must keep the bar up anyway.
            if (HasRecentDynamic(t) && AppContext.IsAgentMinimized(t.AgentName))
            {
                return true;
            }
        }
        return false;
    }

    private void OnHeartbeatTick(object sender, EventArgs e)
    {
        if (!IsHandleCreated || IsDisposed) return;

        Point cursorPos = Cursor.Position;
        bool inBounds = Visible && Bounds.Contains(cursorPos);
        DateTime now = DateTime.Now;
        bool hoverChanged = false;

        if (inBounds)
        {
            lastHoverTime = now;
            if (!isHovered)
            {
                isHovered = true;
                hoverChanged = true;
                RenderLayeredWindow();
            }
        }
        else
        {
            // Smooth 160ms collapse debounce so passing cursor or quick mouse micro-exits don't flicker
            if (isHovered && (now - lastHoverTime).TotalMilliseconds > 160 && !moving && !resizingWidth)
            {
                isHovered = false;
                lastHoverPillIndex = -1;
                hoverColorDot = false;
                hoverChanged = true;
                RenderLayeredWindow();
            }
        }

        var active = GetActiveTasks();
        // Pin is a user-owned desktop-resident switch. Never drop it because a
        // session expired, completed, or was auto-cleared; only the pin icon
        // or the close button may release it.

        // Fixed-cadence rotation when idle: if not pinned, not hovered and not manually locked, advance every 5s
        if (active.Count > 1 && string.IsNullOrEmpty(pinnedAgent) && !isHovered && Environment.TickCount > manualLockUntilTick)
        {
            if (Environment.TickCount - lastRotationTick >= AutoRotationCadenceMs)
            {
                currentAgentIndex = (currentAgentIndex + 1) % active.Count;
                lastDisplayedTask = active[currentAgentIndex];
                lastRotationTick = Environment.TickCount;
                RenderLayeredWindow();
            }
        }

        if (Visible && fadeStartTick > 0)
        {
            int elapsed = Environment.TickCount - fadeStartTick;
            if (isHovered || moving || resizingWidth || ShouldBeVisible())
            {
                fadeStartTick = 0;
                if (currentAlpha != 255) { currentAlpha = 255; RenderLayeredWindow(); }
            }
            else if (elapsed >= 5000)
            {
                fadeStartTick = 0;
                currentAlpha = 255;
                Visible = false;
            }
            else
            {
                byte targetAlpha = elapsed <= 3500 ? (byte)255 : (byte)Math.Max(0, (5000 - elapsed) * 255 / 1500);
                if (currentAlpha != targetAlpha)
                {
                    currentAlpha = targetAlpha;
                    RenderLayeredWindow();
                }
            }
        }

        // Foreground changes show the bar from the win-event hook. This poll only
        // backstops a missed event, and still times the completed-task acknowledge.
        int fgWait = Visible ? 250 : 60;
        if (!moving && !resizingWidth && tasks != null && tasks.Count > 0 && (now - lastFgCheckTime).TotalMilliseconds >= fgWait)
        {
            SyncVisibilityToForeground();

            // Auto-acknowledge completed tasks after user focuses the agent window for >= 5s.
            // A pinned bar is resident: do not silently clear/hide it.
            if (string.IsNullOrEmpty(pinnedAgent))
            {
                for (int i = tasks.Count - 1; i >= 0; i--)
                {
                    AgentTaskStatus t = tasks[i];
                    bool done = !string.IsNullOrEmpty(t.Message) && (t.Message.StartsWith("✅") || t.Message.Contains("完成") || t.Message.Contains("Completed"));
                    if (done && (!t.HasProgress || string.IsNullOrEmpty(t.StepProgress)))
                    {
                        if (AppContext.IsAgentWindowInForeground(t.AgentName))
                        {
                            t.ForegroundSeenSeconds += 0.25;
                            if (t.ForegroundSeenSeconds >= 5.0 && AppContext.Current != null)
                            {
                                AppContext.Current.ClearAgentProgress(t.AgentName);
                            }
                        }
                        else
                        {
                            t.ForegroundSeenSeconds = 0;
                        }
                    }
                }
            }
        }

        bool detailAdvanced = false;
        if (tasks != null)
        {
            for (int i = 0; i < tasks.Count; i++)
            {
                if (tasks[i] != null && tasks[i].AdvanceDetailQueue(Visible)) detailAdvanced = true;
            }
        }

        // Animation self-driven: running comet ring & waiting pulse run continuously
        bool hasActiveAnimation = moving || resizingWidth || isHovered || hoverChanged || detailAdvanced;
        if (!hasActiveAnimation && active.Count > 0)
        {
            for (int i = 0; i < active.Count; i++)
            {
                AgentTaskStatus t = active[i];
                bool done = !string.IsNullOrEmpty(t.Message) && (t.Message.StartsWith("✅") || t.Message.Contains("完成") || t.Message.Contains("Completed"));
                if (t.HasProgress && !string.IsNullOrEmpty(t.StepProgress)) done = false;
                if (!done) { hasActiveAnimation = true; break; }
            }
        }

        if (Visible && (hasActiveAnimation || hoverChanged))
        {
            RenderLayeredWindow();
        }
    }

    private void UpdateLayoutAndSize()
    {
        var active = GetActiveTasks();
        if (active.Count == 0) return;
        if (currentAgentIndex >= active.Count) currentAgentIndex = Math.Max(0, active.Count - 1);

        int totalH = CardHeight + (active.Count > 1 ? (int)Math.Round(CardHeight * 0.7) : 0);

        if (Height != totalH || Width != CurrentWidth)
        {
            Screen screen = Screen.FromPoint(Location);
            int newY = Location.Y;
            if (newY + totalH > screen.WorkingArea.Bottom)
            {
                newY = screen.WorkingArea.Bottom - totalH;
            }
            Location = new Point(Location.X, newY);
            Size = new Size(CurrentWidth, totalH);
        }
        RenderLayeredWindow();
    }

    private void OnHudMouseWheel(object sender, MouseEventArgs e)
    {
        bool ctrlAlt = (Control.ModifierKeys & Keys.Control) != 0 && (Control.ModifierKeys & Keys.Alt) != 0;
        if (ctrlAlt)
        {
            float delta = e.Delta > 0 ? 0.5f : -0.5f;
            float next = Math.Max(7.0f, Math.Min(16.0f, settings.TaskStatusFontSize + delta));
            if (Math.Abs(next - settings.TaskStatusFontSize) < 0.01f) return;
            settings.TaskStatusFontSize = next;
            settings.Save();
            UpdateLayoutAndSize();
            return;
        }

        var active = GetActiveTasks();
        if (active.Count > 1)
        {
            int step = e.Delta > 0 ? -1 : 1;
            currentAgentIndex = (currentAgentIndex + step + active.Count) % active.Count;
            lastDisplayedTask = active[currentAgentIndex];
            lastRotationTick = Environment.TickCount;
            manualLockUntilTick = Environment.TickCount + 5000;
            RenderLayeredWindow();
        }
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x80 | 0x08000000 | 0x00080000; // WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_LAYERED
            return cp;
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e) { }
    protected override void OnPaint(PaintEventArgs e) { }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x84) // WM_NCHITTEST
        {
            m.Result = (IntPtr)1; // HTCLIENT: direct interaction without Alt
            return;
        }
        base.WndProc(ref m);
    }

    internal static string GetPillText(AgentTaskStatus t)
    {
        if (t == null) return "AI";
        Color c; string l;
        GetAgentBadge(t.AgentName, out c, out l);
        return l ?? "AI";
    }

    // Multi-session disambiguation: when the same agent has >= 2 live sessions,
    // append a stable 1-based index derived from SessionKey lexicographic order
    // so "Claude #1" always refers to the same session across repaints.
    private static string GetPillTextForTask(AgentTaskStatus t, List<AgentTaskStatus> active)
    {
        if (t == null) return "AI";
        Color c; string baseLabel;
        GetAgentBadge(t.AgentName, out c, out baseLabel);
        if (string.IsNullOrEmpty(baseLabel)) baseLabel = "AI";
        if (active == null) return baseLabel;

        string targetName = BaseAgentName(t);
        // Collect every live session of this agent (with or without a
        // SessionKey) so "Codex" + "Codex" both get numbered instead of one
        // silently collapsing into the other.
        List<AgentTaskStatus> siblings = new List<AgentTaskStatus>();
        for (int i = 0; i < active.Count; i++)
        {
            var s = active[i];
            if (s == null) continue;
            if (!string.Equals(BaseAgentName(s), targetName, StringComparison.OrdinalIgnoreCase)) continue;
            siblings.Add(s);
        }
        if (siblings.Count < 2) return baseLabel;

        siblings.Sort(CompareTasksForDisplay);
        for (int i = 0; i < siblings.Count; i++)
        {
            if (object.ReferenceEquals(siblings[i], t)) return baseLabel + " #" + (i + 1);
        }
        return baseLabel;
    }

    private float GetTaskPillWidth(AgentTaskStatus t)
    {
        var active = GetActiveTasks();
        string label = GetPillTextForTask(t, active);
        // Scale the glyph-width estimate with the height-derived pill font
        // (10.5px per char at the 10pt / 28px default).
        float scale = CardHeight / 28f;
        return Math.Max(52f * scale, Math.Min(132f * scale, (label != null ? label.Length : 2) * 10.5f * scale + 22f * scale));
    }

    private static float GetPillWidth(string name)
    {
        Color c; string label;
        GetAgentBadge(name, out c, out label);
        return Math.Max(48f, (label != null ? label.Length : 2) * 10f + 20f);
    }

    private int GetIndicatorDotIndex(Point pt, int cardH)
    {
        if (pt.Y < cardH - 3) return -1;
        var active = GetActiveTasks();
        int count = active.Count;
        if (count <= 1) return -1;
        float totalW = 0f;
        for (int i = 0; i < count; i++) totalW += GetTaskPillWidth(active[i]);
        totalW += (count - 1) * 8f;
        float curX = (Width - totalW) / 2f;
        for (int i = 0; i < count; i++)
        {
            float pillW = GetTaskPillWidth(active[i]);
            // 外扩点击与悬停热区：左右各 4px，上下扩充至整个药丸及外围
            int pillZoneH = Math.Max(24, (int)Math.Round(cardH * 0.8));
            if (pt.X >= curX - 4 && pt.X <= curX + pillW + 4 && pt.Y >= cardH - 3 && pt.Y <= cardH + pillZoneH) return i;
            curX += pillW + 8f;
        }
        return -1;
    }

    private void OnHudMouseDown(object sender, MouseEventArgs e)
    {
        var active = GetActiveTasks();
        if (active.Count == 0) return;
        if (currentAgentIndex >= active.Count) currentAgentIndex = 0;
        int cardH = CardHeight;

        // Bottom indicator click
        int dotIdx = GetIndicatorDotIndex(e.Location, cardH);
        if (dotIdx >= 0)
        {
            currentAgentIndex = dotIdx;
            lastDisplayedTask = active[dotIdx];
            // Clicking a pill manually switches the protagonist but does NOT
            // move the pin; the pin is only set/cleared by the pin icon.
            lastRotationTick = Environment.TickCount;
            manualLockUntilTick = Environment.TickCount + 5000;
            RenderLayeredWindow();
            return;
        }

        if (e.Button != MouseButtons.Left) return;

        // Text-color swatch, left of the pin. Only the dot opens the picker.
        if (isHovered && ColorSlotRect(Width, cardH).Contains(e.Location))
        {
            ChooseStatusTextColor();
            return;
        }

        // Pin button [📌] (Left of status icon)
        Rectangle pinRect = HoverSlotRect(Width, cardH, 45);
        if (isHovered && pinRect.Contains(e.Location))
        {
            pinnedAgent = string.IsNullOrEmpty(pinnedAgent) ? active[currentAgentIndex].AgentName : null;
            if (!string.IsNullOrEmpty(pinnedAgent))
            {
                fadeStartTick = 0;
                currentAlpha = 255;
            }
            RenderLayeredWindow();
            return;
        }

        // Top-right close button [✕] — same as turning off agent status sensing.
        Rectangle cornerClose = HoverSlotRect(Width, cardH, 27);
        if (cornerClose.Contains(e.Location))
        {
            if (AppContext.Current != null) AppContext.Current.TurnOffAgentStatusPin(null);
            return;
        }

        // Bottom edge of the card row (excluding the 8px right-resize corner)
        // resizes card height; font scales proportionally via CardHeight.
        int cardH2 = CardHeight;
        if (e.Y >= cardH2 - 5 && e.Y < cardH2 && e.X < Width - 8)
        {
            resizingHeight = true;
            dragStart = Cursor.Position;
            dragStartHeight = CardHeight;
            return;
        }

        // Right edge (last 8px) resizes width; elsewhere starts move / click
        if (e.X >= Width - 8)
        {
            resizingWidth = true;
            dragStart = Cursor.Position;
            dragStartWidth = Width;
            return;
        }
        moving = true;
        dragStart = Cursor.Position;
        formStart = Location;
    }

    private void OnHudMouseMove(object sender, MouseEventArgs e)
    {
        if (!moving && !resizingWidth)
        {
            int cardH = CardHeight;
            // Bottom edge resize (excluding the right-resize corner)
            if (e.Y >= cardH - 5 && e.Y < cardH && e.X < Width - 8)
            {
                if (Cursor.Current != Cursors.SizeNS) Cursor.Current = Cursors.SizeNS;
            }
            else if (e.X >= Width - 8 && e.Y < cardH)
            {
                if (Cursor.Current != Cursors.SizeWE) Cursor.Current = Cursors.SizeWE;
            }
            else
            {
                bool showPin = isHovered;
                int curHoverPill = GetIndicatorDotIndex(e.Location, cardH);
                bool onColor = showPin && ColorSlotRect(Width, cardH).Contains(e.Location);
                bool hand = HoverSlotRect(Width, cardH, 27).Contains(e.Location) || (showPin && HoverSlotRect(Width, cardH, 45).Contains(e.Location)) || onColor || curHoverPill >= 0;
                Cursor.Current = hand ? Cursors.Hand : Cursors.Default;
                if (onColor != hoverColorDot)
                {
                    hoverColorDot = onColor;
                    RenderLayeredWindow();
                }
                if (curHoverPill != lastHoverPillIndex)
                {
                    lastHoverPillIndex = curHoverPill;
                    RenderLayeredWindow();
                }
            }
        }

        if (resizingWidth)
        {
            int newWidth = Math.Max(MinHudWidth, Math.Min(MaxHudWidth, dragStartWidth + Cursor.Position.X - dragStart.X));
            if (newWidth != Width)
            {
                Width = newWidth;
                RenderLayeredWindow();
            }
            return;
        }

        if (resizingHeight)
        {
            int newHeight = Math.Max(CollapsedHeight, Math.Min(MaxCardHeight, dragStartHeight + Cursor.Position.Y - dragStart.Y));
            if (newHeight != CardHeight)
            {
                settings.TaskStatusHeight = newHeight;
                UpdateLayoutAndSize();
            }
            return;
        }

        if (!moving) return;

        int newX = formStart.X + Cursor.Position.X - dragStart.X;
        int newY = formStart.Y + Cursor.Position.Y - dragStart.Y;

        Rectangle screen = Screen.FromPoint(Cursor.Position).WorkingArea;
        int centerX = screen.Left + screen.Width / 2;
        int windowCenterX = newX + Width / 2;
        const int snapThreshold = 18;
        bool snapH = Math.Abs(windowCenterX - centerX) <= snapThreshold;
        if (snapH) newX = centerX - Width / 2;
        Location = new Point(newX, newY);

        if (snapH)
        {
            if (snapGuideForm == null || !snapGuideForm.Visible) ShowSnapGuide();
            if (snapGuideForm != null)
            {
                snapGuideV = true;
                snapGuideForm.Invalidate();
            }
        }
        else
        {
            HideSnapGuide();
        }
    }

    private void OnHudMouseUp(object sender, MouseEventArgs e)
    {
        if (resizingWidth)
        {
            resizingWidth = false;
            settings.TaskStatusWidth = Width;
            settings.Save();
            return;
        }
        if (resizingHeight)
        {
            resizingHeight = false;
            settings.TaskStatusHeight = CardHeight;
            settings.Save();
            return;
        }
        if (!moving) return;
        moving = false;
        HideSnapGuide();

        // Left-click without drag (< 4px): direct Warp/Focus to agent window + auto-dismiss if already completed!
        if (Math.Abs(Cursor.Position.X - dragStart.X) < 4 && Math.Abs(Cursor.Position.Y - dragStart.Y) < 4)
        {
            var active = GetActiveTasks();
            int cardH = CardHeight;
            if (e.Y < cardH && active.Count > 0)
            {
                if (currentAgentIndex >= active.Count) currentAgentIndex = 0;
                AgentTaskStatus clicked = active[currentAgentIndex];
                if (AppContext.Current != null)
                {
                    AppContext.Current.WarpToAgentWindow(clicked.AgentName);
                    bool done = !string.IsNullOrEmpty(clicked.Message) && (clicked.Message.StartsWith("✅") || clicked.Message.Contains("完成") || clicked.Message.Contains("Completed"));
                    if (done && (!clicked.HasProgress || string.IsNullOrEmpty(clicked.StepProgress)))
                    {
                        AppContext.Current.ClearAgentProgress(clicked.AgentName);
                    }
                    if (!ShouldBeVisible() && fadeStartTick == 0) fadeStartTick = Environment.TickCount;
                }
            }
            return;
        }
        settings.TaskStatusX = Location.X;
        settings.TaskStatusY = Location.Y;
        settings.Save();
    }

    internal void SetTasks(IList<AgentTaskStatus> values)
    {
        tasks = values != null ? new List<AgentTaskStatus>(values) : new List<AgentTaskStatus>();
        UpdateLayoutAndSize();
    }

    private void RenderLayeredWindow()
    {
        if (!IsHandleCreated || IsDisposed || Width <= 0 || Height <= 0) return;
        if (renderBitmap == null || renderBitmap.Width != Width || renderBitmap.Height != Height)
        {
            if (renderBitmap != null) { renderBitmap.Dispose(); renderBitmap = null; }
            renderBitmap = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
        }
        using (Graphics g = Graphics.FromImage(renderBitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.FromArgb(0, 0, 0, 0));
            DrawTasks(g);
        }

        AppContext.PremultiplyBitmap(renderBitmap, ref premulBuffer);
        AppContext.UpdateLayered(Left, Top, Width, Height, Handle, renderBitmap, currentAlpha);
    }

    private void DrawTasks(Graphics g)
    {
        var active = GetActiveTasks();
        if (active.Count == 0) return;
        if (currentAgentIndex >= active.Count) currentAgentIndex = 0;

        // Font scales linearly with card height: 9.5pt at the 28px default,
        // clamped to a sane 7pt..22pt band so the 60px max still fits.
        float baseFs = settings != null && settings.TaskStatusFontSize > 0 ? settings.TaskStatusFontSize : 9.5f;
        float fs = Math.Max(7.0f, Math.Min(22.0f, baseFs * CardHeight / 28f));
        int unitH = CardHeight;
        Point clientCursor = PointToClient(Cursor.Position);

        EnsureCachedGdiObjects(fs);
        Font badgeFont = cachedBadgeFont;
        Font titleFont = cachedTitleFont;
        SolidBrush titleBrush = cachedTitleBrush;
        Pen borderPen = isHovered ? cachedBorderHover : cachedBorderNormal;
        StringFormat titleFormat = cachedTitleFormat;
        StringFormat centerFormat = cachedCenterFormat;

        double pulse = (DateTime.Now.Ticks / 10000000.0) * Math.PI * 1.5;

        // Render single active card (Fixed 28px single row)
        AgentTaskStatus task = active[currentAgentIndex];
        int cardY = 0;
        Rectangle cardRect = new Rectangle(0, cardY, Width - 1, unitH - 1);

        // 1. Fluent Acrylic Pill Background
        using (GraphicsPath path = AppContext.GetRoundedRectPath(cardRect, 14))
        using (SolidBrush bgBrush = new SolidBrush(ColorBg))
        {
            g.FillPath(bgBrush, path);
            g.DrawPath(borderPen, path);
        }

        bool isZh = settings == null || settings.Language != "en";
        bool isMulti = active.Count > 1;

        float ringCenterX = Width - 18f;
        float ringCenterY = cardY + CardHeight / 2f;
        float ringRadius = 6.0f;

        Color statusColor = GetTaskStatusColor(task);
        bool isCompleted = statusColor == ColorDone;
        bool isError = statusColor == ColorError;
        bool isWaiting = statusColor == ColorWaiting || isError;
        bool isRunning = statusColor == ColorRunning;

        // The pills under the bar already name each agent. The main line
        // keeps a name chip only when there is no pill row.
        float textX = 10f;
        if (!isMulti)
        {
            Color badgeColor; string badgeLabel;
            GetAgentBadge(task.AgentName, out badgeColor, out badgeLabel);
            int chipH = Math.Max(16, (int)Math.Round(CardHeight * 0.64));
            textX = DrawChip(g, 8f, cardY + (CardHeight - chipH) / 2f, badgeLabel, badgeFont, badgeColor, centerFormat, chipH);
        }

        // 4. Main Line Content: Three-State Dispatch (Live Action default, Title on Hover & Done)
        string targetGoal = ExtractGoal(task.Message);
        string mainText;

        if (isCompleted)
        {
            mainText = !string.IsNullOrEmpty(targetGoal) ? targetGoal : task.SubDetail;
            if (!string.IsNullOrEmpty(mainText) && !mainText.StartsWith("✅")) mainText = "✅ " + mainText;
        }
        else if (isHovered)
        {
            mainText = !string.IsNullOrEmpty(targetGoal) ? ("🎯 " + targetGoal) : task.SubDetail;
        }
        else
        {
            mainText = !string.IsNullOrEmpty(task.SubDetail) ? task.SubDetail : targetGoal;
            if (isWaiting && !string.IsNullOrEmpty(mainText) && !mainText.StartsWith("❓")) mainText = "❓ " + mainText;
            else if (isError && !string.IsNullOrEmpty(mainText) && !mainText.StartsWith("⚠️")) mainText = "⚠️ " + mainText;
        }

        string timeBadge = "";
        if (isRunning && task.TurnStartTime > DateTime.MinValue)
        {
            int elapsedSec = Math.Max(0, (int)(DateTime.Now - task.TurnStartTime).TotalSeconds);
            if (elapsedSec >= 1) timeBadge = FormatElapsed(elapsedSec);
        }

        // Pin icon only appears on hover, even when pinned (the pin state is
        // already conveyed by the HUD staying locked to this agent).
        bool showPin = isHovered;
        bool showTime = !string.IsNullOrEmpty(timeBadge);

        // Measure the fixed time badge so the main text never overlaps it.
        float timeW = 0f;
        if (showTime)
        {
            SizeF ts = g.MeasureString(timeBadge, badgeFont);
            timeW = ts.Width + 4f;
        }

        float rightLimit = ringCenterX - 10f;
        if (showPin) rightLimit -= 36f;
        if (showTime) rightLimit -= timeW;

        string textToDraw = !string.IsNullOrEmpty(mainText) ? mainText : "";
        float textW = Math.Max(40f, rightLimit - textX);
        if (!isCompleted && !isHovered && IsPhaseStatusLine(textToDraw))
        {
            DrawPhaseLine(g, textToDraw, titleFont, titleBrush, titleFormat, textX, cardY, textW, CardHeight);
        }
        else
        {
            RectangleF titleRect = new RectangleF(textX, cardY, textW, CardHeight);
            g.DrawString(textToDraw, titleFont, titleBrush, titleRect, titleFormat);
        }

        // 4b. Fixed Elapsed Time Badge (just left of the status ring, distinct color)
        if (showTime)
        {
            float timeRight = ringCenterX - 10f - (showPin ? 36f : 0f);
            RectangleF timeRect = new RectangleF(timeRight - timeW, cardY, timeW, CardHeight);
            using (SolidBrush timeBrush = new SolidBrush(Color.FromArgb(180, 148, 163, 184))) // slate-400, muted but distinct from main text
            using (StringFormat rightFmt = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
            {
                g.DrawString(timeBadge, badgeFont, timeBrush, timeRect, rightFmt);
            }
        }

        if (showPin)
        {
            Rectangle colorSlot = ColorSlotRect(Width, CardHeight);
            Rectangle swatch = ColorSwatchRect(colorSlot);
            bool hoverDot = colorSlot.Contains(clientCursor);
            if (hoverDot)
            {
                using (SolidBrush dotBg = new SolidBrush(Color.FromArgb(45, 255, 255, 255)))
                    g.FillEllipse(dotBg, colorSlot);
            }
            Color textColor = StatusTextColor();
            using (SolidBrush dotFill = new SolidBrush(textColor))
                g.FillEllipse(dotFill, swatch);
            using (Pen dotRing = new Pen(Color.FromArgb(160, 255, 255, 255), 1f))
                g.DrawEllipse(dotRing, swatch);
        }

        // 4a. Pin Icon Slot (Directly left of status ring: ringCenterX - 20f)
        if (showPin)
        {
            bool isPinned = !string.IsNullOrEmpty(pinnedAgent);
            Rectangle pinRect = new Rectangle((int)ringCenterX - 27, (int)ringCenterY - 9, 18, 18);
            bool hoverPin = isHovered && pinRect.Contains(clientCursor);

            if (hoverPin)
            {
                using (SolidBrush pinBg = new SolidBrush(Color.FromArgb(45, 255, 255, 255)))
                {
                    g.FillEllipse(pinBg, pinRect);
                }
            }

            Color pinColor = isPinned ? Color.FromArgb(56, 189, 248) : (hoverPin ? Color.White : Color.FromArgb(160, 203, 213, 225));
            float px = pinRect.X + pinRect.Width / 2f;
            float py = pinRect.Y + pinRect.Height / 2f;

            using (Pen pinPen = new Pen(pinColor, 1.4f))
            {
                pinPen.StartCap = LineCap.Round;
                pinPen.EndCap = LineCap.Round;
                g.DrawLine(pinPen, px - 3.5f, py - 4f, px + 3.5f, py - 4f);
                g.DrawLine(pinPen, px - 2f, py, px + 2f, py);
                g.DrawLine(pinPen, px, py - 4f, px, py + 4.5f);
            }
        }

        // 4. Right-Side Slot: Targeted In-place Morphing (Status Indicator vs Close Button [✕])
        Rectangle cornerCloseRect = new Rectangle((int)ringCenterX - 9, (int)ringCenterY - 9, 18, 18);
        bool hoverCorner = cornerCloseRect.Contains(clientCursor);

        if (hoverCorner)
        {
            // In-place Morphing: When mouse directly points at right icon area, seamlessly morph into sleek [✕]
            using (SolidBrush cornerBg = new SolidBrush(Color.FromArgb(65, 255, 255, 255)))
            using (SolidBrush cornerFg = new SolidBrush(Color.White))
            {
                g.FillEllipse(cornerBg, cornerCloseRect);
                g.DrawString("✕", badgeFont, cornerFg, cornerCloseRect, centerFormat);
            }
        }
        else
        {
            if (isRunning)
            {
                // 1. Background circular track
                using (Pen trackPen = new Pen(Color.FromArgb(40, statusColor), 1.0f))
                {
                    g.DrawEllipse(trackPen, ringCenterX - ringRadius, ringCenterY - ringRadius, ringRadius * 2f, ringRadius * 2f);
                }

                // 2. Center soft glowing core
                float corePulse = (float)(0.4 + 0.6 * ((Math.Sin(pulse * 1.5) + 1.0) / 2.0));
                using (SolidBrush coreGlow = new SolidBrush(Color.FromArgb((int)(75 * corePulse), statusColor)))
                {
                    g.FillEllipse(coreGlow, ringCenterX - 2f, ringCenterY - 2f, 4f, 4f);
                }

                // 3. Orbiting Comet arc
                double t = (DateTime.Now.Ticks / 10000000.0);
                float headAngle = (float)((t * 280.0) % 360.0);
                using (Pen tailPen = new Pen(Color.FromArgb(180, statusColor), 1.6f))
                {
                    g.DrawArc(tailPen, ringCenterX - ringRadius, ringCenterY - ringRadius, ringRadius * 2f, ringRadius * 2f, headAngle - 80f, 80f);
                }

                // 4. Comet Head (High-contrast bright white/cyan core)
                float headRad = (float)(headAngle * Math.PI / 180.0);
                float headX = ringCenterX + ringRadius * (float)Math.Cos(headRad);
                float headY = ringCenterY + ringRadius * (float)Math.Sin(headRad);

                using (SolidBrush headGlow = new SolidBrush(Color.FromArgb(150, statusColor)))
                {
                    g.FillEllipse(headGlow, headX - 2.5f, headY - 2.5f, 5f, 5f);
                }
                using (SolidBrush headCore = new SolidBrush(Color.White))
                {
                    g.FillEllipse(headCore, headX - 1.4f, headY - 1.4f, 2.8f, 2.8f);
                }
            }
            else if (isWaiting)
            {
                float blinkWave = (float)(0.25 + 0.75 * ((Math.Sin(pulse * 2.0) + 1.0) / 2.0));
                using (Pen amberPen = new Pen(Color.FromArgb((int)(200 * blinkWave), statusColor), 1.4f))
                {
                    g.DrawEllipse(amberPen, ringCenterX - ringRadius, ringCenterY - ringRadius, ringRadius * 2f, ringRadius * 2f);
                }
                using (SolidBrush amberCore = new SolidBrush(Color.FromArgb((int)(240 * blinkWave), statusColor)))
                {
                    g.FillEllipse(amberCore, ringCenterX - 2f, ringCenterY - 2f, 4f, 4f);
                }
            }
            else // Completed: Obvious vector ✅ completion badge with emerald disc and crisp white checkmark
            {
                // 1. Soft emerald ambient aura
                using (SolidBrush glow = new SolidBrush(Color.FromArgb(45, statusColor)))
                {
                    g.FillEllipse(glow, ringCenterX - 8f, ringCenterY - 8f, 16f, 16f);
                }

                // 2. Solid vibrant emerald disc
                using (SolidBrush disc = new SolidBrush(statusColor))
                {
                    g.FillEllipse(disc, ringCenterX - 6.5f, ringCenterY - 6.5f, 13f, 13f);
                }

                // 3. Crisp white checkmark ✓
                using (Pen checkPen = new Pen(Color.White, 1.8f))
                {
                    checkPen.StartCap = LineCap.Round;
                    checkPen.EndCap = LineCap.Round;
                    checkPen.LineJoin = LineJoin.Round;
                    g.DrawLine(checkPen, ringCenterX - 3.2f, ringCenterY - 0.1f, ringCenterX - 0.9f, ringCenterY + 2.4f);
                    g.DrawLine(checkPen, ringCenterX - 0.9f, ringCenterY + 2.4f, ringCenterX + 3.3f, ringCenterY - 2.3f);
                }
            }
        }

        // 5. Centered Multi-Agent Status Pills Below Card (Multi-Project disambiguation by root dir)
        if (isMulti)
        {
            float totalW = 0f;
            for (int i = 0; i < active.Count; i++) totalW += GetTaskPillWidth(active[i]);
            totalW += (active.Count - 1) * 8f;

            float curX = (Width - totalW) / 2f;
            // Pill row sits directly under the card; scale its offset and
            // height proportionally so a 60px card still has room for pills.
            int pillY = CardHeight + 1;
            int pillH = Math.Max(14, (int)Math.Round(CardHeight * 0.6));

            for (int i = 0; i < active.Count; i++)
            {
                AgentTaskStatus t = active[i];
                bool isCur = (i == currentAgentIndex);
                Color bColor; string bLabel;
                GetAgentBadge(t.AgentName, out bColor, out bLabel);
                string pillLabel = GetPillTextForTask(t, active);
                float pillW = GetTaskPillWidth(t);
                Rectangle pillRect = new Rectangle((int)curX, pillY, (int)pillW, pillH);

                Color dotColor = GetTaskStatusColor(t);
                bool isErr = dotColor == ColorError;
                bool isWait = dotColor == ColorWaiting || isErr;

                bool isPillHover = (lastHoverPillIndex == i);

                using (GraphicsPath pPath = AppContext.GetRoundedRectPath(pillRect, 8))
                {
                    // 彻底根治 DWM Alpha=0 穿透：非当前胶囊必须填充微透明底色 (Alpha >= 18)，Hover 时明显微光
                    Color pillBgColor = isCur
                        ? Color.FromArgb(48, bColor)
                        : (isPillHover ? Color.FromArgb(32, bColor) : Color.FromArgb(18, 255, 255, 255));
                    using (SolidBrush pBg = new SolidBrush(pillBgColor))
                    {
                        g.FillPath(pBg, pPath);
                    }
                    if (isCur || isPillHover)
                    {
                        using (Pen pBorder = new Pen(Color.FromArgb(isCur ? 60 : 35, bColor), 1f))
                        {
                            g.DrawPath(pBorder, pPath);
                        }
                    }
                }

                using (SolidBrush textBrush = new SolidBrush((isCur || isPillHover) ? Color.White : Color.FromArgb(190, 203, 213, 225)))
                {
                    RectangleF labelRect = new RectangleF(pillRect.X + 6f, pillRect.Y, pillRect.Width - 18f, pillRect.Height);
                    g.DrawString(pillLabel, cachedPillFont, textBrush, labelRect, cachedCenterFormat);

                    float dotX = pillRect.Right - 8f;
                    float dotY = pillRect.Y + 8.5f;
                    if (isWait || isErr)
                    {
                        float pulseAlpha = (float)(0.3 + 0.7 * ((Math.Sin(pulse * 2.5) + 1.0) / 2.0));
                        using (SolidBrush glow = new SolidBrush(Color.FromArgb((int)(130 * pulseAlpha), dotColor)))
                        {
                            g.FillEllipse(glow, dotX - 4.5f, dotY - 4.5f, 9f, 9f);
                        }
                    }
                    using (SolidBrush dotBrush = new SolidBrush(dotColor))
                    {
                        g.FillEllipse(dotBrush, dotX - 2.5f, dotY - 2.5f, 5.0f, 5.0f);
                    }
                }

                curX += pillW + 8f;
            }
        }
    }

    internal void UpdatePosition()
    {
        Screen screen = Screen.FromPoint(Location.X > -9000 ? Location : Cursor.Position);
        Rectangle work = screen.WorkingArea;
        int w = CurrentWidth;
        int x = (settings != null && settings.TaskStatusX != -99999) ? settings.TaskStatusX : work.Left + (work.Width - w) / 2;
        int y = (settings != null && settings.TaskStatusY != -99999) ? settings.TaskStatusY : work.Top + 12;
        Location = new Point(Math.Max(work.Left, Math.Min(work.Right - w, x)), Math.Max(work.Top, Math.Min(work.Bottom - Height, y)));
    }
    private static readonly int[] AgentHues = new int[] { 0x38BDF8, 0x818CF8, 0xA855F7, 0xEC4899, 0xF43F5E, 0xFB923C, 0xFBBF24, 0x34D399, 0x2DD4BF };
    private static string FormatElapsed(int sec) { return sec >= 60 ? (sec / 60 + "m" + (sec % 60).ToString("D2") + "s") : (sec + "s"); }



    private static float DrawChip(Graphics g, float x, float y, string text, Font font, Color color, StringFormat sf, int chipHeight = 18)
    {
        float w = Math.Max(28f, g.MeasureString(text, font).Width + 10f);
        Rectangle r = new Rectangle((int)x, (int)y, (int)w, chipHeight);
        using (GraphicsPath path = AppContext.GetRoundedRectPath(r, 4))
        using (SolidBrush bg = new SolidBrush(Color.FromArgb(40, color)))
        using (Pen border = new Pen(Color.FromArgb(120, color), 1f))
        using (SolidBrush fg = new SolidBrush(color))
        {
            g.FillPath(bg, path);
            g.DrawPath(border, path);
            g.DrawString(text, font, fg, r, sf);
        }
        return r.Right + 8f;
    }

    private static Color GetTaskStatusColor(AgentTaskStatus t)
    {
        string m = t.Message ?? "";
        if (t.HasProgress && !string.IsNullOrEmpty(t.StepProgress)) return ColorRunning;
        if (m.StartsWith("✅") || m.Contains("完成") || m.Contains("Completed")) return ColorDone;
        if (m.StartsWith("⚠️") || m.Contains("异常") || m.IndexOf("Error", StringComparison.OrdinalIgnoreCase) >= 0) return ColorError;
        if (m.StartsWith("❓") || m.Contains("等待") || m.Contains("Waiting") || m.Contains("决策")) return ColorWaiting;
        return ColorRunning;
    }

    private static string ExtractGoal(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return "";
        int s = msg.IndexOf('['), e = msg.IndexOf(']');
        string g = (s >= 0 && e > s) ? msg.Substring(s + 1, e - s - 1) : Regex.Replace(msg, @"^[⏳✅❓⚠️]\s*", "");
        g = g.Replace("任务进行中", "").Replace("Task in progress", "").Replace("处理中...", "").Replace("Processing...", "").Replace("处理中", "").Trim(' ', '·', '-');
        return (g == "当前会话" || g == "当前任务" || g == "Session" || g == "Task") ? "" : g;
    }

    private static void GetAgentBadge(string rawName, out Color color, out string label)
    {
        string n = rawName ?? "";
        int atIdx = n.IndexOf('@');
        string baseAgent = atIdx > 0 ? n.Substring(0, atIdx).Trim() : n;
        string ln = baseAgent.ToLowerInvariant();
        int rgb = 0; label = null;
        if (ln.Contains("claude")) { rgb = 0xDA7756; label = "Claude"; }
        else if (ln.Contains("antigravity") || ln.Contains("agy") || ln.Contains("gemini")) { rgb = 0x3B82F6; label = "AGY"; }
        else if (ln.Contains("codex")) { rgb = 0x0EA5E9; label = "Codex"; }
        else if (ln.Contains("cursor")) { rgb = 0x10B981; label = "Cursor"; }
        else if (ln.Contains("windsurf")) { rgb = 0x06B6D4; label = "Windsurf"; }
        else if (ln.Contains("trae")) { rgb = 0x8B5CF6; label = "Trae"; }
        else if (ln.Contains("aider")) { rgb = 0xF97316; label = "Aider"; }
        else if (ln.Contains("copilot")) { rgb = 0x6366F1; label = "Copilot"; }
        else if (ln.Contains("zed")) { rgb = 0x084CCF; label = "Zed"; }
        else if (ln.Contains("augment")) { rgb = 0xA855F7; label = "Augment"; }
        else if (ln.Contains("junie")) { rgb = 0xFE2857; label = "Junie"; }
        else if (ln.Contains("openhands")) { rgb = 0xF59E0B; label = "OpenHands"; }
        else if (ln.Contains("replit")) { rgb = 0xF26207; label = "Replit"; }
        else if (ln.Contains("phind")) { rgb = 0x7C3AED; label = "Phind"; }
        else if (ln.Contains("blackbox")) { rgb = 0x111827; label = "Blackbox"; }
        else if (ln.Contains("sweep")) { rgb = 0x0EA5E9; label = "Sweep"; }
        else if (ln.Contains("mentat")) { rgb = 0x84CC16; label = "Mentat"; }
        else if (ln.Contains("gpt engineer") || ln.Contains("gptengineer") || ln.Contains("gpt-engineer")) { rgb = 0x10A37F; label = "GPTE"; }
        else if (ln.Contains("smol")) { rgb = 0xEC4899; label = "smol"; }
        else if (ln == "v0" || ln.StartsWith("v0 ")) { rgb = 0x000000; label = "v0"; }
        else if (ln.Contains("bolt")) { rgb = 0xFACC15; label = "Bolt"; }
        else if (ln.Contains("lovable")) { rgb = 0xFB7185; label = "Lovable"; }
        else if (ln.Contains("qoder")) { rgb = 0x06B6D4; label = "Qoder"; }
        else if (ln.Contains("kiro")) { rgb = 0x8B5CF6; label = "Kiro"; }
        else if (ln.Contains("deepseek")) { rgb = 0x4D6BFE; label = "DeepSeek"; }
        if (label != null) { color = Color.FromArgb(rgb | unchecked((int)0xFF000000)); return; }
        color = GetDeterministicAgentColor(baseAgent);
        label = baseAgent.Length <= 6 ? (baseAgent.Length == 0 ? "AI" : baseAgent) : baseAgent.Substring(0, 5) + "…";
    }

    private static bool IsPhaseStatusLine(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        return PhaseLabel(text, "正在思考") || PhaseLabel(text, "计划下一步")
            || PhaseLabel(text, "Thinking") || PhaseLabel(text, "Planning next moves");
    }

    private static bool PhaseLabel(string text, string label)
    {
        if (string.Equals(text, label, StringComparison.Ordinal)) return true;
        return text.StartsWith(label + " \u00b7 ", StringComparison.Ordinal);
    }

    // One, two, then three dots, holding each step for 400ms. Paint only.
    private static int PhaseDotCount()
    {
        return (int)(unchecked((uint)Environment.TickCount) / 400u) % 3 + 1;
    }

    private static void DrawPhaseLine(Graphics g, string text, Font font, Brush brush, StringFormat format, float x, float y, float width, int height)
    {
        int n = PhaseDotCount();
        float step = Math.Max(8f, font.GetHeight(g) * 0.42f);
        float gap = 6f;
        float slot = step * 3f;
        float inkW = g.MeasureString(text, font, new SizeF(10000f, height), format).Width;
        bool overflow = inkW + gap + slot > width;
        float wordsW = overflow ? Math.Max(12f, width - gap - slot) : inkW + 2f;
        g.DrawString(text, font, brush, new RectangleF(x, y, wordsW, height), format);
        float dotsX = x + (overflow ? wordsW : inkW) + gap;
        using (StringFormat dotFmt = new StringFormat
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.None,
            FormatFlags = StringFormatFlags.NoWrap
        })
        {
            for (int i = 0; i < n; i++)
            {
                g.DrawString(".", font, brush, new RectangleF(dotsX + i * step, y, step, height), dotFmt);
            }
        }
    }

    internal static Rectangle HoverSlotRect(int width, int cardH, int fromRight)
    {
        int cy = Math.Max(0, cardH) / 2;
        return new Rectangle(width - fromRight, cy - 9, 18, 18);
    }

    internal static Rectangle ColorSlotRect(int width, int cardH)
    {
        return HoverSlotRect(width, cardH, 63);
    }

    internal static Rectangle ColorSwatchRect(Rectangle slot)
    {
        return new Rectangle(slot.X + 4, slot.Y + 4, 10, 10);
    }

    internal Color StatusTextColor()
    {
        if (settings != null && !string.IsNullOrEmpty(settings.TaskStatusTextColor))
        {
            try { return ColorTranslator.FromHtml(settings.TaskStatusTextColor); }
            catch { }
        }
        return ColorTitleText;
    }

    private void ChooseStatusTextColor()
    {
        if (settings == null) return;
        using (ColorDialog cd = new ColorDialog())
        {
            cd.Color = StatusTextColor();
            cd.AllowFullOpen = true;
            cd.FullOpen = true;
            cd.AnyColor = true;
            if (cd.ShowDialog(this) == DialogResult.OK)
            {
                settings.TaskStatusTextColor = ColorTranslator.ToHtml(cd.Color);
                settings.Save();
                RenderLayeredWindow();
            }
        }
    }

    internal static Color GetDeterministicAgentColor(string name)
    {
        if (string.IsNullOrEmpty(name)) return AppContext.Win11Accent;
        return Color.FromArgb(AgentHues[Math.Abs(name.GetHashCode()) % AgentHues.Length] | unchecked((int)0xFF000000));
    }
    private void EnsureCachedGdiObjects(float fs)
    {
        // Badge & pill fonts derive from the (height-scaled) title size so every
        // glyph in the HUD grows together. At the 28px default these land on
        // 9.0pt / 10.0pt, matching the original look.
        float badgeSize = Math.Max(7.0f, Math.Min(22.0f, fs * 0.95f));
        if (cachedBadgeFont == null || Math.Abs(cachedBadgeSize - badgeSize) > 0.01f)
        {
            if (cachedBadgeFont != null) cachedBadgeFont.Dispose();
            cachedBadgeFont = new Font("Microsoft YaHei UI", badgeSize, FontStyle.Regular, GraphicsUnit.Point);
            cachedBadgeSize = badgeSize;
        }
        float pillSize = Math.Max(7.5f, Math.Min(23.0f, fs * 1.05f));
        if (cachedPillFont == null || Math.Abs(cachedPillSize - pillSize) > 0.01f)
        {
            if (cachedPillFont != null) cachedPillFont.Dispose();
            cachedPillFont = new Font("Microsoft YaHei UI", pillSize, FontStyle.Regular, GraphicsUnit.Point);
            cachedPillSize = pillSize;
        }
        if (cachedTitleFont == null || Math.Abs(cachedTitleFontSize - fs) > 0.01f)
        {
            if (cachedTitleFont != null) cachedTitleFont.Dispose();
            cachedTitleFont = new Font("Microsoft YaHei UI", fs, FontStyle.Regular, GraphicsUnit.Point);
            cachedTitleFontSize = fs;
        }
        Color titleColor = StatusTextColor();
        if (cachedTitleBrush == null) cachedTitleBrush = new SolidBrush(titleColor);
        else if (cachedTitleBrush.Color.ToArgb() != titleColor.ToArgb()) cachedTitleBrush.Color = titleColor;
        if (cachedBorderNormal == null) cachedBorderNormal = new Pen(ColorBorderNormal, 1.0f);
        if (cachedBorderHover == null) cachedBorderHover = new Pen(ColorBorderHover, 1.0f);
        if (cachedTitleFormat == null) cachedTitleFormat = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        if (cachedCenterFormat == null) cachedCenterFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
    }

    private void ShowSnapGuide()
    {
        if (snapGuideForm == null) snapGuideForm = AppContext.CreateSnapGuide(OnSnapGuidePaint);
        Rectangle scr = Screen.FromPoint(Cursor.Position).WorkingArea;
        snapGuideForm.Bounds = scr;
        snapGuideForm.Show();
    }

    private void HideSnapGuide()
    {
        if (snapGuideForm != null && snapGuideForm.Visible) snapGuideForm.Hide();
    }

    private void OnSnapGuidePaint(object sender, PaintEventArgs e)
    {
        AppContext.DrawSnapGuideLines((Form)sender, e, snapGuideV, false);
    }

    private void CleanupCachedGdiObjects()
    {
        HideSnapGuide();
        if (snapGuideForm != null) { try { snapGuideForm.Dispose(); } catch { } snapGuideForm = null; }
        IDisposable[] d = new IDisposable[] { cachedBadgeFont, cachedPillFont, cachedTitleFont, cachedTitleBrush, cachedBorderNormal, cachedBorderHover, cachedTitleFormat, cachedCenterFormat };
        for (int i = 0; i < d.Length; i++) if (d[i] != null) try { d[i].Dispose(); } catch { }
    }
}
