param(
    [string]$Server = "localhost",
    [string]$User = "sa",
    [string]$Password = "",
    [string]$Trusted = ""
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

$scripts = @(
    (Join-Path $root "mssql_init.sql"),
    (Join-Path $root "mssql\stg_trade_result.sql"),
    (Join-Path $root "mssql\load_trade_result_from_buffer.sql"),
    (Join-Path $root "mssql\load_trade_result_from_buffer_short.sql"),
    (Join-Path $root "mssql\load_trade_result_from_buffer_short_typed.sql"),
    (Join-Path $root "mssql\stg_trade_result_typed_csv.sql"),
    (Join-Path $root "mssql\fix_isaddress_yn.sql"),
    (Join-Path $root "mssql\stg_trade_result_string_buffer.sql"),
    (Join-Path $root "mssql\load_trade_result_from_string_buffer.sql")
)

foreach ($s in $scripts) {
    if (-not (Test-Path $s)) { throw "Missing script: $s" }
}

$auth = @()
if ($Trusted -eq "true" -or $Trusted -eq "1") {
    $auth = @("-E")
} elseif ($Password) {
    $auth = @("-U", $User, "-P", $Password)
} else {
    $auth = @("-U", $User)
}

Write-Host "Initializing ImportFile on $Server ..."
foreach ($s in $scripts) {
    Write-Host "  -> $(Split-Path $s -Leaf)"
    & sqlcmd -S $Server @auth -b -i $s
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host "Done. Includes stg_trade_result_typed_csv (StreamingBulkTyped)."
