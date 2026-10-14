Это все фигня, ничего не делаем и листам на 65 строку, просто ставим sqlncli
# 1. Генерируем SSL-сертификат под имя вашего компьютера
$HostName = [System.Net.Dns]::GetHostName() # Автоматически вернет имя ПК (например, 'FAZENDA')
$FriendlyName = "SQL_Server_2022_SSL_$HostName"
Write-Host $HostName
Write-Host $FriendlyName
$Cert = New-SelfSignedCertificate -DnsName "$HostName" -CertStoreLocation "cert:\LocalMachine\My" -FriendlyName "SQL_Server_2022_SSL_Fazenda" -KeyUsage DigitalSignature,KeyEncipherment -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.1")
Write-Host $Cert
$Thumbprint = $Cert.Thumbprint
$SqlAccount = Get-WmiObject Win32_Service | Where-Object {$_.Name -like "MSSQL*" -and $_.PathName -like "*sqlservr.exe*"} | Select-Object Name, StartName, DisplayName
$ServiceAccount = $SqlAccount.StartName
Write-Host $ServiceAccount

#cnhjrf 10? далее построчно применяем команды:
#$Thumbprint = "3FEF6A21F0EEB0EB0A0DD0DB563C11240A5DAF47"
#$ServiceAccount = "NT SERVICE\MSSQLSERVER" # Измените, если у вас именованный экземпляр
# Получаем доступ к файлу ключа на диске
#$Cert = Get-Item "Cert:\LocalMachine\My\$Thumbprint"

$KeyPath = "$env:ProgramData\Microsoft\Crypto\RSA\MachineKeys\"
$PairName = $Cert.PrivateKey.CspKeyContainerInfo.UniqueKeyContainerName
$FullPath = Join-Path $KeyPath $PairName

# Выдаем права на Чтение (Read) для службы SQL Server
$Acl = Get-Acl $FullPath
$AccessRule = New-Object System.Security.AccessControl.FileSystemAccessRule($ServiceAccount, "Read", "Allow")
$Acl.AddAccessRule($AccessRule)
Set-Acl $FullPath $Acl

Write-Host "Права для $ServiceAccount успешно добавлены!" -ForegroundColor Green


# Путь в реестре для MSSQLSERVER (для именованного экземпляра путь будет другим, например MSSQL16.MYINSTANCE)
$RegistryPath = "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQLServer\SuperSocketNetLib"

# 1. Привязываем наш сертификат по его отпечатку
Set-ItemProperty -Path $RegistryPath -Name "Certificate" -Value $Thumbprint

# 2. Включаем принудительное шифрование (Force Encryption = Yes)
Set-ItemProperty -Path $RegistryPath -Name "ForceEncryption" -Value 1

Write-Host "Сертификат привязан, ForceEncryption включен!" -ForegroundColor Green

#Restart-Service -Name "MSSQLSERVER" -Force
#Возвращием все назад
# Находим путь к настройкам сети вашей службы MSSQLSERVER
$RegistryPath = "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQLServer\SuperSocketNetLib"


# 1. Сбрасываем принудительное шифрование
Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQLServer\SuperSocketNetLib" -Name "ForceEncryption" -Value 0

# 2. Полностью ОЧИЩАЕМ поле сертификата (удаляем старый отпечаток)
Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQLServer\SuperSocketNetLib" -Name "Certificate" -Value ""

# 3. Пробуем запустить службу заново
Start-Service -Name "MSSQLSERVER"

# Проверяем статус
Get-Service -Name "MSSQLSERVER"

Get-Content "C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Log\ERRORLOG" -Tail 30
Get-WinEvent -FilterHashtable @{LogName='Application'; ProviderName='MSSQLSERVER'; Level=2} -MaxEvents 20 | Format-Table TimeCreated, Id, Message -Wrap

# Сертификаты мастдай
# 1. Скачиваем официальный пакет SQL Native Client 2012 (x64) во временную папку
https://learn.microsoft.com/ru-ru/sql/relational-databases/native-client/sql-server-native-client?view=sql-server-ver15
# этот линк кривой Invoke-WebRequest -Uri "https://microsoft.com" -OutFile "\$env:TEMP\sqlncli.msi"

# 2. Запускаем "тихую" установку без вызова диалоговых окон
Start-Process msiexec.exe -ArgumentList "/i `"$env:TEMP\sqlncli.msi`" /qn IACCEPTSQLNCLILICENSETERMS=YES" -Wait

Write-Host "Установка SQLNCLI11 завершена успешно!" -ForegroundColor Green