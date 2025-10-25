using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using SqlServerMcp.Configuration;
using SqlServerMcp.Services;

// Get connection string from environment variable
var connectionString = Environment.GetEnvironmentVariable("SQL_SERVER_CONNECTION_STRING");
if (string.IsNullOrEmpty(connectionString))
{
    Console.Error.WriteLine("Error: SQL_SERVER_CONNECTION_STRING environment variable is required");
    Console.Error.WriteLine("Example: Server=localhost;Database=MyDB;Trusted_Connection=True;TrustServerCertificate=True;");
    return 1;
}

// Parse configuration from environment variables
var config = new SqlServerConfig
{
    ConnectionString = connectionString,
    ReadOnly = bool.Parse(Environment.GetEnvironmentVariable("SQL_SERVER_READONLY") ?? "true"),
    DefaultTimeout = int.Parse(Environment.GetEnvironmentVariable("SQL_SERVER_DEFAULT_TIMEOUT") ?? "30"),
    MaxRows = int.Parse(Environment.GetEnvironmentVariable("SQL_SERVER_MAX_ROWS") ?? "1000"),
    AllowProcedureExecution = bool.Parse(Environment.GetEnvironmentVariable("SQL_SERVER_ALLOW_PROCEDURE_EXECUTION") ?? "false"),
    AllowedProcedures = ParseList(Environment.GetEnvironmentVariable("SQL_SERVER_ALLOWED_PROCEDURES")),
    AllowFunctionExecution = bool.Parse(Environment.GetEnvironmentVariable("SQL_SERVER_ALLOW_FUNCTION_EXECUTION") ?? "true"),
    MaxTvpRows = int.Parse(Environment.GetEnvironmentVariable("SQL_SERVER_MAX_TVP_ROWS") ?? "10000"),
    EnableAdminTools = bool.Parse(Environment.GetEnvironmentVariable("SQL_SERVER_ENABLE_ADMIN_TOOLS") ?? "false")
};

// Display configuration
Console.WriteLine("=== SQL Server MCP Server Configuration ===");
Console.WriteLine($"Read-Only Mode: {config.ReadOnly}");
Console.WriteLine($"Default Timeout: {config.DefaultTimeout}s");
Console.WriteLine($"Max Rows: {config.MaxRows}");
Console.WriteLine($"Allow Procedure Execution: {config.AllowProcedureExecution}");
if (config.AllowedProcedures.Any())
{
    Console.WriteLine($"Allowed Procedures: {string.Join(", ", config.AllowedProcedures)}");
}
Console.WriteLine($"Allow Function Execution: {config.AllowFunctionExecution}");
Console.WriteLine($"Enable Admin Tools: {config.EnableAdminTools}");
Console.WriteLine();

// Validate connection
try
{
    Console.WriteLine("Validating database connection...");
    await ConnectionValidator.ValidateConnectionAsync(connectionString);
    Console.WriteLine();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed to connect to database: {ex.Message}");
    return 1;
}

// Build and configure the MCP server
var builder = Host.CreateEmptyApplicationBuilder(null);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly(); // Auto-discover all tools with [McpServerToolType] attribute

// Register services
builder.Services.AddSingleton(config);
builder.Services.AddSingleton<ISqlService, SqlService>();
builder.Services.AddSingleton<ResultFormatter>();

var app = builder.Build();

Console.WriteLine("=== SQL Server MCP Server Started ===");
Console.WriteLine("Ready to accept MCP requests via stdio");
Console.WriteLine("Press Ctrl+C to stop");
Console.WriteLine();

// Run the server
await app.RunAsync();

return 0;

// Helper function to parse comma-separated lists
static List<string> ParseList(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
        return new List<string>();

    return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToList();
}
