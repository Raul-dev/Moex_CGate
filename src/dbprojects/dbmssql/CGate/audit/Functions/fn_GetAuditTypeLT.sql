CREATE   FUNCTION [audit].[fn_GetAuditTypeLT](
    @AuditEnable nvarchar(256) = NULL
)RETURNS int
AS
BEGIN

    IF @AuditEnable = 'FullAuditEnabled'
        RETURN ISNULL((SELECT [IntValue] FROM [audit].[Setting] WHERE [ID] = 2), 0)
    ELSE
        RETURN ISNULL((SELECT [IntValue] FROM [audit].[Setting] WHERE [Code] = @AuditEnable), 0)
    RETURN 0 
END