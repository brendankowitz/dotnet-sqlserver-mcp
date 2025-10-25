namespace SqlServerMcp.Models;

public record TableInfo
{
    public required string SchemaName { get; init; }
    public required string TableName { get; init; }
    public required string TableType { get; init; }
    public long RowCount { get; init; }
    public decimal SizeMB { get; init; }
    public DateTime CreatedDate { get; init; }
    public DateTime ModifiedDate { get; init; }
}
