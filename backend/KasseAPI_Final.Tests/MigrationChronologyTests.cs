using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Migration ids must sort after the baseline <c>InitialCreate</c>.
/// An earlier timestamp is applied before the schema exists.
/// </summary>
public sealed class MigrationChronologyTests
{
    /// <summary>
    /// January 2025 ids predate <c>InitialCreate</c>. They were never applied
    /// as schema changes; history-only reconciliation records them as applied.
    /// </summary>
    private static readonly HashSet<string> KnownLegacyIds = new()
    {
        "20250115000000_UpdateProductTableForRKSV",
        "20250115000001_UpdateProductTableNamingConvention",
    };

    private static readonly Regex MigrationAttributePattern = new(
        @"\[Migration\(""(?<id>[^""]+)""\)\]",
        RegexOptions.Compiled);

    private readonly ITestOutputHelper _output;

    public MigrationChronologyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void EveryMigrationId_SortsAfterInitialCreate()
    {
        var directory = Path.Combine(FindBackendRoot(), "Migrations");
        var ids = new List<string>();
        var unreadable = new List<string>();

        foreach (var path in Directory.EnumerateFiles(directory, "*.cs"))
        {
            var name = Path.GetFileName(path);
            if (name.EndsWith(".Designer.cs", StringComparison.Ordinal)
                || name is "AppDbContextModelSnapshot.cs" or "UnregisteredRecentMigrations.cs" or "MigrationDesignerModel.cs")
            {
                continue;
            }

            var text = File.ReadAllText(path);
            if (!Regex.IsMatch(text, @":\s*Migration\b"))
                continue;

            var id = MigrationAttributePattern.Match(text);
            if (!id.Success)
            {
                var designerPath = Path.Combine(
                    directory,
                    Path.GetFileNameWithoutExtension(path) + ".Designer.cs");
                if (File.Exists(designerPath))
                    id = MigrationAttributePattern.Match(File.ReadAllText(designerPath));
            }

            if (!id.Success)
            {
                unreadable.Add($"{name}: missing [Migration]");
                continue;
            }

            ids.Add(id.Groups["id"].Value);
        }

        var initialCreate = ids
            .Where(id => id.EndsWith("_InitialCreate", StringComparison.Ordinal))
            .OrderBy(id => id, StringComparer.Ordinal)
            .FirstOrDefault();

        Assert.False(
            string.IsNullOrEmpty(initialCreate),
            "No [Migration] id ending in _InitialCreate was found under backend/Migrations.");

        foreach (var legacyId in ids.Where(KnownLegacyIds.Contains).OrderBy(id => id, StringComparer.Ordinal))
        {
            _output.WriteLine(
                "warning: legacy migration id is excluded from the InitialCreate chronology gate: " + legacyId);
        }

        var offenders = ids
            .Where(id => !KnownLegacyIds.Contains(id))
            .Where(id => string.CompareOrdinal(id, initialCreate) < 0)
            .OrderBy(id => id, StringComparer.Ordinal)
            .Concat(unreadable)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Migration IDs must sort after " + initialCreate + ". Offending ids:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, offenders));
    }

    private static string FindBackendRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "KasseAPI_Final.csproj")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
