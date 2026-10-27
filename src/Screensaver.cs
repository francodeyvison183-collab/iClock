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

internal sealed class QuoteScreensaverForm : Form
{
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_MOUSEMOVE = 0x0200;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_RBUTTONDOWN = 0x0206;
    private const int WM_XBUTTONDOWN = 0x020B;
    private const int WM_MOUSEWHEEL = 0x020A;

    private readonly Settings saverSettings;
    private readonly List<QuoteItem> saverQuotes;
    private readonly int saverQuoteIndex;
    // Mouse-exit is armed shortly after the window shows: the stale mousemove
    // that arrives with activation must not kill the saver (real saver or the
    // settings "preview" — the preview is simply this same form launched now).
    private bool saverMouseArmed;
    private Timer saverArmTimer;

    public QuoteScreensaverForm()
    {
        saverSettings = Settings.Load();
        saverQuotes = DailyQuoteDialog.LoadQuotes();
        if (saverQuotes == null || saverQuotes.Count == 0) saverQuotes = new List<QuoteItem>(DailyQuoteDialog.DefaultQuotes);
        saverQuoteIndex = AppContext.ResolveTodayQuoteIndex(saverSettings, saverQuotes.Count);

        FormBorderStyle = FormBorderStyle.None;
        WindowState = FormWindowState.Maximized;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.Black;
        DoubleBuffered = true;
        Bounds = Screen.PrimaryScreen.Bounds;
        Cursor.Hide();

        Capture = true;

        saverArmTimer = new Timer();
        saverArmTimer.Interval = 500;
        saverArmTimer.Tick += delegate
        {
            saverArmTimer.Stop();
            saverArmTimer.Dispose();
            saverArmTimer = null;
            saverMouseArmed = true;
        };
        saverArmTimer.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && saverArmTimer != null)
        {
            saverArmTimer.Stop();
            saverArmTimer.Dispose();
            saverArmTimer = null;
        }
        base.Dispose(disposing);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        ForceForeground();
        Program.LogSaverEvent(string.Format("form shown bounds={0} quotes={1} index={2}",
            Bounds, saverQuotes != null ? saverQuotes.Count : 0, saverQuoteIndex));
    }

    private void ForceForeground()
    {
        // The saver must be foreground or input goes to the desktop beneath it.
        try
        {
            NativeMethods.SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0040);
            NativeMethods.SetForegroundWindow(Handle);
            NativeMethods.BringWindowToTop(Handle);
        }
        catch { }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ForceForeground();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        g.Clear(Color.Black);

        QuoteItem q = saverQuotes[saverQuoteIndex % saverQuotes.Count];
        string lang = saverSettings != null ? saverSettings.Language : "zh";
        string text = string.IsNullOrEmpty(q.Text) ? "" : q.Text;
        string author = string.IsNullOrEmpty(q.Author) ? "" : q.Author;

        string colStr = !string.IsNullOrEmpty(saverSettings.QuoteColor) ? saverSettings.QuoteColor : "#F77F00";
        Color mainColor;
        try { mainColor = ColorTranslator.FromHtml(colStr); }
        catch { mainColor = Color.FromArgb(247, 127, 0); }

        // Paint exactly like the desktop quote note (DailyQuoteDialog): same
        // font chain, the configured 文字大小 as-is, the same "plain text +
        // —— author" layout, and the note's default position (centered card
        // anchored at Y=120 below the top). The card rides on the black saver
        // background, so the preview and the real saver are pixel-identical.
        string family = !string.IsNullOrEmpty(saverSettings.QuoteFont)
            ? saverSettings.QuoteFont
            : (!string.IsNullOrEmpty(saverSettings.FontFamily) ? saverSettings.FontFamily : "Segoe UI");
        float setSize = saverSettings != null && saverSettings.QuoteFontSize >= 8.0f && saverSettings.QuoteFontSize <= 24.0f
            ? saverSettings.QuoteFontSize
            : 18.0f;

        Font textFont = null, authorFont = null;
        try
        {
            textFont = new Font(family, setSize, FontStyle.Regular, GraphicsUnit.Point);
        }
        catch { textFont = AppContext.GetUiFont(lang, setSize); }
        try
        {
            authorFont = new Font(family, Math.Max(8.0f, setSize - 1.5f), FontStyle.Italic, GraphicsUnit.Point);
        }
        catch
        {
            try { authorFont = new Font(family, Math.Max(8.0f, setSize - 1.5f), FontStyle.Regular, GraphicsUnit.Point); }
            catch { authorFont = AppContext.GetUiFont(lang, Math.Max(8.0f, setSize - 1.5f)); }
        }

        try
        {
            int padX = 18;
            int padTop = Math.Max(26, (int)Math.Round(setSize * 1.0f) + 8);
            float gap = Math.Max(6f, setSize * 0.35f);
            float padBottom = Math.Max(14f, setSize * 0.6f);

            // Card width mirrors the note default: 40% of the working area,
            // clamped to [320, scr-40]. Long text wraps inside the content width.
            Rectangle scr = Screen.PrimaryScreen.WorkingArea;
            int cardW = Math.Max(320, Math.Min(scr.Width - 40, (int)Math.Round(scr.Width * 0.40)));
            int contentW = cardW - padX * 2;
            SizeF szText = g.MeasureString(text, textFont, contentW);
            int cardH = padTop + (int)Math.Ceiling(szText.Height);
            if (!string.IsNullOrEmpty(author))
            {
                SizeF szAuthor = g.MeasureString("—— " + author, authorFont, contentW);
                cardH += (int)Math.Ceiling(gap + szAuthor.Height);
            }
            cardH += (int)Math.Ceiling(padBottom);
            cardH = Math.Max(56, cardH);

            // Screensaver position: horizontally centered, the text block's
            // vertical CENTER anchored at 38% of the screen height so the
            // quote reads optically balanced and scales across resolutions.
            // Text only on black; no card frame here (the desktop note keeps its).
            int centerAnchoredY = scr.Top + (int)Math.Round(scr.Height * 0.38) - cardH / 2;
            Rectangle card = new Rectangle(scr.Left + (scr.Width - cardW) / 2, centerAnchoredY, cardW, cardH);

            using (StringFormat sf = new StringFormat { Trimming = StringTrimming.Word, Alignment = StringAlignment.Center })
            {
                RectangleF textBounds = new RectangleF(card.Left + padX, card.Top + padTop, contentW, szText.Height);
                float authorY = card.Top + padTop + szText.Height + gap;
                SizeF szAuthor = SizeF.Empty;
                if (!string.IsNullOrEmpty(author))
                {
                    szAuthor = g.MeasureString("—— " + author, authorFont, contentW);
                }
                RectangleF authorBounds = new RectangleF(card.Left + padX, authorY, contentW, szAuthor.Height + 2);

                float brightness = mainColor.GetBrightness();
                float shadowOffset = setSize >= 18f ? 2f : 1f;
                Color shadowColor = (brightness < 0.32f)
                    ? Color.FromArgb(90, 255, 255, 255)
                    : Color.FromArgb(110, 0, 0, 0);

                using (StringFormat asf = new StringFormat { Alignment = StringAlignment.Center })
                using (SolidBrush shadowBrush = new SolidBrush(shadowColor))
                {
                    RectangleF shadowText = new RectangleF(textBounds.X + shadowOffset, textBounds.Y + shadowOffset, textBounds.Width, textBounds.Height);
                    g.DrawString(text, textFont, shadowBrush, shadowText, sf);
                    if (!string.IsNullOrEmpty(author))
                    {
                        RectangleF shadowAuthor = new RectangleF(authorBounds.X + shadowOffset, authorBounds.Y + shadowOffset, authorBounds.Width, authorBounds.Height);
                        g.DrawString("—— " + author, authorFont, shadowBrush, shadowAuthor, asf);
                    }
                }

                using (StringFormat asf = new StringFormat { Alignment = StringAlignment.Center })
                using (SolidBrush textBrush = new SolidBrush(mainColor))
                using (SolidBrush authorBrush = new SolidBrush(Color.FromArgb(190, mainColor.R, mainColor.G, mainColor.B)))
                {
                    g.DrawString(text, textFont, textBrush, textBounds, sf);
                    if (!string.IsNullOrEmpty(author))
                    {
                        g.DrawString("—— " + author, authorFont, authorBrush, authorBounds, asf);
                    }
                }
            }

            DrawBrandMark(g, scr, mainColor);
        }
        finally
        {
            textFont.Dispose();
            authorFont.Dispose();
        }
    }

    // Brand mark low on the saver: the app icon brightened so its dark navy
    // body separates from the black background, sitting on a soft accent halo.
    // Purely decorative — any failure here must never take the quote down.
    private static void DrawBrandMark(Graphics g, Rectangle scr, Color accent)
    {
        try
        {
            const int logoSize = 32;
            int cx = scr.Left + scr.Width / 2;
            int cy = scr.Top + (int)Math.Round(scr.Height * 0.80);

            int haloSize = (int)Math.Round(logoSize * 1.6);
            Rectangle haloRect = new Rectangle(cx - haloSize / 2, cy - haloSize / 2, haloSize, haloSize);
            using (GraphicsPath haloPath = new GraphicsPath())
            {
                haloPath.AddEllipse(haloRect);
                using (PathGradientBrush halo = new PathGradientBrush(haloPath))
                {
                    halo.CenterColor = Color.FromArgb(52, accent.R, accent.G, accent.B);
                    halo.SurroundColors = new Color[] { Color.FromArgb(0, accent.R, accent.G, accent.B) };
                    g.FillPath(halo, haloPath);
                }
            }

            Bitmap logo = LoadBrandLogo(logoSize);
            if (logo == null) return;
            using (logo)
            using (ImageAttributes attr = new ImageAttributes())
            {
                // Linear lift: out = 0.864 * in + 0.136. The dark navy body goes
                // from #1B2340 to about #3A415A while white and the orange slash
                // keep their original values (a plain multiply would blow the
                // orange out to yellow).
                attr.SetColorMatrix(new ColorMatrix(new float[][]
                {
                    new float[] { 0.864f, 0f, 0f, 0f, 0.136f },
                    new float[] { 0f, 0.864f, 0f, 0f, 0.136f },
                    new float[] { 0f, 0f, 0.864f, 0f, 0.136f },
                    new float[] { 0f, 0f, 0f, 1f, 0f },
                    new float[] { 0f, 0f, 0f, 0f, 1f }
                }));
                g.DrawImage(logo,
                    new Rectangle(cx - logoSize / 2, cy - logoSize / 2, logoSize, logoSize),
                    0, 0, logo.Width, logo.Height, GraphicsUnit.Pixel, attr);
            }
        }
        catch { }
    }

    // Prefer the embedded 32px PNG (1:1 at logo size); fall back to the 24px
    // window icon when the build has no PNG resource.
    private static Bitmap LoadBrandLogo(int size)
    {
        try
        {
            using (Stream s = typeof(AppContext).Assembly.GetManifestResourceStream("logo32.png"))
            {
                if (s != null)
                {
                    using (Bitmap src = new Bitmap(s))
                    {
                        return ScaleBitmap(src, size);
                    }
                }
            }
        }
        catch { }
        try
        {
            using (Icon ico = AppContext.CreateIcon())
            {
                using (Bitmap src = ico.ToBitmap())
                {
                    return ScaleBitmap(src, size);
                }
            }
        }
        catch { }
        return null;
    }

    private static Bitmap ScaleBitmap(Bitmap src, int size)
    {
        Bitmap dst = new Bitmap(size, size);
        using (Graphics g2 = Graphics.FromImage(dst))
        {
            g2.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g2.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g2.DrawImage(src, new Rectangle(0, 0, size, size));
        }
        return dst;
    }

    private void ExitSaver()
    {
        Cursor.Show();
        Close();
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case WM_KEYDOWN:
            case WM_SYSKEYDOWN:
                // F-keys and modifier-only presses are excluded? No: standard savers
                // exit on any key; keep it simple and consistent.
                if ((int)m.WParam == (int)Keys.LWin || (int)m.WParam == (int)Keys.RWin) break;
                ExitSaver();
                return;
            case WM_MOUSEMOVE:
                // The stale activation move arrives before the arm timer fires;
                // once armed, any real mouse movement exits, and the settings
                // preview behaves exactly like the real saver.
                if (saverMouseArmed)
                {
                    int lp = m.LParam.ToInt32();
                    int y = (lp >> 16) & 0xFFFF, x = lp & 0xFFFF;
                    if ((short)x != 0 || (short)y != 0)
                    {
                        saverMouseArmed = false;
                        ExitSaver();
                        return;
                    }
                }
                break;
            case WM_LBUTTONDOWN:
            case WM_MBUTTONDOWN:
            case WM_RBUTTONDOWN:
            case WM_XBUTTONDOWN:
            case WM_MOUSEWHEEL:
                ExitSaver();
                return;
            case 0x0010: // WM_CLOSE
                Cursor.Show();
                break;
        }
        base.WndProc(ref m);
    }
}
