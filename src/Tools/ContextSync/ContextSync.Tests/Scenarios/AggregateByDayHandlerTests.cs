using System.Collections;
using ContextSync.Scenarios;
using ContextSync.Scenarios.Handlers;
using Scriban;
using Scriban.Runtime;
using Xunit;

namespace ContextSync.Tests.Scenarios;

public class AggregateByDayHandlerTests
{
    private static ScriptObject Worklog(string key, string author, string date, double hours)
    {
        var row = new ScriptObject();
        row["issue_key"] = key;
        row["author"] = author;
        row["date"] = date;
        row["hours"] = hours;
        return row;
    }

    private static ScriptObject Run(Dictionary<string, object?> args, List<ScriptObject> worklogs)
    {
        var definition = new ScenarioDefinition { Name = "agg-test" };
        var context = new ScenarioContext(definition);
        context.Root["worklogs"] = worklogs;

        var ctx = new ScenarioExecContext
        {
            Console = Spectre.Console.AnsiConsole.Console,
            Root = context.Root,
            Definition = definition,
            Scenario = context,
            DryRun = true,
            GetJira = () => throw new InvalidOperationException()
        };

        var step = new ScenarioStep { Id = "agg", Action = "aggregate.by_day", Args = args };
        var output = new AggregateByDayHandler().ExecuteAsync(step, ctx, CancellationToken.None).Result;
        return Assert.IsType<ScriptObject>(output);
    }

    private static List<ScriptObject> AsRowList(object? value)
    {
        Assert.IsAssignableFrom<IList>(value);
        return ((IList)value!).Cast<object>().Cast<ScriptObject>().ToList();
    }

    [Fact]
    public void SumsWorklogsByAuthor()
    {
        var args = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["author"] = "myuser",
            ["expected"] = 8
        };

        var result = Run(args, new List<ScriptObject>
        {
            Worklog("TASK-1", "myuser", "2026-01-05", 3.0),
            Worklog("TASK-2", "myuser", "2026-01-05", 2.0),
            Worklog("OTHER-1", "someone-else", "2026-01-05", 5.0),
            Worklog("TASK-2", "myuser", "2026-01-06", 8.0)
        });

        var days = AsRowList(result["days"]);
        Assert.Equal(2, days.Count);

        Assert.Equal(1, result["wrong_days"]);
        Assert.Equal(13.0, ((ScriptObject)result).ContainsKey("total_logged") ? (double)((ScriptObject)result)["total_logged"]! : 0.0);

        var day1 = days[0];
        Assert.Equal("2026-01-05", (string)day1["date"]!);
        Assert.Equal(5.0, (double)day1["logged_hours"]!);
        Assert.Equal(8.0, (double)day1["expected_hours"]!);
        Assert.Equal(-3.0, (double)day1["diff"]!);

        var issues = (IList)day1["issues"]!;
        Assert.Equal(2, issues.Count);

        var day2 = days[1];
        Assert.Equal(8.0, (double)day2["logged_hours"]!);
    }
}
