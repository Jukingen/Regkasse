namespace KasseAPI_Final.Authorization;

/// <summary>Code-defined industry onboarding blueprints (system-role slots; no matrix rewrite).</summary>
public static class IndustryPermissionTemplates
{
    public const string None = "none";
    public const string Restaurant = "restaurant";
    public const string Retail = "retail";
    public const string Hotel = "hotel";

    public sealed record Slot(
        string Key,
        string DisplayNameDe,
        string SystemRole,
        IReadOnlyList<string> RecommendedPackageSlugs,
        bool SeedStarterUser);

    public sealed record Template(
        string Id,
        string NameDe,
        string DescriptionDe,
        string? SuggestedDemoImportProfileId,
        IReadOnlyList<Slot> Slots);

    public static readonly IReadOnlyList<Template> All = new[]
    {
        new Template(
            Restaurant,
            "Restoran",
            "Restoran işletmeleri için varsayılan roller (sistem rolleri + starter kullanıcılar).",
            "restaurant-standard",
            new[]
            {
                new Slot("manager", "Restoran Müdürü", Roles.Manager, new[] { "user-management", "reporting" }, false),
                new Slot("cashier", "Kasiyer", Roles.Cashier, new[] { "cash-operations" }, true),
                new Slot("waiter", "Garson", Roles.Waiter, new[] { "cash-operations" }, true),
                new Slot("kitchen", "Mutfak", Roles.Kitchen, Array.Empty<string>(), true),
            }),
        new Template(
            Retail,
            "Perakende",
            "Perakende mağazaları için varsayılan roller.",
            null,
            new[]
            {
                new Slot("storeManager", "Mağaza Müdürü", Roles.Manager, new[] { "user-management", "reporting" }, false),
                new Slot("cashier", "Kasiyer", Roles.Cashier, new[] { "cash-operations" }, true),
                new Slot("inventory", "Envanter Yöneticisi", Roles.Accountant, new[] { "reporting" }, true),
            }),
        new Template(
            Hotel,
            "Otel",
            "Otel işletmeleri için varsayılan roller.",
            null,
            new[]
            {
                new Slot("frontDesk", "Ön Büro", Roles.Cashier, new[] { "cash-operations" }, true),
                new Slot("housekeeping", "Kat Hizmetleri", Roles.Waiter, Array.Empty<string>(), true),
                new Slot("fnb", "F&B Yöneticisi", Roles.Manager, new[] { "reporting", "cash-operations" }, false),
            }),
    };

    public static Template? Get(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || string.Equals(id, None, StringComparison.OrdinalIgnoreCase))
            return null;
        return All.FirstOrDefault(t => string.Equals(t.Id, id.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsValidId(string? id) =>
        string.IsNullOrWhiteSpace(id)
        || string.Equals(id, None, StringComparison.OrdinalIgnoreCase)
        || Get(id) != null;
}
