using System.Reflection;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.FeatureFlags;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FeatureFlagNamesTests
{
    [Fact]
    public void DeclaredNames_AreUnique_AndReservedStayOutOfResolution()
    {
        var constants = typeof(FeatureFlagNames)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToArray();

        var declared = FeatureFlagNames.All.Concat(FeatureFlagNames.Reserved).ToArray();

        Assert.Equal(constants.Length, constants.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            constants.OrderBy(name => name, StringComparer.Ordinal),
            declared.OrderBy(name => name, StringComparer.Ordinal));
        Assert.DoesNotContain(FeatureFlagNames.EInvoicingPeppol, FeatureFlagNames.All);
        Assert.DoesNotContain(FeatureFlagNames.Reserved, name => FeatureFlagNames.All.Contains(name));
        Assert.Equal([FeatureFlagNames.EInvoicingPeppol], FeatureFlagNames.Reserved);
        Assert.False(CountryFeatureFlagDefaults.TryGet(
            FeatureFlagNames.EInvoicingPeppol,
            new CountryProfileRegistry().Get(CountryProfileCodes.EuDefault),
            out _));
    }

    [Fact]
    public void Peppol_IsReserved_AndCannotBeResolvedOn()
    {
        Assert.Equal([FeatureFlagNames.EInvoicingPeppol], FeatureFlagNames.Reserved);
        Assert.DoesNotContain(FeatureFlagNames.EInvoicingPeppol, FeatureFlagNames.All);

        var factory = new Mock<IDbContextFactory<AppDbContext>>(MockBehavior.Strict);
        var service = new FeatureFlagService(
            factory.Object,
            Options.Create(new FeatureFlagsOptions()).ToMonitor(),
            new MemoryCache(new MemoryCacheOptions()),
            Mock.Of<IAuditLogService>(),
            NullLogger<FeatureFlagService>.Instance,
            new CountryProfileRegistry());

        var resolved = service.Resolve(
            FeatureFlagNames.EInvoicingPeppol,
            Guid.NewGuid(),
            tenantRow: null,
            globalRow: null,
            profile: null,
            loadFromDb: true);

        Assert.False(resolved.Enabled);
        Assert.Equal(FeatureFlagSources.Reserved, resolved.Source);
        Assert.False(service.IsEnabled(FeatureFlagNames.EInvoicingPeppol, Guid.NewGuid().ToString("D")));
        factory.VerifyNoOtherCalls();

        var optionNames = typeof(FeatureFlagsOptions)
            .GetProperties()
            .Concat(typeof(EInvoicingFeatureFlagsOptions).GetProperties())
            .Concat(typeof(FiscalFeatureFlagsOptions).GetProperties())
            .Select(property => property.Name);
        Assert.DoesNotContain(optionNames, name => name.Contains("Peppol", StringComparison.OrdinalIgnoreCase));

        var registry = new CountryProfileRegistry();
        foreach (var code in new[]
        {
            CountryProfileCodes.Austria,
            CountryProfileCodes.Germany,
            CountryProfileCodes.Switzerland,
            CountryProfileCodes.EuDefault,
        })
        {
            var found = CountryFeatureFlagDefaults.TryGet(
                FeatureFlagNames.EInvoicingPeppol,
                registry.Get(code),
                out var enabled);
            Assert.False(found);
            Assert.False(enabled);
        }
    }

    [Fact]
    public void FiscalRksvAt_RemainsLockedOn_ForAustria()
    {
        var austria = new CountryProfileRegistry().Get(CountryProfileCodes.Austria);

        Assert.True(CountryFeatureFlagDefaults.IsRksvAtLocked(FeatureFlagNames.FiscalRksvAt, austria));
        Assert.True(CountryFeatureFlagDefaults.TryGet(FeatureFlagNames.FiscalRksvAt, austria, out var enabled));
        Assert.True(enabled);

        var germany = new CountryProfileRegistry().Get(CountryProfileCodes.Germany);
        Assert.False(CountryFeatureFlagDefaults.IsRksvAtLocked(FeatureFlagNames.FiscalRksvAt, germany));
        Assert.True(CountryFeatureFlagDefaults.TryGet(FeatureFlagNames.FiscalRksvAt, germany, out var deRksv));
        Assert.False(deRksv);
    }
}
