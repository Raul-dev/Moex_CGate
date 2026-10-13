using System.Globalization;
using System.Text.RegularExpressions;

namespace ContextSync.Scenarios.Handlers;

public sealed record SheetEntry(string Key, string DateIso, double? Hours, string? Note, string Url, int LineNumber);

public sealed record SheetParseResult(List<SheetEntry> Entries, List<string> Warnings);

public static class SheetParser
{
    public static SheetParseResult Parse(string text, ScenarioFileFormat format)
    {
        if (string.IsNullOrWhiteSpace(format.DateLineRegex))
            throw new ScenarioStepException("file_format.date_line is required");
        if (string.IsNullOrWhiteSpace(format.EntryLineRegex))
            throw new ScenarioStepException("file_format.entry_line is required");

        var dateRegex = new Regex(format.DateLineRegex, RegexOptions.Compiled);
        var entryRegex = new Regex(format.EntryLineRegex, RegexOptions.Compiled);

        var entries = new List<SheetEntry>();
        var warnings = new List<string>();
        DateTime? currentDate = null;

        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var whole = lines[i].TrimEnd('\r');
            var line = whole.Trim();
            var lineNumber = i + 1;

            if (format.SkipBlank && string.IsNullOrWhiteSpace(line))
                continue;

            var dateMatch = dateRegex.Match(line);
            if (dateMatch.Success)
            {
                var dateGroup = dateMatch.Groups["date"];
                if (dateGroup.Success)
                {
                    var parsed = ParseDate(dateGroup.Value, format.DateFormat);
                    if (parsed == null)
                        warnings.Add($"line {lineNumber}: invalid date '{dateGroup.Value}'");
                    else
                        currentDate = parsed;
                }
                else
                {
                    warnings.Add($"line {lineNumber}: date_line pattern must contain named group 'date'");
                }
                continue;
            }

            var entryMatch = entryRegex.Match(line);
            if (entryMatch.Success)
            {
                if (currentDate == null)
                {
                    warnings.Add($"line {lineNumber}: entry before any date line, skipped: {line}");
                    continue;
                }

                var dateIso = currentDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                var hoursGroup = entryMatch.Groups["hours"];
                var hours = ParseHours(hoursGroup.Value);
                if (hoursGroup.Success && hoursGroup.Value.Length > 0 && hours == null)
                    warnings.Add($"line {lineNumber}: cannot parse hours token '{hoursGroup.Value}'");

                var note = entryMatch.Groups["note"];
                entries.Add(new SheetEntry(
                    entryMatch.Groups["key"].Value,
                    dateIso,
                    hours,
                    note.Success && note.Value.Length > 0 ? note.Value : null,
                    line,
                    lineNumber));
                continue;
            }

            if (line.Length > 0)
                warnings.Add($"line {lineNumber}: line does not match any pattern, skipped: {line}");
        }

        return new SheetParseResult(entries, warnings);
    }

    private static DateTime? ParseDate(string value, string dateFormat)
    {
        try
        {
            return DateTime.ParseExact(value, dateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static double? ParseHours(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;
        var normalized = token.Trim().TrimEnd('h', 'H').Trim();
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var hours)
            ? hours
            : null;
    }
}
