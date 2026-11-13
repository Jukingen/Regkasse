using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.DigitalServices;

/// <summary>Read-only digital service price catalog (static; not license_sales).</summary>
public sealed class DigitalServicePricingService : IDigitalServicePricingService
{
    public IReadOnlyList<ServicePricing> GetPricing(string? type = null)
    {
        if (string.IsNullOrWhiteSpace(type))
            return ServicePricingData.GetPricing();
        return ServicePricingData.GetByType(type.Trim());
    }

    public ServicePricing? GetByServiceId(string serviceId) =>
        ServicePricingData.GetByServiceId(serviceId);
}
