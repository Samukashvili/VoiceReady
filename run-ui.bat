@echo off
setlocal
cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo VoiceReady could not start because dotnet was not found.
    echo Install the .NET 10 SDK, then run this file again.
    echo.
    pause
    exit /b 1
)

if not exist "src\VoiceReady.App\VoiceReady.App.csproj" (
    echo VoiceReady could not find src\VoiceReady.App\VoiceReady.App.csproj.
    echo Make sure you extracted or cloned the full repository before running this file.
    echo.
    pause
    exit /b 1
)

dotnet run --project src\VoiceReady.App %*
set "VOICE_READY_EXIT=%ERRORLEVEL%"
if not "%VOICE_READY_EXIT%"=="0" (
    echo.
    echo VoiceReady exited with error code %VOICE_READY_EXIT%.
    echo If the error above mentions a missing SDK, install the .NET 10 SDK.
    echo.
    pause
)
exit /b %VOICE_READY_EXIT%
