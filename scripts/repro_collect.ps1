Param(
    [ValidateSet("win-x64", "win-arm64")]
    [string]$Runtime = "win-x64",
    [string]$DataDir = "",
    [string]$OutDir = ""
)

$ErrorActionPreference = "Stop"

function Ensure-Dir {
    param([string]$Path)
    if (-not (Test-Path $Path)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }
}

$root = Split-Path -Parent $PSScriptRoot
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"

if ([string]::IsNullOrWhiteSpace($DataDir)) {
    $preferred = "D:\BillTrackerRepro"
    if (Test-Path "D:\") {
        $DataDir = $preferred
    }
    else {
        $DataDir = Join-Path $env:TEMP "BillTrackerRepro"
    }
}
Ensure-Dir $DataDir

if ([string]::IsNullOrWhiteSpace($OutDir)) {
    $OutDir = Join-Path $root "artifacts/repro/$timestamp"
}
if (Test-Path $OutDir) {
    Remove-Item $OutDir -Recurse -Force
}
Ensure-Dir $OutDir

$exePath = Join-Path $root "dist/$Runtime/GlassFactory.BillTracker.App.exe"
if (-not (Test-Path $exePath)) {
    throw "未找到 EXE：$exePath，请先运行 scripts/build_windows.ps1"
}

# Pre-write settings to reduce manual interaction
$settingsDir = Join-Path $env:APPDATA "GlassFactoryBillTracker"
Ensure-Dir $settingsDir
$settingsPath = Join-Path $settingsDir "settings.json"
@{
    DataDirectory = $DataDir
} | ConvertTo-Json | Set-Content -Path $settingsPath -Encoding UTF8

$envFile = Join-Path $OutDir "env.txt"
$smokeOut = Join-Path $OutDir "smoke_check.txt"
$manualRunOut = Join-Path $OutDir "manual_run.txt"
$forcedCrashOut = Join-Path $OutDir "forced_crash.txt"
$forcedCrashLogDump = Join-Path $OutDir "forced_crash_crashlog.txt"

"Timestamp=$timestamp" | Set-Content $envFile
"OS=$([System.Environment]::OSVersion.VersionString)" | Add-Content $envFile
"DotnetVersion=$(& dotnet --version 2>$null)" | Add-Content $envFile
"ProcessArchitecture=$([System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture)" | Add-Content $envFile
"OSArchitecture=$([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture)" | Add-Content $envFile
"Timezone=$((Get-TimeZone).Id)" | Add-Content $envFile
"Runtime=$Runtime" | Add-Content $envFile
"DataDir=$DataDir" | Add-Content $envFile
"ExePath=$exePath" | Add-Content $envFile

try {
    $commit = (& git -C $root rev-parse HEAD 2>$null).Trim()
    if ([string]::IsNullOrWhiteSpace($commit)) { $commit = "N/A" }
    "Commit=$commit" | Add-Content $envFile
}
catch {
    "Commit=N/A" | Add-Content $envFile
}

Copy-Item $settingsPath (Join-Path $OutDir "settings.json.copy") -Force

# Run smoke check
$smokeScript = Join-Path $root "scripts/publish_smoke_check.ps1"
& powershell -ExecutionPolicy Bypass -File $smokeScript -Runtime $Runtime -DataDir $DataDir *>&1 | Tee-Object -FilePath $smokeOut

# Manual repro round
Write-Host "`n=== 手动复现步骤 ===" -ForegroundColor Yellow
Write-Host "1) 即将启动应用，请在主界面按 Ctrl+N（或点新建订单）" -ForegroundColor Yellow
Write-Host "2) 若出现卡死/秒退，请关闭窗口或等待退出" -ForegroundColor Yellow
Write-Host "3) 完成后回到此窗口按回车继续收集" -ForegroundColor Yellow

$proc = Start-Process -FilePath $exePath -PassThru
"ManualRunPID=$($proc.Id)" | Set-Content $manualRunOut
Read-Host "完成手动复现后按回车"

try {
    if (-not $proc.HasExited) {
        Write-Host "进程仍在运行。若程序卡死请先在任务管理器结束，再按回车。" -ForegroundColor Yellow
        Read-Host "结束进程后按回车"
        $proc.Refresh()
        if (-not $proc.HasExited) {
            Stop-Process -Id $proc.Id -Force
            Start-Sleep -Milliseconds 500
        }
    }
}
catch {
}

$proc.Refresh()
"ManualRunExitCode=$($proc.ExitCode)" | Add-Content $manualRunOut

# Forced crash round
Write-Host "`n=== 执行 --force-crash 验证 crash.log ===" -ForegroundColor Yellow
$forceProc = Start-Process -FilePath $exePath -ArgumentList "--force-crash" -PassThru
$forceProc.WaitForExit()
"ForcedCrashPID=$($forceProc.Id)" | Set-Content $forcedCrashOut
"ForcedCrashExitCode=$($forceProc.ExitCode)" | Add-Content $forcedCrashOut

# Collect logs and events
$collectScript = Join-Path $root "scripts/collect_logs.ps1"
& powershell -ExecutionPolicy Bypass -File $collectScript -DataDir $DataDir -OutDir $OutDir | Out-Null

$tempCrash = Join-Path $env:TEMP "GlassFactoryBillTracker/logs/crash.log"
$dataCrash = Join-Path $DataDir "logs/crash.log"
"" | Set-Content $forcedCrashLogDump
if (Test-Path $tempCrash) {
    Add-Content -Path $forcedCrashLogDump -Value "===== $tempCrash ====="
    Get-Content -Path $tempCrash -Raw | Add-Content -Path $forcedCrashLogDump
}
if (Test-Path $dataCrash) {
    Add-Content -Path $forcedCrashLogDump -Value "`n===== $dataCrash ====="
    Get-Content -Path $dataCrash -Raw | Add-Content -Path $forcedCrashLogDump
}

# Zip bundle
$zipPath = Join-Path $root "artifacts/repro/repro_bundle_$timestamp.zip"
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}
Compress-Archive -Path (Join-Path $OutDir "*") -DestinationPath $zipPath -CompressionLevel Optimal

Write-Host "`n诊断包已生成：$zipPath" -ForegroundColor Green
Write-Host "请把该 zip 发给开发者进行精准定位。" -ForegroundColor Green
