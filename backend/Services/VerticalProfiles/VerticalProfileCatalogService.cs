using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Caching;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.VerticalProfiles;

public interface IVerticalProfileCatalogService
{
    Task<(VerticalProfileMutationResult? Result, VerticalProfileCatalogError? Error)> CreateAsync(
        UpsertVerticalProfileRequest request,
        string actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    Task<(VerticalProfileMutationResult? Result, VerticalProfileCatalogError? Error)> UpdateAsync(
        string profileId,
        UpsertVerticalProfileRequest request,
        string actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    Task<(VerticalProfileMutationResult? Result, VerticalProfileCatalogError? Error)> UpdateFeaturesAsync(
        string profileId,
        UpdateVerticalProfileFeaturesRequest request,
        string actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    Task<(VerticalProfileMutationResult? Result, VerticalProfileCatalogError? Error)> CloneAsync(
        string profileId,
        CloneVerticalProfileRequest request,
        string actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    Task<VerticalProfileCatalogError?> DeleteAsync(
        string profileId,
        string actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VerticalProfileTenantSummaryDto>?> ListTenantsAsync(
        string profileId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VerticalProfileTenantGroupDto>> GroupTenantsByProfileAsync(
        CancellationToken cancellationToken = default);
}

public sealed class VerticalProfileCatalogService : IVerticalProfileCatalogService
{
    private readonly AppDbContext _db;
    private readonly IVerticalProfileRegistry _registry;
    private readonly IAuditLogService _auditLog;
    private readonly ICacheService _cache;
    private readonly ILogger<VerticalProfileCatalogService> _logger;

    public VerticalProfileCatalogService(
        AppDbContext db,
        IVerticalProfileRegistry registry,
        IAuditLogService auditLog,
        ICacheService cache,
        ILogger<VerticalProfileCatalogService> logger)
    {
        _db = db;
        _registry = registry;
        _auditLog = auditLog;
        _cache = cache;
        _logger = logger;
    }

    public async Task<(VerticalProfileMutationResult? Result, VerticalProfileCatalogError? Error)> CreateAsync(
        UpsertVerticalProfileRequest request,
        string actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!VerticalProfileJson.TryNormalizeId(request.Id, out var id))
        {
            return (null, Invalid("Profile id must be a lowercase slug (2-64 characters)."));
        }

        if (await ProfileIdTakenAsync(id, cancellationToken).ConfigureAwait(false))
        {
            return (null, new VerticalProfileCatalogError(
                VerticalProfileCatalogErrorCodes.ProfileExists,
                "A vertical profile with this id already exists.",
                StatusCodes.Status409Conflict));
        }

        MergedVerticalProfile? cloneSource = null;
        if (!string.IsNullOrWhiteSpace(request.CloneFrom))
        {
            cloneSource = await _registry.FindActiveAsync(request.CloneFrom, cancellationToken)
                .ConfigureAwait(false);
            if (cloneSource is null)
            {
                return (null, NotFound("Clone source profile was not found."));
            }
        }

        string features;
        string requiredFields;
        string optionalFields;
        string layout;
        try
        {
            features = JsonProvided(request.PosFeatures)
                ? VerticalProfileJson.NormalizeFeatures(request.PosFeatures)
                : cloneSource?.PosFeatures ?? VerticalProfileJson.DefaultFeaturesJson();
            requiredFields = JsonProvided(request.RequiredFields)
                ? VerticalProfileJson.NormalizeFields(request.RequiredFields, "requiredFields")
                : cloneSource?.RequiredFields ?? VerticalProfileJson.DefaultFieldsJson;
            optionalFields = JsonProvided(request.OptionalFields)
                ? VerticalProfileJson.NormalizeFields(request.OptionalFields, "optionalFields")
                : cloneSource?.OptionalFields ?? VerticalProfileJson.DefaultFieldsJson;
            layout = string.IsNullOrWhiteSpace(request.PosLayout)
                ? cloneSource?.PosLayout ?? VerticalProfileLayouts.Standard
                : VerticalProfileJson.NormalizeLayout(request.PosLayout);
        }
        catch (ArgumentException ex)
        {
            return (null, Invalid(ex.Message));
        }

        var name = string.IsNullOrWhiteSpace(request.Name) ? id : request.Name.Trim();
        if (name.Length is < 1 or > 200)
            return (null, Invalid("Name must be 1-200 characters."));

        var now = DateTime.UtcNow;
        if (!VerticalProfileRegistry.IsSeed(id))
        {
            _db.VerticalProfiles.Add(new VerticalProfile
            {
                Id = id,
                Name = name,
                PosFeatures = features,
                RequiredFields = requiredFields,
                OptionalFields = optionalFields,
                PosLayout = layout,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            });
        }

        _db.VerticalProfileOverrides.Add(new VerticalProfileOverride
        {
            ProfileId = id,
            Name = name,
            PosFeaturesJson = features,
            RequiredFieldsJson = requiredFields,
            OptionalFieldsJson = optionalFields,
            PosLayout = layout,
            IsDeleted = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        });

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await InvalidateAsync(id, [], cancellationToken).ConfigureAwait(false);
        await AuditAsync(
                AuditEventType.VerticalProfileCreated,
                "VERTICAL_PROFILE_CREATED",
                id,
                actorUserId,
                actorRole,
                $"Vertical profile {id} created",
                oldValues: null,
                newValues: new { id, name, posLayout = layout },
                cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation("Vertical profile {ProfileId} created", id);
        var created = await RequireDtoAsync(id, cancellationToken).ConfigureAwait(false);
        return (new VerticalProfileMutationResult(created, [], []), null);
    }

    public Task<(VerticalProfileMutationResult? Result, VerticalProfileCatalogError? Error)> UpdateAsync(
        string profileId,
        UpsertVerticalProfileRequest request,
        string actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!string.IsNullOrWhiteSpace(request.Id)
            && !string.Equals(
                request.Id.Trim(),
                profileId.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<(VerticalProfileMutationResult?, VerticalProfileCatalogError?)>((
                null,
                new VerticalProfileCatalogError(
                    VerticalProfileCatalogErrorCodes.IdImmutable,
                    "Profile id cannot be changed.",
                    StatusCodes.Status400BadRequest)));
        }

        return SaveReplacementAsync(
            profileId,
            request.Name,
            JsonProvided(request.PosFeatures) ? request.PosFeatures : null,
            JsonProvided(request.RequiredFields) ? request.RequiredFields : null,
            JsonProvided(request.OptionalFields) ? request.OptionalFields : null,
            request.PosLayout,
            actorUserId,
            actorRole,
            cancellationToken);
    }

    public Task<(VerticalProfileMutationResult? Result, VerticalProfileCatalogError? Error)> UpdateFeaturesAsync(
        string profileId,
        UpdateVerticalProfileFeaturesRequest request,
        string actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.PosFeatures.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return Task.FromResult<(VerticalProfileMutationResult?, VerticalProfileCatalogError?)>((
                null,
                new VerticalProfileCatalogError(
                    VerticalProfileCatalogErrorCodes.InvalidProfile,
                    "posFeatures is required.",
                    StatusCodes.Status400BadRequest)));
        }

        return SaveReplacementAsync(
            profileId,
            name: null,
            posFeatures: request.PosFeatures,
            requiredFields: null,
            optionalFields: null,
            posLayout: null,
            actorUserId,
            actorRole,
            cancellationToken);
    }

    public async Task<(VerticalProfileMutationResult? Result, VerticalProfileCatalogError? Error)> CloneAsync(
        string profileId,
        CloneVerticalProfileRequest request,
        string actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var source = await _registry.FindActiveAsync(profileId, cancellationToken).ConfigureAwait(false);
        if (source is null)
            return (null, NotFound("Vertical profile not found."));

        return await CreateAsync(
                new UpsertVerticalProfileRequest
                {
                    Id = request.Id,
                    Name = string.IsNullOrWhiteSpace(request.Name) ? request.Id : request.Name,
                    CloneFrom = source.Id,
                    PosLayout = source.PosLayout,
                },
                actorUserId,
                actorRole,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<VerticalProfileCatalogError?> DeleteAsync(
        string profileId,
        string actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        var profile = await _registry.FindActiveAsync(profileId, cancellationToken).ConfigureAwait(false);
        if (profile is null)
            return NotFound("Vertical profile not found.");

        var tenants = await LoadTenantSummariesAsync(profile.Id, cancellationToken).ConfigureAwait(false);
        if (tenants.Count > 0)
        {
            return new VerticalProfileCatalogError(
                VerticalProfileCatalogErrorCodes.ProfileInUse,
                "Vertical profile is assigned to one or more tenants.",
                StatusCodes.Status409Conflict,
                tenants);
        }

        if (VerticalProfileRegistry.IsSeed(profile.Id))
        {
            return new VerticalProfileCatalogError(
                VerticalProfileCatalogErrorCodes.ProfileIsSeed,
                "Seed profiles stay in the catalog and cannot be deleted.",
                StatusCodes.Status400BadRequest);
        }

        var now = DateTime.UtcNow;
        var overrideRow = await _db.VerticalProfileOverrides
            .FirstOrDefaultAsync(row => row.ProfileId == profile.Id, cancellationToken)
            .ConfigureAwait(false);
        if (overrideRow is null)
        {
            overrideRow = new VerticalProfileOverride
            {
                ProfileId = profile.Id,
                Name = profile.Name,
                PosFeaturesJson = profile.PosFeatures,
                RequiredFieldsJson = profile.RequiredFields,
                OptionalFieldsJson = profile.OptionalFields,
                PosLayout = profile.PosLayout,
                CreatedAtUtc = now,
            };
            _db.VerticalProfileOverrides.Add(overrideRow);
        }

        overrideRow.IsDeleted = true;
        overrideRow.UpdatedAtUtc = now;

        var baseRow = await _db.VerticalProfiles
            .FirstOrDefaultAsync(row => row.Id == profile.Id, cancellationToken)
            .ConfigureAwait(false);
        if (baseRow is not null)
        {
            baseRow.IsActive = false;
            baseRow.UpdatedAtUtc = now;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await InvalidateAsync(profile.Id, [], cancellationToken).ConfigureAwait(false);
        await AuditAsync(
                AuditEventType.VerticalProfileDeleted,
                "VERTICAL_PROFILE_DELETED",
                profile.Id,
                actorUserId,
                actorRole,
                $"Vertical profile {profile.Id} soft-deleted",
                oldValues: new { profile.Id, profile.Name },
                newValues: new { profile.Id, isDeleted = true },
                cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation("Vertical profile {ProfileId} soft-deleted", profile.Id);
        return null;
    }

    public async Task<IReadOnlyList<VerticalProfileTenantSummaryDto>?> ListTenantsAsync(
        string profileId,
        CancellationToken cancellationToken = default)
    {
        var profile = await _registry.FindActiveAsync(profileId, cancellationToken).ConfigureAwait(false);
        if (profile is null)
            return null;

        return await LoadTenantSummariesAsync(profile.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<VerticalProfileTenantGroupDto>> GroupTenantsByProfileAsync(
        CancellationToken cancellationToken = default)
    {
        var profiles = await _registry.ListActiveAsync(includeTenantCounts: false, cancellationToken)
            .ConfigureAwait(false);
        var groups = profiles.ToDictionary(
            profile => profile.Id,
            profile => new List<VerticalProfileTenantSummaryDto>(),
            StringComparer.Ordinal);

        var assignments = await _db.CompanySettings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Select(settings => new { settings.TenantId, settings.VerticalProfileId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (assignments.Count == 0)
        {
            return profiles
                .Select(profile => new VerticalProfileTenantGroupDto(
                    profile.Id,
                    profile.Name,
                    profile.Source,
                    0,
                    []))
                .ToArray();
        }

        var tenantIds = assignments.Select(row => row.TenantId).Distinct().ToArray();
        var tenants = await _db.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(tenant => tenantIds.Contains(tenant.Id))
            .Select(tenant => new { tenant.Id, tenant.Name, tenant.Slug })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var tenantById = tenants.ToDictionary(tenant => tenant.Id);
        var overrideCounts = await LoadOverrideCountsAsync(tenantIds, cancellationToken).ConfigureAwait(false);

        foreach (var assignment in assignments)
        {
            if (!tenantById.TryGetValue(assignment.TenantId, out var tenant))
                continue;

            var profileId = string.IsNullOrWhiteSpace(assignment.VerticalProfileId)
                ? VerticalProfileIds.Gastronomy
                : assignment.VerticalProfileId;
            if (!groups.TryGetValue(profileId, out var bucket))
            {
                bucket = [];
                groups[profileId] = bucket;
            }

            bucket.Add(new VerticalProfileTenantSummaryDto(
                tenant.Id,
                tenant.Name,
                tenant.Slug,
                overrideCounts.GetValueOrDefault(tenant.Id)));
        }

        return profiles
            .Select(profile =>
            {
                var tenantsOnProfile = groups.GetValueOrDefault(profile.Id) ?? [];
                return new VerticalProfileTenantGroupDto(
                    profile.Id,
                    profile.Name,
                    profile.Source,
                    tenantsOnProfile.Count,
                    tenantsOnProfile.OrderBy(tenant => tenant.Name, StringComparer.Ordinal).ToArray());
            })
            .ToArray();
    }

    private async Task<(VerticalProfileMutationResult? Result, VerticalProfileCatalogError? Error)> SaveReplacementAsync(
        string profileId,
        string? name,
        JsonElement? posFeatures,
        JsonElement? requiredFields,
        JsonElement? optionalFields,
        string? posLayout,
        string actorUserId,
        string actorRole,
        CancellationToken cancellationToken)
    {
        var current = await _registry.FindActiveAsync(profileId, cancellationToken).ConfigureAwait(false);
        if (current is null)
            return (null, NotFound("Vertical profile not found."));

        string features;
        string required;
        string optional;
        string layout;
        string nextName;
        try
        {
            features = posFeatures is { } featureElement
                ? VerticalProfileJson.NormalizeFeatures(featureElement)
                : current.PosFeatures;
            required = requiredFields is { } requiredElement
                ? VerticalProfileJson.NormalizeFields(requiredElement, "requiredFields")
                : current.RequiredFields;
            optional = optionalFields is { } optionalElement
                ? VerticalProfileJson.NormalizeFields(optionalElement, "optionalFields")
                : current.OptionalFields;
            layout = string.IsNullOrWhiteSpace(posLayout)
                ? current.PosLayout
                : VerticalProfileJson.NormalizeLayout(posLayout);
            nextName = string.IsNullOrWhiteSpace(name) ? current.Name : name.Trim();
            if (nextName.Length is < 1 or > 200)
                return (null, Invalid("Name must be 1-200 characters."));
        }
        catch (ArgumentException ex)
        {
            return (null, Invalid(ex.Message));
        }

        var removed = VerticalProfileJson.RemovedFeatureKeys(current.PosFeatures, features);
        var affected = removed.Count == 0
            ? []
            : await LoadTenantSummariesAsync(current.Id, cancellationToken).ConfigureAwait(false);

        var now = DateTime.UtcNow;
        var overrideRow = await _db.VerticalProfileOverrides
            .FirstOrDefaultAsync(row => row.ProfileId == current.Id, cancellationToken)
            .ConfigureAwait(false);
        if (overrideRow is null)
        {
            overrideRow = new VerticalProfileOverride
            {
                ProfileId = current.Id,
                CreatedAtUtc = now,
            };
            _db.VerticalProfileOverrides.Add(overrideRow);
        }

        overrideRow.Name = nextName;
        overrideRow.PosFeaturesJson = features;
        overrideRow.RequiredFieldsJson = required;
        overrideRow.OptionalFieldsJson = optional;
        overrideRow.PosLayout = layout;
        overrideRow.IsDeleted = false;
        overrideRow.UpdatedAtUtc = now;

        if (!VerticalProfileRegistry.IsSeed(current.Id))
        {
            var baseRow = await _db.VerticalProfiles
                .FirstOrDefaultAsync(row => row.Id == current.Id, cancellationToken)
                .ConfigureAwait(false);
            if (baseRow is null)
            {
                _db.VerticalProfiles.Add(new VerticalProfile
                {
                    Id = current.Id,
                    Name = nextName,
                    PosFeatures = features,
                    RequiredFields = required,
                    OptionalFields = optional,
                    PosLayout = layout,
                    IsActive = true,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now,
                });
            }
            else
            {
                baseRow.Name = nextName;
                baseRow.PosFeatures = features;
                baseRow.RequiredFields = required;
                baseRow.OptionalFields = optional;
                baseRow.PosLayout = layout;
                baseRow.IsActive = true;
                baseRow.UpdatedAtUtc = now;
            }
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var tenantIds = affected.Count > 0
            ? affected.Select(tenant => tenant.Id).ToArray()
            : await LoadTenantIdsAsync(current.Id, cancellationToken).ConfigureAwait(false);
        await InvalidateAsync(current.Id, tenantIds, cancellationToken).ConfigureAwait(false);
        await AuditAsync(
                AuditEventType.VerticalProfileUpdated,
                "VERTICAL_PROFILE_UPDATED",
                current.Id,
                actorUserId,
                actorRole,
                $"Vertical profile {current.Id} updated",
                oldValues: new { current.Name, current.PosLayout },
                newValues: new { name = nextName, posLayout = layout, removedFeatures = removed },
                cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation("Vertical profile {ProfileId} updated", current.Id);
        var dto = await RequireDtoAsync(current.Id, cancellationToken).ConfigureAwait(false);
        return (new VerticalProfileMutationResult(dto, removed, affected), null);
    }

    private async Task<bool> ProfileIdTakenAsync(string id, CancellationToken cancellationToken)
    {
        var active = await _registry.FindActiveAsync(id, cancellationToken).ConfigureAwait(false);
        if (active is not null)
            return true;

        return await _db.VerticalProfiles.AnyAsync(row => row.Id == id, cancellationToken).ConfigureAwait(false)
            || await _db.VerticalProfileOverrides.AnyAsync(row => row.ProfileId == id, cancellationToken)
                .ConfigureAwait(false);
    }

    private async Task<VerticalProfileDto> RequireDtoAsync(string id, CancellationToken cancellationToken)
    {
        var profiles = await _registry.ListActiveAsync(includeTenantCounts: true, cancellationToken)
            .ConfigureAwait(false);
        var profile = profiles.First(candidate => candidate.Id == id);
        return ToDto(profile);
    }

    internal static VerticalProfileDto ToDto(MergedVerticalProfile profile) =>
        new(
            profile.Id,
            profile.Name,
            ParseElement(profile.PosFeatures),
            ParseElement(profile.RequiredFields),
            ParseElement(profile.OptionalFields),
            profile.PosLayout,
            VerticalProfileJson.CountEnabledFeatures(profile.PosFeatures),
            profile.TenantCount,
            profile.Source);

    private async Task<IReadOnlyList<Guid>> LoadTenantIdsAsync(
        string profileId,
        CancellationToken cancellationToken)
    {
        var rows = await _db.CompanySettings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Select(settings => new { settings.TenantId, settings.VerticalProfileId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Where(row => ResolveProfileId(row.VerticalProfileId) == profileId)
            .Select(row => row.TenantId)
            .ToArray();
    }

    private async Task<IReadOnlyList<VerticalProfileTenantSummaryDto>> LoadTenantSummariesAsync(
        string profileId,
        CancellationToken cancellationToken)
    {
        var tenantIds = await LoadTenantIdsAsync(profileId, cancellationToken).ConfigureAwait(false);
        if (tenantIds.Count == 0)
            return [];

        var tenants = await _db.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(tenant => tenantIds.Contains(tenant.Id))
            .Select(tenant => new { tenant.Id, tenant.Name, tenant.Slug })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var overrideCounts = await LoadOverrideCountsAsync(tenantIds, cancellationToken).ConfigureAwait(false);

        return tenants
            .OrderBy(tenant => tenant.Name, StringComparer.Ordinal)
            .Select(tenant => new VerticalProfileTenantSummaryDto(
                tenant.Id,
                tenant.Name,
                tenant.Slug,
                overrideCounts.GetValueOrDefault(tenant.Id)))
            .ToArray();
    }

    private async Task<Dictionary<Guid, int>> LoadOverrideCountsAsync(
        IReadOnlyCollection<Guid> tenantIds,
        CancellationToken cancellationToken)
    {
        var rows = await _db.TenantVerticalOverrides
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(row => tenantIds.Contains(row.TenantId))
            .Select(row => new { row.TenantId, row.OverridesJson })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ToDictionary(
            row => row.TenantId,
            row => VerticalProfileJson.CountTopLevelKeys(row.OverridesJson));
    }

    private async Task InvalidateAsync(
        string profileId,
        IReadOnlyCollection<Guid> tenantIds,
        CancellationToken cancellationToken)
    {
        await _cache.RemoveAsync(CacheKeys.VerticalProfileCatalog, cancellationToken).ConfigureAwait(false);
        await _cache.RemoveAsync(
                CacheKeys.Format(CacheKeys.VerticalProfileEffective, profileId),
                cancellationToken)
            .ConfigureAwait(false);

        foreach (var tenantId in tenantIds.Distinct())
        {
            await CacheInvalidationHelper.InvalidateTenantCacheAsync(
                    _cache,
                    tenantId,
                    cancellationToken,
                    CacheKeys.VerticalProfileEffective)
                .ConfigureAwait(false);
        }
    }

    private async Task AuditAsync(
        AuditEventType actionType,
        string action,
        string profileId,
        string actorUserId,
        string actorRole,
        string description,
        object? oldValues,
        object? newValues,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        await _auditLog.LogSystemOperationAsync(
                action: action,
                entityType: "VerticalProfile",
                userId: actorUserId,
                userRole: actorRole,
                description: description,
                status: AuditLogStatus.Success,
                correlationIdOverride: Guid.NewGuid().ToString("N"),
                actionType: actionType,
                entityId: null,
                tenantId: null,
                oldValues: oldValues,
                newValues: newValues)
            .ConfigureAwait(false);
    }

    private static string ResolveProfileId(string? profileId) =>
        string.IsNullOrWhiteSpace(profileId) ? VerticalProfileIds.Gastronomy : profileId;

    private static bool JsonProvided(JsonElement element) =>
        element.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null;

    private static VerticalProfileCatalogError Invalid(string message) =>
        new(VerticalProfileCatalogErrorCodes.InvalidProfile, message, StatusCodes.Status400BadRequest);

    private static VerticalProfileCatalogError NotFound(string message) =>
        new(VerticalProfileCatalogErrorCodes.ProfileNotFound, message, StatusCodes.Status404NotFound);

    private static JsonElement ParseElement(string json)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        return document.RootElement.Clone();
    }
}
