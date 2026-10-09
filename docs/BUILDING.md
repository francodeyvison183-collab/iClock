# Building iClock

## Requirements

- Windows with .NET Framework 4.x compiler tools available.
- PowerShell 5.1 or later.
- No NuGet restore or third-party package download is required.

The build script looks for `csc.exe` under `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319` and then `%WINDIR%\Microsoft.NET\Framework\v4.0.30319`.

## Build

From the repository root in PowerShell:

```powershell
.\build.ps1
```

The script compiles `src/iClock.cs` as a Windows GUI executable and writes `dist/iClock.exe`. The output directory is ignored by Git. The executable targets the .NET Framework v4 runtime and references only `System.Windows.Forms.dll` and `System.Drawing.dll` from the framework.

Equivalent compiler invocation:

```powershell
csc.exe /nologo /target:winexe /optimize+ /out:dist\iClock.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /resource:src\zan.jpg,zan.jpg /win32icon:src\app.ico src\iClock.cs
```

## Release checklist

- Build from a clean checkout using `build.ps1`.
- Confirm the application starts, tray menu opens, and the configured hotkey can start and pause the countdown.
- Confirm the release executable is below the project's 1 MB target.
- Attach `dist/iClock.exe` to a GitHub Release; do not commit generated binaries to the source tree.
- Update `CHANGELOG.md` and tag the source commit used for the release.
