namespace KasseAPI_Final.Services.RestoreVerification;

public interface IValidationRestoreExecutionService
{
    /// <summary>
    /// Restores a backup into an isolated database, runs continuity + fiscal validation SQL, then drops the database.
    /// </summary>
    Task<RestoreResult> ExecuteValidationRestoreAsync(
        ValidationRestoreExecutionRequest request,
        CancellationToken cancellationToken = default);
}
