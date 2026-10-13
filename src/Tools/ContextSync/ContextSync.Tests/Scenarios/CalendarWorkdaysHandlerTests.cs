using ContextSync.Scenarios;
using ContextSync.Scenarios.Handlers;
using Scriban;
using Scriban.Runtime;
using Xunit;

namespace ContextSync.Tests.Scenarios;

public class CalendarWorkdaysHandlerTests
{
    [Fact]
    public void ExplicitRangeSkipsWeekends()
    {
        var result = Execute("2026-01-05", "2026-01-07");

        Assert.Equal(3, result["count"]);
        Assert.Equal("2026-01-05", result["range_from"]);
        Assert.Equal("2026-01-07", result["range_to"]);

        var days = Assert.IsType<List<object?>>(result["list"]);
        Assert.Equal(new[] { "2026-01-05", "2026-01-06", "2026-01-07" }, days.Select(d => d!.ToString()));
    }

    [Fact]
    public void HolidaysExcluded()
    {
        var result = Execute("2026-01-05", "2026-01-07", new List<object?> { "2026-01-06" });

        Assert.Equal(2, result["count"]);
        var days = Assert.IsType<List<object?>>(result["list"]);
        Assert.DoesNotContain("2026-01-06", days.Select(d => d!.ToString()!));
    }

    [Fact]
    public void WeekendOnlyRangeIsEmpty()
    {
        var result = Execute("2026-01-10", "2026-01-11");

        Assert.Equal(0, result["count"]);
    }

    [Fact]
    public void StartOfWeekMonday()
    {
        var today = new DateTime(2026, 9, 4);
        Assert.Equal(new DateTime(2026, 8, 31), CalendarWorkdaysHandler.StartOfWeek(today, DayOfWeek.Monday));
    }

    [Theory]
    [InlineData("monday", DayOfWeek.Monday)]
    [InlineData("sunday", DayOfWeek.Sunday)]
    [InlineData("MON", DayOfWeek.Monday)]
    [InlineData("unknown", DayOfWeek.Monday)]
    public void WeekStartParsing(string input, DayOfWeek expected)
    {
        Assert.Equal(expected, CalendarWorkdaysHandler.ParseWeekStart(input));
    }

    private static ScriptObject Execute(string from, string to, List<object?>? holidays = null)
    {
        var args = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["from"] = from,
            ["to"] = to
        };
        if (holidays != null)
            args["holidays"] = holidays;

        var definition = new ScenarioDefinition { Name = "test" };
        var context = new ScenarioContext(definition);
        var ctx = new ScenarioExecContext
        {
            Console = Spectre.Console.AnsiConsole.Console,
            Root = context.Root,
            Definition = definition,
            Scenario = context,
            DryRun = true,
            GetJira = () => throw new InvalidOperationException("not needed")
        };

        var step = new ScenarioStep { Id = "days", Action = "calendar.workdays", Args = args };
        var output = new CalendarWorkdaysHandler().ExecuteAsync(step, ctx, CancellationToken.None).Result;
        return Assert.IsType<ScriptObject>(output);
    }
}
