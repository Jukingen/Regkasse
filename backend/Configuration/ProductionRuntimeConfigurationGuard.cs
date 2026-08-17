using KasseAPI_Final.Services.Tse;

namespace KasseAPI_Final.Configuration;

/// <summary>
/// Production fail-closed checks for non-fiscal host settings (CSRF, 2FA, rate limit, Redis, backup adapter, payment gateway).
/// Fiscal TSE/FON simulation is already locked by <see cref="TseProductionOptionsValidator"/>.
/// </summary>
public static class ProductionRuntimeConfigurationGuard
{
    public const string CsrfMustBeEnabled = "CSRF must be enabled in Production";
    public const string FonSimulationNotAllowed = "FON simulation not allowed in Production";
    public const string BackupMustUsePgDump = "Backup must use PgDump in Production";
    public const string PaymentGatewayMockNotAllowed = "PaymentGateway Mock not allowed in Production";
    public const string TwoFactorMustBeEnabled = "TwoFactorAuth must be enabled in Production";
    public const string RateLimitingMustBeEnabled = "RateLimiting must be enabled in Production";
    public const string RedisMustBeEnabled = "Redis must be enabled in Production";

    /// <summary>
    /// Collects Production violations. Empty when the host is not Production or when config is safe.
    /// CSRF that is still false is included; callers that auto-enable CSRF should pass
    /// <paramref name="csrfForceEnabled"/>.
    /// </summary>
    public static IReadOnlyList<string> CollectViolations(
        IConfiguration configuration,
        bool csrfForceEnabled = false)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var errors = new List<string>();

        var csrf = Bind<CsrfOptions>(configuration, CsrfOptions.SectionName);
        if (!csrfForceEnabled && !csrf.Enabled)
            errors.Add(CsrfMustBeEnabled);

        if (TseFiscalConfigLockEvaluator.IsFinanzOnlineSimulated(configuration))
            errors.Add(FonSimulationNotAllowed);

        var backup = Bind<BackupOptions>(configuration, BackupOptions.SectionName);
        if (backup.ExecutionAdapterKind != BackupExecutionAdapterKind.PgDump)
            errors.Add(BackupMustUsePgDump);

        var gateway = Bind<PaymentGatewayOptions>(configuration, PaymentGatewayOptions.SectionName);
        if (gateway.IsMockProvider)
            errors.Add(PaymentGatewayMockNotAllowed);

        var twoFactor = Bind<TwoFactorAuthOptions>(configuration, TwoFactorAuthOptions.SectionName);
        if (!twoFactor.Enabled)
            errors.Add(TwoFactorMustBeEnabled);

        var rateLimiting = Bind<RateLimitingOptions>(configuration, RateLimitingOptions.SectionName);
        if (!rateLimiting.Enabled)
            errors.Add(RateLimitingMustBeEnabled);

        var redis = Bind<RedisOptions>(configuration, RedisOptions.SectionName);
        if (!redis.Enabled || string.IsNullOrWhiteSpace(redis.ConnectionString))
            errors.Add(RedisMustBeEnabled);

        return errors;
    }

    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> when Production config is unsafe.
    /// No-op outside Production and during OpenAPI export.
    /// </summary>
    public static void ThrowIfUnsafe(IHostEnvironment environment, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(configuration);

        if (!environment.IsProduction())
            return;

        var errors = CollectViolations(configuration, csrfForceEnabled: false);
        if (errors.Count == 0)
            return;

        throw new InvalidOperationException(
            "Unsafe Production configuration: " + string.Join(" ", errors));
    }

    private static T Bind<T>(IConfiguration configuration, string sectionName)
        where T : class, new()
    {
        var instance = new T();
        configuration.GetSection(sectionName).Bind(instance);
        return instance;
    }
}
