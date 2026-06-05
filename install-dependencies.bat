@echo off
setlocal
cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
  echo .NET SDK was not found on PATH.
  echo Install the .NET 10 SDK, then run this installer again.
  exit /b 1
)

echo Restoring .NET and local Vosk dependencies...
dotnet restore VoiceReady.slnx
if errorlevel 1 exit /b 1

if not exist "tools\vosk\models\vosk-model-small-en-us-0.15\conf\model.conf" (
  echo Downloading the local Vosk English model...
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\vosk\download-model.ps1
  if errorlevel 1 exit /b 1
) else (
  echo Local Vosk model already exists.
)

echo.
echo Dependencies installed successfully.
echo You can now run voice mode with run-voice.bat.
