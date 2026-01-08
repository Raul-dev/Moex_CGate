
CREATE PROCEDURE [rmq].[sp_GetRabbitEndpoints]
AS

SET NOCOUNT ON;

SELECT EndpointID, 
       AliasName, 
	   ServerName, 
	   Port, 
	   VHost, 
	   LoginName, 
	   CAST(LoginPassword AS nvarchar(256)) AS LoginPassword, 
	   Exchange, 
	   RoutingKey, 
	   ConnectionChannels, 
	   IsEnabled
FROM rmq.RabbitEndpoint
WHERE IsEnabled = 1;