
param (
    [string]$SqlServer = "localhost",
    [string]$DbName = "MoexData"
)
# Принудительно включаем UTF-8 для красивых эмодзи и русского текста
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# Функция вывода милого гайда по оживлению SQL Server
function Show-FriendlyHelpMessage {
    Write-Host ""
    Write-Host "=======================================================================" -ForegroundColor Yellow
    Write-Host " ✨ Хьюстон, у нас проблемы с базой данных! Давайте это исправим ✨" -ForegroundColor Yellow
    Write-Host "=======================================================================" -ForegroundColor Yellow
    Write-Host "Кажется, ваш MS SQL Server на [$SqlServer] спит или база [$DbName] не создана."
    Write-Host ""
    Write-Host "🔹 Вариант 1: Окружение уже скачано, нужно просто запустить Docker/Службу" -ForegroundColor Cyan
    Write-Host "   Попробуйте запустить стартовый скрипт из корня репозитория:"
    Write-Host "   👉  .\start.ps1  (в самом корне проекта Moex_CGate)" -ForegroundColor Magenta
    Write-Host ""
    Write-Host "🔹 Вариант 2: Если SQL Server установлен локально как служба Windows" -ForegroundColor Cyan
    Write-Host "   Откройте PowerShell от имени Администратора и запустите команду:"
    Write-Host "   👉  net start MSSQLSERVER" -ForegroundColor Green
    Write-Host "   (или net start MSSQL`$SQLEXPRESS, если у вас Express-редакция)"
    Write-Host ""
    Write-Host "🔹 Вариант 3: У вас ещё нет MS SQL Server 2022 Developer Edition?" -ForegroundColor Cyan
    Write-Host "   1. Скачайте бесплатный инсталлятор с официального сайта Microsoft."
    Write-Host "   2. При установке выберите тип 'Basic' (Базовая) или 'Developer'."
    Write-Host "   3. После установки вернитесь сюда и запустите этот скрипт снова! 🚀"
    Write-Host "=======================================================================" -ForegroundColor Yellow
    Write-Host ""
}

Write-Host "===================================================" -ForegroundColor Cyan
Write-Host "  🔍 Проверка системного окружения для бенчмарка... " -ForegroundColor Cyan
Write-Host "===================================================" -ForegroundColor Cyan

# 1. Проверяем доступность самого SQL Server (убираем системные ошибки через 2>$null)
Write-Host "[1/2] Проверка подключения к серверу $SqlServer... " -NoNewline
$serverTest = sqlcmd -S $SqlServer -Q "SELECT 1" -t 3 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "😴 [НЕАКТИВЕН]" -ForegroundColor Yellow
    Show-FriendlyHelpMessage
    exit 1
}
Write-Host "🟢 [АКТИВЕН]" -ForegroundColor Green

# 2. Проверяем существование базы данных
Write-Host "[2/2] Проверка наличия базы данных $DbName... " -NoNewline
$dbTest = sqlcmd -S $SqlServer -Q "IF DB_ID('$DbName') IS NULL THROW 50000, 'Missing', 1;" -t 3 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ [НЕ НАЙДЕНА]" -ForegroundColor Red
    Show-FriendlyHelpMessage
    exit 1
}
Write-Host "🟢 [НАЙДЕНА]" -ForegroundColor Green

Write-Host ""
Write-Host "===================================================" -ForegroundColor Cyan
Write-Host " 🚀 Окружение готово! Запускаем компиляцию тестов... " -ForegroundColor Cyan
Write-Host "===================================================" -ForegroundColor Cyan

# 3. Запуск компиляции в один поток (обход внутренней ошибки CLR 0x80131506)
dotnet build -c Release -m:1
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ ОШИБКА: Сборка проекта завершилась неудачей." -ForegroundColor Red
    exit $LASTEXITCODE
}

# 4. Запуск бенчмарка
Write-Host "[ЗАПУСК] Инициализация BenchmarkDotNet..." -ForegroundColor Yellow
dotnet .\bin\Release\net9.0\TestPerformance.dll
