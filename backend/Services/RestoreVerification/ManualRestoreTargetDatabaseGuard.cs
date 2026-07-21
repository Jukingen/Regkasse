using System.Text.RegularExpressions;
using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;

namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>Blocks production and invalid PostgreSQL database names for manual restore targets.</summary>
public sealed class ManualRestoreTargetDatabaseGuard
{
    private static readonly Regex IdentifierRegex = new(
        @"^[a-z_][a-z0-9_]{0,62}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IConfiguration _configuration;
    private readonly IOptionsMonitor<ManualRestoreApprovalOptions> _options;

    public ManualRestoreTargetDatabaseGuard(
        IConfiguration configuration,
        IOptionsMonitor<ManualRestoreApprovalOptions> options)
    {
        _configuration = configuration;
        _options = options;
    }

    public void ValidateOrThrow(string targetDatabaseName)
    {
        var normalized = targetDatabaseName.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(normalized))
            throw new ArgumentException("Target database name is required.", nameof(targetDatabaseName));

        if (!IdentifierRegex.IsMatch(normalized))
            throw new ArgumentException(
                "Target database name must be a lowercase PostgreSQL identifier (letters, digits, underscore; max 63).",
                nameof(targetDatabaseName));

        var prefix = (_options.CurrentValue.TargetDatabaseNamePrefix ?? "restore_validation_").Trim().ToLowerInvariant();
        if (!normalized.StartsWith(prefix, StringComparison.Ordinal))
            throw new ArgumentException(
                $"Target database name must start with '{prefix}' (validation-only restore).",
                nameof(targetDatabaseName));

        var blocked = CollectBlockedNames();
        if (blocked.Contains(normalized))
            throw new ArgumentException(
                "Target database name matches a blocked production or operational database.",
                nameof(targetDatabaseName));
    }

    private HashSet<string> CollectBlockedNames()
    {
        var blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in _options.CurrentValue.AdditionalBlockedDatabaseNames ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(name))
                blocked.Add(name.Trim());
        }

        AddConnectionDatabase(blocked, "DefaultConnection");

        var restoreSection = _configuration.GetSection("RestoreVerification");
        var isoConn = restoreSection["IsolatedRestoreAdminConnectionStringName"];
        if (!string.IsNullOrWhiteSpace(isoConn))
            AddConnectionDatabase(blocked, isoConn.Trim());

        var fiscalConn = restoreSection["FiscalValidationConnectionStringName"];
        if (!string.IsNullOrWhiteSpace(fiscalConn))
            AddConnectionDatabase(blocked, fiscalConn.Trim());

        return blocked;
    }

    private void AddConnectionDatabase(HashSet<string> blocked, string? connectionStringName)
    {
        if (string.IsNullOrWhiteSpace(connectionStringName))
            return;

        var cs = _configuration.GetConnectionString(connectionStringName);
        if (string.IsNullOrWhiteSpace(cs))
            return;

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(cs);
            if (!string.IsNullOrWhiteSpace(builder.Database))
                blocked.Add(builder.Database.Trim());
        }
        catch
        {
            // Ignore malformed connection strings at validation time.
        }
    }
}
