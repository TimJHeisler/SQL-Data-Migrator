using System.Data;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace SQLDataMigrator;

public enum MigrationLogLevel
{
    Info,
    Warning,
    Error,
    Success
}

public sealed class ConnectionProfile
{
    public string Server { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string AuthMode { get; set; } = "Integrated Security";
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool TrustCertificate { get; set; }
}

public sealed class TransferRequest
{
    public ConnectionProfile Source { get; set; } = new();
    public ConnectionProfile Destination { get; set; } = new();
    public string DestinationDatabaseName { get; set; } = string.Empty;
    public bool PreserveIdentity { get; set; } = true;
    public bool ScriptFunctionsAndTvfs { get; set; } = true;
    public bool PreserveIndexes { get; set; } = true;
    public bool PreserveForeignKeys { get; set; } = true;
    public List<TableSelectionItem> Tables { get; set; } = new();
}

public sealed class TransferSummary
{
    public int SuccessCount { get; set; }
    public int FailCount { get; set; }
}

public sealed class TableTransferMetric
{
    public string Schema { get; set; } = string.Empty;
    public string Table { get; set; } = string.Empty;
    public long EstimatedRows { get; set; }
    public long RowsCopied { get; set; }
    public TimeSpan Elapsed { get; set; }
    public bool Success { get; set; }
}

public sealed class MigrationEngine
{
    private const int MaxProcessMemoryMb = 500;
    private const int MemoryGcThresholdMb = 425;

    public async Task<string> TestConnectionAsync(ConnectionProfile profile, CancellationToken cancellationToken)
    {
        var connString = BuildConnectionString(profile.Server, "master", profile.AuthMode, profile.UserName, profile.Password, profile.TrustCertificate);
        var result = await ExecuteScalarAsync(connString, "SELECT @@SERVERNAME;", cancellationToken);
        return Convert.ToString(result) ?? string.Empty;
    }

    public async Task<List<string>> LoadDatabasesAsync(ConnectionProfile profile, CancellationToken cancellationToken)
    {
        var connString = BuildConnectionString(profile.Server, "master", profile.AuthMode, profile.UserName, profile.Password, profile.TrustCertificate);
        var dt = await ExecuteQueryAsync(connString, "SELECT name FROM sys.databases WHERE database_id > 4 ORDER BY name;", cancellationToken);
        return dt.Rows.Cast<DataRow>().Select(r => Convert.ToString(r["name"]) ?? string.Empty).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
    }

    public async Task<List<TableSelectionItem>> LoadTablesAsync(ConnectionProfile sourceProfile, CancellationToken cancellationToken)
    {
        var connString = BuildConnectionString(sourceProfile.Server, sourceProfile.Database, sourceProfile.AuthMode, sourceProfile.UserName, sourceProfile.Password, sourceProfile.TrustCertificate);
        var sql = @"
SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    SUM(COALESCE(p.row_count, 0)) AS [RowCount]
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
LEFT JOIN sys.dm_db_partition_stats p
    ON p.object_id = t.object_id
    AND p.index_id IN (0, 1)
WHERE t.is_ms_shipped = 0
GROUP BY s.name, t.name
ORDER BY s.name, t.name;";

        var dt = await ExecuteQueryAsync(connString, sql, cancellationToken);
        return dt.Rows.Cast<DataRow>().Select(r => new TableSelectionItem
        {
            Selected = false,
            Schema = Convert.ToString(r["SchemaName"]) ?? string.Empty,
            Table = Convert.ToString(r["TableName"]) ?? string.Empty,
            RowCount = Convert.ToInt64(r["RowCount"]),
            RowLimit = 0,
            RowFilter = string.Empty
        }).ToList();
    }

    public async Task<List<string>> ValidateWhereFiltersAsync(
        ConnectionProfile sourceProfile,
        IEnumerable<TableSelectionItem> tables,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var sourceConn = BuildConnectionString(
            sourceProfile.Server,
            sourceProfile.Database,
            sourceProfile.AuthMode,
            sourceProfile.UserName,
            sourceProfile.Password,
            sourceProfile.TrustCertificate);

        await using var sourceConnection = new SqlConnection(sourceConn);
        await sourceConnection.OpenAsync(cancellationToken);

        foreach (var table in tables)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var whereClause = BuildWhereClause(table.RowFilter);
            if (string.IsNullOrWhiteSpace(whereClause))
            {
                continue;
            }

            var tableName = $"[{table.Schema.Replace("]", "]]")}].[{table.Table.Replace("]", "]]")}]";
            try
            {
                ValidateWhereFilterText(whereClause);
                var validateSql = $"SELECT TOP (1) 1 FROM {tableName} WITH (NOLOCK) WHERE ({whereClause});";
                await ExecuteNonQueryWithOpenConnectionAsync(sourceConnection, validateSql, cancellationToken);
            }
            catch (Exception ex)
            {
                errors.Add($"[{table.Schema}].[{table.Table}] filter invalid: {ex.Message}");
            }
        }

        return errors;
    }

    public async Task<bool> DatabaseExistsAsync(
        ConnectionProfile destinationProfile,
        string databaseName,
        CancellationToken cancellationToken)
    {
        var connString = BuildConnectionString(
            destinationProfile.Server,
            "master",
            destinationProfile.AuthMode,
            destinationProfile.UserName,
            destinationProfile.Password,
            destinationProfile.TrustCertificate);

        var escapedDb = databaseName.Replace("'", "''");
        var sql = $"SELECT CASE WHEN DB_ID(N'{escapedDb}') IS NULL THEN 0 ELSE 1 END;";
        var result = await ExecuteScalarAsync(connString, sql, cancellationToken);
        return Convert.ToInt32(result) == 1;
    }

    public async Task<TransferSummary> TransferAsync(
        TransferRequest request,
        CancellationToken cancellationToken,
        Action<string, MigrationLogLevel>? log,
        Action<int>? progressPercent,
        Action<string>? status,
        Action<TableTransferMetric>? tableCompleted = null)
    {
        var sourceConn = BuildConnectionString(request.Source.Server, request.Source.Database, request.Source.AuthMode, request.Source.UserName, request.Source.Password, request.Source.TrustCertificate);
        var destinationMasterConn = BuildConnectionString(request.Destination.Server, "master", request.Destination.AuthMode, request.Destination.UserName, request.Destination.Password, request.Destination.TrustCertificate);
        var destinationDbConn = BuildConnectionString(request.Destination.Server, request.DestinationDatabaseName, request.Destination.AuthMode, request.Destination.UserName, request.Destination.Password, request.Destination.TrustCertificate);

        var selected = request.Tables.Where(t => t.Selected).ToList();
        var preCopyUnits = 1 + (request.ScriptFunctionsAndTvfs ? 1 : 0) + 1;
        var postCopyUnits = (request.PreserveIndexes ? 1 : 0) + (request.PreserveForeignKeys ? 1 : 0);
        var totalUnits = preCopyUnits + 1 + postCopyUnits;
        var currentUnit = 0;

        void EmitProgress(double completedUnits)
        {
            if (totalUnits <= 0)
            {
                progressPercent?.Invoke(0);
                return;
            }

            var percent = (int)Math.Clamp(Math.Round(completedUnits / totalUnits * 100.0), 0, 100);
            progressPercent?.Invoke(percent);
        }

        EmitProgress(0);

        log?.Invoke("Phase 1/3: Creating destination database if missing...", MigrationLogLevel.Info);
        await EnsureDatabaseAsync(destinationMasterConn, request.DestinationDatabaseName, cancellationToken);
        currentUnit++;
        EmitProgress(currentUnit);

        if (request.ScriptFunctionsAndTvfs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            log?.Invoke("Phase 2: Scripting functions/TVFs...", MigrationLogLevel.Info);
            await ScriptFunctionsAsync(sourceConn, destinationDbConn, cancellationToken, log);
            currentUnit++;
            EmitProgress(currentUnit);
        }

        log?.Invoke("Phase: Creating table schemas...", MigrationLogLevel.Info);
        foreach (var table in selected)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await EnsureTableSchemaAsync(sourceConn, destinationDbConn, table, cancellationToken, log);
        }
        currentUnit++;
        EmitProgress(currentUnit);

        log?.Invoke("Phase: Copying data with SqlBulkCopy...", MigrationLogLevel.Info);
        var successCount = 0;
        var failCount = 0;
        var totalCopyRows = selected.Sum(t => t.RowLimit > 0 ? Math.Min(t.RowCount, t.RowLimit) : t.RowCount);
        var copiedRows = 0L;
        var copyPhaseStartUnit = currentUnit;

        if (totalCopyRows <= 0)
        {
            EmitProgress(copyPhaseStartUnit + 1);
        }

        foreach (var table in selected)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var estimatedRows = table.RowLimit > 0
                ? Math.Min(table.RowCount, table.RowLimit)
                : table.RowCount;
            var tableTimer = Stopwatch.StartNew();
            var tableCopiedRows = 0L;

            status?.Invoke($"Copying [{table.Schema}].[{table.Table}]");
            try
            {
                var rowsCopied = await CopyRowsAsync(
                    sourceConn,
                    destinationDbConn,
                    table,
                    request.PreserveIdentity,
                    cancellationToken,
                    deltaRows =>
                    {
                        if (deltaRows <= 0)
                        {
                            return;
                        }

                        tableCopiedRows += deltaRows;
                        copiedRows += deltaRows;

                        var safeCopiedRows = Math.Min(totalCopyRows, copiedRows);
                        var copyFraction = totalCopyRows <= 0 ? 1.0 : (double)safeCopiedRows / totalCopyRows;
                        EmitProgress(copyPhaseStartUnit + copyFraction);
                    });

                if (rowsCopied > tableCopiedRows)
                {
                    var remainder = rowsCopied - tableCopiedRows;
                    copiedRows += remainder;
                    var safeCopiedRows = Math.Min(totalCopyRows, copiedRows);
                    var copyFraction = totalCopyRows <= 0 ? 1.0 : (double)safeCopiedRows / totalCopyRows;
                    EmitProgress(copyPhaseStartUnit + copyFraction);
                }

                successCount++;
                log?.Invoke($"Copied [{table.Schema}].[{table.Table}] ({rowsCopied:N0} rows)", MigrationLogLevel.Success);
                tableCompleted?.Invoke(new TableTransferMetric
                {
                    Schema = table.Schema,
                    Table = table.Table,
                    EstimatedRows = estimatedRows,
                    RowsCopied = rowsCopied,
                    Elapsed = tableTimer.Elapsed,
                    Success = true
                });
            }
            catch (Exception ex)
            {
                failCount++;
                log?.Invoke($"Failed [{table.Schema}].[{table.Table}]: {ex.Message}", MigrationLogLevel.Error);
                tableCompleted?.Invoke(new TableTransferMetric
                {
                    Schema = table.Schema,
                    Table = table.Table,
                    EstimatedRows = estimatedRows,
                    RowsCopied = 0,
                    Elapsed = tableTimer.Elapsed,
                    Success = false
                });
            }

        }

        currentUnit = copyPhaseStartUnit + 1;
        EmitProgress(currentUnit);

        if (request.PreserveIndexes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            log?.Invoke("Phase: Scripting nonclustered indexes...", MigrationLogLevel.Info);
            await ScriptIndexesAsync(sourceConn, destinationDbConn, selected, cancellationToken, log);
            currentUnit++;
            EmitProgress(currentUnit);
        }

        if (request.PreserveForeignKeys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            log?.Invoke("Phase: Applying foreign keys...", MigrationLogLevel.Info);
            await ApplyForeignKeysAsync(sourceConn, destinationDbConn, selected, cancellationToken, log);
            currentUnit++;
            EmitProgress(currentUnit);
        }

        EmitProgress(totalUnits);
        return new TransferSummary { SuccessCount = successCount, FailCount = failCount };
    }

    private async Task EnsureDatabaseAsync(string masterConnectionString, string databaseName, CancellationToken cancellationToken)
    {
        var safeName = databaseName.Replace("]", "]]");
        var sql = $@"
IF DB_ID(N'{databaseName.Replace("'", "''")}') IS NULL
BEGIN
    CREATE DATABASE [{safeName}];
END;";

        await ExecuteNonQueryAsync(masterConnectionString, sql, cancellationToken);
    }

    private async Task EnsureTableSchemaAsync(string sourceConnectionString, string destinationConnectionString, TableSelectionItem table, CancellationToken cancellationToken, Action<string, MigrationLogLevel>? log)
    {
        var escapedSchema = table.Schema.Replace("'", "''");
        var escapedTable = table.Table.Replace("'", "''");

        var existsSql = $@"
SELECT COUNT(1)
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE s.name = N'{escapedSchema}' AND t.name = N'{escapedTable}';";

        var exists = Convert.ToInt32(await ExecuteScalarAsync(destinationConnectionString, existsSql, cancellationToken));
        if (exists > 0)
        {
            await EnsurePrimaryKeyAsync(sourceConnectionString, destinationConnectionString, table, cancellationToken);
            return;
        }

        var columnsSql = $@"
SELECT
    c.name AS ColumnName,
    ty.name AS TypeName,
    c.max_length,
    c.precision,
    c.scale,
    c.is_nullable,
    c.is_identity
FROM sys.columns c
JOIN sys.tables t ON c.object_id = t.object_id
JOIN sys.schemas s ON t.schema_id = s.schema_id
JOIN sys.types ty ON c.user_type_id = ty.user_type_id
WHERE s.name = N'{escapedSchema}'
  AND t.name = N'{escapedTable}'
  AND c.is_computed = 0
ORDER BY c.column_id;";

        var dtColumns = await ExecuteQueryAsync(sourceConnectionString, columnsSql, cancellationToken);
        if (dtColumns.Rows.Count == 0)
        {
            throw new InvalidOperationException($"No columns found for [{table.Schema}].[{table.Table}]");
        }

        var columnDefinitions = new List<string>(dtColumns.Rows.Count);
        foreach (DataRow row in dtColumns.Rows)
        {
            var name = Convert.ToString(row["ColumnName"]) ?? string.Empty;
            var typeSql = BuildSqlType(row);
            var isNullable = Convert.ToBoolean(row["is_nullable"]);
            var isIdentity = Convert.ToBoolean(row["is_identity"]);
            var identitySql = isIdentity ? " IDENTITY(1,1)" : string.Empty;
            var nullableSql = isNullable ? "NULL" : "NOT NULL";

            columnDefinitions.Add($"[{name}] {typeSql}{identitySql} {nullableSql}");
        }

        var safeSchema = table.Schema.Replace("]", "]]");
        var safeTable = table.Table.Replace("]", "]]");

        var createSchemaSql = $@"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'{escapedSchema}')
BEGIN
    EXEC(N'CREATE SCHEMA [{safeSchema}] AUTHORIZATION [dbo]');
END;";

        var createTableSql = new StringBuilder()
            .Append("CREATE TABLE [")
            .Append(safeSchema)
            .Append("].[")
            .Append(safeTable)
            .AppendLine("] (")
            .Append("    ")
            .Append(string.Join("," + Environment.NewLine + "    ", columnDefinitions))
            .AppendLine()
            .Append(");")
            .ToString();

        await ExecuteNonQueryAsync(destinationConnectionString, createSchemaSql, cancellationToken);
        await ExecuteNonQueryAsync(destinationConnectionString, createTableSql, cancellationToken);
        await EnsurePrimaryKeyAsync(sourceConnectionString, destinationConnectionString, table, cancellationToken);
        log?.Invoke($"Created table [{table.Schema}].[{table.Table}]", MigrationLogLevel.Info);
    }

    private async Task EnsurePrimaryKeyAsync(string sourceConnectionString, string destinationConnectionString, TableSelectionItem table, CancellationToken cancellationToken)
    {
        var escapedSchema = table.Schema.Replace("'", "''");
        var escapedTable = table.Table.Replace("'", "''");

        var sourcePkSql = $@"
SELECT
    kc.name AS ConstraintName,
    i.type_desc AS IndexType,
    STUFF((
        SELECT
            ', ' + QUOTENAME(c2.name) + CASE WHEN ic2.is_descending_key = 1 THEN ' DESC' ELSE ' ASC' END
        FROM sys.index_columns ic2
        JOIN sys.columns c2
            ON c2.object_id = ic2.object_id
           AND c2.column_id = ic2.column_id
        WHERE ic2.object_id = i.object_id
          AND ic2.index_id = i.index_id
          AND ic2.is_included_column = 0
        ORDER BY ic2.key_ordinal
        FOR XML PATH(''), TYPE
    ).value('.', 'nvarchar(max)'), 1, 2, '') AS KeyCols
FROM sys.key_constraints kc
JOIN sys.tables t ON t.object_id = kc.parent_object_id
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.indexes i ON i.object_id = kc.parent_object_id AND i.index_id = kc.unique_index_id
WHERE kc.type = 'PK'
  AND s.name = N'{escapedSchema}'
  AND t.name = N'{escapedTable}';";

        var dt = await ExecuteQueryAsync(sourceConnectionString, sourcePkSql, cancellationToken);
        if (dt.Rows.Count == 0)
        {
            return;
        }

        var row = dt.Rows[0];
        var constraintName = Convert.ToString(row["ConstraintName"]) ?? string.Empty;
        var indexType = Convert.ToString(row["IndexType"]) ?? "CLUSTERED";
        var keyCols = Convert.ToString(row["KeyCols"]) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(constraintName) || string.IsNullOrWhiteSpace(keyCols))
        {
            return;
        }

        var safeSchema = table.Schema.Replace("]", "]]");
        var safeTable = table.Table.Replace("]", "]]");
        var safeConstraint = constraintName.Replace("]", "]]");
        var pkType = string.Equals(indexType, "NONCLUSTERED", StringComparison.OrdinalIgnoreCase) ? "NONCLUSTERED" : "CLUSTERED";

        var applySql = $@"
IF NOT EXISTS (
    SELECT 1
    FROM sys.key_constraints
    WHERE [type] = 'PK'
      AND parent_object_id = OBJECT_ID(N'[{safeSchema}].[{safeTable}]')
)
BEGIN
    ALTER TABLE [{safeSchema}].[{safeTable}]
    ADD CONSTRAINT [{safeConstraint}] PRIMARY KEY {pkType} ({keyCols});
END;";

        await ExecuteNonQueryAsync(destinationConnectionString, applySql, cancellationToken);
    }

    private static string BuildSqlType(DataRow row)
    {
        var type = (Convert.ToString(row["TypeName"]) ?? string.Empty).ToLowerInvariant();
        var maxLength = Convert.ToInt32(row["max_length"]);
        var precision = Convert.ToInt32(row["precision"]);
        var scale = Convert.ToInt32(row["scale"]);
        return type switch
        {
            "varchar" => maxLength == -1 ? "varchar(max)" : $"varchar({maxLength})",
            "nvarchar" => maxLength == -1 ? "nvarchar(max)" : $"nvarchar({maxLength / 2})",
            "char" => $"char({maxLength})",
            "nchar" => $"nchar({maxLength / 2})",
            "varbinary" => maxLength == -1 ? "varbinary(max)" : $"varbinary({maxLength})",
            "binary" => $"binary({maxLength})",
            "decimal" => $"decimal({precision},{scale})",
            "numeric" => $"numeric({precision},{scale})",
            "datetime2" => $"datetime2({scale})",
            "datetimeoffset" => $"datetimeoffset({scale})",
            "time" => $"time({scale})",
            _ => type
        };
    }

    private async Task<long> CopyRowsAsync(
        string sourceConnectionString,
        string destinationConnectionString,
        TableSelectionItem table,
        bool keepIdentity,
        CancellationToken cancellationToken,
        Action<long>? onRowsCopied)
    {
        var safeSchema = table.Schema.Replace("]", "]]");
        var safeTable = table.Table.Replace("]", "]]");
        var destinationTable = $"[{safeSchema}].[{safeTable}]";
        var sourceTable = $"[{safeSchema}].[{safeTable}]";
        var whereClause = BuildWhereClause(table.RowFilter);

        if (!string.IsNullOrWhiteSpace(whereClause))
        {
            ValidateWhereFilterText(table.RowFilter);
        }

        var estimatedRows = table.RowLimit > 0
            ? Math.Min(table.RowCount, table.RowLimit)
            : table.RowCount;
        var batchSize = GetAdaptiveBatchSize(estimatedRows);
        var notifyAfter = Math.Max(1000, batchSize / 2);

        var selectSql = BuildSelectSql(sourceTable, table.RowLimit, whereClause);

        try
        {
            await ExecuteNonQueryAsync(destinationConnectionString, $"TRUNCATE TABLE {destinationTable};", cancellationToken);
        }
        catch
        {
            await ExecuteNonQueryAsync(destinationConnectionString, $"DELETE FROM {destinationTable};", cancellationToken);
        }

        var options = SqlBulkCopyOptions.TableLock;
        if (keepIdentity) options |= SqlBulkCopyOptions.KeepIdentity;

        await using var sourceConnection = new SqlConnection(sourceConnectionString);
        await sourceConnection.OpenAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(whereClause))
        {
            var validateSql = $"SELECT TOP (1) 1 FROM {sourceTable} WITH (NOLOCK) WHERE ({whereClause});";
            await ExecuteNonQueryWithOpenConnectionAsync(sourceConnection, validateSql, cancellationToken);
        }

        await using var sourceCommand = new SqlCommand(selectSql, sourceConnection) { CommandTimeout = 0 };
        await using var reader = await sourceCommand.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);

        await using var destinationConnection = new SqlConnection(destinationConnectionString);
        await destinationConnection.OpenAsync(cancellationToken);

        using var bulkCopy = new SqlBulkCopy(destinationConnection, options, null)
        {
            DestinationTableName = destinationTable,
            BulkCopyTimeout = 0,
            BatchSize = batchSize,
            EnableStreaming = true,
            NotifyAfter = notifyAfter
        };

        long notifiedRows = 0;
        bulkCopy.SqlRowsCopied += (_, args) =>
        {
            var delta = args.RowsCopied - notifiedRows;
            notifiedRows = args.RowsCopied;
            if (delta > 0)
            {
                onRowsCopied?.Invoke(delta);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                args.Abort = true;
            }
        };

        for (var i = 0; i < reader.FieldCount; i++)
        {
            var columnName = reader.GetName(i);
            bulkCopy.ColumnMappings.Add(columnName, columnName);
        }

        await bulkCopy.WriteToServerAsync(reader, cancellationToken);
        EnforceMemoryBudget($"copy [{table.Schema}].[{table.Table}]");

        await using var countCommand = new SqlCommand($"SELECT COUNT_BIG(1) FROM {destinationTable};", destinationConnection)
        {
            CommandTimeout = 120
        };
        var countResult = await countCommand.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(countResult);
    }

    private static string BuildSelectSql(string sourceTable, int rowLimit, string whereClause)
    {
        if (rowLimit > 0)
        {
            if (string.IsNullOrWhiteSpace(whereClause))
            {
                return $"SELECT TOP ({rowLimit}) * FROM {sourceTable} WITH (NOLOCK);";
            }

            return $"SELECT TOP ({rowLimit}) * FROM {sourceTable} WITH (NOLOCK) WHERE ({whereClause});";
        }

        if (string.IsNullOrWhiteSpace(whereClause))
        {
            return $"SELECT * FROM {sourceTable} WITH (NOLOCK);";
        }

        return $"SELECT * FROM {sourceTable} WITH (NOLOCK) WHERE ({whereClause});";
    }

    private static string BuildWhereClause(string? filter)
    {
        return string.IsNullOrWhiteSpace(filter) ? string.Empty : filter.Trim();
    }

    private static void ValidateWhereFilterText(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return;
        }

        var normalized = filter.Trim();
        if (normalized.Contains(';')
            || normalized.Contains("--", StringComparison.Ordinal)
            || normalized.Contains("/*", StringComparison.Ordinal)
            || normalized.Contains("*/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Filter validation failed: comments and semicolons are not allowed.");
        }

        var disallowed = new[]
        {
            "INSERT", "UPDATE", "DELETE", "DROP", "ALTER", "CREATE", "TRUNCATE",
            "EXEC", "EXECUTE", "MERGE", "GRANT", "REVOKE", "DENY"
        };

        foreach (var token in disallowed)
        {
            if (Regex.IsMatch(normalized, $@"\b{token}\b", RegexOptions.IgnoreCase))
            {
                throw new InvalidOperationException($"Filter validation failed: keyword '{token}' is not allowed.");
            }
        }
    }

    private static int GetAdaptiveBatchSize(long estimatedRows)
    {
        if (estimatedRows <= 0)
        {
            return 10_000;
        }

        if (estimatedRows < 50_000)
        {
            return 2_000;
        }

        if (estimatedRows <= 500_000)
        {
            return 10_000;
        }

        return 20_000;
    }

    private void EnforceMemoryBudget(string operation)
    {
        var currentMb = Process.GetCurrentProcess().PrivateMemorySize64 / (1024 * 1024);
        if (currentMb >= MemoryGcThresholdMb)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            currentMb = Process.GetCurrentProcess().PrivateMemorySize64 / (1024 * 1024);
        }

        if (currentMb > MaxProcessMemoryMb)
        {
            throw new InvalidOperationException($"Memory budget exceeded ({currentMb} MB > {MaxProcessMemoryMb} MB) during {operation}.");
        }
    }

    private async Task ScriptFunctionsAsync(string sourceConnectionString, string destinationConnectionString, CancellationToken cancellationToken, Action<string, MigrationLogLevel>? log)
    {
        const string sql = @"
SELECT
    SCHEMA_NAME(o.schema_id) AS SchemaName,
    o.name AS ObjectName,
    m.definition
FROM sys.objects o
JOIN sys.sql_modules m ON m.object_id = o.object_id
WHERE o.type IN ('FN', 'IF', 'TF', 'FS', 'FT')
  AND o.is_ms_shipped = 0
ORDER BY o.create_date;";

        var dt = await ExecuteQueryAsync(sourceConnectionString, sql, cancellationToken);
        var applied = 0;
        foreach (DataRow row in dt.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var schema = Convert.ToString(row["SchemaName"]) ?? "dbo";
            var name = Convert.ToString(row["ObjectName"]) ?? string.Empty;
            var definition = Convert.ToString(row["definition"]);
            if (string.IsNullOrWhiteSpace(definition)) continue;

            var normalized = Regex.Replace(definition, @"^\s*CREATE\s+FUNCTION\s+", "CREATE OR ALTER FUNCTION ", RegexOptions.IgnoreCase);
            try
            {
                await ExecuteNonQueryAsync(destinationConnectionString, normalized, cancellationToken);
                applied++;
                log?.Invoke($"Function scripted: [{schema}].[{name}]", MigrationLogLevel.Info);
            }
            catch (Exception ex)
            {
                log?.Invoke($"Function skipped [{schema}].[{name}]: {ex.Message}", MigrationLogLevel.Warning);
            }
        }

        log?.Invoke($"Functions/TVFs applied: {applied}", MigrationLogLevel.Success);
    }

    private async Task ScriptIndexesAsync(string sourceConnectionString, string destinationConnectionString, IReadOnlyList<TableSelectionItem> selectedTables, CancellationToken cancellationToken, Action<string, MigrationLogLevel>? log)
    {
        foreach (var table in selectedTables)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var escapedSchema = table.Schema.Replace("'", "''");
            var escapedTable = table.Table.Replace("'", "''");

            var sql = $@"
SELECT
    i.name AS IndexName,
    i.is_unique AS IsUnique,
    i.filter_definition AS FilterDefinition,
    STUFF((
        SELECT
            ', ' + QUOTENAME(c2.name) + CASE WHEN ic2.is_descending_key = 1 THEN ' DESC' ELSE ' ASC' END
        FROM sys.index_columns ic2
        JOIN sys.columns c2
            ON c2.object_id = ic2.object_id
           AND c2.column_id = ic2.column_id
        WHERE ic2.object_id = i.object_id
          AND ic2.index_id = i.index_id
          AND ic2.is_included_column = 0
        ORDER BY ic2.key_ordinal
        FOR XML PATH(''), TYPE
    ).value('.', 'nvarchar(max)'), 1, 2, '') AS KeyCols,
    STUFF((
        SELECT
            ', ' + QUOTENAME(c3.name)
        FROM sys.index_columns ic3
        JOIN sys.columns c3
            ON c3.object_id = ic3.object_id
           AND c3.column_id = ic3.column_id
        WHERE ic3.object_id = i.object_id
          AND ic3.index_id = i.index_id
          AND ic3.is_included_column = 1
        ORDER BY ic3.index_column_id
        FOR XML PATH(''), TYPE
    ).value('.', 'nvarchar(max)'), 1, 2, '') AS IncludedCols
FROM sys.indexes i
JOIN sys.tables t ON t.object_id = i.object_id
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE s.name = N'{escapedSchema}'
  AND t.name = N'{escapedTable}'
  AND i.is_primary_key = 0
  AND i.is_unique_constraint = 0
  AND i.type_desc = 'NONCLUSTERED';";

            var dt = await ExecuteQueryAsync(sourceConnectionString, sql, cancellationToken);
            var tableApplied = 0;
            foreach (DataRow row in dt.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var indexName = Convert.ToString(row["IndexName"]) ?? string.Empty;
                var isUnique = Convert.ToBoolean(row["IsUnique"]);
                var keyCols = Convert.ToString(row["KeyCols"]) ?? string.Empty;
                var includedCols = Convert.ToString(row["IncludedCols"]);
                var filterDefinition = Convert.ToString(row["FilterDefinition"]);
                if (string.IsNullOrWhiteSpace(indexName) || string.IsNullOrWhiteSpace(keyCols)) continue;

                var safeSchema = table.Schema.Replace("]", "]]");
                var safeTable = table.Table.Replace("]", "]]");
                var safeIndex = indexName.Replace("]", "]]");
                var uniquePart = isUnique ? "UNIQUE " : string.Empty;
                var includePart = string.IsNullOrWhiteSpace(includedCols) ? string.Empty : $" INCLUDE ({includedCols})";
                var filterPart = string.IsNullOrWhiteSpace(filterDefinition) ? string.Empty : $" WHERE {filterDefinition}";

                var createSql = $@"
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'{indexName.Replace("'", "''")}'
      AND object_id = OBJECT_ID(N'[{safeSchema}].[{safeTable}]')
)
BEGIN
    CREATE {uniquePart}NONCLUSTERED INDEX [{safeIndex}]
    ON [{safeSchema}].[{safeTable}] ({keyCols}){includePart}{filterPart};
END;";

                try
                {
                    await ExecuteNonQueryAsync(destinationConnectionString, createSql, cancellationToken);
                    tableApplied++;
                }
                catch (Exception ex)
                {
                    log?.Invoke($"Index skipped [{table.Schema}].[{table.Table}].[{indexName}]: {ex.Message}", MigrationLogLevel.Warning);
                }
            }

            if (tableApplied > 0)
            {
                log?.Invoke($"Indexes scripted on [{table.Schema}].[{table.Table}]: {tableApplied}", MigrationLogLevel.Info);
            }
        }
    }

    private async Task ApplyForeignKeysAsync(string sourceConnectionString, string destinationConnectionString, IReadOnlyList<TableSelectionItem> selectedTables, CancellationToken cancellationToken, Action<string, MigrationLogLevel>? log)
    {
        var tableSet = selectedTables.Select(t => $"{t.Schema}.{t.Table}").ToHashSet(StringComparer.OrdinalIgnoreCase);

        const string sql = @"
SELECT
    fk.name AS FKName,
    ps.name AS ParentSchema,
    pt.name AS ParentTable,
    rs.name AS RefSchema,
    rt.name AS RefTable,
    STUFF((
        SELECT ', ' + QUOTENAME(pc2.name)
        FROM sys.foreign_key_columns fkc2
        JOIN sys.columns pc2 ON pc2.object_id = fkc2.parent_object_id AND pc2.column_id = fkc2.parent_column_id
        WHERE fkc2.constraint_object_id = fk.object_id
        ORDER BY fkc2.constraint_column_id
        FOR XML PATH(''), TYPE
    ).value('.', 'nvarchar(max)'), 1, 2, '') AS ParentCols,
    STUFF((
        SELECT ', ' + QUOTENAME(rc2.name)
        FROM sys.foreign_key_columns fkc3
        JOIN sys.columns rc2 ON rc2.object_id = fkc3.referenced_object_id AND rc2.column_id = fkc3.referenced_column_id
        WHERE fkc3.constraint_object_id = fk.object_id
        ORDER BY fkc3.constraint_column_id
        FOR XML PATH(''), TYPE
    ).value('.', 'nvarchar(max)'), 1, 2, '') AS RefCols,
    fk.delete_referential_action_desc AS DeleteAction,
    fk.update_referential_action_desc AS UpdateAction
FROM sys.foreign_keys fk
JOIN sys.tables pt ON pt.object_id = fk.parent_object_id
JOIN sys.schemas ps ON ps.schema_id = pt.schema_id
JOIN sys.tables rt ON rt.object_id = fk.referenced_object_id
JOIN sys.schemas rs ON rs.schema_id = rt.schema_id
WHERE fk.is_ms_shipped = 0
ORDER BY ps.name, pt.name, fk.name;";

        var dt = await ExecuteQueryAsync(sourceConnectionString, sql, cancellationToken);
        var applied = 0;
        foreach (DataRow row in dt.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var parentSchema = Convert.ToString(row["ParentSchema"]) ?? string.Empty;
            var parentTable = Convert.ToString(row["ParentTable"]) ?? string.Empty;
            var refSchema = Convert.ToString(row["RefSchema"]) ?? string.Empty;
            var refTable = Convert.ToString(row["RefTable"]) ?? string.Empty;
            if (!tableSet.Contains($"{parentSchema}.{parentTable}") || !tableSet.Contains($"{refSchema}.{refTable}")) continue;

            var fkName = Convert.ToString(row["FKName"]) ?? string.Empty;
            var parentCols = Convert.ToString(row["ParentCols"]) ?? string.Empty;
            var refCols = Convert.ToString(row["RefCols"]) ?? string.Empty;
            var deleteAction = (Convert.ToString(row["DeleteAction"]) ?? "NO_ACTION").Replace("_", " ");
            var updateAction = (Convert.ToString(row["UpdateAction"]) ?? "NO_ACTION").Replace("_", " ");
            if (string.IsNullOrWhiteSpace(fkName) || string.IsNullOrWhiteSpace(parentCols) || string.IsNullOrWhiteSpace(refCols)) continue;

            var safeFk = fkName.Replace("]", "]]");
            var safeParentSchema = parentSchema.Replace("]", "]]");
            var safeParentTable = parentTable.Replace("]", "]]");
            var safeRefSchema = refSchema.Replace("]", "]]");
            var safeRefTable = refTable.Replace("]", "]]");
            var deletePart = string.Equals(deleteAction, "NO ACTION", StringComparison.OrdinalIgnoreCase) ? string.Empty : $" ON DELETE {deleteAction}";
            var updatePart = string.Equals(updateAction, "NO ACTION", StringComparison.OrdinalIgnoreCase) ? string.Empty : $" ON UPDATE {updateAction}";

            var applySql = $@"
IF OBJECT_ID(N'[{safeParentSchema}].[{safeFk}]', 'F') IS NULL
BEGIN
    ALTER TABLE [{safeParentSchema}].[{safeParentTable}]
    ADD CONSTRAINT [{safeFk}] FOREIGN KEY ({parentCols})
    REFERENCES [{safeRefSchema}].[{safeRefTable}] ({refCols}){deletePart}{updatePart};
END;";

            try
            {
                await ExecuteNonQueryAsync(destinationConnectionString, applySql, cancellationToken);
                applied++;
            }
            catch (Exception ex)
            {
                log?.Invoke($"FK skipped [{fkName}] on [{parentSchema}].[{parentTable}]: {ex.Message}", MigrationLogLevel.Warning);
            }
        }

        log?.Invoke($"Foreign keys applied: {applied}", MigrationLogLevel.Success);
    }

    private static string BuildConnectionString(string server, string database, string authMode, string user, string pass, bool trustCertificate)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = server,
            InitialCatalog = database,
            ConnectTimeout = 15,
            ApplicationName = "SQLDataMigrator",
            Encrypt = true,
            TrustServerCertificate = trustCertificate
        };

        if (string.Equals(authMode, "SQL Login", StringComparison.OrdinalIgnoreCase))
        {
            builder.IntegratedSecurity = false;
            builder.UserID = user;
            builder.Password = pass;
        }
        else
        {
            builder.IntegratedSecurity = true;
        }

        return builder.ConnectionString;
    }

    private static async Task<DataTable> ExecuteQueryAsync(string connectionString, string sql, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var table = new DataTable();
        table.Load(reader);
        return table;
    }

    private static async Task<object?> ExecuteScalarAsync(string connectionString, string sql, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        return await command.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task ExecuteNonQueryAsync(string connectionString, string sql, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ExecuteNonQueryWithOpenConnectionAsync(SqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
