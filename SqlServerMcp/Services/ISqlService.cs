using SqlServerMcp.Models;
using System.Data;

namespace SqlServerMcp.Services;

public interface ISqlService
{
    // Core query execution
    Task<QueryResult> ExecuteQueryAsync(string sql, int timeout, int maxRows);
    Task<QueryResult> ExecuteParameterizedQueryAsync(string sql, Dictionary<string, object> parameters, int timeout, int maxRows);

    // Schema discovery
    Task<List<TableInfo>> GetTablesAsync(string? schemaPattern, string? tablePattern, bool includeSystemTables);
    Task<TableDetails> GetTableDetailsAsync(string tableName, bool includeIndexes, bool includeConstraints);
    Task<List<ViewInfo>> GetViewsAsync(string? schemaPattern, string? viewPattern);
    Task<List<StoredProcedureInfo>> GetStoredProceduresAsync(string? schemaPattern, string? procedurePattern, bool includeParameters);

    // Metadata
    Task<List<ForeignKeyInfo>> GetForeignKeysAsync(string? tableName, string direction);
    Task<List<IndexInfo>> GetIndexesAsync(string? tableName, bool includeStats);
    Task<List<ColumnReference>> FindColumnsByTypeAsync(string dataType, string? schemaPattern, string? tablePattern);
    Task<List<DependencyInfo>> GetDependenciesAsync(string objectName, string dependencyType);

    // Diagnostics
    Task<DatabaseInfo> GetDatabaseInfoAsync();
    Task<List<ConnectionInfo>> GetConnectionsAsync();
    Task<List<QueryStatInfo>> GetQueryStatsAsync(string metric, int topN);

    // Data validation
    Task<List<ConstraintViolation>> CheckConstraintsAsync(string? tableName, string constraintType);
    Task<QueryResult> FindDuplicatesAsync(string tableName, string[] columns, int limit);

    // Stored procedures
    Task<StoredProcedureResult> ExecuteStoredProcedureAsync(string procedureName, Dictionary<string, object>? parameters, int timeout, bool returnResults);
    Task<string> GetProcedureDefinitionAsync(string procedureName);
    Task<List<ParameterInfo>> GetProcedureParametersAsync(string procedureName);

    // Functions
    Task<List<FunctionInfo>> GetFunctionsAsync(string? schemaPattern, string? functionPattern, string? functionType);
    Task<string> ExecuteScalarFunctionAsync(string functionName, object[]? parameters);
    Task<QueryResult> ExecuteTableFunctionAsync(string functionName, object[]? parameters, int maxRows);
    Task<string> GetFunctionDefinitionAsync(string functionName);
    Task<List<ParameterInfo>> GetFunctionParametersAsync(string functionName);

    // Database management
    Task<List<DatabaseListInfo>> ListDatabasesAsync();
    Task SwitchDatabaseAsync(string databaseName);
    Task<string> GetCurrentDatabaseAsync();
}

// Additional model classes
public record ViewInfo
{
    public required string SchemaName { get; init; }
    public required string ViewName { get; init; }
    public string? Definition { get; init; }
}

public record ForeignKeyInfo
{
    public required string ForeignKeyName { get; init; }
    public required string ParentTable { get; init; }
    public required string ParentColumns { get; init; }
    public required string ReferencedTable { get; init; }
    public required string ReferencedColumns { get; init; }
    public required string UpdateRule { get; init; }
    public required string DeleteRule { get; init; }
}

public record ColumnReference
{
    public required string SchemaName { get; init; }
    public required string TableName { get; init; }
    public required string ColumnName { get; init; }
    public required string DataType { get; init; }
}

public record DependencyInfo
{
    public required string ObjectName { get; init; }
    public required string ObjectType { get; init; }
    public required string DependencyType { get; init; }
}

public record DatabaseInfo
{
    public required string DatabaseName { get; init; }
    public required string CompatibilityLevel { get; init; }
    public required string Collation { get; init; }
    public required string RecoveryModel { get; init; }
    public decimal DatabaseSizeMB { get; init; }
    public decimal LogSizeMB { get; init; }
    public DateTime CreatedDate { get; init; }
    public required string Owner { get; init; }
}

public record ConnectionInfo
{
    public int SessionId { get; init; }
    public required string LoginName { get; init; }
    public required string HostName { get; init; }
    public required string ProgramName { get; init; }
    public DateTime? LoginTime { get; init; }
}

public record QueryStatInfo
{
    public required string QueryText { get; init; }
    public long ExecutionCount { get; init; }
    public long TotalCpuTimeMs { get; init; }
    public long AvgCpuTimeMs { get; init; }
    public long TotalDurationMs { get; init; }
    public long AvgDurationMs { get; init; }
}

public record ConstraintViolation
{
    public required string TableName { get; init; }
    public required string ConstraintName { get; init; }
    public required string ConstraintType { get; init; }
    public required string ViolationDetails { get; init; }
}

public record StoredProcedureResult
{
    public List<QueryResult> ResultSets { get; init; } = new();
    public Dictionary<string, object?> OutputParameters { get; init; } = new();
    public int? ReturnValue { get; init; }
    public double ExecutionTimeSeconds { get; init; }
    public List<string> Messages { get; init; } = new();
}

public record DatabaseListInfo
{
    public required string DatabaseName { get; init; }
    public int DatabaseId { get; init; }
    public DateTime CreatedDate { get; init; }
    public required string State { get; init; }
    public required string RecoveryModel { get; init; }
    public decimal SizeMB { get; init; }
}
