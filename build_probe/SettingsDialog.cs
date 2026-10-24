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

internal sealed class SettingsDialog : Form
{
    private NumericUpDown minutes, size;
    private ComboBox timerColorCombo;
    private bool updatingTimerColorCombo;
    private string selectedColor;
    private TextBox endMessage;
    private ComboBox format, fontCombo;
    private ToggleSwitch dailyQuoteToggle;
    private Button btnEditQuotes, btnAiQuotes;
    private ComboBox soundStyle, noticeStyle;
    private TextBox hotkeyBox;
    private int hotkeyModifiers, hotkeyKey, candidateMods, candidateKey;
    private bool hotkeyActive;
    private Label lblMinutes, lblSize, lblFont, lblColor, lblFormat, lblEndMsg, lblHotkey;
    private Label lblSound, lblNotice, lblDailyGoal;
    private ComboBox dailyGoal;
    public Settings Value { get; private set; }
    public event Action PreviewRequested;
    public event Action SettingsCommitted;
    public event Action<bool> QuotePreviewRequested;
    public event Action QuoteNextRequested;
    public bool IsDailyQuoteChecked { get { return dailyQuoteToggle != null && dailyQuoteToggle.Checked; } }
    public bool IsQuoteDesktopVisible { get { return IsDailyQuoteChecked; } }
    public int DialogQuoteIndex { get { return dialogQuoteIndex; } }
    public int ActiveTab { get { return activeTab; } }
    private static readonly string[] fontFamilies = { "", "", "", "", "" };

    private static int lastActiveTab = 0;
    private int activeTab = 0;
    private int hoveredTab = -1;
    private Button btnTabTimer, btnTabQuote, btnTabAgent;
    private Button btnAskDemo, btnProgressDemo, btnNotifyDemo;
    private Panel panelCapabilityCard;
    private ToggleSwitch chkDesktopCollaboration;
    private Label lblTool1Name, lblTool2Name, lblTool3Name;
    private Label lblTool1Desc, lblTool2Desc, lblTool3Desc;
    // Two-section layout: collaboration readiness first, capability previews second.
    private Panel panelSkillCard, panelSessionCard, panelCapCard;
    private Label lblSkillGuidance, lblSkillFeedback;
    private string agentRowsFingerprint;
    private System.Windows.Forms.Timer cliCopyFeedbackTimer;
    private System.Windows.Forms.Timer agentListTimer;
    // Dynamic per-agent rows (running agents only), rebuilt by RefreshSkillInstallUi.
    private Panel panelAgentRows;
    private Label lblAgentEmpty;
    private Label lblSkillMethod1, lblSkillMethod2;
    private Label lblCliGuideHint;
    private Button btnCopyCliGuide;
    // Install failure closure: reason, human self-help, retry, overwrite, agent-assist copy.
    private Panel panelInstallFail;
    private Label lblFailReason, lblFailHint;
    private Button btnRetryInstall, btnOverwriteInstall, btnCopyFixPrompt;
    private string lastInstallError;
    private bool lastInstallConflict;
    private string lastInstallAgent;

    private NumericUpDown quoteFontSize;
    private bool isSyncingQuoteFontSize;
    private Label lblQuoteSize, lblQuoteFont, lblQuoteColor, lblQuoteLayer, lblQuoteQuotes;
    private StyledTipLabel lblQuoteTip;
    private ComboBox quoteFontCombo, quoteColorCombo, quoteLayerCombo;
    // The screensaver's on/off state and its idle wait are one control: "" plus
    // the minute options, the way macOS' screen saver offers "".
    private ComboBox quoteSaverWaitCombo;
    private LinkLabel lnkPreviewSaver;
    private Label lblDailyQuote;
    private bool updatingQuoteColorCombo;
    private string selectedQuoteColor = "";
    private string selectedQuoteFont = "";
    private Panel panelQuoteCard;
    private Label lblCardQuoteText, lblCardQuoteAuthor;
    private Button btnNextQuote;
    private List<QuoteItem> dialogQuoteList;
    private int dialogQuoteIndex;
    private ToolTip toolTip;
    private static readonly string[] quoteFontFamilies = {
        "", "", "", "", "", "", "", "", "", ""
    };
    private static readonly string[] TimerPresetColors = { "", "", "", "", "", "", "", "", "", "" };
    private static readonly string[] QuotePresetColors = { "", "", "", "", "", "", "", "", "" };
    private static readonly string[] timerColorNamesZh = { "", "", "", "", "", "", "", "", "" };
    private static readonly string[] timerColorNamesEn = { "", "", "", "", "", "", "", "", "" };
    private static readonly string[] quoteColorNamesZh = { "", "", "", "", "", "", "", "", "" };
    private static readonly string[] quoteColorNamesEn = { "", "", "", "", "", "", "", "", "" };

    private const int EM_SETMARGINS = 0x00D3;
    private const int EC_LEFTMARGIN = 0x0001;
    private const int EC_RIGHTMARGIN = 0x0002;

    private static void UpdateInputPadding(Control ctrl)
    {
        if (ctrl == null) return;
        if (ctrl is TextBox)
        {
            TextBox tb = (TextBox)ctrl;
            if (tb.IsHandleCreated)
            {
                NativeMethods.SendMessage(tb.Handle, EM_SETMARGINS, (IntPtr)(EC_LEFTMARGIN | EC_RIGHTMARGIN), (IntPtr)(4 | (4 << 16)));
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

    private void SetupTabButton(Button btn, string text, int x, int tabIdx)
    {
        btn.Text = text;
        btn.SetBounds(x, 8, 114, 34);
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 0;
        btn.BackColor = Color.Transparent;
        btn.ForeColor = Color.Transparent;
        btn.Cursor = Cursors.Hand;
        btn.Tag = tabIdx;
        btn.Click += OnTabBtnClick;
        btn.MouseEnter += OnTabBtnMouseEnter;
        btn.MouseLeave += OnTabBtnMouseLeave;
        btn.Paint += OnTabBtnPaint;
        Controls.Add(btn);
    }
    private void OnTabBtnClick(object sender, EventArgs e) { SetActiveTab((int)((Control)sender).Tag); }
    private void OnTabBtnMouseEnter(object sender, EventArgs e) { hoveredTab = (int)((Control)sender).Tag; ((Control)sender).Invalidate(); }
    private void OnTabBtnMouseLeave(object sender, EventArgs e) { hoveredTab = -1; ((Control)sender).Invalidate(); }
    private void OnTabBtnPaint(object sender, PaintEventArgs pe) { Button btn = (Button)sender; DrawWin11Tab(pe.Graphics, btn, (int)btn.Tag); }

    public SettingsDialog(Settings s) : this(s, true) { }

    public SettingsDialog(Settings s, bool hotkeyActive)
    {
        this.hotkeyActive = hotkeyActive;
        Value = s;
        // Empty stored color means "" -> resolve to the default preset so the combo can select it.
        selectedQuoteColor = string.IsNullOrEmpty(s.QuoteColor) ? QuotePresetColors[0] : s.QuoteColor;
        selectedQuoteFont = s.QuoteFont ?? "";
        bool en = Value.Language == "";
        Font = AppContext.GetUiFont(Value.Language, 9.5f);
        BackColor = AppContext.Win11Bg;
        AppContext.ApplyModernWindowStyle(this);
        Text = en ? "" : ""; FormBorderStyle = FormBorderStyle.FixedDialog; StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false; ClientSize = new Size(380, 470);

        btnTabTimer = new Button(); SetupTabButton(btnTabTimer, en ? "" : "", 18, 0);
        btnTabQuote = new Button(); SetupTabButton(btnTabQuote, en ? "" : "", 133, 1);
        btnTabAgent = new Button(); SetupTabButton(btnTabAgent, en ? "" : "", 248, 2);

        KeyPreview = true;
        KeyDown += delegate(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.Tab)
            {
                SetActiveTab(e.Shift ? (activeTab + 2) % 3 : (activeTab + 1) % 3);
                e.SuppressKeyPress = true;
            }
        };

        lblMinutes = AddLabel(en ? "" : "", 18, TimerLabelY(0)); minutes = AddNumber(Value.Minutes, 1, 1440, 172, TimerRowY(0));
        minutes.ValueChanged += OnPreviewChanged;
        lblSize = AddLabel(en ? "" : "", 18, TimerLabelY(1)); size = AddNumber(Value.FontSize, 12, 120, 172, TimerRowY(1));
        size.ValueChanged += OnPreviewChanged;
        lblFont = AddLabel(en ? "" : "", 18, TimerLabelY(2));
        fontCombo = AddCombo(TimerRowY(2));
        fontCombo.Items.AddRange(en
            ? new object[] { "", "", "", "", "" }
            : new object[] { "", "", "", "", "" });
        fontCombo.SelectedIndex = Value.FontFamily == "" ? 1 :
            (Value.FontFamily == "" ? 2 :
            (Value.FontFamily == "" ? 3 :
            (Value.FontFamily == "" ? 4 : 0)));
        fontCombo.DrawItem += OnDrawFontItem;
        fontCombo.SelectedIndexChanged += OnQuoteFontComboChanged;
        lblColor = AddLabel(en ? "" : "", 18, TimerLabelY(3)); selectedColor = Value.Color;
        timerColorCombo = AddCombo(TimerRowY(3));
        PopulateTimerColorCombo(en);
        timerColorCombo.SelectedIndex = GetTimerColorIndex(selectedColor);
        timerColorCombo.DrawItem += OnDrawTimerColorItem;
        timerColorCombo.SelectedIndexChanged += OnTimerColorComboChanged;
        lblFormat = AddLabel(en ? "" : "", 18, TimerLabelY(4));
        format = AddCombo(TimerRowY(4));
        format.Items.AddRange(en ? new object[] { "", "", "" } : new object[] { "", "", "" });
        format.SelectedIndex = Value.Format == "" ? 1 : (Value.Format == "" ? 2 : 0);
        format.DrawItem += OnDrawComboItem;
        format.SelectedIndexChanged += OnPreviewChanged;
        hotkeyModifiers = Value.HotkeyModifiers; hotkeyKey = Value.HotkeyKey;
        candidateMods = hotkeyModifiers; candidateKey = hotkeyKey;
        lblEndMsg = AddLabel(en ? "" : "", 18, TimerLabelY(5));
        endMessage = new TextBox();
        endMessage.AutoSize = false;
        endMessage.Multiline = false;
        endMessage.MaxLength = 200;
        endMessage.Cursor = Cursors.IBeam;
        endMessage.Text = Value.EndMessage;
        endMessage.SetBounds(172, TimerRowY(5), 188, 28);
        endMessage.TextChanged += OnPreviewChanged;
        ApplyInputPadding(endMessage);
        ApplySettingsInputSize(endMessage);
        Controls.Add(endMessage);
        lblHotkey = AddLabel(en ? "" : "", 18, TimerLabelY(8));
        hotkeyBox = new TextBox();
        hotkeyBox.AutoSize = false;
        hotkeyBox.ReadOnly = true;
        hotkeyBox.Multiline = false;
        hotkeyBox.SetBounds(172, TimerRowY(8), 188, 28);
        if (!hotkeyActive)
        {
            hotkeyBox.Text = AppContext.HotkeyText(hotkeyModifiers, hotkeyKey) + (en ? "" : "");
            hotkeyBox.ForeColor = Color.FromArgb(202, 80, 16);
        }
        else if (Value.HotkeyKey == (int)Keys.Space && (Value.HotkeyModifiers & 3) != 0)
        {
            hotkeyBox.Text = AppContext.HotkeyText(hotkeyModifiers, hotkeyKey) + (en ? "" : "");
            hotkeyBox.ForeColor = Color.FromArgb(202, 80, 16);
        }
        else
        {
            hotkeyBox.Text = AppContext.HotkeyText(hotkeyModifiers, hotkeyKey);
        }
        hotkeyBox.KeyDown += CaptureHotkey;
        ApplyInputPadding(hotkeyBox);
        ApplySettingsInputSize(hotkeyBox);
        Controls.Add(hotkeyBox);
        lblSound = AddLabel(en ? "" : "", 18, TimerLabelY(6));
        soundStyle = AddCombo(TimerRowY(6));
        PopulateSoundStyleCombo(en);
        soundStyle.SelectedIndex = SoundStyleIndex(Value.EndSound, Value.EndSoundStyle);
        soundStyle.DrawItem += OnDrawComboItem;
        soundStyle.SelectionChangeCommitted += OnSoundStyleCommitted;

        lblNotice = AddLabel(en ? "" : "", 18, TimerLabelY(7));
        noticeStyle = AddCombo(TimerRowY(7));
        PopulateNoticeStyleCombo(en);
        noticeStyle.SelectedIndex = NoticeStyleIndex(Value.EndNotice, Value.NoticeStyle);
        noticeStyle.DrawItem += OnDrawComboItem;
        noticeStyle.SelectionChangeCommitted += OnPreviewChanged;

        // 每日专注目标：关闭 + 1/2/3/4/6/8 小时，和结束通知同一套下拉。
        lblDailyGoal = AddLabel(en ? "" : "", 18, TimerLabelY(9));
        dailyGoal = AddCombo(TimerRowY(9));
        PopulateDailyGoalCombo(en, Value.DailyGoalMinutes);
        dailyGoal.DrawItem += OnDrawComboItem;
        dailyGoal.SelectionChangeCommitted += OnDailyGoalChanged;
        UpdateStreakLabel();


        dialogQuoteList = DailyQuoteDialog.LoadQuotes();
        if (dialogQuoteList == null || dialogQuoteList.Count == 0) dialogQuoteList = new List<QuoteItem>(DailyQuoteDialog.DefaultQuotes);
        dialogQuoteIndex = AppContext.ResolveTodayQuoteIndex(s, dialogQuoteList.Count);

        // Section 1 Hero Banner: panelQuoteCard (Top = 62, Height = 98px, vertically centered content)
        panelQuoteCard = new Panel();
        panelQuoteCard.SetBounds(18, 62, 344, 98);
        panelQuoteCard.BackColor = Color.FromArgb(246, 248, 250);
        panelQuoteCard.Paint += delegate(object sender, PaintEventArgs pe)
        {
            pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            pe.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // Top-left "" / "" corner triangle ribbon badge (Proposal A)
            int badgeSize = 36;
            PointF[] pts = new PointF[] {
                new PointF(1, 1),
                new PointF(badgeSize, 1),
                new PointF(1, badgeSize)
            };
            using (SolidBrush b = new SolidBrush(AppContext.Win11Accent))
            {
                pe.Graphics.FillPolygon(b, pts);
            }

            GraphicsState st = pe.Graphics.Save();
            pe.Graphics.TranslateTransform(11.5f, 11.5f);
            pe.Graphics.RotateTransform(-45);
            bool isEn = DialogEnglish();
            string tagText = isEn ? "" : "";
            using (Font f = AppContext.GetUiFont(isEn ? "" : "", isEn ? 6.5f : 7.5f, FontStyle.Bold))
            using (SolidBrush tb = new SolidBrush(Color.White))
            {
                SizeF sz = pe.Graphics.MeasureString(tagText, f);
                pe.Graphics.DrawString(tagText, f, tb, -sz.Width / 2.0f + 0.5f, -sz.Height / 2.0f + 0.5f);
            }
            pe.Graphics.Restore(st);

            using (Pen p = new Pen(AppContext.Win11Border, 1f))
            {
                pe.Graphics.DrawRectangle(p, 0, 0, panelQuoteCard.Width - 1, panelQuoteCard.Height - 1);
            }
        };
        Controls.Add(panelQuoteCard);

        lblCardQuoteText = new Label();
        lblCardQuoteText.BackColor = Color.Transparent;
        lblCardQuoteText.SetBounds(38, 12, 268, 48);
        lblCardQuoteText.Font = AppContext.GetUiFont(en ? "" : "", 9.5f, FontStyle.Regular);
        lblCardQuoteText.ForeColor = Color.FromArgb(31, 41, 55);
        lblCardQuoteText.TextAlign = ContentAlignment.MiddleCenter;
        lblCardQuoteText.AutoEllipsis = true;
        panelQuoteCard.Controls.Add(lblCardQuoteText);

        btnNextQuote = new Button();
        btnNextQuote.Text = "";
        btnNextQuote.SetBounds(12, 64, 26, 24);
        btnNextQuote.FlatStyle = FlatStyle.Flat;
        btnNextQuote.FlatAppearance.BorderSize = 0;
        btnNextQuote.FlatAppearance.MouseDownBackColor = Color.FromArgb(220, 224, 230);
        btnNextQuote.FlatAppearance.MouseOverBackColor = Color.FromArgb(235, 238, 242);
        btnNextQuote.BackColor = Color.Transparent;
        btnNextQuote.Cursor = Cursors.Hand;
        btnNextQuote.Paint += delegate(object sender, PaintEventArgs pe)
        {
            pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            bool hov = btnNextQuote.ClientRectangle.Contains(btnNextQuote.PointToClient(Cursor.Position));
            Color iconCol = hov ? AppContext.Win11Accent : Color.FromArgb(120, 125, 135);
            float cx = btnNextQuote.Width / 2.0f;
            float cy = btnNextQuote.Height / 2.0f;
            using (Pen p = new Pen(iconCol, 1.5f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                pe.Graphics.DrawArc(p, cx - 5.0f, cy - 5.0f, 10f, 10f, 45, 270);
                float ax = cx + 3.5f;
                float ay = cy - 3.5f;
                pe.Graphics.DrawLine(p, ax, ay, ax + 3.0f, ay);
                pe.Graphics.DrawLine(p, ax, ay, ax, ay - 3.0f);
            }
        };
        btnNextQuote.MouseEnter += delegate { btnNextQuote.Invalidate(); };
        btnNextQuote.MouseLeave += delegate { btnNextQuote.Invalidate(); };
        if (toolTip == null) toolTip = new ToolTip();
        toolTip.SetToolTip(btnNextQuote, en ? "" : "");
        btnNextQuote.Click += delegate
        {
            if (dialogQuoteList != null && dialogQuoteList.Count > 0)
            {
                dialogQuoteIndex = (dialogQuoteIndex + 1) % dialogQuoteList.Count;
                UpdateDialogQuoteCard();
                if (QuoteNextRequested != null) QuoteNextRequested();
                TriggerQuotePreview();
            }
        };
        panelQuoteCard.Controls.Add(btnNextQuote);

        lblCardQuoteAuthor = new Label();
        lblCardQuoteAuthor.SetBounds(44, 64, 286, 24);
        lblCardQuoteAuthor.Font = AppContext.GetUiFont(en ? "" : "", 8.5f, FontStyle.Italic);
        lblCardQuoteAuthor.ForeColor = AppContext.Win11TextSecondary;
        lblCardQuoteAuthor.TextAlign = ContentAlignment.MiddleRight;
        lblCardQuoteAuthor.AutoEllipsis = true;
        panelQuoteCard.Controls.Add(lblCardQuoteAuthor);
        UpdateDialogQuoteCard();

        // Section 2: Daily quote settings grid. The master switch leads the grid —
        // with it off none of the style rows below apply — and the rows sit on a
        // uniform 34px pitch so all of them fit between the card and the tip.
        // Row 1: Master switch (Y = 176)
        lblDailyQuote = AddLabel(en ? "" : "", 18, 176);
        lblDailyQuote.SetBounds(18, 176, 296, 24); // the label is wider than AddLabel's 148px default and would clip
        dailyQuoteToggle = new ToggleSwitch();
        dailyQuoteToggle.Checked = Value.DailyQuote;
        dailyQuoteToggle.SetBounds(322, 176, 40, 22);
        dailyQuoteToggle.CheckedChanged += OnDailyQuoteCheckChanged;
        if (toolTip == null) toolTip = new ToolTip();
        toolTip.SetToolTip(dailyQuoteToggle, en
            ? ""
            : "");
        Controls.Add(dailyQuoteToggle);

        // Row 2: Font Size (Y = 210)
        lblQuoteSize = AddLabel(en ? "" : "", 18, 210);
        quoteFontSize = new SettingsNumberBox();
        quoteFontSize.DecimalPlaces = 1;
        quoteFontSize.Increment = 0.5M;
        quoteFontSize.Minimum = 8.0M;
        quoteFontSize.Maximum = 24.0M;
        float initQuoteSize = (Value.QuoteFontSize >= 8.0f && Value.QuoteFontSize <= 24.0f) ? Value.QuoteFontSize : 18.0f;
        quoteFontSize.Value = (decimal)initQuoteSize;
        quoteFontSize.SetBounds(172, 206, 188, 28);
        ApplyInputPadding(quoteFontSize);
        quoteFontSize.ValueChanged += OnQuoteFontSizeChanged;
        Controls.Add(quoteFontSize);

        // Row 3: Font Style (Y = 244)
        lblQuoteFont = AddLabel(en ? "" : "", 18, 244);
        quoteFontCombo = AddCombo(240);
        quoteFontCombo.DropDownWidth = 220;
        quoteFontCombo.MaxDropDownItems = 10;
        quoteFontCombo.Items.AddRange(en
            ? new object[] { "", "", "", "", "", "", "", "", "", "" }
            : new object[] { "", "", "", "", "", "", "", "", "", "" });
        quoteFontCombo.SelectedIndex = GetQuoteFontIndex(selectedQuoteFont);
        quoteFontCombo.DrawItem += OnDrawQuoteFontItem;
        quoteFontCombo.SelectedIndexChanged += OnQuoteFontChanged;

        // Row 4: Color (Full width 188px, self-contained color chips, Y = 278)
        lblQuoteColor = AddLabel(en ? "" : "", 18, 278);
        quoteColorCombo = AddCombo(274);
        PopulateQuoteColorCombo(en);
        quoteColorCombo.SelectedIndex = GetQuoteColorIndex(selectedQuoteColor);
        quoteColorCombo.DrawItem += OnDrawQuoteColorItem;
        quoteColorCombo.SelectedIndexChanged += OnQuoteColorComboChanged;

        // Row 5: Window Layer (常驻层级, Y = 312. Clicking toggle re-uses move-mode.)
        lblQuoteLayer = AddLabel(en ? "" : "", 18, 312);
        quoteLayerCombo = AddCombo(308);
        quoteLayerCombo.Items.AddRange(en
            ? new object[] { "", "" }
            : new object[] { "", "" });
        quoteLayerCombo.SelectedIndex = Value.QuotePinTopMost ? 1 : 0;
        quoteLayerCombo.DrawItem += OnDrawComboItem;
        quoteLayerCombo.SelectedIndexChanged += OnQuoteLayerChanged;

        // Row 6: Quote Library (Y = 346)
        lblQuoteQuotes = AddLabel(en ? "" : "", 18, 346);
        btnEditQuotes = new Button();
        btnEditQuotes.Text = en ? "" : "";
        btnEditQuotes.Font = AppContext.GetUiFont(en ? "" : "", 9f);
        btnEditQuotes.SetBounds(172, 342, 90, 28);
        AppContext.StyleSecondaryButton(btnEditQuotes);
        btnEditQuotes.Click += OnEditQuotesClick;
        if (toolTip == null) toolTip = new ToolTip();
        toolTip.SetToolTip(btnEditQuotes, en ? "" : "");
        Controls.Add(btnEditQuotes);

        btnAiQuotes = new Button();
        btnAiQuotes.Text = en ? "" : "";
        btnAiQuotes.Font = AppContext.GetUiFont(en ? "" : "", 9f);
        btnAiQuotes.SetBounds(270, 342, 90, 28);
        AppContext.StyleSecondaryButton(btnAiQuotes);
        btnAiQuotes.Click += OnAiQuotesClick;
        toolTip.SetToolTip(btnAiQuotes, en ? "" : "");
        Controls.Add(btnAiQuotes);

        // Row 7: Screensaver — preview link + one dropdown that is both the on/off
        // switch and the idle wait. iClock owns this because Windows' own screen
        // saver dialog only lists System32 .scr files and so cannot show or edit
        // this entry.
        lnkPreviewSaver = new LinkLabel();
        string saverLabel = SaverRowLabel(en);
        lnkPreviewSaver.Text = saverLabel;
        int linkStart = saverLabel.IndexOf('[');
        int linkLen = saverLabel.Length - linkStart;
        lnkPreviewSaver.LinkArea = new LinkArea(linkStart < 0 ? 0 : linkStart, linkLen);
        lnkPreviewSaver.LinkColor = AppContext.Win11Accent;
        lnkPreviewSaver.ActiveLinkColor = AppContext.Win11Accent;
        lnkPreviewSaver.LinkBehavior = LinkBehavior.HoverUnderline;
        lnkPreviewSaver.ForeColor = AppContext.Win11TextPrimary;
        lnkPreviewSaver.SetBounds(18, 380, 148, 24); // the wait dropdown takes the middle column
        lnkPreviewSaver.TextAlign = ContentAlignment.MiddleLeft;
        lnkPreviewSaver.LinkClicked += OnPreviewSaverLinkClicked;
        toolTip.SetToolTip(lnkPreviewSaver, en
            ? ""
            : "");
        Controls.Add(lnkPreviewSaver);

        quoteSaverWaitCombo = new ComboBox();
        quoteSaverWaitCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        quoteSaverWaitCombo.DrawMode = DrawMode.OwnerDrawFixed;
        quoteSaverWaitCombo.ItemHeight = 22;
        quoteSaverWaitCombo.SetBounds(172, 376, 188, 28); // full column: this dropdown replaces the toggle too
        PopulateSaverWaitCombo(en);
        quoteSaverWaitCombo.DrawItem += OnDrawComboItem;
        quoteSaverWaitCombo.SelectedIndexChanged += OnSaverWaitChanged;
        UpdateSaverTooltip(en);
        Controls.Add(quoteSaverWaitCombo);

        // Section 3: Tip (Y = 428)
        lblQuoteTip = new StyledTipLabel(en
            ? new string[] { "", "" }
            : new string[] { "", "" });
        lblQuoteTip.UseMnemonic = false;
        lblQuoteTip.Text = en
            ? ""
            : "";
        lblQuoteTip.ForeColor = AppContext.Win11TextSecondary;
        lblQuoteTip.Font = AppContext.GetUiFont(en ? "" : "", 8f);
        // 48px above the saver row (the grid's own pitch is 34), 18px gutter like
        // the labels above.
        lblQuoteTip.SetBounds(18, 428, 344, 22);
        Controls.Add(lblQuoteTip);


        // Main title row: title + master switch.
        Label lblCapabilityTitle = new Label();
        lblCapabilityTitle.Name = "";
        lblCapabilityTitle.Text = en ? "" : "";
        lblCapabilityTitle.Font = AppContext.GetUiFont(en ? "" : "", 9f, FontStyle.Bold);
        lblCapabilityTitle.ForeColor = AppContext.Win11TextPrimary;
        lblCapabilityTitle.TextAlign = ContentAlignment.MiddleLeft;
        lblCapabilityTitle.SetBounds(18, 56, 280, 22);
        Controls.Add(lblCapabilityTitle);

        chkDesktopCollaboration = new ToggleSwitch();
        chkDesktopCollaboration.SetBounds(322, 56, 40, 22);
        chkDesktopCollaboration.AccessibleName = en ? "" : "";
        chkDesktopCollaboration.Checked = Value.DesktopCollaborationEnabled;
        chkDesktopCollaboration.CheckedChanged += OnDesktopCollaborationChanged;
        Controls.Add(chkDesktopCollaboration);
        if (toolTip == null) toolTip = new ToolTip();
        toolTip.SetToolTip(chkDesktopCollaboration, en
            ? ""
            : "");

        // Subtitle: scenario description.
        Label lblCapabilitySub = new Label();
        lblCapabilitySub.Name = "";
        lblCapabilitySub.Text = en
            ? ""
            : "";
        lblCapabilitySub.Font = AppContext.GetUiFont(en ? "" : "", 8f);
        lblCapabilitySub.ForeColor = AppContext.Win11TextSecondary;
        lblCapabilitySub.TextAlign = ContentAlignment.MiddleLeft;
        lblCapabilitySub.SetBounds(18, 78, 344, 28);
        lblCapabilitySub.AutoEllipsis = true;
        Controls.Add(lblCapabilitySub);

        panelCapCard = new Panel();
        panelCapCard.SetBounds(18, 110, 344, 132);
        panelCapCard.BackColor = Color.White;
        panelCapCard.Paint += OnCapabilityCardPaint;
        Controls.Add(panelCapCard);
        panelCapabilityCard = panelCapCard; // Keep legacy reference for compatibility

        // Section title: teach agents the capabilities.
        lblSkillGuidance = new Label();
        lblSkillGuidance.Name = "";
        lblSkillGuidance.Text = en
            ? ""
            : "";
        lblSkillGuidance.Font = AppContext.GetUiFont(en ? "" : "", 8f);
        lblSkillGuidance.ForeColor = AppContext.Win11TextSecondary;
        lblSkillGuidance.SetBounds(18, 248, 344, 18);
        Controls.Add(lblSkillGuidance);

        // Skill card: dynamic per-agent rows + empty-state fallback + install-failure closure.
        panelSkillCard = new Panel();
        panelSkillCard.SetBounds(18, 270, 344, 84);
        panelSkillCard.BackColor = Color.White;
        panelSkillCard.AutoScroll = true;
        panelSkillCard.HorizontalScroll.Enabled = false;
        panelSkillCard.Paint += OnCardPaint;
        Controls.Add(panelSkillCard);

        panelSessionCard = new Panel();
        panelSessionCard.SetBounds(18, 362, 344, 84);
        panelSessionCard.BackColor = Color.White;
        panelSessionCard.Paint += OnCardPaint;
        Controls.Add(panelSessionCard);

        panelAgentRows = new Panel();
        // Wide enough for the row dividers to reach the card's right edge, but not
        // past the client area AutoScroll leaves once a vertical scrollbar appears:
        // at the card's full 344px the panel overflowed by the bar's 17px and
        // WinForms answered with a horizontal scrollbar.
        panelAgentRows.SetBounds(0, 0, panelSkillCard.Width - SystemInformation.VerticalScrollBarWidth, 10);
        panelAgentRows.BackColor = Color.Transparent;
        panelSkillCard.Controls.Add(panelAgentRows);

        lblSkillMethod1 = new Label();
        lblSkillMethod1.Font = AppContext.GetUiFont(en ? "" : "", 8f, FontStyle.Bold);
        lblSkillMethod1.ForeColor = AppContext.Win11TextPrimary;
        lblSkillMethod1.SetBounds(14, 6, 300, 16);
        panelSkillCard.Controls.Add(lblSkillMethod1);

        lblSkillMethod2 = new Label();
        lblSkillMethod2.Font = AppContext.GetUiFont(en ? "" : "", 8f, FontStyle.Bold);
        lblSkillMethod2.ForeColor = AppContext.Win11TextPrimary;
        lblSkillMethod2.SetBounds(14, 6, 316, 18);
        panelSessionCard.Controls.Add(lblSkillMethod2);

        lblCliGuideHint = new Label();
        lblCliGuideHint.Font = AppContext.GetUiFont(en ? "" : "", 8f);
        lblCliGuideHint.ForeColor = AppContext.Win11TextSecondary;
        lblCliGuideHint.SetBounds(14, 30, 208, 36);
        panelSessionCard.Controls.Add(lblCliGuideHint);

        btnCopyCliGuide = new Button();
        btnCopyCliGuide.Font = AppContext.GetUiFont(en ? "" : "", 8f);
        btnCopyCliGuide.SetBounds(232, 36, 98, 24);
        AppContext.StyleSecondaryButton(btnCopyCliGuide);
        btnCopyCliGuide.Click += OnCopyCliGuideClick;
        panelSessionCard.Controls.Add(btnCopyCliGuide);

        lblAgentEmpty = new Label();
        lblAgentEmpty.Name = "";
        lblAgentEmpty.Font = AppContext.GetUiFont(en ? "" : "", 8f);
        lblAgentEmpty.ForeColor = AppContext.Win11TextSecondary;
        lblAgentEmpty.SetBounds(14, 12, 300, 40);
        lblAgentEmpty.Visible = false;
        panelSkillCard.Controls.Add(lblAgentEmpty);

        // Install-failure closure panel (hidden until an install fails).
        panelInstallFail = new Panel();
        panelInstallFail.SetBounds(10, 76, 310, 76);
        panelInstallFail.BackColor = Color.FromArgb(255, 250, 243);
        panelInstallFail.Visible = false;
        panelSkillCard.Controls.Add(panelInstallFail);

        lblFailReason = new Label();
        lblFailReason.Font = AppContext.GetUiFont(en ? "" : "", 8f);
        lblFailReason.ForeColor = Color.FromArgb(180, 83, 9);
        lblFailReason.SetBounds(8, 6, 294, 16);
        panelInstallFail.Controls.Add(lblFailReason);

        lblFailHint = new Label();
        lblFailHint.Font = AppContext.GetUiFont(en ? "" : "", 8f);
        lblFailHint.ForeColor = AppContext.Win11TextSecondary;
        lblFailHint.SetBounds(8, 24, 294, 26);
        panelInstallFail.Controls.Add(lblFailHint);

        btnRetryInstall = new Button();
        btnRetryInstall.Text = en ? "" : "";
        btnRetryInstall.Font = AppContext.GetUiFont(en ? "" : "", 8f);
        btnRetryInstall.SetBounds(8, 52, 70, 22);
        AppContext.StyleSecondaryButton(btnRetryInstall);
        btnRetryInstall.Click += OnRetryInstallClick;
        panelInstallFail.Controls.Add(btnRetryInstall);

        btnOverwriteInstall = new Button();
        btnOverwriteInstall.Text = en ? "" : "";
        btnOverwriteInstall.Font = AppContext.GetUiFont(en ? "" : "", 8f);
        btnOverwriteInstall.SetBounds(84, 52, 70, 22);
        AppContext.StyleSecondaryButton(btnOverwriteInstall);
        btnOverwriteInstall.Click += OnOverwriteInstallClick;
        panelInstallFail.Controls.Add(btnOverwriteInstall);

        btnCopyFixPrompt = new Button();
        btnCopyFixPrompt.Text = en ? "" : "";
        btnCopyFixPrompt.Font = AppContext.GetUiFont(en ? "" : "", 8f);
        btnCopyFixPrompt.SetBounds(160, 52, 96, 22);
        AppContext.StyleSecondaryButton(btnCopyFixPrompt);
        btnCopyFixPrompt.Click += OnCopyFixPromptClick;
        panelInstallFail.Controls.Add(btnCopyFixPrompt);

        lblSkillFeedback = new Label();
        lblSkillFeedback.Name = "";
        lblSkillFeedback.Font = AppContext.GetUiFont(en ? "" : "", 8f);
        lblSkillFeedback.ForeColor = AppContext.Win11TextSecondary;
        lblSkillFeedback.AutoSize = false;
        lblSkillFeedback.SetBounds(14, 156, 300, 18);
        lblSkillFeedback.Visible = false;
        panelSkillCard.Controls.Add(lblSkillFeedback);

        // Rebuild agent rows so the new language's button text applies (fingerprint includes language,
        // but force it anyway in case the cache still holds the old DetectAgents list).
        agentRowsFingerprint = null;
        RefreshSkillInstallUi();

        // Row 1: decision approval
        lblTool1Name = new Label();
        lblTool1Name.Text = en ? "" : "";
        lblTool1Name.Font = AppContext.GetUiFont(en ? "" : "", 9f, FontStyle.Regular);
        lblTool1Name.ForeColor = AppContext.Win11TextPrimary;
        lblTool1Name.TextAlign = ContentAlignment.MiddleLeft;
        lblTool1Name.SetBounds(14, 3, 252, 18);
        panelCapabilityCard.Controls.Add(lblTool1Name);

        lblTool1Desc = new Label();
        lblTool1Desc.Text = en ? "" : "";
        lblTool1Desc.Font = AppContext.GetUiFont(en ? "" : "", 8f);
        lblTool1Desc.ForeColor = AppContext.Win11TextSecondary;
        lblTool1Desc.SetBounds(14, 19, 252, 16);
        panelCapabilityCard.Controls.Add(lblTool1Desc);

        btnAskDemo = new Button();
        btnAskDemo.Text = en ? "" : "";
        btnAskDemo.Font = AppContext.GetUiFont(en ? "" : "", 8.5f);
        btnAskDemo.SetBounds(274, 9, 58, 26);
        AppContext.StyleSecondaryButton(btnAskDemo);
        btnAskDemo.Click += OnAskDemoClick;
        panelCapabilityCard.Controls.Add(btnAskDemo);

        // Row 2: task status
        lblTool2Name = new Label();
        lblTool2Name.Text = en ? "" : "";
        lblTool2Name.Font = AppContext.GetUiFont(en ? "" : "", 9f, FontStyle.Regular);
        lblTool2Name.ForeColor = AppContext.Win11TextPrimary;
        lblTool2Name.TextAlign = ContentAlignment.MiddleLeft;
        lblTool2Name.SetBounds(14, 47, 252, 18);
        panelCapabilityCard.Controls.Add(lblTool2Name);

        lblTool2Desc = new Label();
        lblTool2Desc.Text = en ? "" : "";
        lblTool2Desc.Font = AppContext.GetUiFont(en ? "" : "", 8f);
        lblTool2Desc.ForeColor = AppContext.Win11TextSecondary;
        lblTool2Desc.SetBounds(14, 63, 252, 16);
        panelCapabilityCard.Controls.Add(lblTool2Desc);

        btnProgressDemo = new Button();
        btnProgressDemo.Text = en ? "" : "";
        btnProgressDemo.Font = AppContext.GetUiFont(en ? "" : "", 8.5f);
        btnProgressDemo.SetBounds(274, 53, 58, 26);
        AppContext.StyleSecondaryButton(btnProgressDemo);
        btnProgressDemo.Click += OnProgressDemoClick;
        panelCapabilityCard.Controls.Add(btnProgressDemo);

        // Row 3: completion reminder
        lblTool3Name = new Label();
        lblTool3Name.Text = en ? "" : "";
        lblTool3Name.Font = AppContext.GetUiFont(en ? "" : "", 9f, FontStyle.Regular);
        lblTool3Name.ForeColor = AppContext.Win11TextPrimary;
        lblTool3Name.TextAlign = ContentAlignment.MiddleLeft;
        lblTool3Name.SetBounds(14, 91, 252, 18);
        panelCapabilityCard.Controls.Add(lblTool3Name);

        lblTool3Desc = new Label();
        lblTool3Desc.Text = en ? "" : "";
        lblTool3Desc.Font = AppContext.GetUiFont(en ? "" : "", 8f);
        lblTool3Desc.ForeColor = AppContext.Win11TextSecondary;
        lblTool3Desc.SetBounds(14, 107, 252, 16);
        panelCapabilityCard.Controls.Add(lblTool3Desc);

        btnNotifyDemo = new Button();
        btnNotifyDemo.Text = en ? "" : "";
        btnNotifyDemo.Font = AppContext.GetUiFont(en ? "" : "", 8.5f);
        btnNotifyDemo.SetBounds(274, 97, 58, 26);
        AppContext.StyleSecondaryButton(btnNotifyDemo);
        btnNotifyDemo.Click += OnNotifyDemoClick;
        panelCapabilityCard.Controls.Add(btnNotifyDemo);

        string capabilityTip1 = en ? "" : "";
        string capabilityTip2 = en ? "" : "";
        string capabilityTip3 = en ? "" : "";
        toolTip.SetToolTip(lblTool1Name, capabilityTip1);
        toolTip.SetToolTip(btnAskDemo, capabilityTip1);
        toolTip.SetToolTip(lblTool2Name, capabilityTip2);
        toolTip.SetToolTip(btnProgressDemo, capabilityTip2);
        toolTip.SetToolTip(lblTool3Name, capabilityTip3);
        toolTip.SetToolTip(btnNotifyDemo, capabilityTip3);

        AppContext.CenterFormOnScreen(this);
        SetActiveTab(lastActiveTab);
    }

    internal void SetActiveTab(int tabIndex)
    {
        activeTab = tabIndex;
        lastActiveTab = tabIndex;
        bool isTimer = (tabIndex == 0);
        bool isQuote = (tabIndex == 1);
        bool isAgent = (tabIndex == 2);

        lblMinutes.Visible = isTimer;
        minutes.Visible = isTimer;
        lblSize.Visible = isTimer;
        size.Visible = isTimer;
        lblFont.Visible = isTimer;
        fontCombo.Visible = isTimer;
        lblColor.Visible = isTimer;
        timerColorCombo.Visible = isTimer;
        lblFormat.Visible = isTimer;
        format.Visible = isTimer;
        lblEndMsg.Visible = isTimer;
        endMessage.Visible = isTimer;
        lblHotkey.Visible = isTimer;
        hotkeyBox.Visible = isTimer;
        if (lblSound != null) lblSound.Visible = isTimer;
        soundStyle.Visible = isTimer;
        if (lblNotice != null) lblNotice.Visible = isTimer;
        noticeStyle.Visible = isTimer;
        if (lblDailyGoal != null) lblDailyGoal.Visible = isTimer;
        if (dailyGoal != null) dailyGoal.Visible = isTimer;

        if (lblDailyQuote != null) lblDailyQuote.Visible = isQuote;
        if (dailyQuoteToggle != null) dailyQuoteToggle.Visible = isQuote;
        panelQuoteCard.Visible = isQuote;
        lblQuoteSize.Visible = isQuote;
        quoteFontSize.Visible = isQuote;
        lblQuoteFont.Visible = isQuote;
        quoteFontCombo.Visible = isQuote;
        lblQuoteColor.Visible = isQuote;
        quoteColorCombo.Visible = isQuote;
        lblQuoteLayer.Visible = isQuote;
        quoteLayerCombo.Visible = isQuote;
        lblQuoteQuotes.Visible = isQuote;
        btnEditQuotes.Visible = isQuote;
        if (lnkPreviewSaver != null) lnkPreviewSaver.Visible = isQuote;
        if (quoteSaverWaitCombo != null) quoteSaverWaitCombo.Visible = isQuote;
        if (btnAiQuotes != null) btnAiQuotes.Visible = isQuote;
        lblQuoteTip.Visible = isQuote;
        SyncQuoteStyleEnabled();

        if (panelSkillCard != null) panelSkillCard.Visible = isAgent;
        if (panelSessionCard != null) panelSessionCard.Visible = isAgent;
        if (panelCapCard != null) panelCapCard.Visible = isAgent;
        if (lblSkillMethod2 != null) lblSkillMethod2.Visible = isAgent;
        if (lblCliGuideHint != null) lblCliGuideHint.Visible = isAgent;
        if (btnCopyCliGuide != null) btnCopyCliGuide.Visible = isAgent;
        if (chkDesktopCollaboration != null) chkDesktopCollaboration.Visible = isAgent;
        if (lblSkillGuidance != null) lblSkillGuidance.Visible = isAgent;
        foreach (Control control in Controls)
        {
            if (control != null && (control.Name == "" || control.Name == "")) control.Visible = isAgent;
        }

        UpdateTabButtons();
        TriggerQuotePreview();
        if (isAgent)
        {
            bool collabEnabled = chkDesktopCollaboration != null && chkDesktopCollaboration.Checked;
            if (panelCapCard != null) panelCapCard.Enabled = collabEnabled;
            if (panelSkillCard != null) panelSkillCard.Enabled = collabEnabled;
            if (panelSessionCard != null) panelSessionCard.Enabled = collabEnabled;
            if (btnCopyCliGuide != null) btnCopyCliGuide.Enabled = collabEnabled;
            if (lblSkillGuidance != null) lblSkillGuidance.Enabled = collabEnabled;
            RefreshSkillInstallUi();
            if (agentListTimer == null)
            {
                agentListTimer = new System.Windows.Forms.Timer();
                agentListTimer.Interval = 3000;
                agentListTimer.Tick += delegate { RefreshSkillInstallUi(); };
            }
            agentListTimer.Start();
        }
        else if (agentListTimer != null) agentListTimer.Stop();
        Update();
    }

    private void UpdateTabButtons()
    {
        if (btnTabTimer != null) btnTabTimer.Invalidate();
        if (btnTabQuote != null) btnTabQuote.Invalidate();
        if (btnTabAgent != null) btnTabAgent.Invalidate();
    }

    private void DrawWin11Tab(Graphics g, Button btn, int tabIdx)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        bool isActive = (activeTab == tabIdx);
        bool isHov = (hoveredTab == tabIdx && !isActive);

        g.Clear(BackColor);

        // Continuous baseline divider running across inactive tabs only
        if (!isActive)
        {
            using (Pen basePen = new Pen(Color.FromArgb(226, 230, 236), 1f))
            {
                g.DrawLine(basePen, 0, btn.Height - 1, btn.Width, btn.Height - 1);
            }
        }

        if (isActive)
        {
            // Windows 11 File Explorer Active Tab: pure white card with 6px top rounded corners and flat/seamless bottom
            Rectangle cardRect = new Rectangle(0, 2, btn.Width - 1, btn.Height - 2);
            using (GraphicsPath path = new GraphicsPath())
            {
                int d = 6 * 2;
                path.AddArc(cardRect.X, cardRect.Y, d, d, 180, 90);
                path.AddArc(cardRect.Right - d, cardRect.Y, d, d, 270, 90);
                path.AddLine(cardRect.Right, cardRect.Bottom, cardRect.X, cardRect.Bottom);
                path.CloseFigure();

                using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(10, 0, 0, 0)))
                using (SolidBrush bgBrush = new SolidBrush(Color.White))
                using (Pen borderPen = new Pen(Color.FromArgb(216, 220, 228), 1f))
                {
                    // Subtle top shadow
                    using (GraphicsPath shadowPath = new GraphicsPath())
                    {
                        shadowPath.AddArc(cardRect.X, cardRect.Y + 1, d, d, 180, 90);
                        shadowPath.AddArc(cardRect.Right - d, cardRect.Y + 1, d, d, 270, 90);
                        shadowPath.AddLine(cardRect.Right, cardRect.Bottom, cardRect.X, cardRect.Bottom);
                        shadowPath.CloseFigure();
                        g.FillPath(shadowBrush, shadowPath);
                    }
                    g.FillPath(bgBrush, path);

                    // Draw U-shaped border (left, top, right) leaving the bottom seamlessly open
                    using (GraphicsPath borderPath = new GraphicsPath())
                    {
                        borderPath.AddLine(cardRect.X, cardRect.Bottom, cardRect.X, cardRect.Y + 6);
                        borderPath.AddArc(cardRect.X, cardRect.Y, d, d, 180, 90);
                        borderPath.AddLine(cardRect.X + 6, cardRect.Y, cardRect.Right - 6, cardRect.Y);
                        borderPath.AddArc(cardRect.Right - d, cardRect.Y, d, d, 270, 90);
                        borderPath.AddLine(cardRect.Right, cardRect.Y + 6, cardRect.Right, cardRect.Bottom);
                        g.DrawPath(borderPen, borderPath);
                    }
                }
            }
        }
        else if (isHov)
        {
            // Inactive Tab hover: soft rounded preview capsule
            Rectangle hovRect = new Rectangle(3, 4, btn.Width - 7, btn.Height - 8);
            using (GraphicsPath hovPath = AppContext.GetRoundedRectPath(hovRect, 5))
            using (SolidBrush hovBrush = new SolidBrush(Color.FromArgb(235, 239, 245)))
            {
                g.FillPath(hovBrush, hovPath);
            }
        }

        // Win11 File Explorer vertical divider between inactive tabs
        if (!isActive && tabIdx < 2 && activeTab != tabIdx + 1)
        {
            using (Pen divPen = new Pen(Color.FromArgb(212, 216, 222), 1f))
            {
                int divH = 14;
                int divY = (btn.Height - divH) / 2;
                g.DrawLine(divPen, btn.Width - 1, divY, btn.Width - 1, divY + divH);
            }
        }

        // Pure clean text without emoji clutter
        string label = btn.Text ?? "";

        Color textCol = isActive ? Color.FromArgb(24, 28, 36) : (isHov ? Color.FromArgb(32, 36, 44) : Color.FromArgb(95, 107, 124));
        Font tabFont = AppContext.GetUiFont(DialogEnglish() ? "" : "", 9.5f, isActive ? FontStyle.Bold : FontStyle.Regular);
        using (tabFont)
        using (SolidBrush textBrush = new SolidBrush(textCol))
        using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        {
            g.DrawString(label, tabFont, textBrush, new RectangleF(0, isActive ? 1 : 0, btn.Width, btn.Height), sf);
        }
    }

    private Color GetEffectiveQuoteColor()
    {
        string col = !string.IsNullOrEmpty(selectedQuoteColor) ? selectedQuoteColor : "";
        try { return ColorTranslator.FromHtml(col); }
        catch { return Color.White; }
    }

    private static int GetQuoteFontIndex(string font)
    {
        if (string.IsNullOrEmpty(font)) return 0;
        for (int i = 1; i < quoteFontFamilies.Length; i++)
        {
            if (string.Equals(quoteFontFamilies[i], font, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return 0;
    }

    private static int GetTimerColorIndex(string colorHex)
    {
        if (string.IsNullOrEmpty(colorHex)) return 0;
        for (int i = 0; i < 9; i++)
            if (string.Equals(TimerPresetColors[i], colorHex, StringComparison.OrdinalIgnoreCase)) return i;
        return 9;
    }

    private static int GetQuoteColorIndex(string colorHex)
    {
        if (string.IsNullOrEmpty(colorHex)) return 0;
        for (int i = 1; i < 8; i++)
            if (string.Equals(QuotePresetColors[i], colorHex, StringComparison.OrdinalIgnoreCase)) return i;
        return 8;
    }

    private static void PopulateColorCombo(ComboBox cb, bool en, string[] namesZh, string[] namesEn)
    {
        if (cb == null) return;
        cb.Items.Clear();
        string[] names = en ? namesEn : namesZh;
        for (int i = 0; i < names.Length; i++)
            cb.Items.Add(names[i]);
    }
    private void PopulateTimerColorCombo(bool en)
    {
        // Timer keeps its original 9 presets plus a "" header (idx 0 -> TimerPresetColors[0]).
        if (timerColorCombo == null) return;
        timerColorCombo.Items.Clear();
        timerColorCombo.Items.Add(en ? "" : "");
        string[] names = en ? timerColorNamesEn : timerColorNamesZh;
        for (int i = 0; i < names.Length; i++) timerColorCombo.Items.Add(names[i]);
    }
    private void PopulateQuoteColorCombo(bool en)
    {
        PopulateColorCombo(quoteColorCombo, en, quoteColorNamesZh, quoteColorNamesEn);
    }

    private void DrawColorItem(DrawItemEventArgs e, ComboBox cb, string[] presets, string currentHex, bool isQuote)
    {
        if (cb == null || e.Index < 0 || e.Index >= cb.Items.Count) return;
        e.DrawBackground();
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        Color chipCol;
        if (e.Index == 0)
        {
            chipCol = isQuote ? ColorTranslator.FromHtml(presets[0]) : Color.Red;
        }
        else if (e.Index >= 1 && e.Index <= (isQuote ? 7 : 8))
        {
            chipCol = ColorTranslator.FromHtml(presets[e.Index]);
        }
        else
        {
            bool isCustom = isQuote
                ? (!string.IsNullOrEmpty(selectedQuoteColor) && GetQuoteColorIndex(selectedQuoteColor) == 8)
                : (!string.IsNullOrEmpty(selectedColor) && GetTimerColorIndex(selectedColor) == 9);
            if (isCustom)
            {
                try { chipCol = ColorTranslator.FromHtml(currentHex); } catch { chipCol = Color.White; }
            }
            else
            {
                chipCol = AppContext.Win11Accent;
            }
        }

        int circleSize = 10;
        int circleX = e.Bounds.X + 6;
        int circleY = e.Bounds.Y + (e.Bounds.Height - circleSize) / 2;

        using (SolidBrush sb = new SolidBrush(chipCol))
        using (Pen p = new Pen(Color.FromArgb(140, 140, 140), 1f))
        {
            e.Graphics.FillEllipse(sb, circleX, circleY, circleSize, circleSize);
            e.Graphics.DrawEllipse(p, circleX, circleY, circleSize, circleSize);
        }

        string text = cb.Items[e.Index].ToString();
        using (SolidBrush tb = new SolidBrush(e.ForeColor))
        using (StringFormat sf = new StringFormat { LineAlignment = StringAlignment.Center })
        {
            Rectangle textRect = new Rectangle(circleX + circleSize + 6, e.Bounds.Y, e.Bounds.Width - (circleSize + 12), e.Bounds.Height);
            e.Graphics.DrawString(text, e.Font, tb, textRect, sf);
        }
        e.DrawFocusRectangle();
    }

    private void OnDrawTimerColorItem(object sender, DrawItemEventArgs e)
    {
        DrawColorItem(e, timerColorCombo, TimerPresetColors, selectedColor, false);
    }

    private void OnDrawQuoteColorItem(object sender, DrawItemEventArgs e)
    {
        DrawColorItem(e, quoteColorCombo, QuotePresetColors, selectedQuoteColor, true);
    }

    private void ChooseCustomColor(bool isQuote)
    {
        Color initCol;
        try { initCol = isQuote ? GetEffectiveQuoteColor() : ColorTranslator.FromHtml(selectedColor); }
        catch { initCol = Color.Red; }
        using (ColorDialog cd = new ColorDialog())
        {
            cd.Color = initCol;
            cd.AllowFullOpen = true;
            cd.FullOpen = true;
            cd.AnyColor = true;
            if (cd.ShowDialog(this) == DialogResult.OK)
            {
                if (isQuote) selectedQuoteColor = ColorTranslator.ToHtml(cd.Color);
                else selectedColor = ColorTranslator.ToHtml(cd.Color);
                UpdateTimerColorPreview();
                UpdateQuoteColorPreview();
                TriggerQuotePreview();
                if (!isQuote) TriggerPreview();
            }
            else
            {
                if (isQuote) UpdateQuoteColorPreview();
                else UpdateTimerColorPreview();
            }
        }
    }

    private void OnTimerColorComboChanged(object sender, EventArgs e)
    {
        if (updatingTimerColorCombo || timerColorCombo == null) return;
        int idx = timerColorCombo.SelectedIndex;
        if (idx >= 0 && idx <= 8)
        {
            selectedColor = TimerPresetColors[idx];
            UpdateTimerColorPreview();
            UpdateQuoteColorPreview();
            TriggerQuotePreview();
            TriggerPreview();
        }
        else if (idx == 9)
        {
            ChooseCustomColor(false);
        }
    }

    private void UpdateTimerColorPreview()
    {
        if (timerColorCombo != null)
        {
            updatingTimerColorCombo = true;
            try
            {
                timerColorCombo.SelectedIndex = GetTimerColorIndex(selectedColor);
            }
            finally
            {
                updatingTimerColorCombo = false;
            }
            timerColorCombo.Invalidate();
        }
    }

    private void OnQuoteColorComboChanged(object sender, EventArgs e)
    {
        if (updatingQuoteColorCombo || quoteColorCombo == null) return;
        int idx = quoteColorCombo.SelectedIndex;
        if (idx >= 0 && idx <= 7)
        {
            selectedQuoteColor = QuotePresetColors[idx];
            UpdateQuoteColorPreview();
            TriggerQuotePreview();
        }
        else if (idx == 8)
        {
            ChooseCustomColor(true);
        }
    }

    private void UpdateQuoteColorPreview()
    {
        if (quoteColorCombo != null)
        {
            updatingQuoteColorCombo = true;
            try
            {
                quoteColorCombo.SelectedIndex = GetQuoteColorIndex(selectedQuoteColor);
            }
            finally
            {
                updatingQuoteColorCombo = false;
            }
            quoteColorCombo.Invalidate();
        }
    }

    private void UpdateDialogQuoteCard()
    {
        if (dialogQuoteList == null || dialogQuoteList.Count == 0) return;
        QuoteItem q = dialogQuoteList[dialogQuoteIndex % dialogQuoteList.Count];
        if (lblCardQuoteText != null)
        {
            lblCardQuoteText.Text = "" + q.Text + "";
        }
        if (lblCardQuoteAuthor != null)
        {
            lblCardQuoteAuthor.Text = !string.IsNullOrEmpty(q.Author) ? "" + q.Author : "";
        }
    }

    public void SyncNextQuote()
    {
        if (dialogQuoteList != null && dialogQuoteList.Count > 0)
        {
            dialogQuoteIndex = (dialogQuoteIndex + 1) % dialogQuoteList.Count;
            UpdateDialogQuoteCard();
        }
    }

    public void SyncQuoteIndex(int idx)
    {
        if (dialogQuoteList != null && dialogQuoteList.Count > 0)
        {
            dialogQuoteIndex = Math.Abs(idx) % dialogQuoteList.Count;
            UpdateDialogQuoteCard();
        }
    }


    private static void OnCardPaint(object sender, PaintEventArgs pe)
    {
        Control card = (Control)sender;
        using (Pen p = new Pen(AppContext.Win11Border, 1f))
        {
            pe.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
        }
    }

    private void OnPreviewChanged(object sender, EventArgs e) { TriggerPreview(); }
    private void OnQuoteFontComboChanged(object sender, EventArgs e) { TriggerQuotePreview(); TriggerPreview(); }
    private void OnSoundStyleCommitted(object sender, EventArgs e)
    {
        if (soundStyle != null && soundStyle.SelectedIndex > 0)
            AppContext.PlaySoundStyle(SoundStyleFromIndex(soundStyle.SelectedIndex));
        TriggerPreview();
    }
    private void OnDailyQuoteCheckChanged(object sender, EventArgs e)
    {
        // Style rows only make sense while the desktop note is on; gray them out
        // when it is off. The screensaver toggle stays independent of this state.
        bool noteOn = dailyQuoteToggle != null && dailyQuoteToggle.Checked;
        if (lblQuoteSize != null) lblQuoteSize.Enabled = noteOn;
        if (quoteFontSize != null) quoteFontSize.Enabled = noteOn;
        if (lblQuoteFont != null) lblQuoteFont.Enabled = noteOn;
        if (quoteFontCombo != null) quoteFontCombo.Enabled = noteOn;
        if (lblQuoteColor != null) lblQuoteColor.Enabled = noteOn;
        if (quoteColorCombo != null) quoteColorCombo.Enabled = noteOn;
        if (lblQuoteLayer != null) lblQuoteLayer.Enabled = noteOn;
        if (quoteLayerCombo != null) quoteLayerCombo.Enabled = noteOn;
        if (lblQuoteQuotes != null) lblQuoteQuotes.Enabled = noteOn;
        if (btnEditQuotes != null) btnEditQuotes.Enabled = noteOn;
        if (btnAiQuotes != null) btnAiQuotes.Enabled = noteOn;
        TriggerQuotePreview();
    }
    private void SyncQuoteStyleEnabled()
    {
        // Initial graying state for the style rows, matched to the toggle value.
        bool noteOn = dailyQuoteToggle != null && dailyQuoteToggle.Checked && activeTab == 1;
        if (lblQuoteSize != null) lblQuoteSize.Enabled = noteOn;
        if (quoteFontSize != null) quoteFontSize.Enabled = noteOn;
        if (lblQuoteFont != null) lblQuoteFont.Enabled = noteOn;
        if (quoteFontCombo != null) quoteFontCombo.Enabled = noteOn;
        if (lblQuoteColor != null) lblQuoteColor.Enabled = noteOn;
        if (quoteColorCombo != null) quoteColorCombo.Enabled = noteOn;
        if (lblQuoteLayer != null) lblQuoteLayer.Enabled = noteOn;
        if (quoteLayerCombo != null) quoteLayerCombo.Enabled = noteOn;
        if (lblQuoteQuotes != null) lblQuoteQuotes.Enabled = noteOn;
        if (btnEditQuotes != null) btnEditQuotes.Enabled = noteOn;
        if (btnAiQuotes != null) btnAiQuotes.Enabled = noteOn;
    }
    private void OnQuoteFontSizeChanged(object sender, EventArgs e) { if (!isSyncingQuoteFontSize) TriggerQuotePreview(); }
    private void OnQuoteFontChanged(object sender, EventArgs e)
    {
        int fi = quoteFontCombo.SelectedIndex;
        selectedQuoteFont = (fi > 0 && fi < quoteFontFamilies.Length) ? quoteFontFamilies[fi] : "";
        TriggerQuotePreview();
    }
    private void OnQuoteLayerChanged(object sender, EventArgs e) { TriggerQuotePreview(); }
    // The dropdown is both the on/off switch and the idle wait, so one handler
    // covers all three transitions: off, on, and moving the wait while on.
    private void OnSaverWaitChanged(object sender, EventArgs e)
    {
        if (quoteSaverWaitCombo == null || Value == null) return;
        int idx = quoteSaverWaitCombo.SelectedIndex;
        if (idx < 0) return;
        bool en = DialogEnglish();
        bool wasOn = Value.QuoteSaverEnabled;
        bool wantOn = idx > 0;
        if (wantOn) Value.SaverMinutes = SaverWaitPresets[idx - 1];
        Value.QuoteSaverEnabled = wantOn;
        Value.Save();
        bool ok = true;
        if (!wantOn) Program.RestoreSaverRegistry(Value);
        else if (wasOn) Program.ApplySaverWait(Value); // only the wait moved: just push it
        else ok = Program.ApplySaverRegistry(Value);
        if (!ok)
        {
            // Could not take over the Windows screensaver: fall back to 关闭 so the
            // row never claims a state Windows is not actually in.
            Value.QuoteSaverEnabled = false;
            Value.Save();
            SelectSaverWaitIndex(0);
            try
            {
                MessageBox.Show(this,
                    en ? ""
                       : "",
                    "", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch { }
        }
        UpdateSaverTooltip(en);
    }

    private void UpdateSaverTooltip(bool en)
    {
        if (toolTip == null || quoteSaverWaitCombo == null) return;
        toolTip.SetToolTip(quoteSaverWaitCombo, en
            ? ""
            : "");
    }

    private static readonly int[] SaverWaitPresets = { 1, 5, 10, 15, 30 };

    // Shared by the build code and the language switch so the two cannot drift.
    private static string SaverRowLabel(bool en)
    {
        return en ? "" : "";
    }

    // Changing the selection without re-entering the change handler — used when a
    // failed registration has to be rolled back to 关闭.
    private void SelectSaverWaitIndex(int idx)
    {
        if (quoteSaverWaitCombo == null) return;
        quoteSaverWaitCombo.SelectedIndexChanged -= OnSaverWaitChanged;
        quoteSaverWaitCombo.SelectedIndex = idx;
        quoteSaverWaitCombo.SelectedIndexChanged += OnSaverWaitChanged;
    }

    // Refill the dropdown in the current language. The change handler is detached
    // while the items are rebuilt: re-labelling must not look like the user picked
    // a new option and re-register the screensaver.
    private void PopulateSaverWaitCombo(bool en)
    {
        if (quoteSaverWaitCombo == null) return;
        int mins = Value != null && Value.SaverMinutes > 0 ? Value.SaverMinutes : 10;
        int sel = 0;
        if (Value != null && Value.QuoteSaverEnabled)
            for (int i = 0; i < SaverWaitPresets.Length; i++)
                if (SaverWaitPresets[i] <= mins) sel = i + 1;
        string[] items = new string[SaverWaitPresets.Length + 1];
        items[0] = en ? "" : "";
        for (int i = 0; i < SaverWaitPresets.Length; i++)
            items[i + 1] = string.Format(en ? "" : "", SaverWaitPresets[i]);
        quoteSaverWaitCombo.SelectedIndexChanged -= OnSaverWaitChanged;
        quoteSaverWaitCombo.BeginUpdate();
        quoteSaverWaitCombo.Items.Clear();
        quoteSaverWaitCombo.Items.AddRange(items);
        quoteSaverWaitCombo.SelectedIndex = sel;
        quoteSaverWaitCombo.EndUpdate();
        quoteSaverWaitCombo.SelectedIndexChanged += OnSaverWaitChanged;
    }

    private void OnPreviewSaverLinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
    {
        // WYSIWYG: the preview IS the real screensaver — full-screen black
        // background, configured font/size, note default position. Moving the
        // mouse or pressing Esc exits, just like the actual saver.
        lnkPreviewSaver.LinkVisited = false;
        CommitQuoteSettings();
        QuoteScreensaverForm preview = new QuoteScreensaverForm();
        preview.Show();
    }
    private void OnEditQuotesClick(object sender, EventArgs e) { AppContext.OpenQuotesFile(); }
    private void OnAiQuotesClick(object sender, EventArgs e)
    {
        using (AiQuoteDialog dlg = new AiQuoteDialog(Value))
        {
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                TriggerQuotePreview();
            }
        }
    }
    private void OnCopyCliGuideClick(object sender, EventArgs e)
    {
        try
        {
            bool en = Value != null && Value.Language == "";
            Clipboard.SetText(CliPromptHelper.GenerateCliAgentPrompt(en));
            btnCopyCliGuide.Text = en ? "" : "";
            if (cliCopyFeedbackTimer == null)
            {
                cliCopyFeedbackTimer = new System.Windows.Forms.Timer();
                cliCopyFeedbackTimer.Interval = 2000;
                cliCopyFeedbackTimer.Tick += delegate
                {
                    cliCopyFeedbackTimer.Stop();
                    if (btnCopyCliGuide != null && !btnCopyCliGuide.IsDisposed)
                        btnCopyCliGuide.Text = Value != null && Value.Language == "" ? "" : "";
                };
            }
            cliCopyFeedbackTimer.Stop();
            cliCopyFeedbackTimer.Start();
        }
        catch (Exception ex)
        {
            ShowSkillFeedback(((Value != null && Value.Language == "") ? "" : "") + ex.Message);
        }
    }

    private void OnInstallCliSkillClick(object sender, EventArgs e)
    {
        Button btn = sender as Button;
        AttemptInstall(btn != null ? btn.Tag as string : null, false);
    }

    private void OnRetryInstallClick(object sender, EventArgs e)
    {
        AttemptInstall(lastInstallAgent, false);
    }

    private void OnOverwriteInstallClick(object sender, EventArgs e)
    {
        AttemptInstall(lastInstallAgent, true);
    }

    private void AttemptInstall(string agentName, bool overwrite)
    {
        try
        {
            string path;
            string result = overwrite ? CliSkillInstaller.Overwrite(agentName, out path) : CliSkillInstaller.Install(agentName, out path);
            lastInstallAgent = agentName;
            if (result == "")
            {
                ShowInstallFailure(agentName, true, null);
            }
            else
            {
                HideInstallFailure();
                lblSkillFeedback.Text = "";
                agentRowsFingerprint = null;
                RefreshSkillInstallUi();
            }
        }
        catch (Exception ex)
        {
            ShowInstallFailure(agentName, false, ex);
        }
    }

    private void ShowInstallFailure(string agentName, bool conflict, Exception ex)
    {
        bool en = Value != null && Value.Language == "";
        lastInstallAgent = agentName;
        lastInstallConflict = conflict;
        lastInstallError = conflict ? null : (ex != null ? ex.Message : "");
        if (lblFailReason == null) return;
        if (conflict)
        {
            lblFailReason.Text = en ? "" : "";
            lblFailHint.Text = en
                ? ""
                : "";
            btnOverwriteInstall.Visible = true;
        }
        else
        {
            string msg = ex != null ? ex.Message : "";
            string lower = msg.ToLowerInvariant();
            string target = string.IsNullOrEmpty(agentName) ? "" : (en ? "" + agentName + "" : "" + agentName + "");
            lblFailReason.Text = (en ? "" : "") + Truncate(msg, 60) + target;
            if (ex is UnauthorizedAccessException)
                lblFailHint.Text = en ? "" : "";
            else if (lower.Contains("") || lower.Contains("") || lower.Contains(""))
                lblFailHint.Text = en ? "" : "";
            else
                lblFailHint.Text = en ? "" : "";
            btnOverwriteInstall.Visible = false;
        }
        panelInstallFail.Visible = true;
        RefreshSkillInstallUi();
        panelSkillCard.ScrollControlIntoView(panelInstallFail);
    }

    private void HideInstallFailure()
    {
        if (panelInstallFail != null) panelInstallFail.Visible = false;
        lastInstallError = null;
        lastInstallConflict = false;
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
        return s.Substring(0, max) + "";
    }

    private void OnCopyFixPromptClick(object sender, EventArgs e)
    {
        try
        {
            bool en = Value != null && Value.Language == "";
            string prompt = CliPromptHelper.GenerateSkillFixPrompt(en, lastInstallAgent, lastInstallConflict, lastInstallError);
            Clipboard.SetText(prompt);
            ShowSkillFeedback(en ? "" : "");
        }
        catch (Exception ex)
        {
            ShowSkillFeedback(((Value != null && Value.Language == "") ? "" : "") + ex.Message);
        }
    }

    private void ShowSkillFeedback(string message)
    {
        if (lblSkillFeedback == null) return;
        lblSkillFeedback.Text = message;
        lblSkillFeedback.ForeColor = Color.FromArgb(180, 83, 9);
        lblSkillFeedback.Visible = true;
        RefreshSkillInstallUi();
        panelSkillCard.ScrollControlIntoView(lblSkillFeedback);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (cliCopyFeedbackTimer != null)
        {
            cliCopyFeedbackTimer.Stop();
            cliCopyFeedbackTimer.Dispose();
            cliCopyFeedbackTimer = null;
        }
        base.OnFormClosed(e);
    }

    private void RefreshSkillInstallUi()
    {
        if (lblSkillGuidance == null || panelSkillCard == null) return;
        bool en = Value != null && Value.Language == "";
        lblSkillGuidance.Text = en
            ? ""
            : "";
        lblSkillGuidance.Font = AppContext.GetUiFont(en ? "" : "", 8f);

        lblSkillMethod1.Text = en ? "" : "";
        lblSkillMethod2.Text = en ? "" : "";
        lblCliGuideHint.Text = en
            ? ""
            : "";
        lblCliGuideHint.Font = AppContext.GetUiFont(en ? "" : "", 8f);
        lblCliGuideHint.ForeColor = AppContext.Win11TextSecondary;
        btnCopyCliGuide.Text = en ? "" : "";

        BuildAgentRows(en, false);

        bool hasAgents = panelAgentRows.Controls.Count > 0;

        // Method 1 block: title then agent rows (or the empty-state text).
        lblSkillMethod1.SetBounds(14, 6, 300, 16);
        int y = lblSkillMethod1.Bottom;
        panelAgentRows.Top = y;
        y = panelAgentRows.Bottom;

        lblAgentEmpty.Visible = !hasAgents;
        if (!hasAgents)
        {
            lblAgentEmpty.Text = en
                ? ""
                : "";
            lblAgentEmpty.Font = AppContext.GetUiFont(en ? "" : "", 8f);
            lblAgentEmpty.SetBounds(14, y, 300, 20);
            y = lblAgentEmpty.Bottom;
        }

        bool showFail = panelInstallFail != null && panelInstallFail.Visible;
        int contentBottom = y;
        if (showFail)
        {
            panelInstallFail.Top = contentBottom + 6;
            lblSkillFeedback.Visible = false;
        }
        else
        {
            lblSkillFeedback.Top = contentBottom + 6;
            lblSkillFeedback.Visible = !string.IsNullOrEmpty(lblSkillFeedback.Text);
        }
        lblSkillFeedback.Font = AppContext.GetUiFont(en ? "" : "", 8f);
        int minHeight = (showFail ? panelInstallFail.Bottom : (lblSkillFeedback.Visible ? lblSkillFeedback.Bottom : contentBottom)) + 4;
        panelSkillCard.AutoScrollMinSize = new Size(0, minHeight);
    }

    private void BuildAgentRows(bool en, bool force)
    {
        // Fingerprint = agent names + per-agent install state + language.
        // Skip the rebuild entirely when nothing changed so the list doesn't flicker.
        List<string> running = AppContext.DetectAgents();
        StringBuilder fps = new StringBuilder(en ? "" : "");
        foreach (string a in running) { fps.Append(a); fps.Append('='); fps.Append(CliSkillInstaller.IsInstalled(a)); fps.Append(';'); }
        string fp = fps.ToString();
        // Rows contribute 3 controls each (dot, name, button); dividers contribute running.Count - 1.
        int expectedControls = running.Count * 3 + Math.Max(0, running.Count - 1);
        if (!force && fp == agentRowsFingerprint && panelAgentRows.Controls.Count == expectedControls)
            return;
        agentRowsFingerprint = fp;

        panelAgentRows.SuspendLayout();
        foreach (Control c in panelAgentRows.Controls) c.Dispose();
        panelAgentRows.Controls.Clear();
        int y = 0;
        int rowHeight = 28;
        int rowIndex = 0;
        foreach (string agent in running)
        {
            bool rowInstalled = CliSkillInstaller.IsInstalled(agent);

            // Solid 8x8 dot instead of a font glyph so it renders identically at any DPI.
            Panel dot = new Panel();
            dot.BackColor = Color.Transparent;
            dot.SetBounds(16, y + (rowHeight - 8) / 2, 8, 8);
            dot.Paint += delegate(object sender, PaintEventArgs pe)
            {
                pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (SolidBrush b = new SolidBrush(Color.FromArgb(22, 163, 74)))
                    pe.Graphics.FillEllipse(b, 0, 0, 7, 7);
            };
            panelAgentRows.Controls.Add(dot);

            Label name = new Label();
            name.Text = agent;
            name.Font = AppContext.GetUiFont(en ? "" : "", 8.5f);
            name.ForeColor = AppContext.Win11TextPrimary;
            name.TextAlign = ContentAlignment.MiddleLeft;
            // Button is right-aligned to the row (16px inner margin), so the
            // agent name takes whatever space is left of it.
            int btnX = panelAgentRows.Width - 82 - 16;
            name.SetBounds(32, y, Math.Max(80, btnX - 32 - 8), rowHeight);
            panelAgentRows.Controls.Add(name);

            Button btn = new Button();
            btn.Font = AppContext.GetUiFont(en ? "" : "", 8f);
            btn.SetBounds(btnX, y + (rowHeight - 24) / 2, 82, 24);
            btn.Tag = agent;
            // Solid fill, no border: reduces line-noise against the row divider.
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.Cursor = rowInstalled ? Cursors.Default : Cursors.Hand;
            if (rowInstalled)
            {
                btn.Text = en ? "" : "";
                btn.Enabled = false;
                btn.BackColor = Color.FromArgb(229, 231, 235);
                btn.ForeColor = Color.FromArgb(107, 114, 128);
            }
            else
            {
                btn.Text = en ? "" : "";
                btn.BackColor = Color.FromArgb(59, 130, 246);
                btn.ForeColor = Color.White;
                btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(37, 99, 235);
                btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(29, 78, 216);
                btn.Click += OnInstallCliSkillClick;
            }
            panelAgentRows.Controls.Add(btn);
            y += rowHeight;

            // Full-width divider between rows (not after the last one).
            if (rowIndex < running.Count - 1)
            {
                Panel divider = new Panel();
                divider.BackColor = Color.FromArgb(229, 231, 235);
                divider.SetBounds(0, y, panelAgentRows.Width, 1);
                panelAgentRows.Controls.Add(divider);
                y += 1;
            }
            rowIndex++;
        }
        panelAgentRows.Height = running.Count > 0 ? y : 0;
        panelAgentRows.ResumeLayout();
    }

    private void OnDesktopCollaborationChanged(object sender, EventArgs e)
    {
        bool enabled = chkDesktopCollaboration.Checked;
        bool en = Value.Language == "";
        if (!enabled && AppContext.Current != null && AppContext.Current.HasActiveDesktopCollaboration)
        {
            DialogResult result = MessageBox.Show(this,
                en ? ""
                   : "",
                en ? "" : "",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (result != DialogResult.Yes)
            {
                chkDesktopCollaboration.Checked = true;
                return;
            }
        }
        Value.DesktopCollaborationEnabled = enabled;
        Value.Save();
        if (!enabled && AppContext.Current != null) AppContext.Current.CancelDesktopCollaboration();
        if (panelCapCard != null) panelCapCard.Enabled = enabled;
        if (panelSkillCard != null) panelSkillCard.Enabled = enabled;
        if (panelSessionCard != null) panelSessionCard.Enabled = enabled;
        if (btnCopyCliGuide != null) btnCopyCliGuide.Enabled = enabled;
        if (lblSkillGuidance != null) lblSkillGuidance.Enabled = enabled;
        RefreshSkillInstallUi();
    }

    private void OnAskDemoClick(object sender, EventArgs e)
    {
        bool en = Value != null && Value.Language == "";
        List<string> agents = AppContext.DetectAgents();
        string agent = (agents != null && agents.Count > 0) ? agents[0] : (en ? "" : "");
        using (AskHumanDialog demo = new AskHumanDialog(
            Value,
            en ? "" : "",
            en ? new string[] { "", "" } : new string[] { "", "" },
            0, null, false, agent))
        {
            demo.ShowDialog(this);
        }
    }
    private void OnProgressDemoClick(object sender, EventArgs e)
    {
        bool en = Value != null && Value.Language == "";
        string msg = en ? "" : "";
        if (AppContext.Current != null)
        {
            AppContext.Current.SetAgentProgress(0.65f, msg, "");
            Timer t = new Timer { Interval = 4000 };
            t.Tick += delegate {
                t.Stop();
                t.Dispose();
                if (AppContext.Current != null)
                {
                    AppContext.Current.ClearAgentProgress("");
                }
            };
            t.Start();
        }
    }
    private void OnNotifyDemoClick(object sender, EventArgs e)
    {
        bool en = Value != null && Value.Language == "";
        NoticeDialog notifyDemo = new NoticeDialog(
            Value, DateTime.Now, "", IntPtr.Zero, null,
            en ? "" : ""
        );
        notifyDemo.Show();
    }
    private static void OnCapabilityCardPaint(object sender, PaintEventArgs pe)
    {
        Control pnl = (Control)sender;
        using (Pen p = new Pen(AppContext.Win11Border, 1f))
            pe.Graphics.DrawRectangle(p, 0, 0, pnl.Width - 1, pnl.Height - 1);
        using (Pen divPen = new Pen(Color.FromArgb(238, 240, 244), 1f))
        {
            pe.Graphics.DrawLine(divPen, 10, 44, pnl.Width - 10, 44);
            pe.Graphics.DrawLine(divPen, 10, 88, pnl.Width - 10, 88);
        }
    }

    private bool DialogEnglish()
    {
        return Value != null && Value.Language == "";
    }

    internal void ApplyInterfaceLanguage(bool toEnglish)
    {
        if (Value != null) Value.Language = toEnglish ? "" : "";
        Font = AppContext.GetUiFont(toEnglish ? "" : "", 9.5f);
        Text = toEnglish ? "" : "";
        if (toEnglish && endMessage.Text == "") endMessage.Text = "";
        else if (!toEnglish && endMessage.Text == "") endMessage.Text = "";

        btnTabTimer.Text = toEnglish ? "" : "";
        btnTabQuote.Text = toEnglish ? "" : "";
        btnTabAgent.Text = toEnglish ? "" : "";
        UpdateTabButtons();

        lblMinutes.Text = toEnglish ? "" : "";
        lblSize.Text = toEnglish ? "" : "";
        lblFont.Text = toEnglish ? "" : "";
        lblColor.Text = toEnglish ? "" : "";
        lblFormat.Text = toEnglish ? "" : "";
        lblEndMsg.Text = toEnglish ? "" : "";
        lblHotkey.Text = toEnglish ? "" : "";
        if (lblDailyGoal != null) lblDailyGoal.Text = toEnglish ? "" : "";
        if (lblSound != null) lblSound.Text = toEnglish ? "" : "";
        if (lblNotice != null) lblNotice.Text = toEnglish ? "" : "";
        PopulateDailyGoalCombo(toEnglish, ReadDailyGoalSelection());
        UpdateStreakLabel();

        if (lblDailyQuote != null)
        {
            lblDailyQuote.Text = toEnglish ? "" : "";
            lblDailyQuote.SetBounds(18, 176, 296, 24);
        }
        lblQuoteSize.Text = toEnglish ? "" : "";
        lblQuoteFont.Text = toEnglish ? "" : "";
        lblQuoteColor.Text = toEnglish ? "" : "";
        lblQuoteLayer.Text = toEnglish ? "" : "";
        int qlSel = quoteLayerCombo != null ? quoteLayerCombo.SelectedIndex : 0;
        if (quoteLayerCombo != null)
        {
            quoteLayerCombo.Items.Clear();
            quoteLayerCombo.Items.AddRange(toEnglish
                ? new object[] { "", "" }
                : new object[] { "", "" });
            quoteLayerCombo.SelectedIndex = qlSel >= 0 ? qlSel : 0;
        }
        if (lnkPreviewSaver != null)
        {
            string saverLabel = SaverRowLabel(toEnglish);
            lnkPreviewSaver.Text = saverLabel;
            int linkStart = saverLabel.IndexOf('[');
            int linkLen = saverLabel.Length - linkStart;
            lnkPreviewSaver.LinkArea = new LinkArea(linkStart < 0 ? 0 : linkStart, linkLen);
            lnkPreviewSaver.SetBounds(18, 380, 148, 24);
        }
        PopulateSaverWaitCombo(toEnglish); // also restores the selected option
        UpdateSaverTooltip(toEnglish);
        lblQuoteQuotes.Text = toEnglish ? "" : "";
        btnEditQuotes.Text = toEnglish ? "" : "";
        btnEditQuotes.Font = AppContext.GetUiFont(toEnglish ? "" : "", 9f);
        if (btnAiQuotes != null)
        {
            btnAiQuotes.Text = toEnglish ? "" : "";
            btnAiQuotes.Font = AppContext.GetUiFont(toEnglish ? "" : "", 9f);
        }
        if (lblQuoteTip != null)
        {
            lblQuoteTip.HighlightWords = toEnglish
                ? new string[] { "", "" }
                : new string[] { "", "" };
            lblQuoteTip.Font = AppContext.GetUiFont(toEnglish ? "" : "", 8f);
            lblQuoteTip.Text = toEnglish
                ? ""
                : "";
        }
        if (toolTip != null)
        {
            if (btnNextQuote != null) toolTip.SetToolTip(btnNextQuote, toEnglish ? "" : "");
            toolTip.SetToolTip(btnEditQuotes, toEnglish ? "" : "");
            if (btnAiQuotes != null) toolTip.SetToolTip(btnAiQuotes, toEnglish ? "" : "");
            if (dailyQuoteToggle != null) toolTip.SetToolTip(dailyQuoteToggle, toEnglish ? "" : "");
        }
        if (lblCardQuoteText != null)
        {
            lblCardQuoteText.Font = AppContext.GetUiFont(toEnglish ? "" : "", 9.5f, FontStyle.Regular);
        }
        if (lblCardQuoteAuthor != null)
        {
            lblCardQuoteAuthor.Font = AppContext.GetUiFont(toEnglish ? "" : "", 8.5f, FontStyle.Italic);
        }

        RefreshSkillInstallUi();
        foreach (Control control in Controls)
        {
            if (control != null && control.Name == "")
            {
                control.Text = toEnglish ? "" : "";
                control.Font = AppContext.GetUiFont(toEnglish ? "" : "", 9f, FontStyle.Bold);
            }
            else if (control != null && control.Name == "")
            {
                control.Text = toEnglish
                    ? ""
                    : "";
                control.Font = AppContext.GetUiFont(toEnglish ? "" : "", 8f);
            }
        }
        if (btnAskDemo != null) btnAskDemo.Text = toEnglish ? "" : "";
        if (btnProgressDemo != null) btnProgressDemo.Text = toEnglish ? "" : "";
        if (btnNotifyDemo != null) btnNotifyDemo.Text = toEnglish ? "" : "";
        if (lblTool1Name != null) { lblTool1Name.Text = toEnglish ? "" : ""; lblTool1Name.Font = AppContext.GetUiFont(toEnglish ? "" : "", 9f, FontStyle.Regular); }
        if (lblTool1Desc != null) { lblTool1Desc.Text = toEnglish ? "" : ""; lblTool1Desc.Font = AppContext.GetUiFont(toEnglish ? "" : "", 8f); }
        if (lblTool2Name != null) { lblTool2Name.Text = toEnglish ? "" : ""; lblTool2Name.Font = AppContext.GetUiFont(toEnglish ? "" : "", 9f, FontStyle.Regular); }
        if (lblTool2Desc != null) { lblTool2Desc.Text = toEnglish ? "" : ""; lblTool2Desc.Font = AppContext.GetUiFont(toEnglish ? "" : "", 8f); }
        if (lblTool3Name != null) { lblTool3Name.Text = toEnglish ? "" : ""; lblTool3Name.Font = AppContext.GetUiFont(toEnglish ? "" : "", 9f, FontStyle.Regular); }
        if (lblTool3Desc != null) { lblTool3Desc.Text = toEnglish ? "" : ""; lblTool3Desc.Font = AppContext.GetUiFont(toEnglish ? "" : "", 8f); }
        if (toolTip != null)
        {
            string capabilityTip1 = toEnglish ? "" : "";
            string capabilityTip2 = toEnglish ? "" : "";
            string capabilityTip3 = toEnglish ? "" : "";
            if (lblTool1Name != null) toolTip.SetToolTip(lblTool1Name, capabilityTip1);
            if (btnAskDemo != null) toolTip.SetToolTip(btnAskDemo, capabilityTip1);
            if (lblTool2Name != null) toolTip.SetToolTip(lblTool2Name, capabilityTip2);
            if (btnProgressDemo != null) toolTip.SetToolTip(btnProgressDemo, capabilityTip2);
            if (lblTool3Name != null) toolTip.SetToolTip(lblTool3Name, capabilityTip3);
            if (btnNotifyDemo != null) toolTip.SetToolTip(btnNotifyDemo, capabilityTip3);
        }
        if (panelQuoteCard != null) panelQuoteCard.Invalidate();

        UpdateTabButtons();
        updatingTimerColorCombo = true;
        try
        {
            int tcSel = timerColorCombo != null ? timerColorCombo.SelectedIndex : 0;
            PopulateTimerColorCombo(toEnglish);
            if (timerColorCombo != null) timerColorCombo.SelectedIndex = tcSel >= 0 ? tcSel : 0;
        }
        finally
        {
            updatingTimerColorCombo = false;
        }
        UpdateTimerColorPreview();

        updatingQuoteColorCombo = true;
        try
        {
            int qcSel = quoteColorCombo != null ? quoteColorCombo.SelectedIndex : 0;
            PopulateQuoteColorCombo(toEnglish);
            if (quoteColorCombo != null) quoteColorCombo.SelectedIndex = qcSel >= 0 ? qcSel : 0;
        }
        finally
        {
            updatingQuoteColorCombo = false;
        }
        UpdateQuoteColorPreview();
        TriggerQuotePreview();

        int sel = format.SelectedIndex;
        format.Items.Clear();
        format.Items.AddRange(toEnglish
            ? new object[] { "", "", "" }
            : new object[] { "", "", "" });
        format.SelectedIndex = sel >= 0 ? sel : 0;
        int fontSel = fontCombo.SelectedIndex;
        fontCombo.Items.Clear();
        fontCombo.Items.AddRange(toEnglish
            ? new object[] { "", "", "", "", "" }
            : new object[] { "", "", "", "", "" });
        fontCombo.SelectedIndex = fontSel >= 0 ? fontSel : 0;

        int qfSel = quoteFontCombo.SelectedIndex;
        quoteFontCombo.Items.Clear();
        quoteFontCombo.Items.AddRange(toEnglish
            ? new object[] { "", "", "", "", "", "", "", "", "", "" }
            : new object[] { "", "", "", "", "", "", "", "", "", "" });
        quoteFontCombo.SelectedIndex = qfSel >= 0 ? qfSel : 0;

        PopulateSoundStyleCombo(toEnglish);
        PopulateNoticeStyleCombo(toEnglish);
        if (hotkeyActive)
        {
            hotkeyBox.Text = AppContext.HotkeyText(hotkeyModifiers, hotkeyKey);
            hotkeyBox.ForeColor = AppContext.Win11TextPrimary;
        }
        else if (candidateMods == 0 && !((candidateKey >= (int)Keys.F1 && candidateKey <= (int)Keys.F12) || candidateKey == (int)Keys.Pause || candidateKey == (int)Keys.Scroll))
        {
            hotkeyBox.Text = toEnglish ? "" : "";
            hotkeyBox.ForeColor = Color.FromArgb(202, 80, 16);
        }
        else
        {
            hotkeyBox.Text = AppContext.HotkeyText(candidateMods, candidateKey) + (toEnglish ? "" : "");
            hotkeyBox.ForeColor = Color.FromArgb(202, 80, 16);
        }

        UpdateInputPadding(minutes);
        UpdateInputPadding(size);
        UpdateInputPadding(quoteFontSize);
        UpdateInputPadding(endMessage);
        UpdateInputPadding(hotkeyBox);
        UpdateInputPadding(dailyGoal);
        ApplySettingsInputSize(minutes);
        ApplySettingsInputSize(size);
        ApplySettingsInputSize(fontCombo);
        ApplySettingsInputSize(timerColorCombo);
        ApplySettingsInputSize(format);
        ApplySettingsInputSize(endMessage);
        ApplySettingsInputSize(soundStyle);
        ApplySettingsInputSize(noticeStyle);
        ApplySettingsInputSize(hotkeyBox);
        ApplySettingsInputSize(dailyGoal);
        ApplySettingsInputSize(quoteFontSize);
        ApplySettingsInputSize(quoteFontCombo);
        ApplySettingsInputSize(quoteColorCombo);
        ApplySettingsInputSize(quoteLayerCombo);
        ApplySettingsInputSize(quoteSaverWaitCombo);
        TriggerPreview();
    }

    public Settings ApplyCurrentSettings()
    {
        Value.Minutes = minutes != null ? (int)minutes.Value : Value.Minutes;
        Value.FontSize = size != null ? (int)size.Value : Value.FontSize;
        Value.Color = !string.IsNullOrEmpty(selectedColor) ? selectedColor : Value.Color;
        Value.FontFamily = (fontCombo != null && fontCombo.SelectedIndex >= 0 && fontCombo.SelectedIndex < fontFamilies.Length)
            ? fontFamilies[fontCombo.SelectedIndex]
            : (Value.FontFamily ?? "");
        Value.HotkeyModifiers = hotkeyModifiers;
        Value.HotkeyKey = hotkeyKey;
        Value.Format = (format != null && format.SelectedIndex == 1) ? "" : ((format != null && format.SelectedIndex == 2) ? "" : "");
        Value.EndSound = soundStyle != null && soundStyle.SelectedIndex > 0;
        if (soundStyle != null && soundStyle.SelectedIndex > 0) Value.EndSoundStyle = SoundStyleFromIndex(soundStyle.SelectedIndex);
        Value.EndNotice = noticeStyle != null && noticeStyle.SelectedIndex > 0;
        if (noticeStyle != null && noticeStyle.SelectedIndex == 2) Value.NoticeStyle = "";
        else if (noticeStyle != null && noticeStyle.SelectedIndex == 1) Value.NoticeStyle = "";
        Value.EndMessage = endMessage != null ? endMessage.Text.Trim() : Value.EndMessage;
        Value.DailyQuote = dailyQuoteToggle != null ? dailyQuoteToggle.Checked : Value.DailyQuote;
        Value.QuoteColor = selectedQuoteColor ?? "";
        Value.QuoteFont = selectedQuoteFont ?? "";
        if (quoteFontSize != null) Value.QuoteFontSize = (float)quoteFontSize.Value;
        Value.QuotePinTopMost = quoteLayerCombo != null && quoteLayerCombo.SelectedIndex == 1;
        Value.QuoteSaverEnabled = quoteSaverWaitCombo != null && quoteSaverWaitCombo.SelectedIndex > 0;
        Value.DailyGoalMinutes = ReadDailyGoalSelection();
        return Value;
    }

    private static readonly int[] DailyGoalPresets = { 60, 120, 180, 240, 360, 480 };

    private void OnDailyGoalChanged(object sender, EventArgs e)
    {
        TriggerPreview();
        UpdateStreakLabel();
    }

    private void PopulateDailyGoalCombo(bool en, int minutes)
    {
        if (dailyGoal == null) return;
        int sel = DailyGoalComboIndex(minutes);
        dailyGoal.SelectionChangeCommitted -= OnDailyGoalChanged;
        dailyGoal.BeginUpdate();
        dailyGoal.Items.Clear();
        dailyGoal.Items.Add(en ? "" : "");
        for (int i = 0; i < DailyGoalPresets.Length; i++)
        {
            int hours = DailyGoalPresets[i] / 60;
            dailyGoal.Items.Add(en
                ? (hours == 1 ? "" : hours + "")
                : (hours + ""));
        }
        dailyGoal.SelectedIndex = sel;
        dailyGoal.EndUpdate();
        dailyGoal.SelectionChangeCommitted += OnDailyGoalChanged;
    }

    // 正数靠到最近的一档时长，不会靠到「关闭」。
    private static int SnapDailyGoalMinutes(int minutes)
    {
        if (minutes <= 0) return 0;
        int best = DailyGoalPresets[0];
        int bestDist = Math.Abs(minutes - best);
        for (int i = 1; i < DailyGoalPresets.Length; i++)
        {
            int d = Math.Abs(minutes - DailyGoalPresets[i]);
            if (d < bestDist || (d == bestDist && DailyGoalPresets[i] > best))
            {
                best = DailyGoalPresets[i];
                bestDist = d;
            }
        }
        return best;
    }

    private static int DailyGoalComboIndex(int minutes)
    {
        int snapped = SnapDailyGoalMinutes(minutes);
        if (snapped <= 0) return 0;
        for (int i = 0; i < DailyGoalPresets.Length; i++)
            if (DailyGoalPresets[i] == snapped) return i + 1;
        return 0;
    }

    // 读取「每日专注目标」：0 = 关闭，其余为分钟数。
    private int ReadDailyGoalSelection()
    {
        if (dailyGoal == null) return Value != null ? Value.DailyGoalMinutes : 0;
        int idx = dailyGoal.SelectedIndex;
        if (idx <= 0 || idx > DailyGoalPresets.Length) return 0;
        return DailyGoalPresets[idx - 1];
    }

    private static int SoundStyleIndex(bool enabled, string style)
    {
        if (!enabled) return 0;
        if (style == "") return 2;
        if (style == "" || style == "" || style == "") return 3;
        if (style == "") return 4;
        if (style == "") return 5;
        return 1;
    }

    private static string SoundStyleFromIndex(int index)
    {
        if (index == 2) return "";
        if (index == 3) return "";
        if (index == 4) return "";
        if (index == 5) return "";
        return "";
    }

    private static int NoticeStyleIndex(bool enabled, string style)
    {
        if (!enabled) return 0;
        return style == "" ? 2 : 1;
    }

    private void PopulateSoundStyleCombo(bool en)
    {
        if (soundStyle == null) return;
        int sel = soundStyle.Items.Count > 0 ? soundStyle.SelectedIndex : -1;
        soundStyle.BeginUpdate();
        soundStyle.Items.Clear();
        soundStyle.Items.AddRange(en
            ? new object[] { "", "", "", "", "", "" }
            : new object[] { "", "", "", "", "", "" });
        if (sel >= 0 && sel < soundStyle.Items.Count) soundStyle.SelectedIndex = sel;
        soundStyle.EndUpdate();
    }

    private void PopulateNoticeStyleCombo(bool en)
    {
        if (noticeStyle == null) return;
        int sel = noticeStyle.Items.Count > 0 ? noticeStyle.SelectedIndex : -1;
        noticeStyle.BeginUpdate();
        noticeStyle.Items.Clear();
        noticeStyle.Items.AddRange(en
            ? new object[] { "", "", "" }
            : new object[] { "", "", "" });
        if (sel >= 0 && sel < noticeStyle.Items.Count) noticeStyle.SelectedIndex = sel;
        noticeStyle.EndUpdate();
    }

    // 设置页里的「连续 N 天」提示：写在目标下拉框的悬浮提示里，标签保持单行。
    private void UpdateStreakLabel()
    {
        if (lblDailyGoal == null) return;
        bool en = DialogEnglish();
        lblDailyGoal.Text = en ? "" : "";
        if (dailyGoal == null) return;
        int goal = ReadDailyGoalSelection();
        string tip;
        if (goal <= 0)
        {
            tip = en ? "" : "";
        }
        else
        {
            tip = en ? "" : "";
            try
            {
                string dir = Path.GetDirectoryName(Settings.FilePath);
                int streak = AppContext.ComputeStreak(delegate(string dk) { return AppContext.GetDayFocusSecs(dir, dk); }, goal, DateTime.Today.ToString(""));
                if (streak > 0) tip = en ? "" + streak + "" : "" + streak + "";
            }
            catch { }
        }
        if (toolTip == null) toolTip = new ToolTip();
        toolTip.SetToolTip(dailyGoal, tip);
    }

    private void CommitQuoteSettings()
    {
        if (Value == null) return;
        Value.DailyQuote = dailyQuoteToggle != null ? dailyQuoteToggle.Checked : Value.DailyQuote;
        Value.QuoteColor = selectedQuoteColor ?? "";
        Value.QuoteFont = selectedQuoteFont ?? "";
        if (quoteFontSize != null) Value.QuoteFontSize = (float)quoteFontSize.Value;
        Value.QuotePinTopMost = quoteLayerCombo != null && quoteLayerCombo.SelectedIndex == 1;
        Value.QuoteSaverEnabled = quoteSaverWaitCombo != null && quoteSaverWaitCombo.SelectedIndex > 0;
        Value.Save();
    }

    private void TriggerPreview()
    {
        if (PreviewRequested != null && minutes != null && size != null && fontCombo != null && format != null && timerColorCombo != null)
        {
            ApplyCurrentSettings();
            Value.Save();
            try
            {
                PreviewRequested();
            }
            finally
            {
                // 预览绘制失败时也要提交，否则开机自启只写进了 ini，注册表启动项不会更新。
                if (SettingsCommitted != null) SettingsCommitted();
            }
        }
    }

    private void TriggerQuotePreview()
    {
        if (dailyQuoteToggle != null)
        {
            CommitQuoteSettings();
            if (QuotePreviewRequested != null)
            {
                QuotePreviewRequested(activeTab == 1 && dailyQuoteToggle.Checked);
            }
        }
    }

    private const int SettingsInputHeight = 28;

    private void ApplySettingsInputSize(Control c)
    {
        if (c == null) return;
        TextBox tb = c as TextBox;
        if (tb != null)
        {
            tb.AutoSize = false;
            tb.MinimumSize = new Size(0, SettingsInputHeight);
            tb.Height = SettingsInputHeight;
            return;
        }
        ComboBox cb = c as ComboBox;
        if (cb != null)
        {
            cb.IntegralHeight = false;
            cb.FlatStyle = FlatStyle.Standard;
            cb.DrawMode = DrawMode.OwnerDrawFixed;
            cb.ItemHeight = 22;
            cb.Height = SettingsInputHeight;
            return;
        }
        NumericUpDown nud = c as NumericUpDown;
        if (nud != null)
        {
            // 默认会按字体收成 24，和 28 的下拉框对不齐。
            nud.AutoSize = false;
            nud.MinimumSize = new Size(0, SettingsInputHeight);
            nud.Height = SettingsInputHeight;
            return;
        }
        c.Height = SettingsInputHeight;
    }

    private Label AddLabel(string t, int x, int y) { Label l = new Label(); l.Text = t; l.ForeColor = AppContext.Win11TextPrimary; l.SetBounds(x, y, 148, 24); l.TextAlign = ContentAlignment.MiddleLeft; Controls.Add(l); return l; }
    private NumericUpDown AddNumber(int v, int min, int max, int x, int y) { NumericUpDown n = new SettingsNumberBox(); n.Minimum = min; n.Maximum = max; n.Value = Math.Min(max, Math.Max(min, v)); n.SetBounds(x, y, 188, SettingsInputHeight); ApplyInputPadding(n); ApplySettingsInputSize(n); Controls.Add(n); return n; }
    private ComboBox AddCombo(int y)
    {
        ComboBox c = new ComboBox();
        c.DropDownStyle = ComboBoxStyle.DropDownList;
        c.DrawMode = DrawMode.OwnerDrawFixed;
        c.FlatStyle = FlatStyle.Standard;
        c.IntegralHeight = false;
        c.ItemHeight = 22;
        c.SetBounds(172, y, 188, SettingsInputHeight);
        Controls.Add(c);
        ApplySettingsInputSize(c);
        return c;
    }

    // 倒计时页 10 行：行距 40，末行底边 446，与 AI 协作页 session 卡片同一底边距（24）。
    private static int TimerRowY(int index) { return 58 + index * 40; }
    private static int TimerLabelY(int index) { return TimerRowY(index) + 4; }

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
            hotkeyBox.Text = DialogEnglish() ? "" : "";
            hotkeyBox.ForeColor = Color.FromArgb(202, 80, 16);
            return;
        }
        string text = AppContext.HotkeyText(mods, (int)key);
        if (key == Keys.Space && (mods & 3) != 0)
        {
            hotkeyBox.Text = text + (DialogEnglish() ? "" : "");
            hotkeyBox.ForeColor = Color.FromArgb(202, 80, 16);
            return;
        }
        bool isCurrent = hotkeyActive && (mods == Value.HotkeyModifiers && (int)key == Value.HotkeyKey);
        bool available = isCurrent || AppContext.ProbeHotkey(mods, (int)key);
        if (!available)
        {
            hotkeyBox.Text = text + (DialogEnglish() ? "" : "");
            hotkeyBox.ForeColor = Color.FromArgb(202, 80, 16);
        }
        else
        {
            hotkeyModifiers = mods; hotkeyKey = (int)key;
            hotkeyBox.Text = text;
            hotkeyBox.ForeColor = AppContext.Win11TextPrimary;
            TriggerPreview();
        }
    }
    private void OnDrawFontItem(object sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= fontCombo.Items.Count) return;
        string family = e.Index < fontFamilies.Length ? fontFamilies[e.Index] : "";
        Font f = null;
        try { f = new Font(family, e.Font.Size, FontStyle.Regular, e.Font.Unit); } catch { }
        DrawComboItem(e, fontCombo.Items[e.Index].ToString(), f ?? e.Font);
        if (f != null) f.Dispose();
    }
    private void OnDrawQuoteFontItem(object sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || quoteFontCombo == null || e.Index >= quoteFontCombo.Items.Count) return;
        string family = (e.Index > 0 && e.Index < quoteFontFamilies.Length) ? quoteFontFamilies[e.Index] : "";
        Font f = null;
        try { f = new Font(family, e.Font.Size, FontStyle.Regular, e.Font.Unit); } catch { }
        DrawComboItem(e, quoteFontCombo.Items[e.Index].ToString(), f ?? e.Font);
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
    public void SetDailyQuoteChecked(bool chk)
    {
        if (dailyQuoteToggle != null && dailyQuoteToggle.Checked != chk)
        {
            dailyQuoteToggle.Checked = chk;
        }
    }

    public void SetDesktopQuoteChecked(bool chk)
    {
        SetDailyQuoteChecked(chk);
    }

    public void SyncQuotePin(bool pinned)
    {
        if (quoteLayerCombo != null)
        {
            Value.QuotePinTopMost = pinned;
            Value.Save();
            int target = pinned ? 1 : 0;
            if (quoteLayerCombo.SelectedIndex != target)
            {
                quoteLayerCombo.SelectedIndex = target;
            }
        }
    }

    public void SyncQuoteFontSize(float sz)
    {
        if (quoteFontSize != null && sz >= 8.0f && sz <= 24.0f)
        {
            Value.QuoteFontSize = sz;
            decimal dec = (decimal)sz;
            if (quoteFontSize.Value != dec)
            {
                isSyncingQuoteFontSize = true;
                try
                {
                    quoteFontSize.Value = dec;
                    Value.QuoteFontSize = sz;
                }
                finally
                {
                    isSyncingQuoteFontSize = false;
                }
            }
            Value.Save();
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
    }
}

/// <summary>
/// Primary button that shows the chosen rest length and opens the preset
/// menu from a drawn arrow zone on its right edge. The arrow used to be part
/// of the button text while the clickable zone was the last 24 pixels, so
/// the visible arrow sat outside it and the menu was impossible to hit.
/// </summary>
internal sealed class RestLengthButton : Button
{
    public const int ArrowZoneWidth = 36;

    private bool arrowHot;

    public RestLengthButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        // Paints itself: UserPaint plus an OnPaint that never calls the base.
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    public int ArrowZoneLeft { get { return Width - ArrowZoneWidth; } }

    public bool InArrowZone(int x) { return x >= ArrowZoneLeft; }

    protected override void OnMouseMove(MouseEventArgs mevent)
    {
        base.OnMouseMove(mevent);
        bool hot = InArrowZone(mevent.X);
        if (hot != arrowHot) { arrowHot = hot; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (arrowHot) { arrowHot = false; Invalidate(); }
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        Graphics g = pevent.Graphics;
        g.Clear(BackColor);
        if (arrowHot)
        {
            using (SolidBrush hot = new SolidBrush(ControlPaint.Dark(BackColor, 0.18f)))
                g.FillRectangle(hot, ArrowZoneLeft, 0, ArrowZoneWidth, Height);
        }
        // The text keeps the centred look it had before the arrow was split
        // off: it is centred in the space left of the arrow zone.
        TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, ArrowZoneLeft, Height), ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        using (Pen separator = new Pen(Color.FromArgb(90, 255, 255, 255)))
            g.DrawLine(separator, ArrowZoneLeft - 0.5f, 8, ArrowZoneLeft - 0.5f, Height - 8);
        // A filled triangle rather than a glyph: crisp at any DPI, no tofu box.
        float cx = ArrowZoneLeft + ArrowZoneWidth / 2f;
        float cy = Height / 2f;
        using (SolidBrush arrow = new SolidBrush(arrowHot ? Color.White : Color.FromArgb(210, 255, 255, 255)))
            g.FillPolygon(arrow, new PointF[] { new PointF(cx - 5f, cy - 3f), new PointF(cx + 5f, cy - 3f), new PointF(cx, cy + 3f) });
    }
}

