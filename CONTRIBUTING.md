# Contributing to iClock

Thanks for helping improve iClock. This is a small Windows utility, so keep changes focused and avoid adding runtime dependencies without a clear need.

## Before you start

- Search existing issues before opening a duplicate.
- For a substantial change, open an issue first to discuss its scope.
- Bug reports should include Windows version, .NET Framework version if known, steps to reproduce, and expected versus actual behavior. Do not include private countdown history or other personal data.

## Pull requests

1. Fork the repository and create a focused branch.
2. Keep changes limited to the stated issue; update the relevant user or build documentation.
3. Build with `.\build.ps1` on Windows with the .NET Framework 4.x compiler available.
4. Describe the change, its user-visible impact, and how you built it in the pull request.
5. Do not commit `dist/`, private settings, personal history files, or unrelated binaries.

By submitting a contribution, you agree that it may be distributed under the repository's MIT License. You retain copyright in your contribution.

## Scope and style

- Preserve the small, dependency-free Windows implementation where practical.
- Keep UI strings available in both Chinese and English.
- Keep settings and history local; do not add telemetry or network access without explicit discussion.
- Use clear names and comments where behavior is not obvious.
