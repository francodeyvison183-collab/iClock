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
