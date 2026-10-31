# iClock User Guide / 使用指南

## Getting started / 开始使用

Run `iClock.exe`. The overlay is hidden before countdown starts. Press the global hotkey to start counting. The default is `Ctrl+Shift+T`. The same hotkey pauses and resumes the current session. When the countdown finishes, the text hides automatically and a top-most notification appears, requiring confirmation to dismiss.

运行 `iClock.exe` 后，倒计时开始前桌面不显示文字；按全局快捷键后才开始倒计时。默认快捷键为 `Ctrl+Shift+T`，同一快捷键也可暂停和继续。倒计时结束后文字自动隐藏，并弹出置顶消息提示，需要用户确认后才消失。

Right-click the iClock tray icon to open the menu. Groups are: start/pause, reset and position; today's history (with a live duration hint) and settings; language and the auto-start switch; About; Exit.

右键点击托盘中的 iClock 图标可打开菜单。分组为：开始/暂停、重置、调整位置；今日记录（右侧显示当天累计）与设置；语言与开机自启动开关；关于；退出。

## Settings / 设置面板

Settings uses an authentic Windows 11 File Explorer multi-tab navigation / 设置面板采用原汁原味的 Windows 11 资源管理器风格多标签页：

### ⏱ 倒计时 / Timer Tab
- **Duration / 倒计时时长:** 1 to 1,440 minutes. Changing settings while a session is paused keeps the paused session's remaining time; reset to use the newly configured duration.
- **Text size / 文字大小:** 12 to 120 pixels (default: 20 pixels / 默认 20 像素).
- **Font style / 字体样式:** Segoe UI, Consolas, Arial, Impact, Microsoft YaHei.
- **Text color / 文字颜色:** select a color using the system color picker (default: Red `#FF0000` / 默认红色).
- **Display format / 显示格式:** total minutes and seconds (`MM:SS`, default / 默认), hours/minutes/seconds (`HH:MM:SS`), or unit labels (`Chinese units`).
- **End message / 结束提示词:** optional text shown in the prompt when the countdown finishes.
- **Start/pause hotkey / 快捷键:** click the hotkey field and press a key combination containing Ctrl, Alt, or Shift. Default is `Ctrl + Shift + T`.
- **End sound & notification / 声音与通知:** independently control the prompt sound style (**QQ Message / QQ 经典提示 (嘀嘀)**, **Classic Telephone Ring / 经典电话铃 (叮铃铃)**, **Alarm Ring / 醒目闹铃**, all >= 3s duration) and notification mode (**Popup / Auto DND Fullscreen** vs **In-Place Pulse**).
- **Daily focus goal / 每日专注目标:** Off, or 1 / 2 / 3 / 4 / 6 / 8 hours. When on, a flat progress line appears under the overlay digits and the tray 「今日记录」 row shows today's total next to the goal. / 关闭，或 1 / 2 / 3 / 4 / 6 / 8 小时。开启后悬浮数字下方出现平直进度线，托盘「今日记录」右侧显示当天累计与目标。
- **Eye-care reminder / 护眼提醒:** one dropdown that is both the switch and the interval, the same shape as 「结束声音」. While on, iClock samples the system's last-input time every 30 seconds; 3 minutes with no input counts as a real break and resets the accumulated screen time. It does not accumulate during a rest countdown, and it stays quiet while a full-screen app or this Settings window is open, reminding you once you are back. Hover the ⓘ beside it for the details. / 一个下拉同时管开关与间隔，与「结束声音」用法一致。开启后每 30 秒采样一次系统最后输入时间，连续 3 分钟无输入视为一次真正的休息并清零。休息倒计时期间不计时；全屏游戏或本设置窗口打开时先不弹卡，回到普通窗口再提醒。旁边的 ⓘ 悬停可看说明。
- **Interface language / 界面语言:** on the tray menu, choose **简体中文** or **English**. The menu stays open after a switch. / 在托盘菜单中选择，切换后菜单保持打开。
- **Start with Windows / 开机自启动:** a capsule switch on the tray menu. Enabling it writes the 64-bit HKCU Run key so Explorer launches iClock at logon. / 托盘菜单上的胶囊开关。打开后写入 64 位 HKCU Run，登录时由资源管理器拉起。

### 💬 每日金句 / Daily Quotes Tab
- **Hero Banner & Dual Switches / 每日金句展台与顶栏双开关:**
  - **Today's Quote Card / 每日金句展台 (Top = 62..160):** Positioned at the very top directly below the tab strip, prominently presenting the active quote and author in an elegant 98px frosted container with optically balanced vertical centering, plus a flat borderless vector `↻ 换一句 (Next quote)` button in the bottom-left corner (`Left = 12`).
  - **Master Enable Switch / 开启每日金句 (Left = 18, Y = 172):** Master switch controlling whether daily quotes are enabled upon startup/unlock.
  - **Desktop Display Toggle / 桌面悬浮显示 (Right-Aligned, Right = 362, Y = 172):** Toggle to immediately project and preview the floating quote on your desktop wallpaper. Unchecking `开启每日金句` automatically turns off desktop floating display.
  - **Pinned Quote Persistence / 钉住桌面金句持久化:** Pinned quotes (`📌 钉在桌面`) remain active on the desktop even after closing SettingsDialog.
- **Unified 2-Column Styling Grid / 统一双列栅格体系:** Matches the exact design language and 34px vertical pitch of Tab 0 (`⏱ 倒计时`):
  - **Duration / 显示时长（秒）:** Adjust floating duration (`3` to `60` seconds, default: 10s).
  - **Text size / 文字大小:** Dedicated spinner (`8.0pt` ~ `24.0pt`, step 0.5pt). Adjustments made on desktop via `Ctrl + MouseWheel` synchronize back to this control in real time.
  - **Font style / 字体样式:** Real font preview dropdown with WYSIWYG font face rendering (`跟随倒计时 (默认)`, `微软雅黑`, `楷体`, `宋体`, `Segoe UI`).
  - **Text color / 文字颜色:** Standard 188px full-width color dropdown with 10px circular color chips + hex labels (no redundant right-side swatch):
    - `跟随倒计时 (Follow Timer)`: Automatically harmonizes with countdown timer color.
    - 8 Curated Desktop Presets: `柔白` (#FFFFFF), `暖金` (#FFD166), `薄荷` (#06D6A0), `淡青` (#4CC9F0), `樱粉` (#F72585), `竹青` (#52B788), `暮紫` (#7209B7), `暖橙` (#F77F00).
    - `自选… (Custom…)`: Select Custom to open standard RGB palette.
  - **Window level / 常驻层级:** Select persistent window layer for pinned desktop quotes:
    - `桌面背景置底 (默认) / Desktop Layer (Default)`: Sits at `HWND_BOTTOM` directly above the wallpaper, serving as an ambient desk calendar that never covers windows or documents.
    - `始终置顶显示 / Always on Top`: Sets `HWND_TOPMOST` to float above active application windows.
    - *Smart Layering*: Morning 10-second wakeup popups always remain TopMost so open windows on boot do not hide them.
  - **Quote library / 我的金句:** `自定义… (Edit Quotes)` button opens local `quotes.txt` in Notepad.
- **Multi-Monitor Position Memory / 多显示器扩展屏位置记忆:**
  - Full support for multi-monitor setups including secondary screens placed to the left or above primary display (negative coordinates `QuoteX`/`QuoteY`). Automatically verifies visibility across `Screen.AllScreens` and recovers safely if a monitor is disconnected.

### 🤖 智能体协作 / AI Agent Collaboration Tab (Plan B-1)
- **Agent Status Sensing Toggle / 智能体状态自动感知:** pins what a background agent is doing to the desktop, so you can read it at a glance without switching to the agent's window.
- **Deep Collaboration Toggle / 智能体运行深度协作:** accepts the agent's decision requests (`--ask`) and completion alerts (`--notice`) while it runs.
- **Authentic Agent Stream / 100% 真实的智能体状态:** iClock reads the agent's **own session log** and reconstructs what it is doing, what it is thinking, and which command it ran — no synthesized filler text. The parser speaks each agent's own format:
  - *Claude Code* — Anthropic `content` blocks and `tool_use` records.
  - *OpenAI Codex* — `function_call` and `item_completed` / `event_msg` records.
  - *Antigravity* — planner responses, tool calls, and done records.
  - *Cursor*, plus a *generic* fallback for everything else.
  - Every command inside one record is shown, in order — not just the last one.
- **Unified Multi-Agent Status Bar / 多智能体统一状态条:** when several agents run at once, one bar at the top of the desktop holds them all:
  - Pills sort by priority (waiting / error > running > done) and rotate on a fixed 5-second cadence.
  - Multiple sessions of the same agent get stable `#1` / `#2` labels.
  - The bar's height is draggable, and the pin icon keeps it resident on the desktop.
  - The bar sits top-centered by default, fades out on foreground change after a 5-second completion debounce, and draws a vertical dashed snap guide when dragged near the screen centre.
- **Process-Guided Lazy Watching / 进程引导的懒监听:** a session directory is watched only while the matching agent process is running, so idle agents cost nothing.
- **Detected Agents Only / 仅显示已检测到的智能体:** cleanly displays detected agents installed or running on your system with real-time status indicators (`🟢 [运行中 / Running]` or `⚪ [已就绪 / Ready]`).
- **Real-Time Re-Scan / 🔄 重新检测:** one-click button to refresh processes and system directories immediately without restarting iClock.
- **One-Click Window Restore / 一键恢复窗口:** clicking **「查看 / View」** on the completion notification immediately restores and focuses the corresponding AI agent window.

## Position, Drag & Ergonomics / 位置、随心拖拽与微交互

- **Direct Alt+Drag / 按住 Alt 直接拖拽**: Hold `Alt` and drag the countdown text or quote directly with the left mouse button to reposition it anywhere on your screen. Release to automatically save coordinates. / 在屏幕上按住 `Alt` 键，鼠标左键直接拖拽倒计时或金句文字即可瞬时定位，松开自动保存，完全无需进菜单。
- **Tray Scroll Wheel / 托盘滚轮微调时长**: Hover your mouse over the system tray icon and roll the mouse wheel to adjust duration by `±1 min` (or `±5 min` with `Shift`). You can also hold `Alt` and scroll the wheel directly over the floating clock. / 鼠标悬停在系统托盘图标上滚动滚轮，直接以 `±1分钟`（按住 `Shift` 为 `±5分钟`）快速微调倒计时时长；按住 `Alt` 在悬浮文字上滚动滚轮亦可同步调整。
- **Menu Move Mode / 菜单调整模式**: You can also choose **Adjust position** in the tray menu to display the positioning frame for both countdown and quote. / 亦可从托盘菜单点击“调整位置”同时对倒计时或金句进行拖动定位。
- **True 32-bit ARGB & Adaptive Shadow / 真抗锯齿与自适应投影**: Powered by native `UpdateLayeredWindow`, text renders with smooth anti-aliased subpixel precision and adaptive drop shadows, crystal-clear across bright wallpapers, dark IDEs, and full-screen games. / 采用 Windows 原生 `UpdateLayeredWindow` 32 位 ARGB 渲染，消除传统色键点阵毛刺，配合平滑自适应边缘微投影，在浅色文档、深黑终端或高对比游戏画面下均清晰可读。

## Daily Quotes / 每日金句

iClock features an ultra-minimalist Daily Quotes popup designed to inspire your day upon first startup or session unlock:
- **Automatic Daily Appearance / 每日唤醒**: Pops up once per day on program startup or morning unlock, smoothly floating horizontally centered at `Y = 120px` (comfortably below countdown text `Y = 60..96px` and above the centered settings panel `Y = 242px+`). / 每天首次启动或早晨解锁后自动弹出一次，平滑浮动于屏幕水平居中 Y = 120px 处，位于默认倒计时文字下方并处于居中设置面板上方，三者中轴对齐、匀称美观且互不重叠。
- **Transparent Ambient & Click-Through / 透明背景与鼠标穿透**: By default, the quote renders cleanly with a pure transparent background and soft text drop shadow, free of borders, card boxes, or decorative labels. Native `WM_NCHITTEST` (`HTTRANSPARENT`) ensures all mouse clicks pass 100% through to windows below without interfering with your work. / 默认完全透明背景，文字带有自适应柔和投影，无边框与多余标签干扰；原生鼠标点击穿透，完全不阻碍底层窗口与游戏的正常操作。
- **Consistent Font & Color / 字体与颜色同倒计时同步**: Quote text automatically adopts the exact same font family (`settings.FontFamily`) and vibrant color (`settings.Color`) configured for your countdown timer. Text renders with adaptive dual-layer high-contrast drop-shadows, ensuring crisp readability on any desktop background. / 自动继承倒计时设置的字体与主题颜色，名言正文与倒计时颜色完全一致，作者名字以同色系柔和呼应，配合自适应高对比投影，在任何深浅背景下均清晰通透。
- **Unified Alt + Drag & Hover Card / Alt + 拖拽与微交互卡片**: Hold `Alt` and hover over the quote to smoothly wake up the Win11 acrylic card frame and action buttons (Next quote, Pin, Close). While holding `Alt`, drag anywhere on the quote to move its position, or drag the right edge to adjust width. Releasing `Alt` seamlessly restores clean click-through ambient text. / 按住 `Alt` 悬停于金句时唤醒微交互卡片与操作按钮（换一句、固定、关闭）；按住 `Alt` 直接拖拽卡片任意空白区域即可调整位置，拖动右侧抓手可调节宽度；松开 `Alt` 立即恢复为纯净无打扰的点击穿透文字。
- **Zero-Resource Hibernation / 极致低能耗与零轮询休眠**: Position adjustment is completely event-driven via Win32 `WM_NCHITTEST` without any 200ms cursor polling timers. When pinned to desktop, all timers are completely stopped (0 timer wakeups, 0% CPU, 0 memory allocations). / 交互与位置调整基于原生 `WM_NCHITTEST` 消息驱动，彻底移除了原本每 200ms 的鼠标坐标轮询定时器；桌面常驻固定时定时器彻底停止（0 唤醒、0% CPU 占用、0 额外内存分配）。
- **Intuitive Gestures & Shortcuts / 极简手势与快捷操作**:
  - `Alt + Drag / 拖拽定位`: Hold `Alt` and drag anywhere on the card to move it; new coordinates are automatically saved across sessions. / 按住 `Alt` 键在卡片任意区域拖拽即可移动位置，松开自动保存。
  - `Alt + Right-Edge Drag / 拖拽拉宽`: Hold `Alt` and drag the right edge to widen display width. / 按住 `Alt` 键拖拽卡片右侧可拉宽或收窄卡片。
  - `Alt + Double Click / 双击换一句`: Hold `Alt` and double-click the card to cycle to the next quote instantly (`换一句`). / 按住 `Alt` 双击卡片空白区域即刻切换下一句名言。
  - `Alt + Ctrl + Scroll / 字体缩放`: Hold `Alt + Ctrl` and scroll the mouse wheel to dynamically scale quote font size (`8pt` - `24pt`); new font size is automatically saved across sessions (`QuoteFontSize`). / 按住 `Alt + Ctrl` 滚轮动态调节字号（8pt ~ 24pt），松开自动永久记忆保存。
  - `Esc / 键盘退出`: Press `Esc` to dismiss and fade out immediately. / 按 `Esc` 键即刻渐隐关闭。
  - `Micro Buttons / 微按钮`: `↻ 换一句 / Next Quote`、`📌 固定 / Pin (永久钉在桌面)`、`✕ 关闭 / Close`。支持顶栏左侧平齐内联原生微胶囊提示（0ms 响应、与图标水平平齐并停靠于图标左侧，彻底消除对正文的遮挡并规避屏幕顶边截断）。
- **Custom Quotes File / 自定义名言库**: Automatically loads `quotes.txt` from the application directory or `%APPDATA%\iClock\quotes.txt`. Supports both UTF-8 and ANSI/GBK encodings. Format: `Quote Text —— Author` (or simple text lines). Supports arbitrary leading sequence numbers in text (e.g. `1. `, `02、`, `[3]`, `(4)`, `5) `, `6: `, `No. 7 `, `#8 `, `①`, `1．`), which are automatically hidden when displayed on the desktop HUD and preview card. If no custom file is found, iClock provides 10 built-in classic quotes. / 自动加载程序同目录或 `%APPDATA%\iClock\quotes.txt`。自适应 UTF-8 及 ANSI/GBK 编码。格式为 `名言正文 —— 作者`（作者可省略）。支持在名言文本前添加任意格式的序号（如 `1. `、`02、`、`[3]`、`(4)`、`5) `、`6: `、`No.7 `、`#8 `、`①`、`1．` 等），在桌面卡片及设置预览中均会自动隐去序号展示。若无自定义名言文件，默认提供 10 条经典中英名言。
- **AI Agent Quote Expansion / AI 智能体命令行批量扩充**: In Settings Tab 1, click **「✨ AI 扩充」** beside **「自定义…」** to prepare a batch of curated quotes (**5–100**, default 30). The dialog builds one instruction and copies it to the clipboard (`复制指令`); you paste it into whatever agent you are using, and the agent imports the quotes through the iClock command line (`--quote-add`). Imported quotes are deduplicated and backed up.
  - **One Instruction, Any Agent / 一段指令，任意智能体**: iClock does not run the agent and does not list agents — the import happens over its own command line, so the dialog only hands over the instruction and waits (`• 正在等待导入...`, with `重新复制` / `返回修改`).
  - **Live In-Place Completion Report**: once the quotes arrive through the command line, the dialog immediately presents a celebratory success card (`✨ 金句扩充完成！`), reports the new quotes, previews the first one, and synchronizes to desktop display with automatic deduplication and `.bak` backup. / 在设置面板”每日金句”中，点击”自定义…”并排的”✨ AI 扩充”按钮，即可弹出 380 × 280px 极简扩充面板：填写偏好描述或关键词、选择扩充数量（**5–100 条**，默认 30 条），点击 `复制指令` 把扩充指令复制到剪贴板，粘贴到任意智能体对话框（Ctrl+V）发送即可——智能体会执行 iClock 命令行 `--quote-add` 自动入库，iClock 不代跑智能体、也不列出智能体列表；
  - **统一当场汇报**：一旦新金句入库成功，弹窗当场切换为”✨ 金句扩充完成！”成果汇报卡片，展示收录成果与最新金句预览，彻底告别”点了没反应/不知道成败”的体验盲区！
- **Settings & Audition / 设置与演示**: Manage independently via Settings Tab 1, select from 10 curated color presets, customize font style with live acrylic card preview, click **自定义…** or **✨ AI 扩充** to manage library, and audition the floating quote on your desktop anytime.

## History & Daily Statistics / 专注历史记录与统计

Choose **Today's history** from the tray menu to open the comprehensive history window. The right-hand hint shows today's accumulated focus time (and `/ goal` when a daily goal is set).
- **Daily Details / 单日明细**: Inspect granular session logs for any day using the date switcher (`◀`, dropdown, `▶`). Each record logs start/end times, configured duration, actual focused duration, and result (completed, reset, interrupted, or **in progress**). A started but unfinished countdown appears as 「进行中」 and counts toward today's total. The summary footer dynamically displays total focus time, completed cycles, and completion rate.
- **Days Summary / 按天汇总**: Switch tabs to inspect daily aggregated focus metrics over selected ranges (Last 7 Days, 14 Days, 30 Days, or All History). Shows total focus time, cycle counts, completion rates, and achievement evaluations (● Excellent, ● Good, ● Fair, ● Needs work).
- **Drill-Down / 穿透钻取**: Double-click any day in the Days Summary table to immediately jump to that day's granular breakdown in Daily Details.
- **Keyboard Shortcuts / 快捷交互**: Press `Left`/`Right` arrow keys to step through dates, `Ctrl+Tab` to switch views, and `Ctrl+C` to copy selected rows to clipboard.
- **Time tracking / 时间跟踪统计**: always on. iClock records foreground process names (never window titles) from unlock until lock or sleep. Daily Details shows a color axis; clicking that axis toggles between the **activity window** and the **full day**, and the window starts at the day's first real record. Idle after 3 minutes is counted as idle, not as the last app. The tray menu draws a color strip under Today's history.
- **Trends / 趋势**: a third view charting **screen-active vs. focus time** per day, with an application ranking for the selected window (This week / Last 30 days).

从托盘菜单选择「今日记录」，即可打开专注历史记录与统计窗口。菜单右侧提示当天累计时长（设了每日目标时带上 `/ 目标`）。
- **单日明细**：通过顶部的日期切换器（`◀`、下拉菜单、`▶`）查看任意日期的具体倒计时明细，包含开始与结束时间、设定时长、实际专注时长及结果（已完成、已重置、已中断、**进行中**）。已经开始、尚未结束的倒计时会作为「进行中」出现，并计入当天累计。底部汇总栏动态呈现当日累计专注时长、完成周期数与达成率。
- **按天汇总**：切换至该标签页可宏观查看按天聚合的专注指标，支持切换统计范围（最近 7 天、14 天、30 天、全部历史）。直观呈现每日专注总时长、完成/总数、完成率以及达成状态（● 卓越、● 良好、● 及格、● 待提升）。
- **穿透钻取**：在按天汇总表中双击任意一行日期，即可无缝穿透跳转至该日的“单日明细”查看具体会话。
- **快捷交互**：支持通过键盘 `←` / `→` 键前后切换日期，`Ctrl+Tab` 快速切换视图，选中文本支持 `Ctrl+C` 复制到剪贴板。
- **时间跟踪统计**：始终开启。iClock 只记前台进程名（不记窗口标题）。单日明细顶部出现色轴，点击色带即可在「活动窗口」与「全天」之间切换，窗口起点对齐当天第一条真实记录。托盘「今日记录」下方出现通栏色带。空闲超过 3 分钟记为空闲，不算给最后一个应用。
- **趋势**：第三个视图，按天对比「屏幕活跃 vs 专注」双序列曲线，并给出所选窗口内的应用排行（本周 / 最近 30 天）。

## Command Line & Script Pipeline (IPC) / 命令行与脚本联动

iClock includes a native zero-latency Win32 IPC messaging pipeline (`WM_COPYDATA`). When iClock is running in the background, secondary CLI executions transmit commands directly to the active process without spawning duplicate instances.

iClock 内置原生的 Win32 IPC 消息管道（`WM_COPYDATA`）。当 iClock 在后台常驻运行时，终端再次执行命令行会直接将指令秒级传递给后台主进程，零内存开销、0ms 响应，绝不重复拉起进程。

### Global PATH Setup / 全局免 .exe 终端配置

Windows natively supports command execution without `.exe` via `%PATHEXT%`. To call `iclock` from anywhere (like `claude` or `pi`), run once:

Windows 系统原生支持无需输入 `.exe` 后缀。首次使用时，只需在终端运行一次内置的 `--install-cli` 指令：

```powershell
.\dist\iClock --install-cli
```

This appends iClock's directory to your User `PATH` environment variable. Open any new terminal window and type `iclock` directly.

程序会自动将当前程序目录写入用户的 `PATH` 环境变量。之后新开任意终端窗口，即可直接输入 `iclock`。

### Command Options / 参数速查

| Option / 参数 | Shorthand / 简写 | Description / 说明 | Example / 示例 |
| :--- | :--- | :--- | :--- |
| `iclock` | - | Launch GUI or bring running instance to front / 启动主程序 | `iclock` |
| `--start [mins]` | `-s [m]` | Start work countdown / 启动自定义分钟工作倒计时 | `iclock -s 25` |
| `--pause` | `-p` | Pause active countdown / 暂停倒计时 | `iclock -p` |
| `--resume` | `-r` | Resume paused countdown / 继续倒计时 | `iclock -r` |
| `--toggle` | `-t` | Toggle Pause/Resume / 切换暂停与继续 | `iclock -t` |
| `--reset` | - | Reset timer to idle & hide overlay / 重置并隐藏倒计时 | `iclock --reset` |
| `--rest [mins]` | - | Start rest break timer (default 5m) / 开启休息倒计时 | `iclock --rest 10` |
| `-n <msg>` | `--notice` | Trigger TopMost alert & chime / 触发置顶弹窗与提示音 | `iclock -n "Task Done"` |
| `--ask <question>` | `-a` | Request a desktop decision / 呼出桌面决策请示 | `iclock --ask "Continue?" --options "Yes,No" --json` |
| `--progress <value>` | `-pr` | Project task status; use `clear` to remove / 投射任务进度，`clear` 清除 | `iclock --progress 3/5 "Testing" --agent-name "Codex" --json` |
| `--status <text\|clear>` | `-st` | Update the AI status bar text; accepts `--detail`, `--progress`, `--thinking`, `--done`, `--waiting`, `--error`, `--agent-name`, `--project` / 更新 AI 状态条文字 | `iclock --status "Refactoring" --thinking --agent-name "Codex" --json` |
| `--json` | - | Return structured collaboration response / 返回结构化协作结果 | `iclock --notice "Done" --json` |
| `--quote-add <file>` | - | Import quote lines from a UTF-8 file / 从 UTF-8 文件导入金句 | `iclock --quote-add "quotes.txt" --json` |
| `-q` | `--quote` | Show Daily Quote popup / 弹出每日金句浮窗 | `iclock -q` |
| `--once` | - | Single-run mode: exit automatically upon completion (ignored if desktop instance is running) / 单次运行模式：任务结束后自动完全退出（若桌面端已运行则不退出桌面端） | `iclock -s 25 --once` |
| `--quit` / `--exit` | `quit` | Gracefully terminate running iClock instance / 干净安全地退出正在运行的 iClock 实例 | `iclock --quit` |
| `--install-cli` | - | Configure User PATH / 自动配置全局 PATH | `iclock --install-cli` |
| `--help` | `-h` | Display help in terminal / 控制台打印帮助 | `iclock -h` |

### Script Integration Examples / 脚本联动示例

```bash
# 1. Alert when a long compilation or build finishes / 构建完成后提醒
npm run build && iclock -n "前端构建成功！"

# 2. Alert after automated test execution / 测试套件跑完提醒
pytest && iclock -n "All tests passed!"

# 3. Start a focus session from terminal / 终端快速开启专注计时
iclock -s 45
```

## AI Agent CLI Collaboration / 智能体命令行协同

AI agents can call iClock's local CLI to request a decision, show task progress, and notify on completion. Run `iclock --install-cli` once to add the executable directory to User PATH, then give the agent the instructions from **Settings → AI Collaboration**. Add `--json` for structured machine-readable results.

智能体通过本地 CLI 调用 iClock 的桌面协作能力：`--ask` 请求人工决策，`--progress` 投射任务状态，`--notice` 发送完成提醒；追加 `--json` 可获取结构化 JSON 返回。先运行 `iclock --install-cli` 配置 PATH，再将「设置 → AI 协作」中的命令行提示提供给智能体。

```powershell
iclock --ask "是否继续部署？" --options "继续,取消" --json
iclock --progress 3/5 "正在运行测试" --agent-name "Codex" --json
iclock --progress clear --agent-name "Codex" --json
iclock --notice "任务已完成" --agent-name "Codex" --json
iclock --quote-add "quotes.txt" --json
```

旧版智能体 MCP 配置不会由 iClock 自动修改；请手动删除智能体设置中的旧 `iclock` 服务。

### 🛡️ Agent Runtime Harness 伴生实践 (DeepSeek / Pi / Python Agent)

将 iClock 用作智能体执行框架的伴生外骨骼（Ambient Harness）：

```python
import subprocess

class iClockHarness:
    @staticmethod
    def set_progress(pct: int, task: str = ""):
        subprocess.run(["iclock", "-pr", str(pct), task], check=False)

    @staticmethod
    def request_approval(action: str, options: str = "批准,取消") -> str:
        res = subprocess.run(
            ["iclock", "-a", action, "--options", options],
            capture_output=True, text=True, check=False
        )
        return res.stdout.strip()

    @staticmethod
    def notify_done(summary: str):
        subprocess.run(["iclock", "-n", summary], check=False)
```
- **DeepSeek `<think>` 阶段**：调用 `set_progress(20, "DeepSeek 推理分析中...")`，屏幕边缘直观感知，无需死盯终端；
- **安全审计门禁**：调用 `request_approval("是否推送代码到主分支？")`，桌面置顶展示且不抢焦点，支持按键秒级放行；
- **任务交付提醒**：调用 `notify_done("自动化构建完成")`，音画同步唤回人类注意力。

---

Settings are stored in `%APPDATA%\iClock\settings.ini`. History files are stored as UTF-8 TSV files named `history-YYYYMMDD.tsv` under the same directory, using the session's start date.

