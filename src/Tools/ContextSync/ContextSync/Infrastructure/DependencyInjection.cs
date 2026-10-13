using ContextSync.Abstractions;
using ContextSync.dal.Abstractions;
using ContextSync.dal.Models;
using ContextSync.dal.Readers;
using ContextSync.dal.Settings;
using ContextSync.Infrastructure.Settings;
using ContextSync.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ContextSync.Infrastructure;

public static class DependencyInjection
{
    public static IServiceProvider ConfigureServices(IConfiguration configuration)
    {
        var settings = new AppSettings();
        configuration.Bind(settings);

        var services = new ServiceCollection();

        services.AddSingleton(settings);
        services.AddSingleton(settings.Jira);
        services.AddSingleton(settings.Confluence);
        services.AddSingleton(settings.Database);
        services.AddSingleton(settings.Html);
        services.AddSingleton(settings.FileSource);

        services.AddHttpClient<IJiraService, JiraService>();
        services.AddHttpClient<IConfluenceService, ConfluenceService>();

        services.AddSingleton<IHtmlGeneratorService, HtmlGeneratorService>();
        services.AddSingleton<ISyncOrchestrator, SyncOrchestrator>();

        services.AddTransient<ISchemaReader>(sp =>
        {
            var db = sp.GetRequiredService<DatabaseSettings>();
            return new SchemaReader(db);
        });

        services.AddTransient<ITemplateRenderer>(sp =>
        {
            var htmlSettings = sp.GetRequiredService<HtmlSettings>();
            return new ScribanTemplateRenderer(htmlSettings.Template);
        });

        services.AddTransient<ISourceReader>(sp =>
        {
            var fileSettings = sp.GetRequiredService<FileSourceSettings>();
            return new FileSourceReader(
                fileSettings.InputDirectory,
                fileSettings.FilePattern,
                fileSettings.Recursive,
                fileSettings.Encoding);
        });

        return services.BuildServiceProvider();
    }

    public static IServiceProvider ConfigureServicesWithDbReader(IConfiguration configuration, DatabaseSettings? dbOverride = null)
    {
        var settings = new AppSettings();
        configuration.Bind(settings);

        if (dbOverride != null)
            settings.Database = dbOverride;

        var services = new ServiceCollection();

        services.AddSingleton(settings);
        services.AddSingleton(settings.Jira);
        services.AddSingleton(settings.Confluence);
        services.AddSingleton(settings.Database);
        services.AddSingleton(settings.Html);
        services.AddSingleton(settings.FileSource);

        services.AddHttpClient<IJiraService, JiraService>();
        services.AddHttpClient<IConfluenceService, ConfluenceService>();

        services.AddSingleton<IHtmlGeneratorService, HtmlGeneratorService>();
        services.AddSingleton<ISyncOrchestrator, SyncOrchestrator>();

        services.AddTransient<ISchemaReader>(sp =>
        {
            var db = sp.GetRequiredService<DatabaseSettings>();
            return new SchemaReader(db);
        });

        services.AddTransient<ITemplateRenderer>(sp =>
        {
            var htmlSettings = sp.GetRequiredService<HtmlSettings>();
            return new ScribanTemplateRenderer(htmlSettings.Template);
        });

        services.AddTransient<DatabaseSourceReader>(sp =>
        {
            var db = sp.GetRequiredService<DatabaseSettings>();
            return new DatabaseSourceReader(db);
        });

        services.AddTransient<ISourceReader>(sp =>
        {
            var fileSettings = sp.GetRequiredService<FileSourceSettings>();
            return new FileSourceReader(
                fileSettings.InputDirectory,
                fileSettings.FilePattern,
                fileSettings.Recursive,
                fileSettings.Encoding);
        });

        return services.BuildServiceProvider();
    }
}
