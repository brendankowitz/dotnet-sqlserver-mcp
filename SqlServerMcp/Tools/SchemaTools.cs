using System.ComponentModel;
using ModelContextProtocol.Server;
using SqlServerMcp.Services;

namespace SqlServerMcp.Tools;

[McpServerToolType]
public class SchemaTools
{
    private readonly ISqlService _sqlService;
    private readonly ResultFormatter _formatter;

    public SchemaTools(ISqlService sqlService, ResultFormatter formatter)
    {
        _sqlService = sqlService;
        _formatter = formatter;
    }

    [McpServerTool]
    [Description("Get detailed schema information for a specific table.")]
    public async Task<string> DescribeTable(
        [Description("Table name (can include schema: schema.table)")] string tableName,
        [Description("Include index information")] bool includeIndexes = true,
        [Description("Include constraint information")] bool includeConstraints = true)
    {
        try
        {
            var details = await _sqlService.GetTableDetailsAsync(tableName, includeIndexes, includeConstraints);
            return _formatter.FormatTableDetails(details);
        }
        catch (Exception ex)
        {
            return $"Error describing table: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("List all stored procedures with metadata.")]
    public async Task<string> ListStoredProcedures(
        [Description("Filter by schema name pattern")] string? schemaPattern = null,
        [Description("Filter by procedure name pattern")] string? procedurePattern = null,
        [Description("Include parameter information")] bool includeParameters = false)
    {
        try
        {
            var procedures = await _sqlService.GetStoredProceduresAsync(schemaPattern, procedurePattern, includeParameters);

            if (procedures.Count == 0)
            {
                return "No stored procedures found.";
            }

            var result = new Models.QueryResult
            {
                ColumnNames = new[] { "Schema", "Procedure", "Created", "Modified" },
                Rows = procedures.Select(p => new object[]
                {
                    p.SchemaName,
                    p.ProcedureName,
                    p.CreatedDate.ToString("yyyy-MM-dd HH:mm"),
                    p.ModifiedDate.ToString("yyyy-MM-dd HH:mm")
                }).ToList(),
                ExecutionTimeSeconds = 0,
                WasTruncated = false
            };

            return _formatter.FormatQueryResult(result);
        }
        catch (Exception ex)
        {
            return $"Error listing stored procedures: {ex.Message}";
        }
    }
}
