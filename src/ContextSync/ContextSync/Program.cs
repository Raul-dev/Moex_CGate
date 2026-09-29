using Spectre.Console.Cli;
using Spectre.Console;
using ContextSync.Features.SyncJira;
using ContextSync.Features.SyncWiki;
using ContextSync.Features.GenerateHtml;
using ContextSync.Features.SyncAll;
using ContextSync.Features.SchemaHtml;
using ContextSync.Features.RunScenario;
using Serilog;

[assembly: System.Runtime.Versioning.SupportedOSPlatform("windows")]

namespace ContextSync;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("logs/contextsync-.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try
        {
            var app = new CommandApp(new TypeRegistrar());

            app.Configure(config =>
            {
                config.SetApplicationName("ContextSync");
                config.SetApplicationVersion("1.0.0");
                config.ValidateExamples();

                config.AddCommand<SyncJiraCommand>("jira")
                    .WithDescription("Sync source documents to Jira issues")
                    .WithExample("jira", "-i", "docs", "-p", "PROJ", "--type", "Task")
                    .WithExample("jira", "--from-db", "-s", "localhost", "-d", "Tasks", "-q", "SELECT * FROM Tasks");

                config.AddCommand<SyncWikiCommand>("wiki")
                    .WithDescription("Sync source documents to Confluence pages")
                    .WithExample("wiki", "-i", "docs", "-s", "SPACE", "-p", "Parent Page")
                    .WithExample("wiki", "--from-db", "--server", "localhost", "--database", "Tasks", "-q", "SELECT * FROM Pages");

                config.AddCommand<GenerateHtmlCommand>("html")
                    .WithDescription("Generate HTML files from source documents")
                    .WithExample("html", "-i", "docs", "-o", "output", "-t", "report")
                    .WithExample("html", "--from-db", "-q", "SELECT * FROM Content");

                config.AddCommand<SyncAllCommand>("all")
                    .WithDescription("Sync to Jira + Confluence + generate HTML (full pipeline)")
                    .WithExample("all", "-i", "docs")
                    .WithExample("all", "--no-html", "-i", "docs")
                    .WithExample("all", "--from-db", "-q", "SELECT * FROM SyncSource");

                config.AddCommand<SchemaHtmlCommand>("schema")
                    .WithDescription("Export SQL Server table schemas to HTML using Scriban templates")
                    .WithExample("schema", "-s", "localhost", "-d", "MyDB", "-o", "output/schema")
                    .WithExample("schema", "-s", "localhost", "-d", "MyDB", "-t", "Users")
                    .WithExample("schema", "-s", "localhost", "-d", "MyDB", "-f", "tbl%", "--template", "table-schema.sbnhtml");

                config.AddBranch<ScenarioSettings>("scenario", branch =>
                {
                    branch.SetDescription("Run declarative YAML scenarios (worklog upload, time reports)");
                    branch.AddCommand<RunScenarioCommand>("run")
                        .WithDescription("Execute a YAML scenario by name or path")
                        .WithExample("scenario", "run", "unfilled-time-report")
                        .WithExample("scenario", "run", "unfilled-time-report", "--weeks", "2", "--user", "myuser")
                        .WithExample("scenario", "run", "worklog-upload", "--file", @"..\..\..\WorkTimeUpload\timesheet_20260101.txt", "--apply");
                    branch.AddCommand<ListScenarioCommand>("list")
                        .WithDescription("List available scenario templates")
                        .WithExample("scenario", "list");
                    branch.AddCommand<ValidateScenarioCommand>("validate")
                        .WithDescription("Validate a YAML scenario file")
                        .WithExample("scenario", "validate", "worklog-upload");
                });
            });

            return await app.RunAsync(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "ContextSync terminated unexpectedly");
            AnsiConsole.WriteException(ex);
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}

internal sealed class TypeRegistrar : ITypeRegistrar
{
    private readonly Dictionary<Type, Func<object>> _registrations = new();

    public void Register(Type service, Type implementation)
    {
        _registrations[service] = () => Create(implementation);
    }

    private static object Create(Type type)
    {
        var ctors = type.GetConstructors();

        var ctor = ctors.FirstOrDefault(c => c.GetParameters().Length == 0);
        if (ctor != null)
            return Activator.CreateInstance(type)!;

        ctor = ctors.FirstOrDefault(c => c.GetParameters().Length == 1
            && c.GetParameters()[0].ParameterType == typeof(IAnsiConsole));
        if (ctor != null)
            return Activator.CreateInstance(type, AnsiConsole.Console)!;

        return Activator.CreateInstance(type)!;
    }

    public void RegisterInstance(Type service, object implementation)
    {
        _registrations[service] = () => implementation;
    }

    public void RegisterLazy(Type service, Func<object> factory)
    {
        _registrations[service] = factory;
    }

    public ITypeResolver Build()
    {
        return new TypeResolver(_registrations);
    }
}

internal sealed class TypeResolver : ITypeResolver
{
    private readonly Dictionary<Type, Func<object>> _registrations;

    public TypeResolver(Dictionary<Type, Func<object>> registrations)
    {
        _registrations = registrations;
    }

    public object? Resolve(Type? type)
    {
        if (type == null)
            return null;

        if (type == typeof(IAnsiConsole))
            return AnsiConsole.Console;

        if (_registrations.TryGetValue(type, out var factory))
            return factory();

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            var elementType = type.GetGenericArguments()[0];
            var element = Resolve(elementType);
            if (element != null)
                return Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType), new[] { element });

            return Array.CreateInstance(elementType, 0);
        }

        if (type.IsClass && !type.IsAbstract)
        {
            try
            {
                return CreateType(type);
            }
            catch { }
        }

        return null;
    }

    private static object CreateType(Type type)
    {
        var ctors = type.GetConstructors();

        var ctor = ctors.FirstOrDefault(c => c.GetParameters().Length == 0);
        if (ctor != null)
            return Activator.CreateInstance(type)!;

        ctor = ctors.FirstOrDefault(c => c.GetParameters().Length == 1
            && c.GetParameters()[0].ParameterType == typeof(IAnsiConsole));
        if (ctor != null)
            return Activator.CreateInstance(type, AnsiConsole.Console)!;

        return Activator.CreateInstance(type)!;
    }
}
