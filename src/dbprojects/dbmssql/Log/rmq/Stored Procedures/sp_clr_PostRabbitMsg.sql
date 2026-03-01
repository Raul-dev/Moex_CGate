CREATE PROCEDURE [rmq].[sp_clr_PostRabbitMsg]
@EndpointID INT NULL, @Message NVARCHAR (MAX) NULL
AS EXTERNAL NAME [RabbitMQSqlClr4].[RabbitMQSqlClr.RabbitMQSqlServer].[sp_clr_PostRabbitMsg]

