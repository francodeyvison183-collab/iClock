# iClock User Guide / 使用指南

## Getting started / 开始使用

Run `iClock.exe`. The overlay is hidden before countdown starts. Press the global hotkey to start counting. The default is `Ctrl+Shift+T`. The same hotkey pauses and resumes the current session. When the countdown finishes, the text hides automatically and a top-most notification appears, requiring confirmation to dismiss.

运行 `iClock.exe` 后，倒计时开始前桌面不显示文字；按全局快捷键后才开始倒计时。默认快捷键为 `Ctrl+Shift+T`，同一快捷键也可暂停和继续。倒计时结束后文字自动隐藏，并弹出置顶消息提示，需要用户确认后才消失。

Right-click the iClock tray icon to open the menu. The menu allows you to start/pause, reset the countdown, adjust position, view today's history, open settings, view about & updates, or exit.

右键点击托盘中的 iClock 图标可打开菜单，用于开始/暂停、重置倒计时、调整位置、查看今日记录、打开设置、关于与检查更新或退出。

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
- **Interface language / 界面语言:** choose **简体中文** or **English**.
- **End sound & notification / 声音与通知:** independently control the prompt sound style (**QQ Message / QQ 经典提示 (嘀嘀)**, **Classic Telephone Ring / 经典电话铃 (叮铃铃)**, **Alarm Ring / 醒目闹铃**, all >= 3s duration) and notification mode (**Popup / Auto DND Fullscreen** vs **In-Place Pulse**).
- **Start with Windows / 开机自启动:** located at the bottom of the countdown settings tab, controls automatic start on user logon.

### 💬 每日格言 / Daily Quotes Tab
- **Hero Banner & Dual Switches / 每日格言展台与顶栏双开关:**
  - **Today's Quote Card / 每日格言展台 (Top = 62..160):** Positioned at the very top directly below the tab strip, prominently presenting the active quote and author in an elegant 98px frosted container with optically balanced vertical centering, plus a flat borderless vector `↻ 换一句 (Next quote)` button in the bottom-left corner (`Left = 12`).
  - **Master Enable Switch / 开启每日格言 (Left = 18, Y = 172):** Master switch controlling whether daily quotes are enabled upon startup/unlock.
  - **Desktop Display Toggle / 桌面悬浮显示 (Right-Aligned, Right = 362, Y = 172):** Toggle to immediately project and preview the floating quote on your desktop wallpaper. Unchecking `开启每日格言` automatically turns off desktop floating display.
  - **Pinned Quote Persistence / 钉住桌面格言持久化:** Pinned quotes (`📌 钉在桌面`) remain active on the desktop even after closing SettingsDialog.
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
  - **Quote library / 名言词库:** `自定义… (Edit Quotes)` button opens local `quotes.txt` in Notepad.
- **Multi-Monitor Position Memory / 多显示器扩展屏位置记忆:**
  - Full support for multi-monitor setups including secondary screens placed to the left or above primary display (negative coordinates `QuoteX`/`QuoteY`). Automatically verifies visibility across `Screen.AllScreens` and recovers safely if a monitor is disconnected.

### 🤖 智能体协作 / AI Agent Collaboration Tab (Plan B-1)
- **Master Alert Toggle / 启用智能体后台任务完成提醒:** automatically notifies you with a floating prompt when AI coding agents finish long tasks in the background or minimized.
- **Domestic & International Agent Matrix / 全面覆盖国内外主流智能体:** Automatically detects and tracks 20+ top AI coding assistants and CLI agents:
  - *International*: Cursor, Antigravity, Claude Code, GitHub Copilot, Codex, Gemini, Windsurf/Codeium, OpenCode, Aider, Cline, Roo Code, Continue, Tabnine, Cody, Amazon Q, Devin, Grok, Pi.
  - *Domestic (国内主流)*: 字节跳动 Trae, 阿里通义灵码 (Lingma), 智谱 CodeGeeX / Z Code, 百度文心快码 (Comate), MiniMax (Code), 腾讯云 CodeBuddy, 华为云 CodeArts Snap, 非十科技 Fitten Code, 代码小浣熊 (Raccoon), WorkBuddy, Kimi Code, SheetAgent.
- **4-Tier Perception Engine / 四级全向感知矩阵:**
  1. 🥇 **Tier 1 (WinRT Toast Notification Listener)**: Real-time listener for system toast alerts from Antigravity, Cursor, etc. Triggers with 0ms latency, extracts the actual task summary, and 100% eliminates false positives.
  2. 🥈 **Tier 2 (Win32 Shell Hook Taskbar Flash `HSHELL_FLASH`)**: Intercepts native taskbar flashing (`FlashWindowEx`) with 0ms latency and 0.0% CPU overhead, instantly capturing background completion signals.
  3. 🥉 **Tier 3 (Terminal & Window Title State)**: Dynamically inspects status badges (`[Running]`, `Thinking...`, `●`) in terminal/IDE window titles, firing immediately when status reverts to idle.
  4. 🛡️ **Tier 4 (High-Threshold Cluster CPU Fallback)**: Multi-process aggregated CPU edge detector with enhanced guard thresholds ($\ge 5$s sustained busy, $\ge 6$s continuous quiet, 30s cooldown debounce) to prevent false triggers during multi-step tool pauses.
- **Detected Agents Only / 仅显示已检测到的智能体:** cleanly displays detected agents installed or running on your system with real-time status indicators (`🟢 [运行中 / Running]` or `⚪ [已就绪 / Ready]`).
- **Real-Time Re-Scan / 🔄 重新检测:** one-click button to refresh processes and system directories immediately without restarting iClock.
- **One-Click Window Restore / 一键恢复窗口:** clicking **「查看 / View」** on the completion notification immediately restores and focuses the corresponding AI agent window.

## Position, Drag & Ergonomics / 位置、随心拖拽与微交互

- **Direct Alt+Drag / 按住 Alt 直接拖拽**: Hold `Alt` and drag the countdown text or quote directly with the left mouse button to reposition it anywhere on your screen. Release to automatically save coordinates. / 在屏幕上按住 `Alt` 键，鼠标左键直接拖拽倒计时或格言文字即可瞬时定位，松开自动保存，完全无需进菜单。
- **Tray Scroll Wheel / 托盘滚轮微调时长**: Hover your mouse over the system tray icon and roll the mouse wheel to adjust duration by `±1 min` (or `±5 min` with `Shift`). You can also hold `Alt` and scroll the wheel directly over the floating clock. / 鼠标悬停在系统托盘图标上滚动滚轮，直接以 `±1分钟`（按住 `Shift` 为 `±5分钟`）快速微调倒计时时长；按住 `Alt` 在悬浮文字上滚动滚轮亦可同步调整。
- **Menu Move Mode / 菜单调整模式**: You can also choose **Adjust position** in the tray menu to display the positioning frame for both countdown and quote. / 亦可从托盘菜单点击“调整位置”同时对倒计时或格言进行拖动定位。
- **True 32-bit ARGB & Adaptive Shadow / 真抗锯齿与自适应投影**: Powered by native `UpdateLayeredWindow`, text renders with smooth anti-aliased subpixel precision and adaptive drop shadows, crystal-clear across bright wallpapers, dark IDEs, and full-screen games. / 采用 Windows 原生 `UpdateLayeredWindow` 32 位 ARGB 渲染，消除传统色键点阵毛刺，配合平滑自适应边缘微投影，在浅色文档、深黑终端或高对比游戏画面下均清晰可读。

## Daily Quotes / 每日格言

iClock features an ultra-minimalist Daily Quotes popup designed to inspire your day upon first startup or session unlock:
- **Automatic Daily Appearance / 每日唤醒**: Pops up once per day on program startup or morning unlock, smoothly floating horizontally centered at `Y = 120px` (comfortably below countdown text `Y = 60..96px` and above the centered settings panel `Y = 242px+`). / 每天首次启动或早晨解锁后自动弹出一次，平滑浮动于屏幕水平居中 Y = 120px 处，位于默认倒计时文字下方并处于居中设置面板上方，三者中轴对齐、匀称美观且互不重叠。
- **Transparent Ambient & Click-Through / 透明背景与鼠标穿透**: By default, the quote renders cleanly with a pure transparent background and soft text drop shadow, free of borders, card boxes, or decorative labels. Native `WM_NCHITTEST` (`HTTRANSPARENT`) ensures all mouse clicks pass 100% through to windows below without interfering with your work. / 默认完全透明背景，文字带有自适应柔和投影，无边框与多余标签干扰；原生鼠标点击穿透，完全不阻碍底层窗口与游戏的正常操作。
- **Consistent Font & Color / 字体与颜色同倒计时同步**: Quote text automatically adopts the exact same font family (`settings.FontFamily`) and vibrant color (`settings.Color`) configured for your countdown timer. Text renders with adaptive dual-layer high-contrast drop-shadows, ensuring crisp readability on any desktop background. / 自动继承倒计时设置的字体与主题颜色，名言正文与倒计时颜色完全一致，作者名字以同色系柔和呼应，配合自适应高对比投影，在任何深浅背景下均清晰通透。
- **Unified Alt + Drag & Hover Card / Alt + 拖拽与微交互卡片**: Hold `Alt` and hover over the quote to smoothly wake up the Win11 acrylic card frame and action buttons (Next quote, Pin, Close). While holding `Alt`, drag anywhere on the quote to move its position, or drag the right edge to adjust width. Releasing `Alt` seamlessly restores clean click-through ambient text. / 按住 `Alt` 悬停于格言时唤醒微交互卡片与操作按钮（换一句、固定、关闭）；按住 `Alt` 直接拖拽卡片任意空白区域即可调整位置，拖动右侧抓手可调节宽度；松开 `Alt` 立即恢复为纯净无打扰的点击穿透文字。
- **Zero-Resource Hibernation / 极致低能耗与零轮询休眠**: Position adjustment is completely event-driven via Win32 `WM_NCHITTEST` without any 200ms cursor polling timers. When pinned to desktop, all timers are completely stopped (0 timer wakeups, 0% CPU, 0 memory allocations). / 交互与位置调整基于原生 `WM_NCHITTEST` 消息驱动，彻底移除了原本每 200ms 的鼠标坐标轮询定时器；桌面常驻固定时定时器彻底停止（0 唤醒、0% CPU 占用、0 额外内存分配）。
- **Intuitive Gestures & Shortcuts / 极简手势与快捷操作**:
  - `Alt + Drag / 拖拽定位`: Hold `Alt` and drag anywhere on the card to move it; new coordinates are automatically saved across sessions. / 按住 `Alt` 键在卡片任意区域拖拽即可移动位置，松开自动保存。
  - `Alt + Right-Edge Drag / 拖拽拉宽`: Hold `Alt` and drag the right edge to widen display width. / 按住 `Alt` 键拖拽卡片右侧可拉宽或收窄卡片。
  - `Alt + Double Click / 双击换一句`: Hold `Alt` and double-click the card to cycle to the next quote instantly (`换一句`). / 按住 `Alt` 双击卡片空白区域即刻切换下一句名言。
  - `Alt + Ctrl + Scroll / 字体缩放`: Hold `Alt + Ctrl` and scroll the mouse wheel to dynamically scale quote font size (`8pt` - `24pt`); new font size is automatically saved across sessions (`QuoteFontSize`). / 按住 `Alt + Ctrl` 滚轮动态调节字号（8pt ~ 24pt），松开自动永久记忆保存。
  - `Esc / 键盘退出`: Press `Esc` to dismiss and fade out immediately. / 按 `Esc` 键即刻渐隐关闭。
  - `Micro Buttons / 微按钮`: `↻ 换一句 / Next Quote`、`📌 固定 / Pin (永久钉在桌面)`、`✕ 关闭 / Close`。支持顶栏左侧平齐内联原生微胶囊提示（0ms 响应、与图标水平平齐并停靠于图标左侧，彻底消除对正文的遮挡并规避屏幕顶边截断）。
- **Custom Quotes File / 自定义名言库**: Automatically loads `quotes.txt` from the application directory or `%APPDATA%\iClock\quotes.txt`. Supports both UTF-8 and ANSI/GBK encodings. Format: `Quote Text —— Author` (or simple text lines). Supports arbitrary leading sequence numbers in text (e.g. `1. `, `02、`, `[3]`, `(4)`, `5) `, `6: `, `No. 7 `, `#8 `, `①`, `1．`), which are automatically hidden when displayed on the desktop HUD and preview card. If no custom file is found, iClock provides 10 built-in classic quotes. / 自动加载程序同目录或 `%APPDATA%\iClock\quotes.txt`。自适应 UTF-8 及 ANSI/GBK 编码。格式为 `名言正文 —— 作者`（作者可省略）。支持在名言文本前添加任意格式的序号（如 `1. `、`02、`、`[3]`、`(4)`、`5) `、`6: `、`No.7 `、`#8 `、`①`、`1．` 等），在桌面卡片及设置预览中均会自动隐去序号展示。若无自定义名言文件，默认提供 10 条经典中英名言。
- **AI Agent Quote Expansion / AI 智能体 MCP 原生自动扩充**: In Settings Tab 1, click **「✨ AI 扩充」** beside **「自定义…」** to invoke your local MCP AI agents (Cursor, Antigravity, Claude Desktop, Windsurf, Cline, Roo Code, Trae) to expand your quotes library with **30 curated quotes** per batch. Features an ultra-minimalist 410 × 265px dialog with unified tag pills (delete with `✕`, add custom tags with `➕ 添加` or Enter), pure MCP agent filtering (only agents with verified MCP integration are listed; unconfigured agents are completely excluded), zero auto-injection with clear guidance directing to 【AI Collaboration】 if no agent is configured, and a **Dual-Track In-Place Execution & Reporting Engine**:
  - **CLI Track (后台直接扩充)**: Agents supporting direct headless generation (e.g. Claude Code CLI, Ollama, Antigravity CLI) can be expanded directly in background (`⚡ 直接扩充`) without manual pasting, presenting live progress and reporting completion on the fly.
  - **IDE Track (对话框协作扩充)**: IDE agents (e.g. Cursor, Windsurf) keep the dialog open with clear next-step guidance (`✓ 30 条格言扩充指令已复制到剪贴板！请切换至对话框按 Ctrl+V 发送`), window activation helper (`唤起窗口`), and live MCP callback listening (`• 正在等待智能体通过 MCP 写入词库...`).
  - **Live In-Place Completion Report**: In either track, once quotes are added via `iclock_quote(action: "add")` or CLI parsing, the dialog immediately presents a celebratory success card (`✨ 格言扩充完成！`), reports the 30 new quotes, previews the first quote, and synchronizes to desktop display with automatic deduplication and `.bak` backup. / 在设置面板“每日格言”中，点击“自定义…”并排的“✨ AI 扩充”按钮，即可弹出 410 × 265px 极简扩充面板。采用统一标签池体系（支持 `✕` 随时删除默认标签，输入框与 `➕ 添加` 回车新增自定义标签，标签修改自动持久化记忆）；备选智能体严格过滤（仅展示实机已成功接入 MCP 的智能体，彻底剔除未配置项）；弹窗内不做自动注入，若未接入任何智能体则温和引导前往【AI 协作】一键接入；单次精选扩充 **30 条**高质量名言，独创**双轨就地执行与实时汇报反馈体系**：
  - **命令行直接执行轨**：对于支持命令行的智能体（如 Claude Code CLI、Ollama、Antigravity CLI），支持一键 `⚡ 直接扩充`，后台静默自动创作并直接入库，实时更新进度；
  - **IDE 对话框协作轨**：对于 Cursor 等 IDE，点击后弹窗就地进入协作与监听状态（指令已复制，指引切换至聊天框 Ctrl+V 发送，提供“唤起窗口”与“重新复制”快捷按钮，动态监听智能体写入）；
  - **统一当场汇报**：无论哪条轨道，一旦新格言入库成功，弹窗当场切换为“✨ 格言扩充完成！”成果汇报卡片，展示 30 条收录成果与最新格言预览，彻底告别“点了没反应/不知道成败”的体验盲区！
- **Settings & Audition / 设置与演示**: Manage independently via Settings Tab 1, select from 10 curated color presets, customize font style with live acrylic card preview, click **自定义…** or **✨ AI 扩充** to manage library, and audition the floating quote on your desktop anytime.

## History & Daily Statistics / 专注历史记录与统计

Choose **View today's history** from the tray menu to open the comprehensive history window.
- **Daily Details / 单日明细**: Inspect granular session logs for any day using the date switcher (`◀`, dropdown, `▶`). Each record logs start/end times, configured duration, actual focused duration, and result (completed, reset, interrupted). The summary footer dynamically displays total focus time, completed cycles, and completion rate.
- **Days Summary / 按天汇总**: Switch tabs to inspect daily aggregated focus metrics over selected ranges (Last 7 Days, 14 Days, 30 Days, or All History). Shows total focus time, cycle counts, completion rates, and achievement evaluations (● Excellent, ● Good, ● Fair, ● Needs work).
- **Drill-Down / 穿透钻取**: Double-click any day in the Days Summary table to immediately jump to that day's granular breakdown in Daily Details.
- **Keyboard Shortcuts / 快捷交互**: Press `Left`/`Right` arrow keys to step through dates, `Ctrl+Tab` to switch views, and `Ctrl+C` to copy selected rows to clipboard.

从托盘菜单选择“查看今日记录”，即可打开专注历史记录与统计窗口：
- **单日明细**：通过顶部的日期切换器（`◀`、下拉菜单、`▶`）查看任意日期的具体倒计时明细，包含开始与结束时间、设定时长、实际专注时长及结果（已完成、已重置、已中断）。底部汇总栏动态呈现当日累计专注时长、完成周期数与达成率。
- **按天汇总**：切换至该标签页可宏观查看按天聚合的专注指标，支持切换统计范围（最近 7 天、14 天、30 天、全部历史）。直观呈现每日专注总时长、完成/总数、完成率以及达成状态（● 卓越、● 良好、● 及格、● 待提升）。
- **穿透钻取**：在按天汇总表中双击任意一行日期，即可无缝穿透跳转至该日的“单日明细”查看具体会话。
- **快捷交互**：支持通过键盘 `←` / `→` 键前后切换日期，`Ctrl+Tab` 快速切换视图，选中文本支持 `Ctrl+C` 复制到剪贴板。

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
| `-q` | `--quote` | Show Daily Quote popup / 弹出每日格言浮窗 | `iclock -q` |
| `--once` | - | Single-run mode: exit automatically upon completion (ignored if desktop instance is running) / 单次运行模式：任务结束后自动完全退出（若桌面端已运行则不退出桌面端） | `iclock -s 25 --once` |
| `--quit` / `--exit` | `quit` | Gracefully terminate running iClock instance / 干净安全地退出正在运行的 iClock 实例 | `iclock --quit` |
| `--mcp` | - | Start Model Context Protocol stdio server / 启动 MCP stdio 服务 | `iclock --mcp` |
| `--mcp-prompt` | - | Print AI agent MCP setup prompt / 输出发给智能体的 MCP 接入提示词 | `iclock --mcp-prompt` |
| `--mcp-config` | - | Print MCP client JSON configuration snippet / 打印 MCP 客户端配置代码 | `iclock --mcp-config` |
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

## Model Context Protocol (MCP) Server / 智能体标准协议 (MCP) 服务

iClock 内置原生的 **Model Context Protocol (MCP)** Server，通过标准 stdio 协议与任意现代 AI 智能体（Claude Desktop, Cursor, Antigravity, Cline, Roo Code 等）无缝握手。

GUI 与 MCP 服务共用同一个 `iClock.exe`：智能体以 `iClock.exe --mcp` 启动桥接进程，桥接进程通过本机 Named Pipe 连接桌面 GUI；GUI 未运行时会自动拉起。工具调用由线程池并发处理，因此审批等待不会阻塞状态查询、进度或提醒。

通过 MCP，智能体不再受限于 IDE 内的文字窗口，而是在你的 Windows 物理桌面上拥有了**感知与交互实体**。

### Configuration & One-Click Setup / 客户端配置与一键提示词接入 (单页直达·零维护)

iClock 采用 **单页直达·零弹窗·即改即存** 的一体化设计，所有 AI 协作与 MCP 功能均集中收拢在设置面板的 **「AI 协作」** 标签页内，去除了传统繁琐的底部保存/取消按钮，关闭窗口即可自动持久化：

1. **智能体运行状态一目了然**：
   - 打开 iClock 设置 $\rightarrow$ **「AI 协作」** 标签页；
   - **上半板块（智能体后台任务提醒）**：清晰展示检测到的智能体进程数量及能力副标题，右上角配有 **`[ 重新检测 ]`** 与动态状态胶囊 **`[ ● 已开启 ]` / `[ ○ 已停用 ]`**；
   - 列表以轻盈卡片展示检测到的智能体进程，右侧配有清晰的状态徽章（**`[ 运行中 ]`** / **`[ 已就绪 ]`** / **`[ 未监听 ]`**），纯净直观，零视觉干扰。
2. **一键复制「接入提示词」（让智能体自主配置，零门槛·跨宿主）**：
   - 在「AI 协作」下半板块「MCP 协议直连」区域，直接点击 **`[ 复制接入提示词 ]`** 按钮；
   - 系统自动解析本机 `iClock.exe` 绝对物理路径，生成一段结构清晰的接入提示词并写入剪贴板；
   - 用户只需将提示词粘贴发送给任意 AI 智能体（如 Antigravity、Cursor、Claude、Windsurf、Cline、Roo Code 等），智能体即可识别本机的宿主环境与配置格式，自主合并配置，省去手动找目录、改 JSON 的繁琐步骤！
3. **三大核心能力一键「演示」体验 (精致纯白发丝线卡片)**：
   - 在「AI 协作」底部功能列表中，汇聚三大高价值刚需场景：**决策请示门禁**、**任务完成提醒**、**桌面状态投射**；
   - 采用精致统一的 Win11 纯白圆角卡片与微发丝线分割（彻底去除生硬的粗黑外框），每一项右侧均配有极简的 **`[ 演示 ]`** 按钮；
   - 无需启动任何外部智能体即可一键亲身体验优雅置顶的决策门禁、全向声音唤醒与磨砂态势浮窗。
4. **终端命令行极简辅助输出**：
   - 控制台输出接入提示词：`iclock --mcp-prompt`
   - 控制台打印标准 MCP JSON 配置代码：`iclock --mcp-config`

### Native MCP Tools / 内置智能体工具

| Tool Name / 工具名 | Parameters / 参数 | Description / 核心价值 |
| :--- | :--- | :--- |
| **`iclock_ask_human`** *(Killer Feature)* | `question` (string, required)<br>`options` (string[], optional)<br>`timeout_seconds` (int, default: 0)<br>`background_only` (bool, default: false) | **决策请示门禁 (Human-in-the-Loop)**：在用户屏幕中央唤起高优先级 Fluent 交互卡片，等待用户按键选择或输入。适合智能体在执行高风险命令（部署、删除库表、付费 API、冲突合并）前获取人类审批。默认永不超时、无倒计时催促，直到用户主动确认或按 Esc 取消。 |
| **`iclock_progress`** | `progress` (string, required, e.g. "45" or "clear")<br>`message` (string, optional)<br>`agent_name` (string, optional) | **多智能体任务状态卡**：进度条与当前步骤文字在同一行；倒计时运行时显示在时钟下方，无倒计时时独立显示。每个智能体独立占一行，`clear` 只清除自己的行；同一 MCP 客户端内并行时用相同的 `agent_name` 更新和清除。 |
| **`iclock_timer`** | `action` ("start"\|"pause"\|"resume"\|"reset")<br>`minutes` (int, default: 25)<br>`message` (string, optional) | **倒计时协同 (Timer Sync)**：智能体联动番茄钟，根据工作节奏自动启停倒计时或推送休息提示。 |
| **`iclock_notify`** | `message` (string, required)<br>`sound` (bool, default: true) | **任务完成强提醒 (Task Notification)**：智能体长任务完成时在屏幕弹出置顶提醒卡片，播放提示音，将注意力拉回工作台。 |
| **`iclock_quote`** | `text` (string, optional)<br>`author` (string, optional)<br>`agent_name` (string, optional) | **桌面状态文字**：更新任务状态卡中当前智能体对应行的状态文字；也可轮换或扩充桌面格言。 |
| **`iclock_status`** | *(none)* | **桌面状态感知**：实时读取当前倒计时运行状态、剩余秒数与设定时长，帮助智能体智能避开用户专注高潮。 |

### `iclock_ask_human` 交互体验亮点
1. **永不超时，消除焦虑**：默认无时限耐心等待，彻底去除倒数秒表压迫感，从容阅读代码与深度思考；
2. **智能前后台感知与免打扰 (Smart Focus Detection)**：当宿主（如 Cursor）正处于前台活跃状态时，卡片采用温和置顶呈现（不抢夺键盘焦点），不中断用户正在打字输入的代码；当宿主处于后台（切换至浏览器或全屏视频）时，才触发机械铃音与强聚焦唤醒；
3. **支持 `background_only` 聊天窗降级**：支持传入 `background_only: true`。若宿主在前台，系统直接返回 `{ in_foreground: true }`，引导智能体直接在内置聊天窗口提问；
4. **键盘直达**：提供数字选项时，用户直接按键盘 `1`..`9` 即可一键确认，按 `Esc` 随时主动取消；
5. **文本自由输入**：当 `options` 为空时自动切换为输入框模式，按 `Enter` 快速提交；
6. **全屏穿透强提醒**：无论用户正在全屏打代码、看文档还是播放视频，卡片均以置顶层级醒目呈现；
7. **可选无人值守超时**：仅在无人值守自动化脚本或 CI 流水线中明确传入 `timeout_seconds > 0` 时才激活超时保护。

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

