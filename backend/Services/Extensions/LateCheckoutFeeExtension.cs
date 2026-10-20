using System.Globalization;
using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.VerticalProfiles;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Extensions;

public static class LateCheckoutFeePolicy
{
    public const string SettingsKey = "Extensions.LateCheckoutFee";
    public const string FeeDescription = "Später Check-out";
    public const string DefaultFreeUntil = "11:00";
    public const string DefaultTimeZone = "Europe/Vienna";

    public static readonly string[] CommonTimeZones =
    [
        "Europe/Vienna",
        "Europe/Berlin",
        "Europe/Zurich",
        "Europe/Paris",
        "Europe/Rome",
        "Europe/Amsterdam",
        "Europe/London",
        "Europe/Prague",
        "Europe/Warsaw",
        "Europe/Budapest",
        "UTC",
    ];

    public static LateCheckoutFeeOptionsDto Defaults { get; } = new(
        DefaultFreeUntil,
        0m,
        DefaultTimeZone);

    public static bool TryRead(string? json, out LateCheckoutFeeOptionsDto options)
    {
        options = Defaults;
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var document = JsonDocument.Parse(json);
            return TryRead(document.RootElement, out options);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static bool TryRead(JsonElement options, out LateCheckoutFeeOptionsDto parsed)
    {
        parsed = Defaults;
        if (options.ValueKind != JsonValueKind.Object
            || !options.TryGetProperty("freeUntilLocalTime", out var timeNode)
            || timeNode.ValueKind != JsonValueKind.String
            || !TryParseClock(timeNode.GetString(), out var clock)
            || !options.TryGetProperty("fee", out var feeNode)
            || feeNode.ValueKind != JsonValueKind.Number
            || !feeNode.TryGetDecimal(out var fee)
            || fee < 0
            || !options.TryGetProperty("timeZone", out var zoneNode)
            || zoneNode.ValueKind != JsonValueKind.String
            || !IsKnownZone(zoneNode.GetString()))
            return false;

        parsed = new LateCheckoutFeeOptionsDto(
            clock.ToString("HH:mm", CultureInfo.InvariantCulture),
            decimal.Round(fee, 2, MidpointRounding.AwayFromZero),
            zoneNode.GetString()!);
        return true;
    }

    public static bool IsAfterFreeUntil(DateTimeOffset utcNow, LateCheckoutFeeOptionsDto options)
    {
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(options.TimeZone, out var zone))
            return false;
        if (!TryParseClock(options.FreeUntilLocalTime, out var freeUntil))
            return false;

        var local = TimeZoneInfo.ConvertTime(utcNow, zone);
        return TimeOnly.FromDateTime(local.DateTime) > freeUntil;
    }

    public static bool Same(LateCheckoutFeeOptionsDto left, LateCheckoutFeeOptionsDto right) =>
        left.Fee == right.Fee
        && string.Equals(left.FreeUntilLocalTime, right.FreeUntilLocalTime, StringComparison.Ordinal)
        && string.Equals(left.TimeZone, right.TimeZone, StringComparison.Ordinal);

    private static bool TryParseClock(string? text, out TimeOnly clock)
    {
        clock = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return TimeOnly.TryParseExact(
                text.Trim(),
                ["HH:mm", "H:mm", "HH:mm:ss"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out clock);
    }

    private static bool IsKnownZone(string? timeZone)
    {
        if (string.IsNullOrWhiteSpace(timeZone))
            return false;
        if (!CommonTimeZones.Contains(timeZone, StringComparer.Ordinal))
            return false;
        return TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out _);
    }
}

public sealed record LateCheckoutFeeOptionsDto(
    string FreeUntilLocalTime,
    decimal Fee,
    string TimeZone);

/// <summary>
/// Posts one non-fiscal late-checkout line when a folio closes after the free local time.
/// The extension updates <c>guest_folios.balance</c>. <c>LodgingService</c> saves that
/// change before the close commits. A second close does not add the line again.
/// </summary>
[TenantExtensionHook(TenantExtensionHooks.FolioBeforeClose)]
public sealed class LateCheckoutFeeExtension : ITenantFeatureExtension
{
    private readonly AppDbContext _db;
    private readonly IVerticalProfileService _profiles;
    private readonly TimeProvider _clock;

    public LateCheckoutFeeExtension(
        AppDbContext db,
        IVerticalProfileService profiles,
        TimeProvider? clock = null)
    {
        _db = db;
        _profiles = profiles;
        _clock = clock ?? TimeProvider.System;
    }

    public string FeatureName => TenantExtensionNames.LateCheckoutFee;

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
        if (folio is null)
            return new ExtensionResult(true);

        var alreadyCharged = await _db.GuestFolioItems
            .AnyAsync(
                row => row.FolioId == folio.Id && row.Description == LateCheckoutFeePolicy.FeeDescription,
                ct)
            .ConfigureAwait(false);
        if (alreadyCharged)
            return new ExtensionResult(true);

        var policyJson = await _db.TenantSettings.AsNoTracking()
            .Where(row => row.TenantId == context.TenantId && row.Key == LateCheckoutFeePolicy.SettingsKey)
            .Select(row => row.Value)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (!LateCheckoutFeePolicy.TryRead(policyJson, out var options) || options.Fee <= 0)
            return new ExtensionResult(true);
        if (!LateCheckoutFeePolicy.IsAfterFreeUntil(_clock.GetUtcNow(), options))
            return new ExtensionResult(true);

        var now = _clock.GetUtcNow().UtcDateTime;
        _db.GuestFolioItems.Add(new GuestFolioItem
        {
            FolioId = folio.Id,
            PaymentDetailId = null,
            Description = LateCheckoutFeePolicy.FeeDescription,
            Amount = options.Fee,
            CreatedAtUtc = now,
        });
        folio.Balance = decimal.Round(folio.Balance + options.Fee, 2, MidpointRounding.AwayFromZero);
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
