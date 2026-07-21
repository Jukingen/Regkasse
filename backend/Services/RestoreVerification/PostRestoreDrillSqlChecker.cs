using System.Diagnostics;
using KasseAPI_Final.Models.RestoreVerification;
using Npgsql;

namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// İzole geri yüklenen PostgreSQL üzerinde şema/süreklilik SQL kontrolleri; sonuçlar makine okunur ve L4 için muhafazakâr yorumlanır.
/// </summary>
public sealed class PostRestoreDrillSqlChecker : IPostRestoreDrillSqlChecker
{
    private readonly ILogger<PostRestoreDrillSqlChecker> _logger;

    /// <summary>A: RKSV / mali omurga — L4 için zorunlu (ilişki var + sorgulanabilir).</summary>
    private static readonly PostRestoreTableSpec[] FiscalSpineTables =
    [
        new("receipts", "fiscal_spine", "Receipts", null),
        new("payment_details", "fiscal_spine", "Payment details", null),
        new("receipt_items", "fiscal_spine", "Receipt line items", null),
        new("receipt_tax_lines", "fiscal_spine", "Receipt tax lines", null),
        new("signature_chain_state", "fiscal_spine", "Signature chain state", null),
        new("TseSignatures", "fiscal_spine", "TSE signatures", null),
        new("DailyClosings", "fiscal_spine", "Daily closings", null)
    ];

    /// <summary>B: Çevrimdışı / outbox / raporlar / yedek telemetrisi — L4 için zorunlu.</summary>
    private static readonly PostRestoreTableSpec[] ContinuityResilienceTables =
    [
        new("offline_transactions", "continuity_resilience", "Offline transactions", null),
        new("finanz_online_outbox_messages", "continuity_resilience", "FinanzOnline outbox", null),
        new("tagesbericht_reports", "continuity_resilience", "Tagesbericht reports", null),
        new("monatsbericht_reports", "continuity_resilience", "Monatsbericht reports", null),
        new("jahresbericht_reports", "continuity_resilience", "Jahresbericht reports", null),
        new("periodenbericht_runs", "continuity_resilience", "Periodenbericht runs", null),
        new("backup_runs", "continuity_resilience", "Backup runs (telemetry)", null),
        new("backup_artifacts", "continuity_resilience", "Backup artifacts (telemetry)", null),
        new("backup_verifications", "continuity_resilience", "Backup verifications (telemetry)", null),
        new("restore_verification_runs", "continuity_resilience", "Restore verification runs (telemetry)", null),
        new("backup_runtime_execution_preferences", "continuity_resilience", "Backup runtime preferences (telemetry)", null)
    ];

    /// <summary>C: Kimlik / oturum / ayarlar / EF geçmişi — L4 için zorunlu.</summary>
    private static readonly PostRestoreTableSpec[] PlatformTables =
    [
        new("AspNetUsers", "platform", "Identity users", null),
        new("AspNetRoles", "platform", "Identity roles", null),
        new("AspNetUserRoles", "platform", "Identity user-role links", null),
        new("auth_sessions", "platform", "Auth sessions", null),
        new("refresh_tokens", "platform", "Refresh tokens", null),
        new("company_settings", "platform", "Company settings", null),
        new("system_settings", "platform", "System settings", null),
        new("UserSettings", "platform", "User settings", null),
        new("__EFMigrationsHistory", "platform", "EF Core migrations history", 1L)
    ];

    public PostRestoreDrillSqlChecker(ILogger<PostRestoreDrillSqlChecker> logger)
    {
        _logger = logger;
    }

    /// <summary>Yönetim bağlantısından hedef DB adına Npgsql bağlantı dizesi üretir (host kullanıcı korunur).</summary>
    public static string BuildTargetDatabaseConnectionString(string adminConnectionString, string databaseName)
    {
        var b = new NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            Database = databaseName
        };
        return b.ConnectionString;
    }

    /// <summary>
    /// L4 post-restore katmanı: <see cref="PostRestoreSqlFailureCategoryMapper.IsRequiredCheckPassingForL4"/> (status + failureCategory).
    /// </summary>
    public static bool ComputeL4LayerPass(IReadOnlyList<PostRestoreSqlCheckRow> checks) =>
        checks.All(PostRestoreSqlFailureCategoryMapper.IsRequiredCheckPassingForL4);

    public async Task<PostRestoreDrillSqlOutcome> RunContinuityChecksAsync(
        string targetDatabaseConnectionString,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(targetDatabaseConnectionString))
            return FailedPrerequisiteRun("MISSING_TARGET_CONNECTION", PostRestoreSqlReasonCodes.MissingTargetConnection,
                "Target database connection string is missing.");

        var checks = new List<PostRestoreSqlCheckRow>();
        var startedAt = DateTimeOffset.UtcNow;
        var sw = Stopwatch.StartNew();

        try
        {
            await using var conn = new NpgsqlConnection(targetDatabaseConnectionString);
            await conn.OpenAsync(cancellationToken);

            foreach (var spec in FiscalSpineTables.Concat(ContinuityResilienceTables).Concat(PlatformTables))
                checks.Add(await EvaluateTableSpecAsync(conn, spec, cancellationToken));

            await AppendFiscalInvariantChecksAsync(conn, checks, cancellationToken);
            await AppendReferentialIntegrityChecksAsync(conn, checks, cancellationToken);
            AppendInformativeDatasetSignals(checks);

            var passed = ComputeL4LayerPass(checks);
            sw.Stop();
            var completedAt = DateTimeOffset.UtcNow;
            return new PostRestoreDrillSqlOutcome
            {
                Executed = true,
                Passed = passed,
                ErrorDetail = passed ? null : "One or more L4-required continuity checks failed or were inconclusive.",
                StartedAtUtc = startedAt,
                CompletedAtUtc = completedAt,
                DurationMs = sw.ElapsedMilliseconds,
                Checks = checks
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Post-restore continuity SQL checks failed");
            sw.Stop();
            var completedAt = DateTimeOffset.UtcNow;
            checks.Add(new PostRestoreSqlCheckRow
            {
                Id = "runner.exception",
                Name = "Check runner",
                Category = "continuity_resilience",
                Status = PostRestoreSqlCheckStatus.Failed,
                Severity = PostRestoreSqlCheckSeverity.RequiredForL4,
                ReasonCode = PostRestoreSqlReasonCodes.UnhandledException,
                FailureCategory = PostRestoreSqlFailureCategory.Unknown,
                Summary = "Unexpected error while running post-restore SQL checks.",
                Detail = ex.Message
            });
            return new PostRestoreDrillSqlOutcome
            {
                Executed = true,
                Passed = false,
                ErrorDetail = ex.Message,
                StartedAtUtc = startedAt,
                CompletedAtUtc = completedAt,
                DurationMs = sw.ElapsedMilliseconds,
                Checks = checks
            };
        }
    }

    private static PostRestoreDrillSqlOutcome FailedPrerequisiteRun(string id, string reasonCode, string message) =>
        new()
        {
            Executed = false,
            Passed = false,
            ErrorDetail = message,
            Checks = new[]
            {
                new PostRestoreSqlCheckRow
                {
                    Id = id,
                    Name = "Connection / prerequisites",
                    Category = "continuity_resilience",
                    Status = PostRestoreSqlCheckStatus.Inconclusive,
                    Severity = PostRestoreSqlCheckSeverity.RequiredForL4,
                    ReasonCode = reasonCode,
                    FailureCategory = PostRestoreSqlFailureCategory.Unknown,
                    Summary = message
                }
            }
        };

    private static async Task<PostRestoreSqlCheckRow> EvaluateTableSpecAsync(
        NpgsqlConnection conn,
        PostRestoreTableSpec spec,
        CancellationToken ct)
    {
        var exists = await RelationExistsAsync(conn, spec.RelName, ct);
        if (!exists)
        {
            return new PostRestoreSqlCheckRow
            {
                Id = $"{spec.Category}.{spec.RelName}",
                Name = spec.DisplayName,
                Category = spec.Category,
                Status = PostRestoreSqlCheckStatus.Failed,
                Severity = PostRestoreSqlCheckSeverity.RequiredForL4,
                ReasonCode = PostRestoreSqlReasonCodes.SchemaRelationMissing,
                FailureCategory = PostRestoreSqlFailureCategory.MissingTable,
                Summary = $"Required relation public.{QuotePgIdent(spec.RelName)} is missing after restore."
            };
        }

        try
        {
            var q = $"SELECT COUNT(*)::bigint FROM public.{QuotePgIdent(spec.RelName)}";
            await using var cmd = new NpgsqlCommand(q, conn);
            cmd.CommandTimeout = 120;
            var o = await cmd.ExecuteScalarAsync(ct);
            var count = o is long l ? l : Convert.ToInt64(o ?? 0L);

            if (spec.MinRows.HasValue && count < spec.MinRows.Value)
            {
                var belowMinCategory = spec.RelName == PostRestoreSqlFailureCategoryMapper.EfMigrationsHistoryTableName
                    ? PostRestoreSqlFailureCategory.MigrationHistoryMissing
                    : PostRestoreSqlFailureCategory.RowCountUnexpected;
                return new PostRestoreSqlCheckRow
                {
                    Id = $"{spec.Category}.{spec.RelName}",
                    Name = spec.DisplayName,
                    Category = spec.Category,
                    Status = PostRestoreSqlCheckStatus.Failed,
                    Severity = PostRestoreSqlCheckSeverity.RequiredForL4,
                    ReasonCode = PostRestoreSqlReasonCodes.MigrationHistoryInsufficient,
                    FailureCategory = belowMinCategory,
                    Summary =
                        $"Relation is present but row_count {count} is below required minimum {spec.MinRows.Value} (implausible application database).",
                    MeasuredValue = count,
                    ExpectedAtLeast = spec.MinRows
                };
            }

            var emptyNote = count == 0 ? " Table is empty (valid for fresh or unused datasets)." : string.Empty;
            return new PostRestoreSqlCheckRow
            {
                Id = $"{spec.Category}.{spec.RelName}",
                Name = spec.DisplayName,
                Category = spec.Category,
                Status = PostRestoreSqlCheckStatus.Passed,
                Severity = PostRestoreSqlCheckSeverity.RequiredForL4,
                ReasonCode = PostRestoreSqlReasonCodes.TableAccessible,
                FailureCategory = PostRestoreSqlFailureCategory.None,
                Summary = $"Relation present and queryable; row_count={count}.{emptyNote}",
                MeasuredValue = count
            };
        }
        catch (Exception ex)
        {
            return new PostRestoreSqlCheckRow
            {
                Id = $"{spec.Category}.{spec.RelName}",
                Name = spec.DisplayName,
                Category = spec.Category,
                Status = PostRestoreSqlCheckStatus.Inconclusive,
                Severity = PostRestoreSqlCheckSeverity.RequiredForL4,
                ReasonCode = PostRestoreSqlReasonCodes.TableQueryFailed,
                FailureCategory = PostRestoreSqlFailureCategory.QueryFailed,
                Summary = "Relation exists but COUNT query failed (corrupt or inaccessible table?).",
                Detail = ex.Message
            };
        }
    }

    private static async Task AppendFiscalInvariantChecksAsync(
        NpgsqlConnection conn,
        List<PostRestoreSqlCheckRow> checks,
        CancellationToken ct)
    {
        const string emptyReceiptNumbers = """
            SELECT COUNT(*)::bigint
            FROM receipts
            WHERE receipt_number IS NULL OR btrim(receipt_number::text) = ''
            """;

        var emptyRn = await ScalarLongAsync(conn, emptyReceiptNumbers, ct);
        checks.Add(new PostRestoreSqlCheckRow
        {
            Id = "fiscal_invariant.receipt_number_nonempty",
            Name = "Receipt numbers non-empty when rows exist",
            Category = "fiscal_invariant",
            Status = emptyRn == 0 ? PostRestoreSqlCheckStatus.Passed : PostRestoreSqlCheckStatus.Failed,
            Severity = PostRestoreSqlCheckSeverity.RequiredForL4,
            ReasonCode = emptyRn == 0
                ? PostRestoreSqlReasonCodes.FiscalReceiptNumberInvariantOk
                : PostRestoreSqlReasonCodes.FiscalReceiptNumberEmptyOrNull,
            FailureCategory = emptyRn == 0
                ? PostRestoreSqlFailureCategory.None
                : PostRestoreSqlFailureCategory.IntegrityCheckFailed,
            Summary = emptyRn == 0
                ? "No receipts with empty receipt_number."
                : $"Found {emptyRn} receipt row(s) with null/empty receipt_number.",
            MeasuredValue = emptyRn,
            ExpectedAtLeast = 0
        });

        const string duplicateReceiptNumbers = """
            SELECT COUNT(*)::bigint
            FROM (
              SELECT receipt_number
              FROM receipts
              WHERE receipt_number IS NOT NULL AND btrim(receipt_number::text) <> ''
              GROUP BY receipt_number
              HAVING COUNT(*) > 1
            ) d
            """;

        var dupRn = await ScalarLongAsync(conn, duplicateReceiptNumbers, ct);
        checks.Add(new PostRestoreSqlCheckRow
        {
            Id = "fiscal_invariant.receipt_number_unique",
            Name = "Receipt numbers unique (non-empty)",
            Category = "fiscal_invariant",
            Status = dupRn == 0 ? PostRestoreSqlCheckStatus.Passed : PostRestoreSqlCheckStatus.Failed,
            Severity = PostRestoreSqlCheckSeverity.RequiredForL4,
            ReasonCode = dupRn == 0
                ? PostRestoreSqlReasonCodes.FiscalReceiptNumberInvariantOk
                : PostRestoreSqlReasonCodes.FiscalReceiptNumberDuplicate,
            FailureCategory = dupRn == 0
                ? PostRestoreSqlFailureCategory.None
                : PostRestoreSqlFailureCategory.IntegrityCheckFailed,
            Summary = dupRn == 0
                ? "No duplicate non-empty receipt_number values."
                : $"Found {dupRn} duplicate receipt_number group(s).",
            MeasuredValue = dupRn
        });
    }

    private static async Task AppendReferentialIntegrityChecksAsync(
        NpgsqlConnection conn,
        List<PostRestoreSqlCheckRow> checks,
        CancellationToken ct)
    {
        const string orphanItems = """
            SELECT COUNT(*)::bigint
            FROM receipt_items ri
            LEFT JOIN receipts r ON r.receipt_id = ri.receipt_id
            WHERE r.receipt_id IS NULL
            """;

        var orphanItemCount = await ScalarLongAsync(conn, orphanItems, ct);
        checks.Add(new PostRestoreSqlCheckRow
        {
            Id = "referential_integrity.orphan_receipt_items",
            Name = "Receipt items reference receipts",
            Category = "referential_integrity",
            Status = orphanItemCount == 0 ? PostRestoreSqlCheckStatus.Passed : PostRestoreSqlCheckStatus.Failed,
            Severity = PostRestoreSqlCheckSeverity.RequiredForL4,
            ReasonCode = orphanItemCount == 0
                ? PostRestoreSqlReasonCodes.ReferentialOk
                : PostRestoreSqlReasonCodes.OrphanReceiptItems,
            FailureCategory = orphanItemCount == 0
                ? PostRestoreSqlFailureCategory.None
                : PostRestoreSqlFailureCategory.IntegrityCheckFailed,
            Summary = orphanItemCount == 0
                ? "No receipt_items rows without matching receipt."
                : $"{orphanItemCount} receipt_items row(s) without matching receipt.",
            MeasuredValue = orphanItemCount
        });

        const string orphanTax = """
            SELECT COUNT(*)::bigint
            FROM receipt_tax_lines rtl
            LEFT JOIN receipts r ON r.receipt_id = rtl.receipt_id
            WHERE r.receipt_id IS NULL
            """;

        var orphanTaxCount = await ScalarLongAsync(conn, orphanTax, ct);
        checks.Add(new PostRestoreSqlCheckRow
        {
            Id = "referential_integrity.orphan_receipt_tax_lines",
            Name = "Receipt tax lines reference receipts",
            Category = "referential_integrity",
            Status = orphanTaxCount == 0 ? PostRestoreSqlCheckStatus.Passed : PostRestoreSqlCheckStatus.Failed,
            Severity = PostRestoreSqlCheckSeverity.RequiredForL4,
            ReasonCode = orphanTaxCount == 0
                ? PostRestoreSqlReasonCodes.ReferentialOk
                : PostRestoreSqlReasonCodes.OrphanReceiptTaxLines,
            FailureCategory = orphanTaxCount == 0
                ? PostRestoreSqlFailureCategory.None
                : PostRestoreSqlFailureCategory.IntegrityCheckFailed,
            Summary = orphanTaxCount == 0
                ? "No receipt_tax_lines rows without matching receipt."
                : $"{orphanTaxCount} receipt_tax_lines row(s) without matching receipt.",
            MeasuredValue = orphanTaxCount
        });

        const string receiptsMissingPayment = """
            SELECT COUNT(*)::bigint
            FROM receipts r
            LEFT JOIN payment_details pd ON pd.id = r.payment_id
            WHERE pd.id IS NULL
            """;

        var orphanReceiptPayCount = await ScalarLongAsync(conn, receiptsMissingPayment, ct);
        checks.Add(new PostRestoreSqlCheckRow
        {
            Id = "referential_integrity.receipts_payment_details",
            Name = "Receipts reference payment_details",
            Category = "referential_integrity",
            Status = orphanReceiptPayCount == 0 ? PostRestoreSqlCheckStatus.Passed : PostRestoreSqlCheckStatus.Failed,
            Severity = PostRestoreSqlCheckSeverity.RequiredForL4,
            ReasonCode = orphanReceiptPayCount == 0
                ? PostRestoreSqlReasonCodes.ReferentialOk
                : PostRestoreSqlReasonCodes.ReceiptsMissingPaymentDetails,
            FailureCategory = orphanReceiptPayCount == 0
                ? PostRestoreSqlFailureCategory.None
                : PostRestoreSqlFailureCategory.IntegrityCheckFailed,
            Summary = orphanReceiptPayCount == 0
                ? "No receipts without matching payment_details row."
                : $"{orphanReceiptPayCount} receipt row(s) without matching payment_details.",
            MeasuredValue = orphanReceiptPayCount
        });
    }

    /// <summary>D: Veri hacmi hakkında muhafazakâr bilgilendirme; L4’ü geçirmek veya düşürmek için kullanılmaz.</summary>
    private static void AppendInformativeDatasetSignals(List<PostRestoreSqlCheckRow> checks)
    {
        var receiptCount = checks
            .Where(c => c.Id == "fiscal_spine.receipts" && c.Status == PostRestoreSqlCheckStatus.Passed)
            .Select(c => c.MeasuredValue)
            .FirstOrDefault();

        if (receiptCount == null)
            return;

        var n = receiptCount.Value;
        checks.Add(new PostRestoreSqlCheckRow
        {
            Id = "informative.restored_receipt_row_count",
            Name = "Restored receipt row count (signal)",
            Category = "informative",
            Status = PostRestoreSqlCheckStatus.Passed,
            Severity = PostRestoreSqlCheckSeverity.Informative,
            ReasonCode = PostRestoreSqlReasonCodes.DatasetSizeNote,
            FailureCategory = PostRestoreSqlFailureCategory.None,
            Summary = n == 0
                ? "Restored database has zero receipts (empty dataset or pre-production); not a failure by itself."
                : $"Restored database contains {n} receipt row(s).",
            MeasuredValue = n
        });
    }

    private static string QuotePgIdent(string name) => "\"" + name.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    private static async Task<bool> RelationExistsAsync(
        NpgsqlConnection conn,
        string relName,
        CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(
            """
            SELECT EXISTS (
              SELECT 1
              FROM pg_class c
              JOIN pg_namespace n ON n.oid = c.relnamespace
              WHERE n.nspname = 'public'
                AND c.relkind IN ('r', 'p', 'm')
                AND c.relname = @name
            )
            """,
            conn);
        cmd.Parameters.AddWithValue("name", relName);
        cmd.CommandTimeout = 60;
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is true;
    }

    private static async Task<long> ScalarLongAsync(NpgsqlConnection conn, string sql, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.CommandTimeout = 120;
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is long l ? l : Convert.ToInt64(o ?? 0L);
    }

    private readonly record struct PostRestoreTableSpec(
        string RelName,
        string Category,
        string DisplayName,
        long? MinRows);
}
