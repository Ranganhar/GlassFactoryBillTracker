Param(
    [ValidateSet("win-x64", "win-arm64")]
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

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

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet SDK not found. Install .NET 8 SDK first: https://dotnet.microsoft.com/download/dotnet/8.0"
}

$sdkVersion = (& dotnet --version).Trim()
$sdkMajor = [int]($sdkVersion.Split('.')[0])
if ($sdkMajor -lt 8) {
    throw "Current dotnet SDK version is $sdkVersion. .NET 8 or later is required."
}

$root = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $root "GlassFactory.BillTracker.sln"
$appProject = Join-Path $root "src/GlassFactory.BillTracker.App/GlassFactory.BillTracker.App.csproj"
$testsProject = Join-Path $root "tests/GlassFactory.BillTracker.Tests/GlassFactory.BillTracker.Tests.csproj"

$publishDir = Join-Path $root "artifacts/publish/$Runtime"
$distDir = Join-Path $root "dist/$Runtime"

if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}
if (Test-Path $distDir) {
    Remove-Item $distDir -Recurse -Force
}

Invoke-Step "Restore" { dotnet restore $solution }
Invoke-Step "Build" { dotnet build $solution -c $Configuration --no-restore }
Invoke-Step "Test" { dotnet test $testsProject -c $Configuration --no-build }

Invoke-Step "Publish ($Runtime)" {
    dotnet publish $appProject `
        -c $Configuration `
        -r $Runtime `
        --self-contained true `
        /p:PublishSingleFile=true `
        /p:SelfContained=true `
        /p:IncludeNativeLibrariesForSelfExtract=true `
        /p:PublishTrimmed=false `
        /p:DebugType=None `
        /p:DebugSymbols=false `
        -o $publishDir
}

New-Item -ItemType Directory -Path $distDir -Force | Out-Null
Copy-Item -Path (Join-Path $publishDir "*") -Destination $distDir -Recurse -Force

$exePath = Join-Path $distDir "GlassFactory.BillTracker.App.exe"
if (-not (Test-Path $exePath)) {
    throw "Publish completed, but exe not found: $exePath"
}

$exeSizeMb = [Math]::Round(((Get-Item $exePath).Length / 1MB), 2)

Write-Host "`nPublish succeeded." -ForegroundColor Green
Write-Host "Runtime      : $Runtime"
Write-Host "Configuration: $Configuration"
Write-Host "Dist Dir     : $distDir"
Write-Host "EXE Path     : $exePath"
Write-Host "EXE Size(MB) : $exeSizeMb"
