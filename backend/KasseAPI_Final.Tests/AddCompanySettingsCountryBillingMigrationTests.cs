using System.Reflection;
using KasseAPI_Final.Data;
using KasseAPI_Final.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Inspects the recorded migration operations without a database: every new column must carry a
/// <c>defaultValue</c> (which is what backfills existing rows in PostgreSQL), the explicit backfill
/// statements must be present, and no existing column or index may be touched.
/// </summary>
public sealed class AddCompanySettingsCountryBillingMigrationTests
{
    private const string Table = "company_settings";

    [Fact]
    public void Migration_IsDiscoveredByEf()
    {
        var migrationAttribute = typeof(AddCompanySettingsCountryBilling).GetCustomAttribute<MigrationAttribute>();
        Assert.NotNull(migrationAttribute);
        Assert.Equal("20260916110000_AddCompanySettingsCountryBilling", migrationAttribute!.Id);

        var contextAttribute = typeof(AddCompanySettingsCountryBilling).GetCustomAttribute<DbContextAttribute>();
        Assert.NotNull(contextAttribute);
        Assert.Equal(typeof(AppDbContext), contextAttribute!.ContextType);
    }

    [Fact]
    public void Up_AddsExactlyTheThreeNewColumns()
    {
        var added = RunUp().OfType<AddColumnOperation>().ToList();

        Assert.Equal(3, added.Count);
        Assert.All(added, op => Assert.Equal(Table, op.Table));
        Assert.Equal(
            ["billing_country", "vat_regime", "tax_exempt"],
            added.Select(op => op.Name).ToArray());
    }

    [Fact]
    public void Up_GivesEveryNonNullableColumnADefaultValue()
    {
        var added = RunUp().OfType<AddColumnOperation>().ToList();

        var billingCountry = added.Single(op => op.Name == "billing_country");
        Assert.True(billingCountry.IsNullable);
        Assert.Equal(2, billingCountry.MaxLength);
        Assert.Null(billingCountry.DefaultValue);

        var vatRegime = added.Single(op => op.Name == "vat_regime");
        Assert.False(vatRegime.IsNullable);
        Assert.Equal(32, vatRegime.MaxLength);
        Assert.Equal("AT_RKSV_STANDARD", vatRegime.DefaultValue);

        var taxExempt = added.Single(op => op.Name == "tax_exempt");
        Assert.False(taxExempt.IsNullable);
        Assert.Equal(false, taxExempt.DefaultValue);
    }

    [Fact]
    public void Up_BackfillsExistingRowsToTheAustrianRegime()
    {
        var statements = RunUp().OfType<SqlOperation>().Select(op => Normalize(op.Sql)).ToList();

        Assert.Contains(
            statements,
            sql => sql.Contains("UPDATE company_settings", StringComparison.OrdinalIgnoreCase)
                   && sql.Contains("vat_regime = 'AT_RKSV_STANDARD'", StringComparison.OrdinalIgnoreCase));

        Assert.Contains(
            statements,
            sql => sql.Contains("UPDATE company_settings", StringComparison.OrdinalIgnoreCase)
                   && sql.Contains("tax_exempt = false", StringComparison.OrdinalIgnoreCase));

        // The operating country is already 'AT' for existing rows and must not be rewritten.
        Assert.DoesNotContain(statements, sql => sql.Contains("SET country", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Up_DoesNotModifyAnyExistingColumnOrIndex()
    {
        var operations = RunUp();

        Assert.Empty(operations.OfType<AlterColumnOperation>());
        Assert.Empty(operations.OfType<DropColumnOperation>());
        Assert.Empty(operations.OfType<RenameColumnOperation>());
        Assert.Empty(operations.OfType<CreateIndexOperation>());
        Assert.Empty(operations.OfType<DropIndexOperation>());
        Assert.Empty(operations.OfType<AlterTableOperation>());
    }

    [Fact]
    public void Down_DropsOnlyTheThreeNewColumns()
    {
        var migration = new AddCompanySettingsCountryBilling();
        var builder = new MigrationBuilder(activeProvider: "Npgsql.EntityFrameworkCore.PostgreSQL");
        Invoke(migration, "Down", builder);

        var dropped = builder.Operations.OfType<DropColumnOperation>().ToList();
        Assert.Equal(builder.Operations.Count, dropped.Count);
        Assert.All(dropped, op => Assert.Equal(Table, op.Table));
        Assert.Equal(
            ["billing_country", "vat_regime", "tax_exempt"],
            dropped.Select(op => op.Name).ToArray());
    }

    private static List<MigrationOperation> RunUp()
    {
        var migration = new AddCompanySettingsCountryBilling();
        var builder = new MigrationBuilder(activeProvider: "Npgsql.EntityFrameworkCore.PostgreSQL");
        Invoke(migration, "Up", builder);
        return [.. builder.Operations];
    }

    private static void Invoke(Migration migration, string methodName, MigrationBuilder builder)
    {
        var method = typeof(AddCompanySettingsCountryBilling)
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(migration, [builder]);
    }

    private static string Normalize(string sql) =>
        string.Join(' ', sql.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
