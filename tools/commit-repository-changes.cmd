@echo off
powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "%~dp0commit-repository-changes.ps1" %*
exit /b %ERRORLEVEL%
