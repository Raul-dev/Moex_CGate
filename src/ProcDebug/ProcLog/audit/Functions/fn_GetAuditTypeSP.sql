CREATE   FUNCTION [audit].[fn_GetAuditTypeSP](
    @AuditEnable nvarchar(256) = NULL
)RETURNS int
AS
BEGIN
    IF @AuditEnable IS NULL
        RETURN NULL
    RETURN 1
/*  отключаем айдит тут, он приходит массивом 
    IF @AuditEnable = 'FullAuditEnabled'
        RETURN 1 --simple enable
    IF @AuditEnable = 'FullAuditEnabledLnk'
       RETURN 2  --LinkedServer  
        --for advanced setup:
        --RETURN ISNULL((SELECT [IntValue] FROM [audit].[Setting] WHERE [ID] = 1), 0)
    ELSE
        RETURN ISNULL((SELECT [IntValue] FROM [audit].[Setting] WHERE [Code] = @AuditEnable), 0)
    RETURN 0 
*/    
END