using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.VerticalProfiles;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Extensions;

public static class MinibarAutoChargePolicy
{
    public const string SettingsKey = "Extensions.MinibarAutoCharge";
    public const string RejectionCode = "MINIBAR_SETTLEMENT_REQUIRED";

    /// <summary>Absent or unreadable policy blocks check-out while minibar rows are still pending.</summary>
    public static bool RequireSettlementBeforeClose(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return true;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("requireSettlementBeforeClose", out var flag)
                && flag.ValueKind is JsonValueKind.True or JsonValueKind.False)
                return flag.GetBoolean();
        }
        catch (JsonException)
        {
            return true;
        }

        return true;
    }
}

public sealed record MinibarAutoChargeOptionsDto(bool RequireSettlementBeforeClose);

/// <summary>
/// Posts pending room minibar rows onto the guest folio when it closes.
/// Charges stay non-fiscal: no payment row and no TSE signature.
/// </summary>
[TenantExtensionHook(TenantExtensionHooks.FolioBeforeClose)]
public sealed class MinibarAutoChargeExtension : ITenantFeatureExtension
{
    private readonly AppDbContext _db;
    private readonly IVerticalProfileService _profiles;
    private readonly IAuditLogService _audit;

    public MinibarAutoChargeExtension(
        AppDbContext db,
        IVerticalProfileService profiles,
        IAuditLogService audit)
    {
        _db = db;
        _profiles = profiles;
        _audit = audit;
    }

    public string FeatureName => TenantExtensionNames.MinibarAutoCharge;

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

        var pending = await _db.RoomMinibarItems
            .Where(row => row.TenantId == context.TenantId
                && row.RoomId == folio.RoomId
                && row.Status == RoomMinibarItemStatus.Pending)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        if (pending.Count == 0)
            return new ExtensionResult(true);

        var policyJson = await _db.TenantSettings.AsNoTracking()
            .Where(row => row.TenantId == context.TenantId && row.Key == MinibarAutoChargePolicy.SettingsKey)
            .Select(row => row.Value)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (MinibarAutoChargePolicy.RequireSettlementBeforeClose(policyJson))
            return new ExtensionResult(false, MinibarAutoChargePolicy.RejectionCode);

        var now = DateTime.UtcNow;
        foreach (var row in pending)
        {
            var amount = decimal.Round(row.Quantity * row.UnitPrice, 2, MidpointRounding.AwayFromZero);
            _db.GuestFolioItems.Add(new GuestFolioItem
            {
                FolioId = folio.Id,
                PaymentDetailId = null,
                Description = row.Description,
                Amount = amount,
                CreatedAtUtc = now,
            });
            row.Status = RoomMinibarItemStatus.Charged;
            row.ChargedFolioId = folio.Id;
            row.ChargedAtUtc = now;
            row.UpdatedAtUtc = now;
            folio.Balance = decimal.Round(folio.Balance + amount, 2, MidpointRounding.AwayFromZero);

            await _audit.LogSystemOperationAsync(
                    action: "FOLIO_CHARGED",
                    entityType: "GuestFolio",
                    userId: "system",
                    userRole: "Cashier",
                    description: $"Folio charge {amount:0.00} ({row.Description}). No payment row and no TSE receipt.",
                    status: AuditLogStatus.Success,
                    correlationIdOverride: Guid.NewGuid().ToString("N"),
                    actionType: AuditEventType.FolioCharged,
                    entityId: folio.Id,
                    tenantId: context.TenantId)
                .ConfigureAwait(false);
        }

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
