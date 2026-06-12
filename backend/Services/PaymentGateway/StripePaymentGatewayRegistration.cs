using KasseAPI_Final.Configuration;
using Stripe;

namespace KasseAPI_Final.Services.PaymentGateway;

internal static class StripePaymentGatewayRegistration
{
    public static IServiceCollection AddStripePaymentGateway(this IServiceCollection services)
    {
        services.AddSingleton<IStripeClient>(sp =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PaymentGatewayOptions>>().Value;
            var apiKey = opts.ResolveStripeApiKey();
            return new StripeClient(string.IsNullOrWhiteSpace(apiKey) ? "sk_not_configured" : apiKey);
            return new StripeClient(apiKey);
        });

        return services;
    }
}
