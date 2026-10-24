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

internal sealed class WelcomeDialog : Form
{
    public bool StartRequested { get; private set; }

    public WelcomeDialog(Settings s) : this(s, true, false) { }

    public WelcomeDialog(Settings s, bool hotkeyActive, bool fallbackUsed = false)
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
        try { Icon = AppContext.CreateIcon(); } catch { }
        ClientSize = new Size(380, 208);

        Label lblTitle = new Label();
        lblTitle.Text = en ? "Welcome to iClock" : "欢迎使用 iClock";
        lblTitle.Font = AppContext.GetUiFont(s.Language, 13.5f, FontStyle.Bold);
        lblTitle.ForeColor = AppContext.Win11Accent;
        lblTitle.TextAlign = ContentAlignment.MiddleCenter;
        lblTitle.SetBounds(0, 16, ClientSize.Width, 26);
        Controls.Add(lblTitle);

        Label lblPrompt = new Label();
        if (!hotkeyActive)
        {
            lblPrompt.Text = en ? "Candidate hotkeys occupied. Click Start below:" : "候选快捷键均被占用，可点击下方按钮开始：";
            lblPrompt.ForeColor = Color.FromArgb(202, 80, 16);
        }
        else if (fallbackUsed)
        {
            lblPrompt.Text = en ? "Default hotkey occupied. Switched to backup:" : "默认快捷键已被占用，已自动启用备用键：";
            lblPrompt.ForeColor = Color.FromArgb(202, 80, 16);
        }
        else
        {
            lblPrompt.Text = en ? "Press shortcut to start or pause:" : "按下快捷键启动或暂停倒计时：";
            lblPrompt.ForeColor = AppContext.Win11TextPrimary;
        }
        lblPrompt.Font = AppContext.GetUiFont(s.Language, 9.5f, FontStyle.Regular);
        lblPrompt.TextAlign = ContentAlignment.MiddleCenter;
        lblPrompt.SetBounds(0, 52, ClientSize.Width, 20);
        Controls.Add(lblPrompt);

        string hotkey = hotkeyActive ? AppContext.HotkeyText(s.HotkeyModifiers, s.HotkeyKey) : (en ? "(No Hotkey)" : "(快捷键未生效)");
        Label badge = new Label();
        badge.Text = hotkey;
        badge.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
        badge.ForeColor = hotkeyActive ? (fallbackUsed ? Color.FromArgb(202, 80, 16) : AppContext.Win11Accent) : Color.FromArgb(202, 80, 16);
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

        int iconSize = Math.Max(16, SystemInformation.SmallIconSize.Width);
        int gap = 4;
        int totalTrayW = lblT1.PreferredWidth + gap + iconSize + gap + lblT2.PreferredWidth;
        int trayStartX = (ClientSize.Width - totalTrayW) / 2;

        lblT1.Location = new Point(trayStartX, (trayPanel.Height - lblT1.PreferredHeight) / 2);
        trayPanel.Controls.Add(lblT1);

        PictureBox trayIcon = new PictureBox();
        trayIcon.SetBounds(trayStartX + lblT1.PreferredWidth + gap, (trayPanel.Height - iconSize) / 2, iconSize, iconSize);
        trayIcon.SizeMode = PictureBoxSizeMode.Zoom;
        try
        {
            Icon appIcon = AppContext.CreateIcon();
            if (appIcon != null)
            {
                using (Icon targetIco = new Icon(appIcon, iconSize, iconSize))
                {
                    trayIcon.Image = targetIco.ToBitmap();
                }
            }
        }
        catch { }
        trayPanel.Controls.Add(trayIcon);

        lblT2.Location = new Point(trayStartX + lblT1.PreferredWidth + gap + iconSize + gap, (trayPanel.Height - lblT2.PreferredHeight) / 2);
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

        AppContext.CenterFormOnScreen(this);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        AppContext.CenterFormOnScreen(this);
    }
}

internal class AboutDialog : Form
{
    private AppContext app;
    private string language;
    private Button btnCheck;
    // Auto-update state: the version found, its exe download url, the local
    // destination, and whether the download has finished.
    private string updateNewVersion;
    private string updateExeUrl;
    private string updateDestExe;
    private bool updateDownloaded;

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
        lblDesc.UseMnemonic = false;
        lblDesc.Text = en ? "Minimalist & distraction-free desktop focus companion" : "极简通透、轻量无打扰的桌面专注与效率伴侣";
        lblDesc.Font = AppContext.GetUiFont(language, 9f, FontStyle.Regular);
        lblDesc.ForeColor = AppContext.Win11TextSecondary;
        lblDesc.SetBounds(20, 44, 310, 20);
        Controls.Add(lblDesc);

        Label lblVersion = new Label();
        lblVersion.Text = (en ? "Version: v" : "当前版本: v") + AppContext.CURRENT_VERSION;
        lblVersion.Font = AppContext.GetUiFont(language, 9.5f, FontStyle.Regular);
        lblVersion.ForeColor = AppContext.Win11TextPrimary;
        int verW = TextRenderer.MeasureText(lblVersion.Text, lblVersion.Font).Width;
        lblVersion.SetBounds(20, 72, verW, 24);
        lblVersion.TextAlign = ContentAlignment.MiddleLeft;
        Controls.Add(lblVersion);

        LinkLabel link = new LinkLabel();
        link.Text = "(GitHub ↗)";
        link.Font = AppContext.GetUiFont(language, 9f, FontStyle.Regular);
        link.LinkColor = AppContext.Win11Accent;
        link.LinkBehavior = LinkBehavior.HoverUnderline;
        int linkW = TextRenderer.MeasureText(link.Text, link.Font).Width;
        link.SetBounds(lblVersion.Right + 2, 74, linkW, 20);
        link.LinkClicked += delegate { try { Process.Start("https://github.com/francodeyvison183-collab/iClock"); } catch { } };
        Controls.Add(link);

        btnCheck = new Button();
        btnCheck.Text = en ? "Check Updates" : "检查更新";
        btnCheck.Font = AppContext.GetUiFont(language, 9f, FontStyle.Regular);
        btnCheck.SetBounds(220, 70, 110, 26);
        AppContext.StyleSecondaryButton(btnCheck);
        btnCheck.Click += OnCheckUpdates;
        Controls.Add(btnCheck);

        Label line = new Label();
        line.BackColor = AppContext.Win11Border;
        line.BorderStyle = BorderStyle.None;
        line.SetBounds(20, 106, 310, 1);
        Controls.Add(line);

        Label lblSponsor = new Label();
        lblSponsor.Text = en ? "If iClock helps, feel free to support via WeChat Pay!" : "如果 iClock 对你有帮助，欢迎微信扫码赞赏支持！";
        lblSponsor.Font = AppContext.GetUiFont(language, 9f, FontStyle.Regular);
        lblSponsor.ForeColor = AppContext.Win11TextPrimary;
        lblSponsor.TextAlign = ContentAlignment.MiddleCenter;
        lblSponsor.SetBounds(15, 118, 320, 24);
        Controls.Add(lblSponsor);

        HqPictureBox pic = new HqPictureBox();
        pic.SetBounds(25, 146, 300, 300);
        pic.SizeMode = PictureBoxSizeMode.Zoom;
        pic.BorderStyle = BorderStyle.None;
        pic.Image = LoadSponsorImage();
        Controls.Add(pic);

        Label lblAuthor = new Label();
        lblAuthor.UseMnemonic = false;
        lblAuthor.Text = en ? "Designed & Built by gujiangwen" : "作者：gujiangwen";
        lblAuthor.Font = AppContext.GetUiFont(language, 8.5f, FontStyle.Regular);
        lblAuthor.ForeColor = AppContext.Win11TextSecondary;
        lblAuthor.TextAlign = ContentAlignment.MiddleCenter;
        lblAuthor.SetBounds(20, 454, 310, 18);
        Controls.Add(lblAuthor);

        LinkLabel linkEmail = new LinkLabel();
        linkEmail.UseMnemonic = false;
        string emailPrefix = en ? "Feedback: " : "反馈交流：";
        string emailAddress = "francodeyvison183@gmail.com";
        linkEmail.Text = emailPrefix + emailAddress;
        linkEmail.Font = AppContext.GetUiFont(language, 8.5f, FontStyle.Regular);
        linkEmail.LinkColor = AppContext.Win11Accent;
        linkEmail.ActiveLinkColor = Color.FromArgb(0, 90, 158);
        linkEmail.VisitedLinkColor = AppContext.Win11Accent;
        linkEmail.LinkBehavior = LinkBehavior.HoverUnderline;
        linkEmail.LinkArea = new LinkArea(emailPrefix.Length, emailAddress.Length);
        linkEmail.TextAlign = ContentAlignment.MiddleCenter;
        linkEmail.SetBounds(20, 476, 310, 18);

        ToolTip tip = new ToolTip();
        tip.SetToolTip(linkEmail, en ? "Click to send email or copy address" : "点击发送邮件或复制邮箱地址");
        linkEmail.LinkClicked += delegate
        {
            try { Clipboard.SetText(emailAddress); } catch { }
            try { tip.Show(en ? "Email copied to clipboard!" : "邮箱地址已复制到剪贴板！", linkEmail, 150, 20, 2000); } catch { }
            try { Process.Start("mailto:" + emailAddress); } catch { }
        };
        Controls.Add(linkEmail);

        KeyPreview = true;
        KeyDown += delegate(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                Close();
            }
        };
        AppContext.CenterFormOnScreen(this);
    }

    private void OnCheckUpdates(object sender, EventArgs e)
    {
        bool en = language == "en";

        // If a download has already finished, the button offers to apply it.
        if (updateDownloaded)
        {
            ApplyUpdateAndRestart();
            return;
        }

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

                    string ver = latest.TrimStart('v', 'V');
                    if (AppContext.IsNewer(ver, AppContext.CURRENT_VERSION))
                    {
                        updateNewVersion = ver;
                        updateExeUrl = AppContext.BuildUpdateExeUrl(ver);
                        btnCheck.Text = en ? ("Download v" + ver) : ("下载 v" + ver);
                        btnCheck.Click -= OnCheckUpdates;
                        btnCheck.Click += OnDownloadUpdate;
                        string targetUrl = string.IsNullOrEmpty(url) ? "https://github.com/francodeyvison183-collab/iClock/releases/latest" : url;
                        if (app != null) app.NotifyUpdateFound(latest, targetUrl);
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

    private void OnDownloadUpdate(object sender, EventArgs e)
    {
        bool en = language == "en";
        btnCheck.Enabled = false;
        btnCheck.Text = en ? "Downloading..." : "下载中...";

        string dir = Path.Combine(Path.GetTempPath(), "iClock-update");
        try { Directory.CreateDirectory(dir); } catch { }
        updateDestExe = Path.Combine(dir, "iClock-new.exe");

        try
        {
            using (System.Net.WebClient wc = new System.Net.WebClient())
            {
                wc.DownloadProgressChanged += delegate(object s, System.Net.DownloadProgressChangedEventArgs pe)
                {
                    try
                    {
                        if (IsDisposed || !IsHandleCreated) return;
                        BeginInvoke(new MethodInvoker(delegate
                        {
                            btnCheck.Text = en ? ("Downloading… " + pe.ProgressPercentage + "%") : ("下载中… " + pe.ProgressPercentage + "%");
                        }));
                    }
                    catch { }
                };
                wc.DownloadFileCompleted += delegate(object s, System.ComponentModel.AsyncCompletedEventArgs pe)
                {
                    try
                    {
                        if (IsDisposed || !IsHandleCreated) return;
                        BeginInvoke(new MethodInvoker(delegate
                        {
                            if (pe.Error != null || pe.Cancelled)
                            {
                                btnCheck.Enabled = true;
                                btnCheck.Text = en ? "Check Updates" : "检查更新";
                                btnCheck.Click -= OnDownloadUpdate;
                                btnCheck.Click += OnCheckUpdates;
                                updateExeUrl = null;
                                string errMsg = pe.Error == null ? "" : pe.Error.Message;
                                MessageBox.Show(this, en ? "Download failed: " + errMsg : "下载失败：" + errMsg, "iClock", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                return;
                            }
                            updateDownloaded = true;
                            btnCheck.Text = en ? "Restart to Update" : "重启应用更新";
                            btnClickSwitchBack();
                            // Persist the pending update so a restart applies it.
                            AppContext.MarkPendingUpdate(updateNewVersion, updateDestExe);
                            string msg = en ? "Download complete. Click to restart and apply the update." : "下载完成，点击重启应用以更新。";
                            try { app.ShowTrayNotification(en ? "iClock Update Ready" : "iClock 更新已就绪", msg, 8000); } catch { }
                            MessageBox.Show(this, msg, "iClock", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }));
                    }
                    catch { }
                };
                wc.DownloadFileAsync(new Uri(updateExeUrl), updateDestExe);
            }
        }
        catch
        {
            btnCheck.Enabled = true;
            btnCheck.Text = en ? "Check Updates" : "检查更新";
            btnCheck.Click -= OnDownloadUpdate;
            btnCheck.Click += OnCheckUpdates;
            updateExeUrl = null;
            MessageBox.Show(this, en ? "Failed to start download." : "启动下载失败。", "iClock", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void btnClickSwitchBack()
    {
        bool en = language == "en";
        btnCheck.Click -= OnDownloadUpdate;
        btnCheck.Click += delegate
        {
            ApplyUpdateAndRestart();
        };
    }

    private void ApplyUpdateAndRestart()
    {
        bool en = language == "en";
        string currentExe = Application.ExecutablePath;
        string newExe = updateDestExe;
        if (string.IsNullOrEmpty(newExe) || !File.Exists(newExe))
        {
            MessageBox.Show(this, en ? "Update file not found. Please check for updates again." : "更新文件不存在，请重新检查更新。", "iClock", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        btnCheck.Enabled = false;
        string error;
        if (AppContext.SwapUpdateInPlace(currentExe, newExe, out error))
        {
            // Clean up the pending marker now that the swap is in place.
            try { AppContext.ClearPendingUpdate(); } catch { }
            Process.Start(currentExe);
            Application.Exit();
            return;
        }
        // Hot-swap failed (file locked, etc.): keep the pending marker so the
        // next launch applies the update, and tell the user.
        btnCheck.Enabled = true;
        try { AppContext.MarkPendingUpdate(updateNewVersion, newExe); } catch { }
        MessageBox.Show(this, en ? "Could not replace the running file. The update will be applied on the next launch of iClock." : "无法替换正在运行的文件，更新将在下次启动 iClock 时自动应用。", "iClock", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

// 托盘菜单里的开机自启动：整行可点，右侧胶囊只表示当前开或关。
