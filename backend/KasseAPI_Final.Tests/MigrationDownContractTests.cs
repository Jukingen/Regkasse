using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Testcontainers.PostgreSql;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Live migrations (not the archive) must ship a Down that reverses schema changes.
/// Committed migration files are not edited here. The PostgreSQL round-trip is opt-in via Docker
/// and is excluded from the default unit filter.
/// </summary>
public sealed class MigrationDownContractTests
{
    [Fact]
    public void LiveMigrations_DownReversesUp()
    {
        var migrationsDir = FindLiveMigrationsDirectory();
        var failures = new List<string>();
        foreach (var file in Directory.EnumerateFiles(migrationsDir, "*.cs"))
        {
            var name = Path.GetFileName(file);
            if (name.EndsWith(".Designer.cs", StringComparison.Ordinal) || name == "AppDbContextModelSnapshot.cs")
                continue;

            var source = File.ReadAllText(file);
            var up = ExtractMethodBody(source, "Up");
            var down = ExtractMethodBody(source, "Down");
            if (!ContainsOperation(up))
                continue;
            if (!ContainsOperation(down))
                failures.Add(name + " Down is empty while Up changes schema");
        }

        Assert.True(failures.Count == 0, string.Join("; ", failures));
    }

    /// <summary>
    /// Applies the live chain, rolls back to 0, then reapplies. Uses a throwaway container only.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "PostgreSql")]
    public async Task Apply_RollbackToZero_Reapply()
    {
        PostgreSqlContainer? container = null;
        try
        {
            container = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
            await container.StartAsync();
        }
        catch (Exception ex)
        {
            if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Migration rollback needs Docker: " + ex.Message, ex);
            Skip.If(true, "Docker/Testcontainers unavailable: " + ex.Message);
            return;
        }

        await using (container)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(container!.GetConnectionString())
                .Options;
            await using var db = new AppDbContext(options);
            await db.Database.MigrateAsync();

            var applied = await db.Database.GetAppliedMigrationsAsync();
            Assert.NotEmpty(applied);

            var migrator = db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>()
                ?? throw new InvalidOperationException("IMigrator was not registered.");
            await migrator.MigrateAsync("0");

            var afterDown = await db.Database.GetAppliedMigrationsAsync();
            Assert.Empty(afterDown);

            await db.Database.MigrateAsync();
            var reapplied = await db.Database.GetAppliedMigrationsAsync();
            Assert.Equal(applied.Count(), reapplied.Count());
        }
    }

    private static bool ContainsOperation(string body)
    {
        var stripped = System.Text.RegularExpressions.Regex.Replace(body, @"//.*?$|/\*.*?\*/", string.Empty,
            System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.Multiline);
        return stripped.Contains("migrationBuilder.", StringComparison.Ordinal);
    }

    private static string ExtractMethodBody(string source, string methodName)
    {
        var marker = "void " + methodName + "(";
        var start = source.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
            throw new InvalidOperationException("Missing " + methodName);
        var brace = source.IndexOf('{', start);
        var depth = 0;
        for (var i = brace; i < source.Length; i++)
        {
            if (source[i] == '{')
                depth++;
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return source.Substring(brace + 1, i - brace - 1);
            }
        }

        throw new InvalidOperationException("Unclosed " + methodName);
    }

    private static string FindLiveMigrationsDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var direct = Path.Combine(dir.FullName, "Migrations");
            if (File.Exists(Path.Combine(direct, "20261028121002_InitialCreate.cs")))
                return direct;
            var nested = Path.Combine(dir.FullName, "backend", "Migrations");
            if (File.Exists(Path.Combine(nested, "20261028121002_InitialCreate.cs")))
                return nested;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Live Migrations directory was not found.");
    }
}
