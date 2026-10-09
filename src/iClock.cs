using System;
using System.Drawing;
using System.Drawing.Text;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Media;
using System.Net;
using Microsoft.Win32;

internal sealed class Settings
{
    public int Minutes = 25;
    public int FontSize = 32;
    public int X = 80;
    public int Y = 80;
    public int HotkeyModifiers = 3;
    public int HotkeyKey = (int)Keys.Space;
    public string Color = "#FFFFFF";
    public string Format = "HH:MM:SS";
    public string EndMessage = "倒计时结束";
    public string Language = "zh";
    public bool AutoStart;
    public bool EndSound = true;
    public bool EndNotice = true;

    public static string FilePath
    {
        get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "iClock", "settings.ini"); }
    }

    public static Settings Load()
    {
        Settings s = new Settings();
        try
        {
            if (!File.Exists(FilePath)) return s;
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
                else if (k == "Format" && (v == "HH:MM:SS" || v == "MM:SS" || v == "Chinese")) s.Format = v;
                else if (k == "EndMessage") s.EndMessage = v;
                else if (k == "Language" && (v == "zh" || v == "en")) s.Language = v;
                else if (k == "AutoStart" && bool.TryParse(v, out b)) s.AutoStart = b;
                else if (k == "EndSound" && bool.TryParse(v, out b)) s.EndSound = b;
                else if (k == "EndNotice" && bool.TryParse(v, out b)) s.EndNotice = b;
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
            "Color=" + Color, "Format=" + Format, "EndMessage=" + EndMessage, "Language=" + Language, "AutoStart=" + AutoStart,
            "EndSound=" + EndSound, "EndNotice=" + EndNotice
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
        try { c = ColorTranslator.FromHtml(settings.Color); } catch { c = Color.White; }
        cachedFont = new Font("Segoe UI", settings.FontSize, FontStyle.Bold, GraphicsUnit.Pixel);
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

    public void SetMoveMode(bool enabled)
    {
        MoveMode = enabled;
        if (enabled)
        {
            TransparencyKey = Color.Empty; BackColor = Color.Black; Opacity = 0.40; TopMost = true;
        }
        else
        {
            Opacity = 1.0; BackColor = Color.Fuchsia; TransparencyKey = Color.Fuchsia;
        }
        RecreateHandle();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
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
    private ComboBox format;
    private CheckBox startup, sound, notice;
    private TextBox hotkeyBox;
    private int hotkeyModifiers, hotkeyKey;
    public Settings Value { get; private set; }

    public SettingsDialog(Settings s)
    {
        Value = new Settings();
        Value.Minutes = s.Minutes; Value.FontSize = s.FontSize; Value.X = s.X; Value.Y = s.Y;
        Value.HotkeyModifiers = s.HotkeyModifiers; Value.HotkeyKey = s.HotkeyKey;
        Value.Color = s.Color; Value.Format = s.Format; Value.EndMessage = s.EndMessage;
        Value.Language = s.Language;
        Value.AutoStart = s.AutoStart; Value.EndSound = s.EndSound; Value.EndNotice = s.EndNotice;
        bool en = Value.Language == "en";
        Text = en ? "iClock Settings" : "iClock 设置"; FormBorderStyle = FormBorderStyle.FixedDialog; StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false; ClientSize = new Size(350, 356);
        AddLabel(en ? "Duration (minutes)" : "倒计时（分钟）", 16, 20); minutes = AddNumber(Value.Minutes, 1, 1440, 150, 16);
        AddLabel(en ? "Text size" : "文字大小", 16, 56); size = AddNumber(Value.FontSize, 12, 120, 150, 52);
        AddLabel(en ? "Text color" : "文字颜色", 16, 92); selectedColor = Value.Color;
        colorButton = new Button(); colorButton.Text = en ? "Choose…" : "点击选取…"; colorButton.SetBounds(150, 88, 105, 26); colorButton.Click += ChooseColor; Controls.Add(colorButton);
        colorPreview = new Panel(); colorPreview.SetBounds(265, 90, 40, 22); colorPreview.BorderStyle = BorderStyle.FixedSingle; UpdateColorPreview(); Controls.Add(colorPreview);
        AddLabel(en ? "Display format" : "显示格式", 16, 128); format = new ComboBox(); format.DropDownStyle = ComboBoxStyle.DropDownList; format.SetBounds(150, 124, 150, 24);
        format.Items.AddRange(en ? new object[] { "HH:MM:SS", "MM:SS", "Chinese units" } : new object[] { "HH:MM:SS", "MM:SS", "中文单位" }); format.SelectedIndex = Value.Format == "MM:SS" ? 1 : (Value.Format == "Chinese" ? 2 : 0); Controls.Add(format);
        AddLabel(en ? "End message" : "结束时弹出消息", 16, 160); endMessage = new TextBox(); endMessage.SetBounds(150, 160, 180, 36); endMessage.Multiline = true; endMessage.MaxLength = 200; endMessage.Text = Value.EndMessage; Controls.Add(endMessage);
        hotkeyModifiers = Value.HotkeyModifiers; hotkeyKey = Value.HotkeyKey;
        AddLabel(en ? "Start/pause hotkey" : "启动/暂停快捷键", 16, 208); hotkeyBox = new TextBox(); hotkeyBox.ReadOnly = true; hotkeyBox.SetBounds(150, 208, 180, 24); hotkeyBox.Text = HotkeyText(hotkeyModifiers, hotkeyKey); hotkeyBox.KeyDown += CaptureHotkey; Controls.Add(hotkeyBox);
        startup = AddCheck(en ? "Start with Windows" : "开机自启动", Value.AutoStart, 16, 238);
        sound = AddCheck(en ? "Sound at end" : "结束时声音提醒", Value.EndSound, 16, 262);
        notice = AddCheck(en ? "Notification at end" : "结束时系统通知", Value.EndNotice, 16, 286);
        Button save = new Button(); save.Text = en ? "Save" : "保存"; save.SetBounds(170, 320, 70, 28); save.Click += SaveClick; Controls.Add(save);
        Button cancel = new Button(); cancel.Text = en ? "Cancel" : "取消"; cancel.SetBounds(250, 320, 70, 28); cancel.DialogResult = DialogResult.Cancel; Controls.Add(cancel);
        AcceptButton = save; CancelButton = cancel;
    }

    private void AddLabel(string t, int x, int y) { Label l = new Label(); l.Text = t; l.SetBounds(x, y, 132, 24); l.TextAlign = ContentAlignment.MiddleLeft; Controls.Add(l); }
    private NumericUpDown AddNumber(int v, int min, int max, int x, int y) { NumericUpDown n = new NumericUpDown(); n.Minimum = min; n.Maximum = max; n.Value = Math.Min(max, Math.Max(min, v)); n.SetBounds(x, y, 150, 24); Controls.Add(n); return n; }
    private CheckBox AddCheck(string t, bool v, int x, int y) { CheckBox c = new CheckBox(); c.Text = t; c.Checked = v; c.SetBounds(x, y, 220, 24); Controls.Add(c); return c; }
    private void ChooseColor(object sender, EventArgs e)
    {
        Color initial; try { initial = ColorTranslator.FromHtml(selectedColor); } catch { initial = Color.White; }
        using (ColorDialog d = new ColorDialog())
        {
            d.Color = initial; d.AllowFullOpen = true; d.FullOpen = true; d.AnyColor = true;
            if (d.ShowDialog(this) == DialogResult.OK) { selectedColor = ColorTranslator.ToHtml(d.Color); UpdateColorPreview(); }
        }
    }
    private void UpdateColorPreview()
    {
        try { colorPreview.BackColor = ColorTranslator.FromHtml(selectedColor); } catch { colorPreview.BackColor = Color.White; }
    }
    private void CaptureHotkey(object sender, KeyEventArgs e)
    {
        e.SuppressKeyPress = true; e.Handled = true;
        Keys key = e.KeyCode;
        if (key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu || key == Keys.LWin || key == Keys.RWin) return;
        int mods = 0;
        if ((e.Modifiers & Keys.Control) != 0) mods |= 2;
        if ((e.Modifiers & Keys.Alt) != 0) mods |= 1;
        if ((e.Modifiers & Keys.Shift) != 0) mods |= 4;
        if (mods == 0) { hotkeyBox.Text = Value.Language == "en" ? "Use a modifier + key" : "请按修饰键 + 按键"; return; }
        hotkeyModifiers = mods; hotkeyKey = (int)key; hotkeyBox.Text = HotkeyText(mods, (int)key);
    }
    private static string HotkeyText(int mods, int key)
    {
        string value = "";
        if ((mods & 2) != 0) value += "Ctrl+";
        if ((mods & 1) != 0) value += "Alt+";
        if ((mods & 4) != 0) value += "Shift+";
        return value + ((Keys)key).ToString();
    }
    private void SaveClick(object sender, EventArgs e)
    {
        Value.Minutes = (int)minutes.Value; Value.FontSize = (int)size.Value; Value.Color = selectedColor;
        Value.HotkeyModifiers = hotkeyModifiers; Value.HotkeyKey = hotkeyKey;
        Value.EndMessage = endMessage.Text.Trim();
        Value.Format = format.SelectedIndex == 1 ? "MM:SS" : (format.SelectedIndex == 2 ? "Chinese" : "HH:MM:SS");
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
        Text = "iClock";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        TopMost = true;
        ClientSize = new Size(340, 210);

        string titleText = String.IsNullOrEmpty(s.EndMessage) ? (en ? "Countdown Finished" : "倒计时结束") : s.EndMessage;
        Label titleLabel = new Label();
        titleLabel.Text = titleText;
        titleLabel.Font = new Font("Segoe UI", 16, FontStyle.Bold, GraphicsUnit.Pixel);
        titleLabel.TextAlign = ContentAlignment.MiddleCenter;
        titleLabel.SetBounds(20, 18, 300, 28);
        Controls.Add(titleLabel);

        string timeText = s.Format == "MM:SS" ? "00:00" :
            (s.Format == "Chinese" ? (en ? "00h 00m 00s" : "00时00分00秒") : "00:00:00");
        Label timeLabel = new Label();
        timeLabel.Text = timeText;
        timeLabel.Font = new Font("Segoe UI", 28, FontStyle.Bold, GraphicsUnit.Pixel);
        timeLabel.ForeColor = Color.FromArgb(28, 114, 190);
        timeLabel.TextAlign = ContentAlignment.MiddleCenter;
        timeLabel.SetBounds(20, 52, 300, 42);
        Controls.Add(timeLabel);

        finishLabel = new Label();
        finishLabel.Font = new Font("Segoe UI", 12, FontStyle.Regular, GraphicsUnit.Pixel);
        finishLabel.ForeColor = Color.Gray;
        finishLabel.TextAlign = ContentAlignment.MiddleCenter;
        finishLabel.SetBounds(20, 102, 300, 20);
        Controls.Add(finishLabel);
        UpdateFinishLabel();

        Button btn = new Button();
        btn.Text = en ? "OK" : "确定";
        btn.Font = new Font("Segoe UI", 12, FontStyle.Regular, GraphicsUnit.Pixel);
        btn.SetBounds(115, 145, 110, 36);
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

internal sealed class SponsorDialog : Form
{
    public SponsorDialog(string language)
    {
        bool en = language == "en";
        Text = en ? "Sponsor iClock" : "赞赏 iClock";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(300, 370);

        Label lbl = new Label();
        lbl.Text = en ? "If iClock helps you, thank you for supporting!" : "如果 iClock 对你有帮助，欢迎赞赏支持！";
        lbl.Font = new Font("Segoe UI", 12, FontStyle.Regular, GraphicsUnit.Pixel);
        lbl.TextAlign = ContentAlignment.MiddleCenter;
        lbl.SetBounds(15, 14, 270, 30);
        Controls.Add(lbl);

        PictureBox pic = new PictureBox();
        pic.SetBounds(35, 48, 230, 230);
        pic.SizeMode = PictureBoxSizeMode.Zoom;
        pic.Image = LoadSponsorImage();
        Controls.Add(pic);

        Label sub = new Label();
        sub.Text = en ? "WeChat Pay" : "微信扫一扫 赞赏码";
        sub.Font = new Font("Segoe UI", 12, FontStyle.Bold, GraphicsUnit.Pixel);
        sub.ForeColor = Color.FromArgb(28, 114, 190);
        sub.TextAlign = ContentAlignment.MiddleCenter;
        sub.SetBounds(15, 286, 270, 20);
        Controls.Add(sub);

        Button btn = new Button();
        btn.Text = en ? "Close" : "关闭";
        btn.Font = new Font("Segoe UI", 12, FontStyle.Regular, GraphicsUnit.Pixel);
        btn.SetBounds(105, 320, 90, 32);
        btn.Click += delegate { Close(); };
        Controls.Add(btn);
        AcceptButton = btn;
        CancelButton = btn;
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

internal sealed class AppContext : ApplicationContext
{
    private const int HOTKEY_ID = 0x4A10;
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_NOREPEAT = 0x4000;
    private Settings settings;
    private Overlay overlay;
    private NotifyIcon tray;
    private ToolStripMenuItem menuStart, menuReset, menuMove, menuSettings, menuHistory, menuSponsor, menuLanguage, menuLanguageChinese, menuLanguageEnglish, menuExit, menuUpdate;
    private const string CURRENT_VERSION = "1.0.0";
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
        if (!SetHotkey(settings.HotkeyModifiers, settings.HotkeyKey))
        {
            settings.HotkeyModifiers = 3; settings.HotkeyKey = (int)Keys.Space; SetHotkey(3, (int)Keys.Space);
        }
        tray = new NotifyIcon(); tray.Icon = appIcon; tray.Text = "iClock"; tray.Visible = true; tray.ContextMenuStrip = MakeMenu();
        tray.BalloonTipClicked += delegate { if (!string.IsNullOrEmpty(updateUrl)) { try { Process.Start(updateUrl); } catch { } } };
        timer = new Timer(); timer.Interval = 100; timer.Tick += Tick;
        ApplyStartup();
        ResetDisplay();
        TrackAndCheckUpdates();
    }

    private ContextMenuStrip MakeMenu()
    {
        ContextMenuStrip m = new ContextMenuStrip();
        menuStart = new ToolStripMenuItem(); menuStart.Click += delegate { Toggle(); };
        menuReset = new ToolStripMenuItem(); menuReset.Click += delegate { if (activeNotice != null && !activeNotice.IsDisposed) { activeNotice.Close(); activeNotice = null; } running = false; timer.Stop(); if (sessionActive) LogSession("Reset"); sessionActive = false; ResetDisplay(); overlay.Hide(); };
        menuMove = new ToolStripMenuItem(); menuMove.Click += delegate { ToggleMove(); };
        menuSettings = new ToolStripMenuItem(); menuSettings.Click += delegate { ShowSettings(); };
        menuHistory = new ToolStripMenuItem(); menuHistory.Click += delegate { ShowHistory(); };
        menuSponsor = new ToolStripMenuItem(); menuSponsor.Click += delegate { ShowSponsor(); };
        menuLanguage = new ToolStripMenuItem();
        menuLanguageChinese = new ToolStripMenuItem("简体中文"); menuLanguageChinese.Click += delegate { SelectLanguage("zh"); };
        menuLanguageEnglish = new ToolStripMenuItem("English"); menuLanguageEnglish.Click += delegate { SelectLanguage("en"); };
        menuLanguage.DropDownItems.Add(menuLanguageChinese); menuLanguage.DropDownItems.Add(menuLanguageEnglish);
        menuExit = new ToolStripMenuItem(); menuExit.Click += delegate { Exit(); };
        m.Items.Add(menuStart); m.Items.Add(menuReset); m.Items.Add(menuMove); m.Items.Add(menuSettings);
        m.Items.Add(menuHistory); m.Items.Add(menuSponsor); m.Items.Add(menuLanguage);
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add(menuExit);
        UpdateMenuText();
        return m;
    }

    private void UpdateMenuText()
    {
        bool en = settings.Language == "en";
        if (menuStart == null) return;
        menuStart.Text = (en ? "Start / pause  (" : "开始 / 暂停  (") + HotkeyText(settings.HotkeyModifiers, settings.HotkeyKey) + ")";
        menuReset.Text = en ? "Reset countdown" : "重置倒计时";
        menuMove.Text = overlay.MoveMode ? (en ? "Finish position adjustment" : "完成位置调整") : (en ? "Adjust text position" : "调整文字位置");
        menuSettings.Text = en ? "Settings…" : "设置…";
        menuHistory.Text = en ? "View today's history…" : "查看今日记录…";
        menuSponsor.Text = en ? "Sponsor…" : "赞赏作者…";
        menuLanguage.Text = en ? "Language" : "语言";
        menuLanguageChinese.Checked = settings.Language == "zh";
        menuLanguageEnglish.Checked = settings.Language == "en";
        menuExit.Text = en ? "Exit iClock" : "退出 iClock";
        if (menuUpdate != null) menuUpdate.Text = en ? "⭐ Update available (" + latestVersion + ")…" : "⭐ 发现新版本 (" + latestVersion + ")…";
        tray.Text = "iClock";
    }

    private void SelectLanguage(string language)
    {
        bool toEnglish = language == "en";
        if (settings.Language == language) return;
        if (settings.EndMessage == "倒计时结束" && toEnglish) settings.EndMessage = "Countdown finished";
        else if (settings.EndMessage == "Countdown finished" && !toEnglish) settings.EndMessage = "倒计时结束";
        settings.Language = language;
        settings.Save();
        overlay.SetSettings(settings);
        UpdateMenuText();
        if (running || overlay.Visible)
        {
            lastSeconds = -1;
            if (running) Tick(null, EventArgs.Empty);
            else Display(remaining);
        }
    }

    private void Toggle()
    {
        if (running) { remaining = ReadRemaining(); running = false; timer.Stop(); lastSeconds = -1; Display(remaining); }
        else {
            if (activeNotice != null && !activeNotice.IsDisposed) { activeNotice.Close(); activeNotice = null; }
            if (remaining <= TimeSpan.Zero) remaining = TimeSpan.FromMinutes(settings.Minutes);
            if (!sessionActive) { sessionStart = DateTime.Now; sessionMinutes = settings.Minutes; sessionActive = true; }
            deadlineTimestamp = Stopwatch.GetTimestamp() + (long)(remaining.TotalSeconds * Stopwatch.Frequency);
            running = true;
            lastSeconds = -1;
            timer.Interval = 100;
            timer.Start();
            overlay.Show();
            Tick(null, EventArgs.Empty);
        }
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
        overlay.Hide();
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
        bool enabled = !overlay.MoveMode; overlay.SetMoveMode(enabled);
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
            if (!SetHotkey(d.Value.HotkeyModifiers, d.Value.HotkeyKey))
            {
                d.Value.HotkeyModifiers = activeHotkeyModifiers; d.Value.HotkeyKey = activeHotkeyKey;
                bool en = d.Value.Language == "en";
                MessageBox.Show(en ? "That hotkey is already in use. The previous hotkey was kept." : "该快捷键已被其他程序占用，快捷键设置未更改。", "iClock", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            settings = d.Value; settings.Save(); overlay.SetSettings(settings); ApplyStartup();
            UpdateMenuText();
            lastSeconds = -1;
            if (!running && !sessionActive) { ResetDisplay(); overlay.Hide(); }
            else if (running) { Tick(null, EventArgs.Empty); }
        }
    }
    private bool SetHotkey(int modifiers, int key)
    {
        bool hadHotkey = hotkeyRegistered;
        int oldModifiers = activeHotkeyModifiers, oldKey = activeHotkeyKey;
        if (hadHotkey) UnregisterHotKey(messageWindow.Handle, HOTKEY_ID);
        bool ok = RegisterHotKey(messageWindow.Handle, HOTKEY_ID, (uint)(modifiers | 0x4000), (uint)key);
        if (ok) { activeHotkeyModifiers = modifiers; activeHotkeyKey = key; hotkeyRegistered = true; return true; }
        hotkeyRegistered = false;
        if (hadHotkey && RegisterHotKey(messageWindow.Handle, HOTKEY_ID, (uint)(oldModifiers | 0x4000), (uint)oldKey)) hotkeyRegistered = true;
        return false;
    }
    private void ShowHistory()
    {
        bool en = settings.Language == "en";
        string path = Path.Combine(Path.GetDirectoryName(Settings.FilePath), "history-" + DateTime.Now.ToString("yyyyMMdd") + ".tsv");
        Form f = new Form(); f.Text = en ? "iClock - Today's countdown history" : "iClock - 今日倒计时记录"; f.StartPosition = FormStartPosition.CenterScreen; f.ClientSize = new Size(610, 320); f.MinimizeBox = false; f.MaximizeBox = false;
        ListView list = new ListView(); list.View = View.Details; list.FullRowSelect = true; list.GridLines = true; list.SetBounds(12, 12, 586, 296);
        list.Columns.Add(en ? "Start time" : "开始时间", 145); list.Columns.Add(en ? "End time" : "结束时间", 145); list.Columns.Add(en ? "Duration" : "设定时长", 90); list.Columns.Add(en ? "Result" : "结果", 170);
        try
        {
            if (File.Exists(path)) foreach (string row in File.ReadAllLines(path, Encoding.UTF8))
            {
                string[] cols = row.Split('\t'); if (cols.Length < 4) continue;
                string result = cols[3];
                if (en) result = result == "Completed" || result == "已完成" ? "Completed" : (result == "Reset" || result == "已重置" ? "Reset" : (result == "Interrupted" || result == "已中断" ? "Interrupted" : result));
                else result = result == "Completed" ? "已完成" : (result == "Reset" ? "已重置" : (result == "Interrupted" ? "已中断" : result));
                ListViewItem item = new ListViewItem(cols[0]); item.SubItems.Add(cols[1]); item.SubItems.Add(cols[2] + (en ? " min" : " 分钟")); item.SubItems.Add(result); list.Items.Add(item);
            }
        }
        catch { }
        if (list.Items.Count == 0) list.Items.Add(new ListViewItem(en ? "No countdown records today" : "今天还没有倒计时记录"));
        f.Controls.Add(list); f.ShowDialog(); f.Dispose();
    }
    private void ShowSponsor()
    {
        using (SponsorDialog d = new SponsorDialog(settings.Language))
        {
            d.ShowDialog();
        }
    }
    private void LogSession(string result)
    {
        try
        {
            string dir = Path.GetDirectoryName(Settings.FilePath); Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "history-" + sessionStart.ToString("yyyyMMdd") + ".tsv");
            string row = sessionStart.ToString("HH:mm:ss") + "\t" + DateTime.Now.ToString("HH:mm:ss") + "\t" + sessionMinutes + "\t" + result + Environment.NewLine;
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
        if (sessionActive) { LogSession("Interrupted"); sessionActive = false; }
        timer.Stop(); tray.Visible = false; if (hotkeyRegistered) UnregisterHotKey(messageWindow.Handle, HOTKEY_ID);
        overlay.Close(); overlay.Dispose(); messageWindow.DestroyHandle(); tray.Dispose(); appIcon.Dispose(); timer.Dispose(); ExitThread();
    }
    protected override void ExitThreadCore() { base.ExitThreadCore(); }
    public void HandleHotkey() { Toggle(); }
    private static string HotkeyText(int mods, int key)
    {
        string value = "";
        if ((mods & 2) != 0) value += "Ctrl+";
        if ((mods & 1) != 0) value += "Alt+";
        if ((mods & 4) != 0) value += "Shift+";
        return value + ((Keys)key).ToString();
    }

    private static Icon CreateIcon()
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
            string latest = null;
            string url = null;
            try
            {
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
                byte[] data = Encoding.UTF8.GetBytes("{\"event\":\"launch\",\"page\":\"main\",\"ua\":\"iClock Desktop v" + CURRENT_VERSION + "\"}");
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

            if (!string.IsNullOrEmpty(latest) && IsNewer(latest, CURRENT_VERSION))
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
        });
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
            menuUpdate.ForeColor = Color.FromArgb(28, 114, 190);
            menuUpdate.Font = new Font(tray.ContextMenuStrip.Font, FontStyle.Bold);
            menuUpdate.Click += delegate
            {
                try { Process.Start(updateUrl); } catch { }
            };
            tray.ContextMenuStrip.Items.Insert(0, menuUpdate);
            tray.ContextMenuStrip.Items.Insert(1, new ToolStripSeparator());
        }
    }

    private static bool IsNewer(string latest, string current)
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

    private static string ExtractJsonValue(string json, string key)
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
