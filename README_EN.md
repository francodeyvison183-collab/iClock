# iClock

[简体中文](README.md) · [GitHub upload guide](GITHUB_UPLOAD.md) · [User guide](docs/USER_GUIDE.md) · [Build guide](docs/BUILDING.md) · [Release guide](docs/RELEASING.md) · [Changelog](CHANGELOG.md)

iClock is a lightweight Windows desktop countdown. It displays transparent, always-on-top text and supports click-through, global hotkeys, tray controls, and startup at sign-in.

## Features

- Transparent, topmost desktop text with mouse click-through during normal use.
- Configurable duration, text color, and size; `HH:MM:SS`, `MM:SS`, and Chinese-unit formats.
- A tray command enables position mode, where a translucent area can be dragged.
- A configurable global start/pause hotkey; default: `Ctrl+Alt+Space`.
- Optional system sound and customizable top-most prompt requiring confirmation when the countdown ends.
- Daily history for completed, reset, and interrupted countdowns.
- A tray language picker for Simplified Chinese or English (default: Simplified Chinese); optional per-user startup.
- A single executable; the current build is about 27 KB and has no installer.

## Requirements

- Windows 10 or Windows 11.
- .NET Framework 4.x. iClock uses Windows WinForms, GDI+, and Win32 APIs, with no third-party NuGet packages.

## Download and build

**For regular users:** [Download the Windows release](../../releases/latest). Get `iClock-1.0.0-windows.zip`, extract it, and run `iClock.exe`; no build or installer is needed. For the first release, the maintainer must upload the provided ZIP as a GitHub Release asset.

iClock requires Windows and .NET Framework 4.x; the package does not bundle an installer or runtime. To build from source, use `build.ps1`; it writes `dist/iClock.exe`. See the [build guide](docs/BUILDING.md).

```powershell
.\build.ps1
```

At startup, the overlay is hidden before countdown starts. Press the hotkey to start. When the countdown ends, the text hides automatically and a top-most notification appears. Right-click the tray icon to open settings, view today's history, switch languages, or exit.

## Data and privacy

Settings and countdown history are stored locally under `%APPDATA%\iClock`. The app makes no network requests and collects no telemetry. Enabling startup changes only the current user's Windows `Run` registry key. See [Privacy](docs/PRIVACY.md).

## Contributing

Issues and improvements are welcome. Please read [CONTRIBUTING.md](CONTRIBUTING.md) and the [Code of Conduct](CODE_OF_CONDUCT.md). Report security concerns privately as described in [SECURITY.md](SECURITY.md).

## License

MIT License. See [LICENSE](LICENSE).
