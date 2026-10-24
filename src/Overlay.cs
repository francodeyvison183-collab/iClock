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


    private Bitmap renderBitmap;
    private Graphics renderGraphics;
    private byte[] premulBuffer;

    // Snap guide overlay (fullscreen transparent window for drawing center lines)
    private Form snapGuideForm;
    private bool snapGuideH, snapGuideV;

    private readonly Settings settings;
    private string text;
    private bool moving;
    private Point dragStart;
    private Point formStart;
    private bool snapH, snapV; // horizontal/vertical center snap active during drag
    private Font cachedFont;
    private SolidBrush cachedBrush;
    private SolidBrush cachedShadowBrush;
    private StringFormat cachedFormat;
    private float cachedDigitCenterY;
    private float cachedDigitHeight;
    // 模板文本宽度：进度条与数字同宽，窗口尺寸变化时也不会跳动。
    private float cachedTextWidth;
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

    public void RefreshSettings()
    {
        RebuildGdiResources();
        UpdateLocation();
        RenderLayeredWindow();
    }

    public void SetVisualPreview(string previewText)
    {
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
        // 每日目标进度条占据数字下方的一条空间；关闭（0）时窗口高度回到纯文字。
        bool showProgress = settings.DailyGoalMinutes > 0 && progressFraction >= 0f;
        using (Bitmap bmp = new Bitmap(1, 1))
        using (Graphics g = Graphics.FromImage(bmp))
        {
            SizeF size = g.MeasureString(template, cachedFont);
            float extraPauseWidth = (float)Math.Ceiling(cachedFont.Size * 0.75f);
            cachedTextWidth = size.Width;
            int textH = Math.Max(50, (int)Math.Ceiling(size.Height) + 10);
            if (showProgress) textH += 8;
            Size = new Size(Math.Max(100, (int)Math.Ceiling(size.Width + extraPauseWidth) + 16), textH);
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

    // 每日专注目标进度条：画在数字下方，与文字同宽。整段等高、两端平直，没有末端圆点。
    private void DrawDailyProgressBar(Graphics g)
    {
        const int barHeight = 2;
        // 条宽固定为模板文字宽度：倒计时每秒都在变，若跟着实际文字量，
        // 进度条会随位数变化抖动。
        int barW = Math.Max(20, (int)Math.Round(cachedTextWidth > 0 ? cachedTextWidth : Width));
        int barX = (Width - barW) / 2;
        int barY = Height - barHeight - 3;
        // 关掉抗锯齿，避免扁椭圆或半像素边缘把同一条线画成中间粗、两头细。
        SmoothingMode prev = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.None;
        Color trackColor = Color.FromArgb(60, cachedShadowBrush != null ? cachedShadowBrush.Color : Color.Gray);
        using (SolidBrush trackBrush = new SolidBrush(trackColor))
        {
            g.FillRectangle(trackBrush, barX, barY, barW, barHeight);
        }
        int fillW = (int)Math.Round(barW * Math.Max(0f, Math.Min(1f, progressFraction)));
        if (fillW > barW) fillW = barW;
        if (fillW > 0 && cachedBrush != null)
        {
            g.FillRectangle(cachedBrush, barX, barY, fillW, barHeight);
        }
        g.SmoothingMode = prev;
    }

    public void UpdateText(string value)
    {
        if (text == value) return;
        text = value;
        RenderLayeredWindow();
    }

    public bool IsPaused { get; private set; }

    // 今日专注进度（0..1）；< 0 表示未设置/未计算，不画进度条。
    private float progressFraction = -1f;
    private bool progressFull; // 当日已达标：进度条画满

    public void SetDailyProgress(float fraction, bool full)
    {
        fraction = Math.Max(-1f, Math.Min(1f, fraction));
        if (!full) fraction = Math.Min(fraction, 0.999f);
        bool changed = progressFraction != fraction || progressFull != full;
        progressFraction = fraction;
        progressFull = full;
        if (!changed && renderBitmap != null) return;
        RebuildGdiResources();
        RenderLayeredWindow();
    }

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
            using (Pen borderPen = new Pen(snapH || snapV ? Color.FromArgb(0, 180, 255) : AppContext.Win11Accent, snapH || snapV ? 3.0f : 2.0f))
            {
                renderGraphics.FillPath(bgBrush, path);
                renderGraphics.DrawPath(borderPen, path);
            }
        }

        if (!string.IsNullOrEmpty(text) && cachedFont != null && cachedBrush != null)
        {
            // 进度条占掉底部 8px 时，文字仍在上面那块区域里居中，
            // 这样开关目标不会让数字上下跳动。
            bool barVisible = progressFraction >= 0f && settings.DailyGoalMinutes > 0;
            int textAreaH = barVisible ? Math.Max(1, Height - 8) : Height;
            Rectangle rect = new Rectangle(0, 0, Width, textAreaH);
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
                    float centerY = cachedDigitCenterY > 0 ? cachedDigitCenterY : (textAreaH / 2f);
                    float iconY = centerY - barHeight / 2f;
                    float textX = startX + iconWidth + spacing;
                    RectangleF textRect = new RectangleF(textX, 0, textSize.Width, textAreaH);

                    using (StringFormat sf = new StringFormat(StringFormat.GenericTypographic))
                    {
                        sf.Alignment = StringAlignment.Center;
                        sf.LineAlignment = StringAlignment.Center;

                        FillRoundedBar(renderGraphics, shadowBrush, startX + 1.5f, iconY + 2.0f, barWidth, barHeight);
                        FillRoundedBar(renderGraphics, shadowBrush, startX + barWidth + barGap + 1.5f, iconY + 2.0f, barWidth, barHeight);
                        RectangleF shadowTextRect = new RectangleF(textX + 1.5f, 2.0f, textSize.Width, textAreaH);
                        renderGraphics.DrawString(text, cachedFont, shadowBrush, shadowTextRect, sf);

                        FillRoundedBar(renderGraphics, cachedBrush, startX, iconY, barWidth, barHeight);
                        FillRoundedBar(renderGraphics, cachedBrush, startX + barWidth + barGap, iconY, barWidth, barHeight);
                        renderGraphics.DrawString(text, cachedFont, cachedBrush, textRect, sf);
                    }
                }
                else
                {
                    RectangleF shadowRect = new RectangleF(1.5f, 2.0f, Width, textAreaH);
                    renderGraphics.DrawString(text, cachedFont, shadowBrush, shadowRect, cachedFormat);
                    renderGraphics.DrawString(text, cachedFont, cachedBrush, rect, cachedFormat);
                }

                if (barVisible)
                {
                    DrawDailyProgressBar(renderGraphics);
                }
            }
        }

        AppContext.PremultiplyBitmap(renderBitmap, ref premulBuffer);

        byte alpha = (byte)Math.Max(0, Math.Min(255, (int)(Opacity * 255)));
        AppContext.UpdateLayered(Left, Top, Width, Height, Handle, renderBitmap, alpha);
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
            int newX = formStart.X + Cursor.Position.X - dragStart.X;
            int newY = formStart.Y + Cursor.Position.Y - dragStart.Y;

            // Snap to screen center lines
            Rectangle screen = Screen.FromPoint(Cursor.Position).WorkingArea;
            int centerX = screen.Left + screen.Width / 2;
            int centerY = screen.Top + screen.Height / 2;
            int windowCenterX = newX + Width / 2;
            int windowCenterY = newY + Height / 2;
            const int snapThreshold = 24;

            snapH = Math.Abs(windowCenterX - centerX) <= snapThreshold;
            snapV = Math.Abs(windowCenterY - centerY) <= snapThreshold;

            if (snapH) newX = centerX - Width / 2;
            if (snapV) newY = centerY - Height / 2;

            Location = new Point(newX, newY);
            RenderLayeredWindow(); // redraw snap guides

            // Update fullscreen guide overlay
            if (snapH || snapV)
            {
                if (snapGuideForm == null || !snapGuideForm.Visible)
                    ShowSnapGuide();
                if (snapGuideForm != null)
                {
                    snapGuideH = snapV; // horizontal line shows when Y-axis snapped
                    snapGuideV = snapH; // vertical line shows when X-axis snapped
                    snapGuideForm.Invalidate();
                }
            }
            else
            {
                HideSnapGuide();
            }
        }
    }
    private void OnMouseUp(object sender, MouseEventArgs e)
    {
        if (!moving) return;
        moving = false;
        snapH = snapV = false;
        HideSnapGuide();
        EnsureVisible();
        if (PositionChanged != null) PositionChanged(Location);
    }

    private void ShowSnapGuide()
    {
        if (snapGuideForm == null)
        {
            snapGuideForm = AppContext.CreateSnapGuide(OnSnapGuidePaint);
        }
        Rectangle scr = Screen.FromPoint(Cursor.Position).WorkingArea;
        snapGuideForm.Bounds = scr;
        snapGuideForm.Show();
    }

    private void HideSnapGuide()
    {
        if (snapGuideForm != null)
        {
            snapGuideForm.Hide();
        }
    }

    private void OnSnapGuidePaint(object sender, PaintEventArgs e)
    {
        if (!snapGuideH && !snapGuideV) return;
        Form f = (Form)sender;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (Pen pen = new Pen(Color.FromArgb(180, 0, 180, 255), 1.0f))
        {
            pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
            if (snapGuideV)
            {
                int x = f.Width / 2;
                e.Graphics.DrawLine(pen, x, 0, x, f.Height);
            }
            if (snapGuideH)
            {
                int y = f.Height / 2;
                e.Graphics.DrawLine(pen, 0, y, f.Width, y);
            }
        }
    }

}

