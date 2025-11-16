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
            using var command = new SqlCommand("SELECT @@VERSION, DB_NAME() as CurrentDatabase", connection);
            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                var version = reader.GetString(0);
                var currentDb = reader.IsDBNull(1) ? "master" : reader.GetString(1);

                Console.WriteLine($"Successfully connected to SQL Server");
                Console.WriteLine($"Server version: {version?.Split('\n')[0]}");
                Console.WriteLine($"Current database: {currentDb}");

                if (currentDb == "master")
                {
                    Console.WriteLine();
                    Console.WriteLine("⚠️  NOTE: Connected to 'master' database (default)");
                    Console.WriteLine("   Consider using 'list_databases' to see available databases");
                    Console.WriteLine("   or 'switch_database' to change to a specific database.");
                }
            }
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
