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

The script compiles every `src/*.cs` module together as a Windows GUI executable and writes `dist/iClock.exe`. It also compiles the companion console shim `dist/iclock.com`, generates `dist/iclock.cmd`, and builds the test binaries `dist/SelfCheck.exe` and `dist/QuoteLayoutCheck.exe`. The output directory is ignored by Git. The executable targets the .NET Framework v4 runtime and references framework assemblies only, including `System.Windows.Forms.dll`, `System.Drawing.dll`, `System.Runtime.WindowsRuntime.dll`, `System.Core.dll`, and `System.Web.Extensions.dll`.

Equivalent compiler invocation:

```powershell
csc.exe /nologo /target:winexe /optimize+ /out:dist\iClock.exe /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Runtime.WindowsRuntime.dll /r:System.Web.Extensions.dll /resource:src\zan.jpg,zan.jpg /win32icon:src\app.ico src\*.cs
```

The companion console entry point is a separate, tiny compile:

```powershell
csc.exe /nologo /target:exe /optimize+ /out:dist\iclock.com src\CliLauncher.cs
```

## Release checklist

- Build from a clean checkout using `build.ps1`.
- Confirm the application starts, tray menu opens, and the configured hotkey can start and pause the countdown.
- Confirm the release executable is below the project's 1 MB hard limit (currently about 435 KB).
- Run `dist\SelfCheck.exe` and `dist\QuoteLayoutCheck.exe`; both must pass.
- Run `scripts/package.ps1` to stage the release assets. It reads the version from `CURRENT_VERSION` in `src/iClock.cs`, so the artifact names can never drift from the binary; attach the staged `.exe`, `.zip`, and `SHA256SUMS.txt` to the GitHub Release. Do not commit generated binaries to the source tree.
- Update `CHANGELOG.md` and tag the source commit used for the release.
