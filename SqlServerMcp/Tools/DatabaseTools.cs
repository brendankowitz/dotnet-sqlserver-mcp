using System.ComponentModel;
using ModelContextProtocol.Server;
using SqlServerMcp.Services;

namespace SqlServerMcp.Tools;

[McpServerToolType]
public class DatabaseTools
{
    private readonly ISqlService _sqlService;
    private readonly ResultFormatter _formatter;

    public DatabaseTools(ISqlService sqlService, ResultFormatter formatter)
    {
        _sqlService = sqlService;
        _formatter = formatter;
    }

    [McpServerTool]
    [Description("List all available databases on the SQL Server instance.")]
    public async Task<string> ListDatabases()
    {
        try
        {
            var databases = await _sqlService.ListDatabasesAsync();

            if (databases.Count == 0)
            {
                return "No user databases found.";
            }

            var result = new Models.QueryResult
            {
                ColumnNames = new[] { "Database Name", "Database ID", "Created Date", "State", "Recovery Model", "Size (MB)" },
                Rows = databases.Select(d => new object[]
                {
                    d.DatabaseName,
                    d.DatabaseId,
                    d.CreatedDate.ToString("yyyy-MM-dd HH:mm"),
                    d.State,
                    d.RecoveryModel,
                    d.SizeMB
                }).ToList(),
                ExecutionTimeSeconds = 0,
                WasTruncated = false
            };

            return _formatter.FormatQueryResult(result);
        }
        catch (Exception ex)
        {
            return $"Error listing databases: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Get the current database name.")]
    public async Task<string> GetCurrentDatabase()
    {
        try
        {
            var currentDb = await _sqlService.GetCurrentDatabaseAsync();
            return $"Current database: {currentDb}";
        }
        catch (Exception ex)
        {
            return $"Error getting current database: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Switch to a different database. Note: This changes the database context for subsequent queries.")]
    public async Task<string> SwitchDatabase(
        [Description("The name of the database to switch to")] string databaseName)
    {
        try
        {
            var currentDb = await _sqlService.GetCurrentDatabaseAsync();
            await _sqlService.SwitchDatabaseAsync(databaseName);
            var newDb = await _sqlService.GetCurrentDatabaseAsync();

            return $"Successfully switched from '{currentDb}' to '{newDb}'";
        }
        catch (Exception ex)
        {
            return $"Error switching database: {ex.Message}";
        }
    }
}
