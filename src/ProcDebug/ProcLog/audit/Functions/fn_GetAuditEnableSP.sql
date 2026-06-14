CREATE   FUNCTION [audit].[fn_GetAuditEnableSP](
    @AuditEnable nvarchar(256) = NULL
)RETURNS nvarchar(256)
AS
BEGIN
    RETURN NULLIF(@AuditEnable,'')
    --Оключаем лог хардкодом, чтобы не ходить в таблийцы
    --RETURN NULLIF(@AuditEnable, 'DisableLog') 
END