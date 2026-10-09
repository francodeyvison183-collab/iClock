using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

internal static class TestCheck
{
    [STAThread]
    private static int Main()
    {
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

            // 0. Verify default settings: FontSize = 20, Color = "#FF0000"
            Type settingsType = asm.GetType("Settings");
            object defaultSettings = Activator.CreateInstance(settingsType);
            int defSize = (int)settingsType.GetField("FontSize").GetValue(defaultSettings);
            string defColor = (string)settingsType.GetField("Color").GetValue(defaultSettings);
            if (defSize != 20 || defColor != "#FF0000")
            {
                Console.WriteLine("FAIL: Default FontSize (" + defSize + ") should be 20 and Color (" + defColor + ") should be #FF0000");
                return 18;
            }
            Console.WriteLine("PASS: Default settings have FontSize = 20 and Color = #FF0000 (red).");

            // 1. Overlay must be hidden before countdown starts
            if (overlay.Visible)
            {
                Console.WriteLine("FAIL: Overlay visible before countdown starts");
                return 1;
            }
            Console.WriteLine("PASS: Overlay is hidden before countdown starts.");

            // 2. Tray menu must not contain Show/hide text
            ContextMenuStrip menu = tray.ContextMenuStrip;
            foreach (ToolStripItem item in menu.Items)
            {
                if (item.Text != null && (item.Text.Contains("显示") || item.Text.Contains("Show / hide")))
                {
                    Console.WriteLine("FAIL: Found visibility menu item: " + item.Text);
                    return 2;
                }
            }
            Console.WriteLine("PASS: Tray menu does not contain show/hide text.");

            // 3. Start countdown -> Overlay becomes visible
            MethodInfo toggle = appType.GetMethod("Toggle", bf);
            toggle.Invoke(app, null);
            if (!overlay.Visible)
            {
                Console.WriteLine("FAIL: Overlay should be visible after starting countdown");
                return 3;
            }
            Console.WriteLine("PASS: Overlay is visible after starting countdown.");

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
            Console.WriteLine("PASS: NoticeDialog is TopMost and displays countdown finish time.");
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
            if (menuAbout == null || menuAbout.Text != "关于 iClock…")
            {
                Console.WriteLine("FAIL: menuAbout missing or incorrect text");
                return 10;
            }
            Type aboutType = asm.GetType("AboutDialog");
            Form aboutDialog = (Form)Activator.CreateInstance(aboutType, new object[] { "zh" });
            PictureBox pb = null;
            Button btnCheck = null;
            foreach (Control c in aboutDialog.Controls)
            {
                if (c is PictureBox) pb = (PictureBox)c;
                if (c is Button && (c.Text == "检查更新" || c.Text == "Check Updates")) btnCheck = (Button)c;
            }
            if (pb == null || pb.Image == null || pb.Image.Width != 1152)
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
            if (pb.Width < 280)
            {
                Console.WriteLine("FAIL: AboutDialog PictureBox width too narrow: " + pb.Width);
                return 12;
            }
            aboutDialog.Close();
            Console.WriteLine("PASS: AboutDialog and embedded zan.jpg loaded with full content width (" + pb.Width + "px) and Check Updates button.");

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
            if (appTimer.Interval < 80 || appTimer.Interval > 400)
            {
                Console.WriteLine("FAIL: Timer interval outside expected [80, 400] range: " + appTimer.Interval);
                return 14;
            }
            Console.WriteLine("PASS: Overlay caches GDI handles and timer uses dynamic heartbeat alignment (" + appTimer.Interval + "ms).");

            // 9. Verify SettingsDialog contains language selection and default update check & deduplicated launch reporting
            Type settingsDialogType = asm.GetType("SettingsDialog");
            Form settingsDialog = (Form)Activator.CreateInstance(settingsDialogType, new object[] { settings });
            bool hasUpdateCheck = false;
            bool hasLanguageCombo = false;
            foreach (Control c in settingsDialog.Controls)
            {
                if (c is CheckBox && (c.Text.Contains("自动检查版本更新") || c.Text.Contains("Check for updates")))
                {
                    hasUpdateCheck = true;
                    break;
                }
                if (c is ComboBox && ((ComboBox)c).Items.Contains("简体中文"))
                {
                    hasLanguageCombo = true;
                }
            }
            settingsDialog.Dispose();
            if (hasUpdateCheck)
            {
                Console.WriteLine("FAIL: SettingsDialog should not have manual update check option");
                return 15;
            }
            if (!hasLanguageCombo)
            {
                Console.WriteLine("FAIL: SettingsDialog missing language option");
                return 15;
            }
            FieldInfo launchField = appType.GetField("launchReported", BindingFlags.NonPublic | BindingFlags.Static);
            if (launchField == null || !(bool)launchField.GetValue(null))
            {
                Console.WriteLine("FAIL: launchReported was not set on startup or field missing");
                return 15;
            }
            Console.WriteLine("PASS: SettingsDialog contains language selection and launchReported is de-duplicated.");

            // 10. Verify version comparison and JSON parsing
            BindingFlags sbf = BindingFlags.NonPublic | BindingFlags.Static;
            FieldInfo verField = appType.GetField("CURRENT_VERSION", sbf);
            if (verField == null || (string)verField.GetValue(null) != "1.01")
            {
                Console.WriteLine("FAIL: CURRENT_VERSION should be 1.01");
                return 16;
            }
            MethodInfo isNewer = appType.GetMethod("IsNewer", sbf);
            MethodInfo extractJson = appType.GetMethod("ExtractJsonValue", sbf);
            if (!(bool)isNewer.Invoke(null, new object[] { "1.02", "1.01" }) ||
                !(bool)isNewer.Invoke(null, new object[] { "v2.0", "1.01" }) ||
                (bool)isNewer.Invoke(null, new object[] { "1.01", "1.01" }))
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
    }
}
