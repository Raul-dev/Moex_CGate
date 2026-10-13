using Spectre.Console;
using Scriban.Runtime;
using ContextSync.Abstractions;

namespace ContextSync.Scenarios;

public interface IScenarioStepHandler
{
    string Type { get; }

    Task<object?> ExecuteAsync(ScenarioStep step, ScenarioExecContext ctx, CancellationToken cancellationToken);
}

public sealed class ScenarioExecContext
{
    public required IAnsiConsole Console { get; init; }
    public required ScriptObject Root { get; init; }
    public required ScenarioDefinition Definition { get; init; }
    public required ScenarioContext Scenario { get; init; }
    public required bool DryRun { get; init; }
    public required Func<IJiraService> GetJira { get; init; }
    public List<string> Warnings { get; } = new();
}
