Param(
    [ValidateSet("win-x64", "win-arm64")]
    [string]$Runtime = "win-x64",
    [string]$DataDir = "",
    [string]$OutputDir = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)

$root = Split-Path -Parent $PSScriptRoot
$exePath = Join-Path $root "dist/$Runtime/GlassFactory.BillTracker.App.exe"
$collectScript = Join-Path $PSScriptRoot "collect_logs.ps1"

if ([string]::IsNullOrWhiteSpace($DataDir)) {
    $DataDir = Join-Path $env:LOCALAPPDATA "GlassFactory.BillTracker"
}

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $root "artifacts/repro"
}

if (-not (Test-Path $exePath)) {
    throw "Executable not found: $exePath. Run scripts/build_windows.ps1 first."
}

if (-not (Test-Path $collectScript)) {
    throw "Script not found: $collectScript"
}

Write-Host "Step 1/3: Launch app" -ForegroundColor Cyan
Write-Host "EXE: $exePath" -ForegroundColor Cyan

$proc = Start-Process -FilePath $exePath -PassThru

Write-Host "Step 2/3: Reproduce the issue in the app window." -ForegroundColor Yellow
Write-Host "After reproducing, return here and press Enter to continue..." -ForegroundColor Yellow
Read-Host | Out-Null

Write-Host "Step 3/3: Stop app and collect logs" -ForegroundColor Cyan

try {
    if (-not $proc.HasExited) {
        Stop-Process -Id $proc.Id -Force
        Start-Sleep -Milliseconds 500
    }
}
catch {
    Write-Host "Warning: failed to stop app process cleanly: $($_.Exception.Message)" -ForegroundColor Yellow
}

& $collectScript -DataDir $DataDir -OutputDir $OutputDir

Write-Host "Repro package generation completed." -ForegroundColor Green
Write-Host "Please send the generated zip file to developers for analysis." -ForegroundColor Green
