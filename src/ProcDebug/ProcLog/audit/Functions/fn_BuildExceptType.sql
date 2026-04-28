
CREATE FUNCTION [audit].fn_BuildExceptType()
RETURNS @Result TABLE (
    TypeName nvarchar(128) NOT NULL
)
AS
BEGIN
    INSERT INTO @Result (TypeName) VALUES ('тип1');
    INSERT INTO @Result (TypeName) VALUES ('тип2');
    INSERT INTO @Result (TypeName) VALUES ('TBigList');
    RETURN;
END;