using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.AdminTenants;

/// <summary>Links mandant rows to <c>issued_licenses</c> via customer name marker <c>[tenant:guid]</c>.</summary>
internal static class TenantLicenseLink
{
    public static string Marker(Guid tenantId) => $"[tenant:{tenantId:D}]";

    public static string BuildCustomerName(Tenant tenant)
    {
        var name = tenant.Name.Trim();
        var marker = Marker(tenant.Id);
        var combined = $"{name} {marker}";
        return combined.Length <= 256 ? combined : name.Length + 1 + marker.Length <= 256
            ? $"{name[..(256 - marker.Length - 1)].TrimEnd()} {marker}"
            : marker;
    }

    public static bool CustomerNameReferencesTenant(string customerName, Guid tenantId) =>
        customerName.Contains(Marker(tenantId), StringComparison.OrdinalIgnoreCase);
}
