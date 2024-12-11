--SELECT  RAND()
/*TRUNCATE TABLE  [crs].[orders_log_buffer]
TRUNCATE TABLE [dbo].[msgqueue] 
SELECT * FROM [crs].[orders_log_buffer]
SELECT * FROM [dbo].[msgqueue]
EXEC sp_GenerationRandomArray 'crs','orders_log'
SELECT * FROM [dbo].[DataGeneration]
TRUNCATE TABLE [dbo].[msgqueue]
[["55","42","205","90","24","0","176","81","33","106","0.297903","CEE77209-0E03-4CB2-9076-4CC8AACD2B27","0.463252","29","54","0.346265","CE1DEF8B-7F57-462C-91B4-5AE43F9703B2","39F706CF-4E6F-48ED-B66B-231C5D27FD0D","558D5F92-C888-442B-8581-FE854C62F8B8","239","30DDF115-78CA-4CC0-BC69-950E6CCCA660","44BDE929-0C36-42D6-8C1E-A0B126DA2FC8","D00D1BC0-E1BB-4202-8037-77ACA2C4E5DB","6E32C3E0-363D-4879-99D6-E879B9C9DDE3","227","255","78","95","242","44","178","183","234","35","215","178","59"]]

*/
CREATE   PROCEDURE [dbo].[sp_GenerationRandomArray](
@SchemaName sysname,
@TableName sysname
)
AS
BEGIN

  SET NOCOUNT ON;
  DECLARE @FullName varchar(256),
    @Array varchar(max),
    @ArrayItem varchar(max),
    @Rand float,
    @column_id int, 
    @object_id int, 
    @system_type_id int, 
    @max_length int, 
    @Range varchar(200),
    @MsgCount int =1,
    @MsgId uniqueidentifier,
    @StrMsg varchar(100)


  SET @FullName = @SchemaName+'.'+@TableName
  SET @MsgCount = RAND() * 20 + 3
  SET @StrMsg = 'Generated Orders in 1 message: ' + CAST(@MsgCount AS varchar(25))
  EXEC audit.sp_Print @StrMsg

  WHILE @MsgCount > 0 BEGIN
  
    SELECT
      @column_id  = column_id, 
      @object_id = object_id, 
      @system_type_id = system_type_id, 
      @max_length = max_length, 
      @Range = [Range]
     FROM [dbo].[DataGeneration]
    WHERE object_id = OBJECT_ID(@SchemaName+'.'+@TableName)
    AND column_id = 1
  
    SET @ArrayItem = NULL
    WHILE @column_id IS NOT NULL BEGIN

      SELECT @ArrayItem = ISNULL(@ArrayItem + ',','') + '"' + dbo.fn_GenerationRandomField(@system_type_id, @max_length, RAND(),NEWID(),@Range) + '"'
      IF @ArrayItem IS NULL BEGIN
        SELECT @MsgCount, @column_id, @ArrayItem
        RETURN -1
      END
      SET @column_id = (SELECT TOP 1 column_id  FROM [dbo].[DataGeneration] WHERE object_id = OBJECT_ID(@SchemaName+'.'+@TableName)  AND column_id > @column_id ORDER BY Id)
      
      SELECT
        @column_id  = column_id, 
        @object_id = object_id, 
        @system_type_id = system_type_id, 
        @max_length = max_length, 
        @Range = [Range]
      FROM [dbo].[DataGeneration]
      WHERE object_id = OBJECT_ID(@SchemaName + '.' + @TableName)
      AND column_id = @column_id
    END
    SET @Array = ISNULL(@Array +',','') + '[' + @ArrayItem + ']'
    SET @MsgCount = @MsgCount - 1
  
  END
  SET @Array = '[' + @Array + ']'
  
  SET @MsgId = NEWID()

  IF NOT @Array IS NULL 
      INSERT [dbo].[msgqueue] (session_id, msg_id, msg, msg_key)
      SELECT 0, @MsgId, @Array, @SchemaName + '.' + @TableName
  ELSE BEGIN
    SET @StrMsg = 'NULL msg skip'
    EXEC audit.sp_Print @StrMsg
  END
    
END