using System.ComponentModel;
using ModelContextProtocol.Server;
using SqlServerMcp.Services;
using SqlServerMcp.Configuration;

namespace SqlServerMcp.Tools;

[McpServerToolType]
public class QueryTools
{
    private readonly ISqlService _sqlService;
    private readonly ResultFormatter _formatter;
    private readonly SqlServerConfig _config;

    public QueryTools(ISqlService sqlService, ResultFormatter formatter, SqlServerConfig config)
    {
        _sqlService = sqlService;
        _formatter = formatter;
        _config = config;
    }

    [McpServerTool]
    [Description("Execute a SQL query and return results in a formatted table.")]
    public async Task<string> ExecuteSql(
        [Description("T-SQL query to execute")] string sql,
        [Description("Command timeout in seconds")] int timeout = 30,
        [Description("Maximum rows to return")] int maxRows = 1000)
    {
        try
        {
            var result = await _sqlService.ExecuteQueryAsync(sql, timeout, maxRows);
            return _formatter.FormatQueryResult(result);
        }
        catch (Exception ex)
        {
            return $"Error executing query: {ex.Message}";
        }
    }

}
