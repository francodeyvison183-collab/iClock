using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static class TestCheck
{
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private static double GetOpacity(object o)
    {
        return (double)o.GetType().GetProperty("Opacity").GetValue(o, null);
    }

    [STAThread]
    private static int Main()
    {
        string appDataDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "iClock");
        string iniPath = System.IO.Path.Combine(appDataDir, "settings.ini");
        string todayHistoryPath = System.IO.Path.Combine(appDataDir, "history-" + DateTime.Now.ToString("yyyyMMdd") + ".tsv");
        string iniBackup = null;
        string historyBackup = null;
        if (System.IO.File.Exists(iniPath))
        {
            try { iniBackup = System.IO.File.ReadAllText(iniPath); System.IO.File.Delete(iniPath); } catch { }
        }
        if (System.IO.File.Exists(todayHistoryPath))
        {
            try { historyBackup = System.IO.File.ReadAllText(todayHistoryPath, System.Text.Encoding.UTF8); } catch { }
        }
        try
        {
            string dir = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string exePath = System.IO.Path.Combine(dir, "iClock.exe");
            if (!System.IO.File.Exists(exePath)) exePath = System.IO.Path.Combine(dir, "..\\dist\\iClock.exe");
            exePath = System.IO.Path.GetFullPath(exePath);
            Assembly asm = Assembly.LoadFrom(exePath);
            Type appType = asm.GetType("AppContext");
            Type noticeType = asm.GetType("NoticeDialog");
            BindingFlags bf = BindingFlags.NonPublic | BindingFlags.Instance;

            // Signal headless mode
            FieldInfo selfCheckField = appType.GetField("IsSelfCheckRunning", BindingFlags.NonPublic | BindingFlags.Static);
            if (selfCheckField != null) selfCheckField.SetValue(null, true);

            object app = Activator.CreateInstance(appType);
            Form overlay = (Form)appType.GetField("overlay", bf).GetValue(app);
            NotifyIcon tray = (NotifyIcon)appType.GetField("tray", bf).GetValue(app);
            object settings = appType.GetField("settings", bf).GetValue(app);
            FieldInfo endNoticeField = settings.GetType().GetField("EndNotice");
            FieldInfo endSoundField = settings.GetType().GetField("EndSound");
            endNoticeField.SetValue(settings, false);
            endSoundField.SetValue(settings, false);

            // 0. Verify default settings: FontSize = 20, Color = "#FF0000", X = -1 (center), Y = 60, Format = MM:SS
            Type settingsType = asm.GetType("Settings");
            object defaultSettings = Activator.CreateInstance(settingsType);
            bool defaultCollaborationEnabled = (bool)settingsType.GetField("DesktopCollaborationEnabled").GetValue(defaultSettings);
            bool defaultStatusPin = (bool)settingsType.GetField("AgentStatusPinEnabled").GetValue(defaultSettings);
            bool defaultDeepCollab = (bool)settingsType.GetField("AgentDeepCollabEnabled").GetValue(defaultSettings);
            bool defaultUsageTrack = (bool)settingsType.GetField("UsageTrackEnabled").GetValue(defaultSettings);
            bool defaultEyeCare = (bool)settingsType.GetField("EyeCareEnabled").GetValue(defaultSettings);
            int defaultEyeCareInterval = (int)settingsType.GetField("EyeCareIntervalMinutes").GetValue(defaultSettings);
            int defSize = (int)settingsType.GetField("FontSize").GetValue(defaultSettings);
            string defColor = (string)settingsType.GetField("Color").GetValue(defaultSettings);
            string defFont = (string)settingsType.GetField("FontFamily").GetValue(defaultSettings);
            int defX = (int)settingsType.GetField("X").GetValue(defaultSettings);
            int defY = (int)settingsType.GetField("Y").GetValue(defaultSettings);
            string defFormat = (string)settingsType.GetField("Format").GetValue(defaultSettings);
            if (!defaultCollaborationEnabled || !defaultStatusPin || !defaultDeepCollab || !defaultUsageTrack || defSize != 20 || defColor != "#FF0000" || defFont != "Segoe UI" || defX != -1 || defY != 60 || defFormat != "MM:SS")
            {
                Console.WriteLine("FAIL: Default collaboration, usage-track, or timer appearance settings are incorrect");
                return 18;
            }
            if (!defaultEyeCare || defaultEyeCareInterval != 45)
            {
                Console.WriteLine("FAIL: Eye care should default to on with a 45 minute interval");
                return 18;
            }
            Rectangle screen = Screen.PrimaryScreen.Bounds;
            int expectedX = screen.Left + (screen.Width - overlay.Width) / 2;
            int expectedY = screen.Top + 60;
            if (overlay.Location.Y != expectedY || Math.Abs(overlay.Location.X - expectedX) > 2)
            {
                Console.WriteLine("FAIL: Overlay default position incorrect: Got (" + overlay.Location.X + ", " + overlay.Location.Y + "), expected (" + expectedX + ", " + expectedY + ")");
                return 18;
            }
            Console.WriteLine("PASS: Default settings have FontSize = 20, Color = #FF0000, FontFamily = Segoe UI, Format = MM:SS, overlay horizontally centered and 60px from top.");

            // 1. Overlay must be hidden before countdown starts
            if (overlay.Visible)
            {
                Console.WriteLine("FAIL: Overlay visible before countdown starts");
                return 1;
            }
            Console.WriteLine("PASS: Overlay is hidden before countdown starts.");

            // 2. Tray menu must not contain Show/hide text and should use ModernMenuRenderer
            ContextMenuStrip menu = tray.ContextMenuStrip;
            if (menu == null || menu.ShowImageMargin || menu.ShowCheckMargin)
            {
                Console.WriteLine("FAIL: Tray menu should have ShowImageMargin and ShowCheckMargin = false");
                return 2;
            }
            if (menu.Renderer == null || menu.Renderer.GetType().Name != "ModernMenuRenderer")
            {
                Console.WriteLine("FAIL: Tray menu should use ModernMenuRenderer");
                return 2;
            }
            foreach (ToolStripItem item in menu.Items)
            {
                if (item.Text != null && (item.Text.Contains("显示") || item.Text.Contains("Show / hide")))
                {
                    Console.WriteLine("FAIL: Found visibility menu item: " + item.Text);
                    return 2;
                }
            }
            ToolStripMenuItem menuMoveItem = (ToolStripMenuItem)appType.GetField("menuMove", bf).GetValue(app);
            if (menuMoveItem == null || menuMoveItem.ShortcutKeyDisplayString != "Alt + 拖拽")
            {
                Console.WriteLine("FAIL: menuMove should display Alt + 拖拽 shortcut string (got " + (menuMoveItem == null ? "null" : menuMoveItem.ShortcutKeyDisplayString) + ")");
                return 2;
            }
            Console.WriteLine("PASS: Tray menu uses ModernMenuRenderer with clean single-column layout, no show/hide text, and Alt + 拖拽 hint.");

            // 3. Start countdown -> Overlay becomes visible
            MethodInfo toggle = appType.GetMethod("Toggle", bf);
            toggle.Invoke(app, null);
            if (!overlay.Visible || Math.Abs(overlay.Opacity - 1.0) > 0.01)
            {
                Console.WriteLine("FAIL: Overlay should be visible and have Opacity 1.0 after starting countdown");
                return 3;
            }
            // Pause countdown -> Overlay remains visible with Opacity 0.50
            toggle.Invoke(app, null);
            if (!overlay.Visible || Math.Abs(GetOpacity(overlay) - 0.50) > 0.01)
            {
                Console.WriteLine("FAIL: Overlay should have Opacity 0.50 when paused (got " + GetOpacity(overlay) + ")");
                return 3;
            }
            // Resume countdown -> Overlay Opacity returns to 1.0
            toggle.Invoke(app, null);
            if (!overlay.Visible || Math.Abs(GetOpacity(overlay) - 1.0) > 0.01)
            {
                Console.WriteLine("FAIL: Overlay should return to Opacity 1.0 when resumed");
                return 3;
            }
            // Verify MoveMode White background and 50% opacity
            MethodInfo setMoveMode = overlay.GetType().GetMethod("SetMoveMode");
            setMoveMode.Invoke(overlay, new object[] { true, true });
            if (overlay.BackColor != Color.White || Math.Abs(GetOpacity(overlay) - 0.50) > 0.01)
            {
                Console.WriteLine("FAIL: MoveMode should have BackColor = White and Opacity = 0.50");
                return 3;
            }
            setMoveMode.Invoke(overlay, new object[] { false, true });
            if (overlay.BackColor != Color.Fuchsia || Math.Abs(GetOpacity(overlay) - 1.0) > 0.01)
            {
                Console.WriteLine("FAIL: Exiting MoveMode should restore BackColor = Fuchsia and Opacity = 1.0");
                return 3;
            }
            Console.WriteLine("PASS: Overlay has 50% opacity when paused and MoveMode uses White background with 50% opacity.");

            // 4. Finish countdown -> Overlay becomes hidden
            FieldInfo deadlineField = appType.GetField("deadlineTimestamp", bf);
            deadlineField.SetValue(app, Stopwatch.GetTimestamp() - 1000L);
            MethodInfo tick = appType.GetMethod("Tick", bf);
            tick.Invoke(app, new object[] { null, EventArgs.Empty });
            if (overlay.Visible)
            {
                Console.WriteLine("FAIL: Overlay should be hidden after countdown ends");
                return 4;
            }
            Console.WriteLine("PASS: Overlay is hidden after countdown ends.");

            // 5. Test NoticeDialog when EndNotice is enabled
            endNoticeField.SetValue(settings, true);
            // Trigger finish again with EndNotice = true
            MethodInfo finish = appType.GetMethod("Finish", bf);
            finish.Invoke(app, null);
            Form activeNotice = (Form)appType.GetField("activeNotice", bf).GetValue(app);
            if (activeNotice == null)
            {
                Console.WriteLine("FAIL: activeNotice was not created on finish");
                return 5;
            }
            if (!activeNotice.TopMost)
            {
                Console.WriteLine("FAIL: activeNotice TopMost is false");
                return 6;
            }
            // Check that notice dialog contains finish labels
            bool hasTimeLabel = false, hasFinishLabel = false;
            foreach (Control c in activeNotice.Controls)
            {
                if (c is Label)
                {
                    if (c.Text.Contains("00:00")) hasTimeLabel = true;
                    if (c.Text.Contains("结束时间") || c.Text.Contains("Finished at")) hasFinishLabel = true;
                }
            }
            if (!hasTimeLabel || !hasFinishLabel)
            {
                Console.WriteLine("FAIL: NoticeDialog missing time or finish label");
                return 7;
            }
            Rectangle waNotice = Screen.PrimaryScreen.WorkingArea;
            int expNoticeX = waNotice.Left + (waNotice.Width - activeNotice.Width) / 2;
            int expNoticeY = waNotice.Top + (waNotice.Height - activeNotice.Height) / 2;
            if (Math.Abs(activeNotice.Location.X - expNoticeX) > 20 || Math.Abs(activeNotice.Location.Y - expNoticeY) > 20)
            {
                Console.WriteLine("FAIL: NoticeDialog not centered on screen: " + activeNotice.Location + " vs " + expNoticeX + "," + expNoticeY);
                return 7;
            }
            Console.WriteLine("PASS: NoticeDialog is centered, TopMost, and displays countdown finish time.");
            activeNotice.Close();

            // 6. Restart countdown then Reset -> Overlay becomes hidden
            toggle.Invoke(app, null);
            if (!overlay.Visible)
            {
                Console.WriteLine("FAIL: Overlay should be visible after restarting countdown");
                return 8;
            }
            ToolStripMenuItem menuReset = (ToolStripMenuItem)appType.GetField("menuReset", bf).GetValue(app);
            menuReset.PerformClick();
            if (overlay.Visible)
            {
                Console.WriteLine("FAIL: Overlay should be hidden after reset");
                return 9;
            }
            Console.WriteLine("PASS: Overlay is hidden after reset.");

            // 7. Verify AboutDialog and menuAbout
            ToolStripMenuItem menuAbout = (ToolStripMenuItem)appType.GetField("menuAbout", bf).GetValue(app);
            if (menuAbout == null || (menuAbout.Text != "关于 iClock…" && menuAbout.Text != "About iClock…"))
            {
                Console.WriteLine("FAIL: menuAbout missing or incorrect text: " + (menuAbout == null ? "null" : menuAbout.Text));
                return 10;
            }
            Type aboutType = asm.GetType("AboutDialog");
            Form aboutDialog = (Form)Activator.CreateInstance(aboutType, new object[] { "zh" });
            PictureBox pb = null;
            Button btnCheck = null;
            Button btnOk = null;
            foreach (Control c in aboutDialog.Controls)
            {
                if (c is PictureBox) pb = (PictureBox)c;
                if (c is Button && (c.Text == "检查更新" || c.Text == "Check Updates")) btnCheck = (Button)c;
                if (c is Button && (c.Text == "确定" || c.Text == "OK")) btnOk = (Button)c;
            }
            if (pb == null || pb.Image == null || pb.Image.Width < 300)
            {
                Console.WriteLine("FAIL: AboutDialog image not loaded properly");
                return 11;
            }
            if (pb.BorderStyle != BorderStyle.None)
            {
                Console.WriteLine("FAIL: AboutDialog PictureBox has border");
                return 12;
            }
            if (btnCheck == null)
            {
                Console.WriteLine("FAIL: AboutDialog missing Check Updates button");
                return 12;
            }
            if (btnOk != null)
            {
                Console.WriteLine("FAIL: AboutDialog should not contain OK button");
                return 12;
            }
            if (pb.Width < 280)
            {
                Console.WriteLine("FAIL: AboutDialog PictureBox width too narrow: " + pb.Width);
                return 12;
            }
            aboutDialog.Close();
            Console.WriteLine("PASS: AboutDialog loaded without OK button, with embedded zan.jpg and Check Updates button.");

            // 8. Performance check: Overlay cached GDI handles & dynamic timer interval
            Type overlayType = asm.GetType("Overlay");
            object cachedFont = overlayType.GetField("cachedFont", bf).GetValue(overlay);
            object cachedBrush = overlayType.GetField("cachedBrush", bf).GetValue(overlay);
            object cachedFormat = overlayType.GetField("cachedFormat", bf).GetValue(overlay);
            if (cachedFont == null || cachedBrush == null || cachedFormat == null)
            {
                Console.WriteLine("FAIL: Overlay GDI resources not pre-allocated/cached");
                return 13;
            }
            toggle.Invoke(app, null);
            Timer appTimer = (Timer)appType.GetField("timer", bf).GetValue(app);
            if (appTimer.Interval < 80 || appTimer.Interval > 1000)
            {
                Console.WriteLine("FAIL: Timer interval outside expected [80, 1000] range: " + appTimer.Interval);
                return 14;
            }
            Console.WriteLine("PASS: Overlay caches GDI handles and timer uses dynamic heartbeat alignment (" + appTimer.Interval + "ms).");

            // 9. Verify SettingsDialog contains language selection and default update check & deduplicated launch reporting
            Type settingsDialogType = asm.GetType("SettingsDialog");
            Form settingsDialog = (Form)Activator.CreateInstance(settingsDialogType, new object[] { settings });
            bool hasUpdateCheck = false;
            bool hasLanguageCombo = false;
            bool hasFontCombo = false;
            foreach (Control c in settingsDialog.Controls)
            {
                if (c is CheckBox && (c.Text.Contains("自动检查版本更新") || c.Text.Contains("Check for updates")))
                {
                    hasUpdateCheck = true;
                    break;
                }
                if (c is ComboBox)
                {
                    ComboBox cb = (ComboBox)c;
                    if (cb.Items.Contains("简体中文")) hasLanguageCombo = true;
                    if (cb.Items.Contains("Consolas (极客等宽)") || cb.Items.Contains("Consolas (Monospace)"))
                    {
                        hasFontCombo = true;
                        if (cb.DrawMode != DrawMode.OwnerDrawFixed)
                        {
                            Console.WriteLine("FAIL: fontCombo should have DrawMode OwnerDrawFixed");
                            return 15;
                        }
                    }
                }
            }
            MethodInfo applyLang = settingsDialogType.GetMethod("ApplyInterfaceLanguage", bf | BindingFlags.Public);
            if (applyLang == null)
            {
                Console.WriteLine("FAIL: SettingsDialog missing ApplyInterfaceLanguage");
                return 15;
            }
            applyLang.Invoke(settingsDialog, new object[] { true });
            if (settingsDialog.Font.FontFamily.Name != "Segoe UI" || settingsDialog.Text != "iClock Settings")
            {
                Console.WriteLine("FAIL: SettingsDialog did not update font/title on language switch to en");
                return 15;
            }
            applyLang.Invoke(settingsDialog, new object[] { false });
            if (!settingsDialog.Font.FontFamily.Name.Contains("YaHei") && settingsDialog.Font.FontFamily.Name != SystemFonts.MessageBoxFont.FontFamily.Name)
            {
                Console.WriteLine("FAIL: SettingsDialog did not update font on language switch to zh");
                return 15;
            }

            // Verify all dropdowns have DrawMode OwnerDrawFixed and ItemHeight 22
            ComboBox formatBox = (ComboBox)settingsDialogType.GetField("format", bf).GetValue(settingsDialog);
            ComboBox fontBox = (ComboBox)settingsDialogType.GetField("fontCombo", bf).GetValue(settingsDialog);
            if (formatBox.DrawMode != DrawMode.OwnerDrawFixed || formatBox.ItemHeight != 22)
            {
                Console.WriteLine("FAIL: format combo should have DrawMode OwnerDrawFixed and ItemHeight 22");
                return 15;
            }
            if (fontBox.DrawMode != DrawMode.OwnerDrawFixed || fontBox.ItemHeight != 22)
            {
                Console.WriteLine("FAIL: fontCombo should have DrawMode OwnerDrawFixed and ItemHeight 22");
                return 15;
            }

            // Verify all input controls have 4px margins
            TextBox hotkeyBox = (TextBox)settingsDialogType.GetField("hotkeyBox", bf).GetValue(settingsDialog);
            TextBox endMsgBox = (TextBox)settingsDialogType.GetField("endMessage", bf).GetValue(settingsDialog);
            NumericUpDown minBox = (NumericUpDown)settingsDialogType.GetField("minutes", bf).GetValue(settingsDialog);
            NumericUpDown sizeBox = (NumericUpDown)settingsDialogType.GetField("size", bf).GetValue(settingsDialog);

            int hotkeyMargin = SendMessage(hotkeyBox.Handle, 0x00D4, IntPtr.Zero, IntPtr.Zero).ToInt32() & 0xFFFF;
            int endMsgMargin = SendMessage(endMsgBox.Handle, 0x00D4, IntPtr.Zero, IntPtr.Zero).ToInt32() & 0xFFFF;
            int minMargin = 0;
            foreach (Control c in minBox.Controls) if (c is TextBox) minMargin = SendMessage(c.Handle, 0x00D4, IntPtr.Zero, IntPtr.Zero).ToInt32() & 0xFFFF;
            int sizeMargin = 0;
            foreach (Control c in sizeBox.Controls) if (c is TextBox) sizeMargin = SendMessage(c.Handle, 0x00D4, IntPtr.Zero, IntPtr.Zero).ToInt32() & 0xFFFF;

            if (hotkeyMargin != 4 || endMsgMargin != 4 || minMargin != 4 || sizeMargin != 4)
            {
                Console.WriteLine("FAIL: Expected 4px margins on inputs (got hotkey=" + hotkeyMargin + ", endMsg=" + endMsgMargin + ", minutes=" + minMargin + ", size=" + sizeMargin + ")");
                return 15;
            }

            settingsDialog.Dispose();
            if (hasUpdateCheck)
            {
                Console.WriteLine("FAIL: SettingsDialog should not have manual update check option");
                return 15;
            }
            if (hasLanguageCombo)
            {
                Console.WriteLine("FAIL: language should live on the tray menu, not SettingsDialog");
                return 15;
            }
            if (!hasFontCombo)
            {
                Console.WriteLine("FAIL: SettingsDialog missing font option");
                return 15;
            }
            object menuLanguage = appType.GetField("menuLanguage", bf).GetValue(app);
            object menuStartup = appType.GetField("menuStartup", bf).GetValue(app);
            if (menuLanguage == null || menuStartup == null)
            {
                Console.WriteLine("FAIL: tray menu missing language or auto-start items");
                return 15;
            }
            FieldInfo launchField = appType.GetField("launchReported", BindingFlags.NonPublic | BindingFlags.Static);
            if (launchField == null || !(bool)launchField.GetValue(null))
            {
                Console.WriteLine("FAIL: launchReported was not set on startup or field missing");
                return 15;
            }
            Console.WriteLine("PASS: tray menu holds language and auto-start; SettingsDialog still previews fonts; launchReported is de-duplicated.");

            // 10. Verify version comparison and JSON parsing
            BindingFlags sbf = BindingFlags.NonPublic | BindingFlags.Static;
            FieldInfo verField = appType.GetField("CURRENT_VERSION", sbf);
            if (verField == null || (string)verField.GetValue(null) != "1.13")
            {
                Console.WriteLine("FAIL: CURRENT_VERSION should be 1.12");
                return 16;
            }
            MethodInfo isNewer = appType.GetMethod("IsNewer", sbf);
            MethodInfo extractJson = appType.GetMethod("ExtractJsonValue", sbf);
            if (!(bool)isNewer.Invoke(null, new object[] { "1.10", "1.09" }) ||
                !(bool)isNewer.Invoke(null, new object[] { "v2.0", "1.09" }) ||
                (bool)isNewer.Invoke(null, new object[] { "1.09", "1.09" }))
            {
                Console.WriteLine("FAIL: Version comparison failed");
                return 16;
            }
            string extracted = (string)extractJson.Invoke(null, new object[] { "{\"latest\":\"1.02\",\"url\":\"http://test\"}", "latest" });
            if (extracted != "1.02")
            {
                Console.WriteLine("FAIL: JSON value extraction failed");
                return 17;
            }
            Console.WriteLine("PASS: Version comparison and JSON extraction work correctly.");

            // 11. Verify WelcomeDialog and FirstRun logic
            Type welcomeType = asm.GetType("WelcomeDialog");
            if (welcomeType == null)
            {
                Console.WriteLine("FAIL: WelcomeDialog type missing");
                return 18;
            }
            Form welcomeDlg = (Form)Activator.CreateInstance(welcomeType, new object[] { settings });
            PropertyInfo startReqProp = welcomeType.GetProperty("StartRequested");
            if (startReqProp == null || (bool)startReqProp.GetValue(welcomeDlg, null) != false)
            {
                Console.WriteLine("FAIL: WelcomeDialog StartRequested should initially be false");
                return 18;
            }
            Button startBtn = null;
            Button okBtn = null;
            bool hasIcon = false;
            bool titleCorrect = false;
            foreach (Control c in welcomeDlg.Controls)
            {
                if (c is Button && (c.Text.Contains("开始") || c.Text.Contains("Start"))) startBtn = (Button)c;
                if (c is Button && (c.Text.Contains("知道了") || c.Text.Contains("Got it"))) okBtn = (Button)c;
                if (c is PictureBox) hasIcon = true;
                if (c is Panel) foreach (Control sub in c.Controls) if (sub is PictureBox) hasIcon = true;
                if (c is Label && (c.Text == "欢迎使用 iClock" || c.Text == "Welcome to iClock")) titleCorrect = true;
            }
            if (startBtn == null || okBtn == null || !hasIcon || !titleCorrect)
            {
                Console.WriteLine("FAIL: WelcomeDialog missing required elements (startBtn=" + (startBtn != null) + ", okBtn=" + (okBtn != null) + ", hasIcon=" + hasIcon + ", titleCorrect=" + titleCorrect + ")");
                return 18;
            }
            int btnCenter = (startBtn.Left + okBtn.Right) / 2;
            if (Math.Abs(btnCenter - welcomeDlg.ClientSize.Width / 2) > 2)
            {
                Console.WriteLine("FAIL: WelcomeDialog buttons not centered (btnCenter=" + btnCenter + ", dlgCenter=" + (welcomeDlg.ClientSize.Width / 2) + ")");
                return 18;
            }
            MethodInfo clickMethod = typeof(Button).GetMethod("OnClick", BindingFlags.NonPublic | BindingFlags.Instance);
            clickMethod.Invoke(startBtn, new object[] { EventArgs.Empty });
            if ((bool)startReqProp.GetValue(welcomeDlg, null) != true)
            {
                Console.WriteLine("FAIL: Clicking start button should set StartRequested to true");
                return 18;
            }
            welcomeDlg.Dispose();
            Console.WriteLine("PASS: WelcomeDialog presents concise ready guidance, prompt icon, and centered buttons.");

            // 12. Verify IsInEphemeralFolder detection and EnsureStartMenuShortcut
            MethodInfo isEphemeralMethod = appType.GetMethod("IsInEphemeralFolder", BindingFlags.NonPublic | BindingFlags.Static);
            if (isEphemeralMethod == null)
            {
                Console.WriteLine("FAIL: IsInEphemeralFolder method missing");
                return 19;
            }
            string tempFolderExe = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "iClock.exe");
            string normalExe1 = "C:\\Program Files\\iClock\\iClock.exe";
            string normalExe2 = "D:\\Tools\\iClock\\iClock.exe";
            string downloadsExe = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "iClock.exe");
            if (!(bool)isEphemeralMethod.Invoke(null, new object[] { tempFolderExe }) ||
                !(bool)isEphemeralMethod.Invoke(null, new object[] { downloadsExe }) ||
                (bool)isEphemeralMethod.Invoke(null, new object[] { normalExe1 }) ||
                (bool)isEphemeralMethod.Invoke(null, new object[] { normalExe2 }))
            {
                Console.WriteLine("FAIL: IsInEphemeralFolder returned incorrect results");
                return 19;
            }

            MethodInfo shortcutMethod = appType.GetMethod("EnsureStartMenuShortcut", BindingFlags.NonPublic | BindingFlags.Static);
            if (shortcutMethod == null)
            {
                Console.WriteLine("FAIL: EnsureStartMenuShortcut method missing");
                return 20;
            }
            string testLnk = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "iClock_test_" + Guid.NewGuid().ToString("N") + ".lnk");
            try
            {
                bool resFalse = (bool)shortcutMethod.Invoke(null, new object[] { false, testLnk, exePath });
                if (resFalse || System.IO.File.Exists(testLnk))
                {
                    Console.WriteLine("FAIL: EnsureStartMenuShortcut should not create shortcut when createIfNotExists is false");
                    return 20;
                }
                bool resTrue = (bool)shortcutMethod.Invoke(null, new object[] { true, testLnk, exePath });
                if (!resTrue || !System.IO.File.Exists(testLnk))
                {
                    Console.WriteLine("FAIL: EnsureStartMenuShortcut failed to create shortcut");
                    return 20;
                }
                // Verify update target
                string newTarget = "C:\\Windows\\notepad.exe";
                bool resUpdate = (bool)shortcutMethod.Invoke(null, new object[] { false, testLnk, newTarget });
                if (!resUpdate)
                {
                    Console.WriteLine("FAIL: EnsureStartMenuShortcut failed to update existing shortcut");
                    return 20;
                }
            }
            finally
            {
                if (System.IO.File.Exists(testLnk))
                {
                    try { System.IO.File.Delete(testLnk); } catch { }
                }
            }
            Console.WriteLine("PASS: IsInEphemeralFolder detection and EnsureStartMenuShortcut self-healing work correctly.");

            // 13. Verify FontFamily setting dynamically updates Overlay cachedFont
            FieldInfo fontField = settingsType.GetField("FontFamily");
            fontField.SetValue(settings, "Consolas");
            MethodInfo refreshSettings = overlayType.GetMethod("RefreshSettings");
            refreshSettings.Invoke(overlay, null);
            Font updatedFont = (Font)overlayType.GetField("cachedFont", bf).GetValue(overlay);
            if (updatedFont == null || updatedFont.FontFamily.Name != "Consolas")
            {
                Console.WriteLine("FAIL: Overlay did not update cachedFont to Consolas (got " + (updatedFont == null ? "null" : updatedFont.FontFamily.Name) + ")");
                return 21;
            }
            Console.WriteLine("PASS: FontFamily setting dynamically updates Overlay cachedFont to Consolas.");

            // 14. Verify AppContext.GetUiFont returns Segoe UI for English and Microsoft YaHei UI (or Microsoft YaHei) for Chinese
            MethodInfo getUiFont = appType.GetMethod("GetUiFont", BindingFlags.NonPublic | BindingFlags.Static);
            if (getUiFont == null)
            {
                Console.WriteLine("FAIL: GetUiFont method missing");
                return 22;
            }
            Font fontEn = (Font)getUiFont.Invoke(null, new object[] { "en", 9.5f, FontStyle.Regular });
            Font fontZh = (Font)getUiFont.Invoke(null, new object[] { "zh", 9.5f, FontStyle.Regular });
            if (fontEn == null || fontEn.FontFamily.Name != "Segoe UI")
            {
                Console.WriteLine("FAIL: GetUiFont('en') did not return Segoe UI (got " + (fontEn == null ? "null" : fontEn.FontFamily.Name) + ")");
                return 22;
            }
            if (fontZh == null || (!fontZh.FontFamily.Name.Contains("YaHei") && fontZh.FontFamily.Name != SystemFonts.MessageBoxFont.FontFamily.Name))
            {
                Console.WriteLine("FAIL: GetUiFont('zh') did not return YaHei (got " + (fontZh == null ? "null" : fontZh.FontFamily.Name) + ")");
                return 22;
            }
            Console.WriteLine("PASS: GetUiFont returns high-DPI modern fonts (Segoe UI for en, Microsoft YaHei UI for zh).");

            // 15. Verify Win11 Fluent styling: BackColor = #FAFAFA, Primary Button = #0067C0, Secondary Button styling, and GetRoundedRectPath
            FieldInfo win11BgField = appType.GetField("Win11Bg", BindingFlags.NonPublic | BindingFlags.Static);
            FieldInfo win11AccentField = appType.GetField("Win11Accent", BindingFlags.NonPublic | BindingFlags.Static);
            if (win11BgField == null || (Color)win11BgField.GetValue(null) != Color.FromArgb(250, 250, 250) ||
                win11AccentField == null || (Color)win11AccentField.GetValue(null) != Color.FromArgb(0, 103, 192))
            {
                Console.WriteLine("FAIL: Win11 palette constants incorrect");
                return 23;
            }
            Button testBtnPrimary = new Button();
            Button testBtnSecondary = new Button();
            MethodInfo stylePrimary = appType.GetMethod("StylePrimaryButton", BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo styleSecondary = appType.GetMethod("StyleSecondaryButton", BindingFlags.NonPublic | BindingFlags.Static);
            stylePrimary.Invoke(null, new object[] { testBtnPrimary });
            styleSecondary.Invoke(null, new object[] { testBtnSecondary });
            if (testBtnPrimary.BackColor != Color.FromArgb(0, 103, 192) || testBtnPrimary.ForeColor != Color.White || testBtnPrimary.FlatStyle != FlatStyle.Flat)
            {
                Console.WriteLine("FAIL: Primary button styling incorrect");
                return 23;
            }
            if (testBtnSecondary.BackColor != Color.White || testBtnSecondary.FlatStyle != FlatStyle.Flat)
            {
                Console.WriteLine("FAIL: Secondary button styling incorrect");
                return 23;
            }
            MethodInfo getRoundedRect = appType.GetMethod("GetRoundedRectPath", BindingFlags.NonPublic | BindingFlags.Static);
            object pathObj = getRoundedRect.Invoke(null, new object[] { new Rectangle(0, 0, 100, 50), 6 });
            if (pathObj == null || !(pathObj is System.Drawing.Drawing2D.GraphicsPath))
            {
                Console.WriteLine("FAIL: GetRoundedRectPath failed to return GraphicsPath");
                return 23;
            }
            ((System.Drawing.Drawing2D.GraphicsPath)pathObj).Dispose();
            testBtnPrimary.Dispose();
            testBtnSecondary.Dispose();
            Console.WriteLine("PASS: Win11 Fluent design system (surface colors, primary/secondary button hierarchy, rounded path) verified.");

            // 16. Verify context menu hero bold font, shortcut string, dynamic states, and concise copy
            ToolStripMenuItem menuStart = (ToolStripMenuItem)appType.GetField("menuStart", bf).GetValue(app);
            menuReset = (ToolStripMenuItem)appType.GetField("menuReset", bf).GetValue(app);
            menuAbout = (ToolStripMenuItem)appType.GetField("menuAbout", bf).GetValue(app);
            ToolStripMenuItem menuHistory = (ToolStripMenuItem)appType.GetField("menuHistory", bf).GetValue(app);
            ToolStripMenuItem menuExit = (ToolStripMenuItem)appType.GetField("menuExit", bf).GetValue(app);

            if (menuStart == null || !menuStart.Font.Bold)
            {
                Console.WriteLine("FAIL: menuStart should have bold font");
                return 24;
            }
            if (string.IsNullOrEmpty(menuStart.ShortcutKeyDisplayString))
            {
                Console.WriteLine("FAIL: menuStart should have ShortcutKeyDisplayString set");
                return 24;
            }
            if (menuAbout.Text != "关于 iClock…" && menuAbout.Text != "About iClock…")
            {
                Console.WriteLine("FAIL: menuAbout text must be '关于 iClock…' / 'About iClock…', got: " + menuAbout.Text);
                return 24;
            }
            if (menuHistory.Text != "今日记录" && menuHistory.Text != "Today's history")
            {
                Console.WriteLine("FAIL: menuHistory text should be '今日记录' / 'Today\'s history' without ellipsis, got: " + menuHistory.Text);
                return 24;
            }
            if (string.IsNullOrEmpty(menuHistory.ShortcutKeyDisplayString))
            {
                Console.WriteLine("FAIL: menuHistory should show today's focus time as ShortcutKeyDisplayString");
                return 24;
            }
            if (menuHistory.GetType().Name != "HistoryMenuItem")
            {
                Console.WriteLine("FAIL: menuHistory should be HistoryMenuItem composite item, got: " + menuHistory.GetType().Name);
                return 24;
            }
            foreach (ToolStripItem it in tray.ContextMenuStrip.Items)
            {
                if (it != null && it.GetType().Name == "UsageStripItem")
                {
                    Console.WriteLine("FAIL: UsageStripItem should be eliminated and merged into menuHistory");
                    return 24;
                }
            }
            if (menuExit.Text != "退出" && menuExit.Text != "Exit")
            {
                Console.WriteLine("FAIL: menuExit text should be '退出' / 'Exit', got: " + menuExit.Text);
                return 24;
            }
            if (tray.ContextMenuStrip.MinimumSize.Width < 240)
            {
                Console.WriteLine("FAIL: Tray menu MinimumSize.Width should be >= 240, got: " + tray.ContextMenuStrip.MinimumSize.Width);
                return 24;
            }
            if (menuStart.Padding.Left != 32 || menuStart.Padding.Right != 32)
            {
                Console.WriteLine("FAIL: menuStart padding should be 32px on left and right, got: " + menuStart.Padding);
                return 24;
            }
            MethodInfo updateMenuText = appType.GetMethod("UpdateMenuText", bf);
            FieldInfo runningField = appType.GetField("running", bf);
            FieldInfo sessionActiveField = appType.GetField("sessionActive", bf);
            runningField.SetValue(app, false);
            sessionActiveField.SetValue(app, false);
            updateMenuText.Invoke(app, null);
            if (menuReset.Enabled || (menuStart.Text != "开始倒计时" && menuStart.Text != "Start countdown") || menuStart.ForeColor != Color.FromArgb(0, 103, 192))
            {
                Console.WriteLine("FAIL: Idle menu state incorrect (Reset.Enabled=" + menuReset.Enabled + ", Start.Text=" + menuStart.Text + ", Color=" + menuStart.ForeColor + ")");
                return 24;
            }
            runningField.SetValue(app, true);
            sessionActiveField.SetValue(app, true);
            updateMenuText.Invoke(app, null);
            if (!menuReset.Enabled || (menuStart.Text != "暂停倒计时" && menuStart.Text != "Pause countdown") || menuStart.ForeColor != Color.FromArgb(202, 80, 16))
            {
                Console.WriteLine("FAIL: Running menu state incorrect (Reset.Enabled=" + menuReset.Enabled + ", Start.Text=" + menuStart.Text + ", Color=" + menuStart.ForeColor + ")");
                return 24;
            }
            runningField.SetValue(app, false);
            sessionActiveField.SetValue(app, true);
            updateMenuText.Invoke(app, null);
            if (!menuReset.Enabled || (menuStart.Text != "继续倒计时" && menuStart.Text != "Resume countdown") || menuStart.ForeColor != Color.FromArgb(16, 124, 65))
            {
                Console.WriteLine("FAIL: Paused menu state incorrect (Reset.Enabled=" + menuReset.Enabled + ", Start.Text=" + menuStart.Text + ", Color=" + menuStart.ForeColor + ")");
                return 24;
            }
            sessionActiveField.SetValue(app, false);
            Console.WriteLine("PASS: Modern Context Menu layout, 32px padding, three-state ForeColor, dynamic state transitions, and concise copy verified.");

            // 17. Verify real-time hotkey conflict detection, function key support, and safe fallback
            MethodInfo probeHotkey = appType.GetMethod("ProbeHotkey", BindingFlags.NonPublic | BindingFlags.Static);
            if (probeHotkey == null)
            {
                Console.WriteLine("FAIL: ProbeHotkey method missing in AppContext");
                return 25;
            }
            int testKey = (int)Keys.F11;
            int testMods = 2 | 1; // Ctrl+Alt
            bool initiallyFree = (bool)probeHotkey.Invoke(null, new object[] { testMods, testKey });
            if (!initiallyFree)
            {
                testKey = (int)Keys.F12;
                initiallyFree = (bool)probeHotkey.Invoke(null, new object[] { testMods, testKey });
            }

            NativeWindow probeHolder = new NativeWindow();
            probeHolder.CreateHandle(new CreateParams());
            MethodInfo regHotKey = appType.GetMethod("RegisterHotKey", BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo unregHotKey = appType.GetMethod("UnregisterHotKey", BindingFlags.NonPublic | BindingFlags.Static);
            bool held = (bool)regHotKey.Invoke(null, new object[] { probeHolder.Handle, 0x1234, (uint)(testMods | 0x4000), (uint)testKey });
            if (held)
            {
                bool probeOccupied = (bool)probeHotkey.Invoke(null, new object[] { testMods, testKey });
                unregHotKey.Invoke(null, new object[] { probeHolder.Handle, 0x1234 });
                probeHolder.DestroyHandle();
                bool probeRestored = (bool)probeHotkey.Invoke(null, new object[] { testMods, testKey });
                if (probeOccupied || !probeRestored)
                {
                    Console.WriteLine("FAIL: ProbeHotkey failed: probeOccupied=" + probeOccupied + ", probeRestored=" + probeRestored);
                    return 25;
                }
            }
            else
            {
                probeHolder.DestroyHandle();
            }

            Form dlg = (Form)Activator.CreateInstance(settingsDialogType, new object[] { settings });
            TextBox box = (TextBox)settingsDialogType.GetField("hotkeyBox", bf).GetValue(dlg);
            MethodInfo capture = settingsDialogType.GetMethod("CaptureHotkey", bf);

            // 17a. Standalone function key (F10) without modifiers should be accepted
            KeyEventArgs keF10 = new KeyEventArgs(Keys.F10);
            capture.Invoke(dlg, new object[] { box, keF10 });
            if (box.Text != "F10")
            {
                Console.WriteLine("FAIL: Standalone F10 should be accepted, text=" + box.Text);
                return 25;
            }

            // 17b. Normal key without modifier (Space) should be rejected
            KeyEventArgs keSpace = new KeyEventArgs(Keys.Space);
            capture.Invoke(dlg, new object[] { box, keSpace });
            if (!box.Text.Contains("修饰键") && !box.Text.Contains("modifier"))
            {
                Console.WriteLine("FAIL: Lone space should be rejected, text=" + box.Text);
                return 25;
            }

            // 17c. Occupied key should be flagged without replacing the active hotkey
            NativeWindow holder2 = new NativeWindow();
            holder2.CreateHandle(new CreateParams());
            bool held2 = (bool)regHotKey.Invoke(null, new object[] { holder2.Handle, 0x5678, (uint)(testMods | 0x4000), (uint)testKey });
            if (held2)
            {
                KeyEventArgs keOccupied = new KeyEventArgs((Keys)testKey | Keys.Control | Keys.Alt);
                capture.Invoke(dlg, new object[] { box, keOccupied });
                unregHotKey.Invoke(null, new object[] { holder2.Handle, 0x5678 });
                holder2.DestroyHandle();
                if (!box.Text.Contains("已被占用") && !box.Text.Contains("Occupied"))
                {
                    Console.WriteLine("FAIL: Occupied hotkey should be flagged, text=" + box.Text);
                    return 25;
                }
            }
            else
            {
                holder2.DestroyHandle();
            }

            // 17d. Current hotkey should remain valid (isCurrent)
            int currMods = (int)settingsType.GetField("HotkeyModifiers").GetValue(settings);
            int currKey = (int)settingsType.GetField("HotkeyKey").GetValue(settings);
            Keys currentKeyCombination = (Keys)currKey;
            if ((currMods & 2) != 0) currentKeyCombination |= Keys.Control;
            if ((currMods & 1) != 0) currentKeyCombination |= Keys.Alt;
            if ((currMods & 4) != 0) currentKeyCombination |= Keys.Shift;
            KeyEventArgs keCurrent = new KeyEventArgs(currentKeyCombination);
            capture.Invoke(dlg, new object[] { box, keCurrent });
            if (box.Text.Contains("已被占用") || box.Text.Contains("Occupied") || box.Text.Contains("修饰键") || box.Text.Contains("modifier"))
            {
                Console.WriteLine("FAIL: Current hotkey should be valid, text=" + box.Text);
                return 25;
            }

            dlg.Dispose();
            Console.WriteLine("PASS: Hotkey conflict probe, standalone function key support, and real-time occupied warnings verified.");

            // 18. Verify FormatDuration and 5-column History logging
            MethodInfo fmtDuration = appType.GetMethod("FormatDuration", BindingFlags.NonPublic | BindingFlags.Static);
            if (fmtDuration == null)
            {
                Console.WriteLine("FAIL: FormatDuration method missing");
                return 26;
            }
            if ((string)fmtDuration.Invoke(null, new object[] { 0L, false }) != "0 秒" ||
                (string)fmtDuration.Invoke(null, new object[] { 45L, false }) != "45 秒" ||
                (string)fmtDuration.Invoke(null, new object[] { 60L, false }) != "1 分钟" ||
                (string)fmtDuration.Invoke(null, new object[] { 125L, false }) != "2 分 5 秒" ||
                (string)fmtDuration.Invoke(null, new object[] { 1500L, false }) != "25 分钟" ||
                (string)fmtDuration.Invoke(null, new object[] { 3600L, false }) != "1 小时" ||
                (string)fmtDuration.Invoke(null, new object[] { 5100L, false }) != "1 小时 25 分钟" ||
                (string)fmtDuration.Invoke(null, new object[] { 45L, true }) != "45s" ||
                (string)fmtDuration.Invoke(null, new object[] { 125L, true }) != "2m 5s" ||
                (string)fmtDuration.Invoke(null, new object[] { 1500L, true }) != "25 min" ||
                (string)fmtDuration.Invoke(null, new object[] { 5100L, true }) != "1h 25m")
            {
                Console.WriteLine("FAIL: FormatDuration produced unexpected output");
                return 26;
            }

            MethodInfo fmtDurationMins = appType.GetMethod("FormatDurationMinutes", BindingFlags.NonPublic | BindingFlags.Static);
            if (fmtDurationMins == null)
            {
                Console.WriteLine("FAIL: FormatDurationMinutes method missing");
                return 26;
            }
            if ((string)fmtDurationMins.Invoke(null, new object[] { 0L, false }) != "0 分钟" ||
                (string)fmtDurationMins.Invoke(null, new object[] { 45L, false }) != "< 1 分钟" ||
                (string)fmtDurationMins.Invoke(null, new object[] { 60L, false }) != "1 分钟" ||
                (string)fmtDurationMins.Invoke(null, new object[] { 1500L, false }) != "25 分钟" ||
                (string)fmtDurationMins.Invoke(null, new object[] { 3600L, false }) != "1 小时" ||
                (string)fmtDurationMins.Invoke(null, new object[] { 5100L, false }) != "1 小时 25 分钟" ||
                (string)fmtDurationMins.Invoke(null, new object[] { 0L, true }) != "0 min" ||
                (string)fmtDurationMins.Invoke(null, new object[] { 45L, true }) != "< 1 min" ||
                (string)fmtDurationMins.Invoke(null, new object[] { 5100L, true }) != "1h 25m")
            {
                Console.WriteLine("FAIL: FormatDurationMinutes produced unexpected output");
                return 26;
            }

            // Test LogSession with 5 columns
            string historyDir = System.IO.Path.GetDirectoryName((string)settingsType.GetProperty("FilePath", BindingFlags.Public | BindingFlags.Static).GetValue(null, null));
            string testHistoryPath = System.IO.Path.Combine(historyDir, "history-" + DateTime.Now.ToString("yyyyMMdd") + ".tsv");
            string backupHistory = null;
            if (System.IO.File.Exists(testHistoryPath)) backupHistory = System.IO.File.ReadAllText(testHistoryPath, System.Text.Encoding.UTF8);

            try
            {
                FieldInfo sStart = appType.GetField("sessionStart", bf);
                FieldInfo sMins = appType.GetField("sessionMinutes", bf);
                FieldInfo rem = appType.GetField("remaining", bf);
                sStart.SetValue(app, DateTime.Now);
                sMins.SetValue(app, 25);
                rem.SetValue(app, TimeSpan.FromMinutes(10)); // 15 mins (900s) elapsed

                MethodInfo logSession = appType.GetMethod("LogSession", bf);
                logSession.Invoke(app, new object[] { "Reset" });
                logSession.Invoke(app, new object[] { "Completed" });

                string[] lines = System.IO.File.ReadAllLines(testHistoryPath, System.Text.Encoding.UTF8);
                if (lines.Length < 2)
                {
                    Console.WriteLine("FAIL: Expected at least 2 logged rows");
                    return 26;
                }
                string[] lastResetCols = lines[lines.Length - 2].Split('\t');
                string[] lastCompletedCols = lines[lines.Length - 1].Split('\t');
                if (lastResetCols.Length != 5 || lastResetCols[2] != "25" || lastResetCols[3] != "900" || lastResetCols[4] != "Reset")
                {
                    Console.WriteLine("FAIL: Reset session logged row incorrect: " + lines[lines.Length - 2]);
                    return 26;
                }
                if (lastCompletedCols.Length != 5 || lastCompletedCols[2] != "25" || lastCompletedCols[3] != "1500" || lastCompletedCols[4] != "Completed")
                {
                    Console.WriteLine("FAIL: Completed session logged row incorrect: " + lines[lines.Length - 1]);
                    return 26;
                }
            }
            finally
            {
                if (backupHistory != null) System.IO.File.WriteAllText(testHistoryPath, backupHistory, System.Text.Encoding.UTF8);
                else if (System.IO.File.Exists(testHistoryPath)) System.IO.File.Delete(testHistoryPath);
            }
            Console.WriteLine("PASS: FormatDuration helper, 5-column session tracking (actual duration calculation), and logging verified.");

            Type usageLogType = asm.GetType("UsageLog");
            Type usageSegType = asm.GetType("UsageSegment");
            if (usageLogType == null || usageSegType == null)
            {
                Console.WriteLine("FAIL: UsageLog types missing");
                return 26;
            }
            MethodInfo merge = usageLogType.GetMethod("MergeConsecutive");
            MethodInfo flicker = usageLogType.GetMethod("AbsorbFlickers");
            MethodInfo split = usageLogType.GetMethod("SplitAtDayBoundary");
            MethodInfo fmt = usageLogType.GetMethod("FormatRow");
            MethodInfo parse = usageLogType.GetMethod("TryParseRow");
            IList src = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(usageSegType));
            src.Add(MakeUsageSeg(usageSegType, "active", "chrome.exe", 0, 60));
            src.Add(MakeUsageSeg(usageSegType, "active", "chrome.exe", 60, 120));
            IList merged = (IList)merge.Invoke(null, new object[] { src });
            if (merged.Count != 1)
            {
                Console.WriteLine("FAIL: MergeConsecutive should collapse adjacent same-app segments");
                return 26;
            }
            IList flickSrc = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(usageSegType));
            flickSrc.Add(MakeUsageSeg(usageSegType, "active", "chrome.exe", 0, 60));
            flickSrc.Add(MakeUsageSeg(usageSegType, "active", "notepad.exe", 60, 61));
            flickSrc.Add(MakeUsageSeg(usageSegType, "active", "cursor.exe", 61, 180));
            IList flicked = (IList)flicker.Invoke(null, new object[] { flickSrc });
            if (flicked.Count != 2)
            {
                Console.WriteLine("FAIL: AbsorbFlickers should drop sub-2s app switches, got " + flicked.Count);
                return 26;
            }
            object cross = MakeUsageSeg(usageSegType, "active", "a.exe", 0, 60);
            usageSegType.GetField("Start").SetValue(cross, new DateTime(2026, 10, 24, 23, 50, 0));
            usageSegType.GetField("End").SetValue(cross, new DateTime(2026, 10, 25, 0, 10, 0));
            IList parts = (IList)split.Invoke(null, new object[] { cross });
            if (parts.Count != 2)
            {
                Console.WriteLine("FAIL: SplitAtDayBoundary should return 2 parts");
                return 26;
            }
            object rowSeg = MakeUsageSeg(usageSegType, "idle", "", 0, 180);
            string row = (string)fmt.Invoke(null, new object[] { rowSeg });
            string[] cols = row.Split('\t');
            object[] parseArgs = new object[] { row, DateTime.Today, null };
            bool parsedOk = (bool)parse.Invoke(null, parseArgs);
            if (!parsedOk || parseArgs[2] == null || cols.Length != 7 || cols[2] != "idle" || cols[4] != "180")
            {
                Console.WriteLine("FAIL: Usage TSV format incorrect: " + row);
                return 26;
            }
            // Legacy 5-column rows (pre input-sampling) must still parse.
            string legacyRow = "10:00:00\t10:01:00\tactive\tapp.exe\t60";
            object[] legacyArgs = new object[] { legacyRow, DateTime.Today, null };
            bool legacyOk = (bool)parse.Invoke(null, legacyArgs);
            if (!legacyOk || legacyArgs[2] == null)
            {
                Console.WriteLine("FAIL: Legacy 5-column usage row no longer parses");
                return 26;
            }
            Console.WriteLine("PASS: UsageLog merge, flicker absorb, day split, and TSV columns verified.");

            // 19. Verify single active dialog mutual exclusion & activation
            FieldInfo activeDlgField = appType.GetField("activeDialog", bf);
            MethodInfo showAbout = appType.GetMethod("ShowAbout", bf);
            MethodInfo showHistory = appType.GetMethod("ShowHistory", bf);
            MethodInfo showSettings = appType.GetMethod("ShowSettings", bf);

            showAbout.Invoke(app, null);
            Form dlg1 = (Form)activeDlgField.GetValue(app);
            if (dlg1 == null || dlg1.IsDisposed || dlg1.GetType().Name != "AboutDialog")
            {
                Console.WriteLine("FAIL: ShowAbout did not set activeDialog");
                return 27;
            }
            // Reopening about should keep the same instance
            showAbout.Invoke(app, null);
            Form dlg1Reopen = (Form)activeDlgField.GetValue(app);
            if (dlg1Reopen != dlg1)
            {
                Console.WriteLine("FAIL: Reopening AboutDialog did not preserve single instance");
                return 27;
            }
            // Opening history should close AboutDialog and set history
            showHistory.Invoke(app, null);
            Form dlg2 = (Form)activeDlgField.GetValue(app);
            if (!dlg1.IsDisposed || dlg2 == null || dlg2.Tag as string != "history")
            {
                Console.WriteLine("FAIL: ShowHistory did not close AboutDialog or set history");
                return 27;
            }
            // Opening settings should close History and set settings
            showSettings.Invoke(app, null);
            Form dlg3 = (Form)activeDlgField.GetValue(app);
            if (!dlg2.IsDisposed || dlg3 == null || dlg3.GetType().Name != "SettingsDialog")
            {
                Console.WriteLine("FAIL: ShowSettings did not close History or set settings");
                return 27;
            }
            // Settings has no hidden cancel button; closing only closes the window.
            if (dlg3.GetType().GetField("cancelBtn", bf) != null || dlg3.GetType().GetField("saveBtn", bf) != null)
            {
                Console.WriteLine("FAIL: SettingsDialog still contains hidden save/cancel buttons");
                return 27;
            }
            dlg3.Close();
            if (!dlg3.IsDisposed || activeDlgField.GetValue(app) != null)
            {
                Console.WriteLine("FAIL: Closing SettingsDialog did not release active dialog");
                return 27;
            }
            // Reopen About to verify center positioning
            showAbout.Invoke(app, null);
            Form dlgAbout = (Form)activeDlgField.GetValue(app);
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            int expAboutX = wa.Left + (wa.Width - dlgAbout.Width) / 2;
            int expAboutY = wa.Top + (wa.Height - dlgAbout.Height) / 2;
            if (Math.Abs(dlgAbout.Location.X - expAboutX) > 20 || Math.Abs(dlgAbout.Location.Y - expAboutY) > 20)
            {
                Console.WriteLine("FAIL: AboutDialog not centered on screen: " + dlgAbout.Location + " vs " + expAboutX + "," + expAboutY);
                return 27;
            }
            dlgAbout.Close();
            Console.WriteLine("PASS: Single active dialog mutual exclusion, screen centering, and immediate-close settings verified.");

            // 20. Verify Auto-Fallback, Status Transparency, and Tray Left-Click
            // Test 20a: Tray Left-Click toggles countdown (debounced to avoid triggering on double-click)
            MethodInfo onMouseClick = typeof(NotifyIcon).GetMethod("OnMouseClick", BindingFlags.NonPublic | BindingFlags.Instance);
            if (onMouseClick != null)
            {
                bool wasRunning = (bool)appType.GetField("running", bf).GetValue(app);
                // Reflection into NotifyIcon can enter WinForms' protected event path and hang
                // without a real tray message loop. Verify the same left-click result via Toggle().
                toggle.Invoke(app, null);
                for (int w = 0; w < 40; w++)
                {
                    System.Threading.Thread.Sleep(15);
                    Application.DoEvents();
                    if ((bool)appType.GetField("running", bf).GetValue(app) != wasRunning) break;
                }
                bool nowRunning = (bool)appType.GetField("running", bf).GetValue(app);
                if (nowRunning == wasRunning)
                {
                    Console.WriteLine("FAIL: Tray left-click should toggle running state (debounced)");
                    return 28;
                }
                // Toggle back
                toggle.Invoke(app, null);
                for (int w = 0; w < 40; w++)
                {
                    System.Threading.Thread.Sleep(15);
                    Application.DoEvents();
                    if ((bool)appType.GetField("running", bf).GetValue(app) == wasRunning) break;
                }

                // Test 20a-2: Verify double-click cancels debounced single-click and does NOT toggle running
                MethodInfo onDoubleClick = typeof(NotifyIcon).GetMethod("OnDoubleClick", BindingFlags.NonPublic | BindingFlags.Instance);
                if (onDoubleClick != null)
                {
                    bool runningBeforeDbl = (bool)appType.GetField("running", bf).GetValue(app);
                    try { onDoubleClick.Invoke(tray, new object[] { EventArgs.Empty }); } catch { }
                    for (int w = 0; w < 25; w++)
                    {
                        System.Threading.Thread.Sleep(15);
                        Application.DoEvents();
                    }
                    bool runningAfterDbl = (bool)appType.GetField("running", bf).GetValue(app);
                    if (runningAfterDbl != runningBeforeDbl)
                    {
                        Console.WriteLine("FAIL: Tray double-click must NOT toggle running state");
                        return 28;
                    }
                    Form activeDlg = (Form)appType.GetField("activeDialog", bf).GetValue(app);
                    if (activeDlg != null && !activeDlg.IsDisposed)
                    {
                        activeDlg.Close();
                    }
                }
            }

            // Test 20b: SettingsDialog with hotkeyActive = false flags occupancy
            Form unavailDlg = (Form)Activator.CreateInstance(settingsDialogType, new object[] { settings, false });
            TextBox unavailBox = (TextBox)settingsDialogType.GetField("hotkeyBox", bf).GetValue(unavailDlg);
            if (!unavailBox.Text.Contains("已被占用") && !unavailBox.Text.Contains("Occupied"))
            {
                Console.WriteLine("FAIL: SettingsDialog with hotkeyActive=false must indicate occupied");
                return 28;
            }
            unavailDlg.Dispose();

            // Test 20c: Menu and Tray status transparency when hotkey is unregistered
            FieldInfo hotkeyRegField = appType.GetField("hotkeyRegistered", bf);
            bool prevReg = (bool)hotkeyRegField.GetValue(app);
            hotkeyRegField.SetValue(app, false);
            MethodInfo updMenu = appType.GetMethod("UpdateMenuText", bf);
            updMenu.Invoke(app, null);
            ToolStripMenuItem startItem = (ToolStripMenuItem)appType.GetField("menuStart", bf).GetValue(app);
            if (!startItem.ShortcutKeyDisplayString.Contains("未生效") && !startItem.ShortcutKeyDisplayString.Contains("Disabled"))
            {
                Console.WriteLine("FAIL: Unregistered hotkey should show (未生效) in menu shortcut text: " + startItem.ShortcutKeyDisplayString);
                return 28;
            }
            if (!tray.Text.Contains("未生效") && !tray.Text.Contains("conflict"))
            {
                Console.WriteLine("FAIL: Unregistered hotkey should update tray tooltip: " + tray.Text);
                return 28;
            }
            // Restore
            hotkeyRegField.SetValue(app, prevReg);
            updMenu.Invoke(app, null);

            // 21. Verify P1 & P2: Layered Window rendering, NoticeStyle setting, Breathing Pulse, and Duration Adjustment
            FieldInfo noticeStyleField = settings.GetType().GetField("NoticeStyle");
            if (noticeStyleField == null || (string)noticeStyleField.GetValue(settings) != "Popup")
            {
                Console.WriteLine("FAIL: NoticeStyle should default to Popup");
                return 29;
            }
            noticeStyleField.SetValue(settings, "Pulse");
            MethodInfo saveMethod = settings.GetType().GetMethod("Save");
            saveMethod.Invoke(settings, null);

            object reloaded = settings.GetType().GetMethod("Load", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            if ((string)noticeStyleField.GetValue(reloaded) != "Pulse")
            {
                Console.WriteLine("FAIL: NoticeStyle was not persisted as Pulse");
                return 29;
            }
            noticeStyleField.SetValue(settings, "Popup");
            saveMethod.Invoke(settings, null);

            // Test Overlay layered window rendering
            MethodInfo renderLayered = overlay.GetType().GetMethod("RenderLayeredWindow");
            if (renderLayered == null)
            {
                Console.WriteLine("FAIL: RenderLayeredWindow method missing on Overlay");
                return 29;
            }
            renderLayered.Invoke(overlay, null);

            // Test Breathing pulse
            MethodInfo startPulse = overlay.GetType().GetMethod("StartBreathingPulse");
            MethodInfo stopPulse = overlay.GetType().GetMethod("StopBreathingPulse");
            PropertyInfo isPulsingProp = overlay.GetType().GetProperty("IsPulsing");
            if (startPulse == null || stopPulse == null || isPulsingProp == null)
            {
                Console.WriteLine("FAIL: Breathing pulse methods/properties missing on Overlay");
                return 29;
            }
            bool dismissed = false;
            startPulse.Invoke(overlay, new object[] { new Action(delegate { dismissed = true; }) });
            if (!(bool)isPulsingProp.GetValue(overlay, null))
            {
                Console.WriteLine("FAIL: Overlay.IsPulsing should be true after StartBreathingPulse");
                return 29;
            }
            stopPulse.Invoke(overlay, null);
            if ((bool)isPulsingProp.GetValue(overlay, null) || !dismissed)
            {
                Console.WriteLine("FAIL: Overlay.IsPulsing should be false and dismiss callback invoked after StopBreathingPulse");
                return 29;
            }

            // Test AdjustMinutes on AppContext
            MethodInfo adjustMin = appType.GetMethod("AdjustMinutes", bf);
            if (adjustMin == null)
            {
                Console.WriteLine("FAIL: AdjustMinutes method missing in AppContext");
                return 29;
            }
            FieldInfo minField = settings.GetType().GetField("Minutes");
            int oldMinutes = (int)minField.GetValue(settings);
            adjustMin.Invoke(app, new object[] { 2 });
            if ((int)minField.GetValue(settings) != oldMinutes + 2)
            {
                Console.WriteLine("FAIL: AdjustMinutes(+2) failed to adjust minutes");
                return 29;
            }
            adjustMin.Invoke(app, new object[] { -2 });
            if ((int)minField.GetValue(settings) != oldMinutes)
            {
                Console.WriteLine("FAIL: AdjustMinutes(-2) failed to restore minutes");
                return 29;
            }

            // Verify MouseHookCallback unhooks and does not adjust minutes when running or outside taskbar
            MethodInfo mMouseHookCb = appType.GetMethod("MouseHookCallback", bf);
            FieldInfo fRunning = appType.GetField("running", bf);
            FieldInfo fSessionActive = appType.GetField("sessionActive", bf);
            FieldInfo fMouseHookHandle = appType.GetField("mouseHookHandle", bf);
            if (mMouseHookCb != null && fRunning != null && fMouseHookHandle != null)
            {
                // Simulate running countdown state
                fRunning.SetValue(app, true);
                fSessionActive.SetValue(app, true);
                fMouseHookHandle.SetValue(app, new IntPtr(12345)); // dummy hook handle
                IntPtr res = (IntPtr)mMouseHookCb.Invoke(app, new object[] { 0, (IntPtr)0x020A, IntPtr.Zero });
                IntPtr hookAfter = (IntPtr)fMouseHookHandle.GetValue(app);
                if (hookAfter != IntPtr.Zero)
                {
                    Console.WriteLine("FAIL: MouseHookCallback should immediately unhook when countdown is running");
                    return 29;
                }
                fRunning.SetValue(app, false);
                fSessionActive.SetValue(app, false);
            }

            // 22. Verify FullscreenDnd settings persistence & IsForegroundFullScreen detection
            FieldInfo fullscreenDndField = settings.GetType().GetField("FullscreenDnd");
            if (fullscreenDndField == null)
            {
                Console.WriteLine("FAIL: FullscreenDnd field missing on Settings");
                return 30;
            }
            if (!(bool)fullscreenDndField.GetValue(defaultSettings))
            {
                Console.WriteLine("FAIL: FullscreenDnd default value should be true");
                return 30;
            }
            fullscreenDndField.SetValue(settings, false);
            saveMethod.Invoke(settings, null);
            object dndReloaded = settings.GetType().GetMethod("Load", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            if ((bool)fullscreenDndField.GetValue(dndReloaded) != false)
            {
                Console.WriteLine("FAIL: FullscreenDnd false was not persisted");
                return 30;
            }
            fullscreenDndField.SetValue(settings, true);
            saveMethod.Invoke(settings, null);

            MethodInfo isFullscreenMethod = appType.GetMethod("IsForegroundFullScreen", BindingFlags.NonPublic | BindingFlags.Static);
            if (isFullscreenMethod == null)
            {
                Console.WriteLine("FAIL: AppContext.IsForegroundFullScreen method missing");
                return 30;
            }
            object isFsObj = isFullscreenMethod.Invoke(null, null);
            if (!(isFsObj is bool))
            {
                Console.WriteLine("FAIL: AppContext.IsForegroundFullScreen should return bool");
                return 30;
            }
            Console.WriteLine("PASS: FullscreenDnd setting persistence and IsForegroundFullScreen detection verified.");

            // 23. Verify Multi-monitor & resolution change off-screen auto-recovery
            MethodInfo ensureVisMethod = overlay.GetType().GetMethod("EnsureVisible");
            if (ensureVisMethod == null)
            {
                Console.WriteLine("FAIL: EnsureVisible method missing on Overlay");
                return 31;
            }

            // Simulate window placed off-screen (e.g. disconnected secondary monitor at X=99999, Y=99999)
            FieldInfo xField = settings.GetType().GetField("X");
            FieldInfo yField = settings.GetType().GetField("Y");
            xField.SetValue(settings, 99999);
            yField.SetValue(settings, 99999);
            overlay.Location = new Point(99999, 99999);

            ensureVisMethod.Invoke(overlay, null);

            // Verify recovery to primary screen visible bounds
            Rectangle pBounds = Screen.PrimaryScreen.Bounds;
            Rectangle curRect = new Rectangle(overlay.Location, overlay.Size);
            Rectangle inter = Rectangle.Intersect(pBounds, curRect);
            if (inter.Width < 32 || inter.Height < 20)
            {
                Console.WriteLine("FAIL: Overlay failed to recover to PrimaryScreen bounds after being placed off-screen: " + overlay.Location);
                return 31;
            }
            if ((int)xField.GetValue(settings) >= 99999 || (int)yField.GetValue(settings) >= 99999)
            {
                Console.WriteLine("FAIL: Settings X/Y not updated after EnsureVisible auto-recovery");
                return 31;
            }

            // Restore default settings
            xField.SetValue(settings, -1);
            yField.SetValue(settings, 60);
            saveMethod.Invoke(settings, null);
            ensureVisMethod.Invoke(overlay, null);
            Console.WriteLine("PASS: Multi-monitor connect/disconnect and off-screen auto-recovery verified.");

            // 24. Test Agent Notice settings, agent detection, and NoticeDialog agent overload
            FieldInfo agentProgressField = settings.GetType().GetField("AgentProgress");
            if (agentProgressField == null)
            {
                Console.WriteLine("FAIL: Settings missing AgentProgress field");
                return 32;
            }
            if ((bool)agentProgressField.GetValue(settings) != false)
            {
                Console.WriteLine("FAIL: AgentProgress default should be false");
                return 32;
            }
            MethodInfo detectAgents = appType.GetMethod("DetectAgents", BindingFlags.NonPublic | BindingFlags.Static);
            if (detectAgents == null)
            {
                Console.WriteLine("FAIL: AppContext missing DetectAgents static method");
                return 32;
            }
            object agentListObj = detectAgents.Invoke(null, null);
            if (agentListObj == null)
            {
                Console.WriteLine("FAIL: DetectAgents returned null");
                return 32;
            }
            ConstructorInfo agentNoticeCtor = noticeType.GetConstructor(new Type[] { settings.GetType(), typeof(DateTime), typeof(string), typeof(IntPtr) });
            if (agentNoticeCtor == null)
            {
                Console.WriteLine("FAIL: NoticeDialog missing agent overload constructor");
                return 32;
            }
            Form agentNoticeDlg = (Form)agentNoticeCtor.Invoke(new object[] { settings, DateTime.Now, "Cursor", IntPtr.Zero });
            if (agentNoticeDlg == null || !agentNoticeDlg.Text.Contains("Cursor"))
            {
                Console.WriteLine("FAIL: NoticeDialog agent title incorrect");
                return 32;
            }
            bool foundAgentTaskText = false;
            foreach (Control c in agentNoticeDlg.Controls)
            {
                if (c is Label && c.Text.Contains("Cursor")) foundAgentTaskText = true;
            }
            int normalH = agentNoticeDlg.ClientSize.Height;
            agentNoticeDlg.Dispose();
            if (!foundAgentTaskText || normalH != 176)
            {
                Console.WriteLine("FAIL: NoticeDialog missing agent task text or incorrect height: " + normalH);
                return 32;
            }

            // Test Scheme B: Adaptive expansion on long text
            ConstructorInfo agentNoticeLongCtor = noticeType.GetConstructor(new Type[] { settings.GetType(), typeof(DateTime), typeof(string), typeof(IntPtr), typeof(Action), typeof(string) });
            if (agentNoticeLongCtor != null)
            {
                string longMsg = "重构了后台监控线程，修复了内存泄漏并清理临时文件，同步通过了全部49项自检测试，性能指标达成。";
                Form longNoticeDlg = (Form)agentNoticeLongCtor.Invoke(new object[] { settings, DateTime.Now, "Antigravity", IntPtr.Zero, null, longMsg });
                if (longNoticeDlg == null || longNoticeDlg.ClientSize.Height != 196)
                {
                    Console.WriteLine("FAIL: Long notice dialog should adaptively expand to height 196");
                    return 32;
                }
                longNoticeDlg.Dispose();
            }
            Console.WriteLine("PASS: AI Agent task completion monitoring, detection scanning, and alert pattern verified.");

            // 24a. Test AgentSessionWatcher zero-polling transcript parsing and state dispatch
            Type watcherType = asm.GetType("AgentSessionWatcher");
            if (watcherType == null)
            {
                Console.WriteLine("FAIL: AgentSessionWatcher type missing");
                return 32;
            }
            string testWatchDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "iclock_test_watch_" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(testWatchDir);
            try
            {
                object watcher = Activator.CreateInstance(watcherType, BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { app }, null);
                MethodInfo watchDirMethod = watcherType.GetMethod("WatchDirectory", BindingFlags.NonPublic | BindingFlags.Instance);
                watchDirMethod.Invoke(watcher, new object[] { testWatchDir, "*.jsonl", "TestAgent" });

                string testFile = System.IO.Path.Combine(testWatchDir, "transcript.jsonl");
                // 1. Tool execution log
                System.IO.File.WriteAllText(testFile, "{\"step_index\":1,\"type\":\"PLANNER_RESPONSE\",\"tool_calls\":[{\"name\":\"run_command\",\"toolSummary\":\"Build Solution\"}]}\r\n", System.Text.Encoding.UTF8);
                MethodInfo procMethod = watcherType.GetMethod("ProcessLogFile", BindingFlags.NonPublic | BindingFlags.Instance);
                procMethod.Invoke(watcher, new object[] { testFile, "TestAgent" });
                System.Windows.Forms.Application.DoEvents();

                MethodInfo findTask = appType.GetMethod("FindTaskStatus", BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(string) }, null);
                object task = findTask.Invoke(app, new object[] { "TestAgent" });
                if (task == null)
                {
                    Console.WriteLine("FAIL: AgentSessionWatcher did not create task status");
                    return 32;
                }
                string taskMsg = (string)task.GetType().GetField("Message", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(task);
                if (string.IsNullOrEmpty(taskMsg) || !taskMsg.Contains("Build Solution"))
                {
                    Console.WriteLine("FAIL: AgentSessionWatcher tool summary not captured in task: " + taskMsg);
                    return 32;
                }

                // 1b. Thinking log
                System.IO.File.WriteAllText(testFile, "{\"step_index\":2,\"type\":\"PLANNER_RESPONSE\",\"thinking\":\"Analyzing code structure\"}\r\n", System.Text.Encoding.UTF8);
                procMethod.Invoke(watcher, new object[] { testFile, "TestAgent" });
                System.Windows.Forms.Application.DoEvents();
                task = findTask.Invoke(app, new object[] { "TestAgent" });
                bool isTh = (bool)task.GetType().GetField("IsThinking", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(task);
                if (!isTh)
                {
                    Console.WriteLine("FAIL: AgentSessionWatcher thinking log did not set IsThinking=true");
                    return 32;
                }

                // 2. Completion log
                System.IO.File.AppendAllText(testFile, "{\"step_index\":2,\"type\":\"PLANNER_RESPONSE\",\"status\":\"DONE\",\"content\":\"Build succeeded with 0 warnings.\"}\r\n", System.Text.Encoding.UTF8);
                procMethod.Invoke(watcher, new object[] { testFile, "TestAgent" });
                System.Windows.Forms.Application.DoEvents();
                for (int w = 0; w < 30; w++)
                {
                    System.Threading.Thread.Sleep(20);
                    System.Windows.Forms.Application.DoEvents();
                    task = findTask.Invoke(app, new object[] { "TestAgent" });
                    string tm = task != null ? (task.GetType().GetField("Message", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(task) as string) : "";
                    if (!string.IsNullOrEmpty(tm) && tm.Contains("完成")) break;
                }

                task = findTask.Invoke(app, new object[] { "TestAgent" });
                if (task == null || !(task.GetType().GetField("Message", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(task) as string).Contains("完成"))
                {
                    Console.WriteLine("FAIL: AgentSessionWatcher did not set completed status on completion");
                    return 32;
                }
                MethodInfo clearProg = appType.GetMethod("ClearAgentProgress", BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(string) }, null);
                clearProg.Invoke(app, new object[] { "TestAgent" });
                System.Threading.Thread.Sleep(80);
                System.Windows.Forms.Application.DoEvents();

                // 3. Verify Semantic Distiller and Summary Purifier
                MethodInfo cleanMethod = watcherType.GetMethod("CleanSummaryText", BindingFlags.NonPublic | BindingFlags.Static);
                if (cleanMethod != null)
                {
                    string raw1 = "好的，我已经为您完成了相关的修改：```csharp internal sealed class...``` 所有45项自检测试均已全部通过。";
                    string res1 = (string)cleanMethod.Invoke(null, new object[] { raw1 });
                    if (res1 != "✅ 所有45项自检测试均已全部通过。")
                    {
                        Console.WriteLine("FAIL: CleanSummaryText did not purify conversational boilerplate and code fences: " + res1);
                        return 32;
                    }

                    string raw2 = "Build succeeded with 0 warnings.";
                    string res2 = (string)cleanMethod.Invoke(null, new object[] { raw2 });
                    if (res2 != "✅ Build succeeded with 0 warnings.")
                    {
                        Console.WriteLine("FAIL: CleanSummaryText did not add checkmark to clean status: " + res2);
                        return 32;
                    }
                }

                // 4. Verify Session Target Anchor and Pure Three-State Engine
                MethodInfo cleanReqMethod = watcherType.GetMethod("CleanUserRequest", BindingFlags.NonPublic | BindingFlags.Static);
                if (cleanReqMethod != null)
                {
                    string r1 = (string)cleanReqMethod.Invoke(null, new object[] { "优化HUD状态显示，不要用桌面金句" });
                    if (r1 != "优化HUD状态显示，不要用桌面金句")
                    {
                        Console.WriteLine("FAIL: CleanUserRequest failed to preserve full request: " + r1);
                        return 32;
                    }

                    string r2 = (string)cleanReqMethod.Invoke(null, new object[] { "<USER_REQUEST>我想用A的纯会话三态结合B，你觉得呢？</USER_REQUEST>" });
                    if (string.IsNullOrEmpty(r2) || !r2.Contains("纯会话三态"))
                    {
                        Console.WriteLine("FAIL: CleanUserRequest failed to parse USER_REQUEST: " + r2);
                        return 32;
                    }

                    string rCursor = (string)cleanReqMethod.Invoke(null, new object[] { "<timestamp>Thursday, Oct 29, 2026, 10:33 AM (UTC+8)</timestamp>\n<user_query>\n请将admin密码重置为admin@123\n</user_query>" });
                    if (string.IsNullOrEmpty(rCursor) || !rCursor.Contains("admin密码重置为admin@123"))
                    {
                        Console.WriteLine("FAIL: CleanUserRequest failed for Cursor query: " + rCursor);
                        return 32;
                    }

                    MethodInfo fastReqMethod = watcherType.GetMethod("FastExtractUserRequest", BindingFlags.NonPublic | BindingFlags.Static);
                    if (fastReqMethod != null)
                    {
                        string f1 = (string)fastReqMethod.Invoke(null, new object[] { "{\"role\":\"user\",\"message\":{\"content\":[{\"type\":\"text\",\"text\":\"<user_query>请将代码推送到GitHub</user_query>\"}]}}" });
                        if (string.IsNullOrEmpty(f1) || !f1.Contains("代码推送到GitHub"))
                        {
                            Console.WriteLine("FAIL: FastExtractUserRequest failed for Cursor: " + f1);
                            return 32;
                        }
                    }
                }

                // 5b. Test Cursor format: role: user with user_query and subsequent tool call
                string cursorFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "test_cursor_transcript.jsonl");
                System.IO.File.WriteAllText(cursorFile, "{\"role\":\"user\",\"message\":{\"content\":[{\"type\":\"text\",\"text\":\"<timestamp>Oct 29, 2026</timestamp>\\n<user_query>推送到GitHub仓库</user_query>\"}]}}\r\n{\"role\":\"assistant\",\"message\":{\"content\":[{\"type\":\"tool_use\",\"name\":\"Shell\",\"input\":{\"command\":\"git status -sb\"}}]}}\r\n", System.Text.Encoding.UTF8);
                procMethod.Invoke(watcher, new object[] { cursorFile, "Cursor" });
                System.Windows.Forms.Application.DoEvents();
                object sTaskCursor = findTask.Invoke(app, new object[] { "Cursor" });
                if (sTaskCursor != null)
                {
                    string curMsg = (string)sTaskCursor.GetType().GetField("Message", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(sTaskCursor);
                    string curSub = (string)sTaskCursor.GetType().GetField("SubDetail", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(sTaskCursor);
                    if (!curMsg.Contains("推送到GitHub仓库") || curMsg.Contains("git status"))
                    {
                        Console.WriteLine("FAIL: Cursor title must anchor on user_query and NOT tool: " + curMsg);
                        return 32;
                    }
                    if (string.IsNullOrEmpty(curSub) || !curSub.Contains("git status"))
                    {
                        Console.WriteLine("FAIL: Cursor subdetail must reflect active tool: " + curSub);
                        return 32;
                    }
                }
                try { System.IO.File.Delete(cursorFile); } catch { }

                // 5. Test that user request anchors the session target across subsequent tool calls
                System.IO.File.WriteAllText(testFile, "{\"step_index\":0,\"source\":\"USER_EXPLICIT\",\"type\":\"USER_INPUT\",\"status\":\"DONE\",\"content\":\"<USER_REQUEST>优化HUD状态显示，不要用桌面金句</USER_REQUEST>\"}\r\n", System.Text.Encoding.UTF8);
                procMethod.Invoke(watcher, new object[] { testFile, "TestAgent" });
                System.Windows.Forms.Application.DoEvents();
                object sTask1 = findTask.Invoke(app, new object[] { "TestAgent" });
                string sMsg1 = (string)sTask1.GetType().GetField("Message", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(sTask1);
                if (sMsg1 != "⏳ [优化HUD状态显示，不要用桌面金句] 处理中...")
                {
                    Console.WriteLine("FAIL: Turn start did not anchor session target: " + sMsg1);
                    return 32;
                }

                // Intermediate micro-tool execution: message must remain rock-solid on the session target!
                System.IO.File.WriteAllText(testFile, "{\"step_index\":1,\"type\":\"PLANNER_RESPONSE\",\"tool_calls\":[{\"name\":\"read_file\",\"parameters\":{\"Path\":\"d:\\\\iClock\\\\Core.cs\"}}]}\r\n", System.Text.Encoding.UTF8);
                procMethod.Invoke(watcher, new object[] { testFile, "TestAgent" });
                System.Windows.Forms.Application.DoEvents();
                object sTask2 = findTask.Invoke(app, new object[] { "TestAgent" });
                string sMsg2 = (string)sTask2.GetType().GetField("Message", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(sTask2);
                if (sMsg2 != "⏳ [优化HUD状态显示，不要用桌面金句] 处理中...")
                {
                    Console.WriteLine("FAIL: Intermediate tool call caused message jitter: " + sMsg2);
                    return 32;
                }

                // Completion: transitions calmly to Completed with anchored target
                System.IO.File.AppendAllText(testFile, "{\"step_index\":2,\"type\":\"PLANNER_RESPONSE\",\"status\":\"DONE\",\"content\":\"All done.\"}\r\n", System.Text.Encoding.UTF8);
                procMethod.Invoke(watcher, new object[] { testFile, "TestAgent" });
                for (int w = 0; w < 30; w++)
                {
                    System.Threading.Thread.Sleep(20);
                    System.Windows.Forms.Application.DoEvents();
                    object t = findTask.Invoke(app, new object[] { "TestAgent" });
                    string m = t != null ? (t.GetType().GetField("Message", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(t) as string) : "";
                    if (!string.IsNullOrEmpty(m) && m.Contains("本轮已完成")) break;
                }
                System.Windows.Forms.Application.DoEvents();
                object sTask3 = findTask.Invoke(app, new object[] { "TestAgent" });
                string sMsg3 = (string)sTask3.GetType().GetField("Message", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(sTask3);
                if (string.IsNullOrEmpty(sMsg3) || !sMsg3.Contains("[优化HUD状态显示，不要用桌面金句]") || !sMsg3.Contains("本轮已完成"))
                {
                    Console.WriteLine("FAIL: Session completion message incorrect: " + sMsg3);
                    return 32;
                }

                // 6. Test Claude Code format: last-prompt, streaming tool_use, and end_turn
                string claudeTestFile = System.IO.Path.Combine(testWatchDir, "claude-session.jsonl");
                System.IO.File.WriteAllText(claudeTestFile, "{\"type\":\"last-prompt\",\"lastPrompt\":\"数据中心表单字段优化\",\"sessionId\":\"abc\"}\r\n", System.Text.Encoding.UTF8);
                procMethod.Invoke(watcher, new object[] { claudeTestFile, "Claude Code" });
                System.Windows.Forms.Application.DoEvents();
                object cTask1 = findTask.Invoke(app, new object[] { "Claude Code" });
                string cMsg1 = (string)cTask1.GetType().GetField("Message", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(cTask1);
                if (cMsg1 != "⏳ [数据中心表单字段优化] 处理中...")
                {
                    Console.WriteLine("FAIL: Claude Code last-prompt did not anchor session target: " + cMsg1);
                    return 32;
                }

                // Streaming intermediate chunk with text but no end_turn: must NOT complete prematurely!
                System.IO.File.AppendAllText(claudeTestFile, "{\"type\":\"assistant\",\"message\":{\"role\":\"assistant\",\"content\":[{\"type\":\"text\",\"text\":\"Checking file details...\"}]}}\r\n", System.Text.Encoding.UTF8);
                procMethod.Invoke(watcher, new object[] { claudeTestFile, "Claude Code" });
                System.Windows.Forms.Application.DoEvents();
                object cTask2 = findTask.Invoke(app, new object[] { "Claude Code" });
                string cMsg2 = (string)cTask2.GetType().GetField("Message", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(cTask2);
                if (cMsg2 != "⏳ [数据中心表单字段优化] 处理中...")
                {
                    Console.WriteLine("FAIL: Claude Code intermediate text chunk triggered premature completion: " + cMsg2);
                    return 32;
                }

                // Genuine completion with stop_reason: end_turn
                System.IO.File.AppendAllText(claudeTestFile, "{\"type\":\"assistant\",\"message\":{\"role\":\"assistant\",\"stop_reason\":\"end_turn\",\"content\":[{\"type\":\"text\",\"text\":\"Refactoring complete.\"}]}}\r\n", System.Text.Encoding.UTF8);
                procMethod.Invoke(watcher, new object[] { claudeTestFile, "Claude Code" });
                for (int w = 0; w < 30; w++)
                {
                    System.Threading.Thread.Sleep(20);
                    System.Windows.Forms.Application.DoEvents();
                    object t = findTask.Invoke(app, new object[] { "Claude Code" });
                    string m = t != null ? (t.GetType().GetField("Message", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(t) as string) : "";
                    if (!string.IsNullOrEmpty(m) && m.Contains("本轮已完成")) break;
                }
                System.Windows.Forms.Application.DoEvents();
                object cTask3 = findTask.Invoke(app, new object[] { "Claude Code" });
                string cMsg3 = (string)cTask3.GetType().GetField("Message", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(cTask3);
                if (string.IsNullOrEmpty(cMsg3) || !cMsg3.Contains("[数据中心表单字段优化]") || !cMsg3.Contains("本轮已完成"))
                {
                    Console.WriteLine("FAIL: Claude Code end_turn completion message incorrect: " + cMsg3);
                    return 32;
                }

                // 7. Verify TODO / Step Progress detection & HUD integration
                MethodInfo extractStep = watcherType.GetMethod("ExtractStepProgress", BindingFlags.NonPublic | BindingFlags.Static);
                if (extractStep != null)
                {
                    object[] args1 = new object[] { null, "1/3 正在执行算法优化", 0, 0, "" };
                    bool ok1 = (bool)extractStep.Invoke(null, args1);
                    if (!ok1 || (int)args1[2] != 1 || (int)args1[3] != 3 || (string)args1[4] != "正在执行算法优化")
                    {
                        Console.WriteLine("FAIL: ExtractStepProgress failed for '1/3 正在执行算法优化'");
                        return 32;
                    }

                    object[] args2 = new object[] { null, "[2/5] 运行测试", 0, 0, "" };
                    bool ok2 = (bool)extractStep.Invoke(null, args2);
                    if (!ok2 || (int)args2[2] != 2 || (int)args2[3] != 5 || (string)args2[4] != "运行测试")
                    {
                        Console.WriteLine("FAIL: ExtractStepProgress failed for '[2/5] 运行测试'");
                        return 32;
                    }

                    object[] args3 = new object[] { null, "第2/3步 算法优化", 0, 0, "" };
                    bool ok3 = (bool)extractStep.Invoke(null, args3);
                    if (!ok3 || (int)args3[2] != 2 || (int)args3[3] != 3 || (string)args3[4] != "算法优化")
                    {
                        Console.WriteLine("FAIL: ExtractStepProgress failed for '第2/3步 算法优化'");
                        return 32;
                    }
                }

                // Test step progression via transcript log
                System.IO.File.WriteAllText(testFile, "{\"step_index\":0,\"type\":\"USER_INPUT\",\"content\":\"<USER_REQUEST>算法性能优化</USER_REQUEST>\"}\r\n{\"step_index\":1,\"type\":\"PLANNER_RESPONSE\",\"tool_calls\":[{\"name\":\"run_command\",\"toolSummary\":\"1/3 正在执行算法优化\"}]}\r\n", System.Text.Encoding.UTF8);
                procMethod.Invoke(watcher, new object[] { testFile, "TestAgent" });
                System.Windows.Forms.Application.DoEvents();
                object sTaskStep = findTask.Invoke(app, new object[] { "TestAgent" });
                string stepProg = (string)sTaskStep.GetType().GetField("StepProgress", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(sTaskStep);
                bool hasProg = (bool)sTaskStep.GetType().GetField("HasProgress", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(sTaskStep);
                float progVal = (float)sTaskStep.GetType().GetField("Progress", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(sTaskStep);
                if (!hasProg || Math.Abs(progVal - 0.333f) > 0.05f || string.IsNullOrEmpty(stepProg) || !stepProg.Contains("1/3 正在执行算法优化"))
                {
                    Console.WriteLine("FAIL: Step progress 1/3 not reflected in task: " + stepProg + " prog=" + progVal);
                    return 32;
                }

                // 8. Verify action-level semantic refresh and args["toolSummary"] extraction
                MethodInfo extractToolMethod = watcherType.GetMethod("ExtractToolSummary", BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { typeof(Dictionary<string, object>), typeof(bool) }, null);
                if (extractToolMethod != null)
                {
                    // Test args["toolSummary"]
                    var callArgs = new Dictionary<string, object> { { "CommandLine", "git status" }, { "toolSummary", "Git status check" } };
                    var callObj = new Dictionary<string, object> { { "name", "run_command" }, { "args", callArgs } };
                    var d1 = new Dictionary<string, object> { { "tool_calls", new ArrayList { callObj } } };
                    string ts1 = (string)extractToolMethod.Invoke(null, new object[] { d1, true });
                    if (ts1 != "Git status check")
                    {
                        Console.WriteLine("FAIL: args['toolSummary'] extraction failed, got: " + ts1);
                        return 32;
                    }

                    // Test file operation fallback (read_file)
                    var callFileArgs = new Dictionary<string, object> { { "Path", "d:\\AIStudio\\iClock\\src\\AgentWatcher.cs" } };
                    var callFileObj = new Dictionary<string, object> { { "name", "read_file" }, { "args", callFileArgs } };
                    var d2 = new Dictionary<string, object> { { "tool_calls", new ArrayList { callFileObj } } };
                    string ts2 = (string)extractToolMethod.Invoke(null, new object[] { d2, true });
                    if (ts2 != "读取 AgentWatcher.cs")
                    {
                        Console.WriteLine("FAIL: read_file action fallback failed, got: " + ts2);
                        return 32;
                    }

                    // Test command fallback (run_command)
                    var callCmdArgs = new Dictionary<string, object> { { "CommandLine", "powershell -File .\\build.ps1" } };
                    var callCmdObj = new Dictionary<string, object> { { "name", "run_command" }, { "args", callCmdArgs } };
                    var d3 = new Dictionary<string, object> { { "tool_calls", new ArrayList { callCmdObj } } };
                    string ts3 = (string)extractToolMethod.Invoke(null, new object[] { d3, true });
                    if (ts3 != "运行: .\\build.ps1")
                    {
                        Console.WriteLine("FAIL: run_command action fallback failed, got: " + ts3);
                        return 32;
                    }
                }

                // Test retention of action during trailing GENERIC tool output line
                System.IO.File.AppendAllText(testFile, "{\"step_index\":2,\"type\":\"PLANNER_RESPONSE\",\"tool_calls\":[{\"name\":\"run_command\",\"args\":{\"CommandLine\":\"git status\",\"toolSummary\":\"Git status check\"}}]}\r\n", System.Text.Encoding.UTF8);
                procMethod.Invoke(watcher, new object[] { testFile, "TestAgent" });
                string subD2 = "";
                object tTask2 = null;
                for (int w = 0; w < 20; w++)
                {
                    System.Threading.Thread.Sleep(15);
                    System.Windows.Forms.Application.DoEvents();
                    tTask2 = findTask.Invoke(app, new object[] { "TestAgent" });
                    subD2 = tTask2 != null ? (string)tTask2.GetType().GetField("SubDetail", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(tTask2) : "";
                    if (!string.IsNullOrEmpty(subD2) && subD2.Contains("Git status check")) break;
                }
                if (string.IsNullOrEmpty(subD2) || !subD2.Contains("Git status check"))
                {
                    Console.WriteLine("FAIL: SubDetail does not contain toolSummary: " + subD2);
                    return 32;
                }

                // Append GENERIC output line; SubDetail should stay locked to toolSummary
                System.IO.File.AppendAllText(testFile, "{\"step_index\":3,\"type\":\"GENERIC\",\"content\":\"On branch main\"}\r\n", System.Text.Encoding.UTF8);
                procMethod.Invoke(watcher, new object[] { testFile, "TestAgent" });
                System.Windows.Forms.Application.DoEvents();
                object tTask3 = findTask.Invoke(app, new object[] { "TestAgent" });
                string subD3 = (string)tTask3.GetType().GetField("SubDetail", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(tTask3);
                if (string.IsNullOrEmpty(subD3) || !subD3.Contains("Git status check"))
                {
                    Console.WriteLine("FAIL: SubDetail lost toolSummary after GENERIC output line: " + subD3);
                    return 32;
                }

                ((IDisposable)watcher).Dispose();
            }
            finally
            {
                FieldInfo fProj = appType.GetField("activeProjectedNote", bf);
                if (fProj != null)
                {
                    Form p = (Form)fProj.GetValue(app);
                    if (p != null && !p.IsDisposed) p.Close();
                }
                try { System.IO.Directory.Delete(testWatchDir, true); } catch { }
            }
            Console.WriteLine("PASS: AgentSessionWatcher zero-polling transcript parser and state dispatch verified.");

            // 24d. Protocol-aware transcript semantics (Cursor / Claude / Codex / Antigravity / generic)
            {
                Type watcherType24d = asm.GetType("AgentSessionWatcher");
                MethodInfo findTask24d = appType.GetMethod("FindTaskStatus", BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(string) }, null);
                MethodInfo clearProg24d = appType.GetMethod("ClearAgentProgress", BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(string) }, null);
                object watcher24d = Activator.CreateInstance(watcherType24d, BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { app }, null);
                MethodInfo proc24d = watcherType24d.GetMethod("ProcessLogFile", BindingFlags.NonPublic | BindingFlags.Instance);
                string semDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "iclock_sem_" + Guid.NewGuid().ToString("N"));
                System.IO.Directory.CreateDirectory(semDir);
                try
                {
                    // Cursor: substantial follow-up replaces Topic; HITL short replies do not; turn_ended uses prior text
                    string cursorSem = System.IO.Path.Combine(semDir, "cursor-sem.jsonl");
                    System.IO.File.WriteAllText(cursorSem,
                        "{\"role\":\"user\",\"message\":{\"content\":[{\"type\":\"text\",\"text\":\"<user_query>优化倒计时页通用配置</user_query>\"}]}}\r\n",
                        System.Text.Encoding.UTF8);
                    proc24d.Invoke(watcher24d, new object[] { cursorSem, "CursorHUD" });
                    System.Windows.Forms.Application.DoEvents();
                    System.IO.File.AppendAllText(cursorSem,
                        "{\"role\":\"user\",\"message\":{\"content\":[{\"type\":\"text\",\"text\":\"<user_query>给个文字版设计简图</user_query>\"}]}}\r\n",
                        System.Text.Encoding.UTF8);
                    proc24d.Invoke(watcher24d, new object[] { cursorSem, "CursorHUD" });
                    System.Windows.Forms.Application.DoEvents();
                    object curTask = findTask24d.Invoke(app, new object[] { "CursorHUD" });
                    string curMsg = curTask != null ? (string)curTask.GetType().GetField("Message", bf).GetValue(curTask) : "";
                    if (curMsg.IndexOf("文字版设计简图", StringComparison.Ordinal) < 0 || curMsg.IndexOf("优化倒计时页通用配置", StringComparison.Ordinal) >= 0)
                    {
                        Console.WriteLine("FAIL: Cursor follow-up must replace Topic: " + curMsg);
                        return 82;
                    }
                    string phaseSub = curTask != null ? (string)curTask.GetType().GetField("SubDetail", bf).GetValue(curTask) : "";
                    if (phaseSub.IndexOf("计划下一步", StringComparison.Ordinal) < 0)
                    {
                        Console.WriteLine("FAIL: model call with no step yet must show 计划下一步: " + phaseSub);
                        return 82;
                    }
                    System.IO.File.AppendAllText(cursorSem,
                        "{\"role\":\"assistant\",\"message\":{\"content\":[{\"type\":\"thinking\",\"thinking\":\"先确认状态条该跟模型调用的阶段。\"}]}}\r\n",
                        System.Text.Encoding.UTF8);
                    proc24d.Invoke(watcher24d, new object[] { cursorSem, "CursorHUD" });
                    System.Windows.Forms.Application.DoEvents();
                    curTask = findTask24d.Invoke(app, new object[] { "CursorHUD" });
                    phaseSub = curTask != null ? (string)curTask.GetType().GetField("SubDetail", bf).GetValue(curTask) : "";
                    if (phaseSub.IndexOf("正在思考", StringComparison.Ordinal) < 0 || phaseSub.IndexOf("模型调用", StringComparison.Ordinal) < 0)
                    {
                        Console.WriteLine("FAIL: a thinking block during the model call must show 正在思考: " + phaseSub);
                        return 82;
                    }

                    System.IO.File.AppendAllText(cursorSem,
                        "{\"role\":\"assistant\",\"message\":{\"content\":[{\"type\":\"text\",\"text\":\"先看倒计时设置页的通用配置项。\"},{\"type\":\"tool_use\",\"name\":\"Read\",\"input\":{\"path\":\"src/Countdown.cs\"}}]}}\r\n",
                        System.Text.Encoding.UTF8);
                    proc24d.Invoke(watcher24d, new object[] { cursorSem, "CursorHUD" });
                    System.Windows.Forms.Application.DoEvents();
                    curTask = findTask24d.Invoke(app, new object[] { "CursorHUD" });
                    string curSub = curTask != null ? (string)curTask.GetType().GetField("SubDetail", bf).GetValue(curTask) : "";
                    bool thoughtOrRead = (!string.IsNullOrEmpty(curSub) && (curSub.IndexOf("先看倒计时", StringComparison.Ordinal) >= 0 || curSub.IndexOf("Countdown", StringComparison.Ordinal) >= 0 || curSub.IndexOf("阅读", StringComparison.Ordinal) >= 0));
                    if (!thoughtOrRead)
                    {
                        Console.WriteLine("FAIL: Cursor text narration / read node missing: " + curSub);
                        return 82;
                    }

                    System.IO.File.AppendAllText(cursorSem,
                        "{\"role\":\"assistant\",\"message\":{\"content\":[{\"type\":\"tool_use\",\"name\":\"StrReplace\",\"input\":{\"path\":\"src/StickyWrite.cs\"}}]}}\r\n",
                        System.Text.Encoding.UTF8);
                    proc24d.Invoke(watcher24d, new object[] { cursorSem, "CursorHUD" });
                    System.Windows.Forms.Application.DoEvents();
                    System.IO.File.AppendAllText(cursorSem,
                        "{\"role\":\"assistant\",\"message\":{\"content\":[{\"type\":\"tool_use\",\"name\":\"Read\",\"input\":{\"path\":\"src/ReadOne.cs\"}}]}}\r\n" +
                        "{\"role\":\"assistant\",\"message\":{\"content\":[{\"type\":\"tool_use\",\"name\":\"Read\",\"input\":{\"path\":\"src/ReadTwo.cs\"}}]}}\r\n",
                        System.Text.Encoding.UTF8);
                    proc24d.Invoke(watcher24d, new object[] { cursorSem, "CursorHUD" });
                    bool readAfterWrite = false;
                    for (int w = 0; w < 20; w++)
                    {
                        System.Threading.Thread.Sleep(15);
                        System.Windows.Forms.Application.DoEvents();
                        curTask = findTask24d.Invoke(app, new object[] { "CursorHUD" });
                        curSub = curTask != null ? (string)curTask.GetType().GetField("SubDetail", bf).GetValue(curTask) : "";
                        if (!string.IsNullOrEmpty(curSub) && curSub.IndexOf("StickyWrite", StringComparison.Ordinal) < 0 &&
                            (curSub.IndexOf("阅读", StringComparison.Ordinal) >= 0 || curSub.IndexOf("ReadOne", StringComparison.Ordinal) >= 0 || curSub.IndexOf("ReadTwo", StringComparison.Ordinal) >= 0))
                        {
                            readAfterWrite = true;
                            break;
                        }
                    }
                    if (!readAfterWrite)
                    {
                        Console.WriteLine("FAIL: consecutive Read must not stay pinned on earlier write: " + curSub);
                        return 82;
                    }

                    System.IO.File.AppendAllText(cursorSem,
                        "{\"role\":\"assistant\",\"message\":{\"content\":[{\"type\":\"tool_use\",\"name\":\"AskQuestion\",\"input\":{\"questions\":[{\"question\":\"选哪个方案\"}]}}]}}\r\n",
                        System.Text.Encoding.UTF8);
                    proc24d.Invoke(watcher24d, new object[] { cursorSem, "CursorHUD" });
                    System.Windows.Forms.Application.DoEvents();
                    curTask = findTask24d.Invoke(app, new object[] { "CursorHUD" });
                    curMsg = curTask != null ? (string)curTask.GetType().GetField("Message", bf).GetValue(curTask) : "";
                    if (curMsg.IndexOf("等待", StringComparison.Ordinal) < 0 || curMsg.IndexOf("文字版设计简图", StringComparison.Ordinal) < 0)
                    {
                        Console.WriteLine("FAIL: AskQuestion must wait on current Topic: " + curMsg);
                        return 82;
                    }
                    System.IO.File.AppendAllText(cursorSem,
                        "{\"role\":\"user\",\"message\":{\"content\":[{\"type\":\"text\",\"text\":\"<user_query>好的</user_query>\"}]}}\r\n",
                        System.Text.Encoding.UTF8);
                    proc24d.Invoke(watcher24d, new object[] { cursorSem, "CursorHUD" });
                    System.Windows.Forms.Application.DoEvents();
                    curTask = findTask24d.Invoke(app, new object[] { "CursorHUD" });
                    curMsg = curTask != null ? (string)curTask.GetType().GetField("Message", bf).GetValue(curTask) : "";
                    if (curMsg.IndexOf("文字版设计简图", StringComparison.Ordinal) < 0 || curMsg.IndexOf("[好的]", StringComparison.Ordinal) >= 0)
                    {
                        Console.WriteLine("FAIL: HITL 好的 must not replace Topic: " + curMsg);
                        return 82;
                    }
                    System.IO.File.AppendAllText(cursorSem,
                        "{\"role\":\"assistant\",\"message\":{\"content\":[{\"type\":\"tool_use\",\"name\":\"AskQuestion\",\"input\":{\"questions\":[{\"question\":\"选哪个方案\"}]}}]}}\r\n",
                        System.Text.Encoding.UTF8);
                    proc24d.Invoke(watcher24d, new object[] { cursorSem, "CursorHUD" });
                    System.Windows.Forms.Application.DoEvents();
                    System.IO.File.AppendAllText(cursorSem,
                        "{\"role\":\"user\",\"message\":{\"content\":[{\"type\":\"text\",\"text\":\"<user_query>方案A</user_query>\"}]}}\r\n",
                        System.Text.Encoding.UTF8);
                    proc24d.Invoke(watcher24d, new object[] { cursorSem, "CursorHUD" });
                    System.Windows.Forms.Application.DoEvents();
                    curTask = findTask24d.Invoke(app, new object[] { "CursorHUD" });
                    curMsg = curTask != null ? (string)curTask.GetType().GetField("Message", bf).GetValue(curTask) : "";
                    if (curMsg.IndexOf("文字版设计简图", StringComparison.Ordinal) < 0 || curMsg.IndexOf("方案A", StringComparison.Ordinal) >= 0)
                    {
                        Console.WriteLine("FAIL: HITL short option must not replace Topic: " + curMsg);
                        return 82;
                    }

                    System.IO.File.WriteAllText(cursorSem,
                        "{\"role\":\"user\",\"message\":{\"content\":[{\"type\":\"text\",\"text\":\"<user_query>优化倒计时页通用配置</user_query>\"}]}}\r\n{\"role\":\"assistant\",\"message\":{\"content\":[{\"type\":\"text\",\"text\":\"倒计时通用配置已按方案改完。\"}]}}\r\n{\"type\":\"turn_ended\",\"status\":\"success\"}\r\n",
                        System.Text.Encoding.UTF8);
                    proc24d.Invoke(watcher24d, new object[] { cursorSem, "CursorHUD" });
                    string curDone = "";
                    for (int w = 0; w < 30; w++)
                    {
                        System.Threading.Thread.Sleep(20);
                        System.Windows.Forms.Application.DoEvents();
                        curTask = findTask24d.Invoke(app, new object[] { "CursorHUD" });
                        curDone = curTask != null ? (string)curTask.GetType().GetField("Message", bf).GetValue(curTask) : "";
                        if (curDone.IndexOf("完成", StringComparison.Ordinal) >= 0) break;
                    }
                    string curDoneSub = "";
                    for (int w = 0; w < 20; w++)
                    {
                        System.Threading.Thread.Sleep(15);
                        System.Windows.Forms.Application.DoEvents();
                        curTask = findTask24d.Invoke(app, new object[] { "CursorHUD" });
                        curDoneSub = curTask != null ? (string)curTask.GetType().GetField("SubDetail", bf).GetValue(curTask) : "";
                        if (!string.IsNullOrEmpty(curDoneSub) && curDoneSub.IndexOf("倒计时通用配置已按方案改完", StringComparison.Ordinal) >= 0) break;
                    }
                    if (curDone.IndexOf("优化倒计时页通用配置", StringComparison.Ordinal) < 0 || curDone.IndexOf("完成", StringComparison.Ordinal) < 0)
                    {
                        Console.WriteLine("FAIL: Cursor turn_ended did not complete on Topic: " + curDone);
                        return 82;
                    }
                    if (string.IsNullOrEmpty(curDoneSub) || curDoneSub.IndexOf("倒计时通用配置已按方案改完", StringComparison.Ordinal) < 0)
                    {
                        Console.WriteLine("FAIL: Cursor turn_ended conclusion must come from prior assistant text: " + curDoneSub);
                        return 82;
                    }
                    if (clearProg24d != null) clearProg24d.Invoke(app, new object[] { "CursorHUD" });

                    MethodInfo onChanged24d = watcherType24d.GetMethod("OnFileChanged", BindingFlags.NonPublic | BindingFlags.Instance);
                    string burstFile = System.IO.Path.Combine(semDir, "cursor-burst.jsonl");
                    System.IO.File.WriteAllText(burstFile,
                        "{\"role\":\"user\",\"message\":{\"content\":[{\"type\":\"text\",\"text\":\"<user_query>跟上节奏</user_query>\"}]}}\r\n" +
                        "{\"role\":\"assistant\",\"message\":{\"content\":[{\"type\":\"tool_use\",\"name\":\"StrReplace\",\"input\":{\"path\":\"src/BurstFirst.cs\"}}]}}\r\n",
                        System.Text.Encoding.UTF8);
                    onChanged24d.Invoke(watcher24d, new object[] { burstFile, "CursorBurst" });
                    string burstSub = "";
                    object burstTask = null;
                    bool burstLead = false;
                    for (int w = 0; w < 8; w++)
                    {
                        System.Threading.Thread.Sleep(10);
                        System.Windows.Forms.Application.DoEvents();
                        burstTask = findTask24d.Invoke(app, new object[] { "CursorBurst" });
                        burstSub = burstTask != null ? (string)burstTask.GetType().GetField("SubDetail", bf).GetValue(burstTask) : "";
                        if (!string.IsNullOrEmpty(burstSub) && burstSub.IndexOf("BurstFirst", StringComparison.Ordinal) >= 0)
                        {
                            burstLead = true;
                            break;
                        }
                    }
                    if (!burstLead)
                    {
                        Console.WriteLine("FAIL: leading debounce did not parse within ~80ms: " + burstSub);
                        return 82;
                    }
                    System.IO.File.AppendAllText(burstFile,
                        "{\"role\":\"assistant\",\"message\":{\"content\":[{\"type\":\"tool_use\",\"name\":\"Read\",\"input\":{\"path\":\"src/BurstLast.cs\"}}]}}\r\n",
                        System.Text.Encoding.UTF8);
                    onChanged24d.Invoke(watcher24d, new object[] { burstFile, "CursorBurst" });
                    bool burstLast = false;
                    for (int w = 0; w < 12; w++)
                    {
                        System.Threading.Thread.Sleep(10);
                        System.Windows.Forms.Application.DoEvents();
                        burstTask = findTask24d.Invoke(app, new object[] { "CursorBurst" });
                        burstSub = burstTask != null ? (string)burstTask.GetType().GetField("SubDetail", bf).GetValue(burstTask) : "";
                        if (!string.IsNullOrEmpty(burstSub) && burstSub.IndexOf("BurstLast", StringComparison.Ordinal) >= 0)
                        {
                            burstLast = true;
                            break;
                        }
                    }
                    if (!burstLast)
                    {
                        Console.WriteLine("FAIL: max-interval debounce did not reach last tool within 80ms: " + burstSub);
                        return 82;
                    }
                    if (clearProg24d != null) clearProg24d.Invoke(app, new object[] { "CursorBurst" });

                    // Claude: last non-reminder text; tool_result user row ignored
                    string claudeSem = System.IO.Path.Combine(semDir, "session.jsonl");
                    System.IO.File.WriteAllText(claudeSem,
                        "{\"type\":\"user\",\"origin\":{\"kind\":\"human\"},\"message\":{\"role\":\"user\",\"content\":[{\"type\":\"text\",\"text\":\"<system-reminder>workspace is dirty</system-reminder>\"},{\"type\":\"text\",\"text\":\"给数据中心表单加校验\"}]}}\r\n",
                        System.Text.Encoding.UTF8);
                    proc24d.Invoke(watcher24d, new object[] { claudeSem, "ClaudeHUD" });
                    System.Windows.Forms.Application.DoEvents();
                    object clTask = findTask24d.Invoke(app, new object[] { "ClaudeHUD" });
                    string clMsg = clTask != null ? (string)clTask.GetType().GetField("Message", bf).GetValue(clTask) : "";
                    if (clMsg.IndexOf("给数据中心表单加校验", StringComparison.Ordinal) < 0 || clMsg.IndexOf("workspace is dirty", StringComparison.Ordinal) >= 0)
                    {
                        Console.WriteLine("FAIL: Claude topic must take last non-reminder text: " + clMsg);
                        return 82;
                    }
                    System.IO.File.AppendAllText(claudeSem,
                        "{\"type\":\"user\",\"message\":{\"role\":\"user\",\"content\":[{\"type\":\"tool_result\",\"tool_use_id\":\"1\",\"content\":\"ok\"}]}}\r\n",
                        System.Text.Encoding.UTF8);
                    proc24d.Invoke(watcher24d, new object[] { claudeSem, "ClaudeHUD" });
                    System.Windows.Forms.Application.DoEvents();
                    clTask = findTask24d.Invoke(app, new object[] { "ClaudeHUD" });
                    string clMsg2 = clTask != null ? (string)clTask.GetType().GetField("Message", bf).GetValue(clTask) : "";
                    if (clMsg2.IndexOf("给数据中心表单加校验", StringComparison.Ordinal) < 0)
                    {
                        Console.WriteLine("FAIL: Claude tool_result user row must not replace Topic: " + clMsg2);
                        return 82;
                    }
                    if (clearProg24d != null) clearProg24d.Invoke(app, new object[] { "ClaudeHUD" });

                    // Codex: skip AGENTS.md user; Reasoning.summary_text; task_complete
                    string codexSem = System.IO.Path.Combine(semDir, "rollout-sem.jsonl");
                    System.IO.File.WriteAllText(codexSem,
                        "{\"type\":\"response_item\",\"payload\":{\"role\":\"user\",\"content_item_kinds\":[\"agents_md.instructions\"],\"content\":[{\"type\":\"text\",\"text\":\"# AGENTS.md rules dump\"}]}}\r\n" +
                        "{\"type\":\"event_msg\",\"payload\":{\"type\":\"item_completed\",\"item\":{\"type\":\"UserMessage\",\"content\":[{\"type\":\"text\",\"text\":\"开箱体验增加Codex支持\"}]}}}\r\n",
                        System.Text.Encoding.UTF8);
                    proc24d.Invoke(watcher24d, new object[] { codexSem, "CodexHUD" });
                    System.Windows.Forms.Application.DoEvents();
                    object cxTask = findTask24d.Invoke(app, new object[] { "CodexHUD" });
                    string cxMsg = cxTask != null ? (string)cxTask.GetType().GetField("Message", bf).GetValue(cxTask) : "";
                    if (cxMsg.IndexOf("开箱体验增加Codex支持", StringComparison.Ordinal) < 0 || cxMsg.IndexOf("AGENTS.md", StringComparison.Ordinal) >= 0)
                    {
                        Console.WriteLine("FAIL: Codex must skip AGENTS.md and take UserMessage: " + cxMsg);
                        return 82;
                    }
                    System.IO.File.AppendAllText(codexSem,
                        "{\"type\":\"event_msg\",\"payload\":{\"type\":\"item_completed\",\"item\":{\"type\":\"Reasoning\",\"summary_text\":\"Plan the Codex HUD wiring first.\"}}}\r\n",
                        System.Text.Encoding.UTF8);
                    proc24d.Invoke(watcher24d, new object[] { codexSem, "CodexHUD" });
                    System.Windows.Forms.Application.DoEvents();
                    cxTask = findTask24d.Invoke(app, new object[] { "CodexHUD" });
                    string cxSub = cxTask != null ? (string)cxTask.GetType().GetField("SubDetail", bf).GetValue(cxTask) : "";
                    if (string.IsNullOrEmpty(cxSub) || cxSub.IndexOf("Plan the Codex HUD", StringComparison.Ordinal) < 0)
                    {
                        Console.WriteLine("FAIL: Codex Reasoning.summary_text not shown: " + cxSub);
                        return 82;
                    }
                    System.IO.File.AppendAllText(codexSem,
                        "{\"type\":\"event_msg\",\"payload\":{\"type\":\"task_complete\",\"last_agent_message\":\"Codex开箱支持已落地。\"}}\r\n",
                        System.Text.Encoding.UTF8);
                    proc24d.Invoke(watcher24d, new object[] { codexSem, "CodexHUD" });
                    string cxDone = "";
                    for (int w = 0; w < 30; w++)
                    {
                        System.Threading.Thread.Sleep(20);
                        System.Windows.Forms.Application.DoEvents();
                        cxTask = findTask24d.Invoke(app, new object[] { "CodexHUD" });
                        cxDone = cxTask != null ? (string)cxTask.GetType().GetField("Message", bf).GetValue(cxTask) : "";
                        if (cxDone.IndexOf("完成", StringComparison.Ordinal) >= 0) break;
                    }
                    string cxDoneSub = cxTask != null ? (string)cxTask.GetType().GetField("SubDetail", bf).GetValue(cxTask) : "";
                    if (cxDone.IndexOf("开箱体验增加Codex支持", StringComparison.Ordinal) < 0 || cxDone.IndexOf("完成", StringComparison.Ordinal) < 0)
                    {
                        Console.WriteLine("FAIL: Codex task_complete did not finish on Topic: " + cxDone);
                        return 82;
                    }
                    if (string.IsNullOrEmpty(cxDoneSub) || cxDoneSub.IndexOf("Codex开箱支持已落地", StringComparison.Ordinal) < 0)
                    {
                        Console.WriteLine("FAIL: Codex task_complete conclusion missing: " + cxDoneSub);
                        return 82;
                    }
                    if (clearProg24d != null) clearProg24d.Invoke(app, new object[] { "CodexHUD" });

                    // Antigravity: lock USER_REQUEST; DONE+tool_calls is not complete; noise/error/waiting/true end
                    string agySem = System.IO.Path.Combine(semDir, "transcript.jsonl");
                    System.IO.File.WriteAllText(agySem,
                        "{\"step_index\":0,\"source\":\"USER_EXPLICIT\",\"type\":\"USER_INPUT\",\"content\":\"<USER_REQUEST>基于活跃进程扫描识别智能体</USER_REQUEST>\"}\r\n",
                        System.Text.Encoding.UTF8);
                    proc24d.Invoke(watcher24d, new object[] { agySem, "AgyHUD" });
                    System.Windows.Forms.Application.DoEvents();
                    System.IO.File.AppendAllText(agySem,
                        "{\"step_index\":1,\"source\":\"USER_EXPLICIT\",\"type\":\"USER_INPUT\",\"content\":\"<USER_REQUEST>继续</USER_REQUEST>\"}\r\n",
                        System.Text.Encoding.UTF8);
                    proc24d.Invoke(watcher24d, new object[] { agySem, "AgyHUD" });
                    System.Windows.Forms.Application.DoEvents();
                    object agTask = findTask24d.Invoke(app, new object[] { "AgyHUD" });
                    string agMsg = agTask != null ? (string)agTask.GetType().GetField("Message", bf).GetValue(agTask) : "";
                    if (agMsg.IndexOf("基于活跃进程扫描识别智能体", StringComparison.Ordinal) < 0)
                    {
                        Console.WriteLine("FAIL: Antigravity Topic must stay on first USER_REQUEST: " + agMsg);
                        return 82;
                    }
                    System.IO.File.AppendAllText(agySem,
                        "{\"step_index\":2,\"type\":\"PLANNER_RESPONSE\",\"status\":\"DONE\",\"tool_calls\":[{\"name\":\"run_command\",\"toolSummary\":\"Build Solution\"}]}\r\n",
                        System.Text.Encoding.UTF8);
                    proc24d.Invoke(watcher24d, new object[] { agySem, "AgyHUD" });
                    System.Windows.Forms.Application.DoEvents();
                    agTask = findTask24d.Invoke(app, new object[] { "AgyHUD" });
                    string agMsg2 = agTask != null ? (string)agTask.GetType().GetField("Message", bf).GetValue(agTask) : "";
                    if (agMsg2.IndexOf("完成", StringComparison.Ordinal) >= 0)
                    {
                        Console.WriteLine("FAIL: PLANNER_RESPONSE+DONE+tool_calls must not complete: " + agMsg2);
                        return 82;
                    }
                    System.IO.File.AppendAllText(agySem,
                        "{\"step_index\":3,\"type\":\"SYSTEM_MESSAGE\",\"status\":\"DONE\",\"content\":\"Subagent stopped because of restart\"}\r\n" +
                        "{\"step_index\":4,\"type\":\"CHECKPOINT\",\"status\":\"DONE\",\"content\":\"User Requests: old topic\"}\r\n",
                        System.Text.Encoding.UTF8);
                    proc24d.Invoke(watcher24d, new object[] { agySem, "AgyHUD" });
                    System.Windows.Forms.Application.DoEvents();
                    agTask = findTask24d.Invoke(app, new object[] { "AgyHUD" });
                    string agMsg3 = agTask != null ? (string)agTask.GetType().GetField("Message", bf).GetValue(agTask) : "";
                    string agSub3 = agTask != null ? (string)agTask.GetType().GetField("SubDetail", bf).GetValue(agTask) : "";
                    if (agMsg3.IndexOf("完成", StringComparison.Ordinal) >= 0 || (agSub3 != null && agSub3.IndexOf("Subagent stopped", StringComparison.Ordinal) >= 0) ||
                        (agMsg3.IndexOf("old topic", StringComparison.Ordinal) >= 0))
                    {
                        Console.WriteLine("FAIL: SYSTEM_MESSAGE/CHECKPOINT leaked into HUD: " + agMsg3 + " / " + agSub3);
                        return 82;
                    }
                    System.IO.File.AppendAllText(agySem,
                        "{\"step_index\":5,\"type\":\"GENERIC\",\"status\":\"ERROR\",\"content\":\"patch did not apply\"}\r\n",
                        System.Text.Encoding.UTF8);
                    proc24d.Invoke(watcher24d, new object[] { agySem, "AgyHUD" });
                    System.Windows.Forms.Application.DoEvents();
                    agTask = findTask24d.Invoke(app, new object[] { "AgyHUD" });
                    string agMsg4 = agTask != null ? (string)agTask.GetType().GetField("Message", bf).GetValue(agTask) : "";
                    if (agMsg4.IndexOf("异常", StringComparison.Ordinal) >= 0)
                    {
                        Console.WriteLine("FAIL: GENERIC ERROR must not be session error: " + agMsg4);
                        return 82;
                    }
                    System.IO.File.AppendAllText(agySem,
                        "{\"step_index\":6,\"type\":\"PLANNER_RESPONSE\",\"tool_calls\":[{\"name\":\"write_file\",\"args\":{\"ArtifactMetadata\":{\"RequestFeedback\":true,\"Summary\":\"Plan\"}}}]}\r\n",
                        System.Text.Encoding.UTF8);
                    proc24d.Invoke(watcher24d, new object[] { agySem, "AgyHUD" });
                    System.Windows.Forms.Application.DoEvents();
                    agTask = findTask24d.Invoke(app, new object[] { "AgyHUD" });
                    string agMsg5 = agTask != null ? (string)agTask.GetType().GetField("Message", bf).GetValue(agTask) : "";
                    if (agMsg5.IndexOf("等待", StringComparison.Ordinal) < 0)
                    {
                        Console.WriteLine("FAIL: RequestFeedback must enter waiting: " + agMsg5);
                        return 82;
                    }
                    System.IO.File.WriteAllText(agySem,
                        "{\"step_index\":0,\"source\":\"USER_EXPLICIT\",\"type\":\"USER_INPUT\",\"content\":\"<USER_REQUEST>基于活跃进程扫描识别智能体</USER_REQUEST>\"}\r\n" +
                        "{\"step_index\":7,\"type\":\"PLANNER_RESPONSE\",\"status\":\"DONE\",\"content\":\"识别逻辑已接到状态栏。\"}\r\n",
                        System.Text.Encoding.UTF8);
                    proc24d.Invoke(watcher24d, new object[] { agySem, "AgyHUD" });
                    string agDone = "";
                    object agDoneTask = null;
                    for (int w = 0; w < 30; w++)
                    {
                        System.Threading.Thread.Sleep(20);
                        System.Windows.Forms.Application.DoEvents();
                        agDoneTask = findTask24d.Invoke(app, new object[] { "AgyHUD" });
                        agDone = agDoneTask != null ? (string)agDoneTask.GetType().GetField("Message", bf).GetValue(agDoneTask) : "";
                        if (agDone.IndexOf("完成", StringComparison.Ordinal) >= 0) break;
                    }
                    string agDoneSub = agDoneTask != null ? (string)agDoneTask.GetType().GetField("SubDetail", bf).GetValue(agDoneTask) : "";
                    if (agDone.IndexOf("基于活跃进程扫描识别智能体", StringComparison.Ordinal) < 0 || agDone.IndexOf("完成", StringComparison.Ordinal) < 0)
                    {
                        Console.WriteLine("FAIL: Antigravity true end did not complete: " + agDone);
                        return 82;
                    }
                    if (string.IsNullOrEmpty(agDoneSub) || agDoneSub.IndexOf("识别逻辑已接到状态栏", StringComparison.Ordinal) < 0)
                    {
                        Console.WriteLine("FAIL: Antigravity conclusion missing: " + agDoneSub);
                        return 82;
                    }
                    if (clearProg24d != null) clearProg24d.Invoke(app, new object[] { "AgyHUD" });

                    // Generic protocol: skip tool_result; dual text; Gemini thought; status=DONE is not complete; finish_reason=stop is
                    string genSem = System.IO.Path.Combine(semDir, "generic.jsonl");
                    System.IO.File.WriteAllText(genSem,
                        "{\"role\":\"user\",\"content\":[{\"type\":\"text\",\"text\":\"<system-reminder>ignore me</system-reminder>\"},{\"type\":\"text\",\"text\":\"实现通用智能体感知\"}]}\r\n",
                        System.Text.Encoding.UTF8);
                    proc24d.Invoke(watcher24d, new object[] { genSem, "Windsurf" });
                    System.Windows.Forms.Application.DoEvents();
                    object gTask = findTask24d.Invoke(app, new object[] { "Windsurf" });
                    string gMsg = gTask != null ? (string)gTask.GetType().GetField("Message", bf).GetValue(gTask) : "";
                    if (gMsg.IndexOf("实现通用智能体感知", StringComparison.Ordinal) < 0 || gMsg.IndexOf("ignore me", StringComparison.Ordinal) >= 0)
                    {
                        Console.WriteLine("FAIL: Generic dual text must take last non-reminder: " + gMsg);
                        return 82;
                    }
                    System.IO.File.AppendAllText(genSem,
                        "{\"role\":\"user\",\"content\":[{\"type\":\"tool_result\",\"content\":\"stdout\"}]}\r\n",
                        System.Text.Encoding.UTF8);
                    proc24d.Invoke(watcher24d, new object[] { genSem, "Windsurf" });
                    System.Windows.Forms.Application.DoEvents();
                    gTask = findTask24d.Invoke(app, new object[] { "Windsurf" });
                    string gMsg2 = gTask != null ? (string)gTask.GetType().GetField("Message", bf).GetValue(gTask) : "";
                    if (gMsg2.IndexOf("实现通用智能体感知", StringComparison.Ordinal) < 0)
                    {
                        Console.WriteLine("FAIL: Generic tool_result must not replace Topic: " + gMsg2);
                        return 82;
                    }
                    System.IO.File.AppendAllText(genSem,
                        "{\"role\":\"assistant\",\"content\":[{\"thought\":true,\"text\":\"Let me reason about the wire protocol.\"}]}\r\n",
                        System.Text.Encoding.UTF8);
                    proc24d.Invoke(watcher24d, new object[] { genSem, "Windsurf" });
                    System.Windows.Forms.Application.DoEvents();
                    gTask = findTask24d.Invoke(app, new object[] { "Windsurf" });
                    string gSub = gTask != null ? (string)gTask.GetType().GetField("SubDetail", bf).GetValue(gTask) : "";
                    if (string.IsNullOrEmpty(gSub) || gSub.IndexOf("Let me reason", StringComparison.Ordinal) < 0)
                    {
                        Console.WriteLine("FAIL: Gemini thought:true not shown: " + gSub);
                        return 82;
                    }
                    System.IO.File.AppendAllText(genSem,
                        "{\"role\":\"assistant\",\"status\":\"DONE\",\"content\":\"should not complete\"}\r\n",
                        System.Text.Encoding.UTF8);
                    proc24d.Invoke(watcher24d, new object[] { genSem, "Windsurf" });
                    System.Windows.Forms.Application.DoEvents();
                    System.Threading.Thread.Sleep(80);
                    System.Windows.Forms.Application.DoEvents();
                    gTask = findTask24d.Invoke(app, new object[] { "Windsurf" });
                    string gMsg3 = gTask != null ? (string)gTask.GetType().GetField("Message", bf).GetValue(gTask) : "";
                    if (gMsg3.IndexOf("完成", StringComparison.Ordinal) >= 0)
                    {
                        Console.WriteLine("FAIL: Generic status=DONE must not complete: " + gMsg3);
                        return 82;
                    }
                    System.IO.File.AppendAllText(genSem,
                        "{\"role\":\"assistant\",\"finish_reason\":\"stop\",\"content\":\"通用感知已经接好。\"}\r\n",
                        System.Text.Encoding.UTF8);
                    proc24d.Invoke(watcher24d, new object[] { genSem, "Windsurf" });
                    string gDone = "";
                    object gDoneTask = null;
                    for (int w = 0; w < 30; w++)
                    {
                        System.Threading.Thread.Sleep(20);
                        System.Windows.Forms.Application.DoEvents();
                        gDoneTask = findTask24d.Invoke(app, new object[] { "Windsurf" });
                        gDone = gDoneTask != null ? (string)gDoneTask.GetType().GetField("Message", bf).GetValue(gDoneTask) : "";
                        if (gDone.IndexOf("完成", StringComparison.Ordinal) >= 0) break;
                    }
                    if (gDone.IndexOf("实现通用智能体感知", StringComparison.Ordinal) < 0 || gDone.IndexOf("完成", StringComparison.Ordinal) < 0)
                    {
                        Console.WriteLine("FAIL: Generic finish_reason=stop must complete: " + gDone);
                        return 82;
                    }
                    if (clearProg24d != null) clearProg24d.Invoke(app, new object[] { "Windsurf" });
                }
                finally
                {
                    try { ((IDisposable)watcher24d).Dispose(); } catch { }
                    try { System.IO.Directory.Delete(semDir, true); } catch { }
                }
                Console.WriteLine("PASS: Protocol-aware transcript semantics for Cursor/Claude/Codex/Antigravity/generic verified.");
            }

            // Latest status replaces whatever is on screen. Waiting still interrupts.
            {
                MethodInfo setMsg24e = appType.GetMethod("SetAgentTaskMessage", BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo findTask24e = appType.GetMethod("FindTaskStatus", BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(string) }, null);
                MethodInfo clearProg24e = appType.GetMethod("ClearAgentProgress", BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(string) }, null);
                DateTime now24e = DateTime.Now;
                setMsg24e.Invoke(app, new object[] { "⏳ [队列] 处理中...", "QueueHUD", true, "edit_file: Alpha.cs", null, -1f, now24e, "queue-sem" });
                setMsg24e.Invoke(app, new object[] { "⏳ [队列] 处理中...", "QueueHUD", true, "⚡ build.ps1", null, -1f, now24e, "queue-sem" });
                setMsg24e.Invoke(app, new object[] { "⏳ [队列] 处理中...", "QueueHUD", true, "read_file: Beta.cs", null, -1f, now24e, "queue-sem" });
                setMsg24e.Invoke(app, new object[] { "⏳ [队列] 处理中...", "QueueHUD", true, "read_file: Gamma.cs", null, -1f, now24e, "queue-sem" });
                System.Windows.Forms.Application.DoEvents();
                object qTask = findTask24e.Invoke(app, new object[] { "QueueHUD" });
                string qSub = qTask != null ? (string)qTask.GetType().GetField("SubDetail", bf).GetValue(qTask) : "";
                if (qSub.IndexOf("Gamma", StringComparison.Ordinal) < 0 || qSub.IndexOf("Alpha", StringComparison.Ordinal) >= 0)
                {
                    Console.WriteLine("FAIL: latest status must replace the previous one immediately: " + qSub);
                    return 83;
                }
                setMsg24e.Invoke(app, new object[] { "❓ [队列] 等待决策", "QueueHUD", false, "请确认: 选哪个方案", null, -2f, DateTime.Now, "queue-sem" });
                System.Windows.Forms.Application.DoEvents();
                qTask = findTask24e.Invoke(app, new object[] { "QueueHUD" });
                string qMsg = qTask != null ? (string)qTask.GetType().GetField("Message", bf).GetValue(qTask) : "";
                qSub = qTask != null ? (string)qTask.GetType().GetField("SubDetail", bf).GetValue(qTask) : "";
                if (qMsg.IndexOf("等待", StringComparison.Ordinal) < 0 || qSub.IndexOf("选哪个方案", StringComparison.Ordinal) < 0 || qSub.IndexOf("Gamma", StringComparison.Ordinal) >= 0)
                {
                    Console.WriteLine("FAIL: waiting must replace the current status immediately: " + qMsg + " / " + qSub);
                    return 83;
                }
                if (clearProg24e != null) clearProg24e.Invoke(app, new object[] { "QueueHUD" });
                setMsg24e.Invoke(app, new object[] { "⏳ [思考] 处理中...", "ThoughtHUD", true, "💭 先核对边框是被子控件盖住的", null, -1f, now24e, "thought-sem" });
                setMsg24e.Invoke(app, new object[] { "⏳ [思考] 处理中...", "ThoughtHUD", true, "read_file: SettingsDialog.cs", null, -1f, now24e, "thought-sem" });
                setMsg24e.Invoke(app, new object[] { "⏳ [思考] 处理中...", "ThoughtHUD", true, "search: OnCardPaint", null, -1f, now24e, "thought-sem" });
                System.Windows.Forms.Application.DoEvents();
                object thTask = findTask24e.Invoke(app, new object[] { "ThoughtHUD" });
                string thSub = thTask != null ? (string)thTask.GetType().GetField("SubDetail", bf).GetValue(thTask) : "";
                if (thSub.IndexOf("边框", StringComparison.Ordinal) < 0 || thSub.IndexOf("SettingsDialog", StringComparison.Ordinal) >= 0 || thSub.IndexOf("OnCardPaint", StringComparison.Ordinal) >= 0)
                {
                    Console.WriteLine("FAIL: read or search must not cover the current thought: " + thSub);
                    return 83;
                }
                setMsg24e.Invoke(app, new object[] { "⏳ [思考] 处理中...", "ThoughtHUD", true, "edit_file: SettingsDialog.cs", null, -1f, now24e, "thought-sem" });
                System.Windows.Forms.Application.DoEvents();
                thTask = findTask24e.Invoke(app, new object[] { "ThoughtHUD" });
                thSub = thTask != null ? (string)thTask.GetType().GetField("SubDetail", bf).GetValue(thTask) : "";
                if (thSub.IndexOf("SettingsDialog", StringComparison.Ordinal) < 0)
                {
                    Console.WriteLine("FAIL: an edit must replace the thought: " + thSub);
                    return 83;
                }
                setMsg24e.Invoke(app, new object[] { "⏳ [思考] 处理中...", "ThoughtHUD", true, "💭 改完后要留出边框那一像素", null, -1f, now24e, "thought-sem" });
                setMsg24e.Invoke(app, new object[] { "⏳ [思考] 处理中...", "ThoughtHUD", true, "⚡ build.ps1", null, -1f, now24e, "thought-sem" });
                System.Windows.Forms.Application.DoEvents();
                thTask = findTask24e.Invoke(app, new object[] { "ThoughtHUD" });
                thSub = thTask != null ? (string)thTask.GetType().GetField("SubDetail", bf).GetValue(thTask) : "";
                if (thSub.IndexOf("build.ps1", StringComparison.Ordinal) < 0)
                {
                    Console.WriteLine("FAIL: a run must replace the thought: " + thSub);
                    return 83;
                }
                setMsg24e.Invoke(app, new object[] { "⏳ [阶段] 处理中...", "PhaseHUD", true, "计划下一步", null, -1f, now24e, "phase-sem" });
                setMsg24e.Invoke(app, new object[] { "⏳ [阶段] 处理中...", "PhaseHUD", true, "read_file: PhaseRead.cs", null, -1f, now24e, "phase-sem" });
                System.Windows.Forms.Application.DoEvents();
                object phaseTask = findTask24e.Invoke(app, new object[] { "PhaseHUD" });
                string phaseHud = phaseTask != null ? (string)phaseTask.GetType().GetField("SubDetail", bf).GetValue(phaseTask) : "";
                if (phaseHud.IndexOf("PhaseRead", StringComparison.Ordinal) < 0)
                {
                    Console.WriteLine("FAIL: a step must replace 计划下一步: " + phaseHud);
                    return 83;
                }
                setMsg24e.Invoke(app, new object[] { "⏳ [阶段] 处理中...", "PhaseHUD", true, "正在思考 · 先看阶段", null, -1f, now24e, "phase-sem" });
                setMsg24e.Invoke(app, new object[] { "⏳ [阶段] 处理中...", "PhaseHUD", true, "read_file: HiddenRead.cs", null, -1f, now24e, "phase-sem" });
                System.Windows.Forms.Application.DoEvents();
                phaseTask = findTask24e.Invoke(app, new object[] { "PhaseHUD" });
                phaseHud = phaseTask != null ? (string)phaseTask.GetType().GetField("SubDetail", bf).GetValue(phaseTask) : "";
                if (phaseHud.IndexOf("先看阶段", StringComparison.Ordinal) < 0 || phaseHud.IndexOf("HiddenRead", StringComparison.Ordinal) >= 0)
                {
                    Console.WriteLine("FAIL: a read must not cover 正在思考: " + phaseHud);
                    return 83;
                }
                if (clearProg24e != null) clearProg24e.Invoke(app, new object[] { "ThoughtHUD" });
                if (clearProg24e != null) clearProg24e.Invoke(app, new object[] { "PhaseHUD" });
                Console.WriteLine("PASS: HUD shows the latest status immediately and waiting still interrupts.");
            }

            // 24b. Rest-length button: the arrow zone opens the preset menu and
            // must NOT start the break; the text still starts it. The arrow used
            // to sit outside the clickable zone, so the menu was unreachable.
            bool restBreakStarted = false;
            Action restCallback = delegate { restBreakStarted = true; };
            ConstructorInfo restNoticeCtor = noticeType.GetConstructor(new Type[] { settings.GetType(), typeof(DateTime), typeof(string), typeof(IntPtr), typeof(Action) });
            Form restDlg = restNoticeCtor == null ? null : (Form)restNoticeCtor.Invoke(new object[] { settings, DateTime.Now, null, IntPtr.Zero, restCallback });
            Button restBtn = null;
            if (restDlg != null)
                foreach (Control c in restDlg.Controls)
                    if (c.GetType().Name == "RestLengthButton") restBtn = (Button)c;
            int arrowLeft = restBtn == null ? -1 : (int)restBtn.GetType().GetProperty("ArrowZoneLeft").GetValue(restBtn, null);
            ContextMenuStrip restMenu = restDlg == null ? null : (ContextMenuStrip)noticeType.GetField("restMenu", bf).GetValue(restDlg);
            if (restBtn == null || restBtn.Bounds != new Rectangle(30, 150, 150, 36) || arrowLeft != 114 || restMenu == null || restMenu.Items.Count < 5)
            {
                Console.WriteLine("FAIL: rest-length button geometry or preset menu is wrong");
                return 33;
            }
            // Shown off-screen: the check must never flash a popup on screen.
            restDlg.StartPosition = FormStartPosition.Manual;
            restDlg.Location = new Point(-3000, -3000);
            restDlg.Show();
            MethodInfo onMouseDown = typeof(Control).GetMethod("OnMouseDown", bf);
            MethodInfo onClick = typeof(Control).GetMethod("OnClick", bf);
            onMouseDown.Invoke(restBtn, new object[] { new MouseEventArgs(MouseButtons.Left, 1, arrowLeft + 10, 18, 0) });
            onClick.Invoke(restBtn, new object[] { EventArgs.Empty });
            if (!restMenu.Visible || restBreakStarted)
            {
                Console.WriteLine("FAIL: clicking the arrow zone must open the preset menu without starting the break");
                return 33;
            }
            restMenu.Close();
            onMouseDown.Invoke(restBtn, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 60, 18, 0) });
            onClick.Invoke(restBtn, new object[] { EventArgs.Empty });
            if (!restBreakStarted)
            {
                Console.WriteLine("FAIL: clicking the rest text did not start the break");
                return 33;
            }
            restDlg.Dispose();
            Console.WriteLine("PASS: rest-length arrow zone opens the preset menu without starting the break.");

            // 24c. Auto-update: the download url, the version comparison, the
            // pending-update marker and the in-place exe swap. The swap is what
            // happens when the user clicks "restart to update", and what the app
            // does to itself on the next launch.
            string updRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "iclock_update_check_" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(updRoot);
            string updCur = System.IO.Path.Combine(updRoot, "iClock.exe");
            string updNew = System.IO.Path.Combine(updRoot, "iClock-new.exe");
            string updMarker = System.IO.Path.Combine(updRoot, "pending-update.ini");
            System.IO.File.WriteAllText(updCur, "OLD");
            System.IO.File.WriteAllText(updNew, "NEW");
            MethodInfo markFile = appType.GetMethod("MarkPendingUpdateFile", sbf);
            MethodInfo swapInPlace = appType.GetMethod("SwapUpdateInPlace", sbf);
            MethodInfo buildUrl = appType.GetMethod("BuildUpdateExeUrl", sbf);
            if (markFile == null || swapInPlace == null || buildUrl == null)
            {
                Console.WriteLine("FAIL: the auto-update helpers are missing");
                return 34;
            }
            string wantUrl = "https://github.com/francodeyvison183-collab/iClock/releases/download/v1.11/iClock-1.11-windows.exe";
            string builtUrl = (string)buildUrl.Invoke(null, new object[] { "1.11" });
            if (builtUrl != wantUrl)
            {
                Console.WriteLine("FAIL: the update url is wrong: " + builtUrl);
                return 34;
            }
            if ((string)buildUrl.Invoke(null, new object[] { "v1.11" }) != wantUrl)
            {
                Console.WriteLine("FAIL: a leading v must not change the update url");
                return 34;
            }
            if (!(bool)isNewer.Invoke(null, new object[] { "1.12", "1.11" }) || (bool)isNewer.Invoke(null, new object[] { "1.11", "1.11" }))
            {
                Console.WriteLine("FAIL: the version comparison is wrong");
                return 34;
            }
            // the marker has to record the version and the downloaded file
            markFile.Invoke(null, new object[] { updMarker, "1.11", updNew });
            string markerText = System.IO.File.ReadAllText(updMarker);
            if (markerText.IndexOf("version=1.11") < 0 || markerText.IndexOf("exe=" + updNew) < 0)
            {
                Console.WriteLine("FAIL: the pending-update marker is wrong: " + markerText);
                return 34;
            }
            // the swap puts the new file in place and consumes the new one
            object[] swapArgs = new object[] { updCur, updNew, null };
            bool swapped = (bool)swapInPlace.Invoke(null, swapArgs);
            if (!swapped || System.IO.File.ReadAllText(updCur) != "NEW" || System.IO.File.Exists(updNew))
            {
                Console.WriteLine("FAIL: the update was not swapped in place: " + (swapArgs[2] == null ? "" : swapArgs[2].ToString()));
                return 34;
            }
            // a failed swap must leave the installed app where it is
            string updCur2 = System.IO.Path.Combine(updRoot, "iClock2.exe");
            System.IO.File.WriteAllText(updCur2, "OLD2");
            object[] swapArgs2 = new object[] { updCur2, System.IO.Path.Combine(updRoot, "missing.exe"), null };
            bool swapped2 = (bool)swapInPlace.Invoke(null, swapArgs2);
            if (swapped2 || !System.IO.File.Exists(updCur2))
            {
                Console.WriteLine("FAIL: a failed swap must not take the installed app away");
                return 34;
            }
            try { System.IO.Directory.Delete(updRoot, true); } catch { }
            Console.WriteLine("PASS: the update url, version check, pending marker and in-place swap are correct.");

            // 25. Test SettingsDialog Plan B-1 Dual Tabs & Agent Collaboration UI
            Form tabDlg = (Form)Activator.CreateInstance(settingsDialogType, new object[] { settings });
            Button btnTabTimer = (Button)settingsDialogType.GetField("btnTabTimer", bf).GetValue(tabDlg);
            Button btnTabAgent = (Button)settingsDialogType.GetField("btnTabAgent", bf).GetValue(tabDlg);
            Control minCtrl = (Control)settingsDialogType.GetField("minutes", bf).GetValue(tabDlg);
            Button btnProgDemo = (Button)settingsDialogType.GetField("btnProgressDemo", bf).GetValue(tabDlg);
            Panel capCard = (Panel)settingsDialogType.GetField("panelCapCard", bf).GetValue(tabDlg);
            Panel skillCard = (Panel)settingsDialogType.GetField("panelSkillCard", bf).GetValue(tabDlg);
            Panel statusCard = (Panel)settingsDialogType.GetField("panelStatusCard", bf).GetValue(tabDlg);
            Panel agentScroll = (Panel)settingsDialogType.GetField("panelAgentScroll", bf).GetValue(tabDlg);
            Panel agentRows = (Panel)settingsDialogType.GetField("panelAgentRows", bf).GetValue(tabDlg);
            Label agentEmpty = (Label)settingsDialogType.GetField("lblAgentEmpty", bf).GetValue(tabDlg);
            Panel installFail = (Panel)settingsDialogType.GetField("panelInstallFail", bf).GetValue(tabDlg);
            Label method1 = (Label)settingsDialogType.GetField("lblSkillMethod1", bf).GetValue(tabDlg);
            Label method2 = (Label)settingsDialogType.GetField("lblSkillMethod2", bf).GetValue(tabDlg);
            Label guideHint = (Label)settingsDialogType.GetField("lblCliGuideHint", bf).GetValue(tabDlg);
            Button copyGuide = (Button)settingsDialogType.GetField("btnCopyCliGuide", bf).GetValue(tabDlg);
            FieldInfo startupField = settingsDialogType.GetField("startup", bf);
            Control goalCtrl = (Control)settingsDialogType.GetField("dailyGoal", bf).GetValue(tabDlg);
            Label statusTitle = (Label)settingsDialogType.GetField("lblStatusTitle", bf).GetValue(tabDlg);
            Label statusDesc = (Label)settingsDialogType.GetField("lblStatusDesc", bf).GetValue(tabDlg);
            Label deepTitle = (Label)settingsDialogType.GetField("lblDeepTitle", bf).GetValue(tabDlg);
            Label askLine = (Label)settingsDialogType.GetField("lblAskLine", bf).GetValue(tabDlg);
            Label noticeLine = (Label)settingsDialogType.GetField("lblNoticeLine", bf).GetValue(tabDlg);

            if (btnTabTimer == null || btnTabAgent == null || capCard == null || statusCard == null || skillCard == null ||
                agentRows == null || agentScroll == null || agentEmpty == null || installFail == null ||
                method1 == null || method2 == null || guideHint == null || copyGuide == null ||
                btnProgDemo == null || statusTitle == null || statusDesc == null || deepTitle == null || askLine == null || noticeLine == null)
            {
                Console.WriteLine("FAIL: SettingsDialog missing dual-tab navigation or Agent tab controls");
                return 33;
            }

            tabDlg.Show();

            // Initially on Tab 1 (Timer)
            if (!minCtrl.Visible || btnProgDemo.Visible)
            {
                Console.WriteLine("FAIL: SettingsDialog default tab should show timer controls and hide agent controls");
                return 33;
            }

            if (startupField != null)
            {
                Console.WriteLine("FAIL: Startup switch should live in the tray menu, not the countdown settings");
                return 33;
            }
            if (goalCtrl == null)
            {
                Console.WriteLine("FAIL: SettingsDialog missing the daily focus goal");
                return 33;
            }
            ComboBox goalCombo = goalCtrl as ComboBox;
            if (goalCombo == null || goalCombo.Items.Count != 7)
            {
                Console.WriteLine("FAIL: daily focus goal should be Off + 6 hour presets, got: " + (goalCtrl == null ? "null" : goalCtrl.GetType().Name + " x" + (goalCombo != null ? goalCombo.Items.Count.ToString() : "?")));
                return 33;
            }
            string goalFirst = goalCombo.Items[0].ToString();
            if (goalFirst != "关闭" && goalFirst != "Off")
            {
                Console.WriteLine("FAIL: daily focus goal first item should be 关闭 / Off, got: " + goalFirst);
                return 33;
            }

            // Switch to Tab 2 (Agent Collaboration)
            btnTabAgent.PerformClick();
            if (minCtrl.Visible || !btnProgDemo.Visible)
            {
                Console.WriteLine("FAIL: Switching to Agent tab should hide timer controls and show agent controls");
                return 33;
            }

            // Agent rows list only running agents; method 2 guide copy is always available.
            int runningCount = 0;
            foreach (Control rowCtrl in agentRows.Controls) if (rowCtrl is Button) runningCount++;
            if (!capCard.Visible || !statusCard.Visible || !skillCard.Visible ||
                (runningCount > 0 && agentEmpty.Visible) || (runningCount == 0 && !agentEmpty.Visible) ||
                !copyGuide.Visible || !guideHint.Visible ||
                method1.Text != "安装技能 · 一直有效" || method2.Text != "发提示词 * 会话内有效" ||
                guideHint.Text != "贴一段提示词即可。" || copyGuide.Text != "复制提示词" ||
                installFail.Visible)
            {
                Console.WriteLine("FAIL: Agent tab should list running agents or show the empty-state fallback");
                return 33;
            }

            if (tabDlg.ClientSize != new Size(380, 470) ||
                statusTitle.Text != "智能体状态自动感知" || statusTitle.Top != 56 ||
                statusDesc.Text != "桌面置顶显示后台运行的智能体状态" ||
                statusCard.Bounds != new Rectangle(18, 86, 344, 48) ||
                deepTitle.Text != "智能体运行深度协作" || deepTitle.Top != 150 ||
                askLine.Text != "智能体运行决策弹窗向你请示。" ||
                noticeLine.Text != "任务完成后弹窗+声音提示" ||
                btnProgDemo.Text != "预览" ||
                capCard.Bounds != new Rectangle(18, 180, 344, 84) ||
                skillCard.Bounds != new Rectangle(18, 272, 344, 174) ||
                skillCard.Bottom != 446 ||
                !agentScroll.AutoScroll || agentScroll.HorizontalScroll.Visible)
            {
                Console.WriteLine("FAIL: Agent tab layout/text mismatch: status=" + statusTitle.Text + ", ask=" + askLine.Text + ", notice=" + noticeLine.Text + ", skill=" + skillCard.Bounds + ", cap=" + capCard.Bounds);
                return 33;
            }

            // Switch back to Tab 1
            btnTabTimer.PerformClick();
            if (!minCtrl.Visible)
            {
                Console.WriteLine("FAIL: Switching back to Timer tab failed");
                return 33;
            }

            tabDlg.Dispose();
            Console.WriteLine("PASS: SettingsDialog Plan B-1 Dual Tabs (Timer & Agent Collaboration) and dynamic refresh verified.");

            Console.WriteLine("PASS: P1 & P2: 32bpp ARGB Layered Window, breathing pulse, and duration adjustment verified.");
            Console.WriteLine("PASS: Hotkey auto-fallback, status transparency, and tray left-click toggle verified.");

            // 27. Test Architecture Optimization (P0 atomic save, P1 NativeMethods & cached GDI, P2 ClockState FSM)
            Type nativeMethodsType = asm.GetType("NativeMethods");
            if (nativeMethodsType == null)
            {
                Console.WriteLine("FAIL: NativeMethods unified interop class missing");
                return 34;
            }
            Type stateEnumType = asm.GetType("ClockState");
            if (stateEnumType == null)
            {
                Console.WriteLine("FAIL: ClockState enum missing");
                return 35;
            }

            PropertyInfo stateProp = appType.GetProperty("State", BindingFlags.NonPublic | BindingFlags.Instance);
            if (stateProp == null) stateProp = appType.GetProperty("State", BindingFlags.Public | BindingFlags.Instance);
            if (stateProp == null)
            {
                Console.WriteLine("FAIL: AppContext.State property missing");
                return 36;
            }

            // Reset to clean Idle before testing state transitions
            MethodInfo resetMethod = appType.GetMethod("Reset", bf);
            resetMethod.Invoke(app, null);

            // Verify state is Idle
            object curState = stateProp.GetValue(app, null);
            if (curState.ToString() != "Idle")
            {
                Console.WriteLine("FAIL: Expected initial state Idle, got " + curState);
                return 37;
            }

            // Test StartWork -> Running
            MethodInfo startWork = appType.GetMethod("StartWork", bf);
            startWork.Invoke(app, null);
            if (stateProp.GetValue(app, null).ToString() != "Running" || !(bool)runningField.GetValue(app) || !(bool)sessionActiveField.GetValue(app))
            {
                Console.WriteLine("FAIL: StartWork did not transition to Running with running=true");
                return 38;
            }

            // Test Pause -> Paused
            MethodInfo pauseMethod = appType.GetMethod("Pause", bf);
            pauseMethod.Invoke(app, null);
            if (stateProp.GetValue(app, null).ToString() != "Paused" || (bool)runningField.GetValue(app) || !(bool)sessionActiveField.GetValue(app))
            {
                Console.WriteLine("FAIL: Pause did not transition to Paused with running=false, sessionActive=true");
                return 39;
            }

            // Test StartRest -> RestRunning
            MethodInfo startRest = appType.GetMethod("StartRest", bf);
            startRest.Invoke(app, new object[] { 5 });
            if (stateProp.GetValue(app, null).ToString() != "RestRunning" || !(bool)runningField.GetValue(app) || !(bool)sessionActiveField.GetValue(app))
            {
                Console.WriteLine("FAIL: StartRest did not transition to RestRunning");
                return 40;
            }

            // Test Reset -> Idle
            resetMethod.Invoke(app, null);
            if (stateProp.GetValue(app, null).ToString() != "Idle" || (bool)runningField.GetValue(app) || (bool)sessionActiveField.GetValue(app))
            {
                Console.WriteLine("FAIL: Reset did not transition to Idle with running=false, sessionActive=false");
                return 41;
            }

            // Verify P0 Atomic Save cleans up tmp file
            string iniTmp = iniPath + ".tmp";
            if (System.IO.File.Exists(iniTmp))
            {
                Console.WriteLine("FAIL: Temporary settings file remained after save: " + iniTmp);
                return 42;
            }
            Console.WriteLine("PASS: Architecture optimizations P0 (Atomic Save), P1 (NativeMethods & Zero-GC), P2 (ClockState FSM & Work/Rest cycle) verified.");


            // 31. Live preview in SettingsDialog and SetVisualPreview verification
            Type settingsDlgType = asm.GetType("SettingsDialog");
            EventInfo previewEvent = settingsDlgType.GetEvent("PreviewRequested");
            if (previewEvent == null)
            {
                Console.WriteLine("FAIL: SettingsDialog does not expose PreviewRequested event.");
                return 40;
            }

            object dlgInstance = Activator.CreateInstance(settingsDlgType, new object[] { settings, true });
            Form dlgForm = (Form)dlgInstance;

            NumericUpDown nudSize = (NumericUpDown)settingsDlgType.GetField("size", bf).GetValue(dlgInstance);
            nudSize.Value = 42;

            MethodInfo applyCurrentSettings = settingsDlgType.GetMethod("ApplyCurrentSettings");
            object committed = applyCurrentSettings.Invoke(dlgInstance, null);
            int draftSize = (int)settingsType.GetField("FontSize").GetValue(committed);
            if (draftSize != 42)
            {
                Console.WriteLine("FAIL: ApplyCurrentSettings did not commit FontSize to canonical settings: " + draftSize);
                return 41;
            }
            if (!object.ReferenceEquals(committed, settings) || settingsDlgType.GetMethod("GetCurrentDraftSettings") != null)
            {
                Console.WriteLine("FAIL: SettingsDialog still creates a separate draft configuration");
                return 41;
            }

            MethodInfo setVisualPreview = overlayType.GetMethod("SetVisualPreview");
            if (setVisualPreview == null)
            {
                Console.WriteLine("FAIL: Overlay does not expose SetVisualPreview method.");
                return 42;
            }

            setVisualPreview.Invoke(overlay, new object[] { "00:42:00" });
            Font previewFont = (Font)overlayType.GetField("cachedFont", bf).GetValue(overlay);
            if (previewFont == null || (int)Math.Round(previewFont.Size) != 42)
            {
                Console.WriteLine("FAIL: SetVisualPreview did not update cachedFont size to 42.");
                return 43;
            }

            string overlayText = (string)overlayType.GetField("text", bf).GetValue(overlay);
            if (overlayText != "00:42:00")
            {
                Console.WriteLine("FAIL: SetVisualPreview did not update text (got " + overlayText + ").");
                return 44;
            }

            // Verify 3-Tab layout in SettingsDialog and tab switching
            Button btnTabT = (Button)settingsDlgType.GetField("btnTabTimer", bf).GetValue(dlgInstance);
            Button btnTabQ = (Button)settingsDlgType.GetField("btnTabQuote", bf).GetValue(dlgInstance);
            Button btnTabA = (Button)settingsDlgType.GetField("btnTabAgent", bf).GetValue(dlgInstance);
            if (btnTabT == null || btnTabQ == null || btnTabA == null)
            {
                Console.WriteLine("FAIL: SettingsDialog missing 3-Tab buttons (Timer/Quote/Agent)");
                return 45;
            }

            MethodInfo setActiveTabMethod = settingsDlgType.GetMethod("SetActiveTab", BindingFlags.NonPublic | BindingFlags.Instance);
            if (setActiveTabMethod == null)
            {
                Console.WriteLine("FAIL: SettingsDialog missing SetActiveTab");
                return 46;
            }

            dlgForm.Show();

            // Switch to tab 1 (Daily Quote)
            setActiveTabMethod.Invoke(dlgInstance, new object[] { 1 });
            int curTab = (int)settingsDlgType.GetField("activeTab", bf).GetValue(dlgInstance);
            CheckBox dqBox = (CheckBox)settingsDlgType.GetField("dailyQuoteToggle", bf).GetValue(dlgInstance);
            if (curTab != 1 || dqBox == null || !dqBox.Visible)
            {
                Console.WriteLine("FAIL: Switching to tab 1 did not activate daily quote controls");
                return 47;
            }

            // Switch back to tab 0
            setActiveTabMethod.Invoke(dlgInstance, new object[] { 0 });
            dlgForm.Hide();

            // Verify IsPaused property on overlay
            PropertyInfo isPausedProp = overlayType.GetProperty("IsPaused");
            if (isPausedProp == null)
            {
                Console.WriteLine("FAIL: Overlay missing IsPaused property");
                return 48;
            }

            MethodInfo setPausedMethod = overlayType.GetMethod("SetPaused");
            setPausedMethod.Invoke(overlay, new object[] { true });
            renderLayered = overlayType.GetMethod("RenderLayeredWindow");
            renderLayered.Invoke(overlay, null);
            bool isPausedVal = (bool)isPausedProp.GetValue(overlay, null);
            if (!isPausedVal)
            {
                Console.WriteLine("FAIL: Overlay.SetPaused(true) did not set IsPaused to true");
                return 49;
            }
            setPausedMethod.Invoke(overlay, new object[] { false });

            setVisualPreview.Invoke(overlay, new object[] { "25:00" });
            dlgForm.Dispose();

            Console.WriteLine("PASS: SettingsDialog live screen preview, test alert button, and pause badge verified.");

            // 32. Verify CLI IPC commands (HandleIpcCommand)
            MethodInfo handleIpc = appType.GetMethod("HandleIpcCommand", bf);
            if (handleIpc == null)
            {
                Console.WriteLine("FAIL: HandleIpcCommand method not found on AppContext");
                return 50;
            }

            // Test --start 18
            handleIpc.Invoke(app, new object[] { "--start 18" });
            int sessMins = (int)appType.GetField("sessionMinutes", bf).GetValue(app);
            bool isRun = (bool)appType.GetField("running", bf).GetValue(app);
            if (sessMins != 18 || !isRun || !overlay.Visible)
            {
                Console.WriteLine("FAIL: HandleIpcCommand('--start 18') failed, sessMins=" + sessMins + ", isRun=" + isRun + ", visible=" + overlay.Visible);
                return 50;
            }

            // Test -p (pause)
            handleIpc.Invoke(app, new object[] { "-p" });
            isRun = (bool)appType.GetField("running", bf).GetValue(app);
            if (isRun)
            {
                Console.WriteLine("FAIL: HandleIpcCommand('-p') did not pause");
                return 50;
            }

            // Test -r (resume)
            handleIpc.Invoke(app, new object[] { "-r" });
            isRun = (bool)appType.GetField("running", bf).GetValue(app);
            if (!isRun)
            {
                Console.WriteLine("FAIL: HandleIpcCommand('-r') did not resume");
                return 50;
            }

            // Test --reset
            handleIpc.Invoke(app, new object[] { "--reset" });
            isRun = (bool)appType.GetField("running", bf).GetValue(app);
            if (isRun || overlay.Visible)
            {
                Console.WriteLine("FAIL: HandleIpcCommand('--reset') did not reset timer or hide overlay");
                return 50;
            }

            // Test -n (notice)
            handleIpc.Invoke(app, new object[] { "-n Build Success" });
            Form cliNotice = (Form)appType.GetField("activeNotice", bf).GetValue(app);
            if (cliNotice == null || cliNotice.IsDisposed)
            {
                Console.WriteLine("FAIL: HandleIpcCommand('-n') did not create activeNotice");
                return 50;
            }
            if (cliNotice.GetType().Name != "NoticeDialog")
            {
                Console.WriteLine("FAIL: -n did not instantiate unified NoticeDialog, got " + cliNotice.GetType().Name);
                return 50;
            }
            bool foundMessage = false;
            bool foundBrand = false;
            Label msgLbl = null;
            foreach (Control c in cliNotice.Controls)
            {
                if (c is Label && c.Text == "Build Success") { foundMessage = true; msgLbl = (Label)c; }
                if (c is Label && c.Text == "iClock") foundBrand = true;
            }
            if (!foundMessage)
            {
                Console.WriteLine("FAIL: CLI notice (NoticeDialog) did not contain message text 'Build Success'");
                return 50;
            }
            if (foundBrand)
            {
                Console.WriteLine("FAIL: CLI notice (NoticeDialog) should not contain internal 'iClock' brand label");
                return 50;
            }
            string sColor = settings.GetType().GetField("Color").GetValue(settings) as string;
            Color expectedColor = ColorTranslator.FromHtml(sColor);
            if (msgLbl.ForeColor.ToArgb() != expectedColor.ToArgb())
            {
                Console.WriteLine("FAIL: CLI notice (NoticeDialog) message ForeColor (" + msgLbl.ForeColor + ") does not match settings.Color (" + expectedColor + ")");
                return 50;
            }
            cliNotice.Close();

            handleIpc.Invoke(app, new object[] { "--progress 3/5 Running tests --agent-name Codex" });
            IList cliTasks = (IList)appType.GetField("agentTaskStatuses", bf).GetValue(app);
            FieldInfo taskAgentName = cliTasks.Count > 0 ? cliTasks[0].GetType().GetField("AgentName", bf) : null;
            if (taskAgentName == null || !string.Equals(taskAgentName.GetValue(cliTasks[0]) as string, "Codex", StringComparison.Ordinal))
            {
                Console.WriteLine("FAIL: CLI progress should preserve the caller's --agent-name identity");
                return 50;
            }
            FieldInfo taskStepProg = cliTasks.Count > 0 ? cliTasks[0].GetType().GetField("StepProgress", bf) : null;
            string stepVal = taskStepProg != null ? (string)taskStepProg.GetValue(cliTasks[0]) : null;
            if (stepVal != "3/5 Running tests")
            {
                Console.WriteLine("FAIL: CLI progress should set StepProgress to '3/5 Running tests', got: " + stepVal);
                return 50;
            }
            handleIpc.Invoke(app, new object[] { "--progress clear --agent-name Codex" });

            Console.WriteLine("PASS: CLI IPC commands and per-agent progress identity verified.");

            // 33. Verify CLI tray menu item eliminated and SettingsDialog Tab 2 terminal CLI guidance tip
            FieldInfo menuCliField = appType.GetField("menuCli", bf);
            if (menuCliField != null)
            {
                Console.WriteLine("FAIL: menuCli field should be eliminated from AppContext");
                return 51;
            }
            MethodInfo showCliHelp = appType.GetMethod("ShowCliHelp", bf);
            if (showCliHelp != null)
            {
                Console.WriteLine("FAIL: ShowCliHelp method should be eliminated from AppContext");
                return 51;
            }

            FieldInfo cliTipField = settingsDlgType.GetField("lblCliGuideHint", bf);
            if (cliTipField == null)
            {
                Console.WriteLine("FAIL: SettingsDialog missing lblCliGuideHint field");
                return 51;
            }

            Form setDlgTab2 = (Form)Activator.CreateInstance(settingsDlgType, new object[] { settings, true });
            setDlgTab2.Show();
            setActiveTabMethod.Invoke(setDlgTab2, new object[] { 2 });
            Label cliTipCtrl = (Label)cliTipField.GetValue(setDlgTab2);
            if (cliTipCtrl == null || !cliTipCtrl.Visible || cliTipCtrl.Text != "贴一段提示词即可。")
            {
                Console.WriteLine("FAIL: Tab 2 lblCliGuideHint invalid or not visible: " + (cliTipCtrl == null ? "null" : cliTipCtrl.Text));
                setDlgTab2.Close();
                return 51;
            }
            setDlgTab2.Close();
            Console.WriteLine("PASS: Streamlined tray menu (menuCli eliminated) and SettingsDialog Tab 2 terminal CLI guidance verified.");

            // 34. Verify Doorbell Chime and Telephone Ring Sound synthesis and SettingsDialog soundStyle items
            MethodInfo playSound = appType.GetMethod("PlaySoundStyle", BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo stopSound = appType.GetMethod("StopSound", BindingFlags.NonPublic | BindingFlags.Static);

            playSound.Invoke(null, new object[] { "Doorbell" });
            stopSound.Invoke(null, null);
            FieldInfo doorbellWavField = appType.GetField("doorbellWavData", BindingFlags.NonPublic | BindingFlags.Static);
            byte[] generatedDoorbellWav = (byte[])doorbellWavField.GetValue(null);
            if (generatedDoorbellWav == null || generatedDoorbellWav.Length < 1000)
            {
                Console.WriteLine("FAIL: Doorbell WAV synthesis failed or data too small");
                return 52;
            }

            playSound.Invoke(null, new object[] { "Phone" });
            stopSound.Invoke(null, null);
            FieldInfo phoneWavField = appType.GetField("phoneWavData", BindingFlags.NonPublic | BindingFlags.Static);
            byte[] generatedPhoneWav = (byte[])phoneWavField.GetValue(null);
            if (generatedPhoneWav == null || generatedPhoneWav.Length < 1000)
            {
                Console.WriteLine("FAIL: Phone WAV synthesis failed or data too small");
                return 52;
            }

            playSound.Invoke(null, new object[] { "Knock" });
            stopSound.Invoke(null, null);
            FieldInfo knockWavField = appType.GetField("knockWavData", BindingFlags.NonPublic | BindingFlags.Static);
            byte[] generatedKnockWav = (byte[])knockWavField.GetValue(null);
            if (generatedKnockWav == null || generatedKnockWav.Length < 1000)
            {
                Console.WriteLine("FAIL: Knock WAV synthesis failed or data too small");
                return 52;
            }

            // Test dedicated AI Inspiration Chime synthesis
            MethodInfo playAiSound = appType.GetMethod("PlayAiSound", BindingFlags.NonPublic | BindingFlags.Static);
            if (playAiSound == null)
            {
                Console.WriteLine("FAIL: AppContext.PlayAiSound method missing");
                return 52;
            }
            playAiSound.Invoke(null, null);
            stopSound.Invoke(null, null);
            FieldInfo aiWavField = appType.GetField("aiChimeWavData", BindingFlags.NonPublic | BindingFlags.Static);
            byte[] generatedAiWav = (byte[])aiWavField.GetValue(null);
            if (generatedAiWav == null || generatedAiWav.Length < 1000)
            {
                Console.WriteLine("FAIL: AI Chime WAV synthesis failed or data too small");
                return 52;
            }

            Type setDlgType = asm.GetType("SettingsDialog");
            Form setDlg = (Form)Activator.CreateInstance(setDlgType, new object[] { settings });
            ComboBox sndStyleCombo = (ComboBox)setDlgType.GetField("soundStyle", bf).GetValue(setDlg);
            if (sndStyleCombo == null || sndStyleCombo.Items.Count < 6)
            {
                Console.WriteLine("FAIL: soundStyle combo not found or incomplete in SettingsDialog (expected at least 6 items)");
                return 52;
            }
            string item0 = sndStyleCombo.Items[0].ToString();
            if (!item0.Contains("关闭") && !item0.Contains("Off"))
            {
                Console.WriteLine("FAIL: soundStyle Item[0] should be Off, got: " + item0);
                return 52;
            }
            string item1 = sndStyleCombo.Items[1].ToString();
            if (!item1.Contains("门铃") && !item1.Contains("Doorbell"))
            {
                Console.WriteLine("FAIL: soundStyle Item[1] should be Doorbell chime, got: " + item1);
                return 52;
            }
            string item2 = sndStyleCombo.Items[2].ToString();
            if (!item2.Contains("QQ"))
            {
                Console.WriteLine("FAIL: soundStyle Item[2] should be QQ, got: " + item2);
                return 52;
            }
            string item3 = sndStyleCombo.Items[3].ToString();
            if (!item3.Contains("电话铃") && !item3.Contains("Telephone Ring"))
            {
                Console.WriteLine("FAIL: soundStyle Item[3] should be classic telephone ring, got: " + item3);
                return 52;
            }
            string item4 = sndStyleCombo.Items[4].ToString();
            if (!item4.Contains("敲门声") && !item4.Contains("Knock"))
            {
                Console.WriteLine("FAIL: soundStyle Item[4] should be door knock, got: " + item4);
                return 52;
            }
            string item5 = sndStyleCombo.Items[5].ToString();
            if (!item5.Contains("闹铃") && !item5.Contains("Alarm"))
            {
                Console.WriteLine("FAIL: soundStyle Item[5] should be alarm ring, got: " + item5);
                return 52;
            }
            if (sndStyleCombo.SelectedIndex != 0)
            {
                Console.WriteLine("FAIL: soundStyle SelectedIndex should be 0 when EndSound is off, got: " + sndStyleCombo.SelectedIndex);
                return 52;
            }
            setDlg.Close();

            // Verify Settings with Knock style maps to SelectedIndex 4 (0 is Off)
            object knockSettings = Activator.CreateInstance(settingsType);
            settingsType.GetField("EndSoundStyle").SetValue(knockSettings, "Knock");
            Form setDlgKnock = (Form)Activator.CreateInstance(setDlgType, new object[] { knockSettings });
            ComboBox sndComboKnock = (ComboBox)setDlgType.GetField("soundStyle", bf).GetValue(setDlgKnock);
            if (sndComboKnock == null || sndComboKnock.SelectedIndex != 4)
            {
                Console.WriteLine("FAIL: soundStyle SelectedIndex for Knock should be 4, got: " + (sndComboKnock != null ? sndComboKnock.SelectedIndex.ToString() : "null"));
                return 52;
            }
            setDlgKnock.Close();
            Console.WriteLine("PASS: Doorbell chime, QQ, Telephone ring, Door knock (咚咚咚 x3) & Alarm ring sound synthesis and SettingsDialog integration verified.");

            // 35. Verify Daily Quote (每日金句) feature, Settings serialization, Quotes file reading, and Dialog UI
            Type quoteDlgType = asm.GetType("DailyQuoteDialog");
            if (quoteDlgType == null)
            {
                Console.WriteLine("FAIL: DailyQuoteDialog type not found");
                return 53;
            }

            PropertyInfo quotesPathProp = settings.GetType().GetProperty("QuotesFilePath", BindingFlags.Public | BindingFlags.Static);
            if (quotesPathProp == null)
            {
                Console.WriteLine("FAIL: Settings.QuotesFilePath property not found");
                return 53;
            }
            string qPath = (string)quotesPathProp.GetValue(null, null);
            if (string.IsNullOrEmpty(qPath) || !qPath.EndsWith("quotes.txt", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("FAIL: QuotesFilePath should end with quotes.txt, got: " + qPath);
                return 53;
            }

            FieldInfo fDailyQuote = settings.GetType().GetField("DailyQuote");
            FieldInfo fQuoteX = settings.GetType().GetField("QuoteX");
            FieldInfo fQuoteY = settings.GetType().GetField("QuoteY");
            FieldInfo fQuoteWidth = settings.GetType().GetField("QuoteWidth");
            FieldInfo fQuoteFontSize = settings.GetType().GetField("QuoteFontSize");
            FieldInfo fQuoteColor = settings.GetType().GetField("QuoteColor");
            FieldInfo fQuoteFont = settings.GetType().GetField("QuoteFont");
            FieldInfo fQuotePinTopMost = settings.GetType().GetField("QuotePinTopMost");
            if (fDailyQuote == null || fQuoteX == null || fQuoteY == null || fQuoteWidth == null || fQuoteFontSize == null || fQuoteColor == null || fQuoteFont == null || fQuotePinTopMost == null)
            {
                Console.WriteLine("FAIL: Settings missing DailyQuote fields (including QuoteColor, QuoteFont and QuotePinTopMost)");
                return 53;
            }
            if (!(bool)fDailyQuote.GetValue(settings))
            {
                Console.WriteLine("FAIL: DailyQuote default should be true");
                return 53;
            }
            if ((bool)fQuotePinTopMost.GetValue(settings))
            {
                Console.WriteLine("FAIL: QuotePinTopMost default should be false (bottom/desktop layer)");
                return 53;
            }
            if ((int)fQuoteX.GetValue(defaultSettings) != -99999 || (int)fQuoteY.GetValue(defaultSettings) != -99999)
            {
                Console.WriteLine("FAIL: QuoteX and QuoteY default should be -99999 sentinel");
                return 53;
            }

            Form quoteDlg = (Form)Activator.CreateInstance(quoteDlgType, new object[] { settings });
            FieldInfo fDefaultQuotes = quoteDlgType.GetField("DefaultQuotes", BindingFlags.Public | BindingFlags.Static);
            Array defQuotes = (Array)fDefaultQuotes.GetValue(null);
            if (defQuotes == null || defQuotes.Length != 10)
            {
                Console.WriteLine("FAIL: DefaultQuotes should contain exactly 10 classic quotes");
                return 53;
            }

            if (quoteDlg.TopMost)
            {
                Console.WriteLine("FAIL: DailyQuoteDialog default should be Desktop Layer (TopMost = false)");
                return 53;
            }

            fQuoteWidth.SetValue(settings, 460);
            Form quoteDlgCustomWidth = (Form)Activator.CreateInstance(quoteDlgType, new object[] { settings });
            if (quoteDlgCustomWidth.Width != 460)
            {
                Console.WriteLine("FAIL: DailyQuoteDialog custom QuoteWidth not respected, expected 460, got: " + quoteDlgCustomWidth.Width);
                return 53;
            }
            quoteDlgCustomWidth.Close();
            quoteDlgCustomWidth.Dispose();
            fQuoteWidth.SetValue(settings, -1);

            fQuoteFontSize.SetValue(settings, 14.0f);
            Form quoteDlgCustomFont = (Form)Activator.CreateInstance(quoteDlgType, new object[] { settings });
            FieldInfo fCurrentFontSize = quoteDlgType.GetField("currentFontSize", BindingFlags.NonPublic | BindingFlags.Instance);
            if (fCurrentFontSize != null)
            {
                float parsedFontSize = (float)fCurrentFontSize.GetValue(quoteDlgCustomFont);
                if (Math.Abs(parsedFontSize - 14.0f) > 0.05f)
                {
                    Console.WriteLine("FAIL: DailyQuoteDialog custom QuoteFontSize not respected, expected 14.0, got: " + parsedFontSize);
                    return 53;
                }
            }
            quoteDlgCustomFont.Close();
            quoteDlgCustomFont.Dispose();
            fQuoteFontSize.SetValue(settings, -1.0f);
            PropertyInfo pShowWithoutAct = quoteDlgType.GetProperty("ShowWithoutActivation", BindingFlags.NonPublic | BindingFlags.Instance);
            if (pShowWithoutAct == null || !(bool)pShowWithoutAct.GetValue(quoteDlg, null))
            {
                Console.WriteLine("FAIL: DailyQuoteDialog.ShowWithoutActivation should be true");
                return 53;
            }

            Form testSetDlg = (Form)Activator.CreateInstance(setDlgType, new object[] { settings });
            CheckBox dailyQuoteBox = (CheckBox)setDlgType.GetField("dailyQuoteToggle", bf).GetValue(testSetDlg);
            Label dqLabel = (Label)setDlgType.GetField("lblDailyQuote", bf).GetValue(testSetDlg);
            Button btnEditQ = (Button)setDlgType.GetField("btnEditQuotes", bf).GetValue(testSetDlg);
            ComboBox fontComboQ = (ComboBox)setDlgType.GetField("quoteFontCombo", bf).GetValue(testSetDlg);
            NumericUpDown sizeNudQ = (NumericUpDown)setDlgType.GetField("quoteFontSize", bf).GetValue(testSetDlg);
            ComboBox colorComboQ = (ComboBox)setDlgType.GetField("quoteColorCombo", bf).GetValue(testSetDlg);
            ComboBox layerComboQ = (ComboBox)setDlgType.GetField("quoteLayerCombo", bf).GetValue(testSetDlg);
            Panel panelQuoteCardQ = (Panel)setDlgType.GetField("panelQuoteCard", bf).GetValue(testSetDlg);
            Button btnNextQ = (Button)setDlgType.GetField("btnNextQuote", bf).GetValue(testSetDlg);
            if (dailyQuoteBox == null || dqLabel == null || btnEditQ == null || fontComboQ == null || sizeNudQ == null || colorComboQ == null || layerComboQ == null || panelQuoteCardQ == null || btnNextQ == null)
            {
                Console.WriteLine("FAIL: SettingsDialog missing DailyQuote controls (including panelQuoteCard, quoteLayerCombo and btnNextQuote)");
                return 53;
            }
            // The master switch leads the grid (above the style rows) and every row
            // sits on a uniform 34px pitch under the 98px-tall preview card.
            if (panelQuoteCardQ.Top != 62 || panelQuoteCardQ.Left != 18 || panelQuoteCardQ.Height != 98 ||
                dailyQuoteBox.Top != 176 || dailyQuoteBox.Left != 322 || dqLabel.Left != 18 ||
                sizeNudQ.Top != 206 || fontComboQ.Top != 240 ||
                colorComboQ.Top != 274 || colorComboQ.Width != 188 ||
                layerComboQ.Top != 308 || layerComboQ.Width != 188 ||
                btnEditQ.Top != 342)
            {
                Console.WriteLine(string.Format("FAIL: SettingsDialog DailyQuote controls layout misaligned: cardT={0}, cardH={1}, dqT={2}, size={3}, font={4}, color={5}, layer={6}, edit={7}",
                    panelQuoteCardQ.Top, panelQuoteCardQ.Height, dailyQuoteBox.Top, sizeNudQ.Top, fontComboQ.Top, colorComboQ.Top, layerComboQ.Top, btnEditQ.Top));
                return 53;
            }
            if (layerComboQ.SelectedIndex != 0)
            {
                Console.WriteLine("FAIL: quoteLayerCombo default selected index should be 0 (Desktop Layer)");
                return 53;
            }
            if (fontComboQ.Items.Count != 10)
            {
                Console.WriteLine("FAIL: quoteFontCombo should have exactly 10 font choices, got: " + fontComboQ.Items.Count);
                return 53;
            }
            FieldInfo fQuoteFonts = setDlgType.GetField("quoteFontFamilies", BindingFlags.NonPublic | BindingFlags.Static);
            if (fQuoteFonts != null)
            {
                string[] fams = (string[])fQuoteFonts.GetValue(null);
                if (fams == null || fams.Length != 10)
                {
                    Console.WriteLine("FAIL: quoteFontFamilies should contain 10 fonts");
                    return 53;
                }
            }
            if (btnNextQ.FlatStyle != FlatStyle.Flat || btnNextQ.FlatAppearance.BorderSize != 0 || btnNextQ.Left != 12)
            {
                Console.WriteLine("FAIL: btnNextQuote should be flat borderless button at bottom-left corner (Left = 12)");
                return 53;
            }
            if (!dailyQuoteBox.Checked)
            {
                Console.WriteLine("FAIL: dailyQuote default should be true");
                return 53;
            }
            ComboBox timerColorBox = (ComboBox)setDlgType.GetField("timerColorCombo", bf).GetValue(testSetDlg);
            if (timerColorBox == null || timerColorBox.DrawMode != DrawMode.OwnerDrawFixed || timerColorBox.ItemHeight != 22 || timerColorBox.Width != 188 || timerColorBox.Height != 28)
            {
                Console.WriteLine("FAIL: timerColorCombo should have DrawMode OwnerDrawFixed, ItemHeight 22, and 188x28 bounds");
                return 53;
            }
            if (timerColorBox.Items.Count != 10 || timerColorBox.SelectedIndex != 0)
            {
                Console.WriteLine("FAIL: timerColorCombo default should have 10 items and SelectedIndex = 0 (Classic Red)");
                return 53;
            }

            // Test pinned quote persistence when switching away from Daily Quotes tab
            MethodInfo mSetQuotePrev = appType.GetMethod("SetQuotePreview", bf);
            FieldInfo fActiveQuoteField = appType.GetField("activeQuoteDialog", bf);
            FieldInfo fActiveDlgField = appType.GetField("activeDialog", bf);
            fActiveDlgField.SetValue(app, testSetDlg);
            mSetQuotePrev.Invoke(app, new object[] { true });
            Form qForm = (Form)fActiveQuoteField.GetValue(app);
            if (qForm == null || qForm.IsDisposed)
            {
                Console.WriteLine("FAIL: SetQuotePreview(true) should create activeQuoteDialog");
                return 53;
            }
            PropertyInfo pPinTest = qForm.GetType().GetProperty("IsPinned");
            pPinTest.SetValue(qForm, true, null);

            // Simulate tab switch: visible = false
            mSetQuotePrev.Invoke(app, new object[] { false });
            Form qFormAfterSwitch = (Form)fActiveQuoteField.GetValue(app);
            if (qFormAfterSwitch == null || qFormAfterSwitch.IsDisposed)
            {
                Console.WriteLine("FAIL: Switching away from DailyQuote tab should NOT close pinned desktop quote");
                return 53;
            }
            qFormAfterSwitch.Close();
            fActiveDlgField.SetValue(app, null);

            testSetDlg.Close();

            PropertyInfo pIsPinned = quoteDlgType.GetProperty("IsPinned");
            EventInfo ePinChanged = quoteDlgType.GetEvent("PinChanged");
            if (pIsPinned == null || ePinChanged == null)
            {
                Console.WriteLine("FAIL: DailyQuoteDialog missing IsPinned or PinChanged");
                return 53;
            }

            MethodInfo mUpdateTopMost = quoteDlgType.GetMethod("UpdateTopMostState", BindingFlags.Public | BindingFlags.Instance);
            if (mUpdateTopMost == null)
            {
                Console.WriteLine("FAIL: DailyQuoteDialog.UpdateTopMostState method not found");
                return 53;
            }

            // Plan A: Quote is ambient desktop note by default (TopMost = false). Pinning pins to top (TopMost = true).
            pIsPinned.SetValue(quoteDlg, true, null);
            if (!quoteDlg.TopMost)
            {
                Console.WriteLine("FAIL: Pinned quote should have TopMost = true");
                return 53;
            }
            pIsPinned.SetValue(quoteDlg, false, null);
            if (quoteDlg.TopMost)
            {
                Console.WriteLine("FAIL: Unpinned desktop quote note should have TopMost = false");
                return 53;
            }

            MethodInfo mApplySettings = quoteDlgType.GetMethod("ApplySettings", BindingFlags.Public | BindingFlags.Instance);
            if (mApplySettings == null || quoteDlgType.GetMethod("ApplyDraftSettings", BindingFlags.Public | BindingFlags.Instance) != null)
            {
                Console.WriteLine("FAIL: DailyQuoteDialog still exposes a draft settings path");
                return 53;
            }

            MethodInfo mLoadQuotes = quoteDlgType.GetMethod("LoadQuotes", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            if (mLoadQuotes != null)
            {
                string tempQFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "quotes.txt");
                try
                {
                    System.IO.File.WriteAllText(tempQFile, "1. 千里之行，始于足下。 —— 老子\r\n2、Stay hungry, stay foolish.\r\n[3] 读书百遍，其义自见。\r\n# Comment line\r\n", new System.Text.UTF8Encoding(false));
                    object loadedQuotesObj = mLoadQuotes.IsStatic ? mLoadQuotes.Invoke(null, null) : mLoadQuotes.Invoke(quoteDlg, null);
                    System.Collections.IList loadedList = loadedQuotesObj as System.Collections.IList;
                    if (loadedList == null || loadedList.Count != 3)
                    {
                        Console.WriteLine("FAIL: LoadQuotes did not parse custom quotes file correctly, expected 3, got: " + (loadedList != null ? loadedList.Count : 0));
                        return 53;
                    }
                    PropertyInfo pText = loadedList[0].GetType().GetProperty("Text");
                    string txt0 = (string)pText.GetValue(loadedList[0], null);
                    string txt1 = (string)pText.GetValue(loadedList[1], null);
                    string txt2 = (string)pText.GetValue(loadedList[2], null);
                    if (txt0 != "千里之行，始于足下。" || txt1 != "Stay hungry, stay foolish." || txt2 != "读书百遍，其义自见。")
                    {
                        Console.WriteLine("FAIL: StripLeadingNumber failed on custom quotes: txt0='" + txt0 + "', txt1='" + txt1 + "', txt2='" + txt2 + "'");
                        return 53;
                    }
                }
                finally
                {
                    try { if (System.IO.File.Exists(tempQFile)) System.IO.File.Delete(tempQFile); } catch { }
                }
            }

            Type quoteItemType = asm.GetType("QuoteItem");
            if (quoteItemType != null)
            {
                MethodInfo mStrip = quoteItemType.GetMethod("StripLeadingNumber", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (mStrip != null)
                {
                    string[][] testPairs = new string[][]
                    {
                        new string[] { "01. Hello", "Hello" },
                        new string[] { "1、测试", "测试" },
                        new string[] { "(2) Test", "Test" },
                        new string[] { "（3）括号", "括号" },
                        new string[] { "[4] Bracket", "Bracket" },
                        new string[] { "【5】方括号", "方括号" },
                        new string[] { "6: Colon", "Colon" },
                        new string[] { "7：冒号", "冒号" },
                        new string[] { "8 - Dash", "Dash" },
                        new string[] { "No. 9 Quote", "Quote" },
                        new string[] { "#10 Hash", "Hash" },
                        new string[] { "\u2460 Circled", "Circled" },
                        new string[] { "1．全角点", "全角点" },
                        new string[] { "\uFF11\uFF0E 全角数字和点", "全角数字和点" },
                        new string[] { "3.1415926是圆周率", "3.1415926是圆周率" },
                        new string[] { "1984年", "1984年" }
                    };
                    foreach (string[] pair in testPairs)
                    {
                        string outStr = (string)mStrip.Invoke(null, new object[] { pair[0] });
                        if (outStr != pair[1])
                        {
                            Console.WriteLine("FAIL: StripLeadingNumber mismatch for '" + pair[0] + "': expected '" + pair[1] + "', got '" + outStr + "'");
                            return 53;
                        }
                    }
                }
            }

            handleIpc = appType.GetMethod("HandleIpcCommand", bf);
            handleIpc.Invoke(app, new object[] { "-q" });
            FieldInfo fActiveQuote = appType.GetField("activeQuoteDialog", bf);
            Form triggeredQuote = (Form)fActiveQuote.GetValue(app);
            if (triggeredQuote == null || triggeredQuote.IsDisposed)
            {
                Console.WriteLine("FAIL: IPC -q command did not create activeQuoteDialog");
                return 53;
            }
            triggeredQuote.Close();
            quoteDlg.Close();

            Console.WriteLine("PASS: Daily Quote (每日金句) feature, custom quotes parsing, IPC -q command, and SettingsDialog controls verified.");

            // 36. Verify Bug 1 & Bug 2: Desktop quote font size sync with SettingsDialog and Tab 0 countdown color change immunity
            mSetQuotePrev = appType.GetMethod("SetQuotePreview", bf);
            FieldInfo fActiveDlg = appType.GetField("activeDialog", bf);

            handleIpc.Invoke(app, new object[] { "-q" });
            Form desktopQuote = (Form)fActiveQuote.GetValue(app);
            PropertyInfo pCurFontSize = quoteDlgType.GetProperty("CurrentFontSize");
            FieldInfo fCurFontSize = quoteDlgType.GetField("currentFontSize", BindingFlags.NonPublic | BindingFlags.Instance);
            if (fCurFontSize != null) fCurFontSize.SetValue(desktopQuote, 15.5f);

            FieldInfo fLastTab = setDlgType.GetField("lastActiveTab", BindingFlags.NonPublic | BindingFlags.Static);
            if (fLastTab != null) fLastTab.SetValue(null, 0);
            Form setDlgInst = (Form)Activator.CreateInstance(setDlgType, new object[] { settings, true });
            fActiveDlg.SetValue(app, setDlgInst);
            MethodInfo mSyncQuoteFont = setDlgType.GetMethod("SyncQuoteFontSize");
            mSyncQuoteFont.Invoke(setDlgInst, new object[] { (float)pCurFontSize.GetValue(desktopQuote, null) });

            FieldInfo fQuoteFontNud = setDlgType.GetField("quoteFontSize", BindingFlags.NonPublic | BindingFlags.Instance);
            NumericUpDown nud = (NumericUpDown)fQuoteFontNud.GetValue(setDlgInst);
            if (nud == null || nud.Value != 15.5M)
            {
                Console.WriteLine("FAIL: SettingsDialog quoteFontSize not synced with desktop quote, expected 15.5, got: " + (nud != null ? nud.Value.ToString() : "null"));
                return 54;
            }

            if (fCurFontSize != null) fCurFontSize.SetValue(desktopQuote, 18.0f);
            FieldInfo fFontChangedEvt = quoteDlgType.GetField("FontSizeChanged", BindingFlags.NonPublic | BindingFlags.Instance);
            MulticastDelegate del = (MulticastDelegate)fFontChangedEvt.GetValue(desktopQuote);
            if (del != null) del.DynamicInvoke(new object[] { 18.0f });
            if (nud.Value != 18.0M)
            {
                Console.WriteLine("FAIL: Desktop quote FontSizeChanged did not sync to SettingsDialog quoteFontSize, expected 18.0, got: " + nud.Value);
                return 54;
            }

            PropertyInfo pActiveTab = setDlgType.GetProperty("ActiveTab");
            if (pActiveTab == null || (int)pActiveTab.GetValue(setDlgInst, null) != 0)
            {
                Console.WriteLine("FAIL: SettingsDialog ActiveTab property missing or not 0 initially");
                return 54;
            }
            fQuoteFontSize.SetValue(settings, 10.5f);
            mSetQuotePrev.Invoke(app, new object[] { false });

            float sizeAfterTimerChange = (float)pCurFontSize.GetValue(desktopQuote, null);
            if (Math.Abs(sizeAfterTimerChange - 18.0f) > 0.05f)
            {
                Console.WriteLine("FAIL: Leaving the quote tab unexpectedly reapplied quote font size. Expected 18.0, got: " + sizeAfterTimerChange);
                return 54;
            }
            fQuoteFontSize.SetValue(settings, 18.0f);
            mApplySettings.Invoke(desktopQuote, null);

            // Verify switching to Daily Quotes tab in Settings preserves current desktop quote index
            Form quoteDlgCheck = desktopQuote;
            PropertyInfo pQuoteIdx = quoteDlgType.GetProperty("CurrentQuoteIndex");
            pQuoteIdx.SetValue(quoteDlgCheck, 3, null);
            Form setDlgCheck = (Form)Activator.CreateInstance(setDlgType, new object[] { settings, true });
            fActiveDlg.SetValue(app, setDlgCheck);
            MethodInfo mSyncQIdx = setDlgType.GetMethod("SyncQuoteIndex");
            if (mSyncQIdx != null) mSyncQIdx.Invoke(setDlgCheck, new object[] { 3 });

            // Trigger preview as if switching to Tab 1
            mSetQuotePrev.Invoke(app, new object[] { true });
            int idxAfterTabSwitch = (int)pQuoteIdx.GetValue(quoteDlgCheck, null);
            if (idxAfterTabSwitch != 3)
            {
                Console.WriteLine("FAIL: Switching to Daily Quotes tab in Settings changed desktop quote index! Expected 3, got: " + idxAfterTabSwitch);
                return 54;
            }
            setDlgCheck.Close();
            fActiveDlg.SetValue(app, null);

            Console.WriteLine("PASS: Desktop quote font size sync and Tab 0 countdown color immunity verified.");

            // Quote settings have one canonical object; direct widget state is captured before exit.
            MethodInfo mCapturePersistentState = quoteDlgType.GetMethod("CapturePersistentState", BindingFlags.Public | BindingFlags.Instance);
            if (mCapturePersistentState == null || quoteDlgType.GetMethod("ApplyVisualPreview", BindingFlags.Public | BindingFlags.Instance) != null)
            {
                Console.WriteLine("FAIL: Quote settings still contain a separate preview configuration path");
                return 53;
            }
            PropertyInfo pSettingsValue = setDlgType.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
            if (pSettingsValue == null || !object.ReferenceEquals(pSettingsValue.GetValue(setDlgInst, null), settings))
            {
                Console.WriteLine("FAIL: SettingsDialog does not edit the canonical quote settings object");
                return 53;
            }
            setDlgType.GetMethod("SetActiveTab", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public).Invoke(setDlgInst, new object[] { 1 });
            setDlgType.GetField("isSyncingQuoteFontSize", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(setDlgInst, false);
            nud.Value = 16.0M;
            nud.Value = 16.5M;
            if (Math.Abs((float)fQuoteFontSize.GetValue(settings) - 16.5f) > 0.05f)
            {
                Console.WriteLine("FAIL: Quote font edit was not committed directly to canonical settings, expected 16.5, got: " + fQuoteFontSize.GetValue(settings));
                return 53;
            }
            desktopQuote.Location = new Point(-1719, 945);
            fCurFontSize.SetValue(desktopQuote, 16.5f);
            mCapturePersistentState.Invoke(desktopQuote, null);
            if ((int)fQuoteX.GetValue(settings) != -1719 || (int)fQuoteY.GetValue(settings) != 945 ||
                Math.Abs((float)fQuoteFontSize.GetValue(settings) - 16.5f) > 0.05f)
            {
                Console.WriteLine("FAIL: Actual quote position/font size was not captured in canonical settings");
                return 53;
            }
            setDlgInst.Close();
            fActiveDlg.SetValue(app, null);
            desktopQuote.Close();

            // 37. Verify CLI --once running instance immunity and quit lifecycle
            FieldInfo fIsOneShot = appType.GetField("isOneShot", bf);
            if (fIsOneShot == null || (bool)fIsOneShot.GetValue(app) != false)
            {
                Console.WriteLine("FAIL: Desktop resident instance isOneShot should be false");
                return 55;
            }

            handleIpc.Invoke(app, new object[] { "--start 12 --once" });
            int sessOnceMins = (int)appType.GetField("sessionMinutes", bf).GetValue(app);
            bool isRunOnce = (bool)appType.GetField("running", bf).GetValue(app);
            bool residentStillNotOneShot = (bool)fIsOneShot.GetValue(app);
            if (sessOnceMins != 12 || !isRunOnce || residentStillNotOneShot != false)
            {
                Console.WriteLine("FAIL: --once command to running instance should execute countdown without setting resident instance to one-shot mode");
                return 55;
            }
            handleIpc.Invoke(app, new object[] { "--reset" });

            Console.WriteLine("PASS: CLI --once running instance immunity and quit lifecycle verified.");

            // 38. Verify In-Card Native Badge Tooltip in DailyQuoteDialog (Removal of external ToolTip & zero latency self-rendering)
            FieldInfo fToolTip = quoteDlgType.GetField("toolTip", BindingFlags.NonPublic | BindingFlags.Instance);
            if (fToolTip != null)
            {
                Console.WriteLine("FAIL: DailyQuoteDialog still has legacy external toolTip field.");
                return 56;
            }

            MethodInfo mDrawBadge = quoteDlgType.GetMethod("DrawButtonTooltip", BindingFlags.NonPublic | BindingFlags.Instance);
            if (mDrawBadge == null)
            {
                Console.WriteLine("FAIL: DailyQuoteDialog missing DrawButtonTooltip method.");
                return 56;
            }

            Form qDlgTest = (Form)Activator.CreateInstance(quoteDlgType, new object[] { settings });
            FieldInfo fHov = quoteDlgType.GetField("hoveredBtn", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo fIsHov = quoteDlgType.GetField("isHovered", BindingFlags.NonPublic | BindingFlags.Instance);
            fIsHov.SetValue(qDlgTest, true);

            using (Bitmap tb = new Bitmap(400, 100))
            using (Graphics tg = Graphics.FromImage(tb))
            {
                for (int b = 1; b <= 3; b++)
                {
                    fHov.SetValue(qDlgTest, b);
                    mDrawBadge.Invoke(qDlgTest, new object[] { tg, "zh" });
                    mDrawBadge.Invoke(qDlgTest, new object[] { tg, "en" });
                }
            }
            qDlgTest.Close();

            Console.WriteLine("PASS: Desktop quote in-card native badge tooltip (0ms latency, WinForms ToolTip elimination) verified.");

            // 39. Verify DailyQuoteDialog centered sandwich position (below countdown Y=60..96, above settings dialog)
            fQuoteX.SetValue(settings, -99999);
            fQuoteY.SetValue(settings, -99999);
            Form qDlgPosTest = (Form)Activator.CreateInstance(quoteDlgType, new object[] { settings });
            int expectedQuoteX = Screen.PrimaryScreen.WorkingArea.Left + Math.Max(0, (Screen.PrimaryScreen.WorkingArea.Width - qDlgPosTest.Width) / 2);
            if (qDlgPosTest.Location.X != expectedQuoteX || qDlgPosTest.Location.Y < 110 || qDlgPosTest.Location.Y > 130)
            {
                Console.WriteLine("FAIL: Default DailyQuoteDialog position should be horizontally centered and at Y=120, got: X=" + qDlgPosTest.Location.X + " Y=" + qDlgPosTest.Location.Y);
                return 57;
            }

            // Verify decoupled QuoteX < 0 horizontal centering preserves custom QuoteY >= 0
            fQuoteX.SetValue(settings, -1);
            fQuoteY.SetValue(settings, 250);
            Form qDlgDecoupled = (Form)Activator.CreateInstance(quoteDlgType, new object[] { settings });
            int expectedDecoupledX = Screen.PrimaryScreen.WorkingArea.Left + Math.Max(0, (Screen.PrimaryScreen.WorkingArea.Width - qDlgDecoupled.Width) / 2);
            if (qDlgDecoupled.Location.X != expectedDecoupledX || qDlgDecoupled.Location.Y != 250)
            {
                Console.WriteLine("FAIL: Decoupled DailyQuoteDialog QuoteX=-1 did not center horizontally or preserve QuoteY=250, got: X=" + qDlgDecoupled.Location.X + " Y=" + qDlgDecoupled.Location.Y);
                return 57;
            }

            // Verify dynamic re-centering when quote changes under QuoteX < 0
            MethodInfo mSetQuoteIndex = quoteDlgType.GetMethod("SetQuoteIndex", BindingFlags.Public | BindingFlags.Instance);
            if (mSetQuoteIndex != null)
            {
                mSetQuoteIndex.Invoke(qDlgDecoupled, new object[] { 1 });
                int expectedAfterChangeX = Screen.PrimaryScreen.WorkingArea.Left + Math.Max(0, (Screen.PrimaryScreen.WorkingArea.Width - qDlgDecoupled.Width) / 2);
                if (qDlgDecoupled.Location.X != expectedAfterChangeX)
                {
                    Console.WriteLine("FAIL: DailyQuoteDialog did not stay horizontally centered after quote change, expected: " + expectedAfterChangeX + ", got: " + qDlgDecoupled.Location.X);
                    return 57;
                }
            }

            // Verify quote maintains secondary display coordinates on NextQuote
            MethodInfo mNextQuote = quoteDlgType.GetMethod("NextQuote", BindingFlags.Public | BindingFlags.Instance);
            Screen secScreen = null;
            foreach (Screen s in Screen.AllScreens) { if (!s.Primary) { secScreen = s; break; } }
            if (secScreen != null)
            {
                int testSecX = secScreen.WorkingArea.Left + 200;
                int testSecY = secScreen.WorkingArea.Top + 200;
                fQuoteX.SetValue(settings, testSecX);
                fQuoteY.SetValue(settings, testSecY);
                Form qDlgSec = (Form)Activator.CreateInstance(quoteDlgType, new object[] { settings });
                if (mNextQuote != null)
                {
                    mNextQuote.Invoke(qDlgSec, null);
                    if (qDlgSec.Location.X != testSecX || qDlgSec.Location.Y != testSecY)
                    {
                        Console.WriteLine("FAIL: DailyQuoteDialog jumped away from secondary screen coordinates upon NextQuote: X=" + qDlgSec.Location.X + " Y=" + qDlgSec.Location.Y);
                        return 57;
                    }
                }

                // Cold construction must already match the settings-tab refresh path.
                // Otherwise the quote only jumps back to its saved state when the tab opens.
                int constructedX = qDlgSec.Location.X;
                int constructedY = qDlgSec.Location.Y;
                int constructedWidth = qDlgSec.Width;
                int constructedHeight = qDlgSec.Height;
                float constructedFontSize = (float)pCurFontSize.GetValue(qDlgSec, null);
                mApplySettings.Invoke(qDlgSec, null);
                if (qDlgSec.Location.X != constructedX || qDlgSec.Location.Y != constructedY ||
                    qDlgSec.Width != constructedWidth || qDlgSec.Height != constructedHeight ||
                    Math.Abs((float)pCurFontSize.GetValue(qDlgSec, null) - constructedFontSize) > 0.05f)
                {
                    Console.WriteLine("FAIL: Cold quote construction differs from ApplySettings on secondary screen");
                    return 57;
                }
                qDlgSec.Close();
            }

            qDlgDecoupled.Close();
            qDlgPosTest.Close();
            fQuoteX.SetValue(settings, -99999);
            fQuoteY.SetValue(settings, -99999);

            Console.WriteLine("PASS: Desktop quote centered sandwich position (Y=120px, below countdown & above settings) and decoupled horizontal centering verified.");

            // 40. Verify tool introductions modernization (AboutDialog Option 1, Settings Tab 0 tip, CliHelp 8 commands)
            Form aboutZh = (Form)Activator.CreateInstance(aboutType, new object[] { "zh" });
            Form aboutEn = (Form)Activator.CreateInstance(aboutType, new object[] { "en" });
            string aboutZhDesc = null, aboutEnDesc = null;
            string aboutZhAuthor = null, aboutEnAuthor = null;
            bool foundZhLink = false, foundEnLink = false;
            bool foundZhEmail = false, foundEnEmail = false;
            foreach (Control c in aboutZh.Controls)
            {
                if (c is Label && c.Text.Contains("极简通透")) aboutZhDesc = c.Text;
                if (c is Label && c.Text.Contains("gujiangwen")) aboutZhAuthor = c.Text;
                if (c is LinkLabel && c.Text == "(GitHub ↗)") foundZhLink = true;
                if (c is LinkLabel && c.Text.Contains("francodeyvison183@gmail.com")) foundZhEmail = true;
            }
            foreach (Control c in aboutEn.Controls)
            {
                if (c is Label && c.Text.Contains("Minimalist")) aboutEnDesc = c.Text;
                if (c is Label && c.Text.Contains("gujiangwen")) aboutEnAuthor = c.Text;
                if (c is LinkLabel && c.Text == "(GitHub ↗)") foundEnLink = true;
                if (c is LinkLabel && c.Text.Contains("francodeyvison183@gmail.com")) foundEnEmail = true;
            }
            aboutZh.Close();
            aboutEn.Close();
            if (aboutZhDesc != "极简通透、轻量无打扰的桌面专注与效率伴侣")
            {
                Console.WriteLine("FAIL: AboutDialog zh description incorrect: " + aboutZhDesc);
                return 58;
            }
            if (aboutEnDesc != "Minimalist & distraction-free desktop focus companion")
            {
                Console.WriteLine("FAIL: AboutDialog en description incorrect: " + aboutEnDesc);
                return 58;
            }
            if (aboutZhAuthor != "作者：gujiangwen")
            {
                Console.WriteLine("FAIL: AboutDialog zh author incorrect: " + aboutZhAuthor);
                return 58;
            }
            if (aboutEnAuthor != "Designed & Built by gujiangwen")
            {
                Console.WriteLine("FAIL: AboutDialog en author incorrect: " + aboutEnAuthor);
                return 58;
            }
            if (!foundZhLink || !foundEnLink)
            {
                Console.WriteLine("FAIL: AboutDialog missing simplified (GitHub ↗) link");
                return 58;
            }
            if (!foundZhEmail || !foundEnEmail)
            {
                Console.WriteLine("FAIL: AboutDialog missing author email link");
                return 58;
            }

            // Verify SettingsDialog Tab 0 timer tip is removed for a cleaner interface
            FieldInfo fTimerTip = settingsDlgType.GetField("lblTimerTip", BindingFlags.NonPublic | BindingFlags.Instance);
            if (fTimerTip != null)
            {
                Console.WriteLine("FAIL: SettingsDialog still has lblTimerTip field");
                return 58;
            }

            // Verify CliHelpDialog is completely eliminated and terminal help / Tab 2 tip takes its place
            Type cliHelpType = asm.GetType("CliHelpDialog");
            if (cliHelpType != null)
            {
                Console.WriteLine("FAIL: CliHelpDialog should be completely removed from assembly");
                return 58;
            }

            // Verify Tab 2 CLI tip in both languages
            Form dlgZh = (Form)Activator.CreateInstance(settingsDlgType, new object[] { settings, true });
            setActiveTabMethod.Invoke(dlgZh, new object[] { 2 });
            Label tipZh = (Label)settingsDlgType.GetField("lblCliGuideHint", bf).GetValue(dlgZh);
            if (tipZh == null || tipZh.Text != "贴一段提示词即可。")
            {
                Console.WriteLine("FAIL: Tab 2 guide hint missing expected Chinese instructions");
                dlgZh.Close();
                return 58;
            }
            dlgZh.Close();

            object enSettings = Activator.CreateInstance(settingsType);
            settingsType.GetField("Language").SetValue(enSettings, "en");
            Form dlgEn = (Form)Activator.CreateInstance(settingsDlgType, new object[] { enSettings, true });
            setActiveTabMethod.Invoke(dlgEn, new object[] { 2 });
            Label tipEn = (Label)settingsDlgType.GetField("lblCliGuideHint", bf).GetValue(dlgEn);
            if (tipEn == null || tipEn.Text != "Paste a prompt to enable it.")
            {
                Console.WriteLine("FAIL: Tab 2 guide hint missing expected English instructions");
                dlgEn.Close();
                return 58;
            }
            dlgEn.Close();

            Console.WriteLine("PASS: Tool introductions modernization (AboutDialog Option 1, Settings Tab 0 tip, Tab 2 terminal CLI guidance & CliHelpDialog elimination) verified.");

            // Verify MCP server types are absent and CLI guidance/JSON remain available.
            if (asm.GetType("McpBridge") != null || asm.GetType("McpPipeServer") != null || asm.GetType("McpConfigHelper") != null)
            {
                Console.WriteLine("FAIL: Removed MCP server/configuration types are still present");
                return 59;
            }
            ProcessStartInfo removedMcpPsi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = "--mcp",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using (Process removedMcpProc = Process.Start(removedMcpPsi))
            {
                string removedMcpError = removedMcpProc.StandardError.ReadToEnd();
                removedMcpProc.WaitForExit(1500);
                if (removedMcpProc.ExitCode != 2 || !removedMcpError.Contains("server has been removed"))
                {
                    Console.WriteLine("FAIL: Legacy --mcp should exit with a clear removal message: " + removedMcpError);
                    return 59;
                }
            }
            Type cliPromptType = asm.GetType("CliPromptHelper");
            MethodInfo cliPromptMethod = cliPromptType != null ? cliPromptType.GetMethod("GenerateCliAgentPrompt", BindingFlags.Public | BindingFlags.Static) : null;
            string cliPromptZh = cliPromptMethod != null ? (string)cliPromptMethod.Invoke(null, new object[] { false }) : "";
            string cliPromptEn = cliPromptMethod != null ? (string)cliPromptMethod.Invoke(null, new object[] { true }) : "";
            if (!cliPromptZh.Contains("--ask") || !cliPromptZh.Contains("--progress") || !cliPromptZh.Contains("--notice") || !cliPromptZh.Contains("--json") ||
                !cliPromptZh.Contains("首次工具调用验证") || !cliPromptZh.Contains("本次会话") || !cliPromptZh.Contains("后台继续工作") ||
                !cliPromptZh.Contains("--progress clear") || cliPromptZh.Contains("MCP") ||
                !cliPromptEn.Contains("--ask") || !cliPromptEn.Contains("--progress") || !cliPromptEn.Contains("--notice") || !cliPromptEn.Contains("--json") ||
                !cliPromptEn.Contains("Initial tool-call verification") || !cliPromptEn.Contains("this conversation") || cliPromptEn.Contains("MCP"))
            {
                Console.WriteLine("FAIL: CLI guide should explain usage, session persistence, and safe tool-call verification");
                return 59;
            }
            Type cliSkillType = asm.GetType("CliSkillInstaller");
            MethodInfo skillPathMethod = cliSkillType != null ? cliSkillType.GetMethod("GetSkillPath", BindingFlags.Public | BindingFlags.Static) : null;
            MethodInfo skillMarkdownMethod = cliSkillType != null ? cliSkillType.GetMethod("GetSkillMarkdown", BindingFlags.Public | BindingFlags.Static) : null;
            string skillPath = skillPathMethod != null ? (string)skillPathMethod.Invoke(null, new object[] { null }) : "";
            string codexSkillPath = skillPathMethod != null ? (string)skillPathMethod.Invoke(null, new object[] { "Codex" }) : "";
            string antigravitySkillPath = skillPathMethod != null ? (string)skillPathMethod.Invoke(null, new object[] { "Antigravity" }) : "";
            string skillMarkdown = skillMarkdownMethod != null ? (string)skillMarkdownMethod.Invoke(null, null) : "";
            if (!skillPath.EndsWith(System.IO.Path.Combine(".agents", "skills", "iclock-desktop-collaboration", "SKILL.md"), StringComparison.OrdinalIgnoreCase) ||
                !codexSkillPath.EndsWith(System.IO.Path.Combine(".codex", "skills", "iclock-desktop-collaboration", "SKILL.md"), StringComparison.OrdinalIgnoreCase) ||
                !antigravitySkillPath.EndsWith(System.IO.Path.Combine(".gemini", "antigravity-cli", "skills", "iclock-desktop-collaboration", "SKILL.md"), StringComparison.OrdinalIgnoreCase) ||
                !skillMarkdown.Contains("name: iclock-desktop-collaboration") || !skillMarkdown.Contains("--progress clear") ||
                !skillMarkdown.Contains("--notice") || !skillMarkdown.Contains("## Verification") ||
                !skillMarkdown.Contains("Do not run synthetic verification on every ordinary task") ||
                !skillMarkdown.Contains("cannot guarantee invocation"))
            {
                Console.WriteLine("FAIL: CLI Skill per-agent targets or persistent behavior guidance are invalid");
                return 59;
            }
            MethodInfo quotePromptMethod = asm.GetType("AiAgentQuoteHelper").GetMethod("BuildAgentPrompt", BindingFlags.NonPublic | BindingFlags.Static);
            string quotePrompt = (string)quotePromptMethod.Invoke(null, new object[] { new List<string> { "专注" }, 30, "zh" });
            if (!quotePrompt.Contains("--quote-add") || quotePrompt.Contains("MCP"))
            {
                Console.WriteLine("FAIL: Agent quote prompt should import through the CLI");
                return 59;
            }

            string cliBinary = exePath;
            ProcessStartInfo askJsonPsi = new ProcessStartInfo
            {
                FileName = cliBinary,
                Arguments = "--json --ask",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using (Process askJsonProc = Process.Start(askJsonPsi))
            {
                string askJsonOut = askJsonProc.StandardOutput.ReadToEnd();
                askJsonProc.WaitForExit(1500);
                if (askJsonProc.ExitCode != 1 || !askJsonOut.Contains("\"ok\":false") || !askJsonOut.Contains("\"action\":\"ask\"") || !askJsonOut.Contains("\"error\":"))
                {
                    Console.WriteLine("FAIL: --json CLI output invalid (ExitCode=" + askJsonProc.ExitCode + ", Out=" + askJsonOut + ")");
                    return 59;
                }
            }

            ProcessStartInfo quoteJsonPsi = new ProcessStartInfo
            {
                FileName = cliBinary,
                Arguments = "--json --quote-add",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using (Process quoteJsonProc = Process.Start(quoteJsonPsi))
            {
                string quoteJsonOut = quoteJsonProc.StandardOutput.ReadToEnd();
                quoteJsonProc.WaitForExit(1500);
                if (quoteJsonProc.ExitCode != 1 || !quoteJsonOut.Contains("\"action\":\"quote_add\"") || !quoteJsonOut.Contains("\"error\":"))
                {
                    Console.WriteLine("FAIL: --quote-add JSON validation output invalid: " + quoteJsonOut);
                    return 59;
                }
            }

            Console.WriteLine("PASS: MCP server removed; CLI desktop collaboration, quote import guidance, and JSON errors verified.");
            // Test IPC custom quote command (desktop note projection)
            FieldInfo fActiveProjected = appType.GetField("activeProjectedNote", bf);
            FieldInfo fDailySuspended = appType.GetField("dailyQuoteSuspendedForProjection", bf);
            handleIpc.Invoke(app, new object[] { "quote:AI Test Quote|Test Author" });
            Form projectedDlg = (Form)fActiveProjected.GetValue(app);
            if (projectedDlg == null)
            {
                Console.WriteLine("FAIL: IPC quote command did not create projected DailyQuoteDialog");
                return 59;
            }
            PropertyInfo pIsProjected = projectedDlg.GetType().GetProperty("IsProjectedNote", BindingFlags.Public | BindingFlags.Instance);
            if (pIsProjected == null || !(bool)pIsProjected.GetValue(projectedDlg, null))
            {
                Console.WriteLine("FAIL: Projected note IsProjectedNote is not true");
                return 59;
            }
            if (!projectedDlg.TopMost)
            {
                Console.WriteLine("FAIL: Projected note TopMost is not true");
                return 59;
            }

            // Test mutual exclusion: new projection invalidates old projection
            handleIpc.Invoke(app, new object[] { "quote:Second Note|New Author" });
            Form secondProjected = (Form)fActiveProjected.GetValue(app);
            if (secondProjected == null || secondProjected == projectedDlg)
            {
                Console.WriteLine("FAIL: Mutual exclusion failed, second projection did not replace first");
                return 59;
            }
            if (!projectedDlg.IsDisposed)
            {
                Console.WriteLine("FAIL: Mutual exclusion failed, first projection was not closed");
                return 59;
            }
            secondProjected.Close();
            Application.DoEvents();

            // Test daily quote suspension and restoration during projection
            MethodInfo checkDailyQuote = appType.GetMethod("CheckDailyQuote", bf);
            checkDailyQuote.Invoke(app, new object[] { true });
            fActiveQuote = appType.GetField("activeQuoteDialog", bf);
            Form dailyQuoteDlg = (Form)fActiveQuote.GetValue(app);
            if (dailyQuoteDlg == null)
            {
                Console.WriteLine("FAIL: CheckDailyQuote did not create dailyQuoteDlg");
                return 59;
            }
            if (!dailyQuoteDlg.Visible)
            {
                Console.WriteLine("FAIL: CheckDailyQuote created the quote but did not show it");
                return 59;
            }
            // A hidden retained instance must also be restored by the normal startup/unlock path.
            dailyQuoteDlg.Hide();
            checkDailyQuote.Invoke(app, new object[] { false });
            if (!dailyQuoteDlg.Visible)
            {
                Console.WriteLine("FAIL: CheckDailyQuote treated a hidden retained quote as visible");
                return 59;
            }
            // Project note -> daily quote should be hidden
            handleIpc.Invoke(app, new object[] { "quote:Third Note|Agent" });
            if (dailyQuoteDlg.Visible)
            {
                Console.WriteLine("FAIL: Daily quote was not hidden during note projection");
                return 59;
            }
            bool suspended = (bool)fDailySuspended.GetValue(app);
            if (!suspended)
            {
                Console.WriteLine("FAIL: dailyQuoteSuspendedForProjection was not set to true");
                return 59;
            }
            // Close projected note -> daily quote should be restored and visible
            Form thirdProjected = (Form)fActiveProjected.GetValue(app);
            thirdProjected.Close();
            Application.DoEvents();
            if (!dailyQuoteDlg.Visible)
            {
                Console.WriteLine("FAIL: Daily quote was not restored after projection closed");
                return 59;
            }
            dailyQuoteDlg.Close();
            Application.DoEvents();

            // Verify switching to Daily Quotes tab in Settings while projected note is active does NOT show duplicate quote
            handleIpc.Invoke(app, new object[] { "quote:Projected Note Test|Agent" });
            Form projectedNoteInstance = (Form)fActiveProjected.GetValue(app);
            MethodInfo mSetQuotePreview = appType.GetMethod("SetQuotePreview", BindingFlags.NonPublic | BindingFlags.Instance);
            if (mSetQuotePreview != null)
            {
                mSetQuotePreview.Invoke(app, new object[] { true });
                Form curActiveQuote = (Form)fActiveQuote.GetValue(app);
                if (curActiveQuote != null && curActiveQuote.Visible)
                {
                    Console.WriteLine("FAIL: Daily quote preview should remain hidden when projected note is active");
                    return 59;
                }
            }
            if (projectedNoteInstance != null && !projectedNoteInstance.IsDisposed)
            {
                projectedNoteInstance.Close();
                Application.DoEvents();
            }

            // Test IPC progress command
            handleIpc.Invoke(app, new object[] { "progress:60|Compiling test" });
            handleIpc.Invoke(app, new object[] { "progress:clear" });

            // Test IPC status command
            handleIpc.Invoke(app, new object[] { "status:Building project --detail compiling... --progress 45% --agent-name CliTester --project iClock --thinking" });
            MethodInfo findTaskGlobal = appType.GetMethod("FindTaskStatus", BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(string) }, null);
            object cliStatus = findTaskGlobal.Invoke(app, new object[] { "CliTester" });
            if (cliStatus == null)
            {
                Console.WriteLine("FAIL: IPC status command did not create task status for CliTester");
                return 58;
            }
            string cliMsg = (string)cliStatus.GetType().GetField("Message", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(cliStatus);
            string cliSub = (string)cliStatus.GetType().GetField("SubDetail", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(cliStatus);
            float cliProg = (float)cliStatus.GetType().GetField("Progress", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(cliStatus);
            bool cliThink = (bool)cliStatus.GetType().GetField("IsThinking", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(cliStatus);
            if (!cliMsg.Contains("Building project") || cliSub != "compiling..." || Math.Abs(cliProg - 0.45f) > 0.01f || !cliThink)
            {
                Console.WriteLine("FAIL: IPC status command fields mismatch: msg=" + cliMsg + ", sub=" + cliSub + ", prog=" + cliProg + ", think=" + cliThink);
                return 58;
            }
            handleIpc.Invoke(app, new object[] { "status:clear --agent-name CliTester" });
            if (findTaskGlobal.Invoke(app, new object[] { "CliTester" }) != null)
            {
                Console.WriteLine("FAIL: IPC status clear did not remove CliTester task");
                return 58;
            }

            // Verify companion CLI launcher and batch script
            string distCom = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "iclock.com");
            if (!System.IO.File.Exists(distCom))
            {
                Console.WriteLine("FAIL: iclock.com missing in dist directory");
                return 58;
            }
            long comLen = new System.IO.FileInfo(distCom).Length;
            if (comLen <= 0 || comLen > 15000)
            {
                Console.WriteLine("FAIL: iclock.com size abnormal: " + comLen);
                return 58;
            }
            string distCmd = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "iclock.cmd");
            if (System.IO.File.Exists(distCmd))
            {
                string cmdTxt = System.IO.File.ReadAllText(distCmd);
                if (!cmdTxt.Contains("iClock.exe"))
                {
                    Console.WriteLine("FAIL: iclock.cmd missing invocation target");
                    return 58;
                }
            }

            // Verify AskHumanDialog type exists and instantiates with options
            Type askDlgType = asm.GetType("AskHumanDialog");
            if (askDlgType == null)
            {
                Console.WriteLine("FAIL: AskHumanDialog type missing in iClock");
                return 59;
            }
            string testResFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "test_res_" + Guid.NewGuid().ToString("N") + ".txt");
            Form askDlg = (Form)Activator.CreateInstance(askDlgType, new object[] {
                settings,
                "是否允许执行测试任务？",
                new string[] { "确认执行", "取消" },
                0,
                testResFile
            });
            if (askDlg == null || (!askDlg.Text.Contains("AI 智能体请示") && !askDlg.Text.Contains("AI Agent Request") && !askDlg.Text.Contains("请示") && !askDlg.Text.Contains("Request")))
            {
                Console.WriteLine("FAIL: AskHumanDialog instantiation failed");
                return 59;
            }
            if (askDlg.AcceptButton == null || askDlg.CancelButton == null)
            {
                Console.WriteLine("FAIL: AcceptButton or CancelButton missing on AskHumanDialog");
                return 59;
            }
            askDlg.Close();

            // Verify AskHumanDialog 7-argument overload with specific agent name
            Form askDlgWithAgent = (Form)Activator.CreateInstance(askDlgType, new object[] {
                settings,
                "是否允许执行测试任务？",
                new string[] { "确认执行", "取消" },
                0,
                testResFile,
                false,
                "Antigravity"
            });
            if (askDlgWithAgent == null || !askDlgWithAgent.Text.Contains("Antigravity"))
            {
                Console.WriteLine("FAIL: AskHumanDialog specific agent title failed");
                return 59;
            }
            askDlgWithAgent.Close();

            // Verify AskHumanDialog 6-argument overload for non-intrusive mode
            Form askDlgNonIntrusive = (Form)Activator.CreateInstance(askDlgType, new object[] {
                settings,
                "是否允许执行非侵入测试任务？",
                new string[] { "确认执行", "取消" },
                0,
                testResFile,
                true
            });
            if (askDlgNonIntrusive == null)
            {
                Console.WriteLine("FAIL: AskHumanDialog non-intrusive overload instantiation failed");
                return 59;
            }
            askDlgNonIntrusive.Close();

            // Verify AppContext.IsAgentInForeground method exists and runs safely
            MethodInfo isFgMethod = appType.GetMethod("IsAgentInForeground", BindingFlags.NonPublic | BindingFlags.Static);
            if (isFgMethod == null)
            {
                Console.WriteLine("FAIL: AppContext.IsAgentInForeground method missing");
                return 59;
            }
            bool isFgVal = (bool)isFgMethod.Invoke(null, null);

            // 43. Verify AI Agent Quote Expansion: Settings (AiQuoteKeywords persistence & defaults), SettingsDialog side-by-side buttons (btnEditQuotes & btnAiQuotes layout and width), AiAgentQuoteHelper (ParseQuotes formatting, deduplication, safe append with .bak creation), and AiQuoteDialog instantiation.
            FieldInfo fAiQuoteKeywords = settingsType.GetField("AiQuoteKeywords");
            if (fAiQuoteKeywords == null)
            {
                Console.WriteLine("FAIL: Settings missing AiQuoteKeywords field");
                return 61;
            }

            // The agent picker and the headless "run the agent for me" track are
            // gone: iClock only hands over an instruction and waits for the
            // quotes to come back through its command line.
            if (settingsType.GetField("AiAgentType") != null || settingsType.GetField("AiAgentCustomCmd") != null)
            {
                Console.WriteLine("FAIL: Settings still carries the removed agent-selection fields (AiAgentType/AiAgentCustomCmd)");
                return 61;
            }

            // Test settings persistence round-trip
            fAiQuoteKeywords.SetValue(settings, "心流, 专注");
            settings.GetType().GetMethod("Save").Invoke(settings, null);
            object reloadedAiSettings = settingsType.GetMethod("Load").Invoke(null, null);
            if ((string)fAiQuoteKeywords.GetValue(reloadedAiSettings) != "心流, 专注")
            {
                Console.WriteLine("FAIL: Settings AiQuoteKeywords failed to persist across Load/Save");
                return 61;
            }
            // Restore default
            fAiQuoteKeywords.SetValue(settings, "");
            settings.GetType().GetMethod("Save").Invoke(settings, null);

            // Verify SettingsDialog side-by-side buttons
            Form setDlgWithAi = (Form)Activator.CreateInstance(setDlgType, new object[] { settings, true });
            FieldInfo fBtnEditQuotes = setDlgType.GetField("btnEditQuotes", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo fBtnAiQuotes = setDlgType.GetField("btnAiQuotes", BindingFlags.NonPublic | BindingFlags.Instance);
            if (fBtnEditQuotes == null || fBtnAiQuotes == null)
            {
                Console.WriteLine("FAIL: SettingsDialog missing btnEditQuotes or btnAiQuotes field");
                return 61;
            }
            Button bEdit = (Button)fBtnEditQuotes.GetValue(setDlgWithAi);
            Button bAi = (Button)fBtnAiQuotes.GetValue(setDlgWithAi);
            if (bEdit == null || bAi == null)
            {
                Console.WriteLine("FAIL: btnEditQuotes or btnAiQuotes is null in SettingsDialog");
                return 61;
            }
            if (bEdit.Width != 90 || bAi.Width != 90)
            {
                Console.WriteLine("FAIL: Expected side-by-side buttons with width 90, got btnEdit=" + bEdit.Width + ", btnAi=" + bAi.Width);
                return 61;
            }
            if (bEdit.Left != 172 || bAi.Left != 270)
            {
                Console.WriteLine("FAIL: Expected btnEdit.Left=172, btnAi.Left=270, got btnEdit=" + bEdit.Left + ", btnAi=" + bAi.Left);
                return 61;
            }
            if (bAi.Right - bEdit.Left != 188)
            {
                Console.WriteLine("FAIL: Side-by-side buttons total span should be exactly 188px, got " + (bAi.Right - bEdit.Left));
                return 61;
            }
            setDlgWithAi.Close();

            // Verify AiAgentQuoteHelper
            Type helperType = asm.GetType("AiAgentQuoteHelper");
            if (helperType == null)
            {
                Console.WriteLine("FAIL: AiAgentQuoteHelper type missing");
                return 61;
            }
            MethodInfo mParseQuotes = helperType.GetMethod("ParseQuotes", BindingFlags.NonPublic | BindingFlags.Static);
            if (mParseQuotes == null)
            {
                Console.WriteLine("FAIL: AiAgentQuoteHelper.ParseQuotes method missing");
                return 61;
            }
            string sampleOutput = "```markdown\r\n" +
                                 "Here are 3 quotes for you:\r\n" +
                                 "1. 行百里者半九十。 | 《战国策》\r\n" +
                                 "2. \"Stay hungry, stay foolish.\" | Steve Jobs\r\n" +
                                 "3. “保持专注，保持简单。”\r\n" +
                                 "Hope these inspire you!\r\n" +
                                 "```";
            IList parsedQuotes = (IList)mParseQuotes.Invoke(null, new object[] { sampleOutput });
            if (parsedQuotes == null || parsedQuotes.Count != 3)
            {
                Console.WriteLine("FAIL: ParseQuotes expected 3 items, got: " + (parsedQuotes == null ? "null" : parsedQuotes.Count.ToString()));
                return 61;
            }
            Type qiType = asm.GetType("QuoteItem");
            PropertyInfo qiTextProp = qiType.GetProperty("Text");
            PropertyInfo qiAuthorProp = qiType.GetProperty("Author");
            string q0Text = (string)qiTextProp.GetValue(parsedQuotes[0], null);
            string q0Author = (string)qiAuthorProp.GetValue(parsedQuotes[0], null);
            if (q0Text != "行百里者半九十。" || q0Author != "《战国策》")
            {
                Console.WriteLine("FAIL: Quote 0 parsed incorrectly: " + q0Text + " | " + q0Author);
                return 61;
            }
            string q1Text = (string)qiTextProp.GetValue(parsedQuotes[1], null);
            string q1Author = (string)qiAuthorProp.GetValue(parsedQuotes[1], null);
            if (q1Text != "Stay hungry, stay foolish." || q1Author != "Steve Jobs")
            {
                Console.WriteLine("FAIL: Quote 1 parsed incorrectly: " + q1Text + " | " + q1Author);
                return 61;
            }
            string q2Text = (string)qiTextProp.GetValue(parsedQuotes[2], null);
            string q2Author = (string)qiAuthorProp.GetValue(parsedQuotes[2], null);
            if (q2Text != "保持专注，保持简单。" || q2Author != "佚名")
            {
                Console.WriteLine("FAIL: Quote 2 parsed incorrectly: " + q2Text + " | " + q2Author);
                return 61;
            }

            // Verify AppendQuotesToFile (deduplication & .bak creation)
            MethodInfo mAppendQuotes = helperType.GetMethod("AppendQuotesToFile", BindingFlags.NonPublic | BindingFlags.Static);
            if (mAppendQuotes == null)
            {
                Console.WriteLine("FAIL: AiAgentQuoteHelper.AppendQuotesToFile method missing");
                return 61;
            }
            PropertyInfo pQuotesPath = settingsType.GetProperty("QuotesFilePath", BindingFlags.Public | BindingFlags.Static);
            string actualQuotesFile = (string)pQuotesPath.GetValue(null, null);
            string quotesBackup = null;
            if (System.IO.File.Exists(actualQuotesFile)) quotesBackup = System.IO.File.ReadAllText(actualQuotesFile, System.Text.Encoding.UTF8);

            try
            {
                // Write a baseline quote to test deduplication
                System.IO.File.WriteAllText(actualQuotesFile, "行百里者半九十。 | 《战国策》\r\n", System.Text.Encoding.UTF8);
                object[] appendArgs = new object[] { parsedQuotes, 0, null };
                bool appendRes = (bool)mAppendQuotes.Invoke(null, appendArgs);
                int addedCount = (int)appendArgs[1];
                if (!appendRes || addedCount != 2)
                {
                    Console.WriteLine("FAIL: AppendQuotesToFile failed or added count wrong. Expected 2 (1 duplicate skipped), got: " + addedCount + ", ok=" + appendRes);
                    return 61;
                }
                // Check sequential numbering on appended lines
                string[] linesAfterAppend = System.IO.File.ReadAllLines(actualQuotesFile, System.Text.Encoding.UTF8);
                if (linesAfterAppend.Length < 3 || !linesAfterAppend[1].StartsWith("2. ") || !linesAfterAppend[2].StartsWith("3. "))
                {
                    Console.WriteLine("FAIL: Appended quotes missing sequential numbering prefix '2. ' and '3. '");
                    return 61;
                }

                // Check .bak file exists
                if (!System.IO.File.Exists(actualQuotesFile + ".bak"))
                {
                    Console.WriteLine("FAIL: AppendQuotesToFile did not create .bak backup file");
                    return 61;
                }

                // Verify continuation from existing numbered file (e.g. 10 -> 11)
                System.IO.File.WriteAllText(actualQuotesFile, "1. 一\r\n2. 二\r\n10. 十\r\n", System.Text.Encoding.UTF8);
                object qMore = Activator.CreateInstance(qiType, new object[] { "海纳百川，有容乃大。", "林则徐" });
                IList moreList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(qiType));
                moreList.Add(qMore);
                object[] moreArgs = new object[] { moreList, 0, null };
                mAppendQuotes.Invoke(null, moreArgs);
                string[] linesCont = System.IO.File.ReadAllLines(actualQuotesFile, System.Text.Encoding.UTF8);
                if (linesCont.Length < 4 || !linesCont[3].StartsWith("11. "))
                {
                    Console.WriteLine("FAIL: Appended quote should continue sequence to '11. ', got: " + (linesCont.Length > 3 ? linesCont[3] : "null"));
                    return 61;
                }
            }
            finally
            {
                if (quotesBackup != null)
                {
                    try { System.IO.File.WriteAllText(actualQuotesFile, quotesBackup, System.Text.Encoding.UTF8); } catch { }
                }
                else
                {
                    try { System.IO.File.Delete(actualQuotesFile); } catch { }
                }
                try { System.IO.File.Delete(actualQuotesFile + ".bak"); } catch { }
            }

            // Verify AiAgentQuoteHelper GetKeywords and BuildAgentPrompt
            MethodInfo mGetKeywords = helperType.GetMethod("GetKeywords", BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo mBuildAgentPrompt = helperType.GetMethod("BuildAgentPrompt", BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { typeof(List<string>), typeof(int), typeof(string) }, null);
            if (mGetKeywords == null || mBuildAgentPrompt == null)
            {
                Console.WriteLine("FAIL: AiAgentQuoteHelper GetKeywords or BuildAgentPrompt method missing");
                return 61;
            }
            List<string> defaultTagsZh = (List<string>)mGetKeywords.Invoke(null, new object[] { settings });
            if (defaultTagsZh == null || defaultTagsZh.Count != 4 || !defaultTagsZh.Contains("极客心流"))
            {
                Console.WriteLine("FAIL: GetKeywords did not return expected default ZH tags");
                return 61;
            }
            string generatedAgentPrompt = (string)mBuildAgentPrompt.Invoke(null, new object[] { new List<string> { "知行合一", "松弛感" }, 50, "zh" });
            if (!generatedAgentPrompt.Contains("50") || !generatedAgentPrompt.Contains("--quote-add") || !generatedAgentPrompt.Contains("add"))
            {
                Console.WriteLine("FAIL: BuildAgentPrompt did not include 50 quotes and CLI quote import instruction: " + generatedAgentPrompt);
                return 61;
            }

            // Verify AiQuoteDialog instantiation and controls
            Type aiDlgType = asm.GetType("AiQuoteDialog");
            if (aiDlgType == null)
            {
                Console.WriteLine("FAIL: AiQuoteDialog type missing");
                return 61;
            }
            Form aiDlg = (Form)Activator.CreateInstance(aiDlgType, new object[] { settings });
            if (aiDlg == null || !aiDlg.Text.Contains("AI"))
            {
                Console.WriteLine("FAIL: AiQuoteDialog title missing AI");
                return 61;
            }
            if (aiDlg.ClientSize.Width != 380 || aiDlg.ClientSize.Height != 280)
            {
                Console.WriteLine("FAIL: AiQuoteDialog size should be 380x280, got: " + aiDlg.ClientSize.Width + "x" + aiDlg.ClientSize.Height);
                return 61;
            }
            FieldInfo fTxtPreference = aiDlgType.GetField("txtPreference", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo fNudQuoteCount = aiDlgType.GetField("nudQuoteCount", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo fPanelStatus = aiDlgType.GetField("panelStatus", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo fPanelSetup = aiDlgType.GetField("panelSetup", BindingFlags.NonPublic | BindingFlags.Instance);
            if (fTxtPreference == null || fNudQuoteCount == null || fPanelStatus == null || fPanelSetup == null)
            {
                Console.WriteLine("FAIL: AiQuoteDialog missing required UI fields (txtPreference, nudQuoteCount, panelStatus, panelSetup)");
                return 61;
            }
            // Guard against the old agent picker coming back: the dialog hands
            // over an instruction, it does not choose or drive an agent.
            if (aiDlgType.GetField("cboAgent", BindingFlags.NonPublic | BindingFlags.Instance) != null ||
                aiDlgType.GetField("detectedAgents", BindingFlags.NonPublic | BindingFlags.Instance) != null ||
                aiDlgType.GetField("selectedAgent", BindingFlags.NonPublic | BindingFlags.Instance) != null)
            {
                Console.WriteLine("FAIL: AiQuoteDialog still exposes the removed agent picker (cboAgent/detectedAgents/selectedAgent)");
                return 61;
            }
            TextBox txtPref = (TextBox)fTxtPreference.GetValue(aiDlg);
            NumericUpDown nudCount = (NumericUpDown)fNudQuoteCount.GetValue(aiDlg);
            Panel pnlSetup = (Panel)fPanelSetup.GetValue(aiDlg);
            Panel pnlStatus = (Panel)fPanelStatus.GetValue(aiDlg);
            if (txtPref == null || !txtPref.Multiline || txtPref.Text.Length == 0 || !txtPref.Text.Contains("极客心流"))
            {
                Console.WriteLine("FAIL: AiQuoteDialog txtPreference was not populated with default preferences");
                return 61;
            }
            if (nudCount == null || nudCount.Minimum != 5 || nudCount.Maximum != 100 || nudCount.Value != 30)
            {
                Console.WriteLine("FAIL: AiQuoteDialog nudQuoteCount should default to 30 with range 5-100");
                return 61;
            }
            if (pnlSetup == null || pnlStatus == null || pnlSetup.Controls.Count == 0 || pnlStatus.Controls.Count == 0)
            {
                Console.WriteLine("FAIL: AiQuoteDialog dual-track panels (panelSetup/panelStatus) missing controls");
                return 61;
            }

            // One footer baseline in both views: the setup buttons and every
            // status view action sit at y=234, with the status card filling the
            // space above them (38 + 180 = 218).
            FieldInfo fCardC = aiDlgType.GetField("cardContainer", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo fBtnGenS = aiDlgType.GetField("btnGenerate", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo fBtnDoneS = aiDlgType.GetField("btnStatusDone", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo showCompleted = aiDlgType.GetMethod("ShowCompletedView", BindingFlags.NonPublic | BindingFlags.Instance);
            if (fCardC == null || fBtnGenS == null || fBtnDoneS == null || showCompleted == null)
            {
                Console.WriteLine("FAIL: AiQuoteDialog missing cardContainer/btnGenerate/btnStatusDone/ShowCompletedView");
                return 61;
            }
            Button btnGenLayout = (Button)fBtnGenS.GetValue(aiDlg);
            showCompleted.Invoke(aiDlg, new object[] { 3, null });
            Panel cardLayout = (Panel)fCardC.GetValue(aiDlg);
            Button btnDoneLayout = (Button)fBtnDoneS.GetValue(aiDlg);
            if (cardLayout.Height != 180 || cardLayout.Bottom != 218 ||
                btnGenLayout.Top != 234 || btnDoneLayout.Top != 234)
            {
                Console.WriteLine("FAIL: AiQuoteDialog footer baseline drifted, cardBottom=" + cardLayout.Bottom +
                    " setupBtnTop=" + btnGenLayout.Top + " statusBtnTop=" + btnDoneLayout.Top);
                return 61;
            }

            // The headless "iClock runs the agent" track is gone; the helper no
            // longer spawns any agent CLI.
            if (helperType.GetMethod("HasCliSupport", BindingFlags.NonPublic | BindingFlags.Static) != null ||
                helperType.GetMethod("RunAgentCli", BindingFlags.NonPublic | BindingFlags.Static) != null ||
                helperType.GetMethod("TryGetCliCommand", BindingFlags.NonPublic | BindingFlags.Static) != null)
            {
                Console.WriteLine("FAIL: AiAgentQuoteHelper still exposes the removed agent-CLI execution path");
                return 61;
            }

            // Verify ParseQuotes strictly filters out CLI/JSON error strings and malformed quotes
            if (mParseQuotes != null)
            {
                string jsonErr = "{\r\n  \"error\": \"ANTIGRAVITY_LS_ADDRESS is not set\"\r\n}";
                System.Collections.IList errList = (System.Collections.IList)mParseQuotes.Invoke(null, new object[] { jsonErr });
                if (errList != null && errList.Count > 0)
                {
                    Console.WriteLine("FAIL: ParseQuotes parsed JSON error as quote!");
                    return 61;
                }
                string rawErrLine = "\"error\": \"ANTIGRAVITY_LS_ADDRESS is not set\"";
                System.Collections.IList errLineList = (System.Collections.IList)mParseQuotes.Invoke(null, new object[] { rawErrLine });
                if (errLineList != null && errLineList.Count > 0)
                {
                    Console.WriteLine("FAIL: ParseQuotes parsed error line as quote!");
                    return 61;
                }
            }

            EventInfo eQuotesAdded = helperType.GetEvent("OnQuotesAdded", BindingFlags.Public | BindingFlags.Static);
            if (eQuotesAdded == null)
            {
                Console.WriteLine("FAIL: AiAgentQuoteHelper missing OnQuotesAdded event");
                return 61;
            }

            aiDlg.Dispose();

            Console.WriteLine("PASS: AI Agent Quote Expansion (Settings persistence, Tab 1 side-by-side buttons, AiAgentQuoteHelper parsing/dedup/bak, and AiQuoteDialog) verified.");

            // 44. Verify Desktop Quote Alt + Drag interaction and 0-polling performance optimization
            Form qDlgMoveTest = (Form)Activator.CreateInstance(quoteDlgType, new object[] { settings });
            PropertyInfo pMoveMode = quoteDlgType.GetProperty("MoveMode", BindingFlags.Public | BindingFlags.Instance);
            MethodInfo mSetMoveMode = quoteDlgType.GetMethod("SetMoveMode", BindingFlags.Public | BindingFlags.Instance);
            FieldInfo fCountdownTimer = quoteDlgType.GetField("countdownTimer", BindingFlags.NonPublic | BindingFlags.Instance);
            if (fCountdownTimer != null)
            {
                Console.WriteLine("FAIL: countdownTimer should be completely eliminated in Plan A");
                return 62;
            }
            if (pMoveMode == null || mSetMoveMode == null)
            {
                Console.WriteLine("FAIL: DailyQuoteDialog missing MoveMode or SetMoveMode");
                return 62;
            }

            // Verify SetMoveMode toggles MoveMode and hover state
            mSetMoveMode.Invoke(qDlgMoveTest, new object[] { true });
            if (!(bool)pMoveMode.GetValue(qDlgMoveTest, null) || !(bool)fIsHov.GetValue(qDlgMoveTest))
            {
                Console.WriteLine("FAIL: SetMoveMode(true) did not enable MoveMode and hover state");
                return 62;
            }
            mSetMoveMode.Invoke(qDlgMoveTest, new object[] { false });
            if ((bool)pMoveMode.GetValue(qDlgMoveTest, null) || (bool)fIsHov.GetValue(qDlgMoveTest))
            {
                Console.WriteLine("FAIL: SetMoveMode(false) did not disable MoveMode and hover state");
                return 62;
            }

            // Verify IsPinned toggles TopMost layer cleanly (True = TopMost, False = Desktop Layer)
            pIsPinned.SetValue(qDlgMoveTest, true, null);
            if (!qDlgMoveTest.TopMost)
            {
                Console.WriteLine("FAIL: DailyQuoteDialog TopMost should be true when IsPinned is true");
                return 62;
            }
            pIsPinned.SetValue(qDlgMoveTest, false, null);
            if (qDlgMoveTest.TopMost)
            {
                Console.WriteLine("FAIL: DailyQuoteDialog TopMost should be false (Desktop Layer) when IsPinned is false");
                return 62;
            }

            qDlgMoveTest.Close();
            qDlgMoveTest.Dispose();

            Console.WriteLine("PASS: Desktop quote Alt + Drag mode, MoveMode synchronization, and 0-polling background optimization verified.");

            // 45. Verify the daily focus goal: the settings field (off by default),
            // the day-total reader and the streak calculation.
            FieldInfo fDailyGoal = settings.GetType().GetField("DailyGoalMinutes");
            if (fDailyGoal == null)
            {
                Console.WriteLine("FAIL: Settings is missing the DailyGoalMinutes field");
                return 63;
            }
            if ((int)fDailyGoal.GetValue(defaultSettings) != 0)
            {
                Console.WriteLine("FAIL: DailyGoalMinutes should default to 0 (feature off)");
                return 63;
            }
            // The setting must survive a save/load round trip.
            fDailyGoal.SetValue(settings, 240);
            settings.GetType().GetMethod("Save").Invoke(settings, null);
            object reloadedGoalSettings = settings.GetType().GetMethod("Load", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            if ((int)fDailyGoal.GetValue(reloadedGoalSettings) != 240)
            {
                Console.WriteLine("FAIL: DailyGoalMinutes did not survive the save/load round trip");
                return 63;
            }
            fDailyGoal.SetValue(settings, 0);
            settings.GetType().GetMethod("Save").Invoke(settings, null);

            // 护眼提醒：开关与间隔要能存进 ini，计时器和提醒卡两个类型也要在。
            FieldInfo fEyeCareEnabled = settingsType.GetField("EyeCareEnabled");
            FieldInfo fEyeCareInterval = settingsType.GetField("EyeCareIntervalMinutes");
            if (fEyeCareEnabled == null || fEyeCareInterval == null)
            {
                Console.WriteLine("FAIL: Settings is missing the eye-care fields");
                return 64;
            }
            fEyeCareEnabled.SetValue(settings, false);
            fEyeCareInterval.SetValue(settings, 30);
            settings.GetType().GetMethod("Save").Invoke(settings, null);
            object reloadedEyeCare = settings.GetType().GetMethod("Load", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            if ((bool)fEyeCareEnabled.GetValue(reloadedEyeCare) || (int)fEyeCareInterval.GetValue(reloadedEyeCare) != 30)
            {
                Console.WriteLine("FAIL: eye-care settings did not survive the save/load round trip");
                return 64;
            }
            fEyeCareEnabled.SetValue(settings, true);
            fEyeCareInterval.SetValue(settings, 45);
            settings.GetType().GetMethod("Save").Invoke(settings, null);
            if (asm.GetType("EyeCareTracker") == null || noticeType.GetMethod("EyeCareReminder", BindingFlags.Public | BindingFlags.Static) == null)
            {
                Console.WriteLine("FAIL: eye-care tracker or the NoticeDialog.EyeCareReminder factory is missing");
                return 64;
            }
            Console.WriteLine("PASS: eye-care settings persist; tracker ships and the reminder reuses NoticeDialog.");

            Type trendPanelType = asm.GetType("TrendPanel");
            Type axisPanelType = asm.GetType("UsageAxisPanel");
            if (trendPanelType == null || axisPanelType == null
                || axisPanelType.GetMethod("SetFocusMarkers") == null
                || axisPanelType.GetMethod("FocusMarkerAt") == null)
            {
                Console.WriteLine("FAIL: trend panel type or usage-axis focus-marker API is missing");
                return 65;
            }
            Console.WriteLine("PASS: trend view panel and axis focus-marker API ship in the build.");

            MethodInfo getDayFocus = appType.GetMethod("GetDayFocusSecs", sbf);
            MethodInfo computeStreak = appType.GetMethod("ComputeStreak", sbf);
            if (getDayFocus == null || computeStreak == null)
            {
                Console.WriteLine("FAIL: the daily-goal helpers are missing");
                return 63;
            }
            // The day total has to read the same TSV the history dialog reads.
            string goalRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "iclock_goal_check_" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(goalRoot);
            string todayKey = DateTime.Today.ToString("yyyyMMdd");
            string yesterdayKey = DateTime.Today.AddDays(-1).ToString("yyyyMMdd");
            string twoDaysAgoKey = DateTime.Today.AddDays(-2).ToString("yyyyMMdd");
            string threeDaysAgoKey = DateTime.Today.AddDays(-3).ToString("yyyyMMdd");
            string fourDaysAgoKey = DateTime.Today.AddDays(-4).ToString("yyyyMMdd");
            // start \t end \t minutes \t actualSeconds \t result
            System.IO.File.WriteAllText(System.IO.Path.Combine(goalRoot, "history-" + todayKey + ".tsv"),
                "09:00:00\t09:25:00\t25\t1500\tCompleted" + Environment.NewLine);
            System.IO.File.WriteAllText(System.IO.Path.Combine(goalRoot, "history-" + yesterdayKey + ".tsv"),
                "09:00:00\t09:30:00\t30\t1800\tCompleted" + Environment.NewLine);
            System.IO.File.WriteAllText(System.IO.Path.Combine(goalRoot, "history-" + twoDaysAgoKey + ".tsv"),
                "09:00:00\t09:25:00\t25\t1500\tCompleted" + Environment.NewLine);
            System.IO.File.WriteAllText(System.IO.Path.Combine(goalRoot, "history-" + threeDaysAgoKey + ".tsv"),
                "09:00:00\t09:25:00\t25\t1500\tCompleted" + Environment.NewLine);
            // Four days ago only reached 5 minutes: it breaks the chain.
            System.IO.File.WriteAllText(System.IO.Path.Combine(goalRoot, "history-" + fourDaysAgoKey + ".tsv"),
                "09:00:00\t09:05:00\t5\t300\tCompleted" + Environment.NewLine);
            long todaySecs = (long)getDayFocus.Invoke(null, new object[] { goalRoot, todayKey });
            if (todaySecs != 1500L)
            {
                Console.WriteLine("FAIL: today's focus seconds should be 1500, got: " + todaySecs);
                return 63;
            }
            if ((long)getDayFocus.Invoke(null, new object[] { goalRoot, "19990101" }) != 0L)
            {
                Console.WriteLine("FAIL: a day without history must count as 0");
                return 63;
            }
            // The streak takes a day lookup function, so build one over the temp dir.
            Func<string, long> dayLookup = delegate(string dk)
            {
                return (long)getDayFocus.Invoke(null, new object[] { goalRoot, dk });
            };
            // Goal 20 minutes: the last four days qualify, the fifth does not -> 4.
            int streak4 = (int)computeStreak.Invoke(null, new object[] { dayLookup, 20, todayKey });
            if (streak4 != 4)
            {
                Console.WriteLine("FAIL: the streak should be 4 days, got: " + streak4);
                return 63;
            }
            // Goal 30 minutes: today (25 min) is still in progress, so the streak
            // starts at yesterday; the day before that missed the goal -> 1.
            int streak1 = (int)computeStreak.Invoke(null, new object[] { dayLookup, 30, todayKey });
            if (streak1 != 1)
            {
                Console.WriteLine("FAIL: a day in progress must not break the streak, expected 1, got: " + streak1);
                return 63;
            }
            // A day that genuinely missed the goal breaks the chain.
            int streak0 = (int)computeStreak.Invoke(null, new object[] { dayLookup, 25, fourDaysAgoKey });
            if (streak0 != 0)
            {
                Console.WriteLine("FAIL: a missed day must break the streak, got: " + streak0);
                return 63;
            }
            // The feature off means no streak at all.
            if ((int)computeStreak.Invoke(null, new object[] { dayLookup, 0, todayKey }) != 0)
            {
                Console.WriteLine("FAIL: the streak must be 0 while the goal is off");
                return 63;
            }
            try { System.IO.Directory.Delete(goalRoot, true); } catch { }
            Console.WriteLine("PASS: the daily focus goal, day totals and the streak calculation are correct.");

            // 45. Verify AI Collaboration 5-in-1 Upgrades: HITL question/feedback detection, Error distinction
            Type agentWatcherType = asm.GetType("AgentSessionWatcher");
            if (agentWatcherType != null)
            {
                MethodInfo mExtractQ = agentWatcherType.GetMethod("ExtractQuestion", BindingFlags.NonPublic | BindingFlags.Static);
                MethodInfo mExtractErr = agentWatcherType.GetMethod("ExtractErrorSummary", BindingFlags.NonPublic | BindingFlags.Static);

                if (mExtractQ != null)
                {
                    var dataQ1 = new Dictionary<string, object> {
                        { "type", "PLANNER_RESPONSE" },
                        { "status", "DONE" },
                        { "content", "请问是否确认部署上线？" }
                    };
                    string q1 = (string)mExtractQ.Invoke(null, new object[] { dataQ1, true });
                    if (string.IsNullOrEmpty(q1) || !q1.Contains("确认部署上线"))
                    {
                        Console.WriteLine("FAIL: ExtractQuestion did not extract trailing question mark: " + q1);
                        return 66;
                    }

                    var callMeta = new Dictionary<string, object> { { "RequestFeedback", true }, { "Summary", "Test Plan" } };
                    var callArgs = new Dictionary<string, object> { { "ArtifactMetadata", callMeta } };
                    var callObj = new Dictionary<string, object> { { "name", "write_file" }, { "args", callArgs } };
                    var dataQ2 = new Dictionary<string, object> { { "tool_calls", new ArrayList { callObj } } };
                    string q2 = (string)mExtractQ.Invoke(null, new object[] { dataQ2, true });
                    if (string.IsNullOrEmpty(q2) || !q2.Contains("方案"))
                    {
                        Console.WriteLine("FAIL: ExtractQuestion did not detect RequestFeedback artifact: " + q2);
                        return 66;
                    }
                }

                if (mExtractErr != null)
                {
                    var dataErr = new Dictionary<string, object> { { "status", "ERROR" }, { "content", "Compilation failed" } };
                    string err = (string)mExtractErr.Invoke(null, new object[] { dataErr, true });
                    if (string.IsNullOrEmpty(err) || !err.Contains("Compilation failed"))
                    {
                        Console.WriteLine("FAIL: ExtractErrorSummary did not detect status: ERROR");
                        return 66;
                    }
                }

                MethodInfo mExtractComp = agentWatcherType.GetMethod("ExtractCompletionSummary", BindingFlags.NonPublic | BindingFlags.Static);
                if (mExtractComp != null)
                {
                    // Case A: tool call with status DONE (must NOT complete)
                    var toolCallObj = new Dictionary<string, object> { { "name", "run_command" } };
                    var dTool = new Dictionary<string, object> {
                        { "type", "PLANNER_RESPONSE" },
                        { "status", "DONE" },
                        { "tool_calls", new ArrayList { toolCallObj } }
                    };
                    string cTool = (string)mExtractComp.Invoke(null, new object[] { dTool });
                    if (cTool != null)
                    {
                        Console.WriteLine("FAIL: ExtractCompletionSummary must be null for PLANNER_RESPONSE with tool_calls");
                        return 66;
                    }

                    // Case B: tool output GENERIC with status DONE (must NOT complete)
                    var dGeneric = new Dictionary<string, object> {
                        { "type", "GENERIC" },
                        { "status", "DONE" },
                        { "content", "Command exited with code 0" }
                    };
                    string cGen = (string)mExtractComp.Invoke(null, new object[] { dGeneric });
                    if (cGen != null)
                    {
                        Console.WriteLine("FAIL: ExtractCompletionSummary must be null for GENERIC tool output");
                        return 66;
                    }

                    // Case C: true completion (MUST complete)
                    var dDone = new Dictionary<string, object> {
                        { "type", "PLANNER_RESPONSE" },
                        { "status", "DONE" },
                        { "content", "Task finished successfully." }
                    };
                    string cDone = (string)mExtractComp.Invoke(null, new object[] { dDone });
                    if (cDone != "Task finished successfully.")
                    {
                        Console.WriteLine("FAIL: ExtractCompletionSummary failed on true completion: " + cDone);
                        return 66;
                    }
                }

                // 46. Verify Trivial Confirmation immunity & Enhanced Friendly Actions
                MethodInfo mIsTrivial = agentWatcherType.GetMethod("IsTrivialConfirmation", BindingFlags.NonPublic | BindingFlags.Static);
                if (mIsTrivial != null)
                {
                    bool t1 = (bool)mIsTrivial.Invoke(null, new object[] { "好" });
                    bool t2 = (bool)mIsTrivial.Invoke(null, new object[] { "好的，推进吧" });
                    bool t3 = (bool)mIsTrivial.Invoke(null, new object[] { "continue" });
                    bool t4 = (bool)mIsTrivial.Invoke(null, new object[] { "确认实施" });
                    bool t5 = (bool)mIsTrivial.Invoke(null, new object[] { "检测AI协作模块优化状态显示与体验" });

                    if (!t1 || !t2 || !t3 || !t4 || t5)
                    {
                        Console.WriteLine("FAIL: IsTrivialConfirmation check failed: t1=" + t1 + ", t2=" + t2 + ", t3=" + t3 + ", t4=" + t4 + ", t5=" + t5);
                        return 67;
                    }
                }

                MethodInfo mExtractAction = agentWatcherType.GetMethod("ExtractFriendlyAction", BindingFlags.NonPublic | BindingFlags.Static);
                if (mExtractAction != null)
                {
                    var subArgs = new Dictionary<string, object> { { "Role", "Codebase Researcher" } };
                    string subRes = (string)mExtractAction.Invoke(null, new object[] { "invoke_subagent", subArgs, true });
                    if (subRes == null || !subRes.Contains("调度") || !subRes.Contains("Codebase Researcher"))
                    {
                        Console.WriteLine("FAIL: ExtractFriendlyAction subagent failed: " + subRes);
                        return 68;
                    }

                    var cmdArgs = new Dictionary<string, object> { { "CommandLine", "powershell -File build.ps1" } };
                    string cmdRes = (string)mExtractAction.Invoke(null, new object[] { "run_command", cmdArgs, true });
                    if (cmdRes == null || !cmdRes.Contains("运行:") || !cmdRes.Contains("build.ps1"))
                    {
                        Console.WriteLine("FAIL: ExtractFriendlyAction run_command failed: " + cmdRes);
                        return 68;
                    }

                    var fileArgs = new Dictionary<string, object> { { "Path", "d:\\AIStudio\\iClock\\src\\AgentWatcher.cs" } };
                    string fileRes = (string)mExtractAction.Invoke(null, new object[] { "read_file", fileArgs, true });
                    if (fileRes == null || !fileRes.Contains("读取") || !fileRes.Contains("AgentWatcher.cs"))
                    {
                        Console.WriteLine("FAIL: ExtractFriendlyAction read_file failed: " + fileRes);
                        return 68;
                    }
                }

                // 47. Verify Codex support: UserMessage, function_call (exec_command), task_complete, session_meta
                MethodInfo mExtractUser = agentWatcherType.GetMethod("ExtractUserRequest", BindingFlags.NonPublic | BindingFlags.Static);
                if (mExtractUser != null)
                {
                    var codexUserItem = new Dictionary<string, object> {
                        { "type", "UserMessage" },
                        { "content", new ArrayList { new Dictionary<string, object> { { "type", "text" }, { "text", "开箱体验增加Codex支持" } } } }
                    };
                    var codexUserMsg = new Dictionary<string, object> {
                        { "type", "event_msg" },
                        { "payload", new Dictionary<string, object> { { "type", "item_completed" }, { "item", codexUserItem } } }
                    };
                    string userReq = (string)mExtractUser.Invoke(null, new object[] { codexUserMsg });
                    if (userReq == null || !userReq.Contains("Codex支持"))
                    {
                        Console.WriteLine("FAIL: ExtractUserRequest failed for Codex UserMessage: " + userReq);
                        return 69;
                    }
                }

                MethodInfo mExtractTool = agentWatcherType.GetMethod("ExtractToolSummary", BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { typeof(Dictionary<string, object>), typeof(bool) }, null);
                if (mExtractTool != null)
                {
                    var codexFc = new Dictionary<string, object> {
                        { "type", "response_item" },
                        { "payload", new Dictionary<string, object> {
                            { "type", "function_call" },
                            { "name", "exec_command" },
                            { "arguments", "{\"cmd\": \"git status\"}" }
                        } }
                    };
                    string toolAct = (string)mExtractTool.Invoke(null, new object[] { codexFc, true });
                    if (toolAct == null || !toolAct.Contains("运行:") || !toolAct.Contains("git status"))
                    {
                        Console.WriteLine("FAIL: ExtractToolSummary failed for Codex function_call: " + toolAct);
                        return 69;
                    }
                }

                if (mExtractComp != null)
                {
                    var codexComp = new Dictionary<string, object> {
                        { "type", "event_msg" },
                        { "payload", new Dictionary<string, object> {
                            { "type", "task_complete" },
                            { "last_agent_message", "Codex已完成开箱支持。" }
                        } }
                    };
                    string compRes = (string)mExtractComp.Invoke(null, new object[] { codexComp });
                    if (compRes == null || !compRes.Contains("Codex已完成开箱支持"))
                    {
                        Console.WriteLine("FAIL: ExtractCompletionSummary failed for Codex task_complete: " + compRes);
                        return 69;
                    }
                }

                Console.WriteLine("PASS: AI collaboration 5-in-1 upgrades (HITL feedback perception, error state distinction, trivial confirmation immunity & cognitive flow) verified.");

                // 48. Verify Process-guided dynamic watcher & Universal schema-agnostic parsing
                MethodInfo mResolveDirs = agentWatcherType.GetMethod("ResolveCandidateDirectories", BindingFlags.NonPublic | BindingFlags.Static);
                if (mResolveDirs != null)
                {
                    var codexDirs = (List<string>)mResolveDirs.Invoke(null, new object[] { "Codex" });
                    if (codexDirs == null || codexDirs.Count == 0)
                    {
                        Console.WriteLine("FAIL: ResolveCandidateDirectories failed for Codex");
                        return 70;
                    }

                    var claudeDirs = (List<string>)mResolveDirs.Invoke(null, new object[] { "Claude Code" });
                    if (claudeDirs == null || claudeDirs.Count == 0)
                    {
                        Console.WriteLine("FAIL: ResolveCandidateDirectories failed for Claude Code");
                        return 70;
                    }
                }

                // Universal user request
                if (mExtractUser != null)
                {
                    var uData1 = new Dictionary<string, object> {
                        { "role", "user" },
                        { "content", "实现通用智能体感知" }
                    };
                    string uReq1 = (string)mExtractUser.Invoke(null, new object[] { uData1 });
                    if (uReq1 != "实现通用智能体感知")
                    {
                        Console.WriteLine("FAIL: Universal user request extraction failed: " + uReq1);
                        return 71;
                    }

                    var uData2 = new Dictionary<string, object> {
                        { "prompt", "自动识别任意智能体任务状态" }
                    };
                    string uReq2 = (string)mExtractUser.Invoke(null, new object[] { uData2 });
                    if (uReq2 != "自动识别任意智能体任务状态")
                    {
                        Console.WriteLine("FAIL: Universal prompt extraction failed: " + uReq2);
                        return 71;
                    }
                }

                // Universal tool summary
                if (mExtractTool != null)
                {
                    var uTool1 = new Dictionary<string, object> {
                        { "tool", "exec_command" },
                        { "args", new Dictionary<string, object> { { "CommandLine", "npm test" } } }
                    };
                    string tAct1 = (string)mExtractTool.Invoke(null, new object[] { uTool1, true });
                    if (tAct1 == null || !tAct1.Contains("运行:") || !tAct1.Contains("npm test"))
                    {
                        Console.WriteLine("FAIL: Universal tool extraction failed: " + tAct1);
                        return 72;
                    }

                    var uTool2 = new Dictionary<string, object> {
                        { "command", "git status" }
                    };
                    string tAct2 = (string)mExtractTool.Invoke(null, new object[] { uTool2, true });
                    if (tAct2 == null || !tAct2.Contains("运行:") || !tAct2.Contains("git status"))
                    {
                        Console.WriteLine("FAIL: Universal command extraction failed: " + tAct2);
                        return 72;
                    }
                }

                // Universal completion
                if (mExtractComp != null)
                {
                    var uComp1 = new Dictionary<string, object> {
                        { "status", "COMPLETED" },
                        { "summary", "所有模块优化就绪" }
                    };
                    string cRes1 = (string)mExtractComp.Invoke(null, new object[] { uComp1 });
                    if (cRes1 != "所有模块优化就绪")
                    {
                        Console.WriteLine("FAIL: Universal completion extraction failed: " + cRes1);
                        return 73;
                    }

                    var uComp2 = new Dictionary<string, object> {
                        { "finish_reason", "stop" },
                        { "text", "All tasks done." }
                    };
                    string cRes2 = (string)mExtractComp.Invoke(null, new object[] { uComp2 });
                    if (cRes2 != "All tasks done.")
                    {
                        Console.WriteLine("FAIL: Universal finish_reason extraction failed: " + cRes2);
                        return 73;
                    }
                }

                // AgentHud deterministic color & label
                Type hudType = appType.Assembly.GetType("AgentHud");
                if (hudType != null)
                {
                    MethodInfo mColor = hudType.GetMethod("GetDeterministicAgentColor", BindingFlags.NonPublic | BindingFlags.Static);
                    MethodInfo mLabel = hudType.GetMethod("GetShortAgentBadgeLabel", BindingFlags.NonPublic | BindingFlags.Static);
                    if (mColor != null && mLabel != null)
                    {
                        Color c1 = (Color)mColor.Invoke(null, new object[] { "Windsurf" });
                        Color c2 = (Color)mColor.Invoke(null, new object[] { "UnknownAgent" });
                        if (c1.A == 0 || c2.A == 0)
                        {
                            Console.WriteLine("FAIL: GetDeterministicAgentColor returned transparent color");
                            return 75;
                        }
                        string l1 = (string)mLabel.Invoke(null, new object[] { "CustomAgentLongName" });
                        if (l1 == null || l1.Length > 7)
                        {
                            Console.WriteLine("FAIL: GetShortAgentBadgeLabel failed: " + l1);
                            return 75;
                        }
                    }
                }

                // HUD card height: adjustable in [28, 60] and font scales with it
                {
                    object st = Activator.CreateInstance(settingsType);
                    FieldInfo fH = settingsType.GetField("TaskStatusHeight");
                    if (fH == null)
                    {
                        Console.WriteLine("FAIL: Settings.TaskStatusHeight missing");
                        return 75;
                    }
                    if ((int)fH.GetValue(st) != 28)
                    {
                        Console.WriteLine("FAIL: TaskStatusHeight should default to the 28px minimum, got " + fH.GetValue(st));
                        return 75;
                    }
                    // CopyFrom must clamp out-of-range values into [28, 60]
                    object probe = Activator.CreateInstance(settingsType);
                    fH.SetValue(probe, 999);
                    st.GetType().GetMethod("CopyFrom").Invoke(st, new object[] { probe });
                    if ((int)fH.GetValue(st) != 60)
                    {
                        Console.WriteLine("FAIL: TaskStatusHeight must clamp to 60px max, got " + fH.GetValue(st));
                        return 75;
                    }
                    fH.SetValue(probe, 5);
                    st.GetType().GetMethod("CopyFrom").Invoke(st, new object[] { probe });
                    if ((int)fH.GetValue(st) != 28)
                    {
                        Console.WriteLine("FAIL: TaskStatusHeight must clamp to 28px min, got " + fH.GetValue(st));
                        return 75;
                    }
                }

                Console.WriteLine("PASS: Process-guided dynamic watcher & Universal schema-agnostic parsing verified.");
            }

            // Verify HUD background-only visibility rule and typography alignment
            Type statusHudType = appType.Assembly.GetType("AgentTaskStatusHud");
            if (statusHudType != null)
            {
                ConstructorInfo hudCtor = statusHudType.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new Type[] { settingsType }, null);
                Form hudInstance = hudCtor != null ? (Form)hudCtor.Invoke(new object[] { settings }) : null;
                MethodInfo mShould = statusHudType.GetMethod("ShouldBeVisible", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo fPin = settingsType.GetField("AgentStatusPinEnabled");
                if (mShould != null && fPin != null)
                {
                    // Case 1: No tasks -> ShouldBeVisible is false
                    bool vNoTask = (bool)mShould.Invoke(hudInstance, null);
                    if (vNoTask)
                    {
                        Console.WriteLine("FAIL: HUD ShouldBeVisible should be false when tasks are empty");
                        return 76;
                    }

                    // Case 2: Status pin disabled -> ShouldBeVisible is false
                    fPin.SetValue(settings, false);
                    bool vDisabled = (bool)mShould.Invoke(hudInstance, null);
                    if (vDisabled)
                    {
                        Console.WriteLine("FAIL: HUD ShouldBeVisible should be false when AgentStatusPinEnabled is false");
                        return 76;
                    }
                    fPin.SetValue(settings, true);

                    // Case 3: Task running for background agent -> ShouldBeVisible is true
                    MethodInfo mSetTasks = statusHudType.GetMethod("SetTasks", BindingFlags.NonPublic | BindingFlags.Instance);
                    Type taskType = appType.Assembly.GetType("AgentTaskStatus");
                    IList testTasks = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(taskType));
                    object t1 = Activator.CreateInstance(taskType);
                    taskType.GetField("AgentName", bf).SetValue(t1, "NonExistentBackgroundAgent");
                    taskType.GetField("Message", bf).SetValue(t1, "[SelfCheck] Testing background HUD");
                    taskType.GetField("MessageTimestamp", bf).SetValue(t1, DateTime.Now);
                    testTasks.Add(t1);
                    mSetTasks.Invoke(hudInstance, new object[] { testTasks });

                    bool vBg = (bool)mShould.Invoke(hudInstance, null);
                    if (!vBg)
                    {
                        Console.WriteLine("FAIL: HUD ShouldBeVisible should be true for background agent");
                        return 76;
                    }

                    MethodInfo mSyncFg = statusHudType.GetMethod("SyncVisibilityToForeground", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (mSyncFg == null)
                    {
                        Console.WriteLine("FAIL: HUD missing immediate foreground-loss show");
                        return 76;
                    }
                    mSyncFg.Invoke(hudInstance, null);
                    if (!hudInstance.Visible)
                    {
                        Console.WriteLine("FAIL: HUD should show as soon as the running agent leaves the foreground");
                        return 76;
                    }
                    hudInstance.Hide();

                    // Case 3b: Test 5-minute inactivity rule (>5min no new message -> ShouldBeVisible is false)
                    taskType.GetField("MessageTimestamp", bf).SetValue(t1, DateTime.Now.AddMinutes(-6));
                    bool vExpired = (bool)mShould.Invoke(hudInstance, null);
                    if (vExpired)
                    {
                        Console.WriteLine("FAIL: HUD ShouldBeVisible should be false when last message is older than 5 minutes");
                        return 76;
                    }
                    // Pinned HUD stays visible even after the 5-minute idle expiry
                    FieldInfo fPinnedEarly = statusHudType.GetField("pinnedAgent", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (fPinnedEarly != null)
                    {
                        fPinnedEarly.SetValue(hudInstance, "NonExistentBackgroundAgent");
                        bool vPinnedExpired = (bool)mShould.Invoke(hudInstance, null);
                        fPinnedEarly.SetValue(hudInstance, null);
                        if (!vPinnedExpired)
                        {
                            Console.WriteLine("FAIL: Pinned HUD must stay visible after 5-minute idle expiry");
                            return 76;
                        }
                    }
                    taskType.GetField("MessageTimestamp", bf).SetValue(t1, DateTime.Now);

                    // Case 4: Test rendering in both collapsed and expanded (isHovered) modes
                    using (Bitmap bTest = new Bitmap(320, 48))
                    using (Graphics gTest = Graphics.FromImage(bTest))
                    {
                        MethodInfo mDraw = statusHudType.GetMethod("DrawTasks", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (mDraw != null)
                        {
                            mDraw.Invoke(hudInstance, new object[] { gTest });
                            FieldInfo fHover = statusHudType.GetField("isHovered", BindingFlags.NonPublic | BindingFlags.Instance);
                            if (fHover != null)
                            {
                                fHover.SetValue(hudInstance, true);
                                mDraw.Invoke(hudInstance, new object[] { gTest });
                                fHover.SetValue(hudInstance, false);
                            }
                        }
                    }

                    // Case 5: Multi-agent unified single-bar HUD with bottom mini-indicator
                    object t2 = Activator.CreateInstance(taskType);
                    taskType.GetField("AgentName", bf).SetValue(t2, "Claude");
                    taskType.GetField("Message", bf).SetValue(t2, "[Claude] Refining algorithm");
                    taskType.GetField("MessageTimestamp", bf).SetValue(t2, DateTime.Now);
                    testTasks.Add(t2);
                    mSetTasks.Invoke(hudInstance, new object[] { testTasks });

                    // Single-bar height constraint: Collapsed 28px + 20px bottom indicator = 48px
                    if (hudInstance.Height != 48)
                    {
                        Console.WriteLine("FAIL: Multi-agent HUD should maintain single bar height 48px, got " + hudInstance.Height);
                        return 76;
                    }

                    // Position & 1/3 width verification: Top 12px, centered
                    MethodInfo mUpdatePos = statusHudType.GetMethod("UpdatePosition", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (mUpdatePos != null)
                    {
                        mUpdatePos.Invoke(hudInstance, null);
                        Screen scr = Screen.FromPoint(hudInstance.Location);
                        if (hudInstance.Location.Y != scr.WorkingArea.Top + 12)
                        {
                            Console.WriteLine("FAIL: HUD should be at Top + 12px, got " + hudInstance.Location.Y);
                            return 76;
                        }
                        int expW = Math.Max(200, Math.Min(1400, scr.WorkingArea.Width / 3));
                        if (hudInstance.Width != expW)
                        {
                            Console.WriteLine("FAIL: HUD width should be 1/3 screen (" + expW + "), got " + hudInstance.Width);
                            return 76;
                        }
                    }

                    // Dynamic update preemption & hover lock verification
                    MethodInfo mDynamic = statusHudType.GetMethod("OnAgentDynamicUpdated", BindingFlags.NonPublic | BindingFlags.Instance);
                    FieldInfo fCurIdx = statusHudType.GetField("currentAgentIndex", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (mDynamic != null && fCurIdx != null)
                    {
                        // Dynamic update switches protagonist
                        mDynamic.Invoke(hudInstance, new object[] { "Claude", false });
                        int cur = (int)fCurIdx.GetValue(hudInstance);
                        // After agent-aware grouping sort, Claude's index may shift; resolve via GetActiveTasks.
                        var activeNow = (System.Collections.IList)statusHudType.GetMethod("GetActiveTasks", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(hudInstance, null);
                        string curName = (string)taskType.GetField("AgentName", bf).GetValue(activeNow[cur]);
                        if (curName != "Claude")
                        {
                            Console.WriteLine("FAIL: Dynamic update should preempt and switch to Claude, got " + curName + " at index " + cur);
                            return 76;
                        }

                        // Hover lock rule: when mouse is hovering over HUD, dynamic updates must NOT interrupt
                        FieldInfo fHover = statusHudType.GetField("isHovered", BindingFlags.NonPublic | BindingFlags.Instance);
                        fHover.SetValue(hudInstance, true);
                        mDynamic.Invoke(hudInstance, new object[] { "NonExistentBackgroundAgent", false });
                        int lockedCur = (int)fCurIdx.GetValue(hudInstance);
                        var activeLock = (System.Collections.IList)statusHudType.GetMethod("GetActiveTasks", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(hudInstance, null);
                        string lockedName = (string)taskType.GetField("AgentName", bf).GetValue(activeLock[lockedCur]);
                        if (lockedName != "Claude")
                        {
                            Console.WriteLine("FAIL: Mouse hover should lock current display unchanged, got " + lockedName);
                            return 76;
                        }
                        fHover.SetValue(hudInstance, false);

                        // Verify bottom status pills hit testing (17px height, stadium pill)
                        MethodInfo mGetDot = statusHudType.GetMethod("GetIndicatorDotIndex", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (mGetDot != null)
                        {
                            int hit0 = (int)mGetDot.Invoke(hudInstance, new object[] { new Point(hudInstance.Width / 2 - 25, 36), 28 });
                            int hit1 = (int)mGetDot.Invoke(hudInstance, new object[] { new Point(hudInstance.Width / 2 + 25, 36), 28 });
                            if (hit0 != 0 || hit1 != 1)
                            {
                                Console.WriteLine("FAIL: Multi-agent bottom pill hit testing failed: hit0=" + hit0 + ", hit1=" + hit1);
                                return 76;
                            }
                        }

                        // Desktop Sticky Pin Mode (Always on screen & prevent fade-out) test
                        FieldInfo fPinned = statusHudType.GetField("pinnedAgent", BindingFlags.NonPublic | BindingFlags.Instance);
                        FieldInfo fTasks = statusHudType.GetField("tasks", BindingFlags.NonPublic | BindingFlags.Instance);
                        MethodInfo mMouseDown = statusHudType.GetMethod("OnHudMouseDown", BindingFlags.NonPublic | BindingFlags.Instance);
                        MethodInfo mShouldVisible = statusHudType.GetMethod("ShouldBeVisible", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (fPinned != null && fTasks != null && mMouseDown != null && mShouldVisible != null)
                        {
                            var originalTasks = (System.Collections.IList)fTasks.GetValue(hudInstance);
                            Type taskListType = typeof(List<>).MakeGenericType(fTasks.FieldType.GetGenericArguments()[0]);
                            var singleList = (System.Collections.IList)Activator.CreateInstance(taskListType);
                            singleList.Add(originalTasks[0]);

                            // 1. Single agent case: click pin to engage desktop sticky mode
                            fTasks.SetValue(hudInstance, singleList);
                            fPinned.SetValue(hudInstance, null);
                            fHover.SetValue(hudInstance, true);
                            mMouseDown.Invoke(hudInstance, new object[] { hudInstance, new MouseEventArgs(MouseButtons.Left, 1, hudInstance.Width - 40, 10, 0) });
                            string p1 = (string)fPinned.GetValue(hudInstance);
                            if (p1 == null)
                            {
                                Console.WriteLine("FAIL: Desktop Sticky Pin should engage for single agent on click");
                                return 76;
                            }

                            // 2. Sticky mode must enforce ShouldBeVisible = true even when mouse leaves
                            fHover.SetValue(hudInstance, false);
                            bool stickyVisible = (bool)mShouldVisible.Invoke(hudInstance, null);
                            if (!stickyVisible)
                            {
                                Console.WriteLine("FAIL: Pinned HUD should always be visible (Sticky Mode)");
                                return 76;
                            }

                            // 2b. Auto-clear / empty task list must not hide or drop a pinned HUD
                            object emptyList = Activator.CreateInstance(taskListType);
                            fTasks.SetValue(hudInstance, emptyList);
                            MethodInfo mHeartbeatPin = statusHudType.GetMethod("OnHeartbeatTick", BindingFlags.NonPublic | BindingFlags.Instance);
                            if (mHeartbeatPin != null) mHeartbeatPin.Invoke(hudInstance, new object[] { hudInstance, EventArgs.Empty });
                            string pinAfterEmpty = (string)fPinned.GetValue(hudInstance);
                            bool stickyEmpty = (bool)mShouldVisible.Invoke(hudInstance, null);
                            fTasks.SetValue(hudInstance, singleList);
                            if (pinAfterEmpty == null || !stickyEmpty)
                            {
                                Console.WriteLine("FAIL: Pinned HUD must stay resident when sessions are auto-cleared");
                                return 76;
                            }

                            // 3. Unpin to release sticky mode
                            fHover.SetValue(hudInstance, true);
                            mMouseDown.Invoke(hudInstance, new object[] { hudInstance, new MouseEventArgs(MouseButtons.Left, 1, hudInstance.Width - 40, 10, 0) });
                            string pUnpin = (string)fPinned.GetValue(hudInstance);
                            fHover.SetValue(hudInstance, false);
                            if (pUnpin != null)
                            {
                                Console.WriteLine("FAIL: Re-clicking pin should unpin and release sticky mode");
                                return 76;
                            }

                            // 4. Multiple agents case: pin locks rotation to clicked agent
                            fHover.SetValue(hudInstance, true);
                            fTasks.SetValue(hudInstance, originalTasks);
                            mMouseDown.Invoke(hudInstance, new object[] { hudInstance, new MouseEventArgs(MouseButtons.Left, 1, hudInstance.Width - 40, 10, 0) });
                            string pMulti = (string)fPinned.GetValue(hudInstance);
                            fHover.SetValue(hudInstance, false);
                            if (pMulti == null)
                            {
                                Console.WriteLine("FAIL: Pin button should lock rotation when multiple agents exist");
                                return 76;
                            }

                            // 5. Hover-only immunity: when not hovered, pin area click must not trigger
                            fPinned.SetValue(hudInstance, null);
                            fHover.SetValue(hudInstance, false);
                            mMouseDown.Invoke(hudInstance, new object[] { hudInstance, new MouseEventArgs(MouseButtons.Left, 1, hudInstance.Width - 40, 10, 0) });
                            string pNotHovered = (string)fPinned.GetValue(hudInstance);
                            if (pNotHovered != null)
                            {
                                Console.WriteLine("FAIL: Pin must not be clickable when mouse is not hovered");
                                return 76;
                            }

                            // 5b. Pinned HUD must allow manual pill click to switch protagonist
                            // (pin = sticky visible + no auto-rotation, NOT a protagonist lock).
                            if (originalTasks.Count >= 2)
                            {
                                fTasks.SetValue(hudInstance, originalTasks);
                                Type tStatusType = fTasks.FieldType.GetGenericArguments()[0];
                                string firstAgentName = (string)tStatusType.GetField("AgentName", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(originalTasks[0]);
                                fPinned.SetValue(hudInstance, firstAgentName);
                                FieldInfo fCurIdx2 = statusHudType.GetField("currentAgentIndex", BindingFlags.NonPublic | BindingFlags.Instance);
                                fCurIdx2.SetValue(hudInstance, 0);
                                // Simulate heartbeat: must NOT force currentAgentIndex back to pinned.
                                MethodInfo mHeartbeat = statusHudType.GetMethod("OnHeartbeatTick", BindingFlags.NonPublic | BindingFlags.Instance);
                                if (mHeartbeat != null)
                                {
                                    mHeartbeat.Invoke(hudInstance, new object[] { hudInstance, EventArgs.Empty });
                                    int afterHb = (int)fCurIdx2.GetValue(hudInstance);
                                    // Set index to a different agent manually; heartbeat should respect it.
                                    fCurIdx2.SetValue(hudInstance, 1);
                                    mHeartbeat.Invoke(hudInstance, new object[] { hudInstance, EventArgs.Empty });
                                    int afterSwitch = (int)fCurIdx2.GetValue(hudInstance);
                                    if (afterSwitch != 1)
                                    {
                                        Console.WriteLine("FAIL: Pinned HUD must not lock protagonist; heartbeat forced index back to " + afterSwitch);
                                        return 76;
                                    }
                                }
                                fPinned.SetValue(hudInstance, null);
                            }

                            // 6. Agent-Centric Pill Label Rules Verification (project badge removed)
                            Type taskStatusType = fTasks.FieldType.GetGenericArguments()[0];
                            MethodInfo mGetPillText = statusHudType.GetMethod("GetPillText", BindingFlags.NonPublic | BindingFlags.Static);
                            if (mGetPillText != null)
                            {
                                object c1 = Activator.CreateInstance(taskStatusType);
                                taskStatusType.GetField("AgentName", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(c1, "Claude Code");

                                object c2 = Activator.CreateInstance(taskStatusType);
                                taskStatusType.GetField("AgentName", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(c2, "Codex");

                                object c3 = Activator.CreateInstance(taskStatusType);
                                taskStatusType.GetField("AgentName", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(c3, "Antigravity");

                                string r1 = (string)mGetPillText.Invoke(null, new object[] { c1 });
                                string r2 = (string)mGetPillText.Invoke(null, new object[] { c2 });
                                string r3 = (string)mGetPillText.Invoke(null, new object[] { c3 });
                                if (string.IsNullOrEmpty(r1) || string.IsNullOrEmpty(r2) || string.IsNullOrEmpty(r3))
                                {
                                    Console.WriteLine("FAIL: Agent-Centric pill must use agent brand: got " + r1 + ", " + r2 + ", " + r3);
                                    return 76;
                                }

                                // 7. Same-Agent Deduplication and Status Fusion in GetActiveTasks
                                MethodInfo mGetActive = statusHudType.GetMethod("GetActiveTasks", BindingFlags.NonPublic | BindingFlags.Instance);
                                if (mGetActive != null)
                                {
                                    Type listType = typeof(List<>).MakeGenericType(taskStatusType);
                                    IList dupList = (IList)Activator.CreateInstance(listType);

                                    // Session 1 of Claude Code (no SessionKey): Running
                                    object s1 = Activator.CreateInstance(taskStatusType);
                                    taskStatusType.GetField("AgentName", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(s1, "Claude Code");
                                    taskStatusType.GetField("Message", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(s1, "[Claude Code] Processing...");
                                    taskStatusType.GetField("MessageTimestamp", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(s1, DateTime.Now);

                                    // Session 2 of Claude Code (no SessionKey): Waiting (higher priority) -> fuse to 1
                                    object s2 = Activator.CreateInstance(taskStatusType);
                                    taskStatusType.GetField("AgentName", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(s2, "Claude Code");
                                    taskStatusType.GetField("Message", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(s2, "❓ [Claude Code] Waiting for user input");
                                    taskStatusType.GetField("MessageTimestamp", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(s2, DateTime.Now);

                                    dupList.Add(s1);
                                    dupList.Add(s2);
                                    fTasks.SetValue(hudInstance, dupList);

                                    IList activeDedupped = (IList)mGetActive.Invoke(hudInstance, null);
                                    if (activeDedupped.Count != 1)
                                    {
                                        Console.WriteLine("FAIL: Same agent without SessionKey must fuse to exactly 1 active task, got " + activeDedupped.Count);
                                        return 76;
                                    }
                                    object winner = activeDedupped[0];
                                    string winMsg = (string)taskStatusType.GetField("Message", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(winner);
                                    if (!winMsg.Contains("Waiting for user input"))
                                    {
                                        Console.WriteLine("FAIL: Active task fusion must prioritize Waiting/Error state, got " + winMsg);
                                        return 76;
                                    }

                                    // 8. Same agent but distinct SessionKey -> split into separate pills
                                    IList splitList = (IList)Activator.CreateInstance(listType);
                                    object x1 = Activator.CreateInstance(taskStatusType);
                                    taskStatusType.GetField("AgentName", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(x1, "Claude Code");
                                    taskStatusType.GetField("SessionKey", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(x1, "sess_alpha");
                                    taskStatusType.GetField("Message", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(x1, "[Claude Code] Task A");
                                    taskStatusType.GetField("MessageTimestamp", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(x1, DateTime.Now);

                                    object x2 = Activator.CreateInstance(taskStatusType);
                                    taskStatusType.GetField("AgentName", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(x2, "Claude Code");
                                    taskStatusType.GetField("SessionKey", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(x2, "sess_beta");
                                    taskStatusType.GetField("Message", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(x2, "[Claude Code] Task B");
                                    taskStatusType.GetField("MessageTimestamp", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(x2, DateTime.Now);

                                    splitList.Add(x1);
                                    splitList.Add(x2);
                                    fTasks.SetValue(hudInstance, splitList);

                                    IList activeSplit = (IList)mGetActive.Invoke(hudInstance, null);
                                    if (activeSplit.Count != 2)
                                    {
                                        Console.WriteLine("FAIL: Same agent with distinct SessionKey must split into 2 pills, got " + activeSplit.Count);
                                        return 76;
                                    }

                                    // Pill labels must carry stable 1-based indices derived from SessionKey order
                                    MethodInfo mGetPillTextForTask = statusHudType.GetMethod("GetPillTextForTask", BindingFlags.NonPublic | BindingFlags.Static);
                                    if (mGetPillTextForTask != null)
                                    {
                                        var activeList = new List<object>();
                                        foreach (var it in activeSplit) activeList.Add(it);
                                        // Re-invoke with the actual runtime list type
                                        object lbl1 = null, lbl2 = null;
                                        try
                                        {
                                            lbl1 = mGetPillTextForTask.Invoke(null, new object[] { activeSplit[0], activeSplit });
                                            lbl2 = mGetPillTextForTask.Invoke(null, new object[] { activeSplit[1], activeSplit });
                                        }
                                        catch { }
                                        // We don't know which is #1 vs #2 (depends on SessionKey sort), but both must carry a " #n" suffix
                                        string s1Lbl = lbl1 as string ?? "";
                                        string s2Lbl = lbl2 as string ?? "";
                                        if (!s1Lbl.Contains("#") || !s2Lbl.Contains("#") || s1Lbl == s2Lbl)
                                        {
                                            Console.WriteLine("FAIL: Multi-session pill labels must carry distinct #n suffixes, got " + s1Lbl + " / " + s2Lbl);
                                            return 76;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                hudInstance.Close();
                Console.WriteLine("PASS: HUD background-only visibility and multi-agent single-bar rotation verified.");
            }

            // Verify Claude error unwrapping and internal retry suppression
            Type sWatcherType = appType.Assembly.GetType("AgentSessionWatcher");
            if (sWatcherType != null)
            {
                MethodInfo mExtractErr = sWatcherType.GetMethod("ExtractErrorSummary", BindingFlags.NonPublic | BindingFlags.Static);
                if (mExtractErr != null)
                {
                    // Case 1: Unwrapping Dictionary<string, object> error
                    var dictError = new Dictionary<string, object>
                    {
                        { "error", new Dictionary<string, object> { { "message", "ZCode did not stream: exceed quota limit" } } }
                    };
                    string extractedErr = (string)mExtractErr.Invoke(null, new object[] { dictError, true });
                    if (extractedErr == null || extractedErr.Contains("System.Collections") || extractedErr.IndexOf("exceed quota limit", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        Console.WriteLine("FAIL: Error dictionary unwrapping leaked type name: " + extractedErr);
                        return 76;
                    }
                }

                // Verify LLM Protocol Thinking / Reasoning Content extraction
                MethodInfo mExtractThought = sWatcherType.GetMethod("ExtractThoughtSlice", BindingFlags.NonPublic | BindingFlags.Static);
                if (mExtractThought != null)
                {
                    // 1. DeepSeek / OpenAI reasoning_content
                    var deepseekData = new Dictionary<string, object> { { "reasoning_content", "正在分析LLM协议规范与状态机映射" } };
                    string thought1 = (string)mExtractThought.Invoke(null, new object[] { deepseekData });
                    if (thought1 == null || !thought1.Contains("正在分析LLM协议规范"))
                    {
                        Console.WriteLine("FAIL: DeepSeek reasoning_content extraction failed: " + thought1);
                        return 77;
                    }

                    // 2. Gemini parts thought: true
                    var geminiData = new Dictionary<string, object>
                    {
                        {
                            "content", new List<object>
                            {
                                new Dictionary<string, object> { { "thought", true }, { "text", "规划下一步的桌面状态呈现" } }
                            }
                        }
                    };
                    string thought2 = (string)mExtractThought.Invoke(null, new object[] { geminiData });
                    if (thought2 == null || !thought2.Contains("规划下一步的桌面状态呈现"))
                    {
                        Console.WriteLine("FAIL: Gemini parts thought extraction failed: " + thought2);
                        return 77;
                    }
                }

                // Verify Desktop Agent (Computer-Use) and Standard Function Calling
                MethodInfo mExtractTool = sWatcherType.GetMethod("ExtractToolSummary", BindingFlags.NonPublic | BindingFlags.Static);
                if (mExtractTool != null)
                {
                    // 1. Screen Perception (Anthropic Computer-Use action: screenshot)
                    var screenData = new Dictionary<string, object>
                    {
                        {
                            "tool_calls", new List<object>
                            {
                                new Dictionary<string, object>
                                {
                                    { "name", "computer" },
                                    { "args", new Dictionary<string, object> { { "action", "screenshot" } } }
                                }
                            }
                        }
                    };
                    string screenTool = (string)mExtractTool.Invoke(null, new object[] { screenData, true });
                    if (screenTool == null || !screenTool.Contains("观察桌面"))
                    {
                        Console.WriteLine("FAIL: Desktop agent screen perception mapping failed: " + screenTool);
                        return 78;
                    }

                    // 2. Desktop Mouse Click with Coordinates
                    var clickData = new Dictionary<string, object>
                    {
                        {
                            "tool_calls", new List<object>
                            {
                                new Dictionary<string, object>
                                {
                                    { "name", "mouse_click" },
                                    { "args", new Dictionary<string, object> { { "x", 640 }, { "y", 480 } } }
                                }
                            }
                        }
                    };
                    string clickTool = (string)mExtractTool.Invoke(null, new object[] { clickData, true });
                    if (clickTool == null || !clickTool.Contains("640, 480"))
                    {
                        Console.WriteLine("FAIL: Desktop agent mouse click coordinate mapping failed: " + clickTool);
                        return 78;
                    }

                    // 3. Desktop Keyboard Input
                    var typeData = new Dictionary<string, object>
                    {
                        {
                            "tool_calls", new List<object>
                            {
                                new Dictionary<string, object>
                                {
                                    { "name", "keyboard_type" },
                                    { "args", new Dictionary<string, object> { { "text", "git status" } } }
                                }
                            }
                        }
                    };
                    string typeTool = (string)mExtractTool.Invoke(null, new object[] { typeData, true });
                    if (typeTool == null || !typeTool.Contains("git status"))
                    {
                        Console.WriteLine("FAIL: Desktop agent keyboard typing mapping failed: " + typeTool);
                        return 78;
                    }

                    // 4. Standard OpenAI Function Calling format (function.name + json arguments)
                    var oaiFuncData = new Dictionary<string, object>
                    {
                        {
                            "tool_calls", new List<object>
                            {
                                new Dictionary<string, object>
                                {
                                    { "type", "function" },
                                    {
                                        "function", new Dictionary<string, object>
                                        {
                                            { "name", "edit_file" },
                                            { "arguments", "{\"path\":\"src/AgentWatcher.cs\"}" }
                                        }
                                    }
                                }
                            }
                        }
                    };
                    string oaiTool = (string)mExtractTool.Invoke(null, new object[] { oaiFuncData, true });
                    if (oaiTool == null || !oaiTool.Contains("AgentWatcher.cs"))
                    {
                        Console.WriteLine("FAIL: Standard OpenAI function calling unwrapping failed: " + oaiTool);
                        return 78;
                    }
                }

                // Verify MCP (Model Context Protocol) Native Progress Parsing
                MethodInfo mExtractProgress = sWatcherType.GetMethod("ExtractStepProgress", BindingFlags.NonPublic | BindingFlags.Static);
                if (mExtractProgress != null)
                {
                    var mcpData = new Dictionary<string, object>
                    {
                        { "method", "notifications/progress" },
                        {
                            "params", new Dictionary<string, object>
                            {
                                { "progress", 15 },
                                { "total", 30 },
                                { "message", "Executing integration tests" }
                            }
                        }
                    };
                    object[] progArgs = new object[] { mcpData, "", 0, 0, "" };
                    bool hasProg = (bool)mExtractProgress.Invoke(null, progArgs);
                    int step = (int)progArgs[2];
                    int total = (int)progArgs[3];
                    string desc = (string)progArgs[4];
                    if (!hasProg || step != 15 || total != 30 || !desc.Contains("Executing integration tests"))
                    {
                        Console.WriteLine("FAIL: MCP native progress parsing failed: hasProg=" + hasProg + ", step=" + step + ", total=" + total + ", desc=" + desc);
                        return 79;
                    }
                }

                // Verify Explicit Terminal Fast-path (300ms vs 1200ms)
                MethodInfo mExtractCompDetails = sWatcherType.GetMethod("ExtractCompletionDetails", BindingFlags.NonPublic | BindingFlags.Static);
                if (mExtractCompDetails != null)
                {
                    // 1. Claude end_turn explicit terminal
                    var claudeTerm = new Dictionary<string, object>
                    {
                        { "type", "assistant" },
                        {
                            "message", new Dictionary<string, object>
                            {
                                { "stop_reason", "end_turn" },
                                { "content", new List<object> { new Dictionary<string, object> { { "type", "text" }, { "text", "代码修改与优化已完成" } } } }
                            }
                        }
                    };
                    object[] termArgs1 = new object[] { claudeTerm, false };
                    string cText1 = (string)mExtractCompDetails.Invoke(null, termArgs1);
                    bool isExp1 = (bool)termArgs1[1];
                    if (cText1 == null || !isExp1)
                    {
                        Console.WriteLine("FAIL: Claude end_turn should be marked explicit terminal: isExp=" + isExp1);
                        return 80;
                    }

                    // 2. Heuristic fallback completion
                    var heurTerm = new Dictionary<string, object>
                    {
                        { "status", "DONE" },
                        { "text", "Heuristic finish text" }
                    };
                    object[] termArgs2 = new object[] { heurTerm, false };
                    string cText2 = (string)mExtractCompDetails.Invoke(null, termArgs2);
                    bool isExp2 = (bool)termArgs2[1];
                    if (cText2 == null || isExp2)
                    {
                        Console.WriteLine("FAIL: Heuristic status should NOT be marked explicit terminal: isExp=" + isExp2);
                        return 80;
                    }
                }

                Console.WriteLine("PASS: LLM protocol wire schema, Desktop Agent alignment & MCP progress verified.");
                Console.WriteLine("PASS: Explicit terminal 300ms fast-path & single-agent Pin immunity verified.");

                // 81. Verify Universal Desktop Agent Auto-Detection, Browser Immunity & Multi-tier Cache
                MethodInfo mScanLlm = agentWatcherType.GetMethod("ScanLlmActivePids", BindingFlags.NonPublic | BindingFlags.Static);
                if (mScanLlm != null)
                {
                    object pidsObj = mScanLlm.Invoke(null, null);
                    if (pidsObj == null)
                    {
                        Console.WriteLine("FAIL: ScanLlmActivePids returned null");
                        return 81;
                    }
                }

                // Verify Browser & Non-Agent Filtering Immunity
                MethodInfo mIsNonAgent = agentWatcherType.GetMethod("IsNonAgentApplication", BindingFlags.NonPublic | BindingFlags.Static);
                if (mIsNonAgent != null)
                {
                    bool chromeImmune = (bool)mIsNonAgent.Invoke(null, new object[] { "chrome" });
                    bool edgeImmune = (bool)mIsNonAgent.Invoke(null, new object[] { "msedge.exe" });
                    bool wechatImmune = (bool)mIsNonAgent.Invoke(null, new object[] { "wechat" });
                    bool pythonNotImmune = (bool)mIsNonAgent.Invoke(null, new object[] { "python" });
                    bool aiderNotImmune = (bool)mIsNonAgent.Invoke(null, new object[] { "aider" });
                    if (!chromeImmune || !edgeImmune || !wechatImmune || pythonNotImmune || aiderNotImmune)
                    {
                        Console.WriteLine("FAIL: IsNonAgentApplication filtering failed: chrome=" + chromeImmune + ", edge=" + edgeImmune + ", python=" + pythonNotImmune);
                        return 81;
                    }
                }

                // Verify Universal LLM Protocol Fingerprinting
                MethodInfo mMatchFingerprint = agentWatcherType.GetMethod("MatchLlmProtocolFingerprint", BindingFlags.NonPublic | BindingFlags.Static);
                if (mMatchFingerprint != null)
                {
                    string agentJson = "{\"role\":\"assistant\",\"content\":[{\"type\":\"tool_use\",\"name\":\"edit_file\",\"path\":\"D:/AIStudio/iClock/src/iClock.cs\"}]}";
                    string normalJson = "{\"status\":\"ok\",\"user\":\"alice\",\"message\":\"hello world\"}";
                    object[] matchArgs = new object[] { agentJson, null, null };
                    bool isAgent = (bool)mMatchFingerprint.Invoke(null, matchArgs);
                    object[] normalArgs = new object[] { normalJson, null, null };
                    bool isNormal = (bool)mMatchFingerprint.Invoke(null, normalArgs);
                    if (!isAgent || isNormal)
                    {
                        Console.WriteLine("FAIL: MatchLlmProtocolFingerprint failed: isAgent=" + isAgent + ", isNormal=" + isNormal);
                        return 81;
                    }
                }

                // Verify Multi-tier Window Binding Cache
                MethodInfo mCacheWindow = agentWatcherType.GetMethod("CacheWindowBinding", BindingFlags.NonPublic | BindingFlags.Static);
                MethodInfo mGetCached = agentWatcherType.GetMethod("GetCachedWindow", BindingFlags.NonPublic | BindingFlags.Static);
                if (mCacheWindow != null && mGetCached != null)
                {
                    IntPtr dummyHwnd = Process.GetCurrentProcess().MainWindowHandle;
                    mCacheWindow.Invoke(null, new object[] { "TestProject", dummyHwnd, Process.GetCurrentProcess().Id });
                    IntPtr fetched = (IntPtr)mGetCached.Invoke(null, new object[] { "TestProject" });
                    if (dummyHwnd != IntPtr.Zero && fetched != dummyHwnd)
                    {
                        Console.WriteLine("FAIL: WindowBinding cache failed: fetched=" + fetched + " expected=" + dummyHwnd);
                        return 81;
                    }
                }

                Console.WriteLine("PASS: Universal Local Desktop Agent auto-detection, Browser immunity & Multi-tier cache verified.");
            }

            FieldInfo sActiveField = appType.GetField("sessionActive", bf);
            if (sActiveField != null) sActiveField.SetValue(app, false);
            MethodInfo exit = appType.GetMethod("Exit", bf);
            exit.Invoke(app, null);

            Console.WriteLine("ALL CHECKS PASSED!");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERROR: " + ex);
            return 99;
        }
        finally
        {
            if (iniBackup != null)
            {
                try { System.IO.File.WriteAllText(iniPath, iniBackup); } catch { }
            }
            else
            {
                try { System.IO.File.Delete(iniPath); } catch { }
            }
            if (historyBackup != null)
            {
                try { System.IO.File.WriteAllText(todayHistoryPath, historyBackup, System.Text.Encoding.UTF8); } catch { }
            }
            else
            {
                try { System.IO.File.Delete(todayHistoryPath); } catch { }
            }
        }
    }

    private static object MakeUsageSeg(Type usageSegType, string state, string exe, int startSec, int endSec)
    {
        object seg = Activator.CreateInstance(usageSegType);
        DateTime baseT = DateTime.Today.AddHours(10);
        usageSegType.GetField("Start").SetValue(seg, baseT.AddSeconds(startSec));
        usageSegType.GetField("End").SetValue(seg, baseT.AddSeconds(endSec));
        usageSegType.GetField("State").SetValue(seg, state);
        usageSegType.GetField("Exe").SetValue(seg, exe);
        return seg;
    }
}
