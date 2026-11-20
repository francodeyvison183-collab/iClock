# WorkBuddy 状态感知逆向

## 安装形态（本机实证）

- 双实例数据根：`~/.workbuddy\` 与 `~/.workbuddy-ai\`（各带实例目录
  `<uuid>\`，仅含 `sidebar-list-snapshot.json`）。
- 桌面壳：`%LOCALAPPDATA%\WorkBuddy\logs\`（main/renderer log）。
- 进程名待确认（本机当前无 WorkBuddy 进程）。

## 一级状态源：进程注册表 `~/.workbuddy\sessions\<pid>.json`（本机实证）

与 Claude Code 同构（疑似同源框架）：

```json
{"pid":12792,"lastHeartbeat":1791728906728,"sessionId":"b1e28618-...",
 "cwd":"C:\\Users\\17902\\WorkBuddy\\2026-10-11-14-35-56",
 "startedAt":1791704056237,"kind":"interactive",
 "url":"http://127.0.0.1:63479","endpoint":"http://127.0.0.1:63479",
 "mode":"local","version":"2.147.0","os":"win32","arch":"x64",
 "hostname":"Marx-G","updatedAt":1791728906730}
```

关键字段：

- `lastHeartbeat`（毫秒 epoch）—— 活跃度的权威信号：与 `DateTime.Now` 差值
  < ~15s 即视为该会话活着；进程消失或心跳停滞 → 清理。
- `endpoint`：本地 HTTP 端点（`mode: local`），**可拉实时状态 —— 未逆向，
  列为高价值后续目标**。
- 无 `status` 字段（区别于 Claude 注册表）—— 运行/等待语义需走转录。

## 二级状态源：项目转录 `~/.workbuddy\projects\<slug>\<sessionId>.jsonl`

实测（502KB）行 schema：`{content, cwd, id, message, parentId, providerData,
role, sessionId, status, timestamp, type}`。行类型分布：

```
function_call 38 | function_call_result 38 | reasoning 22 | message 21
file-history-snapshot 11 | session-meta 4 | ai-title 1
```

- 尾行 `type=message, status=completed` —— `status` 列存在，实测值
  `completed`；运行中形态（`in_progress`/`streaming`?）**未知**。
- `function_call` / `function_call_result` 成对 → 工具执行进度。
- `reasoning` 行 → 思考阶段。

**校准状态（2026-11-21）**：本机无活跃 WorkBuddy 会话，无法验证运行中
`status` 枚举。当前实现只用心跳注册表判定在线，转录解析走通用路径。
- 子代理：`projects\<slug>\<sessionId>\subagents\agent-*.jsonl`；
  工具结果旁路：`...\tool-results\*`。
- `~/.workbuddy\traces\<pid>\trace_*.json`：大体积 trace（2.2MB），暂缓。
- `~/.workbuddy\tasks\<sessionId>\<n>.json`：任务计划步骤（~350B/个）。

## 感知策略（落地 iClock）

1. 轮询 `~/.workbuddy\sessions\*.json` 与 `~/.workbuddy-ai\sessions\*.json`：
   心跳新鲜 → 会话在线（发布胶囊）；过期/进程死亡 → 清理。
2. 监听 `projects\**\*.jsonl`（排除 subagents / file-rollback）：
   `function_call` 未配对 → 运行中；尾行 `status=completed` → 完成；
   `reasoning` → 思考。

SessionKey：转录文件路径；注册表行用 `wb-reg:<sessionId>`。

## 已知未知

- 本地 endpoint 的 HTTP API 面（`GET /`? —— 值得用 REA `capture-web-*` 或
  手工 curl 探一轮）。
- 转录 `status` 的完整枚举。
