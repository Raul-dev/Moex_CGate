# Укажите путь к папке с вашими CSV файлами
$folderPath = "."

# Укажите путь, куда сохранить обрезанные файлы
$outputPath = ".\Res"

# Создаем папку для сохранения, если она не существует
if (!(Test-Path $outputPath)) {
    New-Item -ItemType Directory -Path $outputPath | Out-Null
}

# ИСПРАВЛЕНО: добавили \* к пути и заменили -Filter на -Include
$csvFiles = Get-ChildItem -Path "$folderPath\*" -Include *.csv, *.txt

foreach ($file in $csvFiles) {
    # Защита: пропускаем файлы внутри целевой папки .\Res, чтобы скрипт не обрабатывал сам себя
    if ($file.FullName -like "*(Resolve-Path $outputPath)*") { continue }

    Write-Host "Обработка файла: $($file.Name)..."
    
    # Полный путь к новому файлу
    $newFilePath = Join-Path $outputPath $file.Name
    
    # Читаем первые 300 строк и сразу записываем в новый файл
    Get-Content -Path $file.FullName -TotalCount 300 | Set-Content -Path $newFilePath
}

Write-Host "Готово! Все файлы обработаны." -ForegroundColor Green
