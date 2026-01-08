CREATE PROCEDURE [rmq].[sp_clr_PostRabbitMsg]
@EndpointID INT NULL, @Message NVARCHAR (MAX) NULL
AS EXTERNAL NAME [RabbitMQ.SqlServer].[RabbitMQSqlClr.RabbitMQSqlServer].[sp_clr_PostRabbitMsg]

