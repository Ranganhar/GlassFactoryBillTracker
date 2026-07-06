Param(
    [ValidateSet("win-x64", "win-arm64")]
    [string]$Runtime = "win-x64",
    [string]$DataDir = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)

$root = Split-Path -Parent $PSScriptRoot
$exePath = Join-Path $root "dist/$Runtime/GlassFactory.BillTracker.App.exe"

if (-not (Test-Path $exePath)) {
    throw "Publish artifact not found: $exePath. Run scripts/build_windows.ps1 first."
}

if ([string]::IsNullOrWhiteSpace($DataDir)) {
    $runId = "{0:yyyyMMdd-HHmmss}-{1}" -f (Get-Date), ([Guid]::NewGuid().ToString("N").Substring(0, 8))
    $DataDir = Join-Path $env:TEMP ("GlassFactoryBillTracker_PublishSmoke_{0}" -f $runId)
}

Write-Host "Detected EXE: $exePath" -ForegroundColor Green
Write-Host "Running DbSmokeTest, DataDir=$DataDir" -ForegroundColor Cyan

$smokeOutput = dotnet run --project (Join-Path $root "tools/GlassFactory.BillTracker.DbSmokeTest/GlassFactory.BillTracker.DbSmokeTest.csproj") -- $DataDir
$smokeOutput | ForEach-Object { Write-Host $_ }

if ($LASTEXITCODE -ne 0) {
    throw "DbSmokeTest failed."
}

if (-not ($smokeOutput -join "`n").Contains("SMOKE_TEST_PASS")) {
    throw "SMOKE_TEST_PASS marker was not found."
}

Write-Host "Publish smoke check passed." -ForegroundColor Green
