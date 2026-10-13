namespace ContextSync.dal.Models;

public class TableSchema
{
    public string TableName { get; set; } = "";
    public List<ColumnSchema> Columns { get; set; } = new();
}

public class ColumnSchema
{
    public int ColumnId { get; set; }
    public string Name { get; set; } = "";
    public string DataType { get; set; } = "";
    public bool IsNullable { get; set; }
    public bool IsForeignKey { get; set; }
    public string? ReferencedTable { get; set; }
    public string? ReferencedColumn { get; set; }
}
