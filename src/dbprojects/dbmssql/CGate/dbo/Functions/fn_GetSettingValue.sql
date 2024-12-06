
CREATE       Function [dbo].[fn_GetSettingValue](
    @SettingID    varchar(50)
) RETURNS nvarchar(256)
AS
BEGIN
    RETURN (SELECT StrValue FROM [dbo].[Setting] WITH( NOLOCK ) WHERE SettingID = @SettingID)
END