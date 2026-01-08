
CREATE PROCEDURE [rmq].[sp_UpsertRabbitEndpoint]   @Alias varchar(256), 
											  @ServerName varchar(512),
											  @Port int = 5672,
											  @VHost nvarchar(256) = '/',
											  @LoginName varchar(256),
											  @LoginPassword nvarchar(256),
											  @Exchange varchar(128),
											  @RoutingKey varchar(256),
											  @ConnectionChannels int = 5,
											  @IsEnabled bit = 1
AS

SET NOCOUNT ON;

DECLARE @errMsg nvarchar(max); /*this is used to set an explanatory error message BEFORE you call something, 
                                 it is what we previously used to put inside our RAISERROR's*/


BEGIN TRY

--vary basic proc, no validations etc.

--poor mans encryption
DECLARE @pwd varbinary(128) = CAST(@LoginPassword AS varbinary(128));

SET @errMsg = 'Merging into rmq.tb_RabbitEndpoint';
MERGE rmq.RabbitEndpoint AS tgt
USING(SELECT @Alias AS AliasName, @ServerName AS ServerName, @Port AS Port, @VHost AS VHost, 
             @LoginName AS LoginName, @pwd AS LoginPassword, @Exchange AS Exchange, 
			 @RoutingKey AS RoutingKey, @ConnectionChannels AS ConnectionChannels, @IsEnabled AS IsEnabled) AS src
ON(tgt.AliasName = src.AliasName)
WHEN NOT MATCHED THEN
  INSERT(AliasName, ServerName, Port, VHost, LoginName, LoginPassword, Exchange, RoutingKey, ConnectionChannels, IsEnabled)
  VALUES(src.AliasName, src.ServerName, src.Port, src.VHost, src.LoginName, src.LoginPassword, src.Exchange, src.RoutingKey, 
         src.ConnectionChannels, src.IsEnabled)
WHEN MATCHED THEN
  UPDATE
    SET ServerName = src.ServerName, 
	    Port = src.Port, 
		VHost = src.VHost, 
		LoginName = src.LoginName, 
		LoginPassword = src.LoginPassword, 
		Exchange = src.Exchange, 
		RoutingKey = src.RoutingKey, 
		ConnectionChannels = src.ConnectionChannels, 
		IsEnabled = src.IsEnabled;

END TRY
BEGIN CATCH
  DECLARE @thisProc nvarchar(256) = 'rmq.pr_UpsertRabbitEndpoint'; --used for error messages
  DECLARE @sysErrMsg varchar(max);  -- used to set customised error messages
  DECLARE @errSev int; -- error severity
  DECLARE @errState int; -- error state

  --error handling code
  SELECT @sysErrMsg = ERROR_MESSAGE(), @errSev = ERROR_SEVERITY(), @errState = ERROR_STATE()
     
  --re-raise upstream
  RAISERROR('Error in %s: %s. %s.', @errSev, @errState, @thisProc, @errMsg, @sysErrMsg)


END CATCH