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

internal enum ClockState
{
    Idle,
    Running,
    Paused,
    RestRunning,
    RestPaused,
    Alarming,
    MoveMode
}

internal sealed class UsageSegment
{
    public DateTime Start;
    public DateTime End;
    public string State;
    public string Exe;
    // Foreground-input samples while this segment was open. Counting samples
    // (not keystrokes) keeps the tracker content-free: ~1 sample per 30s, a
    // segment of heavy use has most samples, light use few.
    public int InputSamples;
    public int ActiveSamples;
    public long Seconds
    {
        get { return Math.Max(0L, (long)Math.Round((End - Start).TotalSeconds)); }
    }
}

internal sealed class UsageSummary
{
    public string State;
    public string Exe;
    public long Seconds;
    public int InputSamples;
    public int ActiveSamples;
}

internal static class UsageLog
{
    public const int IdleMinutes = 3;
    public const int FlickerSeconds = 2;
    public const string Active = "active";
    public const string Idle = "idle";
    public const string Locked = "locked";
    public const string Sleep = "sleep";

    private static readonly Dictionary<string, string> DisplayCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Color> ColorCache = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
    // Hues already handed out, in assignment order, so a new app can dodge
    // colors that are visually identical to an existing one.
    private static readonly List<float> UsedHues = new List<float>();

    public static string DataDir()
    {
        return Path.GetDirectoryName(Settings.FilePath);
    }

    public static string PathFor(string dateKey)
    {
        return Path.Combine(DataDir(), "usage-" + dateKey + ".tsv");
    }

    public static string FormatRow(UsageSegment s)
    {
        return s.Start.ToString("HH:mm:ss") + "\t" + s.End.ToString("HH:mm:ss") + "\t" + (s.State ?? "") + "\t" + (s.Exe ?? "") + "\t" + s.Seconds
            + "\t" + s.InputSamples + "\t" + s.ActiveSamples;
    }

    public static bool TryParseRow(string line, DateTime day, out UsageSegment seg)
    {
        seg = null;
        if (string.IsNullOrEmpty(line)) return false;
        string[] p = line.Split('\t');
        if (p.Length < 5) return false;
        DateTime t0, t1;
        if (!DateTime.TryParseExact(p[0].Trim(), "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out t0)) return false;
        if (!DateTime.TryParseExact(p[1].Trim(), "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out t1)) return false;
        string state = (p[2] ?? "").Trim();
        if (state != Active && state != Idle && state != Locked && state != Sleep) return false;
        seg = new UsageSegment();
        seg.Start = day.Date.Add(t0.TimeOfDay);
        seg.End = day.Date.Add(t1.TimeOfDay);
        if (seg.End < seg.Start) seg.End = seg.End.AddDays(1);
        seg.State = state;
        seg.Exe = state == Active ? (p[3] ?? "").Trim() : "";
        // Columns 6/7 (input sample counts) are optional for older files.
        int n;
        if (p.Length > 5 && int.TryParse(p[5].Trim(), out n)) seg.InputSamples = Math.Max(0, n);
        if (p.Length > 6 && int.TryParse(p[6].Trim(), out n)) seg.ActiveSamples = Math.Max(0, n);
        return true;
    }

    public static List<UsageSegment> LoadDay(string dateKey)
    {
        List<UsageSegment> list = new List<UsageSegment>();
        DateTime day;
        if (!DateTime.TryParseExact(dateKey, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day))
            return list;
        string path = PathFor(dateKey);
        if (!File.Exists(path)) return list;
        try
        {
            foreach (string row in File.ReadAllLines(path, new UTF8Encoding(false)))
            {
                UsageSegment seg;
                if (TryParseRow(row, day, out seg)) list.Add(seg);
            }
        }
        catch { }
        return list;
    }

    public static void Append(UsageSegment s)
    {
        if (s == null || s.Seconds <= 0) return;
        try
        {
            string dir = DataDir();
            Directory.CreateDirectory(dir);
            string path = PathFor(s.Start.ToString("yyyyMMdd"));
            File.AppendAllText(path, FormatRow(s) + Environment.NewLine, new UTF8Encoding(false));
        }
        catch { }
    }

    public static List<UsageSegment> SplitAtDayBoundary(UsageSegment s)
    {
        List<UsageSegment> list = new List<UsageSegment>();
        if (s == null) return list;
        if (s.End.Date <= s.Start.Date)
        {
            list.Add(s);
            return list;
        }
        UsageSegment a = new UsageSegment();
        a.Start = s.Start;
        a.End = s.Start.Date.AddDays(1);
        a.State = s.State;
        a.Exe = s.Exe;
        double frac = (a.End - a.Start).TotalSeconds / Math.Max(1.0, s.Seconds);
        a.InputSamples = (int)Math.Round(s.InputSamples * frac);
        a.ActiveSamples = (int)Math.Round(s.ActiveSamples * frac);
        UsageSegment b = new UsageSegment();
        b.Start = a.End;
        b.End = s.End;
        b.State = s.State;
        b.Exe = s.Exe;
        b.InputSamples = Math.Max(0, s.InputSamples - a.InputSamples);
        b.ActiveSamples = Math.Max(0, s.ActiveSamples - a.ActiveSamples);
        list.Add(a);
        list.AddRange(SplitAtDayBoundary(b));
        return list;
    }

    public static List<UsageSegment> MergeConsecutive(List<UsageSegment> src)
    {
        List<UsageSegment> dst = new List<UsageSegment>();
        if (src == null) return dst;
        for (int i = 0; i < src.Count; i++)
        {
            UsageSegment cur = CloneSeg(src[i]);
            if (dst.Count > 0 && SameKey(dst[dst.Count - 1], cur))
                dst[dst.Count - 1].End = cur.End;
            else
                dst.Add(cur);
        }
        return dst;
    }

    public static List<UsageSegment> AbsorbFlickers(List<UsageSegment> src)
    {
        List<UsageSegment> work = new List<UsageSegment>();
        if (src == null) return work;
        for (int i = 0; i < src.Count; i++) work.Add(CloneSeg(src[i]));
        for (int i = 0; i < work.Count; )
        {
            UsageSegment cur = work[i];
            if (cur.State == Active && cur.Seconds > 0 && cur.Seconds < FlickerSeconds)
            {
                if (i + 1 < work.Count && work[i + 1].State == Active)
                {
                    work[i + 1].Start = cur.Start;
                    work[i + 1].InputSamples += cur.InputSamples;
                    work[i + 1].ActiveSamples += cur.ActiveSamples;
                    work.RemoveAt(i);
                    continue;
                }
                if (i > 0 && work[i - 1].State == Active)
                {
                    work[i - 1].End = cur.End;
                    work[i - 1].InputSamples += cur.InputSamples;
                    work[i - 1].ActiveSamples += cur.ActiveSamples;
                    work.RemoveAt(i);
                    continue;
                }
            }
            i++;
        }
        return MergeConsecutive(work);
    }

    public static List<UsageSummary> Summarize(List<UsageSegment> segs)
    {
        Dictionary<string, UsageSummary> map = new Dictionary<string, UsageSummary>(StringComparer.OrdinalIgnoreCase);
        List<UsageSummary> order = new List<UsageSummary>();
        if (segs == null) return order;
        for (int i = 0; i < segs.Count; i++)
        {
            UsageSegment s = segs[i];
            long sec = s.Seconds;
            if (sec <= 0) continue;
            string key = s.State == Active ? ("a:" + (s.Exe ?? "")) : s.State;
            UsageSummary row;
            if (!map.TryGetValue(key, out row))
            {
                row = new UsageSummary();
                row.State = s.State;
                row.Exe = s.Exe ?? "";
                map[key] = row;
                order.Add(row);
            }
            row.Seconds += sec;
            row.InputSamples += s.InputSamples;
            row.ActiveSamples += s.ActiveSamples;
        }
        order.Sort(delegate(UsageSummary a, UsageSummary b) { return b.Seconds.CompareTo(a.Seconds); });
        return order;
    }

    public static Color ColorFor(string state, string exe)
    {
        if (state == Idle) return Color.FromArgb(200, 204, 210);
        if (state == Locked) return Color.FromArgb(160, 166, 176);
        if (state == Sleep) return Color.FromArgb(120, 126, 136);
        if (state != Active) return Color.FromArgb(230, 232, 236);
        string key = string.IsNullOrEmpty(exe) ? "unknown" : exe;
        Color c;
        if (ColorCache.TryGetValue(key, out c)) return c;
        float hue;
        lock (ColorCache)
        {
            int h = 0;
            string low = key.ToLowerInvariant();
            for (int i = 0; i < low.Length; i++) h = unchecked(h * 33 + low[i]);
            hue = (h & 0xFF) / 255f * 360f;
            // Slide the hue forward until it is clearly distinct from every
            // already-assigned color; the hash only picks the starting point.
            for (int tries = 0; tries < 17; tries++)
            {
                float minDiff = 360f;
                for (int i = 0; i < UsedHues.Count; i++)
                {
                    float d = Math.Abs(UsedHues[i] - hue);
                    if (d > 180f) d = 360f - d;
                    if (d < minDiff) minDiff = d;
                }
                if (minDiff >= 22f) break;
                hue = (hue + 22f) % 360f;
            }
            UsedHues.Add(hue);
        }
        c = ColorFromHsv(hue, 0.52f, 0.84f);
        ColorCache[key] = c;
        return c;
    }

    public static string LabelFor(string state, string exe, bool en)
    {
        // "Not operating (may be watching)" — the person is present but the
        // mouse/keyboard is quiet; leaving the machine is locked/sleep.
        if (state == Idle) return en ? "Not operating" : "未操作（可能在观看）";
        if (state == Locked) return en ? "Locked" : "已锁屏";
        if (state == Sleep) return en ? "Sleep" : "睡眠";
        if (state != Active) return en ? "Untracked" : "未记录";
        return DisplayName(exe);
    }

    public static string DisplayName(string exe)
    {
        if (string.IsNullOrEmpty(exe)) return "unknown";
        string cached;
        if (DisplayCache.TryGetValue(exe, out cached)) return cached;
        string name = exe;
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            name = name.Substring(0, name.Length - 4);
        DisplayCache[exe] = name;
        return name;
    }

    public static void RememberDisplayName(string exe, string fullPath)
    {
        if (string.IsNullOrEmpty(exe) || string.IsNullOrEmpty(fullPath)) return;
        if (DisplayCache.ContainsKey(exe)) return;
        try
        {
            FileVersionInfo info = FileVersionInfo.GetVersionInfo(fullPath);
            string d = info.FileDescription;
            if (string.IsNullOrEmpty(d)) d = info.ProductName;
            if (!string.IsNullOrEmpty(d))
            {
                DisplayCache[exe] = d.Trim();
                return;
            }
        }
        catch { }
        DisplayName(exe);
    }

    public static bool IsIgnoredExe(string exe)
    {
        if (string.IsNullOrEmpty(exe)) return false;
        string low = exe.ToLowerInvariant();
        // explorer 是 shell:桌面、任务栏、托盘通知全由它承载,GetForegroundWindow 经常返回
        // shell 窗口而非用户实际打开的文件夹窗口,记录它会产生大量假时长(用户根本没开
        // 资源管理器却记了几小时)。文件管理交互通常很短,误杀代价远小于虚报。
        return low == "lockapp.exe" || low == "logonui.exe" || low == "scrnsave.scr"
            || low == "iclock.scr" || low == "explorer.exe" || low.EndsWith(".scr");
    }

    private static bool SameKey(UsageSegment a, UsageSegment b)
    {
        if (a == null || b == null) return false;
        if (a.State != b.State) return false;
        if (a.State == Active)
            return string.Equals(a.Exe ?? "", b.Exe ?? "", StringComparison.OrdinalIgnoreCase);
        return true;
    }

    private static UsageSegment CloneSeg(UsageSegment s)
    {
        UsageSegment n = new UsageSegment();
        n.Start = s.Start; n.End = s.End; n.State = s.State; n.Exe = s.Exe;
        n.InputSamples = s.InputSamples; n.ActiveSamples = s.ActiveSamples;
        return n;
    }

    private static Color ColorFromHsv(float h, float s, float v)
    {
        float c = v * s;
        float x = c * (1f - Math.Abs((h / 60f) % 2f - 1f));
        float m = v - c;
        float r = 0, g = 0, b = 0;
        if (h < 60) { r = c; g = x; }
        else if (h < 120) { r = x; g = c; }
        else if (h < 180) { g = c; b = x; }
        else if (h < 240) { g = x; b = c; }
        else if (h < 300) { r = x; b = c; }
        else { r = c; b = x; }
        return Color.FromArgb(255, (int)((r + m) * 255), (int)((g + m) * 255), (int)((b + m) * 255));
    }
}

internal sealed class UsageTracker
{
    private readonly Control invokeTarget;
    private NativeMethods.WinEventDelegate hookProc;
    private IntPtr hook = IntPtr.Zero;
    private Timer idleTimer;
    private PowerModeChangedEventHandler powerHandler;
    private UsageSegment open;
    private bool running;
    private bool suppressChanged;
    private uint lastInputTick;

    public event Action Changed;

    public UsageTracker(Control invokeTarget)
    {
        this.invokeTarget = invokeTarget;
    }

    public bool IsRunning { get { return running; } }

    public void Start()
    {
        if (running) return;
        running = true;
        suppressChanged = true;
        hookProc = OnWinEvent;
        try
        {
            hook = NativeMethods.SetWinEventHook(
                NativeMethods.EVENT_SYSTEM_FOREGROUND, NativeMethods.EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero, hookProc, 0, 0,
                NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);
        }
        catch { hook = IntPtr.Zero; }
        idleTimer = new Timer();
        idleTimer.Interval = 30000;
        idleTimer.Tick += delegate { CheckIdle(); };
        idleTimer.Start();
        powerHandler = OnPower;
        try { SystemEvents.PowerModeChanged += powerHandler; } catch { powerHandler = null; }
        SampleForeground();
        suppressChanged = false;
        RaiseChanged();
    }

    public void Stop()
    {
        if (!running) return;
        Flush();
        running = false;
        if (hook != IntPtr.Zero)
        {
            try { NativeMethods.UnhookWinEvent(hook); } catch { }
            hook = IntPtr.Zero;
        }
        hookProc = null;
        if (idleTimer != null)
        {
            try { idleTimer.Stop(); idleTimer.Dispose(); } catch { }
            idleTimer = null;
        }
        if (powerHandler != null)
        {
            try { SystemEvents.PowerModeChanged -= powerHandler; } catch { }
            powerHandler = null;
        }
        open = null;
        RaiseChanged();
    }

    public void Flush()
    {
        if (open == null) return;
        CloseOpen(DateTime.Now);
    }

    public void OnSessionSwitch(SessionSwitchReason reason)
    {
        if (!running) return;
        if (reason == SessionSwitchReason.SessionLock)
            Transition(UsageLog.Locked, "");
        else if (reason == SessionSwitchReason.SessionUnlock)
            SampleForeground();
    }

    public List<UsageSegment> GetDayWithOpen(string dateKey)
    {
        List<UsageSegment> list = UsageLog.LoadDay(dateKey);
        if (open != null && open.Start.ToString("yyyyMMdd") == dateKey)
        {
            UsageSegment live = new UsageSegment();
            live.Start = open.Start;
            live.End = DateTime.Now;
            live.State = open.State;
            live.Exe = open.Exe;
            if (live.Seconds > 0) list.Add(live);
        }
        return UsageLog.MergeConsecutive(list);
    }

    private void OnWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (!running) return;
        if (invokeTarget != null && invokeTarget.IsHandleCreated && invokeTarget.InvokeRequired)
        {
            try { invokeTarget.BeginInvoke(new Action(SampleForeground)); } catch { }
            return;
        }
        SampleForeground();
    }

    private void OnPower(object sender, PowerModeChangedEventArgs e)
    {
        if (!running) return;
        if (e.Mode == PowerModes.Suspend) Transition(UsageLog.Sleep, "");
        else if (e.Mode == PowerModes.Resume) SampleForeground();
    }

    private void CheckIdle()
    {
        if (!running) return;
        if (open != null && (open.State == UsageLog.Locked || open.State == UsageLog.Sleep)) return;
        try
        {
            NativeMethods.LASTINPUTINFO li = new NativeMethods.LASTINPUTINFO();
            li.cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.LASTINPUTINFO));
            if (!NativeMethods.GetLastInputInfo(ref li)) return;
            uint idleMs = ((uint)Environment.TickCount) - li.dwTime;
            // One sample per 30s tick: input happened since the last sample when
            // the last-input tick advanced. Counted, never recorded as content.
            if (open != null && li.dwTime != lastInputTick)
            {
                open.InputSamples++;
                if (idleMs < (uint)(UsageLog.IdleMinutes * 60 * 1000)) open.ActiveSamples++;
            }
            lastInputTick = li.dwTime;
            if (idleMs >= (uint)(UsageLog.IdleMinutes * 60 * 1000))
            {
                if (open == null || open.State != UsageLog.Idle)
                    Transition(UsageLog.Idle, "");
            }
            else if (open != null && open.State == UsageLog.Idle)
            {
                SampleForeground();
            }
        }
        catch { }
        RaiseChanged();
    }

    private void SampleForeground()
    {
        if (!running) return;
        string exe, path;
        if (!TryGetForeground(out exe, out path))
        {
            Transition(UsageLog.Active, "unknown");
            return;
        }
        if (UsageLog.IsIgnoredExe(exe))
        {
            // shell 等被排除的进程拿到前台时关掉当前 open 段,否则上一个应用的
            // 时间段会把「其实没在用电脑」的时长也吞进去。
            Transition(UsageLog.Idle, "");
            return;
        }
        UsageLog.RememberDisplayName(exe, path);
        Transition(UsageLog.Active, exe);
    }

    private bool TryGetForeground(out string exe, out string fullPath)
    {
        exe = null;
        fullPath = null;
        try
        {
            IntPtr fg = NativeMethods.GetForegroundWindow();
            if (fg == IntPtr.Zero) return false;
            uint pid = 0;
            NativeMethods.GetWindowThreadProcessId(fg, out pid);
            if (pid == 0) return false;
            fullPath = GetImagePath((int)pid);
            if (!string.IsNullOrEmpty(fullPath))
            {
                exe = Path.GetFileName(fullPath);
                return true;
            }
            exe = FindExeByPid((int)pid);
            return !string.IsNullOrEmpty(exe);
        }
        catch { return false; }
    }

    private static string GetImagePath(int pid)
    {
        IntPtr hProc = NativeMethods.OpenProcess(0x1000, false, pid);
        if (hProc == IntPtr.Zero) return null;
        try
        {
            StringBuilder sb = new StringBuilder(1024);
            int size = sb.Capacity;
            if (NativeMethods.QueryFullProcessImageName(hProc, 0, sb, ref size))
                return sb.ToString();
        }
        catch { }
        finally { NativeMethods.CloseHandle(hProc); }
        return null;
    }

    private static string FindExeByPid(int pid)
    {
        string found = null;
        AppContext.EnumerateSystemProcesses(delegate(int p, string name)
        {
            if (p == pid && !string.IsNullOrEmpty(name)) found = name;
        });
        return found;
    }

    private void Transition(string state, string exe)
    {
        DateTime now = DateTime.Now;
        if (open != null
            && open.State == UsageLog.Active && state == UsageLog.Active
            && open.Seconds < UsageLog.FlickerSeconds)
        {
            open.Exe = exe;
            RaiseChanged();
            return;
        }
        if (open != null && open.State == state
            && (state != UsageLog.Active || string.Equals(open.Exe ?? "", exe ?? "", StringComparison.OrdinalIgnoreCase)))
        {
            RaiseChanged();
            return;
        }
        CloseOpen(now);
        open = new UsageSegment();
        open.Start = now;
        open.End = now;
        open.State = state;
        open.Exe = state == UsageLog.Active ? (exe ?? "") : "";
        RaiseChanged();
    }

    private void CloseOpen(DateTime now)
    {
        if (open == null) return;
        open.End = now;
        List<UsageSegment> parts = UsageLog.SplitAtDayBoundary(open);
        for (int i = 0; i < parts.Count; i++)
            UsageLog.Append(parts[i]);
        open = null;
    }

    private void RaiseChanged()
    {
        if (suppressChanged) return;
        Action h = Changed;
        if (h == null) return;
        try { h(); } catch { }
    }
}

internal sealed class HistoryMenuItem : ToolStripMenuItem
{
    private List<UsageSegment> segments;
    private DateTime axisStart, axisEnd;
    public bool ShowStrip;

    public override Size GetPreferredSize(Size constrainingSize)
    {
        Size sz = base.GetPreferredSize(constrainingSize);
        return ShowStrip ? new Size(sz.Width, 38) : sz;
    }

    public void SetSegments(List<UsageSegment> segs, DateTime start, DateTime end, bool show)
    {
        ShowStrip = show;
        segments = segs;
        axisStart = start;
        axisEnd = end <= start ? start.AddMinutes(1) : end;
        Invalidate();
    }

    internal void DrawStrip(Graphics g, int width, int height, bool selected)
    {
        if (!ShowStrip) return;
        int left = ModernMenuRenderer.MenuTextLeft;
        int right = width - ModernMenuRenderer.MenuShortcutRight - 2;
        int barH = 6;
        Rectangle rc = new Rectangle(left, height - barH - 6, Math.Max(8, right - left), barH);
        SmoothingMode prev = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (GraphicsPath path = AppContext.GetRoundedRectPath(rc, 3))
        {
            using (SolidBrush bg = new SolidBrush(selected ? Color.FromArgb(220, 224, 230) : Color.FromArgb(230, 232, 236)))
                g.FillPath(bg, path);
            if (segments != null && segments.Count > 0)
            {
                double span = Math.Max(1.0, (axisEnd - axisStart).TotalSeconds);
                Region old = g.Clip;
                g.SetClip(path);
                for (int i = 0; i < segments.Count; i++)
                {
                    UsageSegment s = segments[i];
                    double a = (s.Start - axisStart).TotalSeconds / span;
                    double b = (s.End - axisStart).TotalSeconds / span;
                    if (b <= 0 || a >= 1) continue;
                    if (a < 0) a = 0;
                    if (b > 1) b = 1;
                    int x = rc.X + (int)Math.Round(a * rc.Width);
                    int w = Math.Max(1, (int)Math.Round((b - a) * rc.Width));
                    using (SolidBrush br = new SolidBrush(UsageLog.ColorFor(s.State, s.Exe)))
                        g.FillRectangle(br, x, rc.Y, w, rc.Height);
                }
                g.Clip = old;
            }
        }
        g.SmoothingMode = prev;
    }
}

internal sealed class UsageAxisPanel : Panel
{
    private List<UsageSegment> segments = new List<UsageSegment>();
    private DateTime axisStart = DateTime.Today;
    private DateTime axisEnd = DateTime.Now;
    private DateTime dayStart = DateTime.Today;
    private DateTime dayEnd = DateTime.Now;
    private string highlightKey = "";
    private bool en;
    private bool fullDay;
    // 倒计时周期标记（意图）叠加在用时色带（现实）上：条内底部的强调色刻度。
    // State 复用为结果标记："done" 表示完成或进行中，其余为中断/重置。
    private List<UsageSegment> focusMarks = new List<UsageSegment>();

    public UsageAxisPanel()
    {
        DoubleBuffered = true;
        BackColor = Color.White;
        Cursor = Cursors.Hand;
    }

    public void SetData(List<UsageSegment> segs, DateTime start, DateTime end, string highlight, bool english)
    {
        segments = segs != null ? segs : new List<UsageSegment>();
        highlightKey = highlight ?? "";
        en = english;
        // start/end is the activity window (first-to-last record); remember it
        // so clicking can toggle between it and the full-day view without the
        // caller re-feeding data.
        dayStart = start;
        dayEnd = end;
        ApplyRange();
    }

    // Activity window = the day's real first-to-last activity; full day = midnight to midnight.
    public void SetDayBounds(DateTime dayStart, DateTime dayEnd)
    {
        this.dayStart = dayStart;
        this.dayEnd = dayEnd;
        ApplyRange();
    }

    public bool ToggleZoom()
    {
        fullDay = !fullDay;
        ApplyRange();
        return fullDay;
    }

    private void ApplyRange()
    {
        if (fullDay)
        {
            axisStart = dayStart.Date;
            axisEnd = dayStart.Date.AddDays(1);
        }
        else
        {
            axisStart = dayStart;
            axisEnd = dayEnd <= dayStart ? dayStart.AddMinutes(1) : dayEnd;
        }
        Invalidate();
    }

    public void SetHighlight(string highlight)
    {
        highlightKey = highlight ?? "";
        Invalidate();
    }

    public void SetFocusMarkers(List<UsageSegment> marks)
    {
        focusMarks = marks != null ? marks : new List<UsageSegment>();
        Invalidate();
    }

    public UsageSegment FocusMarkerAt(int x)
    {
        Rectangle bar = BarRect();
        if (bar.Width <= 0 || x < bar.X || x > bar.Right) return null;
        double span = Math.Max(1.0, (axisEnd - axisStart).TotalSeconds);
        double t = (x - bar.X) / (double)bar.Width;
        DateTime at = axisStart.AddSeconds(t * span);
        for (int i = 0; i < focusMarks.Count; i++)
        {
            if (at >= focusMarks[i].Start && at < focusMarks[i].End) return focusMarks[i];
        }
        return null;
    }

    public UsageSegment HitTest(int x)
    {
        Rectangle bar = BarRect();
        if (bar.Width <= 0 || !bar.Contains(x, bar.Y + bar.Height / 2)) return null;
        double span = Math.Max(1.0, (axisEnd - axisStart).TotalSeconds);
        double t = (x - bar.X) / (double)bar.Width;
        DateTime at = axisStart.AddSeconds(t * span);
        for (int i = 0; i < segments.Count; i++)
        {
            if (at >= segments[i].Start && at < segments[i].End) return segments[i];
        }
        return null;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle bar = BarRect();
        using (SolidBrush bg = new SolidBrush(Color.FromArgb(236, 238, 242)))
            g.FillRectangle(bg, bar);
        double span = Math.Max(1.0, (axisEnd - axisStart).TotalSeconds);
        DateTime untrackedEnd = segments.Count > 0 ? segments[0].Start : axisEnd;
        if (untrackedEnd > axisStart)
        {
            int x0 = bar.X;
            int w0 = Math.Max(1, (int)Math.Round((untrackedEnd - axisStart).TotalSeconds / span * bar.Width));
            using (SolidBrush br = new SolidBrush(UsageLog.ColorFor("", "")))
                g.FillRectangle(br, x0, bar.Y, w0, bar.Height);
        }
        for (int i = 0; i < segments.Count; i++)
        {
            UsageSegment s = segments[i];
            double a = (s.Start - axisStart).TotalSeconds / span;
            double b = (s.End - axisStart).TotalSeconds / span;
            if (b <= 0 || a >= 1) continue;
            if (a < 0) a = 0;
            if (b > 1) b = 1;
            int x = bar.X + (int)Math.Round(a * bar.Width);
            int w = Math.Max(1, (int)Math.Round((b - a) * bar.Width));
            Color col = UsageLog.ColorFor(s.State, s.Exe);
            string key = s.State == UsageLog.Active ? ("a:" + (s.Exe ?? "")) : s.State;
            if (!string.IsNullOrEmpty(highlightKey) && !string.Equals(key, highlightKey, StringComparison.OrdinalIgnoreCase))
                col = Color.FromArgb(90, col);
            using (SolidBrush br = new SolidBrush(col))
                g.FillRectangle(br, x, bar.Y, w, bar.Height);
        }
        using (Pen p = new Pen(Color.FromArgb(215, 220, 228)))
            g.DrawRectangle(p, bar.X, bar.Y, bar.Width - 1, bar.Height - 1);

        // 倒计时周期标记：条内底部的强调色刻度 + 两端竖线。
        // 完成的实心，中断/重置的半透明 —— 意图叠在现实之上。
        if (focusMarks.Count > 0)
        {
            for (int i = 0; i < focusMarks.Count; i++)
            {
                UsageSegment m = focusMarks[i];
                double fa = (m.Start - axisStart).TotalSeconds / span;
                double fb = (m.End - axisStart).TotalSeconds / span;
                if (fb <= 0 || fa >= 1) continue;
                if (fa < 0) fa = 0;
                if (fb > 1) fb = 1;
                int mx = bar.X + (int)Math.Round(fa * bar.Width);
                int mw = Math.Max(2, (int)Math.Round((fb - fa) * bar.Width));
                Color mc = m.State == "done" ? AppContext.Win11Accent : Color.FromArgb(130, AppContext.Win11Accent);
                using (SolidBrush br = new SolidBrush(mc))
                    g.FillRectangle(br, mx, bar.Bottom - 4, mw, 3);
                using (Pen pen = new Pen(mc))
                {
                    g.DrawLine(pen, mx, bar.Bottom - 8, mx, bar.Bottom - 1);
                    g.DrawLine(pen, mx + mw - 1, bar.Bottom - 8, mx + mw - 1, bar.Bottom - 1);
                }
            }
        }

        Font tickFont = AppContext.GetUiFont(en ? "en" : "zh", 8f);
        try
        {
            int hours = Math.Max(1, (int)Math.Ceiling(span / 3600.0));
            int step = hours <= 8 ? 1 : (hours <= 16 ? 2 : 4);
            DateTime tick = new DateTime(axisStart.Year, axisStart.Month, axisStart.Day, axisStart.Hour, 0, 0);
            if (tick < axisStart) tick = tick.AddHours(1);
            TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            while (tick <= axisEnd)
            {
                double a = (tick - axisStart).TotalSeconds / span;
                int x = bar.X + (int)Math.Round(a * bar.Width);
                using (Pen p = new Pen(Color.FromArgb(200, 204, 210)))
                    g.DrawLine(p, x, bar.Bottom, x, bar.Bottom + 3);
                string label = tick.ToString("HH:mm");
                Size sz = TextRenderer.MeasureText(label, tickFont, Size.Empty, flags);
                // Center on the tick but keep the label inside the panel: the
                // first/last ticks sit at the bar's edges and would otherwise
                // be clipped mid-character.
                int lx = Math.Max(0, Math.Min(Width - sz.Width, x - sz.Width / 2));
                TextRenderer.DrawText(g, label, tickFont, new Rectangle(lx, bar.Bottom + 3, sz.Width, 14), AppContext.Win11ShortcutGray, flags);
                tick = tick.AddHours(step);
            }
        }
        finally { tickFont.Dispose(); }

    }

    private Rectangle BarRect()
    {
        return new Rectangle(8, 4, Math.Max(8, Width - 16), 16);
    }
}

internal sealed class TrendDayStat
{
    public string DateKey = "";
    public long ScreenSecs;
    public long FocusSecs;
    public int FocusSessions;
    public int FocusDone;
}

internal sealed class TrendAppStat
{
    public string Exe = "";
    public long Seconds;
}

// 趋势视图面板：上半部分是「屏幕活跃 vs 专注」双序列日对比图，
// 下半部分是窗口内应用时长排行。只读已有 TSV 数据，不做任何采集。
internal sealed class TrendPanel : Panel
{
    private List<TrendDayStat> days = new List<TrendDayStat>();
    private List<TrendAppStat> apps = new List<TrendAppStat>();
    private bool en;
    private readonly ToolTip tip;
    private string tipText = "";

    public TrendPanel()
    {
        DoubleBuffered = true;
        BackColor = Color.White;
        tip = new ToolTip();
        MouseMove += OnMouseMove;
        MouseLeave += delegate { SetTip(""); };
        Disposed += delegate { tip.Dispose(); };
    }

    public void SetData(List<TrendDayStat> dayList, List<TrendAppStat> appList, bool english)
    {
        days = dayList != null ? dayList : new List<TrendDayStat>();
        apps = appList != null ? appList : new List<TrendAppStat>();
        en = english;
        SetTip("");
        Invalidate();
    }

    private Rectangle ChartRect()
    {
        return new Rectangle(48, 26, Math.Max(40, Width - 66), Math.Max(40, Height / 2 - 40));
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        int idx = DayIndexAt(e.X, e.Y);
        if (idx < 0) { SetTip(""); return; }
        TrendDayStat d = days[idx];
        DateTime dt;
        string label = d.DateKey;
        if (DateTime.TryParseExact(d.DateKey, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
            label = dt.ToString("MM-dd") + " " + dt.ToString("ddd", en ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo("zh-CN"));
        SetTip(label + "\r\n"
            + (en ? "Screen active " : "屏幕活跃 ") + FormatDur(d.ScreenSecs) + "\r\n"
            + (en ? "Focus " : "专注 ") + FormatDur(d.FocusSecs)
            + (d.FocusSessions > 0
                ? (en ? " (" + d.FocusDone + "/" + d.FocusSessions + " done)" : "（完成 " + d.FocusDone + "/" + d.FocusSessions + "）")
                : ""));
    }

    private void SetTip(string text)
    {
        if (text == tipText) return;
        tipText = text;
        tip.SetToolTip(this, text);
    }

    private int DayIndexAt(int x, int y)
    {
        Rectangle chart = ChartRect();
        if (days.Count == 0 || x < chart.X || x > chart.Right || y < chart.Y || y > chart.Bottom) return -1;
        double slot = chart.Width / (double)days.Count;
        int idx = (int)((x - chart.X) / slot);
        return Math.Min(days.Count - 1, Math.Max(0, idx));
    }

    private string FormatDur(long secs)
    {
        long h = secs / 3600, m = (secs % 3600) / 60;
        if (h > 0) return en ? h + "h " + m + "m" : h + " 小时 " + m + " 分钟";
        if (m > 0) return en ? m + "m" : m + " 分钟";
        return en ? secs + "s" : secs + " 秒";
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Font font = AppContext.GetUiFont(en ? "en" : "zh", 9f);
        Font small = AppContext.GetUiFont(en ? "en" : "zh", 8f);
        Font smallBold = AppContext.GetUiFont(en ? "en" : "zh", 8f, FontStyle.Bold);
        try
        {
            Color axisGray = AppContext.Win11ShortcutGray;
            Color screenColor = Color.FromArgb(196, 201, 209);
            Color focusColor = AppContext.Win11Accent;
            TextFormatFlags nf = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;

            // 图例（右上角）
            string lgScreen = en ? "Screen active" : "屏幕活跃";
            string lgFocus = en ? "Focus" : "专注";
            Size szS = TextRenderer.MeasureText(g, lgScreen, small, Size.Empty, nf);
            Size szF = TextRenderer.MeasureText(g, lgFocus, small, Size.Empty, nf);
            int lgW = 12 + 4 + szS.Width + 14 + 12 + 4 + szF.Width;
            int lgX = Math.Max(8, Width - lgW - 10);
            using (SolidBrush br = new SolidBrush(screenColor)) g.FillRectangle(br, lgX, 6, 12, 8);
            TextRenderer.DrawText(g, lgScreen, small, new Point(lgX + 16, 4), AppContext.Win11TextSecondary, nf);
            int fx = lgX + 16 + szS.Width + 14;
            using (SolidBrush br = new SolidBrush(focusColor)) g.FillRectangle(br, fx, 6, 12, 8);
            TextRenderer.DrawText(g, lgFocus, small, new Point(fx + 16, 4), AppContext.Win11TextSecondary, nf);

            Rectangle chart = ChartRect();
            if (days.Count == 0)
            {
                TextRenderer.DrawText(g, en ? "No data yet" : "暂无数据", font, chart, axisGray,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
            else
            {
                long scaleSecs = 3600;
                for (int i = 0; i < days.Count; i++)
                    scaleSecs = Math.Max(scaleSecs, Math.Max(days[i].ScreenSecs, days[i].FocusSecs));
                // 刻度向上取整到半小时，网格线才落在好读的数字上。
                scaleSecs = ((scaleSecs + 1799) / 1800) * 1800;

                for (int i = 0; i <= 4; i++)
                {
                    int y = chart.Bottom - (int)Math.Round(chart.Height * i / 4.0);
                    using (Pen p = new Pen(i == 0 ? Color.FromArgb(210, 214, 222) : Color.FromArgb(232, 235, 240)))
                        g.DrawLine(p, chart.X, y, chart.Right, y);
                    if (i == 0) continue;
                    long v = scaleSecs * i / 4;
                    string lbl = v >= 3600
                        ? ((v % 3600 == 0) ? (v / 3600).ToString() : (v / 3600.0).ToString("0.#")) + "h"
                        : (v / 60).ToString() + "m";
                    Size sz = TextRenderer.MeasureText(g, lbl, small, Size.Empty, nf);
                    TextRenderer.DrawText(g, lbl, small, new Point(Math.Max(0, chart.X - 6 - sz.Width), y - sz.Height / 2), axisGray, nf);
                }

                // 双序列柱：左灰=屏幕活跃，右蓝=专注。今天的日期标签用强调色加粗。
                int n = days.Count;
                double slot = chart.Width / (double)n;
                int barW = Math.Max(3, Math.Min(12, (int)(slot * 0.28)));
                string todayKey = DateTime.Today.ToString("yyyyMMdd");
                int labelStep = n <= 8 ? 1 : (int)Math.Ceiling(n / 7.0);
                for (int i = 0; i < n; i++)
                {
                    TrendDayStat d = days[i];
                    int cx = chart.X + (int)Math.Round((i + 0.5) * slot);
                    int hS = (int)Math.Round(d.ScreenSecs / (double)scaleSecs * chart.Height);
                    int hF = (int)Math.Round(d.FocusSecs / (double)scaleSecs * chart.Height);
                    if (d.ScreenSecs > 0 && hS < 2) hS = 2;
                    if (d.FocusSecs > 0 && hF < 2) hF = 2;
                    if (hS > 0)
                    {
                        using (SolidBrush br = new SolidBrush(screenColor))
                            g.FillRectangle(br, cx - barW - 1, chart.Bottom - hS, barW, hS);
                    }
                    if (hF > 0)
                    {
                        using (SolidBrush br = new SolidBrush(focusColor))
                            g.FillRectangle(br, cx + 1, chart.Bottom - hF, barW, hF);
                    }
                    if (i % labelStep == 0 || i == n - 1)
                    {
                        DateTime dt;
                        string lbl = d.DateKey;
                        if (DateTime.TryParseExact(d.DateKey, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                            lbl = dt.ToString("MM-dd");
                        bool isToday = d.DateKey == todayKey;
                        Font lf = isToday ? smallBold : small;
                        Size sz = TextRenderer.MeasureText(g, lbl, lf, Size.Empty, nf);
                        int lx = Math.Max(0, Math.Min(Width - sz.Width, cx - sz.Width / 2));
                        TextRenderer.DrawText(g, lbl, lf, new Point(lx, chart.Bottom + 5), isToday ? focusColor : axisGray, nf);
                    }
                }
            }

            // 分隔线 + 应用排行
            int divY = chart.Bottom + 24;
            using (Pen p = new Pen(Color.FromArgb(232, 235, 240)))
                g.DrawLine(p, 12, divY, Width - 12, divY);
            TextRenderer.DrawText(g, en ? "Top apps in this window" : "窗口内应用排行", font,
                new Point(12, divY + 8), AppContext.Win11TextPrimary, nf);

            int rowsY = divY + 32;
            int rowH = 21;
            int maxRows = Math.Max(1, (Height - rowsY - 4) / rowH);
            if (apps.Count == 0)
            {
                TextRenderer.DrawText(g, en ? "(no screen-active records yet)" : "（暂无屏幕活跃记录）", small,
                    new Point(12, rowsY + 2), axisGray, nf);
            }
            else
            {
                long maxApp = Math.Max(1, apps[0].Seconds);
                int barX = 208;
                int durW = 110;
                int barMaxW = Math.Max(40, Width - barX - durW - 22);
                int count = Math.Min(apps.Count, maxRows);
                for (int i = 0; i < count; i++)
                {
                    int y = rowsY + i * rowH;
                    TextRenderer.DrawText(g, (i + 1).ToString(), small, new Point(12, y + 2), axisGray, nf);
                    string name = UsageLog.DisplayName(apps[i].Exe);
                    TextRenderer.DrawText(g, name, font, new Rectangle(32, y, barX - 44, rowH), AppContext.Win11TextPrimary,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.WordEllipsis | TextFormatFlags.NoPadding);
                    Rectangle bar = new Rectangle(barX, y + rowH / 2 - 5, barMaxW, 10);
                    using (SolidBrush bg = new SolidBrush(Color.FromArgb(240, 242, 245)))
                        g.FillRectangle(bg, bar);
                    int bw = (int)Math.Round(apps[i].Seconds / (double)maxApp * barMaxW);
                    if (bw > 0)
                    {
                        using (SolidBrush br = new SolidBrush(UsageLog.ColorFor(UsageLog.Active, apps[i].Exe)))
                            g.FillRectangle(br, bar.X, bar.Y, bw, bar.Height);
                    }
                    TextRenderer.DrawText(g, FormatDur(apps[i].Seconds), small,
                        new Rectangle(Width - durW - 12, y + 2, durW, rowH), AppContext.Win11TextSecondary,
                        TextFormatFlags.Right | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
                }
            }
        }
        finally
        {
            font.Dispose();
            small.Dispose();
            smallBold.Dispose();
        }
    }
}

