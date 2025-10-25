using SqlServerMcp.Models;
using System.Text;

namespace SqlServerMcp.Services;

public class ResultFormatter
{
    public string FormatQueryResult(QueryResult result)
    {
        if (result.Rows.Count == 0)
        {
            return $"No rows returned.\n\nExecution time: {result.ExecutionTimeSeconds:F2} seconds";
        }

        var sb = new StringBuilder();

        // Calculate column widths
        var columnWidths = new int[result.ColumnNames.Length];
        for (int i = 0; i < result.ColumnNames.Length; i++)
        {
            columnWidths[i] = result.ColumnNames[i].Length;
        }

        // Check data for max widths
        foreach (var row in result.Rows)
        {
            for (int i = 0; i < row.Length; i++)
            {
                var value = FormatValue(row[i]);
                columnWidths[i] = Math.Max(columnWidths[i], value.Length);
            }
        }

        // Build header row
        sb.Append("| ");
        for (int i = 0; i < result.ColumnNames.Length; i++)
        {
            sb.Append(result.ColumnNames[i].PadRight(columnWidths[i]));
            sb.Append(" | ");
        }
        sb.AppendLine();

        // Build separator
        sb.Append("|");
        for (int i = 0; i < result.ColumnNames.Length; i++)
        {
            sb.Append(new string('-', columnWidths[i] + 2));
            sb.Append("|");
        }
        sb.AppendLine();

        // Build data rows
        foreach (var row in result.Rows)
        {
            sb.Append("| ");
            for (int i = 0; i < row.Length; i++)
            {
                var value = FormatValue(row[i]);
                sb.Append(value.PadRight(columnWidths[i]));
                sb.Append(" | ");
            }
            sb.AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine($"Total rows: {result.RowCount}");
        if (result.WasTruncated)
        {
            sb.AppendLine("Note: Results were truncated to the maximum row limit.");
        }
        sb.AppendLine($"Execution time: {result.ExecutionTimeSeconds:F2} seconds");

        return sb.ToString();
    }

    public string FormatTableList(List<TableInfo> tables)
    {
        if (tables.Count == 0)
        {
            return "No tables found.";
        }

        var result = new QueryResult
        {
            ColumnNames = new[] { "Schema", "Table", "Type", "Rows", "Size (MB)", "Created", "Modified" },
            Rows = tables.Select(t => new object[]
            {
                t.SchemaName,
                t.TableName,
                t.TableType,
                t.RowCount,
                t.SizeMB,
                t.CreatedDate.ToString("yyyy-MM-dd HH:mm"),
                t.ModifiedDate.ToString("yyyy-MM-dd HH:mm")
            }).ToList(),
            ExecutionTimeSeconds = 0,
            WasTruncated = false
        };

        return FormatQueryResult(result);
    }

    public string FormatTableDetails(TableDetails details)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"Table: {details.SchemaName}.{details.TableName}");
        sb.AppendLine();

        // Columns
        sb.AppendLine("=== Columns ===");
        if (details.Columns.Any())
        {
            var columnResult = new QueryResult
            {
                ColumnNames = new[] { "Column", "Type", "Length", "Precision", "Scale", "Nullable", "Identity", "Default", "Computed" },
                Rows = details.Columns.Select(c => new object[]
                {
                    c.ColumnName,
                    c.DataType,
                    c.MaxLength,
                    c.Precision,
                    c.Scale,
                    c.IsNullable ? "YES" : "NO",
                    c.IsIdentity ? "YES" : "NO",
                    c.DefaultValue ?? "",
                    c.ComputedExpression ?? ""
                }).ToList(),
                ExecutionTimeSeconds = 0,
                WasTruncated = false
            };
            sb.Append(FormatQueryResult(columnResult));
        }
        else
        {
            sb.AppendLine("No columns found.");
        }

        sb.AppendLine();

        // Indexes
        if (details.Indexes.Any())
        {
            sb.AppendLine("=== Indexes ===");
            var indexResult = new QueryResult
            {
                ColumnNames = new[] { "Index", "Type", "Columns", "Unique", "PK", "Filter" },
                Rows = details.Indexes.Select(i => new object[]
                {
                    i.IndexName,
                    i.IndexType,
                    i.Columns,
                    i.IsUnique ? "YES" : "NO",
                    i.IsPrimaryKey ? "YES" : "NO",
                    i.FilterDefinition ?? ""
                }).ToList(),
                ExecutionTimeSeconds = 0,
                WasTruncated = false
            };
            sb.Append(FormatQueryResult(indexResult));
            sb.AppendLine();
        }

        // Constraints
        if (details.Constraints.Any())
        {
            sb.AppendLine("=== Constraints ===");
            var constraintResult = new QueryResult
            {
                ColumnNames = new[] { "Constraint", "Type", "Referenced Table" },
                Rows = details.Constraints.Select(c => new object[]
                {
                    c.ConstraintName,
                    c.ConstraintType,
                    c.ReferencedTable ?? ""
                }).ToList(),
                ExecutionTimeSeconds = 0,
                WasTruncated = false
            };
            sb.Append(FormatQueryResult(constraintResult));
        }

        return sb.ToString();
    }

    public string FormatStoredProcedureResult(StoredProcedureResult result)
    {
        var sb = new StringBuilder();

        // Result sets
        for (int i = 0; i < result.ResultSets.Count; i++)
        {
            sb.AppendLine($"=== Result Set {i + 1} ===");
            sb.Append(FormatQueryResult(result.ResultSets[i]));
            sb.AppendLine();
        }

        // Output parameters
        if (result.OutputParameters.Any())
        {
            sb.AppendLine("=== Output Parameters ===");
            foreach (var param in result.OutputParameters)
            {
                sb.AppendLine($"{param.Key} = {FormatValue(param.Value)}");
            }
            sb.AppendLine();
        }

        // Return value
        sb.AppendLine("=== Return Value ===");
        sb.AppendLine(result.ReturnValue?.ToString() ?? "NULL");
        sb.AppendLine();

        // Messages
        if (result.Messages.Any())
        {
            sb.AppendLine("=== Messages ===");
            foreach (var message in result.Messages)
            {
                sb.AppendLine(message);
            }
            sb.AppendLine();
        }

        sb.AppendLine($"Total execution time: {result.ExecutionTimeSeconds:F2} seconds");

        return sb.ToString();
    }

    public string FormatDatabaseInfo(DatabaseInfo info)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Database Name: {info.DatabaseName}");
        sb.AppendLine($"Compatibility Level: {info.CompatibilityLevel}");
        sb.AppendLine($"Collation: {info.Collation}");
        sb.AppendLine($"Recovery Model: {info.RecoveryModel}");
        sb.AppendLine($"Database Size: {info.DatabaseSizeMB:F2} MB");
        sb.AppendLine($"Log Size: {info.LogSizeMB:F2} MB");
        sb.AppendLine($"Created: {info.CreatedDate:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Owner: {info.Owner}");
        return sb.ToString();
    }

    private string FormatValue(object? value)
    {
        if (value == null || value == DBNull.Value)
        {
            return "NULL";
        }

        return value switch
        {
            DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss"),
            decimal dec => dec.ToString("0.##"),
            double dbl => dbl.ToString("0.##"),
            float flt => flt.ToString("0.##"),
            byte[] bytes => $"0x{BitConverter.ToString(bytes).Replace("-", "")}",
            _ => value.ToString() ?? ""
        };
    }
}
