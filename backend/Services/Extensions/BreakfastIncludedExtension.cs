using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.VerticalProfiles;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Extensions;

public static class BreakfastIncludedPolicy
{
    public const string SettingsKey = "Extensions.BreakfastIncluded";

    /// <summary>
    /// Reads a stored price. A missing or unreadable value is a no-op for the extension.
    /// </summary>
    public static bool TryReadPrice(string? json, out decimal pricePerPersonPerNight)
    {
        pricePerPersonPerNight = 0;
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var document = JsonDocument.Parse(json);
            return TryReadPrice(document.RootElement, out pricePerPersonPerNight);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static bool TryReadPrice(JsonElement options, out decimal pricePerPersonPerNight)
    {
        pricePerPersonPerNight = 0;
        if (options.ValueKind != JsonValueKind.Object
            || !options.TryGetProperty("pricePerPersonPerNight", out var node)
            || node.ValueKind != JsonValueKind.Number
            || !node.TryGetDecimal(out var value)
            || value < 0)
            return false;

        pricePerPersonPerNight = decimal.Round(value, 2, MidpointRounding.AwayFromZero);
        return true;
    }

    /// <summary>
    /// Whole nights between check-in and check-out. A missing check-out or a same-day stay is one night.
    /// </summary>
    public static int Nights(DateTime checkIn, DateTime? checkOut)
    {
        if (checkOut is not DateTime end)
            return 1;

        return Math.Max(1, (end - checkIn).Days);
    }
}

public sealed record BreakfastIncludedOptionsDto(decimal PricePerPersonPerNight);

/// <summary>
/// Posts one non-fiscal breakfast line when a new folio's notes mention breakfast.
/// The extension updates <c>guest_folios.balance</c>. <c>LodgingService</c> saves that
/// change in the open folio transaction. There is no guest-count column, so persons is 1.
/// </summary>
[TenantExtensionHook(TenantExtensionHooks.FolioOpened)]
public sealed class BreakfastIncludedExtension : ITenantFeatureExtension
{
    private readonly AppDbContext _db;
    private readonly IVerticalProfileService _profiles;

    public BreakfastIncludedExtension(AppDbContext db, IVerticalProfileService profiles)
    {
        _db = db;
        _profiles = profiles;
    }

    public string FeatureName => TenantExtensionNames.BreakfastIncluded;

    public bool AppliesTo(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            return false;

        var profile = _profiles
            .GetForAdminTenantAsync(tenantId)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();
        return string.Equals(profile?.ProfileId, VerticalProfileIds.Beherbergung, StringComparison.Ordinal);
    }

    public async Task<ExtensionResult> ExecuteAsync(ExtensionContext context, CancellationToken ct)
    {
        if (!TryReadGuid(context.Payload, "folioId", out var folioId))
            return new ExtensionResult(true);

        var folio = await _db.GuestFolios
            .FirstOrDefaultAsync(row => row.Id == folioId && row.TenantId == context.TenantId, ct)
            .ConfigureAwait(false);
        if (folio is null || folio.Notes is null
            || !folio.Notes.Contains("breakfast", StringComparison.OrdinalIgnoreCase))
            return new ExtensionResult(true);

        var policyJson = await _db.TenantSettings.AsNoTracking()
            .Where(row => row.TenantId == context.TenantId && row.Key == BreakfastIncludedPolicy.SettingsKey)
            .Select(row => row.Value)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (!BreakfastIncludedPolicy.TryReadPrice(policyJson, out var price) || price <= 0)
            return new ExtensionResult(true);

        var nights = BreakfastIncludedPolicy.Nights(folio.CheckIn, folio.CheckOut);
        const int persons = 1;
        var amount = decimal.Round(nights * persons * price, 2, MidpointRounding.AwayFromZero);
        var now = DateTime.UtcNow;
        _db.GuestFolioItems.Add(new GuestFolioItem
        {
            FolioId = folio.Id,
            PaymentDetailId = null,
            Description = $"Frühstück ({nights} Nächte)",
            Amount = amount,
            CreatedAtUtc = now,
        });
        folio.Balance = decimal.Round(folio.Balance + amount, 2, MidpointRounding.AwayFromZero);
        folio.UpdatedAtUtc = now;
        return new ExtensionResult(true);
    }

    private static bool TryReadGuid(IReadOnlyDictionary<string, object?>? payload, string key, out Guid id)
    {
        id = Guid.Empty;
        if (payload is null || !payload.TryGetValue(key, out var raw) || raw is null)
            return false;
        switch (raw)
        {
            case Guid guid:
                id = guid;
                return id != Guid.Empty;
            case string text when Guid.TryParse(text, out var parsed):
                id = parsed;
                return id != Guid.Empty;
            default:
                return false;
        }
    }
}
