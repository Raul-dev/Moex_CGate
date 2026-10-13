namespace ContextSync.Scenarios;

public sealed class ScenarioLoadException : Exception
{
    public List<string> Errors { get; }

    public ScenarioLoadException(string message, List<string> errors) : base(message)
    {
        Errors = errors;
    }
}

public static class ScenarioLoader
{
    public const string DefaultTemplatesDir = @"..\..\..\WorkTimeUpload\template";

    private static readonly string[] KnownActions =
    [
        "jira.test_connection",
        "jira.search",
        "jira.get_worklogs",
        "jira.add_worklog",
        "jira.timesheet",
        "jira.tempo_worklogs",
        "file.parse_sheet",
        "calendar.workdays",
        "aggregate.by_day",
        "report.console",
        "report.day_totals",
        "report.link"
    ];

    public static string ResolvePath(string nameOrPath, string? templatesDir = null)
    {
        var dir = templatesDir ?? DefaultTemplatesDir;

        if (File.Exists(nameOrPath))
            return Path.GetFullPath(nameOrPath);

        var directToDir = Path.Combine(dir, Path.GetFileName(nameOrPath));
        if (File.Exists(directToDir))
            return Path.GetFullPath(directToDir);

        if (string.IsNullOrWhiteSpace(Path.GetExtension(nameOrPath)))
        {
            var candidate = Path.Combine(dir, nameOrPath + ".yaml");
            if (File.Exists(candidate)) return candidate;
        }

        throw new FileNotFoundException($"Scenario '{nameOrPath}' not found (directory: {dir})", nameOrPath);
    }

    public static List<(string Name, string Description, string Path)> ListTemplates(string? templatesDir = null)
    {
        var dir = templatesDir ?? DefaultTemplatesDir;
        var list = new List<(string, string, string)>();

        if (!Directory.Exists(dir))
            return list;

        foreach (var file in Directory.EnumerateFiles(dir, "*.yaml")
                     .Concat(Directory.EnumerateFiles(dir, "*.yml")).OrderBy(f => f))
        {
            try
            {
                var def = Load(file);
                list.Add((def.Name, def.Description, file));
            }
            catch (Exception)
            {
                list.Add((Path.GetFileNameWithoutExtension(file), "(failed to load)", Path.GetFullPath(file)));
            }
        }

        return list;
    }

    public static ScenarioDefinition Load(string path, Dictionary<string, string?>? inputOverrides = null)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Scenario file not found: {path}", path);

        var yaml = File.ReadAllText(path);
        var (document, parseErrors) = YamlNormalizer.ParseDocument(yaml);
        if (parseErrors.Count > 0)
            throw new ScenarioLoadException($"Scenario YAML parse errors in '{path}':\n  " + string.Join("\n  ", parseErrors), parseErrors);

        var def = Map(document, path);

        var errors = Validate(def);
        if (errors.Count > 0)
            throw Fail(errors, path);

        ApplyInputOverrides(def, inputOverrides);
        return def;
    }

    private static ScenarioLoadException Fail(List<string> errors, string path)
    {
        var message = errors.Count == 1
            ? $"Scenario validation error in '{path}': {errors[0]}"
            : $"Scenario validation errors in '{path}':\n  " + string.Join("\n  ", errors);
        return new ScenarioLoadException(message, errors);
    }

    private static ScenarioDefinition Map(Dictionary<string, object?> document, string path)
    {
        var def = new ScenarioDefinition
        {
            Name = document.GetString("name", Path.GetFileNameWithoutExtension(path)),
            Description = document.GetString("description", ""),
            Version = document.Get<int>("version") ?? 1,
            Connection = MapConnection(document),
            FileFormat = MapFileFormat(document),
            Inputs = document.GetDictionary("inputs") ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
            Defaults = document.GetDictionary("defaults") ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
            Steps = MapSteps(document)
        };
        return def;
    }

    private static ScenarioConnection MapConnection(Dictionary<string, object?> document)
    {
        var result = new ScenarioConnection();
        var connection = document.GetDictionary("connection");
        if (connection == null) return result;

        var jira = connection.GetDictionary("jira");
        if (jira != null)
        {
            result.Jira = new ScenarioJiraConnection
            {
                Url = jira.GetString("url", ""),
                Username = jira.GetString("username", ""),
                Token = jira.GetString("token", ""),
                PasswordProtected = jira.GetString("password_protected", ""),
                TimeoutSeconds = jira.Get<int>("timeout_seconds") ?? 30,
                MaxRetries = jira.Get<int>("max_retries") ?? 3
            };
        }
        return result;
    }

    private static ScenarioFileFormat? MapFileFormat(Dictionary<string, object?> document)
    {
        var format = document.GetDictionary("file_format");
        if (format == null) return null;

        return new ScenarioFileFormat
        {
            DateLineRegex = format.GetString("date_line", ""),
            EntryLineRegex = format.GetString("entry_line", ""),
            SkipBlank = format.Get<bool>("skip_blank") ?? true,
            DateFormat = format.GetString("date_format", "yyyyMMdd"),
            Encoding = format.GetString("encoding", "UTF-8")
        };
    }

    private static List<ScenarioStep> MapSteps(Dictionary<string, object?> document)
    {
        var steps = new List<ScenarioStep>();
        var list = document.GetList("steps");
        if (list == null) return steps;

        foreach (var item in list)
        {
            if (item is not Dictionary<string, object?> step)
                continue;

            steps.Add(new ScenarioStep
            {
                Id = step.GetString("id", ""),
                Action = step.GetString("action", ""),
                ForEach = step.GetString("for_each", "") == "" ? null : step.GetString("for_each"),
                Flatten = step.Get<bool>("flatten") ?? false,
                OnError = step.GetString("on_error", "stop") == "" ? "stop" : step.GetString("on_error", "stop"),
                Args = step.GetDictionary("args") ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            });
        }

        return steps;
    }

    public static List<string> Validate(ScenarioDefinition def)
    {
        var errors = new List<string>();
        var indexes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(def.Name))
            errors.Add("name: empty");
        if (def.Steps.Count == 0)
            errors.Add("steps: empty");

        for (var i = 0; i < def.Steps.Count; i++)
        {
            var step = def.Steps[i];

            if (string.IsNullOrWhiteSpace(step.Action))
            {
                errors.Add($"steps[{i}] (id '{step.Id}'): action required");
                continue;
            }

            if (!KnownActions.Contains(step.Action))
                errors.Add($"steps[{i}] (id '{step.Id}'): unknown action '{step.Action}'. Known actions: {string.Join(", ", KnownActions)}");

            if (step.Action.StartsWith("jira.", StringComparison.Ordinal) && (def.Connection.Jira == null || def.Connection.Jira.Url == ""))
                errors.Add($"steps[{i}] (id '{step.Id}'): jira step requires connection.jira (url)");

            if (step.Action == "file.parse_sheet" && def.FileFormat == null)
                errors.Add($"steps[{i}] (id '{step.Id}'): file.parse_sheet requires file_format section");

            if (!string.IsNullOrWhiteSpace(step.Id))
            {
                if (!seenIds.Add(step.Id))
                    errors.Add($"steps[{i}] (id '{step.Id}'): duplicate id");
            }
            else if (step.ForEach != null)
            {
                errors.Add($"steps[{i}] (action '{step.Action}'): step with for_each requires id");
            }
        }

        return errors;
    }

    private static void ApplyInputOverrides(ScenarioDefinition def, Dictionary<string, string?>? overrides)
    {
        if (overrides == null) return;
        foreach (var kv in overrides)
        {
            if (string.IsNullOrWhiteSpace(kv.Key)) continue;
            def.Inputs[kv.Key.Trim()] = kv.Value ?? "";
        }
    }
}
