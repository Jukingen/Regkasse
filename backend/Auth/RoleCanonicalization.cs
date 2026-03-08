namespace KasseAPI_Final.Auth;

/// <summary>
/// Eski (legacy) rol adlarını kanonik role eşler. Migration sonrası alias kaldırılacak (dökümante).
/// </summary>
public static class RoleCanonicalization
{
    /// <summary>Kanonik rol adları (policy ve token için tek kaynak).</summary>
    public static class Canonical
    {
        public const string SuperAdmin = "SuperAdmin";
        public const string Admin = "Admin";
        public const string BranchManager = "BranchManager";
        public const string Auditor = "Auditor";
        public const string Cashier = "Cashier";
        public const string Kellner = "Kellner";
        public const string Demo = "Demo";
    }

    /// <summary>
    /// Legacy rol -> kanonik rol eşlemesi. Geçici: migration milestone sonrası kaldırılacak.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> LegacyToCanonical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "Administrator", Canonical.Admin },
    };

    /// <summary>
    /// Verilen rolü kanonik role çevirir. Bilinmeyen/legacy ise kanonik döner; yoksa aynen döner.
    /// </summary>
    public static string GetCanonicalRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return string.Empty;
        return LegacyToCanonical.TryGetValue(role.Trim(), out var canonical) ? canonical : role.Trim();
    }

    /// <summary>Şu an desteklenen legacy alias'lar (test ve migration dokümantasyonu için).</summary>
    public static IReadOnlyCollection<string> GetLegacyAliases() => LegacyToCanonical.Keys.ToList();
}
