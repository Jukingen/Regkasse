using KasseAPI_Final.Configuration;

namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

/// <summary>
/// Super Admin snapshot for optional FinanzOnline runtime flags that otherwise live in appsettings.
/// Secrets, SOAP URLs, and Production Mode/cutover stay in deployment config.
/// </summary>
public sealed class FinanzOnlineRuntimeOverlay
{
    public const string SettingsKey = "FinanzOnline:Runtime";

    /// <summary>Applied to Session, Registrierkassen, and TransmissionQuery together (mixed layers are unsupported).</summary>
    public bool? UseSimulation { get; set; }
    public bool? EnableRealTestSubmission { get; set; }
    public bool? EnableRealTestQuery { get; set; }
    public bool? RetryJobEnabled { get; set; }
    public int? RetryIntervalSeconds { get; set; }
    public int? RetryMaxRetryCount { get; set; }
    public int? RetryBaseDelaySeconds { get; set; }
    public int? RetryBackoffCapSeconds { get; set; }
    public int? RetryBatchSize { get; set; }

    public bool HasAny =>
        UseSimulation is not null
        || EnableRealTestSubmission is not null
        || EnableRealTestQuery is not null
        || RetryJobEnabled is not null
        || RetryIntervalSeconds is not null
        || RetryMaxRetryCount is not null
        || RetryBaseDelaySeconds is not null
        || RetryBackoffCapSeconds is not null
        || RetryBatchSize is not null;

    public bool IsComplete =>
        UseSimulation is not null
        && EnableRealTestSubmission is not null
        && EnableRealTestQuery is not null
        && RetryJobEnabled is not null
        && RetryIntervalSeconds is not null
        && RetryMaxRetryCount is not null
        && RetryBaseDelaySeconds is not null
        && RetryBackoffCapSeconds is not null
        && RetryBatchSize is not null;
}

public static class FinanzOnlineRuntimeLimits
{
    public static readonly FinanzOnlineOutboxWorkerRange RetryIntervalSeconds = new()
    {
        Min = 30,
        Max = 3600,
        Values = [30, 60, 120, 300, 600],
    };

    public static readonly FinanzOnlineOutboxWorkerRange RetryMaxRetryCount = new()
    {
        Min = 1,
        Max = 5,
        Values = [1, 2, 3, 4, 5],
    };

    public static readonly FinanzOnlineOutboxWorkerRange RetryBaseDelaySeconds = new()
    {
        Min = 10,
        Max = 3600,
        Values = [30, 60, 120, 300, 600],
    };

    public static readonly FinanzOnlineOutboxWorkerRange RetryBackoffCapSeconds = new()
    {
        Min = 60,
        Max = 86400,
        Values = [300, 600, 1800, 3600],
    };

    public static readonly FinanzOnlineOutboxWorkerRange RetryBatchSize = new()
    {
        Min = 1,
        Max = 200,
        Values = [10, 25, 50, 100],
    };
}

public static class FinanzOnlineRuntimeOptionExtensions
{
    public static FinanzOnlineSessionOptions WithRuntime(
        this FinanzOnlineSessionOptions source,
        FinanzOnlineRuntimeOverlay? overlay,
        bool isProduction)
    {
        var sim = ResolveSimulation(source.UseSimulation, overlay, isProduction);
        if (sim == source.UseSimulation)
            return source;
        return new FinanzOnlineSessionOptions
        {
            UseSimulation = sim,
            BaseUrl = source.BaseUrl,
            SoapNamespace = source.SoapNamespace,
            RequestTimeoutSeconds = source.RequestTimeoutSeconds,
            CacheClockSkewSeconds = source.CacheClockSkewSeconds,
            DefaultCredential = source.DefaultCredential,
            ScopedCredentials = source.ScopedCredentials,
        };
    }

    public static FinanzOnlineRegistrierkassenOptions WithRuntime(
        this FinanzOnlineRegistrierkassenOptions source,
        FinanzOnlineRuntimeOverlay? overlay,
        bool isProduction)
    {
        var sim = ResolveSimulation(source.UseSimulation, overlay, isProduction);
        var realTest = isProduction ? false : overlay?.EnableRealTestSubmission ?? source.EnableRealTestSubmission;
        if (sim)
            realTest = false;
        if (sim == source.UseSimulation && realTest == source.EnableRealTestSubmission)
            return source;
        return new FinanzOnlineRegistrierkassenOptions
        {
            UseSimulation = sim,
            EnableRealTestSubmission = realTest,
            BaseUrl = source.BaseUrl,
            SoapNamespace = source.SoapNamespace,
            SoapAction = source.SoapAction,
            RequestTimeoutSeconds = source.RequestTimeoutSeconds,
        };
    }

    public static FinanzOnlineTransmissionQueryOptions WithRuntime(
        this FinanzOnlineTransmissionQueryOptions source,
        FinanzOnlineRuntimeOverlay? overlay,
        bool isProduction)
    {
        var sim = ResolveSimulation(source.UseSimulation, overlay, isProduction);
        var realQuery = isProduction ? false : overlay?.EnableRealTestQuery ?? source.EnableRealTestQuery;
        if (sim)
            realQuery = false;
        if (sim == source.UseSimulation && realQuery == source.EnableRealTestQuery)
            return source;
        return new FinanzOnlineTransmissionQueryOptions
        {
            UseSimulation = sim,
            EnableRealTestQuery = realQuery,
            BaseUrl = source.BaseUrl,
            QueryPath = source.QueryPath,
            RequestTimeoutSeconds = source.RequestTimeoutSeconds,
        };
    }

    public static FinanzOnlineRetryJobOptions WithRuntime(
        this FinanzOnlineRetryJobOptions source,
        FinanzOnlineRuntimeOverlay? overlay)
    {
        if (overlay is null || !overlay.HasAny)
            return source;
        return new FinanzOnlineRetryJobOptions
        {
            Enabled = overlay.RetryJobEnabled ?? source.Enabled,
            Interval = overlay.RetryIntervalSeconds is int s
                ? TimeSpan.FromSeconds(Math.Max(1, s))
                : source.Interval,
            MaxRetryCount = overlay.RetryMaxRetryCount ?? source.MaxRetryCount,
            BaseDelaySeconds = overlay.RetryBaseDelaySeconds ?? source.BaseDelaySeconds,
            BackoffCapSeconds = overlay.RetryBackoffCapSeconds ?? source.BackoffCapSeconds,
            BatchSize = overlay.RetryBatchSize ?? source.BatchSize,
            AlertFailedThreshold = source.AlertFailedThreshold,
            RegisterRepeatedFailureThreshold = source.RegisterRepeatedFailureThreshold,
        };
    }

    private static bool ResolveSimulation(bool config, FinanzOnlineRuntimeOverlay? overlay, bool isProduction)
    {
        if (isProduction)
            return false;
        return overlay?.UseSimulation ?? config;
    }
}
