# Codex 状态感知逆向

## 安装形态（本机实证）

- 桌面应用（MSIX）：`C:\Program Files\WindowsApps\OpenAI.Codex_26.1117.3838.0_x64__...\app\resources\codex.exe`
  （目录 ACL 受限，无法静态读取）。
- CLI（npm 包装）：`~/nodejs/node_modules/@openai/codex/bin/codex.js` →
  按 `PLATFORM_PACKAGE_BY_TARGET` 落到 `@openai/codex-win32-x64` 原生二进制。

## 一级状态源：rollout 会话文件
`~/.codex/sessions/YYYY/MM/DD/rollout-<ts>-<uuid>.jsonl`

注意：目录按**会话创建日期**归档（实测文件创建于 10-30、最后写入 11-20 22:59，
活跃长会话不挪目录）。首行 `session_meta`，payload 含 `cwd`、`git`、`originator`、
`cli_version`、`model_provider`、`session_id`、`runtime_workspace_roots`。

全文件行类型分布（实测 4.2MB / 3000+ 行）：

```
event_msg 1511 | response_item 1473 | token_usage_record 438
turn_context 22 | world_state 7 | compacted 1 | session_meta 1
```

event_msg.payload.type 分布（turn 生命周期的权威信号）：

```
item_completed 1010 | token_count 442 | thread_settings_applied 23
task_started 21 | task_complete 16 | turn_aborted 3
```

response_item.payload.type 分布（工具执行细节）：

```
reasoning 397 | function_call_output 333 | function_call 332
message 219 | custom_tool_call 98 | custom_tool_call_output 98
```

## 状态机推导

```
task_started              → RUNNING（TurnStartTime = 该行 timestamp）
function_call 未配 output  → 工具执行中（StepProgress 可由成对计数得出）
task_complete             → DONE
turn_aborted              → IDLE（用户中断，区别于 error）
reasoning 行              → IsThinking 窗口
错误信号                  → 本轮未见显式 error 事件；UNKNOWN，建议结合
                            task_complete 缺失 + turn 结束推断
```

  **活跃样本实证（2026-11-21）**：Tail 2000 行确认 `event_msg` 类型分布：
  `item_completed 563 | token_count 259 | task_started 15 | task_complete 13 |
  thread_settings_applied 21`。`turn_aborted` 全文件 80 例，payload 含
  `reason: "interrupted"`、`started_at`、`completed_at`、`duration_ms`。
  `task_complete` 含 `last_agent_message`（完成摘要）、`started_at`、
  `completed_at`、`duration_ms`。代码映射与实证一致。

同一 rollout 内多 turn 交替：以最后一个 task_started/complete 的相对顺序为准。
`SessionKey` 直接用 rollout 文件路径（现有 watcher 已这么做）。

## 二级状态源

- `~/.codex/.codex-global-state.json`（48KB）：桌面壳 Electron 状态，键含
  `pinned-thread-ids`、`thread-project-assignments`、`queued-follow-ups`、
  `sidebar-project-thread-orders` —— thread→项目映射可增强胶囊归属。
- `~/.codex/archived_sessions\`、`~/.codex/rollout-migrations\`。

## 映射到 iClock

| AgentTaskStatus | 来源 |
|---|---|
| `SessionKey` | rollout 文件路径（现状不变） |
| `IsThinking` | 最后 payload 为 reasoning，或 task_started 后无 item_completed |
| `Message` | task_started 后首个 function_call 的工具名 + ExtractGoal(message) |
| `TurnStartTime` | task_started.timestamp |
| 完成 | task_complete → 现有完成流程；turn_aborted → 静默回落 idle |

## 已知未知

- 错误态的显式事件形态（本轮样本未出现 error 类 event_msg）。
- `world_state` / `compacted` 行语义。
- MSIX 壳与 CLI 是否共享同一 sessions 目录（本机实测：桌面进程在跑、
  sessions 由 CLI 写入，推断共享，未验证）。
*** Add File: rea/cursor.md

## 补充（二轮取证）

- `~/.codex/sqlite/codex.db`（155KB + 4MB WAL，WAL 于 11-20 22:50 仍在写）：
  本地 thread 索引库，schema 未逆向；可作为 thread→项目/标题映射候选。
- `~/.codex/archived_sessions\`：归档 rollout，勿与活跃会话混读。
