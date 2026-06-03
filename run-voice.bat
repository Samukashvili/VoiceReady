@echo off
setlocal
cd /d "%~dp0"
if exist ".venv\Scripts\python.exe" (
  set "PATH=%CD%\.venv\Scripts;%PATH%"
)
dotnet run --project src\VoiceReady.Cli -- --voice %*
