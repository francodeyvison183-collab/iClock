# GitHub Release assets

Upload these two files to the corresponding GitHub Release:

- `iClock-1.01-windows.exe` — standalone executable.
- `iClock-1.01-windows.zip` — application portable package.
- `iClock-1.01-SHA256SUMS.txt` — SHA-256 checksums.

These generated assets are ignored by Git so binaries do not enter the source history. Publish the ZIP package so users receive the application, quick-start guide, and license together. Do not upload the standalone `.exe` sidecar; the ZIP is the supported download package.
