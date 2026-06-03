@echo off
setlocal
cd /d "%~dp0"

where python >nul 2>nul
if errorlevel 1 (
  echo Python was not found on PATH.
  echo Install Python 3.10+ from https://www.python.org/downloads/ and enable "Add python.exe to PATH".
  exit /b 1
)

if not exist ".venv\Scripts\python.exe" (
  echo Creating local Python virtual environment...
  python -m venv .venv
  if errorlevel 1 exit /b 1
)

echo Upgrading pip...
".venv\Scripts\python.exe" -m pip install --upgrade pip
if errorlevel 1 exit /b 1

echo Installing faster-whisper dependencies...
".venv\Scripts\python.exe" -m pip install -r tools\faster-whisper\requirements.txt
if errorlevel 1 exit /b 1

if not exist "tools\faster-whisper\models\base.en\model.bin" (
  echo Downloading faster-whisper base.en model...
  ".venv\Scripts\python.exe" tools\faster-whisper\download_model.py --repo-id Systran/faster-whisper-base.en --output tools\faster-whisper\models\base.en
  if errorlevel 1 exit /b 1
) else (
  echo faster-whisper base.en model already exists.
)

echo.
echo Dependencies installed successfully.
echo You can now run voice mode with run-voice.bat.
