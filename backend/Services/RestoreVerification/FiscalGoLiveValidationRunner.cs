using Microsoft.Extensions.Logging;
using Npgsql;

namespace KasseAPI_Final.Services.RestoreVerification;

public sealed class FiscalGoLiveValidationRunner : IFiscalGoLiveValidationRunner
{
    private readonly ILogger<FiscalGoLiveValidationRunner> _logger;

    public FiscalGoLiveValidationRunner(ILogger<FiscalGoLiveValidationRunner> logger)
    {
        _logger = logger;
    }

    public async Task<FiscalGoLiveValidationOutcome> RunScriptAsync(
        string absoluteScriptPath,
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(absoluteScriptPath))
        {
            return new FiscalGoLiveValidationOutcome
            {
                Executed = false,
                Passed = false,
                ErrorDetail = "Fiscal validation script file not found."
            };
        }

        var sql = await File.ReadAllTextAsync(absoluteScriptPath, cancellationToken);

        try
        {
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.CommandTimeout = 300;
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            var failCount = 0;
            var warnCount = 0;
            while (await reader.ReadAsync(cancellationToken))
            {
                var sev = reader.GetString(reader.GetOrdinal("severity"));
                if (string.Equals(sev, "FAIL", StringComparison.OrdinalIgnoreCase))
                    failCount++;
                else if (string.Equals(sev, "WARN", StringComparison.OrdinalIgnoreCase))
                    warnCount++;
            }

            string? summary = null;
            if (await reader.NextResultAsync(cancellationToken))
            {
                if (await reader.ReadAsync(cancellationToken))
                {
                    summary = reader.GetString(0);
                }
            }

            var passed = failCount == 0;
            if (!passed)
                _logger.LogWarning(
                    "Fiscal go-live validation reported FAIL rows: count={FailCount}, warn={WarnCount}",
                    failCount,
                    warnCount);

            return new FiscalGoLiveValidationOutcome
            {
                Executed = true,
                Passed = passed,
                FailCount = failCount,
                WarnCount = warnCount,
                SummaryLine = summary
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Fiscal go-live validation script execution failed");
            return new FiscalGoLiveValidationOutcome
            {
                Executed = false,
                Passed = false,
                ErrorDetail = ex.Message
            };
        }
    }
}
