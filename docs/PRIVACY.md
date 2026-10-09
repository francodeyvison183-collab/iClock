# Privacy

iClock is designed to work locally.

- The application does not make network requests, collect telemetry, or require an account.
- Settings are stored in `%APPDATA%\iClock\settings.ini` for the current Windows user.
- Countdown history is stored in `%APPDATA%\iClock\history-YYYYMMDD.tsv` and contains session start time, end time, configured duration, and result.
- If startup is enabled, iClock creates or removes its value under the current user's `Software\Microsoft\Windows\CurrentVersion\Run` registry key. It does not configure startup for other users.
- Removing the application executable does not automatically remove settings, history, or the startup entry. Turn off startup from Settings and delete `%APPDATA%\iClock` yourself if you want to remove local data.

The repository does not include user settings or history files. Please do not attach them to public issues without reviewing and sanitizing their contents.
