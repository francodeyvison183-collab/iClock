# 路径韧性评估：换个用户 / 换个安装位置会怎样

结论：**安装路径变了不影响**（代码从不按安装路径找数据），**用户主目录变了会影响**
（所有数据定位都建立在「主目录下的固定子路径」假设上）。

## 当前定位策略分类

### A. 用户主目录 + 固定子路径（脆弱点）

全部走 `Environment.GetFolderPath(UserProfile)` / `LocalApplicationData` / `ApplicationData`：

```
~/.claude/{sessions,projects}        Claude
~/.codex/{sessions,.codex-global-state.json}   Codex
~/.cursor/{projects,agent-cli-state.json}      Cursor
~/.gemini/antigravity/{brain,daemon}   Antigravity
~/.zcode/{v2,cli}                      ZCode
~/.kimi-code/sessions                  Kimi
~/.workbuddy[-ai]/{sessions,projects}  WorkBuddy
%LOCALAPPDATA%/Claude-3p/claude-code-sessions  Claude 桌面壳
%APPDATA%/Cursor/User/globalStorage            Cursor worker
```

换用户后这些根全部失效 → `ResolveCandidateDirectories` 返回空 / 注册表
读不到 → 感知静默失效（不报错，但也不显示）。

### B. 进程名匹配（与路径无关，天然稳健）

`MatchAgent` 按进程名子串识别（claude/codex/zcode/kimi/workbuddy/...），
只要进程跑起来就能识别——这部分换用户/换路径都不受影响。

### C. 注册表 / 配置文件内的自描述路径（稳健）

- Claude 注册表 `<pid>.json` 自带 `messagingSocketPath`、`cwd`；
- Antigravity `daemon/hub_*.json` 自带 `httpPort/httpsPort`；
- WorkBuddy `sessions/<pid>.json` 自带 `endpoint`。
这些值由智能体自己写入，天然跟随其真实数据位置——**这是应该主推的方向**。

## 会失效的具体场景

1. 用户主目录重定向（漫游配置 / `USERPROFILE` 改到 D 盘）。
2. 便携版 / 绿色安装（智能体数据写到自己的安装目录而非主目录）。
3. 多用户共享一台机器（`C:\Users\Public` 或其他用户的主目录）。
4. 智能体支持自定义数据目录（env var / `--data-dir` 启动参数）。

## 加固方案（按价值排序）

1. **多根探根**（已落地）：候选根不止 USERPROFILE，还包括
   `HOMEDRIVE+HOMEPATH`、`Public`、`Default`，以及从运行中进程的可执行
   路径反推（如 `C:\Program Files\ZCode\ZCode.exe` → 探其目录旁的
   `data/.zcode`）。`ResolveCandidateDirectories` 对每个根做存在性检查，
   一个根失效自动落到下一个。
2. **进程路径反推**（已落地）：`AppContext.GetRunningAgentExePaths()` 从
   `Process.MainModule.FileName` 拿到每个已识别智能体的真实可执行路径，
   作为候选根之一。
3. **注册表内路径优先**（部分已用）：Claude/Cursor/WorkBuddy 的注册表
   文件里已经带了 `cwd`/`endpoint`/管道名，优先于任何硬编码根。
4. **配置文件覆盖**（可选，未落地）：`settings.ini` 增加
   `AgentDataRoot.<AgentName>=<path>`，供用户/部署方显式指定。

## 仍未覆盖的场景

- 智能体数据写到完全自定义的位置（非主目录、非安装目录、非任何候选根）。
  只能靠场景 4 的显式配置解决。
- 多用户同时登录并各自跑同一智能体：候选根会命中多个，需要按「当前
  交互用户」或「进程所有者」过滤（未落地）。
