namespace KasseAPI_Final.Logging;

/// <summary>Shared keys for <see cref="Microsoft.Extensions.Logging.ILogger.BeginScope{TState}"/> request enrichment.</summary>
public static class LogContextKeys
{
    public const string Tenant = "Tenant";
    public const string TenantId = "TenantId";
    public const string User = "User";
    public const string UserId = "UserId";
    public const string Role = "Role";
    public const string CorrelationId = "CorrelationId";
}
