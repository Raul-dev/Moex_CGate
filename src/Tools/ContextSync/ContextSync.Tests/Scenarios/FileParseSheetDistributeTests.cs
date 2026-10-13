using ContextSync.Scenarios.Handlers;
using Xunit;

namespace ContextSync.Tests.Scenarios;

public class FileParseSheetDistributeTests
{
    private static ContextSync.Scenarios.Handlers.SheetParseResult Run(ContextSync.Scenarios.Handlers.SheetParseResult parsed, double max) =>
        FileParseSheetHandler.DistributeHours(parsed, max);

    [Fact]
    public void TwoEntriesWithoutHoursSplitBudgetEvenly()
    {
        var parsed = new ContextSync.Scenarios.Handlers.SheetParseResult(
            new List<ContextSync.Scenarios.Handlers.SheetEntry>
            {
                new("TASK-38586", "2026-08-03", null, null, "", 2),
                new("TASK-26829", "2026-08-03", null, null, "", 3)
            },
            new List<string>());

        var result = FileParseSheetHandler.DistributeHours(parsed, 8.0);

        Assert.All(result.Entries, e => Assert.Equal(4.0, e.Hours));
    }

    [Fact]
    public void ThreeEntriesGetWholeHours()
    {
        var parsed = new ContextSync.Scenarios.Handlers.SheetParseResult(
            new List<ContextSync.Scenarios.Handlers.SheetEntry>
            {
                new("TASK-A", "2026-09-01", null, null, "", 2),
                new("TASK-B", "2026-09-01", null, null, "", 3),
                new("TASK-C", "2026-09-01", null, null, "", 4)
            },
            new List<string>());

        var result = FileParseSheetHandler.DistributeHours(parsed, 8.0);

        Assert.Equal(3.0, result.Entries[0].Hours);
        Assert.Equal(3.0, result.Entries[1].Hours);
        Assert.Equal(2.0, result.Entries[2].Hours);
    }

    [Fact]
    public void KnownHoursLeaveWholeRemainderForUnknown()
    {
        var parsed = new ContextSync.Scenarios.Handlers.SheetParseResult(
            new List<ContextSync.Scenarios.Handlers.SheetEntry>
            {
                new("TASK-36118", "2026-01-12", 6.0, null, "", 2),
                new("TASK-33804", "2026-01-12", null, null, "", 3)
            },
            new List<string>());

        var result = FileParseSheetHandler.DistributeHours(parsed, 8.0);

        Assert.Equal(6.0, result.Entries[0].Hours);
        Assert.Equal(2.0, result.Entries[1].Hours);
    }

    [Fact]
    public void KnownTwoWithThreeUnknownSplitWhole()
    {
        var parsed = new ContextSync.Scenarios.Handlers.SheetParseResult(
            new List<ContextSync.Scenarios.Handlers.SheetEntry>
            {
                new("TASK-A", "2026-09-01", 2.0, null, "", 2),
                new("TASK-B", "2026-09-01", null, null, "", 3),
                new("TASK-C", "2026-09-01", null, null, "", 4),
                new("TASK-D", "2026-09-01", null, null, "", 5)
            },
            new List<string>());

        var result = FileParseSheetHandler.DistributeHours(parsed, 8.0);

        Assert.Equal(2.0, result.Entries[1].Hours);
        Assert.Equal(2.0, result.Entries[2].Hours);
        Assert.Equal(2.0, result.Entries[3].Hours);
    }

    [Fact]
    public void ExhaustedBudgetResolvesUnknownToZeroWithWarning()
    {
        var parsed = new ContextSync.Scenarios.Handlers.SheetParseResult(
            new List<ContextSync.Scenarios.Handlers.SheetEntry>
            {
                new("TASK-36118", "2026-01-12", 8.0, null, "", 2),
                new("TASK-33804", "2026-01-12", null, null, "", 3)
            },
            new List<string>());

        var result = FileParseSheetHandler.DistributeHours(parsed, 8.0);

        Assert.Equal(0.0, result.Entries[1].Hours);
        Assert.Contains(result.Warnings, w => w.Contains("day budget reached"));
    }

    [Fact]
    public void ZeroMaxHoursAddsMissingHoursWarning()
    {
        var parsed = new ContextSync.Scenarios.Handlers.SheetParseResult(
            new List<ContextSync.Scenarios.Handlers.SheetEntry>
            {
                new("TASK-1", "2026-01-12", null, null, "", 2)
            },
            new List<string>());

        var result = FileParseSheetHandler.DistributeHours(parsed, 0.0);

        Assert.Contains(result.Warnings, w => w.Contains("args.max_hours is missing"));
    }
}
