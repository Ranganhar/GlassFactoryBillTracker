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
        throw "步骤失败: $Name"
    }
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "未检测到 dotnet SDK。请先安装 .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0"
}

$sdkVersion = (& dotnet --version).Trim()
$sdkMajor = [int]($sdkVersion.Split('.')[0])
if ($sdkMajor -lt 8) {
    throw "当前 dotnet SDK 版本为 $sdkVersion，需 .NET 8 或更高版本。"
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
    throw "发布完成但未找到 exe：$exePath"
}

$exeSizeMb = [Math]::Round(((Get-Item $exePath).Length / 1MB), 2)

Write-Host "`n发布成功。" -ForegroundColor Green
Write-Host "Runtime      : $Runtime"
Write-Host "Configuration: $Configuration"
Write-Host "Dist Dir     : $distDir"
Write-Host "EXE Path     : $exePath"
Write-Host "EXE Size(MB) : $exeSizeMb"
