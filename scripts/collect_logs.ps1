Param(
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

function Append-FileIfExists {
    param(
        [string]$Source,
        [string]$Destination
    )

    if (Test-Path $Source) {
        Add-Content -Path $Destination -Value ("`n===== FILE: " + $Source + " =====`n")
        Get-Content -Path $Source -Raw | Add-Content -Path $Destination
    }
}

$root = Split-Path -Parent $PSScriptRoot
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
if ([string]::IsNullOrWhiteSpace($OutDir)) {
    $OutDir = Join-Path $root "artifacts/repro/$timestamp"
}
Ensure-Dir $OutDir

$envFile = Join-Path $OutDir "env.txt"
$settingsCopyPath = Join-Path $OutDir "settings.json.copy"
$settingsMeta = Join-Path $OutDir "settings_path.txt"
$appLogDump = Join-Path $OutDir "app_logs_combined.txt"
$crashLogDump = Join-Path $OutDir "crash_logs_combined.txt"
$eventLogDump = Join-Path $OutDir "eventlog_application.txt"

$settingsPath = Join-Path $env:APPDATA "GlassFactoryBillTracker/settings.json"
$emergencyLogDir = Join-Path $env:TEMP "GlassFactoryBillTracker/logs"

if ([string]::IsNullOrWhiteSpace($DataDir)) {
    if (Test-Path $settingsPath) {
        try {
            $settings = Get-Content -Path $settingsPath -Raw | ConvertFrom-Json
            if ($settings -and $settings.DataDirectory) {
                $DataDir = $settings.DataDirectory
            }
        }
        catch {
        }
    }
}

"Timestamp=$timestamp" | Set-Content $envFile
"OS=$([System.Environment]::OSVersion.VersionString)" | Add-Content $envFile
"DotnetVersion=$(& dotnet --version 2>$null)" | Add-Content $envFile
"ProcessArchitecture=$([System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture)" | Add-Content $envFile
"OSArchitecture=$([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture)" | Add-Content $envFile
"Timezone=$((Get-TimeZone).Id)" | Add-Content $envFile
"DataDir=$DataDir" | Add-Content $envFile

try {
    $commit = (& git -C $root rev-parse HEAD 2>$null).Trim()
    if ([string]::IsNullOrWhiteSpace($commit)) { $commit = "N/A" }
    "Commit=$commit" | Add-Content $envFile
}
catch {
    "Commit=N/A" | Add-Content $envFile
}

"SettingsPath=$settingsPath" | Set-Content $settingsMeta
if (Test-Path $settingsPath) {
    Copy-Item $settingsPath $settingsCopyPath -Force
}

# Collect app logs
"" | Set-Content $appLogDump
"" | Set-Content $crashLogDump

Get-ChildItem -Path $emergencyLogDir -Filter "app*.log" -File -ErrorAction SilentlyContinue |
    ForEach-Object { Append-FileIfExists -Source $_.FullName -Destination $appLogDump }
Get-ChildItem -Path $emergencyLogDir -Filter "crash*.log" -File -ErrorAction SilentlyContinue |
    ForEach-Object { Append-FileIfExists -Source $_.FullName -Destination $crashLogDump }

if (-not [string]::IsNullOrWhiteSpace($DataDir)) {
    $dataLogDir = Join-Path $DataDir "logs"

    Get-ChildItem -Path $dataLogDir -Filter "app*.log" -File -ErrorAction SilentlyContinue |
        ForEach-Object { Append-FileIfExists -Source $_.FullName -Destination $appLogDump }
    Get-ChildItem -Path $dataLogDir -Filter "crash*.log" -File -ErrorAction SilentlyContinue |
        ForEach-Object { Append-FileIfExists -Source $_.FullName -Destination $crashLogDump }
}

# Collect event logs
$events = Get-WinEvent -LogName Application -MaxEvents 200 -ErrorAction SilentlyContinue |
    Where-Object {
        $_.ProviderName -match "\.NET Runtime|Application Error" -or
        $_.Message -match "GlassFactory\.BillTracker\.App|FORCED_CRASH_TEST"
    } |
    Select-Object -First 50 TimeCreated, Id, LevelDisplayName, ProviderName, Message

$events | Format-List | Out-File -FilePath $eventLogDump -Encoding UTF8

Write-Host "收集完成：$OutDir" -ForegroundColor Green
