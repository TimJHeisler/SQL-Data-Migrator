namespace SQLQueryOptimizer
{
    internal sealed class TableTransferJob
    {
        public string SchemaName { get; set; } = "dbo";
        public string TableName { get; set; } = string.Empty;
        public string FullName => $"[{SchemaName}].[{TableName}]";
        public long SourceRowCount { get; set; }
        public int RowLimit { get; set; } = 1000;
        public bool Include { get; set; } = true;
        public string? PrimaryKeyColumn { get; set; }

        // Runtime state
        public long RowsCopied { get; set; }
        public string Status { get; set; } = "Pending";
        public string? ErrorMessage { get; set; }
        public bool Success => string.IsNullOrEmpty(ErrorMessage) && Status == "Done";
    }
}
