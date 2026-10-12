using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Extensions;

/// <summary>
/// Applies the tenant's active hair-salon campaigns to cart line gross totals.
/// The payment service signs whatever gross this hook returns. Disabled tenants
/// never reach <see cref="ExecuteAsync"/>.
/// </summary>
[TenantExtensionHook(TenantExtensionHooks.PaymentBeforeSign)]
public sealed class HairSalonCampaignExtension : ITenantFeatureExtension
{
    private readonly AppDbContext _db;
    private readonly IAuditLogService _audit;
    private readonly ILogger<HairSalonCampaignExtension> _logger;

    public HairSalonCampaignExtension(
        AppDbContext db,
        IAuditLogService audit,
        ILogger<HairSalonCampaignExtension> logger)
    {
        _db = db;
        _audit = audit;
        _logger = logger;
    }

    public string FeatureName => TenantExtensionNames.HairSalonCampaign;

    public bool AppliesTo(Guid tenantId) => tenantId != Guid.Empty;

    public async Task<ExtensionResult> ExecuteAsync(ExtensionContext context, CancellationToken ct)
    {
        var lines = ExtensionCart.ReadLines(context.Payload);
        if (lines.Count == 0)
            return new ExtensionResult(true);

        var now = DateTime.UtcNow;
        var campaigns = await _db.HairSalonCampaigns
            .AsNoTracking()
            .Where(row => row.TenantId == context.TenantId
                && row.IsActive
                && row.ValidFromUtc <= now
                && now < row.ValidUntilUtc
                && row.DiscountPercent > 0
                && row.DiscountPercent <= 100)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (campaigns.Count == 0)
            return new ExtensionResult(true);

        var applied = new List<Guid>();
        var adjusted = new List<ExtensionCartLine>(lines.Count);
        var changed = false;
        foreach (var line in lines)
        {
            var percent = BestPercent(campaigns, line.ProductId, applied);
            var gross = ExtensionCart.DiscountGross(line.GrossTotal, percent);
            if (gross != line.GrossTotal)
                changed = true;
            adjusted.Add(line with { GrossTotal = gross });
        }

        if (!changed)
            return new ExtensionResult(true);

        try
        {
            await _audit.LogSystemOperationAsync(
                    action: "HAIR_SALON_CAMPAIGN_APPLIED",
                    entityType: "HairSalonCampaign",
                    userId: "extension",
                    userRole: "system",
                    description: "Hair salon campaign adjusted cart gross before signing",
                    actionType: AuditEventType.HairSalonCampaignApplied,
                    tenantId: context.TenantId,
                    newValues: new
                    {
                        CampaignIds = applied.Distinct().ToArray(),
                        LineCount = adjusted.Count,
                    })
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Hair salon campaign audit write failed for tenant {TenantId}", context.TenantId);
        }

        return new ExtensionResult(
            true,
            ModifiedPayload: ExtensionCart.Write(adjusted, applied.Distinct().ToArray()));
    }

    private static decimal BestPercent(
        IReadOnlyList<HairSalonCampaign> campaigns,
        Guid productId,
        List<Guid> applied)
    {
        decimal best = 0;
        Guid? winner = null;
        foreach (var campaign in campaigns)
        {
            var products = campaign.AppliesToProductIds;
            var matches = products is null || products.Count == 0 || products.Contains(productId);
            if (!matches || campaign.DiscountPercent <= best)
                continue;
            best = campaign.DiscountPercent;
            winner = campaign.Id;
        }

        if (winner is Guid id)
            applied.Add(id);
        return best;
    }
}
