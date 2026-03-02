Param(
    [string]$DataDir = "",
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($DataDir)) {
    $DataDir = Join-Path $env:LOCALAPPDATA "GlassFactory.BillTracker"
}

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $root "artifacts/repro"
}

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$stagingDir = Join-Path $OutputDir "collect_$timestamp"
$zipPath = Join-Path $OutputDir "billtracker_logs_$timestamp.zip"

if (Test-Path $stagingDir) {
    Remove-Item $stagingDir -Recurse -Force
}
New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null

Write-Host "DataDir   : $DataDir" -ForegroundColor Cyan
Write-Host "OutputDir : $OutputDir" -ForegroundColor Cyan

if (-not (Test-Path $DataDir)) {
    throw "Data directory not found: $DataDir"
}

$pathsToCopy = @(
    "logs",
    "exports",
    "attachments",
    "billtracker.db"
)

foreach ($item in $pathsToCopy) {
    $source = Join-Path $DataDir $item
    if (Test-Path $source) {
        $dest = Join-Path $stagingDir $item
        Copy-Item -Path $source -Destination $dest -Recurse -Force
        Write-Host "Collected: $item" -ForegroundColor Green
    }
    else {
        Write-Host "Skipped (not found): $item" -ForegroundColor Yellow
    }
}

$envInfoPath = Join-Path $stagingDir "environment.txt"
$sysInfoPath = Join-Path $stagingDir "systeminfo.txt"

@(
    "Timestamp: $(Get-Date -Format o)",
    "Machine: $env:COMPUTERNAME",
    "User: $env:USERNAME",
    "OS: $([System.Environment]::OSVersion.VersionString)",
    "PowerShell: $($PSVersionTable.PSVersion)",
    "DataDir: $DataDir"
) | Set-Content -Path $envInfoPath -Encoding UTF8

try {
    systeminfo | Set-Content -Path $sysInfoPath -Encoding UTF8
}
catch {
    "systeminfo command failed: $($_.Exception.Message)" | Set-Content -Path $sysInfoPath -Encoding UTF8
}

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Compress-Archive -Path (Join-Path $stagingDir "*") -DestinationPath $zipPath -Force

Write-Host "Archive created: $zipPath" -ForegroundColor Green
Write-Host "Done." -ForegroundColor Green
