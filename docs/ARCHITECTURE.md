# Architecture

iClock is an ultra-lightweight Windows desktop utility compiled directly via `csc.exe` against the .NET Framework (4.6+ / 4.8). It has zero external package dependencies, produces one compact binary (~434 KB), and runs with minimal resource usage (< 10 MB RAM, ~0.0% idle CPU).

The source tree is split into 16 focused modules compiled together into that single binary:

| Module | Responsibility |
| :--- | :--- |
| `iClock.cs` | Entry point, countdown engine, tray menu, history windows, CLI dispatch |
| `Core.cs` | `Settings`, `NativeMethods` (all Win32 P/Invoke), shared helpers |
| `Controls.cs` | Reusable Win11-styled controls (toggle switch, quote rows, cards) |
| `Overlay.cs` | Layered countdown window and its GDI+ rendering pipeline |
| `Quotes.cs` | Daily quote library, screensaver, custom quote import |
| `Usage.cs` | Usage tracking, day-axis panel, trend panel, history menu item |
| `AgentWatcher.cs` | Filesystem watchers for agent session directories |
| `AgentTranscriptParse.cs` | Protocol-aware transcript parsing for each agent's log format |
| `AgentHud.cs` | Multi-agent status bar HUD, pill row, priority rotation |
| `SettingsDialog.cs` | Settings window (timer / quotes / AI collaboration tabs) |
| `Dialogs.cs` | `NoticeDialog`, about dialog, dialog mutual exclusion |
| `EyeCare.cs` | Eye-care reminder tracker |
| `Screensaver.cs` | Windows screensaver mode |
| `Menu.cs` | Tray menu construction and language submenu |
| `WelcomeAbout.cs` | First-run welcome and the about box |
| `CliLauncher.cs` | The companion `iclock.com` console entry point |

## Architectural Foundations

### 1. State Machine & Lifecycle (`ClockState`)
The application core uses a finite state machine (`ClockState`) to manage transitions:
- `Idle`: No active session; overlay is hidden.
- `Running`: Work countdown active; high-precision monotonic deadline tracked via `Stopwatch.GetTimestamp()`.
- `Paused`: Work countdown paused; overlay dimmed to 50% opacity.
- `RestRunning`: Pomodoro rest break countdown active (default 5 min).
- `RestPaused`: Rest break paused.
- `Alarming`: Session completed; triggers sound synthesis and prompt (NoticeDialog or Breathing Pulse).
- `MoveMode`: Interactive drag mode with semi-transparent frame and coordinate persistence.

### 2. High-Precision Monotonic Heartbeat
Countdown uses a high-resolution `Stopwatch` frequency deadline rather than wall-clock datetime polling. The UI timer dynamically calculates the fractional millisecond offset to the next integer second (`timer.Interval = frac * 1000 + 15ms`), aligning wakeups directly to second boundaries. This eliminates timer drift and avoids redundant redraws.

### 3. Layered Window Rendering & Zero-GC Pipeline (`Overlay`)
- Borderless, topmost form using `WS_EX_LAYERED`, `WS_EX_TOOLWINDOW`, and `WS_EX_TRANSPARENT`.
- `UpdateLayeredWindow` GDI+ rendering pipeline with subpixel anti-aliasing and manual pre-multiplied alpha arithmetic.
- Zero-GC rendering: Offscreen `Bitmap`, `Graphics`, and byte conversion buffers are cached across frames, eliminating heap churn during high-frequency breathing animations (20 FPS).

### 4. Consolidated Native Interop (`NativeMethods`)
All Win32 P/Invoke declarations (`user32.dll`, `gdi32.dll`, `dwmapi.dll`, `kernel32.dll`) and Win32 structures (`POINT`, `SIZE`, `BLENDFUNCTION`, `RECT`) are centralized in `NativeMethods`, eliminating duplicate P/Invoke definitions across UI components.

### 5. Atomic Configuration Persistence (`Settings`)
Configuration (`settings.ini`) uses an atomic write pattern: settings are serialized to a temporary file (`settings.ini.tmp`) before calling `File.Replace`, guaranteeing file integrity even during unexpected system shutdowns or power cuts.

### 6. Ambient Agent Session Watching & Multi-Agent HUD
Instead of guessing what an agent is doing from CPU load or window-title heuristics, iClock reads the agent's **own session log** and reconstructs its state from the real transcript. The old heuristics (cluster CPU thresholds, `RegisterShellHookWindow` / `HSHELL_FLASH` capture, toast-listener interception) are gone; what remains is a direct, protocol-aware read of what the agent actually wrote.

- **Process-Guided Lazy Watching**: watchers are mounted only for agents whose process is actually running. A directory for an idle agent is never opened, so no file handles and no CPU are spent on agents that are not working.
- **Watched Session Roots**: `~/.claude/projects`, `~/.codex/sessions`, `~/.gemini/antigravity/brain` (+ `~/.antigravity`), `~/.cursor/projects`, plus the `sessions` / `projects` / `logs` / `transcripts` subfolders of a generic agent root, and a `.claude` folder next to the executable.
- **Protocol-Aware Parsing (`AgentTranscriptParse`)**: each agent's log format has its own parser — `Cursor`, `Claude`, `Codex`, `Antigravity`, and a generic fallback. The parser recognizes the tool-call shapes each agent emits (`tool_calls`, Anthropic `content` blocks, Codex `function_call` / `item_completed` events, Antigravity planner/done records) and extracts the real command text, the assistant's reasoning, and the turn outcome.
- **Zero-Allocation Sniffing**: the hot path uses a hand-written JSON key-value sniffer instead of deserializing each line, so a long-running watch produces no garbage-collection pressure.
- **Strict Event Validation**: the event type is validated before any prompt or command text is pulled out, and a temporal-continuity intent extractor plus a cursor-based filter drop false positives — a tool call is never mistaken for "thinking finished".
- **Authentic Stream Only**: the HUD never invents filler text. Every line it shows came from the agent's own record, in order; a record with several commands shows all of them, not just the last.
- **Unified Multi-Agent HUD (`AgentHud`)**: one bar at the top of the desktop holds every live session. Pills sort by priority (waiting/error > running > done) and rotate on a fixed cadence; multiple sessions of the same agent get stable `#1` / `#2` labels derived from the lexicographic order of the session key. The bar is draggable in height, pinnable to stay resident, fades out on foreground change after a 5-second debounce, and draws a vertical dashed snap guide when dragged near the screen centre.

### 7. Pure In-Memory Audio Synthesis
Custom PCM audio synthesis generates classic retro sounds (QQ message 4-beep sequence, Pager 3-beep call) dynamically in memory with envelope smoothing, eliminating external sound asset files while ensuring consistent $\ge 3\text{s}$ alert durations.

### 8. Native IPC Message Pipeline & CLI Integration (`WM_COPYDATA`)
To enable terminal scripting and automation without external daemon services or sockets, iClock implements a zero-overhead Win32 inter-process messaging pipeline:
- **Single-Instance Mutex**: At startup, `Program.Main` acquires `System.Threading.Mutex("iClock.SingleInstance")`. If a primary instance already exists, the secondary invocation acts as a transient CLI client.
- **Message-Only Dispatch Window**: The running GUI instance maintains a lightweight message window (`MessageWindow`) with title `"iClock.IpcWindow"`.
- **Zero-Allocation Data Transfer**: Secondary CLI processes marshal arguments via Win32 `WM_COPYDATA` (`0x004A`) with `NativeMethods.COPYDATASTRUCT`. The message is delivered synchronously into the primary instance's message queue on the main UI thread with 0ms dispatch latency.
- **Console Attachment for GUI Binaries**: As a `/target:winexe` application, standard output is not attached to a console by default. `Program.WriteToConsole` probes `GetStdHandle(STD_OUTPUT_HANDLE)` and calls `AttachConsole(-1)` dynamically. This guarantees that CLI feedback, help text (`-h`), and pipe redirections (`|`, `>`) print correctly in interactive PowerShell, CMD, or headless automation runners without flashing unwanted console windows during standard GUI launches.
- **Companion Console Entry (`CliLauncher`)**: the release ships a tiny `iclock.com` console shim next to the GUI binary. Windows resolves `iclock.com` ahead of `iclock.exe`, so a bare `iclock` in a terminal launches the GUI when no arguments are given and otherwise forwards the raw argument string to `iClock.exe` synchronously, preserving quotes and the exit code. This keeps the shell prompt from ever ending up inverted.
- **Idempotent User PATH Registration**: `iclock --install-cli` queries and updates `Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User)` natively, providing global terminal invocation without `.exe` (via Windows `%PATHEXT%`).

### 9. Always-On Usage Tracking & Trend Analysis (`Usage`)
- **Process Names Only**: the tracker records the foreground process name and duration, never window titles, file paths, or any content. `UsageLog.IsIgnoredExe` keeps the shell and other noise out of the samples.
- **Person-Level Model**: each segment is classified `active`, `idle`, `locked`, or `sleep`. "Active" means the user was operating the machine; the other three are person states, not applications, so they never dilute the app ranking.
- **Input Strength**: samples count keyboard/mouse activity, splitting active time into heavy and light input so the summary reads as a person's day rather than a raw process ranking.
- **TSV Persistence**: segments are appended to a per-day TSV file, merged on consecutive entries, and short flicker windows are absorbed so a click does not become its own row.
- **Day-Axis Panel**: the history dialog's color axis shows the whole day at a glance; clicking it toggles between the activity window and the full day, with the window anchored to the day's first real record.
- **Trend Panel**: a second view charts screen-active vs. focus time per day over a selectable window (this week / last 30 days) and ranks the applications inside it.

### 10. Eye-Care Reminder (`EyeCare`)
- **Idle-Based Break Detection**: every 30 seconds the tracker reads the system's last-input timestamp. Three minutes without any input counts as a genuine break and resets the accumulated screen time.
- **No Double Counting**: the clock does not accumulate during a rest countdown, so a scheduled break never counts as screen time.
- **Context-Aware Suppression**: the reminder stays quiet while a full-screen app or the settings window is open, and fires once the user is back in an ordinary window.
- **Shared Notice Path**: the reminder reuses `NoticeDialog`, so every strong reminder in the app looks and behaves the same.
