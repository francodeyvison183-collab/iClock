# Releasing iClock

1. Choose a version using `MAJOR.MINOR.PATCH` and update `CHANGELOG.md` with the release date and user-visible changes.
2. Build from a clean checkout on Windows with `.\build.ps1`.
3. Confirm the application starts, the tray menu opens, the configured hotkey starts and pauses a countdown, and the settings/history dialogs open.
4. Confirm the resulting `dist/iClock.exe` is under 1 MB. Record its SHA-256 hash with `Get-FileHash dist\iClock.exe -Algorithm SHA256`.
5. Commit the release notes, create an annotated tag such as `v1.0.1`, and push the tag.
6. Create a GitHub Release for that tag. Attach the user-friendly `iClock-1.0.0-windows.zip` package (update the version in later filenames) and the SHA-256 sums file. The ZIP includes the executable and its license. Include the hash in the release notes and summarize changes from `CHANGELOG.md`.
7. Keep generated executables out of the source branch; distribute binaries as release assets.

Before publishing the first release, enable private vulnerability reporting in the repository's GitHub Security settings if that feature is available.
