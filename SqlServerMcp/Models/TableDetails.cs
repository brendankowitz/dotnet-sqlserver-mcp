namespace SqlServerMcp.Models;

public record TableDetails
{
    public required string SchemaName { get; init; }
    public required string TableName { get; init; }
    public List<ColumnInfo> Columns { get; init; } = new();
    public List<IndexInfo> Indexes { get; init; } = new();
    public List<ConstraintInfo> Constraints { get; init; } = new();
}

public record IndexInfo
{
    public required string IndexName { get; init; }
    public required string IndexType { get; init; }
    public required string Columns { get; init; }
    public string? IncludedColumns { get; init; }
    public bool IsUnique { get; init; }
    public bool IsPrimaryKey { get; init; }
    public string? FilterDefinition { get; init; }
}

public record ConstraintInfo
{
    public required string ConstraintName { get; init; }
    public required string ConstraintType { get; init; }
    public required string Definition { get; init; }
    public string? ReferencedTable { get; init; }
}
