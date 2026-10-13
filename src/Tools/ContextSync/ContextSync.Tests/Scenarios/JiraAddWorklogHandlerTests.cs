using ContextSync.Scenarios;
using ContextSync.Scenarios.Handlers;
using Scriban;
using Scriban.Runtime;
using Xunit;

namespace ContextSync.Tests.Scenarios;

public class JiraAddWorklogHandlerTests
{
    private static ScriptObject RunDry(Dictionary<string, object?> args)
    {
        var definition = new ScenarioDefinition { Name = "t" };
        definition.Connection = new ScenarioConnection
        {
            Jira = new ScenarioJiraConnection { Url = "https://jira.mydomen.ru", Username = "myuser" }
        };

        var context = new ScenarioContext(definition);
        var ctx = new ScenarioExecContext
        {
            Console = Spectre.Console.AnsiConsole.Console,
            Root = context.Root,
            Definition = definition,
            Scenario = context,
            DryRun = true,
            GetJira = () => throw new InvalidOperationException("dry-run must not touch jira")
        };

        var step = new ScenarioStep { Id = "w", Action = "jira.add_worklog", Args = args };
        var output = new JiraAddWorklogHandler().ExecuteAsync(step, ctx, CancellationToken.None).GetAwaiter().GetResult();
        return Assert.IsType<ScriptObject>(output);
    }

    [Fact]
    public void DryRunComposesStartedAndTimeSpent()
    {
        var row = RunDry(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["issue_key"] = "TASK-36118",
            ["date"] = "2026-01-12",
            ["hours"] = 6,
            ["comment"] = ".",
            ["account"] = "TASK-разработка",
            ["account_field"] = ""
        });

        Assert.Equal("TASK-36118", row["issue_key"]);
        Assert.Equal("2026-01-12", row["date"]);
        Assert.Equal("6h", row["time_spent"]);
        Assert.Equal("dry", row["status"]);
        Assert.True((bool)row["dry_run"]);
    }

    [Fact]
    public void MissingDateAndStartedThrows()
    {
        var ex = Assert.Throws<ScenarioStepException>(() => RunDry(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["issue_key"] = "TASK-1"
        }));

        Assert.Contains("args.started or args.date", ex.Message);
    }

    [Fact]
    public void MissingHoursAndTimeSpentThrows()
    {
        var ex = Assert.Throws<ScenarioStepException>(() => RunDry(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["issue_key"] = "TASK-1",
            ["date"] = "2026-01-12"
        }));

        Assert.Contains("args.time_spent", ex.Message);
        Assert.Contains("args.hours", ex.Message);
    }
}
