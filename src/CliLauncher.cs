using System;
using System.Diagnostics;
using System.IO;

internal static class CliLauncher
{
    private static int Main(string[] args)
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string targetExe = Path.Combine(baseDir, "iClock.exe");
        if (!File.Exists(targetExe))
        {
            Console.Error.WriteLine("[iClock] Error: Cannot find iClock.exe in " + baseDir);
            return 1;
        }

        // When user runs bare `iclock` in terminal with no arguments:
        // launch desktop GUI detached and immediately return prompt to the shell.
        if (args == null || args.Length == 0)
        {
            ProcessStartInfo guiPsi = new ProcessStartInfo(targetExe);
            guiPsi.UseShellExecute = true;
            Process.Start(guiPsi);
            return 0;
        }

        // Pass-through to iClock.exe synchronously via standard pipes,
        // preserving raw arguments, quotes, and exit code.
        string rawArgs = GetRawArguments();
        ProcessStartInfo psi = new ProcessStartInfo(targetExe, rawArgs);
        psi.UseShellExecute = false;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.CreateNoWindow = true;

        using (Process p = new Process())
        {
            p.StartInfo = psi;
            p.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (e.Data != null) Console.WriteLine(e.Data);
            };
            p.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (e.Data != null) Console.Error.WriteLine(e.Data);
            };

            if (!p.Start()) return 1;
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            p.WaitForExit();
            return p.ExitCode;
        }
    }

    private static string GetRawArguments()
    {
        string cmd = Environment.CommandLine;
        if (string.IsNullOrEmpty(cmd)) return "";
        cmd = cmd.TrimStart();
        if (cmd.Length > 0 && cmd[0] == '"')
        {
            int next = cmd.IndexOf('"', 1);
            if (next >= 0) return cmd.Substring(next + 1).TrimStart();
        }
        else
        {
            int next = cmd.IndexOf(' ');
            if (next >= 0) return cmd.Substring(next + 1).TrimStart();
        }
        return "";
    }
}
