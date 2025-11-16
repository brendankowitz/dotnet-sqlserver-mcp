using System.ComponentModel;
using ModelContextProtocol.Server;
using SqlServerMcp.Configuration;
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
                return "No user databases found.\n\n" +
                       "This SQL Server instance only has system databases (master, msdb, model, tempdb).\n" +
                       "User databases may need to be created or restored.";
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

            var formattedResult = _formatter.FormatQueryResult(result);
            formattedResult += $"\n\nFound {databases.Count} user database(s).";
            formattedResult += "\n💡 Use 'switch_database' to connect to a specific database.";

            return formattedResult;
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
            var isSystemDb = new[] { "master", "msdb", "model", "tempdb" }.Contains(currentDb, StringComparer.OrdinalIgnoreCase);

            var message = $"Current database: {currentDb}";

            if (isSystemDb)
            {
                message += "\n\n⚠️  NOTE: You are currently connected to a system database.";
                message += "\nSystem databases contain SQL Server metadata and configuration.";
                message += "\nFor application data, consider using 'list_databases' to find user databases,";
                message += "\nthen 'switch_database' to change to a specific database.";
            }

            return message;
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

    [McpServerTool]
    [Description("Connect to SQL Server using a new connection string. This allows dynamic connection to different SQL Server instances.")]
    public async Task<string> ConnectWithConnectionString(
        [Description("SQL Server connection string (e.g., 'Server=localhost;Database=MyDB;Trusted_Connection=True;TrustServerCertificate=True;')")] string connectionString)
    {
        try
        {
            // Validate the connection string before switching
            await ConnectionValidator.ValidateConnectionAsync(connectionString);

            // If validation succeeds, update the connection string
            _sqlService.SetConnectionString(connectionString);

            var currentDb = await _sqlService.GetCurrentDatabaseAsync();

            return $"✅ Successfully connected!\nCurrent database: {currentDb}\n\n" +
                   $"💡 Use 'list_databases' to see available databases or 'switch_database' to change database.";
        }
        catch (Exception ex)
        {
            return $"❌ Failed to connect: {ex.Message}\n\n" +
                   $"Please verify your connection string and ensure:\n" +
                   $"- The SQL Server instance is accessible\n" +
                   $"- Credentials are correct\n" +
                   $"- Firewall rules allow the connection\n\n" +
                   $"Example connection strings:\n" +
                   $"- Windows Auth: Server=localhost;Database=MyDB;Trusted_Connection=True;TrustServerCertificate=True;\n" +
                   $"- SQL Auth: Server=localhost;Database=MyDB;User Id=sa;Password=Pass123;TrustServerCertificate=True;";
        }
    }
}
