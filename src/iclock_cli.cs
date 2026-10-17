using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
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
            { "description", "Pop up an interactive decision card on the user's desktop to ask a question, request approval, or get choice input (Human-in-the-Loop)." },
            { "inputSchema", new Dictionary<string, object>
                {
                    { "type", "object" },
                    { "properties", new Dictionary<string, object>
                        {
                            { "question", new Dictionary<string, object> { { "type", "string" }, { "description", "The question, prompt, or approval details to display" } } },
                            { "options", new Dictionary<string, object> { { "type", "array" }, { "items", new Dictionary<string, object> { { "type", "string" } } }, { "description", "List of clickable option buttons (e.g. ['Approve', 'Cancel'] or ['Option A', 'Option B']). If omitted, a text input box is presented." } } },
                            { "timeout_seconds", new Dictionary<string, object> { { "type", "integer" }, { "description", "Optional auto-timeout duration in seconds (default: 0 = infinite wait, never times out). Only set when running in unattended automated scripts." } } },
                            { "background_only", new Dictionary<string, object> { { "type", "boolean" }, { "description", "Optional. If true, only pops up desktop card when the agent host window is in the background. If the user is actively focusing the agent window, immediately returns status in_foreground so the agent can ask directly in the chat." } } }
                        }
                    },
                    { "required", new string[] { "question" } }
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
                            { "sound", new Dictionary<string, object> { { "type", "string" }, { "enum", new string[] { "telephone", "qq", "alarm", "none" } }, { "description", "Chime sound style (default: telephone)" } } }
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
            { "description", "Display, cycle, or add inspirational quotes / task sticky notes into iClock's desktop HUD and quotes library." },
            { "inputSchema", new Dictionary<string, object>
                {
                    { "type", "object" },
                    { "properties", new Dictionary<string, object>
                        {
                            { "action", new Dictionary<string, object> { { "type", "string" }, { "enum", new string[] { "show", "add", "next" } }, { "description", "Action to perform: 'show' (default) displays on desktop card; 'add' adds quote(s) permanently into the user's quotes library; 'next' cycles to next quote." } } },
                            { "text", new Dictionary<string, object> { { "type", "string" }, { "description", "Quote text or sticky note content" } } },
                            { "author", new Dictionary<string, object> { { "type", "string" }, { "description", "Author or attribution (e.g. 'Steve Jobs')" } } },
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
            { "description", "Query current iClock status (running state, remaining seconds, duration settings)." },
            { "inputSchema", new Dictionary<string, object>
                {
                    { "type", "object" },
                    { "properties", new Dictionary<string, object>() }
                }
            }
        });

        return list;
    }

    private static string ExecuteToolCall(string toolName, Dictionary<string, object> args)
    {
        if (toolName == "iclock_ask_human")
        {
            bool bgOnly = false;
            if (args.ContainsKey("background_only") && args["background_only"] != null)
            {
                bool.TryParse(args["background_only"].ToString(), out bgOnly);
            }

            if (bgOnly && IsAgentInForeground())
            {
                return "{\"in_foreground\":true,\"action\":\"ask_in_chat\",\"message\":\"User is actively viewing your window. Please ask your question directly in the chat window.\"}";
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

            string cmd = "ask_human:" + reqId + "|" + timeoutSec + "|" + question + "|" + optStr;
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
                        return "用户已响应：" + result;
                    }
                    catch { }
                }
            }
            return "等待用户决策超时（" + timeoutSec + "秒未响应）";
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
                    SendIpc("notice:none|" + label);
                }
                return "已为用户启动倒计时：" + minutes + " 分钟" + (!string.IsNullOrEmpty(label) ? "（" + label + "）" : "");
            }
            else if (action == "pause")
            {
                SendIpc("pause");
                return "已暂停倒计时";
            }
            else if (action == "resume")
            {
                SendIpc("resume");
                return "已恢复倒计时";
            }
            else if (action == "reset")
            {
                SendIpc("reset");
                return "已重置倒计时为初始就绪状态";
            }
            return "未知操作：" + action;
        }
        else if (toolName == "iclock_notify")
        {
            string message = args.ContainsKey("message") ? args["message"] as string : "任务完成";
            string sound = args.ContainsKey("sound") ? args["sound"] as string : "telephone";
            SendIpc("notice:" + sound + "|" + message);
            return "已在用户桌面弹出提醒卡片：" + message;
        }
        else if (toolName == "iclock_quote")
        {
            string action = args.ContainsKey("action") ? args["action"] as string : "show";
            if (string.IsNullOrEmpty(action)) action = "show";

            if (action == "next")
            {
                SendIpc("quote");
                return "已为用户切换至下一句桌面格言";
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
                    return "未提供任何有效格言内容，请传入 text 或 quotes 参数";
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
                        if (File.Exists(quotesPath))
                        {
                            try { File.Copy(quotesPath, quotesPath + ".bak", true); } catch { }
                        }
                        File.AppendAllLines(quotesPath, quotesToAdd.ToArray(), Encoding.UTF8);
                    }
                    catch { }
                }

                try { if (File.Exists(batchFile)) File.Delete(batchFile); } catch { }

                return "已成功为用户收录 " + addedCount + " 条格言至 iClock 词库（已去重并自动创建 .bak 备份），并已即时刷新桌面！";
            }
            else // show
            {
                string text = args.ContainsKey("text") ? args["text"] as string : null;
                string author = args.ContainsKey("author") ? args["author"] as string : null;
                if (!string.IsNullOrEmpty(text))
                {
                    SendIpc("quote:" + text + "|" + (author ?? ""));
                    return "已在桌面展示自定义格言/寄语卡片：" + text;
                }
                else
                {
                    SendIpc("quote");
                    return "已为用户切换至下一句桌面格言";
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

        return "Unknown tool: " + toolName;
    }
}
