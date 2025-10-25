namespace SqlServerMcp.Models;

public record StoredProcedureInfo
{
    public required string SchemaName { get; init; }
    public required string ProcedureName { get; init; }
    public DateTime CreatedDate { get; init; }
    public DateTime ModifiedDate { get; init; }
    public List<ParameterInfo> Parameters { get; init; } = new();
}
