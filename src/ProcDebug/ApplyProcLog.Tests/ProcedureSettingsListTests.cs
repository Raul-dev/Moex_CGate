using Microsoft.Extensions.Configuration;
using Xunit;

namespace ApplyProcLog.Tests;

public class ProcedureSettingsListTests
{
    private static IConfiguration BuildConfig(Dictionary<string, object?> sections)
    {
        var configData = sections.ToDictionary(
            kv => kv.Key,
            kv => kv.Value as object);

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configData.SelectMany(NestedTuples))
            .Build();
    }

    private static IEnumerable<KeyValuePair<string, string?>> NestedTuples(KeyValuePair<string, object?> kv)
    {
        if (kv.Value is IDictionary<string, object?> nested)
        {
            foreach (var inner in nested)
            {
                yield return new KeyValuePair<string, string?>(
                    $"{kv.Key}:{inner.Key}",
                    inner.Value?.ToString());
            }
        }
        else
        {
            yield return new KeyValuePair<string, string?>(kv.Key, kv.Value?.ToString());
        }
    }

    [Fact]
    public void GetAll_ReturnsAllSections()
    {
        var config = BuildConfig(new Dictionary<string, object?>
        {
            ["ProcedureSettings"] = new Dictionary<string, object?>
            {
                ["Enabled"] = "true",
                ["Description"] = "Основные",
                ["AuditEnabledCode"] = "FullAuditEnabled",
                ["Procedures:0"] = "dbo.Proc1"
            },
            ["ProcedureSettingsExport"] = new Dictionary<string, object?>
            {
                ["Enabled"] = "false",
                ["Description"] = "Экспорт",
                ["AuditEnabledCode"] = "MinimalAudit",
                ["Procedures:0"] = "dbo.Proc2"
            }
        });

        var list = new ProcedureSettingsList(config);
        var all = list.GetAll().ToList();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void GetEnabled_ReturnsOnlyEnabledSections()
    {
        var config = BuildConfig(new Dictionary<string, object?>
        {
            ["ProcedureSettings"] = new Dictionary<string, object?>
            {
                ["Enabled"] = "true",
                ["Procedures:0"] = "dbo.Proc1"
            },
            ["ProcedureSettingsExport"] = new Dictionary<string, object?>
            {
                ["Enabled"] = "false",
                ["Procedures:0"] = "dbo.Proc2"
            }
        });

        var list = new ProcedureSettingsList(config);
        var enabled = list.GetEnabled().ToList();

        Assert.Single(enabled);
        Assert.Equal("dbo.Proc1", enabled[0].Procedures[0]);
    }

    [Fact]
    public void GetProcedureNames_ReturnsProceduresFromEnabledSections()
    {
        var config = BuildConfig(new Dictionary<string, object?>
        {
            ["ProcedureSettings"] = new Dictionary<string, object?>
            {
                ["Enabled"] = "true",
                ["Procedures:0"] = "dbo.Proc1",
                ["Procedures:1"] = "dbo.Proc2"
            },
            ["ProcedureSettingsExport"] = new Dictionary<string, object?>
            {
                ["Enabled"] = "true",
                ["Procedures:0"] = "dbo.Proc3"
            }
        });

        var list = new ProcedureSettingsList(config);
        var names = list.GetProcedureNames().ToList();

        Assert.Equal(3, names.Count);
        Assert.Contains("dbo.Proc1", names);
        Assert.Contains("dbo.Proc2", names);
        Assert.Contains("dbo.Proc3", names);
    }

    [Fact]
    public void GetAll_DefaultValues_WhenSectionIsEmpty()
    {
        var config = BuildConfig(new Dictionary<string, object?>
        {
            ["ProcedureSettings"] = new Dictionary<string, object?>
            {
                ["Procedures:0"] = "dbo.Proc1"
            }
        });

        var list = new ProcedureSettingsList(config);
        var section = list.GetAll().First();

        Assert.True(section.Enabled);
        Assert.Equal("FullAuditEnabled", section.AuditEnabledCode);
        Assert.Equal(string.Empty, section.Description);
    }

    [Fact]
    public void GetAll_SkipsNonProcedureSettingsSections()
    {
        var config = BuildConfig(new Dictionary<string, object?>
        {
            ["Logging"] = new Dictionary<string, object?>
            {
                ["LogLevel:Default"] = "Information"
            },
            ["ProcedureSettings"] = new Dictionary<string, object?>
            {
                ["Procedures:0"] = "dbo.Proc1"
            },
            ["DataBaseSettings"] = new Dictionary<string, object?>
            {
                ["ServerName"] = "localhost"
            }
        });

        var list = new ProcedureSettingsList(config);
        var all = list.GetAll().ToList();

        Assert.Single(all);
    }

    [Fact]
    public void Constructor_ThrowsOnNullConfiguration()
    {
        Assert.Throws<ArgumentNullException>(() => new ProcedureSettingsList(null!));
    }

    [Fact]
    public void BuildNameToAuditCodeMap_DeduplicatesProceduresWithCommaSeparatedCodes()
    {
        var config = BuildConfig(new Dictionary<string, object?>
        {
            ["ProcedureSettings"] = new Dictionary<string, object?>
            {
                ["Enabled"] = "true",
                ["AuditEnabledCode"] = "FullAuditEnabled",
                ["Procedures:0"] = "dbo.Proc1",
                ["Procedures:1"] = "dbo.Proc2",
                ["Procedures:2"] = "dbo.DuplicateProc"
            },
            ["ProcedureSettingsMinimal"] = new Dictionary<string, object?>
            {
                ["Enabled"] = "true",
                ["AuditEnabledCode"] = "MinimalAudit",
                ["Procedures:0"] = "dbo.Proc3",
                ["Procedures:1"] = "dbo.DuplicateProc"
            }
        });

        var list = new ProcedureSettingsList(config);
        var map = list.BuildNameToAuditCodeMap();

        Assert.Equal("FullAuditEnabled", map["dbo.Proc1"]);
        Assert.Equal("FullAuditEnabled", map["dbo.Proc2"]);
        Assert.Equal("MinimalAudit", map["dbo.Proc3"]);
        Assert.Equal("FullAuditEnabled,MinimalAudit", map["dbo.DuplicateProc"]);
    }

    [Fact]
    public void BuildNameToAuditCodeMap_SkipsDisabledSections()
    {
        var config = BuildConfig(new Dictionary<string, object?>
        {
            ["ProcedureSettings"] = new Dictionary<string, object?>
            {
                ["Enabled"] = "true",
                ["AuditEnabledCode"] = "FullAuditEnabled",
                ["Procedures:0"] = "dbo.Proc1"
            },
            ["ProcedureSettingsDisabled"] = new Dictionary<string, object?>
            {
                ["Enabled"] = "false",
                ["AuditEnabledCode"] = "DisabledCode",
                ["Procedures:0"] = "dbo.Proc1"
            }
        });

        var list = new ProcedureSettingsList(config);
        var map = list.BuildNameToAuditCodeMap();

        Assert.Single(map);
        Assert.Equal("FullAuditEnabled", map["dbo.Proc1"]);
    }

    [Fact]
    public void BuildNameToAuditCodeMap_CaseInsensitiveMerge()
    {
        var config = BuildConfig(new Dictionary<string, object?>
        {
            ["ProcedureSettings"] = new Dictionary<string, object?>
            {
                ["Enabled"] = "true",
                ["AuditEnabledCode"] = "Code1",
                ["Procedures:0"] = "dbo.Proc1"
            },
            ["ProcedureSettings2"] = new Dictionary<string, object?>
            {
                ["Enabled"] = "true",
                ["AuditEnabledCode"] = "Code2",
                ["Procedures:0"] = "DBO.PROC1"
            }
        });

        var list = new ProcedureSettingsList(config);
        var map = list.BuildNameToAuditCodeMap();

        Assert.Single(map);
        Assert.Equal("Code1,Code2", map["dbo.Proc1"]);
        Assert.Equal("Code1,Code2", map["DBO.PROC1"]);
    }
}
