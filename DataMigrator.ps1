#requires -Version 5.1
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Data

[System.Windows.Forms.Application]::EnableVisualStyles()

$script:DefaultRowLimit = 1000
$script:ServerHistoryPath = Join-Path -Path $PSScriptRoot -ChildPath "server-history.json"
$script:CancelRequested = $false

function Write-Log {
  param(
    [Parameter(Mandatory = $true)] [System.Windows.Forms.RichTextBox] $LogBox,
    [Parameter(Mandatory = $true)] [string] $Message,
    [ValidateSet("info", "warning", "error", "success")] [string] $Level = "info"
  )

  $color = [System.Drawing.Color]::Black
  switch ($Level) {
    "warning" { $color = [System.Drawing.Color]::DarkOrange }
    "error" { $color = [System.Drawing.Color]::Firebrick }
    "success" { $color = [System.Drawing.Color]::ForestGreen }
  }

  $timestamp = (Get-Date).ToString("HH:mm:ss")
  $line = "[$timestamp] $Message"

  $LogBox.SelectionStart = $LogBox.TextLength
  $LogBox.SelectionLength = 0
  $LogBox.SelectionColor = $color
  $LogBox.AppendText($line + [Environment]::NewLine)
  $LogBox.SelectionColor = $LogBox.ForeColor
  $LogBox.ScrollToCaret()
  [System.Windows.Forms.Application]::DoEvents()
}

function Load-ServerHistory {
  if (-not (Test-Path -Path $script:ServerHistoryPath)) {
    return @("ProdSql01", "TestSql01")
  }

  try {
    $raw = Get-Content -Path $script:ServerHistoryPath -Raw
    if ([string]::IsNullOrWhiteSpace($raw)) { return @("ProdSql01", "TestSql01") }
    $parsed = $raw | ConvertFrom-Json
    if ($parsed -is [System.Array]) { return $parsed }
    if ($parsed) { return @($parsed) }
  } catch {
  }

  return @("ProdSql01", "TestSql01")
}

function Save-ServerHistory {
  param([System.Collections.ArrayList] $Items)
  try {
    ($Items.ToArray() | Select-Object -Unique | Select-Object -First 20) | ConvertTo-Json | Set-Content -Path $script:ServerHistoryPath
  } catch {
  }
}

function Update-ServerHistory {
  param(
    [System.Windows.Forms.ComboBox] $Combo,
    [string] $ServerName,
    [System.Collections.ArrayList] $History
  )

  if ([string]::IsNullOrWhiteSpace($ServerName)) { return }
  if (-not $History.Contains($ServerName)) {
    [void]$History.Insert(0, $ServerName)
    Save-ServerHistory -Items $History
    $Combo.Items.Clear()
    foreach ($entry in $History) { [void]$Combo.Items.Add($entry) }
  }
}

function New-SqlConnectionString {
  param(
    [Parameter(Mandatory = $true)] [string] $Server,
    [Parameter(Mandatory = $true)] [string] $Database,
    [Parameter(Mandatory = $true)] [bool] $UseIntegrated,
    [string] $Username,
    [string] $Password
  )

  $builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
  $builder["Data Source"] = $Server
  $builder["Initial Catalog"] = $Database
  $builder["Integrated Security"] = $UseIntegrated
  $builder["Connect Timeout"] = 15
  $builder["Application Name"] = "SqlDataMigrator"

  if (-not $UseIntegrated) {
    $builder["User ID"] = $Username
    $builder["Password"] = $Password
  }

  return $builder.ConnectionString
}

function Invoke-SqlQuery {
  param(
    [Parameter(Mandatory = $true)] [string] $ConnectionString,
    [Parameter(Mandatory = $true)] [string] $Query,
    [hashtable] $Parameters
  )

  $conn = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
  try {
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $Query
    $cmd.CommandTimeout = 120

    if ($Parameters) {
      foreach ($name in $Parameters.Keys) {
        $param = $cmd.Parameters.Add("@$name", [System.Data.SqlDbType]::Variant)
        $param.Value = $Parameters[$name]
      }
    }

    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
    $table = New-Object System.Data.DataTable
    [void]$adapter.Fill($table)
    return $table
  } finally {
    $conn.Dispose()
  }
}

function Invoke-SqlNonQuery {
  param(
    [Parameter(Mandatory = $true)] [string] $ConnectionString,
    [Parameter(Mandatory = $true)] [string] $Query
  )

  $conn = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
  try {
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $Query
    $cmd.CommandTimeout = 120
    [void]$cmd.ExecuteNonQuery()
  } finally {
    $conn.Dispose()
  }
}

function Get-ColumnTypeSql {
  param([System.Data.DataRow] $Column)

  $type = [string]$Column.TypeName
  $maxLength = [int]$Column.max_length
  $precision = [int]$Column.precision
  $scale = [int]$Column.scale

  switch ($type.ToLowerInvariant()) {
    "varchar" {
      if ($maxLength -eq -1) { return "varchar(max)" }
      return "varchar($maxLength)"
    }
    "nvarchar" {
      if ($maxLength -eq -1) { return "nvarchar(max)" }
      return "nvarchar($([int]($maxLength / 2)))"
    }
    "char" { return "char($maxLength)" }
    "nchar" { return "nchar($([int]($maxLength / 2)))" }
    "varbinary" {
      if ($maxLength -eq -1) { return "varbinary(max)" }
      return "varbinary($maxLength)"
    }
    "binary" { return "binary($maxLength)" }
    "decimal" { return "decimal($precision,$scale)" }
    "numeric" { return "numeric($precision,$scale)" }
    "datetime2" { return "datetime2($scale)" }
    "datetimeoffset" { return "datetimeoffset($scale)" }
    "time" { return "time($scale)" }
    default { return $type }
  }
}

function Ensure-TableSchema {
  param(
    [string] $SourceConn,
    [string] $DestConn,
    [string] $SchemaName,
    [string] $TableName,
    [System.Windows.Forms.RichTextBox] $LogBox
  )

  $escapedSchema = $SchemaName.Replace("'", "''")
  $escapedTable = $TableName.Replace("'", "''")

  $existsQuery = @"
SELECT COUNT(1)
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE s.name = '$escapedSchema' AND t.name = '$escapedTable';
"@
  $existsResult = Invoke-SqlQuery -ConnectionString $DestConn -Query $existsQuery
  if ([int]$existsResult.Rows[0][0] -gt 0) {
    return
  }

  $colQuery = @"
SELECT
  c.name,
  ty.name AS TypeName,
  c.max_length,
  c.precision,
  c.scale,
  c.is_nullable,
  c.is_identity
FROM sys.columns c
JOIN sys.tables t ON t.object_id = c.object_id
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.types ty ON ty.user_type_id = c.user_type_id
WHERE s.name = '$escapedSchema'
  AND t.name = '$escapedTable'
  AND c.is_computed = 0
ORDER BY c.column_id;
"@
  $cols = Invoke-SqlQuery -ConnectionString $SourceConn -Query $colQuery
  if ($cols.Rows.Count -eq 0) {
    throw "No columns found for [$SchemaName].[$TableName]."
  }

  $columnDefs = New-Object System.Collections.Generic.List[string]
  foreach ($row in $cols.Rows) {
    $colName = [string]$row.name
    $typeSql = Get-ColumnTypeSql -Column $row
    $nullable = if ([bool]$row.is_nullable) { "NULL" } else { "NOT NULL" }
    $identity = if ([bool]$row.is_identity) { " IDENTITY(1,1)" } else { "" }
    $columnDefs.Add("[$colName] $typeSql$identity $nullable")
  }

  $schemaSql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = '$escapedSchema')
BEGIN
  EXEC('CREATE SCHEMA [$SchemaName] AUTHORIZATION [dbo]');
END;
"@

  $createSql = "CREATE TABLE [$SchemaName].[$TableName] (`n  " + ($columnDefs -join ",`n  ") + "`n);"

  Invoke-SqlNonQuery -ConnectionString $DestConn -Query $schemaSql
  Invoke-SqlNonQuery -ConnectionString $DestConn -Query $createSql
  Write-Log -LogBox $LogBox -Message "Created table [$SchemaName].[$TableName]" -Level "success"
}

function Copy-TableRows {
  param(
    [string] $SourceConn,
    [string] $DestConn,
    [string] $SchemaName,
    [string] $TableName,
    [int] $TopRows,
    [bool] $KeepIdentity,
    [System.Windows.Forms.RichTextBox] $LogBox
  )

  $quoted = "[$SchemaName].[$TableName]"
  $selectSql = "SELECT TOP ($TopRows) * FROM $quoted WITH (NOLOCK);"
  $truncateSql = "TRUNCATE TABLE $quoted;"

  $data = Invoke-SqlQuery -ConnectionString $SourceConn -Query $selectSql

  try {
    Invoke-SqlNonQuery -ConnectionString $DestConn -Query $truncateSql
  } catch {
    Invoke-SqlNonQuery -ConnectionString $DestConn -Query "DELETE FROM $quoted;"
  }

  $options = [System.Data.SqlClient.SqlBulkCopyOptions]::TableLock
  if ($KeepIdentity) {
    $options = $options -bor [System.Data.SqlClient.SqlBulkCopyOptions]::KeepIdentity
  }

  $bulkConn = New-Object System.Data.SqlClient.SqlConnection($DestConn)
  try {
    $bulkConn.Open()
    $bulk = New-Object System.Data.SqlClient.SqlBulkCopy($bulkConn, $options, $null)
    $bulk.DestinationTableName = $quoted
    $bulk.BatchSize = 10000
    $bulk.BulkCopyTimeout = 0

    foreach ($col in $data.Columns) {
      [void]$bulk.ColumnMappings.Add($col.ColumnName, $col.ColumnName)
    }

    $bulk.WriteToServer($data)
    Write-Log -LogBox $LogBox -Message "Copied $($data.Rows.Count) row(s) into $quoted" -Level "success"
  } finally {
    $bulkConn.Dispose()
  }
}

$history = New-Object System.Collections.ArrayList
foreach ($entry in (Load-ServerHistory)) {
  if (-not [string]::IsNullOrWhiteSpace($entry)) { [void]$history.Add([string]$entry) }
}

$form = New-Object System.Windows.Forms.Form
$form.Text = "Prod -> Test SQL Copier (MVP)"
$form.Size = New-Object System.Drawing.Size(1240, 760)
$form.StartPosition = "CenterScreen"

$grpSource = New-Object System.Windows.Forms.GroupBox
$grpSource.Text = "Source"
$grpSource.Location = New-Object System.Drawing.Point(10, 10)
$grpSource.Size = New-Object System.Drawing.Size(600, 190)
$form.Controls.Add($grpSource)

$lblSrcServer = New-Object System.Windows.Forms.Label
$lblSrcServer.Text = "Server:"
$lblSrcServer.Location = New-Object System.Drawing.Point(12, 30)
$lblSrcServer.AutoSize = $true
$grpSource.Controls.Add($lblSrcServer)

$cmbSrcServer = New-Object System.Windows.Forms.ComboBox
$cmbSrcServer.Location = New-Object System.Drawing.Point(120, 26)
$cmbSrcServer.Size = New-Object System.Drawing.Size(320, 24)
$cmbSrcServer.DropDownStyle = "DropDown"
foreach ($entry in $history) { [void]$cmbSrcServer.Items.Add($entry) }
if ($cmbSrcServer.Items.Count -gt 0) { $cmbSrcServer.SelectedIndex = 0 }
$grpSource.Controls.Add($cmbSrcServer)

$btnSrcTest = New-Object System.Windows.Forms.Button
$btnSrcTest.Text = "Test"
$btnSrcTest.Location = New-Object System.Drawing.Point(455, 25)
$btnSrcTest.Size = New-Object System.Drawing.Size(120, 27)
$grpSource.Controls.Add($btnSrcTest)

$lblSrcDb = New-Object System.Windows.Forms.Label
$lblSrcDb.Text = "Database:"
$lblSrcDb.Location = New-Object System.Drawing.Point(12, 64)
$lblSrcDb.AutoSize = $true
$grpSource.Controls.Add($lblSrcDb)

$cmbSrcDb = New-Object System.Windows.Forms.ComboBox
$cmbSrcDb.Location = New-Object System.Drawing.Point(120, 60)
$cmbSrcDb.Size = New-Object System.Drawing.Size(320, 24)
$cmbSrcDb.DropDownStyle = "DropDownList"
$grpSource.Controls.Add($cmbSrcDb)

$btnSrcLoadDb = New-Object System.Windows.Forms.Button
$btnSrcLoadDb.Text = "Load DBs"
$btnSrcLoadDb.Location = New-Object System.Drawing.Point(455, 59)
$btnSrcLoadDb.Size = New-Object System.Drawing.Size(120, 27)
$grpSource.Controls.Add($btnSrcLoadDb)

$rbSrcIntegrated = New-Object System.Windows.Forms.RadioButton
$rbSrcIntegrated.Text = "Integrated"
$rbSrcIntegrated.Location = New-Object System.Drawing.Point(120, 95)
$rbSrcIntegrated.AutoSize = $true
$rbSrcIntegrated.Checked = $true
$grpSource.Controls.Add($rbSrcIntegrated)

$rbSrcSql = New-Object System.Windows.Forms.RadioButton
$rbSrcSql.Text = "SQL Login"
$rbSrcSql.Location = New-Object System.Drawing.Point(230, 95)
$rbSrcSql.AutoSize = $true
$grpSource.Controls.Add($rbSrcSql)

$lblSrcUser = New-Object System.Windows.Forms.Label
$lblSrcUser.Text = "User:"
$lblSrcUser.Location = New-Object System.Drawing.Point(12, 126)
$lblSrcUser.AutoSize = $true
$grpSource.Controls.Add($lblSrcUser)

$txtSrcUser = New-Object System.Windows.Forms.TextBox
$txtSrcUser.Location = New-Object System.Drawing.Point(120, 122)
$txtSrcUser.Size = New-Object System.Drawing.Size(180, 24)
$txtSrcUser.Enabled = $false
$grpSource.Controls.Add($txtSrcUser)

$lblSrcPass = New-Object System.Windows.Forms.Label
$lblSrcPass.Text = "Password:"
$lblSrcPass.Location = New-Object System.Drawing.Point(315, 126)
$lblSrcPass.AutoSize = $true
$grpSource.Controls.Add($lblSrcPass)

$txtSrcPass = New-Object System.Windows.Forms.TextBox
$txtSrcPass.Location = New-Object System.Drawing.Point(390, 122)
$txtSrcPass.Size = New-Object System.Drawing.Size(185, 24)
$txtSrcPass.UseSystemPasswordChar = $true
$txtSrcPass.Enabled = $false
$grpSource.Controls.Add($txtSrcPass)

$grpDest = New-Object System.Windows.Forms.GroupBox
$grpDest.Text = "Destination"
$grpDest.Location = New-Object System.Drawing.Point(620, 10)
$grpDest.Size = New-Object System.Drawing.Size(600, 190)
$form.Controls.Add($grpDest)

$lblDstServer = New-Object System.Windows.Forms.Label
$lblDstServer.Text = "Server:"
$lblDstServer.Location = New-Object System.Drawing.Point(12, 30)
$lblDstServer.AutoSize = $true
$grpDest.Controls.Add($lblDstServer)

$cmbDstServer = New-Object System.Windows.Forms.ComboBox
$cmbDstServer.Location = New-Object System.Drawing.Point(120, 26)
$cmbDstServer.Size = New-Object System.Drawing.Size(320, 24)
$cmbDstServer.DropDownStyle = "DropDown"
foreach ($entry in $history) { [void]$cmbDstServer.Items.Add($entry) }
if ($cmbDstServer.Items.Count -gt 1) { $cmbDstServer.SelectedIndex = 1 }
$grpDest.Controls.Add($cmbDstServer)

$btnDstTest = New-Object System.Windows.Forms.Button
$btnDstTest.Text = "Test"
$btnDstTest.Location = New-Object System.Drawing.Point(455, 25)
$btnDstTest.Size = New-Object System.Drawing.Size(120, 27)
$grpDest.Controls.Add($btnDstTest)

$lblDstDb = New-Object System.Windows.Forms.Label
$lblDstDb.Text = "New DB Name:"
$lblDstDb.Location = New-Object System.Drawing.Point(12, 64)
$lblDstDb.AutoSize = $true
$grpDest.Controls.Add($lblDstDb)

$txtDstDb = New-Object System.Windows.Forms.TextBox
$txtDstDb.Location = New-Object System.Drawing.Point(120, 60)
$txtDstDb.Size = New-Object System.Drawing.Size(320, 24)
$txtDstDb.Text = "TestDb_Copy"
$grpDest.Controls.Add($txtDstDb)

$rbDstIntegrated = New-Object System.Windows.Forms.RadioButton
$rbDstIntegrated.Text = "Integrated"
$rbDstIntegrated.Location = New-Object System.Drawing.Point(120, 95)
$rbDstIntegrated.AutoSize = $true
$rbDstIntegrated.Checked = $true
$grpDest.Controls.Add($rbDstIntegrated)

$rbDstSql = New-Object System.Windows.Forms.RadioButton
$rbDstSql.Text = "SQL Login"
$rbDstSql.Location = New-Object System.Drawing.Point(230, 95)
$rbDstSql.AutoSize = $true
$grpDest.Controls.Add($rbDstSql)

$lblDstUser = New-Object System.Windows.Forms.Label
$lblDstUser.Text = "User:"
$lblDstUser.Location = New-Object System.Drawing.Point(12, 126)
$lblDstUser.AutoSize = $true
$grpDest.Controls.Add($lblDstUser)

$txtDstUser = New-Object System.Windows.Forms.TextBox
$txtDstUser.Location = New-Object System.Drawing.Point(120, 122)
$txtDstUser.Size = New-Object System.Drawing.Size(180, 24)
$txtDstUser.Enabled = $false
$grpDest.Controls.Add($txtDstUser)

$lblDstPass = New-Object System.Windows.Forms.Label
$lblDstPass.Text = "Password:"
$lblDstPass.Location = New-Object System.Drawing.Point(315, 126)
$lblDstPass.AutoSize = $true
$grpDest.Controls.Add($lblDstPass)

$txtDstPass = New-Object System.Windows.Forms.TextBox
$txtDstPass.Location = New-Object System.Drawing.Point(390, 122)
$txtDstPass.Size = New-Object System.Drawing.Size(185, 24)
$txtDstPass.UseSystemPasswordChar = $true
$txtDstPass.Enabled = $false
$grpDest.Controls.Add($txtDstPass)

$lblFilter = New-Object System.Windows.Forms.Label
$lblFilter.Text = "Filter (schema/table):"
$lblFilter.Location = New-Object System.Drawing.Point(10, 214)
$lblFilter.AutoSize = $true
$form.Controls.Add($lblFilter)

$txtFilter = New-Object System.Windows.Forms.TextBox
$txtFilter.Location = New-Object System.Drawing.Point(145, 210)
$txtFilter.Size = New-Object System.Drawing.Size(280, 24)
$form.Controls.Add($txtFilter)

$chkSelectAll = New-Object System.Windows.Forms.CheckBox
$chkSelectAll.Text = "Select All"
$chkSelectAll.Location = New-Object System.Drawing.Point(440, 212)
$chkSelectAll.AutoSize = $true
$form.Controls.Add($chkSelectAll)

$btnLoadTables = New-Object System.Windows.Forms.Button
$btnLoadTables.Text = "Load Tables"
$btnLoadTables.Location = New-Object System.Drawing.Point(540, 209)
$btnLoadTables.Size = New-Object System.Drawing.Size(120, 27)
$form.Controls.Add($btnLoadTables)

$chkKeepIdentity = New-Object System.Windows.Forms.CheckBox
$chkKeepIdentity.Text = "Preserve PKs (Keep Identity)"
$chkKeepIdentity.Location = New-Object System.Drawing.Point(680, 212)
$chkKeepIdentity.AutoSize = $true
$chkKeepIdentity.Checked = $true
$form.Controls.Add($chkKeepIdentity)

$grid = New-Object System.Windows.Forms.DataGridView
$grid.Location = New-Object System.Drawing.Point(10, 245)
$grid.Size = New-Object System.Drawing.Size(760, 420)
$grid.AllowUserToAddRows = $false
$grid.AllowUserToDeleteRows = $false
$grid.RowHeadersVisible = $false
$grid.SelectionMode = "FullRowSelect"
$grid.MultiSelect = $false
$grid.AutoGenerateColumns = $false

$colSelect = New-Object System.Windows.Forms.DataGridViewCheckBoxColumn
$colSelect.Name = "Selected"
$colSelect.HeaderText = "Copy"
$colSelect.Width = 55
$grid.Columns.Add($colSelect) | Out-Null

$colSchema = New-Object System.Windows.Forms.DataGridViewTextBoxColumn
$colSchema.Name = "Schema"
$colSchema.HeaderText = "Schema"
$colSchema.Width = 120
$colSchema.ReadOnly = $true
$grid.Columns.Add($colSchema) | Out-Null

$colTable = New-Object System.Windows.Forms.DataGridViewTextBoxColumn
$colTable.Name = "Table"
$colTable.HeaderText = "Table"
$colTable.Width = 220
$colTable.ReadOnly = $true
$grid.Columns.Add($colTable) | Out-Null

$colCount = New-Object System.Windows.Forms.DataGridViewTextBoxColumn
$colCount.Name = "RowCount"
$colCount.HeaderText = "Source Rows"
$colCount.Width = 130
$colCount.ReadOnly = $true
$grid.Columns.Add($colCount) | Out-Null

$colLimit = New-Object System.Windows.Forms.DataGridViewTextBoxColumn
$colLimit.Name = "RowLimit"
$colLimit.HeaderText = "Row Limit"
$colLimit.Width = 120
$grid.Columns.Add($colLimit) | Out-Null

$form.Controls.Add($grid)

$lblLog = New-Object System.Windows.Forms.Label
$lblLog.Text = "Transfer Log"
$lblLog.Location = New-Object System.Drawing.Point(780, 245)
$lblLog.AutoSize = $true
$form.Controls.Add($lblLog)

$logBox = New-Object System.Windows.Forms.RichTextBox
$logBox.Location = New-Object System.Drawing.Point(780, 268)
$logBox.Size = New-Object System.Drawing.Size(440, 355)
$logBox.ReadOnly = $true
$logBox.BackColor = [System.Drawing.Color]::White
$form.Controls.Add($logBox)

$btnStart = New-Object System.Windows.Forms.Button
$btnStart.Text = "Start Transfer"
$btnStart.Location = New-Object System.Drawing.Point(780, 632)
$btnStart.Size = New-Object System.Drawing.Size(130, 33)
$form.Controls.Add($btnStart)

$btnCancel = New-Object System.Windows.Forms.Button
$btnCancel.Text = "Cancel"
$btnCancel.Location = New-Object System.Drawing.Point(920, 632)
$btnCancel.Size = New-Object System.Drawing.Size(100, 33)
$btnCancel.Enabled = $false
$form.Controls.Add($btnCancel)

$progress = New-Object System.Windows.Forms.ProgressBar
$progress.Location = New-Object System.Drawing.Point(1030, 636)
$progress.Size = New-Object System.Drawing.Size(190, 24)
$progress.Minimum = 0
$progress.Maximum = 100
$form.Controls.Add($progress)

$lblStatus = New-Object System.Windows.Forms.Label
$lblStatus.Text = "Ready"
$lblStatus.Location = New-Object System.Drawing.Point(10, 676)
$lblStatus.Size = New-Object System.Drawing.Size(1210, 30)
$form.Controls.Add($lblStatus)

$allTables = New-Object System.Collections.Generic.List[object]

function Refresh-GridFromFilter {
  $grid.Rows.Clear()
  $filter = $txtFilter.Text.Trim().ToLowerInvariant()

  foreach ($row in $allTables) {
    $name = "$($row.Schema).$($row.Table)".ToLowerInvariant()
    if ([string]::IsNullOrWhiteSpace($filter) -or $name.Contains($filter)) {
      $idx = $grid.Rows.Add()
      $grid.Rows[$idx].Cells["Selected"].Value = [bool]$row.Selected
      $grid.Rows[$idx].Cells["Schema"].Value = [string]$row.Schema
      $grid.Rows[$idx].Cells["Table"].Value = [string]$row.Table
      $grid.Rows[$idx].Cells["RowCount"].Value = [string]$row.RowCount
      $grid.Rows[$idx].Cells["RowLimit"].Value = [string]$row.RowLimit
    }
  }
}

function Set-SqlAuthEnabled {
  param(
    [System.Windows.Forms.RadioButton] $SqlRadio,
    [System.Windows.Forms.TextBox] $User,
    [System.Windows.Forms.TextBox] $Pass
  )
  $enabled = $SqlRadio.Checked
  $User.Enabled = $enabled
  $Pass.Enabled = $enabled
}

$rbSrcIntegrated.Add_CheckedChanged({ Set-SqlAuthEnabled -SqlRadio $rbSrcSql -User $txtSrcUser -Pass $txtSrcPass })
$rbSrcSql.Add_CheckedChanged({ Set-SqlAuthEnabled -SqlRadio $rbSrcSql -User $txtSrcUser -Pass $txtSrcPass })
$rbDstIntegrated.Add_CheckedChanged({ Set-SqlAuthEnabled -SqlRadio $rbDstSql -User $txtDstUser -Pass $txtDstPass })
$rbDstSql.Add_CheckedChanged({ Set-SqlAuthEnabled -SqlRadio $rbDstSql -User $txtDstUser -Pass $txtDstPass })

$btnSrcTest.Add_Click({
  try {
    $conn = New-SqlConnectionString -Server $cmbSrcServer.Text.Trim() -Database "master" -UseIntegrated $rbSrcIntegrated.Checked -Username $txtSrcUser.Text -Password $txtSrcPass.Text
    [void](Invoke-SqlQuery -ConnectionString $conn -Query "SELECT @@SERVERNAME AS ServerName;")
    Update-ServerHistory -Combo $cmbSrcServer -ServerName $cmbSrcServer.Text.Trim() -History $history
    Write-Log -LogBox $logBox -Message "Source server connection OK." -Level "success"
  } catch {
    Write-Log -LogBox $logBox -Message "Source test failed: $($_.Exception.Message)" -Level "error"
  }
})

$btnDstTest.Add_Click({
  try {
    $conn = New-SqlConnectionString -Server $cmbDstServer.Text.Trim() -Database "master" -UseIntegrated $rbDstIntegrated.Checked -Username $txtDstUser.Text -Password $txtDstPass.Text
    [void](Invoke-SqlQuery -ConnectionString $conn -Query "SELECT @@SERVERNAME AS ServerName;")
    Update-ServerHistory -Combo $cmbDstServer -ServerName $cmbDstServer.Text.Trim() -History $history
    Write-Log -LogBox $logBox -Message "Destination server connection OK." -Level "success"
  } catch {
    Write-Log -LogBox $logBox -Message "Destination test failed: $($_.Exception.Message)" -Level "error"
  }
})

$btnSrcLoadDb.Add_Click({
  try {
    $conn = New-SqlConnectionString -Server $cmbSrcServer.Text.Trim() -Database "master" -UseIntegrated $rbSrcIntegrated.Checked -Username $txtSrcUser.Text -Password $txtSrcPass.Text
    $dbs = Invoke-SqlQuery -ConnectionString $conn -Query "SELECT name FROM sys.databases WHERE database_id > 4 ORDER BY name;"
    $cmbSrcDb.Items.Clear()
    foreach ($row in $dbs.Rows) { [void]$cmbSrcDb.Items.Add([string]$row.name) }
    if ($cmbSrcDb.Items.Count -gt 0) { $cmbSrcDb.SelectedIndex = 0 }
    Write-Log -LogBox $logBox -Message "Loaded $($dbs.Rows.Count) source database(s)." -Level "info"
  } catch {
    Write-Log -LogBox $logBox -Message "Load DBs failed: $($_.Exception.Message)" -Level "error"
  }
})

$btnLoadTables.Add_Click({
  if ([string]::IsNullOrWhiteSpace($cmbSrcDb.Text)) {
    [System.Windows.Forms.MessageBox]::Show("Choose a source database first.", "Missing source DB") | Out-Null
    return
  }

  try {
    $sourceConn = New-SqlConnectionString -Server $cmbSrcServer.Text.Trim() -Database $cmbSrcDb.Text.Trim() -UseIntegrated $rbSrcIntegrated.Checked -Username $txtSrcUser.Text -Password $txtSrcPass.Text
    $sql = @"
SELECT
  s.name AS SchemaName,
  t.name AS TableName,
  SUM(COALESCE(p.row_count, 0)) AS RowCount
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
LEFT JOIN sys.dm_db_partition_stats p
  ON p.object_id = t.object_id
  AND p.index_id IN (0, 1)
WHERE t.is_ms_shipped = 0
GROUP BY s.name, t.name
ORDER BY s.name, t.name;
"@
    $result = Invoke-SqlQuery -ConnectionString $sourceConn -Query $sql

    $allTables.Clear()
    foreach ($r in $result.Rows) {
      $item = [PSCustomObject]@{
        Selected = $false
        Schema   = [string]$r.SchemaName
        Table    = [string]$r.TableName
        RowCount = [int64]$r.RowCount
        RowLimit = $script:DefaultRowLimit
      }
      [void]$allTables.Add($item)
    }

    Refresh-GridFromFilter
    Write-Log -LogBox $logBox -Message "Loaded $($result.Rows.Count) table(s)." -Level "success"
  } catch {
    Write-Log -LogBox $logBox -Message "Load Tables failed: $($_.Exception.Message)" -Level "error"
  }
})

$txtFilter.Add_TextChanged({ Refresh-GridFromFilter })

$chkSelectAll.Add_CheckedChanged({
  foreach ($row in $grid.Rows) {
    $row.Cells["Selected"].Value = $chkSelectAll.Checked
  }
})

$grid.Add_CellValueChanged({
  param($sender, $e)
  if ($e.RowIndex -lt 0) { return }

  $schema = [string]$grid.Rows[$e.RowIndex].Cells["Schema"].Value
  $table = [string]$grid.Rows[$e.RowIndex].Cells["Table"].Value
  $entry = $allTables | Where-Object { $_.Schema -eq $schema -and $_.Table -eq $table } | Select-Object -First 1
  if (-not $entry) { return }

  if ($e.ColumnIndex -eq $grid.Columns["Selected"].Index) {
    $entry.Selected = [bool]$grid.Rows[$e.RowIndex].Cells["Selected"].Value
  }

  if ($e.ColumnIndex -eq $grid.Columns["RowLimit"].Index) {
    $val = [string]$grid.Rows[$e.RowIndex].Cells["RowLimit"].Value
    $parsed = 0
    if ([int]::TryParse($val, [ref]$parsed) -and $parsed -gt 0) {
      $entry.RowLimit = $parsed
    } else {
      $entry.RowLimit = $script:DefaultRowLimit
      $grid.Rows[$e.RowIndex].Cells["RowLimit"].Value = $script:DefaultRowLimit
    }
  }
})

$grid.Add_CurrentCellDirtyStateChanged({
  if ($grid.IsCurrentCellDirty) {
    $grid.CommitEdit([System.Windows.Forms.DataGridViewDataErrorContexts]::Commit)
  }
})

$btnCancel.Add_Click({
  $script:CancelRequested = $true
  Write-Log -LogBox $logBox -Message "Cancellation requested. Finishing current table..." -Level "warning"
})

$btnStart.Add_Click({
  if ([string]::IsNullOrWhiteSpace($cmbSrcDb.Text)) {
    [System.Windows.Forms.MessageBox]::Show("Select source database.", "Missing source DB") | Out-Null
    return
  }
  if ([string]::IsNullOrWhiteSpace($txtDstDb.Text)) {
    [System.Windows.Forms.MessageBox]::Show("Enter destination database name.", "Missing destination DB") | Out-Null
    return
  }

  foreach ($row in $grid.Rows) {
    $schema = [string]$row.Cells["Schema"].Value
    $table = [string]$row.Cells["Table"].Value
    $entry = $allTables | Where-Object { $_.Schema -eq $schema -and $_.Table -eq $table } | Select-Object -First 1
    if ($entry) {
      $entry.Selected = [bool]$row.Cells["Selected"].Value
      $limitText = [string]$row.Cells["RowLimit"].Value
      $parsed = 0
      if ([int]::TryParse($limitText, [ref]$parsed) -and $parsed -gt 0) { $entry.RowLimit = $parsed }
    }
  }

  $selected = @($allTables | Where-Object { $_.Selected })
  if ($selected.Count -eq 0) {
    [System.Windows.Forms.MessageBox]::Show("Select at least one table.", "Nothing selected") | Out-Null
    return
  }

  $script:CancelRequested = $false
  $btnStart.Enabled = $false
  $btnCancel.Enabled = $true
  $progress.Value = 0

  $sourceConn = $null
  $destMasterConn = $null
  $destDbConn = $null

  try {
    $sourceConn = New-SqlConnectionString -Server $cmbSrcServer.Text.Trim() -Database $cmbSrcDb.Text.Trim() -UseIntegrated $rbSrcIntegrated.Checked -Username $txtSrcUser.Text -Password $txtSrcPass.Text
    $destMasterConn = New-SqlConnectionString -Server $cmbDstServer.Text.Trim() -Database "master" -UseIntegrated $rbDstIntegrated.Checked -Username $txtDstUser.Text -Password $txtDstPass.Text
    $destDbConn = New-SqlConnectionString -Server $cmbDstServer.Text.Trim() -Database $txtDstDb.Text.Trim() -UseIntegrated $rbDstIntegrated.Checked -Username $txtDstUser.Text -Password $txtDstPass.Text

    Update-ServerHistory -Combo $cmbSrcServer -ServerName $cmbSrcServer.Text.Trim() -History $history
    Update-ServerHistory -Combo $cmbDstServer -ServerName $cmbDstServer.Text.Trim() -History $history

    $phaseMax = $selected.Count + 2
    $phaseStep = 0

    Write-Log -LogBox $logBox -Message "Phase 1/3: Ensure destination database exists..." -Level "info"
    $safeDbName = $txtDstDb.Text.Trim().Replace("]", "]]")
    $createDbSql = @"
IF DB_ID('$safeDbName') IS NULL
BEGIN
  CREATE DATABASE [$safeDbName];
END;
"@
    Invoke-SqlNonQuery -ConnectionString $destMasterConn -Query $createDbSql
    $phaseStep++
    $progress.Value = [Math]::Min(100, [int](($phaseStep / $phaseMax) * 100))

    Write-Log -LogBox $logBox -Message "Phase 2/3: Creating table schemas (no PK/FK/indexes in MVP)..." -Level "info"
    foreach ($t in $selected) {
      if ($script:CancelRequested) { throw "Cancelled by user." }
      Ensure-TableSchema -SourceConn $sourceConn -DestConn $destDbConn -SchemaName $t.Schema -TableName $t.Table -LogBox $logBox
    }
    $phaseStep++
    $progress.Value = [Math]::Min(100, [int](($phaseStep / $phaseMax) * 100))

    Write-Log -LogBox $logBox -Message "Phase 3/3: Copying table data with SqlBulkCopy..." -Level "info"
    $successCount = 0
    $failCount = 0

    foreach ($t in $selected) {
      if ($script:CancelRequested) { throw "Cancelled by user." }

      try {
        $lblStatus.Text = "Copying [$($t.Schema)].[$($t.Table)] ..."
        Copy-TableRows -SourceConn $sourceConn -DestConn $destDbConn -SchemaName $t.Schema -TableName $t.Table -TopRows ([int]$t.RowLimit) -KeepIdentity $chkKeepIdentity.Checked -LogBox $logBox
        $successCount++
      } catch {
        $failCount++
        Write-Log -LogBox $logBox -Message "Table [$($t.Schema)].[$($t.Table)] failed: $($_.Exception.Message)" -Level "error"
      }

      $phaseStep++
      $progress.Value = [Math]::Min(100, [int](($phaseStep / $phaseMax) * 100))
      [System.Windows.Forms.Application]::DoEvents()
    }

    $lblStatus.Text = "Transfer complete."
    Write-Log -LogBox $logBox -Message "Done. Success: $successCount, Failed: $failCount" -Level "success"
  } catch {
    $lblStatus.Text = "Transfer stopped."
    Write-Log -LogBox $logBox -Message "Transfer stopped: $($_.Exception.Message)" -Level "error"
  } finally {
    $btnStart.Enabled = $true
    $btnCancel.Enabled = $false
  }
})

[void]$form.ShowDialog()
