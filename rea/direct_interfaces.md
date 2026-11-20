# 直连接口评估（对比「读日志文件」）

用户反馈：不要只 tail 日志文件，要找更直接准确的感知通道。
本轮对各家的进程间通道做了实测，结论如下（观察 / 推断 / 未知 分开标注）。

## 结论总览

| 智能体 | 直连通道 | 实测结果 | 可行性 |
|---|---|---|---|
| Claude Code | `\\.\pipe\LOCAL\cc-msg-<hash>` 命名管道（peerProtocol=1，notify_idle 特性） | 管道存在，本机连接超时（500ms，被服务端拒绝/需特定握手） | 高价值，需逆向帧格式 |
| Antigravity | `daemon/hub_<port>.json`（httpPort/httpsPort/csrfToken） | HTTP 可达，但所有路径返回 SPA 壳；真实 API 走 websocket/gRPC（https 端口） | 中，需抓 websocket 帧 |
| Codex | `\\.\pipe\codex-mcp-*`、`codex-computer-use-*`、`codex-browser-use-*` 等 MCP 管道 | 管道存在，但为 Codex 自己对外提供的工具通道（不是"问 Codex 状态"的入口） | 低，不适合状态感知 |
| WorkBuddy | `~/.workbuddy/sessions/<pid>.json` 的 `endpoint` 字段（`http://127.0.0.1:<port>`） | 本机当前无存活实例（hb_age 已过期），端口未监听 | 未知，需在实例运行时实测 |
| ZCode | `\\.\pipe\zcode-runtime-*` / `zcode-node-repl-*` | 管道存在，但 node-repl 是 REPL 求值通道，runtime 是宿主内部通道，均无文档 | 低 |
| Kimi Code | `runtimeId: acp:session_<uuid>` 暗示 ACP（Agent Client Protocol）通道 | ACP 标准走 stdio，子进程通道，不是本机常驻监听端口 | 需另辟蹊径 |

## 各通道详情

### Claude Code 命名管道（最有希望）

`~/.claude/sessions/<pid>.json` 提供：
- `messagingSocketPath: \\.\pipe\LOCAL\cc-msg-<40hex>`
- `peerProtocol: 1`
- `peerFeatures: ["notify_idle", "artifact_yield"]`

`notify_idle` 这个特性名强烈暗示存在「我空闲了」的主动推送事件。
实测：`NamedPipeClientStream.Connect(500)` 超时——服务端在监听但拒绝裸连接
（很可能要求握手帧 / 协议头 / 权限 token，伴生文件 `<pid>.<hash>.key` 疑似密钥）。

下一步：用 REA `capture-process` 观察 Claude Code 运行时的管道读写，或用
`open_binary` + Ghidra 分析 `claude.exe` 中 `cc-msg-` 字符串引用处还原帧格式。

### Antigravity hub（HTTP 可达但无明文 API）

`~/.gemini/antigravity/daemon/hub_<port>.json`：
`{pid, httpsPort, httpPort, lsVersion, csrfToken}`（每 daemon 启动一份，本机 71 份）。

实测：GET `http://127.0.0.1:<httpPort>/` 返回 200 + SPA HTML（含 csrfToken 注入），
但 `/api/*`、`/status`、`/health` 等所有路径均返回同一份 SPA——说明 hub 把
非 API 路径全部回落到前端，真实 API 走 **websocket 升级** 或 **https 端口的 gRPC**。

下一步：用浏览器 devtools 抓一次 Antigravity 窗口的 websocket 流量，拿到
帧结构后即可用 `hub.csrfToken` 做认证订阅。

### Codex MCP 管道（方向反了）

本机存在 40+ `\\.\pipe\codex-mcp-*`、`codex-computer-use-*`、`codex-browser-use-*`
管道——这些是 **Codex 作为服务端对外暴露的工具**（让别的进程调它干活），
不是"查询 Codex 状态"的入口。对状态感知无用。

Codex 真正的状态面仍然是 `~/.codex/sessions/**/rollout-*.jsonl` 的
`task_started/task_complete/turn_aborted` 事件（已接入）。

### WorkBuddy 本地 endpoint（未知）

`sessions/<pid>.json` 有 `endpoint: http://127.0.0.1:<port>`，但本机当前无
存活实例（lastHeartbeat 过期），端口未监听。需要 WorkBuddy 运行时实测
`GET <endpoint>/`。

### ZCode 管道（低价值）

`zcode-runtime-*` / `zcode-node-repl-*`：node-repl 是 JS 求值通道（类似
Node REPL），runtime 是宿主内部 RPC。两者均无状态推送语义文档，实测价值低。

### Kimi ACP 通道（需另辟蹊径）

`wire.jsonl` 的 `runtimeId: acp:session_<uuid>` 暗示 ACP 协议。ACP 标准
（agentclientprotocol.com）走 **stdio**（spawn 子进程），不是本机监听端口，
意味着"连接一个已运行的 Kimi 会话"不能走 ACP——只能由 Kimi 主动 spawn
一个 ACP 客户端。

更现实的路径：`~/.kimi-code/sessions/**/wire.jsonl` 本身就是 ACP 事件的
落盘镜像（双向 wire 协议）。tail 它就是"订阅 ACP 事件流"的等价物。

## 结论与建议

1. **"直接问状态"的通道确实存在**（Claude 管道、Antigravity hub、
   WorkBuddy endpoint、Kimi ACP），但都需要逆向私有协议/握手，工作量集中
   在「抓一次真实会话的流量」。
2. **日志/转录文件并非退而求其次**：Kimi 的 wire.jsonl、Codex 的 rollout、
   Cursor 的 agent-transcripts、Antigravity 的 brain transcript 本质都是
   「状态的权威落盘镜像」，tail 它们 = 订阅事件流，延迟在毫秒级（与进程内
   通道相当），且无需破解私有协议。
3. 真正值得继续攻克的两个直连目标：
   - **Claude `cc-msg` 管道**（notify_idle 推送 → 最贴合"实时状态"诉求）；
   - **Antigravity hub websocket**（全量会话状态订阅）。
   两者都需要一次「活跃会话抓包」，建议下一步做。

## 已落地与已回退（2026-11-21 校准后）

- 已回退：无。
- 已落地但**未校准**（推断当事实，需活跃样本验证）：
  - ZCode 宿主日志计数 → 运行/等待映射（历史样本全为 0，映射可能错误）
  - Claude 注册表 `status != "idle"` → 运行判定（运行态枚举值未知）
  - Kimi wire mtime → 在线判定（任务事件行形态未知，保守兜底）
- 已落地且**已实证**：
  - Claude 注册表 + waiting-input 目录 + 转录解析
  - Codex rollout 生命周期事件（task_started/complete/turn_aborted）
  - Cursor agent-transcripts 的 turn_ended + worker 日志 workerId 关联
  - Antigravity brain transcript 的 status 列（GENERIC ERROR 修正为步骤级）
  - WorkBuddy 心跳注册表 + 项目转录
