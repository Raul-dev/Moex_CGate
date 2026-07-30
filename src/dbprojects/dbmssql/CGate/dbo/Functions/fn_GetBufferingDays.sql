CREATE FUNCTION dbo.fn_GetBufferingDays(
  @ProcedureName varchar(256)
) RETURNS int
AS 
BEGIN
  RETURN CASE @ProcedureName WHEN '[crs].[load_OrdersLog]' THEN 1
  ELSE 1
  END
END