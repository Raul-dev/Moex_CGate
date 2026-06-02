using System.Reflection;
using ApplyProcLog;
using Xunit;

namespace ApplyProcLog.Tests;

public class StoredProcedureGeneratorTests
{
    private readonly MethodInfo _makeProcFileName;
    private readonly MethodInfo _generateEmptyProcedure;

    public StoredProcedureGeneratorTests()
    {
        var type = typeof(StoredProcedureGenerator);
        _makeProcFileName = type.GetMethod("MakeProcFileName", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new Exception("MakeProcFileName not found");
        _generateEmptyProcedure = type.GetMethod("GenerateEmptyProcedure", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new Exception("GenerateEmptyProcedure not found");
    }

    private string CallMakeProcFileName(string schema, string procedureName)
    {
        return (string)_makeProcFileName.Invoke(null, new object[] { schema, procedureName })!;
    }

    private string CallGenerateEmptyProcedure(string body, string name)
    {
        var gen = new StoredProcedureGenerator();
        return (string)_generateEmptyProcedure.Invoke(gen, new object[] { body, name })!;
    }

    #region MakeProcFileName

    [Theory]
    [InlineData("TestSchema", "CommTran::Modify", "TestSchema.CommTran_Modify;__1.sql")]
    [InlineData("TestSchema", "CommTran::Modify;1", "TestSchema.CommTran_Modify;__1.sql")]
    [InlineData("TestSchema", "CommTran::Modify;2", "TestSchema.CommTran_Modify;__2.sql")]
    [InlineData("TestSchema", "CommTran::Modify;3", "TestSchema.CommTran_Modify;__3.sql")]
    [InlineData("TestSchema", "Comm::Calculate;10", "TestSchema.Comm_Calculate;__10.sql")]
    [InlineData("TestSchema", "Securities::InOut(Front)", "TestSchema.Securities_InOut(Front);__1.sql")]
    [InlineData("TestSchema", "Securities::InOut(Front);2", "TestSchema.Securities_InOut(Front);__2.sql")]
    public void MakeProcFileName_VariousScenarios(string schema, string procedureName, string expected)
    {
        var result = CallMakeProcFileName(schema, procedureName);
        Assert.Equal(expected, result);
    }

    #endregion

    #region GenerateEmptyProcedure

    [Fact]
    public void GenerateEmptyProcedure_BasicNumbered_ContainsExecAndIfNotExists()
    {
        string body = "ALTER PROCEDURE [TestSchema].[CommTran::Modify];1 AS BEGIN SET NOCOUNT ON; END";

        string result = CallGenerateEmptyProcedure(body, "[TestSchema].[CommTran::Modify];1");

        Assert.NotEmpty(result);
        Assert.Contains("IF NOT EXISTS", result);
        Assert.Contains("EXEC('CREATE PROCEDURE", result);
        Assert.Contains("procedure_number = 0 OR np.object_id IS NULL", result);
    }

    [Fact]
    public void GenerateEmptyProcedure_Version2_ContainsExecAndCorrectCondition()
    {
        string body = "ALTER PROCEDURE [TestSchema].[CommTran::Modify];2 AS BEGIN SET NOCOUNT ON; END";

        string result = CallGenerateEmptyProcedure(body, "[TestSchema].[CommTran::Modify];2");

        Assert.NotEmpty(result);
        Assert.Contains("IF NOT EXISTS", result);
        Assert.Contains("EXEC('CREATE PROCEDURE", result);
        Assert.Contains("procedure_number = 1", result);
    }

    [Fact]
    public void GenerateEmptyProcedure_Version3_ContainsExecAndCorrectCondition()
    {
        string body = "ALTER PROCEDURE [TestSchema].[CommTran::Modify];3 AS BEGIN SET NOCOUNT ON; END";

        string result = CallGenerateEmptyProcedure(body, "[TestSchema].[CommTran::Modify];3");

        Assert.NotEmpty(result);
        Assert.Contains("IF NOT EXISTS", result);
        Assert.Contains("EXEC('CREATE PROCEDURE", result);
        Assert.Contains("procedure_number = 2", result);
    }

    [Fact]
    public void GenerateEmptyProcedure_Version1FromBody_GeneratesValidExec()
    {
        string body = "ALTER PROCEDURE [TestSchema].[Orders::Securities::Modify::ComissCosts] AS BEGIN SET NOCOUNT ON; END";

        string result = CallGenerateEmptyProcedure(body, "[TestSchema].[Orders::Securities::Modify::ComissCosts]");

        Assert.NotEmpty(result);
        Assert.Contains("IF NOT EXISTS", result);
        Assert.Contains("EXEC('CREATE PROCEDURE", result);
        Assert.Contains("[TestSchema].[Orders::Securities::Modify::ComissCosts];1", result);
    }

    [Fact]
    public void GenerateEmptyProcedure_WithParameters_IncludesParams()
    {
        string body = "ALTER PROCEDURE [TestSchema].[TestProc];1 (@p1 int, @p2 varchar(100)) AS BEGIN SET NOCOUNT ON; END";

        string result = CallGenerateEmptyProcedure(body, "[TestSchema].[TestProc];1");

        Assert.NotEmpty(result);
        Assert.Contains("@p1 int", result);
        Assert.Contains("@p2 varchar(100)", result);
    }

    [Fact]
    public void GenerateEmptyProcedure_SetNocountOn_InBody()
    {
        string body = "ALTER PROCEDURE [TestSchema].[Test];1 AS BEGIN SET NOCOUNT ON; END";

        string result = CallGenerateEmptyProcedure(body, "[TestSchema].[Test];1");

        Assert.Contains("SET NOCOUNT ON", result);
    }

    [Fact]
    public void GenerateEmptyProcedure_Version1_HasNpIsNullOrProcedureNumber0()
    {
        string body = "ALTER PROCEDURE [TestSchema].[Test];1 AS BEGIN END";

        string result = CallGenerateEmptyProcedure(body, "[TestSchema].[Test];1");

        Assert.Contains("(np.procedure_number = 0 OR np.object_id IS NULL)", result);
    }

    #endregion
}
