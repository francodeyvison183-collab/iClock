<div align="center">

# <img src="docs/icon-64.png" width="36" height="36" style="vertical-align: -6px;" alt="iClock Logo" /> iClock

### Ultra-lightweight, Transparent Desktop Countdown for Windows

[![Release](https://img.shields.io/github/v/release/francodeyvison183-collab/iClock?color=blue&label=Release)](https://github.com/francodeyvison183-collab/iClock/releases/latest)
[![Platform](https://img.shields.io/badge/Platform-Windows%207%20%7C%208%20%7C%2010%20%7C%2011-0078D6?logo=windows&logoColor=white)](https://github.com/francodeyvison183-collab/iClock/releases/latest)
[![Size](https://img.shields.io/badge/Size-253%20KB-success)](https://github.com/francodeyvison183-collab/iClock/releases/latest)
[![WinGet](https://img.shields.io/badge/WinGet-FrancoDeyvison.iClock-0078D6?logo=windows-terminal&logoColor=white)](https://github.com/microsoft/winget-pkgs/pull/449511)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

<p align="center">
  <b>Single 253 KB Binary · RAM &lt; 15 MB · Mouse Click-Through · Global Hotkey · Zero Dependencies · Pure Local Privacy</b>
</p>

<p align="center">
  <a href="https://francodeyvison183-collab.github.io/iClock/"><b>🌐 Website</b></a> ·
  <a href="https://github.com/francodeyvison183-collab/iClock/releases/latest"><b>🚀 Download Latest (ZIP Archive)</b></a> ·
  <a href="https://francodeyvison183-collab.github.io/iClock/llms.txt"><b>🤖 llms.txt</b></a> ·
  <a href="README.md">简体中文说明</a> ·
  <a href="docs/USER_GUIDE.md">User Guide</a> ·
  <a href="CHANGELOG.md">Changelog</a>
</p>

<img src="docs/preview.png" alt="iClock Preview" width="860" />

</div>

---

## 💡 Why iClock?

Most timer and Pomodoro applications on Windows are bloated **100MB+** Electron packages that continuously chew through CPU and RAM, often bundled with telemetry or ads, while their solid windows obstruct your workspace.

**iClock adheres to extreme engineering minimalism**:

* 🪟 **Native 32-bit ARGB Rendering & Click-Through**: Powered by Windows native `UpdateLayeredWindow`, delivering pristine subpixel anti-aliasing without jagged monochrome edges; adaptive soft drop shadows ensure crystal-clear visibility on light, dark, or noisy backgrounds; default 100% click-through never steals focus.
* ⚡ **Ultra-Lightweight & 0.0% CPU**: Zero third-party dependencies or runtimes. Pure single executable footprint of only **253 KB**. Dynamic heartbeat alignment and memory caching keep awakenings minimal and battery drain at zero.
* ⌨️ **Ergonomic Direct Drag & Tray Scroll**: Reposition instantly by **holding `Alt` + left-click dragging** anywhere on the desktop (no tedious tray menu toggling); hover over the tray icon and **scroll your mouse wheel to adjust duration** (`±1m`, or `±5m` with `Shift`); default `Ctrl + Shift + T` global hotkey.
* 💬 **Desktop Daily Quotes & AI Expansion (0-Polling Hibernation)**: Gracefully appears centered upon startup or morning unlock; hold `Alt` to interact, drag right edge to adjust width, and use **`Alt + Ctrl + Wheel` to scale font size**; event-driven interaction completely eliminates background cursor polling timers (0 wakeups, 0% CPU while idling); expand quotes library via local AI agents / MCP in batches of 30 curated quotes.
* 💻 **Command Line & Script Automation (IPC Pipeline & In-Tab Guidance)**: Built for terminal power users and automated workflows. Features native Win32 `WM_COPYDATA` inter-process communication with 0ms latency and zero background overhead. Settings Tab 2 provides concise guidance: run `iclock -h` in terminal for CLI commands, or execute `iclock --install-cli` once to add iClock to User PATH and invoke globally without `.exe` (just like `claude` or `pi`); integrate with build scripts (`npm run build && iclock -n "Build Complete"`) for instant completion alerts!
* 🔔 **Tiered Alerts & Fullscreen/Game Do-Not-Disturb**: Choose between a **TopMost Popup Dialog** (with classic mechanical telephone ring or chime for critical deadlines) or an **In-Place Breathing Pulse** (desktop digits gently breathe for 5 seconds with a soft chime without stealing window focus). Built-in **Auto Fullscreen DND** automatically downgrades popups to subtle breathing pulses whenever full-screen DirectX/Vulkan games, PPT slideshows, or video playback are detected in the foreground.
* 🤖 **AI Agent Collaboration & Native MCP Server**: Monitors 20+ domestic & international agents (Cursor, Antigravity, Claude Code, GitHub Copilot, Trae, Tongyi Lingma, CodeGeeX, MiniMax, CodeBuddy, Aider, etc.) via a 4-tier perception matrix with instant zero-delay notification and 1-click window restore; built-in native stdio **Model Context Protocol (MCP)** Server (`iclock.com --mcp`) featuring 5 tools including `iclock_ask_human` (Desktop Decision Gate), with 1-click prompt copying (`iclock --mcp-prompt`) for autonomous agent integration!
* 🔒 **100% Local & Portable**: Config automatically prioritizes the executable folder (portable mode) or `%APPDATA%\iClock`, leaving no registry junk or network telemetry.
* 🌍 **Bilingual & History Logs**: Seamlessly switch between English and Simplified Chinese in Settings. About dialog features direct author feedback email (`francodeyvison183@gmail.com`). Daily countdown records are saved as clean TSV files.

---

## 📊 Comparison

| Metric | **iClock (This Project)** | Typical Electron Timer | Windows Clock App |
| :--- | :--- | :--- | :--- |
| **Package Size** | ⚡ **253 KB** (Portable single exe) | 120 MB ~ 250 MB | Pre-installed |
| **Memory (RAM)** | ⚡ **~15 MB** (Physical baseline) | 150 MB ~ 300 MB | ~30 MB |
| **CPU Usage** | ⚡ **0.0%** (Heartbeat aligned) | 0.5% ~ 3.0% (Polling) | 0.0% |
| **Interaction** | ⚡ **Full click-through, borderless** | Solid window blocking view | Minimized or full window |
| **Cold Startup** | ⚡ **< 100ms** (Instantaneous) | 2 ~ 4 seconds | 1 ~ 2 seconds |
| **Network Traffic**| ⚡ **0 network calls, 100% offline** | Telemetry / Update checks | Microsoft account sync |

---

## 🎯 Use Cases

* 🍅 **Deep Focus / Pomodoro Technique**: Fire a 25-minute timer without covering code or documentation.
* 🎤 **Presentations & Meeting Timekeeping**: Keep track of speech duration in the screen corner while freely controlling slides.
* 🎮 **Gaming & Skill Cooldowns**: Borderless click-through prevents accidental mouse clicks during gameplay.
* ☕ **Posture & Eye-Rest Breaks**: Set a 45-minute countdown for mandatory stretch reminders.

---

## 🚀 Quick Start

### Option A: Portable ZIP Archive (Official Recommended · Prevents Browser False Alarms)

1. Go to [GitHub Releases](../../releases/latest) and download **`iClock-1.07-windows.zip`** (Recommended over standalone `.exe` to avoid browser SmartScreen / untrusted binary warning prompts).
2. Extract the archive and double-click `iClock.exe` to run (100% portable, zero installer, zero registry clutter).
3. The overlay stays hidden until you start a countdown.

### Option B: Windows Package Manager WinGet

Run the following in PowerShell or Command Prompt:
```powershell
winget install FrancoDeyvison.iClock
```
> Automatically downloads, verifies, installs, and adds `iClock` to system PATH so you can launch it from any terminal.

### Option C: Scoop Package Manager

```powershell
scoop install https://raw.githubusercontent.com/francodeyvison183-collab/iClock/main/scoop/iclock.json
```

### Common Controls

* **Start / Pause**: Press global hotkey `Ctrl + Shift + T` (with smart fallback if occupied) or left-click the system tray icon.
* **Direct Repositioning (Recommended)**: Hold `Alt` and drag the countdown text directly with left mouse button to reposition anywhere on screen. (Tray menu "Adjust position" also available for both timer and quote).
* **Tray Wheel Duration Tuning**: Hover over the system tray icon and roll your mouse wheel to quickly adjust duration (`±1 min`, or `±5 min` with `Shift`).
* **Desktop Daily Quotes**: On desktop quote text, **hold `Alt` + drag to move**, drag right edge to adjust card width, and use **`Alt + Ctrl + Wheel` to scale font size**. Double-click to cycle quotes, or click `📌` to pin to desktop wallpaper.
* **Settings**: Right-click tray $\rightarrow$ **Settings…** features authentic Windows 11 File Explorer multi-tab architecture (⏱ Timer / 💬 Daily Quotes / 🤖 AI Agents):
  - **Timer**: Duration, font, color, format, alert sound (Gentle Chime / Classic Telephone Bell / Alarm Ring), alert style, auto-start.
  - **Daily Quotes**: Enable switch, desktop display toggle, window level, 10 color chips, `[ Edit Quotes ]` and `[ ✨ AI Quotes ]` batch expansion.
  - **AI Agents**: Agent background task monitor switch, process list, MCP protocol connection (1-click copy setup prompt, 3 core demo triggers), and bottom terminal guidance (`iclock -h` / `iclock --install-cli`).
* **View History & Daily Statistics**: Right-click tray $\rightarrow$ **View today's history…** to inspect granular sessions with date steppers, or switch to **Days Summary** to track focus streaks across the last 7/14/30 days with completion rates, evaluations, double-click drill-down, and `Ctrl+C` table export.

---

## 💻 Command Line & Script Pipeline (CLI & IPC)

iClock features a built-in **native Win32 IPC messaging pipeline (`WM_COPYDATA`)**. Invoking CLI commands never launches redundant background instances; it communicates directly with the active running iClock process in 0ms.

### 1. Global Terminal Invocations without `.exe` (Like `claude` or `pi`)

Windows natively supports extensionless execution through `%PATHEXT%`. Run the built-in `--install-cli` command once from any terminal:

```powershell
.\dist\iClock --install-cli
```

iClock automatically registers its directory into your **User PATH** environment variable. You can then invoke `iclock` from any folder in PowerShell, Command Prompt, Git Bash, or Windows Terminal:

```bash
iclock --help
```

### 2. Command Reference

| Command | Shorthand | Description | Example |
| :--- | :--- | :--- | :--- |
| `iclock` | - | Launch GUI (or retain background if active) | `iclock` |
| `iclock --start [mins]` | `iclock -s [m]` | Start work countdown with custom minutes | `iclock -s 25` or `iclock -s 45` |
| `iclock --pause` | `iclock -p` | Pause running countdown | `iclock -p` |
| `iclock --resume` | `iclock -r` | Resume paused countdown | `iclock -r` |
| `iclock --toggle` | `iclock -t` | Toggle between Pause and Resume | `iclock -t` |
| `iclock --reset` | - | Reset timer to idle and hide overlay | `iclock --reset` |
| `iclock --rest [mins]` | - | Start rest break timer (default: 5 mins) | `iclock --rest 10` |
| `iclock -n <message>` | `iclock --notice` | Show TopMost prompt and chime (for scripts) | `iclock -n "All Tests Passed!"` |
| `iclock --install-cli` | - | Add directory to User PATH environment variable | `iclock --install-cli` |
| `iclock --help` | `iclock -h` | Print console usage and options | `iclock -h` |

### 3. Script & Automation Examples

* **Build pipeline completion notifications**:
  ```bash
  npm run build && iclock -n "Production Build Succeeded!"
  ```
* **Test suite alerts**:
  ```bash
  pytest && iclock -n "All test suites passed!"
  ```
* **Deep focus countdown from terminal**:
  ```bash
  # Start a 50-minute deep work block
  iclock -s 50
  ```

---

## ❓ Frequently Asked Questions (FAQ)

* **Q: Does it interfere with mouse clicks when gaming or coding?**  
  **A**: No. iClock leverages the Windows native `WS_EX_TRANSPARENT` style. Floating digits remain on the topmost layer, but all mouse clicks and scrolls pass through to underlying applications without stealing window focus.
* **Q: Why is the binary only 253 KB?**  
  **A**: Engineering purity. Built with Windows native C# and GDI+ rendering, avoiding any 100MB+ Electron wrappers. Runtime RAM consumption stays strictly under 15 MB.
* **Q: Is it safe, clean, and ad-free?**  
  **A**: 100% open-source under the MIT License. Zero ads, zero background services, zero telemetry. Exiting completely releases all resources. See [PRIVACY.md](docs/PRIVACY.md).

---

## 🛠️ Build from Source

Built with native C# and standard Windows .NET Framework 4.x. **No heavy Visual Studio installation required** — compiles with built-in `csc.exe` in under one second:

```powershell
git clone https://github.com/francodeyvison183-collab/iClock.git
cd iClock
.\build.ps1
```
> Output binary is generated at `dist/iClock.exe` (253 KB). See [Build Guide](docs/BUILDING.md).

---

## 🔒 Privacy & Security

* **Local Storage**: All settings and history are stored locally in `%APPDATA%\iClock`. Simply delete this directory to uninstall cleanly.
* **System Registry**: Only touches `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` if you explicitly enable auto-start.
* **Zero Network Activity**: No network sockets, HTTP requests, or telemetry code. See [Privacy](docs/PRIVACY.md).

---

## 💖 Support & Sponsor

If you find iClock helpful for your daily workflow, consider giving this repository a ⭐️ **Star**, or support the author with a coffee:

<div align="center">
  <img src="src/zan.jpg" width="220" alt="WeChat Pay QR" />
  <p><b>Scan with WeChat to Sponsor</b></p>
  <p><i>Thank you for supporting independent open-source craft!</i></p>
</div>

---

## 📄 License

Licensed under the [MIT License](LICENSE). Free for personal and commercial use. Contributions and PRs welcome!
