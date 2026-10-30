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
    private Form snapGuideForm;
    private bool snapGuideV;
    private string pinnedAgent;
    private Point dragStart;
    private Point formStart;
    private int dragStartWidth;
    private Bitmap renderBitmap;
    private byte[] premulBuffer;
    private Font cachedBadgeFont;
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
        if (!string.IsNullOrEmpty(pinnedAgent) || isHovered || moving || resizingWidth) return true;
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
                RenderLayeredWindow();
            }
        }
        else
        {
            // Smooth 160ms collapse debounce so passing cursor or quick mouse micro-exits don't flicker
            if (isHovered && (now - lastHoverTime).TotalMilliseconds > 160 && !moving && !resizingWidth)
            {
                isHovered = false;
                hoverChanged = true;
                RenderLayeredWindow();
            }
        }

        var active = GetActiveTasks();
        if (!string.IsNullOrEmpty(pinnedAgent))
        {
            if (active.Count > 0 && currentAgentIndex >= active.Count)
            {
                currentAgentIndex = Math.Max(0, active.Count - 1);
                pinnedAgent = active[currentAgentIndex].AgentName;
                RenderLayeredWindow();
            }
        }

        // Fixed-cadence rotation when idle: if not pinned, not hovered and not manually locked, advance every 5s
        if (active.Count > 1 && string.IsNullOrEmpty(pinnedAgent) && !isHovered && Environment.TickCount > manualLockUntilTick)
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

        int totalH = CollapsedHeight + (active.Count > 1 ? 20 : 0);

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
        return Math.Max(48f, (label != null ? label.Length : 2) * 10f + 20f);
    }

    private int GetIndicatorDotIndex(Point pt, int cardH)
    {
        if (pt.Y < cardH) return -1;
        var active = GetActiveTasks();
        int count = active.Count;
        if (count <= 1) return -1;
        float totalW = 0f;
        for (int i = 0; i < count; i++) totalW += GetPillWidth(active[i].AgentName);
        totalW += (count - 1) * 8f;
        float curX = (Width - totalW) / 2f;
        for (int i = 0; i < count; i++)
        {
            float pillW = GetPillWidth(active[i].AgentName);
            if (pt.X >= curX - 2 && pt.X <= curX + pillW + 2 && pt.Y >= cardH && pt.Y <= cardH + 20) return i;
            curX += pillW + 8f;
        }
        return -1;
    }

    private void OnHudMouseDown(object sender, MouseEventArgs e)
    {
        var active = GetActiveTasks();
        if (active.Count == 0) return;
        if (currentAgentIndex >= active.Count) currentAgentIndex = 0;
        int cardH = CollapsedHeight;

        // Bottom indicator click
        int dotIdx = GetIndicatorDotIndex(e.Location, cardH);
        if (dotIdx >= 0)
        {
            currentAgentIndex = dotIdx;
            if (!string.IsNullOrEmpty(pinnedAgent)) pinnedAgent = active[dotIdx].AgentName;
            lastRotationTick = Environment.TickCount;
            manualLockUntilTick = Environment.TickCount + 60000;
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

        // Pin button [📌] (Left of status icon)
        Rectangle pinRect = new Rectangle(Width - 45, 5, 18, 18);
        if (isHovered && pinRect.Contains(e.Location))
        {
            pinnedAgent = string.IsNullOrEmpty(pinnedAgent) ? active[currentAgentIndex].AgentName : null;
            RenderLayeredWindow();
            return;
        }

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
            int cardH = CollapsedHeight;
            if (e.X >= Width - 8 && e.Y < cardH)
            {
                if (Cursor.Current != Cursors.SizeWE) Cursor.Current = Cursors.SizeWE;
            }
            else
            {
                bool showPin = isHovered;
                bool hand = new Rectangle(Width - 27, 5, 18, 18).Contains(e.Location) || (showPin && new Rectangle(Width - 45, 5, 18, 18).Contains(e.Location)) || GetIndicatorDotIndex(e.Location, cardH) >= 0;
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
        if (!moving) return;
        moving = false;
        HideSnapGuide();

        // Left-click without drag (< 4px): direct Warp/Focus to agent window + auto-dismiss if already completed!
        if (Math.Abs(Cursor.Position.X - dragStart.X) < 4 && Math.Abs(Cursor.Position.Y - dragStart.Y) < 4)
        {
            var active = GetActiveTasks();
            int cardH = CollapsedHeight;
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

        float fs = settings != null && settings.TaskStatusFontSize > 0 ? settings.TaskStatusFontSize : 9.5f;
        int unitH = CollapsedHeight;
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
        float ringCenterY = cardY + 14f;
        float ringRadius = 6.0f;

        Color statusColor = GetTaskStatusColor(task);
        bool isCompleted = statusColor == ColorDone;
        bool isError = statusColor == ColorError;
        bool isWaiting = statusColor == ColorWaiting || isError;
        bool isRunning = statusColor == ColorRunning;

        // 2. Agent Brand Badge & Project Hint Chip
        float textX = 8f;
        Color badgeColor; string badgeLabel;
        GetAgentBadge(task.AgentName, out badgeColor, out badgeLabel);
        textX = DrawChip(g, textX, cardY + 5f, badgeLabel, badgeFont, badgeColor, centerFormat);

        if (!isMulti && !string.IsNullOrEmpty(task.ProjectHint))
        {
            textX = DrawChip(g, textX, cardY + 5f, task.ProjectHint, badgeFont, Color.FromArgb(56, 189, 248), centerFormat);
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
            if (elapsedSec >= 1) timeBadge = " (" + FormatElapsed(elapsedSec) + ")";
        }

        string textToDraw = (!string.IsNullOrEmpty(mainText) ? mainText : "") + timeBadge;
        bool showPin = isHovered;
        float rightLimit = showPin ? (ringCenterX - 28f) : (ringCenterX - 10f);
        float textW = Math.Max(40f, rightLimit - textX);
        RectangleF titleRect = new RectangleF(textX, cardY + 4f, textW, 20f);
        g.DrawString(textToDraw, titleFont, titleBrush, titleRect, titleFormat);

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

        // 5. Centered Multi-Agent Status Pills Below Card (17px height, 9pt font, 8px radius Stadium Pill; current agent only gets capsule background)
        if (isMulti)
        {
            float totalW = 0f;
            for (int i = 0; i < active.Count; i++) totalW += GetPillWidth(active[i].AgentName);
            totalW += (active.Count - 1) * 8f;

            float curX = (Width - totalW) / 2f;
            int pillY = 29;
            int pillH = 17;

            for (int i = 0; i < active.Count; i++)
            {
                AgentTaskStatus t = active[i];
                bool isCur = (i == currentAgentIndex);
                Color bColor; string bLabel;
                GetAgentBadge(t.AgentName, out bColor, out bLabel);
                float pillW = GetPillWidth(t.AgentName);
                Rectangle pillRect = new Rectangle((int)curX, pillY, (int)pillW, pillH);

                Color dotColor = GetTaskStatusColor(t);
                bool isErr = dotColor == ColorError;
                bool isWait = dotColor == ColorWaiting || isErr;

                if (isCur)
                {
                    using (GraphicsPath pPath = AppContext.GetRoundedRectPath(pillRect, 8))
                    using (SolidBrush pBg = new SolidBrush(Color.FromArgb(36, bColor)))
                    {
                        g.FillPath(pBg, pPath);
                    }
                }

                using (SolidBrush textBrush = new SolidBrush(isCur ? Color.White : Color.FromArgb(190, 203, 213, 225)))
                {
                    RectangleF labelRect = new RectangleF(pillRect.X + 6f, pillRect.Y, pillRect.Width - 18f, pillRect.Height);
                    g.DrawString(bLabel, badgeFont, textBrush, labelRect, cachedCenterFormat);

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



    private static float DrawChip(Graphics g, float x, float y, string text, Font font, Color color, StringFormat sf)
    {
        float w = Math.Max(28f, g.MeasureString(text, font).Width + 10f);
        Rectangle r = new Rectangle((int)x, (int)y, (int)w, 18);
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
        if (label != null) { color = Color.FromArgb(rgb | unchecked((int)0xFF000000)); return; }
        color = GetDeterministicAgentColor(n);
        label = n.Length <= 6 ? (n.Length == 0 ? "AI" : n) : n.Substring(0, 5) + "…";
    }

    internal static Color GetDeterministicAgentColor(string name)
    {
        if (string.IsNullOrEmpty(name)) return AppContext.Win11Accent;
        return Color.FromArgb(AgentHues[Math.Abs(name.GetHashCode()) % AgentHues.Length] | unchecked((int)0xFF000000));
    }
    private void EnsureCachedGdiObjects(float fs)
    {
        if (cachedBadgeFont == null) cachedBadgeFont = new Font("Microsoft YaHei UI", 9.0f, FontStyle.Regular, GraphicsUnit.Point);
        if (cachedTitleFont == null || Math.Abs(cachedTitleFontSize - fs) > 0.01f)
        {
            if (cachedTitleFont != null) cachedTitleFont.Dispose();
            cachedTitleFont = new Font("Microsoft YaHei UI", fs, FontStyle.Regular, GraphicsUnit.Point);
            cachedTitleFontSize = fs;
        }
        if (cachedTitleBrush == null) cachedTitleBrush = new SolidBrush(ColorTitleText);
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
        IDisposable[] d = new IDisposable[] { cachedBadgeFont, cachedTitleFont, cachedTitleBrush, cachedBorderNormal, cachedBorderHover, cachedTitleFormat, cachedCenterFormat };
        for (int i = 0; i < d.Length; i++) if (d[i] != null) try { d[i].Dispose(); } catch { }
    }
}