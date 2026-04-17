using System.Text;
using Microsoft.Data.SqlClient;

namespace SQLQueryOptimizer
{
    /// <summary>
    /// Scripts database objects from a source SQL Server using sys.* catalog views.
    /// No SMO dependency — pure T-SQL introspection.
    /// </summary>
    internal sealed class SchemaScripter
    {
        private readonly string _connectionString;

        public SchemaScripter(string connectionString)
        {
            _connectionString = connectionString;
        }

        // -------------------------------------------------------------------------
        // Table list
        // -------------------------------------------------------------------------

        public async Task<List<TableTransferJob>> LoadTableListAsync(CancellationToken ct)
        {
            const string sql = """
                SELECT
                    s.name                                          AS SchemaName,
                    t.name                                          AS TableName,
                    COALESCE(p.row_count, 0)                        AS RowCount,
                    ic.pk_col                                       AS PrimaryKeyColumn
                FROM sys.tables t
                JOIN sys.schemas s ON s.schema_id = t.schema_id
                LEFT JOIN (
                    SELECT object_id, SUM(row_count) AS row_count
                    FROM sys.dm_db_partition_stats
                    WHERE index_id IN (0,1)
                    GROUP BY object_id
                ) p ON p.object_id = t.object_id
                LEFT JOIN (
                    SELECT ic2.object_id,
                           MIN(col.name) AS pk_col
                    FROM sys.index_columns ic2
                    JOIN sys.indexes i2     ON i2.object_id = ic2.object_id AND i2.index_id = ic2.index_id
                    JOIN sys.columns col    ON col.object_id = ic2.object_id AND col.column_id = ic2.column_id
                    WHERE i2.is_primary_key = 1
                    GROUP BY ic2.object_id
                ) ic ON ic.object_id = t.object_id
                WHERE t.is_ms_shipped = 0
                ORDER BY s.name, t.name;
                """;

            var jobs = new List<TableTransferJob>();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                jobs.Add(new TableTransferJob
                {
                    SchemaName = reader.GetString(0),
                    TableName = reader.GetString(1),
                    SourceRowCount = reader.GetInt64(2),
                    PrimaryKeyColumn = reader.IsDBNull(3) ? null : reader.GetString(3),
                    RowLimit = 1000,
                    Include = true
                });
            }

            return jobs;
        }

        // -------------------------------------------------------------------------
        // Functions and TVFs
        // -------------------------------------------------------------------------

        public async Task<List<string>> ScriptFunctionsAsync(CancellationToken ct)
        {
            const string sql = """
                SELECT OBJECT_SCHEMA_NAME(o.object_id) AS SchemaName,
                       o.name,
                       m.definition
                FROM sys.objects o
                JOIN sys.sql_modules m ON m.object_id = o.object_id
                WHERE o.type IN ('FN','IF','TF','FS','FT')
                  AND o.is_ms_shipped = 0
                ORDER BY o.create_date;
                """;

            var scripts = new List<string>();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var definition = reader.GetString(2);
                // Wrap in CREATE-or-ALTER pattern safe for re-run
                scripts.Add(WrapInExecSp(definition));
            }

            return scripts;
        }

        // -------------------------------------------------------------------------
        // Table CREATE script
        // -------------------------------------------------------------------------

        public async Task<string> ScriptCreateTableAsync(string schema, string table, CancellationToken ct)
        {
            var sb = new StringBuilder();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            // Columns
            const string colSql = """
                SELECT
                    c.name,
                    tp.name                         AS TypeName,
                    c.max_length,
                    c.precision,
                    c.scale,
                    c.is_nullable,
                    c.is_identity,
                    IDENT_SEED(QUOTENAME(s.name) + '.' + QUOTENAME(t.name))  AS SeedVal,
                    IDENT_INCR(QUOTENAME(s.name) + '.' + QUOTENAME(t.name))  AS IncrVal,
                    c.is_computed,
                    cc.definition                   AS ComputedDef,
                    cc.is_persisted,
                    dc.definition                   AS DefaultDef,
                    dc.name                         AS DefaultName,
                    c.collation_name
                FROM sys.columns c
                JOIN sys.tables t       ON t.object_id = c.object_id
                JOIN sys.schemas s      ON s.schema_id = t.schema_id
                JOIN sys.types tp       ON tp.user_type_id = c.user_type_id
                LEFT JOIN sys.computed_columns cc ON cc.object_id = c.object_id AND cc.column_id = c.column_id
                LEFT JOIN sys.default_constraints dc ON dc.object_id = c.default_object_id
                WHERE s.name = @schema AND t.name = @table
                ORDER BY c.column_id;
                """;

            await using var colCmd = new SqlCommand(colSql, conn) { CommandTimeout = 60 };
            colCmd.Parameters.AddWithValue("@schema", schema);
            colCmd.Parameters.AddWithValue("@table", table);

            sb.AppendLine($"IF OBJECT_ID(N'[{schema}].[{table}]', 'U') IS NULL");
            sb.AppendLine("BEGIN");
            sb.AppendLine($"CREATE TABLE [{schema}].[{table}] (");

            var columns = new List<string>();
            await using (var reader = await colCmd.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    var colName = reader.GetString(0);
                    var typeName = reader.GetString(1);
                    var maxLength = reader.GetInt16(2);
                    var precision = reader.GetByte(3);
                    var scale = reader.GetByte(4);
                    var isNullable = reader.GetBoolean(5);
                    var isIdentity = reader.GetBoolean(6);
                    var seed = reader.IsDBNull(7) ? 1m : reader.GetDecimal(7);
                    var incr = reader.IsDBNull(8) ? 1m : reader.GetDecimal(8);
                    var isComputed = reader.GetBoolean(9);
                    var computedDef = reader.IsDBNull(10) ? null : reader.GetString(10);
                    var isPersisted = !reader.IsDBNull(11) && reader.GetBoolean(11);
                    var defaultDef = reader.IsDBNull(12) ? null : reader.GetString(12);
                    var defaultName = reader.IsDBNull(13) ? null : reader.GetString(13);
                    var collation = reader.IsDBNull(14) ? null : reader.GetString(14);

                    if (isComputed && computedDef != null)
                    {
                        var persisted = isPersisted ? " PERSISTED" : string.Empty;
                        columns.Add($"    [{colName}] AS {computedDef}{persisted}");
                        continue;
                    }

                    var typeDecl = BuildTypeDeclaration(typeName, maxLength, precision, scale, collation);
                    var nullPart = isNullable ? "NULL" : "NOT NULL";
                    var identPart = isIdentity ? $" IDENTITY({seed},{incr})" : string.Empty;

                    var defaultPart = string.Empty;
                    if (defaultDef != null)
                    {
                        var dcName = defaultName != null ? $"CONSTRAINT [{defaultName}] " : string.Empty;
                        defaultPart = $" {dcName}DEFAULT {defaultDef}";
                    }

                    columns.Add($"    [{colName}] {typeDecl}{identPart}{defaultPart} {nullPart}");
                }
            }

            sb.AppendLine(string.Join($",{Environment.NewLine}", columns));
            sb.AppendLine(");");
            sb.AppendLine("END");

            // Primary key
            const string pkSql = """
                SELECT
                    i.name AS IndexName,
                    STRING_AGG(QUOTENAME(c.name) + CASE ic.is_descending_key WHEN 1 THEN ' DESC' ELSE ' ASC' END, ', ')
                        WITHIN GROUP (ORDER BY ic.key_ordinal) AS Cols
                FROM sys.indexes i
                JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                JOIN sys.columns c        ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                JOIN sys.tables t         ON t.object_id = i.object_id
                JOIN sys.schemas s        ON s.schema_id = t.schema_id
                WHERE s.name = @schema AND t.name = @table
                  AND i.is_primary_key = 1
                GROUP BY i.name;
                """;

            await using var pkCmd = new SqlCommand(pkSql, conn) { CommandTimeout = 60 };
            pkCmd.Parameters.AddWithValue("@schema", schema);
            pkCmd.Parameters.AddWithValue("@table", table);
            await using var pkReader = await pkCmd.ExecuteReaderAsync(ct);
            if (await pkReader.ReadAsync(ct))
            {
                var pkName = pkReader.GetString(0);
                var pkCols = pkReader.GetString(1);
                sb.AppendLine($"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'{pkName}' AND object_id = OBJECT_ID(N'[{schema}].[{table}]'))");
                sb.AppendLine($"    ALTER TABLE [{schema}].[{table}] ADD CONSTRAINT [{pkName}] PRIMARY KEY CLUSTERED ({pkCols});");
            }

            return sb.ToString();
        }

        // -------------------------------------------------------------------------
        // Non-clustered indexes
        // -------------------------------------------------------------------------

        public async Task<List<string>> ScriptIndexesAsync(string schema, string table, CancellationToken ct)
        {
            const string sql = """
                SELECT
                    i.name,
                    i.is_unique,
                    i.filter_definition,
                    STRING_AGG(
                        CASE WHEN ic.is_included_column = 0
                             THEN QUOTENAME(c.name) + CASE ic.is_descending_key WHEN 1 THEN ' DESC' ELSE ' ASC' END
                             ELSE NULL END,
                        ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS KeyCols,
                    STRING_AGG(
                        CASE WHEN ic.is_included_column = 1 THEN QUOTENAME(c.name) ELSE NULL END,
                        ', ') WITHIN GROUP (ORDER BY ic.index_column_id) AS IncludedCols
                FROM sys.indexes i
                JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                JOIN sys.columns c        ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                JOIN sys.tables t         ON t.object_id = i.object_id
                JOIN sys.schemas s        ON s.schema_id = t.schema_id
                WHERE s.name = @schema AND t.name = @table
                  AND i.is_primary_key = 0
                  AND i.is_unique_constraint = 0
                  AND i.type_desc = 'NONCLUSTERED'
                GROUP BY i.name, i.is_unique, i.filter_definition;
                """;

            var scripts = new List<string>();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
            cmd.Parameters.AddWithValue("@schema", schema);
            cmd.Parameters.AddWithValue("@table", table);
            await using var reader = await cmd.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
            {
                var name = reader.GetString(0);
                var isUnique = reader.GetBoolean(1);
                var filter = reader.IsDBNull(2) ? null : reader.GetString(2);
                var keyCols = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
                var includedCols = reader.IsDBNull(4) ? null : reader.GetString(4);

                if (string.IsNullOrWhiteSpace(keyCols))
                    continue;

                var uniquePart = isUnique ? "UNIQUE " : string.Empty;
                var includePart = !string.IsNullOrWhiteSpace(includedCols) ? $" INCLUDE ({includedCols})" : string.Empty;
                var filterPart = !string.IsNullOrWhiteSpace(filter) ? $" WHERE {filter}" : string.Empty;

                var script = $"""
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'{name}' AND object_id = OBJECT_ID(N'[{schema}].[{table}]'))
                        CREATE {uniquePart}NONCLUSTERED INDEX [{name}] ON [{schema}].[{table}] ({keyCols}){includePart}{filterPart};
                    """;
                scripts.Add(script);
            }

            return scripts;
        }

        // -------------------------------------------------------------------------
        // Foreign keys (scripted last)
        // -------------------------------------------------------------------------

        public async Task<List<string>> ScriptForeignKeysAsync(IEnumerable<TableTransferJob> tables, CancellationToken ct)
        {
            var tableSet = tables
                .Select(t => $"{t.SchemaName.ToUpperInvariant()}.{t.TableName.ToUpperInvariant()}")
                .ToHashSet();

            const string sql = """
                SELECT
                    fk.name                                         AS FKName,
                    ps.name                                         AS ParentSchema,
                    pt.name                                         AS ParentTable,
                    rs.name                                         AS RefSchema,
                    rt.name                                         AS RefTable,
                    STRING_AGG(QUOTENAME(pc.name), ', ')
                        WITHIN GROUP (ORDER BY fkc.constraint_column_id) AS ParentCols,
                    STRING_AGG(QUOTENAME(rc.name), ', ')
                        WITHIN GROUP (ORDER BY fkc.constraint_column_id) AS RefCols,
                    fk.delete_referential_action_desc,
                    fk.update_referential_action_desc
                FROM sys.foreign_keys fk
                JOIN sys.tables pt          ON pt.object_id = fk.parent_object_id
                JOIN sys.schemas ps         ON ps.schema_id = pt.schema_id
                JOIN sys.tables rt          ON rt.object_id = fk.referenced_object_id
                JOIN sys.schemas rs         ON rs.schema_id = rt.schema_id
                JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
                JOIN sys.columns pc         ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id
                JOIN sys.columns rc         ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
                WHERE fk.is_ms_shipped = 0
                GROUP BY fk.name, ps.name, pt.name, rs.name, rt.name,
                         fk.delete_referential_action_desc, fk.update_referential_action_desc
                ORDER BY ps.name, pt.name, fk.name;
                """;

            var scripts = new List<string>();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
            await using var reader = await cmd.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
            {
                var fkName = reader.GetString(0);
                var parentSchema = reader.GetString(1);
                var parentTable = reader.GetString(2);
                var refSchema = reader.GetString(3);
                var refTable = reader.GetString(4);
                var parentCols = reader.GetString(5);
                var refCols = reader.GetString(6);
                var deleteAction = reader.GetString(7).Replace("_", " ");
                var updateAction = reader.GetString(8).Replace("_", " ");

                // Only script FKs where BOTH sides are in the transfer set
                var parentKey = $"{parentSchema.ToUpperInvariant()}.{parentTable.ToUpperInvariant()}";
                var refKey = $"{refSchema.ToUpperInvariant()}.{refTable.ToUpperInvariant()}";
                if (!tableSet.Contains(parentKey) || !tableSet.Contains(refKey))
                    continue;

                var deletePart = deleteAction == "NO ACTION" ? string.Empty : $" ON DELETE {deleteAction}";
                var updatePart = updateAction == "NO ACTION" ? string.Empty : $" ON UPDATE {updateAction}";

                var script = $"""
                    IF OBJECT_ID(N'{fkName}', 'F') IS NULL
                        ALTER TABLE [{parentSchema}].[{parentTable}]
                            ADD CONSTRAINT [{fkName}]
                            FOREIGN KEY ({parentCols})
                            REFERENCES [{refSchema}].[{refTable}] ({refCols}){deletePart}{updatePart};
                    """;
                scripts.Add(script);
            }

            return scripts;
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private static string BuildTypeDeclaration(string typeName, short maxLength, byte precision, byte scale, string? collation)
        {
            return typeName.ToLowerInvariant() switch
            {
                "varchar" or "char" or "binary" or "varbinary" =>
                    $"[{typeName}]({(maxLength == -1 ? "MAX" : maxLength.ToString())})",
                "nvarchar" or "nchar" =>
                    $"[{typeName}]({(maxLength == -1 ? "MAX" : (maxLength / 2).ToString())})",
                "decimal" or "numeric" =>
                    $"[{typeName}]({precision},{scale})",
                "datetime2" or "time" or "datetimeoffset" =>
                    $"[{typeName}]({scale})",
                "float" =>
                    $"[{typeName}]({precision})",
                _ => $"[{typeName}]"
            };
        }

        private static string WrapInExecSp(string definition)
        {
            // Execute the raw definition in a dynamic sp_executesql so errors in one function
            // don't abort the whole batch
            var escaped = definition.Replace("'", "''");
            return $"EXEC sp_executesql N'{escaped}';";
        }
    }
}
