param(
    [Parameter(Mandatory=$true)]
    [string]$Folder,

    [string]$TargetServerName = "localhost",

    [string]$TargetDBname = "cgate_uts_tmp"
)

if (-not (Test-Path $Folder)) {
    Write-Error "Папка не найдена: $Folder"
    exit 1
}

$sqlFiles = Get-ChildItem -Path $Folder -Filter "*.sql" -File | Sort-Object Name

if ($sqlFiles.Count -eq 0) {
    Write-Warning "SQL файлы не найдены в: $Folder"
    exit 0
}

Write-Host "Сервер: $TargetServerName"
Write-Host "База:   $TargetDBname"
Write-Host "Файлов: $($sqlFiles.Count)"
Write-Host ("-" * 60)

$connectionString = "Server=$TargetServerName;Database=$TargetDBname;Integrated Security=True;TrustServerCertificate=True;"

$applied = 0
$errors = 0
$skipped = 0

foreach ($file in $sqlFiles) {
    Write-Host -NoNewline "[$($file.Name)] -> "
    try {
        # UTF-8 без BOM (C# пишет с BOM)
        $utf8NoBom = New-Object System.Text.UTF8Encoding $false
        $sql = [System.IO.File]::ReadAllText($file.FullName, $utf8NoBom)

        $batchSeparator = "SPLIT_MARKER_RANDOM_SPLIT"
        $sqlBatches = ($sql -replace '\bGO\b', $batchSeparator) -split [regex]::Escape($batchSeparator)

        # Если в файле есть GO — пропускаем первый батч (до первого GO он может быть обрезанным)
        # Если GO нет — выполняем весь файл (единственный батч)
        if ($sqlBatches.Count -gt 1) {
            $batchesToExecute = $sqlBatches | Select-Object -Skip 1
        } else {
            $batchesToExecute = $sqlBatches
        }

        # Одно соединение на файл
        $conn = New-Object System.Data.SqlClient.SqlConnection($connectionString)
        $conn.Open()
        try {
            foreach ($batch in $batchesToExecute) {
                $trimmedBatch = $batch.Trim()
                if ([string]::IsNullOrWhiteSpace($trimmedBatch)) { continue }

                $cmd = $conn.CreateCommand()
                $cmd.CommandText = $trimmedBatch
                $cmd.CommandTimeout = 300
                $cmd.ExecuteNonQuery() | Out-Null
                $cmd.Dispose()
            }
        }
        finally {
            if ($conn.State -eq 'Open') { $conn.Close() }
            $conn.Dispose()
        }

        Write-Host -ForegroundColor Green "OK"
        $applied++
    }
    catch [System.Data.SqlClient.SqlException] {
        if ($_.Exception.Message -match "There is already an object|already exists") {
            Write-Host -ForegroundColor Yellow "SKIP (уже существует)"
            $skipped++
        }
        else {
            Write-Host -ForegroundColor Red "ERROR: $($_.Exception.Message)"
            $errors++
        }
    }
    catch {
        Write-Host -ForegroundColor Red "ERROR: $($_.Exception.Message)"
        $errors++
    }
}

Write-Host ("-" * 60)
Write-Host "Готово:  applied=$applied, skipped=$skipped, errors=$errors"
