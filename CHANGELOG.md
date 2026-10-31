# Changelog

This project follows a simple release-based changelog. Dates use ISO 8601 (`YYYY-MM-DD`).

## [Unreleased]

## [1.14] - 2026-10-31

### Added

- **多智能体统一状态条 (Unified multi-agent HUD)**: 多个智能体同时运行时，桌面顶部只保留一条状态条，胶囊药丸按「等待/错误 > 运行中 > 已完成」的优先级动态排序并以固定节奏轮播；同一智能体的多个会话自动编号 `#1` / `#2`（编号由会话键字典序稳定派生，重绘不跳号）；状态条高度可拖动，悬停出现图钉可置顶常驻；默认顶部居中，前台窗口切换时淡出，任务完成后有 5 秒去抖延迟；拖到屏幕中央附近显示竖向虚线吸附参考线。/ 多个智能体同时运行时，桌面顶部只保留一条状态条，胶囊药丸按优先级动态排序并定时轮播；同一智能体多会话自动编号 `#1` / `#2`；状态条高度可拖动、图钉可置顶；默认顶部居中，前台切换时淡出，完成后 5 秒去抖；拖到屏幕中央附近显示竖向虚线吸附参考线。

- **100% 真实的智能体状态流 (Authentic agent stream)**: 新增协议级解析器，直接读取 Claude Code、OpenAI Codex、Antigravity、Cursor 的本地会话文件，按各自协议还原「正在做什么 / 想什么 / 跑了哪条命令」；一条记录里的多条命令按顺序全部显示，不再只留最后一条；规划与思考阶段有三点动画，智能体最小化后状态条依然跟随最新步骤。/ 新增协议级解析器，直接读取 Claude Code、OpenAI Codex、Antigravity、Cursor 的本地会话文件，还原智能体真实动作；一条记录里的多条命令按顺序全部显示；规划与思考阶段有三点动画，最小化后仍跟随最新步骤。

- **护眼提醒 (Eye-care reminder)**: 倒计时设置页新增「护眼提醒」下拉，一个下拉同时管开关与间隔；每 30 秒采样一次系统最后输入时间，连续 3 分钟无输入视为一次真正的休息（累计清零）；休息倒计时期间不计时，全屏游戏或设置窗口打开时先不弹卡，回到普通窗口再提醒；复用统一的提醒弹窗，旁边有 ⓘ 悬停说明。/ 倒计时设置页新增「护眼提醒」下拉，同时管开关与间隔；3 分钟无输入视为一次真正的休息并清零；休息期间不计时，全屏或设置窗口打开时先不打扰；复用统一提醒弹窗并带 ⓘ 说明。

- **用时统计趋势视图 (Usage trends)**: 今日记录新增【趋势】页签，上半部分是「屏幕活跃 vs 专注」双序列日对比图，下半部分是窗口内的应用排行，范围可选本周或最近 30 天。/ 今日记录新增【趋势】页签，上半部分是「屏幕活跃 vs 专注」双序列日对比图，下半部分是窗口内应用排行，范围可选本周或最近 30 天。

- **新增 `--status` 命令 (New `--status` command)**: 智能体可以用 `-st` 更新状态条上的文字，并带上 `--detail`、`--progress`、`--thinking`、`--done`、`--waiting`、`--error`、`--agent-name`、`--project` 等修饰符。/ 智能体可以用 `-st` 更新状态条文字，支持 `--detail`、`--progress`、`--thinking`、`--done`、`--waiting`、`--error`、`--agent-name`、`--project` 等修饰符。

- **状态条文字颜色自选 (Pick the status text color)**: 悬停状态条时，图钉左侧出现一个色点，点它即可挑一个颜色，状态条文字颜色立刻跟着变；色点跟随当前文字颜色渲染，悬停时有高亮。/ 悬停状态条时图钉左侧出现色点，点一下即可自选状态条文字颜色，色点跟随当前颜色并带悬停高亮。

### Changed

- **用时统计改为始终开启 (Usage tracking always on)**: 删掉原来的开关，用时统计始终记录（只记进程名与时长，不碰任何内容）。单日明细顶部汇总从「应用排名」升级为「在座 / 操作 / 未操作 / 离开」四段人的画像，操作时间再按输入强度分成重操作与轻操作。/ 删除开关，用时统计始终记录（只记进程名与时长）。单日明细顶部汇总改为「在座 / 操作 / 未操作 / 离开」四段画像，操作时间再按输入强度细分。

- **点色带切换活动窗口 / 全天 (Ribbon toggles the activity window)**: 在今日记录色带上点击一次即可在「活动窗口」与「全天」之间切换，窗口起点对齐当天第一条真实记录。/ 点今日记录色带即可在「活动窗口」与「全天」之间切换，窗口起点对齐当天第一条真实记录。

- **伴生 `iclock.com` 控制台入口 (Companion `iclock.com` console entry)**: 裸输入 `iclock` 把命令原样透传给正在运行的 iClock 并同步返回退出码，终端提示符不再错位；智能体窗口前置检测更稳，临时启动的重复进程不再误判。/ 裸输入 `iclock` 原样透传给正在运行的 iClock 并同步返回退出码，终端提示符不再错位；智能体窗口前置检测更稳。

- **源码模块化重构 (Modular refactor)**: 单个 6400 行的 `iClock.cs` 拆成 16 个功能模块（`AgentHud`、`AgentWatcher`、`AgentTranscriptParse`、`Usage`、`Dialogs`、`SettingsDialog`、`Overlay`、`Quotes`、`Screensaver`、`EyeCare`、`Menu`、`Core`、`Controls`、`WelcomeAbout`、`CliLauncher` 等）；HUD 的 GDI 句柄缓存化，解析热路径换成零分配 JSON 键值嗅探器，内存 LOH 抖动消除，常驻内存稳在 10 MB 以内。/ 单个 6400 行的 `iClock.cs` 拆成 16 个功能模块；HUD 的 GDI 句柄缓存化，解析热路径换成零分配 JSON 嗅探器，LOH 抖动消除，常驻内存稳在 10 MB 以内。

- **界面细节 (UI details)**: 休息倒计时的文字前缀换成系统 `☕` 字形并画在数字正上方（固定暖棕色，不跟随倒计时颜色）；每日金句不再带弯引号；设置页倒计时行高收紧到 36 像素且弹窗高度不变；今日记录列宽等宽分布、弹窗尺寸锁定；所有强提醒统一收敛到同一个提醒弹窗，文字自适应 2–3 行、超长自动省略号、悬停可看全文。/ 休息倒计时换成 `☕` 字形；每日金句去掉弯引号；设置页行高收紧到 36 像素；今日记录列宽等宽、弹窗尺寸锁定；所有强提醒统一到一个弹窗，文字自适应 2–3 行并支持省略号与悬停全文。

- **打包脚本自动跟随版本号 (Packaging follows the binary version)**: `scripts/package.ps1` 不再硬编码版本号，直接读取 `src/iClock.cs` 里的 `CURRENT_VERSION`，产物名永远与程序自身一致。/ `scripts/package.ps1` 不再硬编码版本号，直接读取 `src/iClock.cs` 的 `CURRENT_VERSION`，产物名永远与程序一致。

- Binary size is now **435 KB** (444,928 bytes). / 单文件现为 **435 KB**（444,928 字节）。

### Fixed

- **状态条文字渲染更细腻 (Smoother HUD text)**: 抗锯齿模式换成 `AntiAliasGridFit`，边缘不再发毛；同时修掉了 90 秒无脑轮询的看门狗。/ 抗锯齿模式换成 `AntiAliasGridFit`，边缘不再发毛；修掉 90 秒无脑轮询的看门狗。

- **智能体状态不再误报 (Fewer false agent states)**: 解析前严格校验事件类型再取提示词，并加入时序连续性意图提取与游标解析过滤，工具调用不会被误判成「思考完成」。/ 解析前严格校验事件类型再取提示词，并加入时序连续性意图提取与游标解析过滤，工具调用不再被误判成「思考完成」。

- **应用颜色不再撞色 (Distinct app colors)**: 给新应用分配彩带颜色时，色相会自动滑动到与已分配颜色间隔至少 22°，相似名字的应用不再共用几乎一样的颜色；已有应用颜色保持不变。/ 给新应用分配彩带颜色时，色相自动滑动到与已分配颜色间隔至少 22°，相似名字的应用不再共用几乎一样的颜色。

- **常驻内存 < 10 MB (Resident memory under 10 MB)**: 低频内存维护每 5 分钟检查一次工作集，超过 8 MB 即整理一次；HUD GDI 句柄缓存化，解析热路径零分配，私有提交稳定在 35 MB 左右，无泄漏。/ 低频内存维护每 5 分钟检查一次工作集，超过 8 MB 即整理；HUD GDI 句柄缓存化，解析热路径零分配，私有提交稳定在 35 MB 左右，无泄漏。

- **深度协作关掉后说明依然可用 (Deep-collaboration guidance stays visible)**: 关闭「深度协作」开关时，安装技能卡片、复制提示词按钮与智能体列表不再整片变灰或被禁用，照样可读、可复制、可安装。/ 关闭深度协作后，安装技能卡片、复制提示词按钮与智能体列表依然可见可用。

- **钉住不再复活已结束的任务 (Pinning no longer resurrects finished tasks)**: 状态条钉住后会一直留在桌面，但已经完成超过 5 分钟的任务不会被重新拉回列表；智能体名字继续由下方的胶囊药丸承担。/ 钉住后状态条常驻，但完成超过 5 分钟的任务不会被重新拉回，智能体名字仍由下方药丸显示。

## [1.13] - 2026-10-24

### Fixed

- **应用颜色不再撞色 (Distinct app colors)**: when assigning a ribbon color to a new app, the hue now slides forward until it is at least 22° away from every already-assigned hue, so similar-looking names no longer share near-identical colors. Existing apps keep their colors. / 为新应用分配彩带颜色时，色相会自动滑动到与已分配颜色间隔至少 22°，相似名字的应用不再共用几乎一样的颜色；已有应用颜色保持不变。

### Changed

- **常驻内存 < 10 MB (Resident memory under 10 MB)**: a low-frequency maintenance timer checks every 5 minutes and trims the working set when it exceeds 8 MB; measured resident footprint is ~3.5 MB after trim. No leak: private bytes plateau at ~35 MB. / 新增低频内存维护：每 5 分钟检查一次，工作集超过 8 MB 即整理一次，整理后常驻约 3.5 MB；私有提交稳定在 35 MB 左右，无泄漏。

- Binary size is now **332 KB** (340,480 bytes). / 单文件现为 **332 KB**（340,480 字节）。

## [1.12] - 2026-10-24

### Added

- **每日专注目标 (Daily Focus Goal)**: countdown settings now have a daily focus goal dropdown — Off, or 1 / 2 / 3 / 4 / 6 / 8 hours. When a goal is on, the overlay draws a flat progress line under the digits, and the tray 「今日记录」 row shows today's total beside the goal (`1h20m / 4h`). Irregular values from the old minute spinner snap to the nearest preset. / 倒计时设置新增每日专注目标：关闭，或 1 / 2 / 3 / 4 / 6 / 8 小时。开启后悬浮数字下方画一条平直进度线，托盘「今日记录」右侧显示当天累计与目标。旧数字框里的不规则分钟会靠到最近一档。

- **进行中的倒计时写入今日记录 (In-progress sessions in Today's History)**: a started but unfinished countdown appears in Daily Details as 「进行中」, with elapsed time counted into today's total. The tray hint and overlay bar refresh on the minute. / 已开始、尚未结束的倒计时会作为「进行中」出现在单日明细里，已走时长计入当天累计；托盘提示与进度条按整分钟刷新。

### Changed

- **托盘菜单收纳语言与开机自启动 (Language and Auto-start on the tray menu)**: both settings moved out of the countdown tab onto the tray menu. Language is a submenu that stays open after a switch; Auto-start is a capsule switch that stays open when toggled. Menu groups are now: session controls | History + Settings | Language + Auto-start | About | Exit. Width stays the same in Chinese and English. / 语言与开机自启动从倒计时设置挪到托盘菜单。语言用子菜单切换且保持菜单打开；自启动是胶囊开关，切换后菜单也不收。分组为：倒计时操作 | 历史与设置 | 语言与自启动 | 关于 | 退出。中英文宽度一致。

- **设置页输入对齐 (Settings inputs aligned)**: timer fields share a 28 px row height; the end-message box shows a caret when clicked. Binary size is now **315 KB** (322,048 bytes). / 倒计时设置输入框统一 28 像素高，结束消息框点击后能看到光标。单文件现为 **315 KB**（322,048 字节）。

### Fixed

- **开机自启动真正写入 Windows (Auto-start actually registers)**: enabling Auto-start now writes the 64-bit HKCU Run key and clears a leftover StartupApproved block, so Windows Explorer launches iClock at logon. / 打开开机自启动会写入 64 位 HKCU Run，并清掉 StartupApproved 里的禁用标记，登录后资源管理器会真正拉起 iClock。

## [1.11] - 2026-10-23

### Added

- **自动更新 (Auto Update)**: the About dialog's 「检查更新」 button now goes all the way — when a newer release exists the button turns into 「下载 v1.x」, shows the download progress, and becomes 「重启应用更新」; one click replaces the running program and restarts it. / 「关于」对话框的「检查更新」现在能走完全程：发现新版本后按钮变成「下载 v1.x」，下载过程显示进度，完成后变成「重启应用更新」，点一下即替换正在运行的程序并重启。
  - Windows does not allow overwriting a running executable, so iClock renames itself aside and moves the new build into place. If the file is locked the swap is applied on the next launch, and a failed swap now moves the original back instead of leaving the program without its file. / Windows 不允许覆盖正在运行的 exe，iClock 会先改名让位再放入新版本；文件被占用时会在下次启动自动补上，替换失败也会把原文件放回去，不会让程序丢失文件。
  - The latest version is read from the release page redirect instead of the anonymous GitHub api, which answers 403 once its 60 calls per hour and ip quota is used up; a short cache keeps a launch check and a manual check from asking twice in a row. / 最新版本号改从 release 页跳转读取，绕开匿名 API 每 IP 每小时 60 次的配额（用尽即 403）；短时间内重复检查走缓存。

- **每日金句屏保 (Daily Quotes Screensaver)**: iClock can register itself as the Windows screensaver and show the day's quote full-screen.
  - The quote page now leads with the master switch, the screensaver toggle is a single switch (default **off**), and the idle wait moved into a dropdown in the quote page — right-click the toggle for 1 / 5 / 10 / 15 / 30 minutes.
  - The wait time is written to `SCRNSAVE.EXE` (the value Windows actually reads) as well as the registry, and `/s` launches are logged to `saver.log` so a Windows-side launch failure can be diagnosed.
  - A **预览 (Preview)** button launches the real screensaver (mouse or Esc exits), so the preview is what you get by construction instead of a lookalike.
  - The screensaver follows the configured quote font size, anchors the text block by its centre at 38% of screen height, draws text only (no card frame) on black, and paints a 32px brand mark over a soft accent halo.

### Changed

- **AI 扩充金句改为「一段指令，任意智能体」**: the expansion dialog no longer lists or picks an agent. It builds one instruction, copies it to the clipboard (`复制指令`), and waits; the agent you are already using imports the quotes through the iClock command line (`--quote-add`), exactly as before. The dialog now shows the keyword box, the quote count (5–100) and one instruction line, and the waiting view spells out the three steps instead of naming an agent. The window was tightened to **380 × 280** while doing so. / 扩充弹窗不再列出或选择智能体，只负责生成指令、复制到剪贴板并等待入库——导入仍由智能体执行 iClock 命令行完成，与此前一致；弹窗同时收紧为 380 × 280。

- **每日格言 → 每日金句 (Daily Quotes renamed to Daily Golden Lines)**: user-facing Chinese wording now uses 「每日金句」 across the app UI (settings tab, desktop toggle, screensaver row, tooltips, AI expansion prompts and notices) and the current README / user guide / website copy. The desktop toggle label no longer carries the 「便签」 suffix. The AI expansion prompt now asks for `金句内容 | 作者或出处` lines, while the quote library itself (`quotes.txt`, CLI commands and settings keys) is unchanged, and the English UI keeps "Daily Quotes".

### Fixed

- **Countdown-end rest length is actually selectable (倒计时结束的休息时长终于能选)**: the popup's primary button used to carry the ▼ inside its text while the clickable zone was only the last 24 pixels of the button — the visible arrow sat *outside* that zone, so the preset menu was practically impossible to hit, and clicking the words「休息 10 分钟」started the break straight away. The arrow now lives in its own drawn zone (36px, separated by a hairline, highlighted on hover), the text stays centred in the remaining space, the menu marks the length in use with a ✓, and the zone is decided from the mouse-down position so it no longer depends on the order in which the framework raises Click and MouseClick. / 休息时长按钮的 ▼ 原本画在文字里，而可点区域只有最右侧 24px，可见箭头落在可点区之外，菜单几乎点不中，点文字反而直接开始休息。现在箭头独占一块 36px 的绘制区域（有分隔线、悬停高亮），文字在剩余空间居中，菜单用 ✓ 标出当前时长，点击区域改由按下位置判定，不再依赖 Click / MouseClick 的触发顺序。
- The screensaver's startup self-heal no longer resets the idle wait time, and the desktop-note toggle label is no longer clipped.
- The agent rows in the Settings agent tab draw their dividers across the full card width and right-align the install button.

### Removed

- Removed the headless "let iClock run the agent for you" track of the quote expansion: iClock no longer spawns `claude -p` / `ollama run` (that path could only ever hit its 35-second timeout), the agent dropdown and its running/ready list, the `唤起窗口` window-activation helper, the `settings.AiAgentType` / `settings.AiAgentCustomCmd` keys and the now-unused agent-CLI helpers. The agent list in the Settings agent tab is untouched — it belongs to the skill installer.
- Removed the iClock MCP server and its agent-side tool/configuration workflow. Desktop collaboration is available through CLI commands with structured `--json` responses. Existing agent MCP entries are not migrated or deleted automatically.

## [1.10] - 2026-10-21

### Changed

- **AI Collaboration Page Revamp (AI 协作页重构)**: the page was reordered as value → master switch → install skill, skills are installed per agent into that agent's own directory, and only the running agents are listed, each row doubling as its install state.
- **Minimal Task Status HUD (极简任务状态浮层)**: the progress overlay lost its card, agent name and frame, gained real per-pixel alpha, and can now be moved, resized and re-styled with Alt (drag, edge drag, Ctrl+Alt+wheel), with the edits persisted.
- **AI Quote Expansion Dialog (AI 扩充金句对话框)**: the preference became a free-form multi-line box (keywords or a whole sentence), and a quote count (5–100, default 30) was added and parameterised into the prompt.
- **Daily Quote Persistence (每日金句持久化)**: the quote shown today is remembered, so the settings preview, the desktop pager and a restart all show the same line, and a new day continues from the next one.
- **Quote Color Palette Independence (金句颜色独立色板)**: the quote colour decoupled from the countdown colour (default warm orange `#F77F00`) with its own 8-colour soft palette; the task HUD follows the quote colour.

### Added

- **Two Ways to Enable Collaboration (两种启用方式)**: install the skill for a durable setup, or copy a one-off instruction to the current session.

## [1.09] - 2026-10-19

### Fixed

- **Canonical Settings & Immediate-Apply Dialog (设置单一数据源与即改即生效)**:
  - Settings dialog now edits the canonical `Settings` instance directly: draft/preview copies and the hidden save/cancel buttons were removed, every change applies to the live overlay instantly, and configuration drift between the dialog and the running state is structurally eliminated.
  - Quote widget now captures and persists its actual on-screen position, width, and font size before settings/exit transitions, so preview or close paths can no longer overwrite newer user adjustments.
- **Multi-Monitor Desktop Quote Restore (多屏桌面格言位置与宽度恢复)**:
  - Cold quote construction restores the saved position, width, and font size on secondary monitors, including negative-coordinate layouts.
  - First visible render performs a native layered-geometry bounds sync, eliminating the first-appearance jump of the floating quote.
  - `NextQuote` and the settings-tab refresh path preserve saved coordinates and are idempotent; disconnected-monitor coordinates still gracefully fall back to the primary screen.
  - A hidden retained quote instance is treated as not visible and restored by the normal startup/unlock path.
- **Structured MCP Tool Responses (MCP 工具结构化 JSON 响应)**:
  - All six MCP tools now return structured JSON (`status` / `action` / `message` ...), letting agents programmatically distinguish success, cancellation, and timeout.
  - `iclock_ask_human` gains `action: "cancel"` plus `cancelled` / `timed_out` semantics; `question` is no longer required.
  - `iclock_quote` gains `clear` / `hide`; `iclock_progress` validates percentage/fraction/decimal input and rejects malformed values; `iclock_notify` accepts `agent_name`.

### Optimized

- **Regression Suite Hardening (回归测试强化)**:
  - SelfCheck suite expanded and updated for canonical settings, hotkey availability flags, quote persistent-state capture, hidden quote restoration, and secondary-screen coordinate retention (45 checks, all green).
  - Added standalone multi-monitor `tests/QuoteLayoutCheck.cs` (compiled by `build.ps1`), covering cold restore, first-visible layered geometry, settings-refresh stability, and disconnected-monitor fallback.

### Docs

- **Version & Metadata Alignment (文档与元数据对齐)**: bumped to 1.09 across README (both languages), official website, `llms.txt` / `llms-full.txt`, and Scoop/WinGet manifests; fixed the stale 1.07 portable ZIP download name; corrected binary size claims to 275 KB (281,600 bytes).

## [1.08] - 2026-10-18

### Added

- **Tray Double-Click Debouncing & False Trigger Elimination (托盘双击防抖与倒计时零误触)**:
  - **Debounced Tray Single-Click (单击智能防抖)**: Implemented a 200~250ms dynamic debounce window on tray left clicks. Single-clicking continues to toggle countdown cleanly after the window expires.
  - **Instant Double-Click Cancellation (双击即时取消切换)**: Double-clicking the tray icon immediately aborts pending countdown toggle and opens the Settings dialog, completely eliminating the WinForms NotifyIcon `MouseClick` vs `DoubleClick` race condition.
- **Rich Native Synthesized Chimes (多样化原生合成音效)**:
  - **Door Knock Chime (拟真敲门声)**: Added rhythmic physical resonance door knocking ("咚咚咚, 咚咚咚, 咚咚咚" x3) before alarm ring in end sound styles.
  - **Dedicated AI Collaboration Chime (AI 协作专属温和提示音)**: Added a gentle rising triad chime specifically for AI agent task completion, with pure Win32 `Speech On.wav` fallback, clearly distinguishing AI alerts from countdown finish alarms.
  - **Doorbell Chime (叮咚门铃声)**: Three crisp ding-dong chord pairs for comfortable focus completion reminders.
- **Enhanced Flash Approval HUD (智能体决策请示卡醒目度升级)**:
  - Enhanced `iclock_ask_human` UI with a 3.5px left accent bar, 10.5pt clear question typography, and radio card rows for prominent human-in-the-loop decision making.
- **Desktop Note Projection & Quote Stability (桌面便签投射与格言显示保护)**:
  - Ensured projected desktop notes remain TopMost and mutually exclusive with auto-dismiss on agent exit.
  - Suppressed daily quote preview in settings while a desktop note projection is active, preventing overlapping text.
  - Fixed sequential numbering so AI-expanded desktop quotes follow historical serial numbers in order.
  - Preserved on-screen desktop quote content when switching to the "每日格言" tab in settings.

### Optimized

- **Zero-GC & Memory Sawtooth Elimination (平稳低内存与零分配优化)**:
  - Eliminated high-frequency `EmptyWorkingSet` periodic sawtooth oscillations and replaced heavy `Process` APIs with zero-alloc Win32 native methods.
  - Optimized Settings Tab 2 control rendering to eliminate layout thrashing.
  - Maintained executable binary size at ultra-light **273 KB (279,040 bytes)** with zero external dependencies.

## [1.07] - 2026-10-17

### Added

- **Streamlined Tray Menu & Terminal CLI Guidance in AI Collaboration Tab (托盘菜单瘦身、移除冗余命令行弹窗并归并至 AI 协作设置)**:
  - **Eliminated Redundant GUI Dialog (彻底移除冗余的图形化命令行窗口 `CliHelpDialog`)**: Deleted the 185-line WinForms `CliHelpDialog` class and associated window management code. Standardized terminal CLI help on `iclock.com -h` and `iclock --help` (single source of truth for all commands, options, and exit codes).
  - **Streamlined Tray Menu (系统托盘一级菜单瘦身)**: Removed the niche `命令行 (CLI)...` item from the system tray context menu, reducing clutter and cognitive friction for everyday users.
  - **Consolidated Terminal CLI Tip in Settings Tab 2 (在「AI 协作」页面底部加入终端指令引导)**: Added a clean guidance tip at the bottom of Tab 2 merged into a single sentence (`提示：支持在终端直接运行 iclock -h 查看脚本控制指令，或执行 iclock --install-cli 写入 PATH 环境变量。` / `Tip: Run iclock -h in terminal for CLI & script automation, or run iclock --install-cli to add iClock to User PATH.`). Implemented custom GDI+ word-wrapping via `StyledCliTipLabel` with automatic width wrapping (no hardcoded `\n`) and syntax-highlighted commands (`iclock -h` and `iclock --install-cli`) rendered in **Italic Blue** (`AppContext.Win11Accent` `#0067C0`), seamlessly matching the Win11 visual rhythm of Tab 1.
  - **Optimized Tab 2 Vertical Spacing & 3-Row Agent List (优化智能体列表高度为规整 3 行)**: Reduced `panelAgentBox` height from 138px to 114px so exactly 3 agent cards are displayed without partial bottom card truncation. Freed up 24px of vertical breathing room, cleanly distributing margins across the header, agent list, MCP capabilities card, and the bottom tip.
  - **SelfCheck Test Suite Updated (自检测试套件全面同步)**: Updated Tests 33 and 40 in `tests/SelfCheck.cs` to verify `menuCli` elimination, `CliHelpDialog` removal, Tab 2 `lblAgentCliTip` presence, command text containment, and visibility across both languages (all 44 checks passing).

- **Desktop Quote Alt + Drag Mode & Zero-Polling Background Optimization (桌面格言 Alt + 拖拽交互统一与零轮询性能优化)**:
  - **Interaction Consistency (交互体验完全统一)**: Aligned desktop quote position adjustment with the countdown overlay (`Alt + Drag`). In normal desktop state, the quote is 100% click-through (`HTTRANSPARENT`), never intercepting clicks or accidentally popping up when moving the cursor. Holding `Alt` (or clicking "调整位置" in tray menu) instantly makes it interactive (`HTCLIENT`), revealing its rounded translucent card frame, action buttons (Next quote, Pin, Close), and resize handle.
  - **Eliminated 200ms Continuous Cursor Polling (彻底移除 200ms 鼠标轮询)**: Removed the legacy periodic timer checking `Cursor.Position` and bounding box containment 5 times per second. When pinned to desktop (`IsPinned == true`), `countdownTimer` is completely stopped. Result: **0 timer wakeups, 0% CPU, and zero memory allocations** while idling on desktop.
  - **Tray MoveMode Synchronization (托盘调整位置模式双向联动)**: `AppContext.ToggleMove()` now synchronously enables/disables `MoveMode` on `DailyQuoteDialog`. Clicking "调整位置" from the tray menu activates drag frames for both countdown and desktop quote simultaneously.
  - **Settings Tab 1 Tip Refinement (设置界面底部操作提示与 ToolTip 同步更新)**: Updated `lblQuoteTip` on Tab 1 with unified `StyledTipLabel` supporting clause-aware auto-wrapping without manual linebreaks, precise scope clarification (`提示：在桌面格言文字上按住 Alt + 拖拽调整位置，拖动右边缘调节宽度，Alt + Ctrl + 滚轮缩放字号。` / `Tip: Hold Alt + drag on quote text to move, drag right edge to resize, Alt + Ctrl + wheel to scale font.`), and syntax-highlighted shortcut tokens (`Alt + 拖拽` and `Alt + Ctrl + 滚轮`) rendered in **Italic Blue** (`AppContext.Win11Accent` `#0067C0`), aligning baseline layout with Tab 2 (`(18, 412, 344, 40)`), and added descriptive hover tooltip on `桌面悬浮显示` checkbox.
  - **Added SelfCheck Test 44 (自检测试覆盖)**: Added test suite coverage verifying `MoveMode` state toggling, `HTCLIENT`/`HTTRANSPARENT` transitions, and timer shutdown when pinned (44/44 tests green).

- **About Dialog Author Email & Feedback Channel (关于页面新增作者邮箱与即时反馈交互)**:
  - Added author email link `francodeyvison183@gmail.com` in `AboutDialog` with clean, two-line centered layout (`作者：gujiangwen` followed by `反馈交流：francodeyvison183@gmail.com` / `Feedback: francodeyvison183@gmail.com`).
  - Implemented modern interaction: hover-only underline (`HoverUnderline`), `mailto:` trigger on click, automatic clipboard copy with native 2-second ToolTip toast (`✓ 邮箱地址已复制到剪贴板！` / `✓ Email copied to clipboard!`).
  - Adjusted window height from 506px to 528px (+22px) with balanced vertical spacing and native Win11 Fluent styling.
  - Added test assertion coverage in `tests/SelfCheck.cs` (Test 40) ensuring 100% test pass rate across all 43 tests.

- **Fix Tray Scroll Wheel Hijacking Running Countdown (修复托盘启动倒计时后滚轮意外篡改时间缺陷)**:
  - Addressed root cause: `tray.MouseMove` previously installed a global low-level mouse hook (`WH_MOUSE_LL`) with a 2-second timeout lease that intercepted wheel messages system-wide and altered running countdown sessions.
  - Added strict state gating: when the countdown is running or session is active (`running || sessionActive`), `tray.MouseMove` never installs the hook, and `SetState` / `Toggle` / `tray.MouseClick` immediately unhooks any active hook.
  - Added cursor workspace boundary check in `MouseHookCallback`: if the mouse cursor leaves the taskbar notification area and enters the desktop workspace (`WorkingArea.Contains`), the hook immediately self-unhooks, preventing mouse wheel hijacking in editors or browsers.
  - Reduced idle hover lease timeout from 2000ms to 800ms and removed recursive timeout resets on wheel turns.

- **Desktop Quote Default Horizontal Centering & Decoupling (桌面格言默认水平居中与坐标解耦)**:
  - Decoupled `QuoteX` and `QuoteY` coordinate resolution in `DailyQuoteDialog.UpdatePosition`: `QuoteX < 0` (sentinel `-99999` and `-1`) guarantees standard horizontal centering on screen (`WorkingArea.Left + (Width - CardWidth) / 2`), while preserving custom vertical position `QuoteY >= 0` (or default Y=120px sandwich position).
  - Ensured dynamic re-centering on quote length or font size changes when in default centered mode (`QuoteX < 0`), preventing horizontal drifting.
  - In `OnMouseUp`, resizing width no longer hardcodes `QuoteX` to a fixed pixel coordinate when in auto-center mode, and dragging near horizontal center (±24px) snaps cleanly back to `QuoteX = -1`.

- **AI Agent Quote Expansion (每日格言 AI 智能体 MCP 极简自动扩充)**:
  - Added side-by-side `[ 📂 自定义名言 ]` (90px) and `[ ✨ AI 扩充… ]` (90px) buttons in Settings Tab 1, perfectly matching the 188px column alignment with surrounding controls.
  - New minimalist `AiQuoteDialog` window (410 × 265px) featuring a unified tag pool model:
    - Default curated tags (`极客心流`, `斯多葛`, `东方智慧`, `从容反内卷` in ZH; `Geek Flow`, `Stoicism`, `Wisdom`, `Balance` in EN).
    - Every tag pill features a delete button (`✕`) allowing removal of any tag.
    - Quick input box with `➕ 添加` (or Enter key / comma-separation) for adding custom preference tags.
    - Tag changes automatically persist across restarts in `settings.ini` (`AiQuoteKeywords`).
    - All tags in the pool apply directly to generation without redundant labels.
  - **Dual-Track In-Place Execution & Completion Reporting (双轨就地执行与实时汇报反馈)**:
    - Eliminates silent dialog closing: clicking "立即扩充" keeps the dialog open in-place, guiding user actions and eliminating expectation mismatch.
    - **CLI Track (后台直接扩充)**: For agents supporting direct headless CLI generation (e.g. Claude Code CLI, Ollama, Antigravity CLI), clicking `[ ⚡ 直接扩充 ]` runs generation in background, parses quotes, saves them to library, and immediately presents the success report in the dialog with quote preview and desktop projection.
    - **IDE Collaboration Track (对话框协作)**: For IDEs (e.g. Cursor, Windsurf), clicking `[ ✨ 立即扩充 ]` copies 30-quote prompt to clipboard, attempts foreground window activation, and enters a real-time listening state (`• 正在等待智能体通过 MCP 写入词库...`). Provides `[ 唤起窗口 ]` and `[ 重新复制 ]` shortcuts.
    - **Realtime IPC Callback**: Subscribes to `AiAgentQuoteHelper.OnQuotesAdded`; when the agent executes `iclock_quote(action: "add")`, the dialog transitions on the fly to `✨ 格言扩充完成！` reporting the 30 added quotes, previewing the latest quote, and providing `[ 查看词库 ]` and `[ 完成 ]` actions.
  - 100% aligned with Tab 2's MCP ecosystem:
    - Candidate list exclusively presents agents that have already connected to MCP (e.g. `⚡ [Name] (已接入 MCP · 运行中 · 支持后台直接扩充)` / `⚡ [Name] (已接入 MCP · 已就绪 · 对话框协作)`).
    - Unconfigured agents are completely hidden; if no MCP agent is configured, displays a prompt to configure MCP in 【Settings ➔ AI Collaboration】 and disables the Expand button.
    - Automatically remembers the user's selected agent in `settings.AiAgentType`.
  - Batch expansion scale set to **30 curated quotes** per invocation.
  - Native `iclock_quote` MCP tool enhancement:
    - Extended schema with `action: "add"` and batch `quotes: [...]` parameters.
    - Agent calls tool to atomically write quotes with anti-hallucination parsing, `.bak` backup, strict deduplication, and instant desktop HUD refresh.
  - Added unit and integration test suite verification in `SelfCheck.exe` (Test 43, 43/43 passing).

- **Quote Library Button Layout & Text Optimization (名言词库操作按钮文案与布局优化)**:
  - Resolved right-edge text truncation on the Quote Library row in Settings Tab 1.
  - Replaced overly wide labels with crisp, clean labels: `[ 自定义… ]` (54px) and `[ ✨ AI 扩充 ]` (68px) in Chinese; `[ Edit Quotes ]` (72px) and `[ ✨ AI Quotes ]` (74px) in English.
  - Added descriptive hover ToolTips (`在记事本中自定义编辑名言 (quotes.txt)` / `通过 AI 智能体 / MCP 批量创作并扩充词库`) for complete clarity.

- **Silent MCP Handshake & Prompt-Driven Verification (MCP 静默握手与提示词驱动的主动验证)**:
  - Eliminated the intrusive C# bottom-right popup toast (`McpToastDialog`) and configuration field (`McpNotifiedClients`), removing dead UI code and Win32 process-tree queries (`CreateToolhelp32Snapshot`).
  - Upgraded MCP setup prompt (`McpConfigHelper.GenerateAgentPrompt`): after merging configuration, the AI Agent instructs the user to reload the editor window (e.g. `Developer: Reload Window` in Cursor) and then calls the `iclock_notify` tool to send a greeting, verifying desktop connectivity on demand.

- **Removal of Legacy Tool-Side MCP Auto-Injection (彻底移除工具端盲注代码与过时指令)**:
  - Completely deleted legacy tool-side file manipulation code: `McpConfigHelper.Inject`, `McpConfigHelper.Remove`, and JSON formatting utility `McpConfigHelper.FormatJson` (~150 lines).
  - Removed obsolete `--mcp-install` and `--mcp-uninstall` CLI commands and help entries.
  - Fully embraced the modern AI Agent prompt-driven autonomous configuration workflow (`--mcp-prompt`), eliminating brittle hardcoded path guessing and the risk of overwriting user configuration files, while preserving clean read-only MCP detection (`IsAgentMcpConfigured`).

- **Binary Footprint Reduction & Codebase Pruning (可执行文件体积深度优化)**:
  - Deduplicated repeated ComboBox setup logic in `SettingsDialog` (`AddCombo`).
  - Pruned unused helper overloads in `AiAgentQuoteHelper` and replaced redundant closure delegates with native button hover properties.
  - Eliminated `CliHelpDialog`, `McpToastDialog`, `McpNotifiedClients`, and legacy tool-side MCP injection routines.
  - Maintained `iClock.exe` size at ultra-light **253 KB (260,096 bytes)** while packaging full Win11 Fluent UI, stdio MCP Server, IPC pipeline, and 20+ AI agent presets.
  - All 44 regression tests in `SelfCheck.exe` remain 100% green.

## [1.06] - 2026-10-16

### Added

- **Smart Custom Quote Sequence Number Stripping (自定义名言序号智能剥离)**:
  - Supports adding arbitrary sequence numbers to quotes in `quotes.txt` for easy maintenance and sorting in text editors (e.g. `1. `, `02、`, `[3]`, `(4)`, `5) `, `6: `, `No. 7 `, `#8 `, `①`, `1．`, `１．`).
  - Sequence numbers are automatically parsed and stripped at the `QuoteItem` single-point choke gate without altering the quote text or author.
  - Desktop quote HUD overlay, Settings Tab 1 preview card, and MCP status projections automatically present clean, unnumbered quotes.
  - Anti-false-positive regex preserves numbers within actual quote texts (e.g. `《1984》是一部好书`, `3.1415926是圆周率`, `1984年发生的事`, `100%努力`).

- **Native MCP (Model Context Protocol) Server & Desktop Decision Gate (`iclock.com --mcp`)**:
  - Built-in JSON-RPC 2.0 stdio Model Context Protocol (MCP) server directly inside `iclock.com` with **0 external dependencies** (using .NET standard library `JavaScriptSerializer`).
  - Seamlessly bridges AI assistants and autonomous coding agents (Claude Desktop, Cursor, Antigravity, Cline, Roo Code, etc.) with the user's physical Windows desktop.
  - Features 5 native MCP tools:
    - **`iclock_ask_human` (Desktop Decision Gate / 桌面决策请示门禁)**: The flagship Human-in-the-Loop tool. Allows AI agents to pop up an elegant, TopMost Win11 Fluent modal card on the user's screen before making high-stakes decisions (e.g. database migration, deleting files, committing to main, paid API calls). Supports option buttons with number key shortcuts (`1`..`9`), free-text input mode, and defaults to patient infinite wait (永不超时 / 无倒计时焦虑), with optional timeout for unattended scripts.
      - **智能前后台感知与免打扰 (Smart Focus Detection)**：当宿主（如 Cursor）处于前台活跃状态时，门禁窗口以温和非抢焦模式（`ShowWithoutActivation`）置顶浮现，不中断正在打字输入的代码与编辑器焦点；仅当宿主处于后台（用户切去浏览器、全屏视频等）时才触发机械铃音与强聚焦唤醒。
      - **支持 `background_only` 聊天窗自适应降级**：支持传入 `background_only: true`。若检测到用户正聚焦宿主窗口，工具立即返回 `{ in_foreground: true }`，引导智能体自主转而在内置聊天窗口提问。
    - **`iclock_timer`**: Start work/break timers, pause, resume, or reset active desktop countdowns with custom end messages.
    - **`iclock_notify`**: Push custom desktop notification alerts with optional sound effects when background agent tasks complete.
    - **`iclock_quote`**: Display inspirational quotes or custom status messages directly on the desktop overlay.
    - **`iclock_status`**: Real-time query of the countdown timer's active state, remaining seconds, and configured duration.
  - **Zero-Modal Unified MCP Setup & 3 Core Capabilities Showcase (零弹窗单页统一 MCP 配置与三大核心能力演示矩阵)**:
    - **智能体列表精简为 3~4 行与滚动暗示**：将智能体检测列表由 232px 精简至 116px（完整容纳 3 张卡片并自然微露第 4 张上沿，保留流畅鼠标滚轮与滚动条），释放出 116px 黄金纵向空间。
    - **剔除生硬黑色外框，升级原生 Win11 纯白卡片与微发丝线分割 (Hairline Divided Card Container)**：
      - 彻底去除各功能项原生的 `BorderStyle.FixedSingle` 粗黑单线边框。
      - 采用一体化纯白卡片容器 (`panelMcpBox`)，外围环绕浅淡精致的 Win11 边框 (`#D8DCE4`)，行间通过微发丝分割线 (`#EEF0F4`) 柔和分隔，完美融入 Win11 设置美学。
    - **精简聚焦 3 项刚需核心能力与统一对齐的 `[ 演示 ]` 按钮**：
      - 去除智能体协作中必要性较低的“倒计时协同”项，聚焦高价值刚需场景：
      - **决策请示门禁 (`ask_human`)**：关键高危操作置顶阻断式人工审批 $\rightarrow$ `[ 演示 ]`（弹出置顶阻断决策卡片）。
      - **任务完成提醒 (`notify`)**：后台耗时任务完成时全向弹窗与声音唤醒 $\rightarrow$ `[ 演示 ]`（播放机械铃音并弹出完成气泡）。
      - **桌面状态投射 (`quote`)**：智能体在屏幕投射当前进展与工作便签 $\rightarrow$ `[ 演示 ]`（桌面浮现磨砂态势浮窗，展示 AI 实时状态）。
    - **智能体后台任务提醒与 MCP 直连同级融合架构 (Proposal A+C Macro Fusion & Live Status Toggle)**：
      - **同级对齐的两大核心板块**：
        - **上半板块（智能体后台任务提醒）**：采用两行式左侧标题区（粗体标题 + 核心能力副标题说明），右侧对称布置 `[ 重新检测 ]` (72px) 与动态状态切换胶囊按钮 `[ ● 已开启 ]` / `[ ○ 已停用 ]` (76px)，支持用户点击即时启停后台任务检测，同时卡片状态在“正在监听”与“未监听”之间联动过渡；智能体列表精简卡片结构，仅呈现清晰的状态圆点、进程名称与状态徽章（`[ 运行中 ]` / `[ 已就绪 ]` / `[ 未监听 ]`），彻底移除了脆弱且冗余的行内单体配置按钮，配合原生 Win11 7px 超轻极简滚动条，杜绝空白回顶异常。
        - **下半板块（MCP 协议直连·通用智能体一键接入提示词）**：粗体标题 `MCP 协议直连 (3项核心能力)` 与右侧 `[ 复制接入提示词 ]` (118px) 严密对齐；点击即可一键将结构化的智能体接入提示词（含当前机器上的 `iclock.com` 绝对物理路径与参数说明）写入剪贴板，用户只需直接发给任意 AI 智能体（Antigravity、Cursor、Claude、Windsurf、Cline、Roo Code 等），由智能体依据宿主环境全自动完成合并与配置，省去繁琐的手动编辑；下承 150px 纯白发丝线卡片，清晰罗列决策请示门禁、任务完成提醒、桌面状态投射三大核心能力，每项右侧统一匹配极简 `[ 演示 ]` 按钮。
        - **CLI 新增 `--mcp-prompt` 命令行指令**：终端直接执行 `iclock --mcp-prompt` 即可标准输出专用于 AI 智能体的 MCP 接入提示词。
    - **彻底移除底部“保存”和“取消”按钮及分割线（Win11 Fluent 即改即存模式）**：
      - 依循 Windows 11 现代 Fluent 交互规范，消灭传统旧式对话框底部的“保存”和“取消”按钮及横向分割底线。
      - 窗口客户区高度精简至 468px，垂直边距均匀通透，三个 Tab 均告别遮挡与冗余视觉噪音。
      - 引入即改即存（Auto-save on change/close）机制：关闭设置窗口时全量草稿设置自动提交持久化；“开机自启动”勾选时支持即时检测临时/下载目录风险提示，提升交互平滑度与安全性。
    - Automated test coverage: Check 41 in `tests/SelfCheck.cs` verifying full MCP protocol handshake (`initialize`, `ping`, `tools/list`, error handling), `McpConfigHelper` mapping and lossless merge/removal, unified Tab 2 MCP controls (`btnMcpCopyJson`, `btnMcpDemo`, `btnMcpDemoNotify`, `btnMcpDemoQuote`), `GenerateAgentPrompt`, `--mcp-config`, `--mcp-prompt`, dynamic status toggling, and IPC dispatch. All 41 tests pass with 100% success rate.

- **Daily Inspirational Quote Popup (每日格言 / Daily Quotes)**:
  - Automatically pops up once daily upon application startup or morning unlock, elegantly floating horizontally centered at `Y = 120px` (comfortably below countdown text `Y = 60..96px` and above the centered settings panel `Y = 242px+`).
  - Ultra-minimalist borderless design: 100% transparent background and mouse click-through (`HTTRANSPARENT`) by default, completely unblocking clicks to underlying windows or documents. Removed redundant headers to direct attention straight to quote text and author.
  - Hover activation & 80% frosted glass transparency: moving mouse into the card dynamically surfaces a Win11 frosted acrylic card rendered with native 32-bit ARGB `UpdateLayeredWindow` at 80% transparency (20% opacity), with micro vector action glyphs (`↻` Next Quote / 换一句, `📌` Pin to desktop / 钉在屏幕, `✕` Close / 关闭) and bottom progress line.
  - Font & color consistency with countdown: automatically inherits the countdown timer's font family (`settings.FontFamily`) and vibrant color (`settings.Color`), with adaptive dual-layer high-contrast drop-shadows ensuring crystal-clear text on any light or dark background.
  - Right-edge width drag resizing: hover over the right edge (highlighted with a 3-dot grip handle and `↔` cursor) to freely widen or narrow the card; text re-wraps in real time, card height adapts dynamically, and customized width is automatically saved across sessions (`QuoteWidth`).
  - Zero-overhead adaptive timer: 巡检 timer frequency optimized to 200ms (cutting 75% timer wakeups), and completely hibernates (0% polling) during hover interactions via event-driven Win32 mouse tracking.
  - Direct dragging & intuitive gestures: double-click anywhere on the card to cycle to the next quote, press `Esc` to dismiss, drag anywhere on the card blank area to reposition freely with automatic coordinate persistence, and `Ctrl + MouseWheel` to scale font size (`8pt` - `24pt`) with automatic persistence across sessions (`QuoteFontSize`).
  - Dual quotes source: bundled with 10 classic quotes, and seamlessly reads user-defined quotes from `quotes.txt` with adaptive UTF-8 / ANSI encoding detection.
  - 3-Tab Settings Architecture & Aesthetic Customization (三标签独立架构与独立美化):
    - SettingsDialog cleanly refactored into 3 independent tabs: `倒计时 (Timer)`, `每日格言 (Daily Quotes)`, and `AI 智能体 (AI Agents)`. Upgraded tab navigation to authentic **Windows 11 File Explorer Multi-Tab Architecture (Win11 资源管理器标签页风格)**: pure white elevated cards with 6px top rounded corners and seamless flat-bottom connection directly into the content pane for the active tab, subtle 14px vertical dividers between inactive tabs, and pure clean typography without emoji visual clutter.
    - **Top-Left "今日" / "TODAY" Diagonal Corner Ribbon Badge (格言框左上角 45° 缎带角标)**:
      - 在「每日格言」展示卡片 (`panelQuoteCard`) 左上角新增 Windows 11 主题蓝 (`#0067C0`) 45° 斜向缎带小三角角标。
      - 纯白粗体字标示“今日”（英文环境下自适应为“TODAY”），赋予卡片精致的今日日历与便签标签感；通过原生 GDI+ 矢量渲染 (`FillPolygon` + 旋转 `DrawString`)，零内存泄漏、零额外控件开销。
    - Unified Color ComboBox & Preset Palette (双面板统一色卡下拉框):
      - Replaced the asymmetric 128px "点击选取…" button and 52px preview square in Tab 0 with the exact same full-width 188px `ComboBox` (`timerColorCombo`) used in Tab 1.
      - Integrated 9 one-click preset colors (Classic Red, Soft White, Warm Gold, Mint, Cyan, Pink, Green, Purple, Warm Orange) with anti-aliased 10px circular color chips, plus a "自选… / Custom…" option for arbitrary colors.
      - 100% shared rendering logic (`DrawColorItem`) and real-time cross-tab synchronization with `quoteColorCombo` ("跟随倒计时").
    - Pinned Desktop Quote Persistence on Tab Switch (标签页切换桌面格言防清除):
      - Fixed an issue where switching from "每日格言" to "倒计时" or "AI 智能体" tabs erroneously cleared the pinned quote widget from the desktop.
      - Desktop quotes that are pinned (`📌`) or set to "桌面悬浮显示" now seamlessly persist and maintain their configured window level across tab switches.
    - Standardized 34px Vertical Rhythm & Unified 28px Control Heights (统一 34px 行距节律与 28px 控件等高规范):
      - Standardized all input boxes (`TextBox`), dropdown boxes (`ComboBox`), and interactive buttons (`Button`) across the right column (`X = 172, Width = 188`) to a strictly consistent **28px height** (eliminating the previous 36px oversized multiline input box discrepancy).
      - Applied internal 4px padding with `EM_SETRECTNP` formatting rects to ensure single-line text in both `endMessage` and `hotkeyBox` is optically and vertically centered, perfectly harmonizing with all dropdown rows.
      - Aligned sound, notice, and startup rows in Tab 0 to a strict 34px vertical pitch (`Top = 334, 368, 402`), eliminating previous bottom crowding and achieving horizontal baseline parity with Tab 1.
    - 20px Tab Header Spacing & Balanced Whitespace (Tab 标签与设置内容 20px 舒适间距):
      - Expanded the vertical spacing between the Win11 Tab bar baseline (`Y = 42`) and the first row / hero container of all 3 tabs (`Top = 62`) to exactly **20px**, giving the settings content ample breathing room and a refined native desktop layout.
      - Synchronized all downstream row coordinates in Tab 0, Tab 1, and Tab 2, ensuring consistent margins above the footer divider line (`Y = 464`).
    - Dual Switches Row: Master switch `[√] 开启每日格言` on the left (`X = 18, Y = 172`) and `[√] 桌面悬浮显示` right-aligned (`Right = 362, Y = 172`) directly beneath the hero card.
    - Smart Window Layering & Desktop Background Placement (智能动静分层与桌面置底):
      - *Morning wakeup popup* (`!isPinned`): stays `TopMost = true` (`HWND_TOPMOST`) for 10s so open windows never occlude it.
      - *Pinned desktop widget* (`isPinned`): defaults to `HWND_BOTTOM` (`TopMost = false`, 贴在桌面壁纸背景层), acting as an ambient desk calendar that never obstructs code or active documents.
      - *Configurable Window Level (常驻层级)*: added dropdown in Settings (`桌面背景置底 (默认)` / `始终置顶显示`), saved to `QuotePinTopMost`.
      - *Automatic Layer Re-seating*: seamless return to `HWND_BOTTOM` after mouse drag release (`OnMouseUp`) or hover exit (`ExitHoverState`).
    - Multi-Monitor Position Memory & Safety Recovery: supports extended screens situated to the left or above the primary monitor (preserving valid negative coordinates `QuoteX`/`QuoteY` with `-99999` sentinel). Seamlessly verifies visibility across `Screen.AllScreens` and safely auto-recovers upon display disconnects.
    - Pinned Quote Desktop Persistence (钉住桌面格言持久化): quotes pinned (`📌`) on the desktop remain active and pinned after closing SettingsDialog, preserving workflow continuity.
    - Unified 2-Column Styling Grid mirroring Tab 0: strict 34px vertical pitch (`X = 18` labels, `X = 172` controls). Consistent "文字大小" (Text size) and "字体样式" (Font style) terminology. Real font face preview rendering via `OnDrawQuoteFontItem`.
    - Redundancy Elimination (去除冗余色块): Removed duplicate 52px color preview panel in favor of a clean standard 188px full-width color dropdown (`quoteColorCombo`) with embedded 10px circular color chips, achieving pixel-perfect column alignment.
    - Subtractive simplification (YAGNI): Cleanly removed `panelQuotePreview`, `btnPreviewQuote`, `btnTestAlert`, `btnResetQuotePos`, `quoteColorPreview`, and matrix buttons, purifying dialog interior and footer.
  - CLI integration: `-q` / `--quote` command to trigger the Daily Quote popup on demand.
  - **In-Card Native Badge Tooltip (卡片内原生自绘微型胶囊气泡提示)**:
    - Completely replaced legacy OS `ToolTip` window (which suffered from 300ms latency and 25px+ cursor-drop offset) with an in-card native acrylic badge pill rendered directly on the 32-bit ARGB layered window canvas (`UpdateLayeredWindow`).
    - Positioned inline horizontally to the left of the button cluster (`X = btnRefreshRect.Left - 6 - badgeW, Y = 6`), with `padTop` elevated to strictly separate the top action bar from quote text, ensuring 100% zero occlusion of quote text and zero screen-edge clipping.
    - 0ms zero-latency instant response on hover, clean 1px border highlight, soft drop-shadow, and instant state toggle (e.g. clicking pin immediately toggles between "钉在桌面" and "取消固定").
  - **Dual-Subsystem CLI Architecture (`iclock.com` + `iClock.exe`)**:
    - Replaced asynchronous console attachment hacks (`RestoreConsolePrompt()`) with an ultra-lightweight Windows console shim `iclock.com` (7.6KB).
    - In command lines (PowerShell / CMD), typing `iclock` prioritizes `.com` according to Windows `%PATHEXT%` precedence, synchronously awaiting execution and placing the shell prompt cleanly below all output.
    - Double-clicking or shortcut launch runs `iClock.exe` (`/target:winexe`), maintaining zero-console flash for pure GUI desktop workflows.
  - **CLI Lifecycle & Single-Run Automation (`--quit` & `--once`)**:
    - Added graceful CLI exit commands (`iclock --quit`, `iclock quit`, `iclock --exit`, `iclock exit`, `iclock -x`), allowing scripts and terminal workflows to safely terminate the running background instance with clean session persistence and hotkey unregistration.
    - Added single-run mode (`--once`): allows standalone one-shot timers (`iclock -s 25 --once`) or script notices (`iclock -n "Done" --once`) that cleanly exit after completion/dismissal without lingering in the system tray.
    - **Running Desktop Instance Protection (桌面常驻进程防退出保护)**: When iClock is already running as a resident desktop app, issuing commands with `--once` safely executes the countdown or notification on the desktop without terminating the resident program upon completion.
  - **Desktop Quote Font Size Synchronization & Tab 0 Color Immunity**:
    - Fixed an issue where changing countdown timer colors in Tab 0 clobbered and altered the desktop quote's font size back to stale settings.
  - **Interface Introductions Modernization & Alignment (界面工具介绍与指引现代化升级)**:
    - **AboutDialog Product Positioning Modernization (关于窗口产品定位与一句式概括)**:
      - Updated subtitle from the legacy narrow "桌面极简悬浮倒计时工具" to a holistic, feature-free single-sentence byline:
        - 中文：`极简通透、轻量无打扰的桌面专注与效率伴侣`
        - English: `Minimalist & distraction-free desktop focus companion`
      - Enabled `lblDesc.UseMnemonic = false` to eliminate accelerator key consumption of literal `&`, displaying crisply on a single line within 310px width.
      - Added author signature attribution at the bottom of the dialog: `作者：gujiangwen` (zh) / `Designed & Built by gujiangwen` (en).
    - **SettingsDialog Tab 0 Minimalist Layout (倒计时设置界面底部留白极简优化)**:
      - 依用户需求精简移除了倒计时设置面板底部的提示文字，保持界面开阔通透、纯粹简洁。
    - **SettingsDialog Tab 2 AI Agent Single-Line Desc (AI 智能体说明文案单行自适应升级)**:
      - 将「启用智能体后台任务完成提醒」下方的多行硬编码介绍精炼为一句概括性单行文案（`智能体后台任务完成时自动弹窗提醒，点击可切回工作窗口。`），去除 `\n` 硬回车并启用控件级自适应折行。
      - 释放垂直空间使智能体列表容器高度扩展 18px（`panelAgentBox` 增至 232px），提升列表展示容量与整体精致度。
    - **CliHelpDialog Command Quick Reference Expansion (命令行帮助弹窗 8 大高频命令补齐)**:
      - Expanded the CLI command reference from 5 commands to 8 comprehensive commands (`-s`, `-p/-r`, `--reset`, `--rest`, `-n`, `-q`, `--once`, `--quit`).
      - Dynamically bound command bounds (`Width = 142px` code font, `Width = 234px` description) and expanded `ClientSize` to `420 x 430` for comfortable, non-wrapping presentation.
- **Classic Telephone Mechanical Ring Alert**: Replaced alert sound 2 with an authentic mechanical two-bell telephone chime ("叮铃铃") generated via harmonic frequency synthesis (440Hz + 480Hz modulated at 20Hz ringer cadence).
- **Automated Test Suite Expansion**: Added test checks 34 to 40 in `tests/SelfCheck.cs`, verifying telephone tone synthesis, 3-tab settings navigation, daily quote live preview controls, color/font serialization, custom quote parsing, IPC triggers, quote font size wheel synchronization, Tab 0 timer color change immunity, CLI `--once` running instance immunity & quit lifecycle, in-card badge tooltips, centered sandwich layout positioning, and user-facing tool introductions modernization. All 40 tests pass with 100% success rate.

## [1.05] - 2026-10-15

### Added

- **Brand New Application Icon & Multi-Resolution Asset Set**: High-contrast deep navy squircle (`#131C43`) with transparent antialiased rounded corners, crisp white "i", and vibrant coral orange slash (`#FF5738`), calibrated for optimal contrast on both dark and light Windows themes. Preserved complete 9-resolution master assets in `src/icons/` (16×16 to 512×512).
- **CLI Automation & Win32 IPC Pipeline (`WM_COPYDATA`)**: Full terminal and script automation support. Invoke commands directly from PowerShell, CMD, or Bash (`iclock -s [mins]`, `-p`, `-r`, `-t`, `--reset`, `--rest`, `-n <message>`). Built-in `--install-cli` command automatically registers iClock into User PATH, enabling global invocation without `.exe` (matching tools like `claude` and `pi`). Zero-overhead message window dispatch (`MessageWindow`, caption `"iClock.IpcWindow"`) and dynamic console attachment (`AttachConsole(-1)`) allow GUI and console integration seamlessly.
- **Settings Dialog Live Preview & Test Alert Button**: Font, size, and color adjustments now preview directly on the active desktop overlay in real-time. Added a dedicated "Test Alert" (测试提醒演示) button to both Timer and Agent tabs to instantly audition alarm audio, popup notices, or breathing pulses without waiting for a countdown.
- **Dynamic Running Agent Priority**: Currently active and working AI agents are dynamically prioritized and sorted to the top of the Agent Collaboration list.
- **Automated Test Check 32**: Added reflection-based test coverage in `tests/SelfCheck.cs` verifying CLI commands (`--start 18`, `-p`, `-r`, `--reset`, `-n`), maintaining 100% test pass rate across all 32 checks.

### Changed

- **Extreme Binary Slimming to 169 KB**: Decoupled high-res design asset packs from runtime embedding, streamlined the application icon to an uncompressed 24×24 DIB sweet-spot, and downsampled the sponsor QR code to 450×450 high-fidelity bicubic JPEG. Dropped standalone executable size from 505 KB to an ultra-compact **169 KB** (172,544 raw bytes) and compressed ZIP to **80 KB**, saving ~66% total size with zero third-party dependencies.
- **Optical Vertical Centering for Pause Badge**: Calibrated typographic baseline offsets so the pause indicator (`❚❚`) and countdown digits share the exact same optical center line ($0.0\text{px}$ offset) across all font sizes.
- **GDI+ Icon Rendering Compatibility**: Resolved GDI+ `Icon.ToBitmap()` rainbow noise artifacts in the WelcomeDialog with compliant 32bpp DIB layers and procedural vector fallback.
- **Performance & Background Resource Optimization**: Dynamic throttling for WinRT Toast listener during idle periods, automatic detachment of system mouse hooks after tray hover inactivity, and proactive working-set compaction (`TrimMemory`) on dialog close.

## [1.04] - 2026-10-15

### Added

- **AI Agent Background Task Completion Alerts**: Comprehensive monitoring and collaboration for 20+ domestic & international AI coding agents (Cursor, Antigravity, Claude Code, GitHub Copilot, Trae, Tongyi Lingma, CodeGeeX, Baidu Comate, MiniMax, CodeBuddy, Aider, Cline, etc.).
- **4-Tier Perception Engine**:
  - Tier 1: Real-time Windows Toast notification listener (`UserNotificationListener`) with task prompt extraction and zero false positives.
  - Tier 2: Win32 Shell Hook (`RegisterShellHookWindow` + `HSHELL_FLASH`) capturing background IDE/terminal taskbar completion flashes at 0ms latency and 0.0% CPU overhead.
  - Tier 3: Terminal/IDE window title state badge dynamic machine.
  - Tier 4: Multi-process cluster CPU edge detector with enhanced guard thresholds (≥5s heavy burst, ≥6s quiet, 30s cooldown debounce).
- **One-Click Window Restore**: Directly restore and focus active AI agent windows from floating completion notifications.
- **Settings Dialog Dual Tabs (Plan B-1)**: Modern segmented dual tabs (⏱ Timer / 🤖 AI Agent Collaboration) with real-time agent scanning and process status badges.
- **Native 32-bit ARGB Layered Window Rendering**: Replaced colorkey rendering with native `UpdateLayeredWindow`, delivering pristine subpixel anti-aliasing and adaptive soft drop shadows.
- **Ergonomic Direct Alt+Drag Positioning**: Instant desktop repositioning by holding `Alt` and left-click dragging, with automatic coordinate persistence.
- **Tray Scroll Wheel Micro-Tuning**: Roll mouse wheel over the tray icon or floating clock to adjust countdown duration (`±1m` / `±5m`).
- **Tiered Alerts & Auto Fullscreen DND**: TopMost popup alerts vs gentle in-place breathing pulse, with automatic downgrade during full-screen games, PPT slideshows, or video playback.
- **Multi-Day Statistics & History Enhancements**: Added Days Summary view tracking streaks across 7/14/30 days with completion rates, evaluations, double-click drill-down, and `Ctrl+C` table export.

### Changed

- Binary size optimized to 222 KB with zero external dependencies.
- Centralized Win32 interop (`NativeMethods`) and guaranteed process handle scoping (`SafeProcessHandle`).
- Configuration saving upgraded to atomic file replacement (`File.Replace`).

## [1.03] - 2026-10-10

### Added

- Reliable default hotkey `Ctrl+Shift+T` replacing Space hotkey, with automatic migration for legacy configurations.
- Intelligent hotkey auto-fallback cascade and real-time conflict detection with support for standalone `F1`-`F12` function keys.
- High-DPI optimized tray icon geometry with enhanced visual weight across all display scaling levels.
- System tray icon left-click direct start / pause trigger.
- Enforced single active dialog mutual exclusion and active-screen centering for NoticeDialog, WelcomeDialog, and SettingsDialog.
- Today's countdown history table enhancements: sequence number column (`#`), actual duration tracking, auto-stretching full-width layout, and summary status bar.

### Changed

- Default countdown display format changed to `MM:SS`.
- Default countdown screen position updated to horizontal center and 60px from top.

## [1.02] - 2026-10-10

### Changed

- Standardized tray menu with 32px padding, centered text alignment, and dynamic 3-state ForeColor highlights for countdown status.
- Aligned menu item text with horizontal dividers and right-aligned shortcut indicators in distinct tertiary gray.
- Added 4px native internal padding to all dropdown options and input box text items in Settings dialog.
- Unified owner-draw item rendering across font style, time format, and language dropdowns.
- Cleaned up redundant dead classes, duplicate formatting methods, and streamlined menu delegates.

## [1.01] - 2026-10-09

### Changed

- Changed default countdown font size to 20 px for a cleaner desktop overlay.
- Changed default countdown text color to Red (`#FF0000`).

## [1.0.0] - 2026-10-09

### Added

- Transparent, click-through desktop countdown overlay.
- Duration, display format, font size, and color settings.
- Configurable global start/pause hotkey and tray controls.
- Optional startup at sign-in, end sound, and customizable end notification.
- Daily history for completed, reset, and interrupted sessions.
- Tray language picker for Simplified Chinese and English, defaulting to Simplified Chinese.
- Position adjustment mode with a 60%-transparent black background and centered text.
- Monotonic countdown timing with change-only text repainting to reduce timer drift and visible skipped seconds.
