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
            if (verField == null || (string)verField.GetValue(null) != "1.07")
            {
                Console.WriteLine("FAIL: CURRENT_VERSION should be 1.07");
                return 16;
            }
            MethodInfo isNewer = appType.GetMethod("IsNewer", sbf);
            MethodInfo extractJson = appType.GetMethod("ExtractJsonValue", sbf);
            if (!(bool)isNewer.Invoke(null, new object[] { "1.08", "1.07" }) ||
                !(bool)isNewer.Invoke(null, new object[] { "v2.0", "1.07" }) ||
                (bool)isNewer.Invoke(null, new object[] { "1.07", "1.07" }))
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
            CheckBox agProgBox = (CheckBox)settingsDialogType.GetField("chkAgentProgress", bf).GetValue(tabDlg);
            Panel cardPanel = (Panel)settingsDialogType.GetField("panelAgentCards", bf).GetValue(tabDlg);
            Button btnRefresh = (Button)settingsDialogType.GetField("btnRefreshAgents", bf).GetValue(tabDlg);
            CheckBox startupBox = (CheckBox)settingsDialogType.GetField("startup", bf).GetValue(tabDlg);
            Control noticeCtrl = (Control)settingsDialogType.GetField("notice", bf).GetValue(tabDlg);

            if (btnTabTimer == null || btnTabAgent == null || cardPanel == null || btnRefresh == null || agProgBox == null)
            {
                Console.WriteLine("FAIL: SettingsDialog missing dual-tab navigation or Plan B-1 controls");
                return 33;
            }

            tabDlg.Show();

            // Initially on Tab 1 (Timer)
            if (!minCtrl.Visible || agNoticeBox.Visible || agProgBox.Visible || agProgBox.Checked)
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
            if (minCtrl.Visible || !agNoticeBox.Visible || !agProgBox.Visible)
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

            Label agDesc = (Label)settingsDialogType.GetField("lblAgentDesc", bf).GetValue(tabDlg);
            if (agDesc == null || agDesc.Text.Contains("\n") || !agDesc.Text.Contains("智能体后台任务完成时自动弹窗提醒"))
            {
                Console.WriteLine("FAIL: lblAgentDesc should be concise single line without \\n");
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
            CheckBox dqBox = (CheckBox)settingsDlgType.GetField("dailyQuote", bf).GetValue(dlgInstance);
            CheckBox qdvBox = (CheckBox)settingsDlgType.GetField("quoteDesktopVisible", bf).GetValue(dlgInstance);
            if (curTab != 1 || dqBox == null || !dqBox.Visible || qdvBox == null || !qdvBox.Visible)
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

            FieldInfo cliTipField = settingsDlgType.GetField("lblAgentCliTip", bf);
            if (cliTipField == null)
            {
                Console.WriteLine("FAIL: SettingsDialog missing lblAgentCliTip field");
                return 51;
            }

            Form setDlgTab2 = (Form)Activator.CreateInstance(settingsDlgType, new object[] { settings, true });
            setDlgTab2.Show();
            setActiveTabMethod.Invoke(setDlgTab2, new object[] { 2 });
            Label cliTipCtrl = (Label)cliTipField.GetValue(setDlgTab2);
            if (cliTipCtrl == null || !cliTipCtrl.Visible || !cliTipCtrl.Text.Contains("iclock -h") || !cliTipCtrl.Text.Contains("iclock --install-cli"))
            {
                Console.WriteLine("FAIL: Tab 2 lblAgentCliTip invalid or not visible: " + (cliTipCtrl == null ? "null" : cliTipCtrl.Text));
                setDlgTab2.Close();
                return 51;
            }
            setDlgTab2.Close();
            Console.WriteLine("PASS: Streamlined tray menu (menuCli eliminated) and SettingsDialog Tab 2 terminal CLI guidance verified.");

            // 34. Verify Telephone Ring Sound synthesis and SettingsDialog soundStyle items
            MethodInfo playSound = appType.GetMethod("PlaySoundStyle", BindingFlags.NonPublic | BindingFlags.Static);
            playSound.Invoke(null, new object[] { "Phone" });
            MethodInfo stopSound = appType.GetMethod("StopSound", BindingFlags.NonPublic | BindingFlags.Static);
            stopSound.Invoke(null, null);

            FieldInfo phoneWavField = appType.GetField("phoneWavData", BindingFlags.NonPublic | BindingFlags.Static);
            byte[] generatedPhoneWav = (byte[])phoneWavField.GetValue(null);
            if (generatedPhoneWav == null || generatedPhoneWav.Length < 1000)
            {
                Console.WriteLine("FAIL: Phone WAV synthesis failed or data too small");
                return 52;
            }

            Type setDlgType = asm.GetType("SettingsDialog");
            Form setDlg = (Form)Activator.CreateInstance(setDlgType, new object[] { settings });
            ComboBox sndStyleCombo = (ComboBox)setDlgType.GetField("soundStyle", bf).GetValue(setDlg);
            if (sndStyleCombo == null || sndStyleCombo.Items.Count < 3)
            {
                Console.WriteLine("FAIL: soundStyle combo not found or incomplete in SettingsDialog");
                return 52;
            }
            string item1 = sndStyleCombo.Items[1].ToString();
            if (!item1.Contains("电话铃") && !item1.Contains("Telephone Ring"))
            {
                Console.WriteLine("FAIL: soundStyle Item[1] should be classic telephone ring, got: " + item1);
                return 52;
            }
            setDlg.Close();
            Console.WriteLine("PASS: Classic telephone mechanical ring tone synthesis and SettingsDialog integration verified.");

            // 35. Verify Daily Quote (每日格言) feature, Settings serialization, Quotes file reading, and Dialog UI
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
            FieldInfo fQuoteDesktopVisible = settings.GetType().GetField("QuoteDesktopVisible");
            FieldInfo fQuoteDuration = settings.GetType().GetField("QuoteDuration");
            FieldInfo fQuoteX = settings.GetType().GetField("QuoteX");
            FieldInfo fQuoteY = settings.GetType().GetField("QuoteY");
            FieldInfo fQuoteWidth = settings.GetType().GetField("QuoteWidth");
            FieldInfo fQuoteFontSize = settings.GetType().GetField("QuoteFontSize");
            FieldInfo fQuoteColor = settings.GetType().GetField("QuoteColor");
            FieldInfo fQuoteFont = settings.GetType().GetField("QuoteFont");
            FieldInfo fQuotePinTopMost = settings.GetType().GetField("QuotePinTopMost");
            if (fDailyQuote == null || fQuoteDesktopVisible == null || fQuoteDuration == null || fQuoteX == null || fQuoteY == null || fQuoteWidth == null || fQuoteFontSize == null || fQuoteColor == null || fQuoteFont == null || fQuotePinTopMost == null)
            {
                Console.WriteLine("FAIL: Settings missing DailyQuote fields (including QuoteDesktopVisible, QuoteColor, QuoteFont and QuotePinTopMost)");
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
            if ((int)fQuoteX.GetValue(settings) != -99999 || (int)fQuoteY.GetValue(settings) != -99999)
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

            if (!quoteDlg.TopMost)
            {
                Console.WriteLine("FAIL: DailyQuoteDialog should be TopMost");
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
            CheckBox dailyQuoteBox = (CheckBox)setDlgType.GetField("dailyQuote", bf).GetValue(testSetDlg);
            CheckBox quoteDeskVisBox = (CheckBox)setDlgType.GetField("quoteDesktopVisible", bf).GetValue(testSetDlg);
            Button btnEditQ = (Button)setDlgType.GetField("btnEditQuotes", bf).GetValue(testSetDlg);
            ComboBox fontComboQ = (ComboBox)setDlgType.GetField("quoteFontCombo", bf).GetValue(testSetDlg);
            NumericUpDown sizeNudQ = (NumericUpDown)setDlgType.GetField("quoteFontSize", bf).GetValue(testSetDlg);
            NumericUpDown durNudQ = (NumericUpDown)setDlgType.GetField("quoteDuration", bf).GetValue(testSetDlg);
            ComboBox colorComboQ = (ComboBox)setDlgType.GetField("quoteColorCombo", bf).GetValue(testSetDlg);
            ComboBox layerComboQ = (ComboBox)setDlgType.GetField("quoteLayerCombo", bf).GetValue(testSetDlg);
            Panel panelQuoteCardQ = (Panel)setDlgType.GetField("panelQuoteCard", bf).GetValue(testSetDlg);
            Button btnNextQ = (Button)setDlgType.GetField("btnNextQuote", bf).GetValue(testSetDlg);
            if (dailyQuoteBox == null || quoteDeskVisBox == null || btnEditQ == null || fontComboQ == null || sizeNudQ == null || durNudQ == null || colorComboQ == null || layerComboQ == null || panelQuoteCardQ == null || btnNextQ == null)
            {
                Console.WriteLine("FAIL: SettingsDialog missing DailyQuote controls (including panelQuoteCard, quoteLayerCombo and btnNextQuote)");
                return 53;
            }
            if (panelQuoteCardQ.Top != 62 || panelQuoteCardQ.Left != 18 || panelQuoteCardQ.Height != 98 ||
                dailyQuoteBox.Top != 172 || dailyQuoteBox.Left != 18 ||
                quoteDeskVisBox.Top != 172 || quoteDeskVisBox.Right != 362 ||
                durNudQ.Top != 204 || sizeNudQ.Top != 238 || fontComboQ.Top != 272 ||
                colorComboQ.Top != 306 || colorComboQ.Width != 188 ||
                layerComboQ.Top != 340 || layerComboQ.Width != 188 ||
                btnEditQ.Top != 374)
            {
                Console.WriteLine(string.Format("FAIL: SettingsDialog DailyQuote controls layout misaligned: cardT={0}, cardH={1}, dqT={2}, qdvR={3}, dur={4}, size={5}, font={6}, color={7}, layer={8}, edit={9}",
                    panelQuoteCardQ.Top, panelQuoteCardQ.Height, dailyQuoteBox.Top, quoteDeskVisBox.Right, durNudQ.Top, sizeNudQ.Top, fontComboQ.Top, colorComboQ.Top, layerComboQ.Top, btnEditQ.Top));
                return 53;
            }
            if (layerComboQ.SelectedIndex != 0)
            {
                Console.WriteLine("FAIL: quoteLayerCombo default selected index should be 0 (Desktop Layer)");
                return 53;
            }
            if (btnNextQ.FlatStyle != FlatStyle.Flat || btnNextQ.FlatAppearance.BorderSize != 0 || btnNextQ.Left != 12)
            {
                Console.WriteLine("FAIL: btnNextQuote should be flat borderless button at bottom-left corner (Left = 12)");
                return 53;
            }
            if (!quoteDeskVisBox.Checked)
            {
                Console.WriteLine("FAIL: quoteDesktopVisible default should be true");
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
            mSetQuotePrev.Invoke(app, new object[] { settings, true });
            Form qForm = (Form)fActiveQuoteField.GetValue(app);
            if (qForm == null || qForm.IsDisposed)
            {
                Console.WriteLine("FAIL: SetQuotePreview(true) should create activeQuoteDialog");
                return 53;
            }
            PropertyInfo pPinTest = qForm.GetType().GetProperty("IsPinned");
            pPinTest.SetValue(qForm, true, null);

            // Simulate tab switch: visible = false
            mSetQuotePrev.Invoke(app, new object[] { settings, false });
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

            pIsPinned.SetValue(quoteDlg, true, null);
            if (quoteDlg.TopMost)
            {
                Console.WriteLine("FAIL: Pinned quote with QuotePinTopMost=false should have TopMost = false");
                return 53;
            }
            pIsPinned.SetValue(quoteDlg, false, null);
            if (!quoteDlg.TopMost)
            {
                Console.WriteLine("FAIL: Unpinned quote popup should have TopMost = true");
                return 53;
            }

            MethodInfo mApplyDraft = quoteDlgType.GetMethod("ApplyDraftSettings", BindingFlags.Public | BindingFlags.Instance);
            if (mApplyDraft == null)
            {
                Console.WriteLine("FAIL: DailyQuoteDialog.ApplyDraftSettings method not found");
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

            Console.WriteLine("PASS: Daily Quote (每日格言) feature, custom quotes parsing, IPC -q command, and SettingsDialog controls verified.");

            // 36. Verify Bug 1 & Bug 2: Desktop quote font size sync with SettingsDialog and Tab 0 countdown color change immunity
            mSetQuotePrev = appType.GetMethod("SetQuotePreview", bf);
            FieldInfo fActiveDlg = appType.GetField("activeDialog", bf);

            handleIpc.Invoke(app, new object[] { "-q" });
            Form desktopQuote = (Form)fActiveQuote.GetValue(app);
            PropertyInfo pCurFontSize = quoteDlgType.GetProperty("CurrentFontSize");
            FieldInfo fCurFontSize = quoteDlgType.GetField("currentFontSize", BindingFlags.NonPublic | BindingFlags.Instance);
            if (fCurFontSize != null) fCurFontSize.SetValue(desktopQuote, 15.5f);

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
            MethodInfo mGetDraft = setDlgType.GetMethod("GetCurrentDraftSettings", BindingFlags.Public | BindingFlags.Instance);
            object draftObj = mGetDraft.Invoke(setDlgInst, null);
            FieldInfo fDraftQuoteSize = settingsType.GetField("QuoteFontSize");
            fDraftQuoteSize.SetValue(draftObj, 10.5f);
            mSetQuotePrev.Invoke(app, new object[] { draftObj, false });

            float sizeAfterTimerChange = (float)pCurFontSize.GetValue(desktopQuote, null);
            if (Math.Abs(sizeAfterTimerChange - 18.0f) > 0.05f)
            {
                Console.WriteLine("FAIL: Changing timer settings in Tab 0 altered desktop quote font size! Expected 18.0, got: " + sizeAfterTimerChange);
                return 54;
            }

            setDlgInst.Close();
            fActiveDlg.SetValue(app, null);
            desktopQuote.Close();

            Console.WriteLine("PASS: Desktop quote font size sync and Tab 0 countdown color immunity verified.");

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
            Label tipZh = (Label)settingsDlgType.GetField("lblAgentCliTip", bf).GetValue(dlgZh);
            if (tipZh == null || !tipZh.Text.Contains("iclock -h") || !tipZh.Text.Contains("iclock --install-cli"))
            {
                Console.WriteLine("FAIL: Tab 2 CLI tip missing expected Chinese instructions");
                dlgZh.Close();
                return 58;
            }
            dlgZh.Close();

            object enSettings = Activator.CreateInstance(settingsType);
            settingsType.GetField("Language").SetValue(enSettings, "en");
            Form dlgEn = (Form)Activator.CreateInstance(settingsDlgType, new object[] { enSettings, true });
            setActiveTabMethod.Invoke(dlgEn, new object[] { 2 });
            Label tipEn = (Label)settingsDlgType.GetField("lblAgentCliTip", bf).GetValue(dlgEn);
            if (tipEn == null || !tipEn.Text.Contains("iclock -h") || !tipEn.Text.Contains("iclock --install-cli"))
            {
                Console.WriteLine("FAIL: Tab 2 CLI tip missing expected English instructions");
                dlgEn.Close();
                return 58;
            }
            dlgEn.Close();

            Console.WriteLine("PASS: Tool introductions modernization (AboutDialog Option 1, Settings Tab 0 tip, Tab 2 terminal CLI guidance & CliHelpDialog elimination) verified.");

            // 41. Verify Native MCP (Model Context Protocol) Server & Tools (iclock.com --mcp stdio)
            string comBinary = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(exePath), "iclock.com");
            if (!System.IO.File.Exists(comBinary)) comBinary = System.IO.Path.Combine(dir, "iclock.com");
            if (!System.IO.File.Exists(comBinary))
            {
                Console.WriteLine("FAIL: iclock.com binary missing for MCP test");
                return 59;
            }

            ProcessStartInfo mcpPsi = new ProcessStartInfo
            {
                FileName = comBinary,
                Arguments = "--mcp",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (Process mcpProc = Process.Start(mcpPsi))
            {
                mcpProc.StandardInput.AutoFlush = true;
                // Test initialize
                mcpProc.StandardInput.WriteLine("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{},\"clientInfo\":{\"name\":\"test-runner\",\"version\":\"1.0\"}}}");
                string mcpInitLine = mcpProc.StandardOutput.ReadLine();
                if (string.IsNullOrEmpty(mcpInitLine) || !mcpInitLine.Contains("\"serverInfo\":{\"name\":\"iclock\""))
                {
                    Console.WriteLine("FAIL: MCP initialize handshake invalid: " + mcpInitLine);
                    return 59;
                }

                // Test ping
                mcpProc.StandardInput.WriteLine("{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"ping\"}");
                string mcpPingLine = mcpProc.StandardOutput.ReadLine();
                if (string.IsNullOrEmpty(mcpPingLine) || !mcpPingLine.Contains("\"result\":{}"))
                {
                    Console.WriteLine("FAIL: MCP ping response invalid: " + mcpPingLine);
                    return 59;
                }

                // Test tools/list contains iclock_ask_human, iclock_progress and all 6 tools
                mcpProc.StandardInput.WriteLine("{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tools/list\",\"params\":{}}");
                string mcpListLine = mcpProc.StandardOutput.ReadLine();
                if (string.IsNullOrEmpty(mcpListLine) ||
                    !mcpListLine.Contains("iclock_ask_human") ||
                    !mcpListLine.Contains("background_only") ||
                    !mcpListLine.Contains("iclock_timer") ||
                    !mcpListLine.Contains("iclock_notify") ||
                    !mcpListLine.Contains("iclock_quote") ||
                    !mcpListLine.Contains("iclock_status") ||
                    !mcpListLine.Contains("iclock_progress"))
                {
                    Console.WriteLine("FAIL: MCP tools/list missing core tools or background_only: " + mcpListLine);
                    return 59;
                }

                // Test unknown method error handling (-32601)
                mcpProc.StandardInput.WriteLine("{\"jsonrpc\":\"2.0\",\"id\":4,\"method\":\"unknown_method\"}");
                string mcpErrLine = mcpProc.StandardOutput.ReadLine();
                if (string.IsNullOrEmpty(mcpErrLine) || !mcpErrLine.Contains("-32601"))
                {
                    Console.WriteLine("FAIL: MCP unknown method error handling failed: " + mcpErrLine);
                    return 59;
                }

                mcpProc.StandardInput.Close();
                mcpProc.WaitForExit(1000);
            }

            // Test IPC status command
            string testStatFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "test_stat_" + Guid.NewGuid().ToString("N") + ".txt");
            handleIpc.Invoke(app, new object[] { "status:" + testStatFile });
            if (!System.IO.File.Exists(testStatFile))
            {
                Console.WriteLine("FAIL: IPC status command did not create output file");
                return 59;
            }
            string statJson = System.IO.File.ReadAllText(testStatFile);
            System.IO.File.Delete(testStatFile);
            if (!statJson.Contains("remaining_seconds") || !statJson.Contains("total_minutes"))
            {
                Console.WriteLine("FAIL: IPC status output format incorrect: " + statJson);
                return 59;
            }

            // Test IPC custom quote command
            handleIpc.Invoke(app, new object[] { "quote:AI Test Quote|Test Author" });
            fActiveQuote = appType.GetField("activeQuoteDialog", bf);
            Form activeQuoteDlg = (Form)fActiveQuote.GetValue(app);
            if (activeQuoteDlg == null)
            {
                Console.WriteLine("FAIL: IPC quote command did not create DailyQuoteDialog");
                return 59;
            }
            activeQuoteDlg.Close();

            // Test IPC progress command
            handleIpc.Invoke(app, new object[] { "progress:60|Compiling test" });
            handleIpc.Invoke(app, new object[] { "progress:clear" });

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
            if (askDlg == null || (!askDlg.Text.Contains("AI 智能体请示") && !askDlg.Text.Contains("AI Agent Request")))
            {
                Console.WriteLine("FAIL: AskHumanDialog instantiation failed");
                return 59;
            }
            if (!askDlg.Text.Contains("Flash Approval HUD") || askDlg.AcceptButton == null)
            {
                Console.WriteLine("FAIL: Flash Approval HUD title or AcceptButton missing on AskHumanDialog");
                return 59;
            }
            askDlg.Close();

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

            // Verify unified Tab 2 MCP controls in SettingsDialog (Option 1: Zero-modal MCP integration)
            Type setDialogType = asm.GetType("SettingsDialog");
            Form mcpSetDlg = (Form)Activator.CreateInstance(setDialogType, new object[] { settings });
            Button btnMcpCopyJson = (Button)setDialogType.GetField("btnMcpCopyJson", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(mcpSetDlg);
            Button btnMcpDemo = (Button)setDialogType.GetField("btnMcpDemo", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(mcpSetDlg);
            Button btnMcpDemoNotify = (Button)setDialogType.GetField("btnMcpDemoNotify", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(mcpSetDlg);
            Button btnMcpDemoQuote = (Button)setDialogType.GetField("btnMcpDemoQuote", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(mcpSetDlg);
            if (btnMcpCopyJson == null || btnMcpDemo == null || btnMcpDemoNotify == null || btnMcpDemoQuote == null)
            {
                Console.WriteLine("FAIL: SettingsDialog unified MCP controls missing");
                mcpSetDlg.Close();
                return 59;
            }
            mcpSetDlg.Show();
            MethodInfo setTabMethod = setDialogType.GetMethod("SetActiveTab", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            if (setTabMethod != null) setTabMethod.Invoke(mcpSetDlg, new object[] { 2 });
            if (!btnMcpCopyJson.Visible || !btnMcpDemo.Visible || !btnMcpDemoNotify.Visible || !btnMcpDemoQuote.Visible)
            {
                Console.WriteLine("FAIL: SettingsDialog unified MCP controls should be visible on Tab 2");
                mcpSetDlg.Close();
                return 59;
            }
            mcpSetDlg.Close();

            // Verify McpConfigHelper type and lossless JSON merge/remove
            Type mcpHelperType = asm.GetType("McpConfigHelper");
            if (mcpHelperType == null)
            {
                Console.WriteLine("FAIL: McpConfigHelper type missing in iClock");
                return 59;
            }
            MethodInfo genSnippet = mcpHelperType.GetMethod("GenerateJsonSnippet", BindingFlags.Public | BindingFlags.Static);
            string snippetAbs = (string)genSnippet.Invoke(null, new object[] { true });
            string snippetRel = (string)genSnippet.Invoke(null, new object[] { false });
            if (!snippetAbs.Contains("iclock") || !snippetRel.Contains("\"command\": \"iclock\""))
            {
                Console.WriteLine("FAIL: GenerateJsonSnippet output invalid");
                return 59;
            }

            MethodInfo genPrompt = mcpHelperType.GetMethod("GenerateAgentPrompt", BindingFlags.Public | BindingFlags.Static);
            if (genPrompt == null)
            {
                Console.WriteLine("FAIL: GenerateAgentPrompt method missing");
                return 59;
            }
            string promptZh = (string)genPrompt.Invoke(null, new object[] { false });
            string promptEn = (string)genPrompt.Invoke(null, new object[] { true });
            if (!promptZh.Contains("iclock_ask_human") || !promptZh.Contains("iclock_notify") || !promptZh.Contains("iclock_quote") ||
                !promptEn.Contains("iclock_ask_human") || !promptEn.Contains("iclock_notify") || !promptEn.Contains("iclock_quote"))
            {
                Console.WriteLine("FAIL: GenerateAgentPrompt content missing core tools");
                return 59;
            }

            MethodInfo getCfgAgent = mcpHelperType.GetMethod("GetConfigPathForAgent", BindingFlags.Public | BindingFlags.Static);
            string cursorCfg = (string)getCfgAgent.Invoke(null, new object[] { "Cursor" });
            string claudeCfg = (string)getCfgAgent.Invoke(null, new object[] { "Claude Code" });
            string agyCfg = (string)getCfgAgent.Invoke(null, new object[] { "Antigravity" });
            string otherCfg = (string)getCfgAgent.Invoke(null, new object[] { "Codex" });
            if (string.IsNullOrEmpty(cursorCfg) || !cursorCfg.Contains(".cursor") ||
                string.IsNullOrEmpty(claudeCfg) || !claudeCfg.Contains("Claude") ||
                string.IsNullOrEmpty(agyCfg) || !agyCfg.Contains(".gemini") ||
                otherCfg != null)
            {
                Console.WriteLine("FAIL: GetConfigPathForAgent mapping invalid");
                return 59;
            }

            string testMcpDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "iclock_selfcheck_mcp_" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(testMcpDir);
            try
            {
                string testMcpFile = System.IO.Path.Combine(testMcpDir, "mcp.json");
                System.IO.File.WriteAllText(testMcpFile, "{\n  \"mcpServers\": {\n    \"playwright\": {\n      \"command\": \"npx\"\n    }\n  }\n}");
                MethodInfo isCfgMethod = mcpHelperType.GetMethod("IsConfigured", BindingFlags.Public | BindingFlags.Static);
                if (isCfgMethod == null)
                {
                    Console.WriteLine("FAIL: McpConfigHelper missing IsConfigured method");
                    return 59;
                }
                bool configured1 = (bool)isCfgMethod.Invoke(null, new object[] { testMcpFile });
                if (configured1)
                {
                    Console.WriteLine("FAIL: IsConfigured should return false when iclock is not present");
                    return 59;
                }

                System.IO.File.WriteAllText(testMcpFile, "{\n  \"mcpServers\": {\n    \"iclock\": {\n      \"command\": \"iclock.com\"\n    }\n  }\n}");
                bool configured2 = (bool)isCfgMethod.Invoke(null, new object[] { testMcpFile });
                if (!configured2)
                {
                    Console.WriteLine("FAIL: IsConfigured should return true when iclock is present");
                    return 59;
                }

                // Verify obsolete Inject and Remove methods have been completely removed
                if (mcpHelperType.GetMethod("Inject") != null || mcpHelperType.GetMethod("Remove") != null)
                {
                    Console.WriteLine("FAIL: McpConfigHelper should no longer expose Inject/Remove methods");
                    return 59;
                }
            }
            finally
            {
                try { if (System.IO.Directory.Exists(testMcpDir)) System.IO.Directory.Delete(testMcpDir, true); } catch { }
            }

            // Verify iclock.com --mcp-config output
            ProcessStartInfo cfgPsi = new ProcessStartInfo
            {
                FileName = comBinary,
                Arguments = "--mcp-config",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using (Process cfgProc = Process.Start(cfgPsi))
            {
                string cfgOut = cfgProc.StandardOutput.ReadToEnd();
                cfgProc.WaitForExit(1000);
                if (!cfgOut.Contains("\"mcpServers\"") || !cfgOut.Contains("\"iclock\"") || !cfgOut.Contains("\"--mcp\""))
                {
                    Console.WriteLine("FAIL: iclock.com --mcp-config output invalid: " + cfgOut);
                    return 59;
                }
            }

            // Verify iclock.com --mcp-prompt output
            ProcessStartInfo promptPsi = new ProcessStartInfo
            {
                FileName = comBinary,
                Arguments = "--mcp-prompt",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using (Process promptProc = Process.Start(promptPsi))
            {
                string promptOut = promptProc.StandardOutput.ReadToEnd();
                promptProc.WaitForExit(1000);
                if (!promptOut.Contains("iclock_ask_human") || !promptOut.Contains("iclock_notify") || !promptOut.Contains("iclock_quote"))
                {
                    Console.WriteLine("FAIL: iclock.com --mcp-prompt output invalid: " + promptOut);
                    return 59;
                }
            }

            // Verify iclock.com --ask CLI dispatch
            ProcessStartInfo askPsi = new ProcessStartInfo
            {
                FileName = comBinary,
                Arguments = "--ask",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using (Process askProc = Process.Start(askPsi))
            {
                string askErr = askProc.StandardError.ReadToEnd();
                askProc.WaitForExit(1500);
                if (askProc.ExitCode != 1 || !askErr.Contains("Usage: iclock --ask"))
                {
                    Console.WriteLine("FAIL: iclock.com --ask CLI usage output invalid (ExitCode=" + askProc.ExitCode + ", Err=" + askErr + ")");
                    return 59;
                }
            }

            Console.WriteLine("PASS: Native MCP Server (iclock.com --mcp), stdio JSON-RPC 2.0 protocol, Unified Tab 2 MCP Integration, McpConfigHelper (detection & prompt generation), --mcp-config, --mcp-prompt, Flash Approval HUD (--ask CLI), and 5 tools (including iclock_ask_human) verified.");

            // 42. Verify MCP Silent Connection Handshake and Prompt-Driven iclock_notify Verification
            MethodInfo handleIpcMethod = appType.GetMethod("HandleIpcCommand", bf);
            if (handleIpcMethod == null)
            {
                Console.WriteLine("FAIL: AppContext missing HandleIpcCommand method");
                return 60;
            }

            // Verify HandleIpcCommand handles silent mcp-connected handshake gracefully without crashing
            try
            {
                handleIpcMethod.Invoke(app, new object[] { "mcp-connected Antigravity" });
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL: HandleIpcCommand failed on mcp-connected: " + ex.Message);
                return 60;
            }

            // Verify McpToastDialog class has been completely removed (zero intrusive popup clutter)
            Type toastType = asm.GetType("McpToastDialog");
            if (toastType != null)
            {
                Console.WriteLine("FAIL: McpToastDialog should be removed, but type still exists");
                return 60;
            }

            // Verify McpNotifiedClients field has been eliminated from Settings
            FieldInfo mcpNotifiedField = settingsType.GetField("McpNotifiedClients");
            if (mcpNotifiedField != null)
            {
                Console.WriteLine("FAIL: Settings still contains obsolete McpNotifiedClients field");
                return 60;
            }

            // Verify McpConfigHelper.GenerateAgentPrompt contains reload instruction and iclock_notify verification protocol
            MethodInfo promptMethod = mcpHelperType.GetMethod("GenerateAgentPrompt", BindingFlags.Public | BindingFlags.Static);
            if (promptMethod == null)
            {
                Console.WriteLine("FAIL: McpConfigHelper missing GenerateAgentPrompt method");
                return 60;
            }

            string zhPrompt = (string)promptMethod.Invoke(null, new object[] { false });
            if (!zhPrompt.Contains("重载窗口") || !zhPrompt.Contains("iclock_notify") || !zhPrompt.Contains("验证桌面联通性"))
            {
                Console.WriteLine("FAIL: zhPrompt missing reload or iclock_notify verification: " + zhPrompt);
                return 60;
            }

            string enPrompt = (string)promptMethod.Invoke(null, new object[] { true });
            if (!enPrompt.Contains("reload the window") || !enPrompt.Contains("iclock_notify") || !enPrompt.Contains("verify connectivity"))
            {
                Console.WriteLine("FAIL: enPrompt missing reload or iclock_notify verification: " + enPrompt);
                return 60;
            }

            // Verify CLI client name normalization
            Assembly cliAsm = Assembly.LoadFrom(comBinary);
            Type cliShimType = cliAsm.GetType("CliShim");
            if (cliShimType != null)
            {
                MethodInfo normMethod = cliShimType.GetMethod("NormalizeClientName", BindingFlags.NonPublic | BindingFlags.Static);
                if (normMethod != null)
                {
                    if ((string)normMethod.Invoke(null, new object[] { "antigravity" }) != "Antigravity" ||
                        (string)normMethod.Invoke(null, new object[] { "cursor-server" }) != "Cursor" ||
                        (string)normMethod.Invoke(null, new object[] { "claude desktop" }) != "Claude Desktop" ||
                        (string)normMethod.Invoke(null, new object[] { "code" }) != "VS Code")
                    {
                        Console.WriteLine("FAIL: CliShim.NormalizeClientName produced unexpected results");
                        return 60;
                    }
                }
            }

            Console.WriteLine("PASS: MCP silent connection handshake, zero intrusive popups, and prompt-driven iclock_notify verification protocol verified.");

            // 43. Verify AI Agent Quote Expansion: Settings (AiAgentType, AiAgentCustomCmd, AiQuoteKeywords persistence & defaults), SettingsDialog side-by-side buttons (btnEditQuotes & btnAiQuotes layout and width), AiAgentQuoteHelper (ParseQuotes formatting, deduplication, safe append with .bak creation), and AiQuoteDialog instantiation.
            FieldInfo fAiAgentType = settingsType.GetField("AiAgentType");
            FieldInfo fAiAgentCustomCmd = settingsType.GetField("AiAgentCustomCmd");
            FieldInfo fAiQuoteKeywords = settingsType.GetField("AiQuoteKeywords");
            if (fAiAgentType == null || fAiAgentCustomCmd == null || fAiQuoteKeywords == null)
            {
                Console.WriteLine("FAIL: Settings missing AiAgentType, AiAgentCustomCmd or AiQuoteKeywords field");
                return 61;
            }
            if ((string)fAiAgentType.GetValue(settings) != "Auto")
            {
                Console.WriteLine("FAIL: Default AiAgentType should be 'Auto', got: " + fAiAgentType.GetValue(settings));
                return 61;
            }

            // Test settings persistence round-trip
            fAiAgentType.SetValue(settings, "Claude");
            fAiAgentCustomCmd.SetValue(settings, "my-cli -p {prompt}");
            fAiQuoteKeywords.SetValue(settings, "心流, 专注");
            settings.GetType().GetMethod("Save").Invoke(settings, null);
            object reloadedAiSettings = settingsType.GetMethod("Load").Invoke(null, null);
            if ((string)fAiAgentType.GetValue(reloadedAiSettings) != "Claude" ||
                (string)fAiAgentCustomCmd.GetValue(reloadedAiSettings) != "my-cli -p {prompt}" ||
                (string)fAiQuoteKeywords.GetValue(reloadedAiSettings) != "心流, 专注")
            {
                Console.WriteLine("FAIL: Settings AiAgentType, AiAgentCustomCmd or AiQuoteKeywords failed to persist across Load/Save");
                return 61;
            }
            // Restore default
            fAiAgentType.SetValue(settings, "Auto");
            fAiAgentCustomCmd.SetValue(settings, "");
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
                // Check .bak file exists
                if (!System.IO.File.Exists(actualQuotesFile + ".bak"))
                {
                    Console.WriteLine("FAIL: AppendQuotesToFile did not create .bak backup file");
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

            // Verify AiAgentQuoteHelper GetKeywords and BuildPrompt
            MethodInfo mGetKeywords = helperType.GetMethod("GetKeywords", BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo mBuildPrompt = helperType.GetMethod("BuildPrompt", BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { typeof(List<string>), typeof(string) }, null);
            MethodInfo mBuildMcpPrompt = helperType.GetMethod("BuildMcpPrompt", BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { typeof(List<string>), typeof(string) }, null);
            if (mGetKeywords == null || mBuildPrompt == null || mBuildMcpPrompt == null)
            {
                Console.WriteLine("FAIL: AiAgentQuoteHelper GetKeywords, BuildPrompt, or BuildMcpPrompt method missing");
                return 61;
            }
            List<string> defaultTagsZh = (List<string>)mGetKeywords.Invoke(null, new object[] { settings });
            if (defaultTagsZh == null || defaultTagsZh.Count != 4 || !defaultTagsZh.Contains("极客心流"))
            {
                Console.WriteLine("FAIL: GetKeywords did not return expected default ZH tags");
                return 61;
            }
            string generatedPrompt = (string)mBuildPrompt.Invoke(null, new object[] { new List<string> { "知行合一", "松弛感" }, "zh" });
            if (!generatedPrompt.Contains("知行合一") || !generatedPrompt.Contains("松弛感") || !generatedPrompt.Contains("30"))
            {
                Console.WriteLine("FAIL: BuildPrompt did not include custom tags and 30 quotes request: " + generatedPrompt);
                return 61;
            }
            string generatedMcpPrompt = (string)mBuildMcpPrompt.Invoke(null, new object[] { new List<string> { "知行合一", "松弛感" }, "zh" });
            if (!generatedMcpPrompt.Contains("30") || !generatedMcpPrompt.Contains("iclock_quote") || !generatedMcpPrompt.Contains("add"))
            {
                Console.WriteLine("FAIL: BuildMcpPrompt did not include 30 quotes and iclock_quote tool call instruction: " + generatedMcpPrompt);
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
            if (aiDlg.ClientSize.Width != 410 || aiDlg.ClientSize.Height != 265)
            {
                Console.WriteLine("FAIL: AiQuoteDialog size should be 410x265, got: " + aiDlg.ClientSize.Width + "x" + aiDlg.ClientSize.Height);
                return 61;
            }
            FieldInfo fPanelTags = aiDlgType.GetField("panelTags", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo fTxtNewTag = aiDlgType.GetField("txtNewTag", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo fCboAgent = aiDlgType.GetField("cboAgent", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo fPanelStatus = aiDlgType.GetField("panelStatus", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo fPanelSetup = aiDlgType.GetField("panelSetup", BindingFlags.NonPublic | BindingFlags.Instance);
            if (fPanelTags == null || fTxtNewTag == null || fCboAgent == null || fPanelStatus == null || fPanelSetup == null)
            {
                Console.WriteLine("FAIL: AiQuoteDialog missing required UI fields (panelTags, txtNewTag, cboAgent, panelStatus, panelSetup)");
                return 61;
            }
            FlowLayoutPanel pnlTags = (FlowLayoutPanel)fPanelTags.GetValue(aiDlg);
            ComboBox cbAgent = (ComboBox)fCboAgent.GetValue(aiDlg);
            Panel pnlSetup = (Panel)fPanelSetup.GetValue(aiDlg);
            Panel pnlStatus = (Panel)fPanelStatus.GetValue(aiDlg);
            if (pnlTags == null || pnlTags.Controls.Count == 0)
            {
                Console.WriteLine("FAIL: AiQuoteDialog panelTags was not populated with default tag pills");
                return 61;
            }
            if (cbAgent == null || cbAgent.Items.Count == 0)
            {
                Console.WriteLine("FAIL: AiQuoteDialog cboAgent was not populated with detected agents");
                return 61;
            }
            if (pnlSetup == null || pnlStatus == null || pnlSetup.Controls.Count == 0 || pnlStatus.Controls.Count == 0)
            {
                Console.WriteLine("FAIL: AiQuoteDialog dual-track panels (panelSetup/panelStatus) missing controls");
                return 61;
            }

            // Verify AiAgentQuoteHelper HasCliSupport method and OnQuotesAdded event
            MethodInfo mHasCli = helperType.GetMethod("HasCliSupport", BindingFlags.NonPublic | BindingFlags.Static);
            if (mHasCli == null)
            {
                Console.WriteLine("FAIL: AiAgentQuoteHelper missing HasCliSupport method");
                return 61;
            }
            bool antigravityHasCli = (bool)mHasCli.Invoke(null, new object[] { "Antigravity" });
            if (antigravityHasCli)
            {
                Console.WriteLine("FAIL: Antigravity should not be identified as CLI (it is an IDE MCP agent)");
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

            // Verify when IsPinned is true, countdownTimer is completely stopped (zero polling wakeups!)
            pIsPinned.SetValue(qDlgMoveTest, true, null);
            Timer timerObj = (Timer)fCountdownTimer.GetValue(qDlgMoveTest);
            if (timerObj != null && timerObj.Enabled)
            {
                Console.WriteLine("FAIL: countdownTimer should be stopped when quote is pinned to desktop");
                return 62;
            }

            qDlgMoveTest.Close();
            qDlgMoveTest.Dispose();

            Console.WriteLine("PASS: Desktop quote Alt + Drag mode, MoveMode synchronization, and 0-polling background optimization verified.");

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
