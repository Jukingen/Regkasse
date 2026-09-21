using System.Reflection;
using KasseAPI_Final.Data;
using KasseAPI_Final.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Additive issue-time snapshots: nullable columns only, no backfill, no altered existing columns.
/// </summary>
public sealed class AddFiscalDocumentCountryAtIssueSnapshotsMigrationTests
{
    private static readonly string[] Tables = ["invoices", "receipts", "payment_details"];

    [Fact]
    public void Migration_IsDiscoveredByEf()
    {
        var migrationAttribute = typeof(AddFiscalDocumentCountryAtIssueSnapshots).GetCustomAttribute<MigrationAttribute>();
        Assert.NotNull(migrationAttribute);
        Assert.Equal("20260921180000_AddFiscalDocumentCountryAtIssueSnapshots", migrationAttribute!.Id);

        var contextAttribute = typeof(AddFiscalDocumentCountryAtIssueSnapshots).GetCustomAttribute<DbContextAttribute>();
        Assert.NotNull(contextAttribute);
        Assert.Equal(typeof(AppDbContext), contextAttribute!.ContextType);
    }

    [Fact]
    public void Up_AddsNullableSnapshotColumnsOnFiscalTables()
    {
        var added = RunUp().OfType<AddColumnOperation>().ToList();

        Assert.Equal(6, added.Count);
        Assert.All(added, op => Assert.True(op.IsNullable));
        Assert.All(added, op => Assert.Null(op.DefaultValue));
        Assert.Equal(
            Tables.OrderBy(t => t, StringComparer.Ordinal).ToArray(),
            added.Select(op => op.Table).Distinct().OrderBy(t => t, StringComparer.Ordinal).ToArray());

        foreach (var table in Tables)
        {
            var country = added.Single(op => op.Table == table && op.Name == "country_code_at_issue");
            Assert.Equal(2, country.MaxLength);
            var regime = added.Single(op => op.Table == table && op.Name == "vat_regime_at_issue");
            Assert.Equal(32, regime.MaxLength);
        }
    }

    [Fact]
    public void Up_DoesNotBackfillOrModifyExistingColumns()
    {
        var operations = RunUp();

        Assert.Empty(operations.OfType<SqlOperation>());
        Assert.Empty(operations.OfType<AlterColumnOperation>());
        Assert.Empty(operations.OfType<DropColumnOperation>());
        Assert.Empty(operations.OfType<RenameColumnOperation>());
        Assert.Empty(operations.OfType<CreateIndexOperation>());
        Assert.Empty(operations.OfType<DropIndexOperation>());
        Assert.Empty(operations.OfType<AlterTableOperation>());
    }

    [Fact]
    public void Down_DropsOnlyTheSnapshotColumns()
    {
        var migration = new AddFiscalDocumentCountryAtIssueSnapshots();
        var builder = new MigrationBuilder(activeProvider: "Npgsql.EntityFrameworkCore.PostgreSQL");
        Invoke(migration, "Down", builder);

        var dropped = builder.Operations.OfType<DropColumnOperation>().ToList();
        Assert.Equal(builder.Operations.Count, dropped.Count);
        Assert.Equal(6, dropped.Count);
        Assert.All(
            dropped,
            op => Assert.True(
                op.Name is "country_code_at_issue" or "vat_regime_at_issue"));
    }

    private static List<MigrationOperation> RunUp()
    {
        var migration = new AddFiscalDocumentCountryAtIssueSnapshots();
        var builder = new MigrationBuilder(activeProvider: "Npgsql.EntityFrameworkCore.PostgreSQL");
        Invoke(migration, "Up", builder);
        return [.. builder.Operations];
    }

    private static void Invoke(Migration migration, string methodName, MigrationBuilder builder)
    {
        var method = typeof(AddFiscalDocumentCountryAtIssueSnapshots)
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(migration, [builder]);
    }
}
