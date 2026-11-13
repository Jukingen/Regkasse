using System.Text.Json;
using System.Text.Json.Nodes;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Caching;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.VerticalProfiles;

public interface IVerticalProfileService
{
    Task<IReadOnlyList<VerticalProfileDto>> ListActiveAsync(CancellationToken cancellationToken = default);

    Task<VerticalProfileDto?> GetActiveAsync(
        string profileId,
        CancellationToken cancellationToken = default);

    Task<EffectiveVerticalProfileDto?> GetForAdminTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<EffectiveVerticalProfileDto?> GetForCurrentTenantAsync(
        CancellationToken cancellationToken = default);

    Task<(EffectiveVerticalProfileDto? Profile, VerticalProfileUpdateError? Error)> UpdateTenantAsync(
        Guid tenantId,
        UpdateTenantVerticalProfileRequest request,
        string actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts historical rows the target profile would hide. Does not read or write fiscal documents.
    /// </summary>
    Task<(TenantProfileImpactDto? Impact, VerticalProfileUpdateError? Error)> GetProfileImpactAsync(
        Guid tenantId,
        string targetProfileId,
        CancellationToken cancellationToken = default);
}

public sealed class VerticalProfileService : IVerticalProfileService
{
    private const int MaxOverridesJsonLength = 32 * 1024;
    private static readonly IReadOnlySet<string> AllowedOverrideProperties = new HashSet<string>(
        ["posFeatures", "requiredFields", "optionalFields", "posLayout"],
        StringComparer.Ordinal);

    private readonly AppDbContext _db;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly IAuditLogService _auditLog;
    private readonly ILogger<VerticalProfileService> _logger;
    private readonly IVerticalProfileRegistry _registry;
    private readonly ICacheService _cache;

    public VerticalProfileService(
        AppDbContext db,
        ICurrentTenantAccessor tenantAccessor,
        IAuditLogService auditLog,
        ILogger<VerticalProfileService> logger,
        IVerticalProfileRegistry registry,
        ICacheService cache)
    {
        _db = db;
        _tenantAccessor = tenantAccessor;
        _auditLog = auditLog;
        _logger = logger;
        _registry = registry;
        _cache = cache;
    }

    public async Task<IReadOnlyList<VerticalProfileDto>> ListActiveAsync(
        CancellationToken cancellationToken = default)
    {
        var profiles = await _registry
            .ListActiveAsync(includeTenantCounts: true, cancellationToken)
            .ConfigureAwait(false);

        return profiles.Select(VerticalProfileCatalogService.ToDto).ToArray();
    }

    public async Task<VerticalProfileDto?> GetActiveAsync(
        string profileId,
        CancellationToken cancellationToken = default)
    {
        if (!VerticalProfileJson.TryNormalizeId(profileId, out var normalizedId))
            return null;

        var profiles = await _registry
            .ListActiveAsync(includeTenantCounts: true, cancellationToken)
            .ConfigureAwait(false);
        var profile = profiles.FirstOrDefault(candidate => candidate.Id == normalizedId);
        return profile is null ? null : VerticalProfileCatalogService.ToDto(profile);
    }

    public async Task<EffectiveVerticalProfileDto?> GetForAdminTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenantExists = await _db.Tenants
            .AsNoTracking()
            .AnyAsync(tenant => tenant.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (!tenantExists)
            return null;

        var settings = await _db.CompanySettings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (settings is null)
            return null;

        var overridesJson = await _db.TenantVerticalOverrides
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(row => row.TenantId == tenantId)
            .Select(row => row.OverridesJson)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return await ResolveEffectiveAsync(
                settings.VerticalProfileId,
                overridesJson,
                settings.TaxiTariffPerKm,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<EffectiveVerticalProfileDto?> GetForCurrentTenantAsync(
        CancellationToken cancellationToken = default)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return null;

        var settings = await _db.CompanySettings
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (settings is null)
            return null;

        var overridesJson = await _db.TenantVerticalOverrides
            .AsNoTracking()
            .Where(row => row.TenantId == tenantId)
            .Select(row => row.OverridesJson)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return await ResolveEffectiveAsync(
                settings.VerticalProfileId,
                overridesJson,
                settings.TaxiTariffPerKm,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<(EffectiveVerticalProfileDto? Profile, VerticalProfileUpdateError? Error)> UpdateTenantAsync(
        Guid tenantId,
        UpdateTenantVerticalProfileRequest request,
        string actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tenantExists = await _db.Tenants
            .AnyAsync(tenant => tenant.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (!tenantExists)
        {
            return (null, new VerticalProfileUpdateError(
                VerticalProfileUpdateErrorCodes.TenantNotFound,
                "Tenant not found."));
        }

        var profileId = request.ProfileId?.Trim().ToLowerInvariant() ?? string.Empty;
        var profile = await _registry.FindActiveAsync(profileId, cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return (null, new VerticalProfileUpdateError(
                VerticalProfileUpdateErrorCodes.ProfileNotFound,
                "Vertical profile not found."));
        }

        string normalizedOverrides;
        try
        {
            normalizedOverrides = NormalizeAndValidateOverrides(request.Overrides);
        }
        catch (ArgumentException ex)
        {
            return (null, new VerticalProfileUpdateError(
                VerticalProfileUpdateErrorCodes.InvalidOverrides,
                ex.Message));
        }

        var settings = await _db.CompanySettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(row => row.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (settings is null)
        {
            return (null, new VerticalProfileUpdateError(
                VerticalProfileUpdateErrorCodes.CompanySettingsMissing,
                "Company settings not found."));
        }

        var overrideRow = await _db.TenantVerticalOverrides
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(row => row.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        var oldProfileId = settings.VerticalProfileId;
        var oldOverridesJson = overrideRow?.OverridesJson ?? "{}";
        var now = DateTime.UtcNow;
        // Assignment only. Customers, products, payments, appointments, rooms,
        // folios, tickets, and IMEIs stay as historical rows.
        settings.VerticalProfileId = profile.Id;
        settings.TaxiTariffPerKm = request.TaxiTariffPerKm;
        settings.UpdatedAt = now;
        settings.UpdatedBy = actorUserId;

        if (overrideRow is null)
        {
            overrideRow = new TenantVerticalOverride
            {
                TenantId = tenantId,
                OverridesJson = normalizedOverrides,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };
            _db.TenantVerticalOverrides.Add(overrideRow);
        }
        else
        {
            overrideRow.OverridesJson = normalizedOverrides;
            overrideRow.UpdatedAtUtc = now;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _auditLog.LogSystemOperationAsync(
                action: "TENANT_VERTICAL_PROFILE_CHANGED",
                entityType: "Tenant",
                userId: actorUserId,
                userRole: actorRole,
                description: $"Tenant vertical profile changed from {oldProfileId ?? "(unset)"} to {profile.Id}",
                status: AuditLogStatus.Success,
                correlationIdOverride: Guid.NewGuid().ToString("N"),
                actionType: AuditEventType.TenantVerticalProfileChanged,
                entityId: tenantId,
                tenantId: tenantId,
                oldValues: new
                {
                    profileId = oldProfileId,
                    overrides = ParseElement(oldOverridesJson),
                },
                newValues: new
                {
                    profileId = profile.Id,
                    overrides = ParseElement(normalizedOverrides),
                })
            .ConfigureAwait(false);

        await _cache.RemoveAsync(CacheKeys.VerticalProfileCatalog, cancellationToken).ConfigureAwait(false);
        await CacheInvalidationHelper.InvalidateTenantCacheAsync(
                _cache,
                tenantId,
                cancellationToken,
                CacheKeys.VerticalProfileEffective)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Vertical profile {ProfileId} assigned to tenant {TenantId}",
            profile.Id,
            tenantId);

        return (BuildEffective(profile, normalizedOverrides, settings.TaxiTariffPerKm), null);
    }

    public async Task<(TenantProfileImpactDto? Impact, VerticalProfileUpdateError? Error)> GetProfileImpactAsync(
        Guid tenantId,
        string targetProfileId,
        CancellationToken cancellationToken = default)
    {
        var normalizedTarget = targetProfileId?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalizedTarget.Length == 0)
        {
            return (null, new VerticalProfileUpdateError(
                VerticalProfileUpdateErrorCodes.ProfileNotFound,
                "Vertical profile not found."));
        }

        var tenantExists = await _db.Tenants
            .AnyAsync(tenant => tenant.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (!tenantExists)
        {
            return (null, new VerticalProfileUpdateError(
                VerticalProfileUpdateErrorCodes.TenantNotFound,
                "Tenant not found."));
        }

        var settings = await _db.CompanySettings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (settings is null)
        {
            return (null, new VerticalProfileUpdateError(
                VerticalProfileUpdateErrorCodes.CompanySettingsMissing,
                "Company settings not found."));
        }

        var target = await _registry.FindActiveAsync(normalizedTarget, cancellationToken).ConfigureAwait(false);
        if (target is null)
        {
            return (null, new VerticalProfileUpdateError(
                VerticalProfileUpdateErrorCodes.ProfileNotFound,
                "Vertical profile not found."));
        }

        var current = await ResolveEffectiveAsync(
                settings.VerticalProfileId,
                null,
                settings.TaxiTariffPerKm,
                cancellationToken)
            .ConfigureAwait(false);
        var effectiveTarget = BuildEffective(target, "{}", settings.TaxiTariffPerKm);
        var counts = await CountImpactAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return (new TenantProfileImpactDto(
            current?.ProfileId ?? VerticalProfileIds.Gastronomy,
            effectiveTarget.ProfileId,
            counts,
            BuildWarnings(effectiveTarget, counts)), null);
    }

    private async Task<TenantProfileImpactCounts> CountImpactAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var registerIds = _db.CashRegisters
            .IgnoreQueryFilters()
            .Where(row => row.TenantId == tenantId)
            .Select(row => row.Id);

        var customersWithPetData = await _db.Customers
            .IgnoreQueryFilters()
            .CountAsync(
                row => row.TenantId == tenantId && row.PetData != null,
                cancellationToken)
            .ConfigureAwait(false);
        var paymentsWithPrescription = await _db.PaymentDetails
            .IgnoreQueryFilters()
            .CountAsync(
                row => registerIds.Contains(row.CashRegisterId)
                    && row.PrescriptionReference != null
                    && row.PrescriptionReference != "",
                cancellationToken)
            .ConfigureAwait(false);
        var paymentsWithRoute = await _db.PaymentDetails
            .IgnoreQueryFilters()
            .CountAsync(
                row => registerIds.Contains(row.CashRegisterId)
                    && row.RouteFrom != null
                    && row.RouteFrom != "",
                cancellationToken)
            .ConfigureAwait(false);
        var soldImeis = await _db.ProductImeis
            .IgnoreQueryFilters()
            .CountAsync(
                row => row.TenantId == tenantId && row.Status == ProductImeiStatus.Sold,
                cancellationToken)
            .ConfigureAwait(false);
        var appointments = await _db.Appointments
            .IgnoreQueryFilters()
            .CountAsync(row => row.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        var rooms = await _db.Rooms
            .IgnoreQueryFilters()
            .CountAsync(row => row.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        var folios = await _db.GuestFolios
            .IgnoreQueryFilters()
            .CountAsync(row => row.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        var tickets = await _db.TicketRedemptions
            .IgnoreQueryFilters()
            .CountAsync(row => row.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        return new TenantProfileImpactCounts(
            customersWithPetData,
            paymentsWithPrescription,
            paymentsWithRoute,
            soldImeis,
            appointments,
            rooms,
            folios,
            tickets);
    }

    private static IReadOnlyList<TenantProfileImpactWarning> BuildWarnings(
        EffectiveVerticalProfileDto target,
        TenantProfileImpactCounts counts)
    {
        var warnings = new List<TenantProfileImpactWarning>();
        void Add(bool hidden, string code, int count)
        {
            if (hidden && count > 0)
                warnings.Add(new TenantProfileImpactWarning(code, count));
        }

        var hidesPatients = !target.HasPosFeature("patientRecord");
        Add(hidesPatients, TenantProfileImpactCodes.CustomersWithPetData, counts.CustomersWithPetData);
        Add(hidesPatients, TenantProfileImpactCodes.PaymentsWithPrescriptionReference, counts.PaymentsWithPrescriptionReference);
        Add(!VerticalProfileGuard.IsTaxiSurface(target), TenantProfileImpactCodes.PaymentsWithRouteFrom, counts.PaymentsWithRouteFrom);
        Add(!target.HasPosFeature("imeiTracking"), TenantProfileImpactCodes.SoldImeis, counts.SoldImeis);
        Add(!target.HasPosFeature("appointment"), TenantProfileImpactCodes.Appointments, counts.Appointments);
        var hidesLodging = !string.Equals(target.ProfileId, VerticalProfileIds.Beherbergung, StringComparison.Ordinal);
        Add(hidesLodging, TenantProfileImpactCodes.Rooms, counts.Rooms);
        Add(hidesLodging, TenantProfileImpactCodes.Folios, counts.Folios);
        Add(!target.HasPosFeature("ticketScan"), TenantProfileImpactCodes.Tickets, counts.Tickets);
        return warnings;
    }

    private async Task<EffectiveVerticalProfileDto?> ResolveEffectiveAsync(
        string? configuredProfileId,
        string? overridesJson,
        decimal? taxiTariffPerKm,
        CancellationToken cancellationToken)
    {
        var profileId = string.IsNullOrWhiteSpace(configuredProfileId)
            ? VerticalProfileIds.Gastronomy
            : configuredProfileId;

        var profile = await _registry.FindActiveAsync(profileId, cancellationToken).ConfigureAwait(false);

        if (profile is null && !string.Equals(profileId, VerticalProfileIds.Gastronomy, StringComparison.Ordinal))
            profile = await _registry.FindActiveAsync(VerticalProfileIds.Gastronomy, cancellationToken)
                .ConfigureAwait(false);

        return profile is null ? null : BuildEffective(profile, overridesJson ?? "{}", taxiTariffPerKm);
    }

    private static EffectiveVerticalProfileDto BuildEffective(
        MergedVerticalProfile profile,
        string overridesJson,
        decimal? taxiTariffPerKm)
    {
        var effective = new JsonObject
        {
            ["posFeatures"] = ParseNode(profile.PosFeatures),
            ["requiredFields"] = ParseNode(profile.RequiredFields),
            ["optionalFields"] = ParseNode(profile.OptionalFields),
            ["posLayout"] = profile.PosLayout,
        };
        var overrides = ParseNode(overridesJson) as JsonObject ?? new JsonObject();
        DeepMerge(effective, overrides);

        return new EffectiveVerticalProfileDto(
            profile.Id,
            profile.Name,
            ToElement(effective["posFeatures"]),
            ToElement(effective["requiredFields"]),
            ToElement(effective["optionalFields"]),
            effective["posLayout"]?.GetValue<string>() ?? profile.PosLayout,
            ToElement(overrides),
            taxiTariffPerKm);
    }

    /// <summary>
    /// Recursively merges objects. Arrays and scalar values replace the base value; therefore
    /// a customer field-list override leaves the sibling product list intact.
    /// </summary>
    private static void DeepMerge(JsonObject target, JsonObject overlay)
    {
        foreach (var property in overlay)
        {
            if (property.Value is JsonObject overlayObject
                && target[property.Key] is JsonObject targetObject)
            {
                DeepMerge(targetObject, overlayObject);
                continue;
            }

            target[property.Key] = property.Value?.DeepClone();
        }
    }

    private static string NormalizeAndValidateOverrides(JsonElement overrides)
    {
        if (overrides.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return "{}";
        if (overrides.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Overrides must be a JSON object.");

        var raw = overrides.GetRawText();
        if (raw.Length > MaxOverridesJsonLength)
            throw new ArgumentException($"Overrides must not exceed {MaxOverridesJsonLength} characters.");

        var node = JsonNode.Parse(raw) as JsonObject
            ?? throw new ArgumentException("Overrides must be a JSON object.");

        foreach (var property in node)
        {
            if (!AllowedOverrideProperties.Contains(property.Key))
                throw new ArgumentException($"Unknown override property '{property.Key}'.");

            switch (property.Key)
            {
                case "posFeatures":
                    ValidatePosFeatures(property.Value);
                    break;
                case "requiredFields":
                case "optionalFields":
                    ValidateFieldLists(property.Key, property.Value);
                    break;
                case "posLayout":
                    ValidateLayout(property.Value);
                    break;
            }
        }

        return node.ToJsonString();
    }

    private static void ValidatePosFeatures(JsonNode? value)
    {
        if (value is not JsonObject features)
            throw new ArgumentException("posFeatures must be a JSON object.");

        foreach (var feature in features)
        {
            if (feature.Value is not JsonValue jsonValue
                || !jsonValue.TryGetValue<bool>(out _))
            {
                throw new ArgumentException($"posFeatures.{feature.Key} must be a boolean.");
            }
        }
    }

    private static void ValidateFieldLists(string propertyName, JsonNode? value)
    {
        if (value is not JsonObject fieldGroups)
            throw new ArgumentException($"{propertyName} must be a JSON object.");

        foreach (var group in fieldGroups)
        {
            if (group.Key is not ("customer" or "product" or "order"))
                throw new ArgumentException($"Unknown {propertyName} group '{group.Key}'.");
            if (group.Value is not JsonArray fields)
                throw new ArgumentException($"{propertyName}.{group.Key} must be an array.");

            foreach (var field in fields)
            {
                if (field is not JsonValue jsonValue
                    || !jsonValue.TryGetValue<string>(out var fieldName)
                    || string.IsNullOrWhiteSpace(fieldName))
                {
                    throw new ArgumentException($"{propertyName}.{group.Key} must contain non-empty strings.");
                }
            }
        }
    }

    private static void ValidateLayout(JsonNode? value)
    {
        if (value is not JsonValue jsonValue
            || !jsonValue.TryGetValue<string>(out var layout)
            || !VerticalProfileLayouts.All.Contains(layout))
        {
            throw new ArgumentException(
                $"posLayout must be one of: {string.Join(", ", VerticalProfileLayouts.All)}.");
        }
    }

    private static JsonNode ParseNode(string json) =>
        JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json)
        ?? new JsonObject();

    private static JsonElement ParseElement(string json)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        return document.RootElement.Clone();
    }

    private static JsonElement ToElement(JsonNode? node) =>
        JsonSerializer.SerializeToElement(node ?? new JsonObject());
}
