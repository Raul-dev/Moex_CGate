using System.IO;
using ApplyProcLog.dal;

namespace ApplyProcLog.Tests.Integration;

/// <summary>
/// Интеграционные тесты, выполняемые на реальной БД.
/// Запускаются только когда БД доступна.
/// Использование: dotnet test --filter "Category=Integration"
/// </summary>
[Collection("DatabaseIntegration")]
[Trait("Category", "Integration")]
public class IntegrationTests
{
    private readonly DatabaseFixture _fixture;
    private readonly DBHelper _dbHelper;

    public IntegrationTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _dbHelper = new DBHelper(_fixture.ConnectionString);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetSqlProceduresObjecId_ReturnsNonEmpty()
    {
        var result = await _dbHelper.GetSqlProceduresObjecIdAsync();

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void GetSqlProceduresObjecId_ContainsExpectedSchemas()
    {
        var result = _fixture.GetProcedures();
        var schemas = result.Select(r => r.SchemaName).Distinct().ToList();

        Assert.NotEmpty(schemas);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetProcedureParameters_ReturnsParameters()
    {
        var procs = _fixture.GetProcedures();
        var firstProc = procs.FirstOrDefault(p => p.ObjectId > 0);

        if (firstProc.ObjectId == 0)
            return;

        var result = await _dbHelper.GetProcedureParametersAsync(firstProc.ObjectId);

        Assert.NotNull(result);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void ProcedureParamParser_FromObjectId_LoadsParameters()
    {
        var procs = _fixture.GetProcedures();
        var firstProc = procs.FirstOrDefault(p => p.ObjectId > 0);

        if (firstProc.ObjectId == 0)
            return;

        var parser = new ProcedureParamParser(firstProc.ObjectId, _fixture.ConnectionString);

        Assert.NotNull(parser.Parameters);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ProcedureParamParser_CompareBodyVsObjectId()
    {
        var procedures = _fixture.GetProcedures();
        var checkedCount = 0;
        var checkedProcedures = new List<(int ObjectId, string Schema, string Name, int BodyParams, int DbParams)>();

        foreach (var proc in procedures.Where(p => p.ObjectId > 0))
        {
            var procInfos = await _dbHelper.GetSqlProceduresByNumber(proc.ObjectId, default, null);
            if (procInfos.Count == 0 || string.IsNullOrEmpty(procInfos[0].ProcedureBody))
                continue;

            var parserFromBody = new ProcedureParamParser(procInfos[0].ProcedureBody);
            var parserFromObjectId = new ProcedureParamParser(proc.ObjectId, _fixture.ConnectionString);

            if (parserFromBody.Parameters.Count == 0 && parserFromObjectId.Parameters.Count == 0)
                continue;

            if (parserFromBody.Parameters.Count == 0)
            {
                File.WriteAllText($@"debug_body_{proc.ObjectId}.txt", procInfos[0].ProcedureBody ?? "");
                continue;
            }

            Assert.Equal(
                parserFromObjectId.Parameters.Count,
                parserFromBody.Parameters.Count);

            string bodyAudit = parserFromBody.GetParametersForAudit();
            string dbAudit = parserFromObjectId.GetParametersForAudit();
            if (bodyAudit != dbAudit)
            {
                File.WriteAllText($@"debug_audit_{proc.ObjectId}.txt",
                    $"DB:\n{dbAudit}\n\nBODY:\n{bodyAudit}");
            }
            Assert.Equal(bodyAudit, dbAudit);
            for (int j = 0; j < parserFromObjectId.Parameters.Count; j++)
            {
                Assert.Equal(
                    parserFromObjectId.Parameters[j].Name,
                    parserFromBody.Parameters[j].Name);

                string fromDb = NormalizeTypeName(parserFromObjectId.Parameters[j].TypeName);
                string fromBody = NormalizeTypeName(parserFromBody.Parameters[j].TypeName);
                Assert.Equal(fromDb, fromBody);
            }

            checkedProcedures.Add((
                proc.ObjectId,
                proc.SchemaName,
                procInfos[0].ProcedureName,
                parserFromBody.Parameters.Count,
                parserFromObjectId.Parameters.Count));
            checkedCount++;
        }

        Assert.True(checkedCount > 0, "No procedures with parameters were checked");

        Console.WriteLine($"\n=== ProcedureParamParser_CompareBodyVsObjectId: {checkedCount} procedures checked ===");
        foreach (var p in checkedProcedures)
        {
            Console.WriteLine($"  [{p.ObjectId}] {p.Schema}.{p.Name}  body={p.BodyParams}  db={p.DbParams}");
        }
    }

    private static string NormalizeTypeName(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return typeName;

        int paren = typeName.IndexOf('(');
        string baseName = paren >= 0
            ? typeName[..paren].TrimEnd()
            : typeName.Trim();

        return DefaultSqlTypes.IsBuiltIn(baseName)
            ? (paren >= 0 ? typeName[..paren].TrimEnd() : typeName)
            : typeName;
    }
}
