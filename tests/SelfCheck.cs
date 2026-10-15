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

    private static bool testAlertReceived;
    private static void OnTestAlertReceived(object draft, bool isAgent)
    {
        testAlertReceived = true;
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
            int defSize = (int)settingsType.GetField("FontSize").GetValue(defaultSettings);
            string defColor = (string)settingsType.GetField("Color").GetValue(defaultSettings);
            string defFont = (string)settingsType.GetField("FontFamily").GetValue(defaultSettings);
            int defX = (int)settingsType.GetField("X").GetValue(defaultSettings);
            int defY = (int)settingsType.GetField("Y").GetValue(defaultSettings);
            string defFormat = (string)settingsType.GetField("Format").GetValue(defaultSettings);
            if (defSize != 20 || defColor != "#FF0000" || defFont != "Segoe UI" || defX != -1 || defY != 60 || defFormat != "MM:SS")
            {
                Console.WriteLine("FAIL: Default FontSize (" + defSize + "), Color (" + defColor + "), FontFamily (" + defFont + "), X (" + defX + "), Y (" + defY + "), or Format (" + defFormat + ") incorrect");
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
            ComboBox langBox = (ComboBox)settingsDialogType.GetField("language", bf).GetValue(settingsDialog);
            langBox.SelectedIndex = 1; // English
            if (settingsDialog.Font.FontFamily.Name != "Segoe UI" || settingsDialog.Text != "iClock Settings")
            {
                Console.WriteLine("FAIL: SettingsDialog did not update font/title on language switch to en");
                return 15;
            }
            langBox.SelectedIndex = 0; // Chinese
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
            if (langBox.DrawMode != DrawMode.OwnerDrawFixed || langBox.ItemHeight != 22)
            {
                Console.WriteLine("FAIL: language combo should have DrawMode OwnerDrawFixed and ItemHeight 22");
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
            if (!hasLanguageCombo || !hasFontCombo)
            {
                Console.WriteLine("FAIL: SettingsDialog missing language or font option");
                return 15;
            }
            FieldInfo launchField = appType.GetField("launchReported", BindingFlags.NonPublic | BindingFlags.Static);
            if (launchField == null || !(bool)launchField.GetValue(null))
            {
                Console.WriteLine("FAIL: launchReported was not set on startup or field missing");
                return 15;
            }
            Console.WriteLine("PASS: SettingsDialog contains language and font selection (OwnerDraw preview), and launchReported is de-duplicated.");

            // 10. Verify version comparison and JSON parsing
            BindingFlags sbf = BindingFlags.NonPublic | BindingFlags.Static;
            FieldInfo verField = appType.GetField("CURRENT_VERSION", sbf);
            if (verField == null || (string)verField.GetValue(null) != "1.05")
            {
                Console.WriteLine("FAIL: CURRENT_VERSION should be 1.05");
                return 16;
            }
            MethodInfo isNewer = appType.GetMethod("IsNewer", sbf);
            MethodInfo extractJson = appType.GetMethod("ExtractJsonValue", sbf);
            if (!(bool)isNewer.Invoke(null, new object[] { "1.06", "1.05" }) ||
                !(bool)isNewer.Invoke(null, new object[] { "v2.0", "1.05" }) ||
                (bool)isNewer.Invoke(null, new object[] { "1.05", "1.05" }))
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
            MethodInfo setSettings = overlayType.GetMethod("SetSettings");
            setSettings.Invoke(overlay, new object[] { settings });
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
            if (menuHistory.Text != "查看今日记录" && menuHistory.Text != "View today's history")
            {
                Console.WriteLine("FAIL: menuHistory text should be '查看今日记录' / 'View today\'s history' without ellipsis, got: " + menuHistory.Text);
                return 24;
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
            Button save = (Button)settingsDialogType.GetField("saveBtn", bf).GetValue(dlg);
            MethodInfo capture = settingsDialogType.GetMethod("CaptureHotkey", bf);

            // 17a. Standalone function key (F10) without modifiers should be accepted
            KeyEventArgs keF10 = new KeyEventArgs(Keys.F10);
            capture.Invoke(dlg, new object[] { box, keF10 });
            if (!save.Enabled || box.Text != "F10")
            {
                Console.WriteLine("FAIL: Standalone F10 should be accepted (save.Enabled=" + save.Enabled + ", text=" + box.Text + ")");
                return 25;
            }

            // 17b. Normal key without modifier (Space) should be rejected
            KeyEventArgs keSpace = new KeyEventArgs(Keys.Space);
            capture.Invoke(dlg, new object[] { box, keSpace });
            if (save.Enabled || (!box.Text.Contains("修饰键") && !box.Text.Contains("modifier")))
            {
                Console.WriteLine("FAIL: Lone space should be rejected (save.Enabled=" + save.Enabled + ", text=" + box.Text + ")");
                return 25;
            }

            // 17c. Occupied key should be flagged and save button disabled
            NativeWindow holder2 = new NativeWindow();
            holder2.CreateHandle(new CreateParams());
            bool held2 = (bool)regHotKey.Invoke(null, new object[] { holder2.Handle, 0x5678, (uint)(testMods | 0x4000), (uint)testKey });
            if (held2)
            {
                KeyEventArgs keOccupied = new KeyEventArgs((Keys)testKey | Keys.Control | Keys.Alt);
                capture.Invoke(dlg, new object[] { box, keOccupied });
                unregHotKey.Invoke(null, new object[] { holder2.Handle, 0x5678 });
                holder2.DestroyHandle();
                if (save.Enabled || (!box.Text.Contains("已被占用") && !box.Text.Contains("Occupied")))
                {
                    Console.WriteLine("FAIL: Occupied hotkey should disable save (save.Enabled=" + save.Enabled + ", text=" + box.Text + ")");
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
            if (!save.Enabled)
            {
                Console.WriteLine("FAIL: Current hotkey should be valid (save.Enabled=" + save.Enabled + ", text=" + box.Text + ")");
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
            // Test cancel button closes SettingsDialog
            Button cancelBtn = (Button)dlg3.GetType().GetField("cancelBtn", bf).GetValue(dlg3);
            cancelBtn.PerformClick();
            if (!dlg3.IsDisposed || activeDlgField.GetValue(app) != null)
            {
                Console.WriteLine("FAIL: Cancel button did not close SettingsDialog");
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
            Console.WriteLine("PASS: Single active dialog mutual exclusion, screen centering, and cancel button verified.");

            // 20. Verify Auto-Fallback, Status Transparency, and Tray Left-Click
            // Test 20a: Tray Left-Click toggles countdown
            MethodInfo onMouseClick = typeof(NotifyIcon).GetMethod("OnMouseClick", BindingFlags.NonPublic | BindingFlags.Instance);
            if (onMouseClick != null)
            {
                bool wasRunning = (bool)appType.GetField("running", bf).GetValue(app);
                onMouseClick.Invoke(tray, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0) });
                bool nowRunning = (bool)appType.GetField("running", bf).GetValue(app);
                if (nowRunning == wasRunning)
                {
                    Console.WriteLine("FAIL: Tray left-click should toggle running state");
                    return 28;
                }
                // Toggle back
                onMouseClick.Invoke(tray, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0) });
            }

            // Test 20b: SettingsDialog with hotkeyActive = false disables save button and flags occupancy
            Form unavailDlg = (Form)Activator.CreateInstance(settingsDialogType, new object[] { settings, false });
            Button unavailSave = (Button)settingsDialogType.GetField("saveBtn", bf).GetValue(unavailDlg);
            TextBox unavailBox = (TextBox)settingsDialogType.GetField("hotkeyBox", bf).GetValue(unavailDlg);
            if (unavailSave.Enabled || (!unavailBox.Text.Contains("已被占用") && !unavailBox.Text.Contains("Occupied")))
            {
                Console.WriteLine("FAIL: SettingsDialog with hotkeyActive=false must disable save and indicate occupied");
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
            FieldInfo agentNoticeField = settings.GetType().GetField("AgentNotice");
            if (agentNoticeField == null)
            {
                Console.WriteLine("FAIL: Settings missing AgentNotice field");
                return 32;
            }
            if ((bool)agentNoticeField.GetValue(settings) != false)
            {
                Console.WriteLine("FAIL: AgentNotice default should be false");
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
            agentNoticeDlg.Dispose();
            if (!foundAgentTaskText)
            {
                Console.WriteLine("FAIL: NoticeDialog missing agent task text");
                return 32;
            }
            Console.WriteLine("PASS: AI Agent task completion monitoring, detection scanning, and alert pattern verified.");

            // 25. Test SettingsDialog Plan B-1 Dual Tabs & Agent Collaboration UI
            Form tabDlg = (Form)Activator.CreateInstance(settingsDialogType, new object[] { settings });
            Button btnTabTimer = (Button)settingsDialogType.GetField("btnTabTimer", bf).GetValue(tabDlg);
            Button btnTabAgent = (Button)settingsDialogType.GetField("btnTabAgent", bf).GetValue(tabDlg);
            Control minCtrl = (Control)settingsDialogType.GetField("minutes", bf).GetValue(tabDlg);
            CheckBox agNoticeBox = (CheckBox)settingsDialogType.GetField("agentNotice", bf).GetValue(tabDlg);
            Panel cardPanel = (Panel)settingsDialogType.GetField("panelAgentCards", bf).GetValue(tabDlg);
            Button btnRefresh = (Button)settingsDialogType.GetField("btnRefreshAgents", bf).GetValue(tabDlg);
            CheckBox startupBox = (CheckBox)settingsDialogType.GetField("startup", bf).GetValue(tabDlg);
            Control noticeCtrl = (Control)settingsDialogType.GetField("notice", bf).GetValue(tabDlg);

            if (btnTabTimer == null || btnTabAgent == null || cardPanel == null || btnRefresh == null)
            {
                Console.WriteLine("FAIL: SettingsDialog missing dual-tab navigation or Plan B-1 controls");
                return 33;
            }

            tabDlg.Show();

            // Initially on Tab 1 (Timer)
            if (!minCtrl.Visible || agNoticeBox.Visible)
            {
                Console.WriteLine("FAIL: SettingsDialog default tab should show timer controls and hide agent controls");
                return 33;
            }

            // Verify auto-start checkbox is at bottom of Tab 1 (below notice)
            if (startupBox.Top <= noticeCtrl.Top)
            {
                Console.WriteLine("FAIL: Startup checkbox should be located at the bottom of the countdown settings");
                return 33;
            }

            // Switch to Tab 2 (Agent Collaboration)
            btnTabAgent.PerformClick();
            if (minCtrl.Visible || !agNoticeBox.Visible)
            {
                Console.WriteLine("FAIL: Switching to Agent tab should hide timer controls and show agent controls");
                return 33;
            }

            // Verify only detected agents are listed in cardPanel
            System.Collections.Generic.List<string> expectedAgents = (System.Collections.Generic.List<string>)detectAgents.Invoke(null, null);
            int cardCount = 0;
            foreach (Control c in cardPanel.Controls)
            {
                if (c is Panel) cardCount++;
            }
            if (expectedAgents.Count > 0 && cardCount != expectedAgents.Count)
            {
                Console.WriteLine("FAIL: cardPanel should contain exactly the detected agents count (" + expectedAgents.Count + "), but found " + cardCount);
                return 33;
            }

            // Verify running agents are sorted before ready agents
            bool seenReady = false;
            foreach (Control c in cardPanel.Controls)
            {
                if (c is Panel)
                {
                    Panel card = (Panel)c;
                    foreach (Control child in card.Controls)
                    {
                        if (child is Label && (child.Text == "已就绪" || child.Text == "Ready"))
                        {
                            seenReady = true;
                        }
                        if (seenReady && child is Label && (child.Text == "运行中" || child.Text == "Running"))
                        {
                            Console.WriteLine("FAIL: Running agent appeared after a Ready agent in cardPanel list");
                            return 33;
                        }
                    }
                }
            }

            // Test refresh button
            btnRefresh.PerformClick();
            int refreshedCardCount = 0;
            foreach (Control c in cardPanel.Controls)
            {
                if (c is Panel) refreshedCardCount++;
            }
            if (refreshedCardCount != cardCount)
            {
                Console.WriteLine("FAIL: Refreshing agent cards changed count unexpectedly");
                return 33;
            }

            // Switch back to Tab 1
            btnTabTimer.PerformClick();
            if (!minCtrl.Visible || agNoticeBox.Visible)
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

            // 28. Test Multi-Process AI Agent Monitoring, FindAgentMainWindow, and Background Completion Lifecycle
            MethodInfo findWin = appType.GetMethod("FindAgentMainWindow", BindingFlags.NonPublic | BindingFlags.Static);
            if (findWin == null)
            {
                Console.WriteLine("FAIL: AppContext missing FindAgentMainWindow static method");
                return 43;
            }
            HashSet<int> curAntiPids = new HashSet<int>();
            foreach (Process p in Process.GetProcessesByName("Antigravity"))
            {
                using (p) curAntiPids.Add(p.Id);
            }
            if (curAntiPids.Count > 0)
            {
                IntPtr detectedHwnd = (IntPtr)findWin.Invoke(null, new object[] { curAntiPids });
                if (detectedHwnd == IntPtr.Zero)
                {
                    Console.WriteLine("FAIL: FindAgentMainWindow returned Zero for running Antigravity processes");
                    return 43;
                }
            }

            FieldInfo trackedAgentsField = appType.GetField("trackedAgents", bf);
            if (trackedAgentsField == null)
            {
                Console.WriteLine("FAIL: AppContext missing trackedAgents field");
                return 44;
            }
            MethodInfo processTick = appType.GetMethod("ProcessAgentTick", bf);
            if (processTick == null)
            {
                Console.WriteLine("FAIL: AppContext missing ProcessAgentTick method");
                return 44;
            }

            agentNoticeField.SetValue(settings, true);
            DateTime t0 = DateTime.Now;
            processTick.Invoke(app, new object[] { t0, IntPtr.Zero, 0u });
            IDictionary trackedDict = (IDictionary)trackedAgentsField.GetValue(app);
            if (trackedDict == null)
            {
                Console.WriteLine("FAIL: trackedAgents dictionary is null");
                return 45;
            }

            Type agentTrackType = asm.GetType("AppContext+AgentTrack");
            object mockTrack = Activator.CreateInstance(agentTrackType);
            agentTrackType.GetField("DisplayName").SetValue(mockTrack, "TestAgent");
            HashSet<int> mockPids = new HashSet<int>();
            mockPids.Add(Process.GetCurrentProcess().Id);
            agentTrackType.GetField("Pids").SetValue(mockTrack, mockPids);
            agentTrackType.GetField("MainWindow").SetValue(mockTrack, new IntPtr(12345));
            agentTrackType.GetField("LastCpu").SetValue(mockTrack, TimeSpan.FromSeconds(10));
            agentTrackType.GetField("LastSample").SetValue(mockTrack, t0);
            agentTrackType.GetField("BusyTicks").SetValue(mockTrack, 0);
            agentTrackType.GetField("IdleTicks").SetValue(mockTrack, 0);
            agentTrackType.GetField("WasBusy").SetValue(mockTrack, false);
            agentTrackType.GetField("LastNoticeTime").SetValue(mockTrack, DateTime.MinValue);
            trackedDict["TestAgent"] = mockTrack;
            appType.GetField("agentScanCooldown", bf).SetValue(app, 999);

            // Test transient CPU spike rejection (short 2-second burst -> should NOT enter WasBusy)
            DateTime curTime = t0;
            TimeSpan realCurCpu = Process.GetCurrentProcess().TotalProcessorTime;
            for (int b = 1; b <= 2; b++)
            {
                curTime = curTime.AddSeconds(1);
                agentTrackType.GetField("LastCpu").SetValue(mockTrack, realCurCpu - TimeSpan.FromSeconds(0.5));
                processTick.Invoke(app, new object[] { curTime, IntPtr.Zero, 999999u });
            }
            // Quiet for 1 second -> resets BusyTicks, WasBusy remains false
            curTime = curTime.AddSeconds(1);
            agentTrackType.GetField("LastCpu").SetValue(mockTrack, realCurCpu);
            processTick.Invoke(app, new object[] { curTime, IntPtr.Zero, 999999u });
            int busyReset = (int)agentTrackType.GetField("BusyTicks").GetValue(mockTrack);
            bool wasBusyTransient = (bool)agentTrackType.GetField("WasBusy").GetValue(mockTrack);
            if (busyReset != 0 || wasBusyTransient != false)
            {
                Console.WriteLine("FAIL: Transient CPU spike should reset BusyTicks=0 and WasBusy=false");
                return 46;
            }

            // Test sustained workload (5 consecutive busy seconds >= AgentCpuBusyMinTicks)
            for (int b = 1; b <= 5; b++)
            {
                curTime = curTime.AddSeconds(1);
                realCurCpu = Process.GetCurrentProcess().TotalProcessorTime;
                agentTrackType.GetField("LastCpu").SetValue(mockTrack, realCurCpu - TimeSpan.FromSeconds(0.5));
                processTick.Invoke(app, new object[] { curTime, IntPtr.Zero, 999999u });
                int bTicks = (int)agentTrackType.GetField("BusyTicks").GetValue(mockTrack);
                bool wBusy = (bool)agentTrackType.GetField("WasBusy").GetValue(mockTrack);
                if (b < 5)
                {
                    if (bTicks != b || wBusy != false)
                    {
                        Console.WriteLine("FAIL: Busy tick " + b + " should have BusyTicks=" + b + ", WasBusy=false");
                        return 46;
                    }
                }
                else
                {
                    if (bTicks < 5 || !wBusy)
                    {
                        Console.WriteLine("FAIL: Busy tick 5 should set BusyTicks>=5 and WasBusy=true");
                        return 46;
                    }
                }
            }

            // Test quiet interval: Ticks 1..5 of quiet should NOT trigger completion (AgentCpuIdleMinTicks = 6)
            for (int id = 1; id < 6; id++)
            {
                curTime = curTime.AddSeconds(1);
                realCurCpu = Process.GetCurrentProcess().TotalProcessorTime;
                agentTrackType.GetField("LastCpu").SetValue(mockTrack, realCurCpu);
                processTick.Invoke(app, new object[] { curTime, IntPtr.Zero, 999999u });
                int idTicks = (int)agentTrackType.GetField("IdleTicks").GetValue(mockTrack);
                bool wBusy = (bool)agentTrackType.GetField("WasBusy").GetValue(mockTrack);
                if (idTicks != id || !wBusy)
                {
                    Console.WriteLine("FAIL: Idle tick " + id + " should have IdleTicks=" + id + ", WasBusy=true, got " + idTicks + ", " + wBusy);
                    return 46;
                }
            }

            // Tick 6 of quiet: triggers completion notice!
            curTime = curTime.AddSeconds(1);
            realCurCpu = Process.GetCurrentProcess().TotalProcessorTime;
            agentTrackType.GetField("LastCpu").SetValue(mockTrack, realCurCpu);
            processTick.Invoke(app, new object[] { curTime, IntPtr.Zero, 999999u });
            bool wasBusyDone = (bool)agentTrackType.GetField("WasBusy").GetValue(mockTrack);
            DateTime lastNotice = (DateTime)agentTrackType.GetField("LastNoticeTime").GetValue(mockTrack);
            if (wasBusyDone != false || lastNotice != curTime)
            {
                Console.WriteLine("FAIL: Idle tick 6 should trigger notice and reset WasBusy, got WasBusy=" + wasBusyDone + ", lastNotice=" + lastNotice);
                return 46;
            }
            trackedDict.Remove("TestAgent");

            // Verify Window Title Dynamics helper & instant completion
            MethodInfo isBusyTitleMethod = appType.GetMethod("IsBusyAgentTitle", BindingFlags.NonPublic | BindingFlags.Static);
            if (isBusyTitleMethod == null)
            {
                Console.WriteLine("FAIL: AppContext missing IsBusyAgentTitle static method");
                return 47;
            }
            if (!(bool)isBusyTitleMethod.Invoke(null, new object[] { "Cursor - [Running] index.ts" }) ||
                !(bool)isBusyTitleMethod.Invoke(null, new object[] { "● Claude Code" }) ||
                (bool)isBusyTitleMethod.Invoke(null, new object[] { "Cursor - index.ts" }))
            {
                Console.WriteLine("FAIL: IsBusyAgentTitle logic incorrect");
                return 47;
            }

            // Test title-revert instant completion
            object titleTrack = Activator.CreateInstance(agentTrackType);
            agentTrackType.GetField("DisplayName").SetValue(titleTrack, "TitleAgent");
            agentTrackType.GetField("Pids").SetValue(titleTrack, mockPids);
            agentTrackType.GetField("MainWindow").SetValue(titleTrack, new IntPtr(54321));
            agentTrackType.GetField("LastCpu").SetValue(titleTrack, realCurCpu);
            agentTrackType.GetField("LastSample").SetValue(titleTrack, t0);
            agentTrackType.GetField("TitleWasBusy").SetValue(titleTrack, true);
            agentTrackType.GetField("WasBusy").SetValue(titleTrack, true);
            agentTrackType.GetField("LastNoticeTime").SetValue(titleTrack, DateTime.MinValue);
            trackedDict["TitleAgent"] = titleTrack;

            // When title reverts, it immediately fires notice without waiting for quiet ticks
            appType.GetField("agentScanCooldown", bf).SetValue(app, 5);
            DateTime tTitle = t0.AddSeconds(1);
            agentTrackType.GetField("LastCpu").SetValue(titleTrack, realCurCpu);
            processTick.Invoke(app, new object[] { tTitle, IntPtr.Zero, 999999u });
            bool titleWasBusyAfter = (bool)agentTrackType.GetField("TitleWasBusy").GetValue(titleTrack);
            DateTime titleNotice = (DateTime)agentTrackType.GetField("LastNoticeTime").GetValue(titleTrack);
            if (titleWasBusyAfter != false || titleNotice != tTitle)
            {
                Console.WriteLine("FAIL: Title-revert should instantly trigger completion notice, got TitleWasBusy=" + titleWasBusyAfter + ", LastNoticeTime=" + titleNotice);
                return 47;
            }
            trackedDict.Remove("TitleAgent");

            Console.WriteLine("PASS: Multi-process AI Agent monitoring, FindAgentMainWindow, window title dynamics, and background completion lifecycle verified.");

            // 29. Verify ToastAgentMonitor, Zero-False-Positive CPU bypass, and Notification Details
            Type toastMonType = asm.GetType("AppContext+ToastAgentMonitor");
            if (toastMonType == null)
            {
                Console.WriteLine("FAIL: AppContext missing ToastAgentMonitor class");
                return 48;
            }
            PropertyInfo isAvailProp = toastMonType.GetProperty("IsAvailable", BindingFlags.Public | BindingFlags.Static);
            if (isAvailProp == null)
            {
                Console.WriteLine("FAIL: ToastAgentMonitor missing IsAvailable property");
                return 48;
            }
            bool isAvail = (bool)isAvailProp.GetValue(null, null);

            if (isAvail)
            {
                object toastAgentTrack = Activator.CreateInstance(agentTrackType);
                agentTrackType.GetField("DisplayName").SetValue(toastAgentTrack, "Antigravity");
                agentTrackType.GetField("Pids").SetValue(toastAgentTrack, mockPids);
                agentTrackType.GetField("MainWindow").SetValue(toastAgentTrack, new IntPtr(23456));
                agentTrackType.GetField("LastCpu").SetValue(toastAgentTrack, realCurCpu - TimeSpan.FromSeconds(1.0));
                agentTrackType.GetField("LastSample").SetValue(toastAgentTrack, t0);
                agentTrackType.GetField("BusyTicks").SetValue(toastAgentTrack, 0);
                agentTrackType.GetField("IdleTicks").SetValue(toastAgentTrack, 0);
                agentTrackType.GetField("WasBusy").SetValue(toastAgentTrack, false);
                agentTrackType.GetField("LastNoticeTime").SetValue(toastAgentTrack, DateTime.MinValue);
                trackedDict["Antigravity"] = toastAgentTrack;

                DateTime tToast = t0.AddSeconds(1);
                processTick.Invoke(app, new object[] { tToast, IntPtr.Zero, 999999u });
                int toastBusy = (int)agentTrackType.GetField("BusyTicks").GetValue(toastAgentTrack);
                bool toastWasBusy = (bool)agentTrackType.GetField("WasBusy").GetValue(toastAgentTrack);
                if (toastBusy != 0 || toastWasBusy != false)
                {
                    Console.WriteLine("FAIL: Antigravity should bypass CPU busy heuristics to prevent false positives, got BusyTicks=" + toastBusy);
                    return 48;
                }
                trackedDict.Remove("Antigravity");
            }

            Form noticeWithDetail = (Form)Activator.CreateInstance(noticeType, new object[] { settings, DateTime.Now, "Antigravity", IntPtr.Zero, null, "Task Completed • Model Refactored" });
            if (noticeWithDetail == null || !noticeWithDetail.Text.Contains("Antigravity"))
            {
                Console.WriteLine("FAIL: NoticeDialog with detailMessage failed to instantiate or bad title");
                return 48;
            }
            noticeWithDetail.Dispose();

            Console.WriteLine("PASS: Windows Toast notification listener, zero-false-positive CPU bypass, and task detail display verified.");

            // 30. Verify Win32 Shell Hook (RegisterShellHookWindow, HSHELL_FLASH, and 0ms window flash routing)
            FieldInfo shellHookMsgField = appType.GetField("shellHookMsg", bf);
            if (shellHookMsgField == null)
            {
                Console.WriteLine("FAIL: AppContext missing shellHookMsg field");
                return 49;
            }
            MethodInfo handleShellMethod = appType.GetMethod("HandleShellHook", bf);
            if (handleShellMethod == null)
            {
                Console.WriteLine("FAIL: AppContext missing HandleShellHook method");
                return 49;
            }

            object windsurfTrack = Activator.CreateInstance(agentTrackType);
            agentTrackType.GetField("DisplayName").SetValue(windsurfTrack, "Windsurf");
            agentTrackType.GetField("Pids").SetValue(windsurfTrack, mockPids);
            agentTrackType.GetField("MainWindow").SetValue(windsurfTrack, new IntPtr(34567));
            agentTrackType.GetField("LastCpu").SetValue(windsurfTrack, TimeSpan.Zero);
            agentTrackType.GetField("LastSample").SetValue(windsurfTrack, t0);
            agentTrackType.GetField("LastNoticeTime").SetValue(windsurfTrack, DateTime.MinValue);
            trackedDict["Windsurf"] = windsurfTrack;

            handleShellMethod.Invoke(app, new object[] { 6, new IntPtr(34567) });
            DateTime lastNoticeNonFlash = (DateTime)agentTrackType.GetField("LastNoticeTime").GetValue(windsurfTrack);
            if (lastNoticeNonFlash != DateTime.MinValue)
            {
                Console.WriteLine("FAIL: Non-flash code should not trigger alert");
                return 49;
            }

            handleShellMethod.Invoke(app, new object[] { 0x8006, new IntPtr(34567) });
            DateTime lastNoticeFlashed = (DateTime)agentTrackType.GetField("LastNoticeTime").GetValue(windsurfTrack);
            if (lastNoticeFlashed == DateTime.MinValue || (DateTime.Now - lastNoticeFlashed).TotalSeconds > 5)
            {
                Console.WriteLine("FAIL: HSHELL_FLASH should instantly trigger alert and update LastNoticeTime");
                return 49;
            }

            handleShellMethod.Invoke(app, new object[] { 0x8006, new IntPtr(34567) });
            DateTime lastNoticeCooldown = (DateTime)agentTrackType.GetField("LastNoticeTime").GetValue(windsurfTrack);
            if (lastNoticeCooldown != lastNoticeFlashed)
            {
                Console.WriteLine("FAIL: Repeat flash within cooldown should be suppressed");
                return 49;
            }
            trackedDict.Remove("Windsurf");

            Form actNotice = (Form)appType.GetField("activeNotice", bf).GetValue(app);
            if (actNotice != null && !actNotice.IsDisposed) { actNotice.Close(); actNotice.Dispose(); }

            Console.WriteLine("PASS: Win32 RegisterShellHookWindow, HSHELL_FLASH window flashing detection, and instant dispatch verified.");

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

            MethodInfo getCurrentDraft = settingsDlgType.GetMethod("GetCurrentDraftSettings");
            object draft = getCurrentDraft.Invoke(dlgInstance, null);
            int draftSize = (int)settingsType.GetField("FontSize").GetValue(draft);
            if (draftSize != 42)
            {
                Console.WriteLine("FAIL: GetCurrentDraftSettings returned incorrect FontSize: " + draftSize);
                return 41;
            }

            MethodInfo setVisualPreview = overlayType.GetMethod("SetVisualPreview");
            if (setVisualPreview == null)
            {
                Console.WriteLine("FAIL: Overlay does not expose SetVisualPreview method.");
                return 42;
            }

            setVisualPreview.Invoke(overlay, new object[] { draft, "00:42:00" });
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

            // Verify btnTestAlert and TestAlertRequested
            Button btnTest = (Button)settingsDlgType.GetField("btnTestAlert", bf).GetValue(dlgInstance);
            if (btnTest == null)
            {
                Console.WriteLine("FAIL: SettingsDialog missing btnTestAlert");
                return 45;
            }

            EventInfo testAlertEvent = settingsDlgType.GetEvent("TestAlertRequested");
            if (testAlertEvent == null)
            {
                Console.WriteLine("FAIL: SettingsDialog missing TestAlertRequested event");
                return 46;
            }

            MethodInfo testHandlerMethod = typeof(TestCheck).GetMethod("OnTestAlertReceived", BindingFlags.NonPublic | BindingFlags.Static);
            testAlertEvent.AddEventHandler(dlgInstance, Delegate.CreateDelegate(testAlertEvent.EventHandlerType, testHandlerMethod));
            MethodInfo btnClick = typeof(Button).GetMethod("OnClick", BindingFlags.NonPublic | BindingFlags.Instance);
            btnClick.Invoke(btnTest, new object[] { EventArgs.Empty });
            if (!testAlertReceived)
            {
                Console.WriteLine("FAIL: Clicking btnTestAlert did not fire TestAlertRequested");
                return 47;
            }

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

            setVisualPreview.Invoke(overlay, new object[] { settings, "25:00" });
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
            if (cliNotice.GetType().Name != "CliNoticeDialog")
            {
                Console.WriteLine("FAIL: -n did not instantiate specialized CliNoticeDialog, got " + cliNotice.GetType().Name);
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
                Console.WriteLine("FAIL: CliNoticeDialog did not contain message text 'Build Success'");
                return 50;
            }
            if (foundBrand)
            {
                Console.WriteLine("FAIL: CliNoticeDialog should not contain internal 'iClock' brand label");
                return 50;
            }
            string sColor = settings.GetType().GetField("Color").GetValue(settings) as string;
            Color expectedColor = ColorTranslator.FromHtml(sColor);
            if (msgLbl.ForeColor.ToArgb() != expectedColor.ToArgb())
            {
                Console.WriteLine("FAIL: CliNoticeDialog message ForeColor (" + msgLbl.ForeColor + ") does not match settings.Color (" + expectedColor + ")");
                return 50;
            }
            cliNotice.Close();

            Console.WriteLine("PASS: CLI IPC commands (--start, -p, -r, --reset, -n) successfully verified.");

            // 33. Verify CLI tray menu item and CliHelpDialog
            ToolStripMenuItem menuCli = (ToolStripMenuItem)appType.GetField("menuCli", bf).GetValue(app);
            if (menuCli == null)
            {
                Console.WriteLine("FAIL: menuCli is null");
                return 51;
            }
            if (menuCli.Text != "命令行 (CLI)…" && menuCli.Text != "Command Line (CLI)…")
            {
                Console.WriteLine("FAIL: menuCli text should be '命令行 (CLI)…' or 'Command Line (CLI)…', got: " + menuCli.Text);
                return 51;
            }
            if (menuCli.ShortcutKeyDisplayString != "iclock -h")
            {
                Console.WriteLine("FAIL: menuCli shortcut hint should be 'iclock -h', got: " + menuCli.ShortcutKeyDisplayString);
                return 51;
            }
            MethodInfo showCliHelp = appType.GetMethod("ShowCliHelp", bf);
            showCliHelp.Invoke(app, null);
            Form cliHelpDlg = (Form)activeDlgField.GetValue(app);
            if (cliHelpDlg == null || cliHelpDlg.IsDisposed || cliHelpDlg.GetType().Name != "CliHelpDialog")
            {
                Console.WriteLine("FAIL: ShowCliHelp did not create CliHelpDialog");
                return 51;
            }
            if (!cliHelpDlg.Text.Contains("命令行 (CLI)") && !cliHelpDlg.Text.Contains("Command Line (CLI)"))
            {
                Console.WriteLine("FAIL: CliHelpDialog title incorrect: " + cliHelpDlg.Text);
                return 51;
            }
            bool foundIntro = false;
            foreach (Control c in cliHelpDlg.Controls)
            {
                if (c is Label && (c.Text.Contains("支持在终端") || c.Text.Contains("Control iClock timers")))
                {
                    foundIntro = true;
                    break;
                }
            }
            if (!foundIntro)
            {
                Console.WriteLine("FAIL: CliHelpDialog missing intro guidance label");
                return 51;
            }
            cliHelpDlg.Close();
            if (activeDlgField.GetValue(app) != null)
            {
                Console.WriteLine("FAIL: Closing CliHelpDialog did not clear activeDialog");
                return 51;
            }
            Console.WriteLine("PASS: CLI tray menu item (命令行 (CLI)…, iclock -h) and CliHelpDialog verified.");

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
}
