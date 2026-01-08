
CREATE PROCEDURE [rmq].[sp_GetLocalDBConnString] @ConnString nvarchar(512) OUT
AS

SET NOCOUNT ON;

 SELECT @ConnString = SettingStringValue
 FROM rmq.RabbitSetting
 WHERE SettingID = 1;