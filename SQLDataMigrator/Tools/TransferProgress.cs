namespace SQLQueryOptimizer
{
    internal enum TransferPhase
    {
        Connecting,
        CreatingDatabase,
        ScriptingFunctions,
        ScriptingSchemas,
        ScriptingIndexes,
        TransferringData,
        ApplyingForeignKeys,
        Complete,
        Failed
    }

    internal sealed class TransferProgress
    {
        public TransferPhase Phase { get; init; }
        public string Message { get; init; } = string.Empty;
        public string? TableName { get; init; }
        public long RowsCopied { get; init; }
        public long TotalRows { get; init; }
        public int TablesCompleted { get; init; }
        public int TotalTables { get; init; }
        public bool IsError { get; init; }
        public bool IsWarning { get; init; }

        public static TransferProgress Info(string message, TransferPhase phase = TransferPhase.TransferringData) =>
            new() { Message = message, Phase = phase };

        public static TransferProgress Error(string message, TransferPhase phase = TransferPhase.TransferringData) =>
            new() { Message = message, Phase = phase, IsError = true };

        public static TransferProgress Warning(string message) =>
            new() { Message = message, IsWarning = true };

        public static TransferProgress TableUpdate(string table, long rowsCopied, long totalRows, int tablesCompleted, int totalTables) =>
            new()
            {
                Phase = TransferPhase.TransferringData,
                TableName = table,
                RowsCopied = rowsCopied,
                TotalRows = totalRows,
                TablesCompleted = tablesCompleted,
                TotalTables = totalTables,
                Message = totalRows > 0
                    ? $"  [{table}] {rowsCopied:N0} / {totalRows:N0} rows"
                    : $"  [{table}] {rowsCopied:N0} rows copied"
            };
    }
}
