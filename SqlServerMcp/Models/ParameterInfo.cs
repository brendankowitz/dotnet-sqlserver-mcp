using System.Data;

namespace SqlServerMcp.Models;

public record ParameterInfo
{
    public required string ParameterName { get; init; }
    public required string DataType { get; init; }
    public int MaxLength { get; init; }
    public byte Precision { get; init; }
    public byte Scale { get; init; }
    public ParameterDirection Direction { get; init; }
    public bool HasDefault { get; init; }
    public string? DefaultValue { get; init; }
    public bool IsNullable { get; init; }
}
