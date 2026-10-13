using System.Globalization;
using System.Text;
using ContextSync.Scenarios;
using Scriban;
using Scriban.Runtime;

namespace ContextSync.Scenarios.Handlers;

public sealed class FileParseSheetHandler : IScenarioStepHandler
{
    public string Type => "file.parse_sheet";

    public Task<object?> ExecuteAsync(ScenarioStep step, ScenarioExecContext ctx, CancellationToken cancellationToken)
    {
        var args = ScribanEvaluator.RenderArgs(step.Args, ctx.Root);
        var file = args.GetString("file", "");

        if (string.IsNullOrWhiteSpace(file))
            throw new ScenarioStepException("file.parse_sheet: args.file is required");

        if (!File.Exists(file))
            throw new ScenarioStepException($"file.parse_sheet: input file not found: {file}");

        var full = Path.GetFullPath(file);
        var format = ResolveFormat(args, ctx.Definition)
                     ?? throw new ScenarioStepException("file.parse_sheet: file_format section is required");

        var encoding = ResolveEncoding(format.Encoding);
        var text = File.ReadAllText(full, encoding);
        var parsed = SheetParser.Parse(text, format);

        var maxHours = args.GetNumber("max_hours") ?? 0.0;
        var result = DistributeHours(parsed, maxHours);

        foreach (var warning in result.Warnings)
            ctx.Warnings.Add($"{Path.GetFileName(full)}: {warning}");

        var items = result.Entries.Select(entry =>
        {
            var item = new ScriptObject();
            item["key"] = entry.Key;
            item["date"] = entry.DateIso;
            item["hours"] = (object?)entry.Hours ?? 0.0;
            item["note"] = entry.Note;
            item["url"] = entry.Url;
            item["line"] = (long)entry.LineNumber;
            return item;
        }).ToList();

        var row = new ScriptObject
        {
            ["file"] = full,
            ["entries"] = items,
            ["count"] = result.Entries.Count
        };

        return Task.FromResult<object?>(row);
    }

    /// <summary>
    /// Распределяет дневной бюджет (max_hours_per_day) равномерно по записям дня, у которых часы не указаны.
    /// Если бюджет уже исчерпан другими записями, недозаполненные записи остаются с 0 и будут пропущены при загрузке.
    /// </summary>
    public static SheetParseResult DistributeHours(SheetParseResult parsed, double maxHoursPerDay)
    {
        var warnings = new List<string>(parsed.Warnings);

        if (maxHoursPerDay <= 0)
        {
            foreach (var entry in parsed.Entries.Where(e => e.Hours is null or <= 0))
                warnings.Add($"entry {entry.Key} {entry.DateIso}: no hours set and args.max_hours is missing - hours stay empty");
            return new SheetParseResult(parsed.Entries, warnings);
        }

        var output = new List<SheetEntry>();

        foreach (var group in parsed.Entries.GroupBy(e => e.DateIso).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var unknown = group.Where(e => e.Hours is null or <= 0).ToList();
            var known = group.Where(e => e.Hours > 0).Sum(e => e.Hours!.Value);
            var remainder = Math.Max(0, Math.Round(maxHoursPerDay - known, 2));

            if (remainder == 0 && unknown.Count > 0)
                warnings.Add($"{group.Key}: day budget reached ({known:0.##}h), {unknown.Count} entry/ies without hours resolve to 0 and will be skipped");

            // Доли кратны целым часам: базовая часть floor(остаток/записи), недостающий остаток раскидывается по первым записям по +1h.
            var shares = new List<double>();
            if (unknown.Count > 0)
            {
                var wholeRemainder = (int)Math.Floor(remainder);
                var fractional = Math.Round(remainder - wholeRemainder, 2);
                if (fractional > 0)
                    warnings.Add($"{group.Key}: fractional remainder {fractional:0.##}h dropped (shares are whole hours)");

                var baseShare = wholeRemainder / unknown.Count;
                var leftover = wholeRemainder - baseShare * unknown.Count;

                for (var i = 0; i < unknown.Count; i++)
                    shares.Add(baseShare + (i < leftover ? 1 : 0));
            }

            var unknownIndex = 0;
            foreach (var entry in group)
            {
                if (entry.Hours is null or <= 0)
                    output.Add(entry with { Hours = shares[unknownIndex++] });
                else
                    output.Add(entry);
            }
        }

        return new SheetParseResult(output, warnings);
    }

    private static ScenarioFileFormat ResolveFormat(Dictionary<string, object?> args, ScenarioDefinition definition)
    {
        if (!args.TryGetValue("format", out var raw) || raw == null)
        {
            if (definition.FileFormat == null)
                throw new ScenarioStepException("file.parse_sheet: file_format section is required");
            return definition.FileFormat;
        }

        if (raw is string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                if (definition.FileFormat == null)
                    throw new ScenarioStepException("file.parse_sheet: file_format section is required");
                return definition.FileFormat;
            }
            throw new ScenarioStepException($"file.parse_sheet: args.format '{name}' is not supported. Remove args.format to use the file_format section.");
        }

        throw new ScenarioStepException("file.parse_sheet: args.format must be empty; the file_format section is always used");
    }

    private static Encoding ResolveEncoding(string name)
    {
        var trimmed = (name ?? "").Trim();

        if (trimmed.Length == 0 || trimmed.Equals("UTF-8", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("UTF8", StringComparison.OrdinalIgnoreCase))
            return new UTF8Encoding(false);

        if (trimmed.Equals("UTF-8-BOM", StringComparison.OrdinalIgnoreCase))
            return new UTF8Encoding(true);

        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(trimmed);
        }
        catch (Exception ex)
        {
            throw new ScenarioStepException($"file.parse_sheet: unknown encoding '{trimmed}' ({ex.Message})");
        }
    }
}
