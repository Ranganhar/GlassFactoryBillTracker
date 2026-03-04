Param(
    [string]$Configuration = "Release"
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
$OutputEncoding = New-Object System.Text.UTF8Encoding($false)

$script:CurrentStep = "startup"

trap [Exception] {
    Write-Host ""
    Write-Host "FATAL: Unhandled exception." -ForegroundColor Red
    Write-Host ("Step: " + $script:CurrentStep) -ForegroundColor Red

    if ($_.Exception -ne $null) {
        Write-Host ("Type: " + $_.Exception.GetType().FullName) -ForegroundColor Red
        Write-Host ("Message: " + $_.Exception.Message) -ForegroundColor Red
        if ($_.Exception.InnerException -ne $null) {
            Write-Host ("InnerType: " + $_.Exception.InnerException.GetType().FullName) -ForegroundColor Red
            Write-Host ("InnerMessage: " + $_.Exception.InnerException.Message) -ForegroundColor Red
        }
        Write-Host "StackTrace:" -ForegroundColor Red
        Write-Host $_.Exception.StackTrace -ForegroundColor Red
    }

    Write-Host "Invocation:" -ForegroundColor Red
    if ($_.InvocationInfo -ne $null) {
        Write-Host $_.InvocationInfo.PositionMessage -ForegroundColor Red
        Write-Host ("Script: " + $_.InvocationInfo.ScriptName) -ForegroundColor Red
        Write-Host ("Line: " + $_.InvocationInfo.ScriptLineNumber) -ForegroundColor Red
        Write-Host ("Command: " + $_.InvocationInfo.Line) -ForegroundColor Red
    }

    if ($Error.Count -gt 0) {
        Write-Host "LatestErrorRecord:" -ForegroundColor Red
        Write-Host ($Error[0] | Out-String) -ForegroundColor Red
    }

    exit 1
}

function Invoke-Step {
    param(
        [string]$Name,
        [scriptblock]$Action
    )

    $script:CurrentStep = $Name
    Write-Host ""
    Write-Host ("==> " + $Name) -ForegroundColor Cyan

    try {
        & $Action
        Write-Host ("<== " + $Name + " succeeded") -ForegroundColor Green
    }
    catch {
        Write-Host ("<== " + $Name + " failed") -ForegroundColor Red
        throw
    }
}

function Show-DotnetMissingMessage {
    param(
        [string]$ProjectFile
    )

    Write-Host ""
    Write-Host "dotnet was not found in PATH." -ForegroundColor Red
    Write-Host ""
    Write-Host "Diagnostics:" -ForegroundColor Yellow
    Write-Host ("PowerShell: " + $psVersionText)
    Write-Host ("OS: " + $osVersion.ToString())
    Write-Host ("Project: " + $ProjectFile)
    Write-Host ""
    Write-Host "Current PATH:" -ForegroundColor Yellow
    Write-Host $env:PATH
    Write-Host ""
    Write-Host "Quick check:" -ForegroundColor Yellow
    Write-Host "where dotnet"
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "1) Windows 7 cannot build or run this net8.0-windows WPF app with supported tooling."
    Write-Host "2) Recommended: build on Windows 10 or Windows 11 using scripts/build_windows.ps1"
    Write-Host "3) If Windows 7 support is mandatory: retarget to .NET Framework 4.8 (net48) and adjust dependencies."
    Write-Host "   This is a separate engineering effort and is not covered by this script."
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

    $argText = ""
    if ($Arguments -ne $null -and $Arguments.Length -gt 0) {
        $argText = " " + ($Arguments -join " ")
    }

    Write-Host ("Running: " + $DisplayName + $argText) -ForegroundColor DarkGray
    & $FilePath @Arguments 2>&1 | ForEach-Object { Write-Host $_ }

    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        throw ("Command failed: " + $DisplayName + $argText + " (exit code " + $exitCode + ")")
    }
}

function Get-TargetFrameworkText {
    param(
        [string]$ProjectFile
    )

    if (-not (Test-Path $ProjectFile)) {
        throw ("Project file not found: " + $ProjectFile)
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
$scriptDir = Split-Path -Parent $scriptPath
$repoRoot = Split-Path -Parent $scriptDir
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
Write-Host ("Script: " + $scriptName)
Write-Host ("Timestamp: " + $timestamp)
Write-Host ("PowerShell: " + $psVersionText)
Write-Host ("OS: " + $osVersion.ToString())
Write-Host ("CWD: " + $cwd)
Write-Host "============================================================" -ForegroundColor Yellow

if ($scriptName -ne "build_windows7.ps1") {
    Write-Host "This script is expected to run from scripts/build_windows7.ps1" -ForegroundColor Red
    Write-Host "Usage: powershell -ExecutionPolicy Bypass -File .\scripts\build_windows7.ps1" -ForegroundColor Yellow
    exit 4
}

$project = Join-Path $repoRoot "src/GlassFactory.BillTracker.App.Win7/GlassFactory.BillTracker.App.Win7.csproj"
$distDir = Join-Path $repoRoot "dist/win7-x64"
$buildDir = Join-Path $repoRoot "artifacts/build/win7-x64"

Write-Host ("Project: " + $project)
Write-Host ("BuildDir: " + $buildDir)
Write-Host ("DistDir: " + $distDir)

$dotnetCommand = $null
$dotnetSdkVersion = "unknown"
$dotnetSdkMajor = 0

Invoke-Step "Check dotnet" {
    $script:dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $script:dotnetCommand) {
        Write-Host "<== Check dotnet FAILED" -ForegroundColor Red
        Show-DotnetMissingMessage -ProjectFile $project
        exit 2
    }

    Write-Host ("dotnet path: " + $script:dotnetCommand.Source)
}

Invoke-Step "Print dotnet info" {
    Invoke-ExternalCommand -FilePath $dotnetCommand.Source -Arguments @("--info") -DisplayName "dotnet"
}

Invoke-Step "Read dotnet SDK version" {
    Write-Host "Running: dotnet --version" -ForegroundColor DarkGray
    $versionOutput = & $dotnetCommand.Source --version 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) {
        throw ("Command failed: dotnet --version (exit code " + $LASTEXITCODE + ")")
    }

    $script:dotnetSdkVersion = $versionOutput.Trim()
    Write-Host ("dotnet SDK version: " + $dotnetSdkVersion)

    try {
        $script:dotnetSdkMajor = [int]($dotnetSdkVersion.Split('.')[0])
    }
    catch {
        $script:dotnetSdkMajor = 0
    }

    Write-Host ("dotnet SDK major: " + $dotnetSdkMajor)

    if ($dotnetSdkMajor -lt 6) {
        throw ("dotnet SDK appears too old: " + $dotnetSdkVersion + ". Install a newer SDK or build on Windows 10/11.")
    }
}

$targetFrameworkText = "unknown"
Invoke-Step "Read project target framework" {
    $script:targetFrameworkText = Get-TargetFrameworkText -ProjectFile $project
    Write-Host ("TargetFramework value: " + $targetFrameworkText)
}

$isWindows7 = ($osVersion.Major -eq 6 -and $osVersion.Minor -eq 1)
if ($isWindows7) {
    Write-Host "Detected Windows 7 host." -ForegroundColor Green
}
else {
    Write-Host ("Warning: this script targets Windows 7 (6.1). Current OS is " + $osVersion.ToString()) -ForegroundColor Yellow
}

$isModernDotnetTarget = $false
if ($targetFrameworkText -match "net[5-9]" -or $targetFrameworkText -match "netcoreapp") {
    $isModernDotnetTarget = $true
}

if ($isWindows7 -and $isModernDotnetTarget) {
    throw ("Current project target is unsupported on Windows 7: " + $targetFrameworkText + ". Recommended: build on Windows 10/11 with scripts/build_windows.ps1, or retarget to net48 and adjust dependencies.")
}

$net48Release = 0
Invoke-Step "Check .NET Framework 4.8" {
    $script:net48Release = Get-Net48ReleaseValue
    Write-Host (".NET Framework Release value: " + $net48Release)

    if ($targetFrameworkText -match "net4") {
        if ($net48Release -lt 528040) {
            throw "Required .NET Framework 4.8 Developer Pack not found. Install from https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48"
        }
    }
}

$msbuildCommand = $null
Invoke-Step "Check MSBuild" {
    $script:msbuildCommand = Get-Command msbuild.exe -ErrorAction SilentlyContinue
    if (-not $script:msbuildCommand) {
        throw "MSBuild not found. Install Visual Studio Build Tools with .NET desktop build tools."
    }

    Write-Host ("MSBuild path: " + $script:msbuildCommand.Source)
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

Invoke-Step "MSBuild restore" {
    Invoke-ExternalCommand -FilePath $msbuildCommand.Source -Arguments @($project, "/t:Restore", "/p:Configuration=$Configuration", "/p:Platform=Any CPU") -DisplayName "msbuild"
}

Invoke-Step "MSBuild rebuild" {
    Invoke-ExternalCommand -FilePath $msbuildCommand.Source -Arguments @($project, "/t:Rebuild", "/p:Configuration=$Configuration", "/p:Platform=Any CPU", "/p:OutDir=$buildDir\\") -DisplayName "msbuild"
}

Invoke-Step "Copy artifacts" {
    Copy-Item -Path (Join-Path $buildDir "*") -Destination $distDir -Recurse -Force
}

$exePath = Join-Path $distDir "GlassFactory.BillTracker.App.Win7.exe"
Invoke-Step "Validate executable output" {
    if (-not (Test-Path $exePath)) {
        throw ("Build completed, but executable was not found: " + $exePath)
    }
}

$sizeMb = [Math]::Round(((Get-Item $exePath).Length / 1MB), 2)
Write-Host ""
Write-Host "Win7 build pipeline completed successfully." -ForegroundColor Green
Write-Host ("Configuration: " + $Configuration)
Write-Host ("TargetFramework: " + $targetFrameworkText)
Write-Host ("DistDir: " + $distDir)
Write-Host ("ExePath: " + $exePath)
Write-Host ("ExeSizeMB: " + $sizeMb)
exit 0
