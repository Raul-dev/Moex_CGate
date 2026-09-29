using ContextSync.Scenarios;
using Scriban.Runtime;
using ContextSync.Scenarios.Handlers;
using Xunit;

namespace ContextSync.Tests.Scenarios;

public class SheetParserTests
{
    private static ScenarioFileFormat Format() => new()
    {
        DateLineRegex = @"^(?<date>\d{8})$",
        EntryLineRegex = @"^https?://\S+/browse/(?<key>[A-Z][A-Z0-9]+-\d+)(\s+(?<hours>\d+)(h|С‡))?\s?(\s+(?<note>\S+.*))?$",
        SkipBlank = true,
        DateFormat = "yyyyMMdd"
    };

    [Fact]
    public void ParsesFullBlock()
    {
        var text = string.Join("\n",
            "20260112",
            "https://jira.mydomen.ru/browse/TASK-36118 6h notes/TASK-36118",
            "https://jira.mydomen.ru/browse/TASK-33804 2h");

        var result = SheetParser.Parse(text, Format());

        Assert.Equal(2, result.Entries.Count);
        Assert.Empty(result.Warnings);

        var first = result.Entries[0];
        Assert.Equal("TASK-36118", first.Key);
        Assert.Equal("2026-01-12", first.DateIso);
        Assert.Equal(6.0, first.Hours);
        Assert.False(string.IsNullOrEmpty(first.Note));

        var second = result.Entries[1];
        Assert.Equal("TASK-33804", second.Key);
        Assert.Equal(2.0, second.Hours);
        Assert.Null(second.Note);
    }

    [Fact]
    public void EntryWithoutHoursYieldsNullHours()
    {
        var text = string.Join("\n",
            "20260114",
            "https://jira.mydomen.ru/browse/TASK-33804");

        var result = SheetParser.Parse(text, Format());

        var entry = Assert.Single(result.Entries);
        Assert.Null(entry.Hours);
        Assert.Equal("2026-01-14", entry.DateIso);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void UnmatchedLinesGenerateWarnings()
    {
        var text = string.Join("\n",
            "20260112",
            "garbage-line");

        var result = SheetParser.Parse(text, Format());

        Assert.Empty(result.Entries);
        Assert.Single(result.Warnings);
        Assert.Contains("line 2", result.Warnings[0]);
    }

    [Fact]
    public void EntryBeforeDateLineIsSkipped()
    {
        var text = "https://jira.mydomen.ru/browse/TASK-1 4h";

        var result = SheetParser.Parse(text, Format());

        Assert.Empty(result.Entries);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void InvalidDateTextProducesWarning()
    {
        var text = string.Join("\n",
            "20269999",
            "https://jira.mydomen.ru/browse/TASK-1 4h");
        var format = new ScenarioFileFormat
        {
            DateLineRegex = @"^(?<date>\d{8})$",
            EntryLineRegex = @"^https?://\S+",
            SkipBlank = true,
            DateFormat = "yyyyMMdd"
        };

        var result = SheetParser.Parse(text, format);

        Assert.Empty(result.Entries);
        Assert.Contains(result.Warnings, w => w.Contains("invalid date"));
    }
}
