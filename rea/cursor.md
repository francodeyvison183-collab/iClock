# Cursor 状态感知逆向

## 安装形态（本机实证）

- GUI：`%LOCALAPPDATA%\Programs\cursor\`（VS Code fork，`resources\app\` 为解包树）。
- CLI agent：`cursor-agent.ps1` → `%LOCALAPPDATA%\cursor-agent\`。
- 数据：`%APPDATA%\Cursor\User\`（全局）+ `~/.cursor/`（CLI/agent）。

## 一级状态源（CLI/agent 侧）：agent-transcripts
`~/.cursor/projects/<project-slug>/agent-transcripts/<task-uuid>/<task-uuid>.jsonl`

实测（iClock 项目，536KB）：

- 行形态两种：`{"message":"...","role":"user|assistant"}` 与事件行
  `{"type":"..."}`；事件类型全集（全项目 rg 统计）：

```
tool_use 23968 | text 3902 | turn_ended 94 | textbox 1
```

- turn 终止行：`{"type":"turn_ended","status":"success"}` 或 `"error"`
  —— **status 全集实测仅 success/error 两种**。
- 任务目录内含 `compaction`（1 字节标记）；旧版为 `segment_NNN.md` + `INDEX.md` 布局。
- 工具输出旁路：`~/.cursor/projects/<slug>/agent-tools/<task-uuid>.txt`（最大 302KB）。

状态推导：

```
尾行 turn_ended.status=success → DONE
尾行 turn_ended.status=error  → ERROR
无 turn_ended 且 mtime 推进    → RUNNING（tool_use 行驱动 StepProgress）
```

## 二级状态源：worker 通道
`%APPDATA%\Cursor\User\globalStorage\anysphere.cursor-agent-worker\`

- `cursor-agent-worker-<hash>.<pid>.pid` / `.log` / `.owner.json` / `.restart.json`
  —— PID 判活 + 日志 tail 可做 worker 级健康状态。
- `worker-data\cursor-agent-worker-<hash>\`（5 个实例目录）。
- `agent-cli\` 目录；host 侧 `anysphere.cursor-agent-host\data.sqlite`（本机 10-13 后未更新）。
- `~/.cursor/agent-cli-state.json`：`workerIdsByDisplayName` 把
  `"d:\AIStudio\iClock @ Marx-G"` → worker uuid，是**项目 ↔ worker 归属映射**。
- `~/.cursor/ai-tracking/ai-code-tracking.db`（34MB SQLite）。

**关键实证补充（2026-11-21）**：worker 日志头部包含 `workerId: "<uuid>"` 和
`workspacePath: "<project>"`，与 `agent-cli-state.json` 的 `workerIdsByDisplayName`
精确对应。原始文档「无法按名字匹配」的结论**错误**——可通过读取日志前 60 行
提取 `workerId` 实现项目级精确关联。此发现已用于修复 `SyncCursorLiveState`
的项目粒度丢失 bug。

## GUI 深水区（本轮未逆向）

- `%APPDATA%\Cursor\User\globalStorage\state.vscdb`：**4.1GB** SQLite（WAL 活跃），
  GUI composer/chat 状态存于 `cursorDiskKV`（community 已知 schema，本机未验证）。
  iClock (.NET Framework) 无内置 SQLite 栈，v1 建议只走 JSONL 通道。
- `conversation-search.db`（3.7MB SQLite FTS）同理暂缓。

## REA 记录

- `rea analyze-javascript-application <resources\app>` → Evidence
  `ev_deb88aeeec0e8c74bed1614e7ec790e13983bfc966844d40835015fbcf40a8dd`
  （root sha256 `67900241...cad544`，目录格式）。agent 状态逻辑集中在
  workbench 的服务端代理管线与 state.vscdb；本机感知仍以
  `agent-transcripts` 的 `turn_ended.status` 为权威——代码已按此实现。
