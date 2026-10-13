# Очищаем экран и настраиваем кодировку вывода
#Clear-Host
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host " Поиск максимальной Visual Studio и установка SSDT" -ForegroundColor Cyan
Write-Host "=========================================================" -ForegroundColor Cyan

# 1. Проверяем наличие vswhere.exe
$vswherePath = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswherePath)) {
    Write-Error "Утилита vswhere.exe не найдена! Проверьте установку Visual Studio."
    exit 1
}

# 2. Проверяем наличие установщика vs_installer.exe
$installerPath = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vs_installer.exe"
if (-not (Test-Path $installerPath)) {
    Write-Error "vs_installer.exe не найден по стандартному пути."
    exit 1
}

# 3. Ищем самую свежую (максимальную) установленную версию VS / Build Tools
Write-Host "Поиск последней установленной версии VS..." -ForegroundColor Yellow
$latestVs = & $vswherePath -latest -format json | ConvertFrom-Json
Write-Host $latestVs
if (-not $latestVs) {
    Write-Error "Не удалось обнаружить установленные версии Visual Studio на этой машине."
    exit 1
}

# Извлекаем параметры найденной студии
$vsPath = $latestVs.installationPath
$vsName = $latestVs.displayName
$vsVersion = $latestVs.installationVersion

Write-Host "Найдена максимальная конфигурация:" -ForegroundColor Green
Write-Host " -> Имя: $vsName" -ForegroundColor Green
Write-Host " -> Версия: $vsVersion" -ForegroundColor Green
Write-Host " -> Путь: $vsPath" -ForegroundColor Green
Write-Host "--------------------------------------------------------..." -ForegroundColor Cyan
# ПРОВЕРКА: Проверяем, установлен ли уже компонент SSDT в этой конкретной студии
$componentId = "Microsoft.VisualStudio.Component.SQL.SSDT"
$hasSsdt = & $vswherePath -latest -requires $componentId -format json | ConvertFrom-Json

if ($hasSsdt -and $hasSsdt.installationPath -eq $latestVs.installationPath) {
    Write-Host "`n[ИНФО] В выбранной $(${latestVs.displayName}) компонент SSDT уже установлен!" -ForegroundColor Green
    Write-Host "Повторная установка не требуется. Выход." -ForegroundColor Green
    exit 0
}
# 4. Запускаем доустановку компонента
$componentId = "Microsoft.VisualStudio.Component.SQL.SSDT"
Write-Host "Запуск модификации. Добавляем компонент: $componentId" -ForegroundColor Yellow
Write-Host "Пожалуйста, подождите. Будет открыто пассивное окно инсталлятора..." -ForegroundColor Gray

# Формируем аргументы для vs_installer.exe
$arguments = @(
    "modify",
    "--installPath", "`"$vsPath`"",
    "--add", $componentId,
    "--passive",
    "--norestart"
)

# Запускаем процесс установки и ждем его завершения
$process = Start-Process -FilePath $installerPath -ArgumentList $arguments -Wait -PassThru -NoNewWindow

# 5. Проверяем результат работы
if ($process.ExitCode -eq 0) {
    Write-Host "`n[УСПЕХ] Компонент SSDT успешно добавлен в $vsName!" -ForegroundColor Green
    Write-Host "Файлы 'Microsoft.Data.Tools.Schema.SqlTasks.targets' теперь установлены." -ForegroundColor Green
	exit 0
} else {
    Write-Warning "`n[ВНИМАНИЕ] Процесс установки завершился с кодом: (process.ExitCode)"
    Write-Warning "Убедитесь, что закрыты все запущенные среды разработки VS и скрипт запущен от Администратора."
	exit 1
}
