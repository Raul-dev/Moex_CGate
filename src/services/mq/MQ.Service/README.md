1) создание windows сервиса
https://swimburger.net/blog/dotnet/how-to-run-a-dotnet-core-console-app-as-a-service-using-systemd-on-linux
2) Win service
dotnet publish -c Release
New-Service -Name "MyDotNet10Service" -BinaryPathName "..нужен путь\bin\Release\net10.0\publish\MQ.Service.exe"