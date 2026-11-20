# ZCode 状态感知逆向

## 安装形态（本机实证）

- GUI：`C:\Program Files\ZCode\ZCode.exe`（Electron，`resources\app.asar` 384MB），
  实测 15 个进程常驻。
- 数据：`~/.zcode\`（cli/v2/workspace）+ `%APPDATA%\ZCode\session\`。

## 一级状态源：宿主日志的结构化内存计数（本机实证）
`~/.zcode\v2\logs\YYYY-MM-DD.log`（按天滚动，活跃日持续追加）

宿主进程以 ~60s 周期输出一行 `[memory] role=utility_host ...`，内含 agent 状态计数：

```
agent.pendingPermissions=0 agent.pendingUserInputs=0 agent.seqStates=0
agent.sessionEmitters=0 bots.liveStatusProgress=0 bots.runningTasks=0
bots.streamSubs=0 task.runtimeCommands=0 task.taskEmitters=0
task.toolMemoryTasks=0
```

语义映射（命名推断，历史样本全为 0 —— 本机取证窗口内无活跃任务，
**非零样本缺失，列为未知**）：

- `bots.runningTasks > 0` 或 `agent.seqStates > 0` → 有 turn 在执行 → 运行中。
- `agent.pendingUserInputs > 0` 或 `agent.pendingPermissions > 0` → 等待人工输入 → 等待态。
- 全部归零 + `taskBus.sessionRoutes > 0` → 空闲但会话仍挂载。

**关键实证修正（2026-11-21）**：历史日志（2026-11-19 至 2026-11-21）中
`bots.runningTasks`、`agent.pendingUserInputs`、`agent.seqStates` **始终为 0**。
`taskBus.sessionRoutes=4` 表明有会话挂载，但无活跃任务。这意味着：

1. 非零→运行/等待的映射**从未被验证**，可能完全错误。
2. ZCode 宿主进程常驻但智能体任务可能通过其他机制触发（如 IPC 直接调用，
   不经过宿主日志的计数器）。
3. 当前实现按推断映射，属于**未校准的猜测**，需活跃任务样本验证。

## 二级状态源

- CLI 子代理：`~/.zcode\cli\agents\session_<id>\agent_<id>\metadata.json`，
  含 `parentSessionId`、`prompt`、`description`、`completedAt`、`outputFile`
  —— `completedAt` 缺失 = 子代理仍在跑，可用作 step 进度信号。
- 工作区：`~/.zcode\workspace\<ts>-<uuid>\`（mtime 即最近活跃时刻）。
- 内置工具快照：`~/.zcode\v2\runtime\zcode-builtin*.json`。

## 感知策略（落地 iClock）

tail 最新日志（`ReadIncrementalJsonLines` 同款增量游标），解析每行
`agent.pendingUserInputs=N`、`bots.runningTasks=N`、`agent.seqStates=N`：

```
pendingUserInputs>0 / pendingPermissions>0 → ❓ 等待决策（amber）
runningTasks>0 或 seqStates>0              → ⏳ 运行中（cyan，耗时锚取行时间戳）
全 0                                        → 静默回落（不发布）
```

SessionKey：`zcode-log:<日期>`（同一日志文件内多窗口共享一个宿主，粒度到宿主即可）。

## 已知未知

- 非零计数的真实样本（需在一次活跃任务期间抓日志验证）。
- `app.asar` 未用 REA 分析（384MB，超 300s 默认超时；如需可单独跑
  `--analysis-timeout-ms 900000`）。
*** Add File: rea/kimi.md
# Kimi Code 状态感知逆向

## 安装形态（本机实证）

- CLI：`~/.kimi-code\bin\kimi.exe`。
- 桌面壳：`%APPDATA%\kimi-code-app\`（Electron）+ `%LOCALAPPDATA%\kimi-code\{native,web}\`。
- 数据：`~/.kimi-code\{sessions,logs,locks,cache,...}\`。

## 一级状态源：会话目录 + wire.jsonl
`~/.kimi-code\sessions\wd_<runtimeId>\session_<uuid>\`

```
state.json            ← {id, version:2, cwd, createdAt, updatedAt, archived, agents:{main:{homedir,type}}}
notify\state.json     ← {"enabled":false}
agents\main\wire.jsonl  ← 会话线协议（双向事件流）
```

`state.json` 提供 cwd / 会话身份；`agents` 键可枚举该会话内的子代理。

wire.jsonl 实测 schema（v1.5）：

```
{"type":"metadata","protocol_version":"1.5","created_at":...}
{"type":"runtime.set_binding","workspaceId":"...","runtimeId":"local|acp:session_...","agentId":"main","time":...}
{"type":"profile.bind","agentId":"main","modelAlias":"kimi-code/k3","profileName":"agent","thinkingEffort":"high","systemPrompt":"..."}
```

**本机样本为空闲会话，wire.jsonl 仅含绑定握手行；任务/工具事件的行形态
未知（列为后续取证目标）**。

## 二级状态源

- `~/.kimi-code\logs\kimi-code.log`：INFO 级运行日志（query-store 打开等），
  无明确 turn 状态词。
- `~/.kimi-code\locks\__global__.jsonl`（188KB）+ `session_<id>.jsonl`：锁事件流。
- `~/.kimi-code\server\01M5XCJE1GE06X3P8F8S5TG68X.json`：服务端会话注册。
- 桌面壳：`%APPDATA%\kimi-code-app\Local Storage\` leveldb（Electron 状态）。

## 感知策略（落地 iClock）

1. 监听 `~/.kimi-code\sessions\**\state.json` + `wire.jsonl`：
   - 文件创建/更新 → 会话存在；`state.json.updatedAt` 为最近活跃锚。
   - wire.jsonl 追加非握手行（type ∉ {metadata, runtime.set_binding, profile.bind}）→ 运行中。
2. 等待/完成语义依赖 wire 事件行 —— 当前未知，v1 仅做「会话存在 + mtime 活跃」。
3. 进程侧：`kimi.exe` / `kimi-code-app` 进程存活作为兜底。

SessionKey：wire.jsonl 路径（`session_<uuid>` 天然隔离多会话）。

## 已知未知

- wire 协议的用户输入 / 工具调用 / 完成事件行形态（需活跃会话抓包）。
- `runtimeId` 中 `acp:` 前缀暗示 ACP（Agent Client Protocol）通道 —— 高价值后续目标。
