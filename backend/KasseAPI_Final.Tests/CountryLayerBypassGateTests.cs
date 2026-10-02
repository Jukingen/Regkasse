using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.Strategies;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// CI gate for the country layer. Fails when production code bypasses
/// <see cref="VatIdPatterns"/>, when a seeded country has no strategy, or when
/// <see cref="CompanySettings"/> grows a second country column.
/// </summary>
public sealed class CountryLayerBypassGateTests
{
    private static readonly Regex CompanySettingsColumnAdd = new(
        @"AddColumn<[^>]+>\(\s*name:\s*""(?<name>[^""]+)""\s*,\s*table:\s*""company_settings""",
        RegexOptions.Compiled);

    /// <summary>
    /// The operating column <c>country</c> predates the billing migration. It is the one country
    /// field. Billing fields may appear only in <c>20260916110000_AddCompanySettingsCountryBilling</c>.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string[]> AllowedCompanySettingsCountrySchema =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["20260723190000_AddTenantSettingsHistory.cs"] = ["country"],
            ["20260916110000_AddCompanySettingsCountryBilling.cs"] = ["billing_country", "vat_regime", "tax_exempt"],
        };

    [Fact]
    public void VatIdRegexLiterals_LiveOnlyInVatIdPatternsAndCountryProfileSeeds()
    {
        var patterns = typeof(VatIdPatterns)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Where(pattern => pattern.Length > 0)
            .ToArray();

        Assert.NotEmpty(patterns);
        Assert.Contains(@"^ATU\d{8}$", patterns);

        var backendRoot = FindBackendRoot();
        var offenders = new List<string>();
        foreach (var path in EnumerateProductionSources(backendRoot))
        {
            if (IsAllowedVatIdSource(path))
                continue;

            var text = File.ReadAllText(path);
            foreach (var pattern in patterns)
            {
                if (text.Contains(pattern, StringComparison.Ordinal))
                    offenders.Add($"{Relative(backendRoot, path)} declares `{pattern}`");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "VAT-ID regex literals belong only in Models/Countries/VatIdPatterns.cs and CountryProfile seeds "
            + "(CountryProfileRegistry.cs). Offending files:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, offenders.OrderBy(line => line, StringComparer.Ordinal)));
    }

    [Fact]
    public void EveryStrategy_ResolvesForSeededCountries_AndUnknownPairThrowsUnknownTaxRegime()
    {
        var registry = new CountryProfileRegistry();
        var taxResolver = CountryStrategyWiring.CreateTaxResolver();
        var invoiceResolver = CountryStrategyWiring.CreateInvoiceResolver();

        (string Code, VatRegime Regime)[] seeded =
        [
            (CountryProfileCodes.Austria, VatRegime.AT_RKSV_STANDARD),
            (CountryProfileCodes.Germany, VatRegime.DE_USTG_STANDARD),
            (CountryProfileCodes.Switzerland, VatRegime.CH_MWST_STANDARD),
            (CountryProfileCodes.EuDefault, VatRegime.EU_OSS),
        ];

        var resolvedTax = new List<Type>();
        var resolvedInvoice = new List<Type>();
        foreach (var (code, regime) in seeded)
        {
            var profile = registry.Get(code);
            var tax = taxResolver.Resolve(profile, regime);
            var invoice = invoiceResolver.Resolve(profile, regime);

            Assert.Equal(code, tax.CountryCode);
            Assert.Equal(code, invoice.CountryCode);
            resolvedTax.Add(tax.GetType());
            resolvedInvoice.Add(invoice.GetType());
        }

        Assert.Equal(ConcreteImplementations(typeof(ITaxStrategy)), resolvedTax.Distinct().OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray());
        Assert.Equal(ConcreteImplementations(typeof(IInvoiceStrategy)), resolvedInvoice.Distinct().OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray());

        var swiss = registry.Get(CountryProfileCodes.Switzerland);
        var taxError = Assert.Throws<UnknownTaxRegimeException>(
            () => taxResolver.Resolve(swiss, VatRegime.AT_RKSV_STANDARD));
        var invoiceError = Assert.Throws<UnknownTaxRegimeException>(
            () => invoiceResolver.Resolve(swiss, VatRegime.AT_RKSV_STANDARD));

        Assert.Equal(UnknownTaxRegimeException.Code, taxError.ErrorCode);
        Assert.Equal("UNKNOWN_TAX_REGIME", invoiceError.ErrorCode);
    }

    [Fact]
    public void CompanySettings_HasNoCountryCodeColumn_AndBillingMigrationIsTheOnlyExtraCountrySchema()
    {
        Assert.Null(typeof(CompanySettings).GetProperty("CountryCode"));

        var columnNames = typeof(CompanySettings)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.GetCustomAttribute<ColumnAttribute>()?.Name)
            .Where(name => name is not null)
            .ToArray();

        Assert.Contains("country", columnNames);
        Assert.DoesNotContain("country_code", columnNames);

        var backendRoot = FindBackendRoot();
        var migrations = Directory.EnumerateFiles(Path.Combine(backendRoot, "Migrations"), "*.cs", SearchOption.TopDirectoryOnly)
            .Where(path => !path.EndsWith(".Designer.cs", StringComparison.Ordinal))
            .Where(path => !path.EndsWith("AppDbContextModelSnapshot.cs", StringComparison.Ordinal));

        var found = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var path in migrations)
        {
            var up = UpBody(File.ReadAllText(path));
            foreach (Match match in CompanySettingsColumnAdd.Matches(up))
            {
                var name = match.Groups["name"].Value;
                if (!IsCompanySettingsCountrySchema(name))
                    continue;

                var file = Path.GetFileName(path);
                if (!found.TryGetValue(file, out var columns))
                {
                    columns = new SortedSet<string>(StringComparer.Ordinal);
                    found[file] = columns;
                }

                columns.Add(name);
            }
        }

        var offenders = new List<string>();
        foreach (var (file, columns) in found)
        {
            if (!AllowedCompanySettingsCountrySchema.TryGetValue(file, out var allowed))
            {
                offenders.Add($"{file}: {string.Join(", ", columns)}");
                continue;
            }

            var extra = columns.Except(allowed, StringComparer.Ordinal).ToArray();
            var missing = allowed.Except(columns, StringComparer.Ordinal).ToArray();
            if (extra.Length > 0 || missing.Length > 0)
                offenders.Add($"{file}: found [{string.Join(", ", columns)}], allowed [{string.Join(", ", allowed)}]");
        }

        foreach (var required in AllowedCompanySettingsCountrySchema.Keys)
        {
            if (!found.ContainsKey(required))
                offenders.Add($"{required}: expected country schema columns were not found");
        }

        Assert.True(
            offenders.Count == 0,
            "CompanySettings has one country column (`country`). "
            + "Billing fields belong only in 20260916110000_AddCompanySettingsCountryBilling. "
            + "A CountryCode column or another country-schema migration is forbidden. Offending files:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void CountryAuditAndActivityEventNumbers_StayPinned()
    {
        AssertPinned(AuditEventType.TenantCreatedWithCountry, 96);
        AssertPinned(AuditEventType.TenantCountryChanged, 97);
        AssertPinned(AuditEventType.TenantCountryChangedHistoricalPreserved, 98);
        AssertPinned(AuditEventType.EinvoiceValidated, 108);
        AssertPinned(AuditEventType.EinvoiceSubmitted, 109);
        AssertPinned(AuditEventType.EinvoiceSubmissionFailed, 110);
        AssertPinned(AuditEventType.QrRechnungPayloadBuilt, 111);
        AssertPinned(AuditEventType.QrRechnungPdfGenerated, 112);
        AssertPinned(AuditEventType.PeppolParticipantRegistered, 113);
        AssertPinned(AuditEventType.ChQrKnownGapsAccepted, 114);
        AssertPinned(AuditEventType.EinvoiceAckReceived, 115);
        AssertPinned(AuditEventType.QrRechnungPdfDownloaded, 116);
        AssertPinned(AuditEventType.QrRechnungBankUploadConfirmed, 117);
        AssertPinned(AuditEventType.EinvoiceSubmissionRetry, 118);

        AssertPinned(ActivityEventType.TenantCountryChanged, 251);
        AssertPinned(ActivityEventType.QrRechnungPayloadBuilt, 253);
        AssertPinned(ActivityEventType.QrRechnungPdfGenerated, 254);
        AssertPinned(ActivityEventType.TenantCountryChangedHistoricalPreserved, 255);
        AssertPinned(ActivityEventType.EinvoiceValidated, 256);
        AssertPinned(ActivityEventType.EinvoiceSubmitted, 257);
        AssertPinned(ActivityEventType.EinvoiceSubmissionFailed, 258);
        AssertPinned(ActivityEventType.PeppolParticipantRegistered, 259);
        AssertPinned(ActivityEventType.ChQrKnownGapsAccepted, 260);
        AssertPinned(ActivityEventType.ChQrKnownGapsOutstanding, 261);
        AssertPinned(ActivityEventType.EinvoiceAckReceived, 262);
        AssertPinned(ActivityEventType.EinvoiceSubmissionRetry, 263);
    }

    /// <summary>
    /// Integer OpenAPI enums need <c>x-enum-varnames</c> or Orval emits <c>NUMBER_n</c>.
    /// String enums are excluded: Swashbuckle writes the C# member name as the enum value
    /// when the type uses <c>JsonStringEnumConverter</c> (for example <c>VatRegime</c>),
    /// and <c>ActivityEventType</c> is published as those names so the activity feed stays a string.
    /// Orval already uses those strings as the generated constant names, so <c>x-enum-varnames</c>
    /// is not required for them.
    /// </summary>
    [Fact]
    public void GeneratedOpenApi_EveryCSharpEnum_HasEnumVarnames()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(ResolveCommittedSwaggerPath()));
        var schemas = doc.RootElement.GetProperty("components").GetProperty("schemas");
        var offenders = new List<string>();
        var integerEnums = 0;

        foreach (var schema in schemas.EnumerateObject())
        {
            if (!schema.Value.TryGetProperty("enum", out var enumValues)
                || enumValues.ValueKind != JsonValueKind.Array
                || enumValues.GetArrayLength() == 0)
                continue;

            if (!IsIntegerEnum(schema.Value))
                continue;

            integerEnums++;
            if (!schema.Value.TryGetProperty("x-enum-varnames", out var names)
                || names.ValueKind != JsonValueKind.Array)
            {
                offenders.Add($"{schema.Name}: missing x-enum-varnames");
                continue;
            }

            if (names.GetArrayLength() != enumValues.GetArrayLength())
            {
                offenders.Add(
                    $"{schema.Name}: x-enum-varnames length {names.GetArrayLength()} != enum length {enumValues.GetArrayLength()}");
            }
        }

        Assert.True(integerEnums > 0, "Committed swagger.json has no integer enums.");
        Assert.True(
            offenders.Count == 0,
            "Integer enums in backend/swagger.json must carry x-enum-varnames from EnumVarnamesSchemaFilter:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, offenders));

        AssertIntegerEnumVarnames(schemas, "AuditEventType");
        AssertIntegerEnumVarnames(schemas, "BackupRunStatus");
        AssertIntegerEnumVarnames(schemas, "InvoiceStatus");
        AssertStringEnumIsReadableWithoutVarnames(schemas, "ActivityEventType");
        AssertStringEnumIsReadableWithoutVarnames(schemas, "VatRegime");
    }

    [Fact]
    public void AuditEventType_And_ActivityEventType_Are_In_OpenApi()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(ResolveCommittedSwaggerPath()));
        var schemas = doc.RootElement.GetProperty("components").GetProperty("schemas");
        Assert.True(schemas.TryGetProperty("AuditEventType", out _), "AuditEventType is missing from components.schemas");
        Assert.True(schemas.TryGetProperty("ActivityEventType", out _), "ActivityEventType is missing from components.schemas");
    }

    private static void AssertIntegerEnumVarnames(JsonElement schemas, string name)
    {
        Assert.True(schemas.TryGetProperty(name, out var schema), name);
        Assert.Equal("integer", schema.GetProperty("type").GetString());
        Assert.Equal(
            schema.GetProperty("enum").GetArrayLength(),
            schema.GetProperty("x-enum-varnames").GetArrayLength());
    }

    private static void AssertStringEnumIsReadableWithoutVarnames(JsonElement schemas, string name)
    {
        Assert.True(schemas.TryGetProperty(name, out var schema), name);
        Assert.Equal("string", schema.GetProperty("type").GetString());
        var values = schema.GetProperty("enum");
        Assert.True(values.GetArrayLength() > 0, name);
        Assert.Equal(JsonValueKind.String, values[0].ValueKind);
    }

    private static bool IsIntegerEnum(JsonElement schema) =>
        schema.TryGetProperty("type", out var type)
        && type.ValueKind == JsonValueKind.String
        && string.Equals(type.GetString(), "integer", StringComparison.Ordinal);

    private static string ResolveCommittedSwaggerPath()
    {
        var path = Path.Combine(FindBackendRoot(), "swagger.json");
        Assert.True(File.Exists(path), $"Committed OpenAPI not found at {path}");
        return path;
    }

    private static void AssertPinned<TEnum>(TEnum member, int number)
        where TEnum : struct, Enum
    {
        Assert.Equal(number, Convert.ToInt32(member));
        Assert.Equal(member.ToString(), Enum.ToObject(typeof(TEnum), number).ToString());
    }

    private static bool IsCompanySettingsCountrySchema(string columnName) =>
        columnName.Contains("country", StringComparison.OrdinalIgnoreCase)
        || columnName is "vat_regime" or "tax_exempt";

    private static bool IsAllowedVatIdSource(string path) =>
        path.EndsWith($"{Path.DirectorySeparatorChar}VatIdPatterns.cs", StringComparison.Ordinal)
        || path.EndsWith($"{Path.DirectorySeparatorChar}CountryProfileRegistry.cs", StringComparison.Ordinal);

    private static Type[] ConcreteImplementations(Type contract) =>
        contract.Assembly
            .GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && contract.IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

    private static string UpBody(string migrationText)
    {
        var down = migrationText.IndexOf("protected override void Down", StringComparison.Ordinal);
        return down < 0 ? migrationText : migrationText[..down];
    }

    private static IEnumerable<string> EnumerateProductionSources(string backendRoot) =>
        Directory.EnumerateFiles(backendRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains("KasseAPI_Final.Tests", StringComparison.Ordinal));

    private static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path);

    private static string FindBackendRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "KasseAPI_Final.csproj")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
