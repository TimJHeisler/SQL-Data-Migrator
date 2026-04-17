namespace SQLDataMigrator;

public sealed class SessionProject
{
    public int Version { get; set; } = 1;
    public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;

    public List<string> SourceServerHistory { get; set; } = new();
    public List<string> DestinationServerHistory { get; set; } = new();

    public string SourceServer { get; set; } = string.Empty;
    public string SourceDatabase { get; set; } = string.Empty;
    public string SourceAuthMode { get; set; } = "Integrated Security";
    public string SourceUserName { get; set; } = string.Empty;
    public string SourcePassword { get; set; } = string.Empty;
    public bool SourceTrustCertificate { get; set; }

    public string DestinationServer { get; set; } = string.Empty;
    public string DestinationDatabaseName { get; set; } = string.Empty;
    public string DestinationAuthMode { get; set; } = "Integrated Security";
    public string DestinationUserName { get; set; } = string.Empty;
    public string DestinationPassword { get; set; } = string.Empty;
    public bool DestinationTrustCertificate { get; set; }

    public bool PreserveIdentity { get; set; } = true;
    public bool ScriptFunctionsAndTvfs { get; set; } = true;
    public bool PreserveIndexes { get; set; } = true;
    public bool PreserveForeignKeys { get; set; } = true;

    public int BulkRowLimit { get; set; } = 1000;
    public string FilterText { get; set; } = string.Empty;
    public bool SelectAllChecked { get; set; }

    public List<TableSelectionItem> Tables { get; set; } = new();
}
