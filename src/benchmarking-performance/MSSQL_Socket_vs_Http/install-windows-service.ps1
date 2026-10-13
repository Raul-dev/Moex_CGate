<#
Пример установки этой же .NET 9-службы PositionTcpService как Windows-службы.

Требования:
  - запуск из повышенной (Administrator) консоли PowerShell;
  - .NET 9 Runtime installed on host (dotnet --list-runtimes).

Использование:
  .\install-windows-service.ps1                      # собрать, установить и запустить PerfTcpService
  .\install-windows-service.ps1 -Port 8889           # установить на кастомный порт
  .\install-windows-service.ps1 -Uninstall           # остановить и удалить службу

Логи: sc.exe query PerfTcpService, Event Viewer -> контейнер Application.
#>
param(
    [string]$ServiceName = "PerfTcpService",
    [int]$Port = 8888,
    [switch]$Uninstall
)

$ErrorActionPreference = 'Stop'

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
    ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    throw "Скрипт требует прав Administrator (установка Windows-службы)."
}

$projectPath = Join-Path $PSScriptRoot "PositionTcpService\PositionTcpService.csproj"
$publishDir  = Join-Path $PSScriptRoot "artifacts\PerfTcpService"
$exePath     = Join-Path $publishDir "PositionTcpService.exe"

if ($Uninstall) {
    sc.exe stop $ServiceName | Out-Null
    sc.exe delete $ServiceName
    Write-Host "Служба '$ServiceName' удалена (если была установлена)."
    return
}

# Публикация службы (dotnet CLI; тот же exe работает и как консоль, и как Windows Service)
dotnet publish $projectPath -c Release -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "Публикация проекта не удалась." }

sc.exe create $ServiceName binPath= "`"$exePath`" --winsvc --TcpPort=$Port" start= auto
sc.exe description $ServiceName "Perf: TCP position service (4B request / 5KB payload)"
sc.exe start $ServiceName

sc.exe query $ServiceName
Write-Host "Служба '$ServiceName' запущена на порту $Port. Удаление: .\install-windows-service.ps1 -Uninstall"
