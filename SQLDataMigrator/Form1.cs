using System.Text;
using System.Text.Json;

namespace SQLDataMigrator;

public partial class Form1 : Form
{
    private const int DefaultRowLimit = 0;

    private readonly SettingsStore _settingsStore;
    private static readonly JsonSerializerOptions ProjectJsonOptions = new() { WriteIndented = true };
    private const int MaxLogLines = 5000;
    private const long EtaMinSampleRows = 50000;
    private const int EtaRequiredSamples = 3;
    private AppSettings _settings;
    private readonly MigrationEngine _engine = new();
    private readonly ErrorProvider _errorProvider;
    private readonly List<TableSelectionItem> _allTables = new();
    private readonly Queue<double> _etaSampleSecondsPerRow = new();
    private CancellationTokenSource? _transferCts;
    private bool _updatingGrid;
    private long _etaTotalRows;
    private long _etaProcessedRows;

    //If this were a Redgate-style product, high-value next features would be:

    //Schema drift preview + diff report before execute.
    //Preflight validation (permissions, disk space, PK/FK dependencies, unsupported objects).
    //Retry + resume from failed table checkpoint.
    //Data masking/anonymization rules for non-prod copies.
    //Row filters per table (date/window predicates, not just TOP N).
    //Full audit report(duration, rows copied, failures, generated scripts).
    //Profiles/environments(Dev/Test/UAT templates with secrets via Windows Credential Manager).
    //Dry run mode(estimate only, no writes).
    public Form1()
    {
        InitializeComponent();

        _errorProvider = new ErrorProvider
        {
            BlinkStyle = ErrorBlinkStyle.NeverBlink,
            ContainerControl = this
        };

        _settingsStore = new SettingsStore();
        _settings = _settingsStore.Load();

        ConfigureUi();
        LoadSettingsIntoUi();
        WireEvents();
    }
    private async void Form1_ShownAsync(object sender, EventArgs e)
    {
        await LoadSourceDatabasesAsync();
    }
    private void ConfigureUi()
    {
        cmbSourceAuth.Items.AddRange(new[] { "Integrated Security", "SQL Login" });
        cmbDestinationAuth.Items.AddRange(new[] { "Integrated Security", "SQL Login" });

        txtSourcePassword.UseSystemPasswordChar = true;
        txtDestinationPassword.UseSystemPasswordChar = true;

        gridTables.AutoGenerateColumns = false;
        gridTables.AllowUserToAddRows = false;
        gridTables.AllowUserToDeleteRows = false;
        gridTables.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

        var colSelect = new DataGridViewCheckBoxColumn
        {
            Name = "Selected",
            HeaderText = "Copy",
            Width = 55
        };
        var colSchema = new DataGridViewTextBoxColumn
        {
            Name = "Schema",
            HeaderText = "Schema",
            Width = 130,
            ReadOnly = true
        };
        var colTable = new DataGridViewTextBoxColumn
        {
            Name = "Table",
            HeaderText = "Table",
            Width = 260,
            ReadOnly = true
        };
        var colRowCount = new DataGridViewTextBoxColumn
        {
            Name = "RowCount",
            HeaderText = "Source Rows",
            Width = 130,
            ReadOnly = true
        };
        var colRowLimit = new DataGridViewTextBoxColumn
        {
            Name = "RowLimit",
            HeaderText = "Row Limit",
            Width = 120
        };
        var colRowFilter = new DataGridViewTextBoxColumn
        {
            Name = "RowFilter",
            HeaderText = "WHERE Filter (optional)",
            Width = 360
        };

        gridTables.Columns.AddRange(colSelect, colSchema, colTable, colRowCount, colRowLimit, colRowFilter);

        if (cmbSourceAuth.Items.Count > 0 && cmbSourceAuth.SelectedIndex < 0)
        {
            cmbSourceAuth.SelectedIndex = 0;
        }

        if (cmbDestinationAuth.Items.Count > 0 && cmbDestinationAuth.SelectedIndex < 0)
        {
            cmbDestinationAuth.SelectedIndex = 0;
        }

        progressTransfer.Minimum = 0;
        progressTransfer.Maximum = 100;

        ConfigureLogTheme();

        ApplyDarkGreenButtonTheme(btnStartTransfer, isPrimary: true);
        ApplyDarkGreenButtonTheme(btnCancelTransfer, isPrimary: false);
        ApplyDarkGreenButtonTheme(btnLoadTables, isPrimary: false);
        ApplyDarkGreenButtonTheme(btnLoadSourceDbs, isPrimary: false);
        ApplyDarkGreenButtonTheme(btnTestSource, isPrimary: false);
        ApplyDarkGreenButtonTheme(btnTestDestination, isPrimary: false);
        ApplyDarkGreenButtonTheme(btnSetLimitAll, isPrimary: false);
        ApplyDarkGreenButtonTheme(btnSetLimitSelected, isPrimary: false);
        ApplyDarkGreenButtonTheme(btnClearLimitAll, isPrimary: false);
        ApplyDarkGreenButtonTheme(btnClearLimitSelected, isPrimary: false);
        ApplyDarkGreenButtonTheme(btnSaveProject, isPrimary: false);
        ApplyDarkGreenButtonTheme(btnLoadProject, isPrimary: false);

        ToggleAuthInputs(cmbSourceAuth, txtSourceUser, txtSourcePassword);
        ToggleAuthInputs(cmbDestinationAuth, txtDestinationUser, txtDestinationPassword);

        ApplyHelpPanelTheme();
    }

    // ── Help panel ────────────────────────────────────────────────────────────

    private const int HelpPanelWidth = 440;
    private double _helpZoom = 1.0;
    private bool _webHelpReady = false;

    private void ApplyHelpPanelTheme()
    {
        var isDark = IsDarkModeUi();

        if (isDark)
        {
            pnlHelp.BackColor = Color.FromArgb(28, 28, 28);
            pnlHelpHeader.BackColor = Color.FromArgb(36, 36, 36);
            pnlHelpToolbar.BackColor = Color.FromArgb(36, 36, 36);
            lblHelpTitle.ForeColor = Color.FromArgb(220, 220, 220);
            btnCloseHelp.ForeColor = Color.FromArgb(180, 180, 180);
            btnCloseHelp.BackColor = Color.FromArgb(36, 36, 36);
            lblZoom.ForeColor = Color.FromArgb(180, 180, 180);
        }
        else
        {
            pnlHelp.BackColor = Color.FromArgb(240, 245, 255);
            pnlHelpHeader.BackColor = Color.FromArgb(30, 80, 160);
            pnlHelpToolbar.BackColor = Color.FromArgb(230, 238, 252);
            lblHelpTitle.ForeColor = Color.White;
            btnCloseHelp.ForeColor = Color.White;
            btnCloseHelp.BackColor = Color.FromArgb(30, 80, 160);
            lblZoom.ForeColor = Color.FromArgb(50, 50, 80);
        }

        ApplyDarkGreenButtonTheme(btnHelp, isPrimary: false);

        var toolbarBtnBg = isDark ? Color.FromArgb(50, 50, 50) : Color.FromArgb(210, 225, 248);
        var toolbarBtnFg = isDark ? Color.FromArgb(200, 200, 200) : Color.FromArgb(30, 60, 120);
        var toolbarBorder = isDark ? Color.FromArgb(70, 70, 70) : Color.FromArgb(160, 190, 230);
        foreach (var b in new[] { btnZoomIn, btnZoomOut, btnZoomReset })
        {
            b.BackColor = toolbarBtnBg;
            b.ForeColor = toolbarBtnFg;
            b.FlatAppearance.BorderColor = toolbarBorder;
        }
    }

    private void btnHelp_Click(object sender, EventArgs e)
    {
        var opening = !pnlHelp.Visible;
        pnlHelp.Width = opening ? HelpPanelWidth : 0;
        pnlHelp.Visible = opening;
        btnHelp.Text = opening ? "✕ Hide Help" : "? Help";

        if (opening && !_webHelpReady)
        {
            InitWebHelp();
        }
    }

    private void btnCloseHelp_Click(object sender, EventArgs e)
    {
        pnlHelp.Visible = false;
        pnlHelp.Width = 0;
        btnHelp.Text = "? Help";
    }

    private void btnZoomIn_Click(object sender, EventArgs e)
    {
        _helpZoom = Math.Min(3.0, Math.Round(_helpZoom + 0.1, 1));
        ApplyZoom();
    }

    private void btnZoomOut_Click(object sender, EventArgs e)
    {
        _helpZoom = Math.Max(0.4, Math.Round(_helpZoom - 0.1, 1));
        ApplyZoom();
    }

    private void btnZoomReset_Click(object sender, EventArgs e)
    {
        _helpZoom = 1.0;
        ApplyZoom();
    }

    private void ApplyZoom()
    {
        if (_webHelpReady)
        {
            webHelp.ZoomFactor = _helpZoom;
        }
        lblZoom.Text = $"Zoom: {_helpZoom:P0}";
    }

    private async void InitWebHelp()
    {
        try
        {
            await webHelp.EnsureCoreWebView2Async(null);
            _webHelpReady = true;
            webHelp.ZoomFactor = _helpZoom;

            webHelp.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            webHelp.CoreWebView2.Settings.AreDevToolsEnabled = false;
            webHelp.CoreWebView2.Settings.IsZoomControlEnabled = false;

            webHelp.NavigateToString(BuildHelpHtml());
        }
        catch (Exception ex)
        {
            var lbl = new Label
            {
                Text = $"WebView2 runtime not found.\n\nInstall the Microsoft Edge WebView2 Runtime to view help.\n\n{ex.Message}",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft,
                Padding = new Padding(12),
                Font = new Font("Segoe UI", 9f)
            };
            pnlHelp.Controls.Add(lbl);
            lbl.BringToFront();
        }
    }

    private static string BuildHelpHtml() => """
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>SQL Data Migrator – Help</title>
<style>
  :root {
    --accent:     #1a5abf;
    --accent2:    #0e3d8a;
    --bg:         #f4f7ff;
    --surface:    #ffffff;
    --border:     #ccd8f0;
    --text:       #1a1a2e;
    --muted:      #4a5070;
    --tag-bg:     #e6effe;
    --tag-fg:     #1a5abf;
    --code-bg:    #edf1fb;
    --tip-bg:     #fffbea;
    --tip-border: #e8b800;
  }
  * { box-sizing: border-box; margin: 0; padding: 0; }
  html { scroll-behavior: smooth; }
  body {
    font-family: "Segoe UI", system-ui, sans-serif;
    font-size: 13px;
    line-height: 1.65;
    background: var(--bg);
    color: var(--text);
    padding: 20px 18px 48px;
  }

  /* ── Hero ── */
  .hero {
    background: linear-gradient(135deg, var(--accent2) 0%, #2673d9 100%);
    color: #fff;
    border-radius: 10px;
    padding: 22px 24px 18px;
    margin-bottom: 22px;
    box-shadow: 0 4px 16px rgba(14,61,138,.30);
  }
  .hero h1 { font-size: 17px; font-weight: 700; letter-spacing: .02em; margin-bottom: 5px; }
  .hero p  { font-size: 12px; opacity: .88; }

  /* ── TOC ── */
  .toc {
    background: var(--surface);
    border: 1px solid var(--border);
    border-radius: 8px;
    padding: 14px 18px 14px 14px;
    margin-bottom: 22px;
  }
  .toc-head { font-size: 10.5px; text-transform: uppercase; letter-spacing: .1em;
               color: var(--muted); margin-bottom: 8px; font-weight: 600; }
  .toc ol { padding-left: 20px; column-count: 2; column-gap: 10px; }
  .toc li { margin: 3px 0; break-inside: avoid; }
  .toc a  { color: var(--accent); text-decoration: none; font-size: 12px; }
  .toc a:hover { text-decoration: underline; }

  /* ── Sections ── */
  section {
    background: var(--surface);
    border: 1px solid var(--border);
    border-radius: 8px;
    padding: 16px 18px;
    margin-bottom: 14px;
    box-shadow: 0 1px 4px rgba(0,0,0,.04);
  }
  section h2 {
    font-size: 13px;
    font-weight: 700;
    color: var(--accent);
    display: flex;
    align-items: center;
    gap: 8px;
    padding-bottom: 8px;
    border-bottom: 2px solid var(--border);
    margin-bottom: 12px;
  }
  .num {
    background: var(--accent);
    color: #fff;
    border-radius: 50%;
    width: 21px; height: 21px;
    font-size: 10.5px;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    flex-shrink: 0;
    font-weight: 700;
  }

  /* ── Field grid ── */
  .fields { display: flex; flex-direction: column; gap: 7px; margin-top: 2px; }
  .field  { display: grid; grid-template-columns: 120px 1fr; gap: 8px; align-items: start; }
  .fname  {
    font-size: 11.5px; font-weight: 600;
    background: var(--tag-bg); color: var(--tag-fg);
    border-radius: 4px; padding: 2px 8px;
    text-align: center; white-space: nowrap;
    margin-top: 1px;
  }
  .fdesc { font-size: 12.5px; color: var(--muted); }

  /* ── Numbered steps ── */
  .steps { list-style: none; display: flex; flex-direction: column; gap: 5px; }
  .steps li { display: grid; grid-template-columns: 22px 1fr; gap: 8px; align-items: baseline; font-size: 12.5px; }
  .sn {
    background: var(--accent); color: #fff;
    border-radius: 50%;
    width: 19px; height: 19px;
    font-size: 10px;
    display: inline-flex; align-items: center; justify-content: center;
    font-weight: 700; flex-shrink: 0;
  }

  /* ── Code ── */
  code {
    background: var(--code-bg);
    border: 1px solid var(--border);
    border-radius: 3px;
    padding: 1px 5px;
    font-family: "Cascadia Code", Consolas, monospace;
    font-size: 11.5px;
    color: var(--accent2);
  }
  pre {
    background: var(--code-bg);
    border: 1px solid var(--border);
    border-radius: 6px;
    padding: 10px 14px;
    font-family: "Cascadia Code", Consolas, monospace;
    font-size: 11.5px;
    color: var(--accent2);
    margin: 10px 0;
    overflow-x: auto;
  }

  /* ── Log legend ── */
  .log-legend { display: flex; flex-direction: column; gap: 6px; margin-top: 6px; }
  .log-row { display: flex; align-items: center; gap: 10px; font-size: 12.5px; color: var(--muted); }
  .dot { width: 10px; height: 10px; border-radius: 50%; flex-shrink: 0; }

  /* ── Tip box ── */
  .tip {
    background: var(--tip-bg);
    border-left: 4px solid var(--tip-border);
    border-radius: 0 6px 6px 0;
    padding: 10px 14px;
    font-size: 12.5px;
  }
  .tip ul { padding-left: 16px; margin-top: 5px; }
  .tip li { margin: 4px 0; color: var(--muted); }

  p { font-size: 12.5px; color: var(--muted); margin: 6px 0; }
  strong { color: var(--text); }
</style>
</head>
<body>

<div class="hero">
  <h1>SQL Data Migrator — Help</h1>
  <p>Copy tables from a SQL Server source into a brand-new destination database, complete
  with schema, indexes, foreign keys, and functions/TVFs.</p>
</div>

<nav class="toc">
  <div class="toc-head">Contents</div>
  <ol>
    <li><a href="#src">Source Connection</a></li>
    <li><a href="#dst">Destination Connection</a></li>
    <li><a href="#tables">Loading Tables</a></li>
    <li><a href="#options">Options</a></li>
    <li><a href="#limits">Row Limits</a></li>
    <li><a href="#filters">WHERE Filters</a></li>
    <li><a href="#transfer">Running a Transfer</a></li>
    <li><a href="#eta">ETA Estimate</a></li>
    <li><a href="#project">Save / Load Project</a></li>
    <li><a href="#log">Transfer Log</a></li>
    <li><a href="#tips">Tips &amp; Gotchas</a></li>
  </ol>
</nav>

<section id="src">
  <h2><span class="num">1</span> Source Connection</h2>
  <div class="fields">
    <div class="field"><span class="fname">Server</span><span class="fdesc">Hostname, IP, or named instance — e.g. <code>localhost\SQLEXPRESS</code> or <code>10.0.0.5,1433</code></span></div>
    <div class="field"><span class="fname">Auth</span><span class="fdesc"><strong>Integrated Security</strong> uses your Windows credentials. <strong>SQL Login</strong> requires a username &amp; password.</span></div>
    <div class="field"><span class="fname">Trust Cert</span><span class="fdesc">Bypasses TLS certificate validation. Safe for dev/test; avoid in production.</span></div>
    <div class="field"><span class="fname">Load DBs</span><span class="fdesc">Connects to the server and populates the Database dropdown.</span></div>
    <div class="field"><span class="fname">Database</span><span class="fdesc">Select the source database you want to migrate from.</span></div>
    <div class="field"><span class="fname">Test</span><span class="fdesc">Verifies connectivity and logs the SQL Server version to the log panel.</span></div>
  </div>
</section>

<section id="dst">
  <h2><span class="num">2</span> Destination Connection</h2>
  <div class="fields">
    <div class="field"><span class="fname">Server</span><span class="fdesc">Target SQL Server instance — can be the same server as the source.</span></div>
    <div class="field"><span class="fname">New DB name</span><span class="fdesc">Name of the database that will be <strong>created</strong>. The tool refuses to proceed if it already exists.</span></div>
    <div class="field"><span class="fname">Auth / Trust</span><span class="fdesc">Same options as the source connection above.</span></div>
    <div class="field"><span class="fname">Test</span><span class="fdesc">Verifies the destination server is reachable before you start.</span></div>
  </div>
</section>

<section id="tables">
  <h2><span class="num">3</span> Loading Tables</h2>
  <p>Select a source database, then click <strong>Load Tables</strong>. All user tables appear with their estimated row counts.</p>
  <div class="fields" style="margin-top:10px">
    <div class="field"><span class="fname">Filter</span><span class="fdesc">Type any substring of <code>schema.table</code> to narrow the list in real time.</span></div>
    <div class="field"><span class="fname">Select all</span><span class="fdesc">Ticks or unticks every visible row at once.</span></div>
    <div class="field"><span class="fname">Copy ✓</span><span class="fdesc">The checkbox on each grid row controls whether that table is included in the transfer.</span></div>
  </div>
</section>

<section id="options">
  <h2><span class="num">4</span> Options</h2>
  <div class="fields">
    <div class="field"><span class="fname">Preserve identity</span><span class="fdesc">Keeps IDENTITY seed values intact (<code>SET IDENTITY_INSERT ON</code> during bulk copy).</span></div>
    <div class="field"><span class="fname">Script functions</span><span class="fdesc">Copies user-defined functions and TVFs to the destination before data transfer.</span></div>
    <div class="field"><span class="fname">Preserve indexes</span><span class="fdesc">Recreates non-clustered indexes on destination tables after data is loaded.</span></div>
    <div class="field"><span class="fname">Preserve FKs</span><span class="fdesc">Recreates foreign key constraints after all tables are copied, avoiding ordering conflicts.</span></div>
  </div>
</section>

<section id="limits">
  <h2><span class="num">5</span> Row Limits</h2>
  <p>Cap how many rows are copied per table — useful for smoke tests or partial migrations.</p>
  <div class="fields" style="margin-top:10px">
    <div class="field"><span class="fname">Row limit</span><span class="fdesc">Sets a <code>TOP N</code> value applied when reading from the source.</span></div>
    <div class="field"><span class="fname">Set All</span><span class="fdesc">Applies the current spinner value to every table.</span></div>
    <div class="field"><span class="fname">Set Selected</span><span class="fdesc">Applies only to ticked rows.</span></div>
    <div class="field"><span class="fname">Clear All/Sel</span><span class="fdesc">Removes the limit — all rows will be copied.</span></div>
  </div>
  <p style="margin-top:10px">You can also type directly in the <strong>Row Limit</strong> grid column. Type <code>All</code> or leave blank to remove.</p>
</section>

<section id="filters">
  <h2><span class="num">6</span> WHERE Filters</h2>
  <p>Enter a SQL predicate in the <strong>WHERE Filter</strong> column to restrict which rows are read from the source:</p>
  <pre>CreatedDate >= '2024-01-01' AND IsDeleted = 0</pre>
  <p>The filter is validated against the source before the transfer begins. Do <strong>not</strong> include the keyword <code>WHERE</code> itself.</p>
</section>

<section id="transfer">
  <h2><span class="num">7</span> Running a Transfer</h2>
  <p>Click <strong>Start Transfer</strong>. The tool executes these steps in order:</p>
  <ol class="steps" style="margin-top:10px">
    <li><span class="sn">1</span>Creates the destination database.</li>
    <li><span class="sn">2</span>Scripts and executes table schemas (<code>CREATE TABLE</code>).</li>
    <li><span class="sn">3</span>Bulk-copies rows for each selected table, respecting row limits and WHERE filters.</li>
    <li><span class="sn">4</span>Scripts and applies functions / TVFs (if enabled).</li>
    <li><span class="sn">5</span>Recreates non-clustered indexes (if enabled).</li>
    <li><span class="sn">6</span>Recreates foreign key constraints (if enabled).</li>
  </ol>
  <p style="margin-top:10px"><strong>Cancel</strong> — requests a graceful stop. The current table finishes before the operation halts.</p>
</section>

<section id="eta">
  <h2><span class="num">8</span> ETA Estimate</h2>
  <p>The <strong>ETA</strong> label estimates remaining time from a rolling rows-per-second average across completed tables.
  It requires at least <strong>3 tables with 50,000+ rows each</strong> before producing a reliable figure.
  Until then it shows a sampling-progress indicator.</p>
</section>

<section id="project">
  <h2><span class="num">9</span> Save / Load Project</h2>
  <div class="fields">
    <div class="field"><span class="fname">Save Project</span><span class="fdesc">Writes all settings, table selections, row limits, and WHERE filters to a <code>.sdm.json</code> file.</span></div>
    <div class="field"><span class="fname">Load Project</span><span class="fdesc">Restores a saved session — ideal for repeatable migrations or team handoffs.</span></div>
  </div>
</section>

<section id="log">
  <h2><span class="num">10</span> Transfer Log</h2>
  <p>Timestamped entries appear in the log panel for every operation:</p>
  <div class="log-legend">
    <div class="log-row"><span class="dot" style="background:#888"></span>Grey — informational / general progress</div>
    <div class="log-row"><span class="dot" style="background:#1a7a3a"></span>Green — table or phase completed successfully</div>
    <div class="log-row"><span class="dot" style="background:#c06000"></span>Orange — non-fatal warnings (skipped objects, etc.)</div>
    <div class="log-row"><span class="dot" style="background:#c00000"></span>Red — errors that stopped processing</div>
  </div>
  <p style="margin-top:8px">The log is capped at <strong>5,000 lines</strong> to prevent unbounded memory use.</p>
</section>

<section id="tips">
  <h2>Tips &amp; Gotchas</h2>
  <div class="tip">
    <ul>
      <li>Always click <strong>Test</strong> on both connections before starting — saves time diagnosing mid-run failures.</li>
      <li>Use a row limit of <strong>1,000–10,000</strong> for a quick smoke-test before committing to a full run.</li>
      <li>FK cycles can block the FK recreation step. Disable <em>Preserve FKs</em> and apply constraints manually if needed.</li>
      <li>If the destination server is the same as the source, make sure the new database name is unique.</li>
      <li>Connection settings and server history are saved automatically when the app closes.</li>
      <li>Use the <strong>+</strong> / <strong>−</strong> / <strong>Reset</strong> buttons in the toolbar above to zoom this panel.</li>
    </ul>
  </div>
</section>

</body>
</html>
""";

    private void ApplyRowLimitToTables(bool selectedOnly)
    {
        var limit = Decimal.ToInt32(numBulkRowLimit.Value);
        var targets = selectedOnly
            ? _allTables.Where(t => t.Selected).ToList()
            : _allTables;

        foreach (var item in targets)
        {
            item.RowLimit = limit;
        }

        SaveUiToSettings();
        RefreshTableGrid();
        Log(selectedOnly
            ? $"Applied row limit {limit:N0} to selected tables."
            : $"Applied row limit {limit:N0} to all tables.", LogLevel.Info);
    }

    private void ClearRowLimit(bool selectedOnly)
    {
        var targets = selectedOnly
            ? _allTables.Where(t => t.Selected).ToList()
            : _allTables;

        foreach (var item in targets)
        {
            item.RowLimit = 0;
        }

        RefreshTableGrid();
        Log(selectedOnly
            ? "Cleared row limit for selected tables (now All rows)."
            : "Cleared row limit for all tables (now All rows).", LogLevel.Info);
    }

    private void ConfigureLogTheme()
    {
        if (IsDarkModeUi())
        {
            logBox.BackColor = Color.FromArgb(28, 28, 28);
            logBox.ForeColor = Color.Gainsboro;
        }
        else
        {
            logBox.BackColor = Color.White;
            logBox.ForeColor = Color.Black;
        }
    }

    private bool IsDarkModeUi()
    {
        return BackColor.GetBrightness() < 0.45f || ForeColor.GetBrightness() > 0.6f;
    }

    private static void ApplyDarkGreenButtonTheme(Button button, bool isPrimary)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Color.FromArgb(46, 160, 67);

        button.BackColor = isPrimary
            ? Color.FromArgb(40, 150, 65)
            : Color.FromArgb(28, 92, 44);
        button.ForeColor = Color.FromArgb(230, 255, 230);

        button.FlatAppearance.MouseOverBackColor = Color.Lime;
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(120, 255, 120);

        button.MouseEnter += (_, _) => button.ForeColor = Color.Black;
        button.MouseLeave += (_, _) => button.ForeColor = Color.FromArgb(230, 255, 230);
    }

    private void WireEvents()
    {
        cmbSourceAuth.SelectedIndexChanged += (_, _) => ToggleAuthInputs(cmbSourceAuth, txtSourceUser, txtSourcePassword);
        cmbDestinationAuth.SelectedIndexChanged += (_, _) => ToggleAuthInputs(cmbDestinationAuth, txtDestinationUser, txtDestinationPassword);

        btnTestSource.Click += async (_, _) => await TestConnectionAsync(isSource: true);
        btnTestDestination.Click += async (_, _) => await TestConnectionAsync(isSource: false);
        btnLoadSourceDbs.Click += async (_, _) => await LoadSourceDatabasesAsync();
        btnLoadTables.Click += async (_, _) => await LoadSourceTablesAsync();
        btnStartTransfer.Click += async (_, _) => await StartTransferAsync();
        btnCancelTransfer.Click += (_, _) => CancelTransfer();
        btnSetLimitAll.Click += (_, _) => ApplyRowLimitToTables(selectedOnly: false);
        btnSetLimitSelected.Click += (_, _) => ApplyRowLimitToTables(selectedOnly: true);
        btnClearLimitAll.Click += (_, _) => ClearRowLimit(selectedOnly: false);
        btnClearLimitSelected.Click += (_, _) => ClearRowLimit(selectedOnly: true);
        btnSaveProject.Click += (_, _) => SaveProjectFile();
        btnLoadProject.Click += (_, _) => LoadProjectFile();
        txtFilter.TextChanged += (_, _) => RefreshTableGrid();
        txtDestinationDatabase.TextChanged += (_, _) => ClearDestinationValidationErrors();
        cmbDestinationServer.TextChanged += (_, _) => ClearDestinationValidationErrors();
        numBulkRowLimit.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                ApplyRowLimitToTables(selectedOnly: true);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        };

        chkSelectAll.CheckedChanged += (_, _) =>
        {
            foreach (DataGridViewRow row in gridTables.Rows)
            {
                row.Cells["Selected"].Value = chkSelectAll.Checked;
            }
            SyncGridToModel();
        };

        gridTables.CellValueChanged += (_, e) =>
        {
            if (_updatingGrid || e.RowIndex < 0)
            {
                return;
            }

            SyncGridToModel();
        };

        gridTables.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (gridTables.IsCurrentCellDirty)
            {
                gridTables.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };

        FormClosing += (_, _) => SaveUiToSettings();
    }

    private void LoadSettingsIntoUi()
    {
        PopulateServerCombo(cmbSourceServer, _settings.SourceServerHistory);
        PopulateServerCombo(cmbDestinationServer, _settings.DestinationServerHistory);

        cmbSourceServer.Text = _settings.SourceServer;
        cmbDestinationServer.Text = _settings.DestinationServer;
        txtDestinationDatabase.Text = string.IsNullOrWhiteSpace(_settings.DestinationDatabaseName) ? "TestDb_Copy" : _settings.DestinationDatabaseName;

        SelectAuthMode(cmbSourceAuth, _settings.SourceAuthMode);
        SelectAuthMode(cmbDestinationAuth, _settings.DestinationAuthMode);

        txtSourceUser.Text = _settings.SourceUserName;
        txtDestinationUser.Text = _settings.DestinationUserName;
        chkSourceTrustCert.Checked = _settings.SourceTrustCertificate;
        chkDestinationTrustCert.Checked = _settings.DestinationTrustCertificate;
        chkKeepIdentity.Checked = _settings.PreserveIdentity;
        chkScriptFunctions.Checked = _settings.ScriptFunctionsAndTvfs;
        chkPreserveIndexes.Checked = _settings.PreserveIndexes;
        chkPreserveForeignKeys.Checked = _settings.PreserveForeignKeys;
        var value = _settings.LastBulkRowLimit <= 0 ? 1000 : _settings.LastBulkRowLimit;
        numBulkRowLimit.Value = Math.Min(numBulkRowLimit.Maximum, Math.Max(numBulkRowLimit.Minimum, value));

        ToggleAuthInputs(cmbSourceAuth, txtSourceUser, txtSourcePassword);
        ToggleAuthInputs(cmbDestinationAuth, txtDestinationUser, txtDestinationPassword);
    }

    private void SaveUiToSettings()
    {
        _settings.SourceServer = cmbSourceServer.Text.Trim();
        _settings.DestinationServer = cmbDestinationServer.Text.Trim();
        _settings.SourceAuthMode = cmbSourceAuth.Text;
        _settings.DestinationAuthMode = cmbDestinationAuth.Text;
        _settings.SourceUserName = txtSourceUser.Text.Trim();
        _settings.DestinationUserName = txtDestinationUser.Text.Trim();
        _settings.SourceTrustCertificate = chkSourceTrustCert.Checked;
        _settings.DestinationTrustCertificate = chkDestinationTrustCert.Checked;
        _settings.PreserveIdentity = chkKeepIdentity.Checked;
        _settings.ScriptFunctionsAndTvfs = chkScriptFunctions.Checked;
        _settings.PreserveIndexes = chkPreserveIndexes.Checked;
        _settings.PreserveForeignKeys = chkPreserveForeignKeys.Checked;
        _settings.LastBulkRowLimit = Decimal.ToInt32(numBulkRowLimit.Value);
        _settings.DestinationDatabaseName = txtDestinationDatabase.Text.Trim();
        _settings.SourceDatabase = cmbSourceDatabase.Text;

        PushServerHistory(_settings.SourceServerHistory, _settings.SourceServer);
        PushServerHistory(_settings.DestinationServerHistory, _settings.DestinationServer);

        _settingsStore.Save(_settings);
    }

    private void SaveProjectFile()
    {
        try
        {
            SyncGridToModel();

            using var dialog = new SaveFileDialog
            {
                Title = "Save session project",
                Filter = "SQL Data Migrator Project (*.sdm.json)|*.sdm.json|JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = "json",
                AddExtension = true,
                FileName = string.IsNullOrWhiteSpace(_settings.LastProjectFilePath)
                    ? "session.sdm.json"
                    : Path.GetFileName(_settings.LastProjectFilePath),
                InitialDirectory = string.IsNullOrWhiteSpace(_settings.LastProjectFilePath)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                    : Path.GetDirectoryName(_settings.LastProjectFilePath)
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            var project = new SessionProject
            {
                SavedAtUtc = DateTime.UtcNow,
                SourceServerHistory = new List<string>(_settings.SourceServerHistory),
                DestinationServerHistory = new List<string>(_settings.DestinationServerHistory),
                SourceServer = cmbSourceServer.Text.Trim(),
                SourceDatabase = cmbSourceDatabase.Text,
                SourceAuthMode = cmbSourceAuth.Text,
                SourceUserName = txtSourceUser.Text.Trim(),
                SourcePassword = txtSourcePassword.Text,
                SourceTrustCertificate = chkSourceTrustCert.Checked,
                DestinationServer = cmbDestinationServer.Text.Trim(),
                DestinationDatabaseName = txtDestinationDatabase.Text.Trim(),
                DestinationAuthMode = cmbDestinationAuth.Text,
                DestinationUserName = txtDestinationUser.Text.Trim(),
                DestinationPassword = txtDestinationPassword.Text,
                DestinationTrustCertificate = chkDestinationTrustCert.Checked,
                PreserveIdentity = chkKeepIdentity.Checked,
                ScriptFunctionsAndTvfs = chkScriptFunctions.Checked,
                PreserveIndexes = chkPreserveIndexes.Checked,
                PreserveForeignKeys = chkPreserveForeignKeys.Checked,
                BulkRowLimit = Decimal.ToInt32(numBulkRowLimit.Value),
                FilterText = txtFilter.Text,
                SelectAllChecked = chkSelectAll.Checked,
                Tables = _allTables.Select(t => new TableSelectionItem
                {
                    Selected = t.Selected,
                    Schema = t.Schema,
                    Table = t.Table,
                    RowCount = t.RowCount,
                    RowLimit = t.RowLimit,
                    RowFilter = t.RowFilter
                }).ToList()
            };

            var json = JsonSerializer.Serialize(project, ProjectJsonOptions);
            File.WriteAllText(dialog.FileName, json);

            _settings.LastProjectFilePath = dialog.FileName;
            SaveUiToSettings();
            Log($"Project saved: {dialog.FileName}", LogLevel.Success);
        }
        catch (Exception ex)
        {
            Log($"Failed saving project: {ex.Message}", LogLevel.Error);
        }
    }

    private void LoadProjectFile()
    {
        try
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Load session project",
                Filter = "SQL Data Migrator Project (*.sdm.json)|*.sdm.json|JSON files (*.json)|*.json|All files (*.*)|*.*",
                InitialDirectory = string.IsNullOrWhiteSpace(_settings.LastProjectFilePath)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                    : Path.GetDirectoryName(_settings.LastProjectFilePath)
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            var json = File.ReadAllText(dialog.FileName);
            var project = JsonSerializer.Deserialize<SessionProject>(json, ProjectJsonOptions);
            if (project is null)
            {
                throw new InvalidOperationException("Project file is empty or invalid.");
            }

            cmbSourceServer.Text = project.SourceServer;
            cmbDestinationServer.Text = project.DestinationServer;

            if (!string.IsNullOrWhiteSpace(project.SourceDatabase))
            {
                if (!cmbSourceDatabase.Items.Contains(project.SourceDatabase))
                {
                    cmbSourceDatabase.Items.Add(project.SourceDatabase);
                }
                cmbSourceDatabase.SelectedItem = project.SourceDatabase;
            }

            SelectAuthMode(cmbSourceAuth, project.SourceAuthMode);
            SelectAuthMode(cmbDestinationAuth, project.DestinationAuthMode);

            txtSourceUser.Text = project.SourceUserName;
            txtSourcePassword.Text = project.SourcePassword;
            chkSourceTrustCert.Checked = project.SourceTrustCertificate;

            txtDestinationDatabase.Text = project.DestinationDatabaseName;
            txtDestinationUser.Text = project.DestinationUserName;
            txtDestinationPassword.Text = project.DestinationPassword;
            chkDestinationTrustCert.Checked = project.DestinationTrustCertificate;

            chkKeepIdentity.Checked = project.PreserveIdentity;
            chkScriptFunctions.Checked = project.ScriptFunctionsAndTvfs;
            chkPreserveIndexes.Checked = project.PreserveIndexes;
            chkPreserveForeignKeys.Checked = project.PreserveForeignKeys;

            numBulkRowLimit.Value = Math.Min(numBulkRowLimit.Maximum, Math.Max(numBulkRowLimit.Minimum, project.BulkRowLimit <= 0 ? 1000 : project.BulkRowLimit));
            txtFilter.Text = project.FilterText ?? string.Empty;
            chkSelectAll.Checked = project.SelectAllChecked;

            _allTables.Clear();
            foreach (var table in project.Tables)
            {
                _allTables.Add(new TableSelectionItem
                {
                    Selected = table.Selected,
                    Schema = table.Schema,
                    Table = table.Table,
                    RowCount = table.RowCount,
                    RowLimit = table.RowLimit < 0 ? 0 : table.RowLimit,
                    RowFilter = table.RowFilter ?? string.Empty
                });
            }

            _settings.SourceServerHistory = project.SourceServerHistory ?? new List<string>();
            _settings.DestinationServerHistory = project.DestinationServerHistory ?? new List<string>();
            _settings.LastProjectFilePath = dialog.FileName;

            RefreshTableGrid();
            SaveUiToSettings();
            LoadSettingsIntoUi();
            Log($"Project loaded: {dialog.FileName}", LogLevel.Success);
        }
        catch (Exception ex)
        {
            Log($"Failed loading project: {ex.Message}", LogLevel.Error);
        }
    }

    private static void PushServerHistory(List<string> history, string server)
    {
        if (string.IsNullOrWhiteSpace(server))
        {
            return;
        }

        history.RemoveAll(item => string.Equals(item, server, StringComparison.OrdinalIgnoreCase));
        history.Insert(0, server);

        if (history.Count > 20)
        {
            history.RemoveRange(20, history.Count - 20);
        }
    }

    private static void PopulateServerCombo(ComboBox combo, List<string> values)
    {
        combo.Items.Clear();
        foreach (var value in values.Where(v => !string.IsNullOrWhiteSpace(v)))
        {
            combo.Items.Add(value);
        }
    }

    private static void SelectAuthMode(ComboBox combo, string authMode)
    {
        if (string.Equals(authMode, "SQL Login", StringComparison.OrdinalIgnoreCase))
        {
            combo.SelectedItem = "SQL Login";
            return;
        }

        combo.SelectedItem = "Integrated Security";
    }

    private static void ToggleAuthInputs(ComboBox authCombo, TextBox user, TextBox pass)
    {
        var sqlLogin = string.Equals(authCombo.Text, "SQL Login", StringComparison.OrdinalIgnoreCase);
        user.Enabled = sqlLogin;
        pass.Enabled = sqlLogin;
        if (!sqlLogin)
        {
            pass.Text = string.Empty;
        }
    }

    private ConnectionProfile BuildConnectionProfile(bool isSource, string database)
    {
        return new ConnectionProfile
        {
            Server = isSource ? cmbSourceServer.Text.Trim() : cmbDestinationServer.Text.Trim(),
            Database = database,
            AuthMode = isSource ? cmbSourceAuth.Text : cmbDestinationAuth.Text,
            UserName = isSource ? txtSourceUser.Text.Trim() : txtDestinationUser.Text.Trim(),
            Password = isSource ? txtSourcePassword.Text : txtDestinationPassword.Text,
            TrustCertificate = isSource ? chkSourceTrustCert.Checked : chkDestinationTrustCert.Checked
        };
    }

    private async Task TestConnectionAsync(bool isSource)
    {
        var server = isSource ? cmbSourceServer.Text.Trim() : cmbDestinationServer.Text.Trim();
        if (string.IsNullOrWhiteSpace(server))
        {
            MessageBox.Show("Enter a server name.", "Missing value", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            var profile = BuildConnectionProfile(isSource, "master");
            var result = await _engine.TestConnectionAsync(profile, CancellationToken.None);
            Log($"{(isSource ? "Source" : "Destination")} connection OK ({result}).", LogLevel.Success);
            SaveUiToSettings();
            LoadSettingsIntoUi();
        }
        catch (Exception ex)
        {
            Log($"{(isSource ? "Source" : "Destination")} connection failed: {ex.Message}", LogLevel.Error);
        }
    }

    private async Task LoadSourceDatabasesAsync()
    {
        var server = cmbSourceServer.Text.Trim();
        if (string.IsNullOrWhiteSpace(server))
        {
            MessageBox.Show("Enter a source server.", "Missing value", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            var profile = BuildConnectionProfile(isSource: true, "master");
            var databases = await _engine.LoadDatabasesAsync(profile, CancellationToken.None);

            cmbSourceDatabase.Items.Clear();
            foreach (var db in databases)
            {
                cmbSourceDatabase.Items.Add(db);
            }

            if (!string.IsNullOrWhiteSpace(_settings.SourceDatabase)
                && cmbSourceDatabase.Items.Contains(_settings.SourceDatabase))
            {
                cmbSourceDatabase.SelectedItem = _settings.SourceDatabase;
            }
            else if (cmbSourceDatabase.Items.Count > 0)
            {
                cmbSourceDatabase.SelectedIndex = 0;
            }

            Log($"Loaded {databases.Count} source databases.", LogLevel.Info);
            SaveUiToSettings();
        }
        catch (Exception ex)
        {
            Log($"Failed loading source databases: {ex.Message}", LogLevel.Error);
        }
    }

    private async Task LoadSourceTablesAsync()
    {
        if (string.IsNullOrWhiteSpace(cmbSourceDatabase.Text))
        {
            MessageBox.Show("Select a source database first.", "Missing value", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            var profile = BuildConnectionProfile(isSource: true, cmbSourceDatabase.Text);
            var tables = await _engine.LoadTablesAsync(profile, CancellationToken.None);

            _allTables.Clear();
            _allTables.AddRange(tables);

            RefreshTableGrid();
            Log($"Loaded {tables.Count} table(s).", LogLevel.Success);

            foreach (DataGridViewRow row in gridTables.Rows)
            {
                row.Cells["Selected"].Value = true;
            }
        }
        catch (Exception ex)
        {
            Log($"Failed loading tables: {ex.Message}", LogLevel.Error);
        }
    }

    private void RefreshTableGrid()
    {
        _updatingGrid = true;
        gridTables.Rows.Clear();

        var filter = txtFilter.Text.Trim();
        foreach (var item in _allTables)
        {
            var qualified = $"{item.Schema}.{item.Table}";
            if (!string.IsNullOrWhiteSpace(filter)
                && qualified.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            var rowIndex = gridTables.Rows.Add();
            var row = gridTables.Rows[rowIndex];
            row.Cells["Selected"].Value = item.Selected;
            row.Cells["Schema"].Value = item.Schema;
            row.Cells["Table"].Value = item.Table;
            row.Cells["RowCount"].Value = item.RowCount;
            row.Cells["RowLimit"].Value = item.RowLimit <= 0 ? "All" : item.RowLimit.ToString();
            row.Cells["RowFilter"].Value = item.RowFilter;
        }

        _updatingGrid = false;
    }

    private void SyncGridToModel()
    {
        foreach (DataGridViewRow row in gridTables.Rows)
        {
            var schema = Convert.ToString(row.Cells["Schema"].Value) ?? string.Empty;
            var table = Convert.ToString(row.Cells["Table"].Value) ?? string.Empty;

            var model = _allTables.FirstOrDefault(t =>
                string.Equals(t.Schema, schema, StringComparison.OrdinalIgnoreCase)
                && string.Equals(t.Table, table, StringComparison.OrdinalIgnoreCase));
            if (model is null)
            {
                continue;
            }

            model.Selected = Convert.ToBoolean(row.Cells["Selected"].Value ?? false);

            var limitText = Convert.ToString(row.Cells["RowLimit"].Value) ?? string.Empty;
            model.RowLimit = ParseRowLimit(limitText);
            row.Cells["RowLimit"].Value = model.RowLimit <= 0 ? "All" : model.RowLimit.ToString();

            var rowFilterText = Convert.ToString(row.Cells["RowFilter"].Value) ?? string.Empty;
            model.RowFilter = rowFilterText.Trim();
            row.Cells["RowFilter"].Value = model.RowFilter;
        }
    }

    private static int ParseRowLimit(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var normalized = text.Trim();
        if (string.Equals(normalized, "all", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "nolimit", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "none", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "*", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return int.TryParse(normalized, out var parsed) && parsed > 0 ? parsed : 0;
    }

    private async Task StartTransferAsync()
    {
        ClearDestinationValidationErrors();
        SyncGridToModel();
        var selected = _allTables.Where(t => t.Selected).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show("Select at least one table.", "Nothing selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(cmbSourceDatabase.Text))
        {
            MessageBox.Show("Select a source database.", "Missing value", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(txtDestinationDatabase.Text))
        {
            MessageBox.Show("Enter destination database name.", "Missing value", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!await ValidateDestinationDatabaseIsNewAsync())
        {
            return;
        }

        try
        {
            var sourceProfile = BuildConnectionProfile(isSource: true, cmbSourceDatabase.Text);
            var filterErrors = await _engine.ValidateWhereFiltersAsync(sourceProfile, selected, CancellationToken.None);
            if (filterErrors.Count > 0)
            {
                Log("Transfer blocked: one or more WHERE filters are invalid.", LogLevel.Error);
                foreach (var err in filterErrors)
                {
                    Log(err, LogLevel.Error);
                }

                MessageBox.Show(
                    "Fix invalid WHERE filters listed in the log before starting transfer.",
                    "Invalid WHERE filter",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }
        }
        catch (Exception ex)
        {
            Log($"Filter validation failed: {ex.Message}", LogLevel.Error);
            MessageBox.Show(
                "Could not validate WHERE filters. Check source connection and try again.",
                "Filter validation failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        _transferCts = new CancellationTokenSource();
        btnStartTransfer.Enabled = false;
        btnCancelTransfer.Enabled = true;
        progressTransfer.Value = 0;
        lblStatus.Text = "Running transfer...";
        lblEta.Text = "ETA --:--";

        try
        {
            SaveUiToSettings();
            var request = new TransferRequest
            {
                Source = BuildConnectionProfile(isSource: true, cmbSourceDatabase.Text),
                Destination = BuildConnectionProfile(isSource: false, txtDestinationDatabase.Text.Trim()),
                DestinationDatabaseName = txtDestinationDatabase.Text.Trim(),
                PreserveIdentity = chkKeepIdentity.Checked,
                ScriptFunctionsAndTvfs = chkScriptFunctions.Checked,
                PreserveIndexes = chkPreserveIndexes.Checked,
                PreserveForeignKeys = chkPreserveForeignKeys.Checked,
                Tables = selected.Select(t => new TableSelectionItem
                {
                    Selected = t.Selected,
                    Schema = t.Schema,
                    Table = t.Table,
                    RowCount = t.RowCount,
                    RowLimit = t.RowLimit,
                    RowFilter = t.RowFilter
                }).ToList()
            };

            ResetEtaEstimate(request.Tables.Where(t => t.Selected));

            var summary = await _engine.TransferAsync(
                request,
                _transferCts.Token,
                (msg, level) => Log(msg, (LogLevel)level),
                percent => UpdateProgress(percent),
                status => UpdateStatusWithEta(status),
                metric => UpdateEtaFromTable(metric));

            lblStatus.Text = "Transfer completed.";
            lblEta.Text = "ETA 0:00";
            Log($"Summary: Success={summary.SuccessCount}, Failed={summary.FailCount}", LogLevel.Success);
        }
        catch (OperationCanceledException)
        {
            lblStatus.Text = "Transfer cancelled.";
            lblEta.Text = "ETA --:--";
            Log("Transfer cancelled by user.", LogLevel.Warning);
        }
        catch (Exception ex)
        {
            lblStatus.Text = "Transfer stopped.";
            lblEta.Text = "ETA --:--";
            Log($"Transfer failed: {ex.Message}", LogLevel.Error);
        }
        finally
        {
            btnStartTransfer.Enabled = true;
            btnCancelTransfer.Enabled = false;
            _transferCts?.Dispose();
            _transferCts = null;
        }
    }

    private void CancelTransfer()
    {
        _transferCts?.Cancel();
        Log("Cancel requested. Finishing current operation...", LogLevel.Warning);
    }

    private void UpdateProgress(int percent)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => UpdateProgress(percent)));
            return;
        }

        var safePercent = Math.Max(0, Math.Min(100, percent));
        progressTransfer.Value = safePercent;
    }

    private async Task<bool> ValidateDestinationDatabaseIsNewAsync()
    {
        var destinationDbName = txtDestinationDatabase.Text.Trim();
        if (string.IsNullOrWhiteSpace(destinationDbName))
        {
            return false;
        }

        try
        {
            var destinationProfile = BuildConnectionProfile(isSource: false, "master");
            var exists = await _engine.DatabaseExistsAsync(destinationProfile, destinationDbName, CancellationToken.None);
            if (!exists)
            {
                return true;
            }

            _errorProvider.SetError(cmbDestinationServer, "Destination already contains this database name.");
            _errorProvider.SetError(txtDestinationDatabase, "Use a new destination database name.");
            Log($"Transfer blocked: destination database '{destinationDbName}' already exists.", LogLevel.Error);
            return false;
        }
        catch (Exception ex)
        {
            _errorProvider.SetError(cmbDestinationServer, "Could not validate destination server/database.");
            _errorProvider.SetError(txtDestinationDatabase, "Could not validate destination server/database.");
            Log($"Transfer blocked: destination validation failed - {ex.Message}", LogLevel.Error);
            return false;
        }
    }

    private void ClearDestinationValidationErrors()
    {
        _errorProvider.SetError(cmbDestinationServer, string.Empty);
        _errorProvider.SetError(txtDestinationDatabase, string.Empty);
    }

    private void ResetEtaEstimate(IEnumerable<TableSelectionItem> selectedTables)
    {
        _etaSampleSecondsPerRow.Clear();
        _etaProcessedRows = 0;
        _etaTotalRows = selectedTables.Sum(t => t.RowLimit > 0 ? Math.Min(t.RowCount, t.RowLimit) : t.RowCount);
        lblEta.Text = "ETA --:--";
    }

    private void UpdateEtaFromTable(TableTransferMetric metric)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => UpdateEtaFromTable(metric)));
            return;
        }

        var effectiveRows = metric.RowsCopied > 0 ? metric.RowsCopied : metric.EstimatedRows;

        if (metric.Success)
        {
            _etaProcessedRows += effectiveRows;
        }

        if (metric.Success && effectiveRows >= EtaMinSampleRows && metric.Elapsed.TotalSeconds >= 0.5)
        {
            var secondsPerRow = metric.Elapsed.TotalSeconds / effectiveRows;
            _etaSampleSecondsPerRow.Enqueue(secondsPerRow);
            while (_etaSampleSecondsPerRow.Count > EtaRequiredSamples)
            {
                _etaSampleSecondsPerRow.Dequeue();
            }
        }

        UpdateStatusWithEta($"Completed [{metric.Schema}].[{metric.Table}] ({effectiveRows:N0} rows)");
    }

    private void UpdateStatusWithEta(string baseStatus)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => UpdateStatusWithEta(baseStatus)));
            return;
        }

        lblStatus.Text = baseStatus;

        if (_etaTotalRows <= 0)
        {
            lblEta.Text = "ETA --:--";
            return;
        }

        if (_etaSampleSecondsPerRow.Count < EtaRequiredSamples)
        {
            var processedPercent = _etaTotalRows == 0 ? 0 : (_etaProcessedRows * 100.0 / _etaTotalRows);
            lblEta.Text = $"ETA sampling {_etaSampleSecondsPerRow.Count}/{EtaRequiredSamples} ({processedPercent:0.0}%)";
            return;
        }

        var avgSecondsPerRow = _etaSampleSecondsPerRow.Average();
        var remainingRows = Math.Max(0, _etaTotalRows - _etaProcessedRows);
        var etaSeconds = remainingRows <= 0 ? 0 : remainingRows * avgSecondsPerRow;
        var eta = TimeSpan.FromSeconds(Math.Max(0, etaSeconds));

        var etaText = eta.TotalHours >= 1
            ? eta.ToString(@"h\:mm\:ss")
            : eta.ToString(@"m\:ss");

        var processedPercentFinal = _etaTotalRows == 0 ? 0 : (_etaProcessedRows * 100.0 / _etaTotalRows);
        lblEta.Text = $"ETA {etaText} ({processedPercentFinal:0.0}%)";
    }

    private void Log(string message, LogLevel level)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => Log(message, level)));
            return;
        }

        var color = level switch
        {
            LogLevel.Warning => IsDarkModeUi() ? Color.Gold : Color.DarkOrange,
            LogLevel.Error => IsDarkModeUi() ? Color.OrangeRed : Color.Firebrick,
            LogLevel.Success => IsDarkModeUi() ? Color.LimeGreen : Color.ForestGreen,
            _ => IsDarkModeUi() ? Color.Gainsboro : Color.Black
        };

        var line = $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
        logBox.SelectionStart = logBox.TextLength;
        logBox.SelectionLength = 0;
        logBox.SelectionColor = color;
        logBox.AppendText(line);

        if (logBox.Lines.Length > MaxLogLines)
        {
            var lines = logBox.Lines;
            var keepFrom = lines.Length - MaxLogLines;
            logBox.Lines = lines.Skip(keepFrom).ToArray();
        }

        logBox.SelectionColor = logBox.ForeColor;
        logBox.ScrollToCaret();
    }

    private void btnStartTransfer_Click(object sender, EventArgs e)
    {

    }

    private void btnLoadSourceDbs_Click(object sender, EventArgs e)
    {

    }

    private void Form1_Load(object sender, EventArgs e)
    {

    }

    private void btnLoadTables_Click(object sender, EventArgs e)
    {
        logBox.Clear();
    }

    private enum LogLevel
    {
        Info,
        Warning,
        Error,
        Success
    }
}
