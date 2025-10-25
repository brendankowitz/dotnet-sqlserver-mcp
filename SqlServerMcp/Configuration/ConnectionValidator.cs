using Microsoft.Data.SqlClient;

namespace SqlServerMcp.Configuration;

public static class ConnectionValidator
{
    public static async Task ValidateConnectionAsync(string connectionString)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            // Execute a simple query to verify connectivity
            using var command = new SqlCommand("SELECT @@VERSION", connection);
            var version = await command.ExecuteScalarAsync();

            Console.WriteLine($"Successfully connected to SQL Server");
            Console.WriteLine($"Server version: {version?.ToString()?.Split('\n')[0]}");
        }
        catch (SqlException ex)
        {
            throw new InvalidOperationException(
                $"Failed to connect to SQL Server: {ex.Message}\n" +
                $"Error Number: {ex.Number}\n" +
                $"Verify your connection string and ensure SQL Server is accessible.",
                ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to validate connection: {ex.Message}",
                ex);
        }
    }
}
