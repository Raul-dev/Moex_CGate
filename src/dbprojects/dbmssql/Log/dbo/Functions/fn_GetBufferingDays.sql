CREATE FUNCTION dbo.fn_GetBufferingDays(
  @ProcedureName varchar(256)
) RETURNS int
AS 
BEGIN
  RETURN CASE @ProcedureName
    WHEN '[audit].[load_LogText]' THEN 1
    WHEN '[audit].[load_LogError]' THEN 1
    ELSE 1
  END
END