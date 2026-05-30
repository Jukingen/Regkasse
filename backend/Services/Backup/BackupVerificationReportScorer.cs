namespace KasseAPI_Final.Services.Backup;

public static class BackupVerificationReportScorer
{
    public static int CalculateScore(
        bool logicalDumpAnalyzed,
        IReadOnlyList<string> monitoredTableNames,
        IReadOnlySet<string> dumpTableNames,
        IReadOnlyDictionary<string, long> sourceRowCountsByTable,
        IReadOnlyDictionary<string, long> dumpBackedRowCountsByTable)
    {
        if (monitoredTableNames.Count == 0)
            return logicalDumpAnalyzed ? 100 : 40;

        var score = 100;

        if (!logicalDumpAnalyzed)
            score -= 35;

        foreach (var table in monitoredTableNames)
        {
            if (!dumpTableNames.Contains(table))
                score -= 8;
        }

        foreach (var table in monitoredTableNames)
        {
            if (!sourceRowCountsByTable.TryGetValue(table, out var sourceRows))
                continue;
            if (!dumpBackedRowCountsByTable.TryGetValue(table, out var dumpRows))
                continue;
            if (sourceRows == dumpRows)
                continue;

            if (sourceRows <= 0)
                continue;

            var diff = Math.Abs(sourceRows - dumpRows);
            var diffPercent = (double)diff / sourceRows * 100.0;
            score -= (int)Math.Min(15, Math.Round(diffPercent * 0.5, MidpointRounding.AwayFromZero));
        }

        return Math.Clamp(score, 0, 100);
    }

    public static string MapStatus(int score) =>
        score >= 90 ? "Verified" : score >= 70 ? "PartiallyVerified" : "NotVerified";
}
