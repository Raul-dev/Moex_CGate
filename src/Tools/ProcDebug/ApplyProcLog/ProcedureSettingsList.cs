using Microsoft.Extensions.Configuration;

namespace ApplyProcLog;

public class ProcedureSettings
{
    public string DefaultFilter { get; set; } = "%";
    public List<string> ExcludeSchemas { get; set; } = new();
    public List<string> Procedures { get; set; } = new();
    public bool Enabled { get; set; } = true;
    public string Description { get; set; } = string.Empty;
    public string AuditEnabledCode { get; set; } = "FullAuditEnabled";
}

public class ProcedureSettingsList
{
    private readonly IConfiguration _configuration;

    public ProcedureSettingsList(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public IEnumerable<ProcedureSettings> GetAll()
    {
        const string sectionPrefix = "ProcedureSettings";
        var result = new List<ProcedureSettings>();

        foreach (var section in _configuration.GetChildren())
        {
            if (!section.Key.StartsWith(sectionPrefix))
                continue;

            var settings = new ProcedureSettings();
            section.Bind(settings);
            result.Add(settings);
        }

        return result;
    }

    public IEnumerable<ProcedureSettings> GetEnabled()
    {
        return GetAll().Where(s => s.Enabled);
    }

    public IEnumerable<string> GetProcedureNames()
    {
        return GetEnabled().SelectMany(s => s.Procedures);
    }

    public Dictionary<string, string> BuildNameToAuditCodeMap()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var section in GetAll())
        {
            if (!section.Enabled)
                continue;
            foreach (var procName in section.Procedures)
            {
                if (result.TryGetValue(procName, out var existing))
                {
                    result[procName] = string.Join(",", existing, section.AuditEnabledCode);
                }
                else
                {
                    result[procName] = section.AuditEnabledCode;
                }
            }
        }
        return result;
    }
}
