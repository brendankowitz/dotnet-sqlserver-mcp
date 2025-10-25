namespace SqlServerMcp.Models;

public record FunctionInfo
{
    public required string SchemaName { get; init; }
    public required string FunctionName { get; init; }
    public required string FunctionType { get; init; }
    public string? ReturnType { get; init; }
    public DateTime CreatedDate { get; init; }
    public DateTime ModifiedDate { get; init; }
    public List<ParameterInfo> Parameters { get; init; } = new();
}
