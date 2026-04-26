$sql = [System.IO.File]::ReadAllText('D:\Project\Moex_CGate\src\ProcDebug\ApplyProcLog\bin\Debug\net10.0\PROC\Original\BackOffice.Orders__ApproveServices__CheckAfterCalc.sql', (New-Object System.Text.UTF8Encoding $false))

$sep = 'SPLITTER'
$parts = ($sql -replace '\bGO\b', $sep) -split [regex]::Escape($sep)

Write-Host "Total batches: $($parts.Count)"

# Batch 0: отправляемый батч
$b = $parts[0].Trim()

# Проверяем символы
Write-Host "`nChecking batch 0 for non-printable chars..."
$foundBad = $false
for ($i = 0; $i -lt $b.Length; $i++) {
    $c = $b[$i]
    $code = [int]$c
    if ($code -lt 32 -and $c -ne "`r" -ne "`n" -and $c -ne "`t") {
        Write-Host "  BAD char at pos $i : code=$code char='$c'"
        $foundBad = $true
        if ($i -lt 50) {
            Write-Host "  Context: ...$($b.Substring([Math]::Max(0,$i-20), 50))..."
        }
    }
}
if (-not $foundBad) {
    Write-Host "  No non-printable chars found"
}

# Показываем начало отправляемого
Write-Host "`nBatch 0 start:"
Write-Host $b.Substring(0, [Math]::Min(500, $b.Length))

# Если батч слишком длинный, показываем конец
if ($b.Length -gt 500) {
    Write-Host "`nBatch 0 end:"
    Write-Host $b.Substring($b.Length - [Math]::Min(200, $b.Length))
}

# Пробуем выполнить
Write-Host "`n--- Trying to execute on BODB-TEST ---"
try {
    $connStr = "Server=BODB-TEST;Database=BackOffice;Integrated Security=True;TrustServerCertificate=True;"
    $conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $b
    $cmd.CommandTimeout = 300
    $cmd.ExecuteNonQuery() | Out-Null
    Write-Host "SUCCESS"
    $conn.Close()
}
catch {
    Write-Host "ERROR: $($_.Exception.Message)"
}
