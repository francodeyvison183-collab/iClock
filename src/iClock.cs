using System;
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
using Microsoft.Win32;

internal sealed class Settings
{
    public int Minutes = 25;
    public int FontSize = 20;
    public int X = 80;
    public int Y = 80;
    public int HotkeyModifiers = 3;
    public int HotkeyKey = (int)Keys.Space;
    public string Color = "#FF0000";
    public string FontFamily = "Segoe UI";
    public string Format = "HH:MM:SS";
    public string EndMessage = "倒计时结束";
    public string Language = "zh";
    public bool AutoStart;
    public bool EndSound = true;
    public bool EndNotice = true;
    public bool FirstRun;

    public static string FilePath
    {
        get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "iClock", "settings.ini"); }
    }

    public static Settings Load()
    {
        Settings s = new Settings();
        try
        {
            if (!File.Exists(FilePath)) { s.FirstRun = true; return s; }
            foreach (string line in File.ReadAllLines(FilePath))
            {
                int p = line.IndexOf('=');
                if (p < 1) continue;
                string k = line.Substring(0, p).Trim();
                string v = line.Substring(p + 1).Trim();
                int n;
                bool b;
                if (k == "Minutes" && int.TryParse(v, out n)) s.Minutes = Math.Max(1, Math.Min(1440, n));
                else if (k == "FontSize" && int.TryParse(v, out n)) s.FontSize = Math.Max(12, Math.Min(120, n));
                else if (k == "X" && int.TryParse(v, out n)) s.X = n;
                else if (k == "Y" && int.TryParse(v, out n)) s.Y = n;
                else if (k == "HotkeyModifiers" && int.TryParse(v, out n)) s.HotkeyModifiers = n & 7;
                else if (k == "HotkeyKey" && int.TryParse(v, out n) && n > 0) s.HotkeyKey = n;
                else if (k == "Color") s.Color = v;
                else if (k == "FontFamily" && !string.IsNullOrEmpty(v)) s.FontFamily = v;
                else if (k == "Format" && (v == "HH:MM:SS" || v == "MM:SS" || v == "Chinese")) s.Format = v;
                else if (k == "EndMessage") s.EndMessage = v;
                else if (k == "Language" && (v == "zh" || v == "en")) s.Language = v;
                else if (k == "AutoStart" && bool.TryParse(v, out b)) s.AutoStart = b;
                else if (k == "EndSound" && bool.TryParse(v, out b)) s.EndSound = b;
                else if (k == "EndNotice" && bool.TryParse(v, out b)) s.EndNotice = b;
                else if (k == "FirstRun" && bool.TryParse(v, out b)) s.FirstRun = b;
            }
        }
        catch { }
        return s;
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
        File.WriteAllLines(FilePath, new string[] {
            "Minutes=" + Minutes, "FontSize=" + FontSize, "X=" + X, "Y=" + Y,
            "HotkeyModifiers=" + HotkeyModifiers, "HotkeyKey=" + HotkeyKey,
            "Color=" + Color, "FontFamily=" + FontFamily, "Format=" + Format, "EndMessage=" + EndMessage, "Language=" + Language, "AutoStart=" + AutoStart,
            "EndSound=" + EndSound, "EndNotice=" + EndNotice, "FirstRun=" + FirstRun
        });
    }
}

internal sealed class Overlay : Form
{
    private const int WS_EX_TRANSPARENT = 0x20;
    private const int WS_EX_LAYERED = 0x80000;
    private const int WS_EX_TOOLWINDOW = 0x80;
    private const int WM_NCHITTEST = 0x84;
    private const int HTTRANSPARENT = -1;
    private Settings settings;
    private string text;
    private bool moving;
    private Point dragStart;
    private Point formStart;
    private Font cachedFont;
    private SolidBrush cachedBrush;
    private StringFormat cachedFormat;
    public bool MoveMode { get; private set; }
    public event Action<Point> PositionChanged;

    public Overlay(Settings s)
    {
        settings = s;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Fuchsia;
        TransparencyKey = Color.Fuchsia;
        TopMost = true;
        DoubleBuffered = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
        Location = new Point(s.X, s.Y);
        RebuildGdiResources();
        UpdateText("25:00");
        MouseDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseUp += OnMouseUp;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= WS_EX_LAYERED | WS_EX_TOOLWINDOW;
            if (!MoveMode) cp.ExStyle |= WS_EX_TRANSPARENT;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_NCHITTEST && !MoveMode) { m.Result = (IntPtr)HTTRANSPARENT; return; }
        base.WndProc(ref m);
    }

    public void SetSettings(Settings s)
    {
        settings = s;
        RebuildGdiResources();
        Invalidate();
    }

    private void RebuildGdiResources()
    {
        if (cachedFont != null) cachedFont.Dispose();
        if (cachedBrush != null) cachedBrush.Dispose();
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

        string template = settings.Format == "Chinese" ? (settings.Language == "en" ? "00h 00m 00s" : "00时00分00秒") : (settings.Format == "MM:SS" ? "00:00" : "00:00:00");
        using (Bitmap bmp = new Bitmap(1, 1))
        using (Graphics g = Graphics.FromImage(bmp))
        {
            SizeF size = g.MeasureString(template, cachedFont);
            Size = new Size(Math.Max(100, (int)Math.Ceiling(size.Width) + 16), Math.Max(50, (int)Math.Ceiling(size.Height) + 10));
        }
    }

    public void UpdateText(string value)
    {
        if (text == value) return;
        text = value;
        Invalidate();
    }

    public void SetPaused(bool paused)
    {
        if (!MoveMode)
        {
            Opacity = paused ? 0.50 : 1.0;
        }
    }

    public void SetMoveMode(bool enabled, bool isRunning = true)
    {
        MoveMode = enabled;
        if (enabled)
        {
            TransparencyKey = Color.Empty; BackColor = Color.White; Opacity = 0.50; TopMost = true;
        }
        else
        {
            BackColor = Color.Fuchsia; TransparencyKey = Color.Fuchsia; Opacity = isRunning ? 1.0 : 0.50;
        }
        RecreateHandle();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (MoveMode)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = AppContext.GetRoundedRectPath(new Rectangle(1, 1, Width - 3, Height - 3), 6))
            using (Pen borderPen = new Pen(AppContext.Win11Accent, 1.5f))
            {
                e.Graphics.DrawPath(borderPen, path);
            }
        }
        if (text != null && cachedFont != null && cachedBrush != null)
        {
            e.Graphics.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
            e.Graphics.DrawString(text, cachedFont, cachedBrush, ClientRectangle, cachedFormat);
        }
        base.OnPaint(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (cachedFont != null) { cachedFont.Dispose(); cachedFont = null; }
            if (cachedBrush != null) { cachedBrush.Dispose(); cachedBrush = null; }
            if (cachedFormat != null) { cachedFormat.Dispose(); cachedFormat = null; }
        }
        base.Dispose(disposing);
    }

    private void OnMouseDown(object sender, MouseEventArgs e)
    {
        if (!MoveMode || e.Button != MouseButtons.Left) return;
        moving = true; dragStart = Cursor.Position; formStart = Location;
    }
    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (moving) Location = new Point(formStart.X + Cursor.Position.X - dragStart.X, formStart.Y + Cursor.Position.Y - dragStart.Y);
    }
    private void OnMouseUp(object sender, MouseEventArgs e)
    {
        if (!moving) return;
        moving = false;
        if (PositionChanged != null) PositionChanged(Location);
    }
}

internal sealed class SettingsDialog : Form
{
    private NumericUpDown minutes, size;
    private Button colorButton;
    private Panel colorPreview;
    private string selectedColor;
    private TextBox endMessage;
    private ComboBox format, language, fontCombo;
    private CheckBox startup, sound, notice;
    private TextBox hotkeyBox;
    private int hotkeyModifiers, hotkeyKey, candidateMods, candidateKey;
    private Label lblMinutes, lblSize, lblFont, lblColor, lblFormat, lblEndMsg, lblHotkey, lblLanguage;
    private Button saveBtn, cancelBtn;
    public Settings Value { get; private set; }
    private static readonly string[] fontFamilies = { "Segoe UI", "Consolas", "Arial", "Impact", "Microsoft YaHei" };

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }
    private const int EM_SETMARGINS = 0x00D3;
    private const int EM_SETRECTNP = 0x00B4;
    private const int EC_LEFTMARGIN = 0x0001;
    private const int EC_RIGHTMARGIN = 0x0002;

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref RECT lParam);

    private static void UpdateInputPadding(Control ctrl)
    {
        if (ctrl == null) return;
        if (ctrl is TextBox)
        {
            TextBox tb = (TextBox)ctrl;
            if (tb.IsHandleCreated)
            {
                SendMessage(tb.Handle, EM_SETMARGINS, (IntPtr)(EC_LEFTMARGIN | EC_RIGHTMARGIN), (IntPtr)(4 | (4 << 16)));
                if (tb.Multiline)
                {
                    RECT r = new RECT { Left = 4, Top = 4, Right = Math.Max(0, tb.ClientSize.Width - 4), Bottom = Math.Max(0, tb.ClientSize.Height - 4) };
                    SendMessage(tb.Handle, EM_SETRECTNP, IntPtr.Zero, ref r);
                }
            }
        }
        else if (ctrl is NumericUpDown)
        {
            foreach (Control child in ctrl.Controls)
            {
                if (child is TextBox) UpdateInputPadding(child);
            }
        }
    }

    private static void ApplyInputPadding(Control ctrl)
    {
        if (ctrl == null) return;
        IntPtr forceHandle = ctrl.Handle;
        if (ctrl is TextBox)
        {
            TextBox tb = (TextBox)ctrl;
            UpdateInputPadding(tb);
            tb.HandleCreated += (s, e) => UpdateInputPadding(tb);
            tb.FontChanged += (s, e) => UpdateInputPadding(tb);
            tb.SizeChanged += (s, e) => UpdateInputPadding(tb);
        }
        else if (ctrl is NumericUpDown)
        {
            NumericUpDown nud = (NumericUpDown)ctrl;
            foreach (Control child in nud.Controls)
            {
                if (child is TextBox) ApplyInputPadding(child);
            }
            nud.ControlAdded += (s, e) => { if (e.Control is TextBox) ApplyInputPadding(e.Control); };
        }
    }

    public SettingsDialog(Settings s)
    {
        Value = new Settings();
        Value.Minutes = s.Minutes; Value.FontSize = s.FontSize; Value.X = s.X; Value.Y = s.Y;
        Value.HotkeyModifiers = s.HotkeyModifiers; Value.HotkeyKey = s.HotkeyKey;
        Value.Color = s.Color; Value.FontFamily = s.FontFamily; Value.Format = s.Format; Value.EndMessage = s.EndMessage;
        Value.Language = s.Language;
        Value.AutoStart = s.AutoStart; Value.EndSound = s.EndSound; Value.EndNotice = s.EndNotice;
        bool en = Value.Language == "en";
        Font = AppContext.GetUiFont(Value.Language, 9.5f);
        BackColor = AppContext.Win11Bg;
        AppContext.ApplyModernWindowStyle(this);
        Text = en ? "iClock Settings" : "iClock 设置"; FormBorderStyle = FormBorderStyle.FixedDialog; StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false; ClientSize = new Size(380, 440);
        lblMinutes = AddLabel(en ? "Duration (minutes)" : "倒计时（分钟）", 18, 20); minutes = AddNumber(Value.Minutes, 1, 1440, 172, 16);
        lblSize = AddLabel(en ? "Text size" : "文字大小", 18, 56); size = AddNumber(Value.FontSize, 12, 120, 172, 52);
        lblFont = AddLabel(en ? "Font style" : "字体样式", 18, 92);
        fontCombo = new ComboBox(); fontCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        fontCombo.DrawMode = DrawMode.OwnerDrawFixed; fontCombo.ItemHeight = 22;
        fontCombo.SetBounds(172, 88, 188, 24);
        fontCombo.Items.AddRange(en
            ? new object[] { "Segoe UI (Default)", "Consolas (Monospace)", "Arial", "Impact", "Microsoft YaHei" }
            : new object[] { "Segoe UI (默认)", "Consolas (极客等宽)", "Arial", "Impact (醒目粗黑)", "微软雅黑" });
        fontCombo.SelectedIndex = Value.FontFamily == "Consolas" ? 1 :
            (Value.FontFamily == "Arial" ? 2 :
            (Value.FontFamily == "Impact" ? 3 :
            (Value.FontFamily == "Microsoft YaHei" ? 4 : 0)));
        fontCombo.DrawItem += OnDrawFontItem;
        Controls.Add(fontCombo);
        lblColor = AddLabel(en ? "Text color" : "文字颜色", 18, 128); selectedColor = Value.Color;
        colorButton = new Button(); colorButton.Text = en ? "Choose…" : "点击选取…"; colorButton.SetBounds(172, 124, 128, 26);
        AppContext.StyleSecondaryButton(colorButton);
        colorButton.Click += ChooseColor; Controls.Add(colorButton);
        colorPreview = new Panel(); colorPreview.SetBounds(308, 126, 52, 22); colorPreview.BorderStyle = BorderStyle.FixedSingle; UpdateColorPreview(); Controls.Add(colorPreview);
        lblFormat = AddLabel(en ? "Display format" : "显示格式", 18, 164);
        format = new ComboBox();
        format.DropDownStyle = ComboBoxStyle.DropDownList;
        format.DrawMode = DrawMode.OwnerDrawFixed;
        format.ItemHeight = 22;
        format.SetBounds(172, 160, 188, 24);
        format.Items.AddRange(en ? new object[] { "HH:MM:SS", "MM:SS", "H:M:S units (00h 25m 00s)" } : new object[] { "HH:MM:SS", "MM:SS", "中文单位 (00时25分00秒)" });
        format.SelectedIndex = Value.Format == "MM:SS" ? 1 : (Value.Format == "Chinese" ? 2 : 0);
        format.DrawItem += OnDrawComboItem;
        Controls.Add(format);
        hotkeyModifiers = Value.HotkeyModifiers; hotkeyKey = Value.HotkeyKey;
        candidateMods = hotkeyModifiers; candidateKey = hotkeyKey;
        lblEndMsg = AddLabel(en ? "End message" : "结束时弹出消息", 18, 200);
        endMessage = new TextBox();
        endMessage.SetBounds(172, 196, 188, 36);
        endMessage.Multiline = true;
        endMessage.MaxLength = 200;
        endMessage.Text = Value.EndMessage;
        ApplyInputPadding(endMessage);
        Controls.Add(endMessage);
        lblHotkey = AddLabel(en ? "Start/pause hotkey" : "启动/暂停快捷键", 18, 244);
        hotkeyBox = new TextBox();
        hotkeyBox.ReadOnly = true;
        hotkeyBox.SetBounds(172, 240, 188, 24);
        hotkeyBox.Text = AppContext.HotkeyText(hotkeyModifiers, hotkeyKey);
        hotkeyBox.KeyDown += CaptureHotkey;
        ApplyInputPadding(hotkeyBox);
        Controls.Add(hotkeyBox);
        lblLanguage = AddLabel(en ? "Interface language" : "界面语言", 18, 280);
        language = new ComboBox();
        language.DropDownStyle = ComboBoxStyle.DropDownList;
        language.DrawMode = DrawMode.OwnerDrawFixed;
        language.ItemHeight = 22;
        language.SetBounds(172, 276, 188, 24);
        language.Items.AddRange(new object[] { "简体中文", "English" });
        language.SelectedIndex = Value.Language == "en" ? 1 : 0;
        language.SelectedIndexChanged += OnLanguageChanged;
        language.DrawItem += OnDrawComboItem;
        Controls.Add(language);
        startup = AddCheck(en ? "Start with Windows" : "开机自启动", Value.AutoStart, 18, 310);
        sound = AddCheck(en ? "Play sound at end" : "结束时声音提醒", Value.EndSound, 18, 336);
        notice = AddCheck(en ? "Show notification at end" : "结束时系统通知", Value.EndNotice, 18, 362);

        Label footerLine = new Label(); footerLine.BackColor = AppContext.Win11Border; footerLine.BorderStyle = BorderStyle.None; footerLine.SetBounds(18, 388, 344, 1); Controls.Add(footerLine);
        saveBtn = new Button(); saveBtn.Text = en ? "Save" : "保存"; saveBtn.SetBounds(196, 398, 76, 28);
        AppContext.StylePrimaryButton(saveBtn);
        saveBtn.Click += SaveClick; Controls.Add(saveBtn);
        cancelBtn = new Button(); cancelBtn.Text = en ? "Cancel" : "取消"; cancelBtn.SetBounds(284, 398, 76, 28);
        AppContext.StyleSecondaryButton(cancelBtn);
        cancelBtn.DialogResult = DialogResult.Cancel; Controls.Add(cancelBtn);
        AcceptButton = saveBtn; CancelButton = cancelBtn;
    }

    private void OnLanguageChanged(object sender, EventArgs e)
    {
        bool toEnglish = language.SelectedIndex == 1;
        Font = AppContext.GetUiFont(toEnglish ? "en" : "zh", 9.5f);
        Text = toEnglish ? "iClock Settings" : "iClock 设置";
        if (toEnglish && endMessage.Text == "倒计时结束") endMessage.Text = "Countdown finished";
        else if (!toEnglish && endMessage.Text == "Countdown finished") endMessage.Text = "倒计时结束";

        lblMinutes.Text = toEnglish ? "Duration (minutes)" : "倒计时（分钟）";
        lblSize.Text = toEnglish ? "Text size" : "文字大小";
        lblFont.Text = toEnglish ? "Font style" : "字体样式";
        lblColor.Text = toEnglish ? "Text color" : "文字颜色";
        colorButton.Text = toEnglish ? "Choose…" : "点击选取…";
        lblFormat.Text = toEnglish ? "Display format" : "显示格式";
        lblEndMsg.Text = toEnglish ? "End message" : "结束时弹出消息";
        lblHotkey.Text = toEnglish ? "Start/pause hotkey" : "启动/暂停快捷键";
        lblLanguage.Text = toEnglish ? "Interface language" : "界面语言";
        startup.Text = toEnglish ? "Start with Windows" : "开机自启动";
        sound.Text = toEnglish ? "Play sound at end" : "结束时声音提醒";
        notice.Text = toEnglish ? "Show notification at end" : "结束时系统通知";
        saveBtn.Text = toEnglish ? "Save" : "保存";
        cancelBtn.Text = toEnglish ? "Cancel" : "取消";

        int sel = format.SelectedIndex;
        format.Items.Clear();
        format.Items.AddRange(toEnglish
            ? new object[] { "HH:MM:SS", "MM:SS", "H:M:S units (00h 25m 00s)" }
            : new object[] { "HH:MM:SS", "MM:SS", "中文单位 (00时25分00秒)" });
        format.SelectedIndex = sel >= 0 ? sel : 0;
        int fontSel = fontCombo.SelectedIndex;
        fontCombo.Items.Clear();
        fontCombo.Items.AddRange(toEnglish
            ? new object[] { "Segoe UI (Default)", "Consolas (Monospace)", "Arial", "Impact", "Microsoft YaHei" }
            : new object[] { "Segoe UI (默认)", "Consolas (极客等宽)", "Arial", "Impact (醒目粗黑)", "微软雅黑" });
        fontCombo.SelectedIndex = fontSel >= 0 ? fontSel : 0;
        if (saveBtn.Enabled)
        {
            hotkeyBox.Text = AppContext.HotkeyText(hotkeyModifiers, hotkeyKey);
            hotkeyBox.ForeColor = AppContext.Win11TextPrimary;
        }
        else if (candidateMods == 0 && !((candidateKey >= (int)Keys.F1 && candidateKey <= (int)Keys.F12) || candidateKey == (int)Keys.Pause || candidateKey == (int)Keys.Scroll))
        {
            hotkeyBox.Text = toEnglish ? "Use a modifier + key" : "请按修饰键 + 按键";
            hotkeyBox.ForeColor = Color.FromArgb(202, 80, 16);
        }
        else
        {
            hotkeyBox.Text = AppContext.HotkeyText(candidateMods, candidateKey) + (toEnglish ? " (Occupied)" : " (已被占用)");
            hotkeyBox.ForeColor = Color.FromArgb(202, 80, 16);
        }

        UpdateInputPadding(minutes);
        UpdateInputPadding(size);
        UpdateInputPadding(endMessage);
        UpdateInputPadding(hotkeyBox);
    }

    private Label AddLabel(string t, int x, int y) { Label l = new Label(); l.Text = t; l.ForeColor = AppContext.Win11TextPrimary; l.SetBounds(x, y, 148, 24); l.TextAlign = ContentAlignment.MiddleLeft; Controls.Add(l); return l; }
    private NumericUpDown AddNumber(int v, int min, int max, int x, int y) { NumericUpDown n = new NumericUpDown(); n.Minimum = min; n.Maximum = max; n.Value = Math.Min(max, Math.Max(min, v)); n.SetBounds(x, y, 188, 24); ApplyInputPadding(n); Controls.Add(n); return n; }
    private CheckBox AddCheck(string t, bool v, int x, int y) { CheckBox c = new CheckBox(); c.Text = t; c.Checked = v; c.ForeColor = AppContext.Win11TextPrimary; c.SetBounds(x, y, 344, 24); Controls.Add(c); return c; }
    private void ChooseColor(object sender, EventArgs e)
    {
        Color initial; try { initial = ColorTranslator.FromHtml(selectedColor); } catch { initial = Color.Red; }
        using (ColorDialog d = new ColorDialog())
        {
            d.Color = initial; d.AllowFullOpen = true; d.FullOpen = true; d.AnyColor = true;
            if (d.ShowDialog(this) == DialogResult.OK) { selectedColor = ColorTranslator.ToHtml(d.Color); UpdateColorPreview(); }
        }
    }
    private void UpdateColorPreview()
    {
        try { colorPreview.BackColor = ColorTranslator.FromHtml(selectedColor); } catch { colorPreview.BackColor = Color.Red; }
    }
    private void CaptureHotkey(object sender, KeyEventArgs e)
    {
        Keys key = e.KeyCode;
        if (key == Keys.Escape && (e.Modifiers & (Keys.Control | Keys.Alt | Keys.Shift)) == 0) return;
        e.SuppressKeyPress = true; e.Handled = true;
        if (key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu || key == Keys.LWin || key == Keys.RWin) return;
        int mods = 0;
        if ((e.Modifiers & Keys.Control) != 0) mods |= 2;
        if ((e.Modifiers & Keys.Alt) != 0) mods |= 1;
        if ((e.Modifiers & Keys.Shift) != 0) mods |= 4;
        bool isFunctionKey = (key >= Keys.F1 && key <= Keys.F12) || key == Keys.Pause || key == Keys.Scroll;
        candidateMods = mods; candidateKey = (int)key;
        if (mods == 0 && !isFunctionKey)
        {
            hotkeyBox.Text = language.SelectedIndex == 1 ? "Use a modifier + key" : "请按修饰键 + 按键";
            hotkeyBox.ForeColor = Color.FromArgb(202, 80, 16);
            saveBtn.Enabled = false;
            return;
        }
        string text = AppContext.HotkeyText(mods, (int)key);
        bool isCurrent = (mods == Value.HotkeyModifiers && (int)key == Value.HotkeyKey);
        bool available = isCurrent || AppContext.ProbeHotkey(mods, (int)key);
        if (!available)
        {
            hotkeyBox.Text = text + (language.SelectedIndex == 1 ? " (Occupied)" : " (已被占用)");
            hotkeyBox.ForeColor = Color.FromArgb(202, 80, 16);
            saveBtn.Enabled = false;
        }
        else
        {
            hotkeyModifiers = mods; hotkeyKey = (int)key;
            hotkeyBox.Text = text;
            hotkeyBox.ForeColor = AppContext.Win11TextPrimary;
            saveBtn.Enabled = true;
        }
    }
    private void OnDrawFontItem(object sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= fontCombo.Items.Count) return;
        string family = e.Index < fontFamilies.Length ? fontFamilies[e.Index] : "Segoe UI";
        Font f = null;
        try { f = new Font(family, e.Font.Size, FontStyle.Regular, e.Font.Unit); } catch { }
        DrawComboItem(e, fontCombo.Items[e.Index].ToString(), f ?? e.Font);
        if (f != null) f.Dispose();
    }
    private void OnDrawComboItem(object sender, DrawItemEventArgs e)
    {
        ComboBox cb = sender as ComboBox;
        if (cb == null || e.Index < 0 || e.Index >= cb.Items.Count) return;
        DrawComboItem(e, cb.Items[e.Index].ToString(), e.Font);
    }
    private static void DrawComboItem(DrawItemEventArgs e, string text, Font font)
    {
        e.DrawBackground();
        e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        using (SolidBrush b = new SolidBrush(e.ForeColor))
        using (StringFormat sf = new StringFormat { LineAlignment = StringAlignment.Center })
        {
            Rectangle r = new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height);
            e.Graphics.DrawString(text, font, b, r, sf);
        }
        e.DrawFocusRectangle();
    }
    private void SaveClick(object sender, EventArgs e)
    {
        if (startup.Checked && !Value.AutoStart && AppContext.IsInEphemeralFolder(Application.ExecutablePath))
        {
            bool toEnglish = language.SelectedIndex == 1;
            string msg = toEnglish
                ? "iClock is currently located in a temporary or download folder:\r\n" + Application.ExecutablePath + "\r\n\r\nIt is recommended to move iClock.exe to a permanent folder (such as Documents or Tools) before enabling auto-start, to prevent accidental deletion during cleanup.\r\n\r\nDo you still want to enable auto-start?"
                : "检测到当前程序位于临时或下载目录：\r\n" + Application.ExecutablePath + "\r\n\r\n建议将 iClock.exe 移动到固定文件夹（如个人工具目录或文档）后再开启自启，以防清理下载文件时误删。\r\n\r\n是否仍要开启开机自启？";
            string caption = toEnglish ? "iClock Auto-Start Tip" : "iClock 开机自启提示";
            if (MessageBox.Show(this, msg, caption, MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.No)
            {
                startup.Checked = false;
                return;
            }
        }
        Value.Minutes = (int)minutes.Value; Value.FontSize = (int)size.Value; Value.Color = selectedColor;
        Value.FontFamily = fontCombo.SelectedIndex >= 0 && fontCombo.SelectedIndex < fontFamilies.Length
            ? fontFamilies[fontCombo.SelectedIndex]
            : "Segoe UI";
        Value.HotkeyModifiers = hotkeyModifiers; Value.HotkeyKey = hotkeyKey;
        Value.EndMessage = endMessage.Text.Trim();
        Value.Format = format.SelectedIndex == 1 ? "MM:SS" : (format.SelectedIndex == 2 ? "Chinese" : "HH:MM:SS");
        Value.Language = language.SelectedIndex == 1 ? "en" : "zh";
        Value.AutoStart = startup.Checked; Value.EndSound = sound.Checked; Value.EndNotice = notice.Checked;
        DialogResult = DialogResult.OK; Close();
    }
}

internal sealed class NoticeDialog : Form
{
    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private const uint SWP_FLAGS = 0x0043;
    private Settings settings;
    private SoundPlayer player;
    private Timer timer;
    private DateTime finishTime;
    private Label finishLabel;

    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr hWnd);

    public NoticeDialog(Settings s, DateTime finishedAt)
    {
        settings = s;
        finishTime = finishedAt;
        bool en = s.Language == "en";
        Font = AppContext.GetUiFont(s.Language, 9.5f);
        BackColor = AppContext.Win11Bg;
        AppContext.ApplyModernWindowStyle(this);
        Text = "iClock";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        TopMost = true;
        ClientSize = new Size(350, 215);

        string titleText = String.IsNullOrEmpty(s.EndMessage) ? (en ? "Countdown Finished" : "倒计时结束") : s.EndMessage;
        Label titleLabel = new Label();
        titleLabel.Text = titleText;
        titleLabel.Font = AppContext.GetUiFont(s.Language, 13f, FontStyle.Bold);
        titleLabel.ForeColor = AppContext.Win11TextPrimary;
        titleLabel.TextAlign = ContentAlignment.MiddleCenter;
        titleLabel.SetBounds(20, 18, 310, 28);
        Controls.Add(titleLabel);

        string timeText = s.Format == "MM:SS" ? "00:00" :
            (s.Format == "Chinese" ? (en ? "00h 00m 00s" : "00时00分00秒") : "00:00:00");
        Label timeLabel = new Label();
        timeLabel.Text = timeText;
        timeLabel.Font = AppContext.GetUiFont(s.Language, 22f, FontStyle.Bold);
        timeLabel.ForeColor = AppContext.Win11Accent;
        timeLabel.TextAlign = ContentAlignment.MiddleCenter;
        timeLabel.SetBounds(20, 52, 310, 42);
        Controls.Add(timeLabel);

        finishLabel = new Label();
        finishLabel.Font = AppContext.GetUiFont(s.Language, 9.5f, FontStyle.Regular);
        finishLabel.ForeColor = AppContext.Win11TextSecondary;
        finishLabel.TextAlign = ContentAlignment.MiddleCenter;
        finishLabel.SetBounds(20, 102, 310, 22);
        Controls.Add(finishLabel);
        UpdateFinishLabel();

        Button btn = new Button();
        btn.Text = en ? "OK" : "确定";
        btn.Font = AppContext.GetUiFont(s.Language, 9.5f, FontStyle.Regular);
        btn.SetBounds(120, 150, 110, 36);
        AppContext.StylePrimaryButton(btn);
        btn.Click += delegate { Close(); };
        Controls.Add(btn);
        AcceptButton = btn;
        CancelButton = btn;

        timer = new Timer();
        timer.Interval = 1000;
        timer.Tick += OnTimerTick;
    }

    private void UpdateFinishLabel()
    {
        bool en = settings.Language == "en";
        int secs = (int)Math.Max(0, (DateTime.Now - finishTime).TotalSeconds);
        string elapsed = (secs / 60).ToString("00") + ":" + (secs % 60).ToString("00");
        finishLabel.Text = (en ? "Finished at: " : "结束时间: ") + finishTime.ToString("HH:mm:ss") + (secs > 0 ? " (+" + elapsed + ")" : "");
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ForceTopMost();
        StartAlarm();
        if (timer != null) timer.Start();
    }

    public void ForceTopMost()
    {
        try
        {
            TopMost = true;
            BringToFront();
            Activate();
            SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_FLAGS);
            SetForegroundWindow(Handle);
            BringWindowToTop(Handle);
        }
        catch { }
    }

    private void OnTimerTick(object sender, EventArgs e)
    {
        UpdateFinishLabel();
        ForceTopMost();
        if (settings.EndSound && player == null)
        {
            try { SystemSounds.Exclamation.Play(); } catch { }
        }
    }

    private void StartAlarm()
    {
        if (!settings.EndSound) return;
        try
        {
            string wav = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Media", "Alarm01.wav");
            if (File.Exists(wav))
            {
                player = new SoundPlayer(wav);
                player.PlayLooping();
                return;
            }
        }
        catch { }
        try { SystemSounds.Exclamation.Play(); } catch { }
    }

    private void StopAlarm()
    {
        try
        {
            if (player != null) { player.Stop(); player.Dispose(); player = null; }
            if (timer != null) { timer.Stop(); timer.Dispose(); timer = null; }
        }
        catch { }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        StopAlarm();
    }
}

internal sealed class WelcomeDialog : Form
{
    public bool StartRequested { get; private set; }

    public WelcomeDialog(Settings s)
    {
        bool en = s.Language == "en";
        Font = AppContext.GetUiFont(s.Language, 9.5f);
        BackColor = AppContext.Win11Bg;
        AppContext.ApplyModernWindowStyle(this);
        Text = "iClock";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        ShowIcon = false;
        ClientSize = new Size(380, 208);

        Label lblTitle = new Label();
        lblTitle.Text = en ? "Welcome to iClock" : "欢迎使用 iClock";
        lblTitle.Font = AppContext.GetUiFont(s.Language, 13.5f, FontStyle.Bold);
        lblTitle.ForeColor = AppContext.Win11Accent;
        lblTitle.TextAlign = ContentAlignment.MiddleCenter;
        lblTitle.SetBounds(0, 16, ClientSize.Width, 26);
        Controls.Add(lblTitle);

        Label lblPrompt = new Label();
        lblPrompt.Text = en ? "Press shortcut to start or pause:" : "按下快捷键启动或暂停倒计时：";
        lblPrompt.Font = AppContext.GetUiFont(s.Language, 9.5f, FontStyle.Regular);
        lblPrompt.ForeColor = AppContext.Win11TextPrimary;
        lblPrompt.TextAlign = ContentAlignment.MiddleCenter;
        lblPrompt.SetBounds(0, 52, ClientSize.Width, 20);
        Controls.Add(lblPrompt);

        string hotkey = AppContext.HotkeyText(s.HotkeyModifiers, s.HotkeyKey);
        Label badge = new Label();
        badge.Text = hotkey;
        badge.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
        badge.ForeColor = AppContext.Win11Accent;
        badge.BackColor = Color.White;
        badge.TextAlign = ContentAlignment.MiddleCenter;
        int badgeWidth = 120;
        using (Graphics g = CreateGraphics())
        {
            SizeF sz = g.MeasureString(hotkey, badge.Font);
            badgeWidth = Math.Max(70, (int)Math.Ceiling(sz.Width) + 24);
        }
        badge.SetBounds((ClientSize.Width - badgeWidth) / 2, 78, badgeWidth, 28);
        badge.Paint += delegate(object sender, PaintEventArgs pe)
        {
            pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen p = new Pen(Color.FromArgb(209, 213, 219), 1.2f))
            {
                pe.Graphics.DrawRectangle(p, 0, 0, badge.Width - 1, badge.Height - 1);
            }
        };
        Controls.Add(badge);

        Panel trayPanel = new Panel();
        trayPanel.SetBounds(0, 116, ClientSize.Width, 22);
        trayPanel.BackColor = Color.Transparent;

        string t1 = en ? "Right-click system tray icon" : "右键系统托盘图标";
        string t2 = en ? "for settings" : "可进行设置";
        Font trayFont = AppContext.GetUiFont(s.Language, 8.5f, FontStyle.Regular);

        Label lblT1 = new Label();
        lblT1.AutoSize = true;
        lblT1.Text = t1;
        lblT1.Font = trayFont;
        lblT1.ForeColor = AppContext.Win11TextSecondary;

        Label lblT2 = new Label();
        lblT2.AutoSize = true;
        lblT2.Text = t2;
        lblT2.Font = trayFont;
        lblT2.ForeColor = AppContext.Win11TextSecondary;

        int iconSize = 16, gap = 4;
        int totalTrayW = lblT1.PreferredWidth + gap + iconSize + gap + lblT2.PreferredWidth;
        int trayStartX = (ClientSize.Width - totalTrayW) / 2;

        lblT1.Location = new Point(trayStartX, 2);
        trayPanel.Controls.Add(lblT1);

        PictureBox trayIcon = new PictureBox();
        trayIcon.SetBounds(trayStartX + lblT1.PreferredWidth + gap, 3, iconSize, iconSize);
        trayIcon.SizeMode = PictureBoxSizeMode.Zoom;
        try
        {
            Icon appIcon = AppContext.CreateIcon();
            if (appIcon != null) trayIcon.Image = appIcon.ToBitmap();
        }
        catch { }
        trayPanel.Controls.Add(trayIcon);

        lblT2.Location = new Point(trayStartX + lblT1.PreferredWidth + gap + iconSize + gap, 2);
        trayPanel.Controls.Add(lblT2);

        Controls.Add(trayPanel);

        int totalBtnWidth = 126 + 12 + 86;
        int startX = (ClientSize.Width - totalBtnWidth) / 2;

        Button btnStart = new Button();
        btnStart.Text = en ? "Start Countdown" : "开始倒计时";
        btnStart.Font = AppContext.GetUiFont(s.Language, 9.5f, FontStyle.Regular);
        btnStart.SetBounds(startX, 154, 126, 34);
        AppContext.StylePrimaryButton(btnStart);
        btnStart.Click += delegate { StartRequested = true; Close(); };
        Controls.Add(btnStart);

        Button btnOk = new Button();
        btnOk.Text = en ? "Got it" : "知道了";
        btnOk.Font = AppContext.GetUiFont(s.Language, 9.5f, FontStyle.Regular);
        btnOk.SetBounds(startX + 126 + 12, 154, 86, 34);
        AppContext.StyleSecondaryButton(btnOk);
        btnOk.Click += delegate { Close(); };
        Controls.Add(btnOk);

        AcceptButton = btnStart;
        CancelButton = btnOk;

        KeyPreview = true;
        KeyDown += delegate(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space)
            {
                e.Handled = true;
                StartRequested = true;
                Close();
            }
        };
    }
}

internal class AboutDialog : Form
{
    private AppContext app;
    private string language;
    private Button btnCheck;
    private Label lblVersion;

    private sealed class HqPictureBox : PictureBox
    {
        protected override void OnPaint(PaintEventArgs pe)
        {
            pe.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            pe.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            base.OnPaint(pe);
        }
    }

    public AboutDialog(string lang) : this(lang, null) { }

    public AboutDialog(string lang, AppContext appContext)
    {
        language = lang;
        app = appContext;
        bool en = language == "en";
        Font = AppContext.GetUiFont(language, 9.5f);
        BackColor = AppContext.Win11Bg;
        AppContext.ApplyModernWindowStyle(this);
        Text = en ? "About iClock" : "关于 iClock";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(350, 508);

        Label lblTitle = new Label();
        lblTitle.Text = "iClock";
        lblTitle.Font = AppContext.GetUiFont(language, 14f, FontStyle.Bold);
        lblTitle.ForeColor = AppContext.Win11Accent;
        lblTitle.SetBounds(20, 16, 140, 26);
        Controls.Add(lblTitle);

        Label lblDesc = new Label();
        lblDesc.Text = en ? "Desktop Floating Countdown Timer" : "桌面极简悬浮倒计时工具";
        lblDesc.Font = AppContext.GetUiFont(language, 9f, FontStyle.Regular);
        lblDesc.ForeColor = AppContext.Win11TextSecondary;
        lblDesc.SetBounds(20, 44, 310, 20);
        Controls.Add(lblDesc);

        lblVersion = new Label();
        lblVersion.Text = (en ? "Version: v" : "当前版本: v") + AppContext.CURRENT_VERSION;
        lblVersion.Font = AppContext.GetUiFont(language, 9.5f, FontStyle.Regular);
        lblVersion.ForeColor = AppContext.Win11TextPrimary;
        lblVersion.SetBounds(20, 72, 180, 24);
        lblVersion.TextAlign = ContentAlignment.MiddleLeft;
        Controls.Add(lblVersion);

        btnCheck = new Button();
        btnCheck.Text = en ? "Check Updates" : "检查更新";
        btnCheck.Font = AppContext.GetUiFont(language, 9f, FontStyle.Regular);
        btnCheck.SetBounds(210, 70, 120, 26);
        AppContext.StyleSecondaryButton(btnCheck);
        btnCheck.Click += OnCheckUpdates;
        Controls.Add(btnCheck);

        LinkLabel link = new LinkLabel();
        link.Text = "GitHub: francodeyvison183-collab/iClock";
        link.Font = AppContext.GetUiFont(language, 9f, FontStyle.Regular);
        link.LinkColor = AppContext.Win11Accent;
        link.SetBounds(20, 102, 310, 20);
        link.LinkClicked += delegate { try { Process.Start("https://github.com/francodeyvison183-collab/iClock"); } catch { } };
        Controls.Add(link);

        Label line = new Label();
        line.BackColor = AppContext.Win11Border;
        line.BorderStyle = BorderStyle.None;
        line.SetBounds(20, 128, 310, 1);
        Controls.Add(line);

        Label lblSponsor = new Label();
        lblSponsor.Text = en ? "If iClock helps you, thank you for supporting!" : "如果 iClock 对你有帮助，欢迎赞赏支持！";
        lblSponsor.Font = AppContext.GetUiFont(language, 9.5f, FontStyle.Regular);
        lblSponsor.ForeColor = AppContext.Win11TextPrimary;
        lblSponsor.TextAlign = ContentAlignment.MiddleCenter;
        lblSponsor.SetBounds(20, 138, 310, 24);
        Controls.Add(lblSponsor);

        HqPictureBox pic = new HqPictureBox();
        pic.SetBounds(25, 166, 300, 300);
        pic.SizeMode = PictureBoxSizeMode.Zoom;
        pic.BorderStyle = BorderStyle.None;
        pic.Image = LoadSponsorImage();
        Controls.Add(pic);

        Label sub = new Label();
        sub.Text = en ? "WeChat Pay" : "微信扫一扫 赞赏码";
        sub.Font = AppContext.GetUiFont(language, 9.5f, FontStyle.Bold);
        sub.ForeColor = AppContext.Win11Accent;
        sub.TextAlign = ContentAlignment.MiddleCenter;
        sub.SetBounds(20, 472, 310, 20);
        Controls.Add(sub);

        KeyPreview = true;
        KeyDown += delegate(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                Close();
            }
        };
    }

    private void OnCheckUpdates(object sender, EventArgs e)
    {
        bool en = language == "en";
        btnCheck.Enabled = false;
        btnCheck.Text = en ? "Checking..." : "检查中...";

        System.Threading.ThreadPool.QueueUserWorkItem(delegate
        {
            string latest, url;
            bool success = AppContext.QueryLatestVersion("check", out latest, out url);

            try
            {
                if (IsDisposed || !IsHandleCreated) return;
                BeginInvoke(new MethodInvoker(delegate
                {
                    btnCheck.Enabled = true;
                    btnCheck.Text = en ? "Check Updates" : "检查更新";

                    if (!success || string.IsNullOrEmpty(latest))
                    {
                        MessageBox.Show(this, en ? "Failed to check for updates. Please check your network." : "检查更新失败，请检查网络连接。", "iClock", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    if (AppContext.IsNewer(latest, AppContext.CURRENT_VERSION))
                    {
                        string targetUrl = string.IsNullOrEmpty(url) ? "https://github.com/francodeyvison183-collab/iClock/releases/latest" : url;
                        if (app != null) app.NotifyUpdateFound(latest, targetUrl);
                        string msg = en ? "New version " + latest + " is available! Do you want to download it now?" : "发现新版本 " + latest + "！是否立即前往下载？";
                        if (MessageBox.Show(this, msg, "iClock", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                        {
                            try { Process.Start(targetUrl); } catch { }
                        }
                    }
                    else
                    {
                        MessageBox.Show(this, en ? "You are using the latest version (v" + AppContext.CURRENT_VERSION + ")." : "当前已是最新版本 (v" + AppContext.CURRENT_VERSION + ")。", "iClock", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }));
            }
            catch { }
        });
    }

    private static Image LoadSponsorImage()
    {
        try
        {
            Stream stream = typeof(AppContext).Assembly.GetManifestResourceStream("zan.jpg");
            if (stream != null)
            {
                using (stream)
                using (Image temp = Image.FromStream(stream))
                {
                    return new Bitmap(temp);
                }
            }
        }
        catch { }
        try
        {
            string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "src", "zan.jpg");
            if (File.Exists(localPath))
            {
                using (Image temp = Image.FromFile(localPath))
                {
                    return new Bitmap(temp);
                }
            }
            localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "zan.jpg");
            if (File.Exists(localPath))
            {
                using (Image temp = Image.FromFile(localPath))
                {
                    return new Bitmap(temp);
                }
            }
        }
        catch { }
        return null;
    }
}

internal sealed class ModernMenuRenderer : ToolStripProfessionalRenderer
{
    public ModernMenuRenderer() : base(new ModernMenuColorTable()) { }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Enabled) return;
        if (e.Item.Selected)
        {
            int w = (e.ToolStrip != null && e.ToolStrip.ClientSize.Width > 0) ? e.ToolStrip.ClientSize.Width : e.Item.Width;
            Rectangle rc = new Rectangle(4, 2, w - 8, e.Item.Height - 4);
            using (GraphicsPath path = AppContext.GetRoundedRectPath(rc, 4))
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(238, 238, 238)))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillPath(brush, path);
            }
        }
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        int menuWidth = (e.ToolStrip != null && e.ToolStrip.ClientSize.Width > 0) ? e.ToolStrip.ClientSize.Width : e.Item.Width;
        int left = 32;
        int right = menuWidth - 24;
        int width = Math.Max(0, right - left);
        e.TextRectangle = new Rectangle(left, 0, width, e.Item.Height);

        ToolStripMenuItem mi = e.Item as ToolStripMenuItem;
        bool isShortcut = mi != null && !string.IsNullOrEmpty(mi.ShortcutKeyDisplayString) && e.Text == mi.ShortcutKeyDisplayString;

        if (isShortcut)
        {
            e.TextColor = AppContext.Win11ShortcutGray;
            e.TextFormat = TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
        }
        else
        {
            e.TextFormat = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
            if (e.Item.Enabled && !e.Item.ForeColor.IsEmpty && e.Item.ForeColor != SystemColors.ControlText && e.Item.ForeColor != AppContext.Win11TextPrimary)
                e.TextColor = e.Item.ForeColor;
            else if (e.Item.Enabled)
                e.TextColor = AppContext.Win11TextPrimary;
        }
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        int y = e.Item.Height / 2;
        int w = (e.ToolStrip != null && e.ToolStrip.ClientSize.Width > 0) ? e.ToolStrip.ClientSize.Width : e.Item.Width;
        using (Pen p = new Pen(AppContext.Win11Border, 1f))
        {
            e.Graphics.DrawLine(p, 32, y, w - 32, y);
        }
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using (Pen p = new Pen(AppContext.Win11Border, 1f))
        {
            e.Graphics.DrawRectangle(p, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }
    }
}

internal sealed class ModernMenuColorTable : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground { get { return AppContext.Win11Bg; } }
    public override Color ImageMarginGradientBegin { get { return AppContext.Win11Bg; } }
    public override Color ImageMarginGradientMiddle { get { return AppContext.Win11Bg; } }
    public override Color ImageMarginGradientEnd { get { return AppContext.Win11Bg; } }
    public override Color MenuBorder { get { return AppContext.Win11Border; } }
    public override Color MenuItemBorder { get { return Color.Transparent; } }
}

internal sealed class AppContext : ApplicationContext
{
    private const int HOTKEY_ID = 0x4A10;
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_NOREPEAT = 0x4000;
    private Settings settings;
    private Overlay overlay;
    private NotifyIcon tray;
    private ToolStripMenuItem menuStart, menuReset, menuMove, menuHistory, menuSettings, menuAbout, menuExit, menuUpdate;
    internal const string CURRENT_VERSION = "1.02";
    private string updateUrl;
    private string latestVersion;
    private Timer timer;
    private TimeSpan remaining;
    private long deadlineTimestamp;
    private DateTime sessionStart;
    private int sessionMinutes;
    private bool sessionActive;
    private bool running;
    private bool hotkeyRegistered;
    private int activeHotkeyModifiers, activeHotkeyKey;
    private NoticeDialog activeNotice;
    private long lastSeconds = -1;
    private MessageWindow messageWindow;
    private string exePath = Application.ExecutablePath;
    private Icon appIcon;

    public AppContext()
    {
        settings = Settings.Load();
        appIcon = CreateIcon();
        overlay = new Overlay(settings); overlay.PositionChanged += delegate(Point p) { settings.X = p.X; settings.Y = p.Y; settings.Save(); };
        IntPtr forceOverlayHandle = overlay.Handle;
        messageWindow = new MessageWindow(this);
        activeHotkeyModifiers = 3; activeHotkeyKey = (int)Keys.Space;
        bool hotkeyOccupiedOnStartup = false;
        if (!SetHotkey(settings.HotkeyModifiers, settings.HotkeyKey))
        {
            hotkeyOccupiedOnStartup = true;
            settings.HotkeyModifiers = 3; settings.HotkeyKey = (int)Keys.Space;
            SetHotkey(3, (int)Keys.Space);
        }
        tray = new NotifyIcon(); tray.Icon = appIcon; tray.Text = "iClock"; tray.Visible = true; tray.ContextMenuStrip = MakeMenu();
        tray.BalloonTipClicked += delegate { if (!string.IsNullOrEmpty(updateUrl)) { try { Process.Start(updateUrl); } catch { } } };
        timer = new Timer(); timer.Interval = 100; timer.Tick += Tick;
        ApplyStartup();
        EnsureStartMenuShortcut(false);
        ResetDisplay();
        TrackAndCheckUpdates();
        if (hotkeyOccupiedOnStartup)
        {
            bool en = settings.Language == "en";
            string tipText = hotkeyRegistered
                ? (en ? "Configured hotkey was occupied by another app. Switched to Ctrl+Alt+Space." : "设定的快捷键已被其他程序占用，已自动切换为默认快捷键 Ctrl+Alt+Space。")
                : (en ? "Hotkey registration failed (occupied by another app). Please set a new hotkey in Settings." : "快捷键注册失败（已被其他程序占用），请在设置中重新指定快捷键。");
            tray.ShowBalloonTip(4000, "iClock", tipText, ToolTipIcon.Warning);
        }
        if (settings.FirstRun)
        {
            overlay.BeginInvoke(new Action(ShowWelcome));
        }
    }

    private void ShowWelcome()
    {
        if (!settings.FirstRun) return;
        settings.FirstRun = false;
        settings.Save();
        EnsureStartMenuShortcut(true);
        using (WelcomeDialog dlg = new WelcomeDialog(settings))
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
        ContextMenuStrip m = new ContextMenuStrip();
        m.Font = GetUiFont(settings.Language, 9.5f);
        m.Renderer = new ModernMenuRenderer();
        m.ShowImageMargin = false;
        m.ShowCheckMargin = false;
        m.MinimumSize = new Size(260, 0);
        m.Padding = new Padding(2, 14, 2, 6);
        m.Opened += delegate { ApplyModernWindowStyle(m); };
        m.Opening += delegate
        {
            UpdateMenuText();
            int maxNeeded = 260;
            foreach (ToolStripItem it in m.Items)
            {
                ToolStripMenuItem mi = it as ToolStripMenuItem;
                if (mi == null) continue;
                int tw = TextRenderer.MeasureText(mi.Text, mi.Font).Width;
                int scw = string.IsNullOrEmpty(mi.ShortcutKeyDisplayString) ? 0 : TextRenderer.MeasureText(mi.ShortcutKeyDisplayString, m.Font).Width;
                int needed = 32 + tw + (scw > 0 ? 28 + scw : 0) + 24;
                if (needed > maxNeeded) maxNeeded = needed;
            }
            m.MinimumSize = new Size(maxNeeded, 0);
        };

        menuStart = AddMenuItem(m, delegate { Toggle(); });
        menuStart.Font = new Font(m.Font, FontStyle.Bold);
        menuReset = AddMenuItem(m, delegate { Reset(); });
        menuMove = AddMenuItem(m, delegate { ToggleMove(); });
        m.Items.Add(new ToolStripSeparator());
        menuHistory = AddMenuItem(m, delegate { ShowHistory(); });
        menuSettings = AddMenuItem(m, delegate { ShowSettings(); });
        menuAbout = AddMenuItem(m, delegate { ShowAbout(); });
        m.Items.Add(new ToolStripSeparator());
        menuExit = AddMenuItem(m, delegate { Exit(); });
        UpdateMenuText();
        return m;
    }

    private void Reset()
    {
        if (activeNotice != null && !activeNotice.IsDisposed) { activeNotice.Close(); activeNotice = null; }
        if (running) remaining = ReadRemaining();
        running = false;
        timer.Stop();
        if (sessionActive) LogSession("Reset");
        sessionActive = false;
        ResetDisplay();
        overlay.SetPaused(false);
        overlay.Hide();
        UpdateMenuText();
    }

    private ToolStripMenuItem AddMenuItem(ContextMenuStrip m, EventHandler onClick)
    {
        ToolStripMenuItem item = new ToolStripMenuItem();
        item.Padding = new Padding(32, 7, 32, 7);
        item.ForeColor = Win11TextPrimary;
        item.Click += onClick;
        m.Items.Add(item);
        return item;
    }

    private void UpdateMenuText()
    {
        bool en = settings.Language == "en";
        if (menuStart == null) return;

        if (running)
        {
            menuStart.Text = en ? "Pause countdown" : "暂停倒计时";
            menuStart.ForeColor = Color.FromArgb(202, 80, 16);
        }
        else if (sessionActive)
        {
            menuStart.Text = en ? "Resume countdown" : "继续倒计时";
            menuStart.ForeColor = Color.FromArgb(16, 124, 65);
        }
        else
        {
            menuStart.Text = en ? "Start countdown" : "开始倒计时";
            menuStart.ForeColor = Color.FromArgb(0, 103, 192);
        }

        menuStart.ShortcutKeyDisplayString = HotkeyText(settings.HotkeyModifiers, settings.HotkeyKey);
        menuReset.Text = en ? "Reset countdown" : "重置倒计时";
        menuReset.Enabled = running || sessionActive;
        menuMove.Text = overlay.MoveMode ? (en ? "✓ Finish position adjustment" : "✓ 完成位置调整") : (en ? "Adjust text position" : "调整文字位置");
        menuHistory.Text = en ? "View today's history" : "查看今日记录";
        menuSettings.Text = en ? "Settings…" : "设置…";
        menuAbout.Text = en ? "About iClock…" : "关于 iClock…";
        menuExit.Text = en ? "Exit" : "退出";
        if (menuUpdate != null) menuUpdate.Text = en ? "⭐ Update available (" + latestVersion + ")…" : "⭐ 发现新版本 (" + latestVersion + ")…";
        tray.Text = "iClock";
    }

    private void Toggle()
    {
        if (running) { remaining = ReadRemaining(); running = false; timer.Stop(); lastSeconds = -1; overlay.SetPaused(true); Display(remaining); }
        else {
            if (activeNotice != null && !activeNotice.IsDisposed) { activeNotice.Close(); activeNotice = null; }
            if (remaining <= TimeSpan.Zero) remaining = TimeSpan.FromMinutes(settings.Minutes);
            if (!sessionActive) { sessionStart = DateTime.Now; sessionMinutes = settings.Minutes; sessionActive = true; }
            deadlineTimestamp = Stopwatch.GetTimestamp() + (long)(remaining.TotalSeconds * Stopwatch.Frequency);
            running = true;
            lastSeconds = -1;
            timer.Interval = 100;
            timer.Start();
            overlay.SetPaused(false);
            overlay.Show();
            Tick(null, EventArgs.Empty);
        }
        UpdateMenuText();
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
            running = false;
            timer.Stop();
            overlay.SetPaused(false);
            overlay.Hide();
            if (sessionActive) LogSession("Completed");
            sessionActive = false;
            Finish();
            return;
        }
        double secLeft = (double)ticksLeft / Stopwatch.Frequency;
        long totalSecs = Math.Max(0, (long)Math.Ceiling(secLeft));
        remaining = TimeSpan.FromSeconds(secLeft);
        if (totalSecs != lastSeconds)
        {
            lastSeconds = totalSecs;
            FormatAndDisplay(totalSecs);
        }
        double frac = secLeft - Math.Floor(secLeft);
        if (frac <= 0.001) frac = 1.0;
        int nextMs = (int)(frac * 1000) + 15;
        timer.Interval = Math.Max(80, Math.Min(400, nextMs));
    }
    private void FormatAndDisplay(long total)
    {
        long h = total / 3600, m = (total / 60) % 60, s = total % 60;
        string value = settings.Format == "MM:SS" ? (total / 60).ToString("00") + ":" + s.ToString("00") :
            settings.Format == "Chinese" ? (settings.Language == "en" ? h.ToString("00") + "h " + m.ToString("00") + "m " + s.ToString("00") + "s" : h.ToString("00") + "时" + m.ToString("00") + "分" + s.ToString("00") + "秒") : h.ToString("00") + ":" + m.ToString("00") + ":" + s.ToString("00");
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
        overlay.SetPaused(false);
        overlay.Hide();
        UpdateMenuText();
        if (settings.EndNotice)
        {
            if (activeNotice != null && !activeNotice.IsDisposed)
            {
                activeNotice.ForceTopMost();
                return;
            }
            activeNotice = new NoticeDialog(settings, DateTime.Now);
            activeNotice.FormClosed += delegate { activeNotice = null; };
            activeNotice.Show();
        }
        else if (settings.EndSound)
        {
            System.Media.SystemSounds.Exclamation.Play();
        }
    }
    private void ToggleMove()
    {
        bool enabled = !overlay.MoveMode; overlay.SetMoveMode(enabled, running);
        if (enabled) { if (!sessionActive) ResetDisplay(); overlay.Show(); }
        else { if (!sessionActive) overlay.Hide(); }
        bool en = settings.Language == "en";
        tray.ShowBalloonTip(2000, "iClock", enabled ? (en ? "Drag the translucent area to move the text. Use the tray menu to finish." : "拖动半透明区域调整文字位置，再次从托盘菜单退出调整模式。") : (en ? "Text position saved." : "文字位置已保存。"), ToolTipIcon.Info);
        UpdateMenuText();
    }
    private void ShowSettings()
    {
        using (SettingsDialog d = new SettingsDialog(settings))
        {
            if (d.ShowDialog() != DialogResult.OK) return;
            bool hotkeyFailed = false;
            if (!SetHotkey(d.Value.HotkeyModifiers, d.Value.HotkeyKey))
            {
                d.Value.HotkeyModifiers = activeHotkeyModifiers; d.Value.HotkeyKey = activeHotkeyKey;
                hotkeyFailed = true;
            }
            settings = d.Value; settings.Save(); overlay.SetSettings(settings); ApplyStartup();
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
                bool en = settings.Language == "en";
                MessageBox.Show(en ? "That hotkey is already occupied by another application. The previous hotkey was kept, but other settings were saved." : "该快捷键已被其他程序占用，快捷键已保留为原设置，其他设置已成功保存。", "iClock", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
    private bool SetHotkey(int modifiers, int key)
    {
        bool hadHotkey = hotkeyRegistered;
        int oldModifiers = activeHotkeyModifiers, oldKey = activeHotkeyKey;
        if (hadHotkey) UnregisterHotKey(messageWindow.Handle, HOTKEY_ID);
        bool ok = RegisterHotKey(messageWindow.Handle, HOTKEY_ID, (uint)(modifiers | MOD_NOREPEAT), (uint)key);
        if (ok) { activeHotkeyModifiers = modifiers; activeHotkeyKey = key; hotkeyRegistered = true; return true; }
        hotkeyRegistered = false;
        if (hadHotkey && RegisterHotKey(messageWindow.Handle, HOTKEY_ID, (uint)(oldModifiers | MOD_NOREPEAT), (uint)oldKey)) hotkeyRegistered = true;
        return false;
    }
    private struct StatSpan
    {
        public string Text;
        public Font Font;
        public Color Color;
        public StatSpan(string text, Font font, Color color) { Text = text; Font = font; Color = color; }
    }
    private void ShowHistory()
    {
        bool en = settings.Language == "en";
        string path = Path.Combine(Path.GetDirectoryName(Settings.FilePath), "history-" + DateTime.Now.ToString("yyyyMMdd") + ".tsv");
        Form f = new Form();
        f.Font = GetUiFont(settings.Language, 9.5f);
        f.BackColor = Win11Bg;
        ApplyModernWindowStyle(f);
        f.Text = en ? "iClock - Today's countdown history" : "iClock - 今日倒计时记录";
        f.StartPosition = FormStartPosition.CenterScreen;
        f.ClientSize = new Size(640, 380);
        f.MinimizeBox = false; f.MaximizeBox = false;
        ListView list = new ListView();
        list.Font = GetUiFont(settings.Language, 9.5f);
        list.BackColor = Color.White;
        list.ForeColor = Win11TextPrimary;
        list.BorderStyle = BorderStyle.FixedSingle;
        list.View = View.Details; list.FullRowSelect = true; list.GridLines = true;
        list.SetBounds(12, 12, 616, 312);
        list.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        list.OwnerDraw = true;

        Font headerFont = new Font(list.Font, FontStyle.Bold);
        Font boldFont = new Font(list.Font, FontStyle.Bold);
        f.FormClosed += delegate { headerFont.Dispose(); boldFont.Dispose(); };
        f.KeyPreview = true;
        f.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) f.Close(); };

        MethodInvoker updateColumnWidths = delegate
        {
            if (list.Columns.Count < 6) return;
            int clientW = list.ClientSize.Width;
            if (clientW <= 0) return;
            int c0 = 50, c1 = 115, c2 = 115, c3 = 85, c4 = 95;
            list.Columns[0].Width = c0;
            list.Columns[1].Width = c1;
            list.Columns[2].Width = c2;
            list.Columns[3].Width = c3;
            list.Columns[4].Width = c4;
            list.Columns[5].Width = Math.Max(100, clientW - c0 - c1 - c2 - c3 - c4);
        };
        list.Resize += delegate { updateColumnWidths(); };
        f.Shown += delegate { updateColumnWidths(); };

        list.DrawColumnHeader += delegate(object s, DrawListViewColumnHeaderEventArgs e)
        {
            using (SolidBrush b = new SolidBrush(Color.FromArgb(243, 244, 246)))
            {
                e.Graphics.FillRectangle(b, e.Bounds);
            }
            using (Pen p = new Pen(Color.FromArgb(226, 230, 236)))
            {
                e.Graphics.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
                e.Graphics.DrawLine(p, e.Bounds.Right - 1, e.Bounds.Top + 3, e.Bounds.Right - 1, e.Bounds.Bottom - 4);
            }
            TextFormatFlags flags = (e.ColumnIndex == 0)
                ? (TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine)
                : (TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.WordEllipsis);
            Rectangle textRect = (e.ColumnIndex == 0)
                ? e.Bounds
                : new Rectangle(e.Bounds.X + 8, e.Bounds.Y, Math.Max(0, e.Bounds.Width - 16), e.Bounds.Height);
            Color headerColor = (e.ColumnIndex == 0) ? Win11ShortcutGray : Color.FromArgb(60, 60, 60);
            TextRenderer.DrawText(e.Graphics, e.Header.Text, headerFont, textRect, headerColor, flags);
        };
        list.DrawItem += delegate(object s, DrawListViewItemEventArgs e) { };
        list.DrawSubItem += delegate(object s, DrawListViewSubItemEventArgs e)
        {
            if (e.ColumnIndex == 0)
            {
                e.DrawBackground();
                Color fg = ((e.ItemState & ListViewItemStates.Selected) != 0) ? SystemColors.HighlightText : Win11ShortcutGray;
                TextRenderer.DrawText(e.Graphics, e.SubItem.Text, e.Item.Font, e.Bounds, fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
            else
            {
                e.DrawDefault = true;
            }
        };

        list.Columns.Add("#", 50, HorizontalAlignment.Center);
        list.Columns.Add(en ? "Start time" : "开始时间", 115);
        list.Columns.Add(en ? "End time" : "结束时间", 115);
        list.Columns.Add(en ? "Duration" : "设定时长", 85);
        list.Columns.Add(en ? "Actual duration" : "实际时长", 95);
        list.Columns.Add(en ? "Result" : "结果", 150);

        int totalSessions = 0, completedSessions = 0;
        long totalActualSecs = 0;

        try
        {
            if (File.Exists(path)) foreach (string row in File.ReadAllLines(path, Encoding.UTF8))
            {
                string[] cols = row.Split('\t'); if (cols.Length < 5) continue;
                long actualSecs = 0;
                long.TryParse(cols[3], out actualSecs);
                string result = cols[4];
                bool isCompleted = result == "Completed" || result == "已完成";
                if (en) result = isCompleted ? "Completed" : (result == "Reset" || result == "已重置" ? "Reset" : (result == "Interrupted" || result == "已中断" ? "Interrupted" : result));
                else result = isCompleted ? "已完成" : (result == "Reset" ? "已重置" : (result == "Interrupted" ? "已中断" : result));

                totalSessions++;
                if (isCompleted) completedSessions++;
                totalActualSecs += actualSecs;

                ListViewItem item = new ListViewItem(totalSessions.ToString());
                item.SubItems.Add(cols[0]);
                item.SubItems.Add(cols[1]);
                item.SubItems.Add(cols[2] + (en ? " min" : " 分钟"));
                item.SubItems.Add(FormatDuration(actualSecs, en));
                item.SubItems.Add(result);
                list.Items.Add(item);
            }
        }
        catch { }
        if (list.Items.Count == 0)
        {
            ListViewItem emptyItem = new ListViewItem("-");
            emptyItem.SubItems.Add(en ? "No records today" : "今天还没有倒计时记录");
            list.Items.Add(emptyItem);
        }

        Panel summaryPanel = new Panel();
        summaryPanel.SetBounds(12, 332, 616, 36);
        summaryPanel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        summaryPanel.BackColor = Color.FromArgb(243, 244, 246);

        StatSpan[] spans;
        if (totalSessions == 0)
        {
            if (en)
            {
                spans = new StatSpan[]
                {
                    new StatSpan("📊 Today's Summary: ", list.Font, Win11TextPrimary),
                    new StatSpan("No countdowns   |   Actual time ", list.Font, Win11TextSecondary),
                    new StatSpan("0s", boldFont, Win11ShortcutGray),
                    new StatSpan("   |   Completion rate ", list.Font, Win11TextSecondary),
                    new StatSpan("0%", boldFont, Win11ShortcutGray)
                };
            }
            else
            {
                spans = new StatSpan[]
                {
                    new StatSpan("📊 今日统计：", list.Font, Win11TextPrimary),
                    new StatSpan("暂无倒计时   ｜   实际计时 ", list.Font, Win11TextSecondary),
                    new StatSpan("0 秒", boldFont, Win11ShortcutGray),
                    new StatSpan("   ｜   完成率 ", list.Font, Win11TextSecondary),
                    new StatSpan("0%", boldFont, Win11ShortcutGray)
                };
            }
        }
        else
        {
            int rate = (int)Math.Round((double)completedSessions * 100.0 / totalSessions);
            string focusText = FormatDuration(totalActualSecs, en);
            Color rateColor = (rate >= 80) ? Color.FromArgb(16, 124, 65) : Win11Accent;
            if (en)
            {
                spans = new StatSpan[]
                {
                    new StatSpan("📊 Today's Summary: ", list.Font, Win11TextPrimary),
                    new StatSpan(totalSessions.ToString(), boldFont, Win11Accent),
                    new StatSpan(totalSessions == 1 ? " countdown   |   Actual time " : " countdowns   |   Actual time ", list.Font, Win11TextSecondary),
                    new StatSpan(focusText, boldFont, Win11Accent),
                    new StatSpan("   |   Completion rate ", list.Font, Win11TextSecondary),
                    new StatSpan(rate + "%", boldFont, rateColor)
                };
            }
            else
            {
                spans = new StatSpan[]
                {
                    new StatSpan("📊 今日统计：", list.Font, Win11TextPrimary),
                    new StatSpan("累计 ", list.Font, Win11TextSecondary),
                    new StatSpan(totalSessions.ToString(), boldFont, Win11Accent),
                    new StatSpan(" 次倒计时   ｜   实际计时 ", list.Font, Win11TextSecondary),
                    new StatSpan(focusText, boldFont, Win11Accent),
                    new StatSpan("   ｜   完成率 ", list.Font, Win11TextSecondary),
                    new StatSpan(rate + "%", boldFont, rateColor)
                };
            }
        }

        summaryPanel.Paint += delegate(object s, PaintEventArgs e)
        {
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(243, 244, 246)))
            {
                e.Graphics.FillRectangle(bg, summaryPanel.ClientRectangle);
            }
            using (Pen p = new Pen(Color.FromArgb(226, 230, 236), 1f))
            {
                e.Graphics.DrawRectangle(p, 0, 0, summaryPanel.Width - 1, summaryPanel.Height - 1);
            }
            TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            int totalW = 0;
            int[] widths = new int[spans.Length];
            for (int i = 0; i < spans.Length; i++)
            {
                Size sz = TextRenderer.MeasureText(e.Graphics, spans[i].Text, spans[i].Font, Size.Empty, flags);
                widths[i] = sz.Width;
                totalW += sz.Width;
            }
            int curX = Math.Max(8, (summaryPanel.Width - totalW) / 2);
            int y = (summaryPanel.Height - list.Font.Height) / 2;
            for (int i = 0; i < spans.Length; i++)
            {
                TextRenderer.DrawText(e.Graphics, spans[i].Text, spans[i].Font, new Point(curX, y), spans[i].Color, flags);
                curX += widths[i];
            }
        };
        summaryPanel.Resize += delegate { summaryPanel.Invalidate(); };

        f.Controls.Add(list);
        f.Controls.Add(summaryPanel);
        updateColumnWidths();
        f.ShowDialog();
        f.Dispose();
    }
    private void ShowAbout()
    {
        using (AboutDialog d = new AboutDialog(settings.Language, this))
        {
            d.ShowDialog();
        }
    }
    private void LogSession(string result)
    {
        try
        {
            long actualSecs = (result == "Completed")
                ? sessionMinutes * 60L
                : Math.Max(0L, Math.Min(sessionMinutes * 60L, (long)(TimeSpan.FromMinutes(sessionMinutes) - remaining).TotalSeconds));
            string dir = Path.GetDirectoryName(Settings.FilePath); Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "history-" + sessionStart.ToString("yyyyMMdd") + ".tsv");
            string row = sessionStart.ToString("HH:mm:ss") + "\t" + DateTime.Now.ToString("HH:mm:ss") + "\t" + sessionMinutes + "\t" + actualSecs + "\t" + result + Environment.NewLine;
            File.AppendAllText(path, row, new UTF8Encoding(false));
        }
        catch { }
    }
    private void ApplyStartup()
    {
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", true))
            {
                if (k == null) return;
                if (settings.AutoStart) k.SetValue("iClock", "\"" + exePath + "\""); else k.DeleteValue("iClock", false);
            }
        }
        catch { }
    }
    private void Exit()
    {
        if (activeNotice != null && !activeNotice.IsDisposed) { activeNotice.Close(); activeNotice = null; }
        if (running) remaining = ReadRemaining();
        if (sessionActive) { LogSession("Interrupted"); sessionActive = false; }
        timer.Stop(); tray.Visible = false; if (hotkeyRegistered) UnregisterHotKey(messageWindow.Handle, HOTKEY_ID);
        overlay.Close(); overlay.Dispose(); messageWindow.DestroyHandle(); tray.Dispose(); appIcon.Dispose(); timer.Dispose(); ExitThread();
    }
    internal static string FormatDuration(long secs, bool en)
    {
        if (secs < 60) return secs + (en ? "s" : " 秒");
        long m = secs / 60;
        long s = secs % 60;
        if (m < 60)
        {
            if (s == 0) return m + (en ? " min" : " 分钟");
            return en ? m + "m " + s + "s" : m + " 分 " + s + " 秒";
        }
        long h = m / 60;
        m = m % 60;
        if (s == 0 && m == 0) return h + (en ? "h" : " 小时");
        if (s == 0) return en ? h + "h " + m + "m" : h + " 小时 " + m + " 分钟";
        return en ? h + "h " + m + "m " + s + "s" : h + " 小时 " + m + " 分 " + s + " 秒";
    }
    public void HandleHotkey() { Toggle(); }
    internal static string HotkeyText(int mods, int key)
    {
        string value = "";
        if ((mods & 2) != 0) value += "Ctrl+";
        if ((mods & 1) != 0) value += "Alt+";
        if ((mods & 4) != 0) value += "Shift+";
        return value + ((Keys)key).ToString();
    }

    internal static bool ProbeHotkey(int modifiers, int key)
    {
        NativeWindow nw = new NativeWindow();
        try
        {
            nw.CreateHandle(new CreateParams());
            int testId = 0x55AA;
            bool ok = RegisterHotKey(nw.Handle, testId, (uint)(modifiers | MOD_NOREPEAT), (uint)key);
            if (ok) UnregisterHotKey(nw.Handle, testId);
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
        string family = (lang == "en") ? "Segoe UI" : "Microsoft YaHei UI";
        try
        {
            return new Font(family, sizePt, style, GraphicsUnit.Point);
        }
        catch
        {
            try { return new Font("Microsoft YaHei", sizePt, style, GraphicsUnit.Point); }
            catch { return SystemFonts.MessageBoxFont; }
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
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
            DwmSetWindowAttribute(ctrl.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
        }
        catch { }
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
                string downloads = Path.Combine(userProfile, "Downloads");
                if (full.StartsWith(downloads + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(full, downloads, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            string lower = full.ToLowerInvariant();
            if (lower.Contains(@"\appdata\local\temp\") || lower.Contains(@"\downloads\"))
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
                linkPath = Path.Combine(programsDir, "iClock.lnk");
            }
            bool exists = File.Exists(linkPath);
            if (!exists && !createIfNotExists) return false;

            if (string.IsNullOrEmpty(targetExe)) targetExe = Application.ExecutablePath;

            string dir = Path.GetDirectoryName(linkPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return false;
            object shell = Activator.CreateInstance(shellType);
            object sc = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { linkPath });
            if (sc == null) return false;
            Type scType = sc.GetType();

            if (exists)
            {
                string currentTarget = scType.InvokeMember("TargetPath", BindingFlags.GetProperty, null, sc, null) as string;
                if (string.Equals(currentTarget, targetExe, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            scType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { targetExe });
            scType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { Path.GetDirectoryName(targetExe) });
            scType.InvokeMember("Description", BindingFlags.SetProperty, null, sc, new object[] { "iClock Desktop Floating Countdown Timer" });
            scType.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
            return true;
        }
        catch { return false; }
    }

    internal static Icon CreateIcon()
    {
        try
        {
            Icon exeIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (exeIcon != null) return exeIcon;
        }
        catch { }
        Bitmap b = new Bitmap(32, 32);
        using (Graphics g = Graphics.FromImage(b))
        using (SolidBrush btnBrush = new SolidBrush(Color.FromArgb(28, 114, 190)))
        using (SolidBrush darkBrush = new SolidBrush(Color.FromArgb(18, 75, 130)))
        using (SolidBrush whiteBrush = new SolidBrush(Color.White))
        using (Pen tickPen = new Pen(Color.FromArgb(160, 175, 195), 1f))
        using (Pen handPen = new Pen(Color.FromArgb(230, 45, 45), 1.6f))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            float s = 0.5f;
            g.FillRectangle(darkBrush, 28 * s, 3 * s, 8 * s, 7 * s);
            g.FillRectangle(btnBrush, 23 * s, 1 * s, 18 * s, 4 * s);
            g.FillRectangle(btnBrush, 45 * s, 8 * s, 6 * s, 7 * s);
            g.FillEllipse(btnBrush, 5 * s, 10 * s, 54 * s, 54 * s);
            g.FillEllipse(whiteBrush, 10 * s, 15 * s, 44 * s, 44 * s);
            g.DrawLine(tickPen, 32 * s, 17 * s, 32 * s, 21 * s);
            g.DrawLine(tickPen, 52 * s, 37 * s, 48 * s, 37 * s);
            g.DrawLine(tickPen, 32 * s, 57 * s, 32 * s, 53 * s);
            g.DrawLine(tickPen, 12 * s, 37 * s, 16 * s, 37 * s);
            handPen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
            handPen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
            g.DrawLine(handPen, 32 * s, 37 * s, 45 * s, 24 * s);
            g.FillEllipse(darkBrush, 29 * s, 34 * s, 6 * s, 6 * s);
        }
        IntPtr h = b.GetHicon(); Icon icon = (Icon)Icon.FromHandle(h).Clone(); DestroyIcon(h); b.Dispose(); return icon;
    }

    private static bool launchReported;
    private void TrackAndCheckUpdates()
    {
        if (launchReported) return;
        launchReported = true;
        System.Threading.ThreadPool.QueueUserWorkItem(delegate
        {
            string latest, url;
            if (QueryLatestVersion("launch", out latest, out url))
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

    internal static bool QueryLatestVersion(string eventType, out string latest, out string url)
    {
        latest = null;
        url = null;
        try
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
            byte[] data = Encoding.UTF8.GetBytes("{\"event\":\"" + (eventType ?? "launch") + "\",\"page\":\"main\",\"ua\":\"iClock Desktop v" + CURRENT_VERSION + "\"}");
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create("https://green-scene-5a6d.francodeyvison183.workers.dev/track");
            req.Method = "POST";
            req.ContentType = "application/json";
            req.ContentLength = data.Length;
            req.Timeout = 5000;
            req.ReadWriteTimeout = 5000;
            using (Stream s = req.GetRequestStream()) s.Write(data, 0, data.Length);
            using (WebResponse resp = req.GetResponse())
            using (StreamReader r = new StreamReader(resp.GetResponseStream()))
            {
                string body = r.ReadToEnd();
                latest = ExtractJsonValue(body, "latest");
                url = ExtractJsonValue(body, "url");
            }
        }
        catch { }

        if (string.IsNullOrEmpty(latest))
        {
            try
            {
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create("https://api.github.com/repos/francodeyvison183-collab/iClock/releases/latest");
                req.Method = "GET";
                req.UserAgent = "iClock-Desktop-v" + CURRENT_VERSION;
                req.Timeout = 5000;
                req.ReadWriteTimeout = 5000;
                using (WebResponse resp = req.GetResponse())
                using (StreamReader r = new StreamReader(resp.GetResponseStream()))
                {
                    string body = r.ReadToEnd();
                    latest = ExtractJsonValue(body, "tag_name");
                    url = ExtractJsonValue(body, "html_url");
                }
            }
            catch { }
        }
        return !string.IsNullOrEmpty(latest);
    }

    internal void NotifyUpdateFound(string newVersion, string url)
    {
        OnUpdateFound(newVersion, url);
    }

    private void OnUpdateFound(string newVersion, string url)
    {
        if (string.IsNullOrEmpty(url)) url = "https://github.com/francodeyvison183-collab/iClock/releases/latest";
        latestVersion = newVersion;
        updateUrl = url;
        bool en = settings.Language == "en";
        string title = en ? "iClock Update Available" : "iClock 发现新版本";
        string text = en ? "New version " + newVersion + " is available. Click to download." : "发现新版本 " + newVersion + "，点击前往下载更新。";

        try { tray.ShowBalloonTip(6000, title, text, ToolTipIcon.Info); } catch { }

        if (menuUpdate == null && tray.ContextMenuStrip != null)
        {
            menuUpdate = new ToolStripMenuItem(en ? "⭐ Update available (" + newVersion + ")…" : "⭐ 发现新版本 (" + newVersion + ")…");
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
        int k = json.IndexOf("\"" + key + "\"", StringComparison.OrdinalIgnoreCase);
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
        public MessageWindow(AppContext a) { owner = a; CreateHandle(new CreateParams()); }
        protected override void WndProc(ref Message m) { if (m.Msg == WM_HOTKEY) owner.HandleHotkey(); base.WndProc(ref m); }
    }

    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr hIcon);
}

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        bool created;
        using (System.Threading.Mutex m = new System.Threading.Mutex(true, "iClock.SingleInstance", out created))
        {
            if (!created) return;
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new AppContext());
        }
    }
}
