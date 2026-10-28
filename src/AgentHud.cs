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
}

internal sealed class AgentTaskStatusHud : Form
{
    private const int DefaultHudWidth = 320;
    private const int MinHudWidth = 200;
    private const int MaxHudWidth = 800;
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
    private bool snapV;
    private Form snapGuideForm;
    private Bitmap renderBitmap;
    private byte[] premulBuffer;
    private Font cachedBadgeFont;
    private Font cachedTitleFont;
    private float cachedTitleFontSize = -1f;
    private Font cachedSubFont;
    private Font cachedCloseFont;
    private SolidBrush cachedTitleBrush;
    private SolidBrush cachedSubBrush;
    private Pen cachedBorderNormal;
    private Pen cachedBorderHover;
    private Pen cachedDividerPen;
    private StringFormat cachedTitleFormat;
        private StringFormat cachedCenterFormat;

    private Timer heartbeatTimer;
    private bool isHovered;
    private DateTime lastHoverTime;
    private DateTime lastFgCheckTime = DateTime.MinValue;

    private int CurrentWidth
    {
        get { return settings != null && settings.TaskStatusWidth > 0 ? settings.TaskStatusWidth : DefaultHudWidth; }
    }

    internal AgentTaskStatusHud(Settings settings)
    {
        this.settings = settings;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        Size = new Size(CurrentWidth, CollapsedHeight);

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
        if (snapGuideForm != null) { snapGuideForm.Dispose(); snapGuideForm = null; } CleanupCachedGdiObjects();
    }

    private void OnHeartbeatTick(object sender, EventArgs e)
    {
        if (!IsHandleCreated || IsDisposed) return;

        Point cursorPos = Cursor.Position;
        bool inBounds = Bounds.Contains(cursorPos);
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

            // Auto-acknowledge completed tasks after user focuses the agent window for >= 5s
            if (!isHovered && !moving && !resizingWidth && Visible && tasks.Count > 0 && (now - lastFgCheckTime).TotalMilliseconds >= 1000)
            {
                lastFgCheckTime = now;
                for (int i = tasks.Count - 1; i >= 0; i--)
                {
                    AgentTaskStatus t = tasks[i];
                    bool done = !string.IsNullOrEmpty(t.Message) && (t.Message.StartsWith("✅") || t.Message.Contains("完成") || t.Message.Contains("Completed"));
                    if (done && (!t.HasProgress || string.IsNullOrEmpty(t.StepProgress)))
                    {
                        if (AppContext.IsAgentWindowInForeground(t.AgentName, t.ProjectHint))
                        {
                            t.ForegroundSeenSeconds += 1.0;
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

        // Static state throttling: When idle/completed with static graphics and no hover/drag, skip redundant 60ms redraws
        bool hasActiveAnimation = moving || resizingWidth || isHovered || hoverChanged;
        if (!hasActiveAnimation && tasks != null && tasks.Count > 0)
        {
            for (int i = 0; i < tasks.Count; i++)
            {
                AgentTaskStatus t = tasks[i];
                bool done = !string.IsNullOrEmpty(t.Message) && (t.Message.StartsWith("✅") || t.Message.Contains("完成") || t.Message.Contains("Completed"));
                bool err = !string.IsNullOrEmpty(t.Message) && (t.Message.StartsWith("⚠️") || t.Message.Contains("异常") || t.Message.Contains("死循环"));
                if (!done && !err)
                {
                    hasActiveAnimation = true;
                    break;
                }
            }
        }

        if (hasActiveAnimation || hoverChanged)
        {
            RenderLayeredWindow();
        }
    }

    private void UpdateLayoutAndSize()
    {
        if (tasks == null || tasks.Count == 0) return;
        int taskCount = Math.Max(1, tasks.Count);
        int unitH = isHovered ? ExpandedHeight : CollapsedHeight;
        int totalH = unitH * taskCount + (taskCount > 1 ? (taskCount - 1) * 4 : 0);

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

    private int GetTaskIndexAt(Point pt)
    {
        if (tasks == null || tasks.Count == 0) return -1;
        int unitH = isHovered ? ExpandedHeight : CollapsedHeight;
        int idx = pt.Y / (unitH + 4);
        return (idx >= 0 && idx < tasks.Count) ? idx : -1;
    }

    private void OnHudMouseWheel(object sender, MouseEventArgs e)
    {
        bool ctrlAlt = (Control.ModifierKeys & Keys.Control) != 0 && (Control.ModifierKeys & Keys.Alt) != 0;
        if (!ctrlAlt) return;
        float delta = e.Delta > 0 ? 0.5f : -0.5f;
        float next = Math.Max(7.0f, Math.Min(16.0f, settings.TaskStatusFontSize + delta));
        if (Math.Abs(next - settings.TaskStatusFontSize) < 0.01f) return;
        settings.TaskStatusFontSize = next;
        settings.Save();
        UpdateLayoutAndSize();
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

    private void OnHudMouseDown(object sender, MouseEventArgs e)
    {
        // Right-click: quickly dismiss/clear current agent progress
        if (e.Button == MouseButtons.Right)
        {
            int idx = GetTaskIndexAt(e.Location);
            string agent = (idx >= 0 && idx < tasks.Count) ? tasks[idx].AgentName : (tasks.Count > 0 ? tasks[0].AgentName : null);
            if (AppContext.Current != null)
            {
                AppContext.Current.ClearAgentProgress(agent);
            }
            return;
        }

        if (e.Button != MouseButtons.Left) return;

        int taskIdx = GetTaskIndexAt(e.Location);
        if (taskIdx >= 0)
        {
            int unitH = isHovered ? ExpandedHeight : CollapsedHeight;
            int cardY = taskIdx * (unitH + 4);
            Rectangle cornerClose = new Rectangle(Width - 27, cardY + 5, 18, 18);

            if (cornerClose.Contains(e.Location))
            {
                if (AppContext.Current != null)
                {
                    AppContext.Current.ClearAgentProgress(tasks[taskIdx].AgentName);
                }
                return;
            }
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
            if (e.X >= Width - 8)
            {
                if (Cursor.Current != Cursors.SizeWE) Cursor.Current = Cursors.SizeWE;
            }
            else
            {
                int idx = GetTaskIndexAt(e.Location);
                if (idx >= 0)
                {
                    int unitH = isHovered ? ExpandedHeight : CollapsedHeight;
                    int cardY = idx * (unitH + 4);
                    Rectangle cornerClose = new Rectangle(Width - 27, cardY + 5, 18, 18);
                    if (cornerClose.Contains(e.Location))
                    {
                        if (Cursor.Current != Cursors.Hand) Cursor.Current = Cursors.Hand;
                    }
                    else
                    {
                        if (Cursor.Current != Cursors.Default) Cursor.Current = Cursors.Default;
                    }
                }
                else
                {
                    if (Cursor.Current != Cursors.Default) Cursor.Current = Cursors.Default;
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

        if (!moving) return;

        int newX = formStart.X + Cursor.Position.X - dragStart.X;
        int newY = formStart.Y + Cursor.Position.Y - dragStart.Y;

        Rectangle screen = Screen.FromPoint(Cursor.Position).WorkingArea;
        int centerX = screen.Left + screen.Width / 2;
        int windowCenterX = newX + Width / 2;
        const int snapThreshold = 16;
        snapV = Math.Abs(windowCenterX - centerX) <= snapThreshold;
        if (snapV) newX = centerX - Width / 2;
        Location = new Point(newX, newY);

        if (snapV)
        {
            if (snapGuideForm == null)
            {
                snapGuideForm = AppContext.CreateSnapGuide(OnSnapGuidePaint);
            }
            snapGuideForm.Bounds = screen;
            if (!snapGuideForm.Visible) snapGuideForm.Show();
            snapGuideForm.Invalidate();
        }
        else if (snapGuideForm != null && snapGuideForm.Visible)
        {
            snapGuideForm.Hide();
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
        snapV = false;
        if (snapGuideForm != null) snapGuideForm.Hide();

        // Left-click without drag (< 4px): direct Warp/Focus to agent window + auto-dismiss if already completed!
        if (Math.Abs(Cursor.Position.X - dragStart.X) < 4 && Math.Abs(Cursor.Position.Y - dragStart.Y) < 4)
        {
            int idx = GetTaskIndexAt(PointToClient(dragStart));
            if (idx >= 0 && AppContext.Current != null && idx < tasks.Count)
            {
                AgentTaskStatus clicked = tasks[idx];
                AppContext.Current.WarpToAgentWindow(clicked.AgentName, clicked.ProjectHint);
                bool done = !string.IsNullOrEmpty(clicked.Message) && (clicked.Message.StartsWith("✅") || clicked.Message.Contains("完成") || clicked.Message.Contains("Completed"));
                if (done && (!clicked.HasProgress || string.IsNullOrEmpty(clicked.StepProgress)))
                {
                    AppContext.Current.ClearAgentProgress(clicked.AgentName);
                }
            }
            return;
        }
        settings.TaskStatusX = Location.X;
        settings.TaskStatusY = Location.Y;
        settings.Save();
    }

    private void OnSnapGuidePaint(object sender, PaintEventArgs e)
    {
        if (!snapV) return;
        AppContext.DrawSnapGuideLines((Form)sender, e, snapV, false);
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
        AppContext.UpdateLayered(Left, Top, Width, Height, Handle, renderBitmap, 255);
    }

    private void DrawTasks(Graphics g)
    {
        if (tasks.Count == 0) return;

        float fs = settings != null && settings.TaskStatusFontSize > 0 ? settings.TaskStatusFontSize : 9.5f;
        int unitH = isHovered ? ExpandedHeight : CollapsedHeight;
        Point clientCursor = PointToClient(Cursor.Position);

        EnsureCachedGdiObjects(fs);
        Font badgeFont = cachedBadgeFont;
        Font titleFont = cachedTitleFont;
        Font subFont = cachedSubFont;
        Font closeFont = cachedCloseFont;
        SolidBrush titleBrush = cachedTitleBrush;
        SolidBrush subBrush = cachedSubBrush;
        Pen borderPen = isHovered ? cachedBorderHover : cachedBorderNormal;
        Pen dividerPen = cachedDividerPen;
        StringFormat titleFormat = cachedTitleFormat;
        StringFormat subFormat = cachedTitleFormat;
        StringFormat centerFormat = cachedCenterFormat;
        {
            double pulse = (DateTime.Now.Ticks / 10000000.0) * Math.PI * 1.5;

            for (int i = 0; i < tasks.Count; i++)
            {
                AgentTaskStatus task = tasks[i];
                int cardY = i * (unitH + 4);
                Rectangle cardRect = new Rectangle(0, cardY, Width - 1, unitH - 1);

                // 1. Fluent Acrylic Pill Background
                using (GraphicsPath path = AppContext.GetRoundedRectPath(cardRect, isHovered ? 10 : 14))
                using (SolidBrush bgBrush = new SolidBrush(ColorBg))
                {
                    g.FillPath(bgBrush, path);
                    g.DrawPath(borderPen, path);
                }

                // 2. Agent Brand Badge
                string rawName = task.AgentName ?? "";
                Color badgeColor;
                string badgeLabel;
                if (rawName.IndexOf("Claude", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    badgeColor = Color.FromArgb(218, 119, 86); // Claude Coral / Terra Cotta #DA7756
                    badgeLabel = "Claude";
                }
                else if (rawName.IndexOf("Antigravity", StringComparison.OrdinalIgnoreCase) >= 0 || rawName.IndexOf("AGY", StringComparison.OrdinalIgnoreCase) >= 0 || rawName.IndexOf("Gemini", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    badgeColor = Color.FromArgb(59, 130, 246); // AGY Blue #3B82F6
                    badgeLabel = "AGY";
                }
                else if (rawName.IndexOf("Codex", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    badgeColor = Color.FromArgb(14, 165, 233); // Codex Cyan #0EA5E9
                    badgeLabel = "Codex";
                }
                else if (rawName.IndexOf("Cursor", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    badgeColor = Color.FromArgb(16, 185, 129); // Cursor Emerald #10B981
                    badgeLabel = "Cursor";
                }
                else if (rawName.IndexOf("Windsurf", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    badgeColor = Color.FromArgb(6, 182, 212); // Windsurf Teal #06B6D4
                    badgeLabel = "Windsurf";
                }
                else if (rawName.IndexOf("Trae", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    badgeColor = Color.FromArgb(139, 92, 246); // Trae Purple #8B5CF6
                    badgeLabel = "Trae";
                }
                else if (rawName.IndexOf("Aider", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    badgeColor = Color.FromArgb(249, 115, 22); // Aider Orange #F97316
                    badgeLabel = "Aider";
                }
                else if (rawName.IndexOf("Copilot", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    badgeColor = Color.FromArgb(99, 102, 241); // Copilot Indigo #6366F1
                    badgeLabel = "Copilot";
                }
                else
                {
                    badgeColor = GetDeterministicAgentColor(rawName);
                    badgeLabel = GetShortAgentBadgeLabel(rawName);
                }

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

                // 3. Session Target Title (Anchor) & Suffix
                string fullMsg = task.Message ?? "";
                string displayTitle = fullMsg;
                string displaySuffix = "";
                int startBracket = fullMsg.IndexOf('[');
                int endBracket = fullMsg.IndexOf(']');
                if (startBracket >= 0 && endBracket > startBracket)
                {
                    displayTitle = fullMsg.Substring(startBracket + 1, endBracket - startBracket - 1).Trim();
                    displaySuffix = fullMsg.Substring(endBracket + 1).Trim();
                }
                else
                {
                    displayTitle = Regex.Replace(displayTitle, @"^[⏳✅❓⚠️]\s*", "").Trim();
                }

                // Filter out redundant "处理中" / "Processing" / "任务进行中" (right-side comet ring already conveys activity)
                if (!string.IsNullOrEmpty(displaySuffix) && (displaySuffix.Contains("处理中") || displaySuffix.IndexOf("Processing", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    displaySuffix = "";
                }
                if (displayTitle.Contains("处理中") || displayTitle.Contains("任务进行中") || displayTitle.IndexOf("Task in progress", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    displayTitle = Regex.Replace(displayTitle, @"(?:处理中\.{0,3}|任务进行中|Task in progress)", "").Trim(' ', '·', '-');
                }

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

                Color statusColor;
                if (isCompleted) statusColor = ColorDone;
                else if (isError) statusColor = ColorError;
                else if (isWaiting) statusColor = ColorWaiting;
                else statusColor = ColorRunning;

                float textX = badgeRect.Right + 8f;
                float textW = Math.Max(40f, (ringCenterX - 10f) - textX);
                RectangleF titleRect = new RectangleF(textX, cardY + 4f, textW, 20f);

                string textToDraw;
                if (!isHovered)
                {
                    if (!string.IsNullOrEmpty(task.StepProgress))
                    {
                        // Collapsed mode with active step/TODO: prioritize displaying the step e.g. "1/3 正在执行算法优化"
                        string combined = !string.IsNullOrEmpty(displayTitle) ? (displayTitle + " · " + task.StepProgress) : task.StepProgress;
                        if (!string.IsNullOrEmpty(displayTitle) && !string.Equals(displayTitle, task.StepProgress, StringComparison.OrdinalIgnoreCase)
                            && g.MeasureString(combined, titleFont).Width <= textW)
                        {
                            textToDraw = combined;
                        }
                        else
                        {
                            textToDraw = task.StepProgress;
                        }
                    }
                    else if (isRunning)
                    {
                        int elapsedSec = task.TurnStartTime > DateTime.MinValue ? Math.Max(0, (int)(DateTime.Now - task.TurnStartTime).TotalSeconds) : 0;
                        string timeBadge = elapsedSec >= 1 ? (elapsedSec >= 60 ? (elapsedSec / 60 + "m" + (elapsedSec % 60).ToString("D2") + "s") : (elapsedSec + "s")) : "";

                        string briefAction = "";
                        if (!string.IsNullOrEmpty(task.SubDetail))
                        {
                            briefAction = task.SubDetail.Replace("正在执行: ", "").Replace("Running: ", "").Trim();
                            if (briefAction.Length > 28) briefAction = briefAction.Substring(0, 25) + "...";
                        }

                        // Try candidate 1: Title + Action + Time
                        string c1 = !string.IsNullOrEmpty(briefAction) && !string.IsNullOrEmpty(timeBadge)
                            ? (displayTitle + " · " + briefAction + " (" + timeBadge + ")")
                            : (!string.IsNullOrEmpty(briefAction) ? (displayTitle + " · " + briefAction) : (!string.IsNullOrEmpty(timeBadge) ? (displayTitle + " · " + timeBadge) : displayTitle));

                        if (g.MeasureString(c1, titleFont).Width <= textW)
                        {
                            textToDraw = c1;
                        }
                        else
                        {
                            // Try candidate 2: Title + Action
                            string c2 = !string.IsNullOrEmpty(briefAction) ? (displayTitle + " · " + briefAction) : null;
                            if (c2 != null && g.MeasureString(c2, titleFont).Width <= textW)
                            {
                                textToDraw = c2;
                            }
                            else
                            {
                                // Try candidate 3: Title + Time
                                string c3 = !string.IsNullOrEmpty(timeBadge) ? (displayTitle + " · " + timeBadge) : null;
                                if (c3 != null && g.MeasureString(c3, titleFont).Width <= textW)
                                {
                                    textToDraw = c3;
                                }
                                else
                                {
                                    textToDraw = displayTitle;
                                }
                            }
                        }
                    }
                    else if (isCompleted)
                    {
                        if (!string.IsNullOrEmpty(displaySuffix))
                        {
                            string combined = displayTitle + " · " + displaySuffix;
                            if (g.MeasureString(combined, titleFont).Width <= textW) textToDraw = combined;
                            else textToDraw = displayTitle;
                        }
                        else
                        {
                            textToDraw = displayTitle;
                        }
                    }
                    else
                    {
                        textToDraw = displayTitle;
                    }
                }
                else
                {
                    // Expanded mode: top row keeps title clean, details rendered in drawer
                    textToDraw = displayTitle;
                }
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
                        g.DrawString("✕", closeFont, cornerFg, cornerCloseRect, centerFormat);
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

                        int[] tailAlphas = new int[] { 35, 75, 140, 215 };
                        float sweepStart = headAngle - 88f;
                        for (int s = 0; s < 4; s++)
                        {
                            using (Pen tailPen = new Pen(Color.FromArgb(tailAlphas[s], statusColor), 1.5f + s * 0.15f))
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
                            PointF[] checkPts = new PointF[]
                            {
                                new PointF(ringCenterX - 3.2f, ringCenterY - 0.1f),
                                new PointF(ringCenterX - 0.9f, ringCenterY + 2.4f),
                                new PointF(ringCenterX + 3.3f, ringCenterY - 2.3f)
                            };
                            g.DrawLines(checkPen, checkPts);
                        }
                    }
                }

                // 6. Expanded Micro-Drawer Details
                if (isHovered)
                {
                    g.DrawLine(dividerPen, 10f, cardY + 26f, Width - 10f, cardY + 26f);

                    string sub = !string.IsNullOrEmpty(task.SubDetail)
                        ? task.SubDetail
                        : (!string.IsNullOrEmpty(task.StepProgress)
                            ? (settings.Language == "en" ? ("Running: " + task.StepProgress) : ("正在执行: " + task.StepProgress))
                            : (task.IsThinking
                                ? (settings.Language == "en" ? "Analyzing and executing tools..." : "思考分析与规划中...")
                                : (isCompleted
                                    ? (settings.Language == "en" ? "Turn completed successfully" : "本轮会话已顺利完成")
                                    : (isError
                                        ? (settings.Language == "en" ? "Task interrupted or encountered an error" : "任务执行遇到异常或已中断")
                                        : (settings.Language == "en" ? "Waiting for user decision" : "等待用户决策")))));

                    float rightReserved = 0f;
                    if (isRunning && task.TurnStartTime > DateTime.MinValue)
                    {
                        int elapsedSec = Math.Max(0, (int)(DateTime.Now - task.TurnStartTime).TotalSeconds);
                        int stepSec = task.StepStartTime > DateTime.MinValue ? Math.Max(0, (int)(DateTime.Now - task.StepStartTime).TotalSeconds) : elapsedSec;
                        if (elapsedSec >= 1)
                        {
                            string timeBadge = elapsedSec >= 60
                                ? (elapsedSec / 60 + "m" + (elapsedSec % 60).ToString("D2") + "s")
                                : (elapsedSec + "s");
                            SizeF timeSize = g.MeasureString(timeBadge, subFont);
                            rightReserved = timeSize.Width + 6f;
                            Color timerColor = stepSec >= 45 ? ColorWaiting : ColorSuffixText;
                            using (SolidBrush timerBrush = new SolidBrush(timerColor))
                            {
                                RectangleF timeRect = new RectangleF(Width - 10f - timeSize.Width, cardY + 27f, timeSize.Width + 2f, 17f);
                                g.DrawString(timeBadge, subFont, timerBrush, timeRect, subFormat);
                            }
                        }
                    }

                    float subW = Math.Max(40f, Width - 12f - textX - rightReserved);
                    RectangleF subRect = new RectangleF(textX, cardY + 27f, subW, 17f);
                    g.DrawString(sub, subFont, subBrush, subRect, subFormat);
                }
            }
        }
    }

    internal void UpdatePosition(Rectangle clockBounds)
    {
        Screen screen = Screen.FromRectangle(clockBounds);
        Rectangle work = screen.WorkingArea;
        int x = clockBounds.Left + (clockBounds.Width - Width) / 2;
        int y = clockBounds.Bottom + 4;
        if (y + Height > work.Bottom) y = clockBounds.Top - Height - 4;
        x = Math.Max(work.Left, Math.Min(work.Right - Width, x));
        y = Math.Max(work.Top, Math.Min(work.Bottom - Height, y));
        Location = new Point(x, y);
    }
    internal static Color GetDeterministicAgentColor(string name)
    {
        if (string.IsNullOrEmpty(name)) return AppContext.Win11Accent;
        int hash = Math.Abs(name.GetHashCode());
        int[] hues = new int[] { 0x38BDF8, 0x818CF8, 0xA855F7, 0xEC4899, 0xF43F5E, 0xFB923C, 0xFBBF24, 0x34D399, 0x2DD4BF };
        int rgb = hues[hash % hues.Length];
        return Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
    }

    internal static string GetShortAgentBadgeLabel(string rawName)
    {
        if (string.IsNullOrEmpty(rawName)) return "AI";
        string s = rawName.Trim();
        if (s.Length <= 6) return s;
        return s.Substring(0, 5) + "…";
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
        if (cachedCloseFont == null) cachedCloseFont = new Font("Segoe UI", 7.5f, FontStyle.Regular, GraphicsUnit.Point);
        if (cachedTitleBrush == null) cachedTitleBrush = new SolidBrush(ColorTitleText);
        if (cachedSubBrush == null) cachedSubBrush = new SolidBrush(ColorSubDetail);
        if (cachedBorderNormal == null) cachedBorderNormal = new Pen(ColorBorderNormal, 1.0f);
        if (cachedBorderHover == null) cachedBorderHover = new Pen(ColorBorderHover, 1.0f);
        if (cachedDividerPen == null) cachedDividerPen = new Pen(Color.FromArgb(30, 255, 255, 255), 1.0f);
        if (cachedTitleFormat == null) cachedTitleFormat = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
                if (cachedCenterFormat == null) cachedCenterFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
    }

    private void CleanupCachedGdiObjects()
    {
        IDisposable[] d = new IDisposable[] { cachedBadgeFont, cachedTitleFont, cachedSubFont, cachedCloseFont, cachedTitleBrush, cachedSubBrush, cachedBorderNormal, cachedBorderHover, cachedDividerPen, cachedTitleFormat, cachedCenterFormat };
        for (int i = 0; i < d.Length; i++) if (d[i] != null) try { d[i].Dispose(); } catch { }
    }
}