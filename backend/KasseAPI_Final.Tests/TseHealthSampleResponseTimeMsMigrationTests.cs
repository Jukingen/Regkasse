using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TseHealthSampleResponseTimeMsMigrationTests
{
    [Fact]
    public void AddTseHealthSampleResponseTimeMs_IsDiscoveredByEf()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "KasseAPI_Final.csproj")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        var designer = File.ReadAllText(Path.Combine(
            directory!.FullName,
            "Migrations",
            "_archive",
            "2026-10-28",
            "20260723280000_AddTseHealthSampleResponseTimeMs.Designer.cs"));
        Assert.Contains(
            "[Migration(\"20260723280000_AddTseHealthSampleResponseTimeMs\")]",
            designer,
            StringComparison.Ordinal);
        Assert.Contains("[DbContext(typeof(AppDbContext))]", designer, StringComparison.Ordinal);
    }
}
