using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PositionTcpService;

const string WindowsServiceFlag = "--winsvc";

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<Worker>();

// Пример запуска этой же .NET 9-службы как Windows-службы Windows Service:
//   sc.exe create PerfTcpService binPath= "<путь>\PositionTcpService.exe --winsvc"
// См. install-windows-service.ps1 в корне решения.
if (args.Any(a => string.Equals(a, WindowsServiceFlag, StringComparison.OrdinalIgnoreCase)))
{
    builder.Services.AddWindowsService(options => options.ServiceName = "PerfTcpService");
}

// Для systemd-хостинга под Linux: systemctl + Unit-файл (Type=notify поддержано этим же exe).
builder.Services.AddSystemd();

var host = builder.Build();
host.Run();
