namespace KasseAPI_Final.Services;

public interface IDemoResetService
{
    Task<ResetResult> ResetDatabaseAsync(CancellationToken ct);
}

public sealed class ResetResult
{
    public int DeletedRecordsCount { get; init; }
    public List<string> Errors { get; init; } = new();
    public bool StartbelegCreated { get; init; }
}
