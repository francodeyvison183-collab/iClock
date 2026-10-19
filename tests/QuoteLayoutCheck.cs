using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

internal static class QuoteLayoutCheck
{
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [STAThread]
    private static int Main()
    {
        try
        {
            string exe = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "iClock.exe");
            Assembly asm = Assembly.LoadFrom(exe);
            Type settingsType = asm.GetType("Settings");
            Type quoteType = asm.GetType("DailyQuoteDialog");
            object settings = Activator.CreateInstance(settingsType);
            Screen secondary = null;
            foreach (Screen s in Screen.AllScreens) if (!s.Primary) { secondary = s; break; }
            if (secondary == null)
            {
                Console.WriteLine("SKIP: no secondary monitor connected");
                return 0;
            }

            int x = secondary.WorkingArea.Left + 731;
            int y = secondary.WorkingArea.Top + 456;
            settingsType.GetField("QuoteX").SetValue(settings, x);
            settingsType.GetField("QuoteY").SetValue(settings, y);
            settingsType.GetField("QuoteWidth").SetValue(settings, 878);
            settingsType.GetField("QuoteFontSize").SetValue(settings, 16.5f);

            Form quote = (Form)Activator.CreateInstance(quoteType, new object[] { settings });
            PropertyInfo sizeProp = quoteType.GetProperty("CurrentFontSize");
            MethodInfo apply = quoteType.GetMethod("ApplySettings");
            int initialX = quote.Location.X, initialY = quote.Location.Y;
            int initialW = quote.Width, initialH = quote.Height;
            float initialFont = (float)sizeProp.GetValue(quote, null);
            if (initialX != x || initialY != y || initialW != 878 || Math.Abs(initialFont - 16.5f) > 0.05f)
            {
                Console.WriteLine("FAIL: cold construction did not restore saved secondary state: " + quote.Location + " " + quote.Size + " font=" + initialFont);
                return 1;
            }

            apply.Invoke(quote, null);
            if (quote.Location.X != initialX || quote.Location.Y != initialY || quote.Width != initialW || quote.Height != initialH || Math.Abs((float)sizeProp.GetValue(quote, null) - initialFont) > 0.05f)
            {
                Console.WriteLine("FAIL: settings-tab refresh changed cold-restored layout");
                return 2;
            }

            // Exercise the real first-visible path. On negative-coordinate
            // monitors the initial layered update needs one native bounds sync.
            MethodInfo render = quoteType.GetMethod("RenderLayeredQuoteWindow");
            FieldInfo synced = quoteType.GetField("hasRenderedOnce", BindingFlags.NonPublic | BindingFlags.Instance);
            if (synced == null)
            {
                Console.WriteLine("FAIL: first-visible layered geometry guard is missing");
                return 5;
            }
            quote.Show();
            Application.DoEvents();
            Thread.Sleep(100);
            Application.DoEvents();
            if (!(bool)synced.GetValue(quote))
            {
                Console.WriteLine("FAIL: first visible render did not synchronize layered geometry");
                return 5;
            }
            if (quote.Location.X != x || quote.Location.Y != y || quote.Width != 878 || Math.Abs((float)sizeProp.GetValue(quote, null) - 16.5f) > 0.05f)
            {
                Console.WriteLine("FAIL: showing the quote changed restored secondary state: " + quote.Location + " " + quote.Size + " font=" + sizeProp.GetValue(quote, null));
                return 5;
            }
            // This call models the native bounds reassertion used by the
            // production first-visible render and guards the interop signature.
            if (!SetWindowPos(quote.Handle, IntPtr.Zero, x, y, quote.Width, quote.Height, 0x0020))
            {
                Console.WriteLine("FAIL: native layered geometry synchronization failed");
                return 5;
            }
            quote.Close();

            settingsType.GetField("QuoteX").SetValue(settings, -500000);
            settingsType.GetField("QuoteY").SetValue(settings, -500000);
            Form offline = (Form)Activator.CreateInstance(quoteType, new object[] { settings });
            Rectangle primary = Screen.PrimaryScreen.WorkingArea;
            Rectangle visible = Rectangle.Intersect(primary, new Rectangle(offline.Location, offline.Size));
            if (visible.Width < 40 || visible.Height < 30)
            {
                Console.WriteLine("FAIL: disconnected-monitor quote did not fall back to primary screen: " + offline.Location);
                return 3;
            }
            if ((int)settingsType.GetField("QuoteX").GetValue(settings) != -500000 || (int)settingsType.GetField("QuoteY").GetValue(settings) != -500000)
            {
                Console.WriteLine("FAIL: offline fallback overwrote saved extended-screen coordinates");
                return 4;
            }
            MethodInfo capture = quoteType.GetMethod("CapturePersistentState");
            capture.Invoke(offline, null);
            if ((int)settingsType.GetField("QuoteX").GetValue(settings) != -500000 || (int)settingsType.GetField("QuoteY").GetValue(settings) != -500000)
            {
                Console.WriteLine("FAIL: exiting offline fallback overwrote saved extended-screen coordinates");
                return 6;
            }
            offline.Close();
            Console.WriteLine("PASS: cold restore, first-visible layered geometry, settings refresh stability, and disconnected-monitor fallback");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERROR: " + ex);
            return 99;
        }
    }
}
