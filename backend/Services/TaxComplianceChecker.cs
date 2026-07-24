using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

/// <summary>
/// Tenant product MwSt catalog compliance against the current Austrian regulation bands.
/// </summary>
public sealed class TaxComplianceChecker : ITaxComplianceChecker
{
    private const int SampleLimit = 10;

    private readonly AppDbContext _db;
    private readonly ITaxRegulationService _regulation;
    private readonly ILogger<TaxComplianceChecker> _logger;

    public TaxComplianceChecker(
        AppDbContext db,
        ITaxRegulationService regulation,
        ILogger<TaxComplianceChecker> logger)
    {
        _db = db;
        _regulation = regulation;
        _logger = logger;
    }

    public async Task<ComplianceReport> CheckComplianceAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));

        var regulation = await _regulation.GetCurrentRegulationAsync(cancellationToken)
            .ConfigureAwait(false);
        var allowedRates = TaxRegulationService.GetDistinctAllowedRates(regulation);

        // Left join: required Product→TaxGroup FK would otherwise INNER JOIN and hide
        // orphaned / Guid.Empty TaxGroupId rows from the compliance scan.
        var products = await (
                from p in _db.Products.AsNoTracking()
                where p.TenantId == tenantId && p.IsActive
                join g in _db.TaxGroups.AsNoTracking() on p.TaxGroupId equals g.Id into gj
                from g in gj.DefaultIfEmpty()
                select new ProductTaxSnapshot(
                    p.Id,
                    p.TaxGroupId,
                    p.TaxRate,
                    g != null ? (decimal?)g.Rate : null,
                    g != null ? (TaxGroupType?)g.GroupType : null,
                    g != null && g.IsActive))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var totalProducts = products.Count;
        var nonCompliantIds = new HashSet<Guid>();
        var issues = new List<ComplianceIssue>();

        // 1. Missing / empty tax group assignment
        var missingGroup = products
            .Where(p => p.TaxGroupId == Guid.Empty || p.GroupRate is null)
            .ToList();
        if (missingGroup.Count > 0)
        {
            AddIssue(
                issues,
                nonCompliantIds,
                missingGroup,
                severity: "Critical",
                code: "MISSING_TAX_GROUP",
                message: $"{missingGroup.Count} products without a tax group assignment.",
                action: "Assign an Austrian MwSt tax group to each affected product.");
        }

        // 2. Inactive tax group still referenced
        var inactiveGroup = products
            .Where(p => p.TaxGroupId != Guid.Empty && p.GroupRate is not null && !p.GroupIsActive)
            .ToList();
        if (inactiveGroup.Count > 0)
        {
            AddIssue(
                issues,
                nonCompliantIds,
                inactiveGroup,
                severity: "Warning",
                code: "INACTIVE_TAX_GROUP",
                message: $"{inactiveGroup.Count} products reference an inactive tax group.",
                action: "Reassign these products to an active tax group.");
        }

        // 3. Invalid rates (group rate or stamped product rate outside regulation)
        var invalidRates = products
            .Where(p =>
            {
                if (p.GroupRate is null) return false;
                var groupRate = decimal.Round(p.GroupRate.Value, 2, MidpointRounding.AwayFromZero);
                var productRate = decimal.Round(p.ProductTaxRate, 2, MidpointRounding.AwayFromZero);
                return !allowedRates.Contains(groupRate) || !allowedRates.Contains(productRate);
            })
            .ToList();
        if (invalidRates.Count > 0)
        {
            AddIssue(
                issues,
                nonCompliantIds,
                invalidRates,
                severity: "Critical",
                code: "INVALID_TAX_RATE",
                message: $"{invalidRates.Count} products use a tax rate outside current Austrian MwSt bands.",
                action: "Update the tax group or product rate to 0%, 4.9%, 10%, 13%, or 20%.");
        }

        // 4. Group type vs expected regulation rate mismatch (outdated catalog row)
        var outdatedTypeRate = products
            .Where(p =>
            {
                if (p.GroupType is not TaxGroupType type || p.GroupRate is null)
                    return false;
                var expected = ExpectedRateForType(regulation, type);
                if (expected is null) return false;
                var actual = decimal.Round(p.GroupRate.Value, 2, MidpointRounding.AwayFromZero);
                return actual != expected.Value;
            })
            .ToList();
        if (outdatedTypeRate.Count > 0)
        {
            AddIssue(
                issues,
                nonCompliantIds,
                outdatedTypeRate,
                severity: "Warning",
                code: "OUTDATED_TAX_RATE",
                message: $"{outdatedTypeRate.Count} products use a tax group whose rate does not match its Austrian group type.",
                action: "Align the tax group rate with the current regulation for that group type, or correct the group type.");
        }

        // 5. Product.TaxRate desynced from TaxGroup.Rate
        var desynced = products
            .Where(p =>
            {
                if (p.GroupRate is null) return false;
                var groupRate = decimal.Round(p.GroupRate.Value, 2, MidpointRounding.AwayFromZero);
                var productRate = decimal.Round(p.ProductTaxRate, 2, MidpointRounding.AwayFromZero);
                return groupRate != productRate;
            })
            .ToList();
        if (desynced.Count > 0)
        {
            AddIssue(
                issues,
                nonCompliantIds,
                desynced,
                severity: "Warning",
                code: "PRODUCT_RATE_DESYNC",
                message: $"{desynced.Count} products have a stamped tax rate that differs from their tax group rate.",
                action: "Re-save the products (or run a catalog sync) so TaxRate matches the assigned tax group.");
        }

        var nonCompliant = nonCompliantIds.Count;
        var report = new ComplianceReport
        {
            IsCompliant = issues.Count == 0,
            Issues = issues,
            TotalProducts = totalProducts,
            CompliantProducts = Math.Max(0, totalProducts - nonCompliant),
            NonCompliantProducts = nonCompliant,
            CheckedAtUtc = DateTime.UtcNow,
        };

        _logger.LogInformation(
            "Tax compliance for tenant {TenantId}: compliant={Compliant}/{Total}, issues={IssueCount}",
            tenantId,
            report.CompliantProducts,
            report.TotalProducts,
            report.Issues.Count);

        return report;
    }

    private static decimal? ExpectedRateForType(TaxRegulation regulation, TaxGroupType type) =>
        type switch
        {
            TaxGroupType.Standard => decimal.Round(regulation.StandardRate, 2, MidpointRounding.AwayFromZero),
            TaxGroupType.Reduced => decimal.Round(regulation.ReducedRate, 2, MidpointRounding.AwayFromZero),
            TaxGroupType.ReducedNew => decimal.Round(regulation.ReducedNewRate, 2, MidpointRounding.AwayFromZero),
            TaxGroupType.Middle => decimal.Round(regulation.MiddleRate, 2, MidpointRounding.AwayFromZero),
            TaxGroupType.Zero => decimal.Round(regulation.ZeroRate, 2, MidpointRounding.AwayFromZero),
            _ => null,
        };

    private static void AddIssue(
        List<ComplianceIssue> issues,
        HashSet<Guid> nonCompliantIds,
        List<ProductTaxSnapshot> affected,
        string severity,
        string code,
        string message,
        string action)
    {
        foreach (var p in affected)
            nonCompliantIds.Add(p.Id);

        issues.Add(new ComplianceIssue
        {
            Severity = severity,
            Code = code,
            Message = message,
            Action = action,
            AffectedCount = affected.Count,
            SampleProductIds = affected.Take(SampleLimit).Select(p => p.Id).ToArray(),
        });
    }

    private sealed record ProductTaxSnapshot(
        Guid Id,
        Guid TaxGroupId,
        decimal ProductTaxRate,
        decimal? GroupRate,
        TaxGroupType? GroupType,
        bool GroupIsActive);
}
