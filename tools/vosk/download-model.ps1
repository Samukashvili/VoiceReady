$ErrorActionPreference = "Stop"

$modelName = "vosk-model-small-en-us-0.15"
$toolsRoot = Split-Path -Parent $PSScriptRoot
$modelsRoot = Join-Path $PSScriptRoot "models"
$modelPath = Join-Path $modelsRoot $modelName
$archivePath = Join-Path $toolsRoot "$modelName.zip"
$downloadUrl = "https://alphacephei.com/vosk/models/$modelName.zip"

if (Test-Path (Join-Path $modelPath "conf\model.conf")) {
    Write-Host "Local Vosk model already exists at $modelPath"
    exit 0
}

New-Item -ItemType Directory -Path $modelsRoot -Force | Out-Null

try {
    Write-Host "Downloading $downloadUrl"
    Invoke-WebRequest -Uri $downloadUrl -OutFile $archivePath
    Expand-Archive -LiteralPath $archivePath -DestinationPath $modelsRoot -Force
}
finally {
    Remove-Item -LiteralPath $archivePath -Force -ErrorAction SilentlyContinue
}

if (-not (Test-Path (Join-Path $modelPath "conf\model.conf"))) {
    throw "Vosk model extraction did not produce the expected model directory: $modelPath"
}

Write-Host "Installed local Vosk model at $modelPath"
