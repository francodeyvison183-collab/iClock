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
    {
        settings = s;
        finishTime = finishedAt;
        targetHwnd = targetWindow;
        bool isAgent = !string.IsNullOrEmpty(agentName);
        isAgentNotice = isAgent;
        bool en = s.Language == "en";
        Font = AppContext.GetUiFont(s.Language, 9.5f);
        BackColor = AppContext.Win11Bg;
        AppContext.ApplyModernWindowStyle(this);
        Text = isAgent ? (en ? agentName + " Ready" : agentName + " 已就绪") : "iClock";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        TopMost = true;
        ClientSize = new Size(350, 215);

        string titleText = isAgent
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
        if (isAgent)
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
        timeLabel.Font = isAgent
            ? (!string.IsNullOrEmpty(detailMessage) ? AppContext.GetUiFont(s.Language, 11f, FontStyle.Bold) : AppContext.GetUiFont(s.Language, 16f, FontStyle.Bold))
            : AppContext.GetUiFont(s.Language, 22f, FontStyle.Bold);
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
            btn.Text = en ? "OK" : "确定";
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
            btn.Text = isAgent ? (en ? "View" : "查看") : (en ? "OK" : "确定");
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
    }
}

internal sealed class CliNoticeDialog : Form
{
    private Font msgFont;

    public CliNoticeDialog(Settings s, string message, string agentName = null)
    {
        bool en = s != null && s.Language == "en";
        string lang = s != null ? s.Language : "zh";
        Font = AppContext.GetUiFont(lang, 9.5f);
        BackColor = AppContext.Win11Bg;
        AppContext.ApplyModernWindowStyle(this);
        string prefix = !string.IsNullOrEmpty(agentName) ? agentName : "iClock";
        Text = en ? (prefix + " Notice") : (prefix + " 终端提醒");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        ShowIcon = false;
        try { Icon = AppContext.CreateIcon(); } catch { }
        TopMost = true;
        DoubleBuffered = true;
        ClientSize = new Size(350, 160);

        // Resolve user configured color
        Color msgColor;
        try { msgColor = ColorTranslator.FromHtml(s != null ? s.Color : null); }
        catch { msgColor = AppContext.Win11TextPrimary; }
        if (msgColor.IsEmpty) msgColor = AppContext.Win11TextPrimary;
        if (msgColor.R > 230 && msgColor.G > 230 && msgColor.B > 230) msgColor = AppContext.Win11TextPrimary;

        // Resolve user configured font family and font size
        string family = (s != null && !string.IsNullOrEmpty(s.FontFamily)) ? s.FontFamily : "Segoe UI";
        float userSize = (s != null && s.FontSize > 0) ? (float)s.FontSize : 20f;
        float targetSize = Math.Max(12f, Math.Min(32f, userSize));
        while (targetSize >= 12f)
        {
            try { msgFont = new Font(family, targetSize, FontStyle.Bold, GraphicsUnit.Pixel); }
            catch { msgFont = new Font("Segoe UI", targetSize, FontStyle.Bold, GraphicsUnit.Pixel); }

            Size measured = TextRenderer.MeasureText(message, msgFont, new Size(318, 0), TextFormatFlags.WordBreak);
            if (measured.Height <= 72 || targetSize <= 12f)
            {
                break;
            }
            msgFont.Dispose();
            targetSize -= 2f;
        }

        Label lblMsg = new Label();
        lblMsg.Text = message;
        lblMsg.Font = msgFont;
        lblMsg.ForeColor = msgColor;
        lblMsg.TextAlign = ContentAlignment.MiddleCenter;
        lblMsg.SetBounds(16, 18, 318, 78);
        lblMsg.AutoEllipsis = true;
        lblMsg.BackColor = Color.Transparent;
        Controls.Add(lblMsg);

        Button btnOk = new Button();
        btnOk.Text = en ? "Got it" : "我知道了";
        btnOk.Font = AppContext.GetUiFont(lang, 9.5f, FontStyle.Regular);
        btnOk.SetBounds(120, 108, 110, 34);
        AppContext.StylePrimaryButton(btnOk);
        btnOk.Click += delegate { Close(); };
        Controls.Add(btnOk);

        AcceptButton = btnOk;
        CancelButton = btnOk;
        KeyPreview = true;

        FormClosed += delegate
        {
            if (msgFont != null)
            {
                try { msgFont.Dispose(); } catch { }
                msgFont = null;
            }
        };

        AppContext.CenterFormOnScreen(this);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        AppContext.CenterFormOnScreen(this);
        ForceTopMost();
    }

    public void ForceTopMost()
    {
        AppContext.ForceTopMost(this);
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
        string exePath = GetCliExePath().Replace("'", "''");
        string invoke = "& '" + exePath + "'";
        if (en)
        {
            return "Treat these as standing instructions for this conversation and follow them throughout the task. They do not persist automatically into future conversations.\r\n\r\n"
                + "Use iClock desktop collaboration only when you are continuing work in the background or the user is not actively watching. Avoid interrupting the user during a live session unless they explicitly ask. Skip desktop updates for short, immediate tasks. Run the Windows CLI from PowerShell:\r\n"
                + invoke + "\r\n\r\n"
                + "Initial tool-call verification (required after receiving this guide):\r\n"
                + "1. Send one harmless progress update: " + invoke + " --progress \"1/1\" \"Verifying iClock CLI\" --agent-name \"Codex\" --json.\r\n"
                + "2. Parse the JSON response, then clear that same row: " + invoke + " --progress clear --agent-name \"Codex\" --json. Treat the check as passed only if both responses have ok=true; report any error honestly. This verifies command acceptance, not that the user saw the display.\r\n"
                + "3. Do not use --ask or --notice as a test unless the user explicitly requests it; they can interrupt or block.\r\n\r\n"
                + "When collaborating:\r\n"
                + "- Use --ask only when a consequential decision blocks progress or a risky action needs approval. Wait for the user's answer; if declined or cancelled, do not proceed with that action. Example: " + invoke + " --ask \"Deploy now?\" --options \"Deploy,Cancel\" --agent-name \"Codex\" --json.\r\n"
                + "- Use --progress at meaningful milestones of multi-step or long-running background work, not for every action. Always clear your row on success, failure, or cancellation with the same agent name. Example: " + invoke + " --progress \"3/5\" \"Running tests\" --agent-name \"Codex\" --json.\r\n"
                + "- Use --notice for a completed background task only when the user is not already attending to the result; make the message state success or failure. Example: " + invoke + " --notice \"Build finished successfully\" --agent-name \"Codex\" --json.\r\n\r\n"
                + "Use a stable agent name on every call; add a short distinguishing suffix if concurrent instances of the same agent run. With --json, parse the response and treat only ok=true as accepted. Acceptance does not prove a popup was seen. If a command fails, report the error and do not claim success. Never change PATH or install software without approval.\r\n\r\n"
                + "This pasted guide applies to this conversation only. For future conversations, use the installed iClock Skill if the agent supports and loads it; otherwise the user must provide these instructions again. Do not claim cross-session memory unless that Skill is available.";
        }
        return "请将本说明作为本次会话的持续协作规则，在整个任务过程中遵守；它不会自动延续到未来会话。\r\n\r\n"
            + "仅当你需要在后台继续工作，或用户没有实时关注工作过程时，才使用 iClock 桌面协作；用户正在实时查看时避免打扰，除非用户明确要求。短小、即时的任务无需发送桌面状态。请在 Windows PowerShell 中调用：\r\n"
            + invoke + "\r\n\r\n"
            + "首次工具调用验证（收到本说明后必须执行）：\r\n"
            + "1. 发送一次无阻断的进度测试：" + invoke + " --progress \"1/1\" \"验证 iClock CLI\" --agent-name \"Codex\" --json。\r\n"
            + "2. 解析 JSON 后，用相同名称清除测试状态：" + invoke + " --progress clear --agent-name \"Codex\" --json。两次返回都为 ok=true 才算命令调用验证通过；如有错误，准确报告。此验证只说明命令已被 iClock 接受，不代表用户看到了界面。\r\n"
            + "3. 未经用户明确要求，不要用 --ask 或 --notice 做测试；它们可能阻断或弹窗打扰用户。\r\n\r\n"
            + "实际协作规则：\r\n"
            + "- 只有关键决策确实阻塞后续工作，或高风险操作需要批准时才用 --ask；等待用户明确答复，若用户拒绝或取消，不得继续该操作。示例：" + invoke + " --ask \"现在部署吗？\" --options \"部署,取消\" --agent-name \"Codex\" --json。\r\n"
            + "- 对多步骤或耗时的后台任务，只在重要节点用 --progress 汇报，不要每做一步都更新。任务成功、失败或取消时，都用相同智能体名称清除自己的进度。示例：" + invoke + " --progress \"3/5\" \"正在运行测试\" --agent-name \"Codex\" --json。\r\n"
            + "- 仅当后台任务完成且用户没有关注结果时使用 --notice；消息应明确说明成功或失败。示例：" + invoke + " --notice \"构建已成功完成\" --agent-name \"Codex\" --json。\r\n\r\n"
            + "每次调用使用稳定的智能体名称；同类智能体有多个并发实例时，加简短后缀区分。使用 --json 并解析结果，只有 ok=true 才表示 iClock 接受命令；接受不代表用户已看到弹窗。命令失败时如实报告，不得宣称成功。未经同意，不修改 PATH 或安装软件。\r\n\r\n"
            + "本说明只对当前会话有效。后续会话需由支持并加载 iClock Skill 的智能体遵循；若没有可用 Skill，用户需要再次提供说明。未确认 Skill 可用前，不要声称已跨会话记住。";
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
    private bool resultWritten;
    private bool nonIntrusive;
    private readonly Action<string> resultCallback;
    private Timer timeoutTimer;

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
        int dialogW = 380;

        int qHeight = 36;
        try
        {
            Size measured = TextRenderer.MeasureText(question ?? "", AppContext.GetUiFont(lang, 10.5f, FontStyle.Regular), new Size(dialogW - 54, int.MaxValue), TextFormatFlags.WordBreak);
            if (measured.Height > 24) qHeight = Math.Min(140, measured.Height + 4);
            else qHeight = Math.Max(26, measured.Height);
        }
        catch { }

        Panel pnlAccentBar = new Panel();
        pnlAccentBar.BackColor = AppContext.Win11Accent;
        pnlAccentBar.SetBounds(22, 16 + 2, 3, Math.Max(18, qHeight - 4));
        Controls.Add(pnlAccentBar);

        Label lblQuestion = new Label();
        lblQuestion.Text = question ?? "";
        lblQuestion.Font = AppContext.GetUiFont(lang, 10.5f, FontStyle.Regular);
        lblQuestion.ForeColor = Color.FromArgb(18, 18, 18);
        lblQuestion.SetBounds(32, 16, dialogW - 54, qHeight);
        Controls.Add(lblQuestion);

        int currentY = 16 + qHeight + 14;
        List<RadioButton> radioList = new List<RadioButton>();
        TextBox txtInput = null;

        if (hasOptions)
        {
            for (int i = 0; i < options.Length; i++)
            {
                string optText = options[i];
                string displayOpt = optText;
                string prefix = (i + 1).ToString();
                if (!displayOpt.StartsWith(prefix + ".") && !displayOpt.StartsWith(prefix + "、") && !displayOpt.StartsWith(prefix + " "))
                {
                    displayOpt = prefix + ". " + displayOpt;
                }

                OptionRowControl rb = new OptionRowControl();
                rb.Text = displayOpt;
                rb.Tag = optText;
                rb.Font = AppContext.GetUiFont(lang, 9.5f, FontStyle.Regular);
                rb.ForeColor = AppContext.Win11TextPrimary;
                rb.SetBounds(22, currentY, dialogW - 44, 30);
                if (i == 0) rb.Checked = true;
                rb.DoubleClick += delegate
                {
                    WriteResult(optText);
                    Close();
                };
                Controls.Add(rb);
                radioList.Add(rb);
                currentY += 34;
            }
        }
        else
        {
            txtInput = new TextBox();
            txtInput.Font = AppContext.GetUiFont(lang, 9.5f);
            txtInput.SetBounds(22, currentY, dialogW - 44, 26);
            Controls.Add(txtInput);
            currentY += 34;
        }

        currentY += 10;
        int btnW = 76;
        int btnH = 28;

        Button btnConfirm = new Button();
        btnConfirm.Text = en ? "OK" : "确定";
        btnConfirm.Font = AppContext.GetUiFont(lang, 9f);
        btnConfirm.SetBounds(dialogW - 22 - btnW * 2 - 10, currentY, btnW, btnH);
        AppContext.StylePrimaryButton(btnConfirm);
        btnConfirm.Click += delegate
        {
            if (hasOptions)
            {
                string selected = options[0];
                for (int i = 0; i < radioList.Count; i++)
                {
                    if (radioList[i].Checked)
                    {
                        selected = radioList[i].Tag != null ? radioList[i].Tag.ToString() : radioList[i].Text;
                        break;
                    }
                }
                WriteResult(selected);
            }
            else
            {
                WriteResult(txtInput != null ? txtInput.Text : "");
            }
            Close();
        };
        Controls.Add(btnConfirm);

        Button btnCancel = new Button();
        btnCancel.Text = en ? "Cancel" : "取消";
        btnCancel.Font = AppContext.GetUiFont(lang, 9f);
        btnCancel.SetBounds(dialogW - 22 - btnW, currentY, btnW, btnH);
        AppContext.StyleSecondaryButton(btnCancel);
        btnCancel.Click += delegate
        {
            WriteResult("[用户取消 / Cancelled]");
            Close();
        };
        Controls.Add(btnCancel);

        AcceptButton = btnConfirm;
        CancelButton = btnCancel;

        ClientSize = new Size(dialogW, currentY + btnH + 16);

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
            try { resultCallback(result ?? ""); } catch { }
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

