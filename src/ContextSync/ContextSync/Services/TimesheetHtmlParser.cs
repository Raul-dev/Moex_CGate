using System.Net;
using System.Text.RegularExpressions;
using ContextSync.Scenarios;

namespace ContextSync.Services;

/// <summary>
/// Парсер HTML-выдачи jira-timesheet-plugin (TimesheetReport.jspa, sum=day).
/// Логика: строки детализации идут после заголовка с колонками дней
/// (th с title="dd.MM.yyyy"). Ширина "до дней" берётся из заголовка:
/// сумма colspan всех th перед первым th с title (обычно Проект=1 + Задача=4:
/// иконка, ключ, название, приоритет). Часы читаются по тем же расширенным
/// позициям (с учётом colspan каждой td), формат "2h", "1h 30m", "30m".
/// merged/вложенная разметка отбрасывается по позиции >= ширина_дней + число_дней.
/// </summary>
public static class TimesheetHtmlParser
{
    private const string DetailMarker = "Задача";

    private static readonly Regex RowRegex = new("(?s)<tr(?:\\s[^>]*)?>.*?</tr>", RegexOptions.Compiled);
    private static readonly Regex CellRegex = new("(?s)<(td|th)([^>]*)>(.*?)</\\1>", RegexOptions.Compiled);
    private static readonly Regex IssueKeyRegex = new("browse/([A-Z][A-Z0-9]+-\\d+)", RegexOptions.Compiled);
    private static readonly Regex DayTitleRegex = new("title=\"(\\d{1,2}\\.\\d{1,2}\\.\\d{2,4})", RegexOptions.Compiled);
    private static readonly Regex TagRegex = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex ColspanRegex = new("(?i)colspan\\s*=\\s*[\"']?([0-9]+)", RegexOptions.Compiled);
    private static readonly Regex HoursRegex = new("^(?i)(\\d+)\\s*h(?:our)?(?:\\s+(\\d+)\\s*m(?:in)?)?$", RegexOptions.Compiled);
    private static readonly Regex MinutesOnlyRegex = new("^(?i)(\\d+)\\s*m(?:in)?$", RegexOptions.Compiled);

    public static List<JiraTimesheetRow> Parse(string html)
    {
        if (string.IsNullOrEmpty(html))
            throw new InvalidOperationException("Jira timesheet report: HTML is empty, cannot parse");

        var result = new List<JiraTimesheetRow>();
        List<DateTime>? days = null;
        var preDayWidth = 0;

        foreach (Match rowMatch in RowRegex.Matches(html))
        {
            var rowHtml = rowMatch.Value;

            if (DayTitleRegex.Matches(rowHtml).Count >= 2 && rowHtml.Contains(DetailMarker))
            {
                days = ExtractDays(rowHtml);
                preDayWidth = ComputePreDayWidth(rowHtml);
                continue;
            }

            if (days is not { Count: > 0 } || preDayWidth <= 0)
                continue;

            var keyMatch = IssueKeyRegex.Match(rowHtml);
            if (!keyMatch.Success)
                continue;

            var issueKey = keyMatch.Groups[1].Value;
            var summary = "";
            var found = new List<(int DayIndex, double Hours)>();
            var position = 0;

            foreach (var cell in ParseCells(rowHtml))
            {
                if (position >= preDayWidth + days.Count)
                    break;

                if (position == preDayWidth - 2)
                    summary = cell.Text;

                if (position >= preDayWidth)
                {
                    var hours = ParseHours(cell.Text);
                    if (hours > 0)
                        found.Add((position - preDayWidth, hours));
                }

                position += Math.Max(1, cell.Span);
            }

            foreach (var (dayIndex, hours) in found)
            {
                if (dayIndex >= 0 && dayIndex < days.Count)
                    result.Add(new JiraTimesheetRow(
                        days[dayIndex].ToString("yyyy-MM-dd"),
                        issueKey,
                        summary,
                        hours));
            }
        }

        if (result.Count == 0)
            throw new InvalidOperationException(
                "Jira timesheet report: no detail rows parsed; report layout may have changed");

        return result;
    }

    private static List<(string Text, int Span)> ParseCells(string rowHtml)
    {
        var cells = new List<(string, int)>();
        foreach (Match m in CellRegex.Matches(rowHtml))
        {
            var text = Normalize(TagRegex.Replace(m.Groups[3].Value, " "));
            cells.Add((text, Math.Max(1, GetColspan(m.Groups[2].Value))));
        }
        return cells;
    }

    private static int ComputePreDayWidth(string rowHtml)
    {
        var width = 0;
        foreach (Match m in CellRegex.Matches(rowHtml))
        {
            if (DayTitleRegex.IsMatch(m.Groups[2].Value))
                break;
            width += Math.Max(1, GetColspan(m.Groups[2].Value));
        }
        return width;
    }

    private static int GetColspan(string attributes)
    {
        var m = ColspanRegex.Match(attributes);
        return m.Success && int.TryParse(m.Groups[1].Value, out var value) ? value : 1;
    }

    private static List<DateTime> ExtractDays(string rowHtml)
    {
        var days = new List<DateTime>();
        foreach (Match m in DayTitleRegex.Matches(rowHtml))
        {
            var date = ParseDayTitle(m.Groups[1].Value);
            if (date.HasValue)
                days.Add(date.Value);
        }
        return days;
    }

    private static DateTime? ParseDayTitle(string value)
    {
        var parts = value.Split('.');
        if (parts.Length != 3)
            return null;
        if (!int.TryParse(parts[0], out var day) || !int.TryParse(parts[1], out var month) ||
            !int.TryParse(parts[2], out var year))
            return null;
        if (year < 100)
            year += year < 70 ? 2000 : 1900;
        try
        {
            return new DateTime(year, month, day);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static double ParseHours(string cellText)
    {
        var text = cellText.Trim();
        if (text.Length == 0)
            return 0;

        var hours = 0;
        var minutes = 0;
        Match m = HoursRegex.Match(text);
        if (m.Success)
        {
            hours = int.Parse(m.Groups[1].Value);
            if (m.Groups[2].Success)
                minutes = int.Parse(m.Groups[2].Value);
        }
        else
        {
            m = MinutesOnlyRegex.Match(text);
            if (!m.Success)
                return 0;
            minutes = int.Parse(m.Groups[1].Value);
        }

        return Math.Round(hours + minutes / 60.0, 2);
    }

    private static string Normalize(string text)
    {
        text = WebUtility.HtmlDecode(text);
        return Regex.Replace(text, "\\s+", " ").Trim();
    }
}
