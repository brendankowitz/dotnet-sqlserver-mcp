namespace SqlServerMcp.Services;

/// <summary>
/// Service for discovering connection strings from solution files
/// </summary>
public interface IConnectionStringDiscoveryService
{
    /// <summary>
    /// Discovers connection strings from appsettings.json, .env files, etc.
    /// </summary>
    /// <param name="searchPath">Root path to search for configuration files</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of discovered connection strings with metadata</returns>
    Task<List<DiscoveredConnectionString>> DiscoverConnectionStringsAsync(
        string searchPath,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a discovered connection string with its source information
/// </summary>
public record DiscoveredConnectionString
{
    public required string Name { get; init; }
    public required string ConnectionString { get; init; }
    public required string Source { get; init; }
    public required string SourceType { get; init; } // "appsettings", "env", "usersecrets"
    public required string FilePath { get; init; }
    public string? Environment { get; init; } // "Development", "Production", etc.
}
