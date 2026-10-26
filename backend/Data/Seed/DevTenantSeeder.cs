using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Data.Seed;

/// <summary>
/// Additive Development catalog for the <c>dev</c> tenant so each vertical profile
/// has a few sellable products, named customers, and an open register shift.
/// Does not create fiscal receipts or change an existing profile assignment.
/// </summary>
public static class DevTenantSeeder
{
    public const string RegisterNumber = "KASSE-001";
    public const string CreatedBy = "dev-tenant-seed";

    public static IReadOnlyList<DevTenantProfileCatalog> Catalog { get; } =
    [
        Profile(VerticalProfileIds.Gastronomy, "Gastronomie", RksvProductCategory.Food,
            ["Wiener Schnitzel", "Gulasch", "Cola", "Apfelstrudel"],
            ["Anna Berger", "Lukas Hofer"]),
        Profile(VerticalProfileIds.GastronomyTables, "Gastronomie Tische", RksvProductCategory.Food,
            ["Tagesmenü", "Suppe", "Mineralwasser", "Tiramisu"],
            ["Maria Tisch", "Paul Gast"]),
        Profile(VerticalProfileIds.Vet, "Tierarzt", RksvProductCategory.Other,
            ["Impfung", "Kastration", "Wurmkur", "Gesundheitscheck"],
            ["Familie Huber", "Tierheim Nord"]),
        Profile(VerticalProfileIds.Doctor, "Arzt", RksvProductCategory.Other,
            ["Ordination", "Blutabnahme", "Verband", "Beratung"],
            ["Eva Arzt", "Jonas Patient"]),
        Profile(VerticalProfileIds.HairSalon, "Friseur", RksvProductCategory.Other,
            ["Haarschnitt", "Färben", "Waschen", "Styling"],
            ["Sophie Schnitt", "Lara Farbe"]),
        Profile(VerticalProfileIds.MobileServices, "Mobile Dienste", RksvProductCategory.Other,
            ["Vor-Ort-Service", "Anfahrt", "Reparatur", "Inspektion"],
            ["Dienst Kunde", "Hausbesuch Eins"]),
        Profile(VerticalProfileIds.HandyShop, "Handyshop", RksvProductCategory.Other,
            ["Schutzfolie", "Ladekabel", "Hülle", "Displaytausch"],
            ["Handy Max", "IMEI Kunde"]),
        Profile(VerticalProfileIds.Taxi, "Taxi", RksvProductCategory.Other,
            ["Stadtfahrt", "Flughafenfahrt", "Nachtzuschlag", "Wartezeit"],
            ["Fahrgast Wien", "Nachtfahrt"]),
        Profile(VerticalProfileIds.TicketSales, "Ticketverkauf", RksvProductCategory.Other,
            ["Konzert A", "Konzert B", "Stehplatz", "VIP-Loge"],
            ["Konzertbesucher", "Abendkarte"]),
        Profile(VerticalProfileIds.Beherbergung, "Beherbergung", RksvProductCategory.Other,
            ["Zimmer Standard", "Zimmer Deluxe", "Frühstück", "Parkplatz"],
            ["Gast Zimmer", "Frühstück Gast"]),
        Profile(VerticalProfileIds.Retail, "Einzelhandel", RksvProductCategory.Other,
            ["T-Shirt", "Jeans", "Socken", "Gürtel"],
            ["Kunde Laden", "Stammkunde"]),
    ];

    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                row => row.Id == DemoTenantIds.Dev && row.Slug == "dev" && row.IsActive && row.DeletedAtUtc == null,
                cancellationToken)
            .ConfigureAwait(false);
        if (tenant is null)
            return;

        await TenantDemoSeeder.SeedTenantAsync(db, tenant.Id, cancellationToken).ConfigureAwait(false);
    }

    public static string Barcode(string profileId, int index) => $"DV-{profileId}-{index}";

    public static string CustomerNumber(string profileId, int index)
    {
        var compact = profileId.Replace("-", "", StringComparison.Ordinal);
        var stem = compact.Length <= 12 ? compact : compact[..12];
        return $"D-{stem}-{index}";
    }

    private static DevTenantProfileCatalog Profile(
        string profileId,
        string categoryName,
        RksvProductCategory fiscalCategory,
        string[] products,
        string[] customers) =>
        new(profileId, categoryName, fiscalCategory, products, customers);
}

public sealed record DevTenantProfileCatalog(
    string ProfileId,
    string CategoryName,
    RksvProductCategory FiscalCategory,
    IReadOnlyList<string> ProductNames,
    IReadOnlyList<string> CustomerNames);
