Param(
    [string]$Configuration = "Release"
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

trap {
    Write-Host ""
    Write-Host "FATAL: build_windows7.ps1 terminated with an unhandled error." -ForegroundColor Red
    if ($_.Exception) {
        Write-Host $_.Exception.ToString() -ForegroundColor Red
    }
    else {
        Write-Host $_ -ForegroundColor Red
    }
    exit 1
}

function Invoke-Step {
    param(
        [string]$Name,
        [scriptblock]$Action
    )

    Write-Host ""
    Write-Host "==> $Name" -ForegroundColor Cyan
    try {
        & $Action
        Write-Host "<== $Name succeeded" -ForegroundColor Green
    }
    catch {
        Write-Host "<== $Name failed" -ForegroundColor Red
        throw
    }
}

function Invoke-ExternalCommand {
    param(
        [string]$FilePath,
        [string[]]$Arguments,
        [string]$DisplayName
    )

    if ([string]::IsNullOrWhiteSpace($DisplayName)) {
        $DisplayName = $FilePath
    }

    $argumentText = ""
    if ($Arguments -and $Arguments.Length -gt 0) {
        $argumentText = " " + ($Arguments -join " ")
    }

    Write-Host "Running: $DisplayName$argumentText" -ForegroundColor DarkGray
    & $FilePath @Arguments 2>&1 | ForEach-Object { Write-Host $_ }

    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        throw "$DisplayName failed with exit code $exitCode."
    }
}

function Get-TargetFrameworkText {
    param(
        [string]$ProjectFile
    )

    if (-not (Test-Path $ProjectFile)) {
        throw "Project file not found: $ProjectFile"
    }

    $xmlText = Get-Content -Path $ProjectFile | Out-String

    if ($xmlText -match "<TargetFrameworks>([^<]+)</TargetFrameworks>") {
        return $matches[1]
    }

    if ($xmlText -match "<TargetFramework>([^<]+)</TargetFramework>") {
        return $matches[1]
    }

    return "unknown"
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

$scriptPath = $MyInvocation.MyCommand.Path
$scriptName = Split-Path -Leaf $scriptPath
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
$cwd = [Environment]::CurrentDirectory
$osVersion = [System.Environment]::OSVersion.Version

$psVersionText = "unknown"
if ($PSVersionTable -and $PSVersionTable.PSVersion) {
    $psVersionText = $PSVersionTable.PSVersion.ToString()
}
elseif ($host -and $host.Version) {
    $psVersionText = $host.Version.ToString()
}

Write-Host "============================================================" -ForegroundColor Yellow
Write-Host "build_windows7.ps1 started" -ForegroundColor Yellow
Write-Host "Script    : $scriptName"
Write-Host "Timestamp : $timestamp"
Write-Host "PowerShell: $psVersionText"
Write-Host "OS        : $($osVersion.ToString())"
Write-Host "CWD       : $cwd"
Write-Host "============================================================" -ForegroundColor Yellow

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src/GlassFactory.BillTracker.App.Win7/GlassFactory.BillTracker.App.Win7.csproj"
$distDir = Join-Path $root "dist/win7-x64"
$buildDir = Join-Path $root "artifacts/build/win7-x64"

Write-Host "Project   : $project"
Write-Host "Build Dir : $buildDir"
Write-Host "Dist Dir  : $distDir"

$dotnetCommand = $null
$dotnetSdkVersion = "unknown"
$dotnetSdkMajor = 0

Invoke-Step "Detect dotnet" {
    $script:dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $script:dotnetCommand) {
        Write-Host "dotnet command was not found in PATH." -ForegroundColor Red
        Write-Host "Install .NET SDK and ensure dotnet is available from command line." -ForegroundColor Yellow
        Write-Host "Download: https://dotnet.microsoft.com/download" -ForegroundColor Yellow
        exit 2
    }

    Write-Host "dotnet path: $($script:dotnetCommand.Source)"
}

Invoke-Step "Print dotnet info" {
    Invoke-ExternalCommand -FilePath $dotnetCommand.Source -Arguments @("--info") -DisplayName "dotnet"
}

Invoke-Step "Read dotnet SDK version" {
    $versionOutput = & $dotnetCommand.Source --version 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet --version failed with exit code $LASTEXITCODE."
    }

    $script:dotnetSdkVersion = $versionOutput.Trim()
    Write-Host "dotnet SDK version: $dotnetSdkVersion"

    try {
        $script:dotnetSdkMajor = [int]($dotnetSdkVersion.Split('.')[0])
    }
    catch {
        $script:dotnetSdkMajor = 0
    }

    Write-Host "dotnet SDK major: $dotnetSdkMajor"
}

$targetFrameworkText = "unknown"
Invoke-Step "Read project target framework" {
    $script:targetFrameworkText = Get-TargetFrameworkText -ProjectFile $project
    Write-Host "TargetFramework value: $targetFrameworkText"
}

$isWindows7 = ($osVersion.Major -eq 6 -and $osVersion.Minor -eq 1)
if ($isWindows7) {
    Write-Host "Detected Windows 7 host." -ForegroundColor Green
}
else {
    Write-Host "Warning: this script is intended for Windows 7 (6.1). Current OS is $($osVersion.ToString())." -ForegroundColor Yellow
}

$isModernDotnetTarget = $false
if ($targetFrameworkText -match "net[5-9]" -or $targetFrameworkText -match "netcoreapp") {
    $isModernDotnetTarget = $true
}

if ($isWindows7 -and $isModernDotnetTarget) {
    Write-Host "Current project target is not supported on Windows 7: $targetFrameworkText" -ForegroundColor Red
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "1) Recommended: build on Windows 10 or Windows 11 using scripts/build_windows.ps1"
    Write-Host "2) If Windows 7 support is required, retarget to .NET Framework (for example net48) and adjust dependencies"
    exit 3
}

$net48Release = 0
Invoke-Step "Check .NET Framework 4.8" {
    $script:net48Release = Get-Net48ReleaseValue
    Write-Host ".NET Framework Release value: $net48Release"

    if ($targetFrameworkText -match "net4") {
        if ($net48Release -lt 528040) {
            throw "Required .NET Framework 4.8 Developer Pack not found. Install from https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48"
        }
    }
}

$msbuildCommand = $null
Invoke-Step "Detect MSBuild" {
    $script:msbuildCommand = Get-Command msbuild.exe -ErrorAction SilentlyContinue
    if (-not $script:msbuildCommand) {
        throw "MSBuild not found. Install Visual Studio Build Tools with .NET desktop build tools."
    }

    Write-Host "MSBuild path: $($script:msbuildCommand.Source)"
}

Invoke-Step "Prepare output folders" {
    if (Test-Path $buildDir) {
        Remove-Item $buildDir -Recurse -Force
    }

    if (Test-Path $distDir) {
        Remove-Item $distDir -Recurse -Force
    }

    New-Item -ItemType Directory -Path $buildDir -Force | Out-Null
    New-Item -ItemType Directory -Path $distDir -Force | Out-Null
}

Invoke-Step "MSBuild Restore" {
    Invoke-ExternalCommand -FilePath $msbuildCommand.Source -Arguments @($project, "/t:Restore", "/p:Configuration=$Configuration", "/p:Platform=Any CPU") -DisplayName "msbuild"
}

Invoke-Step "MSBuild Rebuild" {
    Invoke-ExternalCommand -FilePath $msbuildCommand.Source -Arguments @($project, "/t:Rebuild", "/p:Configuration=$Configuration", "/p:Platform=Any CPU", "/p:OutDir=$buildDir\\") -DisplayName "msbuild"
}

Invoke-Step "Copy artifacts" {
    Copy-Item -Path (Join-Path $buildDir "*") -Destination $distDir -Recurse -Force
}

$exePath = Join-Path $distDir "GlassFactory.BillTracker.App.Win7.exe"
Invoke-Step "Validate executable output" {
    if (-not (Test-Path $exePath)) {
        throw "Build completed, but executable was not found: $exePath"
    }
}

$sizeMb = [Math]::Round(((Get-Item $exePath).Length / 1MB), 2)
Write-Host ""
Write-Host "Win7 build pipeline completed successfully." -ForegroundColor Green
Write-Host "Configuration: $Configuration"
Write-Host "TargetFramework: $targetFrameworkText"
Write-Host "Dist Dir     : $distDir"
Write-Host "EXE Path     : $exePath"
Write-Host "EXE Size(MB) : $sizeMb"
exit 0
