using KasseAPI_Final.Models.RestoreVerification;

namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// Reason code + bağlamdan <see cref="PostRestoreSqlFailureCategory"/> üretir; eski kanıt satırları için geri dönüş.
/// </summary>
public static class PostRestoreSqlFailureCategoryMapper
{
    public const string EfMigrationsHistoryTableName = "__EFMigrationsHistory";

    /// <summary>
    /// L4 için tek satır: informative her zaman geçer; required ise status + failureCategory ile muhafazakâr değerlendirme.
    /// </summary>
    public static bool IsRequiredCheckPassingForL4(PostRestoreSqlCheckRow c)
    {
        if (c.Severity != PostRestoreSqlCheckSeverity.RequiredForL4)
            return true;
        if (c.Status != PostRestoreSqlCheckStatus.Passed)
            return false;
        if (c.FailureCategory == null)
            return true;
        return c.FailureCategory == PostRestoreSqlFailureCategory.None;
    }

    /// <summary>
    /// İlk (liste sırasına göre) başarısız required-check’in kategorisi; operatör odaklı rollup.
    /// </summary>
    public static PostRestoreSqlFailureCategory? ComputeDominantFailureCategory(
        IReadOnlyList<PostRestoreSqlCheckRow>? checks)
    {
        if (checks == null || checks.Count == 0)
            return PostRestoreSqlFailureCategory.Unknown;

        foreach (var c in checks)
        {
            if (c.Severity != PostRestoreSqlCheckSeverity.RequiredForL4)
                continue;
            if (IsRequiredCheckPassingForL4(c))
                continue;
            return c.FailureCategory
                   ?? FromReasonCode(c.ReasonCode, c.Status, c.Id);
        }

        return null;
    }

    /// <summary>Eski JSON’da <c>failureCategory</c> yoksa reason’dan türetir; <paramref name="checkId"/> <c>category.table</c> biçiminde olabilir.</summary>
    public static PostRestoreSqlFailureCategory FromReasonCode(
        string? reasonCode,
        PostRestoreSqlCheckStatus status,
        string? checkId = null)
    {
        if (status == PostRestoreSqlCheckStatus.Passed)
            return PostRestoreSqlFailureCategory.None;

        var tableHint = string.IsNullOrEmpty(checkId) ? null : ExtractTableFromId(checkId);

        return reasonCode switch
        {
            PostRestoreSqlReasonCodes.SchemaRelationMissing => PostRestoreSqlFailureCategory.MissingTable,
            PostRestoreSqlReasonCodes.TableQueryFailed => PostRestoreSqlFailureCategory.QueryFailed,
            PostRestoreSqlReasonCodes.MigrationHistoryInsufficient when tableHint == EfMigrationsHistoryTableName =>
                PostRestoreSqlFailureCategory.MigrationHistoryMissing,
            PostRestoreSqlReasonCodes.MigrationHistoryInsufficient => PostRestoreSqlFailureCategory.RowCountUnexpected,
            PostRestoreSqlReasonCodes.FiscalReceiptNumberEmptyOrNull or PostRestoreSqlReasonCodes.FiscalReceiptNumberDuplicate
                => PostRestoreSqlFailureCategory.IntegrityCheckFailed,
            PostRestoreSqlReasonCodes.OrphanReceiptItems or PostRestoreSqlReasonCodes.OrphanReceiptTaxLines
                or PostRestoreSqlReasonCodes.ReceiptsMissingPaymentDetails => PostRestoreSqlFailureCategory.IntegrityCheckFailed,
            PostRestoreSqlReasonCodes.SchemaMismatch => PostRestoreSqlFailureCategory.SchemaMismatch,
            PostRestoreSqlReasonCodes.MissingTargetConnection or PostRestoreSqlReasonCodes.UnhandledException =>
                PostRestoreSqlFailureCategory.Unknown,
            PostRestoreSqlReasonCodes.TableAccessible or PostRestoreSqlReasonCodes.FiscalReceiptNumberInvariantOk
                or PostRestoreSqlReasonCodes.ReferentialOk or PostRestoreSqlReasonCodes.DatasetSizeNote =>
                PostRestoreSqlFailureCategory.None,
            _ => PostRestoreSqlFailureCategory.Unknown
        };
    }

    private static string? ExtractTableFromId(string id)
    {
        var i = id.LastIndexOf('.');
        return i < 0 ? null : id[(i + 1)..];
    }
}
