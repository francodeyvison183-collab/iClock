using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

// 护眼提醒：连续盯屏计时器。
// 每 30 秒读一次系统的最后输入时间：3 分钟没有任何输入视为离开屏幕（一次真正
// 的休息），累计清零；休息倒计时期间不计时；全屏或设置窗口打开时先不弹卡，
// 等回到普通窗口再提醒，避免打断游戏、会议或正在改设置的用户。
internal sealed class EyeCareTracker
{
    private const int SampleMs = 30000;
    private const int AwayIdleMs = 3 * 60 * 1000;

    private readonly Func<int> intervalMinutes;
    private readonly Func<bool> inBreak;
    private readonly Func<bool> postpone;
    private readonly Action remind;
    private PowerModeChangedEventHandler powerHandler;

    private Timer timer;
    private bool running;
    private long screenSeconds;

    public EyeCareTracker(Func<int> intervalMinutes, Func<bool> inBreak, Func<bool> postpone, Action remind)
    {
        this.intervalMinutes = intervalMinutes;
        this.inBreak = inBreak;
        this.postpone = postpone;
        this.remind = remind;
    }

    public bool IsRunning { get { return running; } }

    public long ScreenSeconds { get { return screenSeconds; } }

    public void Start()
    {
        if (running) return;
        running = true;
        screenSeconds = 0;
        timer = new Timer();
        timer.Interval = SampleMs;
        timer.Tick += delegate { Sample(); };
        timer.Start();
        powerHandler = OnPower;
        try { SystemEvents.PowerModeChanged += powerHandler; } catch { powerHandler = null; }
    }

    public void Stop()
    {
        running = false;
        if (timer != null)
        {
            try { timer.Stop(); timer.Dispose(); } catch { }
            timer = null;
        }
        if (powerHandler != null)
        {
            try { SystemEvents.PowerModeChanged -= powerHandler; } catch { }
            powerHandler = null;
        }
    }

    // 开始休息、锁屏、休眠后调用：重新累计。
    public void Reset()
    {
        screenSeconds = 0;
    }

    public void OnSessionSwitch(SessionSwitchReason reason)
    {
        if (!running) return;
        // 锁屏就是离开屏幕，回来重新计时。
        if (reason == SessionSwitchReason.SessionLock) Reset();
    }

    private void OnPower(object sender, PowerModeChangedEventArgs e)
    {
        if (!running) return;
        if (e.Mode == PowerModes.Suspend) Reset();
    }

    private void Sample()
    {
        if (!running) return;
        try
        {
            NativeMethods.LASTINPUTINFO li = new NativeMethods.LASTINPUTINFO();
            li.cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.LASTINPUTINFO));
            if (!NativeMethods.GetLastInputInfo(ref li)) return;
            uint idleMs = unchecked((uint)Environment.TickCount) - li.dwTime;
            if (idleMs >= (uint)AwayIdleMs)
            {
                Reset();
                return;
            }
            if (inBreak != null && inBreak()) return;
            screenSeconds += SampleMs / 1000;
            int limit = intervalMinutes != null ? intervalMinutes() : 45;
            if (limit < 5) limit = 45;
            if (screenSeconds < limit * 60L) return;
            if (postpone != null && postpone()) return;
            screenSeconds = 0;
            if (remind != null) remind();
        }
        catch { }
    }
}

// 护眼提醒卡片：右下角非激活小卡，20 秒后自动收起。
// 点击「休息 N 分钟」直接接上现有的休息倒计时，选择稍后就从零重新计时。
internal sealed class EyeCareDialog : Form
{
    private readonly Timer autoCloseTimer;
    private readonly Font titleFont;
    private readonly Font bodyFont;
    private readonly Font buttonFont;
    private readonly Action onStartRest;
    private bool startRest;

    protected override bool ShowWithoutActivation { get { return true; } }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE：不抢焦点，正在打字也不打断
            cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW：不占 Alt+Tab
            return cp;
        }
    }

    public EyeCareDialog(Settings s, int intervalMinutes, Action onStartRest)
    {
        this.onStartRest = onStartRest;
        bool en = s.Language == "en";
        titleFont = AppContext.GetUiFont(s.Language, 11.5f, FontStyle.Bold);
        bodyFont = AppContext.GetUiFont(s.Language, 9.5f);
        buttonFont = AppContext.GetUiFont(s.Language, 9.5f);

        Font = bodyFont;
        Text = en ? "Eye care reminder" : "护眼提醒";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = AppContext.Win11Bg;
        DoubleBuffered = true;
        ClientSize = new Size(384, 136);
        AppContext.ApplyModernWindowStyle(this);

        Label title = new Label();
        title.Text = en ? "Time to rest your eyes" : "该让眼睛歇一歇了";
        title.Font = titleFont;
        title.ForeColor = AppContext.Win11TextPrimary;
        title.TextAlign = ContentAlignment.MiddleLeft;
        title.SetBounds(22, 14, 344, 24);
        Controls.Add(title);

        Label body = new Label();
        body.Text = en
            ? "You've been looking at the screen for " + intervalMinutes + " minutes. Look 6 m away for 20 seconds."
            : "已连续用眼 " + intervalMinutes + " 分钟。望向 6 米外约 20 秒，让睫状肌放松一下。";
        body.Font = bodyFont;
        body.ForeColor = AppContext.Win11TextSecondary;
        body.TextAlign = ContentAlignment.TopLeft;
        body.SetBounds(22, 40, 344, 36);
        Controls.Add(body);

        int restMinutes = s.RestMinutes > 0 ? s.RestMinutes : 10;
        Button btnRest = new Button();
        btnRest.Text = en ? "Rest " + restMinutes + " min" : "休息 " + restMinutes + " 分钟";
        btnRest.Font = buttonFont;
        btnRest.SetBounds(22, 84, 168, 34);
        btnRest.AccessibleName = en ? "Start rest" : "开始休息";
        AppContext.StylePrimaryButton(btnRest);
        btnRest.Click += delegate { startRest = true; Close(); };
        Controls.Add(btnRest);

        Button btnLater = new Button();
        btnLater.Text = en ? "Later" : "稍后";
        btnLater.Font = buttonFont;
        btnLater.SetBounds(200, 84, 96, 34);
        btnLater.AccessibleName = en ? "Dismiss" : "稍后提醒";
        AppContext.StyleSecondaryButton(btnLater);
        btnLater.Click += delegate { Close(); };
        Controls.Add(btnLater);

        Paint += delegate(object sender, PaintEventArgs pe)
        {
            using (SolidBrush bar = new SolidBrush(AppContext.Win11Accent))
                pe.Graphics.FillRectangle(bar, 0, 0, 4, ClientSize.Height);
        };

        autoCloseTimer = new Timer();
        autoCloseTimer.Interval = 20000;
        autoCloseTimer.Tick += delegate { Close(); };
    }

    private void PositionBottomRight()
    {
        Screen scr = AppContext.IsSelfCheckRunning ? Screen.PrimaryScreen : (Screen.FromPoint(Cursor.Position) ?? Screen.PrimaryScreen);
        Rectangle wa = scr.WorkingArea;
        Location = new Point(wa.Right - Width - 18, wa.Bottom - Height - 18);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        PositionBottomRight();
        if (autoCloseTimer != null) autoCloseTimer.Start();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        if (autoCloseTimer != null) { try { autoCloseTimer.Stop(); autoCloseTimer.Dispose(); } catch { } }
        try { titleFont.Dispose(); bodyFont.Dispose(); buttonFont.Dispose(); } catch { }
        if (startRest && onStartRest != null) onStartRest();
    }
}
