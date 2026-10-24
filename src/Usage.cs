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
        return s.Start.ToString("HH:mm:ss") + "\t" + s.End.ToString("HH:mm:ss") + "\t" + (s.State ?? "") + "\t" + (s.Exe ?? "") + "\t" + s.Seconds;
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
        UsageSegment b = new UsageSegment();
        b.Start = a.End;
        b.End = s.End;
        b.State = s.State;
        b.Exe = s.Exe;
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
                    work.RemoveAt(i);
                    continue;
                }
                if (i > 0 && work[i - 1].State == Active)
                {
                    work[i - 1].End = cur.End;
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
        }
        order.Sort(delegate(UsageSummary a, UsageSummary b) { return b.Seconds.CompareTo(a.Seconds); });
        return order;
    }

    public static DateTime EstimateBootOrMidnight()
    {
        DateTime now = DateTime.Now;
        try
        {
            DateTime boot = now - TimeSpan.FromMilliseconds((double)NativeMethods.GetTickCount64());
            if (boot.Date == now.Date) return boot;
        }
        catch { }
        return now.Date;
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
        int h = 0;
        string low = key.ToLowerInvariant();
        for (int i = 0; i < low.Length; i++) h = unchecked(h * 33 + low[i]);
        float hue = (h & 0xFF) / 255f * 360f;
        c = ColorFromHsv(hue, 0.52f, 0.84f);
        ColorCache[key] = c;
        return c;
    }

    public static string LabelFor(string state, string exe, bool en)
    {
        if (state == Idle) return en ? "Idle" : "空闲";
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
        return low == "lockapp.exe" || low == "logonui.exe" || low == "scrnsave.scr"
            || low == "iclock.scr" || low.EndsWith(".scr");
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
        if (UsageLog.IsIgnoredExe(exe)) return;
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

internal sealed class UsageStripItem : ToolStripItem
{
    private List<UsageSegment> segments = new List<UsageSegment>();
    private DateTime axisStart = DateTime.Today;
    private DateTime axisEnd = DateTime.Now;

    public UsageStripItem()
    {
        AutoSize = true;
        Height = 10;
        Overflow = ToolStripItemOverflow.Never;
        Margin = Padding.Empty;
        Padding = Padding.Empty;
        DisplayStyle = ToolStripItemDisplayStyle.None;
        Text = "";
    }

    public override Size GetPreferredSize(Size constrainingSize)
    {
        return new Size(0, Height);
    }

    public void SetSegments(List<UsageSegment> segs, DateTime start, DateTime end)
    {
        segments = segs != null ? segs : new List<UsageSegment>();
        axisStart = start;
        axisEnd = end <= start ? start.AddMinutes(1) : end;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        // 这条不是菜单项，布局把它放在 Padding.Left（X=8），文字项在 X=0。
        // 绘制时扣掉这个偏移，左边才和文字对齐，右边才和开关胶囊对齐。
        int shift = Bounds.X;
        int left = ModernMenuRenderer.MenuTextLeft - shift;
        int right = Width - ModernMenuRenderer.MenuShortcutRight - 2 - shift;
        if (left < 0) left = 0;
        int barH = 6;
        int y = Math.Max(0, (Height - barH) / 2);
        Rectangle rc = new Rectangle(left, y, Math.Max(8, right - left), barH);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (GraphicsPath path = AppContext.GetRoundedRectPath(rc, 4))
        {
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(230, 232, 236)))
                e.Graphics.FillPath(bg, path);
            if (segments != null && segments.Count > 0)
            {
                double span = Math.Max(1.0, (axisEnd - axisStart).TotalSeconds);
                Region old = e.Graphics.Clip;
                e.Graphics.SetClip(path);
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
                        e.Graphics.FillRectangle(br, x, rc.Y, w, rc.Height);
                }
                e.Graphics.Clip = old;
            }
        }
    }
}

internal sealed class UsageAxisPanel : Panel
{
    private List<UsageSegment> segments = new List<UsageSegment>();
    private DateTime axisStart = DateTime.Today;
    private DateTime axisEnd = DateTime.Now;
    private string highlightKey = "";
    private bool en;

    public UsageAxisPanel()
    {
        DoubleBuffered = true;
        BackColor = Color.White;
    }

    public void SetData(List<UsageSegment> segs, DateTime start, DateTime end, string highlight, bool english)
    {
        segments = segs != null ? segs : new List<UsageSegment>();
        axisStart = start;
        axisEnd = end <= start ? start.AddMinutes(1) : end;
        highlightKey = highlight ?? "";
        en = english;
        Invalidate();
    }

    public void SetHighlight(string highlight)
    {
        highlightKey = highlight ?? "";
        Invalidate();
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
                TextRenderer.DrawText(g, label, tickFont, new Rectangle(x - sz.Width / 2, bar.Bottom + 3, sz.Width, 14), AppContext.Win11ShortcutGray, flags);
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

