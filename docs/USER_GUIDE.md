# iClock User Guide / 使用指南

## Getting started / 开始使用

Run `iClock.exe`. The overlay is hidden before countdown starts. Press the global hotkey to start counting. The default is `Ctrl+Shift+T`. The same hotkey pauses and resumes the current session. When the countdown finishes, the text hides automatically and a top-most notification appears, requiring confirmation to dismiss.

运行 `iClock.exe` 后，倒计时开始前桌面不显示文字；按全局快捷键后才开始倒计时。默认快捷键为 `Ctrl+Shift+T`，同一快捷键也可暂停和继续。倒计时结束后文字自动隐藏，并弹出置顶消息提示，需要用户确认后才消失。

Right-click the iClock tray icon to open the menu. The menu allows you to start/pause, reset the countdown, adjust position, view today's history, open settings, view about & updates, or exit.

右键点击托盘中的 iClock 图标可打开菜单，用于开始/暂停、重置倒计时、调整位置、查看今日记录、打开设置、关于与检查更新或退出。

## Settings / 设置面板

Settings uses a modern dual-tab segmented navigation / 设置面板采用双标签页切换：

### ⏱ 倒计时 / Timer Tab
- **Duration / 倒计时时长:** 1 to 1,440 minutes. Changing settings while a session is paused keeps the paused session's remaining time; reset to use the newly configured duration.
- **Text size / 文字大小:** 12 to 120 pixels (default: 20 pixels / 默认 20 像素).
- **Font style / 字体样式:** Segoe UI, Consolas, Arial, Impact, Microsoft YaHei.
- **Text color / 文字颜色:** select a color using the system color picker (default: Red `#FF0000` / 默认红色).
- **Display format / 显示格式:** total minutes and seconds (`MM:SS`, default / 默认), hours/minutes/seconds (`HH:MM:SS`), or unit labels (`Chinese units`).
- **End message / 结束提示词:** optional text shown in the prompt when the countdown finishes.
- **Start/pause hotkey / 快捷键:** click the hotkey field and press a key combination containing Ctrl, Alt, or Shift. Default is `Ctrl + Shift + T`.
- **Interface language / 界面语言:** choose **简体中文** or **English**.
- **End sound & notification / 声音与通知:** independently control the prompt sound style (**QQ Message / QQ 经典提示 (嘀嘀)**, **Pager Beep / 寻呼机提示 (BP机)**, **Alarm Ring / 醒目闹铃**, all >= 3s duration) and notification mode (**Popup / Auto DND Fullscreen** vs **In-Place Pulse**).
- **Start with Windows / 开机自启动:** located at the bottom of the countdown settings tab, controls automatic start on user logon.

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

- **Direct Alt+Drag / 按住 Alt 直接拖拽**: Hold `Alt` and drag the countdown text directly with the left mouse button to reposition it anywhere on your screen. Release to automatically save coordinates. / 在屏幕上按住 `Alt` 键，鼠标左键直接拖拽倒计时文字即可瞬时定位，松开自动保存，完全无需进菜单。
- **Tray Scroll Wheel / 托盘滚轮微调时长**: Hover your mouse over the system tray icon and roll the mouse wheel to adjust duration by `±1 min` (or `±5 min` with `Shift`). You can also hold `Alt` and scroll the wheel directly over the floating clock. / 鼠标悬停在系统托盘图标上滚动滚轮，直接以 `±1分钟`（按住 `Shift` 为 `±5分钟`）快速微调倒计时时长；按住 `Alt` 在悬浮文字上滚动滚轮亦可同步调整。
- **Menu Move Mode / 菜单调整模式**: You can also choose **Adjust text position** in the tray menu to display the positioning frame. / 亦可从托盘菜单点击“调整文字位置”进行拖动定位。
- **True 32-bit ARGB & Adaptive Shadow / 真抗锯齿与自适应投影**: Powered by native `UpdateLayeredWindow`, text renders with smooth anti-aliased subpixel precision and adaptive drop shadows, crystal-clear across bright wallpapers, dark IDEs, and full-screen games. / 采用 Windows 原生 `UpdateLayeredWindow` 32 位 ARGB 渲染，消除传统色键点阵毛刺，配合平滑自适应边缘微投影，在浅色文档、深黑终端或高对比游戏画面下均清晰可读。

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

Settings are stored in `%APPDATA%\iClock\settings.ini`. History files are stored as UTF-8 TSV files named `history-YYYYMMDD.tsv` under the same directory, using the session's start date.
