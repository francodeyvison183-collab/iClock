<div align="center">

# <img src="docs/icon-64.png" width="36" height="36" style="vertical-align: -6px;" alt="iClock Logo" /> iClock

### Ultra-lightweight, Transparent Desktop Countdown for Windows

[![Release](https://img.shields.io/github/v/release/francodeyvison183-collab/iClock?color=blue&label=Release)](https://github.com/francodeyvison183-collab/iClock/releases/latest)
[![Platform](https://img.shields.io/badge/Platform-Windows%207%20%7C%208%20%7C%2010%20%7C%2011-0078D6?logo=windows&logoColor=white)](https://github.com/francodeyvison183-collab/iClock/releases/latest)
[![Size](https://img.shields.io/badge/Size-154%20KB-success)](https://github.com/francodeyvison183-collab/iClock/releases/latest)
[![WinGet](https://img.shields.io/badge/WinGet-FrancoDeyvison.iClock-0078D6?logo=windows-terminal&logoColor=white)](https://github.com/microsoft/winget-pkgs/pull/449511)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

<p align="center">
  <b>Single 154 KB Binary · RAM &lt; 15 MB · Mouse Click-Through · Global Hotkey · Zero Dependencies · Pure Local Privacy</b>
</p>

<p align="center">
  <a href="https://francodeyvison183-collab.github.io/iClock/"><b>🌐 Website</b></a> ·
  <a href="https://github.com/francodeyvison183-collab/iClock/releases/latest"><b>🚀 Download</b></a> ·
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
* ⚡ **Ultra-Lightweight & 0.0% CPU**: Zero third-party dependencies or runtimes. Dynamic heartbeat alignment and memory caching keep awakenings minimal and battery drain at zero.
* ⌨️ **Ergonomic Direct Drag & Tray Scroll**: Reposition instantly by **holding `Alt` + left-click dragging** anywhere on the desktop (no tedious tray menu toggling); hover over the tray icon and **scroll your mouse wheel to adjust duration** (`±1m`, or `±5m` with `Shift`); default `Ctrl + Shift + T` global hotkey.
* 🔔 **Tiered Alerts & Fullscreen/Game Do-Not-Disturb**: Choose between a **TopMost Popup Dialog** (with looping chime for critical deadlines) or an **In-Place Breathing Pulse** (desktop digits gently breathe for 5 seconds with a soft chime without stealing window focus). Built-in **Auto Fullscreen DND** automatically downgrades popups to subtle breathing pulses whenever full-screen DirectX/Vulkan games, PPT slideshows, or video playback are detected in the foreground.
* 🤖 **AI Agent Background Task Completion Alerts**: Designed for developers utilizing AI coding assistants. Automatically monitors 20+ domestic & international agents (Cursor, Antigravity, Claude Code, GitHub Copilot, Trae, Tongyi Lingma, CodeGeeX, MiniMax, CodeBuddy, Aider, etc.) via a 4-tier perception matrix (Toast notification listener, Win32 Shell taskbar flash `HSHELL_FLASH` at 0ms, window title state badge, and edge-detected cluster CPU fallback). Instant zero-delay notification upon task completion with 1-click window restore!
* 🔒 **100% Local & Portable**: Config automatically prioritizes the executable folder (portable mode) or `%APPDATA%\iClock`, leaving no registry junk or network telemetry.
* 🌍 **Bilingual & History Logs**: Seamlessly switch between English and Simplified Chinese in Settings. Daily countdown records are saved as clean TSV files.

---

## 📊 Comparison

| Metric | **iClock (This Project)** | Typical Electron Timer | Windows Clock App |
| :--- | :--- | :--- | :--- |
| **Package Size** | ⚡ **154 KB** (Portable single exe) | 120 MB ~ 250 MB | Pre-installed |
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

### Option A: Windows Package Manager WinGet (Recommended)

Run the following in PowerShell or Command Prompt:
```powershell
winget install FrancoDeyvison.iClock
```
> Automatically downloads, verifies, installs, and adds `iClock` to system PATH so you can launch it from any terminal.

### Option B: Scoop Package Manager

```powershell
scoop install https://raw.githubusercontent.com/francodeyvison183-collab/iClock/main/scoop/iclock.json
```

### Option C: Standalone Portable Binary

1. Go to [GitHub Releases](../../releases/latest) and download `iClock-1.05-windows.zip` or `iClock-1.05-windows.exe`.
2. Double-click to launch (no installer, no registry residue).
3. The overlay stays hidden until you start a countdown.

### Common Controls

* **Start / Pause**: Press global hotkey `Ctrl + Shift + T` (with smart fallback if occupied) or left-click the system tray icon.
* **Direct Repositioning (Recommended)**: Hold `Alt` and drag the countdown text directly with left mouse button to reposition anywhere on screen. (Tray menu "Adjust text position" also available).
* **Tray Wheel Duration Tuning**: Hover over the system tray icon and roll your mouse wheel to quickly adjust duration (`±1 min`, or `±5 min` with `Shift`).
* **Settings**: Right-click tray $\rightarrow$ **Settings…** features modern segmented dual tabs (⏱ Timer / 🤖 AI Agents) to configure duration, font, color, format, alert sound (Gentle Chime / Classic Notice / Alarm Ring), alert style, auto-start, and detected domestic/international AI agents (Cursor, Antigravity, Claude Code, GitHub Copilot, Trae, Tongyi Lingma, CodeGeeX, MiniMax, CodeBuddy, WorkBuddy, Gemini, etc.) background task completion alerts.
* **View History & Daily Statistics**: Right-click tray $\rightarrow$ **View today's history…** to inspect granular sessions with date steppers, or switch to **Days Summary** to track focus streaks across the last 7/14/30 days with completion rates, evaluations, double-click drill-down, and `Ctrl+C` table export.

---

## ❓ Frequently Asked Questions (FAQ)

* **Q: Does it interfere with mouse clicks when gaming or coding?**  
  **A**: No. iClock leverages the Windows native `WS_EX_TRANSPARENT` style. Floating digits remain on the topmost layer, but all mouse clicks and scrolls pass through to underlying applications without stealing window focus.
* **Q: Why is the binary only 154 KB?**  
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
> Output binary is generated at `dist/iClock.exe` (154 KB). See [Build Guide](docs/BUILDING.md).

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
