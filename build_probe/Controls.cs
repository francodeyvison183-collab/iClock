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

internal sealed class DoubleBufferedPanel : Panel
{
    public DoubleBufferedPanel()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        UpdateStyles();
    }
}

internal sealed class SlimScrollBar : Control
{
    private int maximum = 100;
    private int largeChange = 10;
    private int val = 0;
    private bool isHovered = false;
    private bool isDragging = false;
    private int dragStartY = 0;
    private int dragStartVal = 0;

    public event EventHandler ValueChanged;

    public int Maximum
    {
        get { return maximum; }
        set { maximum = Math.Max(0, value); Invalidate(); }
    }

    public int LargeChange
    {
        get { return largeChange; }
        set { largeChange = Math.Max(1, value); Invalidate(); }
    }

    public int Value
    {
        get { return val; }
        set
        {
            int maxVal = Math.Max(0, maximum - largeChange);
            int clamped = Math.Max(0, Math.Min(maxVal, value));
            if (this.val != clamped)
            {
                this.val = clamped;
                Invalidate();
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }
    }

    public SlimScrollBar()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Color.FromArgb(246, 248, 250);
        Cursor = Cursors.Default;
    }

    private Rectangle GetThumbRect()
    {
        int maxVal = maximum - largeChange;
        if (maxVal <= 0 || Height <= 0) return Rectangle.Empty;
        int trackH = Height;
        int thumbH = Math.Max(24, (int)((float)largeChange / maximum * trackH));
        int travel = trackH - thumbH;
        int thumbY = (int)((float)val / maxVal * travel);
        return new Rectangle(1, thumbY, Math.Max(2, Width - 2), thumbH);
    }

    protected override void OnPaint(PaintEventArgs pe)
    {
        pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle thumb = GetThumbRect();
        if (thumb.IsEmpty) return;
        Color c = isDragging ? Color.FromArgb(115, 122, 133) : (isHovered ? Color.FromArgb(156, 163, 175) : Color.FromArgb(200, 205, 212));
        using (Brush b = new SolidBrush(c))
        using (GraphicsPath gp = AppContext.GetRoundedRectPath(thumb, 2))
        {
            pe.Graphics.FillPath(b, gp);
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        isHovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        isHovered = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            Rectangle thumb = GetThumbRect();
            if (thumb.Contains(e.Location))
            {
                isDragging = true;
                dragStartY = e.Y;
                dragStartVal = val;
                Capture = true;
                Invalidate();
            }
            else if (!thumb.IsEmpty)
            {
                if (e.Y < thumb.Top) Value -= largeChange;
                else if (e.Y > thumb.Bottom) Value += largeChange;
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (isDragging)
        {
            int maxVal = maximum - largeChange;
            if (maxVal > 0)
            {
                int trackH = Height;
                int thumbH = Math.Max(24, (int)((float)largeChange / maximum * trackH));
                int travel = trackH - thumbH;
                if (travel > 0)
                {
                    int deltaY = e.Y - dragStartY;
                    Value = dragStartVal + (int)((float)deltaY / travel * maxVal);
                }
            }
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (isDragging)
        {
            isDragging = false;
            Capture = false;
            Invalidate();
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        Value += (e.Delta < 0 ? 36 : -36);
    }
}


internal sealed class StyledTipLabel : Label
{
    private string[] highlightWords;
    public string[] HighlightWords
    {
        get { return highlightWords; }
        set { highlightWords = value; Invalidate(); }
    }

    public StyledTipLabel(params string[] highlightWords)
    {
        this.highlightWords = highlightWords;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        string text = this.Text;
        if (string.IsNullOrEmpty(text)) return;

        using (Font codeFont = new Font(Font.FontFamily, Font.Size, FontStyle.Italic))
        using (SolidBrush regBrush = new SolidBrush(ForeColor))
        using (SolidBrush codeBrush = new SolidBrush(AppContext.Win11Accent))
        using (StringFormat sf = new StringFormat(StringFormat.GenericTypographic))
        {
            sf.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;

            List<Token> tokens = new List<Token>();
            int cur = 0;
            string[] words = highlightWords;

            while (cur < text.Length)
            {
                int matchIdx = -1;
                if (words != null)
                {
                    for (int c = 0; c < words.Length; c++)
                    {
                        string w = words[c];
                        if (!string.IsNullOrEmpty(w) && cur + w.Length <= text.Length && text.Substring(cur, w.Length) == w)
                        {
                            matchIdx = c;
                            break;
                        }
                    }
                }

                if (matchIdx >= 0)
                {
                    tokens.Add(new Token(words[matchIdx], true));
                    cur += words[matchIdx].Length;
                    continue;
                }

                char ch = text[cur];
                bool isCjk = (ch >= 0x4E00 && ch <= 0x9FFF) || ch == '，' || ch == '。' || ch == '；' || ch == '：' || ch == '、';
                if (isCjk)
                {
                    tokens.Add(new Token(ch.ToString(), false));
                    cur++;
                }
                else
                {
                    int start = cur;
                    while (cur < text.Length)
                    {
                        bool nextIsWord = false;
                        if (words != null)
                        {
                            for (int c = 0; c < words.Length; c++)
                            {
                                string w = words[c];
                                if (!string.IsNullOrEmpty(w) && cur + w.Length <= text.Length && text.Substring(cur, w.Length) == w)
                                {
                                    nextIsWord = true;
                                    break;
                                }
                            }
                        }
                        if (nextIsWord) break;

                        char cChar = text[cur];
                        bool nextIsCjk = (cChar >= 0x4E00 && cChar <= 0x9FFF) || cChar == '，' || cChar == '。' || cChar == '；' || cChar == '：' || cChar == '、';
                        if (nextIsCjk) break;

                        cur++;
                        if (cChar == ' ') break;
                    }
                    tokens.Add(new Token(text.Substring(start, cur - start), false));
                }
            }

            float x = ClientRectangle.Left;
            float y = ClientRectangle.Top;
            float lineHeight = Font.GetHeight(g) + 3f;
            float maxX = ClientRectangle.Right;

            for (int i = 0; i < tokens.Count; i++)
            {
                Token tok = tokens[i];
                Font f = tok.IsCode ? codeFont : Font;
                SolidBrush b = tok.IsCode ? codeBrush : regBrush;

                SizeF sz = g.MeasureString(tok.Text, f, PointF.Empty, sf);
                float neededWidth = sz.Width;
                if (i + 1 < tokens.Count)
                {
                    string nextText = tokens[i + 1].Text;
                    if (nextText == "" || nextText == "" || nextText == "" || nextText == "" || nextText == "")
                    {
                        neededWidth += g.MeasureString(nextText, tokens[i + 1].IsCode ? codeFont : Font, PointF.Empty, sf).Width;
                    }
                }

                if (x + neededWidth > maxX && x > ClientRectangle.Left)
                {
                    x = ClientRectangle.Left;
                    y += lineHeight;
                }

                g.DrawString(tok.Text, f, b, new PointF(x, y), sf);
                x += sz.Width + (tok.IsCode && !tok.Text.EndsWith("") ? 2.5f : 0f);
            }
        }
    }

    private struct Token
    {
        public string Text;
        public bool IsCode;
        public Token(string text, bool isCode) { Text = text; IsCode = isCode; }
    }
}

internal sealed class ToggleSwitch : CheckBox
{
    public ToggleSwitch()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        AccessibleRole = AccessibleRole.CheckButton;
        AutoSize = false;
        Text = "";
        Size = new Size(40, 22);
        TabStop = true;
    }

    protected override void OnCheckedChanged(EventArgs e)
    {
        base.OnCheckedChanged(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent != null ? Parent.BackColor : BackColor);
        Rectangle track = new Rectangle(1, 2, Width - 3, Height - 5);
        int diameter = track.Height;
        using (GraphicsPath path = new GraphicsPath())
        {
            path.AddArc(track.X, track.Y, diameter, diameter, 90, 180);
            path.AddArc(track.Right - diameter, track.Y, diameter, diameter, 270, 180);
            path.CloseFigure();
            Color trackColor = !Enabled ? Color.FromArgb(220, 222, 226)
                : Checked ? AppContext.Win11Accent : Color.FromArgb(190, 196, 204);
            using (SolidBrush brush = new SolidBrush(trackColor)) e.Graphics.FillPath(brush, path);
        }
        int knobSize = diameter - 4;
        int knobX = Checked ? track.Right - knobSize - 2 : track.X + 2;
        using (SolidBrush knob = new SolidBrush(Enabled ? Color.White : Color.FromArgb(245, 245, 245)))
            e.Graphics.FillEllipse(knob, knobX, track.Y + 2, knobSize, knobSize);
        if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, ClientRectangle);
    }
}

// 设置页数字框：WinForms 会按字体把高度锁成 24，这里固定成与下拉框相同的 28。
internal class SettingsNumberBox : NumericUpDown
{
    public const int BoxHeight = 28;
    private bool lockingHeight;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        LockHeight();
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        LockHeight();
    }

    private void LockHeight()
    {
        if (lockingHeight || !IsHandleCreated || Height == BoxHeight) return;
        lockingHeight = true;
        try
        {
            NativeMethods.SetWindowPos(Handle, IntPtr.Zero, 0, 0, Width, BoxHeight, 0x0016);
        }
        catch { }
        lockingHeight = false;
    }
}

