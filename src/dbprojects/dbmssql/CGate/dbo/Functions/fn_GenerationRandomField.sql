-- SELECT dbo.fn_GenerationRandomField(127,8,RAND(), NEWID(),'1,100')
-- SELECT dbo.fn_GenerationRandomField(231,40,RAND(), NEWID(),'1,100')

--SELECT * FROM sys.types T WHERE system_type_id = 231


--SELECT CAST(MAX(value) AS int) FROM STRING_SPLIT('1,10000',',')
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
        WHEN 'smallint' THEN CAST(CAST(@Rand*64000 AS smallint ) AS varchar(100))
        WHEN 'int' THEN CAST(CAST(@Rand*(SELECT CAST(MAX(value) AS int) FROM STRING_SPLIT(@range,',')) AS int ) AS varchar(100))
        WHEN 'bigint' THEN CAST(CAST(@Rand*(SELECT CAST(MAX(value) AS int) FROM STRING_SPLIT(@range,',')) AS bigint ) AS varchar(100))
        WHEN 'real' THEN CAST(@Rand*(SELECT CAST(MAX(value) AS int) FROM STRING_SPLIT(@range,',')) AS varchar(100))
        WHEN 'money' THEN CAST(@Rand*(SELECT CAST(MAX(value) AS int) FROM STRING_SPLIT(@range,',')) AS varchar(100))
        WHEN 'float' THEN CAST(@Rand AS varchar(100))
        WHEN 'decimal' THEN CAST(@Rand*(SELECT CAST(MAX(value) AS int) FROM STRING_SPLIT(@range,',')) AS varchar(100))
        WHEN 'numeric' THEN CAST(@Rand*(SELECT CAST(MAX(value) AS int) FROM STRING_SPLIT(@range,',')) AS varchar(100))
        WHEN 'smallmoney' THEN CAST(@Rand*64000 AS varchar(100))
        WHEN 'decimal' THEN CAST(@Rand*(SELECT CAST(MAX(value) AS int) FROM STRING_SPLIT(@range,',')) AS varchar(100))
        WHEN 'datetime' THEN FORMAT(GetDate(),'yyyy.MM.dd HH:mm:ss.ffffff')
        WHEN 'datetime2' THEN FORMAT(GetDate(),'yyyy.MM.dd HH:mm:ss.ffffff')
    ELSE 
     CONVERT(varchar(255), @Newid)
    END
    FROM sys.types T
    WHERE --system_type_id 
    user_type_id = @system_type_id
    )
    SET @Value = (SELECT (
      CASE T.[name] 
      WHEN 'bigint' THEN CAST(
        IIF(CAST(@Value AS bigint) < CAST((SELECT CAST(MIN(value) AS int) FROM STRING_SPLIT(@range,',')) AS bigint ),CAST((SELECT CAST(MIN(value) AS int) FROM STRING_SPLIT(@range,',')) AS bigint ),CAST(@Value AS bigint))
      AS varchar(100))
      ELSE
        @Value
      END
      )
    FROM sys.types T
    WHERE --system_type_id 
    user_type_id = @system_type_id
    )

    RETURN @Value
END