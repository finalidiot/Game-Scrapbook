param([string]$OutputDirectory = "")
$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $projectRoot "artifacts\windows-x64"
}
$appProject = Join-Path $projectRoot "src\GameScrapbook.Windows\GameScrapbook.Windows.csproj"
$checksProject = Join-Path $projectRoot "tests\GameScrapbook.Core.Checks\GameScrapbook.Core.Checks.csproj"
$captureChecksProject = Join-Path $projectRoot "tests\GameScrapbook.Capture.Checks\GameScrapbook.Capture.Checks.csproj"
$layoutChecksProject = Join-Path $projectRoot "tests\GameScrapbook.Layout.Checks\GameScrapbook.Layout.Checks.csproj"
$exportChecksProject = Join-Path $projectRoot "tests\GameScrapbook.Export.Checks\GameScrapbook.Export.Checks.csproj"
Push-Location $projectRoot
try {
    dotnet run --project $checksProject -c Release
    if ($LASTEXITCODE -ne 0) { throw "The core checks failed." }
    dotnet run --project $layoutChecksProject -c Release
    if ($LASTEXITCODE -ne 0) { throw "The snapping and magnetise checks failed." }
    if ($env:OS -eq "Windows_NT") {
        dotnet run --project $captureChecksProject -c Release -- --report (Join-Path $projectRoot "artifacts\capture-checks.json")
        if ($LASTEXITCODE -ne 0) { throw "The capture lifecycle checks failed." }
        dotnet run --project $exportChecksProject -c Release -- --report (Join-Path $projectRoot "artifacts\export-checks.json")
        if ($LASTEXITCODE -ne 0) { throw "The image export checks failed." }
    }
    else { Write-Host "Capture and image export checks require Windows; skipped on this platform." }
    dotnet publish $appProject -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true -p:PublishTrimmed=false `
        -p:DebugType=None -p:DebugSymbols=false -o $OutputDirectory
    if ($LASTEXITCODE -ne 0) { throw "Windows publish failed." }
    Write-Host "Built: $(Join-Path $OutputDirectory 'GameScrapbook.exe')"
}
finally { Pop-Location }
