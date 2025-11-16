using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;
using SqlServerMcp.Services;

namespace SqlServerMcp.Tools;

[McpServerToolType]
public class ConnectionDiscoveryTools
{
    private readonly IConnectionStringDiscoveryService _discoveryService;

    public ConnectionDiscoveryTools(IConnectionStringDiscoveryService discoveryService)
    {
        _discoveryService = discoveryService;
    }

    [McpServerTool]
    [Description("Discover SQL Server connection strings from configuration files in the solution (appsettings.json, .env files, etc.).")]
    public async Task<string> DiscoverConnectionStrings(
        [Description("Root path to search for configuration files (default: current directory)")] string? searchPath = null)
    {
        try
        {
            var path = searchPath ?? Directory.GetCurrentDirectory();

            if (!Directory.Exists(path))
            {
                return $"Error: Directory '{path}' does not exist.";
            }

            var discovered = await _discoveryService.DiscoverConnectionStringsAsync(path);

            if (discovered.Count == 0)
            {
                return $"No connection strings found in '{path}'.\n\n" +
                       "Searched for:\n" +
                       "- appsettings.json and appsettings.*.json files\n" +
                       "- .env and .env.* files\n" +
                       "- Connection strings in ConnectionStrings section\n" +
                       "- Environment variables containing SQL/DATABASE/CONNECTION keywords";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Found {discovered.Count} connection string(s) in '{path}':\n");

            // Group by source file
            var grouped = discovered.GroupBy(d => d.FilePath);

            foreach (var group in grouped)
            {
                var fileName = Path.GetFileName(group.Key);
                var relativePath = Path.GetRelativePath(path, group.Key);

                sb.AppendLine($"📄 {relativePath}");
                sb.AppendLine($"   Source Type: {group.First().SourceType}");
                if (!string.IsNullOrEmpty(group.First().Environment))
                {
                    sb.AppendLine($"   Environment: {group.First().Environment}");
                }
                sb.AppendLine();

                foreach (var conn in group)
                {
                    sb.AppendLine($"   Name: {conn.Name}");

                    // Mask sensitive information in connection strings
                    var maskedConnectionString = MaskSensitiveInfo(conn.ConnectionString);
                    sb.AppendLine($"   Connection String: {maskedConnectionString}");
                    sb.AppendLine();
                }

                sb.AppendLine();
            }

            sb.AppendLine("💡 Tip: You can use these connection strings to configure the MCP server or switch databases.");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error discovering connection strings: {ex.Message}";
        }
    }

    private static string MaskSensitiveInfo(string connectionString)
    {
        // Mask passwords and sensitive information
        var masked = connectionString;

        // Mask Password
        masked = System.Text.RegularExpressions.Regex.Replace(
            masked,
            @"(Password|Pwd)\s*=\s*[^;]+",
            "$1=***MASKED***",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Mask User ID if it's not empty
        masked = System.Text.RegularExpressions.Regex.Replace(
            masked,
            @"(User\s*ID|UID)\s*=\s*([^;]+)",
            m => $"{m.Groups[1].Value}={m.Groups[2].Value.Substring(0, Math.Min(3, m.Groups[2].Value.Length))}***",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return masked;
    }
}
