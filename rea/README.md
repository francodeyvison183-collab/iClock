# REA：智能体状态自动感知逆向研究

目标：为 iClock 的 AI 协作状态栏（`AgentTaskStatusHud` / `AgentSessionWatcher`）建立
Claude、Codex、Cursor、Antigravity 四家智能体的**可自动感知的状态面**。
方法：REA 静态分析（shipped artifact）+ 本机运行时取证（runtime evidence）双轨，
所有结论区分「观察 / 推断 / 未知」。

## 一览

| 智能体 | 一级状态源（权威） | 二级状态源（细节） | 状态语义 | 感知成本 |
|---|---|---|---|---|
| Claude Code | `~/.claude/sessions/<pid>.json` 进程注册表，带 `status` 字段 | 桌面壳会话状态目录、项目转录 jsonl | idle/busy **已实证** | 极低（读小 JSON） |
| Codex | `~/.codex/sessions/**/rollout-*.jsonl` 的 `event_msg` 生命周期 | `.codex-global-state.json` | task_started/complete/turn_aborted **已实证** | 低（增量 tail） |
| Cursor | `~/.cursor/projects/<slug>/agent-transcripts/<task>/<task>.jsonl` | worker `.pid`/`.log`、`agent-tools/<uuid>.txt` | turn_ended status: success/error **已实证** | 低（增量 tail） |
| Antigravity | `~/.gemini/antigravity/brain/<conv>/.system_generated/logs/transcript.jsonl` 的 `status` 列 | daemon `hub_<port>.json` 注册表、`messages/undelivered/` | RUNNING/DONE 已实证；GENERIC ERROR=步骤级，非终止 **已修正** | 低（增量 tail） |
| ZCode | `~/.zcode/v2/logs/YYYY-MM-DD.log` 的 agent/bots 内存计数 | cli 子代理 metadata（completedAt） | **非零样本缺失，映射未验证** | 低（增量 tail） |
| Kimi Code | `~/.kimi-code/sessions/**/state.json` + `wire.jsonl` | logs、locks、server 注册 | 握手行已知，任务事件行**未知** | 低（增量 tail） |
| WorkBuddy | `~/.workbuddy[-ai]/sessions/<pid>.json` 心跳注册表 | 项目转录 jsonl（status=completed）、本地 endpoint | 心跳活性已实证；转录运行态枚举**未知** | 极低（读小 JSON） |

## 四份分册

- `claude_code.md`：进程注册表（status 字段、命名管道）、桌面壳 `waiting-input` 状态目录、转录行类型分布、子代理文件布局。
- `codex_cli.md`：rollout 文件 schema、event_msg/response_item 分布、turn 生命周期映射。
- `cursor.md`：CLI 侧 agent-transcripts schema、turn_ended 状态值、worker 通道、GUI state.vscdb 的定位。
- `antigravity.md`：transcript `status` 列语义（本机实证）、brain 目录结构、daemon hub 注册表、REA 壳层分析结论。
- `zcode.md`：宿主日志内存计数通道（runningTasks / pendingUserInputs）、CLI 子代理元数据。
- `kimi.md`：state.json + wire.jsonl 握手协议（v1.5）、ACP 通道线索。
- `workbuddy.md`：PID 心跳注册表（与 Claude 同构）、项目转录 schema、本地 endpoint。

## 对 iClock 的落地方案（建议优先级）

1. **Claude 注册表直读**（改动最小收益最大）：`Watcher` 增加 `~/.claude/sessions/*.json`
   监听；`status` 字段 + `updatedAt` 直接映射 `IsThinking/Message`；`pid` 用
   `GetProcessById` 判活，进程消失即清状态；`name` 字段可直接做会话显示名。
2. **Antigravity status 列**：现有 watcher 已解析 brain transcript，升级为按行
   `status`（RUNNING/DONE/ERROR）驱动，替代启发式 `PLANNER_RESPONSE`/DONE 字符串猜测。
3. **Codex 事件语义**：按 `event_msg.payload.type` 精确驱动 turn 状态机
   （task_started → running；task_complete → done；turn_aborted → idle），
   `TurnStartTime` 取首个 task_started 的 `timestamp`。
4. **Cursor CLI 转录**：新增 `~/.cursor/projects/*/agent-transcripts/**.jsonl` 监听；
   尾行 `turn_ended.status=success/error` 映射完成/异常；无 turn_ended 且 mtime
   持续推进 → running；`agent-cli-state.json` 提供 worker 与项目目录的映射。
5. **GUI 深水区（暂缓）**：Cursor `state.vscdb`（4.1GB SQLite）与 Claude 命名管道协议
   本轮未逆向，列为后续工作。

## 证据与可复现性

- REA Evidence ID 与命令记录在各分册末尾。原始分析 JSON（数百 MB）**不入库**
  （`.gitignore` 排除 `rea/evidence/`），可用相同 REA 命令重新生成；蒸馏后的
  结论全部沉淀在各分册 `.md`。
- 运行时取证命令均为只读（Get-Content / rg），未改动任何智能体状态。
*** Add File: rea/claude_code.md
