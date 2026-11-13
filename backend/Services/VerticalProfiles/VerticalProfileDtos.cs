using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.VerticalProfiles;

public sealed record VerticalProfileDto(
    string Id,
    string Name,
    JsonElement PosFeatures,
    JsonElement RequiredFields,
    JsonElement OptionalFields,
    string PosLayout,
    int FeatureCount = 0,
    int TenantCount = 0,
    string Source = "seed");

public sealed record EffectiveVerticalProfileDto(
    string ProfileId,
    string Name,
    JsonElement PosFeatures,
    JsonElement RequiredFields,
    JsonElement OptionalFields,
    string PosLayout,
    JsonElement Overrides,
    decimal? TaxiTariffPerKm = null)
{
    public bool HasPosFeature(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || PosFeatures.ValueKind != JsonValueKind.Object)
            return false;

        return PosFeatures.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.True;
    }

    public bool IsTaxiProfile =>
        string.Equals(ProfileId, VerticalProfileIds.Taxi, StringComparison.Ordinal);

    public bool IsTicketSalesProfile =>
        string.Equals(ProfileId, VerticalProfileIds.TicketSales, StringComparison.Ordinal);
}

public sealed class UpdateTenantVerticalProfileRequest
{
    [Required]
    [MaxLength(64)]
    public string ProfileId { get; set; } = string.Empty;

    public JsonElement Overrides { get; set; }

    /// <summary>Optional taxi fare per kilometre stored on company settings, not in override JSON.</summary>
    [Range(0, 9999.99)]
    public decimal? TaxiTariffPerKm { get; set; }
}

public static class VerticalProfileUpdateErrorCodes
{
    public const string TenantNotFound = "TENANT_NOT_FOUND";
    public const string CompanySettingsMissing = "COMPANY_SETTINGS_MISSING";
    public const string ProfileNotFound = "VERTICAL_PROFILE_NOT_FOUND";
    public const string InvalidOverrides = "INVALID_VERTICAL_PROFILE_OVERRIDES";
}

public sealed record VerticalProfileUpdateError(string Code, string Message);

public static class TenantProfileImpactCodes
{
    public const string CustomersWithPetData = "customersWithPetData";
    public const string PaymentsWithPrescriptionReference = "paymentsWithPrescriptionReference";
    public const string PaymentsWithRouteFrom = "paymentsWithRouteFrom";
    public const string SoldImeis = "soldImeis";
    public const string Appointments = "appointments";
    public const string Rooms = "rooms";
    public const string Folios = "folios";
    public const string Tickets = "tickets";
}

public sealed record TenantProfileImpactCounts(
    int CustomersWithPetData,
    int PaymentsWithPrescriptionReference,
    int PaymentsWithRouteFrom,
    int SoldImeis,
    int Appointments,
    int Rooms,
    int Folios,
    int Tickets);

public sealed record TenantProfileImpactWarning(string Code, int Count);

/// <summary>
/// Rows that stay in the database and would be hidden by the target profile.
/// </summary>
public sealed record TenantProfileImpactDto(
    string CurrentProfileId,
    string TargetProfileId,
    TenantProfileImpactCounts Counts,
    IReadOnlyList<TenantProfileImpactWarning> Warnings);

public sealed class UpsertVerticalProfileRequest
{
    [MaxLength(64)]
    public string? Id { get; set; }

    [MaxLength(200)]
    public string? Name { get; set; }

    [MaxLength(64)]
    public string? CloneFrom { get; set; }

    public JsonElement PosFeatures { get; set; }

    public JsonElement RequiredFields { get; set; }

    public JsonElement OptionalFields { get; set; }

    [MaxLength(32)]
    public string? PosLayout { get; set; }
}

public sealed class CloneVerticalProfileRequest
{
    [Required]
    [MaxLength(64)]
    public string Id { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Name { get; set; }
}

public sealed class UpdateVerticalProfileFeaturesRequest
{
    public JsonElement PosFeatures { get; set; }
}

public sealed record VerticalProfileTenantSummaryDto(
    Guid Id,
    string Name,
    string Slug,
    int OverridesCount);

public sealed record VerticalProfileTenantGroupDto(
    string ProfileId,
    string Name,
    string Source,
    int TenantCount,
    IReadOnlyList<VerticalProfileTenantSummaryDto> Tenants);

public sealed record VerticalProfileMutationResult(
    VerticalProfileDto Profile,
    IReadOnlyList<string> RemovedFeatures,
    IReadOnlyList<VerticalProfileTenantSummaryDto> AffectedTenants);

public sealed record VerticalProfileCatalogError(
    string Code,
    string Message,
    int StatusCode,
    IReadOnlyList<VerticalProfileTenantSummaryDto>? Tenants = null);
