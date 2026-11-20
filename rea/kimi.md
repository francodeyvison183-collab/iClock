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

wire.jsonl 实测 schema（v1.5）：

```
{"type":"metadata","protocol_version":"1.5","created_at":...}
{"type":"runtime.set_binding","workspaceId":"...","runtimeId":"local|acp:session_...","agentId":"main","time":...}
{"type":"profile.bind","agentId":"main","modelAlias":"kimi-code/k3","profileName":"agent","thinkingEffort":"high","systemPrompt":"..."}
```

**本机样本为空闲会话，wire.jsonl 仅含绑定握手行；任务/工具事件的行形态
未知（列为后续取证目标）**。runtimeId 的 `acp:` 前缀暗示存在 ACP
（Agent Client Protocol）通道——高价值后续目标。

## 感知策略（落地 iClock）

1. 监听 `~/.kimi-code\sessions\**\state.json` + `wire.jsonl`：
   - wire.jsonl 出现握手行（profile.bind）且 mtime 新鲜 → 会话存在（发布
     "会话已接入" 胶囊）。
   - wire.jsonl 继续追加非握手行 → 运行中（处理中）。
   - mtime 停滞 >3 分钟 → 清理。
2. 进程侧：`kimi.exe` / `kimi-code-app` 存活作为兜底。

SessionKey：wire.jsonl 路径（`session_<uuid>` 天然隔离多会话）。
