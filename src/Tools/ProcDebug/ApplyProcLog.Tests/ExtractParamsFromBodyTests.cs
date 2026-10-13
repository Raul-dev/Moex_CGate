using System.Reflection;
using ApplyProcLog.dal;
using Xunit;

namespace ApplyProcLog.Tests;

public class ExtractParamsFromBodyTests
{
    private readonly MethodInfo _method;

    public ExtractParamsFromBodyTests()
    {
        var type = typeof(DBHelper);
        _method = type.GetMethod("ExtractParamsFromBody", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new Exception("ExtractParamsFromBody not found");
    }

    private string Call(string body) => (string)_method.Invoke(null, new object[] { body })!;

    #region Real procedures from DBAuditTest.uts.Proc::For:Test0001

    [Fact]
    public void Version1_NoVersionSuffix_ExtractsTwoParams()
    {
        // Version 1: no ;N on same line as name, params on next line
        string body = @"
/*
Proc_For_Test0001
*/
-- CREATE SCHEMA UTS
ALTER PROCEDURE [uts].[Proc::For:Test0001]
    @StrPrint   nvarchar(max),
    @PrintLevel int = 1 -- 1-Debug, 2-Info, 3-Warning, 4-Exception, 5-Test, 6-NotPrint
AS
BEGIN
    DECLARE @AuditPrintLevel int =9
    IF @PrintLevel >= 6
        RETURN 0
    --RETURN NULL(/*[dbo].[fn_GetSettingInt]('AuditPrintLevel')*/ 0, 0)

    IF @PrintLevel < @AuditPrintLevel
        RETURN 1

    DECLARE @StrTmp  nvarchar(4000),
        @StrPart     int = 3500,
        @StrLen      int = LEN(@StrPrint),
        @EndPart     int,
        @StrPrintTmp nvarchar(MAX)

END
";
        string result = Call(body);

        Assert.Contains("@StrPrint", result);
        Assert.Contains("@PrintLevel", result);
        Assert.Contains("nvarchar(max)", result);
    }

    [Fact]
    public void Version2_WithVersionSuffix_ExtractsFourParams()
    {
        // Version 2: ;2 after name, types with parentheses and string default
        string body = @"
/*
Proc_For_Test0001
*/
-- CREATE SCHEMA UTS
ALTER PROCEDURE [uts].[Proc::For:Test0001];2
    @StrPrint   nvarchar(max) ='int',@testtime time,
    @PrintLevel int = 1, -- 1-Debug, 2-Info, 3-Warning, 4-Exception, 5-Test, 6-NotPrint
    @testdate datetime2(7) = NULL
AS
BEGIN
    DECLARE @AuditPrintLevel int =9
    IF @PrintLevel >= 6
        RETURN 0
    --RETURN NULL(/*[dbo].[fn_GetSettingInt]('AuditPrintLevel')*/ 0, 0)

    IF @PrintLevel < @AuditPrintLevel
        RETURN 1

    DECLARE @StrTmp  nvarchar(4000),
        @StrPart     int = 3500,
        @StrLen      int = LEN(@StrPrint),
        @EndPart     int,
        @StrPrintTmp nvarchar(MAX)

END
";
        string result = Call(body);

        Assert.Contains("@StrPrint", result);
        Assert.Contains("@testtime", result);
        Assert.Contains("@PrintLevel", result);
        Assert.Contains("@testdate", result);
        Assert.Contains("datetime2(7)", result);
    }

    [Fact]
    public void Version3_WithVersionSuffix_ExtractsFourParamsWithDecimal()
    {
        // Version 3: decimal(18,8), timestamp, float — nested parentheses
        string body = @"
/*
Proc_For_Test0001
*/
-- CREATE SCHEMA UTS
ALTER PROCEDURE [uts].[Proc::For:Test0001];3
    @StrPrint   varchar(10) ='char',@testfloat float = 4.123456,
    @PrintLevel int = 1, -- Comment
    @testdate timestamp = NULL, @testdecimal decimal(18,8) = 0.90321

AS
BEGIN
    DECLARE @AuditPrintLevel int =9
    IF @PrintLevel >= 6
        RETURN 0
    --RETURN NULL(/*[dbo].[fn_GetSettingInt]('AuditPrintLevel')*/ 0, 0)

    IF @PrintLevel < @AuditPrintLevel
        RETURN 1

    DECLARE @StrTmp  nvarchar(4000),
        @StrPart     int = 3500,
        @StrLen      int = LEN(@StrPrint),
        @EndPart     int,
        @StrPrintTmp nvarchar(MAX)

END
";
        string result = Call(body);

        Assert.Contains("@StrPrint", result);
        Assert.Contains("@testfloat", result);
        Assert.Contains("@PrintLevel", result);
        Assert.Contains("@testdate", result);
        Assert.Contains("@testdecimal", result);
        Assert.Contains("decimal(18,8)", result);
    }

    #endregion

    #region Edge cases

    [Fact]
    public void EmptyBody_ReturnsEscapedSingleQuote()
    {
        string result = Call("");
        Assert.Equal("''''", result);
    }

    [Fact]
    public void NullBody_ReturnsEscapedSingleQuote()
    {
        string result = Call(null!);
        Assert.Equal("''''", result);
    }

    [Fact]
    public void WhitespaceOnlyBody_ReturnsEscapedSingleQuote()
    {
        string result = Call("   \n\t  ");
        Assert.Equal("''''", result);
    }

    [Fact]
    public void NoParameterList_ReturnsEscapedSingleQuote()
    {
        string body = @"
ALTER PROCEDURE [dbo].[Proc] AS
BEGIN
    DECLARE @x int;
END
";
        string result = Call(body);
        Assert.Equal("''''", result);
    }

    [Fact]
    public void SingleParamWithDefault_ExtractsParam()
    {
        // '' в конце строкового default — это конец строки (не экранирование),
        // далее сразу ) AS, где ) обрывает скобочную секцию,
        // а FindAsOutsideQuotes найдёт AS вне кавычек
        string body = @"
ALTER PROCEDURE [dbo].[Test];1 (@p1 varchar(100) = 'default') 
AS
BEGIN
    DECLARE @x int = 1;
END
";
        string result = Call(body);

        Assert.Contains("@p1", result);
        Assert.Contains("varchar(100)", result);
    }

    #endregion

    #region Return format (SQL string literal)

    [Fact]
    public void ValidParams_ReturnsAsSingleQuotedString()
    {
        string body = @"
ALTER PROCEDURE [dbo].[Test];1 (@p1 int) AS
BEGIN
    SET NOCOUNT ON;
END
";
        string result = Call(body);

        Assert.StartsWith("'", result);
        Assert.EndsWith("'", result);
    }

    [Fact]
    public void SqlNullDefault_EscapedInResult()
    {
        string body = @"
ALTER PROCEDURE [dbo].[Test];1 (@p1 int = NULL) AS
BEGIN
    SELECT 1;
END
";
        string result = Call(body);

        Assert.Contains("NULL", result);
        Assert.StartsWith("'", result);
        Assert.EndsWith("'", result);
    }

    #endregion
}
