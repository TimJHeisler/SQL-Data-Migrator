namespace SQLDataMigrator;

public sealed class AppSettings
{
    public List<string> SourceServerHistory { get; set; } = new();
    public List<string> DestinationServerHistory { get; set; } = new();
    public string SourceServer { get; set; } = string.Empty;
    public string DestinationServer { get; set; } = string.Empty;
    public string SourceDatabase { get; set; } = string.Empty;
    public string DestinationDatabaseName { get; set; } = "TestDb_Copy";
    public string SourceAuthMode { get; set; } = "Integrated Security";
    public string DestinationAuthMode { get; set; } = "Integrated Security";
    public string SourceUserName { get; set; } = string.Empty;
    public string DestinationUserName { get; set; } = string.Empty;
    public bool SourceTrustCertificate { get; set; }
    public bool DestinationTrustCertificate { get; set; }
    public bool PreserveIdentity { get; set; } = true;
    public bool ScriptFunctionsAndTvfs { get; set; } = true;
    public bool PreserveIndexes { get; set; } = true;
    public bool PreserveForeignKeys { get; set; } = true;
    public int LastBulkRowLimit { get; set; } = 1000;
    public string LastProjectFilePath { get; set; } = string.Empty;
}
