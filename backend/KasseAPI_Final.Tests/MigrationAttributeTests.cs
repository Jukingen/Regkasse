using System.Reflection;
using System.Text.RegularExpressions;
using KasseAPI_Final.Data;
using KasseAPI_Final.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Fails when a migration class is missing <see cref="MigrationAttribute"/>,
/// <see cref="DbContextAttribute"/>, or its <c>*.Designer.cs</c> sibling.
/// </summary>
public sealed class MigrationAttributeTests
{
    private static readonly Regex MigrationAttributePattern = new(
        @"\[Migration\(""(?<id>[^""]+)""\)\]",
        RegexOptions.Compiled);

    [Fact]
    public void EveryMigrationFile_HasDesigner_WithMigrationAndDbContextAttributes()
    {
        var directory = Path.Combine(FindBackendRoot(), "Migrations");
        var offenders = new List<string>();

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

            var designerPath = Path.Combine(
                directory,
                Path.GetFileNameWithoutExtension(path) + ".Designer.cs");
            if (!File.Exists(designerPath))
            {
                offenders.Add($"{name}: missing Designer.cs");
                continue;
            }

            var designer = File.ReadAllText(designerPath);
            if (!MigrationAttributePattern.IsMatch(designer))
                offenders.Add($"{name}: Designer lacks [Migration]");
            if (!designer.Contains("[DbContext(typeof(AppDbContext))]", StringComparison.Ordinal))
                offenders.Add($"{name}: Designer lacks [DbContext(typeof(AppDbContext))]");
            if (!designer.Contains("BuildTargetModel", StringComparison.Ordinal))
                offenders.Add($"{name}: Designer lacks BuildTargetModel");
        }

        Assert.True(
            offenders.Count == 0,
            "Every migration needs a Designer with [Migration], [DbContext(typeof(AppDbContext))], and BuildTargetModel:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, offenders.OrderBy(line => line, StringComparer.Ordinal)));
    }

    [Fact]
    public void EveryMigrationClass_HasUniqueMigrationId_AndAppDbContext()
    {
        var types = typeof(AppDbContext).Assembly
            .GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && typeof(Migration).IsAssignableFrom(type))
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(types);

        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        var offenders = new List<string>();
        foreach (var type in types)
        {
            var migration = type.GetCustomAttribute<MigrationAttribute>();
            if (migration is null)
            {
                offenders.Add($"{type.Name}: missing [Migration]");
                continue;
            }

            var context = type.GetCustomAttribute<DbContextAttribute>();
            if (context is null || context.ContextType != typeof(AppDbContext))
                offenders.Add($"{type.Name}: missing [DbContext(typeof(AppDbContext))]");

            if (!ids.TryAdd(migration.Id, type.Name))
                offenders.Add($"Duplicate MigrationId {migration.Id} on {type.Name} and {ids[migration.Id]}");
        }

        Assert.True(
            offenders.Count == 0,
            string.Join(Environment.NewLine, offenders.OrderBy(line => line, StringComparer.Ordinal)));
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
