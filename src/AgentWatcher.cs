using System;
using System.Collections;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Globalization;

internal sealed partial class AgentSessionWatcher : IDisposable
{
    private readonly AppContext appContext;
    private readonly List<FileSystemWatcher> watchers = new List<FileSystemWatcher>();
    private readonly Dictionary<string, System.Threading.Timer> fileDebounceTimers = new Dictionary<string, System.Threading.Timer>(StringComparer.OrdinalIgnoreCase);
    private readonly JavaScriptSerializer jsonSerializer = new JavaScriptSerializer();
    private readonly object syncLock = new object();
    private enum TurnState { Idle, Processing, Waiting, Completed }
    private enum ParseKind { Cursor, Claude, Codex, Antigravity, Generic }

    private sealed class TurnSession
    {
        internal string Target = "";
        internal string Topic = "";
        internal string CurrentTurn = "";
        internal string Node = "";
        internal string Thought = "";
        internal string Action = "";
        internal string LastHighAction = "";
        internal string Conclusion = "";
        internal int LowReadCount = 0;
        internal bool LastWasGeneric = false;
        internal DateTime StartTime = DateTime.MinValue;
        internal TurnState State = TurnState.Idle;
        internal int Version = 0;
        internal int ActiveStep = 0;
        internal int TotalSteps = 0;
        internal string StepDescription = "";
        internal string LastToolAction = "";
        internal string PendingCompletionSummary = "";
        internal string PendingQuestion = "";
        internal string PendingError = "";
        internal bool LastNodeIsThought = false;
        internal string WatchedPath = "";
        internal string WatchedAgent = "";
        internal readonly List<string> BatchNodes = new List<string>();
    }

    private sealed class FileReadCursor
    {
        internal long Offset;
        internal string Head;
    }

    private readonly Dictionary<string, FileReadCursor> fileCursors = new Dictionary<string, FileReadCursor>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> fileLastParseTick = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> fileBurstAgent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, TurnSession> sessions = new Dictionary<string, TurnSession>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> watchedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private System.Threading.Timer processSyncTimer;
    private System.Threading.Timer hotPollTimer;
    private bool disposed;

    internal AgentSessionWatcher(AppContext appContext)
    {
        this.appContext = appContext;
        jsonSerializer.MaxJsonLength = int.MaxValue;
        InitDefaultWatchers();
    }

    private void InitDefaultWatchers()
    {
        try
        {
            // SelfCheck drives ProcessLogFile directly against temp transcripts;
            // mounting the real user-session watchers would let ambient Claude /
            // Codex transcripts pollute the assertions.
            if (AppContext.IsSelfCheckRunning) return;

            // Initial mount for active agents + common known agent locations
            SyncActiveAgentWatchers();

            processSyncTimer = new System.Threading.Timer(delegate { SyncActiveAgentWatchers(); }, null, 10000, 10000);
            hotPollTimer = new System.Threading.Timer(delegate { HotPollProcessingSessions(); }, null, 200, 200);
        }
        catch { }
    }

    private static void AddDirIfExists(List<string> dirs, string p)
    {
        if (!string.IsNullOrEmpty(p) && Directory.Exists(p) && !dirs.Contains(p)) dirs.Add(p);
    }

    private static void AddTranscriptSubdirs(List<string> dirs, string root)
    {
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return;
        foreach (string sn in new string[] { "sessions", "projects", "logs", "transcripts" })
            AddDirIfExists(dirs, Path.Combine(root, sn));
    }

    internal static List<string> ResolveCandidateDirectories(string agentName)
    {
        List<string> dirs = new List<string>();
        if (string.IsNullOrEmpty(agentName)) return dirs;
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        string n = agentName.ToLowerInvariant();
        if (n.Contains("claude"))
        {
            AddDirIfExists(dirs, Path.Combine(home, ".claude", "projects"));
            if (dirs.Count == 0) AddDirIfExists(dirs, Path.Combine(home, ".claude"));
            try { AddDirIfExists(dirs, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".claude")); } catch { }
        }
        else if (n.Contains("antigravity") || n.Contains("agy") || n.Contains("gemini"))
        {
            AddDirIfExists(dirs, Path.Combine(home, ".gemini", "antigravity", "brain"));
            AddDirIfExists(dirs, Path.Combine(home, ".antigravity"));
        }
        else if (n.Contains("codex"))
        {
            AddDirIfExists(dirs, Path.Combine(home, ".codex", "sessions"));
            if (dirs.Count == 0) AddDirIfExists(dirs, Path.Combine(home, ".codex"));
        }
        else if (n.Contains("cursor"))
        {
            AddDirIfExists(dirs, Path.Combine(home, ".cursor", "projects"));
            if (dirs.Count == 0) AddDirIfExists(dirs, Path.Combine(home, ".cursor"));
        }
        else if (n.Contains("windsurf"))
        {
            AddTranscriptSubdirs(dirs, Path.Combine(home, ".codeium", "windsurf"));
            AddTranscriptSubdirs(dirs, Path.Combine(home, ".windsurf"));
        }
        else if (n.Contains("trae")) AddTranscriptSubdirs(dirs, Path.Combine(home, ".trae"));
        else if (n.Contains("aider")) AddTranscriptSubdirs(dirs, Path.Combine(home, ".aider"));
        else if (n.Contains("openhands") || n.Contains("open-hands"))
        {
            AddTranscriptSubdirs(dirs, Path.Combine(home, ".openhands"));
            AddTranscriptSubdirs(dirs, Path.Combine(home, ".opendevin"));
        }
        else if (n.Contains("augment")) AddTranscriptSubdirs(dirs, Path.Combine(home, ".augment"));
        else if (n.Contains("cline")) AddTranscriptSubdirs(dirs, Path.Combine(home, ".cline"));
        else if (n.Contains("roo")) AddTranscriptSubdirs(dirs, Path.Combine(home, ".roo-code"));
        else if (n.Contains("kiro")) AddTranscriptSubdirs(dirs, Path.Combine(home, ".kiro"));
        else if (n.Contains("continue")) AddTranscriptSubdirs(dirs, Path.Combine(home, ".continue"));
        else if (n.Contains("deepseek")) AddTranscriptSubdirs(dirs, Path.Combine(home, ".deepseek"));

        string slug = Regex.Replace(agentName.ToLowerInvariant(), @"[^a-z0-9]+", "");
        if (!string.IsNullOrEmpty(slug))
        {
            string slugDir = Path.Combine(home, "." + slug);
            if (Directory.Exists(slugDir))
            {
                foreach (string sn in new string[] { "sessions", "projects", "logs", "transcripts" }) AddDirIfExists(dirs, Path.Combine(slugDir, sn));
            }
        }

        return dirs;
    }

    internal void SyncActiveAgentWatchers()
    {
        if (disposed) return;
        try
        {
            HashSet<string> agentsToCheck = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Process-guided: retrieve all currently running agent names
            HashSet<string> running = AppContext.GetRunningAgents();
            if (running != null)
            {
                foreach (string r in running) agentsToCheck.Add(r);
            }

            // 2. Base agents always checked if installed
            agentsToCheck.Add("Antigravity");
            agentsToCheck.Add("Claude Code");
            agentsToCheck.Add("Codex");
            agentsToCheck.Add("Cursor");

            foreach (string agent in agentsToCheck)
            {
                List<string> candidateDirs = ResolveCandidateDirectories(agent);
                for (int i = 0; i < candidateDirs.Count; i++)
                {
                    string dir = candidateDirs[i];
                    lock (syncLock)
                    {
                        if (watchedDirs.Contains(dir)) continue;
                        watchedDirs.Add(dir);
                    }
                    WatchDirectory(dir, WatchFilterFor(agent, dir), agent);
                    ScanRecentSessionInDir(dir, agent);
                }
            }
        }
        catch { }
    }

    private void ScanRecentSessionInDir(string dir, string agentName)
    {
        System.Threading.ThreadPool.QueueUserWorkItem(delegate
        {
            try
            {
                if (!Directory.Exists(dir)) return;
                string newestFile = null;
                DateTime newestTime = DateTime.MinValue;

                if (dir.IndexOf("antigravity", StringComparison.OrdinalIgnoreCase) >= 0 && dir.IndexOf("brain", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    foreach (string convDir in Directory.GetDirectories(dir))
                    {
                        string tp = Path.Combine(convDir, ".system_generated", "logs", "transcript.jsonl");
                        if (!File.Exists(tp)) tp = Path.Combine(convDir, "transcript.jsonl");
                        if (File.Exists(tp))
                        {
                            DateTime mt = File.GetLastWriteTime(tp);
                            if (mt > newestTime) { newestTime = mt; newestFile = tp; }
                        }
                    }
                }
                else
                {
                    foreach (string f in Directory.GetFiles(dir, "*.jsonl", SearchOption.AllDirectories))
                    {
                        if (!IsRelevantTranscript(f, agentName)) continue;
                        DateTime mt = File.GetLastWriteTime(f);
                        if (mt > newestTime) { newestTime = mt; newestFile = f; }
                    }
                }

                if (newestFile != null && (DateTime.Now - newestTime).TotalMinutes < 30)
                {
                    ProcessLogFile(newestFile, agentName);
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
            try { fsw.InternalBufferSize = 64 * 1024; } catch { }
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
        if (!fullPath.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase)) return;
        if (!IsRelevantTranscript(fullPath, agentName)) return;

        // Leading + max-interval: first event parses quickly; later events in the
        // same burst do not reset the timer (avoids trailing-debounce starvation).
        lock (syncLock)
        {
            if (disposed) return;
            if (fileDebounceTimers.ContainsKey(fullPath))
            {
                fileBurstAgent[fullPath] = agentName;
                return;
            }
            int now = Environment.TickCount;
            int last;
            int delay = 15;
            if (fileLastParseTick.TryGetValue(fullPath, out last))
            {
                int since = now - last;
                if (since < 80) delay = 80 - since;
                if (delay < 0) delay = 0;
            }
            fileBurstAgent[fullPath] = agentName;
            string pathCopy = fullPath;
            System.Threading.Timer timer = null;
            timer = new System.Threading.Timer(delegate
            {
                string agentCopy = agentName;
                lock (syncLock)
                {
                    System.Threading.Timer t;
                    if (fileDebounceTimers.TryGetValue(pathCopy, out t))
                    {
                        try { t.Dispose(); } catch { }
                        fileDebounceTimers.Remove(pathCopy);
                    }
                    string a;
                    if (fileBurstAgent.TryGetValue(pathCopy, out a) && !string.IsNullOrEmpty(a))
                    {
                        agentCopy = a;
                        fileBurstAgent.Remove(pathCopy);
                    }
                    fileLastParseTick[pathCopy] = Environment.TickCount;
                }
                if (!disposed)
                {
                    try { ProcessLogFile(pathCopy, agentCopy); }
                    catch { }
                }
            }, null, delay, System.Threading.Timeout.Infinite);
            fileDebounceTimers[fullPath] = timer;
        }
    }

    private void HotPollProcessingSessions()
    {
        if (disposed || AppContext.IsSelfCheckRunning) return;
        List<KeyValuePair<string, string>> hot = new List<KeyValuePair<string, string>>();
        lock (syncLock)
        {
            foreach (KeyValuePair<string, TurnSession> kv in sessions)
            {
                TurnSession s = kv.Value;
                if (s == null || s.State != TurnState.Processing) continue;
                if (string.IsNullOrEmpty(s.WatchedPath) || !s.WatchedPath.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase))
                    continue;
                hot.Add(new KeyValuePair<string, string>(s.WatchedPath, s.WatchedAgent));
            }
        }
        for (int i = 0; i < hot.Count; i++)
        {
            if (disposed) return;
            try { ProcessLogFile(hot[i].Key, hot[i].Value); }
            catch { }
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
        else if (fullPath.IndexOf("codex", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            agentName = "Codex";
        }
        else if (string.IsNullOrEmpty(agentName))
        {
            string lowerPath = fullPath.ToLowerInvariant();
            if (lowerPath.Contains(".claude")) agentName = "Claude Code";
            else if (lowerPath.Contains(".codex")) agentName = "Codex";
            else if (lowerPath.Contains(".gemini") || lowerPath.Contains("antigravity")) agentName = "Antigravity";
            else agentName = "AI Agent";
        }

        // Active CLI overrides passive watcher
        if (appContext != null && appContext.HasRecentCliActivity(agentName)) return;

        bool rewritten;
        List<string> lines = ReadIncrementalJsonLines(fullPath, out rewritten);
        if (lines == null || lines.Count == 0) return;

        DispatchParsedEvents(lines, fullPath, agentName, rewritten);
    }

    internal static List<string> ReadRecentJsonLines(string path, int maxBytes = 65536)
    {
        List<string> result = new List<string>();
        try
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                if (fs.Length == 0) return result;
                long seekLen = Math.Min(fs.Length, Math.Min(maxBytes, 65536));
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

    internal static bool IsTrivialConfirmation(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        string t = text.Trim().Trim('。', '！', '!', '，', ',', '、', ' ', '\t', '\r', '\n', '.', '~').ToLowerInvariant();
        if (t.Length == 0) return false;
        if (t == "好" || t == "好的" || t == "好的呢" || t == "行" || t == "可以" || t == "行的" || t == "可以的" ||
            t == "确认" || t == "确认实施" || t == "确认推进" || t == "继续" || t == "推进" || t == "实施" ||
            t == "继续推进" || t == "全部实施" || t == "全部一并实施" || t == "开始" || t == "开始实施" ||
            t == "ok" || t == "okay" || t == "sure" || t == "yes" || t == "y" || t == "proceed" ||
            t == "continue" || t == "go ahead" || t == "lgtm" || t == "do it")
        {
            return true;
        }
        if (Regex.IsMatch(t, @"^(?:好|好的|行|可以|确认|继续|推进|实施|ok|yes|proceed)\s*[,，、 ]\s*(?:好|好的|行|可以|确认|继续|推进|实施|开始|实施吧|推进吧|开始吧|继续吧|做吧|执行|请继续|请推进)$", RegexOptions.IgnoreCase))
        {
            return true;
        }
        return false;
    }

    internal static string ExtractJsonString(string json, string key)
    {
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key)) return null;
        int idx = json.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
        if (idx < 0) return null;
        int colon = json.IndexOf(':', idx + key.Length + 2);
        if (colon < 0) return null;
        int sStart = colon + 1;
        while (sStart < json.Length && (json[sStart] == ' ' || json[sStart] == '\t' || json[sStart] == '\r' || json[sStart] == '\n')) sStart++;
        if (sStart >= json.Length || json[sStart] != '"') return null;
        sStart++;
        int bs = json.IndexOf('\\', sStart), quote = json.IndexOf('"', sStart);
        if (quote < 0) return null;
        if (bs < 0 || bs > quote) return json.Substring(sStart, quote - sStart);
        StringBuilder sb = new StringBuilder(quote - sStart);
        for (int i = sStart; i < json.Length; i++)
        {
            char c = json[i];
            if (c == '\\' && i + 1 < json.Length)
            {
                char n = json[++i];
                if (n == 'n') sb.Append('\n');
                else if (n == 'r') sb.Append('\r');
                else if (n == 't') sb.Append('\t');
                else if (n == 'u' && i + 4 < json.Length)
                {
                    int u;
                    if (int.TryParse(json.Substring(i + 1, 4), System.Globalization.NumberStyles.HexNumber, null, out u)) { sb.Append((char)u); i += 4; }
                    else sb.Append(n);
                }
                else sb.Append(n);
                continue;
            }
            if (c == '"') return sb.ToString();
            sb.Append(c);
        }
        return sb.ToString();
    }

    internal static string FastExtractUserRequest(string line)
    {
        if (string.IsNullOrEmpty(line)) return null;
        string role = ExtractJsonString(line, "role");
        string raw = null;
        if ("user".Equals(role, StringComparison.OrdinalIgnoreCase) || "human".Equals(role, StringComparison.OrdinalIgnoreCase))
        {
            raw = ExtractJsonString(line, "text") ?? ExtractJsonString(line, "content") ?? ExtractJsonString(line, "prompt") ?? ExtractJsonString(line, "query");
        }
        else
        {
            string type = ExtractJsonString(line, "type");
            if (string.IsNullOrEmpty(type)) return null;
            if (type == "USER_INPUT") raw = ExtractJsonString(line, "content");
            else if (type == "last-prompt") raw = ExtractJsonString(line, "lastPrompt");
            else if (type == "custom-title") raw = ExtractJsonString(line, "customTitle");
            else if (type == "user") raw = ExtractJsonString(line, "text") ?? ExtractJsonString(line, "content");
            else if (type == "UserMessage") raw = ExtractJsonString(line, "text");
        }
        if (string.IsNullOrEmpty(raw)) return null;
        string cl = CleanUserRequest(raw.Trim());
        return (!string.IsNullOrEmpty(cl) && !string.Equals(cl, "continue", StringComparison.OrdinalIgnoreCase)) ? cl : null;
    }

    internal static string FindLatestUserRequestInFile(string filePath, JavaScriptSerializer serializer)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return null;
        try
        {
            List<string> lines = ReadRecentJsonLines(filePath, 262144);
            string lastTrivial = null;
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                string line = lines[i];
                string req = FastExtractUserRequest(line);
                if (string.IsNullOrEmpty(req))
                {
                    try { req = ExtractUserRequest(serializer.Deserialize<Dictionary<string, object>>(line)); } catch { }
                }
                if (!string.IsNullOrEmpty(req))
                {
                    if (!IsTrivialConfirmation(req)) return req;
                    if (lastTrivial == null) lastTrivial = req;
                }
            }
            return lastTrivial;
        }
        catch { }
        return null;
    }

        
    private void DispatchParsedEvents(List<string> lines, string filePath, string agentName, bool rewritten)
    {
        ProcessTranscriptLines(lines, filePath, agentName, rewritten);
    }

    internal static DateTime ExtractMessageTimestamp(Dictionary<string, object> data, string filePath)
    {
        if (data != null)
        {
            string text = ExtractTextFromCommonKeys(data);
            if (!string.IsNullOrEmpty(text))
            {
                Match tm = Regex.Match(text, @"<timestamp>(.*?)<\/timestamp>", RegexOptions.IgnoreCase);
                if (tm.Success)
                {
                    string cleanTs = Regex.Replace(tm.Groups[1].Value, @"\s*\([^)]*\)", "").Trim();
                    DateTime dt;
                    if (DateTime.TryParse(cleanTs, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                        return dt;
                }
            }
            string[] timeKeys = new string[] { "created_at", "timestamp", "time", "date" };
            for (int k = 0; k < timeKeys.Length; k++)
            {
                if (data.ContainsKey(timeKeys[k]))
                {
                    object val = data[timeKeys[k]];
                    if (val is string)
                    {
                        DateTime dt;
                        if (DateTime.TryParse((string)val, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out dt))
                        {
                            return dt.ToLocalTime();
                        }
                    }
                    else if (val is long || val is int || val is double)
                    {
                        try
                        {
                            double num = Convert.ToDouble(val);
                            if (num > 1e11)
                                return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(num).ToLocalTime();
                            if (num > 1e8)
                                return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(num).ToLocalTime();
                        }
                        catch { }
                    }
                }
            }
        }
        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
        {
            try { return File.GetLastWriteTime(filePath); } catch { }
        }
        return DateTime.Now;
    }

    private void DispatchParsedEvent(Dictionary<string, object> data, string agentName, string filePath, bool isZh, TurnSession session, DateTime msgTime)
    {
        if (appContext == null || disposed || session == null) return;

        // Ignore internal background SDK retries unless max retries exhausted
        if (data != null && data.ContainsKey("source") && "request_retry".Equals(data["source"] as string))
        {
            int attempt = data.ContainsKey("retryAttempt") ? Convert.ToInt32(data["retryAttempt"]) : 0;
            int maxRetries = data.ContainsKey("maxRetries") ? Convert.ToInt32(data["maxRetries"]) : 9;
            if (attempt < maxRetries) return;
        }

        // Ignore background scheduled wakeups without user interaction
        if (data != null && data.ContainsKey("wakeupSource") && "schedule_wakeup".Equals(data["wakeupSource"] as string) &&
            data.ContainsKey("promptSource") && "system".Equals(data["promptSource"] as string))
        {
            return;
        }

        // 1. Check for Turn Start (USER_INPUT / user request) -> Anchor Session Target
        string userReq = ExtractUserRequest(data);
        if (!string.IsNullOrEmpty(userReq))
        {
            lock (syncLock)
            {
                bool isTrivial = IsTrivialConfirmation(userReq);
                bool hasRealTarget = !string.IsNullOrEmpty(session.Target) &&
                                     session.Target != "当前会话" && session.Target != "当前任务" &&
                                     session.Target != "Session" && session.Target != "Task" &&
                                     session.Target.Length > 2;

                if (!isTrivial || !hasRealTarget)
                {
                    session.Target = userReq;
                }
                session.StartTime = DateTime.Now;
                session.State = TurnState.Processing;
                session.Version++;
                session.ActiveStep = session.TotalSteps = 0;
                session.StepDescription = session.LastToolAction = "";
            }
            string msg = "⏳ [" + session.Target + "] " + (isZh ? "处理中..." : "Processing...");
            appContext.SetAgentTaskMessage(msg, agentName, true, null, null, -1f, msgTime, filePath);
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
                if (string.IsNullOrEmpty(session.Target))
                {
                    session.Target = "";
                }
            }
            if (!AppContext.IsAgentWindowInForeground(agentName))
            {
                AppContext.PlayAiSound();
            }
            string msg = "❓ [" + session.Target + "] " + (isZh ? "等待决策" : "Waiting for input");
            appContext.SetAgentTaskMessage(msg, agentName, false, question, null, -2f, msgTime, filePath);
            return;
        }

        // 2b. Check for Error / Abrupt Termination
        string errSummary = ExtractErrorSummary(data, isZh);
        if (!string.IsNullOrEmpty(errSummary))
        {
            lock (syncLock)
            {
                session.State = TurnState.Idle;
                session.ActiveStep = session.TotalSteps = 0;
                session.StepDescription = session.LastToolAction = "";
                if (string.IsNullOrEmpty(session.Target))
                {
                    session.Target = "";
                }
            }
            if (!AppContext.IsAgentWindowInForeground(agentName))
            {
                AppContext.PlayAiSound();
            }
            string msg = "⚠️ [" + session.Target + "] " + (isZh ? "执行异常" : "Error");
            appContext.SetAgentTaskMessage(msg, agentName, false, errSummary, null, -2f, msgTime, filePath);
            return;
        }

        // 3. Check for Completion (Done / Final response: 300ms fast-path for explicit terminal wire protocols, 1200ms fallback)
        bool isExplicitTerminal;
        string completionSummary = ExtractCompletionDetails(data, out isExplicitTerminal);
        if (!string.IsNullOrEmpty(completionSummary))
        {
            int currentVer;
            lock (syncLock)
            {
                if (string.IsNullOrEmpty(session.Target)) session.Target = "";
                session.PendingCompletionSummary = CleanSummaryText(completionSummary);
                currentVer = ++session.Version;
            }
            int delay = AppContext.IsSelfCheckRunning
                ? (isExplicitTerminal ? 30 : 60)
                : (isExplicitTerminal ? 300 : 1200);
            ScheduleCommitCompletion(agentName, filePath, currentVer, delay, msgTime);
            return;
        }

        // 4. Processing Phase: Tool calls or Thinking
        DateTime nowDate = DateTime.Now;
        bool isGenericOutput = (data.ContainsKey("type") && "GENERIC".Equals(data["type"])) ||
                               (data.ContainsKey("type") && "response_item".Equals(data["type"]) && data.ContainsKey("payload") &&
                                data["payload"] is Dictionary<string, object> && "function_call_output".Equals(((Dictionary<string, object>)data["payload"])["type"]));
        string curTool = ExtractToolSummary(data, isZh);

        lock (syncLock)
        {
            if (!string.IsNullOrEmpty(curTool)) session.LastToolAction = curTool;

            // Fallback session target anchor if user prompt arrived before watcher was attached
            if (string.IsNullOrEmpty(session.Target))
            {
                string tAct = curTool ?? session.LastToolAction; session.Target = !string.IsNullOrEmpty(tAct) ? (CleanUserRequest(tAct) ?? tAct) : (isZh ? "当前任务" : "Task");
                session.StartTime = nowDate;
            }
        }

        // In State 1 (Processing):
        string stepProgressText = null;
        float progress = -1f;
        string activeDesc = null;
        string lastAct = null;
        lock (syncLock)
        {
            session.State = TurnState.Processing;
            lastAct = session.LastToolAction;
            if (session.ActiveStep > 0 && session.TotalSteps > 0)
            {
                progress = (float)session.ActiveStep / session.TotalSteps;
                activeDesc = session.StepDescription;
                string descPart = !string.IsNullOrEmpty(activeDesc) ? (" " + activeDesc) : "";
                stepProgressText = session.ActiveStep + "/" + session.TotalSteps + descPart;
            }
        }

        string authenticAction = !string.IsNullOrEmpty(curTool)
            ? FormatAuthenticAction(curTool, stepProgressText, activeDesc, session.ActiveStep, session.TotalSteps)
            : null;
        string thought = ExtractThoughtSlice(data);
        string formattedLast = !string.IsNullOrEmpty(lastAct) ? FormatAuthenticAction(lastAct, null, null, 0, 0) : null;

        string subDetail;
        if (!string.IsNullOrEmpty(authenticAction)) subDetail = authenticAction;
        else if (!string.IsNullOrEmpty(thought)) subDetail = "💭 " + thought;
        else if (isGenericOutput) subDetail = !string.IsNullOrEmpty(formattedLast) ? ("✓ " + formattedLast) : null;
        else if (!string.IsNullOrEmpty(stepProgressText)) subDetail = "[" + session.ActiveStep + "/" + session.TotalSteps + "]" + (!string.IsNullOrEmpty(activeDesc) ? (" " + activeDesc) : "");
        else subDetail = formattedLast;

        string procMsg = "⏳ [" + session.Target + "] " + (isZh ? "处理中..." : "Processing...");
        appContext.SetAgentTaskMessage(procMsg, agentName, true, subDetail, stepProgressText, progress, msgTime, filePath);
    }

    private static string FormatAuthenticAction(string act, string stepProg, string desc, int s, int t)
    {
        if (string.IsNullOrEmpty(act) && string.IsNullOrEmpty(stepProg)) return null;
        if (!string.IsNullOrEmpty(act))
        {
            int sp = act.IndexOf(' ');
            if (sp > 0)
            {
                string verb = act.Substring(0, sp);
                string rest = act.Substring(sp + 1).Trim();
                if (verb == "运行:" || verb == "Run:") act = "⚡ " + rest;
                else if (verb == "读取" || verb == "Read") act = "read_file: " + rest;
                else if (verb == "修改" || verb == "Edit") act = "edit_file: " + rest;
                else if (verb == "写入" || verb == "Write") act = "write_file: " + rest;
                else if (verb == "删除" || verb == "Delete") act = "delete_file: " + rest;
                else if (verb == "搜索" || verb == "Search") act = "search: " + rest;
                else if (verb == "操作" || verb == "Process") act = "file: " + rest;
            }
        }
        if (!string.IsNullOrEmpty(stepProg))
        {
            return "[" + s + "/" + t + "] " + (!string.IsNullOrEmpty(desc) ? desc : (act ?? stepProg));
        }
        return act;
    }

    private static readonly char[] SentenceDelims = new char[] { '。', '！', '!', '？', '?', '\n', '\r' };

    private static string ExtractThoughtSlice(Dictionary<string, object> d)
    {
        if (d == null) return null;
        object raw = null;
        // 1. Direct LLM wire protocol fields: thinking, reasoning_content (DeepSeek/OpenAI), thought (Gemini)
        if (d.ContainsKey("thinking")) raw = d["thinking"];
        else if (d.ContainsKey("reasoning_content")) raw = d["reasoning_content"];
        else if (d.ContainsKey("thought")) raw = d["thought"];
        else
        {
            string type = d.ContainsKey("type") ? d["type"] as string : null;
            if (type == "GENERIC" || type == "tool_output" || type == "tool_response") return null;

            // Delta streaming chunk (OpenAI / DeepSeek SSE)
            if (d.ContainsKey("delta") && d["delta"] is Dictionary<string, object>)
            {
                Dictionary<string, object> delta = (Dictionary<string, object>)d["delta"];
                if (delta.ContainsKey("reasoning_content")) raw = delta["reasoning_content"];
                else if (delta.ContainsKey("thinking")) raw = delta["thinking"];
                else if (delta.ContainsKey("thought")) raw = delta["thought"];
            }

            // Payload / Response wrapper (Codex / Antigravity)
            if (raw == null && d.ContainsKey("payload") && d["payload"] is Dictionary<string, object>)
            {
                Dictionary<string, object> pl = (Dictionary<string, object>)d["payload"];
                if ("function_call_output".Equals(pl.ContainsKey("type") ? pl["type"] : null)) return null;
                if (pl.ContainsKey("thinking")) raw = pl["thinking"];
                else if (pl.ContainsKey("reasoning_content")) raw = pl["reasoning_content"];
                else if (pl.ContainsKey("thought")) raw = pl["thought"];
                else if (pl.ContainsKey("content")) raw = pl["content"];
            }

            // Anthropic content blocks or Gemini parts
            if (raw == null && d.ContainsKey("content") && d["content"] is IList)
            {
                foreach (object item in (IList)d["content"])
                {
                    if (item is Dictionary<string, object>)
                    {
                        Dictionary<string, object> part = (Dictionary<string, object>)item;
                        string pType = part.ContainsKey("type") ? part["type"] as string : null;
                        if ("thinking".Equals(pType) || part.ContainsKey("thinking"))
                        {
                            raw = part.ContainsKey("thinking") ? part["thinking"] : (part.ContainsKey("text") ? part["text"] : null);
                            if (raw != null) break;
                        }
                        if (part.ContainsKey("reasoning_content"))
                        {
                            raw = part["reasoning_content"];
                            break;
                        }
                        if (part.ContainsKey("thought") && Equals(part["thought"], true) && part.ContainsKey("text"))
                        {
                            raw = part["text"];
                            break;
                        }
                    }
                }
            }

            if (raw == null && d.ContainsKey("message") && d["message"] is Dictionary<string, object>)
            {
                Dictionary<string, object> msg = (Dictionary<string, object>)d["message"];
                if (msg.ContainsKey("thinking")) raw = msg["thinking"];
                else if (msg.ContainsKey("reasoning_content")) raw = msg["reasoning_content"];
                else if (msg.ContainsKey("thought")) raw = msg["thought"];
                else raw = ExtractTextFromCommonKeys(msg);
            }
            if (raw == null) raw = d.ContainsKey("content") ? d["content"] : (d.ContainsKey("message") ? d["message"] : null);
        }
        if (raw == null) return null;
        string s = raw.ToString().Trim();
        if (s.StartsWith("<thought", StringComparison.OrdinalIgnoreCase) || s.StartsWith("<thinking", StringComparison.OrdinalIgnoreCase))
        {
            s = Regex.Replace(s, @"^<[^>]+>\s*", "").Trim();
        }
        if (string.IsNullOrEmpty(s) || s[0] == '<' || s[0] == '{' || s[0] == '[') return null;
        if (s.StartsWith("- ") || s.StartsWith("* ")) s = s.Substring(2).Trim();
        int dot = s.IndexOfAny(SentenceDelims);
        if (dot > 0) s = s.Substring(0, dot).Trim();
        return s.Length > 2 ? s : null;
    }

    private void ScheduleCommitCompletion(string agentName, string filePath, int expectedVer, int delayMs, DateTime? msgTime)
    {
        System.Threading.ThreadPool.QueueUserWorkItem(delegate
        {
            try
            {
                System.Threading.Thread.Sleep(delayMs);
                string target, summary;
                int sec = 0, ver;
                lock (syncLock)
                {
                    TurnSession s;
                    string sessionLookupKey = agentName + "|" + (filePath ?? "");
                    if (!sessions.TryGetValue(sessionLookupKey, out s) || s.Version != expectedVer) return;
                    s.State = TurnState.Completed;
                    s.ActiveStep = s.TotalSteps = 0; s.StepDescription = s.LastToolAction = "";
                    target = s.Target;
                    summary = s.PendingCompletionSummary;
                    if (s.StartTime > DateTime.MinValue) sec = (int)(DateTime.Now - s.StartTime).TotalSeconds;
                    ver = ++s.Version;
                }
                if (!disposed && appContext != null)
                {
                    bool isZh = appContext.Settings.Language != "en";
                    string timeStr = (sec >= 3) ? (" (" + (sec >= 60 ? (sec / 60 + "m" + sec % 60 + "s") : (sec + "s")) + ")") : "";
                    appContext.SetAgentTaskMessage("✅ [" + target + "] " + (isZh ? "本轮已完成" : "Completed") + timeStr, agentName, false, summary, null, -2f, msgTime, filePath);
                    ScheduleClearProgress(agentName, filePath, ver, 600000);
                    AppContext.TrimMemory();
                }
            }
            catch { }
        });
    }

    private void ScheduleClearProgress(string agentName, string filePath, int expectedVersion, int delayMs)
    {
        System.Threading.ThreadPool.QueueUserWorkItem(delegate
        {
            try
            {
                System.Threading.Thread.Sleep(delayMs);
                lock (syncLock)
                {
                    TurnSession session;
                    string sessionLookupKey = agentName + "|" + (filePath ?? "");
                    if (!sessions.TryGetValue(sessionLookupKey, out session) || session.Version != expectedVersion)
                    {
                        return;
                    }
                }
                if (!disposed && appContext != null)
                {
                    appContext.ClearAgentProgress(agentName, filePath);
                }
            }
            catch { }
        });
    }

    internal static string ExtractUserRequest(Dictionary<string, object> data)
    {
        if (data == null) return null;
        if (IsToolResultUserRow(data)) return null;
        string roleHint = data.ContainsKey("role") ? data["role"] as string : null;
        if (string.Equals(roleHint, "developer", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(roleHint, "system", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        object tObj;
        if (data.TryGetValue("type", out tObj) && tObj is string)
        {
            string type = (string)tObj;
            if (type == "USER_INPUT" || type == "user")
            {
                if (type == "user" && !IsHumanOrigin(data)) return null;
                string t = ExtractLastHumanText(data) ?? ExtractTextFromCommonKeys(data);
                if (!string.IsNullOrEmpty(t)) return CleanUserRequest(t);
            }
            if (type == "last-prompt" && data.ContainsKey("lastPrompt") && data["lastPrompt"] != null)
                return CleanUserRequest(data["lastPrompt"].ToString());
            if (type == "custom-title" && data.ContainsKey("customTitle") && data["customTitle"] != null)
                return CleanUserRequest(data["customTitle"].ToString());
        }

        if (data.ContainsKey("payload") && data["payload"] is Dictionary<string, object>)
        {
            Dictionary<string, object> payload = (Dictionary<string, object>)data["payload"];
            string pType = payload.ContainsKey("type") ? payload["type"] as string : null;
            if ("message".Equals(pType) && "user".Equals(payload.ContainsKey("role") ? payload["role"] : null))
            {
                if (IsCodexNonHumanUser(payload)) return null;
                string t = ExtractLastHumanText(payload) ?? ExtractTextFromCommonKeys(payload);
                if (!string.IsNullOrEmpty(t)) return CleanUserRequest(t);
            }
            else if ("item_completed".Equals(pType) && payload.ContainsKey("item") && payload["item"] is Dictionary<string, object>)
            {
                Dictionary<string, object> item = (Dictionary<string, object>)payload["item"];
                if ("UserMessage".Equals(item.ContainsKey("type") ? item["type"] : null))
                {
                    string t = ExtractTextFromCommonKeys(item);
                    if (!string.IsNullOrEmpty(t)) return CleanUserRequest(t);
                }
            }
        }

        if (data.ContainsKey("role"))
        {
            string r = data["role"] as string;
            if (string.Equals(r, "user", StringComparison.OrdinalIgnoreCase) || string.Equals(r, "human", StringComparison.OrdinalIgnoreCase))
            {
                if (IsCodexNonHumanUser(data)) return null;
                string text = ExtractLastHumanText(data) ?? ExtractTextFromCommonKeys(data);
                if (!string.IsNullOrEmpty(text)) return CleanUserRequest(text);
            }
        }

        object pkVal;
        foreach (string pk in new string[] { "prompt", "user_prompt", "user_request" })
            if (data.TryGetValue(pk, out pkVal) && pkVal is string && ((string)pkVal).Length >= 2) return CleanUserRequest((string)pkVal);



        return null;
    }

    private static string ExtractTextFromCommonKeys(Dictionary<string, object> d)
    {
        if (d == null) return null;
        if (d.ContainsKey("message") && d["message"] is Dictionary<string, object>)
        {
            string fromMsg = ExtractTextFromCommonKeys((Dictionary<string, object>)d["message"]);
            if (!string.IsNullOrEmpty(fromMsg)) return fromMsg;
        }
        if (d.ContainsKey("content"))
        {
            if (d["content"] is string) return d["content"] as string;
            if (d["content"] is IList)
            {
                foreach (object item in (IList)d["content"])
                {
                    if (item is string) return (string)item;
                    if (item is Dictionary<string, object>)
                    {
                        Dictionary<string, object> part = (Dictionary<string, object>)item;
                        if (part.ContainsKey("text") && part["text"] != null) return part["text"].ToString();
                    }
                }
            }
        }
        foreach (string k in new string[] { "text", "message", "summary", "prompt", "query" })
        {
            if (d.ContainsKey(k) && d[k] is string) return (string)d[k];
        }
        return null;
    }


    internal static string CleanUserRequest(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return null;

        // 1. Extract specifically targeted request/query tags if present
        Match m = Regex.Match(raw, @"<(?:USER_REQUEST|user_query|prompt|query)>\s*([\s\S]*?)\s*<\/(?:USER_REQUEST|user_query|prompt|query)>", RegexOptions.IgnoreCase);
        string text = m.Success ? m.Groups[1].Value : raw;

        // 2. Strip disruptive metadata blocks (like <timestamp>...</timestamp>, <context>...</context>)
        text = Regex.Replace(text, @"<(?:timestamp|context|system_info|system_instructions|system-reminder|system_reminder|ADDITIONAL_METADATA|USER_SETTINGS_CHANGE)[^>]*>[\s\S]*?<\/(?:timestamp|context|system_info|system_instructions|system-reminder|system_reminder|ADDITIONAL_METADATA|USER_SETTINGS_CHANGE)>", " ", RegexOptions.IgnoreCase);

        // 3. Strip remaining outer HTML/XML tags without destroying inner text
        text = Regex.Replace(text, @"<\/?[a-zA-Z0-9_\-]+(?:\s+[^>]*)?>", " ");

        // 4. Normalize whitespace
        text = Regex.Replace(text, @"\s+", " ").Trim();

        if (string.IsNullOrEmpty(text)) return null;

        // 5. Strip conversational courtesy openings
        text = Regex.Replace(text, @"^(?:请帮我|帮我|麻烦|我想|请你|能不能|能否|能够|请问|请|Can you|Please|I want to|I would like to|Could you)\s*[,，:：]?\s*", "", RegexOptions.IgnoreCase).Trim();

        if (string.IsNullOrEmpty(text)) return null;

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
        if (args != null)
        {
            foreach (string k in new string[] { "toolSummary", "summary", "description" })
                if (args.ContainsKey(k)) { string v = CleanToolParam(args[k]); if (!string.IsNullOrEmpty(v)) return v; }
        }

        string lower = toolName.ToLowerInvariant();

        // 1. Desktop Agent: Screen perception (Anthropic Computer-Use, OSWorld, UI-TARS)
        if (lower.Contains("screenshot") || lower.Contains("screen_perception") || lower.Contains("read_screen") ||
            lower.Contains("view_screen") || lower.Contains("capture_screen") || lower.Contains("inspect_screen") ||
            (lower.Contains("computer") && args != null && args.ContainsKey("action") && "screenshot".Equals(CleanToolParam(args["action"]))))
        {
            return isZh ? "👁️ 观察桌面" : "👁️ Perceiving desktop";
        }

        // 2. Desktop Agent: Mouse / Coordinate actions
        string compAction = (args != null && args.ContainsKey("action")) ? CleanToolParam(args["action"]) : null;
        bool isMouse = lower.Contains("mouse") || lower.Contains("click") || lower.Contains("cursor") || lower.Contains("drag") ||
                       (lower.Contains("computer") && compAction != null && (compAction.Contains("click") || compAction.Contains("mouse") || compAction.Contains("drag") || compAction.Contains("scroll")));

        if (isMouse)
        {
            string coord = null;
            if (args != null)
            {
                if (args.ContainsKey("coordinate"))
                {
                    object cObj = args["coordinate"];
                    if (cObj is IList && ((IList)cObj).Count >= 2)
                    {
                        IList cList = (IList)cObj;
                        coord = CleanToolParam(cList[0]) + ", " + CleanToolParam(cList[1]);
                    }
                    else coord = CleanToolParam(cObj);
                }
                else if (args.ContainsKey("x") && args.ContainsKey("y"))
                {
                    coord = CleanToolParam(args["x"]) + ", " + CleanToolParam(args["y"]);
                }
            }
            if (!string.IsNullOrEmpty(coord))
            {
                return isZh ? ("🖱️ 桌面点击 (" + coord + ")") : ("🖱️ Click (" + coord + ")");
            }
            return isZh ? "🖱️ 桌面交互" : "🖱️ Desktop interacting";
        }

        // 3. Desktop Agent: Keyboard action
        bool isKeyboard = lower.Contains("type") || lower.Contains("keyboard") || lower.Contains("press_key") || lower.Contains("send_keys") || lower.Contains("hotkey") ||
                          (lower.Contains("computer") && compAction != null && (compAction == "type" || compAction == "key"));
        if (isKeyboard)
        {
            string txt = null;
            if (args != null)
            {
                foreach (string k in new string[] { "text", "key", "keys", "input" })
                    if (args.ContainsKey(k)) { txt = CleanToolParam(args[k]); if (!string.IsNullOrEmpty(txt)) break; }
            }
            if (!string.IsNullOrEmpty(txt))
            {
                if (txt.Length > 20) txt = txt.Substring(0, 20) + "...";
                return isZh ? ("⌨️ 键入: " + txt) : ("⌨️ Type: " + txt);
            }
            return isZh ? "⌨️ 键盘键入" : "⌨️ Typing";
        }

        // 4. Desktop Agent: Window management
        if (lower.Contains("launch_app") || lower.Contains("focus_window") || lower.Contains("switch_window") || lower.Contains("open_application"))
        {
            string appName = null;
            if (args != null)
            {
                foreach (string k in new string[] { "app", "name", "title", "process_name" })
                    if (args.ContainsKey(k)) { appName = CleanToolParam(args[k]); if (!string.IsNullOrEmpty(appName)) break; }
            }
            if (!string.IsNullOrEmpty(appName)) return isZh ? ("🪟 调度窗口: " + appName) : ("🪟 Window: " + appName);
            return isZh ? "🪟 窗口调度" : "🪟 Window action";
        }

        // 5. Desktop Agent: Browser Use
        if (lower.Contains("navigate_page") || lower.Contains("browser_action") || lower.Contains("new_page"))
        {
            string url = null;
            if (args != null && args.ContainsKey("url")) url = CleanToolParam(args["url"]);
            if (!string.IsNullOrEmpty(url))
            {
                if (url.Length > 30) url = url.Substring(0, 30) + "...";
                return isZh ? ("🌐 访问: " + url) : ("🌐 Browse: " + url);
            }
            return isZh ? "🌐 网页操作" : "🌐 Browser action";
        }

        string path = null;
        if (args != null)
        {
            foreach (string k in new string[] { "Path", "path", "filePath", "file_path", "Target" })
                if (args.ContainsKey(k)) { path = CleanToolParam(args[k]); if (!string.IsNullOrEmpty(path)) break; }
        }
        if (!string.IsNullOrEmpty(path))
        {
            string fn = Path.GetFileName(path);
            if (string.IsNullOrEmpty(fn)) fn = path;
            string op = lower.Contains("read") || lower.Contains("view") || lower == "cat" ? (isZh ? "读取 " : "Read ") :
                        lower.Contains("edit") || lower.Contains("replace") || lower.Contains("modify") ? (isZh ? "修改 " : "Edit ") :
                        lower.Contains("write") || lower.Contains("create") || lower.Contains("save") ? (isZh ? "写入 " : "Write ") :
                        lower.Contains("delete") || lower.Contains("remove") ? (isZh ? "删除 " : "Delete ") : (isZh ? "操作 " : "Process ");
            return op + fn;
        }

        string cmd = null;
        if (args != null)
        {
            foreach (string k in new string[] { "CommandLine", "command", "cmd" })
                if (args.ContainsKey(k)) { cmd = CleanToolParam(args[k]); if (!string.IsNullOrEmpty(cmd)) break; }
        }
        if (!string.IsNullOrEmpty(cmd))
        {
            cmd = Regex.Replace(cmd, @"^(?:powershell(?:\.exe)?|cmd(?:\.exe)?|bash(?:\.exe)?)\s+(?:-ExecutionPolicy\s+\w+\s+|-File\s+|-c\s+|/c\s+)*", "", RegexOptions.IgnoreCase).Trim();
            return isZh ? ("运行: " + cmd) : ("Run: " + cmd);
        }

        string q = null;
        if (args != null)
        {
            foreach (string k in new string[] { "query", "queries", "pattern" })
                if (args.ContainsKey(k)) { q = CleanToolParam(args[k]); if (!string.IsNullOrEmpty(q)) break; }
        }
        if (!string.IsNullOrEmpty(q))
        {
            return isZh ? ("搜索: " + q) : ("Search: " + q);
        }

        if (lower == "invoke_subagent" || lower.Contains("subagent") || lower.Contains("task"))
        {
            string role = args != null ? (args.ContainsKey("Role") ? CleanToolParam(args["Role"]) : (args.ContainsKey("TypeName") ? CleanToolParam(args["TypeName"]) : null)) : null;
            return !string.IsNullOrEmpty(role) ? (isZh ? ("调度: " + role) : ("Subagent: " + role)) : (isZh ? "调度子智能体" : "Invoke subagent");
        }
        if (lower.Contains("manage_subagent"))
        {
            string act = args != null && args.ContainsKey("Action") ? CleanToolParam(args["Action"]) : null;
            return (isZh ? "管理智能体" : "Manage subagents") + (!string.IsNullOrEmpty(act) ? (": " + act) : "");
        }
        if (lower == "send_message") return isZh ? "发送协作消息" : "Send message";
        if (lower.Contains("marketplace")) return isZh ? "检索插件" : "Search marketplace";

        if (lower.Contains("command") || lower == "bash" || lower == "terminal" || lower.StartsWith("exec")) return isZh ? "运行命令" : "Running command";
        if (lower.Contains("read")) return isZh ? "读取文件" : "Reading file";
        if (lower.Contains("edit") || lower.Contains("patch")) return isZh ? "修改文件" : "Editing file";
        if (lower.Contains("write")) return isZh ? "写入文件" : "Writing file";
        if (lower.Contains("search")) return isZh ? "搜索网络" : "Web search";
        if (lower.Contains("ask")) return isZh ? "询问用户" : "Asking question";
        return toolName;
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

                    string name = null;
                    Dictionary<string, object> args = null;

                    // Standard OpenAI / DeepSeek function format: { function: { name: "...", arguments: "{...}" } }
                    if (call.ContainsKey("function") && call["function"] is Dictionary<string, object>)
                    {
                        Dictionary<string, object> fn = (Dictionary<string, object>)call["function"];
                        if (fn.ContainsKey("name")) name = CleanToolParam(fn["name"]);
                        object rawArgs = fn.ContainsKey("arguments") ? fn["arguments"] : null;
                        if (rawArgs is Dictionary<string, object>) args = (Dictionary<string, object>)rawArgs;
                        else if (rawArgs is string)
                        {
                            string sArgs = ((string)rawArgs).Trim();
                            if (sArgs.StartsWith("{") && sArgs.EndsWith("}"))
                            {
                                try { args = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(sArgs); } catch { }
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(name) && call.ContainsKey("name")) name = CleanToolParam(call["name"]);
                    if (args == null)
                    {
                        if (call.ContainsKey("args")) args = call["args"] as Dictionary<string, object>;
                        else if (call.ContainsKey("parameters")) args = call["parameters"] as Dictionary<string, object>;
                        else if (call.ContainsKey("input")) args = call["input"] as Dictionary<string, object>;
                    }

                    string action = ExtractFriendlyAction(name, args, isZh);
                    if (!string.IsNullOrEmpty(action)) return action;
                }
            }
        }

        // 2. Claude Code / Anthropic content structure
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
        else if (data.ContainsKey("content") && data["content"] is IList)
        {
            foreach (object item in (IList)data["content"])
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

        // 3. Codex function_call in response_item
        if (data.ContainsKey("type") && "response_item".Equals(data["type"]) && data.ContainsKey("payload"))
        {
            Dictionary<string, object> payload = data["payload"] as Dictionary<string, object>;
            if (payload != null)
            {
                string pType = payload.ContainsKey("type") ? payload["type"] as string : null;
                if ("function_call".Equals(pType) || "custom_tool_call".Equals(pType))
                {
                    string name = payload.ContainsKey("name") ? CleanToolParam(payload["name"]) : null;
                    Dictionary<string, object> args = null;
                    object rawArgs = payload.ContainsKey("arguments") ? payload["arguments"] : null;
                    if (rawArgs is Dictionary<string, object>)
                    {
                        args = (Dictionary<string, object>)rawArgs;
                    }
                    else if (rawArgs is string)
                    {
                        string argStr = ((string)rawArgs).Trim();
                        if (argStr.StartsWith("{") && argStr.EndsWith("}"))
                        {
                            try
                            {
                                args = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(argStr);
                            }
                            catch { }
                        }
                    }
                    string action = ExtractFriendlyAction(name, args, isZh);
                    if (!string.IsNullOrEmpty(action)) return action;
                }
            }
        }

        // 4. Codex item_completed in event_msg
        if (data.ContainsKey("type") && "event_msg".Equals(data["type"]) && data.ContainsKey("payload"))
        {
            Dictionary<string, object> payload = data["payload"] as Dictionary<string, object>;
            if (payload != null && "item_completed".Equals(payload.ContainsKey("type") ? payload["type"] : null) && payload.ContainsKey("item"))
            {
                Dictionary<string, object> item = payload["item"] as Dictionary<string, object>;
                if (item != null)
                {
                    string itType = item.ContainsKey("type") ? item["type"] as string : null;
                    if ("CommandExecution".Equals(itType))
                    {
                        string cmd = null;
                        object cObj = item.ContainsKey("command") ? item["command"] : null;
                        if (cObj is string) cmd = (string)cObj;
                        else if (cObj is IList)
                        {
                            IList cList = (IList)cObj;
                            List<string> parts = new List<string>();
                            foreach (object p in cList) if (p != null) parts.Add(p.ToString());
                            cmd = string.Join(" ", parts.ToArray());
                        }
                        return ExtractFriendlyAction("run_command", new Dictionary<string, object> { { "CommandLine", cmd } }, isZh);
                    }
                    if ("FileChange".Equals(itType) && item.ContainsKey("changes"))
                    {
                        Dictionary<string, object> changes = item["changes"] as Dictionary<string, object>;
                        if (changes != null && changes.Count > 0)
                        {
                            foreach (string pathKey in changes.Keys)
                            {
                                return ExtractFriendlyAction("file_edit", new Dictionary<string, object> { { "Path", pathKey } }, isZh);
                            }
                        }
                    }
                    if ("WebSearch".Equals(itType) && item.ContainsKey("query"))
                    {
                        return ExtractFriendlyAction("web_search", new Dictionary<string, object> { { "query", item["query"] } }, isZh);
                    }
                    if ("ImageView".Equals(itType) && item.ContainsKey("path"))
                    {
                        return ExtractFriendlyAction("read_file", new Dictionary<string, object> { { "Path", item["path"] } }, isZh);
                    }
                }
            }
        }

        // 5. Universal tool heuristics
        string toolName = null;
        Dictionary<string, object> toolArgs = null;

        if (data.ContainsKey("tool") && data["tool"] is string) toolName = data["tool"] as string;
        else if (data.ContainsKey("tool_name") && data["tool_name"] is string) toolName = data["tool_name"] as string;
        else if (data.ContainsKey("function") && data["function"] is string) toolName = data["function"] as string;

        if (!string.IsNullOrEmpty(toolName))
        {
            if (data.ContainsKey("args") && data["args"] is Dictionary<string, object>) toolArgs = (Dictionary<string, object>)data["args"];
            else if (data.ContainsKey("arguments") && data["arguments"] is Dictionary<string, object>) toolArgs = (Dictionary<string, object>)data["arguments"];
            else if (data.ContainsKey("parameters") && data["parameters"] is Dictionary<string, object>) toolArgs = (Dictionary<string, object>)data["parameters"];
            else if (data.ContainsKey("input") && data["input"] is Dictionary<string, object>) toolArgs = (Dictionary<string, object>)data["input"];
            string action = ExtractFriendlyAction(toolName, toolArgs, isZh);
            if (!string.IsNullOrEmpty(action)) return action;
        }

        // Direct command execution key
        if (data.ContainsKey("command") || data.ContainsKey("cmd"))
        {
            string c = (data.ContainsKey("command") ? data["command"] : data["cmd"]) as string;
            if (!string.IsNullOrEmpty(c))
            {
                return ExtractFriendlyAction("run_command", new Dictionary<string, object> { { "CommandLine", c } }, isZh);
            }
        }

        return null;
    }



    internal static bool ExtractStepProgress(Dictionary<string, object> data, string toolSummary, out int step, out int total, out string desc)
    {
        step = 0;
        total = 0;
        desc = "";

        // 0. MCP (Model Context Protocol) Native Progress Notification
        // e.g. method: "notifications/progress", params: { progress: 5, total: 20, message: "..." }
        if (data != null)
        {
            Dictionary<string, object> mcpParams = null;
            if (data.ContainsKey("method") && "notifications/progress".Equals(data["method"] as string) && data.ContainsKey("params") && data["params"] is Dictionary<string, object>)
            {
                mcpParams = (Dictionary<string, object>)data["params"];
            }
            else if (data.ContainsKey("progress") || data.ContainsKey("total_steps") || data.ContainsKey("totalSteps"))
            {
                mcpParams = data;
            }

            if (mcpParams != null)
            {
                object pVal = mcpParams.ContainsKey("progress") ? mcpParams["progress"] : (mcpParams.ContainsKey("step") ? mcpParams["step"] : null);
                object tVal = mcpParams.ContainsKey("total") ? mcpParams["total"] : (mcpParams.ContainsKey("total_steps") ? mcpParams["total_steps"] : (mcpParams.ContainsKey("totalSteps") ? mcpParams["totalSteps"] : null));
                if (pVal != null && tVal != null)
                {
                    try
                    {
                        double pNum = Convert.ToDouble(pVal);
                        double tNum = Convert.ToDouble(tVal);
                        if (tNum > 0 && pNum >= 0 && pNum <= tNum)
                        {
                            step = (int)pNum;
                            total = (int)tNum;
                            object dVal = mcpParams.ContainsKey("message") ? mcpParams["message"] : (mcpParams.ContainsKey("description") ? mcpParams["description"] : null);
                            if (dVal != null) desc = CleanSummaryText(dVal.ToString());
                            if (string.IsNullOrEmpty(desc) && !string.IsNullOrEmpty(toolSummary)) desc = toolSummary;
                            return true;
                        }
                    }
                    catch { }
                }
                else if (pVal != null && tVal == null)
                {
                    try
                    {
                        double pNum = Convert.ToDouble(pVal);
                        if (pNum > 0.0 && pNum <= 1.0)
                        {
                            step = (int)(pNum * 100);
                            total = 100;
                            object dVal = mcpParams.ContainsKey("message") ? mcpParams["message"] : (mcpParams.ContainsKey("description") ? mcpParams["description"] : null);
                            if (dVal != null) desc = CleanSummaryText(dVal.ToString());
                            return true;
                        }
                    }
                    catch { }
                }
            }
        }

        // 1. Tool summary step pattern: e.g. "1/3 正在执行算法优化", "[1/3] 正在执行算法优化", "Step 1/3: 正在执行算法优化"
        if (!string.IsNullOrEmpty(toolSummary))
        {
            Match m = Regex.Match(toolSummary, @"^(?:\[|\(|\b|第)?(?:Step\s*)?(\d+)(?:步|阶段)?\s*(?:[/／]|of)\s*(?:第)?(\d+)(?:步|阶段)?(?:\]|\)|\b)?\s*[:：\-\s]*(.*)$", RegexOptions.IgnoreCase);
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

        // 2. Claude Code / Cursor TodoWrite in tool_calls or content parts
        if (TryExtractTodoProgress(data.ContainsKey("tool_calls") ? data["tool_calls"] as IList : null, out step, out total, out desc))
            return true;
        IList todoParts = null;
        if (data.ContainsKey("message") && data["message"] is Dictionary<string, object>)
        {
            Dictionary<string, object> msg = (Dictionary<string, object>)data["message"];
            todoParts = msg.ContainsKey("content") ? msg["content"] as IList : null;
        }
        else if (data.ContainsKey("content") && data["content"] is IList)
        {
            todoParts = (IList)data["content"];
        }
        if (TryExtractTodoProgress(todoParts, out step, out total, out desc))
            return true;

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
        string q = null;
        if (args.ContainsKey("question") && args["question"] != null) q = args["question"].ToString().Trim();
        else if (args.ContainsKey("questions") && args["questions"] is IList)
        {
            IList qList = (IList)args["questions"];
            if (qList.Count > 0 && qList[0] is Dictionary<string, object>)
            {
                Dictionary<string, object> firstQ = (Dictionary<string, object>)qList[0];
                if (firstQ.ContainsKey("question") && firstQ["question"] != null) q = firstQ["question"].ToString().Trim();
            }
        }
        return !string.IsNullOrEmpty(q) ? q : null;
    }

    internal static string ExtractQuestion(Dictionary<string, object> data, bool isZh = true)
    {
        if (data == null) return null;

        // 1. Tool calls
        if (data.ContainsKey("tool_calls"))
        {
            IList calls = data["tool_calls"] as IList;
            if (calls != null && calls.Count > 0 && calls[0] is Dictionary<string, object>)
            {
                Dictionary<string, object> call = (Dictionary<string, object>)calls[0];
                string name = call.ContainsKey("name") ? call["name"] as string : null;
                Dictionary<string, object> args = call.ContainsKey("args") ? call["args"] as Dictionary<string, object> :
                    (call.ContainsKey("parameters") ? call["parameters"] as Dictionary<string, object> : (call.ContainsKey("input") ? call["input"] as Dictionary<string, object> : null));

                if (name != null && name.IndexOf("question", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    string qText = ExtractQuestionPromptText(args);
                    return !string.IsNullOrEmpty(qText) ? (isZh ? ("请确认: " + qText) : ("Confirm: " + qText)) : (isZh ? "等待用户决策" : "Waiting for user decision");
                }

                if (args != null && args.ContainsKey("ArtifactMetadata"))
                {
                    object meta = args["ArtifactMetadata"];
                    bool reqFb = false;
                    if (meta is Dictionary<string, object>)
                    {
                        Dictionary<string, object> md = (Dictionary<string, object>)meta;
                        if (md.ContainsKey("RequestFeedback") && (Equals(md["RequestFeedback"], true) || "true".Equals(md["RequestFeedback"] as string, StringComparison.OrdinalIgnoreCase)))
                        {
                            reqFb = true;
                        }
                    }
                    else if (meta is string)
                    {
                        string s = (string)meta;
                        reqFb = s.IndexOf("RequestFeedback", StringComparison.OrdinalIgnoreCase) >= 0 && s.IndexOf("true", StringComparison.OrdinalIgnoreCase) >= 0;
                    }
                    if (reqFb)
                    {
                        return isZh ? "实施方案已就绪，等待您确认 (Proceed)" : "Plan ready, awaiting Proceed approval";
                    }
                }
            }
        }

        // 2. Claude Code content: tool_use AskUserQuestion
        if (data.ContainsKey("message") && data["message"] is Dictionary<string, object>)
        {
            Dictionary<string, object> msg = (Dictionary<string, object>)data["message"];
            IList content = msg.ContainsKey("content") ? msg["content"] as IList : null;
            if (content != null)
            {
                foreach (object item in content)
                {
                    Dictionary<string, object> part = item as Dictionary<string, object>;
                    if (part != null && "tool_use".Equals(part.ContainsKey("type") ? part["type"] : null))
                    {
                        string name = part.ContainsKey("name") ? part["name"] as string : null;
                        if (name != null && name.IndexOf("question", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string qText = ExtractQuestionPromptText(part.ContainsKey("input") ? part["input"] as Dictionary<string, object> : null);
                            return !string.IsNullOrEmpty(qText) ? (isZh ? ("请确认: " + qText) : ("Confirm: " + qText)) : (isZh ? "等待用户决策" : "Waiting for user decision");
                        }
                    }
                }
            }
        }

        // 3. Direct question at the end of completed response
        bool isDoneTurn = (data.ContainsKey("type") && "PLANNER_RESPONSE".Equals(data["type"]) && "DONE".Equals(data.ContainsKey("status") ? data["status"] : null) && !data.ContainsKey("tool_calls"))
            || (data.ContainsKey("type") && "assistant".Equals(data["type"]) && data.ContainsKey("message"));

        if (isDoneTurn)
        {
            string text = ExtractTextFromCommonKeys(data);
            if (!string.IsNullOrEmpty(text))
            {
                string trimmed = text.Trim().TrimEnd('*', '_', '`', '#', ' ', '\r', '\n', '\t');
                if (trimmed.EndsWith("？") || trimmed.EndsWith("?"))
                {
                    string[] qlines = trimmed.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = qlines.Length - 1; i >= 0; i--)
                    {
                        string qLine = qlines[i].Trim().Trim('#', '*', '-', ' ', '>');
                        if ((qLine.EndsWith("？") || qLine.EndsWith("?")) && qLine.Length >= 4 && qLine.Length <= 100)
                        {
                            return isZh ? ("请确认: " + qLine) : ("Confirm: " + qLine);
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
        if (data.ContainsKey("type") && "GENERIC".Equals(data["type"])) return null;

        bool isErr = false;
        string errDetail = null;

        if (data.ContainsKey("status"))
        {
            string st = data["status"] as string;
            if (st != null && (st.Equals("ERROR", StringComparison.OrdinalIgnoreCase) ||
                st.Equals("FAILED", StringComparison.OrdinalIgnoreCase) ||
                st.StartsWith("CANCEL", StringComparison.OrdinalIgnoreCase)))
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
            object errObj = data["error"];
            string eStr = null;
            if (errObj is Dictionary<string, object>)
            {
                Dictionary<string, object> errDict = (Dictionary<string, object>)errObj;
                if (errDict.ContainsKey("formatted") && errDict["formatted"] != null)
                    eStr = errDict["formatted"].ToString().Trim();
                else if (errDict.ContainsKey("message") && errDict["message"] != null)
                    eStr = errDict["message"].ToString().Trim();
                else if (errDict.ContainsKey("status") && errDict["status"] != null)
                    eStr = "Status " + errDict["status"].ToString();
            }
            else
            {
                eStr = errObj.ToString().Trim();
            }
            if (!string.IsNullOrEmpty(eStr))
            {
                isErr = true;
                errDetail = eStr;
            }
        }

        if (data.ContainsKey("type") && "event_msg".Equals(data["type"]) && data.ContainsKey("payload"))
        {
            Dictionary<string, object> payload = data["payload"] as Dictionary<string, object>;
            if (payload != null)
            {
                string pType = payload.ContainsKey("type") ? payload["type"] as string : null;
                if ("turn_aborted".Equals(pType))
                {
                    isErr = true;
                    if (payload.ContainsKey("reason") && payload["reason"] != null) errDetail = payload["reason"].ToString();
                }
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
            return cleaned;
        }
        return isZh ? "⚠️ 任务执行异常或已中断" : "⚠️ Task interrupted or encountered an error";
    }


    internal static string ExtractCompletionDetails(Dictionary<string, object> data, out bool isExplicitTerminal)
    {
        isExplicitTerminal = false;
        if (data == null || (data.ContainsKey("type") && "GENERIC".Equals(data["type"]))) return null;
        if (data.ContainsKey("tool_calls") && data["tool_calls"] is IList && ((IList)data["tool_calls"]).Count > 0) return null;

        // 1. Antigravity format (PLANNER_RESPONSE + DONE)
        if (data.ContainsKey("type") && "PLANNER_RESPONSE".Equals(data["type"]) &&
            data.ContainsKey("status") && "DONE".Equals(data["status"]))
        {
            isExplicitTerminal = true;
            if (data.ContainsKey("content") && data["content"] != null)
            {
                string c = data["content"].ToString().Trim();
                if (!string.IsNullOrEmpty(c)) return c;
            }
            if (data.ContainsKey("result") && data["result"] != null)
            {
                string r = data["result"].ToString().Trim();
                if (!string.IsNullOrEmpty(r)) return r;
            }
            return null;
        }

        // 2. Claude Code format (assistant + end_turn)
        if (data.ContainsKey("type") && "assistant".Equals(data["type"]) && data.ContainsKey("message"))
        {
            Dictionary<string, object> msg = data["message"] as Dictionary<string, object>;
            if (msg != null)
            {
                object sr = msg.ContainsKey("stop_reason") ? msg["stop_reason"] : null;
                if (!"end_turn".Equals(sr) && !"stop_sequence".Equals(sr)) return null;
                IList content = msg.ContainsKey("content") ? msg["content"] as IList : null;
                if (content != null)
                {
                    string text = null;
                    foreach (object item in content)
                    {
                        Dictionary<string, object> part = item as Dictionary<string, object>;
                        if (part != null)
                        {
                            if ("tool_use".Equals(part["type"])) return null;
                            if ("text".Equals(part["type"]) && part.ContainsKey("text")) text = part["text"] as string;
                        }
                    }
                    if (!string.IsNullOrEmpty(text))
                    {
                        isExplicitTerminal = true;
                        return text;
                    }
                }
                return null;
            }
        }

        // 3. Codex format (event_msg + task_complete)
        if (data.ContainsKey("type") && "event_msg".Equals(data["type"]) && data.ContainsKey("payload"))
        {
            Dictionary<string, object> payload = data["payload"] as Dictionary<string, object>;
            if (payload != null)
            {
                string pType = payload.ContainsKey("type") ? payload["type"] as string : null;
                if ("task_complete".Equals(pType) || "task_completed".Equals(pType))
                {
                    isExplicitTerminal = true;
                    if (payload.ContainsKey("last_agent_message") && payload["last_agent_message"] != null)
                    {
                        string msg = payload["last_agent_message"].ToString().Trim();
                        if (!string.IsNullOrEmpty(msg)) return msg;
                    }
                    if (payload.ContainsKey("message") && payload["message"] != null)
                    {
                        string msg = payload["message"].ToString().Trim();
                        if (!string.IsNullOrEmpty(msg)) return msg;
                    }
                    return null;
                }
            }
        }

        // 4. Universal heuristic
        string status = data.ContainsKey("status") ? data["status"] as string : (data.ContainsKey("state") ? data["state"] as string : null);
        if (!string.IsNullOrEmpty(status))
        {
            status = status.ToUpperInvariant();
            if (status == "DONE" || status == "COMPLETED" || status == "SUCCESS" || status == "FINISHED")
            {
                string text = ExtractTextFromCommonKeys(data);
                return !string.IsNullOrEmpty(text) ? text : "Task completed.";
            }
        }
        string finishReason = data.ContainsKey("finish_reason") ? data["finish_reason"] as string : (data.ContainsKey("stop_reason") ? data["stop_reason"] as string : null);
        if (!string.IsNullOrEmpty(finishReason))
        {
            finishReason = finishReason.ToLowerInvariant();
            if (finishReason == "stop" || finishReason == "end_turn")
            {
                string text = ExtractTextFromCommonKeys(data);
                return !string.IsNullOrEmpty(text) ? text : "Task completed.";
            }
        }

        return null;
    }

    private static string ExtractCompletionSummary(Dictionary<string, object> data)
    {
        bool ignored;
        return ExtractCompletionDetails(data, out ignored);
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

        string sentence = endIdx > 0 ? cleaned.Substring(0, endIdx).Trim() : cleaned;

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
        if (processSyncTimer != null)
        {
            try { processSyncTimer.Dispose(); } catch { }
            processSyncTimer = null;
        }
        if (hotPollTimer != null)
        {
            try { hotPollTimer.Dispose(); } catch { }
            hotPollTimer = null;
        }
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
            watchedDirs.Clear();
            foreach (System.Threading.Timer t in fileDebounceTimers.Values)
            {
                try { t.Dispose(); } catch { }
            }
            fileDebounceTimers.Clear();
            fileLastParseTick.Clear();
            fileBurstAgent.Clear();
            sessions.Clear();
            fileCursors.Clear();
        }
    }
}
