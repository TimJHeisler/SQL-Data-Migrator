namespace SQLDataMigrator;

public sealed class TableSelectionItem
{
    public bool Selected { get; set; }
    public string Schema { get; set; } = string.Empty;
    public string Table { get; set; } = string.Empty;
    public long RowCount { get; set; }
    public int RowLimit { get; set; }
    public string RowFilter { get; set; } = string.Empty;
}
