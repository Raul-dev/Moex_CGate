using System.Globalization;
using Scriban;
using Scriban.Runtime;

namespace ContextSync.Scenarios;

public sealed class ScenarioStepException : Exception
{
    public ScenarioStepException(string message) : base(message) { }
}

public sealed class ScenarioContext
{
    public ScriptObject Root { get; } = new();

    public ScenarioContext(ScenarioDefinition definition)
    {
        Root.Import("env", new Func<string, string>(name => Environment.GetEnvironmentVariable(name) ?? ""));
        Root.Import("now_utc", new Func<string>(() => DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        Root.Import("def", new Func<object?, object?, object?>((value, fallback) =>
        {
            if (value == null || (value is string s && s.Length == 0))
                return fallback;
            return value;
        }));
        Root["scenario"] = new ScriptObject
        {
            ["name"] = definition.Name,
            ["version"] = definition.Version
        };
        Root["inputs"] = ScriptObjectX.From(definition.Inputs);
        Root["defaults"] = ScriptObjectX.From(definition.Defaults);
        if (definition.Connection.Jira != null)
            Root["jira"] = ScriptObjectX.From(new Dictionary<string, object?>
            {
                ["url"] = definition.Connection.Jira.Url,
                ["username"] = definition.Connection.Jira.Username
            });
    }

    public void SetStepResult(string stepId, object? value) => Root[stepId] = value;

    public object? GetPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var segments = path.Split('.');
        object? current = Root;
        foreach (var segment in segments)
        {
            if (segment.Length == 0) return null;
            current = current switch
            {
                ScriptObject so => so.ContainsKey(segment) ? so[segment] : null,
                System.Collections.IList list => int.TryParse(segment, out var index) && index >= 0 && index < list.Count ? list[index] : null,
                _ => null
            };
            if (current == null) return null;
        }
        return current;
    }

    public List<object?>? GetListPath(string path) =>
        GetPath(path) is System.Collections.IList list ? list.Cast<object?>().ToList() : null;
}

public static class ScriptObjectX
{
    public static ScriptObject From(Dictionary<string, object?> source)
    {
        var result = new ScriptObject();
        foreach (var kv in source)
            result[kv.Key] = kv.Value;
        return result;
    }
}
