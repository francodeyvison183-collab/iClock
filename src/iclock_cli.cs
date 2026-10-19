using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

internal static class CliShim
{
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, ref COPYDATASTRUCT lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    private static string mcpClientName = null;

    private static bool IsAgentInForeground()
    {
        try
        {
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero) return false;
            uint pid = 0;
            GetWindowThreadProcessId(fg, out pid);
            if (pid == 0) return false;
            using (Process p = Process.GetProcessById((int)pid))
            {
                string procName = p.ProcessName.ToLowerInvariant();
                string[] known = new string[] {
                    "cursor", "claude", "windsurf", "code", "trae", "antigravity",
                    "idea", "pycharm", "webstorm", "devenv", "cline"
                };
                foreach (string k in known)
                {
                    if (procName.Contains(k)) return true;
                }
            }
        }
        catch { }
        return false;
    }

    internal static string NormalizeClientName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return null;
        string lower = raw.Trim().ToLowerInvariant();
        if (lower.Contains("antigravity")) return "Antigravity";
        if (lower.Contains("cursor")) return "Cursor";
        if (lower.Contains("claude")) return "Claude Desktop";
        if (lower.Contains("windsurf")) return "Windsurf";
        if (lower.Contains("cline")) return "Cline";
        if (lower.Contains("roo")) return "Roo Code";
        if (lower.Contains("trae")) return "Trae";
        if (lower.Contains("code") || lower.Contains("vscode")) return "VS Code";
        if (lower.Contains("pycharm")) return "PyCharm";
        if (lower.Contains("idea") || lower.Contains("intellij")) return "IntelliJ IDEA";
        if (lower.Contains("webstorm")) return "WebStorm";
        return raw.Trim();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct COPYDATASTRUCT
    {
        public IntPtr dwData;
        public int cbData;
        public IntPtr lpData;
    }

    private const uint WM_COPYDATA = 0x004A;

    private static int Main(string[] args)
    {
        if (args != null && args.Length > 0 && (args[0] == "--mcp" || args[0] == "-mcp"))
        {
            return RunMcpServer();
        }
        if (args != null && args.Length > 0 && (args[0] == "--mcp-config" || args[0] == "-mcp-config" || args[0] == "mcp-config"))
        {
            string cfg = "{\n  \"mcpServers\": {\n    \"iclock\": {\n      \"command\": \"iclock\",\n      \"args\": [\n        \"--mcp\"\n      ]\n    }\n  }\n}";
            Console.WriteLine(cfg);
            return 0;
        }
        if (args != null && args.Length > 0 && (args[0] == "--ask" || args[0] == "-a" || args[0] == "ask"))
        {
            return RunAskCommand(args);
        }
        if (args != null && args.Length > 0 && (args[0] == "--progress" || args[0] == "-pr" || args[0] == "progress"))
        {
            return RunProgressCommand(args);
        }

        string dir = AppDomain.CurrentDomain.BaseDirectory;
        string exe = Path.Combine(dir, "iClock.exe");
        if (!File.Exists(exe)) exe = "iClock.exe";

        if (args == null || args.Length == 0)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = true
            });
            return 0;
        }

        string cmdArgs = string.Join(" ", Array.ConvertAll(args, delegate(string a)
        {
            if (string.IsNullOrEmpty(a)) return "\"\"";
            if (a.Contains(" ") || a.Contains("\t") || a.Contains("\""))
                return "\"" + a.Replace("\"", "\\\"") + "\"";
            return a;
        }));

        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = cmdArgs,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using (Process p = Process.Start(psi))
        {
            string stdout = p.StandardOutput.ReadToEnd();
            string stderr = p.StandardError.ReadToEnd();
            p.WaitForExit();
            if (!string.IsNullOrEmpty(stdout)) Console.Write(stdout);
            if (!string.IsNullOrEmpty(stderr)) Console.Error.Write(stderr);
            return p.ExitCode;
        }
    }

    private static int RunAskCommand(string[] args)
    {
        string question = null;
        List<string> options = new List<string>();
        int timeoutSec = 0;

        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            if (a == "--ask" || a == "-a" || a == "ask")
            {
                if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                {
                    question = args[++i];
                }
            }
            else if (a == "--options" || a == "-o" || a == "--option")
            {
                if (i + 1 < args.Length)
                {
                    string rawOpts = args[++i];
                    string[] parts = rawOpts.Split(new char[] { ',', '，', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string p in parts)
                    {
                        string trimmed = p.Trim();
                        if (!string.IsNullOrEmpty(trimmed)) options.Add(trimmed);
                    }
                }
            }
            else if (a == "--timeout" || a == "-t")
            {
                if (i + 1 < args.Length)
                {
                    int t;
                    if (int.TryParse(args[++i], out t) && t > 0) timeoutSec = t;
                }
            }
            else if (question == null && !a.StartsWith("-"))
            {
                question = a;
            }
        }

        if (string.IsNullOrEmpty(question))
        {
            Console.Error.WriteLine("[iClock] Usage: iclock --ask <question> [--options <opt1,opt2>] [--timeout <sec>]");
            return 1;
        }

        IntPtr hWnd = EnsureIClockRunning();
        if (hWnd == IntPtr.Zero)
        {
            Console.Error.WriteLine("[iClock] Error: Failed to connect to iClock desktop instance.");
            return 1;
        }

        string reqId = Guid.NewGuid().ToString("N").Substring(0, 8);
        string resFile = Path.Combine(Path.GetTempPath(), "iclock_ask_" + reqId + ".txt");
        string optStr = string.Join("~", options.ToArray());

        string cmd = "ask_human:" + reqId + "|" + timeoutSec + "|" + question + "|" + optStr;
        if (!SendIpc(cmd))
        {
            Console.Error.WriteLine("[iClock] Error: Failed to send decision request to iClock.");
            return 1;
        }

        int maxWaitMs = timeoutSec > 0 ? (timeoutSec + 5) * 1000 : int.MaxValue;
        int waited = 0;
        while (waited < maxWaitMs)
        {
            Thread.Sleep(100);
            if (timeoutSec > 0) waited += 100;
            if (File.Exists(resFile))
            {
                try
                {
                    string result = File.ReadAllText(resFile, Encoding.UTF8).Trim();
                    try { File.Delete(resFile); } catch { }
                    if (result == "[用户取消 / Cancelled]" || result == "Cancelled")
                    {
                        Console.Error.WriteLine(result);
                        return 1;
                    }
                    if (result == "[超时未响应 / Timeout]")
                    {
                        Console.Error.WriteLine(result);
                        return 2;
                    }
                    Console.WriteLine(result);
                    return 0;
                }
                catch { }
            }
        }

        Console.Error.WriteLine("[iClock] Timed out waiting for human decision.");
        return 2;
    }

    private static int RunProgressCommand(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("[iClock] Usage: iclock --progress <value|clear> [message]");
            Console.Error.WriteLine("  Examples:");
            Console.Error.WriteLine("    iclock --progress 60 \"Compiling project...\"");
            Console.Error.WriteLine("    iclock --progress 3/5 \"Running step 3 of 5\"");
            Console.Error.WriteLine("    iclock --progress clear");
            return 1;
        }

        string val = args[1].Trim();
        string msg = args.Length > 2 ? string.Join(" ", args, 2, args.Length - 2) : "";

        IntPtr hWnd = EnsureIClockRunning();
        if (hWnd == IntPtr.Zero)
        {
            Console.Error.WriteLine("[iClock] Error: Failed to connect to iClock desktop instance.");
            return 1;
        }

        string payload = string.Equals(val, "clear", StringComparison.OrdinalIgnoreCase) || string.Equals(val, "off", StringComparison.OrdinalIgnoreCase)
            ? "clear"
            : (!string.IsNullOrEmpty(msg) ? (val + "|" + msg) : val);

        if (SendIpc("progress:" + payload))
        {
            if (payload == "clear") Console.WriteLine("[iClock] Agent progress cleared.");
            else Console.WriteLine("[iClock] Agent progress updated: " + val + (!string.IsNullOrEmpty(msg) ? " (" + msg + ")" : ""));
            return 0;
        }
        else
        {
            Console.Error.WriteLine("[iClock] Error: Failed to send progress to iClock.");
            return 1;
        }
    }

    private static IntPtr EnsureIClockRunning()
    {
        IntPtr hWnd = FindWindow(null, "iClock.IpcWindow");
        if (hWnd != IntPtr.Zero) return hWnd;

        string dir = AppDomain.CurrentDomain.BaseDirectory;
        string exe = Path.Combine(dir, "iClock.exe");
        if (!File.Exists(exe)) exe = "iClock.exe";

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = true
            });
        }
        catch { }

        for (int i = 0; i < 40; i++)
        {
            Thread.Sleep(100);
            hWnd = FindWindow(null, "iClock.IpcWindow");
            if (hWnd != IntPtr.Zero) return hWnd;
        }
        return IntPtr.Zero;
    }

    private static bool SendIpc(string cmd)
    {
        IntPtr hWnd = EnsureIClockRunning();
        if (hWnd == IntPtr.Zero) return false;

        byte[] bytes = Encoding.Unicode.GetBytes(cmd + "\0");
        IntPtr ptr = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            COPYDATASTRUCT cds;
            cds.dwData = IntPtr.Zero;
            cds.cbData = bytes.Length;
            cds.lpData = ptr;
            SendMessage(hWnd, WM_COPYDATA, IntPtr.Zero, ref cds);
            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    private static int RunMcpServer()
    {
        JavaScriptSerializer jss = new JavaScriptSerializer();
        jss.MaxJsonLength = int.MaxValue;

        Console.InputEncoding = Encoding.UTF8;
        StreamWriter utf8Writer = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true };
        Console.SetOut(utf8Writer);

        string line;
        while ((line = Console.ReadLine()) != null)
        {
            line = line.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            try
            {
                Dictionary<string, object> req = jss.Deserialize<Dictionary<string, object>>(line);
                if (req == null) continue;

                string method = req.ContainsKey("method") ? req["method"] as string : null;
                object id = req.ContainsKey("id") ? req["id"] : null;

                if (method == "initialize")
                {
                    if (req.ContainsKey("params"))
                    {
                        var prms = req["params"] as Dictionary<string, object>;
                        if (prms != null && prms.ContainsKey("clientInfo"))
                        {
                            var ci = prms["clientInfo"] as Dictionary<string, object>;
                            if (ci != null && ci.ContainsKey("name") && ci["name"] != null)
                            {
                                mcpClientName = NormalizeClientName(ci["name"].ToString());
                            }
                        }
                    }
                    var capabilities = new Dictionary<string, object>
                    {
                        { "tools", new Dictionary<string, object>() }
                    };
                    var serverInfo = new Dictionary<string, object>
                    {
                        { "name", "iclock" },
                        { "version", "1.06" }
                    };
                    var res = new Dictionary<string, object>
                    {
                        { "jsonrpc", "2.0" },
                        { "id", id },
                        { "result", new Dictionary<string, object>
                            {
                                { "protocolVersion", "2024-11-05" },
                                { "capabilities", capabilities },
                                { "serverInfo", serverInfo }
                            }
                        }
                    };
                    Console.WriteLine(jss.Serialize(res));
                }
                else if (method == "notifications/initialized")
                {
                    // No response required for notifications
                }
                else if (method == "ping")
                {
                    var res = new Dictionary<string, object>
                    {
                        { "jsonrpc", "2.0" },
                        { "id", id },
                        { "result", new Dictionary<string, object>() }
                    };
                    Console.WriteLine(jss.Serialize(res));
                }
                else if (method == "tools/list")
                {
                    var toolsList = BuildToolsList();
                    var res = new Dictionary<string, object>
                    {
                        { "jsonrpc", "2.0" },
                        { "id", id },
                        { "result", new Dictionary<string, object>
                            {
                                { "tools", toolsList }
                            }
                        }
                    };
                    Console.WriteLine(jss.Serialize(res));
                }
                else if (method == "tools/call")
                {
                    Dictionary<string, object> prms = req.ContainsKey("params") ? req["params"] as Dictionary<string, object> : null;
                    string toolName = prms != null && prms.ContainsKey("name") ? prms["name"] as string : "";
                    Dictionary<string, object> toolArgs = prms != null && prms.ContainsKey("arguments") ? prms["arguments"] as Dictionary<string, object> : new Dictionary<string, object>();

                    string callResult = ExecuteToolCall(toolName, toolArgs);
                    var res = new Dictionary<string, object>
                    {
                        { "jsonrpc", "2.0" },
                        { "id", id },
                        { "result", new Dictionary<string, object>
                            {
                                { "content", new object[]
                                    {
                                        new Dictionary<string, object> { { "type", "text" }, { "text", callResult } }
                                    }
                                },
                                { "isError", false }
                            }
                        }
                    };
                    Console.WriteLine(jss.Serialize(res));
                }
                else if (id != null)
                {
                    var errRes = new Dictionary<string, object>
                    {
                        { "jsonrpc", "2.0" },
                        { "id", id },
                        { "error", new Dictionary<string, object>
                            {
                                { "code", -32601 },
                                { "message", "Method not found: " + method }
                            }
                        }
                    };
                    Console.WriteLine(jss.Serialize(errRes));
                }
            }
            catch (Exception ex)
            {
                try
                {
                    var errRes = new Dictionary<string, object>
                    {
                        { "jsonrpc", "2.0" },
                        { "error", new Dictionary<string, object>
                            {
                                { "code", -32603 },
                                { "message", "Internal error: " + ex.Message }
                            }
                        }
                    };
                    Console.WriteLine(jss.Serialize(errRes));
                }
                catch { }
            }
        }
        return 0;
    }

    private static List<Dictionary<string, object>> BuildToolsList()
    {
        var list = new List<Dictionary<string, object>>();

        // 1. iclock_ask_human
        list.Add(new Dictionary<string, object>
        {
            { "name", "iclock_ask_human" },
            { "description", "Flash Approval HUD: Pop up an interactive desktop authorization gate on the user's screen for critical decision approvals and choices (Human-in-the-Loop, Enter/1-9/Esc shortcuts, infinite wait by default)." },
            { "inputSchema", new Dictionary<string, object>
                {
                    { "type", "object" },
                    { "properties", new Dictionary<string, object>
                        {
                            { "action", new Dictionary<string, object> { { "type", "string" }, { "enum", new string[] { "ask", "cancel" } }, { "description", "Action to perform: 'ask' (default) prompts user on screen; 'cancel' immediately closes active pending approval card." } } },
                            { "question", new Dictionary<string, object> { { "type", "string" }, { "description", "The question, prompt, or approval details to display (e.g. command details, target branch, risk warning)" } } },
                            { "options", new Dictionary<string, object> { { "type", "array" }, { "items", new Dictionary<string, object> { { "type", "string" } } }, { "description", "List of clickable option buttons (e.g. ['Approve', 'Cancel'] or ['Option A', 'Option B']). If omitted, a text input box is presented." } } },
                            { "timeout_seconds", new Dictionary<string, object> { { "type", "integer" }, { "description", "Optional auto-timeout duration in seconds (default: 0 = infinite wait, no auto-timeout fallback). Only set when running in unattended automated scripts." } } },
                            { "background_only", new Dictionary<string, object> { { "type", "boolean" }, { "description", "Optional. If true, only pops up desktop card when the agent host window is in the background. If the user is actively focusing the agent window, immediately returns status in_foreground so the agent can ask directly in the chat." } } },
                            { "agent_name", new Dictionary<string, object> { { "type", "string" }, { "description", "Optional agent name for display (e.g. 'Cursor', 'Antigravity')" } } }
                        }
                    }
                }
            }
        });

        // 2. iclock_timer
        list.Add(new Dictionary<string, object>
        {
            { "name", "iclock_timer" },
            { "description", "Control the desktop countdown timer (start, pause, resume, reset) with optional task label." },
            { "inputSchema", new Dictionary<string, object>
                {
                    { "type", "object" },
                    { "properties", new Dictionary<string, object>
                        {
                            { "action", new Dictionary<string, object> { { "type", "string" }, { "enum", new string[] { "start", "pause", "resume", "reset" } }, { "description", "Action to perform" } } },
                            { "minutes", new Dictionary<string, object> { { "type", "integer" }, { "description", "Countdown duration in minutes (for start action, default: 25)" } } },
                            { "label", new Dictionary<string, object> { { "type", "string" }, { "description", "Optional task name or label" } } }
                        }
                    },
                    { "required", new string[] { "action" } }
                }
            }
        });

        // 3. iclock_notify
        list.Add(new Dictionary<string, object>
        {
            { "name", "iclock_notify" },
            { "description", "Display an immediate desktop popup notification and sound chime to alert the user." },
            { "inputSchema", new Dictionary<string, object>
                {
                    { "type", "object" },
                    { "properties", new Dictionary<string, object>
                        {
                            { "message", new Dictionary<string, object> { { "type", "string" }, { "description", "Notification message content" } } },
                            { "sound", new Dictionary<string, object> { { "type", "string" }, { "enum", new string[] { "agent", "doorbell", "telephone", "qq", "knock", "alarm", "none" } }, { "description", "Chime sound style: agent (default, gentle AI chime), doorbell, telephone, qq, knock, alarm, or none" } } },
                            { "agent_name", new Dictionary<string, object> { { "type", "string" }, { "description", "Optional agent name projecting this notification (e.g. 'Cursor', 'Antigravity')" } } }
                        }
                    },
                    { "required", new string[] { "message" } }
                }
            }
        });

        // 4. iclock_quote
        list.Add(new Dictionary<string, object>
        {
            { "name", "iclock_quote" },
            { "description", "Display, cycle, add, or clear inspirational quotes / task sticky notes on iClock's desktop HUD and quotes library." },
            { "inputSchema", new Dictionary<string, object>
                {
                    { "type", "object" },
                    { "properties", new Dictionary<string, object>
                        {
                            { "action", new Dictionary<string, object> { { "type", "string" }, { "enum", new string[] { "show", "add", "next", "clear", "hide" } }, { "description", "Action to perform: 'show' (default) displays on desktop card; 'add' adds quote(s) permanently; 'next' cycles to next quote; 'clear' or 'hide' removes current desktop note." } } },
                            { "text", new Dictionary<string, object> { { "type", "string" }, { "description", "Quote text or sticky note content" } } },
                            { "author", new Dictionary<string, object> { { "type", "string" }, { "description", "Author or attribution (e.g. 'Steve Jobs')" } } },
                            { "agent_name", new Dictionary<string, object> { { "type", "string" }, { "description", "Optional agent name projecting this sticky note (e.g. 'Cursor', 'Antigravity')" } } },
                            { "quotes", new Dictionary<string, object> { { "type", "array" }, { "items", new Dictionary<string, object> { { "type", "string" } } }, { "description", "Optional list of multiple quotes in 'Quote text | Author' format to add in batch (when action is 'add')." } } }
                        }
                    }
                }
            }
        });

        // 5. iclock_status
        list.Add(new Dictionary<string, object>
        {
            { "name", "iclock_status" },
            { "description", "Query current iClock status (running state, remaining seconds, duration settings, active label, and progress)." },
            { "inputSchema", new Dictionary<string, object>
                {
                    { "type", "object" },
                    { "properties", new Dictionary<string, object>() }
                }
            }
        });

        // 6. iclock_progress
        list.Add(new Dictionary<string, object>
        {
            { "name", "iclock_progress" },
            { "description", "Report task progress to iClock's desktop HUD (renders an ultra-thin hairline progress bar on the floating clock and shows task steps on hover). Use 'clear' to remove the progress bar." },
            { "inputSchema", new Dictionary<string, object>
                {
                    { "type", "object" },
                    { "properties", new Dictionary<string, object>
                        {
                            { "progress", new Dictionary<string, object> { { "type", "string" }, { "description", "Progress value: percentage (e.g. '60' or '60%'), fraction (e.g. '3/5'), float (e.g. '0.6'), or 'clear' to dismiss." } } },
                            { "message", new Dictionary<string, object> { { "type", "string" }, { "description", "Brief status description of the current step (displayed on hover)." } } }
                        }
                    },
                    { "required", new string[] { "progress" } }
                }
            }
        });

        return list;
    }

    private static bool IsValidProgress(string val, out float parsed)
    {
        parsed = 0f;
        if (string.IsNullOrEmpty(val)) return false;
        val = val.Trim();
        if (val.Equals("clear", StringComparison.OrdinalIgnoreCase) ||
            val.Equals("off", StringComparison.OrdinalIgnoreCase) ||
            val.Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (val.Contains("/"))
        {
            string[] parts = val.Split('/');
            float num, den;
            if (parts.Length == 2 &&
                float.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out num) &&
                float.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out den) &&
                den > 0f)
            {
                parsed = num / den;
                return true;
            }
            return false;
        }
        string clean = val.Replace("%", "").Trim();
        float raw;
        if (float.TryParse(clean, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out raw))
        {
            if (val.Contains("%") || raw > 1.0f) parsed = raw / 100.0f;
            else parsed = raw;
            return true;
        }
        return false;
    }

    private static string JsonOk(Dictionary<string, object> data = null)
    {
        var d = data ?? new Dictionary<string, object>();
        if (!d.ContainsKey("status")) d["status"] = "ok";
        return new JavaScriptSerializer().Serialize(d);
    }

    private static string JsonError(string message)
    {
        var d = new Dictionary<string, object>
        {
            { "status", "error" },
            { "message", message ?? "Unknown error" }
        };
        return new JavaScriptSerializer().Serialize(d);
    }

    private static string ExecuteToolCall(string toolName, Dictionary<string, object> args)
    {
        if (toolName == "iclock_ask_human")
        {
            string action = args.ContainsKey("action") ? (args["action"] as string ?? "").ToLowerInvariant() : "";
            if (action == "cancel" || action == "close")
            {
                SendIpc("ask_human_cancel");
                var cancelResp = new Dictionary<string, object>
                {
                    { "status", "ok" },
                    { "action", "cancel" },
                    { "message", "已通知桌面关闭人机审批弹窗" }
                };
                return new JavaScriptSerializer().Serialize(cancelResp);
            }

            bool bgOnly = false;
            if (args.ContainsKey("background_only") && args["background_only"] != null)
            {
                bool.TryParse(args["background_only"].ToString(), out bgOnly);
            }

            if (bgOnly && IsAgentInForeground())
            {
                return "{\"status\":\"in_foreground\",\"in_foreground\":true,\"action\":\"ask_in_chat\",\"message\":\"User is actively viewing your window. Please ask your question directly in the chat window.\"}";
            }

            string question = args.ContainsKey("question") ? args["question"] as string : "请确认：";
            int timeoutSec = 0;
            if (args.ContainsKey("timeout_seconds"))
            {
                int t;
                if (int.TryParse(args["timeout_seconds"].ToString(), out t) && t > 0) timeoutSec = t;
            }

            List<string> optionsList = new List<string>();
            if (args.ContainsKey("options"))
            {
                IEnumerable opts = args["options"] as IEnumerable;
                if (opts != null)
                {
                    foreach (object o in opts)
                    {
                        if (o != null) optionsList.Add(o.ToString());
                    }
                }
            }

            string reqId = Guid.NewGuid().ToString("N").Substring(0, 8);
            string resFile = Path.Combine(Path.GetTempPath(), "iclock_ask_" + reqId + ".txt");
            string optStr = string.Join("~", optionsList.ToArray());

            string agentName = null;
            if (args.ContainsKey("agent_name") && args["agent_name"] != null) agentName = args["agent_name"].ToString();
            else if (args.ContainsKey("agent") && args["agent"] != null) agentName = args["agent"].ToString();
            else if (!string.IsNullOrEmpty(mcpClientName)) agentName = mcpClientName;

            string cmd = "ask_human:" + reqId + "|" + timeoutSec + "|" + question + "|" + optStr + "|" + (agentName ?? "");
            SendIpc(cmd);

            int maxWaitMs = timeoutSec > 0 ? (timeoutSec + 5) * 1000 : int.MaxValue;
            int waited = 0;
            while (waited < maxWaitMs)
            {
                Thread.Sleep(100);
                if (timeoutSec > 0) waited += 100;
                if (File.Exists(resFile))
                {
                    try
                    {
                        string result = File.ReadAllText(resFile, Encoding.UTF8).Trim();
                        try { File.Delete(resFile); } catch { }
                        bool isCancelled = (result == "[用户取消 / Cancelled]" || result == "用户取消");
                        var resp = new Dictionary<string, object>
                        {
                            { "status", isCancelled ? "cancelled" : "ok" },
                            { "action", isCancelled ? "cancelled" : "responded" },
                            { "result", result },
                            { "cancelled", isCancelled },
                            { "timed_out", false }
                        };
                        return new JavaScriptSerializer().Serialize(resp);
                    }
                    catch { }
                }
            }
            var timeoutResp = new Dictionary<string, object>
            {
                { "status", "timeout" },
                { "action", "timeout" },
                { "message", "等待用户决策超时（" + timeoutSec + "秒未响应）" },
                { "cancelled", false },
                { "timed_out", true }
            };
            return new JavaScriptSerializer().Serialize(timeoutResp);
        }
        else if (toolName == "iclock_timer")
        {
            string action = args.ContainsKey("action") ? (args["action"] as string ?? "start").ToLowerInvariant() : "start";
            int minutes = 25;
            if (args.ContainsKey("minutes"))
            {
                int m;
                if (int.TryParse(args["minutes"].ToString(), out m) && m > 0) minutes = m;
            }
            string label = args.ContainsKey("label") ? args["label"] as string : null;

            if (action == "start")
            {
                SendIpc("start " + minutes);
                if (!string.IsNullOrEmpty(label))
                {
                    SendIpc("timer_label:" + label);
                    SendIpc("notice:none|" + label);
                }
                var resp = new Dictionary<string, object>
                {
                    { "status", "ok" },
                    { "action", "start" },
                    { "minutes", minutes },
                    { "label", label ?? "" },
                    { "message", "已为用户启动倒计时：" + minutes + " 分钟" + (!string.IsNullOrEmpty(label) ? "（" + label + "）" : "") }
                };
                return new JavaScriptSerializer().Serialize(resp);
            }
            else if (action == "pause")
            {
                SendIpc("pause");
                return JsonOk(new Dictionary<string, object> { { "action", "pause" }, { "message", "已暂停倒计时" } });
            }
            else if (action == "resume")
            {
                SendIpc("resume");
                return JsonOk(new Dictionary<string, object> { { "action", "resume" }, { "message", "已恢复倒计时" } });
            }
            else if (action == "reset")
            {
                SendIpc("reset");
                return JsonOk(new Dictionary<string, object> { { "action", "reset" }, { "message", "已重置倒计时为初始就绪状态" } });
            }
            return JsonError("未知操作：" + action);
        }
        else if (toolName == "iclock_notify")
        {
            string message = args.ContainsKey("message") ? args["message"] as string : "任务完成";
            string sound = args.ContainsKey("sound") ? args["sound"] as string : "agent";
            string agentName = null;
            if (args.ContainsKey("agent_name") && args["agent_name"] != null) agentName = args["agent_name"].ToString();
            else if (args.ContainsKey("agent") && args["agent"] != null) agentName = args["agent"].ToString();
            else if (!string.IsNullOrEmpty(mcpClientName)) agentName = mcpClientName;

            SendIpc("notice:" + sound + "|" + message + (!string.IsNullOrEmpty(agentName) ? ("|" + agentName) : ""));
            var resp = new Dictionary<string, object>
            {
                { "status", "ok" },
                { "message", message },
                { "sound", sound },
                { "agent_name", agentName ?? "" }
            };
            return new JavaScriptSerializer().Serialize(resp);
        }
        else if (toolName == "iclock_quote")
        {
            string action = args.ContainsKey("action") ? (args["action"] as string ?? "show").ToLowerInvariant() : "show";
            if (string.IsNullOrEmpty(action)) action = "show";

            if (action == "clear" || action == "hide")
            {
                SendIpc("quote:clear");
                var resp = new Dictionary<string, object>
                {
                    { "status", "ok" },
                    { "action", "clear" },
                    { "message", "已清除桌面便签投射" }
                };
                return new JavaScriptSerializer().Serialize(resp);
            }
            else if (action == "next")
            {
                SendIpc("quote");
                var resp = new Dictionary<string, object>
                {
                    { "status", "ok" },
                    { "action", "next" },
                    { "message", "已为用户切换至下一句桌面格言" }
                };
                return new JavaScriptSerializer().Serialize(resp);
            }
            else if (action == "add")
            {
                List<string> quotesToAdd = new List<string>();
                if (args.ContainsKey("quotes") && args["quotes"] is IEnumerable)
                {
                    foreach (object item in (IEnumerable)args["quotes"])
                    {
                        if (item == null) continue;
                        if (item is string)
                        {
                            string s = (string)item;
                            if (!string.IsNullOrEmpty(s.Trim())) quotesToAdd.Add(s.Trim());
                        }
                        else if (item is Dictionary<string, object>)
                        {
                            Dictionary<string, object> d = (Dictionary<string, object>)item;
                            string qText = d.ContainsKey("text") ? d["text"] as string : "";
                            string qAuth = d.ContainsKey("author") ? d["author"] as string : "";
                            if (!string.IsNullOrEmpty(qText))
                            {
                                quotesToAdd.Add(string.IsNullOrEmpty(qAuth) ? qText : (qText + " | " + qAuth));
                            }
                        }
                    }
                }
                if (quotesToAdd.Count == 0)
                {
                    string singleText = args.ContainsKey("text") ? args["text"] as string : null;
                    string singleAuthor = args.ContainsKey("author") ? args["author"] as string : null;
                    if (!string.IsNullOrEmpty(singleText))
                    {
                        quotesToAdd.Add(string.IsNullOrEmpty(singleAuthor) ? singleText : (singleText + " | " + singleAuthor));
                    }
                }

                if (quotesToAdd.Count == 0)
                {
                    return JsonError("未提供任何有效格言内容，请传入 text 或 quotes 参数");
                }

                string batchId = Guid.NewGuid().ToString("N").Substring(0, 8);
                string batchFile = Path.Combine(Path.GetTempPath(), "iclock_quote_batch_" + batchId + ".txt");
                File.WriteAllLines(batchFile, quotesToAdd.ToArray(), Encoding.UTF8);

                bool ipcSent = SendIpc("quote_add:" + batchFile);
                int addedCount = quotesToAdd.Count;

                if (ipcSent)
                {
                    string resFile = batchFile + ".res";
                    int waited = 0;
                    while (waited < 2000)
                    {
                        Thread.Sleep(50);
                        waited += 50;
                        if (File.Exists(resFile))
                        {
                            try
                            {
                                string rc = File.ReadAllText(resFile, Encoding.UTF8).Trim();
                                int parsed;
                                if (int.TryParse(rc, out parsed)) addedCount = parsed;
                                File.Delete(resFile);
                            }
                            catch { }
                            break;
                        }
                    }
                }
                else
                {
                    // Fallback direct append if iClock.exe is not running
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    string quotesPath = Path.Combine(appData, "iClock", "quotes.txt");
                    try
                    {
                        string dir = Path.GetDirectoryName(quotesPath);
                        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                        int nextIdx = 1;
                        if (File.Exists(quotesPath))
                        {
                            try { File.Copy(quotesPath, quotesPath + ".bak", true); } catch { }
                            string[] existingLines = File.ReadAllLines(quotesPath, Encoding.UTF8);
                            int maxNum = 0, quoteCnt = 0;
                            foreach (string line in existingLines)
                            {
                                string trim = line.Trim();
                                if (string.IsNullOrEmpty(trim) || trim.StartsWith("#") || trim.StartsWith("//")) continue;
                                quoteCnt++;
                                Match m = Regex.Match(trim, @"^(?:(?:No\.?|#)\s*|[\(\[\（\【])?(\d{1,6})");
                                if (m.Success)
                                {
                                    int n;
                                    if (int.TryParse(m.Groups[1].Value, out n)) maxNum = Math.Max(maxNum, n);
                                }
                            }
                            nextIdx = Math.Max(maxNum, quoteCnt) + 1;
                        }
                        List<string> formattedLines = new List<string>();
                        foreach (string q in quotesToAdd)
                        {
                            string cleanQ = Regex.Replace(q.Trim(), @"^(?:(?:No\.?|#)\s*\d+[:：\.、\-—]?|\d+[\.、](?!\d)|\d+[：:、\)）]|\(\d+\)|\[\d+\])\s*", "");
                            string[] qParts = cleanQ.Split(new string[] { " | ", "|", " —— ", "——" }, StringSplitOptions.RemoveEmptyEntries);
                            string textPart = qParts.Length > 0 ? qParts[0].Trim() : cleanQ;
                            string authPart = qParts.Length > 1 ? qParts[1].Trim() : "";
                            string finalLine = nextIdx + ". " + textPart + (string.IsNullOrEmpty(authPart) ? "" : " —— " + authPart);
                            formattedLines.Add(finalLine);
                            nextIdx++;
                        }
                        if (File.Exists(quotesPath))
                        {
                            string curText = File.ReadAllText(quotesPath, Encoding.UTF8);
                            if (!string.IsNullOrEmpty(curText) && !curText.EndsWith("\n") && !curText.EndsWith("\r"))
                            {
                                File.AppendAllText(quotesPath, Environment.NewLine, Encoding.UTF8);
                            }
                        }
                        File.AppendAllLines(quotesPath, formattedLines.ToArray(), Encoding.UTF8);
                    }
                    catch { }
                }

                try { if (File.Exists(batchFile)) File.Delete(batchFile); } catch { }

                var resp = new Dictionary<string, object>
                {
                    { "status", "ok" },
                    { "action", "add" },
                    { "added_count", addedCount },
                    { "message", "已成功为用户收录 " + addedCount + " 条格言至 iClock 词库（已去重并自动创建 .bak 备份），并已即时刷新桌面！" }
                };
                return new JavaScriptSerializer().Serialize(resp);
            }
            else // show
            {
                string text = args.ContainsKey("text") ? args["text"] as string : null;
                string author = args.ContainsKey("author") ? args["author"] as string : null;
                if (!string.IsNullOrEmpty(text))
                {
                    string agentName = null;
                    if (args.ContainsKey("agent_name") && args["agent_name"] != null) agentName = args["agent_name"].ToString();
                    else if (args.ContainsKey("agent") && args["agent"] != null) agentName = args["agent"].ToString();
                    else if (!string.IsNullOrEmpty(mcpClientName)) agentName = mcpClientName;

                    int pid = 0;
                    if (!string.IsNullOrEmpty(mcpClientName))
                    {
                        try { pid = Process.GetCurrentProcess().Id; } catch { }
                    }

                    SendIpc("quote:" + text + "|" + (author ?? "") + "|" + (agentName ?? "") + "|" + pid);
                    var resp = new Dictionary<string, object>
                    {
                        { "status", "ok" },
                        { "action", "show" },
                        { "text", text },
                        { "author", author ?? "" },
                        { "agent_name", agentName ?? "" },
                        { "message", "已在桌面置顶投射便签：" + text }
                    };
                    return new JavaScriptSerializer().Serialize(resp);
                }
                else
                {
                    SendIpc("quote");
                    return JsonOk(new Dictionary<string, object> { { "action", "next" }, { "message", "已为用户切换至下一句桌面格言" } });
                }
            }
        }
        else if (toolName == "iclock_status")
        {
            string reqId = Guid.NewGuid().ToString("N").Substring(0, 8);
            string statFile = Path.Combine(Path.GetTempPath(), "iclock_stat_" + reqId + ".txt");
            SendIpc("status:" + statFile);

            for (int i = 0; i < 20; i++)
            {
                Thread.Sleep(50);
                if (File.Exists(statFile))
                {
                    try
                    {
                        string content = File.ReadAllText(statFile, Encoding.UTF8).Trim();
                        try { File.Delete(statFile); } catch { }
                        return content;
                    }
                    catch { }
                }
            }

            IntPtr hWnd = FindWindow(null, "iClock.IpcWindow");
            return hWnd != IntPtr.Zero
                ? "{\"running\":false,\"status\":\"idle\",\"resident\":true}"
                : "{\"running\":false,\"status\":\"stopped\",\"resident\":false}";
        }
        else if (toolName == "iclock_progress")
        {
            string progress = args.ContainsKey("progress") ? (args["progress"] != null ? args["progress"].ToString() : "clear") : "clear";
            string message = args.ContainsKey("message") ? (args["message"] as string ?? "") : "";
            if (string.Equals(progress, "clear", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(progress, "off", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(progress, "reset", StringComparison.OrdinalIgnoreCase))
            {
                SendIpc("progress:clear");
                var resp = new Dictionary<string, object>
                {
                    { "status", "ok" },
                    { "action", "clear" },
                    { "message", "已清除桌面进度条" }
                };
                return new JavaScriptSerializer().Serialize(resp);
            }

            float parsedPct;
            if (!IsValidProgress(progress, out parsedPct))
            {
                return JsonError("无效的进度格式，支持百分比(如 '60%')、分数(如 '3/5')、小数(如 '0.6')或 'clear'");
            }

            string payload = !string.IsNullOrEmpty(message) ? (progress + "|" + message) : progress;
            SendIpc("progress:" + payload);
            var updateResp = new Dictionary<string, object>
            {
                { "status", "ok" },
                { "action", "update" },
                { "progress", progress },
                { "percentage", Math.Round(parsedPct, 4) },
                { "message", message }
            };
            return new JavaScriptSerializer().Serialize(updateResp);
        }

        return JsonError("Unknown tool: " + toolName);
    }
}
