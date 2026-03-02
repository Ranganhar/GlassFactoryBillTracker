Param(
    [ValidateSet("win-x64", "win-arm64")]
    [string]$Runtime = "win-x64",
    [string]$DataDir = ""
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$exePath = Join-Path $root "dist/$Runtime/GlassFactory.BillTracker.App.exe"

if (-not (Test-Path $exePath)) {
    throw "未找到发布产物：$exePath。请先执行 scripts/build_windows.ps1。"
}

if ([string]::IsNullOrWhiteSpace($DataDir)) {
    $DataDir = Join-Path $env:TEMP "GlassFactoryBillTracker_PublishSmoke"
}

Write-Host "检测到 EXE: $exePath" -ForegroundColor Green
Write-Host "执行 DbSmokeTest, DataDir=$DataDir" -ForegroundColor Cyan

$smokeOutput = dotnet run --project (Join-Path $root "tools/GlassFactory.BillTracker.DbSmokeTest/GlassFactory.BillTracker.DbSmokeTest.csproj") -- $DataDir
$smokeOutput | ForEach-Object { Write-Host $_ }

if ($LASTEXITCODE -ne 0) {
    throw "DbSmokeTest 执行失败。"
}

if (-not ($smokeOutput -join "`n").Contains("SMOKE_TEST_PASS")) {
    throw "未检测到 SMOKE_TEST_PASS。"
}

Write-Host "Publish smoke check passed." -ForegroundColor Green
