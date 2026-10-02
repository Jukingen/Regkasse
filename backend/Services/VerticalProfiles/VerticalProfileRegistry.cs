using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.VerticalProfiles;

public sealed record MergedVerticalProfile(
    string Id,
    string Name,
    string PosFeatures,
    string RequiredFields,
    string OptionalFields,
    string PosLayout,
    string Source,
    int TenantCount);

public interface IVerticalProfileRegistry
{
    Task<IReadOnlyList<MergedVerticalProfile>> ListActiveAsync(
        bool includeTenantCounts,
        CancellationToken cancellationToken = default);

    Task<MergedVerticalProfile?> FindActiveAsync(
        string profileId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, int>> LoadTenantCountsAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Merges code seeds with <c>vertical_profile_overrides</c>. A database row replaces the seed.
/// </summary>
public sealed class VerticalProfileRegistry : IVerticalProfileRegistry
{
    private static readonly HashSet<string> SeedIds = VerticalProfileSeedData.All
        .Select(profile => profile.Id)
        .ToHashSet(StringComparer.Ordinal);

    private readonly AppDbContext _db;

    public VerticalProfileRegistry(AppDbContext db)
    {
        _db = db;
    }

    public static bool IsSeed(string profileId) => SeedIds.Contains(profileId);

    public async Task<IReadOnlyList<MergedVerticalProfile>> ListActiveAsync(
        bool includeTenantCounts,
        CancellationToken cancellationToken = default)
    {
        var merged = await LoadMergedAsync(cancellationToken).ConfigureAwait(false);
        if (!includeTenantCounts)
            return merged;

        var counts = await LoadTenantCountsAsync(cancellationToken).ConfigureAwait(false);
        return merged
            .Select(profile => profile with
            {
                TenantCount = counts.GetValueOrDefault(profile.Id),
            })
            .ToArray();
    }

    public async Task<MergedVerticalProfile?> FindActiveAsync(
        string profileId,
        CancellationToken cancellationToken = default)
    {
        if (!VerticalProfileJson.TryNormalizeId(profileId, out var id))
            return null;

        var merged = await LoadMergedAsync(cancellationToken).ConfigureAwait(false);
        return merged.FirstOrDefault(profile => profile.Id == id);
    }

    public async Task<IReadOnlyDictionary<string, int>> LoadTenantCountsAsync(
        CancellationToken cancellationToken = default)
    {
        var assigned = await _db.CompanySettings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Select(settings => settings.VerticalProfileId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var profileId in assigned)
        {
            var key = string.IsNullOrWhiteSpace(profileId)
                ? VerticalProfileIds.Gastronomy
                : profileId;
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }

        return counts;
    }

    private async Task<IReadOnlyList<MergedVerticalProfile>> LoadMergedAsync(
        CancellationToken cancellationToken)
    {
        var dbRows = await _db.VerticalProfiles
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var overrides = await _db.VerticalProfileOverrides
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var byId = new Dictionary<string, MergedVerticalProfile>(StringComparer.Ordinal);
        foreach (var seed in VerticalProfileSeedData.All)
        {
            byId[seed.Id] = new MergedVerticalProfile(
                seed.Id,
                seed.Name,
                seed.PosFeatures,
                seed.RequiredFields,
                seed.OptionalFields,
                seed.PosLayout,
                VerticalProfileSources.Seed,
                0);
        }

        foreach (var row in dbRows)
        {
            if (!row.IsActive || SeedIds.Contains(row.Id) || byId.ContainsKey(row.Id))
                continue;

            byId[row.Id] = new MergedVerticalProfile(
                row.Id,
                row.Name,
                row.PosFeatures,
                row.RequiredFields,
                row.OptionalFields,
                row.PosLayout,
                VerticalProfileSources.Custom,
                0);
        }

        foreach (var row in overrides)
        {
            if (row.IsDeleted)
            {
                byId.Remove(row.ProfileId);
                continue;
            }

            var source = SeedIds.Contains(row.ProfileId)
                ? VerticalProfileSources.Override
                : VerticalProfileSources.Custom;
            byId[row.ProfileId] = new MergedVerticalProfile(
                row.ProfileId,
                row.Name,
                row.PosFeaturesJson,
                row.RequiredFieldsJson,
                row.OptionalFieldsJson,
                row.PosLayout,
                source,
                0);
        }

        return byId.Values.OrderBy(profile => profile.Id, StringComparer.Ordinal).ToArray();
    }
}
