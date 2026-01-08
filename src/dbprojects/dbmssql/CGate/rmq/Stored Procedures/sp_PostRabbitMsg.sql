
CREATE PROCEDURE rmq.[sp_PostRabbitMsg] @Message nvarchar(max), @EndpointID int = -1
AS
BEGIN
  SET NOCOUNT ON;
  BEGIN TRY
    EXEC rmq.sp_clr_PostRabbitMsg  @EndpointID = @EndpointID, @Message = @Message;
  END TRY
  BEGIN CATCH
    DECLARE @errMsg nvarchar(max);
    DECLARE @errLine int;
    SELECT @errMsg = ERROR_MESSAGE(), @errLine = ERROR_LINE();
    RAISERROR('Error: %s at line: %d', 16, -1, @errMsg, @errLine);
  END CATCH
END