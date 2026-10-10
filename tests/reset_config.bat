@echo off
set "PS=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
if not exist "%PS%" set "PS=powershell"
"%PS%" -NoProfile -ExecutionPolicy Bypass -File "%~dp0reset_config.ps1"
