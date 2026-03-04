Param(
    [string]$Configuration = "Release"
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

function Invoke-Step {
    param(
        [string]$Name,
        [scriptblock]$Action
    )

    Write-Host "`n==> $Name" -ForegroundColor Cyan
    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "Step failed: $Name"
    }
}

function Get-Net48ReleaseValue {
    $paths = @(
        "HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\NET Framework Setup\NDP\v4\Full"
    )

    foreach ($path in $paths) {
        if (Test-Path $path) {
            try {
                $item = Get-ItemProperty -Path $path -Name Release -ErrorAction Stop
                if ($item.Release) {
                    return [int]$item.Release
                }
            }
            catch {
            }
        }
    }

    return 0
}

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src/GlassFactory.BillTracker.App.Win7/GlassFactory.BillTracker.App.Win7.csproj"
$distDir = Join-Path $root "dist/win7-x64"
$buildDir = Join-Path $root "artifacts/build/win7-x64"

$osVersion = [System.Environment]::OSVersion.Version
if ($osVersion.Major -ne 6 -or $osVersion.Minor -ne 1) {
    Write-Host "Warning: this script targets Windows 7 (6.1). Current OS version is $($osVersion.ToString())." -ForegroundColor Yellow
}

$net48Release = Get-Net48ReleaseValue
if ($net48Release -lt 528040) {
    throw "Required .NET Framework 4.8 Developer Pack not found. Install it from: https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48"
}

$msbuild = Get-Command msbuild.exe -ErrorAction SilentlyContinue
if (-not $msbuild) {
    throw "MSBuild not found. Install Visual Studio Build Tools 2019/2022 with .NET desktop build tools."
}

if (Test-Path $buildDir) {
    Remove-Item $buildDir -Recurse -Force
}
if (Test-Path $distDir) {
    Remove-Item $distDir -Recurse -Force
}

New-Item -ItemType Directory -Path $buildDir -Force | Out-Null
New-Item -ItemType Directory -Path $distDir -Force | Out-Null

Invoke-Step "MSBuild Restore" {
    msbuild.exe $project /t:Restore /p:Configuration=$Configuration /p:Platform="Any CPU"
}
Invoke-Step "MSBuild Rebuild" {
    msbuild.exe $project /t:Rebuild /p:Configuration=$Configuration /p:Platform="Any CPU" /p:OutDir="$buildDir\\"
}

Copy-Item -Path (Join-Path $buildDir "*") -Destination $distDir -Recurse -Force

$exePath = Join-Path $distDir "GlassFactory.BillTracker.App.Win7.exe"
if (-not (Test-Path $exePath)) {
    throw "Build completed, but Win7 executable was not found: $exePath"
}

$sizeMb = [Math]::Round(((Get-Item $exePath).Length / 1MB), 2)
Write-Host "`nWin7 build succeeded." -ForegroundColor Green
Write-Host "Configuration: $Configuration"
Write-Host "Dist Dir     : $distDir"
Write-Host "EXE Path     : $exePath"
Write-Host "EXE Size(MB) : $sizeMb"
