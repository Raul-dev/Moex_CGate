using Microsoft.Extensions.Configuration;

namespace ImportFilePerformance;

internal static class AppSettingsLoader
{
    public const string DefaultFileName = "appsettings.json";
    public const string LocalFileName = "appsettings_local.json";
    public const string LocalEnvironmentName = "Local";

    public static bool UseLocalSettings(string[]? args)
    {
        var settingsArg = GetArg(args, "settings");
        if (string.Equals(settingsArg, "local", StringComparison.OrdinalIgnoreCase))
            return true;

        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        return string.Equals(environment, LocalEnvironmentName, StringComparison.OrdinalIgnoreCase);
    }

    public static IConfiguration Load(string[]? args = null)
    {
        if (UseLocalSettings(args))
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", LocalEnvironmentName);

        var basePath = FindDirectoryContaining(DefaultFileName) ?? AppContext.BaseDirectory;
        var builder = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile(DefaultFileName, optional: false, reloadOnChange: true);

        if (UseLocalSettings(args))
        {
            var localDir = FindDirectoryContaining(LocalFileName)
                ?? throw new FileNotFoundException(
                    $"'{LocalFileName}' was not found. Copy '{DefaultFileName}' to '{LocalFileName}' next to the project file to use the Local launch profile.");
            builder.AddJsonFile(Path.Combine(localDir, LocalFileName), optional: false, reloadOnChange: true);
        }

        if (args is { Length: > 0 })
            builder.AddCommandLine(args);

        return builder.Build();
    }

    public static string? GetArg(string[]? args, string name)
    {
        if (args is null || args.Length == 0)
            return null;

        var prefix = $"--{name}=";
        foreach (var a in args)
        {
            if (a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return a[prefix.Length..];
            if (a.Equals($"--{name}", StringComparison.OrdinalIgnoreCase))
            {
                var idx = Array.IndexOf(args, a);
                if (idx >= 0 && idx + 1 < args.Length)
                    return args[idx + 1];
            }
        }

        foreach (var a in args)
        {
            if (a.StartsWith($"{name}=", StringComparison.OrdinalIgnoreCase))
                return a[(name.Length + 1)..];
        }

        return null;
    }

    private static string? FindDirectoryContaining(string fileName)
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            if (string.IsNullOrWhiteSpace(start))
                continue;

            for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, fileName)))
                    return dir.FullName;
            }
        }

        return null;
    }
}
