using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.DigitalServices;

public interface IDigitalServicePricingService
{
    IReadOnlyList<ServicePricing> GetPricing(string? type = null);
    ServicePricing? GetByServiceId(string serviceId);
}
