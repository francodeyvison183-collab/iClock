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
        bool en = s.Language == "";
        Font = AppContext.GetUiFont(s.Language, 9.5f);
        BackColor = AppContext.Win11Bg;
        AppContext.ApplyModernWindowStyle(this);
        Text = "";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        ShowIcon = false;
        try { Icon = AppContext.CreateIcon(); } catch { }
        ClientSize = new Size(380, 208);

        Label lblTitle = new Label();
        lblTitle.Text = en ? "" : "";
        lblTitle.Font = AppContext.GetUiFont(s.Language, 13.5f, FontStyle.Bold);
        lblTitle.ForeColor = AppContext.Win11Accent;
        lblTitle.TextAlign = ContentAlignment.MiddleCenter;
        lblTitle.SetBounds(0, 16, ClientSize.Width, 26);
        Controls.Add(lblTitle);

        Label lblPrompt = new Label();
        if (!hotkeyActive)
        {
            lblPrompt.Text = en ? "" : "";
            lblPrompt.ForeColor = Color.FromArgb(202, 80, 16);
        }
        else if (fallbackUsed)
        {
            lblPrompt.Text = en ? "" : "";
            lblPrompt.ForeColor = Color.FromArgb(202, 80, 16);
        }
        else
        {
            lblPrompt.Text = en ? "" : "";
            lblPrompt.ForeColor = AppContext.Win11TextPrimary;
        }
        lblPrompt.Font = AppContext.GetUiFont(s.Language, 9.5f, FontStyle.Regular);
        lblPrompt.TextAlign = ContentAlignment.MiddleCenter;
        lblPrompt.SetBounds(0, 52, ClientSize.Width, 20);
        Controls.Add(lblPrompt);

        string hotkey = hotkeyActive ? AppContext.HotkeyText(s.HotkeyModifiers, s.HotkeyKey) : (en ? "" : "");
        Label badge = new Label();
        badge.Text = hotkey;
        badge.Font = new Font("", 11f, FontStyle.Bold);
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

        string t1 = en ? "" : "";
        string t2 = en ? "" : "";
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
        btnStart.Text = en ? "" : "";
        btnStart.Font = AppContext.GetUiFont(s.Language, 9.5f, FontStyle.Regular);
        btnStart.SetBounds(startX, 154, 126, 34);
        AppContext.StylePrimaryButton(btnStart);
        btnStart.Click += delegate { StartRequested = true; Close(); };
        Controls.Add(btnStart);

        Button btnOk = new Button();
        btnOk.Text = en ? "" : "";
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
        bool en = language == "";
        Font = AppContext.GetUiFont(language, 9.5f);
        BackColor = AppContext.Win11Bg;
        AppContext.ApplyModernWindowStyle(this);
        Text = en ? "" : "";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(350, 508);

        Label lblTitle = new Label();
        lblTitle.Text = "";
        lblTitle.Font = AppContext.GetUiFont(language, 14f, FontStyle.Bold);
        lblTitle.ForeColor = AppContext.Win11Accent;
        lblTitle.SetBounds(20, 16, 140, 26);
        Controls.Add(lblTitle);

        Label lblDesc = new Label();
        lblDesc.UseMnemonic = false;
        lblDesc.Text = en ? "" : "";
        lblDesc.Font = AppContext.GetUiFont(language, 9f, FontStyle.Regular);
        lblDesc.ForeColor = AppContext.Win11TextSecondary;
        lblDesc.SetBounds(20, 44, 310, 20);
        Controls.Add(lblDesc);

        Label lblVersion = new Label();
        lblVersion.Text = (en ? "" : "") + AppContext.CURRENT_VERSION;
        lblVersion.Font = AppContext.GetUiFont(language, 9.5f, FontStyle.Regular);
        lblVersion.ForeColor = AppContext.Win11TextPrimary;
        int verW = TextRenderer.MeasureText(lblVersion.Text, lblVersion.Font).Width;
        lblVersion.SetBounds(20, 72, verW, 24);
        lblVersion.TextAlign = ContentAlignment.MiddleLeft;
        Controls.Add(lblVersion);

        LinkLabel link = new LinkLabel();
        link.Text = "";
        link.Font = AppContext.GetUiFont(language, 9f, FontStyle.Regular);
        link.LinkColor = AppContext.Win11Accent;
        link.LinkBehavior = LinkBehavior.HoverUnderline;
        int linkW = TextRenderer.MeasureText(link.Text, link.Font).Width;
        link.SetBounds(lblVersion.Right + 2, 74, linkW, 20);
        link.LinkClicked += delegate { try { Process.Start(""); } catch { } };
        Controls.Add(link);

        btnCheck = new Button();
        btnCheck.Text = en ? "" : "";
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
        lblSponsor.Text = en ? "" : "";
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
        lblAuthor.Text = en ? "" : "";
        lblAuthor.Font = AppContext.GetUiFont(language, 8.5f, FontStyle.Regular);
        lblAuthor.ForeColor = AppContext.Win11TextSecondary;
        lblAuthor.TextAlign = ContentAlignment.MiddleCenter;
        lblAuthor.SetBounds(20, 454, 310, 18);
        Controls.Add(lblAuthor);

        LinkLabel linkEmail = new LinkLabel();
        linkEmail.UseMnemonic = false;
        string emailPrefix = en ? "" : "";
        string emailAddress = "";
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
        tip.SetToolTip(linkEmail, en ? "" : "");
        linkEmail.LinkClicked += delegate
        {
            try { Clipboard.SetText(emailAddress); } catch { }
            try { tip.Show(en ? "" : "", linkEmail, 150, 20, 2000); } catch { }
            try { Process.Start("" + emailAddress); } catch { }
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
        bool en = language == "";

        // If a download has already finished, the button offers to apply it.
        if (updateDownloaded)
        {
            ApplyUpdateAndRestart();
            return;
        }

        btnCheck.Enabled = false;
        btnCheck.Text = en ? "" : "";

        System.Threading.ThreadPool.QueueUserWorkItem(delegate
        {
            string latest, url;
            bool success = AppContext.QueryLatestVersion("", out latest, out url);

            try
            {
                if (IsDisposed || !IsHandleCreated) return;
                BeginInvoke(new MethodInvoker(delegate
                {
                    btnCheck.Enabled = true;
                    btnCheck.Text = en ? "" : "";

                    if (!success || string.IsNullOrEmpty(latest))
                    {
                        MessageBox.Show(this, en ? "" : "", "", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    string ver = latest.TrimStart('v', 'V');
                    if (AppContext.IsNewer(ver, AppContext.CURRENT_VERSION))
                    {
                        updateNewVersion = ver;
                        updateExeUrl = AppContext.BuildUpdateExeUrl(ver);
                        btnCheck.Text = en ? ("" + ver) : ("" + ver);
                        btnCheck.Click -= OnCheckUpdates;
                        btnCheck.Click += OnDownloadUpdate;
                        string targetUrl = string.IsNullOrEmpty(url) ? "" : url;
                        if (app != null) app.NotifyUpdateFound(latest, targetUrl);
                    }
                    else
                    {
                        MessageBox.Show(this, en ? "" + AppContext.CURRENT_VERSION + "" : "" + AppContext.CURRENT_VERSION + "", "", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }));
            }
            catch { }
        });
    }

    private void OnDownloadUpdate(object sender, EventArgs e)
    {
        bool en = language == "";
        btnCheck.Enabled = false;
        btnCheck.Text = en ? "" : "";

        string dir = Path.Combine(Path.GetTempPath(), "");
        try { Directory.CreateDirectory(dir); } catch { }
        updateDestExe = Path.Combine(dir, "");

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
                            btnCheck.Text = en ? ("" + pe.ProgressPercentage + "") : ("" + pe.ProgressPercentage + "");
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
                                btnCheck.Text = en ? "" : "";
                                btnCheck.Click -= OnDownloadUpdate;
                                btnCheck.Click += OnCheckUpdates;
                                updateExeUrl = null;
                                string errMsg = pe.Error == null ? "" : pe.Error.Message;
                                MessageBox.Show(this, en ? "" + errMsg : "" + errMsg, "", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                return;
                            }
                            updateDownloaded = true;
                            btnCheck.Text = en ? "" : "";
                            btnClickSwitchBack();
                            // Persist the pending update so a restart applies it.
                            AppContext.MarkPendingUpdate(updateNewVersion, updateDestExe);
                            string msg = en ? "" : "";
                            try { app.ShowTrayNotification(en ? "" : "", msg, 8000); } catch { }
                            MessageBox.Show(this, msg, "", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            btnCheck.Text = en ? "" : "";
            btnCheck.Click -= OnDownloadUpdate;
            btnCheck.Click += OnCheckUpdates;
            updateExeUrl = null;
            MessageBox.Show(this, en ? "" : "", "", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void btnClickSwitchBack()
    {
        bool en = language == "";
        btnCheck.Click -= OnDownloadUpdate;
        btnCheck.Click += delegate
        {
            ApplyUpdateAndRestart();
        };
    }

    private void ApplyUpdateAndRestart()
    {
        bool en = language == "";
        string currentExe = Application.ExecutablePath;
        string newExe = updateDestExe;
        if (string.IsNullOrEmpty(newExe) || !File.Exists(newExe))
        {
            MessageBox.Show(this, en ? "" : "", "", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
        MessageBox.Show(this, en ? "" : "", "", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static Image LoadSponsorImage()
    {
        try
        {
            Stream stream = typeof(AppContext).Assembly.GetManifestResourceStream("");
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
            string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "", "");
            if (File.Exists(localPath))
            {
                using (Image temp = Image.FromFile(localPath))
                {
                    return new Bitmap(temp);
                }
            }
            localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "");
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
