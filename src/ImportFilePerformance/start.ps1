param(
    [string]$Mode = "e2e",
    [string]$Db = "mssql",
    [string]$File = "CsvTradeResult",
    [string]$Strategy = "ParseOnly,StreamingBulk",
    [string]$Configuration = "Release",
    [switch]$Local
)

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "ImportFilePerformance\ImportFilePerformance.csproj"

Write-Host "Building ($Configuration)..."
dotnet build $project -c $Configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$settingsArgs = @()
if ($Local) { $settingsArgs += "--settings=local" }

Write-Host "Running mode=$Mode db=$Db file=$File ..."
dotnet run --no-build --no-launch-profile -c $Configuration --project $project -- --mode=$Mode --db=$Db --file=$File --strategy=$Strategy @settingsArgs
