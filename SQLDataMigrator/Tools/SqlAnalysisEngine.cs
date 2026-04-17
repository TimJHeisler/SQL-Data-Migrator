using System.Diagnostics;
using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Data.SqlClient;

namespace SQLQueryOptimizer;

internal sealed record AnalysisRequest(
    string ConnectionString,
    string QueryText,
    int TimeoutSeconds,
    int BaselineRuns,
    bool EnableJoinSandbox);

internal sealed record Insight(string Title, int Score, string Evidence, string Recommendation);

internal sealed record QueryVariant(string Name, string Sql, string? JoinLabel);

internal sealed class QueryResultPreview
{
    public required IReadOnlyList<string> Columns { get; init; }

    public required IReadOnlyList<IReadOnlyList<string>> Rows { get; init; }

    public static QueryResultPreview Empty { get; } = new()
    {
        Columns = Array.Empty<string>(),
        Rows = Array.Empty<IReadOnlyList<string>>()
    };
}

internal sealed class AnalysisReport
{
    public int SqlMajorVersion { get; init; }

    public bool HasWriteDatabasePermissions { get; init; }

    public required string InputShape { get; init; }

    public required IReadOnlyList<SqlExecutionResult> BaselineRuns { get; init; }

    public required IReadOnlyList<SqlExecutionResult> VariantRuns { get; init; }

    public required IReadOnlyList<Insight> TopInsights { get; init; }

    public required QueryResultPreview QueryResultPreview { get; init; }

    public required IReadOnlyList<string> ProcedureDecomposition { get; init; }

    public required IReadOnlyList<string> Notes { get; init; }

    public string Render()
    {
        var sb = new StringBuilder();

        sb.AppendLine("SQL Query Optimizer Report");
        sb.AppendLine(new string('=', 70));
        sb.AppendLine($"Target: SQL Server {SqlMajorVersion} (supported: 2017+)");
        sb.AppendLine($"Input shape: {InputShape}");
        sb.AppendLine($"Guardrail check: {(HasWriteDatabasePermissions ? "WARNING - login appears to have write permissions" : "OK - login appears read-focused")}");
        sb.AppendLine();

        var successfulBaselineRuns = BaselineRuns.Where(x => x.Success).ToList();
        if (successfulBaselineRuns.Count == 0)
        {
            sb.AppendLine("No successful baseline runs.");
            if (BaselineRuns.Count > 0)
            {
                sb.AppendLine($"Last error: {BaselineRuns[^1].ErrorMessage}");
            }

            return sb.ToString();
        }

        sb.AppendLine("Baseline Metrics (static parameter set)");
        sb.AppendLine("-".PadRight(70, '-'));
        sb.AppendLine($"Runs: {successfulBaselineRuns.Count}/{BaselineRuns.Count} successful");
        sb.AppendLine($"Median client elapsed: {Median(successfulBaselineRuns.Select(x => x.ClientElapsedMs)):N0} ms");
        sb.AppendLine($"P95 client elapsed: {P95(successfulBaselineRuns.Select(x => x.ClientElapsedMs)):N0} ms");
        sb.AppendLine($"Median SQL CPU: {Median(successfulBaselineRuns.Select(x => x.CpuMs)):N0} ms");
        sb.AppendLine($"Median logical reads: {Median(successfulBaselineRuns.Select(x => x.LogicalReads)):N0}");
        sb.AppendLine($"Median rows returned: {Median(successfulBaselineRuns.Select(x => (long)x.RowsReturned)):N0}");

        var spread = ComputeSpreadPercent(successfulBaselineRuns.Select(x => x.ClientElapsedMs));
        sb.AppendLine($"Runtime spread: {spread:N1}% {(spread > 25 ? "(high variance)" : "(stable)")}");
        sb.AppendLine();

        if (TopInsights.Count > 0)
        {
            sb.AppendLine("Top 3 likely causes of slowness");
            sb.AppendLine("-".PadRight(70, '-'));
            for (var i = 0; i < TopInsights.Count; i++)
            {
                var insight = TopInsights[i];
                sb.AppendLine($"{i + 1}) {insight.Title} (score {insight.Score}/100)");
                sb.AppendLine($"   Evidence: {insight.Evidence}");
                sb.AppendLine($"   Recommended next change: {insight.Recommendation}");
            }

            sb.AppendLine();
        }

        if (VariantRuns.Count > 0)
        {
            sb.AppendLine("Join Sandbox Results (COUNT_BIG ladder)");
            sb.AppendLine("-".PadRight(70, '-'));
            foreach (var run in VariantRuns)
            {
                if (run.Success)
                {
                    var label = string.IsNullOrWhiteSpace(run.JoinLabel) ? string.Empty : $" [{run.JoinLabel}]";
                    sb.AppendLine($"- {run.Name}{label}: {run.ClientElapsedMs:N0} ms, CPU {run.CpuMs:N0} ms, reads {run.LogicalReads:N0}, rows {run.RowsReturned:N0}");
                }
                else
                {
                    sb.AppendLine($"- {run.Name}: FAILED ({run.ErrorMessage})");
                }
            }

            sb.AppendLine();
        }

        if (Notes.Count > 0)
        {
            sb.AppendLine("Notes");
            sb.AppendLine("-".PadRight(70, '-'));
            foreach (var note in Notes)
            {
                sb.AppendLine($"- {note}");
            }
        }

        if (ProcedureDecomposition.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Procedure Decomposition");
            sb.AppendLine("-".PadRight(70, '-'));
            foreach (var line in ProcedureDecomposition)
            {
                sb.AppendLine($"- {line}");
            }
        }

        return sb.ToString();
    }

    private static double Median(IEnumerable<long> values)
    {
        var sorted = values.OrderBy(x => x).ToArray();
        if (sorted.Length == 0)
        {
            return 0;
        }

        if (sorted.Length % 2 == 1)
        {
            return sorted[sorted.Length / 2];
        }

        return (sorted[(sorted.Length / 2) - 1] + sorted[sorted.Length / 2]) / 2.0;
    }

    private static double P95(IEnumerable<long> values)
    {
        var sorted = values.OrderBy(x => x).ToArray();
        if (sorted.Length == 0)
        {
            return 0;
        }

        var index = (int)Math.Ceiling(sorted.Length * 0.95) - 1;
        index = Math.Clamp(index, 0, sorted.Length - 1);
        return sorted[index];
    }

    private static double ComputeSpreadPercent(IEnumerable<long> values)
    {
        var list = values.ToList();
        if (list.Count <= 1)
        {
            return 0;
        }

        var min = list.Min();
        var max = list.Max();
        if (min <= 0)
        {
            return 0;
        }

        return ((max - min) / (double)min) * 100.0;
    }
}

internal sealed class SqlExecutionResult
{
    public required string Name { get; init; }

    public bool Success { get; init; }

    public bool TimedOut { get; init; }

    public required long ClientElapsedMs { get; init; }

    public required long SqlElapsedMs { get; init; }

    public required long CpuMs { get; init; }

    public required long LogicalReads { get; init; }

    public required int RowsReturned { get; init; }

    public string? ErrorMessage { get; init; }

    public string PlanXml { get; init; } = string.Empty;

    public IReadOnlyList<string> Messages { get; init; } = Array.Empty<string>();

    public string? JoinLabel { get; init; }

    public QueryResultPreview QueryResultPreview { get; init; } = QueryResultPreview.Empty;
}

internal sealed class SqlAnalysisEngine
{
    private static readonly Regex LogicalReadsRegex = new(@"logical reads\s+(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CpuElapsedRegex = new(@"CPU time =\s*(\d+)\s*ms,\s*elapsed time =\s*(\d+)\s*ms", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DmlRegex = new(@"\b(insert|update|delete|merge|truncate|drop|alter\s+table|create\s+table)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ReadQueryStartRegex = new(@"^\s*(select|with|exec(?:ute)?)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<AnalysisReport> AnalyzeAsync(AnalysisRequest request, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var normalizedSql = NormalizeSql(request.QueryText);
        EnsureReadSafeInput(normalizedSql);

        progress?.Report("Opening SQL connection...");
        await using var connection = new SqlConnection(request.ConnectionString);
        connection.FireInfoMessageEventOnUserErrors = true;
        await connection.OpenAsync(cancellationToken);

        var (majorVersion, hasWritePermission) = await CheckServerAndPermissionGuardrailsAsync(connection, cancellationToken);
        if (majorVersion < 14)
        {
            throw new InvalidOperationException($"Unsupported SQL Server major version {majorVersion}. This tool supports SQL Server 2017+ only.");
        }

        progress?.Report($"Connected to SQL Server {majorVersion}. Running baseline loops...");

        var baselineRuns = new List<SqlExecutionResult>();
        for (var i = 0; i < request.BaselineRuns; i++)
        {
            var runName = $"Baseline #{i + 1}";
            var result = await ExecuteAndMeasureAsync(connection, normalizedSql, request.TimeoutSeconds, runName, null, cancellationToken);
            baselineRuns.Add(result);

            var suffix = result.Success
                ? $"{result.ClientElapsedMs:N0} ms, reads {result.LogicalReads:N0}, CPU {result.CpuMs:N0}"
                : $"FAILED ({result.ErrorMessage})";
            progress?.Report($"{runName}: {suffix}");
        }

        var variants = new List<QueryVariant>();
        var variantRuns = new List<SqlExecutionResult>();
        var notes = new List<string>();

        if (request.EnableJoinSandbox)
        {
            if (QuerySandboxGenerator.TryGenerateJoinLadder(normalizedSql, out var generated, out var sandboxNote))
            {
                notes.Add(sandboxNote);
                variants.AddRange(generated);
                progress?.Report($"Generated {variants.Count} join ladder variants.");

                foreach (var variant in variants)
                {
                    var result = await ExecuteAndMeasureAsync(connection, variant.Sql, request.TimeoutSeconds, variant.Name, variant.JoinLabel, cancellationToken);
                    variantRuns.Add(result);

                    var suffix = result.Success
                        ? $"{result.ClientElapsedMs:N0} ms, reads {result.LogicalReads:N0}, CPU {result.CpuMs:N0}"
                        : $"FAILED ({result.ErrorMessage})";
                    progress?.Report($"{variant.Name}: {suffix}");

                    if (result.TimedOut)
                    {
                        notes.Add("At least one join sandbox variant timed out; ranking still uses successful variants and baseline plan evidence.");
                        break;
                    }
                }
            }
            else
            {
                notes.Add(sandboxNote);
            }
        }
        else
        {
            notes.Add("Join sandbox ladder disabled by user option.");
        }

        var topInsights = BuildInsights(baselineRuns, variantRuns);
        var procedureDecomposition = BuildProcedureDecomposition(baselineRuns, normalizedSql);

        if (hasWritePermission)
        {
            notes.Add("Connection login appears to have INSERT/UPDATE/DELETE rights at DB level. Use a least-privilege read-only login for safe experimentation.");
        }

        notes.Add("Static parameter workflow detected: rankings prioritize repeatable root-cause evidence for this fixed parameter set.");

        return new AnalysisReport
        {
            SqlMajorVersion = majorVersion,
            HasWriteDatabasePermissions = hasWritePermission,
            InputShape = ClassifyInputShape(normalizedSql),
            BaselineRuns = baselineRuns,
            VariantRuns = variantRuns,
            TopInsights = topInsights,
            QueryResultPreview = baselineRuns.FirstOrDefault(x => x.Success)?.QueryResultPreview ?? QueryResultPreview.Empty,
            ProcedureDecomposition = procedureDecomposition,
            Notes = notes
        };
    }

    private static IReadOnlyList<string> BuildProcedureDecomposition(IReadOnlyList<SqlExecutionResult> baselineRuns, string normalizedSql)
    {
        var isExecInput = normalizedSql.TrimStart().StartsWith("exec", StringComparison.OrdinalIgnoreCase)
                          || normalizedSql.TrimStart().StartsWith("execute", StringComparison.OrdinalIgnoreCase);
        if (!isExecInput)
        {
            return Array.Empty<string>();
        }

        var baselineBest = baselineRuns
            .Where(x => x.Success && !string.IsNullOrWhiteSpace(x.PlanXml))
            .OrderBy(x => x.ClientElapsedMs)
            .FirstOrDefault();

        if (baselineBest is null)
        {
            return new[] { "Could not decompose procedure because no successful baseline plan XML was captured." };
        }

        var parts = ProcedurePlanDecomposer.ExtractTopStatements(baselineBest.PlanXml, 5);
        return parts.Count > 0
            ? parts
            : new[] { "Procedure decomposition found no statement-level details in captured plan." };
    }

    private static string ClassifyInputShape(string sql)
    {
        var trimmed = sql.TrimStart();
        if (trimmed.StartsWith("exec", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("execute", StringComparison.OrdinalIgnoreCase))
        {
            return "Procedure call";
        }

        if (trimmed.StartsWith("with", StringComparison.OrdinalIgnoreCase))
        {
            return "CTE + query";
        }

        if (trimmed.StartsWith("select", StringComparison.OrdinalIgnoreCase))
        {
            return "SELECT query";
        }

        return "SQL script";
    }

    private static async Task<(int MajorVersion, bool HasWritePermission)> CheckServerAndPermissionGuardrailsAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT
                               CAST(SERVERPROPERTY('ProductMajorVersion') AS int) AS MajorVersion,
                               CASE
                                   WHEN HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'INSERT') = 1
                                        OR HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'UPDATE') = 1
                                        OR HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'DELETE') = 1
                                   THEN 1
                                   ELSE 0
                               END AS HasWritePermission;
                           """;

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 15;

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Could not read SQL Server version metadata.");
        }

        var majorVersion = reader.GetInt32(0);
        var hasWritePermission = reader.GetInt32(1) == 1;
        return (majorVersion, hasWritePermission);
    }

    private static async Task<SqlExecutionResult> ExecuteAndMeasureAsync(
        SqlConnection connection,
        string sql,
        int timeoutSeconds,
        string runName,
        string? joinLabel,
        CancellationToken cancellationToken)
    {
        var messages = new List<string>();

        void MessageHandler(object? _, SqlInfoMessageEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(args.Message))
            {
                return;
            }

            messages.Add(args.Message);
        }

        connection.InfoMessage += MessageHandler;

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var wrapped = WrapForStatistics(sql);
            await using var command = connection.CreateCommand();
            command.CommandTimeout = timeoutSeconds;
            command.CommandText = wrapped;

            var rowCount = 0;
            var planXml = string.Empty;
            var previewColumns = new List<string>();
            var previewRows = new List<IReadOnlyList<string>>();
            var previewColumnsCaptured = false;
            var previewResultSetIndex = -1;
            var resultSetIndex = 0;

            const int maxPreviewRows = 200;
            const int maxPreviewCellLength = 500;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            do
            {
                if (reader.FieldCount <= 0)
                {
                    resultSetIndex++;
                    continue;
                }

                while (await reader.ReadAsync(cancellationToken))
                {
                    var value = reader.FieldCount == 1
                        ? reader.GetValue(0)?.ToString()
                        : null;

                    if (!string.IsNullOrWhiteSpace(value) && value.Contains("<ShowPlanXML", StringComparison.OrdinalIgnoreCase))
                    {
                        planXml = value;
                        continue;
                    }

                    rowCount++;

                    if (!previewColumnsCaptured)
                    {
                        for (var i = 0; i < reader.FieldCount; i++)
                        {
                            previewColumns.Add(reader.GetName(i));
                        }

                        previewColumnsCaptured = true;
                        previewResultSetIndex = resultSetIndex;
                    }

                    if (resultSetIndex != previewResultSetIndex)
                    {
                        continue;
                    }

                    if (previewRows.Count >= maxPreviewRows)
                    {
                        continue;
                    }

                    var rowValues = new string[reader.FieldCount];
                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        var raw = reader.IsDBNull(i) ? "<NULL>" : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? string.Empty;
                        rowValues[i] = raw.Length > maxPreviewCellLength ? raw[..maxPreviewCellLength] + "..." : raw;
                    }

                    previewRows.Add(rowValues);
                }

                resultSetIndex++;
            }
            while (await reader.NextResultAsync(cancellationToken));

            stopwatch.Stop();

            var stats = ParseStatistics(messages);
            return new SqlExecutionResult
            {
                Name = runName,
                Success = true,
                TimedOut = false,
                ClientElapsedMs = stopwatch.ElapsedMilliseconds,
                SqlElapsedMs = stats.SqlElapsedMs,
                CpuMs = stats.CpuMs,
                LogicalReads = stats.LogicalReads,
                RowsReturned = rowCount,
                PlanXml = planXml,
                JoinLabel = joinLabel,
                QueryResultPreview = new QueryResultPreview
                {
                    Columns = previewColumns,
                    Rows = previewRows
                },
                Messages = messages
            };
        }
        catch (SqlException ex)
        {
            stopwatch.Stop();
            var timedOut = ex.Number == -2;

            var stats = ParseStatistics(messages);
            return new SqlExecutionResult
            {
                Name = runName,
                Success = false,
                TimedOut = timedOut,
                ClientElapsedMs = stopwatch.ElapsedMilliseconds,
                SqlElapsedMs = stats.SqlElapsedMs,
                CpuMs = stats.CpuMs,
                LogicalReads = stats.LogicalReads,
                RowsReturned = 0,
                ErrorMessage = ex.Message,
                JoinLabel = joinLabel,
                Messages = messages
            };
        }
        finally
        {
            connection.InfoMessage -= MessageHandler;
        }
    }

    private static (long LogicalReads, long CpuMs, long SqlElapsedMs) ParseStatistics(IEnumerable<string> messages)
    {
        long logicalReads = 0;
        long cpuMs = 0;
        long sqlElapsedMs = 0;

        foreach (var message in messages)
        {
            foreach (Match match in LogicalReadsRegex.Matches(message))
            {
                if (long.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var reads))
                {
                    logicalReads += reads;
                }
            }

            foreach (Match match in CpuElapsedRegex.Matches(message))
            {
                if (long.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cpu))
                {
                    cpuMs += cpu;
                }

                if (long.TryParse(match.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var elapsed))
                {
                    sqlElapsedMs += elapsed;
                }
            }
        }

        return (logicalReads, cpuMs, sqlElapsedMs);
    }

    private static string WrapForStatistics(string sql)
    {
        return $"SET NOCOUNT ON; SET STATISTICS IO ON; SET STATISTICS TIME ON; SET STATISTICS XML ON; {sql}; SET STATISTICS XML OFF; SET STATISTICS IO OFF; SET STATISTICS TIME OFF;";
    }

    private static string NormalizeSql(string rawSql)
    {
        var lines = rawSql.Replace("\r\n", "\n").Split('\n');
        var filtered = lines.Where(line => !string.Equals(line.Trim(), "GO", StringComparison.OrdinalIgnoreCase));
        return string.Join(Environment.NewLine, filtered).Trim();
    }

    private static void EnsureReadSafeInput(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new InvalidOperationException("Query text cannot be empty.");
        }

        if (!ReadQueryStartRegex.IsMatch(sql))
        {
            throw new InvalidOperationException("Input must be a real SELECT/CTE query or EXEC procedure call.");
        }

        if (sql.Contains("...", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Input appears to be a placeholder/template. Enter a real query or EXEC call.");
        }

        var stripped = StripCommentsAndLiterals(sql);
        var match = DmlRegex.Match(stripped);
        if (match.Success)
        {
            throw new InvalidOperationException($"Blocked potentially write operation token: {match.Value}. This tool only supports read-only troubleshooting.");
        }
    }

    private static string StripCommentsAndLiterals(string sql)
    {
        var sb = new StringBuilder(sql.Length);
        var inSingleQuote = false;
        var inLineComment = false;
        var inBlockComment = false;

        for (var i = 0; i < sql.Length; i++)
        {
            var c = sql[i];
            var next = i + 1 < sql.Length ? sql[i + 1] : '\0';

            if (inLineComment)
            {
                if (c == '\n')
                {
                    inLineComment = false;
                    sb.Append(c);
                }

                continue;
            }

            if (inBlockComment)
            {
                if (c == '*' && next == '/')
                {
                    inBlockComment = false;
                    i++;
                }

                continue;
            }

            if (!inSingleQuote && c == '-' && next == '-')
            {
                inLineComment = true;
                i++;
                continue;
            }

            if (!inSingleQuote && c == '/' && next == '*')
            {
                inBlockComment = true;
                i++;
                continue;
            }

            if (c == '\'')
            {
                inSingleQuote = !inSingleQuote;
                sb.Append(' ');
                continue;
            }

            if (inSingleQuote)
            {
                sb.Append(' ');
                continue;
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    private static IReadOnlyList<Insight> BuildInsights(IReadOnlyList<SqlExecutionResult> baselineRuns, IReadOnlyList<SqlExecutionResult> variantRuns)
    {
        var candidates = new List<Insight>();

        var baselineBest = baselineRuns
            .Where(x => x.Success)
            .OrderBy(x => x.ClientElapsedMs)
            .FirstOrDefault();

        if (baselineBest is null)
        {
            return candidates;
        }

        var facts = PlanFactsExtractor.Extract(baselineBest.PlanXml);

        if (facts.HasSpillWarning)
        {
            candidates.Add(new Insight(
                "Memory grant pressure / tempdb spill risk",
                92,
                $"Execution plan shows spill warnings ({facts.SpillWarningCount}) and grant stress indicators.",
                "Reduce row width early (project fewer columns), push selective filters earlier, and test indexes that support join + filter order to cut spill volume."));
        }

        if (facts.MaxJoinMisestimateRatio >= 10)
        {
            candidates.Add(new Insight(
                "Join cardinality misestimation",
                Math.Min(96, 70 + (int)Math.Round(Math.Log10(facts.MaxJoinMisestimateRatio) * 14)),
                $"Worst join estimate mismatch is ~{facts.MaxJoinMisestimateRatio:N1}x ({facts.WorstJoinDescription}).",
                "Validate join predicates and stats quality, test filtered or composite indexes on join/filter keys, and consider refactoring into staged temp table steps where row counts are stabilized."));
        }

        if (facts.KeyLookupCount > 0)
        {
            candidates.Add(new Insight(
                "Key lookup amplification",
                Math.Min(90, 62 + (facts.KeyLookupCount * 8)),
                $"Plan includes {facts.KeyLookupCount} key lookup operator(s), often causing repeated random IO under nested loops.",
                "Create or adjust covering indexes for the hottest lookup path, or rewrite to reduce lookup-driven row-by-row access."));
        }

        if (facts.UserDefinedFunctionCount > 0)
        {
            candidates.Add(new Insight(
                "Scalar/UDF row-by-row execution overhead",
                82,
                $"Plan references {facts.UserDefinedFunctionCount} UDF node(s), which often serialize CPU work per row.",
                "Inline scalar logic where possible, replace scalar UDF patterns with set-based joins/apply, and retest with same static parameter set."));
        }

        if (facts.MaxMissingIndexImpact >= 50)
        {
            candidates.Add(new Insight(
                "Index gap on critical access path",
                Math.Min(88, 58 + (int)Math.Round(facts.MaxMissingIndexImpact / 3.5)),
                $"Plan contains missing index recommendations (max impact {facts.MaxMissingIndexImpact:N1}).",
                "Prototype nonclustered indexes on join/filter columns plus needed includes for selected output columns; verify logical read reduction before adopting."));
        }

        if (facts.MaxEstimatedRowSizeBytes >= 256)
        {
            candidates.Add(new Insight(
                "Wide row projection increasing memory/IO",
                74,
                $"Estimated row size reaches {facts.MaxEstimatedRowSizeBytes:N0} bytes, indicating wide payload movement.",
                "Trim unnecessary columns in early stages, materialize narrow intermediate sets, and fetch wide attributes only after row count is reduced."));
        }

        var ladderInsight = BuildVariantJumpInsight(variantRuns);
        if (ladderInsight is not null)
        {
            candidates.Add(ladderInsight);
        }

        if (candidates.Count < 3)
        {
            candidates.Add(new Insight(
                "High baseline logical read footprint",
                68,
                $"Median logical reads are {Median(baselineRuns.Where(x => x.Success).Select(x => x.LogicalReads)):N0} across successful baseline runs.",
                "Use the ranked operator hotspots in the execution plan to target the heaviest scans/seeks first, then re-run this same static parameter benchmark."));
        }

        return candidates
            .OrderByDescending(x => x.Score)
            .Take(3)
            .ToList();
    }

    private static Insight? BuildVariantJumpInsight(IReadOnlyList<SqlExecutionResult> variantRuns)
    {
        var successful = variantRuns.Where(x => x.Success).ToList();
        if (successful.Count < 2)
        {
            return null;
        }

        double biggestIncreaseRatio = 0;
        SqlExecutionResult? jumpFrom = null;
        SqlExecutionResult? jumpTo = null;

        for (var i = 1; i < successful.Count; i++)
        {
            var prior = successful[i - 1];
            var current = successful[i];
            if (prior.ClientElapsedMs <= 0)
            {
                continue;
            }

            var ratio = (current.ClientElapsedMs - prior.ClientElapsedMs) / (double)prior.ClientElapsedMs;
            if (ratio > biggestIncreaseRatio)
            {
                biggestIncreaseRatio = ratio;
                jumpFrom = prior;
                jumpTo = current;
            }
        }

        if (jumpFrom is null || jumpTo is null || biggestIncreaseRatio < 0.35)
        {
            return null;
        }

        var score = Math.Min(91, 70 + (int)Math.Round(biggestIncreaseRatio * 22));

        return new Insight(
            "Specific join step introduces major runtime jump",
            score,
            $"Join ladder jump from '{jumpFrom.Name}' to '{jumpTo.Name}' is {(biggestIncreaseRatio * 100):N1}% slower ({jumpFrom.ClientElapsedMs:N0} -> {jumpTo.ClientElapsedMs:N0} ms).",
            "Inspect that join's predicate/selectivity, confirm index support on both sides, and test rewriting that section as a staged temp result before rejoining.");
    }

    private static double Median(IEnumerable<long> values)
    {
        var sorted = values.OrderBy(x => x).ToArray();
        if (sorted.Length == 0)
        {
            return 0;
        }

        if (sorted.Length % 2 == 1)
        {
            return sorted[sorted.Length / 2];
        }

        return (sorted[(sorted.Length / 2) - 1] + sorted[sorted.Length / 2]) / 2.0;
    }
}

internal sealed class PlanFacts
{
    public required bool HasSpillWarning { get; init; }

    public required int SpillWarningCount { get; init; }

    public required int KeyLookupCount { get; init; }

    public required int UserDefinedFunctionCount { get; init; }

    public required double MaxMissingIndexImpact { get; init; }

    public required double MaxJoinMisestimateRatio { get; init; }

    public required string WorstJoinDescription { get; init; }

    public required double MaxEstimatedRowSizeBytes { get; init; }
}

internal static class PlanFactsExtractor
{
    public static PlanFacts Extract(string? planXml)
    {
        if (string.IsNullOrWhiteSpace(planXml))
        {
            return EmptyFacts();
        }

        try
        {
            var doc = XDocument.Parse(planXml);
            var ns = doc.Root?.Name.Namespace;
            if (ns is null)
            {
                return EmptyFacts();
            }

            var spillWarnings = doc.Descendants(ns + "SpillToTempDb").Count();
            var keyLookups = doc.Descendants(ns + "RelOp")
                .Count(x => string.Equals((string?)x.Attribute("PhysicalOp"), "Key Lookup", StringComparison.OrdinalIgnoreCase));

            var udfCount = doc.Descendants(ns + "UserDefinedFunction").Count();

            var missingIndexImpact = doc.Descendants(ns + "MissingIndexGroup")
                .Select(x => ParseDouble((string?)x.Attribute("Impact")))
                .DefaultIfEmpty(0)
                .Max();

            var rowSize = doc.Descendants(ns + "RelOp")
                .Select(x => ParseDouble((string?)x.Attribute("EstimateRowSize")))
                .DefaultIfEmpty(0)
                .Max();

            var worstJoinRatio = 0.0;
            var worstJoinText = "n/a";

            foreach (var relOp in doc.Descendants(ns + "RelOp"))
            {
                var logicalOp = (string?)relOp.Attribute("LogicalOp") ?? string.Empty;
                if (!logicalOp.Contains("join", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var estimateRows = ParseDouble((string?)relOp.Attribute("EstimateRows"));
                if (estimateRows <= 0)
                {
                    continue;
                }

                var actualRows = relOp.Descendants(ns + "RunTimeCountersPerThread")
                    .Select(x => ParseDouble((string?)x.Attribute("ActualRows")))
                    .Sum();

                if (actualRows <= 0)
                {
                    continue;
                }

                var ratio = Math.Max(actualRows / estimateRows, estimateRows / actualRows);
                if (ratio <= worstJoinRatio)
                {
                    continue;
                }

                var physicalOp = (string?)relOp.Attribute("PhysicalOp") ?? logicalOp;
                worstJoinRatio = ratio;
                worstJoinText = $"{physicalOp} (estimated {estimateRows:N1}, actual {actualRows:N1})";
            }

            return new PlanFacts
            {
                HasSpillWarning = spillWarnings > 0,
                SpillWarningCount = spillWarnings,
                KeyLookupCount = keyLookups,
                UserDefinedFunctionCount = udfCount,
                MaxMissingIndexImpact = missingIndexImpact,
                MaxJoinMisestimateRatio = worstJoinRatio,
                WorstJoinDescription = worstJoinText,
                MaxEstimatedRowSizeBytes = rowSize
            };
        }
        catch
        {
            return EmptyFacts();
        }
    }

    private static double ParseDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        return double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private static PlanFacts EmptyFacts()
    {
        return new PlanFacts
        {
            HasSpillWarning = false,
            SpillWarningCount = 0,
            KeyLookupCount = 0,
            UserDefinedFunctionCount = 0,
            MaxMissingIndexImpact = 0,
            MaxJoinMisestimateRatio = 0,
            WorstJoinDescription = "n/a",
            MaxEstimatedRowSizeBytes = 0
        };
    }
}

internal static class ProcedurePlanDecomposer
{
    public static IReadOnlyList<string> ExtractTopStatements(string? planXml, int top)
    {
        if (string.IsNullOrWhiteSpace(planXml) || top <= 0)
        {
            return Array.Empty<string>();
        }

        try
        {
            var doc = XDocument.Parse(planXml);
            var ns = doc.Root?.Name.Namespace;
            if (ns is null)
            {
                return Array.Empty<string>();
            }

            var statements = doc.Descendants(ns + "StmtSimple")
                .Select(x => new
                {
                    StatementType = ((string?)x.Attribute("StatementType") ?? string.Empty).Trim(),
                    StatementText = ((string?)x.Attribute("StatementText") ?? string.Empty).Trim(),
                    Cost = ParseDouble((string?)x.Attribute("StatementSubTreeCost")),
                    JoinCount = x.Descendants(ns + "RelOp")
                        .Count(r => (((string?)r.Attribute("LogicalOp")) ?? string.Empty)
                            .Contains("join", StringComparison.OrdinalIgnoreCase))
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.StatementText))
                .OrderByDescending(x => x.Cost)
                .Take(top)
                .ToList();

            var results = new List<string>(statements.Count);
            for (var i = 0; i < statements.Count; i++)
            {
                var s = statements[i];
                var compactText = Regex.Replace(s.StatementText, @"\s+", " ").Trim();
                if (compactText.Length > 180)
                {
                    compactText = compactText[..180] + "...";
                }

                var type = string.IsNullOrWhiteSpace(s.StatementType) ? "UNKNOWN" : s.StatementType;
                results.Add($"{i + 1}) {type}, subtree cost {s.Cost:N3}, joins in statement {s.JoinCount}: {compactText}");
            }

            return results;
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static double ParseDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        return double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }
}

internal static class QuerySandboxGenerator
{
    public static bool TryGenerateJoinLadder(string sql, out IReadOnlyList<QueryVariant> variants, out string note)
    {
        variants = Array.Empty<QueryVariant>();
        note = "Join sandbox ladder not generated.";

        var trimmed = sql.TrimStart();
        if (!trimmed.StartsWith("select", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("with", StringComparison.OrdinalIgnoreCase))
        {
            note = "Join sandbox skipped: parser currently supports SELECT/CTE text input (not pure EXEC mode).";
            return false;
        }

        if (!TrySplitFromClause(sql, out var fromClause, out var error))
        {
            note = $"Join sandbox skipped: {error}";
            return false;
        }

        var joinStarts = FindJoinStarts(fromClause);
        if (joinStarts.Count == 0)
        {
            note = "Join sandbox skipped: no top-level JOIN found in parsed FROM clause.";
            return false;
        }

        var baseSegment = fromClause[..joinStarts[0]].Trim();
        if (string.IsNullOrWhiteSpace(baseSegment))
        {
            note = "Join sandbox skipped: base FROM segment could not be parsed.";
            return false;
        }

        var joins = new List<string>();
        for (var i = 0; i < joinStarts.Count; i++)
        {
            var start = joinStarts[i];
            var end = i + 1 < joinStarts.Count ? joinStarts[i + 1] : fromClause.Length;
            joins.Add(fromClause[start..end].Trim());
        }

        var generated = new List<QueryVariant>();
        var current = new StringBuilder(baseSegment);

        for (var i = 0; i < joins.Count; i++)
        {
            current.Append(' ');
            current.Append(joins[i]);

            var joinLabel = ExtractJoinLabel(joins[i]);
            generated.Add(new QueryVariant(
                $"JoinStep {i + 1}",
                $"SELECT COUNT_BIG(1) AS SandboxRowCount FROM {current} OPTION (RECOMPILE)",
                joinLabel));
        }

        variants = generated;
        note = "Join sandbox uses COUNT_BIG join-ladder probes without WHERE/GROUP filters to highlight raw join expansion pressure.";
        return true;
    }

    private static bool TrySplitFromClause(string sql, out string fromClause, out string error)
    {
        fromClause = string.Empty;
        error = string.Empty;

        var fromIndex = FindTopLevelKeyword(sql, "from", 0);
        if (fromIndex < 0)
        {
            error = "FROM keyword not found at top query level.";
            return false;
        }

        var clauseStart = fromIndex + 4;
        var stopIndex = sql.Length;

        var endingKeywords = new[] { "where", "group", "having", "order", "option", "union", "for" };
        foreach (var keyword in endingKeywords)
        {
            var idx = FindTopLevelKeyword(sql, keyword, clauseStart);
            if (idx >= 0 && idx < stopIndex)
            {
                stopIndex = idx;
            }
        }

        if (stopIndex <= clauseStart)
        {
            error = "Unable to isolate FROM clause.";
            return false;
        }

        fromClause = sql[clauseStart..stopIndex].Trim();
        return !string.IsNullOrWhiteSpace(fromClause);
    }

    private static List<int> FindJoinStarts(string fromClause)
    {
        var tokens = TokenizeTopLevelWords(fromClause);
        var starts = new List<int>();

        for (var i = 0; i < tokens.Count; i++)
        {
            if (!string.Equals(tokens[i].Word, "JOIN", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var start = tokens[i].Start;
            if (i > 0 && IsJoinType(tokens[i - 1].Word))
            {
                start = tokens[i - 1].Start;
                if (i > 1 && string.Equals(tokens[i - 1].Word, "OUTER", StringComparison.OrdinalIgnoreCase) && IsDirectionalJoinType(tokens[i - 2].Word))
                {
                    start = tokens[i - 2].Start;
                }
            }

            starts.Add(start);
        }

        return starts.Distinct().OrderBy(x => x).ToList();
    }

    private static bool IsJoinType(string word)
    {
        return word.Equals("LEFT", StringComparison.OrdinalIgnoreCase)
               || word.Equals("RIGHT", StringComparison.OrdinalIgnoreCase)
               || word.Equals("FULL", StringComparison.OrdinalIgnoreCase)
               || word.Equals("INNER", StringComparison.OrdinalIgnoreCase)
               || word.Equals("OUTER", StringComparison.OrdinalIgnoreCase)
               || word.Equals("CROSS", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDirectionalJoinType(string word)
    {
        return word.Equals("LEFT", StringComparison.OrdinalIgnoreCase)
               || word.Equals("RIGHT", StringComparison.OrdinalIgnoreCase)
               || word.Equals("FULL", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractJoinLabel(string joinSegment)
    {
        var match = Regex.Match(joinSegment, @"\bjoin\s+([^\s]+)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        return "join";
    }

    private static int FindTopLevelKeyword(string sql, string keyword, int startIndex)
    {
        var depth = 0;
        var inSingleQuote = false;
        var inBracket = false;
        var inLineComment = false;
        var inBlockComment = false;

        for (var i = Math.Max(0, startIndex); i < sql.Length; i++)
        {
            var c = sql[i];
            var next = i + 1 < sql.Length ? sql[i + 1] : '\0';

            if (inLineComment)
            {
                if (c == '\n')
                {
                    inLineComment = false;
                }

                continue;
            }

            if (inBlockComment)
            {
                if (c == '*' && next == '/')
                {
                    inBlockComment = false;
                    i++;
                }

                continue;
            }

            if (!inSingleQuote && !inBracket && c == '-' && next == '-')
            {
                inLineComment = true;
                i++;
                continue;
            }

            if (!inSingleQuote && !inBracket && c == '/' && next == '*')
            {
                inBlockComment = true;
                i++;
                continue;
            }

            if (!inBracket && c == '\'')
            {
                inSingleQuote = !inSingleQuote;
                continue;
            }

            if (inSingleQuote)
            {
                continue;
            }

            if (c == '[')
            {
                inBracket = true;
                continue;
            }

            if (c == ']')
            {
                inBracket = false;
                continue;
            }

            if (inBracket)
            {
                continue;
            }

            if (c == '(')
            {
                depth++;
                continue;
            }

            if (c == ')' && depth > 0)
            {
                depth--;
                continue;
            }

            if (depth != 0)
            {
                continue;
            }

            if (!IsWordBoundary(sql, i - 1))
            {
                continue;
            }

            if (i + keyword.Length > sql.Length)
            {
                continue;
            }

            if (!sql.AsSpan(i, keyword.Length).Equals(keyword, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!IsWordBoundary(sql, i + keyword.Length))
            {
                continue;
            }

            return i;
        }

        return -1;
    }

    private static bool IsWordBoundary(string text, int index)
    {
        if (index < 0 || index >= text.Length)
        {
            return true;
        }

        var c = text[index];
        return !char.IsLetterOrDigit(c) && c != '_';
    }

    private static List<(string Word, int Start)> TokenizeTopLevelWords(string sql)
    {
        var tokens = new List<(string Word, int Start)>();
        var depth = 0;
        var inSingleQuote = false;
        var inBracket = false;
        var inLineComment = false;
        var inBlockComment = false;

        for (var i = 0; i < sql.Length; i++)
        {
            var c = sql[i];
            var next = i + 1 < sql.Length ? sql[i + 1] : '\0';

            if (inLineComment)
            {
                if (c == '\n')
                {
                    inLineComment = false;
                }

                continue;
            }

            if (inBlockComment)
            {
                if (c == '*' && next == '/')
                {
                    inBlockComment = false;
                    i++;
                }

                continue;
            }

            if (!inSingleQuote && !inBracket && c == '-' && next == '-')
            {
                inLineComment = true;
                i++;
                continue;
            }

            if (!inSingleQuote && !inBracket && c == '/' && next == '*')
            {
                inBlockComment = true;
                i++;
                continue;
            }

            if (!inBracket && c == '\'')
            {
                inSingleQuote = !inSingleQuote;
                continue;
            }

            if (inSingleQuote)
            {
                continue;
            }

            if (c == '[')
            {
                inBracket = true;
                continue;
            }

            if (c == ']')
            {
                inBracket = false;
                continue;
            }

            if (inBracket)
            {
                continue;
            }

            if (c == '(')
            {
                depth++;
                continue;
            }

            if (c == ')' && depth > 0)
            {
                depth--;
                continue;
            }

            if (depth != 0)
            {
                continue;
            }

            if (!char.IsLetter(c))
            {
                continue;
            }

            var start = i;
            while (i < sql.Length && (char.IsLetterOrDigit(sql[i]) || sql[i] == '_'))
            {
                i++;
            }

            var word = sql[start..i];
            tokens.Add((word.ToUpperInvariant(), start));
            i--;
        }

        return tokens;
    }
}
