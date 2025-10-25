namespace SqlServerMcp.Models;

public record ColumnInfo
{
    public required string ColumnName { get; init; }
    public required string DataType { get; init; }
    public int MaxLength { get; init; }
    public byte Precision { get; init; }
    public byte Scale { get; init; }
    public bool IsNullable { get; init; }
    public string? DefaultValue { get; init; }
    public bool IsIdentity { get; init; }
    public string? ComputedExpression { get; init; }
}
