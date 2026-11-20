# Antigravity 状态感知逆向

## 安装形态（本机实证）

- 桌面壳：`%LOCALAPPDATA%\Programs\Antigravity\`（Electron/VS Code fork，app.asar）。
- 运行时数据：`~/.gemini/antigravity\`（brain/conversations/daemon/presence 等）。
- daemon：`Antigravity.exe` 多进程常驻（实测 11 进程）。

## 一级状态源：brain transcript（status 列，本机实证）
`~/.gemini/antigravity/brain/<conv-id>/.system_generated/logs/transcript.jsonl`

行 schema：`{content, created_at, source, status, step_index, type}`。
实测活跃会话（27MB，Tail 2000 行）的 type|source|status 组合分布：

```
PLANNER_RESPONSE|MODEL|DONE        973
GENERIC|MODEL|DONE                 901
USER_INPUT|USER_EXPLICIT|DONE       44
SYSTEM_MESSAGE|SYSTEM|DONE          29
GENERIC|MODEL|RUNNING               28   ← 运行中信号
ERROR_MESSAGE|SYSTEM|DONE           16
GENERIC|MODEL|ERROR                  5   ← 错误信号
CHECKPOINT|SYSTEM|DONE               4
```

语义三轴：

- `status ∈ {RUNNING, DONE, ERROR}` —— 感知主信号。
- `source ∈ {MODEL, USER_EXPLICIT, SYSTEM}` —— 消息来源。
- `type ∈ {PLANNER_RESPONSE, GENERIC, USER_INPUT, SYSTEM_MESSAGE, ERROR_MESSAGE, CHECKPOINT}`。

**关键实证修正（2026-11-21）**：`GENERIC status=ERROR` 是**步骤级错误**，
不是 turn 级终止。实证：step 19268 `GENERIC status=ERROR` 后紧跟 step 19269
`PLANNER_RESPONSE status=DONE`，智能体继续执行。`ERROR_MESSAGE` 才是 turn
级异常。因此代码忽略 `GENERIC` 的 `status` 字段是正确的——错误内容已通过
`content` 字段提取为步骤详情，智能体会从错误中恢复。

状态推导：最新行 `status=RUNNING` → 运行中；`ERROR_MESSAGE` → 异常；
`PLANNER_RESPONSE status=DONE` → 完成回落。`GENERIC status=ERROR` 仅作
步骤详情展示，不改变 turn 状态。`created_at` 提供 TurnStartTime / MessageTimestamp。

## brain 目录其余结构

```
<conv-id>/.system_generated/
  logs/transcript.jsonl        ← 主转录（27MB）
  logs/transcript_full.jsonl   ← 全量转录（33MB）
  logs/chunks/transcript[_full]/NNNNNN.jsonl  ← 分块滚动（100KB/块）
  steps/<n>/output.txt         ← 每步输出（编号 4 位递增，实测 20462）
  tasks/task-<n>.log           ← 任务日志
  messages/<uuid>.json         ← 消息缓存 + messages/undelivered/（未投递队列）
  subagents/
<conv-id>/{scratch, browser_evidence, browser_sandbox, .user_uploaded}/
```

## 状态辅助面

- `~/.gemini/antigravity/daemon/hub_<port>.json`：daemon 注册表
  `{pid, httpsPort, httpPort, lsVersion, csrfToken}`（实测 71 份，最新 mtime =
  daemon 启动时刻）。**推断**可用 httpPort+csrfToken 调本地 daemon API 拿实时
  状态 —— 本轮未验证，列为高价值后续目标。
- `presence/<conv-id>.lock`：0 字节锁文件（mtime 停留在会话最后活跃日）。
- `conversations/<conv-id>.db`（189MB SQLite+WAL）会话主存储；
  `conversation_summaries.db`；`antigravity_state.pbtxt` 应用状态（protobuf 文本）。

## REA 壳层分析（shipped artifact）

- `rea analyze-javascript-application app.asar` → Evidence
  `ev_303188eafad799bc5554ff0ba66b4c56528f39a0dd9831e28479a5cc061e5357`
  （1285 模块节点，graph 覆盖 partial）。
- 壳层 dist 共 491 个模块；agent 智能体逻辑**不在壳层**（服务端/daemon 侧），
  壳内相关模块仅 `dist/computer/hudAgents.js`、`dist/computer/hudState.js`、
  `dist/computerActuator/driver/backgroundDriver.js`、`dist/guestAgentControl.js`、
  `dist/hubWindows.js` 等。
- trace `dist/computer/hudAgents.js`（module seed）命中
  `jag_node_8c07d3c50ecff68e709640091f7626766a1cd0b0f89e3ba0b17e2d5469e9e490`，
  模块观测较薄（source-module 一层），佐证「状态面在数据文件，不在壳代码」。
- 字符串 seed `PLANNER_RESPONSE` 未命中图节点 —— 该标记由服务端/daemon 写入
  数据文件，非壳层字面量（与 limitations 一致：图覆盖 partial，不证明不存在）。

## 映射到 iClock

现有 watcher 已监听 `brain/`；升级点：

1. 解析行 `status` 列替代 `PLANNER_RESPONSE`/DONE 字符串启发式（`AgentWatcher.cs`
   的 Antigravity 分支）。
2. `steps/<n>` 递增编号 → `StepProgress`（step n / 无总数，可用 `n` 单调差分）。
3. `messages/undelivered/` 非空 → 等待态候选信号。
4. `SessionKey` = conv-id（目录名），胶囊可并用 `chapter_titles` 会话标题。
