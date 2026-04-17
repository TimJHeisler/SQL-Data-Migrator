namespace SQLDataMigrator;

partial class Form1
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        grpSource = new GroupBox();
        btnLoadSourceDbs = new Button();
        chkSourceTrustCert = new CheckBox();
        txtSourcePassword = new TextBox();
        txtSourceUser = new TextBox();
        lblSourcePassword = new Label();
        lblSourceUser = new Label();
        cmbSourceAuth = new ComboBox();
        lblSourceAuth = new Label();
        cmbSourceDatabase = new ComboBox();
        lblSourceDatabase = new Label();
        btnTestSource = new Button();
        cmbSourceServer = new ComboBox();
        lblSourceServer = new Label();
        grpDestination = new GroupBox();
        chkDestinationTrustCert = new CheckBox();
        txtDestinationPassword = new TextBox();
        txtDestinationUser = new TextBox();
        lblDestinationPassword = new Label();
        lblDestinationUser = new Label();
        cmbDestinationAuth = new ComboBox();
        lblDestinationAuth = new Label();
        txtDestinationDatabase = new TextBox();
        lblDestinationDatabase = new Label();
        btnTestDestination = new Button();
        cmbDestinationServer = new ComboBox();
        lblDestinationServer = new Label();
        lblFilter = new Label();
        txtFilter = new TextBox();
        chkSelectAll = new CheckBox();
        btnLoadTables = new Button();
        chkKeepIdentity = new CheckBox();
        chkScriptFunctions = new CheckBox();
        chkPreserveIndexes = new CheckBox();
        chkPreserveForeignKeys = new CheckBox();
        lblBulkRowLimit = new Label();
        numBulkRowLimit = new NumericUpDown();
        btnSetLimitAll = new Button();
        btnSetLimitSelected = new Button();
        btnClearLimitAll = new Button();
        btnClearLimitSelected = new Button();
        btnSaveProject = new Button();
        btnLoadProject = new Button();
        gridTables = new DataGridView();
        logBox = new RichTextBox();
        lblLog = new Label();
        btnStartTransfer = new Button();
        btnCancelTransfer = new Button();
        progressTransfer = new ProgressBar();
        lblEta = new Label();
        lblStatus = new Label();
        label1 = new Label();
        splitContainer1 = new SplitContainer();
        btnHelp = new Button();
        pnlHelp = new Panel();
        webHelp = new Microsoft.Web.WebView2.WinForms.WebView2();
        pnlHelpToolbar = new Panel();
        lblZoom = new Label();
        btnZoomReset = new Button();
        btnZoomOut = new Button();
        btnZoomIn = new Button();
        pnlHelpHeader = new Panel();
        lblHelpTitle = new Label();
        btnCloseHelp = new Button();
        grpSource.SuspendLayout();
        grpDestination.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)numBulkRowLimit).BeginInit();
        ((System.ComponentModel.ISupportInitialize)gridTables).BeginInit();
        ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
        splitContainer1.Panel1.SuspendLayout();
        splitContainer1.Panel2.SuspendLayout();
        splitContainer1.SuspendLayout();
        pnlHelp.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)webHelp).BeginInit();
        pnlHelpToolbar.SuspendLayout();
        pnlHelpHeader.SuspendLayout();
        SuspendLayout();
        // 
        // grpSource
        // 
        grpSource.Controls.Add(btnLoadSourceDbs);
        grpSource.Controls.Add(chkSourceTrustCert);
        grpSource.Controls.Add(txtSourcePassword);
        grpSource.Controls.Add(txtSourceUser);
        grpSource.Controls.Add(lblSourcePassword);
        grpSource.Controls.Add(lblSourceUser);
        grpSource.Controls.Add(cmbSourceAuth);
        grpSource.Controls.Add(lblSourceAuth);
        grpSource.Controls.Add(cmbSourceDatabase);
        grpSource.Controls.Add(lblSourceDatabase);
        grpSource.Controls.Add(btnTestSource);
        grpSource.Controls.Add(cmbSourceServer);
        grpSource.Controls.Add(lblSourceServer);
        grpSource.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        grpSource.Location = new Point(15, 15);
        grpSource.Margin = new Padding(4);
        grpSource.Name = "grpSource";
        grpSource.Padding = new Padding(4);
        grpSource.Size = new Size(800, 240);
        grpSource.TabIndex = 0;
        grpSource.TabStop = false;
        grpSource.Text = "Source DB";
        // 
        // btnLoadSourceDbs
        // 
        btnLoadSourceDbs.Location = new Point(656, 80);
        btnLoadSourceDbs.Margin = new Padding(4);
        btnLoadSourceDbs.Name = "btnLoadSourceDbs";
        btnLoadSourceDbs.Size = new Size(128, 36);
        btnLoadSourceDbs.TabIndex = 5;
        btnLoadSourceDbs.Text = "Load DBs";
        btnLoadSourceDbs.UseVisualStyleBackColor = true;
        btnLoadSourceDbs.Click += btnLoadSourceDbs_Click;
        // 
        // chkSourceTrustCert
        // 
        chkSourceTrustCert.AutoSize = true;
        chkSourceTrustCert.Font = new Font("Segoe UI", 9F);
        chkSourceTrustCert.Location = new Point(98, 197);
        chkSourceTrustCert.Margin = new Padding(4);
        chkSourceTrustCert.Name = "chkSourceTrustCert";
        chkSourceTrustCert.Size = new Size(158, 29);
        chkSourceTrustCert.TabIndex = 10;
        chkSourceTrustCert.Text = "Trust Certificate";
        chkSourceTrustCert.UseVisualStyleBackColor = true;
        // 
        // txtSourcePassword
        // 
        txtSourcePassword.Font = new Font("Segoe UI", 9F);
        txtSourcePassword.Location = new Point(98, 156);
        txtSourcePassword.Margin = new Padding(4);
        txtSourcePassword.Name = "txtSourcePassword";
        txtSourcePassword.Size = new Size(286, 31);
        txtSourcePassword.TabIndex = 9;
        // 
        // txtSourceUser
        // 
        txtSourceUser.Font = new Font("Segoe UI", 9F);
        txtSourceUser.Location = new Point(98, 117);
        txtSourceUser.Margin = new Padding(4);
        txtSourceUser.Name = "txtSourceUser";
        txtSourceUser.Size = new Size(286, 31);
        txtSourceUser.TabIndex = 8;
        // 
        // lblSourcePassword
        // 
        lblSourcePassword.AutoSize = true;
        lblSourcePassword.Font = new Font("Segoe UI", 9F);
        lblSourcePassword.Location = new Point(42, 162);
        lblSourcePassword.Margin = new Padding(4, 0, 4, 0);
        lblSourcePassword.Name = "lblSourcePassword";
        lblSourcePassword.Size = new Size(50, 25);
        lblSourcePassword.TabIndex = 0;
        lblSourcePassword.Text = "Pass:";
        // 
        // lblSourceUser
        // 
        lblSourceUser.AutoSize = true;
        lblSourceUser.Font = new Font("Segoe UI", 9F);
        lblSourceUser.Location = new Point(41, 122);
        lblSourceUser.Margin = new Padding(4, 0, 4, 0);
        lblSourceUser.Name = "lblSourceUser";
        lblSourceUser.Size = new Size(51, 25);
        lblSourceUser.TabIndex = 0;
        lblSourceUser.Text = "User:";
        // 
        // cmbSourceAuth
        // 
        cmbSourceAuth.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbSourceAuth.Font = new Font("Segoe UI", 9F);
        cmbSourceAuth.FormattingEnabled = true;
        cmbSourceAuth.Location = new Point(98, 76);
        cmbSourceAuth.Margin = new Padding(4);
        cmbSourceAuth.Name = "cmbSourceAuth";
        cmbSourceAuth.Size = new Size(286, 33);
        cmbSourceAuth.TabIndex = 4;
        // 
        // lblSourceAuth
        // 
        lblSourceAuth.AutoSize = true;
        lblSourceAuth.Font = new Font("Segoe UI", 9F);
        lblSourceAuth.Location = new Point(41, 80);
        lblSourceAuth.Margin = new Padding(4, 0, 4, 0);
        lblSourceAuth.Name = "lblSourceAuth";
        lblSourceAuth.Size = new Size(54, 25);
        lblSourceAuth.TabIndex = 0;
        lblSourceAuth.Text = "Auth:";
        // 
        // cmbSourceDatabase
        // 
        cmbSourceDatabase.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbSourceDatabase.Font = new Font("Segoe UI", 9F);
        cmbSourceDatabase.FormattingEnabled = true;
        cmbSourceDatabase.Location = new Point(496, 35);
        cmbSourceDatabase.Margin = new Padding(4);
        cmbSourceDatabase.Name = "cmbSourceDatabase";
        cmbSourceDatabase.Size = new Size(286, 33);
        cmbSourceDatabase.TabIndex = 2;
        // 
        // lblSourceDatabase
        // 
        lblSourceDatabase.AutoSize = true;
        lblSourceDatabase.Font = new Font("Segoe UI", 9F);
        lblSourceDatabase.Location = new Point(405, 37);
        lblSourceDatabase.Margin = new Padding(4, 0, 4, 0);
        lblSourceDatabase.Name = "lblSourceDatabase";
        lblSourceDatabase.Size = new Size(90, 25);
        lblSourceDatabase.TabIndex = 0;
        lblSourceDatabase.Text = "Database:";
        // 
        // btnTestSource
        // 
        btnTestSource.Location = new Point(521, 80);
        btnTestSource.Margin = new Padding(4);
        btnTestSource.Name = "btnTestSource";
        btnTestSource.Size = new Size(128, 36);
        btnTestSource.TabIndex = 3;
        btnTestSource.Text = "Test";
        btnTestSource.UseVisualStyleBackColor = true;
        // 
        // cmbSourceServer
        // 
        cmbSourceServer.Font = new Font("Segoe UI", 9F);
        cmbSourceServer.FormattingEnabled = true;
        cmbSourceServer.Location = new Point(98, 35);
        cmbSourceServer.Margin = new Padding(4);
        cmbSourceServer.Name = "cmbSourceServer";
        cmbSourceServer.Size = new Size(286, 33);
        cmbSourceServer.TabIndex = 1;
        // 
        // lblSourceServer
        // 
        lblSourceServer.AutoSize = true;
        lblSourceServer.Font = new Font("Segoe UI", 9F);
        lblSourceServer.Location = new Point(27, 40);
        lblSourceServer.Margin = new Padding(4, 0, 4, 0);
        lblSourceServer.Name = "lblSourceServer";
        lblSourceServer.Size = new Size(65, 25);
        lblSourceServer.TabIndex = 0;
        lblSourceServer.Text = "Server:";
        // 
        // grpDestination
        // 
        grpDestination.Controls.Add(chkDestinationTrustCert);
        grpDestination.Controls.Add(txtDestinationPassword);
        grpDestination.Controls.Add(txtDestinationUser);
        grpDestination.Controls.Add(lblDestinationPassword);
        grpDestination.Controls.Add(lblDestinationUser);
        grpDestination.Controls.Add(cmbDestinationAuth);
        grpDestination.Controls.Add(lblDestinationAuth);
        grpDestination.Controls.Add(txtDestinationDatabase);
        grpDestination.Controls.Add(lblDestinationDatabase);
        grpDestination.Controls.Add(btnTestDestination);
        grpDestination.Controls.Add(cmbDestinationServer);
        grpDestination.Controls.Add(lblDestinationServer);
        grpDestination.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        grpDestination.Location = new Point(822, 15);
        grpDestination.Margin = new Padding(4);
        grpDestination.Name = "grpDestination";
        grpDestination.Padding = new Padding(4);
        grpDestination.Size = new Size(932, 240);
        grpDestination.TabIndex = 1;
        grpDestination.TabStop = false;
        grpDestination.Text = "Destination DB";
        // 
        // chkDestinationTrustCert
        // 
        chkDestinationTrustCert.AutoSize = true;
        chkDestinationTrustCert.Font = new Font("Segoe UI", 9F);
        chkDestinationTrustCert.Location = new Point(83, 200);
        chkDestinationTrustCert.Margin = new Padding(4);
        chkDestinationTrustCert.Name = "chkDestinationTrustCert";
        chkDestinationTrustCert.Size = new Size(158, 29);
        chkDestinationTrustCert.TabIndex = 22;
        chkDestinationTrustCert.Text = "Trust Certificate";
        chkDestinationTrustCert.UseVisualStyleBackColor = true;
        // 
        // txtDestinationPassword
        // 
        txtDestinationPassword.Font = new Font("Segoe UI", 9F);
        txtDestinationPassword.Location = new Point(83, 161);
        txtDestinationPassword.Margin = new Padding(4);
        txtDestinationPassword.Name = "txtDestinationPassword";
        txtDestinationPassword.Size = new Size(275, 31);
        txtDestinationPassword.TabIndex = 21;
        // 
        // txtDestinationUser
        // 
        txtDestinationUser.Font = new Font("Segoe UI", 9F);
        txtDestinationUser.Location = new Point(83, 122);
        txtDestinationUser.Margin = new Padding(4);
        txtDestinationUser.Name = "txtDestinationUser";
        txtDestinationUser.Size = new Size(275, 31);
        txtDestinationUser.TabIndex = 20;
        // 
        // lblDestinationPassword
        // 
        lblDestinationPassword.AutoSize = true;
        lblDestinationPassword.Font = new Font("Segoe UI", 9F);
        lblDestinationPassword.Location = new Point(28, 164);
        lblDestinationPassword.Margin = new Padding(4, 0, 4, 0);
        lblDestinationPassword.Name = "lblDestinationPassword";
        lblDestinationPassword.Size = new Size(50, 25);
        lblDestinationPassword.TabIndex = 0;
        lblDestinationPassword.Text = "Pass:";
        // 
        // lblDestinationUser
        // 
        lblDestinationUser.AutoSize = true;
        lblDestinationUser.Font = new Font("Segoe UI", 9F);
        lblDestinationUser.Location = new Point(27, 123);
        lblDestinationUser.Margin = new Padding(4, 0, 4, 0);
        lblDestinationUser.Name = "lblDestinationUser";
        lblDestinationUser.Size = new Size(51, 25);
        lblDestinationUser.TabIndex = 0;
        lblDestinationUser.Text = "User:";
        // 
        // cmbDestinationAuth
        // 
        cmbDestinationAuth.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbDestinationAuth.Font = new Font("Segoe UI", 9F);
        cmbDestinationAuth.FormattingEnabled = true;
        cmbDestinationAuth.Location = new Point(83, 83);
        cmbDestinationAuth.Margin = new Padding(4);
        cmbDestinationAuth.Name = "cmbDestinationAuth";
        cmbDestinationAuth.Size = new Size(275, 33);
        cmbDestinationAuth.TabIndex = 17;
        // 
        // lblDestinationAuth
        // 
        lblDestinationAuth.AutoSize = true;
        lblDestinationAuth.Font = new Font("Segoe UI", 9F);
        lblDestinationAuth.Location = new Point(24, 84);
        lblDestinationAuth.Margin = new Padding(4, 0, 4, 0);
        lblDestinationAuth.Name = "lblDestinationAuth";
        lblDestinationAuth.Size = new Size(54, 25);
        lblDestinationAuth.TabIndex = 0;
        lblDestinationAuth.Text = "Auth:";
        // 
        // txtDestinationDatabase
        // 
        txtDestinationDatabase.Font = new Font("Segoe UI", 9F);
        txtDestinationDatabase.Location = new Point(564, 43);
        txtDestinationDatabase.Margin = new Padding(4);
        txtDestinationDatabase.Name = "txtDestinationDatabase";
        txtDestinationDatabase.Size = new Size(286, 31);
        txtDestinationDatabase.TabIndex = 16;
        // 
        // lblDestinationDatabase
        // 
        lblDestinationDatabase.AutoSize = true;
        lblDestinationDatabase.Font = new Font("Segoe UI", 9F);
        lblDestinationDatabase.Location = new Point(432, 45);
        lblDestinationDatabase.Margin = new Padding(4, 0, 4, 0);
        lblDestinationDatabase.Name = "lblDestinationDatabase";
        lblDestinationDatabase.Size = new Size(128, 25);
        lblDestinationDatabase.TabIndex = 0;
        lblDestinationDatabase.Text = "New DB name:";
        // 
        // btnTestDestination
        // 
        btnTestDestination.Location = new Point(564, 86);
        btnTestDestination.Margin = new Padding(4);
        btnTestDestination.Name = "btnTestDestination";
        btnTestDestination.Size = new Size(128, 36);
        btnTestDestination.TabIndex = 18;
        btnTestDestination.Text = "Test";
        btnTestDestination.UseVisualStyleBackColor = true;
        // 
        // cmbDestinationServer
        // 
        cmbDestinationServer.Font = new Font("Segoe UI", 9F);
        cmbDestinationServer.FormattingEnabled = true;
        cmbDestinationServer.Location = new Point(83, 42);
        cmbDestinationServer.Margin = new Padding(4);
        cmbDestinationServer.Name = "cmbDestinationServer";
        cmbDestinationServer.Size = new Size(275, 33);
        cmbDestinationServer.TabIndex = 15;
        // 
        // lblDestinationServer
        // 
        lblDestinationServer.AutoSize = true;
        lblDestinationServer.Font = new Font("Segoe UI", 9F);
        lblDestinationServer.Location = new Point(10, 44);
        lblDestinationServer.Margin = new Padding(4, 0, 4, 0);
        lblDestinationServer.Name = "lblDestinationServer";
        lblDestinationServer.Size = new Size(65, 25);
        lblDestinationServer.TabIndex = 0;
        lblDestinationServer.Text = "Server:";
        // 
        // lblFilter
        // 
        lblFilter.AutoSize = true;
        lblFilter.Location = new Point(15, 275);
        lblFilter.Margin = new Padding(4, 0, 4, 0);
        lblFilter.Name = "lblFilter";
        lblFilter.Size = new Size(185, 25);
        lblFilter.TabIndex = 2;
        lblFilter.Text = "Filter (schema / table):";
        // 
        // txtFilter
        // 
        txtFilter.Location = new Point(221, 270);
        txtFilter.Margin = new Padding(4);
        txtFilter.Name = "txtFilter";
        txtFilter.Size = new Size(349, 31);
        txtFilter.TabIndex = 23;
        // 
        // chkSelectAll
        // 
        chkSelectAll.AutoSize = true;
        chkSelectAll.Location = new Point(15, 419);
        chkSelectAll.Margin = new Padding(4);
        chkSelectAll.Name = "chkSelectAll";
        chkSelectAll.Size = new Size(106, 29);
        chkSelectAll.TabIndex = 24;
        chkSelectAll.Text = "Select all";
        chkSelectAll.UseVisualStyleBackColor = true;
        // 
        // btnLoadTables
        // 
        btnLoadTables.Location = new Point(721, 268);
        btnLoadTables.Margin = new Padding(4);
        btnLoadTables.Name = "btnLoadTables";
        btnLoadTables.Size = new Size(142, 39);
        btnLoadTables.TabIndex = 25;
        btnLoadTables.Text = "Load Tables";
        btnLoadTables.UseVisualStyleBackColor = true;
        btnLoadTables.Click += btnLoadTables_Click;
        // 
        // chkKeepIdentity
        // 
        chkKeepIdentity.AutoSize = true;
        chkKeepIdentity.Checked = true;
        chkKeepIdentity.CheckState = CheckState.Checked;
        chkKeepIdentity.Location = new Point(15, 344);
        chkKeepIdentity.Margin = new Padding(4);
        chkKeepIdentity.Name = "chkKeepIdentity";
        chkKeepIdentity.Size = new Size(222, 29);
        chkKeepIdentity.TabIndex = 26;
        chkKeepIdentity.Text = "Preserve identity values";
        chkKeepIdentity.UseVisualStyleBackColor = true;
        // 
        // chkScriptFunctions
        // 
        chkScriptFunctions.AutoSize = true;
        chkScriptFunctions.Checked = true;
        chkScriptFunctions.CheckState = CheckState.Checked;
        chkScriptFunctions.Location = new Point(245, 344);
        chkScriptFunctions.Margin = new Padding(4);
        chkScriptFunctions.Name = "chkScriptFunctions";
        chkScriptFunctions.Size = new Size(205, 29);
        chkScriptFunctions.TabIndex = 33;
        chkScriptFunctions.Text = "Script functions/TVFs";
        chkScriptFunctions.UseVisualStyleBackColor = true;
        // 
        // chkPreserveIndexes
        // 
        chkPreserveIndexes.AutoSize = true;
        chkPreserveIndexes.Checked = true;
        chkPreserveIndexes.CheckState = CheckState.Checked;
        chkPreserveIndexes.Location = new Point(458, 344);
        chkPreserveIndexes.Margin = new Padding(4);
        chkPreserveIndexes.Name = "chkPreserveIndexes";
        chkPreserveIndexes.Size = new Size(168, 29);
        chkPreserveIndexes.TabIndex = 34;
        chkPreserveIndexes.Text = "Preserve indexes";
        chkPreserveIndexes.UseVisualStyleBackColor = true;
        // 
        // chkPreserveForeignKeys
        // 
        chkPreserveForeignKeys.AutoSize = true;
        chkPreserveForeignKeys.Checked = true;
        chkPreserveForeignKeys.CheckState = CheckState.Checked;
        chkPreserveForeignKeys.Location = new Point(634, 344);
        chkPreserveForeignKeys.Margin = new Padding(4);
        chkPreserveForeignKeys.Name = "chkPreserveForeignKeys";
        chkPreserveForeignKeys.Size = new Size(136, 29);
        chkPreserveForeignKeys.TabIndex = 35;
        chkPreserveForeignKeys.Text = "Preserve FKs";
        chkPreserveForeignKeys.UseVisualStyleBackColor = true;
        // 
        // lblBulkRowLimit
        // 
        lblBulkRowLimit.AutoSize = true;
        lblBulkRowLimit.Location = new Point(385, 420);
        lblBulkRowLimit.Name = "lblBulkRowLimit";
        lblBulkRowLimit.Size = new Size(89, 25);
        lblBulkRowLimit.TabIndex = 37;
        lblBulkRowLimit.Text = "Row limit:";
        // 
        // numBulkRowLimit
        // 
        numBulkRowLimit.Location = new Point(493, 417);
        numBulkRowLimit.Maximum = new decimal(new int[] { 1000000000, 0, 0, 0 });
        numBulkRowLimit.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        numBulkRowLimit.Name = "numBulkRowLimit";
        numBulkRowLimit.Size = new Size(114, 31);
        numBulkRowLimit.TabIndex = 38;
        numBulkRowLimit.ThousandsSeparator = true;
        numBulkRowLimit.Value = new decimal(new int[] { 1000, 0, 0, 0 });
        // 
        // btnSetLimitAll
        // 
        btnSetLimitAll.Location = new Point(625, 414);
        btnSetLimitAll.Name = "btnSetLimitAll";
        btnSetLimitAll.Size = new Size(88, 35);
        btnSetLimitAll.TabIndex = 39;
        btnSetLimitAll.Text = "Set All";
        btnSetLimitAll.UseVisualStyleBackColor = true;
        // 
        // btnSetLimitSelected
        // 
        btnSetLimitSelected.Location = new Point(719, 414);
        btnSetLimitSelected.Name = "btnSetLimitSelected";
        btnSetLimitSelected.Size = new Size(123, 35);
        btnSetLimitSelected.TabIndex = 40;
        btnSetLimitSelected.Text = "Set Selected";
        btnSetLimitSelected.UseVisualStyleBackColor = true;
        // 
        // btnClearLimitAll
        // 
        btnClearLimitAll.Location = new Point(848, 414);
        btnClearLimitAll.Name = "btnClearLimitAll";
        btnClearLimitAll.Size = new Size(95, 35);
        btnClearLimitAll.TabIndex = 41;
        btnClearLimitAll.Text = "Clear All";
        btnClearLimitAll.UseVisualStyleBackColor = true;
        // 
        // btnClearLimitSelected
        // 
        btnClearLimitSelected.Location = new Point(949, 414);
        btnClearLimitSelected.Name = "btnClearLimitSelected";
        btnClearLimitSelected.Size = new Size(136, 35);
        btnClearLimitSelected.TabIndex = 42;
        btnClearLimitSelected.Text = "Clear Selected";
        btnClearLimitSelected.UseVisualStyleBackColor = true;
        // 
        // btnSaveProject
        // 
        btnSaveProject.Location = new Point(1110, 329);
        btnSaveProject.Name = "btnSaveProject";
        btnSaveProject.Size = new Size(150, 35);
        btnSaveProject.TabIndex = 43;
        btnSaveProject.Text = "Save Project...";
        btnSaveProject.UseVisualStyleBackColor = true;
        // 
        // btnLoadProject
        // 
        btnLoadProject.Location = new Point(1266, 329);
        btnLoadProject.Name = "btnLoadProject";
        btnLoadProject.Size = new Size(150, 35);
        btnLoadProject.TabIndex = 44;
        btnLoadProject.Text = "Load Project...";
        btnLoadProject.UseVisualStyleBackColor = true;
        // 
        // gridTables
        // 
        gridTables.AllowUserToAddRows = false;
        gridTables.AllowUserToDeleteRows = false;
        gridTables.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        gridTables.Dock = DockStyle.Fill;
        gridTables.Location = new Point(0, 0);
        gridTables.Margin = new Padding(4);
        gridTables.Name = "gridTables";
        gridTables.RowHeadersVisible = false;
        gridTables.RowHeadersWidth = 51;
        gridTables.Size = new Size(1100, 503);
        gridTables.TabIndex = 27;
        // 
        // logBox
        // 
        logBox.BackColor = SystemColors.Control;
        logBox.Dock = DockStyle.Fill;
        logBox.Location = new Point(0, 0);
        logBox.Margin = new Padding(4);
        logBox.Name = "logBox";
        logBox.ReadOnly = true;
        logBox.Size = new Size(651, 503);
        logBox.TabIndex = 28;
        logBox.Text = "";
        // 
        // lblLog
        // 
        lblLog.AutoSize = true;
        lblLog.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        lblLog.Location = new Point(1098, 420);
        lblLog.Margin = new Padding(4, 0, 4, 0);
        lblLog.Name = "lblLog";
        lblLog.Size = new Size(111, 25);
        lblLog.TabIndex = 0;
        lblLog.Text = "Transfer log";
        // 
        // btnStartTransfer
        // 
        btnStartTransfer.BackColor = SystemColors.Control;
        btnStartTransfer.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        btnStartTransfer.Location = new Point(778, 329);
        btnStartTransfer.Margin = new Padding(4);
        btnStartTransfer.Name = "btnStartTransfer";
        btnStartTransfer.Size = new Size(171, 49);
        btnStartTransfer.TabIndex = 29;
        btnStartTransfer.Text = "Start Transfer";
        btnStartTransfer.UseVisualStyleBackColor = false;
        btnStartTransfer.Click += btnStartTransfer_Click;
        // 
        // btnCancelTransfer
        // 
        btnCancelTransfer.Enabled = false;
        btnCancelTransfer.Location = new Point(957, 329);
        btnCancelTransfer.Margin = new Padding(4);
        btnCancelTransfer.Name = "btnCancelTransfer";
        btnCancelTransfer.Size = new Size(130, 49);
        btnCancelTransfer.TabIndex = 30;
        btnCancelTransfer.Text = "Cancel";
        btnCancelTransfer.UseVisualStyleBackColor = true;
        // 
        // progressTransfer
        // 
        progressTransfer.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        progressTransfer.ForeColor = Color.Lime;
        progressTransfer.Location = new Point(15, 385);
        progressTransfer.Margin = new Padding(4);
        progressTransfer.Name = "progressTransfer";
        progressTransfer.Size = new Size(1761, 24);
        progressTransfer.TabIndex = 31;
        // 
        // lblEta
        // 
        lblEta.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        lblEta.AutoSize = true;
        lblEta.Location = new Point(15, 996);
        lblEta.Margin = new Padding(4, 0, 4, 0);
        lblEta.Name = "lblEta";
        lblEta.Size = new Size(78, 25);
        lblEta.TabIndex = 45;
        lblEta.Text = "ETA --:--";
        // 
        // lblStatus
        // 
        lblStatus.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        lblStatus.Location = new Point(167, 996);
        lblStatus.Margin = new Padding(4, 0, 4, 0);
        lblStatus.Name = "lblStatus";
        lblStatus.Size = new Size(1610, 30);
        lblStatus.TabIndex = 32;
        lblStatus.Text = "Ready";
        // 
        // label1
        // 
        label1.AutoSize = true;
        label1.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        label1.Location = new Point(13, 315);
        label1.Margin = new Padding(4, 0, 4, 0);
        label1.Name = "label1";
        label1.Size = new Size(79, 25);
        label1.TabIndex = 36;
        label1.Text = "Options";
        // 
        // splitContainer1
        // 
        splitContainer1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        splitContainer1.Location = new Point(15, 472);
        splitContainer1.Name = "splitContainer1";
        // 
        // splitContainer1.Panel1
        // 
        splitContainer1.Panel1.Controls.Add(gridTables);
        // 
        // splitContainer1.Panel2
        // 
        splitContainer1.Panel2.Controls.Add(logBox);
        splitContainer1.Size = new Size(1761, 503);
        splitContainer1.SplitterDistance = 1100;
        splitContainer1.SplitterWidth = 10;
        splitContainer1.TabIndex = 46;
        // 
        // btnHelp
        // 
        btnHelp.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnHelp.Location = new Point(1640, 15);
        btnHelp.Margin = new Padding(4);
        btnHelp.Name = "btnHelp";
        btnHelp.Size = new Size(120, 36);
        btnHelp.TabIndex = 50;
        btnHelp.Text = "? Help";
        btnHelp.UseVisualStyleBackColor = true;
        btnHelp.Click += btnHelp_Click;
        // 
        // pnlHelp
        // 
        pnlHelp.Controls.Add(webHelp);
        pnlHelp.Controls.Add(pnlHelpToolbar);
        pnlHelp.Controls.Add(pnlHelpHeader);
        pnlHelp.Dock = DockStyle.Right;
        pnlHelp.Location = new Point(1792, 0);
        pnlHelp.Name = "pnlHelp";
        pnlHelp.Size = new Size(0, 1037);
        pnlHelp.TabIndex = 51;
        pnlHelp.Visible = false;
        // 
        // webHelp
        // 
        webHelp.AllowExternalDrop = true;
        webHelp.CreationProperties = null;
        webHelp.DefaultBackgroundColor = Color.White;
        webHelp.Dock = DockStyle.Fill;
        webHelp.Location = new Point(0, 80);
        webHelp.Name = "webHelp";
        webHelp.Size = new Size(0, 957);
        webHelp.TabIndex = 0;
        webHelp.TabStop = false;
        webHelp.ZoomFactor = 1D;
        // 
        // pnlHelpToolbar
        // 
        pnlHelpToolbar.Controls.Add(lblZoom);
        pnlHelpToolbar.Controls.Add(btnZoomReset);
        pnlHelpToolbar.Controls.Add(btnZoomOut);
        pnlHelpToolbar.Controls.Add(btnZoomIn);
        pnlHelpToolbar.Dock = DockStyle.Top;
        pnlHelpToolbar.Location = new Point(0, 44);
        pnlHelpToolbar.Name = "pnlHelpToolbar";
        pnlHelpToolbar.Padding = new Padding(8, 4, 8, 4);
        pnlHelpToolbar.Size = new Size(0, 36);
        pnlHelpToolbar.TabIndex = 2;
        // 
        // lblZoom
        // 
        lblZoom.AutoSize = true;
        lblZoom.Dock = DockStyle.Left;
        lblZoom.Font = new Font("Segoe UI", 8.5F);
        lblZoom.Location = new Point(132, 4);
        lblZoom.Name = "lblZoom";
        lblZoom.Padding = new Padding(8, 0, 0, 0);
        lblZoom.Size = new Size(113, 23);
        lblZoom.TabIndex = 0;
        lblZoom.Text = "Zoom: 100%";
        lblZoom.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // btnZoomReset
        // 
        btnZoomReset.Dock = DockStyle.Left;
        btnZoomReset.FlatStyle = FlatStyle.Flat;
        btnZoomReset.Font = new Font("Segoe UI", 8.5F);
        btnZoomReset.Location = new Point(80, 4);
        btnZoomReset.Name = "btnZoomReset";
        btnZoomReset.Size = new Size(52, 28);
        btnZoomReset.TabIndex = 2;
        btnZoomReset.Text = "Reset";
        btnZoomReset.UseVisualStyleBackColor = true;
        btnZoomReset.Click += btnZoomReset_Click;
        // 
        // btnZoomOut
        // 
        btnZoomOut.Dock = DockStyle.Left;
        btnZoomOut.FlatStyle = FlatStyle.Flat;
        btnZoomOut.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        btnZoomOut.Location = new Point(44, 4);
        btnZoomOut.Name = "btnZoomOut";
        btnZoomOut.Size = new Size(36, 28);
        btnZoomOut.TabIndex = 1;
        btnZoomOut.Text = "−";
        btnZoomOut.UseVisualStyleBackColor = true;
        btnZoomOut.Click += btnZoomOut_Click;
        // 
        // btnZoomIn
        // 
        btnZoomIn.Dock = DockStyle.Left;
        btnZoomIn.FlatStyle = FlatStyle.Flat;
        btnZoomIn.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        btnZoomIn.Location = new Point(8, 4);
        btnZoomIn.Name = "btnZoomIn";
        btnZoomIn.Size = new Size(36, 28);
        btnZoomIn.TabIndex = 0;
        btnZoomIn.Text = "+";
        btnZoomIn.UseVisualStyleBackColor = true;
        btnZoomIn.Click += btnZoomIn_Click;
        // 
        // pnlHelpHeader
        // 
        pnlHelpHeader.Controls.Add(lblHelpTitle);
        pnlHelpHeader.Controls.Add(btnCloseHelp);
        pnlHelpHeader.Dock = DockStyle.Top;
        pnlHelpHeader.Location = new Point(0, 0);
        pnlHelpHeader.Name = "pnlHelpHeader";
        pnlHelpHeader.Padding = new Padding(12, 0, 6, 0);
        pnlHelpHeader.Size = new Size(0, 44);
        pnlHelpHeader.TabIndex = 1;
        // 
        // lblHelpTitle
        // 
        lblHelpTitle.AutoSize = true;
        lblHelpTitle.Dock = DockStyle.Left;
        lblHelpTitle.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold);
        lblHelpTitle.Location = new Point(12, 0);
        lblHelpTitle.Name = "lblHelpTitle";
        lblHelpTitle.Padding = new Padding(0, 10, 0, 0);
        lblHelpTitle.Size = new Size(227, 40);
        lblHelpTitle.TabIndex = 0;
        lblHelpTitle.Text = "Help & Documentation";
        // 
        // btnCloseHelp
        // 
        btnCloseHelp.Dock = DockStyle.Right;
        btnCloseHelp.FlatAppearance.BorderSize = 0;
        btnCloseHelp.FlatStyle = FlatStyle.Flat;
        btnCloseHelp.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        btnCloseHelp.Location = new Point(-46, 0);
        btnCloseHelp.Name = "btnCloseHelp";
        btnCloseHelp.Size = new Size(40, 44);
        btnCloseHelp.TabIndex = 0;
        btnCloseHelp.Text = "✕";
        btnCloseHelp.UseVisualStyleBackColor = true;
        btnCloseHelp.Click += btnCloseHelp_Click;
        // 
        // Form1
        // 
        AutoScaleDimensions = new SizeF(10F, 25F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1792, 1037);
        Controls.Add(pnlHelp);
        Controls.Add(splitContainer1);
        Controls.Add(label1);
        Controls.Add(btnHelp);
        Controls.Add(btnLoadProject);
        Controls.Add(btnSaveProject);
        Controls.Add(btnClearLimitSelected);
        Controls.Add(btnClearLimitAll);
        Controls.Add(btnSetLimitSelected);
        Controls.Add(btnSetLimitAll);
        Controls.Add(numBulkRowLimit);
        Controls.Add(lblBulkRowLimit);
        Controls.Add(lblEta);
        Controls.Add(lblStatus);
        Controls.Add(progressTransfer);
        Controls.Add(btnCancelTransfer);
        Controls.Add(btnStartTransfer);
        Controls.Add(lblLog);
        Controls.Add(chkPreserveForeignKeys);
        Controls.Add(chkPreserveIndexes);
        Controls.Add(chkScriptFunctions);
        Controls.Add(chkKeepIdentity);
        Controls.Add(btnLoadTables);
        Controls.Add(chkSelectAll);
        Controls.Add(txtFilter);
        Controls.Add(lblFilter);
        Controls.Add(grpDestination);
        Controls.Add(grpSource);
        Margin = new Padding(4);
        MinimumSize = new Size(1654, 1030);
        Name = "Form1";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "SQL Data Migrator";
        Load += Form1_Load;
        Shown += Form1_ShownAsync;
        grpSource.ResumeLayout(false);
        grpSource.PerformLayout();
        grpDestination.ResumeLayout(false);
        grpDestination.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)numBulkRowLimit).EndInit();
        ((System.ComponentModel.ISupportInitialize)gridTables).EndInit();
        splitContainer1.Panel1.ResumeLayout(false);
        splitContainer1.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
        splitContainer1.ResumeLayout(false);
        pnlHelp.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)webHelp).EndInit();
        pnlHelpToolbar.ResumeLayout(false);
        pnlHelpToolbar.PerformLayout();
        pnlHelpHeader.ResumeLayout(false);
        pnlHelpHeader.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private GroupBox grpSource;
    private GroupBox grpDestination;
    private Label lblSourceServer;
    private ComboBox cmbSourceServer;
    private Button btnTestSource;
    private ComboBox cmbSourceDatabase;
    private Label lblSourceDatabase;
    private ComboBox cmbSourceAuth;
    private Label lblSourceAuth;
    private TextBox txtSourceUser;
    private Label lblSourceUser;
    private TextBox txtSourcePassword;
    private Label lblSourcePassword;
    private CheckBox chkSourceTrustCert;
    private Button btnLoadSourceDbs;
    private CheckBox chkDestinationTrustCert;
    private TextBox txtDestinationPassword;
    private TextBox txtDestinationUser;
    private Label lblDestinationPassword;
    private Label lblDestinationUser;
    private ComboBox cmbDestinationAuth;
    private Label lblDestinationAuth;
    private TextBox txtDestinationDatabase;
    private Label lblDestinationDatabase;
    private Button btnTestDestination;
    private ComboBox cmbDestinationServer;
    private Label lblDestinationServer;
    private Label lblFilter;
    private TextBox txtFilter;
    private CheckBox chkSelectAll;
    private Button btnLoadTables;
    private CheckBox chkKeepIdentity;
    private CheckBox chkScriptFunctions;
    private CheckBox chkPreserveIndexes;
    private CheckBox chkPreserveForeignKeys;
    private Label lblBulkRowLimit;
    private NumericUpDown numBulkRowLimit;
    private Button btnSetLimitAll;
    private Button btnSetLimitSelected;
    private Button btnClearLimitAll;
    private Button btnClearLimitSelected;
    private Button btnSaveProject;
    private Button btnLoadProject;
    private DataGridView gridTables;
    private RichTextBox logBox;
    private Label lblLog;
    private Button btnStartTransfer;
    private Button btnCancelTransfer;
    private ProgressBar progressTransfer;
    private Label lblEta;
    private Label lblStatus;
    private Label label1;
    private SplitContainer splitContainer1;
    private Button btnHelp;
    private Panel pnlHelp;
    private Panel pnlHelpHeader;
    private Label lblHelpTitle;
    private Button btnCloseHelp;
    private Panel pnlHelpToolbar;
    private Button btnZoomIn;
    private Button btnZoomOut;
    private Button btnZoomReset;
    private Label lblZoom;
    private Microsoft.Web.WebView2.WinForms.WebView2 webHelp;
}
