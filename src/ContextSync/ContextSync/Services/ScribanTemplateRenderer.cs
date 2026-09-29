using System.Collections.Concurrent;
using ContextSync.Abstractions;
using Scriban;
using Serilog;

namespace ContextSync.Services;

public class ScribanTemplateRenderer : ITemplateRenderer
{
    private readonly string _templatesDir;
    private readonly ConcurrentDictionary<string, Template> _cache = new();

    public ScribanTemplateRenderer(string templatesDir = "Templates")
    {
        _templatesDir = templatesDir;
    }

    public string Render(string templateName, object model)
    {
        var template = GetOrLoadTemplate(templateName);
        return template.Render(model);
    }

    public string RenderText(string templateText, object model)
    {
        var template = Template.Parse(templateText);

        if (template.HasErrors)
        {
            var errors = string.Join("\n", template.Messages.Select(m => m.Message));
            Log.Error("Scriban parse error:\n{Errors}", errors);
            throw new InvalidOperationException($"Scriban template parse error: {errors}");
        }

        return template.Render(model);
    }

    private Template GetOrLoadTemplate(string templateName)
    {
        if (_cache.TryGetValue(templateName, out var cached))
            return cached;

        var filePath = Path.Combine(_templatesDir, templateName);

        if (!File.Exists(filePath))
            filePath = Path.Combine(AppContext.BaseDirectory, _templatesDir, templateName);

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Scriban template not found: {templateName} (searched in {_templatesDir})", templateName);

        var text = File.ReadAllText(filePath);
        var template = Template.Parse(text, filePath);

        if (template.HasErrors)
        {
            var errors = string.Join("\n", template.Messages.Select(m => m.Message));
            throw new InvalidOperationException($"Scriban template parse error in '{templateName}':\n{errors}");
        }

        _cache[templateName] = template;
        return template;
    }
}
