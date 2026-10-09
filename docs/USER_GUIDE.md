# iClock User Guide / 使用指南

## Getting started / 开始使用

Run `iClock.exe`. The overlay is hidden before countdown starts. Press the global hotkey to start counting. The default is `Ctrl+Alt+Space`. The same hotkey pauses and resumes the current session. When the countdown finishes, the text hides automatically and a top-most notification appears, requiring confirmation to dismiss.

运行 `iClock.exe` 后，倒计时开始前桌面不显示文字；按全局快捷键后才开始倒计时。默认快捷键为 `Ctrl+Alt+Space`，同一快捷键也可暂停和继续。倒计时结束后文字自动隐藏，并弹出置顶消息提示，需要用户确认后才消失。

Right-click the iClock tray icon to open the menu. The menu can reset the countdown, adjust position, open settings, show today's history, switch languages, or exit.

右键点击托盘中的 iClock 图标可打开菜单，用于重置倒计时、调整位置、打开设置、查看今日记录、切换语言或退出。

Choose **Language** from the tray menu, then select **简体中文** or **English**. Simplified Chinese is the default. The selection is saved and restored on the next launch.

在托盘菜单中打开“语言”，选择“简体中文”或“English”。默认语言为简体中文，选择会保存并在下次启动时恢复。

## Settings / 设置

- **Duration:** 1 to 1,440 minutes. Changing settings while a session is paused keeps the paused session's remaining time; reset to use the newly configured duration.
- **Text size:** 12 to 120 pixels.
- **Text color:** select a color using the system color picker.
- **Display format:** `HH:MM:SS`, total minutes and seconds (`MM:SS`), or unit labels. Unit labels follow the selected UI language.
- **End message:** optional text shown in the top-most prompt when the countdown finishes. The notification can be disabled separately.
- **Hotkey:** click the hotkey field and press a key combination containing Ctrl, Alt, or Shift. If another app owns the combination, iClock keeps the previous hotkey.
- **Startup:** adds or removes iClock from the current user's Windows sign-in Run key.
- **End sound/notification:** independently control the system sound and the top-most confirmation prompt.

## Position and click-through / 位置与点击穿透

In normal mode the overlay is transparent and click-through. Choose **Adjust text position** in the tray menu to show a black background that is 60% transparent. The text is centered in this area; drag it, then choose **Finish position adjustment**. The position is saved automatically. Normal transparent click-through behavior returns when adjustment ends.

普通模式下文字背景透明并支持点击穿透。在托盘菜单选择“调整文字位置”会显示 60% 透明的黑色背景，文字居中显示。拖动后选择“完成位置调整”，位置会自动保存；退出调整状态后恢复透明点击穿透。

## Today's history / 今日记录

Choose **View today's history** from the tray menu. Entries include the session start and end times, configured duration, and result: completed, reset, or interrupted. A reset creates a record for the active session; exiting iClock during an active or paused session records it as interrupted.

从托盘菜单选择“查看今日记录”，可查看开始和结束时间、设定时长及结果（完成、重置或中断）。重置会为当前会话留下一条记录；在正在计时或暂停期间退出，会记录为中断。

Settings are stored in `%APPDATA%\iClock\settings.ini`. History files are stored as UTF-8 TSV files named `history-YYYYMMDD.tsv` under the same directory, using the session's start date.
