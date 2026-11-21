# Claude Code 状态感知逆向

## 安装形态（本机实证）

- 桌面壳（3p 渠道）：`%LOCALAPPDATA%\Claude-3p\`（入口 `claude-desktop-3p`）。
- Claude Code 本体：`%LOCALAPPDATA%\Claude-3p\claude-code\2.1.325\<hash>\claude.exe`，
  372MB Bun 单文件 exe。REA 无已配置原生引擎（Ghidra 未安装），本轮不做静态反编译，
  以运行时取证为准。
- 另有 `%LOCALAPPDATA%\Claude\`（claude_desktop_config.json）与 `%LOCALAPPDATA%\Claude-Data\`。

## 一级状态源：进程注册表 `~/.claude/sessions/<pid>.json`

每次启动写一份，PID 命名，配 `<pid>.<hash>.key` 伴生文件。实测样本（2026-11-20，字段全量）：

```json
{"pid":52356,"sessionId":"1efc3363-...","cwd":"D:\\AIStudio\\REA-THS",
 "startedAt":1795140595084,"version":"2.1.325","kind":"interactive",
 "entrypoint":"claude-desktop-3p","hostSessionId":"local_37e6d765-...",
 "messagingSocketPath":"\\\\.\\pipe\\LOCAL\\cc-msg-60f2bbd9...",
 "name":"Browse setup for Chrome","nameSource":"user",
 "status":"idle","statusUpdatedAt":1795140823016,
 "peerFeatures":["notify_idle","artifact_yield"]}
```

关键观察：

- `status` 是一等公民状态字段（实测值 `idle`；推断存在 `running` 等运行态，
  **运行态具体枚举值未知**，需挂管道或高频采样验证）。
- `updatedAt` / `statusUpdatedAt` 毫秒时间戳 → 可直接驱动 HUD 的去抖与耗时显示。
- `messagingSocketPath` 是本地命名管道（`peerProtocol: 1`），`notify_idle`
  说明存在主动推送空闲事件的协议 —— **未逆向，列为后续高价值目标**。
- `name`（`nameSource: user`）= 用户命名的会话标题，可直接喂给状态栏胶囊文案。

**注册表非心跳陷阱（2026-11-21 实证）**：`status=busy` 只在 turn 开始时写入，
turn 结束/等待后**不会主动更新回 idle**（实证：转录静默 104 秒、注册表仍停在
43 分钟前的 busy）。用注册表判定「运行中」必须叠加 `statusUpdatedAt/updatedAt`
新鲜度（>2 分钟视为停滞），否则 HUD 会一直显示一个早已停下的会话。

**校准完成（2026-11-21）**：活跃会话实证，运行态枚举值为 **`busy`**（非
`running`）。高频采样（800ms/2s 间隔，30+ 秒）确认 `busy` 持续稳定；
`updatedAt` 时间戳不随时间推进，只在状态变更时更新，**不是心跳**。
 waiting-input 目录在 `busy` 时无该会话文件，与状态一致。

代码 `status != "idle"` 的判定逻辑**碰巧正确**（`busy != "idle"`），但
枚举值本身已确认为 `busy`。

## 二级状态源：桌面壳会话目录
`%LOCALAPPDATA%\Claude-3p\claude-code-sessions\<profile>\<uuid>\`

实测结构：

```
00000000-0000-4000-8000-000000000001\           ← 活跃会话 local_<hostSessionId>.json
00000000-0000-4000-8000-000000000001\waiting-input\  ← 等待用户输入的会话被移入此目录
deleted_<uuid>                                   ← 13 字节墓碑文件
00000000-....profile-origin.json
```

`local_*.json` 字段（23KB 样本，键全量）：`cliSessionId`、`completedTurns`、
`interruptedAwaitingPerson`、`lastActivityAt`、`lastTurnEdgeAt`、`latestUserFrameAt`、
`postTurnSummary`、`turnBoxDeclared/Mounted/WrapUp`、`permissionMode`、`title`、
`priorCliSessionIds` 等。

**waiting-input 文件实证修正（2026-11-21）**：waiting-input 下的文件**不是**
主会话 `local_*.json` 的完整拷贝，而是轻量子集，顶层字段为
`{v, sessionId(=local_ 壳 ID), savedAt(ms), cleanShutdown, entries[]}`——
**没有 `cliSessionId` 字段**。真正的 CLI 会话 UUID 在
`entries[].msg.session_id`（取最后一个带该字段的 entry）。另外 waiting-input
文件会被壳残留（实证：一个引用 23 天前死会话的文件仍躺在目录里），采纳前必须
用 `savedAt` 新鲜度（>10min 视为残留）+ 引用会话当前活性双重过滤。

状态推导：

- 会话 JSON 出现在 `waiting-input\` 子目录，或 `interruptedAwaitingPerson=true`
  → HUD 的「等待决策」态（amber ❓）。
- `lastActivityAt` 推进且不在 waiting-input → running。

## 三级状态源：项目转录 `~/.claude/projects/<encoded-project>/<sessionId>.jsonl`

实测 25.5MB 活跃转录，行类型分布（Tail 3000 行）：

```
assistant 864 | user 496 | api-request 425 | api-request-blob 181
custom-title 152 | last-prompt 146 | agent-name 145 | pr-link 145
atis-latch 145 | attachment 100 | mode 80 | queue-operation 52
system 37 | file-history-delta 13 | file-history-snapshot 10
cost-state 3 | tools-used 1
```

- 子代理：`<sessionId>\subagents\agent-*.jsonl`（每个 1MB+）+ `agent-*.meta.json`；
  主转录内 `isSidechain=true` 标记子代理行。`agent-name` 行提供子代理显示名。
- `cost-state` 行带 `totalDuration`、`totalAPIDuration`、`totalCostUSD`、
  `totalLinesAdded/Removed`、`modelUsage` —— 可做累计耗时/成本的权威锚点。
- `queue-operation` 行表明存在排队消息机制。

## 映射到 iClock

| AgentTaskStatus | 来源 |
|---|---|
| `AgentName` | `claude`（`@` 后缀可用 profile/项目编码区分） |
| `SessionKey` | `sessionId`（注册表）或转录文件路径 |
| `IsThinking` | `status==running`（或转录尾部无完成标记且 mtime 推进） |
| `Message` | `name` + 最后一条 assistant 的 goal 提取（沿用现有 ExtractGoal） |
| `TurnStartTime` | `startedAt`（注册表）或最后一个 user 行时间戳 |
| `MessageTimestamp` | `updatedAt` |
| 等待决策 | `waiting-input\` 目录出现 / `interruptedAwaitingPerson` |
| 清理 | `<pid>.json` 消失或 `GetProcessById(pid)` 失败 |

## 已知未知

- `status` 运行态枚举值（需管道嗅探或对比运行中注册表）。
- 命名管道消息协议（`peerProtocol` 1）。
- `atis-latch`、`turnBox*` 字段的准确语义（命名推断，未验证）。

## REA 记录

- 本轮对 claude.exe 未执行 REA 分析（无原生 provider）；建议后续安装 Ghidra 后
  用 `rea open_binary` + `rea search "statusUpdatedAt"` 定位注册表写入点。
*** Add File: rea/codex_cli.md
