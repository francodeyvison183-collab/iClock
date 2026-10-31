using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

internal sealed partial class AgentSessionWatcher
{
    private static readonly HashSet<string> NoiseTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "token_usage", "token_usage_record", "world_state", "session_meta", "turn_context",
        "attachment", "queue-operation", "SYSTEM_MESSAGE", "CHECKPOINT", "atis-latch",
        "thread_settings_applied"
    };

    private static string WatchFilterFor(string agentName, string dir)
    {
        string n = (agentName ?? "").ToLowerInvariant();
        string d = (dir ?? "").ToLowerInvariant();
        if (n.IndexOf("antigravity", StringComparison.Ordinal) >= 0 || n.IndexOf("agy", StringComparison.Ordinal) >= 0 ||
            d.IndexOf("antigravity", StringComparison.Ordinal) >= 0)
        {
            return "transcript.jsonl";
        }
        return "*.jsonl";
    }

    private static bool IsRelevantTranscript(string path, string agentName)
    {
        if (string.IsNullOrEmpty(path)) return false;
        if (!path.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase)) return false;
        string p = path.Replace('/', '\\').ToLowerInvariant();
        string n = (agentName ?? "").ToLowerInvariant();
        if (p.IndexOf("antigravity", StringComparison.Ordinal) >= 0 || p.IndexOf("\\brain\\", StringComparison.Ordinal) >= 0)
        {
            return p.EndsWith("\\logs\\transcript.jsonl", StringComparison.Ordinal);
        }
        if (p.IndexOf("\\.cursor\\", StringComparison.Ordinal) >= 0 || n.IndexOf("cursor", StringComparison.Ordinal) >= 0 && p.IndexOf("\\.cursor", StringComparison.Ordinal) >= 0)
        {
            return p.IndexOf("\\agent-transcripts\\", StringComparison.Ordinal) >= 0;
        }
        if (p.IndexOf("\\.claude\\", StringComparison.Ordinal) >= 0)
        {
            return p.IndexOf("\\subagents\\", StringComparison.Ordinal) < 0;
        }
        if (p.IndexOf("transcript_full.jsonl", StringComparison.Ordinal) >= 0) return false;
        return true;
    }

    private List<string> ReadIncrementalJsonLines(string path, out bool rewritten)
    {
        rewritten = false;
        List<string> result = new List<string>();
        try
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                if (fs.Length == 0) return result;
                byte[] headBuf = new byte[(int)Math.Min(64, fs.Length)];
                fs.Read(headBuf, 0, headBuf.Length);
                string head = Encoding.UTF8.GetString(headBuf);

                FileReadCursor cur;
                lock (syncLock)
                {
                    if (!fileCursors.TryGetValue(path, out cur))
                    {
                        cur = new FileReadCursor();
                        fileCursors[path] = cur;
                    }
                }

                long start = cur.Offset;
                if (start > fs.Length || string.IsNullOrEmpty(cur.Head) || !string.Equals(cur.Head, head, StringComparison.Ordinal))
                {
                    rewritten = string.IsNullOrEmpty(cur.Head) || !string.Equals(cur.Head, head, StringComparison.Ordinal) || start > fs.Length;
                    start = 0;
                    if (fs.Length > 524288) start = fs.Length - 262144;
                }

                fs.Seek(start, SeekOrigin.Begin);
                int toRead = (int)(fs.Length - start);
                if (toRead <= 0)
                {
                    cur.Offset = fs.Length;
                    cur.Head = head;
                    return result;
                }
                byte[] buffer = new byte[toRead];
                int n = fs.Read(buffer, 0, toRead);
                if (n <= 0) return result;
                string text = Encoding.UTF8.GetString(buffer, 0, n);
                bool incomplete = n == toRead && fs.Length == start + n && !text.EndsWith("\n") && !text.EndsWith("\r");
                string[] lines = text.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                int last = lines.Length;
                long consumed = start + n;
                if (incomplete && last > 0)
                {
                    last--;
                    consumed = fs.Length - Encoding.UTF8.GetByteCount(lines[last]);
                }
                int i0 = 0;
                if (start > 0 && lines.Length > 0 && (!lines[0].StartsWith("{") || !lines[0].EndsWith("}"))) i0 = 1;
                for (int i = i0; i < last; i++)
                {
                    string line = lines[i].Trim().Trim('\uFEFF');
                    if (line.StartsWith("{") && line.EndsWith("}")) result.Add(line);
                }
                if (result.Count == 0 && start == 0 && cur.Offset == 0) rewritten = true;
                if (cur.Offset == 0 && start == 0) rewritten = true;
                cur.Offset = consumed;
                cur.Head = head;
            }
        }
        catch { }
        return result;
    }

    private Dictionary<string, object> TryParseLine(string line)
    {
        if (string.IsNullOrEmpty(line)) return null;
        string type = ExtractJsonString(line, "type");
        if (!string.IsNullOrEmpty(type) && NoiseTypes.Contains(type)) return null;
        try { return jsonSerializer.Deserialize<Dictionary<string, object>>(line); }
        catch { return null; }
    }

    private static bool ShouldIgnoreInternal(Dictionary<string, object> data)
    {
        if (data == null) return true;
        if (data.ContainsKey("source") && "request_retry".Equals(data["source"] as string))
        {
            int attempt = data.ContainsKey("retryAttempt") ? Convert.ToInt32(data["retryAttempt"]) : 0;
            int maxRetries = data.ContainsKey("maxRetries") ? Convert.ToInt32(data["maxRetries"]) : 9;
            if (attempt < maxRetries) return true;
        }
        if (data.ContainsKey("wakeupSource") && "schedule_wakeup".Equals(data["wakeupSource"] as string) &&
            data.ContainsKey("promptSource") && "system".Equals(data["promptSource"] as string))
        {
            return true;
        }
        if (data.ContainsKey("isSidechain") && Equals(data["isSidechain"], true)) return true;
        return false;
    }

    private ParseKind DetectParser(string path, string agentName, List<string> lines)
    {
        string p = (path ?? "").ToLowerInvariant();
        string a = (agentName ?? "").ToLowerInvariant();
        if (p.IndexOf("agent-transcripts", StringComparison.Ordinal) >= 0 || a.IndexOf("cursor", StringComparison.Ordinal) >= 0) return ParseKind.Cursor;
        if (p.IndexOf("claude", StringComparison.Ordinal) >= 0 || a.IndexOf("claude", StringComparison.Ordinal) >= 0) return ParseKind.Claude;
        if (p.IndexOf("codex", StringComparison.Ordinal) >= 0 || a.IndexOf("codex", StringComparison.Ordinal) >= 0) return ParseKind.Codex;
        if (p.IndexOf("antigravity", StringComparison.Ordinal) >= 0 || a.IndexOf("antigravity", StringComparison.Ordinal) >= 0 ||
            a.IndexOf("agy", StringComparison.Ordinal) >= 0) return ParseKind.Antigravity;

        int cap = Math.Min(lines.Count, 12);
        for (int i = 0; i < cap; i++)
        {
            string l = lines[i];
            if (l.IndexOf("PLANNER_RESPONSE", StringComparison.Ordinal) >= 0 || l.IndexOf("USER_INPUT", StringComparison.Ordinal) >= 0)
                return ParseKind.Antigravity;
            if (l.IndexOf("\"turn_ended\"", StringComparison.Ordinal) >= 0 || l.IndexOf("<user_query>", StringComparison.Ordinal) >= 0)
                return ParseKind.Cursor;
            if (l.IndexOf("\"last-prompt\"", StringComparison.Ordinal) >= 0) return ParseKind.Claude;
            if (l.IndexOf("\"event_msg\"", StringComparison.Ordinal) >= 0 || l.IndexOf("\"response_item\"", StringComparison.Ordinal) >= 0)
                return ParseKind.Codex;
        }
        for (int i = Math.Max(cap, lines.Count - 8); i < lines.Count; i++)
        {
            string l = lines[i];
            if (l.IndexOf("PLANNER_RESPONSE", StringComparison.Ordinal) >= 0 || l.IndexOf("USER_INPUT", StringComparison.Ordinal) >= 0)
                return ParseKind.Antigravity;
        }
        return ParseKind.Generic;
    }

    private void ProcessTranscriptLines(List<string> lines, string filePath, string agentName, bool rewritten)
    {
        if (appContext == null || disposed || lines == null || lines.Count == 0) return;
        bool isZh = appContext.Settings == null || appContext.Settings.Language != "en";
        string agentKey = string.IsNullOrEmpty(agentName) ? "AI Agent" : agentName;
        string sessionKey = agentKey + "|" + (filePath ?? "");

        TurnSession session;
        lock (syncLock)
        {
            if (!sessions.TryGetValue(sessionKey, out session))
            {
                session = new TurnSession();
                sessions[sessionKey] = session;
            }
            session.WatchedPath = filePath ?? "";
            session.WatchedAgent = agentKey;
            session.BatchNodes.Clear();
        }

        ParseKind kind = DetectParser(filePath, agentKey, lines);
        ParseOutcome outcome = new ParseOutcome();
        bool topicLocked = false;
        DateTime msgTime = DateTime.MinValue;
        int parsed = 0;

        for (int i = 0; i < lines.Count; i++)
        {
            Dictionary<string, object> data = TryParseLine(lines[i]);
            if (data == null || ShouldIgnoreInternal(data)) continue;
            parsed++;
            DateTime ts = ExtractMessageTimestamp(data, filePath);
            if (ts > msgTime) msgTime = ts;

            switch (kind)
            {
                case ParseKind.Cursor: ParseCursorLine(data, session, isZh, rewritten, ref topicLocked, ref outcome); break;
                case ParseKind.Claude: ParseClaudeLine(data, session, isZh, rewritten, ref topicLocked, ref outcome); break;
                case ParseKind.Codex: ParseCodexLine(data, session, isZh, rewritten, ref topicLocked, ref outcome); break;
                case ParseKind.Antigravity: ParseAntigravityLine(data, session, isZh, rewritten, ref topicLocked, ref outcome); break;
                default: ParseProtocolGenericLine(data, session, isZh, rewritten, ref topicLocked, ref outcome); break;
            }
        }

        if (parsed == 0) return;

        if (msgTime == DateTime.MinValue)
        {
            try { msgTime = File.GetLastWriteTime(filePath); } catch { msgTime = DateTime.Now; }
        }

        lock (syncLock)
        {
            if (string.IsNullOrEmpty(session.Topic) || IsPlaceholderTopic(session.Topic))
            {
                if (!string.IsNullOrEmpty(session.LastToolAction))
                {
                    string fb = CleanUserRequest(session.LastToolAction) ?? session.LastToolAction;
                    session.Topic = fb;
                    session.Target = fb;
                }
                else if (string.IsNullOrEmpty(session.Topic))
                {
                    session.Topic = isZh ? "当前任务" : "Task";
                    session.Target = session.Topic;
                }
            }
            else
            {
                session.Target = session.Topic;
            }
        }

        PublishSession(session, agentKey, filePath, isZh, msgTime, outcome);
    }

    private sealed class ParseOutcome
    {
        internal bool Complete;
        internal bool ExplicitTerminal;
        internal bool Waiting;
        internal bool Error;
        internal bool GenericKeep;
    }

    private static void NoteWaiting(TurnSession session, ref ParseOutcome outcome, string q)
    {
        session.PendingQuestion = q;
        session.State = TurnState.Waiting;
        outcome.Waiting = true;
        outcome.Complete = false;
        outcome.Error = false;
    }

    private static bool IsPlaceholderTopic(string t)
    {
        return t == "当前会话" || t == "当前任务" || t == "Session" || t == "Task";
    }

    private static bool IsHitlShortReply(string text)
    {
        if (string.IsNullOrEmpty(text)) return true;
        if (IsTrivialConfirmation(text)) return true;
        string t = text.Trim();
        if (t.Length > 40) return false;
        if (t.IndexOf('。') >= 0 || t.IndexOf('？') >= 0 || t.IndexOf('?') >= 0) return false;
        return true;
    }

    private void NoteUserIntent(TurnSession s, string text, bool rewritten, ref bool topicLocked)
    {
        if (string.IsNullOrEmpty(text)) return;
        bool trivial = IsTrivialConfirmation(text);
        lock (syncLock)
        {
            s.CurrentTurn = text;
            s.Version++;
            s.AwaitingModel = true;
            s.PendingQuestion = s.PendingError = "";
            s.LastWasGeneric = false;
            bool wasWaiting = s.State == TurnState.Waiting;
            if (s.StartTime == DateTime.MinValue) s.StartTime = DateTime.Now;

            if (wasWaiting && IsHitlShortReply(text))
            {
                s.State = TurnState.Processing;
                return;
            }
            if (trivial && !string.IsNullOrEmpty(s.Topic) && !IsPlaceholderTopic(s.Topic))
            {
                s.State = TurnState.Processing;
                return;
            }

            bool empty = string.IsNullOrEmpty(s.Topic) || IsPlaceholderTopic(s.Topic);
            bool same = !empty && string.Equals(s.Topic, text, StringComparison.Ordinal);
            s.State = TurnState.Processing;
            if (same) return;

            bool newTurn = !empty && !trivial;
            s.Topic = text;
            s.Target = text;
            if (!trivial) topicLocked = true;
            if (newTurn)
            {
                s.StartTime = DateTime.Now;
                s.ActiveStep = s.TotalSteps = 0;
                s.StepDescription = s.LastToolAction = s.Action = s.Thought = s.Conclusion = "";
                s.LowReadCount = 0;
                s.LastHighAction = "";
                s.LastNodeIsThought = false;
                s.AwaitingModel = true;
                s.Node = "";
                s.BatchNodes.Clear();
            }
        }
    }

    private static void OfferBatchNode(TurnSession s, string text)
    {
        if (s == null || string.IsNullOrEmpty(text)) return;
        if (s.BatchNodes.Count > 0 && string.Equals(s.BatchNodes[s.BatchNodes.Count - 1], text, StringComparison.Ordinal))
            return;
        bool isThought = IsThinkingBatchNode(text);
        if (isThought && s.BatchNodes.Count > 0)
        {
            string last = s.BatchNodes[s.BatchNodes.Count - 1];
            if (IsThinkingBatchNode(last))
            {
                s.BatchNodes[s.BatchNodes.Count - 1] = text;
                return;
            }
        }
        s.BatchNodes.Add(text);
    }

    private static bool IsThinkingBatchNode(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        if (text.StartsWith("\ud83d\udcad") || text.StartsWith("\u2687")) return true;
        if (text.StartsWith("\u6b63\u5728\u601d\u8003")) return true;
        return text.StartsWith("Thinking");
    }

    private static string FormatAgentPhase(bool thinking, string sentence, bool isZh)
    {
        string label = isZh ? "\u6b63\u5728\u601d\u8003" : "Thinking";
        if (thinking && string.IsNullOrEmpty(sentence)) return label;
        if (string.IsNullOrEmpty(sentence)) return label;
        return label + " \u00b7 " + sentence;
    }

    private void NoteThought(TurnSession s, string thought, bool isZh)
    {
        if (string.IsNullOrEmpty(thought)) return;
        lock (syncLock)
        {
            s.Thought = thought;
            s.State = TurnState.Processing;
            s.Version++;
            s.LastWasGeneric = false;
            s.LastNodeIsThought = true;
            s.AwaitingModel = false;
            s.PendingQuestion = s.PendingError = "";
        }
        OfferBatchNode(s, FormatAgentPhase(true, thought, isZh));
    }

    private void NoteThinkingPhase(TurnSession s, bool isZh)
    {
        lock (syncLock)
        {
            if (!string.IsNullOrEmpty(s.Thought)) return;
            s.State = TurnState.Processing;
            s.Version++;
            s.LastWasGeneric = false;
            s.LastNodeIsThought = true;
            s.AwaitingModel = false;
            s.PendingQuestion = s.PendingError = "";
        }
        OfferBatchNode(s, FormatAgentPhase(true, null, isZh));
    }

    private void NoteTool(TurnSession s, Dictionary<string, object> data, string summary, bool isZh)
    {
        if (string.IsNullOrEmpty(summary) && data != null) summary = ExtractToolSummary(data, isZh);
        if (string.IsNullOrEmpty(summary)) return;
        int sIdx = 0, tIdx = 0;
        string sd = "";
        bool hasStep = data != null && ExtractStepProgress(data, summary, out sIdx, out tIdx, out sd);
        int rank = RankAction(summary);
        lock (syncLock)
        {
            s.LastToolAction = summary;
            s.Action = summary;
            s.State = TurnState.Processing;
            s.Version++;
            s.LastWasGeneric = false;
            s.LastNodeIsThought = false;
            s.AwaitingModel = false;
            s.PendingQuestion = s.PendingError = "";
            s.Conclusion = "";
            if (rank <= 1)
            {
                s.LastHighAction = summary;
                s.LowReadCount = 0;
            }
            else
            {
                s.LowReadCount++;
            }
            if (hasStep)
            {
                s.ActiveStep = sIdx;
                s.TotalSteps = tIdx;
                s.StepDescription = sd;
            }
        }
        string formatted = FormatAuthenticAction(summary, null, null, 0, 0);
        if (string.IsNullOrEmpty(formatted)) formatted = summary;
        OfferBatchNode(s, formatted);
    }

    private static int RankAction(string action)
    {
        if (string.IsNullOrEmpty(action)) return 1;
        string s = action.ToLowerInvariant();
        if (s.IndexOf("write", StringComparison.Ordinal) >= 0 || s.IndexOf("edit", StringComparison.Ordinal) >= 0 ||
            s.IndexOf("strreplace", StringComparison.Ordinal) >= 0 || s.IndexOf("apply_patch", StringComparison.Ordinal) >= 0 ||
            s.IndexOf("写入", StringComparison.Ordinal) >= 0 || s.IndexOf("修改", StringComparison.Ordinal) >= 0)
            return 0;
        if (s.IndexOf("ask", StringComparison.Ordinal) >= 0 || s.IndexOf("question", StringComparison.Ordinal) >= 0 ||
            s.IndexOf("todo", StringComparison.Ordinal) >= 0)
            return 0;
        if (s.IndexOf("build", StringComparison.Ordinal) >= 0 || s.IndexOf("test", StringComparison.Ordinal) >= 0 ||
            s.IndexOf("selfcheck", StringComparison.Ordinal) >= 0 || s.IndexOf("csc", StringComparison.Ordinal) >= 0)
            return 0;
        if (s.IndexOf("grep", StringComparison.Ordinal) >= 0 || s.IndexOf("search", StringComparison.Ordinal) >= 0 ||
            s.IndexOf("glob", StringComparison.Ordinal) >= 0 || s.IndexOf("subagent", StringComparison.Ordinal) >= 0 ||
            s.IndexOf("搜索", StringComparison.Ordinal) >= 0)
            return 1;
        if (s.IndexOf("read", StringComparison.Ordinal) >= 0 || s.IndexOf("view", StringComparison.Ordinal) >= 0 ||
            s.IndexOf("读取", StringComparison.Ordinal) >= 0 || s.IndexOf("screenshot", StringComparison.Ordinal) >= 0)
            return 2;
        return 1;
    }

    private void PublishSession(TurnSession session, string agentName, string filePath, bool isZh, DateTime msgTime, ParseOutcome outcome)
    {
        if (outcome.Error)
        {
            string err = session.PendingError;
            lock (syncLock) { session.State = TurnState.Idle; session.Version++; }
            if (!AppContext.IsAgentWindowInForeground(agentName)) AppContext.PlayAiSound();
            appContext.SetAgentTaskMessage("⚠️ [" + session.Target + "] " + (isZh ? "执行异常" : "Error"),
                agentName, false, err, null, -2f, msgTime, filePath);
            return;
        }
        if (outcome.Waiting)
        {
            string q = session.PendingQuestion;
            lock (syncLock) { session.State = TurnState.Waiting; session.Version++; }
            if (!AppContext.IsAgentWindowInForeground(agentName)) AppContext.PlayAiSound();
            appContext.SetAgentTaskMessage("❓ [" + session.Target + "] " + (isZh ? "等待决策" : "Waiting for input"),
                agentName, false, q, null, -2f, msgTime, filePath);
            return;
        }
        if (outcome.Complete && !string.IsNullOrEmpty(session.Conclusion))
        {
            int ver;
            lock (syncLock)
            {
                session.PendingCompletionSummary = CleanSummaryText(session.Conclusion);
                ver = ++session.Version;
            }
            int delay = AppContext.IsSelfCheckRunning
                ? (outcome.ExplicitTerminal ? 30 : 60)
                : (outcome.ExplicitTerminal ? 300 : 1200);
            ScheduleCommitCompletion(agentName, filePath, ver, delay, msgTime);
            return;
        }

        string stepProgressText = null;
        float progress = -1f;
        string activeDesc = null;
        string lastAct, thought, curAct;
        int lowN;
        bool genericKeep;
        bool lastThought;
        bool awaitingModel;
        lock (syncLock)
        {
            session.State = TurnState.Processing;
            lastAct = session.LastToolAction;
            curAct = !string.IsNullOrEmpty(session.Action) ? session.Action : lastAct;
            thought = session.Thought;
            lowN = session.LowReadCount;
            genericKeep = outcome.GenericKeep || session.LastWasGeneric;
            lastThought = session.LastNodeIsThought;
            awaitingModel = session.AwaitingModel;
            if (session.ActiveStep > 0 && session.TotalSteps > 0)
            {
                progress = (float)session.ActiveStep / session.TotalSteps;
                activeDesc = session.StepDescription;
                string descPart = !string.IsNullOrEmpty(activeDesc) ? (" " + activeDesc) : "";
                stepProgressText = session.ActiveStep + "/" + session.TotalSteps + descPart;
            }
        }

        string showAct = curAct;
        if (lowN >= 2 && RankAction(curAct) >= 2)
            showAct = isZh ? ("阅读 " + lowN + " 个文件") : ("Read " + lowN + " files");

        string authenticAction = !string.IsNullOrEmpty(showAct)
            ? FormatAuthenticAction(showAct, null, null, 0, 0)
            : null;
        string formattedLast = !string.IsNullOrEmpty(lastAct) ? FormatAuthenticAction(lastAct, null, null, 0, 0) : null;

        string subDetail;
        if (genericKeep) subDetail = !string.IsNullOrEmpty(formattedLast) ? ("✓ " + formattedLast) : authenticAction;
        else if (lastThought && !string.IsNullOrEmpty(thought)) subDetail = FormatAgentPhase(true, thought, isZh);
        else if (!string.IsNullOrEmpty(authenticAction)) subDetail = authenticAction;
        else if (!string.IsNullOrEmpty(thought)) subDetail = FormatAgentPhase(true, thought, isZh);
        else if (!string.IsNullOrEmpty(stepProgressText)) subDetail = "[" + session.ActiveStep + "/" + session.TotalSteps + "]" + (!string.IsNullOrEmpty(activeDesc) ? (" " + activeDesc) : "");
        else subDetail = formattedLast;
        if (string.IsNullOrEmpty(subDetail) && awaitingModel)
            subDetail = FormatAgentPhase(false, null, isZh);

        session.Node = subDetail;
        string procMsg = "⏳ [" + session.Target + "] " + (isZh ? "处理中..." : "Processing...");
        if (session.BatchNodes.Count > 0)
        {
            for (int n = 0; n < session.BatchNodes.Count; n++)
            {
                appContext.SetAgentTaskMessage(procMsg, agentName, true, session.BatchNodes[n], stepProgressText, progress, msgTime, filePath);
            }
            session.BatchNodes.Clear();
        }
        else
        {
            appContext.SetAgentTaskMessage(procMsg, agentName, true, subDetail, stepProgressText, progress, msgTime, filePath);
        }
    }

    private void ParseCursorLine(Dictionary<string, object> data, TurnSession session, bool isZh, bool rewritten, ref bool topicLocked, ref ParseOutcome outcome)
    {
        string type = data.ContainsKey("type") ? data["type"] as string : null;
        if (string.Equals(type, "turn_ended", StringComparison.OrdinalIgnoreCase))
        {
            string st = data.ContainsKey("status") ? data["status"] as string : null;
            if (string.Equals(st, "error", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(data.ContainsKey("reason") ? data["reason"] as string : null, "user_stopped_generation", StringComparison.OrdinalIgnoreCase))
            {
                outcome.Error = true;
                outcome.Complete = false;
                session.PendingError = isZh ? "⚠️ 用户中断" : "⚠️ Stopped by user";
                return;
            }
            if (!string.IsNullOrEmpty(session.Conclusion))
            {
                outcome.Complete = true;
                outcome.ExplicitTerminal = true;
                outcome.Waiting = false;
            }
            return;
        }

        Dictionary<string, object> msg = data.ContainsKey("message") ? data["message"] as Dictionary<string, object> : data;
        string role = (data.ContainsKey("role") ? data["role"] as string : null) ?? (msg != null && msg.ContainsKey("role") ? msg["role"] as string : null);
        IList parts = GetContentParts(msg) ?? GetContentParts(data);

        if (string.Equals(role, "user", StringComparison.OrdinalIgnoreCase) || string.Equals(role, "human", StringComparison.OrdinalIgnoreCase))
        {
            if (ContentIsToolResult(parts)) return;
            string user = ExtractLastHumanText(msg) ?? ExtractLastHumanText(data);
            if (!string.IsNullOrEmpty(user))
            {
                NoteUserIntent(session, user, rewritten, ref topicLocked);
                outcome.Complete = false;
                outcome.Waiting = false;
                outcome.Error = false;
            }
            return;
        }

        if (!string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase) && parts == null) return;

        string q = ExtractQuestion(data, isZh);
        if (!string.IsNullOrEmpty(q))
        {
            NoteWaiting(session, ref outcome, q);
            return;
        }

        bool hasTool = PartsHaveTool(parts);
        string narr = ExtractAssistantNarration(parts, hasTool);
        string thought = ExtractThoughtFromParts(parts) ?? (hasTool ? narr : null);
        if (!string.IsNullOrEmpty(thought)) NoteThought(session, thought, isZh);
        else if (!hasTool && PartsHaveThinking(parts)) NoteThinkingPhase(session, isZh);
        if (hasTool)
        {
            NoteTool(session, data, ExtractToolSummary(data, isZh), isZh);
            outcome.Complete = false;
            outcome.GenericKeep = false;
            return;
        }
        if (!string.IsNullOrEmpty(narr))
        {
            session.Conclusion = narr;
            NoteThought(session, ExtractThoughtSlice(data) ?? FirstSentence(narr), isZh);
        }
    }

    private void ParseClaudeLine(Dictionary<string, object> data, TurnSession session, bool isZh, bool rewritten, ref bool topicLocked, ref ParseOutcome outcome)
    {
        string type = data.ContainsKey("type") ? data["type"] as string : null;
        if (string.Equals(type, "attachment", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type, "queue-operation", StringComparison.OrdinalIgnoreCase))
            return;

        if (string.Equals(type, "last-prompt", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type, "custom-title", StringComparison.OrdinalIgnoreCase))
        {
            string user = ExtractUserRequest(data);
            if (!string.IsNullOrEmpty(user))
            {
                NoteUserIntent(session, user, rewritten, ref topicLocked);
                outcome.Complete = false;
                outcome.Waiting = false;
            }
            return;
        }

        if (string.Equals(type, "user", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(data.ContainsKey("role") ? data["role"] as string : null, "user", StringComparison.OrdinalIgnoreCase))
        {
            if (IsToolResultUserRow(data) || !IsHumanOrigin(data)) return;
            string user = ExtractLastHumanText(data) ?? ExtractUserRequest(data);
            if (!string.IsNullOrEmpty(user))
            {
                NoteUserIntent(session, user, rewritten, ref topicLocked);
                outcome.Complete = false;
                outcome.Waiting = false;
            }
            return;
        }

        Dictionary<string, object> msg = data.ContainsKey("message") ? data["message"] as Dictionary<string, object> : data;
        IList parts = GetContentParts(msg) ?? GetContentParts(data);
        string q = ExtractQuestion(data, isZh);
        if (!string.IsNullOrEmpty(q))
        {
            NoteWaiting(session, ref outcome, q);
            return;
        }

        string err = ExtractErrorSummary(data, isZh);
        if (!string.IsNullOrEmpty(err))
        {
            session.PendingError = err;
            outcome.Error = true;
            outcome.Complete = false;
            return;
        }

        string thought = ExtractThoughtFromParts(parts) ?? ExtractThoughtSlice(data);
        if (!string.IsNullOrEmpty(thought)) NoteThought(session, thought, isZh);

        bool hasTool = PartsHaveTool(parts);
        if (hasTool)
        {
            NoteTool(session, data, ExtractToolSummary(data, isZh), isZh);
            outcome.Complete = false;
            return;
        }

        object sr = msg != null && msg.ContainsKey("stop_reason") ? msg["stop_reason"] : (data.ContainsKey("stop_reason") ? data["stop_reason"] : null);
        string stop = sr as string;
        string text = ExtractAssistantNarration(parts, false);
        if (("end_turn".Equals(stop) || "stop_sequence".Equals(stop)) && !hasTool && !string.IsNullOrEmpty(text))
        {
            session.Conclusion = text;
            outcome.Complete = true;
            outcome.ExplicitTerminal = true;
            return;
        }
        if (!string.IsNullOrEmpty(text)) session.Conclusion = text;
    }

    private void ParseCodexLine(Dictionary<string, object> data, TurnSession session, bool isZh, bool rewritten, ref bool topicLocked, ref ParseOutcome outcome)
    {
        Dictionary<string, object> payload = data.ContainsKey("payload") ? data["payload"] as Dictionary<string, object> : null;
        Dictionary<string, object> item = payload != null && payload.ContainsKey("item") ? payload["item"] as Dictionary<string, object> : null;
        string pType = payload != null && payload.ContainsKey("type") ? payload["type"] as string : null;
        string itemType = item != null && item.ContainsKey("type") ? item["type"] as string : null;

        if ("task_complete".Equals(pType) || "task_completed".Equals(pType))
        {
            string msg = null;
            if (payload.ContainsKey("last_agent_message") && payload["last_agent_message"] != null)
                msg = payload["last_agent_message"].ToString().Trim();
            else if (payload.ContainsKey("message") && payload["message"] != null)
                msg = payload["message"].ToString().Trim();
            if (string.IsNullOrEmpty(msg)) msg = session.Conclusion;
            if (!string.IsNullOrEmpty(msg))
            {
                session.Conclusion = msg;
                outcome.Complete = true;
                outcome.ExplicitTerminal = true;
            }
            return;
        }

        if ("turn_aborted".Equals(pType))
        {
            string err = ExtractErrorSummary(data, isZh);
            session.PendingError = err;
            outcome.Error = true;
            outcome.Complete = false;
            return;
        }

        if ("UserMessage".Equals(itemType) || IsCodexHumanUserRow(data, payload, item))
        {
            Dictionary<string, object> src = item ?? payload ?? data;
            if (IsCodexNonHumanUser(src) || IsCodexNonHumanUser(payload) || IsCodexNonHumanUser(data)) return;
            string user = ExtractLastHumanText(src) ?? ExtractUserRequest(data);
            if (!string.IsNullOrEmpty(user))
            {
                NoteUserIntent(session, user, rewritten, ref topicLocked);
                outcome.Complete = false;
                outcome.Waiting = false;
            }
            return;
        }

        string thought = ExtractCodexThought(data, payload, item);
        if (!string.IsNullOrEmpty(thought)) NoteThought(session, thought, isZh);

        string tool = ExtractToolSummary(data, isZh);
        if (!string.IsNullOrEmpty(tool))
        {
            NoteTool(session, data, tool, isZh);
            outcome.Complete = false;
            outcome.GenericKeep = "function_call_output".Equals(pType);
            return;
        }

        if ("function_call_output".Equals(pType))
        {
            outcome.GenericKeep = true;
            session.LastWasGeneric = true;
            outcome.Complete = false;
        }
    }

    private void ParseAntigravityLine(Dictionary<string, object> data, TurnSession session, bool isZh, bool rewritten, ref bool topicLocked, ref ParseOutcome outcome)
    {
        string type = data.ContainsKey("type") ? data["type"] as string : null;
        if (string.Equals(type, "SYSTEM_MESSAGE", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type, "CHECKPOINT", StringComparison.OrdinalIgnoreCase))
            return;

        if (string.Equals(type, "USER_INPUT", StringComparison.OrdinalIgnoreCase))
        {
            string user = ExtractUserRequest(data);
            if (!string.IsNullOrEmpty(user))
            {
                NoteUserIntent(session, user, rewritten, ref topicLocked);
                outcome.Complete = false;
                outcome.Waiting = false;
                outcome.Error = false;
            }
            return;
        }

        if (string.Equals(type, "GENERIC", StringComparison.OrdinalIgnoreCase))
        {
            string st = data.ContainsKey("status") ? data["status"] as string : null;
            string echoed = data.ContainsKey("content") && data["content"] != null ? data["content"].ToString() : null;
            string line = FirstSentence(echoed);
            lock (syncLock) { session.Version++; }
            if (!string.IsNullOrEmpty(line))
            {
                session.LastWasGeneric = false;
                session.LastNodeIsThought = false;
                outcome.GenericKeep = false;
                outcome.Complete = false;
                OfferBatchNode(session, line);
                return;
            }
            session.LastWasGeneric = true;
            outcome.GenericKeep = true;
            if (string.Equals(st, "RUNNING", StringComparison.OrdinalIgnoreCase))
            {
                outcome.Complete = false;
            }
            return;
        }

        if (!string.Equals(type, "PLANNER_RESPONSE", StringComparison.OrdinalIgnoreCase)) return;

        string q = ExtractQuestion(data, isZh);
        if (!string.IsNullOrEmpty(q))
        {
            NoteWaiting(session, ref outcome, q);
            return;
        }

        bool hasTool = data.ContainsKey("tool_calls") && data["tool_calls"] is IList && ((IList)data["tool_calls"]).Count > 0;
        string thought = ExtractThoughtSlice(data);
        if (!string.IsNullOrEmpty(thought)) NoteThought(session, thought, isZh);

        if (hasTool)
        {
            NoteTool(session, data, ExtractToolSummary(data, isZh), isZh);
            outcome.Complete = false;
            outcome.GenericKeep = false;
            outcome.Waiting = false;
            return;
        }

        string content = null;
        if (data.ContainsKey("content") && data["content"] != null)
        {
            content = data["content"] as string;
            if (content == null) content = data["content"].ToString();
        }
        if (!string.IsNullOrEmpty(content))
        {
            content = content.Trim();
            session.Conclusion = content;
            outcome.Complete = true;
            outcome.ExplicitTerminal = true;
            outcome.Waiting = false;
            return;
        }
    }

    private void ParseProtocolGenericLine(Dictionary<string, object> data, TurnSession session, bool isZh, bool rewritten, ref bool topicLocked, ref ParseOutcome outcome)
    {
        string type = data.ContainsKey("type") ? data["type"] as string : null;
        if (!string.IsNullOrEmpty(type) && NoiseTypes.Contains(type)) return;
        if (string.Equals(type, "GENERIC", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type, "tool_output", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type, "tool_response", StringComparison.OrdinalIgnoreCase))
        {
            string echoed = data.ContainsKey("content") && data["content"] != null ? data["content"].ToString() : null;
            string line = FirstSentence(echoed);
            if (!string.IsNullOrEmpty(line))
            {
                session.LastWasGeneric = false;
                outcome.GenericKeep = false;
                outcome.Complete = false;
                OfferBatchNode(session, line);
                return;
            }
            outcome.GenericKeep = true;
            session.LastWasGeneric = true;
            outcome.Complete = false;
            return;
        }

        Dictionary<string, object> body = UnwrapMessageBody(data);
        string role = (body != null && body.ContainsKey("role") ? body["role"] as string : null) ??
                      (data.ContainsKey("role") ? data["role"] as string : null);
        if (string.Equals(role, "developer", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "system", StringComparison.OrdinalIgnoreCase))
            return;
        if (IsCodexNonHumanUser(body) || IsCodexNonHumanUser(data)) return;

        IList parts = GetContentParts(body) ?? GetContentParts(data);
        if (ContentIsToolResult(parts) || IsToolResultUserRow(data)) return;

        if (string.Equals(role, "user", StringComparison.OrdinalIgnoreCase) || string.Equals(role, "human", StringComparison.OrdinalIgnoreCase))
        {
            string user = ExtractLastHumanText(body) ?? ExtractLastHumanText(data) ?? ExtractUserRequest(data);
            if (!string.IsNullOrEmpty(user))
            {
                NoteUserIntent(session, user, rewritten, ref topicLocked);
                outcome.Complete = false;
                outcome.Waiting = false;
            }
            return;
        }

        string q = ExtractQuestion(data, isZh);
        if (string.IsNullOrEmpty(q) && body != data) q = ExtractQuestion(body, isZh);
        if (!string.IsNullOrEmpty(q))
        {
            NoteWaiting(session, ref outcome, q);
            return;
        }

        string err = ExtractErrorSummary(data, isZh);
        if (!string.IsNullOrEmpty(err))
        {
            session.PendingError = err;
            outcome.Error = true;
            outcome.Complete = false;
            return;
        }

        string thought = ExtractThoughtFromParts(parts) ?? ExtractThoughtSlice(body) ?? ExtractThoughtSlice(data);
        if (!string.IsNullOrEmpty(thought)) NoteThought(session, thought, isZh);

        bool hasTool = PartsHaveTool(parts) ||
                       (data.ContainsKey("tool_calls") && data["tool_calls"] is IList && ((IList)data["tool_calls"]).Count > 0) ||
                       (body != null && body.ContainsKey("tool_calls") && body["tool_calls"] is IList && ((IList)body["tool_calls"]).Count > 0);
        if (hasTool)
        {
            NoteTool(session, data, ExtractToolSummary(data, isZh) ?? ExtractToolSummary(body, isZh), isZh);
            outcome.Complete = false;
            return;
        }

        string finish = (body != null && body.ContainsKey("finish_reason") ? body["finish_reason"] as string : null) ??
                        (body != null && body.ContainsKey("stop_reason") ? body["stop_reason"] as string : null) ??
                        (data.ContainsKey("finish_reason") ? data["finish_reason"] as string : null) ??
                        (data.ContainsKey("stop_reason") ? data["stop_reason"] as string : null);
        string text = ExtractAssistantNarration(parts, false);
        if (string.IsNullOrEmpty(text) && body != null && body.ContainsKey("content") && body["content"] is string)
            text = (string)body["content"];
        if (string.IsNullOrEmpty(text) && data.ContainsKey("content") && data["content"] is string)
            text = (string)data["content"];

        bool explicitStop = false;
        if (!string.IsNullOrEmpty(finish))
        {
            string f = finish.ToLowerInvariant();
            explicitStop = f == "stop" || f == "end_turn" || f == "stop_sequence";
        }

        if (explicitStop && !string.IsNullOrEmpty(text))
        {
            session.Conclusion = text;
            outcome.Complete = true;
            outcome.ExplicitTerminal = true;
            return;
        }

        string status = data.ContainsKey("status") ? data["status"] as string : (body != null && body.ContainsKey("status") ? body["status"] as string : null);
        if (!string.IsNullOrEmpty(status))
        {
            string su = status.ToUpperInvariant();
            if (su == "DONE" || su == "COMPLETED" || su == "SUCCESS" || su == "FINISHED")
            {
                if (!string.IsNullOrEmpty(text) && string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase))
                    session.Conclusion = text;
                return;
            }
        }

        if (string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(text) && !hasTool)
        {
            session.Conclusion = text;
            outcome.Complete = true;
            outcome.ExplicitTerminal = false;
        }
    }

    private static Dictionary<string, object> UnwrapMessageBody(Dictionary<string, object> data)
    {
        if (data == null) return null;
        if (data.ContainsKey("message") && data["message"] is Dictionary<string, object>)
            return (Dictionary<string, object>)data["message"];
        Dictionary<string, object> payload = data.ContainsKey("payload") ? data["payload"] as Dictionary<string, object> : null;
        if (payload != null)
        {
            if (payload.ContainsKey("message") && payload["message"] is Dictionary<string, object>)
                return (Dictionary<string, object>)payload["message"];
            if (payload.ContainsKey("item") && payload["item"] is Dictionary<string, object>)
                return (Dictionary<string, object>)payload["item"];
            if (payload.ContainsKey("role") || payload.ContainsKey("content"))
                return payload;
        }
        if (data.ContainsKey("choices") && data["choices"] is IList)
        {
            IList ch = (IList)data["choices"];
            if (ch.Count > 0 && ch[0] is Dictionary<string, object>)
            {
                Dictionary<string, object> c0 = (Dictionary<string, object>)ch[0];
                if (c0.ContainsKey("delta") && c0["delta"] is Dictionary<string, object>) return (Dictionary<string, object>)c0["delta"];
                if (c0.ContainsKey("message") && c0["message"] is Dictionary<string, object>) return (Dictionary<string, object>)c0["message"];
            }
        }
        return data;
    }

    private static IList GetContentParts(Dictionary<string, object> d)
    {
        if (d == null) return null;
        if (d.ContainsKey("content") && d["content"] is IList) return (IList)d["content"];
        return null;
    }

    private static bool PartsHaveThinking(IList parts)
    {
        if (parts == null) return false;
        foreach (object item in parts)
        {
            Dictionary<string, object> part = item as Dictionary<string, object>;
            if (part == null) continue;
            string pType = part.ContainsKey("type") ? part["type"] as string : null;
            if ("thinking".Equals(pType) || "reasoning".Equals(pType) || part.ContainsKey("thinking") || part.ContainsKey("reasoning_content"))
                return true;
            if (part.ContainsKey("thought") && Equals(part["thought"], true)) return true;
        }
        return false;
    }

    private static bool PartsHaveTool(IList parts)
    {
        if (parts == null) return false;
        foreach (object item in parts)
        {
            Dictionary<string, object> part = item as Dictionary<string, object>;
            if (part == null) continue;
            string pType = part.ContainsKey("type") ? part["type"] as string : null;
            if ("tool_use".Equals(pType) || "function_call".Equals(pType) || "custom_tool_call".Equals(pType)) return true;
        }
        return false;
    }

    private static bool ContentIsToolResult(IList parts)
    {
        if (parts == null || parts.Count == 0) return false;
        bool any = false;
        foreach (object item in parts)
        {
            Dictionary<string, object> part = item as Dictionary<string, object>;
            if (part == null) return false;
            string pType = part.ContainsKey("type") ? part["type"] as string : null;
            if ("tool_result".Equals(pType) || "function_call_output".Equals(pType) || "tool_output".Equals(pType))
                any = true;
            else
                return false;
        }
        return any;
    }

    private static bool IsToolResultUserRow(Dictionary<string, object> data)
    {
        if (data == null) return false;
        IList parts = GetContentParts(data);
        if (parts == null && data.ContainsKey("message") && data["message"] is Dictionary<string, object>)
            parts = GetContentParts((Dictionary<string, object>)data["message"]);
        return ContentIsToolResult(parts);
    }

    private static bool IsHumanOrigin(Dictionary<string, object> data)
    {
        if (data == null) return true;
        object origin = data.ContainsKey("origin") ? data["origin"] : null;
        Dictionary<string, object> od = origin as Dictionary<string, object>;
        if (od == null) return true;
        string kind = od.ContainsKey("kind") ? od["kind"] as string : null;
        if (string.IsNullOrEmpty(kind)) return true;
        return string.Equals(kind, "human", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReminderOrMetaText(string s)
    {
        if (string.IsNullOrEmpty(s)) return true;
        if (s.IndexOf("<user_query", StringComparison.OrdinalIgnoreCase) >= 0 ||
            s.IndexOf("<USER_REQUEST", StringComparison.OrdinalIgnoreCase) >= 0 ||
            s.IndexOf("<prompt", StringComparison.OrdinalIgnoreCase) >= 0)
            return false;
        string t = s.TrimStart();
        if (t.StartsWith("<system-reminder", StringComparison.OrdinalIgnoreCase) ||
            t.StartsWith("<system_reminder", StringComparison.OrdinalIgnoreCase) ||
            t.StartsWith("<timestamp", StringComparison.OrdinalIgnoreCase) ||
            t.StartsWith("<additional_metadata", StringComparison.OrdinalIgnoreCase) ||
            t.StartsWith("<user_settings_change", StringComparison.OrdinalIgnoreCase) ||
            t.StartsWith("<system_info", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    private static string ExtractLastHumanText(Dictionary<string, object> d)
    {
        if (d == null) return null;
        if (d.ContainsKey("message") && d["message"] is Dictionary<string, object>)
        {
            string nested = ExtractLastHumanText((Dictionary<string, object>)d["message"]);
            if (!string.IsNullOrEmpty(nested)) return nested;
        }
        object content = d.ContainsKey("content") ? d["content"] : null;
        if (content is string)
        {
            string s = (string)content;
            if (IsReminderOrMetaText(s) && s.IndexOf("<user_query", StringComparison.OrdinalIgnoreCase) < 0 &&
                s.IndexOf("<USER_REQUEST", StringComparison.OrdinalIgnoreCase) < 0)
                return null;
            return CleanUserRequest(s);
        }
        if (content is IList)
        {
            string last = null;
            foreach (object item in (IList)content)
            {
                if (item is string)
                {
                    string s = (string)item;
                    if (!IsReminderOrMetaText(s)) last = s;
                    continue;
                }
                Dictionary<string, object> part = item as Dictionary<string, object>;
                if (part == null) continue;
                string pType = part.ContainsKey("type") ? part["type"] as string : null;
                if ("tool_result".Equals(pType) || "function_call_output".Equals(pType) || "tool_use".Equals(pType) ||
                    "function_call".Equals(pType) || "thinking".Equals(pType))
                    continue;
                if ("text".Equals(pType) || "input_text".Equals(pType) || part.ContainsKey("text"))
                {
                    string tx = part.ContainsKey("text") && part["text"] != null ? part["text"].ToString() : null;
                    if (!string.IsNullOrEmpty(tx) && !IsReminderOrMetaText(tx)) last = tx;
                }
            }
            return string.IsNullOrEmpty(last) ? null : CleanUserRequest(last);
        }
        return null;
    }

    private static string ExtractThoughtFromParts(IList parts)
    {
        if (parts == null) return null;
        foreach (object item in parts)
        {
            Dictionary<string, object> part = item as Dictionary<string, object>;
            if (part == null) continue;
            string pType = part.ContainsKey("type") ? part["type"] as string : null;
            if ("thinking".Equals(pType) || "reasoning".Equals(pType) || part.ContainsKey("thinking") || part.ContainsKey("reasoning_content"))
            {
                object raw = part.ContainsKey("thinking") ? part["thinking"] :
                    (part.ContainsKey("reasoning_content") ? part["reasoning_content"] :
                    (part.ContainsKey("text") ? part["text"] : null));
                string s = FirstSentence(raw != null ? raw.ToString() : null);
                if (!string.IsNullOrEmpty(s)) return s;
            }
            if (part.ContainsKey("thought") && Equals(part["thought"], true) && part.ContainsKey("text"))
            {
                string s = FirstSentence(part["text"].ToString());
                if (!string.IsNullOrEmpty(s)) return s;
            }
        }
        return null;
    }

    private static string ExtractAssistantNarration(IList parts, bool preferBeforeTool)
    {
        if (parts == null) return null;
        string last = null;
        foreach (object item in parts)
        {
            Dictionary<string, object> part = item as Dictionary<string, object>;
            if (part == null)
            {
                if (item is string) last = (string)item;
                continue;
            }
            string pType = part.ContainsKey("type") ? part["type"] as string : null;
            if ("tool_use".Equals(pType) || "function_call".Equals(pType))
            {
                if (preferBeforeTool) return last;
                continue;
            }
            if ("thinking".Equals(pType) || "reasoning".Equals(pType)) continue;
            if (part.ContainsKey("thought") && Equals(part["thought"], true)) continue;
            if ("text".Equals(pType) || "input_text".Equals(pType) || part.ContainsKey("text"))
            {
                if (part.ContainsKey("text") && part["text"] != null)
                {
                    string tx = part["text"].ToString().Trim();
                    if (!string.IsNullOrEmpty(tx) && !IsReminderOrMetaText(tx)) last = tx;
                }
            }
        }
        return last;
    }

    private static string FirstSentence(string s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        s = s.Trim();
        if (s.Length < 3) return null;
        int dot = s.IndexOfAny(SentenceDelims);
        if (dot > 0) s = s.Substring(0, dot).Trim();
        return s.Length > 2 ? s : null;
    }

    private static bool IsCodexNonHumanUser(Dictionary<string, object> d)
    {
        if (d == null) return false;
        string kinds = CollectKinds(d);
        if (string.IsNullOrEmpty(kinds)) return false;
        string k = kinds.ToLowerInvariant();
        if (k.IndexOf("agents_md", StringComparison.Ordinal) >= 0 ||
            k.IndexOf("environment_context", StringComparison.Ordinal) >= 0 ||
            k.IndexOf("environments.", StringComparison.Ordinal) >= 0)
            return true;
        if (k.IndexOf("user.text", StringComparison.Ordinal) >= 0) return false;
        return k.Length > 0;
    }

    private static bool IsCodexHumanUserRow(Dictionary<string, object> data, Dictionary<string, object> payload, Dictionary<string, object> item)
    {
        if (payload != null && "user".Equals(payload.ContainsKey("role") ? payload["role"] : null) && !IsCodexNonHumanUser(payload))
        {
            string kinds = CollectKinds(payload);
            if (string.IsNullOrEmpty(kinds) || kinds.IndexOf("user.text", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        if (data != null && "user".Equals(data.ContainsKey("role") ? data["role"] : null) && !IsCodexNonHumanUser(data))
            return true;
        return false;
    }

    private static string CollectKinds(Dictionary<string, object> d)
    {
        if (d == null) return "";
        StringBuilder sb = new StringBuilder();
        object kinds = d.ContainsKey("content_item_kinds") ? d["content_item_kinds"] : null;
        if (kinds is IList)
        {
            foreach (object k in (IList)kinds) if (k != null) sb.Append(k.ToString()).Append(',');
        }
        else if (kinds is string) sb.Append((string)kinds);
        object prov = d.ContainsKey("provenance") ? d["provenance"] : null;
        if (prov is IList)
        {
            foreach (object k in (IList)prov) if (k != null) sb.Append(k.ToString()).Append(',');
        }
        else if (prov is string) sb.Append((string)prov);
        else if (prov is Dictionary<string, object>)
        {
            Dictionary<string, object> pd = (Dictionary<string, object>)prov;
            if (pd.ContainsKey("type") && pd["type"] != null) sb.Append(pd["type"].ToString());
        }
        return sb.ToString();
    }

    private static string ExtractCodexThought(Dictionary<string, object> data, Dictionary<string, object> payload, Dictionary<string, object> item)
    {
        if (item != null && "Reasoning".Equals(item.ContainsKey("type") ? item["type"] : null))
        {
            if (item.ContainsKey("summary_text") && item["summary_text"] != null)
                return FirstSentence(item["summary_text"].ToString());
        }
        Dictionary<string, object> src = payload ?? data;
        if (src != null && "Reasoning".Equals(src.ContainsKey("type") ? src["type"] : null) && src.ContainsKey("summary_text"))
            return FirstSentence(src["summary_text"].ToString());
        if (src != null && "reasoning".Equals(src.ContainsKey("type") ? src["type"] as string : null) && src.ContainsKey("summary") && src["summary"] is IList)
        {
            foreach (object s in (IList)src["summary"])
            {
                Dictionary<string, object> row = s as Dictionary<string, object>;
                if (row != null && row.ContainsKey("text") && row["text"] != null)
                {
                    string t = FirstSentence(row["text"].ToString());
                    if (!string.IsNullOrEmpty(t)) return t;
                }
            }
        }
        if (data != null && data.ContainsKey("item") && data["item"] is Dictionary<string, object>)
        {
            Dictionary<string, object> it = (Dictionary<string, object>)data["item"];
            if ("Reasoning".Equals(it.ContainsKey("type") ? it["type"] : null) && it.ContainsKey("summary_text"))
                return FirstSentence(it["summary_text"].ToString());
        }
        return null;
    }

    private static bool TryExtractTodoProgress(IList calls, out int step, out int total, out string desc)
    {
        step = 0;
        total = 0;
        desc = "";
        if (calls == null) return false;
        foreach (object cObj in calls)
        {
            Dictionary<string, object> call = cObj as Dictionary<string, object>;
            if (call == null) continue;
            string name = call.ContainsKey("name") ? call["name"] as string : null;
            if (!string.Equals(name, "TodoWrite", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(name, "todo", StringComparison.OrdinalIgnoreCase))
                continue;
            Dictionary<string, object> args = null;
            if (call.ContainsKey("parameters")) args = call["parameters"] as Dictionary<string, object>;
            if (args == null && call.ContainsKey("input")) args = call["input"] as Dictionary<string, object>;
            if (args == null && call.ContainsKey("args")) args = call["args"] as Dictionary<string, object>;
            IList todos = args != null && args.ContainsKey("todos") ? args["todos"] as IList : null;
            if (todos == null || todos.Count == 0) continue;
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
        return false;
    }
}
