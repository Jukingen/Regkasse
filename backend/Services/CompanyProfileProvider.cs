using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public interface ICompanyProfileProvider
{
    Task<CompanyProfileOptions> GetCompanyProfileAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Tenant-scoped company profile from <see cref="CompanySettings"/> (not appsettings).
/// </summary>
public sealed class CompanyProfileProvider : ICompanyProfileProvider
{
    private readonly AppDbContext _db;
    private readonly ISettingsTenantResolver _settingsTenantResolver;
    private CompanyProfileOptions? _cached;

    public CompanyProfileProvider(AppDbContext db, ISettingsTenantResolver settingsTenantResolver)
    {
        _db = db;
        _settingsTenantResolver = settingsTenantResolver;
    }

    public async Task<CompanyProfileOptions> GetCompanyProfileAsync(CancellationToken cancellationToken = default)
    {
        if (_cached != null)
            return _cached;

        var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken)
            .ConfigureAwait(false);

        var settings = await _db.CompanySettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        _cached = settings != null
            ? CompanyProfileMapper.FromCompanySettings(settings)
            : CompanyProfileMapper.DefaultProfile();

        return _cached;
    }
}

internal static class CompanyProfileMapper
{
    public static CompanyProfileOptions DefaultProfile() => new()
    {
        CompanyName = "Regkasse Demo GmbH",
        TaxNumber = "ATU12345678",
        Street = "Musterstraße 123",
        ZipCode = "1010",
        City = "Wien",
        Country = "AT",
        PhoneNumber = "+43 1 234 567",
        Email = "info@regkasse.at",
        Website = "www.regkasse.at",
        FooterText = "Es bediente Sie unser Team. Danke für Ihren Einkauf!",
        LogoUrl = "https://via.placeholder.com/150",
        DefaultKassenId = "KASSE-001",
    };

    public static CompanyProfileOptions FromCompanySettings(CompanySettings settings)
    {
        ParseCompanyAddress(settings.CompanyAddress, out var street, out var zip, out var city);
        return new CompanyProfileOptions
        {
            CompanyName = settings.CompanyName,
            TaxNumber = settings.CompanyTaxNumber,
            Street = street,
            ZipCode = zip,
            City = city,
            Country = "AT",
            PhoneNumber = settings.CompanyPhone ?? string.Empty,
            Email = settings.CompanyEmail ?? string.Empty,
            Website = settings.CompanyWebsite ?? string.Empty,
            FooterText = settings.CompanyDescription ?? string.Empty,
            LogoUrl = settings.CompanyLogo ?? string.Empty,
            DefaultKassenId = settings.DefaultTseDeviceId ?? string.Empty,
        };
    }

    public static string FormatCompanyAddress(string street, string zipCode, string city) =>
        $"{street.Trim()}, {zipCode.Trim()} {city.Trim()}".Trim();

    public static void ParseCompanyAddress(string? address, out string street, out string zipCode, out string city)
    {
        street = string.Empty;
        zipCode = string.Empty;
        city = "Wien";
        if (string.IsNullOrWhiteSpace(address))
            return;

        var comma = address.IndexOf(", ", StringComparison.Ordinal);
        if (comma < 0)
        {
            street = address.Trim();
            return;
        }

        street = address[..comma].Trim();
        var rest = address[(comma + 2)..].Trim();
        var space = rest.IndexOf(' ');
        if (space < 0)
        {
            zipCode = rest;
            city = string.Empty;
        }
        else
        {
            zipCode = rest[..space].Trim();
            city = rest[(space + 1)..].Trim();
        }
    }

    /// <summary>Freeze RKSV §8 company header fields on a payment at transaction time.</summary>
    public static void ApplySnapshot(PaymentDetails payment, CompanyProfileOptions profile)
    {
        payment.CompanyName = profile.CompanyName;
        payment.CompanyAddress = FormatCompanyAddress(profile.Street, profile.ZipCode, profile.City);
    }

    /// <summary>Copy company snapshot from original payment (storno/refund); fall back to live profile for legacy rows.</summary>
    public static void CopySnapshotFromOriginal(PaymentDetails target, PaymentDetails original, CompanyProfileOptions fallbackProfile)
    {
        target.CompanyName = !string.IsNullOrWhiteSpace(original.CompanyName)
            ? original.CompanyName
            : fallbackProfile.CompanyName;
        target.CompanyAddress = !string.IsNullOrWhiteSpace(original.CompanyAddress)
            ? original.CompanyAddress
            : FormatCompanyAddress(fallbackProfile.Street, fallbackProfile.ZipCode, fallbackProfile.City);
    }

    /// <summary>Resolve RKSV §8 company block for receipt DTO (snapshot preferred over live settings).</summary>
    public static (string Name, string Address, string TaxNumber) ResolveForDisplay(
        PaymentDetails? payment,
        CompanyProfileOptions liveProfile)
    {
        var name = !string.IsNullOrWhiteSpace(payment?.CompanyName)
            ? payment!.CompanyName!
            : liveProfile.CompanyName;
        var address = !string.IsNullOrWhiteSpace(payment?.CompanyAddress)
            ? payment!.CompanyAddress!
            : FormatCompanyAddress(liveProfile.Street, liveProfile.ZipCode, liveProfile.City);
        var taxNumber = !string.IsNullOrWhiteSpace(payment?.Steuernummer)
            ? payment!.Steuernummer
            : liveProfile.TaxNumber;
        return (name, address, taxNumber);
    }
}
