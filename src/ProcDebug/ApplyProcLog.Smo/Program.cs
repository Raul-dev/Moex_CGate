using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;

// ScriptTables.exe - Генерация скриптов структуры таблиц (CREATE + FK + defaults + indexes) через SMO
// Usage: ScriptTables.exe -s <server> -d <database> -t "Schema.Table1,Schema.Table2" -o <outputFolder>

if (args.Length == 0)
{
    ShowHelp();
    return;
}

string? server = null;
string? database = null;
string? tables = null;
string? output = null;

for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "-s" && i + 1 < args.Length) server = args[++i];
    else if (args[i] == "-d" && i + 1 < args.Length) database = args[++i];
    else if (args[i] == "-t" && i + 1 < args.Length) tables = args[++i];
    else if (args[i] == "-o" && i + 1 < args.Length) output = args[++i];
    else if (args[i] == "--help" || args[i] == "-h") { ShowHelp(); return; }
}

if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(database) || string.IsNullOrEmpty(tables))
{
    Console.Error.WriteLine("Error: -s, -d and -t обязательны");
    ShowHelp();
    return;
}

output ??= Path.Combine(Directory.GetCurrentDirectory(), "TableScripts");

if (!Directory.Exists(output))
    Directory.CreateDirectory(output);

var connStr = $"Server={server};Database={database};Integrated Security=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Encrypt=False";

Console.WriteLine($"Server: {server}");
Console.WriteLine($"Database: {database}");
Console.WriteLine($"Output: {output}");
Console.WriteLine();

try
{
    var srv = new Server(new ServerConnection(new SqlConnection(connStr)));
    var db = srv.Databases[database];
    if (db == null)
    {
        Console.Error.WriteLine($"Database '{database}' not found on server '{server}'");
        Environment.Exit(1);
    }

    var tableNames = tables.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    int generated = 0;

    foreach (var fullName in tableNames)
    {
        var parts = fullName.Split('.');
        if (parts.Length != 2)
        {
            Console.WriteLine($"  SKIP: {fullName} (ожидается Schema.Name)");
            continue;
        }

        var schemaName = parts[0].Trim('[', ']');
        var tableName = parts[1].Trim('[', ']');

        Table? table;
        try
        {
            table = db.Tables[tableName, schemaName];
        }
        catch
        {
            table = null;
        }

        if (table == null)
        {
            Console.WriteLine($"  NOT FOUND: {schemaName}.{tableName}");
            continue;
        }

        var scripter = new Scripter(srv)
        {
            Options =
            {
                ScriptSchema = true,
                ScriptData = false,
                IncludeHeaders = false,
                AppendToFile = false,
                ToFileOnly = false,
                IncludeIfNotExists = false,
                Permissions = false,
                DriAllKeys = true,
                DriAllConstraints = true,
                FullTextIndexes = false,
                NoCollation = true
            }
        };

        var scriptParts = new List<string>();
        foreach (var s in scripter.EnumScript(new[] { table.Urn }))
        {
            if (!string.IsNullOrWhiteSpace(s))
                scriptParts.Add(s);
        }

        if (scriptParts.Count == 0)
        {
            Console.WriteLine($"  EMPTY: {schemaName}.{tableName} (SMO не вернул скрипт)");
            continue;
        }

        var safeSchema = schemaName.Replace(':', '_');
        var safeTable = tableName.Replace(':', '_');
        var fileName = $"{safeSchema}.{safeTable}.sql";
        var filePath = Path.Combine(output, fileName);

        File.WriteAllText(filePath, string.Join(Environment.NewLine, scriptParts), new System.Text.UTF8Encoding(false));
        Console.WriteLine($"  OK: {schemaName}.{tableName} -> {fileName}");
        generated++;
    }

    Console.WriteLine();
    Console.WriteLine($"Done. Generated: {generated}");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    Environment.Exit(1);
}

static void ShowHelp()
{
    Console.WriteLine("ScriptTables.exe - Генерация скриптов структуры таблиц через SMO");
    Console.WriteLine();
    Console.WriteLine("Usage: ScriptTables.exe -s <server> -d <database> -t \"Schema.Table1,Schema.Table2\" [-o <output>]");
    Console.WriteLine();
    
