using System.Globalization;
using ContextSync.Scenarios;
using Scriban;
using Scriban.Runtime;

namespace ContextSync.Scenarios.Handlers;

public sealed class CalendarWorkdaysHandler : IScenarioStepHandler
{
    public string Type => "calendar.workdays";

    public Task<object?> ExecuteAsync(ScenarioStep step, ScenarioExecContext ctx, CancellationToken cancellationToken)
    {
        var args = ScribanEvaluator.RenderArgs(step.Args, ctx.Root);

        var fromRaw = args.GetString("from", "");
        var toRaw = args.GetString("to", "");

        DateTime? from = TryParseDate(fromRaw);
        DateTime? to = TryParseDate(toRaw);

        var weekStart = ParseWeekStart(args.GetString("week_start", "monday"));
        var holidays = args.GetList("holidays")
            ?.Where(h => h != null)
            .Select(h => Convert.ToString(h, CultureInfo.InvariantCulture) ?? "")
            .Where(h => h.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (from == null || to == null || from > to)
        {
            var weeks = ResolveWeeks(args.GetString("weeks", "2"));
            var baseStart = StartOfWeek(DateTime.Today, weekStart);
            from = baseStart.AddDays(-7 * (weeks - 1));
            to = baseStart.AddDays(7 * weeks - 1);
        }

        var days = new List<string>();
        for (var day = from.Value.Date; day <= to.Value.Date; day = day.AddDays(1))
        {
            if (day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday)
                continue;
            var iso = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (holidays.Contains(iso))
                continue;
            days.Add(iso);
        }

        var row = new ScriptObject
        {
            ["list"] = days.Cast<object?>().ToList(),
            ["range_from"] = from.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["range_to"] = to.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["count"] = days.Count
        };

        return Task.FromResult<object?>(row);
    }

    internal static int ResolveWeeks(object? raw)
    {
        if (raw is string s && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var weeks))
            return Math.Max(1, weeks);
        return 2;
    }

    internal static DayOfWeek ParseWeekStart(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "sunday" or "sun" => DayOfWeek.Sunday,
            _ => DayOfWeek.Monday
        };
    }

    internal static DateTime StartOfWeek(DateTime day, DayOfWeek weekStart)
    {
        var diff = ((int)day.DayOfWeek - (int)weekStart + 7) % 7;
        return day.Date.AddDays(-diff);
    }

    private static DateTime? TryParseDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var trimmed = value.Trim();
        if (trimmed.Length == 8 && trimmed.All(char.IsDigit))
            return DateTime.TryParseExact(trimmed, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
        return DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result) ? result : null;
    }
}

public static class CalendarArgsExtensions
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string GetString(this Dictionary<string, object?> dict, string key, string fallback)
    {
        if (!dict.TryGetValue(key, out var value) || value == null)
            return fallback;
        if (value is string s) return s;
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? fallback;
    }

    public static List<object?>? GetListValue(this Dictionary<string, object?> dict, string key) =>
        dict.TryGetValue(key, out var value) && value is List<object?> list ? list : null;

    public static double? GetNumber(this Dictionary<string, object?> dict, string key)
    {
        if (!dict.TryGetValue(key, out var value) || value == null)
            return null;

        return value switch
        {
            long l => l,
            double d => d,
            int i => i,
            string s when double.TryParse(s, NumberStyles.Float, Invariant, out var parsed) => parsed,
            _ => null
        };
    }

    public static bool GetBoolValue(this Dictionary<string, object?> dict, string key, bool fallback)
    {
        if (!dict.TryGetValue(key, out var value) || value == null)
            return fallback;
        return value switch
        {
            bool b => b,
            string s => string.Equals(s, "true", StringComparison.OrdinalIgnoreCase),
            _ => fallback
        };
    }
}
