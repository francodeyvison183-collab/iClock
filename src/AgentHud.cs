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
    internal bool HasProgress;
    internal float Progress;
    internal string Message;
    internal bool IsThinking;
    internal string SubDetail;
    internal string StepProgress;
    internal string ProjectHint;
    internal DateTime TurnStartTime;
    internal DateTime StepStartTime;
    internal double ForegroundSeenSeconds;
    internal DateTime MessageTimestamp;
}

internal sealed class AgentTaskStatusHud : Form
{
    private const int MinHudWidth = 200;
    private const int MaxHudWidth = 1400;
    private const int CollapsedHeight = 28;
    private const int ExpandedHeight = 48;

    // Standardized typography & state color palette (Option A)
    private static readonly Color ColorBg = Color.FromArgb(235, 18, 20, 24);              // Deep acrylic glass #121418
    private static readonly Color ColorBorderNormal = Color.FromArgb(45, 255, 255, 255);      // 18% frosted border
    private static readonly Color ColorBorderHover = Color.FromArgb(90, 255, 255, 255);       // 35% hover border
    private static readonly Color ColorTitleText = Color.FromArgb(248, 250, 252);          // #F8FAFC crisp white
    private static readonly Color ColorSuffixText = Color.FromArgb(148, 163, 184);         // #94A3B8 soft slate
    private static readonly Color ColorSubDetail = Color.FromArgb(203, 213, 225);          // #CBD5E1 ice gray
    private static readonly Color ColorRunning = Color.FromArgb(56, 189, 248);             // #38BDF8 Cyan Blue
    private static readonly Color ColorWaiting = Color.FromArgb(245, 158, 11);             // #F59E0B Amber
    private static readonly Color ColorError = Color.FromArgb(248, 113, 113);              // #F87171 Coral Red
    private static readonly Color ColorDone = Color.FromArgb(34, 197, 94);                 // #22C55E Emerald Green

    private readonly Settings settings;
    private List<AgentTaskStatus> tasks = new List<AgentTaskStatus>();
    private bool moving;
    private bool resizingWidth;
    private Point dragStart;
    private Point formStart;
    private int dragStartWidth;
    private Bitmap renderBitmap;
    private byte[] premulBuffer;
    private Font cachedBadgeFont;
    private Font cachedTitleFont;
    private float cachedTitleFontSize = -1f;
    private Font cachedSubFont;
    private SolidBrush cachedTitleBrush;
    private SolidBrush cachedSubBrush;
    private Pen cachedBorderNormal;
    private Pen cachedBorderHover;
    private StringFormat cachedTitleFormat;
    private StringFormat cachedCenterFormat;

    private Timer heartbeatTimer;
    private bool isHovered;
    private int currentAgentIndex = 0;
    private int lastRotationTick = Environment.TickCount;
    private int manualLockUntilTick = 0;
    private const int AutoRotationCadenceMs = 5000;

    private DateTime lastHoverTime;
    private DateTime lastFgCheckTime = DateTime.MinValue;
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
        for (int i = 0; i < tasks.Count; i++)
        {
            AgentTaskStatus t = tasks[i];
            if (t.MessageTimestamp > DateTime.MinValue && (now - t.MessageTimestamp).TotalMinutes >= 5.0) continue;
            active.Add(t);
        }
        return active;
    }

    internal void OnAgentDynamicUpdated(string agentName, bool isHighPriority = false)
    {
        var active = GetActiveTasks();
        if (active.Count <= 1) return;

        // If mouse is currently hovering over HUD, keep current display locked unchanged
        if (isHovered) return;

        // If user manually switched recently, don't interrupt unless high priority alert
        if (Environment.TickCount < manualLockUntilTick && !isHighPriority) return;

        for (int i = 0; i < active.Count; i++)
        {
            if (string.Equals(active[i].AgentName, agentName, StringComparison.OrdinalIgnoreCase))
            {
                if (currentAgentIndex != i)
                {
                    currentAgentIndex = i;
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
        Size = new Size(CurrentWidth, CollapsedHeight);
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
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        if (heartbeatTimer != null) { heartbeatTimer.Stop(); heartbeatTimer.Dispose(); heartbeatTimer = null; }
        if (renderBitmap != null) { renderBitmap.Dispose(); renderBitmap = null; }
        CleanupCachedGdiObjects();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (!Visible) { fadeStartTick = 0; currentAlpha = 255; }
        else RenderLayeredWindow();
    }

    internal bool ShouldBeVisible()
    {
        if (settings == null || !settings.DesktopCollaborationEnabled) return false;
        var active = GetActiveTasks();
        if (active.Count == 0) return false;
        if (isHovered || moving || resizingWidth) return true;
        for (int i = 0; i < active.Count; i++)
        {
            AgentTaskStatus t = active[i];
            if (!AppContext.IsAgentWindowInForeground(t.AgentName, t.ProjectHint))
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
                UpdateLayoutAndSize();
            }
        }
        else
        {
            // Smooth 160ms collapse debounce so passing cursor or quick mouse micro-exits don't flicker
            if (isHovered && (now - lastHoverTime).TotalMilliseconds > 160 && !moving && !resizingWidth)
            {
                isHovered = false;
                hoverChanged = true;
                UpdateLayoutAndSize();
            }
        }

        // Fixed-cadence rotation when idle: if not hovered and not manually locked, advance every 5s
        var active = GetActiveTasks();
        if (active.Count > 1 && !isHovered && Environment.TickCount > manualLockUntilTick)
        {
            if (Environment.TickCount - lastRotationTick >= AutoRotationCadenceMs)
            {
                currentAgentIndex = (currentAgentIndex + 1) % active.Count;
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

        // Environmental awareness & auto-show/hide: only show status bar when agent is in background
        if (!moving && !resizingWidth && tasks != null && tasks.Count > 0 && (now - lastFgCheckTime).TotalMilliseconds >= 250)
        {
            lastFgCheckTime = now;
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
                    Visible = true;
                }
            }
            else
            {
                if (Visible && fadeStartTick == 0)
                {
                    fadeStartTick = Environment.TickCount;
                }
            }

            // Auto-acknowledge completed tasks after user focuses the agent window for >= 5s
            for (int i = tasks.Count - 1; i >= 0; i--)
            {
                AgentTaskStatus t = tasks[i];
                bool done = !string.IsNullOrEmpty(t.Message) && (t.Message.StartsWith("✅") || t.Message.Contains("完成") || t.Message.Contains("Completed"));
                if (done && (!t.HasProgress || string.IsNullOrEmpty(t.StepProgress)))
                {
                    if (AppContext.IsAgentWindowInForeground(t.AgentName, t.ProjectHint))
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

        // Animation self-driven: running comet ring & waiting pulse run continuously
        bool hasActiveAnimation = moving || resizingWidth || isHovered || hoverChanged;
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

        int cardH = isHovered ? ExpandedHeight : CollapsedHeight;
        int totalH = cardH + (active.Count > 1 ? 14 : 0);

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

    private static float GetPillWidth(string name)
    {
        Color c; string label;
        GetAgentBadge(name, out c, out label);
        return Math.Max(42f, (label != null ? label.Length : 2) * 8.5f + 18f);
    }

    private int GetIndicatorDotIndex(Point pt, int cardH)
    {
        if (pt.Y < cardH) return -1;
        var active = GetActiveTasks();
        int count = active.Count;
        if (count <= 1) return -1;
        float totalW = 0f;
        for (int i = 0; i < count; i++) totalW += GetPillWidth(active[i].AgentName);
        totalW += (count - 1) * 6f;
        float curX = (Width - totalW) / 2f;
        for (int i = 0; i < count; i++)
        {
            float pillW = GetPillWidth(active[i].AgentName);
            if (pt.X >= curX - 2 && pt.X <= curX + pillW + 2 && pt.Y >= cardH && pt.Y <= cardH + 14) return i;
            curX += pillW + 6f;
        }
        return -1;
    }

    private void OnHudMouseDown(object sender, MouseEventArgs e)
    {
        var active = GetActiveTasks();
        if (active.Count == 0) return;
        if (currentAgentIndex >= active.Count) currentAgentIndex = 0;
        int cardH = isHovered ? ExpandedHeight : CollapsedHeight;

        // Bottom indicator click
        int dotIdx = GetIndicatorDotIndex(e.Location, cardH);
        if (dotIdx >= 0)
        {
            currentAgentIndex = dotIdx;
            lastRotationTick = Environment.TickCount;
            manualLockUntilTick = Environment.TickCount + 5000;
            RenderLayeredWindow();
            return;
        }

        // Right-click: quickly dismiss/clear current agent progress
        if (e.Button == MouseButtons.Right)
        {
            string agent = active[currentAgentIndex].AgentName;
            if (AppContext.Current != null)
            {
                AppContext.Current.ClearAgentProgress(agent);
            }
            return;
        }

        if (e.Button != MouseButtons.Left) return;

        // Top-right close button [✕]
        Rectangle cornerClose = new Rectangle(Width - 27, 5, 18, 18);
        if (cornerClose.Contains(e.Location))
        {
            if (AppContext.Current != null)
            {
                AppContext.Current.ClearAgentProgress(active[currentAgentIndex].AgentName);
            }
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
            int cardH = isHovered ? ExpandedHeight : CollapsedHeight;
            if (e.X >= Width - 8 && e.Y < cardH)
            {
                if (Cursor.Current != Cursors.SizeWE) Cursor.Current = Cursors.SizeWE;
            }
            else
            {
                bool hand = new Rectangle(Width - 27, 5, 18, 18).Contains(e.Location) || GetIndicatorDotIndex(e.Location, cardH) >= 0;
                Cursor.Current = hand ? Cursors.Hand : Cursors.Default;
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

        if (!moving) return;

        int newX = formStart.X + Cursor.Position.X - dragStart.X;
        int newY = formStart.Y + Cursor.Position.Y - dragStart.Y;

        Rectangle screen = Screen.FromPoint(Cursor.Position).WorkingArea;
        int centerX = screen.Left + screen.Width / 2;
        int windowCenterX = newX + Width / 2;
        const int snapThreshold = 16;
        if (Math.Abs(windowCenterX - centerX) <= snapThreshold) newX = centerX - Width / 2;
        Location = new Point(newX, newY);
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
        if (!moving) return;
        moving = false;

        // Left-click without drag (< 4px): direct Warp/Focus to agent window + auto-dismiss if already completed!
        if (Math.Abs(Cursor.Position.X - dragStart.X) < 4 && Math.Abs(Cursor.Position.Y - dragStart.Y) < 4)
        {
            var active = GetActiveTasks();
            int cardH = isHovered ? ExpandedHeight : CollapsedHeight;
            if (e.Y < cardH && active.Count > 0)
            {
                if (currentAgentIndex >= active.Count) currentAgentIndex = 0;
                AgentTaskStatus clicked = active[currentAgentIndex];
                if (AppContext.Current != null)
                {
                    AppContext.Current.WarpToAgentWindow(clicked.AgentName, clicked.ProjectHint);
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
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
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

        float fs = settings != null && settings.TaskStatusFontSize > 0 ? settings.TaskStatusFontSize : 9.5f;
        int unitH = isHovered ? ExpandedHeight : CollapsedHeight;
        Point clientCursor = PointToClient(Cursor.Position);

        EnsureCachedGdiObjects(fs);
        Font badgeFont = cachedBadgeFont;
        Font titleFont = cachedTitleFont;
        Font subFont = cachedSubFont;
        SolidBrush titleBrush = cachedTitleBrush;
        SolidBrush subBrush = cachedSubBrush;
        Pen borderPen = isHovered ? cachedBorderHover : cachedBorderNormal;
        StringFormat titleFormat = cachedTitleFormat;
        StringFormat centerFormat = cachedCenterFormat;

        double pulse = (DateTime.Now.Ticks / 10000000.0) * Math.PI * 1.5;

        // Render single active card
        AgentTaskStatus task = active[currentAgentIndex];
        int cardY = 0;
        Rectangle cardRect = new Rectangle(0, cardY, Width - 1, unitH - 1);

        // 1. Fluent Acrylic Pill Background
        using (GraphicsPath path = AppContext.GetRoundedRectPath(cardRect, isHovered ? 10 : 14))
        using (SolidBrush bgBrush = new SolidBrush(ColorBg))
        {
            g.FillPath(bgBrush, path);
            g.DrawPath(borderPen, path);
        }

        bool isZh = settings == null || settings.Language != "en";
        bool isMulti = active.Count > 1;

        float ringCenterX = Width - 18f;
        float ringCenterY = cardY + 14f;
        float ringRadius = 6.0f;

        bool isCompleted = !string.IsNullOrEmpty(task.Message) && (task.Message.Contains("完成") || task.Message.Contains("Completed") || task.Message.StartsWith("✅"));
        if (task.HasProgress && !string.IsNullOrEmpty(task.StepProgress))
        {
            isCompleted = false;
        }
        bool isError = !string.IsNullOrEmpty(task.Message) && (task.Message.StartsWith("⚠️") || task.Message.Contains("异常") || task.Message.Contains("死循环") || task.Message.IndexOf("Error", StringComparison.OrdinalIgnoreCase) >= 0 || task.Message.IndexOf("Runaway", StringComparison.OrdinalIgnoreCase) >= 0);
        bool isWaiting = isError || (!string.IsNullOrEmpty(task.Message) && (task.Message.Contains("等待") || task.Message.Contains("Waiting") || task.Message.Contains("决策") || task.Message.StartsWith("❓")));
        bool isRunning = (task.IsThinking || task.HasProgress || (!isCompleted && !isWaiting)) && !isError && !isWaiting;

        Color statusColor = isCompleted ? ColorDone : (isError ? ColorError : (isWaiting ? ColorWaiting : ColorRunning));

        // 2. Agent Brand Badge (Only in single-agent mode; multi-agent has branded pills below)
        float textX = 12f;
        if (!isMulti)
        {
            Color badgeColor;
            string badgeLabel;
            GetAgentBadge(task.AgentName, out badgeColor, out badgeLabel);

            SizeF badgeTextSize = g.MeasureString(badgeLabel, badgeFont);
            float badgeW = Math.Max(30f, badgeTextSize.Width + 10f);
            RectangleF badgeRect = new RectangleF(8f, cardY + 5f, badgeW, 18f);

            using (GraphicsPath badgePath = AppContext.GetRoundedRectPath(Rectangle.Round(badgeRect), 4))
            using (SolidBrush badgeBg = new SolidBrush(Color.FromArgb(45, badgeColor)))
            using (Pen badgeBorder = new Pen(Color.FromArgb(140, badgeColor), 1f))
            using (SolidBrush badgeTextBrush = new SolidBrush(badgeColor))
            {
                g.FillPath(badgeBg, badgePath);
                g.DrawPath(badgeBorder, badgePath);
                g.DrawString(badgeLabel, badgeFont, badgeTextBrush, badgeRect, centerFormat);
            }
            textX = badgeRect.Right + 8f;
        }

        // 3. Row 1: Live Dynamic Action + Project (Inverted hierarchy)
        string projPrefix = !string.IsNullOrEmpty(task.ProjectHint) ? ("[" + task.ProjectHint + "] ") : "";
        string liveAction = task.SubDetail;
        if (string.IsNullOrEmpty(liveAction))
        {
            if (!string.IsNullOrEmpty(task.StepProgress)) liveAction = "[" + task.StepProgress + "]";
            else if (task.IsThinking) liveAction = isZh ? "💭 思考分析中..." : "💭 Thinking...";
            else if (isCompleted) liveAction = isZh ? "✅ 任务已完成" : "✅ Task completed";
            else if (isError) liveAction = isZh ? "⚠️ 遇到异常或已中断" : "⚠️ Task interrupted";
            else if (isWaiting) liveAction = isZh ? "❓ 等待决策" : "❓ Waiting for decision";
            else
            {
                string gFallback = ExtractGoal(task.Message);
                liveAction = (!string.IsNullOrEmpty(gFallback) && gFallback != "当前会话" && gFallback != "当前任务") ? gFallback : (isZh ? "处理中..." : "Processing...");
            }
        }

        string timeBadge = "";
        if (isRunning && task.TurnStartTime > DateTime.MinValue)
        {
            int elapsedSec = Math.Max(0, (int)(DateTime.Now - task.TurnStartTime).TotalSeconds);
            if (elapsedSec >= 1) timeBadge = " (" + FormatElapsed(elapsedSec) + ")";
        }

        string textToDraw = projPrefix + liveAction + timeBadge;
        float textW = Math.Max(40f, (ringCenterX - 10f) - textX);
        RectangleF titleRect = new RectangleF(textX, cardY + 4f, textW, 20f);
        g.DrawString(textToDraw, titleFont, titleBrush, titleRect, titleFormat);

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

                // 3. Orbiting Comet with 4-segment high-energy drag tail
                double t = (DateTime.Now.Ticks / 10000000.0);
                float headAngle = (float)((t * 280.0) % 360.0);

                float sweepStart = headAngle - 88f;
                for (int s = 0; s < 4; s++)
                {
                    using (Pen tailPen = new Pen(Color.FromArgb(35 + s * 60, statusColor), 1.5f + s * 0.15f))
                    {
                        g.DrawArc(tailPen, ringCenterX - ringRadius, ringCenterY - ringRadius, ringRadius * 2f, ringRadius * 2f, sweepStart + s * 22f, 22f);
                    }
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

        // 5. Expanded Micro-Drawer Details (Row 2: Macro Goal)
        if (isHovered)
        {
            g.DrawLine(cachedBorderNormal, 10f, cardY + 26f, Width - 10f, cardY + 26f);

            string goal = ExtractGoal(task.Message);
            string goalText = (isZh ? "🎯 目标: " : "🎯 Goal: ") + (!string.IsNullOrEmpty(goal) ? goal : (isZh ? "当前会话" : "Session"));
            float subW = Math.Max(40f, Width - 10f - textX);
            RectangleF subRect = new RectangleF(textX, cardY + 27f, subW, 17f);
            g.DrawString(goalText, subFont, subBrush, subRect, titleFormat);
        }

        // 6. Centered Multi-Agent Status Pills Below Card (Only when active.Count > 1)
        if (isMulti)
        {
            float totalW = 0f;
            for (int i = 0; i < active.Count; i++) totalW += GetPillWidth(active[i].AgentName);
            totalW += (active.Count - 1) * 6f;

            float curX = (Width - totalW) / 2f;
            int pillY = unitH + 1;
            int pillH = 12;

            for (int i = 0; i < active.Count; i++)
            {
                AgentTaskStatus t = active[i];
                bool isCur = (i == currentAgentIndex);
                Color bColor; string bLabel;
                GetAgentBadge(t.AgentName, out bColor, out bLabel);
                float pillW = GetPillWidth(t.AgentName);
                Rectangle pillRect = new Rectangle((int)curX, pillY, (int)pillW, pillH);

                bool isErr = !string.IsNullOrEmpty(t.Message) && (t.Message.StartsWith("⚠️") || t.Message.Contains("异常") || t.Message.Contains("死循环"));
                bool isWait = isErr || (!string.IsNullOrEmpty(t.Message) && (t.Message.Contains("等待") || t.Message.Contains("Waiting") || t.Message.Contains("决策") || t.Message.StartsWith("❓")));
                bool isDone = !string.IsNullOrEmpty(t.Message) && (t.Message.StartsWith("✅") || t.Message.Contains("完成") || t.Message.Contains("Completed"));
                if (t.HasProgress && !string.IsNullOrEmpty(t.StepProgress)) isDone = false;
                Color dotColor = isDone ? ColorDone : (isErr ? ColorError : (isWait ? ColorWaiting : ColorRunning));

                using (GraphicsPath pPath = AppContext.GetRoundedRectPath(pillRect, 3))
                using (SolidBrush pBg = new SolidBrush(isCur ? Color.FromArgb(70, bColor) : Color.FromArgb(32, 255, 255, 255)))
                using (Pen pBorder = new Pen(isCur ? Color.FromArgb(200, Color.White) : Color.FromArgb(50, 255, 255, 255), 1.0f))
                using (SolidBrush textBrush = new SolidBrush(isCur ? Color.White : Color.FromArgb(170, 203, 213, 225)))
                {
                    g.FillPath(pBg, pPath);
                    g.DrawPath(pBorder, pPath);

                    RectangleF labelRect = new RectangleF(pillRect.X + 4f, pillRect.Y - 1f, pillRect.Width - 14f, pillRect.Height + 2f);
                    g.DrawString(bLabel, badgeFont, textBrush, labelRect, cachedCenterFormat);

                    float dotX = pillRect.Right - 6f;
                    float dotY = pillRect.Y + pillH / 2f;
                    if (isWait || isErr)
                    {
                        float pulseAlpha = (float)(0.3 + 0.7 * ((Math.Sin(pulse * 2.5) + 1.0) / 2.0));
                        using (SolidBrush glow = new SolidBrush(Color.FromArgb((int)(130 * pulseAlpha), dotColor)))
                        {
                            g.FillEllipse(glow, dotX - 4f, dotY - 4f, 8f, 8f);
                        }
                    }
                    using (SolidBrush dotBrush = new SolidBrush(dotColor))
                    {
                        g.FillEllipse(dotBrush, dotX - 2.2f, dotY - 2.2f, 4.4f, 4.4f);
                    }
                }

                curX += pillW + 6f;
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



    private static string ExtractGoal(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return "";
        int s = msg.IndexOf('['), e = msg.IndexOf(']');
        string g = (s >= 0 && e > s) ? msg.Substring(s + 1, e - s - 1) : Regex.Replace(msg, @"^[⏳✅❓⚠️]\s*", "");
        return g.Replace("任务进行中", "").Replace("Task in progress", "").Replace("处理中", "").Trim(' ', '·', '-');
    }

    private static void GetAgentBadge(string rawName, out Color color, out string label)
    {
        string n = rawName ?? "";
        string ln = n.ToLowerInvariant();
        int rgb = 0; label = null;
        if (ln.Contains("claude")) { rgb = 0xDA7756; label = "Claude"; }
        else if (ln.Contains("antigravity") || ln.Contains("agy") || ln.Contains("gemini")) { rgb = 0x3B82F6; label = "AGY"; }
        else if (ln.Contains("codex")) { rgb = 0x0EA5E9; label = "Codex"; }
        else if (ln.Contains("cursor")) { rgb = 0x10B981; label = "Cursor"; }
        else if (ln.Contains("windsurf")) { rgb = 0x06B6D4; label = "Windsurf"; }
        else if (ln.Contains("trae")) { rgb = 0x8B5CF6; label = "Trae"; }
        else if (ln.Contains("aider")) { rgb = 0xF97316; label = "Aider"; }
        else if (ln.Contains("copilot")) { rgb = 0x6366F1; label = "Copilot"; }
        if (label != null) { color = Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF); return; }
        color = GetDeterministicAgentColor(n);
        label = n.Length <= 6 ? (n.Length == 0 ? "AI" : n) : n.Substring(0, 5) + "…";
    }

    internal static Color GetDeterministicAgentColor(string name)
    {
        if (string.IsNullOrEmpty(name)) return AppContext.Win11Accent;
        int rgb = AgentHues[Math.Abs(name.GetHashCode()) % AgentHues.Length];
        return Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
    }
    private void EnsureCachedGdiObjects(float fs)
    {
        if (cachedBadgeFont == null) cachedBadgeFont = new Font("Microsoft YaHei UI", 7.5f, FontStyle.Bold, GraphicsUnit.Point);
        if (cachedTitleFont == null || Math.Abs(cachedTitleFontSize - fs) > 0.01f)
        {
            if (cachedTitleFont != null) cachedTitleFont.Dispose();
            cachedTitleFont = new Font("Microsoft YaHei UI", fs, FontStyle.Regular, GraphicsUnit.Point);
            cachedTitleFontSize = fs;
        }
        if (cachedSubFont == null) cachedSubFont = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point);
        if (cachedTitleBrush == null) cachedTitleBrush = new SolidBrush(ColorTitleText);
        if (cachedSubBrush == null) cachedSubBrush = new SolidBrush(ColorSubDetail);
        if (cachedBorderNormal == null) cachedBorderNormal = new Pen(ColorBorderNormal, 1.0f);
        if (cachedBorderHover == null) cachedBorderHover = new Pen(ColorBorderHover, 1.0f);
        if (cachedTitleFormat == null) cachedTitleFormat = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        if (cachedCenterFormat == null) cachedCenterFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
    }

    private void CleanupCachedGdiObjects()
    {
        IDisposable[] d = new IDisposable[] { cachedBadgeFont, cachedTitleFont, cachedSubFont, cachedTitleBrush, cachedSubBrush, cachedBorderNormal, cachedBorderHover, cachedTitleFormat, cachedCenterFormat };
        for (int i = 0; i < d.Length; i++) if (d[i] != null) try { d[i].Dispose(); } catch { }
    }
}