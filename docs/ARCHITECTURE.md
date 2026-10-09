# Architecture

iClock is a single C# source file compiled as a Windows GUI executable against the .NET Framework. It uses WinForms for the tray icon and settings/history dialogs, GDI+ for text rendering, and small Win32 interop calls for global hotkeys and click-through window styles.

## Runtime pieces

- `AppContext` owns the tray icon, global hotkey message window, 100 ms UI timer, monotonic countdown deadline, settings, and history writes. The countdown uses `Stopwatch` rather than wall-clock time; the overlay is repainted only when its displayed text changes.
- `Overlay` is a borderless, topmost form. Layered-window, tool-window, and transparent extended styles keep it out of the taskbar and allow click-through outside position mode.
- `SettingsDialog` edits the local INI-style settings file and registers the configured global hotkey.
- `ShowHistory` reads the current day's UTF-8 TSV file and displays its records.
- Startup registration uses the current user's `Run` registry key. No administrator rights are required.

The executable has no third-party package dependencies and performs no network activity. The small binary size depends on using the framework already installed on Windows; the framework runtime is not bundled in the executable.
