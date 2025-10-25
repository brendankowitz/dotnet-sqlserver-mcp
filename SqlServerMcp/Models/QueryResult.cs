using System.Data;

namespace SqlServerMcp.Models;

public record QueryResult
{
    public required string[] ColumnNames { get; init; }
    public required List<object[]> Rows { get; init; }
    public int RowCount => Rows.Count;
    public double ExecutionTimeSeconds { get; init; }
    public bool WasTruncated { get; init; }
}
