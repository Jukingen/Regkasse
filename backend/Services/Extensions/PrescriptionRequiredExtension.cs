using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Extensions;

/// <summary>
/// Rejects a sale when a cart line's product requires a prescription and the
/// payment has none. Runs only when the tenant enables <c>prescription_required</c>.
/// </summary>
[TenantExtensionHook(TenantExtensionHooks.PaymentBeforeSign)]
public sealed class PrescriptionRequiredExtension : ITenantFeatureExtension
{
    public const string HasPrescriptionKey = "hasPrescription";

    private readonly AppDbContext _db;
    private readonly ILogger<PrescriptionRequiredExtension> _logger;

    public PrescriptionRequiredExtension(AppDbContext db, ILogger<PrescriptionRequiredExtension> logger)
    {
        _db = db;
        _logger = logger;
    }

    public string FeatureName => TenantExtensionNames.PrescriptionRequired;

    public bool AppliesTo(Guid tenantId) => tenantId != Guid.Empty;

    public async Task<ExtensionResult> ExecuteAsync(ExtensionContext context, CancellationToken ct)
    {
        var lines = ExtensionCart.ReadLines(context.Payload);
        if (lines.Count == 0)
            return new ExtensionResult(true);

        var productIds = lines
            .Select(line => line.ProductId)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        if (productIds.Count == 0)
            return new ExtensionResult(true);

        var requires = await _db.Products
            .AsNoTracking()
            .AnyAsync(
                product => product.TenantId == context.TenantId
                    && productIds.Contains(product.Id)
                    && product.RequiresPrescription,
                ct)
            .ConfigureAwait(false);
        if (!requires)
            return new ExtensionResult(true);

        if (HasPrescription(context.Payload))
            return new ExtensionResult(true);

        _logger.LogInformation(
            "Prescription required and missing. TenantId={TenantId} ProductCount={ProductCount}",
            context.TenantId,
            productIds.Count);
        return new ExtensionResult(false, "A prescription is required for one or more cart items.");
    }

    internal static bool HasPrescription(IReadOnlyDictionary<string, object?>? payload)
    {
        if (payload is null || !payload.TryGetValue(HasPrescriptionKey, out var raw) || raw is null)
            return false;
        return raw switch
        {
            bool flag => flag,
            string text => bool.TryParse(text, out var parsed) && parsed,
            _ => false,
        };
    }
}
