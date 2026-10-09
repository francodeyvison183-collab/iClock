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
            Assembly asm = Assembly.LoadFrom(exePath);
            Type appType = asm.GetType("AppContext");
            BindingFlags bf = BindingFlags.NonPublic | BindingFlags.Instance;

            object app = Activator.CreateInstance(appType);
            Form overlay = (Form)appType.GetField("overlay", bf).GetValue(app);
            NotifyIcon tray = (NotifyIcon)appType.GetField("tray", bf).GetValue(app);
            object settings = appType.GetField("settings", bf).GetValue(app);
            FieldInfo endNoticeField = settings.GetType().GetField("EndNotice");
            FieldInfo endSoundField = settings.GetType().GetField("EndSound");
            endNoticeField.SetValue(settings, false);
            endSoundField.SetValue(settings, false);

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

            // 5. Restart countdown then Reset -> Overlay becomes hidden
            toggle.Invoke(app, null);
            if (!overlay.Visible)
            {
                Console.WriteLine("FAIL: Overlay should be visible after restarting countdown");
                return 5;
            }
            ToolStripMenuItem menuReset = (ToolStripMenuItem)appType.GetField("menuReset", bf).GetValue(app);
            menuReset.PerformClick();
            if (overlay.Visible)
            {
                Console.WriteLine("FAIL: Overlay should be hidden after reset");
                return 6;
            }
            Console.WriteLine("PASS: Overlay is hidden after reset.");

            // 6. Check ShowNativeMessageBox method exists
            MethodInfo msgBox = appType.GetMethod("ShowNativeMessageBox", BindingFlags.NonPublic | BindingFlags.Static);
            if (msgBox == null)
            {
                Console.WriteLine("FAIL: ShowNativeMessageBox method missing");
                return 7;
            }
            Console.WriteLine("PASS: ShowNativeMessageBox P/Invoke method is present.");

            MethodInfo exit = appType.GetMethod("Exit", bf);
            exit.Invoke(app, null);

            Console.WriteLine("ALL 6 CHECKS PASSED!");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERROR: " + ex);
            return 99;
        }
    }
}
