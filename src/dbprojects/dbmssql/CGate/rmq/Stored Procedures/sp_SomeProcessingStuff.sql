
CREATE   PROCEDURE [rmq].[sp_SomeProcessingStuff] @id int
AS
BEGIN
  SET NOCOUNT ON;
  BEGIN TRY
    --create a variable for the endpoint
    DECLARE @endPointId int;
    --create a variable for the message
    DECLARE @msg nvarchar(max) = '{'
    --do important stuff, and collect data for the message
    SET @msg = @msg + '"Id":' + CAST(@id AS varchar(10)) + ','
    -- do some more stuff
    SET @msg = @msg + '"FName":"Hello",';
    SET @msg = @msg + '"LName":"World"';
    SET @msg = @msg + '}';

    --do more stuff
    -- get the endpoint id from somewhere, based on something
    SELECT @endPointId = 1;
    --here is the hook-point
    --call the procedure to send the message
    EXEC rmq.sp_PostRabbitMsg @Message = @msg, @EndpointID = @endPointId;
  END TRY
  BEGIN CATCH
    DECLARE @errMsg nvarchar(max);
    DECLARE @errLine int;
    SELECT @errMsg = ERROR_MESSAGE(), @errLine = ERROR_LINE();
    RAISERROR('Error: %s at line: %d', 16, -1, @errMsg, @errLine);
  END CATCH
END