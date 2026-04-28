
CREATE FUNCTION [audit].fn_BuildProcedureParams(@ObjectId INT)
RETURNS NVARCHAR(MAX)
AS
BEGIN
    DECLARE @Result NVARCHAR(MAX) ;
    DECLARE @SchemaName NVARCHAR(128);
    DECLARE @ProcName NVARCHAR(128),
    @IsOutput bit,
    @LastParam sysname

    SELECT @SchemaName = SCHEMA_NAME(o.schema_id), @ProcName = o.name FROM sys.objects o WHERE o.object_id = @ObjectId AND o.type IN ('P', 'PC');
    IF @SchemaName IS NULL RETURN N'-- Object not found or not a procedure';

    --SET @Result = @Result + N'[' + @SchemaName + N'].[' + @ProcName + N'] ';

    SELECT @Result = ISNULL(@Result + '   ','') + '''' +
        IIF((EXC.[TypeName] IS NULL), 
          CASE WHEN p.is_output = 1 THEN + IIF((EXC.[TypeName] IS NULL), '/*', '') + t.name + IIF((EXC.[TypeName] IS NULL), '*/', '') + p.name + N'=' + p.name + N' OUTPUT ''' ELSE
              CASE WHEN t.name IN ('varchar','nvarchar','char','nchar','text','ntext') THEN
                  p.name + N'=''+ISNULL(''''''''+' + 'LTRIM(CAST(' + p.name + N' AS varchar(' + [audit].[fn_GetEstimatedStringLength](p.user_type_id, p.max_length, p.precision) + N')))' + '+'''''''',''NULL'')' ELSE
                  p.name + N'=''+ISNULL(LTRIM(CAST(' + p.name + N' AS varchar(' + [audit].[fn_GetEstimatedStringLength](p.user_type_id, p.max_length, p.precision) + N'))),''NULL'')'
              END
          END 
         , '/*' +t.name + ' ' + p.name +' = ' + p.name+ '*/'''
        ) + N'+'',''+' +
        CHAR(13) + CHAR(10),
        @IsOutput = p.is_output,
        @LastParam = t.name
    FROM sys.parameters p JOIN sys.types t ON p.user_type_id = t.user_type_id
    LEFT JOIN [audit].[fn_BuildExceptType]() EXC ON t.[name] = EXC.[TypeName]
    WHERE p.object_id = @ObjectId ORDER BY p.parameter_id;

    IF @Result IS NULL
      SET @Result =N'''''';
    ELSE
      IF @LastParam IN ('varchar','nvarchar','char','nchar','text','ntext') 
        SET @Result = LEFT(@Result, LEN(@Result) - 7) ;
      ELSE
        SET @Result = LEFT(@Result, LEN(@Result) - 7) ;

    RETURN @Result;
END
GO
