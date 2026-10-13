namespace ContextSync.dal.Settings;

public class DatabaseSettings
{
    public string Server { get; set; } = "localhost";
    public string Database { get; set; } = "";
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    public bool IntegratedSecurity { get; set; } = true;
    public string QueryText { get; set; } = "";
    public int CommandTimeoutSeconds { get; set; } = 60;

    public string BuildConnectionString()
    {
        if (IntegratedSecurity)
            return $"Server={Server};Database={Database};Integrated Security=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Encrypt=False";
        return $"Server={Server};Database={Database};User={User};Password={Password};MultipleActiveResultSets=true;TrustServerCertificate=True;Encrypt=False";
    }
}
