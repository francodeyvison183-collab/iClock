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
    // Chosen rest length shown on the "" button (arrow zone opens a preset menu).
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
        bool en = s.Language == "";
        Font = AppContext.GetUiFont(s.Language, 9.5f);
        BackColor = AppContext.Win11Bg;
        AppContext.ApplyModernWindowStyle(this);
        Text = isAgent ? (en ? agentName + "" : agentName + "") : "";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        TopMost = true;
        ClientSize = new Size(350, 215);

        string titleText = isAgent
            ? (en ? agentName + "" : agentName + "")
            : (String.IsNullOrEmpty(s.EndMessage) ? (en ? "" : "") : s.EndMessage);
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
            timeText = !string.IsNullOrEmpty(detailMessage) ? detailMessage : (en ? "" : "");
        }
        else
        {
            timeText = s.Format == "" ? "" :
                (s.Format == "" ? (en ? "" : "") : "");
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
            btnRest.Text = string.Format(en ? "" : "", restMinutes);
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
                btnRest.Text = string.Format(en ? "" : "", restMinutes);
                btnRest.Invalidate();
                s.RestMinutes = restMinutes;
                try { s.Save(); } catch { }
                // Mark the length in use so the menu shows what is active.
                for (int i = 0; i < menuItems.Count; i++)
                    menuItems[i].Text = string.Format(en ? "" : "", menuMinutes[i]) + (menuMinutes[i] == restMinutes ? "" : "");
            };
            foreach (int m in menuMinutes)
            {
                int mins = m;
                ToolStripMenuItem it = new ToolStripMenuItem(string.Format(en ? "" : "", mins));
                it.Click += delegate { pickRest(mins); };
                restMenu.Items.Add(it);
                menuItems.Add(it);
            }
            for (int i = 0; i < menuItems.Count; i++)
                if (menuMinutes[i] == restMinutes) menuItems[i].Text += "";

            restTip = new ToolTip();
            restTip.SetToolTip(btnRest, en ? "" : "");

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
            btn.Text = en ? "" : "";
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
            btn.Text = isAgent ? (en ? "" : "") : (en ? "" : "");
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
        bool en = settings.Language == "";
        int secs = (int)Math.Max(0, (DateTime.Now - finishTime).TotalSeconds);
        string elapsed = (secs / 60).ToString("") + "" + (secs % 60).ToString("");
        string prefix = (targetHwnd != IntPtr.Zero) ? (en ? "" : "") : (en ? "" : "");
        finishLabel.Text = prefix + finishTime.ToString("") + (secs > 0 ? "" + elapsed + "" : "");
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
        bool en = s != null && s.Language == "";
        string lang = s != null ? s.Language : "";
        Font = AppContext.GetUiFont(lang, 9.5f);
        BackColor = AppContext.Win11Bg;
        AppContext.ApplyModernWindowStyle(this);
        string prefix = !string.IsNullOrEmpty(agentName) ? agentName : "";
        Text = en ? (prefix + "") : (prefix + "");
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
        string family = (s != null && !string.IsNullOrEmpty(s.FontFamily)) ? s.FontFamily : "";
        float userSize = (s != null && s.FontSize > 0) ? (float)s.FontSize : 20f;
        float targetSize = Math.Max(12f, Math.Min(32f, userSize));
        while (targetSize >= 12f)
        {
            try { msgFont = new Font(family, targetSize, FontStyle.Bold, GraphicsUnit.Pixel); }
            catch { msgFont = new Font("", targetSize, FontStyle.Bold, GraphicsUnit.Pixel); }

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
        btnOk.Text = en ? "" : "";
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
        return Path.Combine(dir, "");
    }

    public static string GenerateCliAgentPrompt(bool en)
    {
        string exePath = GetCliExePath().Replace("", "");
        string invoke = "" + exePath + "";
        if (en)
        {
            return ""
                + ""
                + invoke + ""
                + ""
                + "" + invoke + ""
                + "" + invoke + ""
                + ""
                + ""
                + "" + invoke + ""
                + "" + invoke + ""
                + "" + invoke + ""
                + ""
                + "";
        }
        return ""
            + ""
            + invoke + ""
            + ""
            + "" + invoke + ""
            + "" + invoke + ""
            + ""
            + ""
            + "" + invoke + ""
            + "" + invoke + ""
            + "" + invoke + ""
            + ""
            + "";
    }

    public static string GenerateSkillFixPrompt(bool en, string agentName, bool conflict, string error)
    {
        string path = CliSkillInstaller.GetSkillPath(agentName);
        string content = CliSkillInstaller.GetSkillMarkdown();
        if (en)
        {
            string p = ""
                + "" + path + "";
            if (conflict)
                p += "";
            else
                p += "" + (error ?? "") + ""
                    + "";
            return p + "" + content + "";
        }
        string zh = "" + path + "";
        if (conflict)
            zh += "";
        else
            zh += "" + (error ?? "") + ""
                + "";
        return zh + "" + content + "";
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
            case "":
                return Path.Combine(home, "", "");
            case "":
            case "":
                return Path.Combine(home, "", "", "");
            case "":
                return Path.Combine(home, "", "");
            case "":
                return Path.Combine(home, "", "");
            case "":
                return Path.Combine(home, "", "", "");
            case "":
                return Path.Combine(home, "", "");
            default:
                return Path.Combine(home, "", "");
        }
    }

    public static bool IsInstalled(string agentName)
    {
        return File.Exists(GetSkillPath(agentName));
    }

    public static string GetSkillPath(string agentName)
    {
        return Path.Combine(GetSkillDirectory(agentName), "", "");
    }

    public static string GetSkillMarkdown()
    {
        string exe = CliPromptHelper.GetCliExePath().Replace("", "");
        return ""
            + ""
            + ""
            + ""
            + ""
            + "" + exe + ""
            + ""
            + ""
            + ""
            + ""
            + ""
            + ""
            + ""
            + ""
            + ""
            + "" + CliPromptHelper.GetCliExePath().Replace("", "") + ""
            + "" + CliPromptHelper.GetCliExePath().Replace("", "") + ""
            + "" + CliPromptHelper.GetCliExePath().Replace("", "") + ""
            + "" + CliPromptHelper.GetCliExePath().Replace("", "") + ""
            + ""
            + ""
            + "";
    }

    public static string Install(string agentName, out string path)
    {
        path = GetSkillPath(agentName);
        string content = GetSkillMarkdown();
        string directory = Path.GetDirectoryName(path);
        Directory.CreateDirectory(directory);
        if (File.Exists(path))
            return string.Equals(File.ReadAllText(path), content, StringComparison.Ordinal) ? "" : "";

        try
        {
            using (FileStream stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
                writer.Write(content);
            return "";
        }
        catch (IOException)
        {
            if (File.Exists(path))
                return string.Equals(File.ReadAllText(path), content, StringComparison.Ordinal) ? "" : "";
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
        return "";
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
        string lang = s != null ? s.Language : "";
        bool en = lang == "";

        Font = AppContext.GetUiFont(lang, 9.5f);
        BackColor = AppContext.Win11Bg;
        AppContext.ApplyModernWindowStyle(this);

        string agent = !string.IsNullOrEmpty(agentName) ? agentName : (en ? "" : "");
        Text = en ? (agent + "") : (agent + "");
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
                if (!displayOpt.StartsWith(prefix + "") && !displayOpt.StartsWith(prefix + "") && !displayOpt.StartsWith(prefix + ""))
                {
                    displayOpt = prefix + "" + displayOpt;
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
        btnConfirm.Text = en ? "" : "";
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
        btnCancel.Text = en ? "" : "";
        btnCancel.Font = AppContext.GetUiFont(lang, 9f);
        btnCancel.SetBounds(dialogW - 22 - btnW, currentY, btnW, btnH);
        AppContext.StyleSecondaryButton(btnCancel);
        btnCancel.Click += delegate
        {
            WriteResult("");
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
                WriteResult("");
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
                WriteResult("");
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
                WriteResult("");
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

