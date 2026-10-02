using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Global POS capability/layout profile. Names are localization keys; JSON fields hold
/// extensible vertical configuration without duplicating feature flags.
/// </summary>
[Table("vertical_profiles")]
public sealed class VerticalProfile
{
    [Key]
    [MaxLength(64)]
    [Column("id")]
    public string Id { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Column("pos_features", TypeName = "jsonb")]
    public string PosFeatures { get; set; } = "{}";

    [Required]
    [Column("required_fields", TypeName = "jsonb")]
    public string RequiredFields { get; set; } = """{"customer":[],"product":[]}""";

    [Required]
    [Column("optional_fields", TypeName = "jsonb")]
    public string OptionalFields { get; set; } = """{"customer":[],"product":[]}""";

    [Required]
    [MaxLength(32)]
    [Column("pos_layout")]
    public string PosLayout { get; set; } = VerticalProfileLayouts.Standard;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public static class VerticalProfileLayouts
{
    public const string Standard = "standard";
    public const string Tables = "tables";
    public const string Appointment = "appointment";
    public const string Queue = "queue";

    public const string Taxi = "taxi";
    public const string Ticket = "ticket";

    /// <summary>
    /// Catalog value for lodging-oriented layouts. The POS client still treats an unknown
    /// layout as <see cref="Standard"/>; this value is stored for Super Admin configuration.
    /// </summary>
    public const string Rooms = "rooms";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(
        [Standard, Tables, Appointment, Queue, Taxi, Ticket, Rooms],
        StringComparer.Ordinal);
}

public static class VerticalProfileIds
{
    public const string Gastronomy = "gastronomy";
    public const string GastronomyTables = "gastronomy-tables";
    public const string Vet = "vet";
    public const string HairSalon = "hair-salon";
    public const string MobileServices = "mobile-services";
    public const string HandyShop = "handy-shop";
    public const string Taxi = "taxi";
    public const string TicketSales = "ticket-sales";
    public const string Beherbergung = "beherbergung";
}

/// <summary>Stable database seeds for the first two vertical-profile stages.</summary>
public static class VerticalProfileSeedData
{
    private static readonly DateTime SeededAtUtc =
        new(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc);

    public static IReadOnlyList<VerticalProfile> All { get; } =
    [
        Create(
            VerticalProfileIds.Gastronomy,
            "verticalProfiles.gastronomy.name",
            """{"tables":false,"kitchenDisplay":true,"patientRecord":false,"serviceDuration":false,"appointment":false,"imeiTracking":false,"routeTracking":false,"roomTracking":false}""",
            """{"customer":[],"product":["name","price","taxGroup"]}""",
            """{"customer":["name","phone","email"],"product":["description","category","stock"]}""",
            VerticalProfileLayouts.Standard),
        Create(
            VerticalProfileIds.GastronomyTables,
            "verticalProfiles.gastronomyTables.name",
            """{"tables":true,"kitchenDisplay":true,"patientRecord":false,"serviceDuration":false,"appointment":false,"imeiTracking":false,"routeTracking":false,"roomTracking":false}""",
            """{"customer":[],"product":["name","price","taxGroup"]}""",
            """{"customer":["name","phone","email"],"product":["description","category","stock"]}""",
            VerticalProfileLayouts.Tables),
        Create(
            VerticalProfileIds.HairSalon,
            "verticalProfiles.hairSalon.name",
            """{"tables":false,"kitchenDisplay":false,"patientRecord":false,"serviceDuration":true,"appointment":true,"imeiTracking":false,"routeTracking":false,"roomTracking":false}""",
            """{"customer":["name"],"product":["name","price","durationMinutes"]}""",
            """{"customer":["phone","email","notes"],"product":["description","category","staffId"]}""",
            VerticalProfileLayouts.Appointment),
        Create(
            VerticalProfileIds.Vet,
            "verticalProfiles.vet.name",
            """{"tables":false,"kitchenDisplay":false,"patientRecord":true,"serviceDuration":false,"appointment":false,"imeiTracking":false,"routeTracking":false,"roomTracking":false}""",
            """{"customer":["name","petName"],"product":["name","price","taxGroup"]}""",
            """{"customer":["phone","email","petSpecies","petBreed","petBirthDate","patientNotes"],"product":["description","category"]}""",
            VerticalProfileLayouts.Standard),
        Create(
            VerticalProfileIds.MobileServices,
            "verticalProfiles.mobileServices.name",
            """{"tables":false,"kitchenDisplay":false,"patientRecord":false,"serviceDuration":true,"appointment":true,"imeiTracking":false,"routeTracking":true,"roomTracking":false}""",
            """{"customer":["name","phone","address"],"product":["name","price","serviceDuration"]}""",
            """{"customer":["email","notes","street","postalCode","city"],"product":["description","category"],"order":["location"]}""",
            VerticalProfileLayouts.Appointment),
        Create(
            VerticalProfileIds.HandyShop,
            "verticalProfiles.handyShop.name",
            """{"tables":false,"kitchenDisplay":false,"patientRecord":false,"serviceDuration":false,"appointment":false,"imeiTracking":true,"routeTracking":false,"roomTracking":false}""",
            """{"customer":[],"product":["name","price","taxGroup"]}""",
            """{"customer":["name","phone","email"],"product":["description","category","stock","imei","serialNumber","brand","model"]}""",
            VerticalProfileLayouts.Standard),
        Create(
            VerticalProfileIds.Taxi,
            "verticalProfiles.taxi.name",
            """{"tables":false,"kitchenDisplay":false,"patientRecord":false,"serviceDuration":false,"appointment":false,"imeiTracking":false,"routeTracking":true,"roomTracking":false}""",
            """{"customer":[],"product":["name","price"]}""",
            """{"customer":["name","phone","pickupAddress","destinationAddress"],"product":["description"]}""",
            VerticalProfileLayouts.Taxi),
        Create(
            VerticalProfileIds.TicketSales,
            "verticalProfiles.ticketSales.name",
            """{"tables":false,"kitchenDisplay":false,"patientRecord":false,"serviceDuration":false,"appointment":false,"imeiTracking":false,"routeTracking":false,"roomTracking":true,"ticketScan":true}""",
            """{"customer":[],"product":["name","price"]}""",
            """{"customer":["name","phone","email"],"product":["description","category","room","seat"]}""",
            VerticalProfileLayouts.Ticket),
        Create(
            VerticalProfileIds.Beherbergung,
            "verticalProfiles.beherbergung.name",
            """{"tables":false,"kitchenDisplay":true,"patientRecord":false,"serviceDuration":false,"appointment":false,"imeiTracking":false,"routeTracking":false,"roomTracking":true}""",
            """{"customer":["name"],"product":["name","price"]}""",
            """{"customer":["phone","email","notes"],"product":["description","category"]}""",
            VerticalProfileLayouts.Rooms),
    ];

    private static VerticalProfile Create(
        string id,
        string name,
        string posFeatures,
        string requiredFields,
        string optionalFields,
        string posLayout) =>
        new()
        {
            Id = id,
            Name = name,
            PosFeatures = posFeatures,
            RequiredFields = requiredFields,
            OptionalFields = optionalFields,
            PosLayout = posLayout,
            IsActive = true,
            CreatedAtUtc = SeededAtUtc,
            UpdatedAtUtc = SeededAtUtc,
        };
}
