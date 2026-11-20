# DeepSeek 状态感知逆向

## 结论（本机取证）

本机 **未发现任何独立的 DeepSeek 客户端**：

- 无运行中进程（`Get-Process *deepseek*` 为空）；
- 无 CLI（PATH 上无 `deepseek` / `deepseek-cli` / `ds`）；
- 无数据目录（`~/.deepseek`、`%APPDATA%\DeepSeek`、`%LOCALAPPDATA%\DeepSeek`
  等均不存在）。

因此**没有可逆向的本地目标**，本轮不做任何状态感知实现（避免为不存在的
数据源写猜测性代码）。

## 已有支持面

iClock 早已在品牌识别层支持 DeepSeek：`src/AgentHud.cs` 的 `GetAgentBadge`
包含 `deepseek` 徽章（`0x4D6BFE`，标签 DeepSeek）；`src/iClock.cs` 的
`MatchAgent` 也识别 `deepseek`。一旦 DeepSeek 推出/安装了带本地会话落盘的
客户端（CLI 或桌面端），接入路径与 Claude/Kimi 一致：

1. 找到其会话目录（通常 `~/.deepseek\sessions` 或 `%APPDATA%\DeepSeek\*`）；
2. 在 `AgentWatcher.ResolveCandidateDirectories` 注册目录 + 增量 tail；
3. 按其行 schema 写对应解析分支（或确认 OpenAI 兼容协议后直接复用
   `ParseProtocolGenericLine`）。

## 若后续安装

把客户端跑起来一次，然后告诉我，我会用 REA 跑 `analyze-javascript-application`
/ 运行时取证，按本目录其他分册的格式补齐逆向并实现实时感知。
