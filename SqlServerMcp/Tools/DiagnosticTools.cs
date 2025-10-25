using System.ComponentModel;
using ModelContextProtocol.Server;
using SqlServerMcp.Services;

namespace SqlServerMcp.Tools;

[McpServerToolType]
public class DiagnosticTools
{
    private readonly ISqlService _sqlService;
    private readonly ResultFormatter _formatter;

    public DiagnosticTools(ISqlService sqlService, ResultFormatter formatter)
    {
        _sqlService = sqlService;
        _formatter = formatter;
    }

    [McpServerTool]
    [Description("Get information about current database connections.")]
    public async Task<string> GetConnections()
    {
        try
        {
            var connections = await _sqlService.GetConnectionsAsync();

            if (connections.Count == 0)
            {
                return "No active connections found.";
            }

            var result = new Models.QueryResult
            {
                ColumnNames = new[] { "Session ID", "Login Name", "Host Name", "Program Name", "Login Time" },
                Rows = connections.Select(c => new object[]
                {
                    c.SessionId,
                    c.LoginName,
                    c.HostName,
                    c.ProgramName,
                    c.LoginTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? ""
                }).ToList(),
                ExecutionTimeSeconds = 0,
                WasTruncated = false
            };

            return _formatter.FormatQueryResult(result);
        }
        catch (Exception ex)
        {
            return $"Error getting connections: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Get top queries by execution count, CPU time, or duration.")]
    public async Task<string> GetQueryStats(
        [Description("Metric: 'execution_count', 'cpu_time', 'duration', or 'logical_reads'")] string metric = "execution_count",
        [Description("Number of queries to return")] int topN = 10)
    {
        try
        {
            var stats = await _sqlService.GetQueryStatsAsync(metric, topN);

            if (stats.Count == 0)
            {
                return "No query statistics available.";
            }

            var result = new Models.QueryResult
            {
                ColumnNames = new[] { "Query Text", "Execution Count", "Total CPU (ms)", "Avg CPU (ms)", "Total Duration (ms)", "Avg Duration (ms)" },
                Rows = stats.Select(s => new object[]
                {
                    s.QueryText.Length > 100 ? s.QueryText.Substring(0, 100) + "..." : s.QueryText,
                    s.ExecutionCount,
                    s.TotalCpuTimeMs,
                    s.AvgCpuTimeMs,
                    s.TotalDurationMs,
                    s.AvgDurationMs
                }).ToList(),
                ExecutionTimeSeconds = 0,
                WasTruncated = false
            };

            return _formatter.FormatQueryResult(result);
        }
        catch (Exception ex)
        {
            return $"Error getting query statistics: {ex.Message}";
        }
    }
}
