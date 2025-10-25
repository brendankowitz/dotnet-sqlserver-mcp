# SQL Server MCP Server

A .NET-based Model Context Protocol (MCP) server providing 6 essential tools for SQL Server database inspection, querying, and debugging.

[![NuGet](https://img.shields.io/nuget/v/SqlServerLocalMcp.svg)](https://www.nuget.org/packages/SqlServerLocalMcp/)

## Quick Start

### Install as .NET Tool (Recommended)

```bash
dotnet tool install --global SqlServerLocalMcp
```

Then configure in Claude Desktop (see configuration below).

### Or Build from Source

```bash
cd SqlServerMcp
dotnet build
```

### Test Locally
```bash
export SQL_SERVER_CONNECTION_STRING="Server=localhost;Database=MyDB;Trusted_Connection=True;TrustServerCertificate=True;"
dotnet run
```

## Setup for Claude Desktop

Edit the config file for your platform:

**macOS:** `~/Library/Application Support/Claude/claude_desktop_config.json`
**Windows:** `%APPDATA%\Claude\claude_desktop_config.json`

### Option 1: Using Installed .NET Tool (Recommended)

```json
{
  "mcpServers": {
    "sqlserver": {
      "command": "sqlserverlocal-mcp",
      "args": [],
      "env": {
        "SQL_SERVER_CONNECTION_STRING": "Server=localhost;Database=MySqlDb;Trusted_Connection=True;TrustServerCertificate=True;"
      }
    }
  }
}
```

### Option 2: Running from Source

```json
{
  "mcpServers": {
    "sqlserver": {
      "command": "dotnet",
      "args": ["run", "--project", "C:\\src\\dotnet-sqlserver-mcp\\SqlServerMcp"],
      "env": {
        "SQL_SERVER_CONNECTION_STRING": "Server=localhost;Database=MySqlDb;Trusted_Connection=True;TrustServerCertificate=True;"
      }
    }
  }
}
```

**Important:**
- Use absolute paths
- Windows: Use `\\` for backslashes or `/` for forward slashes
- macOS/Linux: Use forward slashes
- Restart Claude Desktop after editing

## Setup for Claude CLI (Code Editor)

### Option 1: Using Installed .NET Tool (Recommended)

```bash
claude mcp add --transport stdio sqlserver \
  --env SQL_SERVER_CONNECTION_STRING="Server=localhost;Database=MySqlDb;Trusted_Connection=True;TrustServerCertificate=True;" \
  -- sqlserverlocal-mcp
```

### Option 2: Running from Source

**Windows:**
```bash
claude mcp add --transport stdio sqlserver ^
  --env SQL_SERVER_CONNECTION_STRING="Server=localhost;Database=MySqlDb;Trusted_Connection=True;TrustServerCertificate=True;" ^
  -- cmd /c dotnet run --project C:\src\dotnet-sqlserver-mcp\SqlServerMcp
```

**macOS/Linux:**
```bash
claude mcp add --transport stdio sqlserver \
  --env SQL_SERVER_CONNECTION_STRING="Server=localhost;Database=MySqlDb;Trusted_Connection=True;TrustServerCertificate=True;" \
  -- dotnet run --project /path/to/dotnet-sqlserver-mcp/SqlServerMcp
```

**With additional options:**
```bash
claude mcp add --transport stdio sqlserver \
  --env SQL_SERVER_CONNECTION_STRING="Server=localhost;Database=MyDB;Trusted_Connection=True;" \
  --env SQL_SERVER_READONLY="true" \
  --env SQL_SERVER_MAX_ROWS="500" \
  -- dotnet run --project /path/to/SqlServerMcp
```

**Management commands:**
```bash
claude mcp list              # List all servers
claude mcp get sqlserver     # View server details
claude mcp remove sqlserver  # Remove server
```

## Connection Strings

| Type | Example |
|------|---------|
| **Windows Auth** | `Server=localhost;Database=MyDB;Trusted_Connection=True;TrustServerCertificate=True;` |
| **SQL Auth** | `Server=localhost;Database=MyDB;User Id=sa;Password=Pass123;TrustServerCertificate=True;` |
| **Azure SQL** | `Server=myserver.database.windows.net;Database=MyDB;User Id=user;Password=Pass123;Encrypt=true;` |
| **LocalDB** | `Server=(localdb)\\\\mssqllocaldb;Database=MyDB;Trusted_Connection=True;` |

## Configuration Options

Add to the `env` section:

| Variable | Default | Description |
|----------|---------|-------------|
| `SQL_SERVER_CONNECTION_STRING` | *required* | Connection string |
| `SQL_SERVER_READONLY` | `true` | Block INSERT/UPDATE/DELETE |
| `SQL_SERVER_MAX_ROWS` | `1000` | Max rows returned |
| `SQL_SERVER_DEFAULT_TIMEOUT` | `30` | Query timeout (seconds) |
| `SQL_SERVER_ALLOW_PROCEDURE_EXECUTION` | `false` | Allow stored procedures |
| `SQL_SERVER_ALLOWED_PROCEDURES` | `` | Allowed procedures (e.g., `dbo.Get*,reporting.*`) |
| `SQL_SERVER_ALLOW_FUNCTION_EXECUTION` | `true` | Allow functions |

**Example with options:**
```json
{
  "mcpServers": {
    "sqlserver": {
      "command": "dotnet",
      "args": ["run", "--project", "C:\\src\\dotnet-sqlserver-mcp\\SqlServerMcp"],
      "env": {
        "SQL_SERVER_CONNECTION_STRING": "Server=localhost;Database=MyDB;Trusted_Connection=True;TrustServerCertificate=True;",
        "SQL_SERVER_READONLY": "true",
        "SQL_SERVER_MAX_ROWS": "500",
        "SQL_SERVER_ALLOW_PROCEDURE_EXECUTION": "true",
        "SQL_SERVER_ALLOWED_PROCEDURES": "dbo.GetOrders,reporting.*"
      }
    }
  }
}
```

## Available Tools (6)

### Core Essential Tools

| Tool | Purpose | Unique Value |
|------|---------|--------------|
| **execute_sql** | Run any SQL query | Universal workhorse - handles custom queries, aggregations, metadata inspection |
| **describe_table** | Inspect table schema | Clean formatted output for columns, indexes, constraints in one call |
| **get_procedure_definition** | View stored proc source | Clean T-SQL source code without metadata noise |
| **list_stored_procedures** | Find available procs | Quick filtered list with creation/modification dates |
| **get_query_stats** | Performance debugging | Pre-formatted top queries by CPU/duration/execution count |
| **get_connections** | Connection monitoring | Active sessions with program names and login times |

## Usage Examples

Ask:
- "List all tables in my database" → Uses `execute_sql` with INFORMATION_SCHEMA query
- "Describe the Orders table structure" → Uses `describe_table`
- "What foreign keys reference the Customers table?" → Uses `execute_sql` with sys.foreign_keys query
- "Show me the top 10 slowest queries" → Uses `get_query_stats`
- "Find duplicate email addresses in the Users table" → Uses `execute_sql` with GROUP BY/HAVING
- "Show me the code for the GetCustomerOrders stored procedure" → Uses `get_procedure_definition`

## Security

**Read-Only Mode (Default):** Only SELECT queries allowed. Set `SQL_SERVER_READONLY=false` to allow modifications.

**Stored Procedures:** Require `SQL_SERVER_ALLOW_PROCEDURE_EXECUTION=true` and optional allowlist.

**Adjust limits:**
```json
"env": {
  "SQL_SERVER_MAX_ROWS": "100",
  "SQL_SERVER_DEFAULT_TIMEOUT": "60"
}
```

## Project Structure

```
SqlServerMcp/
├── Program.cs              # Entry point
├── Configuration/          # Config models
├── Models/                 # Data models
├── Services/              # SqlService, ResultFormatter
└── Tools/                 # 4 tool classes (6 essential tools)
    ├── QueryTools.cs       # execute_sql
    ├── SchemaTools.cs      # describe_table, list_stored_procedures
    ├── DiagnosticTools.cs  # get_connections, get_query_stats
    └── ProcedureTools.cs   # get_procedure_definition
```

## Development

**Add new tools:** Create methods in `Tools/*.cs` with `[McpServerTool]` attribute:

```csharp
[McpServerTool]
[Description("Your tool description")]
public async Task<string> YourTool(
    [Description("Parameter description")] string parameter)
{
    // Implementation
}
```

**Test with MCP Inspector:**
```bash
npx @modelcontextprotocol/inspector dotnet run --project SqlServerMcp
```

## Requirements

- .NET 8 SDK
- SQL Server (local, Azure SQL, or Docker)
- Claude Desktop or Claude CLI

## License

See [LICENSE](LICENSE) file.

## Resources

- [MCP Specification](https://spec.modelcontextprotocol.io/)
- [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)
- [SQL Server Docs](https://learn.microsoft.com/en-us/sql/)
