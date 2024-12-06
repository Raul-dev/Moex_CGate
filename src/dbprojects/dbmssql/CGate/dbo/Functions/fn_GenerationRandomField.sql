-- SELECT dbo.fn_GenerationRandomField(127,8,RAND(), NEWID(),'1,100')
-- SELECT dbo.fn_GenerationRandomField(231,40,RAND(), NEWID(),'1,100')

--SELECT * FROM sys.types T WHERE system_type_id = 231



CREATE   FUNCTION [dbo].[fn_GenerationRandomField](
@system_type_id int,
@max_length int,
@Rand float,
@Newid uniqueidentifier,
@range varchar(100)
) RETURNS varchar(100)
AS 
BEGIN
    DECLARE @Value varchar(100)
    SET @Value = (SELECT 
    CASE T.[name] 
        WHEN 'tinyint' THEN CAST(CAST(@Rand*256 AS tinyint ) AS varchar(100))
        WHEN 'smallint' THEN CAST(CAST(@Rand*256 AS tinyint ) AS varchar(100))
        WHEN 'int' THEN CAST(CAST(@Rand*256 AS tinyint ) AS varchar(100))
        WHEN 'bigint' THEN CAST(CAST(@Rand*256 AS tinyint ) AS varchar(100))
        WHEN 'real' THEN CAST(@Rand AS varchar(100))
        WHEN 'money' THEN CAST(@Rand AS varchar(100))
        WHEN 'float' THEN CAST(@Rand AS varchar(100))
        WHEN 'decimal' THEN CAST(@Rand AS varchar(100))
        WHEN 'numeric' THEN CAST(@Rand AS varchar(100))
        WHEN 'smallmoney' THEN CAST(@Rand AS varchar(100))
        WHEN 'decimal' THEN CAST(@Rand AS varchar(100))
        WHEN 'datetime' THEN FORMAT(GetDate(),'yyyy.MM.dd HH:mm:ss.ffffff')
        WHEN 'datetime2' THEN FORMAT(GetDate(),'yyyy.MM.dd HH:mm:ss.ffffff')
    ELSE 
     CONVERT(varchar(255), @Newid)
    END
    FROM sys.types T
    WHERE --system_type_id 
    user_type_id = @system_type_id
    )
    RETURN @Value
END