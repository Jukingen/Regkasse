namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// İzole geri yüklenen PostgreSQL veritabanında RKSV / iş sürekliliği için yapılandırılmış SQL kontrolleri.
/// </summary>
public interface IPostRestoreDrillSqlChecker
{
    Task<PostRestoreDrillSqlOutcome> RunContinuityChecksAsync(
        string targetDatabaseConnectionString,
        CancellationToken cancellationToken = default);
}

/// <param name="Passed">Executed true ve <see cref="PostRestoreDrillSqlChecker.ComputeL4LayerPass"/> (RequiredForL4 satırları) geçti.</param>
public sealed class PostRestoreDrillSqlOutcome
{
    public bool Executed { get; init; }

    public bool Passed { get; init; }

    public string? ErrorDetail { get; init; }

    public DateTimeOffset? StartedAtUtc { get; init; }

    public DateTimeOffset? CompletedAtUtc { get; init; }

    public long? DurationMs { get; init; }

    public IReadOnlyList<PostRestoreSqlCheckRow> Checks { get; init; } = Array.Empty<PostRestoreSqlCheckRow>();

    public static PostRestoreDrillSqlOutcome Skipped(string reason) => new()
    {
        Executed = false,
        Passed = false,
        ErrorDetail = reason,
        Checks = Array.Empty<PostRestoreSqlCheckRow>()
    };
}
