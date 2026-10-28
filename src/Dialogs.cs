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

internal sealed class NoticeDialog : Form
{
    private readonly Settings settings;
    private SoundPlayer player;
    private Timer timer;
    private DateTime finishTime;
    private Label finishLabel;

    private IntPtr targetHwnd;
    private bool isAgentNotice;
    // CLI 终端提醒与护眼提醒也复用本弹窗（强提醒统一入口）。
    private readonly bool isCli;
    private readonly bool isEyeCare;
    private Font msgFont;
    // Chosen rest length shown on the "休息 N 分钟" button (arrow zone opens a preset menu).
    private int restMinutes = 10;
    // Kept alive for the lifetime of the popup: a tooltip held only by a
    // local can be collected and silently stop showing.
    private ToolTip restTip;
    // The preset menu, kept as a field so the arrow zone can be tested.
    private ContextMenuStrip restMenu;

    public NoticeDialog(Settings s, DateTime finishedAt) : this(s, finishedAt, null, IntPtr.Zero, null) { }

    public NoticeDialog(Settings s, DateTime finishedAt, string agentName, IntPtr targetWindow) : this(s, finishedAt, agentName, targetWindow, null, null) { }

    public NoticeDialog(Settings s, DateTime finishedAt, string agentName, IntPtr targetWindow, Action onStartRest) : this(s, finishedAt, agentName, targetWindow, onStartRest, null) { }

    public NoticeDialog(Settings s, DateTime finishedAt, string agentName, IntPtr targetWindow, Action onStartRest, string detailMessage)
        : this(s, finishedAt, agentName, targetWindow, onStartRest, detailMessage, null, 0) { }

    // 终端提醒（--notice）：标题 + 消息正文 +「我知道了」，与倒计时结束同一强提醒形态。
    public static NoticeDialog CliNotice(Settings s, string message, string agentName)
    {
        return new NoticeDialog(s, DateTime.Now, agentName, IntPtr.Zero, null, null, message, 0);
    }

    // 护眼提醒：间隔是用户自己设的，到点就该休息 —— 用强提醒，不做自动消失的小卡片。
    public static NoticeDialog EyeCareReminder(Settings s, int intervalMinutes, Action onStartRest)
    {
        return new NoticeDialog(s, DateTime.Now, null, IntPtr.Zero, onStartRest, null, null, intervalMinutes);
    }

    private NoticeDialog(Settings s, DateTime finishedAt, string agentName, IntPtr targetWindow, Action onStartRest, string detailMessage, string cliMessage, int eyeCareIntervalMinutes)
    {
        settings = s;
        finishTime = finishedAt;
        targetHwnd = targetWindow;
        isCli = cliMessage != null;
        isEyeCare = eyeCareIntervalMinutes > 0;
        bool isAgent = !isCli && !string.IsNullOrEmpty(agentName);
        isAgentNotice = isAgent;
        bool en = s.Language == "en";
        Font = AppContext.GetUiFont(s.Language, 9.5f);
        BackColor = AppContext.Win11Bg;
        AppContext.ApplyModernWindowStyle(this);
        string noticePrefix = string.IsNullOrEmpty(agentName) ? "iClock" : agentName;
        Text = isCli ? (en ? noticePrefix + " Notice" : noticePrefix + " 终端提醒")
            : isEyeCare ? (en ? "Eye care reminder" : "护眼提醒")
            : isAgent ? (en ? agentName + " Ready" : agentName + " 已就绪") : "iClock";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        TopMost = true;
        ClientSize = new Size(350, 215);

        string titleText = isCli
            ? (en ? noticePrefix + " Notice" : noticePrefix + " 终端提醒")
            : isEyeCare
            ? (en ? "Time to rest your eyes" : "该让眼睛歇一歇了")
            : isAgent
            ? (en ? agentName + " Task Completed" : agentName + " 任务已完成")
            : (String.IsNullOrEmpty(s.EndMessage) ? (en ? "Countdown Finished" : "倒计时结束") : s.EndMessage);
        Label titleLabel = new Label();
        titleLabel.Text = titleText;
        titleLabel.Font = AppContext.GetUiFont(s.Language, 13f, FontStyle.Bold);
        titleLabel.ForeColor = AppContext.Win11TextPrimary;
        titleLabel.TextAlign = ContentAlignment.MiddleCenter;
        titleLabel.SetBounds(20, 18, 310, 28);
        Controls.Add(titleLabel);

        string timeText;
        if (isCli)
        {
            timeText = cliMessage;
        }
        else if (isEyeCare)
        {
            timeText = en
                ? "You've been looking at the screen for " + eyeCareIntervalMinutes + " minutes"
                : "已连续用眼 " + eyeCareIntervalMinutes + " 分钟";
        }
        else if (isAgent)
        {
            timeText = !string.IsNullOrEmpty(detailMessage) ? detailMessage : (en ? "Ready for review" : "可返回查看结果");
        }
        else
        {
            timeText = s.Format == "MM:SS" ? "00:00" :
                (s.Format == "Chinese" ? (en ? "00h 00m 00s" : "00时00分00秒") : "00:00:00");
        }
        Label timeLabel = new Label();
        timeLabel.Text = timeText;
        if (isCli)
        {
            // 终端消息沿用用户配色，并按可用高度自动缩字号。
            Color msgColor;
            try { msgColor = ColorTranslator.FromHtml(s.Color); } catch { msgColor = AppContext.Win11TextPrimary; }
            if (msgColor.IsEmpty) msgColor = AppContext.Win11TextPrimary;
            if (msgColor.R > 230 && msgColor.G > 230 && msgColor.B > 230) msgColor = AppContext.Win11TextPrimary;
            string family = !string.IsNullOrEmpty(s.FontFamily) ? s.FontFamily : "Segoe UI";
            float targetSize = Math.Max(12f, Math.Min(32f, s.FontSize > 0 ? (float)s.FontSize : 20f));
            while (true)
            {
                try { msgFont = new Font(family, targetSize, FontStyle.Bold, GraphicsUnit.Pixel); }
                catch { msgFont = new Font("Segoe UI", targetSize, FontStyle.Bold, GraphicsUnit.Pixel); }
                Size measured = TextRenderer.MeasureText(cliMessage, msgFont, new Size(310, 0), TextFormatFlags.WordBreak);
                if (measured.Height <= 42 || targetSize <= 12f) break;
                msgFont.Dispose();
                targetSize -= 2f;
            }
            timeLabel.Font = msgFont;
            timeLabel.ForeColor = msgColor;
        }
        else
        {
            timeLabel.Font = isAgent
                ? (!string.IsNullOrEmpty(detailMessage) ? AppContext.GetUiFont(s.Language, 11f, FontStyle.Bold) : AppContext.GetUiFont(s.Language, 16f, FontStyle.Bold))
                : AppContext.GetUiFont(s.Language, 22f, FontStyle.Bold);
            timeLabel.ForeColor = AppContext.Win11Accent;
        }
        timeLabel.TextAlign = ContentAlignment.MiddleCenter;
        timeLabel.SetBounds(20, 52, 310, 42);
        Controls.Add(timeLabel);

        finishLabel = new Label();
        finishLabel.Font = AppContext.GetUiFont(s.Language, 9.5f, FontStyle.Regular);
        finishLabel.ForeColor = AppContext.Win11TextSecondary;
        finishLabel.TextAlign = ContentAlignment.MiddleCenter;
        finishLabel.SetBounds(20, 102, 310, 22);
        Controls.Add(finishLabel);
        if (isCli)
        {
            finishLabel.Visible = false;
        }
        else if (isEyeCare)
        {
            finishLabel.Text = en
                ? "Look at something 6 m away for about 20 seconds to relax your eyes."
                : "望向 6 米外约 20 秒，让睫状肌放松一下。";
        }
        else
        {
            UpdateFinishLabel();
        }

        if (onStartRest != null)
        {
            // Rest length is user-adjustable right here on the popup: the
            // primary button shows the current length and the arrow zone on
            // its right edge opens a preset menu (5/10/15/20/30 plus any
            // saved custom value). Picking one updates the label and persists
            // it; a click on the text starts the rest with the length shown.
            restMinutes = Math.Max(1, Math.Min(60, s.RestMinutes > 0 ? s.RestMinutes : 10));
            RestLengthButton btnRest = new RestLengthButton();
            btnRest.Text = string.Format(en ? "Rest ({0} min)" : "休息 {0} 分钟", restMinutes);
            btnRest.Font = AppContext.GetUiFont(s.Language, 9.5f, FontStyle.Regular);
            btnRest.SetBounds(30, 150, 150, 36);
            AppContext.StylePrimaryButton(btnRest);

            restMenu = new ContextMenuStrip();
            restMenu.ShowImageMargin = false;
            int[] presets = { 5, 10, 15, 20, 30 };
            // A saved custom length keeps a slot in the menu so it stays reachable.
            List<int> menuMinutes = new List<int>(presets);
            if (Array.IndexOf(presets, restMinutes) < 0) menuMinutes.Add(restMinutes);
            List<ToolStripMenuItem> menuItems = new List<ToolStripMenuItem>();
            Action<int> pickRest = delegate(int mins)
            {
                restMinutes = Math.Max(1, Math.Min(60, mins));
                btnRest.Text = string.Format(en ? "Rest ({0} min)" : "休息 {0} 分钟", restMinutes);
                btnRest.Invalidate();
                s.RestMinutes = restMinutes;
                try { s.Save(); } catch { }
                // Mark the length in use so the menu shows what is active.
                for (int i = 0; i < menuItems.Count; i++)
                    menuItems[i].Text = string.Format(en ? "{0} minutes" : "{0} 分钟", menuMinutes[i]) + (menuMinutes[i] == restMinutes ? "  ✓" : "");
            };
            foreach (int m in menuMinutes)
            {
                int mins = m;
                ToolStripMenuItem it = new ToolStripMenuItem(string.Format(en ? "{0} minutes" : "{0} 分钟", mins));
                it.Click += delegate { pickRest(mins); };
                restMenu.Items.Add(it);
                menuItems.Add(it);
            }
            for (int i = 0; i < menuItems.Count; i++)
                if (menuMinutes[i] == restMinutes) menuItems[i].Text += "  ✓";

            restTip = new ToolTip();
            restTip.SetToolTip(btnRest, en ? "Click to start the break · click the arrow to change the length" : "点击开始休息 · 点击箭头更换时长");

            // The zone is decided from the mouse-down position, so it holds
            // no matter whether the framework raises Click before or after
            // MouseClick. Enter via AcceptButton raises Click only, so it
            // always starts the rest.
            int pressX = -1;
            btnRest.MouseDown += delegate(object sender, MouseEventArgs me) { pressX = me.X; };
            btnRest.MouseClick += delegate(object sender, MouseEventArgs me)
            {
                if (me.Button == MouseButtons.Right)
                {
                    pressX = -1;
                    restMenu.Show(Cursor.Position);
                }
            };
            btnRest.Click += delegate
            {
                int down = pressX;
                pressX = -1;
                if (down >= 0 && btnRest.InArrowZone(down))
                {
                    restMenu.Show(btnRest, new Point(btnRest.Width - restMenu.Width, btnRest.Height));
                    return;
                }
                Close();
                onStartRest();
            };
            Controls.Add(btnRest);

            Button btn = new Button();
            btn.Text = isEyeCare ? (en ? "Later" : "稍后") : (en ? "OK" : "确定");
            btn.Font = AppContext.GetUiFont(s.Language, 9.5f, FontStyle.Regular);
            btn.SetBounds(195, 150, 125, 36);
            AppContext.StyleSecondaryButton(btn);
            btn.Click += delegate { Close(); };
            Controls.Add(btn);

            AcceptButton = btnRest;
            CancelButton = btn;
        }
        else
        {
            Button btn = new Button();
            btn.Text = isCli ? (en ? "Got it" : "我知道了") : isAgent ? (en ? "View" : "查看") : (en ? "OK" : "确定");
            btn.Font = AppContext.GetUiFont(s.Language, 9.5f, FontStyle.Regular);
            btn.SetBounds(120, 150, 110, 36);
            AppContext.StylePrimaryButton(btn);
            btn.Click += delegate
            {
                if (targetHwnd != IntPtr.Zero)
                {
                    try
                    {
                        NativeMethods.ShowWindow(targetHwnd, 9);
                        NativeMethods.SetForegroundWindow(targetHwnd);
                    }
                    catch { }
                }
                Close();
            };
            Controls.Add(btn);
            AcceptButton = btn;
            CancelButton = btn;
        }

        timer = new Timer();
        timer.Interval = 1000;
        timer.Tick += OnTimerTick;

        AppContext.CenterFormOnScreen(this);
    }

    private void UpdateFinishLabel()
    {
        if (isCli || isEyeCare) return;
        bool en = settings.Language == "en";
        int secs = (int)Math.Max(0, (DateTime.Now - finishTime).TotalSeconds);
        string elapsed = (secs / 60).ToString("00") + ":" + (secs % 60).ToString("00");
        string prefix = (targetHwnd != IntPtr.Zero) ? (en ? "Completed at: " : "完成时间: ") : (en ? "Finished at: " : "结束时间: ");
        finishLabel.Text = prefix + finishTime.ToString("HH:mm:ss") + (secs > 0 ? " (+" + elapsed + ")" : "");
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        AppContext.CenterFormOnScreen(this);
        ForceTopMost();
        StartAlarm();
        if (timer != null) timer.Start();
    }

    public void ForceTopMost()
    {
        AppContext.ForceTopMost(this);
    }

    private void OnTimerTick(object sender, EventArgs e)
    {
        UpdateFinishLabel();
        ForceTopMost();
    }

    private void StartAlarm()
    {
        if (isCli) return;
        if (isAgentNotice)
        {
            AppContext.PlayAiSound();
        }
        else
        {
            if (settings == null || !settings.EndSound) return;
            AppContext.PlaySoundStyle(settings.EndSoundStyle);
        }
    }

    private void StopAlarm()
    {
        try
        {
            AppContext.StopSound();
            if (player != null) { player.Stop(); player.Dispose(); player = null; }
            if (timer != null) { timer.Stop(); timer.Dispose(); timer = null; }
        }
        catch { }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        StopAlarm();
        if (msgFont != null)
        {
            try { msgFont.Dispose(); } catch { }
            msgFont = null;
        }
    }
}

internal static class CliPromptHelper
{
    public static string GetCliExePath()
    {
        string dir = null;
        try { dir = Path.GetDirectoryName(typeof(CliPromptHelper).Assembly.Location); } catch { }
        if (string.IsNullOrEmpty(dir)) dir = AppDomain.CurrentDomain.BaseDirectory;
        return Path.Combine(dir, "iClock.exe");
    }

    public static string GenerateCliAgentPrompt(bool en)
    {
        string invoke = "& '" + GetCliExePath().Replace("'", "''") + "'";
        if (en)
        {
            return "Standing instructions for this conversation (use when working in background):\r\n"
                + "PowerShell: " + invoke + "\r\n"
                + "Initial tool-call verification: " + invoke + " --progress \"1/1\" \"Verify\" --agent-name \"Codex\" --json\r\n"
                + "Clear check: " + invoke + " --progress clear --agent-name \"Codex\" --json\r\n"
                + "Rules: Use --ask for critical decisions, --progress for milestones, --notice on completion with --json.";
        }
        return "本次会话桌面协作说明（仅在后台继续工作时调用）：\r\n"
            + "PowerShell: " + invoke + "\r\n"
            + "首次工具调用验证: " + invoke + " --progress \"1/1\" \"验证\" --agent-name \"Codex\" --json\r\n"
            + "清除验证: " + invoke + " --progress clear --agent-name \"Codex\" --json\r\n"
            + "规则: 使用 --ask 决策，--progress 汇报进度，--notice 结束提醒，配合 --json。";
    }

    public static string GenerateSkillFixPrompt(bool en, string agentName, bool conflict, string error)
    {
        string path = CliSkillInstaller.GetSkillPath(agentName);
        string content = CliSkillInstaller.GetSkillMarkdown();
        if (en)
        {
            string p = "Help me finish installing the iClock desktop collaboration Skill.\r\n\r\n"
                + "Target file: " + path + "\r\n\r\n";
            if (conflict)
                p += "A different Skill already exists at that path and was not overwritten. Read the existing file, compare it with the intended content below, and if the existing one is not the iClock Skill, back it up (rename to .bak) and write the intended content. If it already matches, do nothing and tell me.\r\n\r\n";
            else
                p += "The automatic install failed with this error: " + (error ?? "unknown") + "\r\n"
                    + "Resolve the cause (e.g. create the directory, close a program locking the file, or adjust permissions), then write the intended content below to the target file.\r\n\r\n";
            return p + "Intended SKILL.md content:\r\n\r\n" + content + "\r\n\r\nAfter writing, confirm by telling me the file exists and its size. Do not modify anything else.";
        }
        string zh = "请帮我完成 iClock 桌面协作 Skill 的安装。\r\n\r\n目标文件：" + path + "\r\n\r\n";
        if (conflict)
            zh += "该路径已存在一个内容不同的 Skill，未被覆盖。请读取现有文件，与下面的目标内容比对；若现有文件不是 iClock Skill，请先备份（重命名为 .bak）再写入目标内容；若已一致则无需操作并告诉我。\r\n\r\n";
        else
            zh += "自动安装失败，错误信息：" + (error ?? "未知") + "\r\n"
                + "请解决原因（如创建目录、关闭占用文件的程序、调整权限），然后将下面的目标内容写入该文件。\r\n\r\n";
        return zh + "目标 SKILL.md 内容：\r\n\r\n" + content + "\r\n\r\n写入完成后，请确认文件存在并告知大小。不要修改其他任何内容。";
    }

}

internal static class CliSkillInstaller
{
    // Per-agent skill directory mapping. Each agent scans its own skills dir;
    // ~/.agents/skills is the shared fallback for agents without a dedicated dir.
    private static string GetSkillDirectory(string agentName)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        switch (agentName)
        {
            case "Codex":
                return Path.Combine(home, ".codex", "skills");
            case "Antigravity":
            case "Gemini":
                return Path.Combine(home, ".gemini", "antigravity-cli", "skills");
            case "Claude Code":
                return Path.Combine(home, ".claude", "skills");
            case "Cursor":
                return Path.Combine(home, ".cursor", "skills");
            case "Windsurf":
                return Path.Combine(home, ".codeium", "windsurf", "skills");
            case "GitHub Copilot":
                return Path.Combine(home, ".copilot", "skills");
            default:
                return Path.Combine(home, ".agents", "skills");
        }
    }

    public static bool IsInstalled(string agentName)
    {
        return File.Exists(GetSkillPath(agentName));
    }

    public static string GetSkillPath(string agentName)
    {
        return Path.Combine(GetSkillDirectory(agentName), "iclock-desktop-collaboration", "SKILL.md");
    }

    public static string GetSkillMarkdown()
    {
        string exe = CliPromptHelper.GetCliExePath().Replace("\\", "/");
        return "---\r\n"
            + "name: iclock-desktop-collaboration\r\n"
            + "description: Use iClock's local CLI to coordinate important decisions, meaningful task progress, and completion alerts with the user.\r\n"
            + "---\r\n\r\n"
            + "# iClock desktop collaboration\r\n\r\n"
            + "Use this skill when iClock desktop collaboration is relevant. Invoke the local Windows CLI with PowerShell; executable: `" + exe + "`. Use `--json` and parse its response.\r\n\r\n"
            + "## When to use\r\n"
            + "- Use desktop collaboration only for background work or when the user is not actively watching; avoid interruptions during a live session unless the user explicitly asks. Short, immediate tasks do not need status updates.\r\n"
            + "- Use `--ask` only for a consequential decision that blocks progress or a risky action needing approval. Wait for the answer; if declined or cancelled, do not proceed with that action.\r\n"
            + "- Use `--progress` only at meaningful milestones of multi-step or long-running work. Keep the same agent name and always clear your row on success, failure, or cancellation.\r\n"
            + "- Use `--notice` only when a background task finishes and the user is not already attending to the result; state clearly whether it succeeded or failed.\r\n\r\n"
            + "## Verification\r\n"
            + "When the user asks to set up or verify iClock CLI collaboration, make a real, non-blocking tool call: send `--progress \"1/1\" \"Verify iClock CLI\" --agent-name \"Codex\" --json`, parse the JSON, then call `--progress clear` with the same name. Pass only if both return `ok: true`; report failures honestly. This checks command acceptance, not whether the user saw the UI. Never use `--ask` or `--notice` for a test without explicit user approval because they can interrupt. Do not run synthetic verification on every ordinary task.\r\n\r\n"
            + "## Examples\r\n\r\n"
            + "```powershell\r\n"
            + "& '" + CliPromptHelper.GetCliExePath().Replace("'", "''") + "' --ask \"Deploy now?\" --options \"Deploy,Cancel\" --agent-name \"Codex\" --json\r\n"
            + "& '" + CliPromptHelper.GetCliExePath().Replace("'", "''") + "' --progress \"2/4\" \"Running tests\" --agent-name \"Codex\" --json\r\n"
            + "& '" + CliPromptHelper.GetCliExePath().Replace("'", "''") + "' --progress clear --agent-name \"Codex\" --json\r\n"
            + "& '" + CliPromptHelper.GetCliExePath().Replace("'", "''") + "' --notice \"Background task finished\" --agent-name \"Codex\" --json\r\n"
            + "```\r\n\r\n"
            + "Only `ok: true` means iClock accepted a command. Acceptance does not prove the user saw a notification. If a command fails, report the error and do not claim the action succeeded. Never change PATH or install software without the user's approval.\r\n\r\n"
            + "When loaded, follow this Skill across relevant conversations, but never claim it is available if the agent has not loaded it. The Skill guides behavior and cannot guarantee invocation; current user instructions take precedence.\r\n";
    }

    public static string Install(string agentName, out string path)
    {
        path = GetSkillPath(agentName);
        string content = GetSkillMarkdown();
        string directory = Path.GetDirectoryName(path);
        Directory.CreateDirectory(directory);
        if (File.Exists(path))
            return string.Equals(File.ReadAllText(path), content, StringComparison.Ordinal) ? "current" : "conflict";

        try
        {
            using (FileStream stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
                writer.Write(content);
            return "installed";
        }
        catch (IOException)
        {
            if (File.Exists(path))
                return string.Equals(File.ReadAllText(path), content, StringComparison.Ordinal) ? "current" : "conflict";
            throw;
        }
    }

    public static string Overwrite(string agentName, out string path)
    {
        path = GetSkillPath(agentName);
        string content = GetSkillMarkdown();
        string directory = Path.GetDirectoryName(path);
        Directory.CreateDirectory(directory);
        File.WriteAllText(path, content, new UTF8Encoding(false));
        return "installed";
    }
}

internal sealed class AskHumanDialog : Form
{
    private string resultFilePath;
    internal string ResultFilePath { get { return resultFilePath; } }
    private bool resultWritten;
    private bool nonIntrusive;
    private readonly Action<string> resultCallback;
    internal readonly string[] Options;
    internal void SubmitResult(string r) { WriteResult(r); Close(); }
    private Timer timeoutTimer;
    private const int HOTKEY_ID_APPROVE = 0x9001;
    private const int HOTKEY_ID_REJECT = 0x9002;
    private bool hotkeysRegistered;
    private Button btnConfirm;
    private Button btnCancel;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            NativeMethods.RegisterHotKey(Handle, HOTKEY_ID_APPROVE, 5, (uint)Keys.Y);
            NativeMethods.RegisterHotKey(Handle, HOTKEY_ID_REJECT, 5, (uint)Keys.N);
            hotkeysRegistered = true;
        }
        catch { }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0312)
        {
            int id = m.WParam.ToInt32();
            if (id == HOTKEY_ID_APPROVE && btnConfirm != null)
            {
                btnConfirm.PerformClick();
                return;
            }
            if (id == HOTKEY_ID_REJECT && btnCancel != null)
            {
                btnCancel.PerformClick();
                return;
            }
        }
        base.WndProc(ref m);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (hotkeysRegistered)
        {
            try
            {
                NativeMethods.UnregisterHotKey(Handle, HOTKEY_ID_APPROVE);
                NativeMethods.UnregisterHotKey(Handle, HOTKEY_ID_REJECT);
            }
            catch { }
            hotkeysRegistered = false;
        }
        base.OnFormClosed(e);
    }

    protected override bool ShowWithoutActivation
    {
        get { return nonIntrusive; }
    }

    public AskHumanDialog(Settings s, string question, string[] options, int timeoutSeconds, string resultFile)
        : this(s, question, options, timeoutSeconds, resultFile, false, null, null)
    {
    }

    public AskHumanDialog(Settings s, string question, string[] options, int timeoutSeconds, string resultFile, bool nonIntrusiveMode)
        : this(s, question, options, timeoutSeconds, resultFile, nonIntrusiveMode, null, null)
    {
    }

    public AskHumanDialog(Settings s, string question, string[] options, int timeoutSeconds, string resultFile, bool nonIntrusiveMode, string agentName)
        : this(s, question, options, timeoutSeconds, resultFile, nonIntrusiveMode, agentName, null)
    {
    }

    public AskHumanDialog(Settings s, string question, string[] options, int timeoutSeconds, string resultFile, bool nonIntrusiveMode, string agentName, Action<string> onResult)
    {
        Options = options;
        resultFilePath = resultFile;
        resultCallback = onResult;
        nonIntrusive = nonIntrusiveMode;
        string lang = s != null ? s.Language : "zh";
        bool en = lang == "en";

        Font = AppContext.GetUiFont(lang, 9.5f);
        BackColor = AppContext.Win11Bg;
        AppContext.ApplyModernWindowStyle(this);

        string agent = !string.IsNullOrEmpty(agentName) ? agentName : (en ? "AI Agent" : "AI 智能体");
        Text = en ? (agent + " Request") : (agent + " 请示");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        TopMost = true;

        bool hasOptions = options != null && options.Length > 0;
        int dialogW = hasOptions ? 380 : 480;
        List<RadioButton> radioList = new List<RadioButton>();
        TextBox txtInput = null;
        btnConfirm = new Button();
        btnCancel = new Button();
        btnCancel.Click += delegate { WriteResult("[用户取消 / Cancelled]"); Close(); };

        if (hasOptions)
        {
            int qH = 36;
            try
            {
                Size m = TextRenderer.MeasureText(question ?? "", AppContext.GetUiFont(lang, 10.5f, FontStyle.Regular), new Size(dialogW - 54, int.MaxValue), TextFormatFlags.WordBreak);
                qH = m.Height > 24 ? Math.Min(140, m.Height + 4) : Math.Max(26, m.Height);
            }
            catch { }

            Panel pnlBar = new Panel();
            pnlBar.BackColor = AppContext.Win11Accent;
            pnlBar.SetBounds(22, 18, 3, Math.Max(18, qH - 4));
            Controls.Add(pnlBar);

            Label lblQ = new Label();
            lblQ.Text = question ?? "";
            lblQ.Font = AppContext.GetUiFont(lang, 10.5f, FontStyle.Regular);
            lblQ.ForeColor = Color.FromArgb(18, 18, 18);
            lblQ.SetBounds(32, 16, dialogW - 54, qH);
            Controls.Add(lblQ);

            int curY = 16 + qH + 14;
            for (int i = 0; i < options.Length; i++)
            {
                string opt = options[i];
                string dOpt = opt;
                string pfx = (i + 1).ToString();
                if (!dOpt.StartsWith(pfx + ".") && !dOpt.StartsWith(pfx + "、") && !dOpt.StartsWith(pfx + " ")) dOpt = pfx + ". " + dOpt;

                OptionRowControl rb = new OptionRowControl();
                rb.Text = dOpt; rb.Tag = opt;
                rb.Font = AppContext.GetUiFont(lang, 9.5f, FontStyle.Regular);
                rb.ForeColor = AppContext.Win11TextPrimary;
                rb.SetBounds(22, curY, dialogW - 44, 30);
                if (i == 0) rb.Checked = true;
                rb.DoubleClick += delegate { WriteResult(opt); Close(); };
                Controls.Add(rb); radioList.Add(rb);
                curY += 34;
            }

            curY += 10;
            int bW = 120, bH = 28;
            btnConfirm.Text = en ? "OK (Alt+Shift+Y)" : "确定 (Alt+Shift+Y)";
            btnConfirm.Font = AppContext.GetUiFont(lang, 9f);
            btnConfirm.SetBounds(dialogW - 22 - bW * 2 - 10, curY, bW, bH);
            AppContext.StylePrimaryButton(btnConfirm);
            btnConfirm.Click += delegate
            {
                string sel = options[0];
                for (int i = 0; i < radioList.Count; i++)
                {
                    if (radioList[i].Checked) { sel = radioList[i].Tag != null ? radioList[i].Tag.ToString() : radioList[i].Text; break; }
                }
                WriteResult(sel); Close();
            };
            Controls.Add(btnConfirm);

            btnCancel.Text = en ? "Cancel (Alt+Shift+N)" : "取消 (Alt+Shift+N)";
            btnCancel.Font = AppContext.GetUiFont(lang, 9f);
            btnCancel.SetBounds(dialogW - 22 - bW, curY, bW, bH);
            AppContext.StyleSecondaryButton(btnCancel);
            Controls.Add(btnCancel);
            ClientSize = new Size(dialogW, curY + bH + 16);
        }
        else
        {
            Text = en ? (agent + " Prompt") : (agent + " 追加指令");
            Label lblH = new Label();
            lblH.Text = en ? ("Add instruction to " + agent) : ("向 " + agent + " 追加指令");
            lblH.Font = AppContext.GetUiFont(lang, 10f, FontStyle.Bold);
            lblH.ForeColor = Color.FromArgb(30, 41, 59);
            lblH.SetBounds(20, 14, dialogW - 40, 22);
            Controls.Add(lblH);

            Panel pnlBox = new Panel();
            pnlBox.BackColor = Color.White;
            pnlBox.BorderStyle = BorderStyle.FixedSingle;
            pnlBox.SetBounds(20, 42, dialogW - 40, 84);
            Controls.Add(pnlBox);

            txtInput = new TextBox();
            txtInput.Multiline = true; txtInput.BorderStyle = BorderStyle.None;
            txtInput.Font = AppContext.GetUiFont(lang, 10f);
            txtInput.ForeColor = Color.FromArgb(15, 23, 42);
            txtInput.SetBounds(8, 8, pnlBox.Width - 16, pnlBox.Height - 16);
            pnlBox.Controls.Add(txtInput);

            Label lblHint = new Label();
            lblHint.Text = en ? "Enter to send · Shift+Enter for newline · Esc to cancel" : "Enter 下发 · Shift+Enter 换行 · Esc 取消";
            lblHint.Font = AppContext.GetUiFont(lang, 8f);
            lblHint.ForeColor = Color.FromArgb(148, 163, 184);
            lblHint.SetBounds(20, 137, 340, 20);
            Controls.Add(lblHint);

            btnConfirm.Text = en ? "Send ➤" : "下发 ➤";
            btnConfirm.Font = AppContext.GetUiFont(lang, 8.8f, FontStyle.Bold);
            btnConfirm.SetBounds(dialogW - 20 - 78, 134, 78, 26);
            AppContext.StylePrimaryButton(btnConfirm);
            btnConfirm.Click += delegate { WriteResult(txtInput != null ? txtInput.Text.Trim() : ""); Close(); };
            Controls.Add(btnConfirm);

            btnCancel.Size = Size.Empty;
            txtInput.KeyDown += delegate(object snd, KeyEventArgs ke)
            {
                if (ke.KeyCode == Keys.Enter && !ke.Shift) { ke.SuppressKeyPress = true; btnConfirm.PerformClick(); }
            };
            Shown += delegate { if (txtInput != null) txtInput.Focus(); };
            ClientSize = new Size(dialogW, 172);
        }
        AcceptButton = btnConfirm;
        CancelButton = btnCancel;

        KeyPreview = true;
        KeyDown += delegate(object sender, KeyEventArgs ke)
        {
            if (hasOptions)
            {
                if ((ke.KeyCode >= Keys.D1 && ke.KeyCode <= Keys.D9) ||
                    (ke.KeyCode >= Keys.NumPad1 && ke.KeyCode <= Keys.NumPad9))
                {
                    int k = (ke.KeyCode >= Keys.NumPad1) ? (ke.KeyCode - Keys.NumPad1) : (ke.KeyCode - Keys.D1);
                    if (k >= 0 && k < radioList.Count)
                    {
                        radioList[k].Checked = true;
                        radioList[k].Focus();
                        ke.Handled = true;
                        return;
                    }
                }
            }
            if (ke.KeyCode == Keys.Escape)
            {
                WriteResult("[用户取消 / Cancelled]");
                Close();
                ke.Handled = true;
                return;
            }
        };

        if (timeoutSeconds > 0)
        {
            timeoutTimer = new Timer();
            timeoutTimer.Interval = timeoutSeconds > 2147483 ? int.MaxValue : timeoutSeconds * 1000;
            timeoutTimer.Tick += delegate
            {
                timeoutTimer.Stop();
                WriteResult("[超时未响应 / Timeout]");
                Close();
            };
            timeoutTimer.Start();
        }

        FormClosed += delegate
        {
            if (timeoutTimer != null)
            {
                try { timeoutTimer.Stop(); timeoutTimer.Dispose(); } catch { }
                timeoutTimer = null;
            }
            if (!resultWritten)
            {
                WriteResult("[用户取消 / Cancelled]");
            }
        };
    }

    private void WriteResult(string result)
    {
        if (resultWritten) return;
        resultWritten = true;
        try
        {
            if (!string.IsNullOrEmpty(resultFilePath))
            {
                File.WriteAllText(resultFilePath, result ?? "", Encoding.UTF8);
            }
        }
        catch { }
        if (resultCallback != null)
        {
            Action<string> cb = resultCallback;
            System.Threading.Thread t = new System.Threading.Thread(delegate()
            {
                try
                {
                    System.Threading.Thread.Sleep(100);
                    cb(result ?? "");
                }
                catch { }
            });
            t.SetApartmentState(System.Threading.ApartmentState.STA);
            t.IsBackground = true;
            t.Start();
        }
    }

    private sealed class OptionRowControl : RadioButton
    {
        private bool isHovered;

        public OptionRowControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
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

        protected override void OnCheckedChanged(EventArgs e)
        {
            base.OnCheckedChanged(e);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            Graphics g = pe.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (Checked)
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(243, 246, 252))) g.FillRectangle(b, r);
                using (Pen p = new Pen(Color.FromArgb(210, 225, 245))) g.DrawRectangle(p, r);
            }
            else
            {
                using (SolidBrush b = new SolidBrush(isHovered ? Color.FromArgb(248, 250, 253) : Color.White)) g.FillRectangle(b, r);
                using (Pen p = new Pen(isHovered ? Color.FromArgb(215, 225, 240) : Color.FromArgb(235, 235, 235))) g.DrawRectangle(p, r);
            }

            using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(32, 32, 32)))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(Text, Font, textBrush, new Rectangle(10, 0, Width - 40, Height), sf);
            }

            int circleX = Width - 24;
            int circleY = (Height - 14) / 2;
            Rectangle circleRect = new Rectangle(circleX, circleY, 14, 14);
            if (Checked)
            {
                using (Pen radioBorder = new Pen(Color.FromArgb(0, 103, 192), 1.5f))
                using (SolidBrush radioDot = new SolidBrush(Color.FromArgb(0, 103, 192)))
                {
                    g.DrawEllipse(radioBorder, circleRect);
                    g.FillEllipse(radioDot, new Rectangle(circleX + 4, circleY + 4, 6, 6));
                }
            }
            else
            {
                using (Pen grayRadioBorder = new Pen(isHovered ? Color.FromArgb(120, 120, 120) : Color.FromArgb(160, 160, 160), 1.2f))
                {
                    g.DrawEllipse(grayRadioBorder, circleRect);
                }
            }
        }
    }
}

