using ApplyProcLog.dal;
using Xunit;

namespace ApplyProcLog.Tests;

public class GetParametersForAuditQuotedTypesTests
{
    [Fact]
    public void BitTinyintUniqueidentifier_AreQuoted_ImageUsesNullComment()
    {
        string body = @"
CREATE PROCEDURE [uts].[Proc_QuotedTypes]
    @p_bit bit = NULL,
    @p_tinyint tinyint = NULL,
    @p_uniqueidentifier uniqueidentifier = NULL,
    @p_image image = NULL,
    @p_int int = NULL
AS
BEGIN
  SET NOCOUNT ON;
END
";
        string audit = new ProcedureParamParser(body).GetParametersForAudit();

        Assert.Contains("'@p_bit='+ISNULL(''''+LTRIM(CAST(@p_bit AS varchar(27)))+'''','NULL')", audit);
        Assert.Contains("'@p_tinyint='+ISNULL(''''+LTRIM(CAST(@p_tinyint AS varchar(3)))+'''','NULL')", audit);
        Assert.Contains("'@p_uniqueidentifier='+ISNULL(''''+LTRIM(CAST(@p_uniqueidentifier AS varchar(36)))+'''','NULL')", audit);
        Assert.Contains("'@p_image=NULL'+' /*image*/'", audit);
        Assert.Contains("'@p_int='+ISNULL(LTRIM(CAST(@p_int AS varchar(11))),'NULL')", audit);
        Assert.DoesNotContain("CAST(@p_image AS", audit);
    }
}
