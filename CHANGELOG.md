# Changelog

This project follows a simple release-based changelog. Dates use ISO 8601 (`YYYY-MM-DD`).

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
