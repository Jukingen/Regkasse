namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Pre-restore RKSV compliance evaluation (same-tenant, integrity, validation-only gates).
/// Does not imply a production restore ran or will run.
/// </summary>
public sealed class ComplianceResult
{
    public bool Succeeded { get; private init; }

    public string? Code { get; private init; }

    public string? Error { get; private init; }

    public Guid? BackupRunId { get; private init; }

    public Guid? TenantId { get; private init; }

    public IReadOnlyList<ComplianceCheckItem> Checks { get; private init; } = Array.Empty<ComplianceCheckItem>();

    public static ComplianceResult Success(
        Guid backupRunId,
        Guid? tenantId,
        IReadOnlyList<ComplianceCheckItem> checks) =>
        new()
        {
            Succeeded = true,
            BackupRunId = backupRunId,
            TenantId = tenantId,
            Checks = checks
        };

    public static ComplianceResult Fail(
        string code,
        string error,
        Guid? backupRunId = null,
        Guid? tenantId = null,
        IReadOnlyList<ComplianceCheckItem>? checks = null) =>
        new()
        {
            Succeeded = false,
            Code = code,
            Error = error,
            BackupRunId = backupRunId,
            TenantId = tenantId,
            Checks = checks ?? Array.Empty<ComplianceCheckItem>()
        };
}

public sealed class ComplianceCheckItem
{
    public string Name { get; init; } = string.Empty;

    public bool Passed { get; init; }

    public string? Detail { get; init; }

    public static ComplianceCheckItem Pass(string name, string? detail = null) =>
        new() { Name = name, Passed = true, Detail = detail };

    public static ComplianceCheckItem Fail(string name, string detail) =>
        new() { Name = name, Passed = false, Detail = detail };
}
