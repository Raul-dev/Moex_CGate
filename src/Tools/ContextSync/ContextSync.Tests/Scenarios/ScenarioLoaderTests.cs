using ContextSync.Scenarios;
using Xunit;

namespace ContextSync.Tests.Scenarios;

public class ScenarioLoaderTests
{
    private static string WriteTempYaml(string yaml)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".yaml");
        File.WriteAllText(path, yaml);
        return path;
    }

    private const string WorklogUploadYaml = """
        name: worklog-upload
        description: test scenario
        version: 1
        connection:
          jira:
            url: https://jira.mydomen.ru
            username: myuser
            token: "{{ env 'X_TOKEN' }}"
        inputs:
          file: D:\tmp\timesheet.txt
        file_format:
          date_line: '^(?<date>\d{8})$'
          entry_line: '^https?://\S+/browse/(?<key>[A-Z][A-Z0-9]+-\d+)\s+(?<hours>\d+)h'
          skip_blank: true
          date_format: yyyyMMdd
        defaults:
          hours: 6
          comment: "."
          account: TASK-разработка
        steps:
          - id: parse
            action: file.parse_sheet
            args:
              file: "{{ inputs.file }}"
          - id: log
            action: jira.add_worklog
            for_each: parse.entries
            flatten: true
            args:
              issue_key: "{{ item.key }}"
              hours: "{{ item.hours | def defaults.hours }}"
              date: "{{ item.date_iso }}"
              comment: "{{ defaults.comment }}"
              account: "{{ defaults.account }}"
              account_field: worklog_account
            on_error: continue
          - id: report
            action: report.console
            args:
              dataset: log
              only_wrong: false
        """;

    [Fact]
    public void LoadsYamlAndMapsSections()
    {
        var path = WriteTempYaml(WorklogUploadYaml);
        try
        {
            var def = ScenarioLoader.Load(path);

            Assert.Equal("worklog-upload", def.Name);
            Assert.Equal(1, def.Version);
            Assert.Equal("https://jira.mydomen.ru", def.Connection.Jira!.Url);
            Assert.Equal("myuser", def.Connection.Jira!.Username);
            Assert.Equal(3, def.Steps.Count);
            Assert.True(def.Steps[1].Flatten);
            Assert.False(def.Steps[0].Flatten);
            Assert.Equal("6", def.Defaults["hours"]!.ToString());
            Assert.Equal("TASK-разработка", def.Defaults["account"]!.ToString());
            Assert.Equal("continue", def.Steps[1].OnError);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void UnknownActionFailsValidation()
    {
        var yaml = WorklogUploadYaml.Replace("action: file.parse_sheet", "action: file.missing_step");
        var path = WriteTempYaml(yaml);

        try
        {
            var ex = Assert.Throws<ScenarioLoadException>(() => ScenarioLoader.Load(path));
            Assert.Contains("unknown action", ex.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void JiraStepWithoutConnectionFailsValidation()
    {
        var yaml = """
            name: bad-connection
            version: 1
            connection:
            steps:
              - id: log
                action: jira.add_worklog
                for_each: items
                args:
                  issue_key: "{{ item.key }}"
            """;
        var path = WriteTempYaml(yaml);

        try
        {
            var ex = Assert.Throws<ScenarioLoadException>(() => ScenarioLoader.Load(path));
            Assert.Contains("connection.jira", ex.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void InputOverridesAreApplied()
    {
        var path = WriteTempYaml(WorklogUploadYaml);
        try
        {
            var def = ScenarioLoader.Load(path, new Dictionary<string, string?> { ["file"] = "D:\\tmp\\other.txt" });
            Assert.Equal("D:\\tmp\\other.txt", def.Inputs["file"]!.ToString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ResolvePathFindsByName()
    {
        var path = WriteTempYaml(WorklogUploadYaml);
        var dir = Path.GetDirectoryName(path)!;
        try
        {
            var resolved = ScenarioLoader.ResolvePath(Path.GetFileNameWithoutExtension(path), dir);
            Assert.Equal(Path.GetFullPath(path), Path.GetFullPath(resolved));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SupportsInputsStringOverrides()
    {
        var path = WriteTempYaml(WorklogUploadYaml);
        try
        {
            var def = ScenarioLoader.Load(path, new Dictionary<string, string?>
            {
                ["user"] = "myuser",
                ["num_of_weeks"] = "3",
                ["expected_hours_day"] = "7.5"
            });

            Assert.Equal("myuser", def.Inputs["user"]!.ToString());
            Assert.Equal("3", def.Inputs["num_of_weeks"]!.ToString());
            Assert.Equal("7.5", def.Inputs["expected_hours_day"]!.ToString());
        }
        finally
        {
            File.Delete(path);
        }
    }
}
