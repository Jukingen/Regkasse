namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// Runs <c>scripts/sql/fiscal_go_live_validation.sql</c> against a target PostgreSQL database (read-only checks in script).
/// </summary>
public interface IFiscalGoLiveValidationRunner
{
    Task<FiscalGoLiveValidationOutcome> RunScriptAsync(
        string absoluteScriptPath,
        string connectionString,
        CancellationToken cancellationToken = default);
}
