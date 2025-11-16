using Microsoft.Data.SqlClient;
using SqlServerMcp.Configuration;
using SqlServerMcp.Models;
using System.Data;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace SqlServerMcp.Services;

public class SqlService : ISqlService
{
    private readonly SqlServerConfig _config;

    public SqlService(SqlServerConfig config)
    {
        _config = config;
    }

    private SqlConnection CreateConnection()
    {
        return new SqlConnection(_config.ConnectionString);
    }

    private void ValidateReadOnly(string sql)
    {
        if (!_config.ReadOnly) return;

        var trimmed = sql.Trim().ToUpperInvariant();
        var forbidden = new[] { "INSERT", "UPDATE", "DELETE", "DROP", "CREATE", "ALTER", "TRUNCATE", "EXEC", "EXECUTE" };

        foreach (var keyword in forbidden)
        {
            if (trimmed.StartsWith(keyword))
            {
                throw new InvalidOperationException(
                    $"Server is in read-only mode. '{keyword}' statements are not allowed.");
            }
        }
    }

    public async Task<QueryResult> ExecuteQueryAsync(string sql, int timeout, int maxRows)
    {
        ValidateReadOnly(sql);

        var stopwatch = Stopwatch.StartNew();
        using var connection = CreateConnection();
        await connection.OpenAsync();

        using var command = new SqlCommand(sql, connection);
        command.CommandTimeout = timeout;

        using var reader = await command.ExecuteReaderAsync();

        var columnNames = new string[reader.FieldCount];
        for (int i = 0; i < reader.FieldCount; i++)
        {
            columnNames[i] = reader.GetName(i);
        }

        var rows = new List<object[]>();
        while (await reader.ReadAsync() && rows.Count < maxRows)
        {
            var row = new object[reader.FieldCount];
            reader.GetValues(row);
            rows.Add(row);
        }

        var wasTruncated = await reader.ReadAsync(); // Check if there are more rows
        stopwatch.Stop();

        return new QueryResult
        {
            ColumnNames = columnNames,
            Rows = rows,
            ExecutionTimeSeconds = stopwatch.Elapsed.TotalSeconds,
            WasTruncated = wasTruncated
        };
    }

    public async Task<QueryResult> ExecuteParameterizedQueryAsync(string sql, Dictionary<string, object> parameters, int timeout, int maxRows)
    {
        ValidateReadOnly(sql);

        var stopwatch = Stopwatch.StartNew();
        using var connection = CreateConnection();
        await connection.OpenAsync();

        using var command = new SqlCommand(sql, connection);
        command.CommandTimeout = timeout;

        foreach (var param in parameters)
        {
            var paramName = param.Key.StartsWith("@") ? param.Key : $"@{param.Key}";
            command.Parameters.AddWithValue(paramName, param.Value ?? DBNull.Value);
        }

        using var reader = await command.ExecuteReaderAsync();

        var columnNames = new string[reader.FieldCount];
        for (int i = 0; i < reader.FieldCount; i++)
        {
            columnNames[i] = reader.GetName(i);
        }

        var rows = new List<object[]>();
        while (await reader.ReadAsync() && rows.Count < maxRows)
        {
            var row = new object[reader.FieldCount];
            reader.GetValues(row);
            rows.Add(row);
        }

        var wasTruncated = await reader.ReadAsync();
        stopwatch.Stop();

        return new QueryResult
        {
            ColumnNames = columnNames,
            Rows = rows,
            ExecutionTimeSeconds = stopwatch.Elapsed.TotalSeconds,
            WasTruncated = wasTruncated
        };
    }

    public async Task<List<TableInfo>> GetTablesAsync(string? schemaPattern, string? tablePattern, bool includeSystemTables)
    {
        var sql = @"
            SELECT
                s.name AS SchemaName,
                t.name AS TableName,
                t.type_desc AS TableType,
                SUM(p.rows) AS RowCount,
                CAST(SUM(a.total_pages) * 8 / 1024.0 AS DECIMAL(10,2)) AS SizeMB,
                t.create_date AS CreatedDate,
                t.modify_date AS ModifiedDate
            FROM sys.tables t
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            LEFT JOIN sys.partitions p ON t.object_id = p.object_id AND p.index_id IN (0, 1)
            LEFT JOIN sys.allocation_units a ON p.partition_id = a.container_id
            WHERE (@includeSystem = 1 OR t.is_ms_shipped = 0)
                AND (@schemaPattern IS NULL OR s.name LIKE @schemaPattern)
                AND (@tablePattern IS NULL OR t.name LIKE @tablePattern)
            GROUP BY s.name, t.name, t.type_desc, t.create_date, t.modify_date
            ORDER BY s.name, t.name";

        using var connection = CreateConnection();
        await connection.OpenAsync();

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@includeSystem", includeSystemTables);
        command.Parameters.AddWithValue("@schemaPattern", (object?)schemaPattern ?? DBNull.Value);
        command.Parameters.AddWithValue("@tablePattern", (object?)tablePattern ?? DBNull.Value);

        var tables = new List<TableInfo>();
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            tables.Add(new TableInfo
            {
                SchemaName = reader.GetString(0),
                TableName = reader.GetString(1),
                TableType = reader.GetString(2),
                RowCount = reader.IsDBNull(3) ? 0 : reader.GetInt64(3),
                SizeMB = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4),
                CreatedDate = reader.GetDateTime(5),
                ModifiedDate = reader.GetDateTime(6)
            });
        }

        return tables;
    }

    public async Task<TableDetails> GetTableDetailsAsync(string tableName, bool includeIndexes, bool includeConstraints)
    {
        var parts = tableName.Split('.');
        var schema = parts.Length > 1 ? parts[0] : "dbo";
        var table = parts.Length > 1 ? parts[1] : parts[0];

        using var connection = CreateConnection();
        await connection.OpenAsync();

        // Get columns
        var columnsSql = @"
            SELECT
                c.name AS ColumnName,
                TYPE_NAME(c.user_type_id) AS DataType,
                c.max_length AS MaxLength,
                c.precision AS Precision,
                c.scale AS Scale,
                c.is_nullable AS IsNullable,
                dc.definition AS DefaultValue,
                c.is_identity AS IsIdentity,
                cc.definition AS ComputedExpression
            FROM sys.columns c
            LEFT JOIN sys.default_constraints dc ON c.default_object_id = dc.object_id
            LEFT JOIN sys.computed_columns cc ON c.object_id = cc.object_id AND c.column_id = cc.column_id
            WHERE c.object_id = OBJECT_ID(@tableName)
            ORDER BY c.column_id";

        var columns = new List<ColumnInfo>();
        using (var command = new SqlCommand(columnsSql, connection))
        {
            command.Parameters.AddWithValue("@tableName", $"{schema}.{table}");
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                columns.Add(new ColumnInfo
                {
                    ColumnName = reader.GetString(0),
                    DataType = reader.GetString(1),
                    MaxLength = reader.GetInt16(2),
                    Precision = reader.GetByte(3),
                    Scale = reader.GetByte(4),
                    IsNullable = reader.GetBoolean(5),
                    DefaultValue = reader.IsDBNull(6) ? null : reader.GetString(6),
                    IsIdentity = reader.GetBoolean(7),
                    ComputedExpression = reader.IsDBNull(8) ? null : reader.GetString(8)
                });
            }
        }

        var indexes = new List<IndexInfo>();
        if (includeIndexes)
        {
            var indexSql = @"
                SELECT
                    i.name AS IndexName,
                    i.type_desc AS IndexType,
                    STRING_AGG(c.name, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS Columns,
                    i.is_unique AS IsUnique,
                    i.is_primary_key AS IsPrimaryKey,
                    i.filter_definition AS FilterDefinition
                FROM sys.indexes i
                INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                WHERE i.object_id = OBJECT_ID(@tableName)
                    AND ic.is_included_column = 0
                GROUP BY i.name, i.type_desc, i.is_unique, i.is_primary_key, i.filter_definition
                ORDER BY i.is_primary_key DESC, i.name";

            using var command = new SqlCommand(indexSql, connection);
            command.Parameters.AddWithValue("@tableName", $"{schema}.{table}");
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                indexes.Add(new IndexInfo
                {
                    IndexName = reader.GetString(0),
                    IndexType = reader.GetString(1),
                    Columns = reader.GetString(2),
                    IsUnique = reader.GetBoolean(3),
                    IsPrimaryKey = reader.GetBoolean(4),
                    FilterDefinition = reader.IsDBNull(5) ? null : reader.GetString(5)
                });
            }
        }

        var constraints = new List<ConstraintInfo>();
        if (includeConstraints)
        {
            var constraintSql = @"
                SELECT
                    con.name AS ConstraintName,
                    con.type_desc AS ConstraintType,
                    OBJECT_NAME(fk.referenced_object_id) AS ReferencedTable
                FROM sys.objects con
                LEFT JOIN sys.foreign_keys fk ON con.object_id = fk.object_id
                WHERE con.parent_object_id = OBJECT_ID(@tableName)
                    AND con.type IN ('PK', 'UQ', 'F', 'C', 'D')
                ORDER BY con.type_desc, con.name";

            using var command = new SqlCommand(constraintSql, connection);
            command.Parameters.AddWithValue("@tableName", $"{schema}.{table}");
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                constraints.Add(new ConstraintInfo
                {
                    ConstraintName = reader.GetString(0),
                    ConstraintType = reader.GetString(1),
                    Definition = "", // Would need additional query to get full definition
                    ReferencedTable = reader.IsDBNull(2) ? null : reader.GetString(2)
                });
            }
        }

        return new TableDetails
        {
            SchemaName = schema,
            TableName = table,
            Columns = columns,
            Indexes = indexes,
            Constraints = constraints
        };
    }

    public async Task<List<ViewInfo>> GetViewsAsync(string? schemaPattern, string? viewPattern)
    {
        var sql = @"
            SELECT
                s.name AS SchemaName,
                v.name AS ViewName,
                m.definition AS Definition
            FROM sys.views v
            INNER JOIN sys.schemas s ON v.schema_id = s.schema_id
            LEFT JOIN sys.sql_modules m ON v.object_id = m.object_id
            WHERE v.is_ms_shipped = 0
                AND (@schemaPattern IS NULL OR s.name LIKE @schemaPattern)
                AND (@viewPattern IS NULL OR v.name LIKE @viewPattern)
            ORDER BY s.name, v.name";

        using var connection = CreateConnection();
        await connection.OpenAsync();

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@schemaPattern", (object?)schemaPattern ?? DBNull.Value);
        command.Parameters.AddWithValue("@viewPattern", (object?)viewPattern ?? DBNull.Value);

        var views = new List<ViewInfo>();
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            views.Add(new ViewInfo
            {
                SchemaName = reader.GetString(0),
                ViewName = reader.GetString(1),
                Definition = reader.IsDBNull(2) ? null : reader.GetString(2)
            });
        }

        return views;
    }

    public async Task<List<StoredProcedureInfo>> GetStoredProceduresAsync(string? schemaPattern, string? procedurePattern, bool includeParameters)
    {
        var sql = @"
            SELECT
                s.name AS SchemaName,
                p.name AS ProcedureName,
                p.create_date AS CreatedDate,
                p.modify_date AS ModifiedDate
            FROM sys.procedures p
            INNER JOIN sys.schemas s ON p.schema_id = s.schema_id
            WHERE p.is_ms_shipped = 0
                AND (@schemaPattern IS NULL OR s.name LIKE @schemaPattern)
                AND (@procedurePattern IS NULL OR p.name LIKE @procedurePattern)
            ORDER BY s.name, p.name";

        using var connection = CreateConnection();
        await connection.OpenAsync();

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@schemaPattern", (object?)schemaPattern ?? DBNull.Value);
        command.Parameters.AddWithValue("@procedurePattern", (object?)procedurePattern ?? DBNull.Value);

        var procedures = new List<StoredProcedureInfo>();
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var proc = new StoredProcedureInfo
            {
                SchemaName = reader.GetString(0),
                ProcedureName = reader.GetString(1),
                CreatedDate = reader.GetDateTime(2),
                ModifiedDate = reader.GetDateTime(3)
            };

            if (includeParameters)
            {
                proc.Parameters.AddRange(await GetProcedureParametersAsync($"{proc.SchemaName}.{proc.ProcedureName}"));
            }

            procedures.Add(proc);
        }

        return procedures;
    }

    public async Task<List<ForeignKeyInfo>> GetForeignKeysAsync(string? tableName, string direction)
    {
        var sql = direction.ToLowerInvariant() switch
        {
            "incoming" => GetIncomingForeignKeysSql(),
            "both" => GetBothForeignKeysSql(),
            _ => GetOutgoingForeignKeysSql()
        };

        using var connection = CreateConnection();
        await connection.OpenAsync();

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@tableName", (object?)tableName ?? DBNull.Value);

        var foreignKeys = new List<ForeignKeyInfo>();
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            foreignKeys.Add(new ForeignKeyInfo
            {
                ForeignKeyName = reader.GetString(0),
                ParentTable = reader.GetString(1),
                ParentColumns = reader.GetString(2),
                ReferencedTable = reader.GetString(3),
                ReferencedColumns = reader.GetString(4),
                UpdateRule = reader.GetString(5),
                DeleteRule = reader.GetString(6)
            });
        }

        return foreignKeys;
    }

    private string GetOutgoingForeignKeysSql() => @"
        SELECT
            fk.name AS ForeignKeyName,
            OBJECT_SCHEMA_NAME(fk.parent_object_id) + '.' + OBJECT_NAME(fk.parent_object_id) AS ParentTable,
            STRING_AGG(pc.name, ', ') AS ParentColumns,
            OBJECT_SCHEMA_NAME(fk.referenced_object_id) + '.' + OBJECT_NAME(fk.referenced_object_id) AS ReferencedTable,
            STRING_AGG(rc.name, ', ') AS ReferencedColumns,
            fk.update_referential_action_desc AS UpdateRule,
            fk.delete_referential_action_desc AS DeleteRule
        FROM sys.foreign_keys fk
        INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
        INNER JOIN sys.columns pc ON fkc.parent_object_id = pc.object_id AND fkc.parent_column_id = pc.column_id
        INNER JOIN sys.columns rc ON fkc.referenced_object_id = rc.object_id AND fkc.referenced_column_id = rc.column_id
        WHERE (@tableName IS NULL OR OBJECT_SCHEMA_NAME(fk.parent_object_id) + '.' + OBJECT_NAME(fk.parent_object_id) = @tableName)
        GROUP BY fk.name, fk.parent_object_id, fk.referenced_object_id, fk.update_referential_action_desc, fk.delete_referential_action_desc";

    private string GetIncomingForeignKeysSql() => @"
        SELECT
            fk.name AS ForeignKeyName,
            OBJECT_SCHEMA_NAME(fk.parent_object_id) + '.' + OBJECT_NAME(fk.parent_object_id) AS ParentTable,
            STRING_AGG(pc.name, ', ') AS ParentColumns,
            OBJECT_SCHEMA_NAME(fk.referenced_object_id) + '.' + OBJECT_NAME(fk.referenced_object_id) AS ReferencedTable,
            STRING_AGG(rc.name, ', ') AS ReferencedColumns,
            fk.update_referential_action_desc AS UpdateRule,
            fk.delete_referential_action_desc AS DeleteRule
        FROM sys.foreign_keys fk
        INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
        INNER JOIN sys.columns pc ON fkc.parent_object_id = pc.object_id AND fkc.parent_column_id = pc.column_id
        INNER JOIN sys.columns rc ON fkc.referenced_object_id = rc.object_id AND fkc.referenced_column_id = rc.column_id
        WHERE (@tableName IS NULL OR OBJECT_SCHEMA_NAME(fk.referenced_object_id) + '.' + OBJECT_NAME(fk.referenced_object_id) = @tableName)
        GROUP BY fk.name, fk.parent_object_id, fk.referenced_object_id, fk.update_referential_action_desc, fk.delete_referential_action_desc";

    private string GetBothForeignKeysSql() => $"{GetOutgoingForeignKeysSql()} UNION {GetIncomingForeignKeysSql()}";

    public async Task<List<IndexInfo>> GetIndexesAsync(string? tableName, bool includeStats)
    {
        var sql = @"
            SELECT
                i.name AS IndexName,
                OBJECT_SCHEMA_NAME(i.object_id) + '.' + OBJECT_NAME(i.object_id) AS TableName,
                i.type_desc AS IndexType,
                STRING_AGG(c.name, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS Columns,
                i.is_unique AS IsUnique,
                i.is_primary_key AS IsPrimaryKey,
                i.filter_definition AS FilterDefinition
            FROM sys.indexes i
            INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
            INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
            WHERE (@tableName IS NULL OR OBJECT_SCHEMA_NAME(i.object_id) + '.' + OBJECT_NAME(i.object_id) = @tableName)
                AND ic.is_included_column = 0
            GROUP BY i.object_id, i.name, i.type_desc, i.is_unique, i.is_primary_key, i.filter_definition
            ORDER BY OBJECT_NAME(i.object_id), i.is_primary_key DESC, i.name";

        using var connection = CreateConnection();
        await connection.OpenAsync();

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@tableName", (object?)tableName ?? DBNull.Value);

        var indexes = new List<IndexInfo>();
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            indexes.Add(new IndexInfo
            {
                IndexName = reader.GetString(0),
                IndexType = reader.GetString(2),
                Columns = reader.GetString(3),
                IsUnique = reader.GetBoolean(4),
                IsPrimaryKey = reader.GetBoolean(5),
                FilterDefinition = reader.IsDBNull(6) ? null : reader.GetString(6)
            });
        }

        return indexes;
    }

    public async Task<List<ColumnReference>> FindColumnsByTypeAsync(string dataType, string? schemaPattern, string? tablePattern)
    {
        var sql = @"
            SELECT
                OBJECT_SCHEMA_NAME(c.object_id) AS SchemaName,
                OBJECT_NAME(c.object_id) AS TableName,
                c.name AS ColumnName,
                TYPE_NAME(c.user_type_id) AS DataType
            FROM sys.columns c
            INNER JOIN sys.tables t ON c.object_id = t.object_id
            WHERE TYPE_NAME(c.user_type_id) = @dataType
                AND (@schemaPattern IS NULL OR OBJECT_SCHEMA_NAME(c.object_id) LIKE @schemaPattern)
                AND (@tablePattern IS NULL OR OBJECT_NAME(c.object_id) LIKE @tablePattern)
            ORDER BY OBJECT_SCHEMA_NAME(c.object_id), OBJECT_NAME(c.object_id), c.name";

        using var connection = CreateConnection();
        await connection.OpenAsync();

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@dataType", dataType);
        command.Parameters.AddWithValue("@schemaPattern", (object?)schemaPattern ?? DBNull.Value);
        command.Parameters.AddWithValue("@tablePattern", (object?)tablePattern ?? DBNull.Value);

        var columns = new List<ColumnReference>();
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            columns.Add(new ColumnReference
            {
                SchemaName = reader.GetString(0),
                TableName = reader.GetString(1),
                ColumnName = reader.GetString(2),
                DataType = reader.GetString(3)
            });
        }

        return columns;
    }

    public async Task<List<DependencyInfo>> GetDependenciesAsync(string objectName, string dependencyType)
    {
        var sql = dependencyType.ToLowerInvariant() switch
        {
            "referenced" => GetReferencedDependenciesSql(),
            "referencing" => GetReferencingDependenciesSql(),
            _ => GetBothDependenciesSql()
        };

        using var connection = CreateConnection();
        await connection.OpenAsync();

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@objectName", objectName);

        var dependencies = new List<DependencyInfo>();
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            dependencies.Add(new DependencyInfo
            {
                ObjectName = reader.GetString(0),
                ObjectType = reader.GetString(1),
                DependencyType = reader.GetString(2)
            });
        }

        return dependencies;
    }

    private string GetReferencedDependenciesSql() => @"
        SELECT
            OBJECT_SCHEMA_NAME(referenced_id) + '.' + OBJECT_NAME(referenced_id) AS ObjectName,
            o.type_desc AS ObjectType,
            'Referenced' AS DependencyType
        FROM sys.sql_expression_dependencies d
        INNER JOIN sys.objects o ON d.referenced_id = o.object_id
        WHERE OBJECT_SCHEMA_NAME(referencing_id) + '.' + OBJECT_NAME(referencing_id) = @objectName";

    private string GetReferencingDependenciesSql() => @"
        SELECT
            OBJECT_SCHEMA_NAME(referencing_id) + '.' + OBJECT_NAME(referencing_id) AS ObjectName,
            o.type_desc AS ObjectType,
            'Referencing' AS DependencyType
        FROM sys.sql_expression_dependencies d
        INNER JOIN sys.objects o ON d.referencing_id = o.object_id
        WHERE OBJECT_SCHEMA_NAME(referenced_id) + '.' + OBJECT_NAME(referenced_id) = @objectName";

    private string GetBothDependenciesSql() => $"{GetReferencedDependenciesSql()} UNION {GetReferencingDependenciesSql()}";

    public async Task<DatabaseInfo> GetDatabaseInfoAsync()
    {
        var sql = @"
            SELECT
                DB_NAME() AS DatabaseName,
                CAST(compatibility_level AS VARCHAR) AS CompatibilityLevel,
                collation_name AS Collation,
                recovery_model_desc AS RecoveryModel,
                CAST(SUM(CASE WHEN type = 0 THEN size END) * 8 / 1024.0 AS DECIMAL(10,2)) AS DatabaseSizeMB,
                CAST(SUM(CASE WHEN type = 1 THEN size END) * 8 / 1024.0 AS DECIMAL(10,2)) AS LogSizeMB,
                create_date AS CreatedDate,
                SUSER_SNAME(owner_sid) AS Owner
            FROM sys.databases
            CROSS JOIN sys.master_files
            WHERE database_id = DB_ID()
            GROUP BY compatibility_level, collation_name, recovery_model_desc, create_date, owner_sid";

        using var connection = CreateConnection();
        await connection.OpenAsync();

        using var command = new SqlCommand(sql, connection);
        using var reader = await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return new DatabaseInfo
            {
                DatabaseName = reader.GetString(0),
                CompatibilityLevel = reader.GetString(1),
                Collation = reader.GetString(2),
                RecoveryModel = reader.GetString(3),
                DatabaseSizeMB = reader.GetDecimal(4),
                LogSizeMB = reader.GetDecimal(5),
                CreatedDate = reader.GetDateTime(6),
                Owner = reader.GetString(7)
            };
        }

        throw new InvalidOperationException("Could not retrieve database information");
    }

    public async Task<List<ConnectionInfo>> GetConnectionsAsync()
    {
        var sql = @"
            SELECT
                session_id AS SessionId,
                login_name AS LoginName,
                host_name AS HostName,
                program_name AS ProgramName,
                login_time AS LoginTime
            FROM sys.dm_exec_sessions
            WHERE database_id = DB_ID()
            ORDER BY login_time DESC";

        using var connection = CreateConnection();
        await connection.OpenAsync();

        using var command = new SqlCommand(sql, connection);
        var connections = new List<ConnectionInfo>();

        try
        {
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                connections.Add(new ConnectionInfo
                {
                    SessionId = reader.GetInt16(0),
                    LoginName = reader.GetString(1),
                    HostName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    ProgramName = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    LoginTime = reader.IsDBNull(4) ? null : reader.GetDateTime(4)
                });
            }
        }
        catch (SqlException ex)
        {
            throw new InvalidOperationException(
                $"Failed to retrieve connections. This may require VIEW SERVER STATE permission. Error: {ex.Message}",
                ex);
        }

        return connections;
    }

    public async Task<List<QueryStatInfo>> GetQueryStatsAsync(string metric, int topN)
    {
        var orderBy = metric.ToLowerInvariant() switch
        {
            "execution_count" => "execution_count DESC",
            "cpu_time" => "total_worker_time DESC",
            "duration" => "total_elapsed_time DESC",
            "logical_reads" => "total_logical_reads DESC",
            _ => "execution_count DESC"
        };

        var sql = $@"
            SELECT TOP {topN}
                SUBSTRING(qt.text, (qs.statement_start_offset/2) + 1,
                    ((CASE qs.statement_end_offset
                        WHEN -1 THEN DATALENGTH(qt.text)
                        ELSE qs.statement_end_offset
                    END - qs.statement_start_offset)/2) + 1) AS QueryText,
                qs.execution_count AS ExecutionCount,
                qs.total_worker_time / 1000 AS TotalCpuTimeMs,
                (qs.total_worker_time / qs.execution_count) / 1000 AS AvgCpuTimeMs,
                qs.total_elapsed_time / 1000 AS TotalDurationMs,
                (qs.total_elapsed_time / qs.execution_count) / 1000 AS AvgDurationMs
            FROM sys.dm_exec_query_stats qs
            CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) qt
            ORDER BY {orderBy}";

        using var connection = CreateConnection();
        await connection.OpenAsync();

        using var command = new SqlCommand(sql, connection);
        var stats = new List<QueryStatInfo>();

        try
        {
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                stats.Add(new QueryStatInfo
                {
                    QueryText = reader.GetString(0).Trim(),
                    ExecutionCount = reader.GetInt64(1),
                    TotalCpuTimeMs = reader.GetInt64(2),
                    AvgCpuTimeMs = reader.GetInt64(3),
                    TotalDurationMs = reader.GetInt64(4),
                    AvgDurationMs = reader.GetInt64(5)
                });
            }
        }
        catch (SqlException ex)
        {
            throw new InvalidOperationException(
                $"Failed to retrieve query statistics. This may require VIEW SERVER STATE permission. Error: {ex.Message}",
                ex);
        }

        return stats;
    }

    public async Task<List<ConstraintViolation>> CheckConstraintsAsync(string? tableName, string constraintType)
    {
        // Note: This is a simplified implementation
        // A full implementation would need to check each constraint type differently
        var violations = new List<ConstraintViolation>();

        // For foreign key constraints, we can check for orphaned records
        if (constraintType.ToLowerInvariant() is "foreign_key" or "all")
        {
            var sql = @"
                SELECT
                    OBJECT_SCHEMA_NAME(fk.parent_object_id) + '.' + OBJECT_NAME(fk.parent_object_id) AS TableName,
                    fk.name AS ConstraintName,
                    'FOREIGN_KEY' AS ConstraintType,
                    'Potential orphaned records exist' AS ViolationDetails
                FROM sys.foreign_keys fk
                WHERE @tableName IS NULL OR OBJECT_SCHEMA_NAME(fk.parent_object_id) + '.' + OBJECT_NAME(fk.parent_object_id) = @tableName";

            using var connection = CreateConnection();
            await connection.OpenAsync();

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@tableName", (object?)tableName ?? DBNull.Value);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                violations.Add(new ConstraintViolation
                {
                    TableName = reader.GetString(0),
                    ConstraintName = reader.GetString(1),
                    ConstraintType = reader.GetString(2),
                    ViolationDetails = reader.GetString(3)
                });
            }
        }

        return violations;
    }

    public async Task<QueryResult> FindDuplicatesAsync(string tableName, string[] columns, int limit)
    {
        var parts = tableName.Split('.');
        var schema = parts.Length > 1 ? parts[0] : "dbo";
        var table = parts.Length > 1 ? parts[1] : parts[0];

        var columnList = string.Join(", ", columns);
        var sql = $@"
            SELECT TOP {limit}
                {columnList},
                COUNT(*) AS DuplicateCount
            FROM [{schema}].[{table}]
            GROUP BY {columnList}
            HAVING COUNT(*) > 1
            ORDER BY COUNT(*) DESC";

        return await ExecuteQueryAsync(sql, _config.DefaultTimeout, limit);
    }

    public async Task<StoredProcedureResult> ExecuteStoredProcedureAsync(string procedureName, Dictionary<string, object>? parameters, int timeout, bool returnResults)
    {
        if (_config.ReadOnly && !_config.AllowProcedureExecution)
        {
            throw new InvalidOperationException("Stored procedure execution is disabled in read-only mode.");
        }

        if (_config.AllowProcedureExecution && _config.AllowedProcedures.Any())
        {
            var isAllowed = _config.AllowedProcedures.Any(pattern =>
            {
                if (pattern.EndsWith(".*"))
                {
                    var schemaPrefix = pattern[..^2];
                    return procedureName.StartsWith(schemaPrefix, StringComparison.OrdinalIgnoreCase);
                }
                return procedureName.Equals(pattern, StringComparison.OrdinalIgnoreCase);
            });

            if (!isAllowed)
            {
                throw new InvalidOperationException(
                    $"Procedure '{procedureName}' is not in the allowed list.");
            }
        }

        var stopwatch = Stopwatch.StartNew();
        using var connection = CreateConnection();
        await connection.OpenAsync();

        using var command = new SqlCommand(procedureName, connection);
        command.CommandType = CommandType.StoredProcedure;
        command.CommandTimeout = timeout;

        // Add parameters
        if (parameters != null)
        {
            foreach (var param in parameters)
            {
                var paramName = param.Key.StartsWith("@") ? param.Key : $"@{param.Key}";
                command.Parameters.AddWithValue(paramName, param.Value ?? DBNull.Value);
            }
        }

        // Add return value parameter
        var returnValue = command.Parameters.Add("@RETURN_VALUE", SqlDbType.Int);
        returnValue.Direction = ParameterDirection.ReturnValue;

        var resultSets = new List<QueryResult>();
        var messages = new List<string>();

        // Capture info messages
        connection.InfoMessage += (sender, e) => messages.Add(e.Message);

        if (returnResults)
        {
            using var reader = await command.ExecuteReaderAsync();
            do
            {
                var columnNames = new string[reader.FieldCount];
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    columnNames[i] = reader.GetName(i);
                }

                var rows = new List<object[]>();
                while (await reader.ReadAsync())
                {
                    var row = new object[reader.FieldCount];
                    reader.GetValues(row);
                    rows.Add(row);
                }

                if (columnNames.Length > 0)
                {
                    resultSets.Add(new QueryResult
                    {
                        ColumnNames = columnNames,
                        Rows = rows,
                        ExecutionTimeSeconds = 0,
                        WasTruncated = false
                    });
                }
            }
            while (await reader.NextResultAsync());
        }
        else
        {
            await command.ExecuteNonQueryAsync();
        }

        // Collect output parameters
        var outputParameters = new Dictionary<string, object?>();
        foreach (SqlParameter param in command.Parameters)
        {
            if (param.Direction == ParameterDirection.Output ||
                param.Direction == ParameterDirection.InputOutput)
            {
                outputParameters[param.ParameterName] = param.Value == DBNull.Value ? null : param.Value;
            }
        }

        stopwatch.Stop();

        return new StoredProcedureResult
        {
            ResultSets = resultSets,
            OutputParameters = outputParameters,
            ReturnValue = returnValue.Value == DBNull.Value ? null : (int?)returnValue.Value,
            ExecutionTimeSeconds = stopwatch.Elapsed.TotalSeconds,
            Messages = messages
        };
    }

    public async Task<string> GetProcedureDefinitionAsync(string procedureName)
    {
        var sql = "SELECT OBJECT_DEFINITION(OBJECT_ID(@procedureName))";

        using var connection = CreateConnection();
        await connection.OpenAsync();

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@procedureName", procedureName);

        var definition = await command.ExecuteScalarAsync();
        return definition?.ToString() ?? "-- Definition not available";
    }

    public async Task<List<ParameterInfo>> GetProcedureParametersAsync(string procedureName)
    {
        var sql = @"
            SELECT
                p.name AS ParameterName,
                TYPE_NAME(p.user_type_id) AS DataType,
                p.max_length AS MaxLength,
                p.precision AS Precision,
                p.scale AS Scale,
                CASE p.is_output WHEN 0 THEN 1 WHEN 1 THEN 2 END AS Direction,
                p.has_default_value AS HasDefault,
                p.default_value AS DefaultValue,
                p.is_nullable AS IsNullable
            FROM sys.parameters p
            WHERE p.object_id = OBJECT_ID(@procedureName)
            ORDER BY p.parameter_id";

        using var connection = CreateConnection();
        await connection.OpenAsync();

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@procedureName", procedureName);

        var parameters = new List<ParameterInfo>();
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            parameters.Add(new ParameterInfo
            {
                ParameterName = reader.GetString(0),
                DataType = reader.GetString(1),
                MaxLength = reader.GetInt16(2),
                Precision = reader.GetByte(3),
                Scale = reader.GetByte(4),
                Direction = (ParameterDirection)reader.GetInt32(5),
                HasDefault = reader.GetBoolean(6),
                DefaultValue = reader.IsDBNull(7) ? null : reader.GetString(7),
                IsNullable = reader.GetBoolean(8)
            });
        }

        return parameters;
    }

    public async Task<List<FunctionInfo>> GetFunctionsAsync(string? schemaPattern, string? functionPattern, string? functionType)
    {
        var typeFilter = functionType?.ToLowerInvariant() switch
        {
            "scalar" => "AND o.type = 'FN'",
            "inline_table" => "AND o.type = 'IF'",
            "multi_table" => "AND o.type = 'TF'",
            _ => "AND o.type IN ('FN', 'IF', 'TF')"
        };

        var sql = $@"
            SELECT
                s.name AS SchemaName,
                o.name AS FunctionName,
                CASE o.type
                    WHEN 'FN' THEN 'Scalar'
                    WHEN 'IF' THEN 'InlineTable'
                    WHEN 'TF' THEN 'MultiStatementTable'
                END AS FunctionType,
                o.create_date AS CreatedDate,
                o.modify_date AS ModifiedDate
            FROM sys.objects o
            INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
            WHERE o.is_ms_shipped = 0
                {typeFilter}
                AND (@schemaPattern IS NULL OR s.name LIKE @schemaPattern)
                AND (@functionPattern IS NULL OR o.name LIKE @functionPattern)
            ORDER BY s.name, o.name";

        using var connection = CreateConnection();
        await connection.OpenAsync();

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@schemaPattern", (object?)schemaPattern ?? DBNull.Value);
        command.Parameters.AddWithValue("@functionPattern", (object?)functionPattern ?? DBNull.Value);

        var functions = new List<FunctionInfo>();
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            functions.Add(new FunctionInfo
            {
                SchemaName = reader.GetString(0),
                FunctionName = reader.GetString(1),
                FunctionType = reader.GetString(2),
                CreatedDate = reader.GetDateTime(3),
                ModifiedDate = reader.GetDateTime(4)
            });
        }

        return functions;
    }

    public async Task<string> ExecuteScalarFunctionAsync(string functionName, object[]? parameters)
    {
        if (!_config.AllowFunctionExecution)
        {
            throw new InvalidOperationException("Function execution is disabled.");
        }

        var paramList = parameters != null && parameters.Length > 0
            ? string.Join(", ", parameters.Select((p, i) => $"@p{i}"))
            : "";

        var sql = $"SELECT {functionName}({paramList})";

        using var connection = CreateConnection();
        await connection.OpenAsync();

        using var command = new SqlCommand(sql, connection);

        if (parameters != null)
        {
            for (int i = 0; i < parameters.Length; i++)
            {
                command.Parameters.AddWithValue($"@p{i}", parameters[i] ?? DBNull.Value);
            }
        }

        var result = await command.ExecuteScalarAsync();
        return result?.ToString() ?? "NULL";
    }

    public async Task<QueryResult> ExecuteTableFunctionAsync(string functionName, object[]? parameters, int maxRows)
    {
        if (!_config.AllowFunctionExecution)
        {
            throw new InvalidOperationException("Function execution is disabled.");
        }

        var paramList = parameters != null && parameters.Length > 0
            ? string.Join(", ", parameters.Select((p, i) => $"@p{i}"))
            : "";

        var sql = $"SELECT * FROM {functionName}({paramList})";

        using var connection = CreateConnection();
        await connection.OpenAsync();

        using var command = new SqlCommand(sql, connection);

        if (parameters != null)
        {
            for (int i = 0; i < parameters.Length; i++)
            {
                command.Parameters.AddWithValue($"@p{i}", parameters[i] ?? DBNull.Value);
            }
        }

        var stopwatch = Stopwatch.StartNew();
        using var reader = await command.ExecuteReaderAsync();

        var columnNames = new string[reader.FieldCount];
        for (int i = 0; i < reader.FieldCount; i++)
        {
            columnNames[i] = reader.GetName(i);
        }

        var rows = new List<object[]>();
        while (await reader.ReadAsync() && rows.Count < maxRows)
        {
            var row = new object[reader.FieldCount];
            reader.GetValues(row);
            rows.Add(row);
        }

        var wasTruncated = await reader.ReadAsync();
        stopwatch.Stop();

        return new QueryResult
        {
            ColumnNames = columnNames,
            Rows = rows,
            ExecutionTimeSeconds = stopwatch.Elapsed.TotalSeconds,
            WasTruncated = wasTruncated
        };
    }

    public async Task<string> GetFunctionDefinitionAsync(string functionName)
    {
        var sql = "SELECT OBJECT_DEFINITION(OBJECT_ID(@functionName))";

        using var connection = CreateConnection();
        await connection.OpenAsync();

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@functionName", functionName);

        var definition = await command.ExecuteScalarAsync();
        return definition?.ToString() ?? "-- Definition not available";
    }

    public async Task<List<ParameterInfo>> GetFunctionParametersAsync(string functionName)
    {
        // Same implementation as GetProcedureParametersAsync since they use the same system views
        return await GetProcedureParametersAsync(functionName);
    }

    public async Task<List<DatabaseListInfo>> ListDatabasesAsync()
    {
        var sql = @"
            SELECT
                d.name AS DatabaseName,
                d.database_id AS DatabaseId,
                d.create_date AS CreatedDate,
                d.state_desc AS State,
                d.recovery_model_desc AS RecoveryModel,
                CAST(SUM(mf.size) * 8.0 / 1024 AS DECIMAL(10,2)) AS SizeMB
            FROM sys.databases d
            LEFT JOIN sys.master_files mf ON d.database_id = mf.database_id
            WHERE d.database_id > 4  -- Exclude system databases
            GROUP BY d.name, d.database_id, d.create_date, d.state_desc, d.recovery_model_desc
            ORDER BY d.name";

        using var connection = CreateConnection();
        await connection.OpenAsync();

        using var command = new SqlCommand(sql, connection);
        using var reader = await command.ExecuteReaderAsync();

        var databases = new List<DatabaseListInfo>();
        while (await reader.ReadAsync())
        {
            databases.Add(new DatabaseListInfo
            {
                DatabaseName = reader.GetString(0),
                DatabaseId = reader.GetInt32(1),
                CreatedDate = reader.GetDateTime(2),
                State = reader.GetString(3),
                RecoveryModel = reader.GetString(4),
                SizeMB = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5)
            });
        }

        return databases;
    }

    public async Task<string> GetCurrentDatabaseAsync()
    {
        using var connection = CreateConnection();
        await connection.OpenAsync();

        using var command = new SqlCommand("SELECT DB_NAME()", connection);
        var result = await command.ExecuteScalarAsync();
        return result?.ToString() ?? "Unknown";
    }

    public async Task SwitchDatabaseAsync(string databaseName)
    {
        // Validate database name to prevent SQL injection
        if (!Regex.IsMatch(databaseName, @"^[a-zA-Z0-9_]+$"))
        {
            throw new ArgumentException("Invalid database name. Only alphanumeric characters and underscores are allowed.");
        }

        using var connection = CreateConnection();
        await connection.OpenAsync();

        // First check if the database exists
        var checkSql = "SELECT COUNT(*) FROM sys.databases WHERE name = @databaseName";
        using var checkCommand = new SqlCommand(checkSql, connection);
        checkCommand.Parameters.AddWithValue("@databaseName", databaseName);
        var exists = (int)await checkCommand.ExecuteScalarAsync()! > 0;

        if (!exists)
        {
            throw new ArgumentException($"Database '{databaseName}' does not exist.");
        }

        // Switch database
        using var switchCommand = new SqlCommand($"USE [{databaseName}]", connection);
        await switchCommand.ExecuteNonQueryAsync();
    }
}
