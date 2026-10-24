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

    private readonly Settings settings;
    private List<QuoteItem> quoteList;
    private int currentQuoteIndex;
    private bool isPinned;
    private bool isHovered;
    private bool isDragging;
    private Point dragStart;
    private Point formStart;

    // Snap guide overlay
    private Form snapGuideForm;
    private bool snapGuideH, snapGuideV;

    private bool isResizingWidth;
    private int dragStartX;
    private int resizeStartWidth;
    private int customWidth = -1;
    private bool isProjectedNote;
    private bool hasRenderedOnce;
    private bool needsFirstVisibleGeometrySync;
    public bool IsProjectedNote { get { return isProjectedNote; } }
    private Timer autoDismissTimer;

    private Timer fadeTimer;
    private float currentFontSize = 18.0f;
    public float CurrentFontSize { get { return currentFontSize; } }
    public int CurrentWidth { get { return customWidth; } }
    public event Action<float> FontSizeChanged;
    public event Action QuoteChanged;
    public event Action<bool> PinChanged;
    public bool MoveMode { get; private set; }
    public bool WasDismissedByUser { get; private set; }
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
                if (!isProjectedNote && settings != null)
                {
                    settings.QuotePinTopMost = isPinned;
                    settings.Save();
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
            SaveQuotePosition();
            UpdateCardLayout(keepLeft: true);
        }
    }

    public void NextQuote()
    {
        if (quoteList != null && quoteList.Count > 0)
        {
            currentQuoteIndex = (currentQuoteIndex + 1) % quoteList.Count;
            SaveQuotePosition();
            UpdateCardLayout(keepLeft: true);
            if (QuoteChanged != null) QuoteChanged();
        }
    }

    private void SaveQuotePosition()
    {
        if (settings == null) return;
        settings.QuoteIndex = currentQuoteIndex;
        settings.QuoteDate = DateTime.Today.ToString("yyyy-MM-dd");
        settings.Save();
    }

    public void ReloadQuotes()
    {
        quoteList = LoadQuotes();
        if (quoteList != null && quoteList.Count > 0)
        {
            currentQuoteIndex = quoteList.Count - 1;
            UpdateCardLayout(keepLeft: false);
            if (QuoteChanged != null) QuoteChanged();
        }
    }

    public void UpdateTopMostState()
    {
        if (IsDisposed) return;
        try
        {
            bool wantTopMost = isProjectedNote || isPinned || isPreviewMode || (settings != null && settings.QuotePinTopMost);
            TopMost = wantTopMost;
            if (IsHandleCreated)
            {
                IntPtr insertAfter = wantTopMost ? HWND_TOPMOST : HWND_BOTTOM;
                NativeMethods.SetWindowPos(Handle, insertAfter, 0, 0, 0, 0, SWP_FLAGS);
            }
        }
        catch { }
    }

    public void ApplySettings()
    {
        if (settings == null) return;
        if (!isProjectedNote)
        {
            customWidth = settings.QuoteWidth > 0 ? settings.QuoteWidth : -1;
        }
        if (settings.QuoteFontSize >= 8.0f && settings.QuoteFontSize <= 24.0f)
        {
            currentFontSize = settings.QuoteFontSize;
        }
        isPinned = isProjectedNote || settings.QuotePinTopMost;
        UpdateTopMostState();
        UpdateCardLayout(keepLeft: false);
    }

    public void CapturePersistentState()
    {
        if (settings == null || isProjectedNote) return;
        if (IsHandleCreated)
        {
            if (!IsUsingPrimaryFallback())
            {
                settings.QuoteX = Location.X;
                settings.QuoteY = Location.Y;
            }
        }
        settings.QuoteWidth = customWidth > 0 ? customWidth : -1;
        if (currentFontSize >= 8.0f && currentFontSize <= 24.0f)
        {
            settings.QuoteFontSize = currentFontSize;
        }
    }
    private double currentOpacity = 1.0;

    private Bitmap renderBitmap;
    private Graphics renderGraphics;
    private byte[] premulBuffer;

    private Rectangle btnRefreshRect;
    private Rectangle btnPinRect;
    private Rectangle btnCloseRect;
    private int hoveredBtn = 0;

    public DailyQuoteDialog(Settings s) : this(s, null, null, false) { }

    public DailyQuoteDialog(Settings s, string customText, string customAuthor)
        : this(s, customText, customAuthor, !string.IsNullOrEmpty(customText)) { }

    public DailyQuoteDialog(Settings s, string customText, string customAuthor, bool isProjected)
    {
        settings = s;
        isProjectedNote = isProjected;
        customWidth = (settings != null && settings.QuoteWidth > 0) ? settings.QuoteWidth : -1;
        isPinned = isProjectedNote || (settings != null && settings.QuotePinTopMost);
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
        TopMost = isPinned;
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
            currentQuoteIndex = AppContext.ResolveTodayQuoteIndex(settings, quoteList.Count);
        }

        if (isProjectedNote)
        {
            autoDismissTimer = new Timer();
            autoDismissTimer.Interval = 60000; // 1 minute auto-dismiss
            autoDismissTimer.Tick += delegate
            {
                if (autoDismissTimer != null)
                {
                    try { autoDismissTimer.Stop(); autoDismissTimer.Dispose(); } catch { }
                    autoDismissTimer = null;
                }
                Dismiss();
            };
            autoDismissTimer.Start();
        }

        FormClosed += delegate
        {
            if (autoDismissTimer != null)
            {
                try { autoDismissTimer.Stop(); autoDismissTimer.Dispose(); } catch { }
                autoDismissTimer = null;
            }
        };

        ApplySettings();
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

    private static bool IsAutoCenterCoord(int coord)
    {
        return coord == -99999 || coord == -1;
    }

    private Screen ResolveSavedScreen()
    {
        if (settings == null) return Screen.PrimaryScreen;
        bool hasCustomX = !IsAutoCenterCoord(settings.QuoteX);
        bool hasCustomY = !IsAutoCenterCoord(settings.QuoteY);
        if (!hasCustomX && !hasCustomY) return Screen.PrimaryScreen;

        Point savedPoint = new Point(hasCustomX ? settings.QuoteX : 0, hasCustomY ? settings.QuoteY : 0);
        foreach (Screen s in Screen.AllScreens)
        {
            if (s.Bounds.Contains(savedPoint)) return s;
        }
        return null;
    }

    private bool IsSavedPositionAvailable(Screen host)
    {
        if (settings == null) return false;
        bool customX = !IsAutoCenterCoord(settings.QuoteX);
        bool customY = !IsAutoCenterCoord(settings.QuoteY);
        if (!customX && !customY) return false;
        Point savedPoint = new Point(customX ? settings.QuoteX : host.Bounds.Left, customY ? settings.QuoteY : host.Bounds.Top);
        return host.Bounds.Contains(savedPoint);
    }

    private Screen GetCurrentScreen()
    {
        Screen savedScreen = ResolveSavedScreen();
        if (savedScreen != null) return savedScreen;
        if (settings != null && (!IsAutoCenterCoord(settings.QuoteX) || !IsAutoCenterCoord(settings.QuoteY)))
        {
            // The saved monitor is disconnected. Use the primary monitor for
            // this session without overwriting the saved extended-screen state.
            return Screen.PrimaryScreen;
        }

        if (IsHandleCreated)
        {
            try
            {
                Screen s = Screen.FromControl(this);
                if (s != null) return s;
            }
            catch { }
        }

        Rectangle currentRect = new Rectangle(Location, Size);
        foreach (Screen s in Screen.AllScreens)
        {
            Rectangle intersect = Rectangle.Intersect(s.WorkingArea, currentRect);
            if (intersect.Width >= 30 && intersect.Height >= 20)
            {
                return s;
            }
        }

        return Screen.PrimaryScreen;
    }

    private bool IsUsingPrimaryFallback()
    {
        if (settings == null) return false;
        bool hasSavedPosition = !IsAutoCenterCoord(settings.QuoteX) || !IsAutoCenterCoord(settings.QuoteY);
        if (!hasSavedPosition) return false;
        Screen primary = Screen.PrimaryScreen;
        if (primary == null) return false;
        return !IsSavedPositionAvailable(primary);
    }

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
            string text = "“" + q.Text + "”";
            int padX = 18;
            int padTop = Math.Max(26, (int)Math.Round(currentFontSize * 1.0f) + 8);
            float gap = Math.Max(6f, currentFontSize * 0.35f);
            float padBottom = Math.Max(14f, currentFontSize * 0.6f);

            Screen host = GetCurrentScreen();
            Rectangle scr = host.WorkingArea;

            int cardW;
            if (customWidth > 0)
            {
                cardW = Math.Max(260, Math.Min(scr.Width - 40, customWidth));
            }
            else
            {
                int defaultW = (int)Math.Round(scr.Width * 0.40);
                cardW = Math.Max(320, Math.Min(scr.Width - 40, defaultW));
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
            if (isProjectedNote)
            {
                btnCloseRect = new Rectangle(cardW - 24, btnY, btnSz, btnSz);
                btnPinRect = Rectangle.Empty;
                btnRefreshRect = Rectangle.Empty;
            }
            else
            {
                btnCloseRect = new Rectangle(cardW - 24, btnY, btnSz, btnSz);
                btnPinRect = new Rectangle(cardW - 46, btnY, btnSz, btnSz);
                btnRefreshRect = new Rectangle(cardW - 68, btnY, btnSz, btnSz);
            }
        }

        bool hasSavedPosition = settings != null && (!IsAutoCenterCoord(settings.QuoteX) || !IsAutoCenterCoord(settings.QuoteY));
        if (!keepLeft || hasSavedPosition || settings == null || IsAutoCenterCoord(settings.QuoteX))
        {
            UpdatePosition();
        }
        RenderLayeredQuoteWindow();
    }

    private void UpdatePosition()
    {
        bool customX = settings != null && !IsAutoCenterCoord(settings.QuoteX);
        bool customY = settings != null && !IsAutoCenterCoord(settings.QuoteY);
        Screen host = GetCurrentScreen();
        Rectangle scr = host.WorkingArea;
        bool savedPositionAvailable = IsSavedPositionAvailable(host);
        int x;
        if (customX && savedPositionAvailable)
        {
            x = settings.QuoteX;
        }
        else
        {
            x = scr.Left + Math.Max(0, (scr.Width - Width) / 2);
        }

        int y;
        if (customY && savedPositionAvailable)
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
            Screen host = GetCurrentScreen();
            Rectangle scr = host.WorkingArea;
            Location = new Point(scr.Left + Math.Max(0, (scr.Width - Width) / 2), scr.Top + 120);
        }
    }

    private void EnterHoverState()
    {
        isHovered = true;
        RenderLayeredQuoteWindow();
    }

    private void ExitHoverState()
    {
        isHovered = false;
        hoveredBtn = 0;
        isResizingWidth = false;
        RenderLayeredQuoteWindow();
        UpdateTopMostState();
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
        WasDismissedByUser = true;
        StartFadeOut();
    }

    public void RenderLayeredQuoteWindow()
    {
        if (!IsHandleCreated || IsDisposed || Width <= 0 || Height <= 0) return;
        bool firstVisibleRender = Visible && !hasRenderedOnce;
        if (firstVisibleRender)
        {
            hasRenderedOnce = true;
            needsFirstVisibleGeometrySync = true;
        }

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
        string colStr = (settings != null && !string.IsNullOrEmpty(settings.QuoteColor)) ? settings.QuoteColor : "#F77F00";
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
            string text = "“" + q.Text + "”";
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

        AppContext.PremultiplyBitmap(renderBitmap, ref premulBuffer);

        byte alpha = (byte)Math.Max(0, Math.Min(255, (int)(currentOpacity * 255)));
        AppContext.UpdateLayered(Left, Top, Width, Height, Handle, renderBitmap, alpha);
        if (firstVisibleRender)
        {
            // On a negative-coordinate monitor, the first layered bitmap can
            // retain stale screen geometry even though Form.Location is right.
            // Reassert the native bounds once after the first visible update.
            NativeMethods.SetWindowPos(Handle, IntPtr.Zero, Left, Top, Width, Height, 0x0020);
            BeginInvoke(new Action(delegate
            {
                if (IsDisposed || !IsHandleCreated || !needsFirstVisibleGeometrySync) return;
                needsFirstVisibleGeometrySync = false;
                NativeMethods.SetWindowPos(Handle, IntPtr.Zero, Left, Top, Width, Height, 0x0020);
                RenderLayeredQuoteWindow();
            }));
        }

    }

    private void DrawActionButtons(Graphics g, Color accentColor)
    {
        if (!isProjectedNote)
        {
            DrawMicroButton(g, btnRefreshRect, 1, accentColor);
            DrawMicroButton(g, btnPinRect, 2, accentColor);
        }
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
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                // Top bar (ceiling = "topmost layer")
                g.DrawLine(p, cx - 4.5f, cy - 5.0f, cx + 4.5f, cy - 5.0f);
                // Arrow pointing up toward the bar
                g.DrawLine(p, cx - 3.0f, cy + 0.5f, cx, cy - 2.5f);
                g.DrawLine(p, cx + 3.0f, cy + 0.5f, cx, cy - 2.5f);
                // Arrow shaft
                g.DrawLine(p, cx, cy + 0.5f, cx, cy + 5.0f);
            }
            if (isPinned)
            {
                using (SolidBrush b = new SolidBrush(pinCol))
                {
                    PointF[] tri = { new PointF(cx, cy - 2.5f), new PointF(cx - 3.0f, cy + 0.5f), new PointF(cx + 3.0f, cy + 0.5f) };
                    g.FillPolygon(b, tri);
                }
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
                ? ((lang == "en") ? "Unpin from top" : "取消置顶")
                : ((lang == "en") ? "Pin on top" : "始终置顶");
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
            int newX = formStart.X + (Cursor.Position.X - dragStart.X);
            int newY = formStart.Y + (Cursor.Position.Y - dragStart.Y);

            // Snap to screen center lines (real-time, with visual feedback)
            Rectangle screen = GetCurrentScreen().WorkingArea;
            int centerX = screen.Left + screen.Width / 2;
            int centerY = screen.Top + screen.Height / 2;
            int windowCenterX = newX + Width / 2;
            int windowCenterY = newY + Height / 2;
            const int snapThreshold = 24;

            bool snapH = Math.Abs(windowCenterX - centerX) <= snapThreshold;
            bool snapV = Math.Abs(windowCenterY - centerY) <= snapThreshold;

            if (snapH) newX = centerX - Width / 2;
            if (snapV) newY = centerY - Height / 2;

            Location = new Point(newX, newY);
            if (snapH || snapV) RenderLayeredQuoteWindow(); // redraw to show snap state

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
            return;
        }

        if (isResizingWidth)
        {
            int deltaX = Cursor.Position.X - dragStartX;
            int minW = 260;
            int maxW = GetCurrentScreen().WorkingArea.Width - 40;
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
            if (settings != null && !isProjectedNote)
            {
                settings.QuoteWidth = customWidth;
                if (!IsAutoCenterCoord(settings.QuoteX)) settings.QuoteX = Location.X;
                if (!IsAutoCenterCoord(settings.QuoteY)) settings.QuoteY = Location.Y;
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
            HideSnapGuide();
            if (settings != null)
            {
                Screen host = GetCurrentScreen();
                Rectangle scr = host.WorkingArea;
                int centerX = scr.Left + Math.Max(0, (scr.Width - Width) / 2);
                if (host.Primary && Math.Abs(Location.X - centerX) <= 24)
                {
                    settings.QuoteX = -1; // auto-center marker
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

    private void ShowSnapGuide()
    {
        if (snapGuideForm == null)
        {
            snapGuideForm = AppContext.CreateSnapGuide(OnSnapGuidePaint);
        }
        Rectangle scr = GetCurrentScreen().WorkingArea;
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
            if (fadeTimer != null) { fadeTimer.Stop(); fadeTimer.Dispose(); fadeTimer = null; }
            if (renderGraphics != null) { renderGraphics.Dispose(); renderGraphics = null; }
            if (renderBitmap != null) { renderBitmap.Dispose(); renderBitmap = null; }

            if (settings != null && !isProjectedNote)
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

    internal static string BuildAgentPrompt(List<string> keywords, int count, string lang)
    {
        bool en = (lang == "en");
        if (count <= 0) count = 30;
        string t = (keywords != null && keywords.Count > 0)
            ? string.Join(en ? ", " : "、", keywords.ToArray())
            : (en ? "Deep focus, craftsmanship and tranquility" : "极客心流、斯多葛与深度专注");
        string cliPath = CliPromptHelper.GetCliExePath().Replace("'", "''");
        if (en)
        {
            return "Please curate and compose " + count + " inspiring desk quotes based on the preference topics: [" + t + "].\r\n" +
                   "Save one quote per line in a UTF-8 text file, each line exactly `Quote text | Author or Source`. Then import it using this PowerShell command:\r\n" +
                   "$quotesPath = Join-Path $env:TEMP 'iclock-quotes.txt'\r\n" +
                   "@'\r\n<put all " + count + " quote lines here>\r\n'@ | Set-Content -Encoding UTF8 $quotesPath\r\n" +
                   "& '" + cliPath + "' --quote-add $quotesPath --json\r\n" +
                   "Parse the JSON and report success only if `ok` is true; include `result.added_count`.\r\n\r\n" +
                   "STRICT RULES:\r\n" +
                   "1. Maximum 35 words per quote, inspiring, timeless and concise.\r\n" +
                   "2. Every quote MUST include authentic author or source attribution.\r\n" +
                   "3. Execute the import command and wait for its result. No introductory explanations.";
        }
        else
        {
            return "请围绕偏好标签【" + t + "】精选创作 " + count + " 条高质量桌面金句。\r\n" +
                   "请将金句保存为 UTF-8 文本文件，每行一条，严格使用 `金句内容 | 作者或出处` 格式，然后在 PowerShell 执行以下命令导入：\r\n" +
                   "$quotesPath = Join-Path $env:TEMP 'iclock-quotes.txt'\r\n" +
                   "@'\r\n<在此放入全部 " + count + " 行金句>\r\n'@ | Set-Content -Encoding UTF8 $quotesPath\r\n" +
                   "& '" + cliPath + "' --quote-add $quotesPath --json\r\n" +
                   "解析 JSON；只有 `ok` 为 true 才表示导入成功，并报告 `result.added_count`。\r\n\r\n" +
                   "严格规范：\r\n" +
                   "1. 每条金句控制在 35 字以内，语言精练有力，富有启发与哲理；\r\n" +
                   "2. 每条金句必须附带真实作者或典籍出处（若佚名请注明佚名）；\r\n" +
                   "3. 必须执行导入命令并等待返回结果，严禁多余的打招呼或寒暄废话。";
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
            error = "未解析到有效的新金句";
            return false;
        }

        string filePath = Settings.QuotesFilePath;
        try
        {
            AppContext.EnsureQuotesFileExists();

            HashSet<string> existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int maxNumber = 0;
            int validQuoteCount = 0;

            if (File.Exists(filePath))
            {
                foreach (string rawLine in File.ReadAllLines(filePath, Encoding.UTF8))
                {
                    string trimmed = rawLine.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith("//")) continue;

                    validQuoteCount++;

                    // Extract sequence number if present (e.g., "1. ", "02、", "No.3: ", "[4] ")
                    Match m = Regex.Match(trimmed, @"^(?:(?:No\.?|#)\s*|[\(\[\（\【])?(\d{1,6})");
                    if (m.Success)
                    {
                        int n;
                        if (int.TryParse(m.Groups[1].Value, out n))
                        {
                            if (n > maxNumber) maxNumber = n;
                        }
                    }

                    string t = QuoteItem.StripLeadingNumber(trimmed).Trim();
                    string[] parts = t.Split(new string[] { " | ", "|", " —— ", "——", " — ", "—", " -- " }, StringSplitOptions.RemoveEmptyEntries);
                    string text = parts.Length > 0 ? parts[0].Trim() : t;
                    if (!string.IsNullOrEmpty(text)) existing.Add(text);
                }

                try
                {
                    string bak = filePath + ".bak";
                    File.Copy(filePath, bak, true);
                }
                catch { }
            }

            int nextIndex = Math.Max(maxNumber, validQuoteCount) + 1;
            List<string> linesToAppend = new List<string>();
            foreach (QuoteItem qi in newQuotes)
            {
                if (string.IsNullOrEmpty(qi.Text)) continue;
                if (qi.Text.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0 && qi.Text.IndexOfAny(new char[] { '"', ':', '{', '}' }) >= 0) continue;
                if (existing.Contains(qi.Text)) continue;

                string prefix = nextIndex + ". ";
                string line = string.IsNullOrEmpty(qi.Author)
                    ? prefix + qi.Text
                    : prefix + qi.Text + " —— " + qi.Author;
                linesToAppend.Add(line);
                existing.Add(qi.Text);
                nextIndex++;
            }

            if (linesToAppend.Count == 0)
            {
                error = "所生成的金句已存在于词库中";
                return false;
            }

            if (File.Exists(filePath))
            {
                string curText = File.ReadAllText(filePath, Encoding.UTF8);
                if (!string.IsNullOrEmpty(curText) && !curText.EndsWith("\n") && !curText.EndsWith("\r"))
                {
                    File.AppendAllText(filePath, Environment.NewLine, Encoding.UTF8);
                }
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

}

internal sealed class AiQuoteDialog : Form
{
    private Settings settings;
    private List<string> currentKeywords;
    private string currentPrompt;
    private int currentQuoteCount = 30;

    // View panels
    private Panel panelSetup;
    private TextBox txtPreference;
    private NumericUpDown nudQuoteCount;
    private Label lblHint;
    private ToolTip tipCount;
    private Button btnCancel;
    private Button btnGenerate;

    private Panel panelStatus;
    private Label lblStatusTitle;
    private Panel cardContainer;
    private Label lblCardMain;
    private Label lblCardSub;
    private Label lblCardBadge;

    private Button btnStatusAction1; // Copy again / Open library
    private Button btnStatusAction2; // Back to setup
    private Button btnStatusBack;    // unused placeholder (kept for layout symmetry)
    private Button btnStatusDone;    // Close / Done

    private System.Windows.Forms.Timer timerStatus;
    private int elapsedSeconds = 0;

    private enum ViewMode { Setup, WaitingImport, Completed }
    private ViewMode currentMode = ViewMode.Setup;

    public AiQuoteDialog(Settings s)
    {
        settings = s;
        string lang = s != null ? s.Language : "zh";
        bool en = (lang == "en");

        currentKeywords = AiAgentQuoteHelper.GetKeywords(s);

        Font = AppContext.GetUiFont(lang, 9.5f);
        BackColor = AppContext.Win11Bg;
        AppContext.ApplyModernWindowStyle(this);
        Text = en ? "AI Quote Library Expansion" : "AI 扩充我的金句";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ShowIcon = false;
        // Compact on purpose: three fields do not need a tall window, and the
        // status view reuses the same box for its own card.
        ClientSize = new Size(380, 280);

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
        if (tipCount != null)
        {
            tipCount.Dispose();
            tipCount = null;
        }
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
            if (currentMode == ViewMode.WaitingImport)
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
        lblHeader.Text = en ? "✨ Expand Daily Quotes with AI" : "✨ AI 扩充我的金句";
        lblHeader.Font = AppContext.GetUiFont(lang, 11f, FontStyle.Bold);
        lblHeader.ForeColor = AppContext.Win11Accent;
        lblHeader.SetBounds(18, 12, 344, 22);
        panelSetup.Controls.Add(lblHeader);

        Label lblTags = new Label();
        lblTags.Text = en ? "Preference description or keywords:" : "偏好描述或关键词：";
        lblTags.Font = AppContext.GetUiFont(lang, 9f);
        lblTags.ForeColor = AppContext.Win11TextPrimary;
        lblTags.SetBounds(18, 36, 344, 18);
        panelSetup.Controls.Add(lblTags);

        txtPreference = new TextBox();
        txtPreference.Multiline = true;
        txtPreference.AcceptsReturn = true;
        txtPreference.ScrollBars = ScrollBars.Vertical;
        txtPreference.Font = AppContext.GetUiFont(lang, 9f);
        // Without the agent picker this column has one input and one number, so
        // the keyword box gets the room the page has, not more.
        txtPreference.SetBounds(18, 56, 344, 96);
        txtPreference.Text = currentKeywords != null && currentKeywords.Count > 0
            ? string.Join(Environment.NewLine, currentKeywords.ToArray())
            : "";
        panelSetup.Controls.Add(txtPreference);

        Label lblCount = new Label();
        lblCount.Text = en ? "Quotes to generate:" : "扩充数量：";
        lblCount.Font = AppContext.GetUiFont(lang, 9f);
        lblCount.ForeColor = AppContext.Win11TextPrimary;
        lblCount.SetBounds(18, 168, 90, 18);
        panelSetup.Controls.Add(lblCount);

        nudQuoteCount = new NumericUpDown();
        nudQuoteCount.Minimum = 5;
        nudQuoteCount.Maximum = 100;
        nudQuoteCount.Increment = 5;
        nudQuoteCount.Font = AppContext.GetUiFont(lang, 9f);
        int savedCount = settings != null && settings.AiQuoteCount > 0 ? settings.AiQuoteCount : 30;
        nudQuoteCount.Value = Math.Max(5, Math.Min(100, savedCount));
        nudQuoteCount.SetBounds(112, 168, 80, 24);
        panelSetup.Controls.Add(nudQuoteCount);

        // The range lives in the tooltip: the row reads as one sentence
        // (「扩充数量 30 条」) instead of carrying a parenthetical.
        tipCount = new ToolTip();
        tipCount.SetToolTip(nudQuoteCount, en ? "5 to 100 quotes per run" : "每次可扩充 5–100 条金句");

        Label lblCountUnit = new Label();
        lblCountUnit.Text = en ? "quotes" : "条";
        lblCountUnit.Font = AppContext.GetUiFont(lang, 8.5f);
        lblCountUnit.ForeColor = AppContext.Win11TextSecondary;
        lblCountUnit.SetBounds(198, 168, 164, 18);
        panelSetup.Controls.Add(lblCountUnit);

        // No agent picker: iClock does not run the agent, it only hands over an
        // instruction and waits for the quotes to come back through the iClock
        // command line. Which agent you paste into is your business, so the
        // dialog does not list them.
        lblHint = new Label();
        lblHint.Text = en
            ? "Copy the instruction, paste it into an agent chat (Ctrl+V), send."
            : "复制指令，粘贴到智能体对话框（Ctrl+V）发送后自动入库。";
        lblHint.ForeColor = AppContext.Win11TextSecondary;
        lblHint.Font = AppContext.GetUiFont(lang, 8.5f);
        lblHint.SetBounds(18, 200, 344, 20);
        lblHint.AutoEllipsis = true;
        panelSetup.Controls.Add(lblHint);

        btnCancel = new Button();
        btnCancel.Text = en ? "Cancel" : "取消";
        btnCancel.SetBounds(180, 234, 80, 30);
        AppContext.StyleSecondaryButton(btnCancel);
        btnCancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
        panelSetup.Controls.Add(btnCancel);

        btnGenerate = new Button();
        // The button copies an instruction; the agent does the generating.
        btnGenerate.Text = en ? "Copy Prompt" : "复制指令";
        btnGenerate.SetBounds(266, 234, 96, 30);
        AppContext.StylePrimaryButton(btnGenerate);
        btnGenerate.Click += OnGenerateClick;
        panelSetup.Controls.Add(btnGenerate);
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
        lblStatusTitle.SetBounds(18, 12, 344, 22);
        panelStatus.Controls.Add(lblStatusTitle);

        // The card stops above the same 234px footer baseline the setup view
        // uses, so the primary action never moves between the two views.
        cardContainer = new Panel();
        cardContainer.SetBounds(18, 38, 344, 180);
        cardContainer.BackColor = Color.FromArgb(246, 248, 250);
        cardContainer.Paint += delegate(object sender, PaintEventArgs pe)
        {
            using (Pen p = new Pen(Color.FromArgb(228, 232, 236)))
            {
                pe.Graphics.DrawRectangle(p, 0, 0, cardContainer.Width - 1, cardContainer.Height - 1);
                // Divider above the pinned status line: it turns the card's
                // spare space into a content area over a footer.
                pe.Graphics.DrawLine(p, 0, 136, cardContainer.Width - 1, 136);
            }
        };
        panelStatus.Controls.Add(cardContainer);

        lblCardMain = new Label();
        lblCardMain.Text = "";
        lblCardMain.Font = AppContext.GetUiFont(lang, 9.5f, FontStyle.Bold);
        lblCardMain.ForeColor = AppContext.Win11TextPrimary;
        lblCardMain.SetBounds(14, 12, 316, 24);
        cardContainer.Controls.Add(lblCardMain);

        lblCardSub = new Label();
        lblCardSub.Text = "";
        lblCardSub.Font = AppContext.GetUiFont(lang, 9f);
        lblCardSub.ForeColor = AppContext.Win11TextSecondary;
        lblCardSub.SetBounds(14, 42, 316, 84);
        cardContainer.Controls.Add(lblCardSub);

        lblCardBadge = new Label();
        lblCardBadge.Text = "";
        lblCardBadge.Font = AppContext.GetUiFont(lang, 8.5f);
        lblCardBadge.ForeColor = AppContext.Win11Accent;
        lblCardBadge.SetBounds(14, 144, 316, 22);
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

    private void ShowSetupView()
    {
        currentMode = ViewMode.Setup;
        if (timerStatus != null) timerStatus.Stop();
        panelStatus.Visible = false;
        panelSetup.Visible = true;
        AcceptButton = btnGenerate;
        CancelButton = btnCancel;
    }

    private void ShowCopiedPromptView()
    {
        bool en = (settings != null && settings.Language == "en");
        currentMode = ViewMode.WaitingImport;
        panelSetup.Visible = false;
        panelStatus.Visible = true;

        // States what iClock did (copied the instruction) and what the user has
        // to do with it. It cannot see inside the agent, so it says nothing
        // about what the agent is doing.
        lblStatusTitle.Text = en ? "✨ Instruction copied" : "✨ 指令已复制到剪贴板";
        lblCardMain.Text = en
            ? string.Format("✓ Instruction for {0} quotes copied!", currentQuoteCount)
            : string.Format("✓ {0} 条金句的扩充指令已复制！", currentQuoteCount);
        lblCardMain.ForeColor = Color.FromArgb(16, 124, 65);
        lblCardSub.Text = en
            ? "① Paste it into any agent chat and send.\r\n② The agent runs the iClock command line to import the quotes.\r\n③ The result is reported here as soon as it arrives."
            : "① 粘贴到任意智能体对话框并发送；\r\n② 智能体会执行 iClock 命令行导入金句；\r\n③ 导入完成后将在此当场汇报。";
        lblCardSub.ForeColor = AppContext.Win11TextSecondary;
        lblCardBadge.Text = en ? "• Waiting for import..." : "• 正在等待导入...";
        lblCardBadge.ForeColor = AppContext.Win11Accent;

        btnStatusAction1.Visible = true;
        btnStatusAction1.Text = en ? "Copy Again" : "重新复制";
        btnStatusAction1.SetBounds(18, 234, 110, 30);
        AppContext.StyleSecondaryButton(btnStatusAction1);

        btnStatusAction2.Visible = true;
        btnStatusAction2.Text = en ? "❮ Back" : "❮ 返回修改";
        btnStatusAction2.SetBounds(136, 234, 110, 30);
        AppContext.StyleSecondaryButton(btnStatusAction2);

        btnStatusBack.Visible = false;
        btnStatusBack.Text = en ? "❮ Back" : "❮ 返回修改";
        btnStatusBack.SetBounds(136, 234, 110, 30);
        AppContext.StyleSecondaryButton(btnStatusBack);

        btnStatusDone.Visible = true;
        btnStatusDone.Text = en ? "Close" : "关闭";
        btnStatusDone.SetBounds(266, 234, 96, 30);
        AppContext.StylePrimaryButton(btnStatusDone);

        AcceptButton = null;
        CancelButton = btnStatusDone;

        elapsedSeconds = 0;
        if (timerStatus != null) timerStatus.Start();
    }

    private void ShowCompletedView(int count, QuoteItem sample)
    {
        bool en = (settings != null && settings.Language == "en");
        currentMode = ViewMode.Completed;
        if (timerStatus != null) timerStatus.Stop();

        panelSetup.Visible = false;
        panelStatus.Visible = true;

        lblStatusTitle.Text = en ? "✨ Quotes Expansion Completed!" : "✨ 金句扩充完成！";
        lblCardMain.Text = en
            ? string.Format("Successfully added {0} new quotes to library!", count)
            : string.Format("已成功将 {0} 条全新精选金句收录至金句库！", count);
        lblCardMain.ForeColor = Color.FromArgb(16, 124, 65);

        string preview = (sample != null && !string.IsNullOrEmpty(sample.Text))
            ? (string.IsNullOrEmpty(sample.Author) ? ("“" + sample.Text + "”") : ("“" + sample.Text + "”\r\n—— " + sample.Author))
            : (en ? "New quotes have been added to the library." : "新金句已加入金句库并刷新。");
        lblCardSub.Text = preview;
        lblCardSub.ForeColor = AppContext.Win11TextPrimary;

        lblCardBadge.Text = en ? "✨ Latest quote projected to desktop display" : "✨ 最新金句已同步投递至桌面每日金句展示";
        lblCardBadge.ForeColor = Color.FromArgb(16, 124, 65);

        btnStatusAction1.Visible = true;
        btnStatusAction1.Text = en ? "Open Library" : "查看金句库";
        btnStatusAction1.SetBounds(18, 234, 120, 30);
        AppContext.StyleSecondaryButton(btnStatusAction1);

        btnStatusAction2.Visible = false;
        btnStatusBack.Visible = false;

        btnStatusDone.Visible = true;
        btnStatusDone.Text = en ? "Done" : "完成";
        btnStatusDone.SetBounds(266, 234, 96, 30);
        AppContext.StylePrimaryButton(btnStatusDone);

        AcceptButton = btnStatusDone;
        CancelButton = btnStatusDone;
    }

    private void OnTimerStatusTick(object sender, EventArgs e)
    {
        bool en = (settings != null && settings.Language == "en");
        if (currentMode == ViewMode.WaitingImport)
        {
            elapsedSeconds++;
            lblCardBadge.Text = en
                ? string.Format("• Waiting for import... ({0}s)", elapsedSeconds)
                : string.Format("• 正在等待导入... (已等待 {0} 秒)", elapsedSeconds);
        }
    }

    private void OnStatusAction1Click(object sender, EventArgs e)
    {
        bool en = (settings != null && settings.Language == "en");
        if (currentMode == ViewMode.WaitingImport)
        {
            CopyPromptToClipboard();
            btnStatusAction1.Text = en ? "Copied ✓" : "已复制 ✓";
        }
        else if (currentMode == ViewMode.Completed)
        {
            AppContext.OpenQuotesFile();
        }
    }

    private void OnStatusAction2Click(object sender, EventArgs e)
    {
        ShowSetupView();
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
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }

    private void OnGenerateClick(object sender, EventArgs e)
    {
        string lang = settings != null ? settings.Language : "zh";

        // Persist preference text (GetKeywords splits newlines/commas on next open).
        string rawPreference = txtPreference != null ? txtPreference.Text.Trim() : "";
        if (settings != null)
        {
            settings.AiQuoteKeywords = rawPreference;
            settings.AiQuoteCount = nudQuoteCount != null ? (int)nudQuoteCount.Value : 30;
            settings.Save();
        }
        currentKeywords = AiAgentQuoteHelper.GetKeywords(settings);
        int quoteCount = settings != null && settings.AiQuoteCount > 0 ? settings.AiQuoteCount : 30;
        currentQuoteCount = quoteCount;

        currentPrompt = AiAgentQuoteHelper.BuildAgentPrompt(currentKeywords, quoteCount, lang);
        CopyPromptToClipboard();
        ShowCopiedPromptView();
    }

    // The one and only way quotes get in: the agent runs iClock's command line
    // after pasting the instruction, and the result comes back over the CLI.
    private void CopyPromptToClipboard()
    {
        try
        {
            Clipboard.SetText(currentPrompt);
        }
        catch { }
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

