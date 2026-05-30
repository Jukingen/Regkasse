using System.Text.RegularExpressions;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Parses <c>pg_restore --list</c> stdout for TABLE DATA TOC entries (custom-format dumps).
/// </summary>
public static partial class PgRestoreListTableDataParser
{
    /// <summary>Preferred business tables for backup verification reporting (public schema).</summary>
    public static readonly IReadOnlyList<string> PreferredMonitoredTableNames =
    [
        "products",
        "categories",
        "payment_details",
        "invoices",
        "receipts",
        "customers",
        "users",
        "audit_logs",
        "cash_registers",
        "offline_transactions",
        "vouchers",
    ];

    // e.g. "3875; 0 176717 TABLE DATA public products postgres"
    [GeneratedRegex(
        @"^\s*\d+\s*;\s*\d+\s+\d+\s+TABLE\s+DATA\s+(?:-\s+)?(\S+)\s+(\S+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TableDataLineRegex();

    public static IReadOnlyList<PgRestoreListTableEntry> ParseTableDataEntries(string? pgRestoreListStdout)
    {
        if (string.IsNullOrWhiteSpace(pgRestoreListStdout))
            return Array.Empty<PgRestoreListTableEntry>();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<PgRestoreListTableEntry>();

        foreach (var rawLine in pgRestoreListStdout.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(';'))
                continue;

            var m = TableDataLineRegex().Match(line);
            if (!m.Success)
                continue;

            var schema = m.Groups[1].Value;
            var table = m.Groups[2].Value;
            var key = $"{schema}.{table}";
            if (!seen.Add(key))
                continue;

            list.Add(new PgRestoreListTableEntry(schema, table));
        }

        return list;
    }
}

public sealed record PgRestoreListTableEntry(string SchemaName, string TableName)
{
    public string QualifiedName => $"{SchemaName}.{TableName}";
}
