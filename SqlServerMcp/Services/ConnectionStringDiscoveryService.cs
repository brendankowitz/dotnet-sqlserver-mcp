using System.Text.Json;
using System.Text.RegularExpressions;

namespace SqlServerMcp.Services;

/// <summary>
/// Service for discovering connection strings from solution files
/// </summary>
public class ConnectionStringDiscoveryService : IConnectionStringDiscoveryService
{
    private static readonly string[] AppSettingsPatterns =
    [
        "appsettings.json",
        "appsettings.*.json"
    ];

    private static readonly string[] EnvFilePatterns =
    [
        ".env",
        ".env.*"
    ];

    public async Task<List<DiscoveredConnectionString>> DiscoverConnectionStringsAsync(
        string searchPath,
        CancellationToken cancellationToken = default)
    {
        var discoveredConnections = new List<DiscoveredConnectionString>();

        if (!Directory.Exists(searchPath))
        {
            throw new DirectoryNotFoundException($"Search path does not exist: {searchPath}");
        }

        // Search for appsettings files
        await DiscoverFromAppSettingsAsync(searchPath, discoveredConnections, cancellationToken);

        // Search for .env files
        await DiscoverFromEnvFilesAsync(searchPath, discoveredConnections, cancellationToken);

        return discoveredConnections;
    }

    private async Task DiscoverFromAppSettingsAsync(
        string searchPath,
        List<DiscoveredConnectionString> discoveredConnections,
        CancellationToken cancellationToken)
    {
        var jsonFiles = Directory.EnumerateFiles(searchPath, "appsettings*.json", SearchOption.AllDirectories)
            .Where(f => !f.Contains("bin") && !f.Contains("obj")) // Skip build outputs
            .ToList();

        foreach (var filePath in jsonFiles)
        {
            try
            {
                var jsonContent = await File.ReadAllTextAsync(filePath, cancellationToken);
                var jsonDoc = JsonDocument.Parse(jsonContent);

                // Look for ConnectionStrings section
                if (jsonDoc.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings))
                {
                    foreach (var property in connectionStrings.EnumerateObject())
                    {
                        var connectionString = property.Value.GetString();
                        if (!string.IsNullOrWhiteSpace(connectionString))
                        {
                            var environment = ExtractEnvironmentFromFileName(Path.GetFileName(filePath));

                            discoveredConnections.Add(new DiscoveredConnectionString
                            {
                                Name = property.Name,
                                ConnectionString = connectionString,
                                Source = Path.GetFileName(filePath),
                                SourceType = "appsettings",
                                FilePath = filePath,
                                Environment = environment
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log but continue - don't fail entire discovery for one bad file
                Console.Error.WriteLine($"Error parsing {filePath}: {ex.Message}");
            }
        }
    }

    private async Task DiscoverFromEnvFilesAsync(
        string searchPath,
        List<DiscoveredConnectionString> discoveredConnections,
        CancellationToken cancellationToken)
    {
        var envFiles = Directory.EnumerateFiles(searchPath, ".env*", SearchOption.AllDirectories)
            .Where(f => !f.Contains("bin") && !f.Contains("obj"))
            .ToList();

        foreach (var filePath in envFiles)
        {
            try
            {
                var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);

                foreach (var line in lines)
                {
                    // Skip comments and empty lines
                    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
                        continue;

                    // Look for connection string patterns
                    var match = Regex.Match(line, @"^([A-Z_][A-Z0-9_]*)\s*=\s*(.+)$", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var key = match.Groups[1].Value;
                        var value = match.Groups[2].Value.Trim().Trim('"', '\'');

                        // Only include if it looks like a connection string or has "CONNECTION" in the name
                        if (IsLikelyConnectionString(key, value))
                        {
                            var environment = ExtractEnvironmentFromFileName(Path.GetFileName(filePath));

                            discoveredConnections.Add(new DiscoveredConnectionString
                            {
                                Name = key,
                                ConnectionString = value,
                                Source = Path.GetFileName(filePath),
                                SourceType = "env",
                                FilePath = filePath,
                                Environment = environment
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error parsing {filePath}: {ex.Message}");
            }
        }
    }

    private static string? ExtractEnvironmentFromFileName(string fileName)
    {
        // appsettings.Development.json -> Development
        // .env.production -> production
        var match = Regex.Match(fileName, @"appsettings\.(.+)\.json", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        match = Regex.Match(fileName, @"\.env\.(.+)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        return null;
    }

    private static bool IsLikelyConnectionString(string key, string value)
    {
        // Check if key contains connection-related keywords
        var connectionKeywords = new[] { "CONNECTION", "DATABASE", "SQL", "SERVER", "DB" };
        if (connectionKeywords.Any(keyword => key.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // Check if value contains connection string patterns
        var connectionPatterns = new[]
        {
            "Server=",
            "Data Source=",
            "Initial Catalog=",
            "Database=",
            "User ID=",
            "Password=",
            "Integrated Security=",
            "TrustServerCertificate="
        };

        return connectionPatterns.Any(pattern => value.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }
}
