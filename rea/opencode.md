# opencode 状态感知逆向

## 安装形态（本机实证）

- 桌面壳：`%LOCALAPPDATA%\Programs\@opencode-aidesktop\OpenCode.exe`（Electron，
  user-data 在 `%APPDATA%\ai.opencode.desktop`，实测 7 进程常驻，v1.19.5）。
- CLI/数据：`~/.local/share/opencode/`（log/repos/snapshot/storage/tool-output +
  `opencode.db` SQLite）。
- 配置：`~/.config/opencode/`（opencode.jsonc、plugins、skills、node_modules）。

## 一级状态源：SQLite `opencode.db`（权威，但 v1 未直接读）

`~/.local/share/opencode/opencode.db`（本机 879MB，WAL 模式）。

Schema（实测）：

- `session`：`id, project_id, parent_id, slug, directory, title, agent, model,
  time_created, time_updated, time_archived, summary_*`。
  - `agent ∈ {build, explore}`（build=主代理，explore=子代理，parent_id 关联）。
  - `time_updated` 毫秒 epoch，活跃锚点（实测活跃会话 0.5min 前）。
  - `directory` = 项目根，`title` = 会话标题。
- `message`：`id, session_id, time_created, data(JSON)`。data 含
  `role(user/assistant), mode, agent, modelID, path{cwd,root}, tokens`。
- `part`：`id, message_id, session_id, time_created, data(JSON)`。
  part.type 实测分布（活跃会话 1510 行）：
  `tool 394 | step-start 320 | step-finish 318 | reasoning 231 | patch 122 |
  text 107 | compaction 3`。

**turn 生命周期（实证）**：

- `step-start`（`{type, snapshot}`）→ step 开始。
- `step-finish`（`{type, reason, snapshot, tokens, cost}`）→ step 结束：
  - `reason="stop"` → turn 终止信号。
  - `reason="tool-calls"` → 继续调用工具（turn 进行中）。
- `tool` part 的 `state.status ∈ {completed, error}`（实测 100 例：99 completed
  + 1 error）→ 工具级结果。`state.input.command`、`state.output` 可提取步骤详情。

**未直接读的原因**：iClock 是 .NET Framework，内置无 SQLite；引入
System.Data.SQLite 需要原生 interop DLL，超 1MB 体积红线风险大。

## 二级状态源（v1 落地）：WAL mtime + 进程存活

- `opencode.db-wal`（4.5MB，活跃会话期间 mtime 持续推进）→ **活跃锚点**。
  mtime ≤3min 视为有活跃会话。
- `OpenCode` 进程名（7 进程）→ 宿主存活信号。
- 二者结合：WAL 新鲜 + 进程存活 → 在线；WAL 停滞 / 进程消失 → 清理。

## 已知未知

- 会话级粒度（title/directory/agent）需读 SQLite——v1 宿主级粒度，标
  「OpenCode」通用胶囊。后续若引入轻量 SQLite 读取（如编译进单文件的
  sqlite3 amalgamation，~250KB），可升级为会话级。
- `tool-output/tool_*` 目录（大输出旁路）、`snapshot/`（git 快照）。
- Electron 壳的 IPC 通道（`ai.opencode.desktop` user-data）未探。

## 映射到 iClock

| AgentTaskStatus | 来源 |
|---|---|
| `AgentName` | `OpenCode`（MatchAgent 进程名识别） |
| `SessionKey` | `opencode-host`（宿主级） |
| `IsThinking` | WAL mtime ≤3min && 进程存活 → true（在线） |
| 清理 | WAL 停滞 >3min 或进程消失 |

## REA 记录

- 本轮仅运行时取证（进程枚举、db schema dump、part 类型分布统计、WAL
  mtime 追踪），未对 OpenCode.exe / app.asar 做静态分析。
- 取证脚本：`scratch/opencode_db.py`、`opencode_sessions.py`、`opencode_parts.py`、
  `opencode_tool.py`（Python sqlite3 只读模式）。
