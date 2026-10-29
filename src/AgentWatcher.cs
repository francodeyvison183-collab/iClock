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
        internal string PendingCompletionSummary = "";
    }

    private readonly Dictionary<string, TurnSession> sessions = new Dictionary<string, TurnSession>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> watchedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private System.Threading.Timer processSyncTimer;
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
            // Initial mount for active agents + common known agent locations
            SyncActiveAgentWatchers();

            // Periodic sync (every 3 seconds): zero overhead via Toolhelp process snapshot cache
            processSyncTimer = new System.Threading.Timer(delegate { SyncActiveAgentWatchers(); }, null, 10000, 10000);
        }
        catch { }
    }

    private static void AddDirIfExists(List<string> dirs, string p)
    {
        if (!string.IsNullOrEmpty(p) && Directory.Exists(p) && !dirs.Contains(p)) dirs.Add(p);
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
        else if (n.Contains("cursor")) AddDirIfExists(dirs, Path.Combine(home, ".cursor"));
        else if (n.Contains("windsurf"))
        {
            AddDirIfExists(dirs, Path.Combine(home, ".codeium", "windsurf"));
            AddDirIfExists(dirs, Path.Combine(home, ".windsurf"));
        }
        else if (n.Contains("trae")) AddDirIfExists(dirs, Path.Combine(home, ".trae"));
        else if (n.Contains("aider")) AddDirIfExists(dirs, Path.Combine(home, ".aider"));

        string slug = Regex.Replace(agentName.ToLowerInvariant(), @"[^a-z0-9]+", "");
        if (!string.IsNullOrEmpty(slug))
        {
            string slugDir = Path.Combine(home, "." + slug);
            if (Directory.Exists(slugDir) && !dirs.Contains(slugDir))
            {
                int before = dirs.Count;
                foreach (string sn in new string[] { "sessions", "projects", "logs", "transcripts" }) AddDirIfExists(dirs, Path.Combine(slugDir, sn));
                if (dirs.Count == before) dirs.Add(slugDir);
            }
            try { AddDirIfExists(dirs, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "." + slug)); } catch { }
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
                    WatchDirectory(dir, "*.json*", agent);
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
                    foreach (string f in Directory.GetFiles(dir, "*.json*", SearchOption.AllDirectories))
                    {
                        if (!f.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase) && !f.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
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
        if (!fullPath.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase) &&
            !fullPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) return;

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
        string type = ExtractJsonString(line, "type");
        if (string.IsNullOrEmpty(type)) return null;
        string raw = null;
        if ("USER_INPUT".Equals(type, StringComparison.Ordinal)) raw = ExtractJsonString(line, "content");
        else if ("last-prompt".Equals(type, StringComparison.Ordinal)) raw = ExtractJsonString(line, "lastPrompt");
        else if ("custom-title".Equals(type, StringComparison.Ordinal)) raw = ExtractJsonString(line, "customTitle");
        else if ("user".Equals(type, StringComparison.Ordinal)) raw = ExtractJsonString(line, "text") ?? ExtractJsonString(line, "content");
        else if ("UserMessage".Equals(type, StringComparison.Ordinal)) raw = ExtractJsonString(line, "text");
        else if ("response_item".Equals(type, StringComparison.Ordinal))
        {
            if (line.IndexOf("\"role\":\"user\"", StringComparison.Ordinal) >= 0 || line.IndexOf("\"role\": \"user\"", StringComparison.Ordinal) >= 0)
                raw = ExtractJsonString(line, "text");
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

    internal static string ExtractProjectHint(Dictionary<string, object> data, string filePath)
    {
        if (data != null)
        {
            object cwdObj = null;
            if (data.ContainsKey("cwd") && data["cwd"] != null) cwdObj = data["cwd"];
            else if (data.ContainsKey("workspace") && data["workspace"] != null) cwdObj = data["workspace"];
            else if (data.ContainsKey("workdir") && data["workdir"] != null) cwdObj = data["workdir"];
            else if (data.ContainsKey("project_path") && data["project_path"] != null) cwdObj = data["project_path"];
            else if (data.ContainsKey("root_path") && data["root_path"] != null) cwdObj = data["root_path"];
            else if (data.ContainsKey("payload"))
            {
                Dictionary<string, object> payload = data["payload"] as Dictionary<string, object>;
                if (payload != null)
                {
                    if (payload.ContainsKey("cwd") && payload["cwd"] != null) cwdObj = payload["cwd"];
                    else if (payload.ContainsKey("thread_settings"))
                    {
                        Dictionary<string, object> ts = payload["thread_settings"] as Dictionary<string, object>;
                        if (ts != null && ts.ContainsKey("cwd") && ts["cwd"] != null) cwdObj = ts["cwd"];
                    }
                }
            }
            if (cwdObj != null)
            {
                try
                {
                    string cwd = cwdObj.ToString().Trim().TrimEnd('\\', '/');
                    string name = Path.GetFileName(cwd);
                    if (!string.IsNullOrEmpty(name)) return name;
                }
                catch { }
            }
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
                else if (filePath.IndexOf(".codex", StringComparison.OrdinalIgnoreCase) >= 0 && File.Exists(filePath))
                {
                    using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
                    {
                        string firstLine = sr.ReadLine();
                        if (!string.IsNullOrEmpty(firstLine))
                        {
                            int cwdIdx = firstLine.IndexOf("\"cwd\":\"", StringComparison.Ordinal);
                            if (cwdIdx >= 0)
                            {
                                int start = cwdIdx + 7;
                                int end = firstLine.IndexOf('"', start);
                                if (end > start)
                                {
                                    string cwd = firstLine.Substring(start, end - start).Replace("\\\\", "\\").TrimEnd('\\', '/');
                                    string name = Path.GetFileName(cwd);
                                    if (!string.IsNullOrEmpty(name)) return name;
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }
        return null;
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
            string l = lines[i];
            if (l.IndexOf("user", StringComparison.OrdinalIgnoreCase) < 0 &&
                l.IndexOf("prompt", StringComparison.OrdinalIgnoreCase) < 0 &&
                l.IndexOf("USER_INPUT", StringComparison.Ordinal) < 0)
            {
                continue;
            }
            string req = FastExtractUserRequest(l);
            if (string.IsNullOrEmpty(req))
            {
                try
                {
                    Dictionary<string, object> d = jsonSerializer.Deserialize<Dictionary<string, object>>(l);
                    req = ExtractUserRequest(d);
                }
                catch { }
            }
            if (!string.IsNullOrEmpty(req))
            {
                latestReq = req;
                break;
            }
        }

        if (!string.IsNullOrEmpty(latestReq))
        {
            lock (syncLock)
            {
                bool isTrivial = IsTrivialConfirmation(latestReq);
                bool hasRealTarget = !string.IsNullOrEmpty(session.Target) &&
                                     session.Target != "当前会话" && session.Target != "当前任务" &&
                                     session.Target != "Session" && session.Target != "Task" &&
                                     session.Target.Length > 2;

                if (!isTrivial || !hasRealTarget)
                {
                    if (isTrivial && !hasRealTarget && !string.IsNullOrEmpty(filePath))
                    {
                        string preceding = FindLatestUserRequestInFile(filePath, jsonSerializer);
                        if (!string.IsNullOrEmpty(preceding) && !IsTrivialConfirmation(preceding))
                        {
                            session.Target = preceding;
                        }
                        else
                        {
                            session.Target = latestReq;
                        }
                    }
                    else
                    {
                        session.Target = latestReq;
                    }
                }

                session.StartTime = DateTime.Now;
                session.State = TurnState.Processing;
                session.ToolCount = 0;
                session.WindowStart = DateTime.Now;
                session.Version++;
                session.ActiveStep = session.TotalSteps = 0;
                session.StepDescription = session.LastToolAction = "";
            }
        }
        else if ((string.IsNullOrEmpty(session.Target) || session.Target == "当前会话" || session.Target == "当前任务" || session.Target == "Session" || session.Target == "Task") && !string.IsNullOrEmpty(filePath))
        {
            string found = FindLatestUserRequestInFile(filePath, jsonSerializer);
            if (!string.IsNullOrEmpty(found))
            {
                lock (syncLock)
                {
                    session.Target = found;
                    session.StartTime = DateTime.Now;
                }
            }
        }

        // 2. Scan recent lines backwards for step progress / TODO updates and active tool action in this turn
        string turnToolAction = null;
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            string l = lines[i];
            if (!l.Contains("tool") && !l.Contains("step") && !l.Contains("task") && !l.Contains("cwd") && !l.Contains("work") && !l.Contains("call") && !l.Contains("user") && !l.Contains("prompt"))
                continue;
            if (l.Contains("\"atis-latch\"") || l.Contains("\"token_usage\"") || l.Contains("\"SYSTEM_MESSAGE\""))
                continue;

            if (FastExtractUserRequest(l) != null) break;

            if (string.IsNullOrEmpty(session.ProjectHint))
            {
                string p = ExtractJsonString(l, "cwd") ?? ExtractJsonString(l, "workspace") ?? ExtractJsonString(l, "workdir") ?? ExtractJsonString(l, "project_path") ?? ExtractJsonString(l, "root_path");
                if (!string.IsNullOrEmpty(p))
                {
                    try { string name = Path.GetFileName(p.Trim().TrimEnd('\\', '/')); if (!string.IsNullOrEmpty(name)) session.ProjectHint = name; } catch { }
                }
            }

            bool needTool = string.IsNullOrEmpty(turnToolAction) && (l.Contains("tool") || l.Contains("call") || l.Contains("action"));
            bool needStep = session.TotalSteps == 0 && (l.Contains("step") || l.Contains("total") || l.Contains("progress") || l.Contains("todo"));
            if (!needTool && !needStep) continue;

            try
            {
                Dictionary<string, object> d = jsonSerializer.Deserialize<Dictionary<string, object>>(l);
                if (d != null)
                {
                    if (ExtractUserRequest(d) != null) break;

                    if (string.IsNullOrEmpty(session.ProjectHint))
                    {
                        string ph = ExtractProjectHint(d, filePath);
                        if (!string.IsNullOrEmpty(ph)) session.ProjectHint = ph;
                    }

                    if (needTool)
                    {
                        string tSum = ExtractToolSummary(d, isZh);
                        if (!string.IsNullOrEmpty(tSum)) turnToolAction = tSum;
                    }

                    int s, t;
                    string sd;
                    if (needStep && ExtractStepProgress(d, turnToolAction, out s, out t, out sd))
                    {
                        lock (syncLock)
                        {
                            session.ActiveStep = s;
                            session.TotalSteps = t;
                            session.StepDescription = sd;
                        }
                    }

                    if (!string.IsNullOrEmpty(turnToolAction) && session.TotalSteps > 0) break;
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
            if (l.Contains("\"atis-latch\"") || l.Contains("\"token_usage\"") || (l.Contains("\"mode\"") && !l.Contains("\"message\"")))
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
            DateTime msgTime = ExtractMessageTimestamp(latestData, filePath);
            DispatchParsedEvent(latestData, agentName, isZh, session, msgTime);
        }
    }

    internal static DateTime ExtractMessageTimestamp(Dictionary<string, object> data, string filePath)
    {
        if (data != null)
        {
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

    private void DispatchParsedEvent(Dictionary<string, object> data, string agentName, bool isZh, TurnSession session, DateTime msgTime)
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
                session.ToolCount = 0;
                session.WindowStart = DateTime.Now;
                session.Version++;
                session.ActiveStep = session.TotalSteps = 0;
                session.StepDescription = session.LastToolAction = "";
            }
            string planDesc = isZh ? "💭 正在分析需求与规划方案..." : "💭 Analyzing request & planning...";
            string msg = "⏳ [" + session.Target + "] " + (isZh ? "处理中..." : "Processing...");
            appContext.SetAgentTaskMessage(msg, agentName, true, planDesc, null, -1f, session.ProjectHint, msgTime);
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
            appContext.SetAgentTaskMessage(msg, agentName, false, question, null, -2f, session.ProjectHint, msgTime);
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
                session.ActiveStep = session.TotalSteps = 0;
                session.StepDescription = session.LastToolAction = "";
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
            appContext.SetAgentTaskMessage(msg, agentName, false, errSummary, null, -2f, session.ProjectHint, msgTime);
            return;
        }

        // 3. Check for Completion (Done / Final response with 5-second silence debounce)
        string completionSummary = ExtractCompletionSummary(data);
        if (!string.IsNullOrEmpty(completionSummary))
        {
            int currentVer;
            lock (syncLock)
            {
                if (string.IsNullOrEmpty(session.Target)) session.Target = isZh ? "当前会话" : "Session";
                session.PendingCompletionSummary = CleanSummaryText(completionSummary);
                currentVer = ++session.Version;
            }
            int delay = AppContext.IsSelfCheckRunning ? 60 : 5000;
            ScheduleCommitCompletion(agentName, currentVer, delay, session.ProjectHint, msgTime);
            return;
        }

        // 4. Processing Phase: Tool calls or Thinking
        // Runaway loop watchdog (> 60 calls in 90s)
        DateTime nowDate = DateTime.Now;
        int count = 1;
        bool isGenericOutput = (data.ContainsKey("type") && "GENERIC".Equals(data["type"])) ||
                               (data.ContainsKey("type") && "response_item".Equals(data["type"]) && data.ContainsKey("payload") &&
                                data["payload"] is Dictionary<string, object> && "function_call_output".Equals(((Dictionary<string, object>)data["payload"])["type"]));
        string curTool = ExtractToolSummary(data, isZh);

        lock (syncLock)
        {
            if (!string.IsNullOrEmpty(curTool)) session.LastToolAction = curTool;
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
                string tAct = curTool ?? session.LastToolAction;
                session.Target = !string.IsNullOrEmpty(tAct) ? (CleanUserRequest(tAct) ?? tAct) : (isZh ? "当前任务" : "Task");
                session.StartTime = nowDate;
            }
        }

        if (count >= 60)
        {
            string warn = "⚠️ [" + session.Target + "] " + (isZh ? ("疑似死循环 (" + count + "次)") : ("Runaway loop (" + count + " calls)"));
            appContext.SetAgentTaskMessage(warn, agentName, false, isZh ? "90秒内工具调用频率过高，疑似陷入循环" : "Runaway loop detected (>200 calls in 90s)", null, -2f, session.ProjectHint, msgTime);
            AppContext.PlayAiSound();
            return;
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
        else if (isGenericOutput) subDetail = !string.IsNullOrEmpty(formattedLast) ? ("✓ " + formattedLast) : (isZh ? "💭 分析中..." : "💭 Analyzing...");
        else if (!string.IsNullOrEmpty(thought)) subDetail = "💭 " + thought;
        else if (!string.IsNullOrEmpty(stepProgressText)) subDetail = "[" + session.ActiveStep + "/" + session.TotalSteps + "]" + (!string.IsNullOrEmpty(activeDesc) ? (" " + activeDesc) : "");
        else subDetail = !string.IsNullOrEmpty(formattedLast) ? formattedLast : (isZh ? "💭 思考分析中..." : "💭 Thinking...");

        string procMsg = "⏳ [" + session.Target + "] " + (isZh ? "处理中..." : "Processing...");
        appContext.SetAgentTaskMessage(procMsg, agentName, true, subDetail, stepProgressText, progress, session.ProjectHint, msgTime);
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
        if (d.ContainsKey("thinking")) raw = d["thinking"];
        else
        {
            string type = d.ContainsKey("type") ? d["type"] as string : null;
            if (type == "GENERIC" || type == "tool_output" || type == "tool_response") return null;
            if (d.ContainsKey("payload") && d["payload"] is Dictionary<string, object>)
            {
                Dictionary<string, object> pl = (Dictionary<string, object>)d["payload"];
                if ("function_call_output".Equals(pl.ContainsKey("type") ? pl["type"] : null)) return null;
                if (pl.ContainsKey("thinking")) raw = pl["thinking"];
                else if (pl.ContainsKey("content")) raw = pl["content"];
            }
            if (raw == null) raw = d.ContainsKey("content") ? d["content"] : (d.ContainsKey("message") ? d["message"] : null);
        }
        if (raw == null) return null;
        string s = raw.ToString().Trim();
        if (string.IsNullOrEmpty(s) || s[0] == '<' || s[0] == '{' || s[0] == '[') return null;
        if (s.StartsWith("- ") || s.StartsWith("* ")) s = s.Substring(2).Trim();
        int dot = s.IndexOfAny(SentenceDelims);
        if (dot > 0) s = s.Substring(0, dot).Trim();
        return s.Length > 2 ? s : null;
    }

    private void ScheduleCommitCompletion(string agentName, int expectedVer, int delayMs, string projectHint, DateTime? msgTime)
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
                    if (!sessions.TryGetValue(agentName, out s) || s.Version != expectedVer) return;
                    s.State = TurnState.Completed;
                    s.ToolCount = s.ActiveStep = s.TotalSteps = 0; s.StepDescription = s.LastToolAction = "";
                    target = s.Target;
                    summary = s.PendingCompletionSummary;
                    if (s.StartTime > DateTime.MinValue) sec = (int)(DateTime.Now - s.StartTime).TotalSeconds;
                    ver = ++s.Version;
                }
                if (!disposed && appContext != null)
                {
                    bool isZh = appContext.Settings.Language != "en";
                    string timeStr = (sec >= 3) ? (" (" + (sec >= 60 ? (sec / 60 + "m" + sec % 60 + "s") : (sec + "s")) + ")") : "";
                    appContext.SetAgentTaskMessage("✅ [" + target + "] " + (isZh ? "本轮已完成" : "Completed") + timeStr, agentName, false, summary, null, -2f, projectHint, msgTime);
                    ScheduleClearProgress(agentName, ver, 600000);
                    AppContext.TrimMemory();
                }
            }
            catch { }
        });
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

        // 5. Codex format:
        // A) response_item with payload.type == "message" && payload.role == "user"
        // B) event_msg with payload.type == "item_completed" && payload.item.type == "UserMessage"
        if (data.ContainsKey("payload"))
        {
            Dictionary<string, object> payload = data["payload"] as Dictionary<string, object>;
            if (payload != null)
            {
                string pType = payload.ContainsKey("type") ? payload["type"] as string : null;
                if ("message".Equals(pType) && "user".Equals(payload.ContainsKey("role") ? payload["role"] : null))
                {
                    string req = ExtractCodexContentText(payload.ContainsKey("content") ? payload["content"] : null);
                    if (!string.IsNullOrEmpty(req)) return req;
                }
                else if ("item_completed".Equals(pType) && payload.ContainsKey("item"))
                {
                    Dictionary<string, object> item = payload["item"] as Dictionary<string, object>;
                    if (item != null && "UserMessage".Equals(item.ContainsKey("type") ? item["type"] : null))
                    {
                        string req = ExtractCodexContentText(item.ContainsKey("content") ? item["content"] : null);
                        if (!string.IsNullOrEmpty(req)) return req;
                    }
                }
            }
        }

        // 6. Universal Schema-Agnostic Heuristics
        // A) Any object with role == user / human
        if (data.ContainsKey("role"))
        {
            string r = data["role"] as string;
            if (string.Equals(r, "user", StringComparison.OrdinalIgnoreCase) || string.Equals(r, "human", StringComparison.OrdinalIgnoreCase))
            {
                string text = ExtractTextFromCommonKeys(data);
                if (!string.IsNullOrEmpty(text)) return CleanUserRequest(text);
            }
        }
        // B) Direct prompt keys
        string[] promptKeys = new string[] { "prompt", "user_prompt", "user_request", "userPrompt", "userRequest" };
        for (int i = 0; i < promptKeys.Length; i++)
        {
            string pk = promptKeys[i];
            if (data.ContainsKey(pk) && data[pk] is string)
            {
                string pStr = data[pk] as string;
                if (!string.IsNullOrEmpty(pStr) && pStr.Length >= 2) return CleanUserRequest(pStr);
            }
        }
        // C) Type suffix matching: e.g. "UserMessage", "user_message", "user_input"
        if (data.ContainsKey("type") && data["type"] is string)
        {
            string t = (data["type"] as string).ToLowerInvariant();
            if (t.Contains("usermessage") || t.Contains("user_message") || t.Contains("userinput") || t.Contains("user_input"))
            {
                string text = ExtractTextFromCommonKeys(data);
                if (!string.IsNullOrEmpty(text)) return CleanUserRequest(text);
            }
        }

        return null;
    }

    private static string ExtractTextFromCommonKeys(Dictionary<string, object> d)
    {
        if (d == null) return null;
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
        foreach (string k in new string[] { "text", "message", "summary" })
        {
            if (d.ContainsKey(k) && d[k] is string) return (string)d[k];
        }
        return null;
    }

    private static string ExtractCodexContentText(object contentObj)
    {
        if (contentObj == null) return null;
        if (contentObj is string)
        {
            string s = CleanUserRequest((string)contentObj);
            return (!string.IsNullOrEmpty(s) && !string.Equals(s, "continue", StringComparison.OrdinalIgnoreCase)) ? s : null;
        }
        IList list = contentObj as IList;
        if (list == null || list.Count == 0) return null;
        for (int i = 0; i < list.Count; i++)
        {
            Dictionary<string, object> item = list[i] as Dictionary<string, object>;
            if (item != null && item.ContainsKey("text") && item["text"] != null)
            {
                string txt = item["text"].ToString().Trim();
                if (txt.StartsWith("#") || txt.StartsWith("<environment") || txt.StartsWith("<app"))
                    continue;
                string cleaned = CleanUserRequest(txt);
                if (!string.IsNullOrEmpty(cleaned) && !string.Equals(cleaned, "continue", StringComparison.OrdinalIgnoreCase))
                {
                    return cleaned;
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
        text = Regex.Replace(text, @"<[^>]+>(?:[\s\S]*?<\/[^>]+>)?", " ");

        // Normalize whitespace
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
        if (args != null)
        {
            foreach (string k in new string[] { "toolSummary", "summary", "description" })
                if (args.ContainsKey(k)) { string v = CleanToolParam(args[k]); if (!string.IsNullOrEmpty(v)) return v; }
        }

        string lower = toolName.ToLowerInvariant();
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


    private static string ExtractCompletionSummary(Dictionary<string, object> data)
    {
        if (data == null || (data.ContainsKey("type") && "GENERIC".Equals(data["type"]))) return null;
        if (data.ContainsKey("tool_calls") && data["tool_calls"] is IList && ((IList)data["tool_calls"]).Count > 0) return null;

        // 1. Antigravity format (PLANNER_RESPONSE + DONE)
        if (data.ContainsKey("type") && "PLANNER_RESPONSE".Equals(data["type"]) &&
            data.ContainsKey("status") && "DONE".Equals(data["status"]))
        {
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
            return "Task completed.";
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
                    if (!string.IsNullOrEmpty(text)) return text;
                }
                return "Task completed.";
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
                    return "Task completed.";
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
            sessions.Clear();
        }
    }
}
