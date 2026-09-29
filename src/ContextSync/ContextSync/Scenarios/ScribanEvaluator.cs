using Scriban;
using Scriban.Runtime;

namespace ContextSync.Scenarios;

public static class ScribanEvaluator
{
    public static string RenderString(string templateText, ScriptObject root)
    {
        var template = Template.Parse(templateText);
        if (template.HasErrors)
            throw new ScenarioStepException($"Scriban template parse error for value '{templateText}': " + string.Join("; ", template.Messages.Select(m => m.Message)));

        return template.Render(root) ?? "";
    }

    public static Dictionary<string, object?> RenderArgs(Dictionary<string, object?> args, ScriptObject root)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in args)
            result[kv.Key] = RenderValue(kv.Value, root);
        return result;
    }

    private static object? RenderValue(object? value, ScriptObject root) => value switch
    {
        string s => RenderString(s, root),
        List<object?> list => list.Select(item => RenderValue(item, root)).ToList(),
        Dictionary<string, object?> dict => RenderArgs(dict, root),
        _ => value
    };
}
