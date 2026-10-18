# Architecture

iClock is an ultra-lightweight Windows desktop utility compiled directly via `csc.exe` against the .NET Framework (4.6+ / 4.8). It has zero external package dependencies, produces a single compact binary (~273 KB), and runs with minimal resource usage (< 15 MB RAM, ~0.0% idle CPU).

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

### 6. Background Agent Monitoring & Multi-Process Aggregation
Modern AI coding assistants (e.g., Antigravity, Cursor, Windsurf, Trae) employ multi-process Chromium/Electron architectures where work is distributed across UI, GPU, utility, and renderer worker processes:
- **Group Aggregation by Agent**: Rather than tracking isolated PIDs, iClock clusters processes by agent application identity (`AgentTrack`). Processor time is aggregated across all worker PIDs to detect sustained CPU bursts ($\ge 0.40\text{s}$ cluster CPU time or $\ge 8\%$ total system CPU).
- **Desktop Window Resolution**: Electron child processes often report `MainWindowHandle == 0`. iClock enumerates top-level visible desktop windows via `EnumDesktopWindows` / `EnumWindows` matching active agent PIDs to reliably locate the primary interactive GUI window handle (`HWND`).
- **Foreground Context & Completion Lifecycle**: Foreground status is resolved by comparing the foreground window and thread process ID against the agent's clustered PIDs. When a fallback agent transitions from sustained high workload ($\ge 5$ consecutive busy ticks, filtering out transient spikes like linting or auto-save) to a stable quiet state in the background ($\ge 6$ consecutive quiet ticks, avoiding premature triggers during multi-step tool calls or LLM pauses), iClock dispatches completion alerts with 30s cooldown and one-click window restoration (`ShowWindow` + `SetForegroundWindow`).
- **Window Title Dynamics & Instant Completion**: iClock inspects top-level session window title strings (`GetAgentWindowTitle`, `IsBusyAgentTitle`). For IDEs and terminal agents that expose status badges or markers (e.g., `[Running]`, `(Running)`, `●`, `Thinking...`, `[Building]`), the moment the title reverts back to normal, iClock immediately triggers completion without waiting for the quiet debounce period, providing 0-latency alerts.
- **Windows Toast Notification Listener (Zero False Positives & Task Context)**: For agents supporting Windows system toast notifications (such as Antigravity and Cursor), iClock listens to incoming notifications via Windows Runtime `UserNotificationListener` dynamically. When a task completion toast arrives, iClock extracts the agent identity and exact task summary text, immediately triggering a zero-latency alert with rich task context while completely bypassing noisy CPU load heuristics to achieve 100% precision and zero false positives.
- **Win32 Shell Hook Taskbar Flashing (`RegisterShellHookWindow` + `HSHELL_FLASH`)**: In addition to Toast notifications, iClock registers a native Win32 Shell hook via `RegisterShellHookWindow`. When background IDEs or terminals (such as Cursor, Windsurf, VS Code, Windows Terminal, JetBrains) complete a task and flash their taskbar button (`FlashWindowEx` / `HSHELL_FLASH`), iClock captures the message via its message pump with 0ms latency and 0.0% CPU overhead, instantly verifying the window's agent identity and routing to completion alerts while suppressing duplicate alerts via a 30s cooldown.
- **Kernel Handle Safety**: Every process handle query is strictly scoped (`using (Process)`), ensuring non-managed OS process handles (`SafeProcessHandle`) are immediately released to eliminate kernel resource leaks.

### 7. Pure In-Memory Audio Synthesis
Custom PCM audio synthesis generates classic retro sounds (QQ message 4-beep sequence, Pager 3-beep call) dynamically in memory with envelope smoothing, eliminating external sound asset files while ensuring consistent $\ge 3\text{s}$ alert durations.

### 8. Native IPC Message Pipeline & CLI Integration (`WM_COPYDATA`)
To enable terminal scripting and automation without external daemon services or sockets, iClock implements a zero-overhead Win32 inter-process messaging pipeline:
- **Single-Instance Mutex**: At startup, `Program.Main` acquires `System.Threading.Mutex("iClock.SingleInstance")`. If a primary instance already exists, the secondary invocation acts as a transient CLI client.
- **Message-Only Dispatch Window**: The running GUI instance maintains a lightweight message window (`MessageWindow`) with title `"iClock.IpcWindow"`.
- **Zero-Allocation Data Transfer**: Secondary CLI processes marshal arguments via Win32 `WM_COPYDATA` (`0x004A`) with `NativeMethods.COPYDATASTRUCT`. The message is delivered synchronously into the primary instance's message queue on the main UI thread with 0ms dispatch latency.
- **Console Attachment for GUI Binaries**: As a `/target:winexe` application, standard output is not attached to a console by default. `Program.WriteToConsole` probes `GetStdHandle(STD_OUTPUT_HANDLE)` and calls `AttachConsole(-1)` dynamically. This guarantees that CLI feedback, help text (`-h`), and pipe redirections (`|`, `>`) print correctly in interactive PowerShell, CMD, or headless automation runners without flashing unwanted console windows during standard GUI launches.
- **Idempotent User PATH Registration**: `iclock --install-cli` queries and updates `Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User)` natively, providing global terminal invocation without `.exe` (via Windows `%PATHEXT%`).

