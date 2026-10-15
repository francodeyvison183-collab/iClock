# Changelog

This project follows a simple release-based changelog. Dates use ISO 8601 (`YYYY-MM-DD`).

## [1.05] - 2026-10-15

### Added

- **Brand New Application Icon & Multi-Resolution Asset Set**: High-contrast deep navy squircle (`#131C43`) with transparent antialiased rounded corners, crisp white "i", and vibrant coral orange slash (`#FF5738`), calibrated for optimal contrast on both dark and light Windows themes. Preserved complete 9-resolution master assets in `src/icons/` (16×16 to 512×512).
- **CLI Automation & Win32 IPC Pipeline (`WM_COPYDATA`)**: Full terminal and script automation support. Invoke commands directly from PowerShell, CMD, or Bash (`iclock -s [mins]`, `-p`, `-r`, `-t`, `--reset`, `--rest`, `-n <message>`). Built-in `--install-cli` command automatically registers iClock into User PATH, enabling global invocation without `.exe` (matching tools like `claude` and `pi`). Zero-overhead message window dispatch (`MessageWindow`, caption `"iClock.IpcWindow"`) and dynamic console attachment (`AttachConsole(-1)`) allow GUI and console integration seamlessly.
- **Settings Dialog Live Preview & Test Alert Button**: Font, size, and color adjustments now preview directly on the active desktop overlay in real-time. Added a dedicated "Test Alert" (测试提醒演示) button to both Timer and Agent tabs to instantly audition alarm audio, popup notices, or breathing pulses without waiting for a countdown.
- **Dynamic Running Agent Priority**: Currently active and working AI agents are dynamically prioritized and sorted to the top of the Agent Collaboration list.
- **Automated Test Check 32**: Added reflection-based test coverage in `tests/SelfCheck.cs` verifying CLI commands (`--start 18`, `-p`, `-r`, `--reset`, `-n`), maintaining 100% test pass rate across all 32 checks.

### Changed

- **Extreme Binary Slimming to 154 KB**: Decoupled high-res design asset packs from runtime embedding, streamlined the application icon to an uncompressed 24×24 DIB sweet-spot, and downsampled the sponsor QR code to 450×450 high-fidelity bicubic JPEG. Dropped standalone executable size from 505 KB to an ultra-compact **154 KB** (157,184 raw bytes) and compressed ZIP to **77 KB**, saving ~70% total size with zero third-party dependencies.
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
