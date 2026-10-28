using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

internal sealed class AgentSessionWatcher : IDisposable
{
    private readonly AppContext appContext;
    private readonly List<FileSystemWatcher> watchers = new List<FileSystemWatcher>();
    private readonly Dictionary<string, System.Threading.Timer> fileDebounceTimers = new Dictionary<string, System.Threading.Timer>(StringComparer.OrdinalIgnoreCase);
    private readonly JavaScriptSerializer jsonSerializer = new JavaScriptSerializer();
    private readonly object syncLock = new object();
    private enum TurnState { Idle, Processing, Waiting, Completed }

    private sealed class TurnSession
    {
        internal string Target = "";
        internal DateTime StartTime = DateTime.MinValue;
        internal TurnState State = TurnState.Idle;
        internal int ToolCount = 0;
        internal DateTime WindowStart = DateTime.MinValue;
        internal int Version = 0;
        internal int ActiveStep = 0;
        internal int TotalSteps = 0;
        internal string StepDescription = "";
        internal string LastToolAction = "";
        internal string ProjectHint = "";
    }

    private readonly Dictionary<string, TurnSession> sessions = new Dictionary<string, TurnSession>(StringComparer.OrdinalIgnoreCase);
    private bool disposed;

    internal AgentSessionWatcher(AppContext appContext)
    {
        this.appContext = appContext;
        InitDefaultWatchers();
    }

    private void InitDefaultWatchers()
    {
        try
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            // 1. Claude Code session projects directory
            string claudeProjects = Path.Combine(userProfile, ".claude", "projects");
            if (Directory.Exists(claudeProjects))
            {
                WatchDirectory(claudeProjects, "*.jsonl", "Claude Code");
            }
            else
            {
                string claudeRoot = Path.Combine(userProfile, ".claude");
                if (Directory.Exists(claudeRoot))
                {
                    WatchDirectory(claudeRoot, "*.jsonl", "Claude Code");
                }
            }

            // 2. Antigravity session brain logs directory
            string antigravityBrain = Path.Combine(userProfile, ".gemini", "antigravity", "brain");
            if (Directory.Exists(antigravityBrain))
            {
                WatchDirectory(antigravityBrain, "*.jsonl", "Antigravity");
            }

            // 3. Local workspace .claude if present
            try
            {
                string localClaude = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".claude");
                if (Directory.Exists(localClaude))
                {
                    WatchDirectory(localClaude, "*.jsonl", "Claude Code");
                }
            }
            catch { }

            // 4. Scan recent active sessions immediately on startup
            ScanRecentActiveSessions();
        }
        catch { }
    }

    private void ScanRecentActiveSessions()
    {
        System.Threading.ThreadPool.QueueUserWorkItem(delegate
        {
            try
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                // 1. Claude Code recent session
                string claudeProjects = Path.Combine(userProfile, ".claude", "projects");
                if (Directory.Exists(claudeProjects))
                {
                    string newestFile = null;
                    DateTime newestTime = DateTime.MinValue;
                    foreach (string f in Directory.GetFiles(claudeProjects, "*.jsonl", SearchOption.AllDirectories))
                    {
                        DateTime mt = File.GetLastWriteTime(f);
                        if (mt > newestTime)
                        {
                            newestTime = mt;
                            newestFile = f;
                        }
                    }
                    if (newestFile != null && (DateTime.Now - newestTime).TotalMinutes < 30)
                    {
                        ProcessLogFile(newestFile, "Claude Code");
                    }
                }

                // 2. Antigravity recent session
                string agBrain = Path.Combine(userProfile, ".gemini", "antigravity", "brain");
                if (Directory.Exists(agBrain))
                {
                    string newestFile = null;
                    DateTime newestTime = DateTime.MinValue;
                    foreach (string f in Directory.GetFiles(agBrain, "transcript.jsonl", SearchOption.AllDirectories))
                    {
                        DateTime mt = File.GetLastWriteTime(f);
                        if (mt > newestTime)
                        {
                            newestTime = mt;
                            newestFile = f;
                        }
                    }
                    if (newestFile != null && (DateTime.Now - newestTime).TotalMinutes < 30)
                    {
                        ProcessLogFile(newestFile, "Antigravity");
                    }
                }
            }
            catch { }
        });
    }

    internal void WatchDirectory(string dirPath, string filter, string agentName)
    {
        if (string.IsNullOrEmpty(dirPath) || !Directory.Exists(dirPath)) return;
        try
        {
            FileSystemWatcher fsw = new FileSystemWatcher(dirPath, filter);
            fsw.IncludeSubdirectories = true;
            fsw.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size;
            fsw.Changed += delegate(object s, FileSystemEventArgs e) { OnFileChanged(e.FullPath, agentName); };
            fsw.Created += delegate(object s, FileSystemEventArgs e) { OnFileChanged(e.FullPath, agentName); };
            fsw.EnableRaisingEvents = true;
            lock (syncLock)
            {
                watchers.Add(fsw);
            }
        }
        catch { }
    }

    private void OnFileChanged(string fullPath, string agentName)
    {
        if (disposed || string.IsNullOrEmpty(fullPath)) return;

        // Debounce: coalesce rapid successive writes with a 50ms trailing timer
        // guarantees zero dropped tool events and freshest file state
        lock (syncLock)
        {
            if (disposed) return;
            System.Threading.Timer timer;
            if (fileDebounceTimers.TryGetValue(fullPath, out timer))
            {
                try { timer.Change(50, System.Threading.Timeout.Infinite); } catch { }
            }
            else
            {
                string pathCopy = fullPath;
                string agentCopy = agentName;
                timer = new System.Threading.Timer(delegate
                {
                    lock (syncLock)
                    {
                        System.Threading.Timer t;
                        if (fileDebounceTimers.TryGetValue(pathCopy, out t))
                        {
                            try { t.Dispose(); } catch { }
                            fileDebounceTimers.Remove(pathCopy);
                        }
                    }
                    if (!disposed)
                    {
                        try
                        {
                            ProcessLogFile(pathCopy, agentCopy);
                        }
                        catch { }
                    }
                }, null, 50, System.Threading.Timeout.Infinite);
                fileDebounceTimers[fullPath] = timer;
            }
        }
    }

    internal void ProcessLogFile(string fullPath, string agentName)
    {
        if (disposed || !File.Exists(fullPath)) return;

        // Resolve agent name: files with claude belong to Claude Code even if watcher directory was initialized differently
        if (fullPath.IndexOf("claude", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            agentName = "Claude Code";
        }
        else if (string.IsNullOrEmpty(agentName))
        {
            string lowerPath = fullPath.ToLowerInvariant();
            if (lowerPath.Contains(".claude")) agentName = "Claude Code";
            else if (lowerPath.Contains(".gemini") || lowerPath.Contains("antigravity")) agentName = "Antigravity";
            else agentName = "AI Agent";
        }

        // Active CLI overrides passive watcher
        if (appContext != null && appContext.HasRecentCliActivity(agentName)) return;

        List<string> lines = ReadRecentJsonLines(fullPath, 65536);
        if (lines == null || lines.Count == 0) return;

        DispatchParsedEvents(lines, fullPath, agentName);
    }

    internal static List<string> ReadRecentJsonLines(string path, int maxBytes = 65536)
    {
        List<string> result = new List<string>();
        try
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                if (fs.Length == 0) return result;
                long seekLen = Math.Min(fs.Length, maxBytes);
                fs.Seek(-seekLen, SeekOrigin.End);
                byte[] buffer = new byte[seekLen];
                int bytesRead = fs.Read(buffer, 0, (int)seekLen);
                if (bytesRead <= 0) return result;
                string text = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                string[] lines = text.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim().Trim('\uFEFF');
                    if (line.StartsWith("{") && line.EndsWith("}"))
                    {
                        result.Add(line);
                    }
                }
            }
        }
        catch { }
        return result;
    }

    private static List<string> ReadHeadJsonLines(string path, int maxBytes = 8192)
    {
        List<string> result = new List<string>();
        try
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                if (fs.Length == 0) return result;
                long len = Math.Min(fs.Length, maxBytes);
                byte[] buffer = new byte[len];
                int bytesRead = fs.Read(buffer, 0, (int)len);
                if (bytesRead <= 0) return result;
                string text = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                string[] lines = text.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim().Trim('\uFEFF');
                    if (line.StartsWith("{") && line.EndsWith("}"))
                    {
                        result.Add(line);
                    }
                }
            }
        }
        catch { }
        return result;
    }

    internal static string ExtractProjectHint(Dictionary<string, object> data, string filePath)
    {
        if (data != null && data.ContainsKey("cwd") && data["cwd"] != null)
        {
            try
            {
                string cwd = data["cwd"].ToString().Trim().TrimEnd('\\', '/');
                string name = Path.GetFileName(cwd);
                if (!string.IsNullOrEmpty(name)) return name;
            }
            catch { }
        }
        if (data != null && data.ContainsKey("tool_calls"))
        {
            IList calls = data["tool_calls"] as IList;
            if (calls != null && calls.Count > 0)
            {
                Dictionary<string, object> c = calls[0] as Dictionary<string, object>;
                if (c != null)
                {
                    Dictionary<string, object> args = c.ContainsKey("args") ? c["args"] as Dictionary<string, object> : null;
                    if (args != null && args.ContainsKey("Path") && args["Path"] != null)
                    {
                        string p = args["Path"].ToString().Trim('"', '\'');
                        try
                        {
                            string dir = Path.GetDirectoryName(p);
                            if (!string.IsNullOrEmpty(dir))
                            {
                                int idx = dir.IndexOf("AIStudio", StringComparison.OrdinalIgnoreCase);
                                if (idx >= 0)
                                {
                                    string sub = dir.Substring(idx);
                                    string[] parts = sub.Split(new char[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
                                    if (parts.Length >= 2) return parts[1];
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
        }
        if (!string.IsNullOrEmpty(filePath))
        {
            try
            {
                string dir = Path.GetDirectoryName(filePath);
                if (dir.Contains(".claude"))
                {
                    string slug = Path.GetFileName(dir);
                    if (!string.IsNullOrEmpty(slug))
                    {
                        string[] parts = slug.Split(new string[] { "--", "-" }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0) return parts[parts.Length - 1];
                    }
                }
            }
            catch { }
        }
        return null;
    }

    internal static string ReadLastJsonLine(string path)
    {
        List<string> lines = ReadRecentJsonLines(path, 65536);
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            string line = lines[i];
            if (line.Contains("\"atis-latch\"") || line.Contains("\"agent-name\"") || line.Contains("\"api-request-blob\"") || line.Contains("\"stop_hook_summary\""))
                continue;
            if (line.Contains("\"mode\"") && !line.Contains("\"message\""))
                continue;
            return line;
        }
        return lines.Count > 0 ? lines[lines.Count - 1] : null;
    }

    private void DispatchParsedEvents(List<string> lines, string filePath, string agentName)
    {
        if (appContext == null || disposed || lines == null || lines.Count == 0) return;
        bool isZh = appContext.Settings == null || appContext.Settings.Language != "en";

        TurnSession session;
        lock (syncLock)
        {
            if (!sessions.TryGetValue(agentName, out session))
            {
                session = new TurnSession();
                sessions[agentName] = session;
            }
        }

        // 1. Always anchor on the CURRENT TURN user request (scan backwards for latest user prompt)
        string latestReq = null;
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            try
            {
                Dictionary<string, object> d = jsonSerializer.Deserialize<Dictionary<string, object>>(lines[i]);
                string req = ExtractUserRequest(d);
                if (!string.IsNullOrEmpty(req))
                {
                    latestReq = req;
                    break;
                }
            }
            catch { }
        }

        if (!string.IsNullOrEmpty(latestReq))
        {
            lock (syncLock)
            {
                if (!string.Equals(session.Target, latestReq, StringComparison.Ordinal))
                {
                    session.Target = latestReq;
                    session.StartTime = DateTime.Now;
                    session.ToolCount = 0;
                    session.WindowStart = DateTime.Now;
                    session.Version++;
                    session.ActiveStep = 0;
                    session.TotalSteps = 0;
                    session.StepDescription = "";
                    session.LastToolAction = "";
                }
            }
        }
        else if (string.IsNullOrEmpty(session.Target) && !string.IsNullOrEmpty(filePath))
        {
            try
            {
                List<string> head = ReadHeadJsonLines(filePath, 8192);
                for (int i = 0; i < head.Count; i++)
                {
                    Dictionary<string, object> d = jsonSerializer.Deserialize<Dictionary<string, object>>(head[i]);
                    string req = ExtractUserRequest(d);
                    if (!string.IsNullOrEmpty(req))
                    {
                        lock (syncLock)
                        {
                            session.Target = req;
                            session.StartTime = DateTime.Now;
                        }
                        break;
                    }
                }
            }
            catch { }
        }

        // 2. Scan recent lines backwards for step progress / TODO updates and active tool action in this turn
        string turnToolAction = null;
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            string l = lines[i];
            if (l.Contains("\"atis-latch\"") || l.Contains("\"agent-name\"") || l.Contains("\"api-request-blob\"") || l.Contains("\"stop_hook_summary\""))
                continue;
            if (l.Contains("\"mode\"") && !l.Contains("\"message\""))
                continue;
            try
            {
                Dictionary<string, object> d = jsonSerializer.Deserialize<Dictionary<string, object>>(l);
                if (d != null)
                {
                    if (ExtractUserRequest(d) != null)
                    {
                        break;
                    }

                    if (string.IsNullOrEmpty(session.ProjectHint))
                    {
                        string ph = ExtractProjectHint(d, filePath);
                        if (!string.IsNullOrEmpty(ph)) session.ProjectHint = ph;
                    }

                    if (string.IsNullOrEmpty(turnToolAction))
                    {
                        string tSum = ExtractToolSummary(d, isZh);
                        if (!string.IsNullOrEmpty(tSum))
                        {
                            turnToolAction = tSum;
                        }
                    }

                    int s, t;
                    string sd;
                    if (session.TotalSteps == 0 && ExtractStepProgress(d, turnToolAction, out s, out t, out sd))
                    {
                        lock (syncLock)
                        {
                            session.ActiveStep = s;
                            session.TotalSteps = t;
                            session.StepDescription = sd;
                        }
                    }

                    if (!string.IsNullOrEmpty(turnToolAction) && session.TotalSteps > 0)
                    {
                        break;
                    }
                }
            }
            catch { }
        }

        if (!string.IsNullOrEmpty(turnToolAction))
        {
            lock (syncLock)
            {
                session.LastToolAction = turnToolAction;
            }
        }

        // 3. Find latest actionable line
        Dictionary<string, object> latestData = null;
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            string l = lines[i];
            if (l.Contains("\"atis-latch\"") || l.Contains("\"agent-name\"") || l.Contains("\"api-request-blob\"") || l.Contains("\"stop_hook_summary\""))
                continue;
            if (l.Contains("\"mode\"") && !l.Contains("\"message\""))
                continue;
            try
            {
                latestData = jsonSerializer.Deserialize<Dictionary<string, object>>(l);
                if (latestData != null) break;
            }
            catch { }
        }

        if (latestData != null)
        {
            DispatchParsedEvent(latestData, agentName, isZh);
        }
    }

    private void DispatchParsedEvent(Dictionary<string, object> data, string agentName, bool isZh)
    {
        if (appContext == null || disposed) return;

        TurnSession session;
        lock (syncLock)
        {
            if (!sessions.TryGetValue(agentName, out session))
            {
                session = new TurnSession();
                sessions[agentName] = session;
            }
        }

        // 1. Check for Turn Start (USER_INPUT / user request) -> Anchor Session Target
        string userReq = ExtractUserRequest(data);
        if (!string.IsNullOrEmpty(userReq))
        {
            lock (syncLock)
            {
                session.Target = userReq;
                session.StartTime = DateTime.Now;
                session.State = TurnState.Processing;
                session.ToolCount = 0;
                session.WindowStart = DateTime.Now;
                session.Version++;
                session.ActiveStep = 0;
                session.TotalSteps = 0;
                session.StepDescription = "";
                session.LastToolAction = "";
            }
            string msg = "⏳ [" + session.Target + "] " + (isZh ? "处理中..." : "Processing...");
            appContext.SetAgentTaskMessage(msg, agentName, true, null, null, -1f, session.ProjectHint);
            return;
        }

        // Invalidate any pending auto-clear timer on new activity
        lock (syncLock)
        {
            session.Version++;
        }

        // 2. Check for Waiting / HITL question
        string question = ExtractQuestion(data, isZh);
        if (!string.IsNullOrEmpty(question))
        {
            lock (syncLock)
            {
                session.State = TurnState.Waiting;
                session.ToolCount = 0;
                if (string.IsNullOrEmpty(session.Target))
                {
                    session.Target = isZh ? "当前会话" : "Session";
                }
            }
            if (!AppContext.IsAgentWindowInForeground(agentName, session.ProjectHint))
            {
                AppContext.PlayAiSound();
            }
            string msg = "❓ [" + session.Target + "] " + (isZh ? "等待决策" : "Waiting for input");
            appContext.SetAgentTaskMessage(msg, agentName, false, question, null, -2f, session.ProjectHint);
            return;
        }

        // 2b. Check for Error / Abrupt Termination
        string errSummary = ExtractErrorSummary(data, isZh);
        if (!string.IsNullOrEmpty(errSummary))
        {
            lock (syncLock)
            {
                session.State = TurnState.Idle;
                session.ToolCount = 0;
                session.ActiveStep = 0;
                session.TotalSteps = 0;
                session.StepDescription = "";
                session.LastToolAction = "";
                if (string.IsNullOrEmpty(session.Target))
                {
                    session.Target = isZh ? "当前会话" : "Session";
                }
            }
            if (!AppContext.IsAgentWindowInForeground(agentName, session.ProjectHint))
            {
                AppContext.PlayAiSound();
            }
            string msg = "⚠️ [" + session.Target + "] " + (isZh ? "执行异常" : "Error");
            appContext.SetAgentTaskMessage(msg, agentName, false, errSummary, null, -2f, session.ProjectHint);
            return;
        }

        // 3. Check for Completion (Done / Final response)
        string completionSummary = ExtractCompletionSummary(data);
        if (!string.IsNullOrEmpty(completionSummary))
        {
            DateTime now = DateTime.Now;
            int sec = 0;
            string target;
            int currentVer;
            lock (syncLock)
            {
                session.State = TurnState.Completed;
                session.ToolCount = 0;
                session.ActiveStep = 0;
                session.TotalSteps = 0;
                session.StepDescription = "";
                session.LastToolAction = "";
                if (string.IsNullOrEmpty(session.Target))
                {
                    session.Target = isZh ? "当前会话" : "Session";
                }
                target = session.Target;
                if (session.StartTime > DateTime.MinValue)
                {
                    sec = (int)(now - session.StartTime).TotalSeconds;
                }
                currentVer = ++session.Version;
            }

            string timeStr = (sec >= 3) ? (" (" + (sec >= 60 ? (sec / 60 + "m" + sec % 60 + "s") : (sec + "s")) + ")") : "";
            string msg = "✅ [" + target + "] " + (isZh ? "本轮已完成" : "Completed") + timeStr;
            string cleanCompletion = CleanSummaryText(completionSummary);
            appContext.SetAgentTaskMessage(msg, agentName, false, cleanCompletion, null, -2f, session.ProjectHint);
            ScheduleClearProgress(agentName, currentVer, 600000); // Keep completed state visible for 10 minutes so user always sees the result
            return;
        }

        // 4. Processing Phase: Tool calls or Thinking
        // Runaway loop watchdog (> 25 calls in 90s)
        DateTime nowDate = DateTime.Now;
        int count = 1;
        string toolSummary = ExtractToolSummary(data, isZh);

        lock (syncLock)
        {
            if (string.IsNullOrEmpty(toolSummary))
            {
                toolSummary = session.LastToolAction;
            }
            else
            {
                session.LastToolAction = toolSummary;
            }
        }
        lock (syncLock)
        {
            if (session.WindowStart > DateTime.MinValue && (nowDate - session.WindowStart).TotalSeconds < 90)
            {
                count = ++session.ToolCount;
            }
            else
            {
                session.WindowStart = nowDate;
                session.ToolCount = 1;
                count = 1;
            }

            // Fallback session target anchor if user prompt arrived before watcher was attached
            if (string.IsNullOrEmpty(session.Target))
            {
                if (!string.IsNullOrEmpty(toolSummary))
                {
                    session.Target = CleanUserRequest(toolSummary) ?? toolSummary;
                }
                else
                {
                    session.Target = isZh ? "当前任务" : "Task";
                }
                session.StartTime = nowDate;
            }
        }

        if (count >= 60)
        {
            string warn = "⚠️ [" + session.Target + "] " + (isZh ? ("疑似死循环 (" + count + "次)") : ("Runaway loop (" + count + " calls)"));
            appContext.SetAgentTaskMessage(warn, agentName, false, isZh ? "90秒内工具调用频率过高，疑似陷入循环" : "Runaway loop detected (>200 calls in 90s)", null, -2f, session.ProjectHint);
            AppContext.PlayAiSound();
            return;
        }

        // In State 1 (Processing):
        string stepProgressText = null;
        float progress = -1f;
        string activeDesc = null;
        lock (syncLock)
        {
            session.State = TurnState.Processing;
            if (session.ActiveStep > 0 && session.TotalSteps > 0)
            {
                progress = (float)session.ActiveStep / session.TotalSteps;
                activeDesc = session.StepDescription;
                string descPart = !string.IsNullOrEmpty(activeDesc) ? (" " + activeDesc) : "";
                stepProgressText = session.ActiveStep + "/" + session.TotalSteps + descPart;
            }
        }

        string subDetail;
        if (!string.IsNullOrEmpty(toolSummary))
        {
            subDetail = isZh ? ("正在执行: " + toolSummary) : ("Running: " + toolSummary);
        }
        else if (!string.IsNullOrEmpty(stepProgressText))
        {
            subDetail = isZh ? ("正在执行: " + stepProgressText) : ("Running: " + stepProgressText);
        }
        else
        {
            subDetail = null;
        }

        string procMsg = "⏳ [" + session.Target + "] " + (isZh ? "处理中..." : "Processing...");
        appContext.SetAgentTaskMessage(procMsg, agentName, true, subDetail, stepProgressText, progress, session.ProjectHint);
    }

    private void ScheduleClearProgress(string agentName, int expectedVersion, int delayMs)
    {
        System.Threading.ThreadPool.QueueUserWorkItem(delegate
        {
            try
            {
                System.Threading.Thread.Sleep(delayMs);
                lock (syncLock)
                {
                    TurnSession session;
                    if (!sessions.TryGetValue(agentName, out session) || session.Version != expectedVersion)
                    {
                        return;
                    }
                }
                if (!disposed && appContext != null)
                {
                    appContext.ClearAgentProgress(agentName);
                }
            }
            catch { }
        });
    }

    internal static string ExtractUserRequest(Dictionary<string, object> data)
    {
        if (data == null) return null;

        // 1. Antigravity format: type == "USER_INPUT"
        if (data.ContainsKey("type") && "USER_INPUT".Equals(data["type"]))
        {
            if (data.ContainsKey("content") && data["content"] != null)
            {
                string cleaned = CleanUserRequest(data["content"].ToString());
                if (!string.IsNullOrEmpty(cleaned) && !string.Equals(cleaned, "continue", StringComparison.OrdinalIgnoreCase))
                {
                    return cleaned;
                }
            }
        }

        // 2. Claude Code format: type == "last-prompt"
        if (data.ContainsKey("type") && "last-prompt".Equals(data["type"]))
        {
            if (data.ContainsKey("lastPrompt") && data["lastPrompt"] != null)
            {
                string p = data["lastPrompt"].ToString().Trim();
                if (!string.IsNullOrEmpty(p) && !string.Equals(p, "continue", StringComparison.OrdinalIgnoreCase))
                {
                    return CleanUserRequest(p);
                }
            }
        }

        // 3. Claude Code format: type == "user"
        if (data.ContainsKey("type") && "user".Equals(data["type"]))
        {
            if (data.ContainsKey("message"))
            {
                Dictionary<string, object> msg = data["message"] as Dictionary<string, object>;
                if (msg != null && msg.ContainsKey("content"))
                {
                    object rawContent = msg["content"];
                    if (rawContent is string)
                    {
                        string str = rawContent.ToString().Trim();
                        if (!string.IsNullOrEmpty(str) && !str.StartsWith("<local-command") && !string.Equals(str, "continue", StringComparison.OrdinalIgnoreCase))
                        {
                            return CleanUserRequest(str);
                        }
                    }
                    IList content = rawContent as IList;
                    if (content != null && content.Count > 0)
                    {
                        foreach (object item in content)
                        {
                            Dictionary<string, object> part = item as Dictionary<string, object>;
                            if (part != null)
                            {
                                if (part.ContainsKey("type") && "tool_result".Equals(part["type"]))
                                    continue;

                                if (part.ContainsKey("text") && part["text"] != null)
                                {
                                    string text = part["text"].ToString().Trim();
                                    if (!text.StartsWith("<local-command") && !string.Equals(text, "continue", StringComparison.OrdinalIgnoreCase))
                                    {
                                        string t = CleanUserRequest(text);
                                        if (!string.IsNullOrEmpty(t)) return t;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        // 4. Claude Code format: type == "custom-title"
        if (data.ContainsKey("type") && "custom-title".Equals(data["type"]))
        {
            if (data.ContainsKey("customTitle") && data["customTitle"] != null)
            {
                string title = data["customTitle"].ToString().Trim();
                if (!string.IsNullOrEmpty(title))
                {
                    return CleanUserRequest(title);
                }
            }
        }

        return null;
    }


    internal static string CleanUserRequest(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return null;

        // Extract <USER_REQUEST> ... </USER_REQUEST> if present
        Match m = Regex.Match(raw, @"<USER_REQUEST>\s*([\s\S]*?)\s*<\/USER_REQUEST>", RegexOptions.IgnoreCase);
        string text = m.Success ? m.Groups[1].Value : raw;

        // Strip xml/html-like metadata tags
        text = Regex.Replace(text, @"<[^>]+>[\s\S]*?<\/[^>]+>", " ");
        text = Regex.Replace(text, @"<[^>]+>", " ");

        // Normalize whitespace
        text = Regex.Replace(text, @"[\r\n\t]+", " ");
        text = Regex.Replace(text, @"\s+", " ").Trim();

        if (string.IsNullOrEmpty(text)) return null;

        // Strip conversational courtesy openings
        text = Regex.Replace(text, @"^(?:请帮我|帮我|麻烦|我想|请你|能不能|能否|能够|请问|请|Can you|Please|I want to|I would like to|Could you)\s*[,，:：]?\s*", "", RegexOptions.IgnoreCase).Trim();

        if (string.IsNullOrEmpty(text)) return null;

        // Cap at a safe upper bound (200 chars) to prevent extreme memory/IPC bloat;
        // visual truncation with ellipsis is handled dynamically by the UI based on interface width.
        if (text.Length > 200)
        {
            text = text.Substring(0, 200).Trim();
        }

        return text.Trim();
    }

    private static string CleanToolParam(object val)
    {
        if (val == null) return null;
        string s = val.ToString().Trim('"', ' ', '\t', '\r', '\n', '\'', '`');
        return string.IsNullOrEmpty(s) ? null : s;
    }

    private static string ExtractFriendlyAction(string toolName, Dictionary<string, object> args, bool isZh)
    {
        if (string.IsNullOrEmpty(toolName)) return null;

        // 1. Explicit summary from args if present
        if (args != null)
        {
            if (args.ContainsKey("toolSummary"))
            {
                string ts = CleanToolParam(args["toolSummary"]);
                if (!string.IsNullOrEmpty(ts)) return ts;
            }
            if (args.ContainsKey("summary"))
            {
                string s = CleanToolParam(args["summary"]);
                if (!string.IsNullOrEmpty(s)) return s;
            }
            if (args.ContainsKey("description"))
            {
                string d = CleanToolParam(args["description"]);
                if (!string.IsNullOrEmpty(d)) return d;
            }
        }

        string lowerName = toolName.ToLowerInvariant();

        // 2. File operations (read, edit, write, delete)
        string filePath = null;
        if (args != null)
        {
            if (args.ContainsKey("Path")) filePath = CleanToolParam(args["Path"]);
            else if (args.ContainsKey("path")) filePath = CleanToolParam(args["path"]);
            else if (args.ContainsKey("filePath")) filePath = CleanToolParam(args["filePath"]);
            else if (args.ContainsKey("file_path")) filePath = CleanToolParam(args["file_path"]);
            else if (args.ContainsKey("Target")) filePath = CleanToolParam(args["Target"]);
        }

        if (!string.IsNullOrEmpty(filePath))
        {
            string fileName = "";
            try { fileName = Path.GetFileName(filePath); } catch { }
            if (string.IsNullOrEmpty(fileName)) fileName = filePath;
            if (fileName.Length > 24) fileName = fileName.Substring(0, 21) + "...";

            if (lowerName.Contains("read") || lowerName.Contains("view") || lowerName == "cat")
                return isZh ? ("读取 " + fileName) : ("Read " + fileName);
            if (lowerName.Contains("edit") || lowerName.Contains("replace") || lowerName.Contains("modify"))
                return isZh ? ("修改 " + fileName) : ("Edit " + fileName);
            if (lowerName.Contains("write") || lowerName.Contains("create") || lowerName.Contains("save"))
                return isZh ? ("写入 " + fileName) : ("Write " + fileName);
            if (lowerName.Contains("delete") || lowerName.Contains("remove"))
                return isZh ? ("删除 " + fileName) : ("Delete " + fileName);
            return isZh ? ("操作 " + fileName) : ("Process " + fileName);
        }

        // 3. Command execution
        string cmd = null;
        if (args != null)
        {
            if (args.ContainsKey("CommandLine")) cmd = CleanToolParam(args["CommandLine"]);
            else if (args.ContainsKey("command")) cmd = CleanToolParam(args["command"]);
            else if (args.ContainsKey("cmd")) cmd = CleanToolParam(args["cmd"]);
        }
        if (!string.IsNullOrEmpty(cmd))
        {
            cmd = Regex.Replace(cmd, @"^(?:powershell(?:\.exe)?|cmd(?:\.exe)?|bash(?:\.exe)?)\s+(?:-ExecutionPolicy\s+\w+\s+|-File\s+|-c\s+|/c\s+)*", "", RegexOptions.IgnoreCase).Trim();
            if (cmd.Length > 26) cmd = cmd.Substring(0, 23) + "...";
            return isZh ? ("运行: " + cmd) : ("Run: " + cmd);
        }

        // 4. Web search
        string query = null;
        if (args != null)
        {
            if (args.ContainsKey("query")) query = CleanToolParam(args["query"]);
            else if (args.ContainsKey("queries")) query = CleanToolParam(args["queries"]);
            else if (args.ContainsKey("pattern")) query = CleanToolParam(args["pattern"]);
        }
        if (!string.IsNullOrEmpty(query))
        {
            if (query.Length > 26) query = query.Substring(0, 23) + "...";
            return isZh ? ("搜索: " + query) : ("Search: " + query);
        }

        // 5. Subagent / Task delegation
        if (lowerName.Contains("subagent") || lowerName.Contains("task"))
        {
            string role = null;
            if (args != null)
            {
                if (args.ContainsKey("Role")) role = CleanToolParam(args["Role"]);
                else if (args.ContainsKey("TypeName")) role = CleanToolParam(args["TypeName"]);
            }
            if (!string.IsNullOrEmpty(role))
                return isZh ? ("子任务: " + role) : ("Subtask: " + role);
        }

        // 6. Generic friendly name fallbacks
        if (lowerName == "run_command" || lowerName == "bash" || lowerName == "terminal" || lowerName == "exec")
            return isZh ? "运行命令" : "Running command";
        if (lowerName == "read_file" || lowerName == "readlocalfile")
            return isZh ? "读取文件" : "Reading file";
        if (lowerName == "edit_file" || lowerName == "editlocalfile")
            return isZh ? "修改文件" : "Editing file";
        if (lowerName == "write_file" || lowerName == "writefile")
            return isZh ? "写入文件" : "Writing file";
        if (lowerName == "search_web" || lowerName == "web_search")
            return isZh ? "搜索网络" : "Web search";
        if (lowerName == "ask_question" || lowerName == "ask_user_question")
            return isZh ? "询问用户" : "Asking question";

        return toolName;
    }

    internal static string ExtractToolSummary(Dictionary<string, object> data)
    {
        return ExtractToolSummary(data, true);
    }

    internal static string ExtractToolSummary(Dictionary<string, object> data, bool isZh)
    {
        if (data == null) return null;

        // 1. Antigravity / OpenAI tool_calls structure
        if (data.ContainsKey("tool_calls"))
        {
            IList calls = data["tool_calls"] as IList;
            if (calls != null && calls.Count > 0)
            {
                Dictionary<string, object> call = calls[0] as Dictionary<string, object>;
                if (call != null)
                {
                    if (call.ContainsKey("toolSummary") && call["toolSummary"] != null)
                    {
                        string s = CleanToolParam(call["toolSummary"]);
                        if (!string.IsNullOrEmpty(s)) return s;
                    }

                    string name = call.ContainsKey("name") ? CleanToolParam(call["name"]) : null;
                    Dictionary<string, object> args = null;
                    if (call.ContainsKey("args")) args = call["args"] as Dictionary<string, object>;
                    else if (call.ContainsKey("parameters")) args = call["parameters"] as Dictionary<string, object>;
                    else if (call.ContainsKey("input")) args = call["input"] as Dictionary<string, object>;

                    string action = ExtractFriendlyAction(name, args, isZh);
                    if (!string.IsNullOrEmpty(action)) return action;
                }
            }
        }

        // 2. Claude Code content structure
        if (data.ContainsKey("message"))
        {
            Dictionary<string, object> msg = data["message"] as Dictionary<string, object>;
            if (msg != null && msg.ContainsKey("content"))
            {
                IList content = msg["content"] as IList;
                if (content != null)
                {
                    foreach (object item in content)
                    {
                        Dictionary<string, object> part = item as Dictionary<string, object>;
                        if (part != null && part.ContainsKey("type") && "tool_use".Equals(part["type"]))
                        {
                            string name = part.ContainsKey("name") ? CleanToolParam(part["name"]) : null;
                            Dictionary<string, object> input = part.ContainsKey("input") ? part["input"] as Dictionary<string, object> : null;
                            string action = ExtractFriendlyAction(name, input, isZh);
                            if (!string.IsNullOrEmpty(action)) return action;
                        }
                    }
                }
            }
        }

        return null;
    }



    internal static bool ExtractStepProgress(Dictionary<string, object> data, string toolSummary, out int step, out int total, out string desc)
    {
        step = 0;
        total = 0;
        desc = "";

        // 1. Tool summary step pattern: e.g. "1/3 正在执行算法优化", "[1/3] 正在执行算法优化", "Step 1/3: 正在执行算法优化"
        if (!string.IsNullOrEmpty(toolSummary))
        {
            Match m = Regex.Match(toolSummary, @"^(?:\[|\(|\b)(?:Step\s*)?(\d+)\s*(?:[/／]|of)\s*(\d+)(?:\]|\)|\b)\s*[:：·\-\s]*(.*)$", RegexOptions.IgnoreCase);
            if (!m.Success)
            {
                m = Regex.Match(toolSummary, @"^第\s*(\d+)\s*(?:步|阶段)?\s*[/／共]\s*(?:共)?\s*(\d+)\s*(?:步|阶段)?\s*[:：·\-\s]*(.*)$", RegexOptions.IgnoreCase);
            }
            if (m.Success)
            {
                int s, t;
                if (int.TryParse(m.Groups[1].Value, out s) && int.TryParse(m.Groups[2].Value, out t) && t > 0 && s > 0 && s <= t)
                {
                    step = s;
                    total = t;
                    desc = m.Groups[3].Value.Trim();
                    if (string.IsNullOrEmpty(desc)) desc = toolSummary;
                    return true;
                }
            }
        }

        if (data == null) return false;

        // 2. Claude Code TodoWrite / todo tool call
        if (data.ContainsKey("tool_calls"))
        {
            IList calls = data["tool_calls"] as IList;
            if (calls != null)
            {
                foreach (object cObj in calls)
                {
                    Dictionary<string, object> call = cObj as Dictionary<string, object>;
                    if (call == null) continue;
                    string name = call.ContainsKey("name") ? call["name"] as string : null;
                    if (string.Equals(name, "TodoWrite", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "todo", StringComparison.OrdinalIgnoreCase))
                    {
                        Dictionary<string, object> args = null;
                        if (call.ContainsKey("parameters")) args = call["parameters"] as Dictionary<string, object>;
                        if (args == null && call.ContainsKey("input")) args = call["input"] as Dictionary<string, object>;
                        if (args != null && args.ContainsKey("todos"))
                        {
                            IList todos = args["todos"] as IList;
                            if (todos != null && todos.Count > 0)
                            {
                                int activeIdx = -1;
                                string activeDesc = null;
                                for (int i = 0; i < todos.Count; i++)
                                {
                                    Dictionary<string, object> td = todos[i] as Dictionary<string, object>;
                                    if (td == null) continue;
                                    string st = td.ContainsKey("status") ? td["status"] as string : "";
                                    string ct = td.ContainsKey("content") ? td["content"] as string : (td.ContainsKey("task") ? td["task"] as string : "");
                                    if (string.Equals(st, "in_progress", StringComparison.OrdinalIgnoreCase))
                                    {
                                        activeIdx = i;
                                        activeDesc = ct;
                                        break;
                                    }
                                    if (activeIdx == -1 && !string.Equals(st, "completed", StringComparison.OrdinalIgnoreCase))
                                    {
                                        activeIdx = i;
                                        activeDesc = ct;
                                    }
                                }
                                if (activeIdx >= 0)
                                {
                                    step = activeIdx + 1;
                                    total = todos.Count;
                                    desc = CleanSummaryText(activeDesc) ?? (activeDesc ?? "").Trim();
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
        }

        // 3. Markdown checklist in content / parameters / result
        string checkText = null;
        if (data.ContainsKey("content") && data["content"] is string) checkText = data["content"] as string;
        else if (data.ContainsKey("result") && data["result"] is string) checkText = data["result"] as string;

        if (!string.IsNullOrEmpty(checkText) && checkText.Contains("- ["))
        {
            MatchCollection items = Regex.Matches(checkText, @"(?m)^\s*-\s*\[([ xX/])\]\s*(.*)$");
            if (items.Count >= 2)
            {
                int activeIdx = -1;
                string activeDesc = null;
                for (int i = 0; i < items.Count; i++)
                {
                    string mark = items[i].Groups[1].Value;
                    string txt = items[i].Groups[2].Value.Trim();
                    if (mark == "/")
                    {
                        activeIdx = i;
                        activeDesc = txt;
                        break;
                    }
                    if (activeIdx == -1 && (mark == " " || mark == ""))
                    {
                        activeIdx = i;
                        activeDesc = txt;
                    }
                }
                if (activeIdx >= 0)
                {
                    step = activeIdx + 1;
                    total = items.Count;
                    desc = CleanSummaryText(activeDesc) ?? (activeDesc ?? "").Trim();
                    return true;
                }
            }
        }

        return false;
    }




    private static string ExtractQuestionPromptText(Dictionary<string, object> args)
    {
        if (args == null) return null;
        if (args.ContainsKey("question") && args["question"] != null)
        {
            string q = args["question"].ToString().Trim();
            if (!string.IsNullOrEmpty(q)) return q.Length > 50 ? q.Substring(0, 47) + "..." : q;
        }
        if (args.ContainsKey("questions"))
        {
            IList qList = args["questions"] as IList;
            if (qList != null && qList.Count > 0)
            {
                Dictionary<string, object> firstQ = qList[0] as Dictionary<string, object>;
                if (firstQ != null && firstQ.ContainsKey("question") && firstQ["question"] != null)
                {
                    string q = firstQ["question"].ToString().Trim();
                    if (!string.IsNullOrEmpty(q)) return q.Length > 50 ? q.Substring(0, 47) + "..." : q;
                }
            }
        }
        return null;
    }

    internal static string ExtractQuestion(Dictionary<string, object> data, bool isZh = true)
    {
        if (data == null) return null;

        // 1. Tool calls: ask_question / ask_user_question / AskUserQuestion
        if (data.ContainsKey("tool_calls"))
        {
            IList calls = data["tool_calls"] as IList;
            if (calls != null && calls.Count > 0)
            {
                Dictionary<string, object> call = calls[0] as Dictionary<string, object>;
                if (call != null && call.ContainsKey("name"))
                {
                    string name = call["name"] as string;
                    if (string.Equals(name, "ask_question", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(name, "ask_user_question", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(name, "AskUserQuestion", StringComparison.OrdinalIgnoreCase))
                    {
                        Dictionary<string, object> args = call.ContainsKey("args") ? call["args"] as Dictionary<string, object> : (call.ContainsKey("parameters") ? call["parameters"] as Dictionary<string, object> : null);
                        string qText = ExtractQuestionPromptText(args);
                        return !string.IsNullOrEmpty(qText) ? (isZh ? ("请确认: " + qText) : ("Confirm: " + qText)) : (isZh ? "等待用户决策" : "Waiting for user decision");
                    }

                    // Check for Plan / Artifact feedback request
                    if (call.ContainsKey("args"))
                    {
                        Dictionary<string, object> args = call["args"] as Dictionary<string, object>;
                        if (args != null && args.ContainsKey("ArtifactMetadata"))
                        {
                            object meta = args["ArtifactMetadata"];
                            bool reqFb = false;
                            if (meta is Dictionary<string, object>)
                            {
                                object r = ((Dictionary<string, object>)meta).ContainsKey("RequestFeedback") ? ((Dictionary<string, object>)meta)["RequestFeedback"] : null;
                                reqFb = r != null && (Equals(r, true) || string.Equals(r.ToString(), "true", StringComparison.OrdinalIgnoreCase));
                            }
                            else if (meta is string)
                            {
                                reqFb = meta.ToString().IndexOf("\"RequestFeedback\":true", StringComparison.OrdinalIgnoreCase) >= 0 || meta.ToString().IndexOf("\"RequestFeedback\": true", StringComparison.OrdinalIgnoreCase) >= 0;
                            }
                            if (reqFb)
                            {
                                return isZh ? "实施方案已就绪，等待您确认 (Proceed)" : "Plan ready, awaiting Proceed approval";
                            }
                        }
                    }
                }
            }
        }

        // 2. Claude Code content: tool_use AskUserQuestion
        if (data.ContainsKey("message"))
        {
            Dictionary<string, object> msg = data["message"] as Dictionary<string, object>;
            if (msg != null && msg.ContainsKey("content"))
            {
                IList content = msg["content"] as IList;
                if (content != null)
                {
                    foreach (object item in content)
                    {
                        Dictionary<string, object> part = item as Dictionary<string, object>;
                        if (part != null && part.ContainsKey("type") && "tool_use".Equals(part["type"]))
                        {
                            string name = part.ContainsKey("name") ? part["name"] as string : null;
                            if (string.Equals(name, "ask_question", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(name, "ask_user_question", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(name, "AskUserQuestion", StringComparison.OrdinalIgnoreCase))
                            {
                                Dictionary<string, object> input = part.ContainsKey("input") ? part["input"] as Dictionary<string, object> : null;
                                string qText = ExtractQuestionPromptText(input);
                                return !string.IsNullOrEmpty(qText) ? (isZh ? ("请确认: " + qText) : ("Confirm: " + qText)) : (isZh ? "等待用户决策" : "Waiting for user decision");
                            }
                        }
                    }
                }
            }
        }

        // 3. Direct question at the end of completed response
        bool isDoneTurn = (data.ContainsKey("type") && "PLANNER_RESPONSE".Equals(data["type"]) && data.ContainsKey("status") && "DONE".Equals(data["status"]) && !data.ContainsKey("tool_calls"))
            || (data.ContainsKey("type") && "assistant".Equals(data["type"]) && data.ContainsKey("message") && ((data["message"] as Dictionary<string, object>) != null) && "end_turn".Equals(((Dictionary<string, object>)data["message"]).ContainsKey("stop_reason") ? ((Dictionary<string, object>)data["message"])["stop_reason"] : null));

        if (isDoneTurn)
        {
            string text = null;
            if (data.ContainsKey("content") && data["content"] != null) text = data["content"].ToString();
            else if (data.ContainsKey("result") && data["result"] != null) text = data["result"].ToString();
            else if (data.ContainsKey("message"))
            {
                Dictionary<string, object> m = data["message"] as Dictionary<string, object>;
                if (m != null && m.ContainsKey("content") && m["content"] is IList)
                {
                    foreach (object p in (IList)m["content"])
                    {
                        Dictionary<string, object> part = p as Dictionary<string, object>;
                        if (part != null && "text".Equals(part["type"]) && part.ContainsKey("text")) text = part["text"] as string;
                    }
                }
            }
            if (!string.IsNullOrEmpty(text))
            {
                string trimmed = text.Trim().TrimEnd('*', '_', '`', '#', ' ', '\r', '\n', '\t');
                if (trimmed.EndsWith("？") || trimmed.EndsWith("?"))
                {
                    string[] lines = trimmed.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = lines.Length - 1; i >= 0; i--)
                    {
                        string qLine = lines[i].Trim().Trim('#', '*', '-', ' ', '>');
                        if ((qLine.EndsWith("？") || qLine.EndsWith("?")) && qLine.Length >= 4 && qLine.Length <= 100)
                        {
                            if (Regex.IsMatch(qLine, @"[？\?]|(?:吗|呢|如何|是否|还是|好吗|可以吗|确认|推进|实施|选择|觉得|should|would|do you|which|proceed|confirm)\s*[\?？]?", RegexOptions.IgnoreCase))
                            {
                                return isZh ? ("请确认: " + qLine) : ("Confirm: " + qLine);
                            }
                        }
                    }
                }
            }
        }

        return null;
    }

    internal static string ExtractErrorSummary(Dictionary<string, object> data, bool isZh = true)
    {
        if (data == null) return null;
        bool isErr = false;
        string errDetail = null;

        if (data.ContainsKey("status"))
        {
            string st = data["status"] as string;
            if (string.Equals(st, "ERROR", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(st, "FAILED", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(st, "CANCELLED", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(st, "CANCELED", StringComparison.OrdinalIgnoreCase))
            {
                isErr = true;
            }
        }
        if (data.ContainsKey("type") && string.Equals(data["type"] as string, "error", StringComparison.OrdinalIgnoreCase))
        {
            isErr = true;
        }
        if (data.ContainsKey("isApiErrorMessage") && Equals(data["isApiErrorMessage"], true))
        {
            isErr = true;
        }

        if (data.ContainsKey("error") && data["error"] != null)
        {
            string eStr = data["error"].ToString().Trim();
            if (!string.IsNullOrEmpty(eStr))
            {
                isErr = true;
                errDetail = eStr;
            }
        }

        if (!isErr) return null;

        if (string.IsNullOrEmpty(errDetail))
        {
            if (data.ContainsKey("content") && data["content"] != null) errDetail = data["content"].ToString().Trim();
            else if (data.ContainsKey("result") && data["result"] != null) errDetail = data["result"].ToString().Trim();
        }

        if (!string.IsNullOrEmpty(errDetail))
        {
            string cleaned = CleanSummaryText(errDetail) ?? errDetail;
            if (cleaned.StartsWith("✅")) cleaned = cleaned.Substring(1).Trim();
            if (!cleaned.StartsWith("⚠️")) cleaned = "⚠️ " + cleaned;
            if (cleaned.Length > 65) cleaned = cleaned.Substring(0, 62) + "...";
            return cleaned;
        }
        return isZh ? "⚠️ 任务执行异常或已中断" : "⚠️ Task interrupted or encountered an error";
    }


    private static string ExtractCompletionSummary(Dictionary<string, object> data)
    {
        // Antigravity format: status == "DONE"
        if (data.ContainsKey("status") && "DONE".Equals(data["status"]))
        {
            if (data.ContainsKey("content") && data["content"] != null)
            {
                string c = data["content"].ToString().Trim();
                if (!string.IsNullOrEmpty(c)) return c;
            }
            if (data.ContainsKey("result") && data["result"] != null)
            {
                return data["result"].ToString().Trim();
            }
            return "Task completed.";
        }

        // Claude Code format: assistant message with stop_reason == "end_turn" or "stop_sequence"
        if (data.ContainsKey("type") && "assistant".Equals(data["type"]))
        {
            if (data.ContainsKey("message"))
            {
                Dictionary<string, object> msg = data["message"] as Dictionary<string, object>;
                if (msg != null)
                {
                    object stopReason = msg.ContainsKey("stop_reason") ? msg["stop_reason"] : null;
                    bool isEndTurn = stopReason != null && ("end_turn".Equals(stopReason) || "stop_sequence".Equals(stopReason));
                    if (!isEndTurn) return null; // In progress (tool_use or streaming chunk)

                    if (msg.ContainsKey("content"))
                    {
                        IList content = msg["content"] as IList;
                        if (content != null && content.Count > 0)
                        {
                            bool hasTool = false;
                            string text = null;
                            foreach (object item in content)
                            {
                                Dictionary<string, object> part = item as Dictionary<string, object>;
                                if (part != null)
                                {
                                    if ("tool_use".Equals(part["type"])) hasTool = true;
                                    if ("text".Equals(part["type"]) && part.ContainsKey("text")) text = part["text"] as string;
                                }
                            }
                            if (!hasTool && !string.IsNullOrEmpty(text))
                            {
                                return text;
                            }
                        }
                    }
                    return "Task completed.";
                }
            }
        }
        return null;
    }


    internal static string CleanSummaryText(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;

        // 1. Remove code blocks ```...``` (preserve content if entire text was inside code block)
        string noCodeBlocks = Regex.Replace(text, @"```[\s\S]*?```", " ").Trim();
        string cleaned = !string.IsNullOrEmpty(noCodeBlocks) ? noCodeBlocks : Regex.Replace(text, @"```[a-zA-Z]*", " ");

        // 2. Remove inline code backticks `code` -> code, bold/italics, and links
        cleaned = Regex.Replace(cleaned, @"`([^`]+)`", "$1");
        cleaned = Regex.Replace(cleaned, @"\*\*([^*]+)\*\*", "$1");
        cleaned = Regex.Replace(cleaned, @"\*([^*]+)\*", "$1");
        cleaned = Regex.Replace(cleaned, @"!\[.*?\]\(.*?\)", " ");
        cleaned = Regex.Replace(cleaned, @"\[(.*?)\]\(.*?\)", "$1");

        // 3. Remove markdown headers, list bullets, blockquotes at start of lines
        cleaned = Regex.Replace(cleaned, @"(?m)^[\s#>*\-+]+", "");

        // 4. Convert unpunctuated newlines between lines to sentence breaks
        cleaned = Regex.Replace(cleaned, @"(?<![。！？.!?])[\r\n]+", "。");
        cleaned = Regex.Replace(cleaned, @"<[^>]+>", " ");
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();

        if (string.IsNullOrEmpty(cleaned)) return null;

        // 5. Strip AI pleasantries and conversational filler prefixes (multi-pass)
        string prev;
        do
        {
            prev = cleaned;
            string t = Regex.Replace(cleaned, @"^(?:好的|好|好的呢|没问题|Sure|Okay|OK)[，,！!\s]+", "", RegexOptions.IgnoreCase).Trim();
            if (!string.IsNullOrEmpty(t)) cleaned = t;

            t = Regex.Replace(cleaned, @"^(?:我已经为您|我已经为你|我已经帮您|我已经|已经|现已|现在已经|I have completed\s*|I have finished\s*|I have\s*|I've\s*)[，,！!\s]*", "", RegexOptions.IgnoreCase).Trim();
            if (!string.IsNullOrEmpty(t)) cleaned = t;

            // Strip introductory clause ending with colon (e.g. "完成了相关的修改：" or "修改总结如下：")
            t = Regex.Replace(cleaned, @"^[^。！？!?\r\n]{2,35}[：:]\s*", "").Trim();
            if (!string.IsNullOrEmpty(t)) cleaned = t;
        } while (cleaned != prev && cleaned.Length > 0);

        if (string.IsNullOrEmpty(cleaned)) return null;

        // 6. Extract the first meaningful sentence (up to 70 chars)
        int endIdx = -1;
        for (int i = 0; i < cleaned.Length; i++)
        {
            char c = cleaned[i];
            if (c == '。' || c == '！' || c == '!' || c == '？' || c == '?')
            {
                endIdx = i + 1;
                break;
            }
            if (c == '.' && (i + 1 == cleaned.Length || char.IsWhiteSpace(cleaned[i + 1]) || cleaned[i + 1] == '\r' || cleaned[i + 1] == '\n'))
            {
                endIdx = i + 1;
                break;
            }
        }

        string sentence = (endIdx > 0 && endIdx <= 70) ? cleaned.Substring(0, endIdx).Trim() : cleaned;
        if (sentence.Length > 70)
        {
            sentence = sentence.Substring(0, 67) + "...";
        }

        if (string.IsNullOrEmpty(sentence)) return null;

        if (char.IsLower(sentence[0]))
        {
            sentence = char.ToUpper(sentence[0]) + (sentence.Length > 1 ? sentence.Substring(1) : "");
        }

        // 7. Add checkmark prefix if not already starting with an emoji/status mark
        if (!sentence.StartsWith("✅") && !sentence.StartsWith("🎉") && !sentence.StartsWith("✔️") && !sentence.StartsWith("✓") && !sentence.StartsWith("✨") && !sentence.StartsWith("⚠️"))
        {
            sentence = "✅ " + sentence;
        }

        return sentence.Trim();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        lock (syncLock)
        {
            for (int i = 0; i < watchers.Count; i++)
            {
                try
                {
                    watchers[i].EnableRaisingEvents = false;
                    watchers[i].Dispose();
                }
                catch { }
            }
            watchers.Clear();
            foreach (System.Threading.Timer t in fileDebounceTimers.Values)
            {
                try { t.Dispose(); } catch { }
            }
            fileDebounceTimers.Clear();
            sessions.Clear();
        }
    }
}
