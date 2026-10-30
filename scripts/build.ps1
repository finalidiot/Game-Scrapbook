param([string]$OutputDirectory = "")
$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $projectRoot "artifacts\windows-x64"
}
$appProject = Join-Path $projectRoot "src\GameScrapbook.Windows\GameScrapbook.Windows.csproj"
$checksProject = Join-Path $projectRoot "tests\GameScrapbook.Core.Checks\GameScrapbook.Core.Checks.csproj"
Push-Location $projectRoot
try {
    dotnet run --project $checksProject -c Release
    if ($LASTEXITCODE -ne 0) { throw "The core checks failed." }
    dotnet publish $appProject -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true -p:PublishTrimmed=false `
        -p:DebugType=None -p:DebugSymbols=false -o $OutputDirectory
    if ($LASTEXITCODE -ne 0) { throw "Windows publish failed." }
    Write-Host "Built: $(Join-Path $OutputDirectory 'GameScrapbook.exe')"
}
finally { Pop-Location }
