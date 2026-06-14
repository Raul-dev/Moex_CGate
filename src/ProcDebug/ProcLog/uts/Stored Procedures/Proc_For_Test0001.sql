/*
Proc_For_Test0001
*/

CREATE     PROCEDURE [uts].[Proc::For:Test0001]
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