CREATE FUNCTION [audit].fn_GetEstimatedStringLength 
(
    @user_type_id INT,
    @max_length SMALLINT,
    @precision TINYINT
)
RETURNS VARCHAR(10)
AS
BEGIN
    DECLARE @typeName NVARCHAR(128) = TYPE_NAME(@user_type_id);
    DECLARE @result VARCHAR(10);

    SET @result = CASE 
        -- Юникод строки (делим на 2, так как max_length в байтах)
        WHEN @typeName IN ('nvarchar', 'nchar', 'sysname') THEN 
            -- 'MAX' = 4000
            CASE WHEN @max_length = -1 THEN '4000' ELSE CAST(@max_length / 2 AS VARCHAR(10)) END
        
        -- Обычные строки
        WHEN @typeName IN ('varchar', 'char') THEN 
            -- 'MAX' = 8000
            CASE WHEN @max_length = -1 THEN '8000' ELSE CAST(@max_length AS VARCHAR(10)) END
        
        -- Точные числа (добавляем запас под знак '-' и точку '.')
        WHEN @typeName IN ('decimal', 'numeric') THEN 
            CAST(@precision + 2 AS VARCHAR(10))
        
        -- Целые числа (максимальное кол-во символов для каждого типа)
        WHEN @typeName = 'bigint'   THEN '20'
        WHEN @typeName = 'int'      THEN '11'
        WHEN @typeName = 'smallint' THEN '6'
        WHEN @typeName = 'tinyint'  THEN '3'
        
        -- Дата и время (форматы ISO)
        WHEN @typeName IN ('datetime', 'datetime2') THEN '27'
        WHEN @typeName = 'date'     THEN '10'
        WHEN @typeName = 'time'     THEN '16'
        
        -- Уникальные идентификаторы (GUID)
        WHEN @typeName = 'uniqueidentifier' THEN '36'
        
        -- Значение по умолчанию для прочих типов
        ELSE '4000' 
    END;

    RETURN @result;
END;