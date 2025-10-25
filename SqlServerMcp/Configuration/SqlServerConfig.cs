namespace SqlServerMcp.Configuration;

public record SqlServerConfig
{
    public required string ConnectionString { get; init; }
    public bool ReadOnly { get; init; } = true;
    public int DefaultTimeout { get; init; } = 30;
    public int MaxRows { get; init; } = 1000;

    // Programmability features
    public bool AllowProcedureExecution { get; init; } = false;
    public List<string> AllowedProcedures { get; init; } = new();
    public bool AllowFunctionExecution { get; init; } = true;
    public int MaxTvpRows { get; init; } = 10000;

    public bool EnableAdminTools { get; init; } = false;
}
