CREATE Function [dbo].[fn_GetSettingInt](
    @SettingID    varchar(50)
) RETURNS INT
AS
BEGIN
  RETURN (SELECT CAST(LTRIM(StrValue) AS INT) FROM [dbo].[Setting] WITH( NOLOCK ) WHERE SettingID = @SettingID)
END
