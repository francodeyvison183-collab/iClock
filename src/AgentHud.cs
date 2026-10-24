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
}

internal sealed class AgentTaskStatusHud : Form
{
    private const int DefaultHudWidth = 300;
    private const int MinHudWidth = 160;
    private const int MaxHudWidth = 800;
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
    private Rectangle btnTextColorRect = Rectangle.Empty;
    private Rectangle btnBarColorRect = Rectangle.Empty;
    private Timer altWatchTimer;
    private bool lastAltState;

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
        Size = new Size(CurrentWidth, 24);
        MouseDown += OnHudMouseDown;
        MouseMove += OnHudMouseMove;
        MouseUp += OnHudMouseUp;
        MouseWheel += OnHudMouseWheel;
        LocationChanged += delegate { RenderLayeredWindow(); };
        SizeChanged += delegate { RenderLayeredWindow(); };
        altWatchTimer = new Timer();
        altWatchTimer.Interval = 120;
        altWatchTimer.Tick += delegate
        {
            bool alt = (NativeMethods.GetAsyncKeyState(0x12) & 0x8000) != 0;
            if (alt != lastAltState)
            {
                lastAltState = alt;
                SetTasks(tasks); // height changes between alt / normal modes
            }
            else
            {
                RenderLayeredWindow();
            }
        };
        altWatchTimer.Start();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        if (altWatchTimer != null) { altWatchTimer.Stop(); altWatchTimer.Dispose(); altWatchTimer = null; }
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
        SetTasks(tasks); // recompute height and re-render
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
        if (m.Msg == 0x84)
        {
            bool alt = (NativeMethods.GetAsyncKeyState(0x12) & 0x8000) != 0;
            m.Result = (IntPtr)(alt ? 1 : -1); // HTCLIENT when Alt held (draggable), HTTRANSPARENT otherwise
            return;
        }
        base.WndProc(ref m);
    }

    private void OnHudMouseDown(object sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        bool alt = (NativeMethods.GetAsyncKeyState(0x12) & 0x8000) != 0;
        if (!alt) return;
        if (btnTextColorRect.Contains(e.Location))
        {
            PickColor(true);
            return;
        }
        if (btnBarColorRect.Contains(e.Location))
        {
            PickColor(false);
            return;
        }
        // Right edge (last 8px) resizes width; elsewhere starts move.
        if (e.X >= Width - 8)
        {
            resizingWidth = true;
            dragStart = Cursor.Position;
            dragStartWidth = Width;
            return;
        }
        moving = true; dragStart = Cursor.Position; formStart = Location;
    }

    private void PickColor(bool forText)
    {
        Color init;
        try
        {
            string hex = forText ? settings.TaskStatusTextColor : settings.TaskStatusBarColor;
            init = !string.IsNullOrEmpty(hex) ? ColorTranslator.FromHtml(hex) : (forText ? Color.White : AppContext.Win11Accent);
        }
        catch { init = forText ? Color.White : AppContext.Win11Accent; }

        // Pause the Alt watcher while the dialog is up so the HUD doesn't tear down mid-pick.
        if (altWatchTimer != null) altWatchTimer.Stop();
        using (ColorDialog cd = new ColorDialog())
        {
            cd.Color = init;
            cd.FullOpen = true;
            // No owner: keeps the HUD top-most and avoids focus-steal side-effects.
            if (cd.ShowDialog() != DialogResult.OK)
            {
                if (altWatchTimer != null) altWatchTimer.Start();
                return;
            }
            string hex = ColorTranslator.ToHtml(cd.Color);
            if (forText) settings.TaskStatusTextColor = hex;
            else settings.TaskStatusBarColor = hex;
            settings.Save();
            if (altWatchTimer != null) altWatchTimer.Start();
            RenderLayeredWindow();
        }
    }

    private void OnHudMouseMove(object sender, MouseEventArgs e)
    {
        // Cursor hint over the right resize edge while Alt is held.
        if (!moving && !resizingWidth)
        {
            bool alt = (NativeMethods.GetAsyncKeyState(0x12) & 0x8000) != 0;
            if (alt && e.X >= Width - 8)
            {
                if (Cursor.Current != Cursors.SizeWE) Cursor.Current = Cursors.SizeWE;
            }
            else if (alt)
            {
                if (Cursor.Current != Cursors.SizeAll) Cursor.Current = Cursors.SizeAll;
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

        // Snap window horizontal center to the screen's vertical center line.
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
        float fs = settings != null && settings.TaskStatusFontSize > 0 ? settings.TaskStatusFontSize : 9.5f;
        int progressRowH = (int)Math.Ceiling(fs * 1.9) + 12;   // bar + gap + message
        int messageRowH = (int)Math.Ceiling(fs * 1.9) + 3;     // message only
        int height = 6;
        for (int i = 0; i < tasks.Count; i++)
        {
            height += tasks[i].HasProgress ? progressRowH : messageRowH;
            if (i > 0) height += 6;
        }
        // Alt-mode: double the height so the hover card breathes, content stays vertically centered.
        bool alt = (NativeMethods.GetAsyncKeyState(0x12) & 0x8000) != 0;
        if (alt) height *= 2;
        Size = new Size(CurrentWidth, Math.Max(24, height));
        RenderLayeredWindow();
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
        bool alt = (NativeMethods.GetAsyncKeyState(0x12) & 0x8000) != 0;

        // Alt-mode hover card: same look as the desktop quote's hover state.
        if (alt)
        {
            Rectangle cardRect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = AppContext.GetRoundedRectPath(cardRect, 10))
            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(51, 20, 22, 28)))
            using (Pen borderPen = new Pen(Color.FromArgb(60, 255, 255, 255), 1.0f))
            {
                g.FillPath(bgBrush, path);
                g.DrawPath(borderPen, path);
            }
        }

        // Text color: explicit TaskStatusTextColor wins; else follow quote color rule (QuoteColor or white).
        Color textColor;
        if (!string.IsNullOrEmpty(settings.TaskStatusTextColor))
        {
            try { textColor = ColorTranslator.FromHtml(settings.TaskStatusTextColor); }
            catch { textColor = Color.White; }
        }
        else
        {
            try { textColor = !string.IsNullOrEmpty(settings.QuoteColor) ? ColorTranslator.FromHtml(settings.QuoteColor) : ColorTranslator.FromHtml("#F77F00"); }
            catch { textColor = Color.White; }
        }
        // Bar color: explicit TaskStatusBarColor wins; else theme accent.
        Color barColor;
        if (!string.IsNullOrEmpty(settings.TaskStatusBarColor))
        {
            try { barColor = ColorTranslator.FromHtml(settings.TaskStatusBarColor); }
            catch { barColor = AppContext.Win11Accent; }
        }
        else
        {
            barColor = AppContext.Win11Accent;
        }

        float fs = settings != null && settings.TaskStatusFontSize > 0 ? settings.TaskStatusFontSize : 9.5f;
        using (Font messageFont = new Font("Microsoft YaHei UI", fs, FontStyle.Regular, GraphicsUnit.Point))
        using (SolidBrush textBrush = new SolidBrush(textColor))
        using (SolidBrush track = new SolidBrush(Color.FromArgb(90, 255, 255, 255)))
        using (SolidBrush fill = new SolidBrush(Color.FromArgb(200, barColor)))
        using (Pen divider = new Pen(Color.FromArgb(120, 200, 200, 210)))
        using (StringFormat messageFormat = new StringFormat())
        {
            messageFormat.Alignment = StringAlignment.Center;
            messageFormat.Trimming = StringTrimming.EllipsisWord;
            messageFormat.FormatFlags = StringFormatFlags.LineLimit;
            float progressRowH = (float)Math.Ceiling(fs * 1.9) + 12f;
            float messageRowH = (float)Math.Ceiling(fs * 1.9) + 3f;
            // Compute content height, then center it vertically inside the (possibly doubled) Alt card.
            float contentH = 0;
            for (int i = 0; i < tasks.Count; i++)
            {
                contentH += tasks[i].HasProgress ? progressRowH : messageRowH;
                if (i > 0) contentH += 6f;
            }
            float y = Math.Max(3f, (Height - contentH) / 2f);
            for (int i = 0; i < tasks.Count; i++)
            {
                AgentTaskStatus task = tasks[i];
                float rowTop = y;
                float rowHeight = task.HasProgress ? progressRowH : messageRowH;
                float messageTop;
                if (task.HasProgress)
                {
                    float barY = y + 1f;
                    float barWidth = Width - 16f;
                    g.FillRectangle(track, 8f, barY, barWidth, 3f);
                    g.FillRectangle(fill, 8f, barY, barWidth * Math.Max(0f, Math.Min(1f, task.Progress)), 3f);
                    messageTop = barY + 6f;
                }
                else
                {
                    messageTop = y;
                }

                string message = string.IsNullOrEmpty(task.Message)
                    ? (settings.Language == "en" ? "Task in progress" : "任务进行中")
                    : task.Message;
                float messageHeight = Math.Max(messageFont.GetHeight(g), rowHeight - (messageTop - rowTop) - 2f);
                g.DrawString(message, messageFont, textBrush, new RectangleF(8f, messageTop, Width - 16f, messageHeight), messageFormat);
                y = rowTop + rowHeight;
                if (i + 1 < tasks.Count)
                {
                    y += 2f;
                    g.DrawLine(divider, 8f, y, Width - 8f, y);
                    y += 4f;
                }
            }
        }

        // Alt-mode action buttons in the top-right corner, matching quote card's style.
        if (alt)
        {
            int btnSz = 18;
            int btnGap = 4;
            int btnY = 6;
            btnBarColorRect = new Rectangle(Width - 6 - btnSz, btnY, btnSz, btnSz);
            btnTextColorRect = new Rectangle(Width - 6 - btnSz - btnGap - btnSz, btnY, btnSz, btnSz);
            DrawColorButton(g, btnTextColorRect, textColor, true);
            DrawColorButton(g, btnBarColorRect, barColor, false);
        }
        else
        {
            btnTextColorRect = Rectangle.Empty;
            btnBarColorRect = Rectangle.Empty;
        }
    }

    private static void DrawColorButton(Graphics g, Rectangle rect, Color fill, bool isText)
    {
        using (GraphicsPath path = AppContext.GetRoundedRectPath(rect, 4))
        using (SolidBrush bg = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
        using (Pen border = new Pen(Color.FromArgb(140, 100, 100, 110), 1f))
        {
            g.FillPath(bg, path);
            g.DrawPath(border, path);
        }
        // Inner swatch: square for text color, horizontal bar for bar color.
        Rectangle inner = isText
            ? new Rectangle(rect.X + 4, rect.Y + 4, rect.Width - 8, rect.Height - 8)
            : new Rectangle(rect.X + 4, rect.Y + 7, rect.Width - 8, rect.Height - 11);
        using (SolidBrush sw = new SolidBrush(fill))
        using (Pen swBorder = new Pen(Color.FromArgb(120, 60, 60, 70), 1f))
        {
            g.FillRectangle(sw, inner);
            g.DrawRectangle(swBorder, inner);
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
}

