using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

public sealed class FinanzOnlineSessionOptions
{
    public const string SectionName = "FinanzOnline:Session";
    public bool UseSimulation { get; set; } = true;

    /// <summary>SOAP service endpoint (document/literal), e.g. https://finanzonline.bmf.gv.at/fonws/ws/session — must be set via configuration.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>XML namespace for session request/response elements (WSDL targetNamespace).</summary>
    public string SoapNamespace { get; set; } = "https://finanzonline.bmf.gv.at/fon/ws/session";

    public int RequestTimeoutSeconds { get; set; } = 20;
    public int CacheClockSkewSeconds { get; set; } = 30;
    public FinanzOnlineScopedCredential DefaultCredential { get; set; } = new();
    public FinanzOnlineScopedCredential[] ScopedCredentials { get; set; } = Array.Empty<FinanzOnlineScopedCredential>();
}

public sealed class FinanzOnlineScopedCredential
{
    public string? TenantId { get; set; }
    public string? BranchId { get; set; }
    public string? RegisterId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? TelematikId { get; set; }
    public string? HerstellerId { get; set; }
}

public sealed class FinanzOnlineCredentialResolutionResult
{
    public bool Success { get; set; }
    public FinanzOnlineCredentials Credentials { get; set; } = new();
    public FinanzOnlineSessionFailureKind FailureKind { get; set; } = FinanzOnlineSessionFailureKind.None;
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}

public interface IFinanzOnlineCredentialProvider
{
    Task<FinanzOnlineCredentialResolutionResult> ResolveAsync(
        FinanzOnlineIntegrationMode mode,
        FinanzOnlineScope scope,
        CancellationToken cancellationToken = default);
}

public sealed class OptionsFinanzOnlineCredentialProvider : IFinanzOnlineCredentialProvider
{
    private readonly IOptionsMonitor<FinanzOnlineSessionOptions> _options;
    private readonly IFinanzOnlineConnectivitySource _connectivitySource;

    public OptionsFinanzOnlineCredentialProvider(
        IOptionsMonitor<FinanzOnlineSessionOptions> options,
        IFinanzOnlineConnectivitySource connectivitySource)
    {
        _options = options;
        _connectivitySource = connectivitySource;
    }

    public async Task<FinanzOnlineCredentialResolutionResult> ResolveAsync(
        FinanzOnlineIntegrationMode mode,
        FinanzOnlineScope scope,
        CancellationToken cancellationToken = default)
    {
        var opts = _options.CurrentValue;
        var scoped = opts.ScopedCredentials.FirstOrDefault(c =>
            IsMatch(c.TenantId, scope.TenantId) &&
            IsMatch(c.BranchId, scope.BranchId) &&
            IsMatch(c.RegisterId, scope.RegisterId));

        var credential = scoped ?? opts.DefaultCredential;
        var company = await _connectivitySource.GetCompanySettingsSnapshotAsync(cancellationToken).ConfigureAwait(false);
        if (company != null)
        {
            if ((string.IsNullOrWhiteSpace(credential.Username) || string.IsNullOrWhiteSpace(credential.Password)) &&
                !string.IsNullOrWhiteSpace(company.Username) &&
                !string.IsNullOrWhiteSpace(company.Password))
            {
                credential = new FinanzOnlineScopedCredential
                {
                    TenantId = credential.TenantId,
                    BranchId = credential.BranchId,
                    RegisterId = credential.RegisterId,
                    Username = company.Username,
                    Password = company.Password,
                    TelematikId = string.IsNullOrWhiteSpace(credential.TelematikId) ? company.TelematikId : credential.TelematikId,
                    HerstellerId = string.IsNullOrWhiteSpace(credential.HerstellerId) ? company.HerstellerId : credential.HerstellerId
                };
            }
            else if (string.IsNullOrWhiteSpace(credential.TelematikId) || string.IsNullOrWhiteSpace(credential.HerstellerId))
            {
                credential = new FinanzOnlineScopedCredential
                {
                    TenantId = credential.TenantId,
                    BranchId = credential.BranchId,
                    RegisterId = credential.RegisterId,
                    Username = credential.Username,
                    Password = credential.Password,
                    TelematikId = string.IsNullOrWhiteSpace(credential.TelematikId) ? company.TelematikId : credential.TelematikId,
                    HerstellerId = string.IsNullOrWhiteSpace(credential.HerstellerId) ? company.HerstellerId : credential.HerstellerId
                };
            }
        }

        if (string.IsNullOrWhiteSpace(credential.Username) || string.IsNullOrWhiteSpace(credential.Password))
        {
            return new FinanzOnlineCredentialResolutionResult
            {
                Success = false,
                FailureKind = FinanzOnlineSessionFailureKind.Permanent,
                ErrorCode = "CREDENTIALS_NOT_CONFIGURED",
                ErrorMessage = "Session credentials are not configured."
            };
        }

        if (string.IsNullOrWhiteSpace(credential.TelematikId) || string.IsNullOrWhiteSpace(credential.HerstellerId))
        {
            return new FinanzOnlineCredentialResolutionResult
            {
                Success = false,
                FailureKind = FinanzOnlineSessionFailureKind.Permanent,
                ErrorCode = "SOAP_PARTICIPANT_IDS_NOT_CONFIGURED",
                ErrorMessage = "FinanzOnline SOAP session requires TelematikId (tid) and HerstellerId (herstellerid)."
            };
        }

        return new FinanzOnlineCredentialResolutionResult
        {
            Success = true,
            Credentials = new FinanzOnlineCredentials
            {
                Username = credential.Username,
                Password = credential.Password,
                TelematikId = credential.TelematikId,
                HerstellerId = credential.HerstellerId
            }
        };
    }

    private static bool IsMatch(string? configured, string? requested)
    {
        if (string.IsNullOrWhiteSpace(configured))
            return true;
        return string.Equals(configured, requested, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class FinanzOnlineSessionTransportRequest
{
    public FinanzOnlineIntegrationMode Mode { get; set; } = FinanzOnlineIntegrationMode.TEST;
    public FinanzOnlineScope Scope { get; set; } = new();
    public FinanzOnlineCorrelationContext Correlation { get; set; } = new();
    public FinanzOnlineCredentials Credentials { get; set; } = new();
    public string? SessionToken { get; set; }
}

public sealed class FinanzOnlineSessionTransportResponse
{
    public bool Success { get; set; }
    public HttpStatusCode? StatusCode { get; set; }
    public string? SessionToken { get; set; }
    public DateTimeOffset? ExpiresAtUtc { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsMalformedResponse { get; set; }
}

public interface IFinanzOnlineSessionTransport
{
    Task<FinanzOnlineSessionTransportResponse> LoginAsync(FinanzOnlineSessionTransportRequest request, CancellationToken cancellationToken = default);
    Task<FinanzOnlineSessionTransportResponse> LogoutAsync(FinanzOnlineSessionTransportRequest request, CancellationToken cancellationToken = default);
}

public sealed class CachedFinanzOnlineSessionClient : IFinanzOnlineSessionClient
{
    private readonly IFinanzOnlineCredentialProvider _credentialProvider;
    private readonly IFinanzOnlineSessionTransport _transport;
    private readonly IOptionsMonitor<FinanzOnlineSessionOptions> _options;
    private readonly ILogger<CachedFinanzOnlineSessionClient> _logger;
    private readonly ConcurrentDictionary<string, (string Token, DateTimeOffset ExpiresAtUtc)> _cache = new();

    public CachedFinanzOnlineSessionClient(
        IFinanzOnlineCredentialProvider credentialProvider,
        IFinanzOnlineSessionTransport transport,
        IOptionsMonitor<FinanzOnlineSessionOptions> options,
        ILogger<CachedFinanzOnlineSessionClient> logger)
    {
        _credentialProvider = credentialProvider;
        _transport = transport;
        _options = options;
        _logger = logger;
    }

    public async Task<FinanzOnlineSessionLoginResponse> LoginAsync(FinanzOnlineSessionLoginRequest request, CancellationToken cancellationToken = default)
    {
        var credentials = await _credentialProvider.ResolveAsync(request.Mode, request.Scope, cancellationToken).ConfigureAwait(false);
        if (!credentials.Success)
        {
            return new FinanzOnlineSessionLoginResponse
            {
                Success = false,
                ErrorCode = credentials.ErrorCode,
                ErrorMessage = credentials.ErrorMessage
            };
        }

        var transportRequest = new FinanzOnlineSessionTransportRequest
        {
            Mode = request.Mode,
            Scope = request.Scope,
            Correlation = request.Correlation,
            Credentials = credentials.Credentials
        };

        try
        {
            var response = await _transport.LoginAsync(transportRequest, cancellationToken).ConfigureAwait(false);
            if (!response.Success || string.IsNullOrWhiteSpace(response.SessionToken))
            {
                return new FinanzOnlineSessionLoginResponse
                {
                    Success = false,
                    ErrorCode = response.ErrorCode ?? "LOGIN_FAILED",
                    ErrorMessage = response.ErrorMessage ?? "Session login failed."
                };
            }

            return new FinanzOnlineSessionLoginResponse
            {
                Success = true,
                SessionToken = response.SessionToken,
                ExpiresAtUtc = response.ExpiresAtUtc ?? DateTimeOffset.UtcNow.AddMinutes(15)
            };
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is OperationCanceledException)
        {
            _logger.LogWarning("FinanzOnline session login transient failure CorrelationId={CorrelationId}",
                request.Correlation.CorrelationId);
            return new FinanzOnlineSessionLoginResponse
            {
                Success = false,
                ErrorCode = "TRANSIENT_NETWORK_FAILURE",
                ErrorMessage = "Transient network failure."
            };
        }
    }

    public async Task<FinanzOnlineSessionLogoutResponse> LogoutAsync(FinanzOnlineSessionLogoutRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var resolved = await _credentialProvider.ResolveAsync(request.Mode, request.Scope, cancellationToken).ConfigureAwait(false);
            var response = await _transport.LogoutAsync(new FinanzOnlineSessionTransportRequest
            {
                Mode = request.Mode,
                Scope = request.Scope,
                Correlation = request.Correlation,
                SessionToken = request.SessionToken,
                Credentials = resolved.Success ? resolved.Credentials : new FinanzOnlineCredentials()
            }, cancellationToken).ConfigureAwait(false);

            return new FinanzOnlineSessionLogoutResponse
            {
                Success = response.Success,
                ErrorCode = response.ErrorCode,
                ErrorMessage = response.ErrorMessage
            };
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is OperationCanceledException)
        {
            _logger.LogWarning("FinanzOnline session logout transient failure CorrelationId={CorrelationId}",
                request.Correlation.CorrelationId);
            return new FinanzOnlineSessionLogoutResponse
            {
                Success = false,
                ErrorCode = "TRANSIENT_NETWORK_FAILURE",
                ErrorMessage = "Transient network failure."
            };
        }
    }

    public async Task<FinanzOnlineSessionAccessResult> GetValidSessionAsync(FinanzOnlineSessionLoginRequest request, CancellationToken cancellationToken = default)
    {
        var key = BuildCacheKey(request.Mode, request.Scope);
        if (_cache.TryGetValue(key, out var cached) && cached.ExpiresAtUtc > DateTimeOffset.UtcNow.AddSeconds(_options.CurrentValue.CacheClockSkewSeconds))
        {
            return new FinanzOnlineSessionAccessResult
            {
                Success = true,
                SessionToken = cached.Token,
                ExpiresAtUtc = cached.ExpiresAtUtc,
                FromCache = true
            };
        }

        var login = await LoginAsync(request, cancellationToken).ConfigureAwait(false);
        if (!login.Success || string.IsNullOrWhiteSpace(login.SessionToken))
        {
            var failureKind = ResolveFailureKind(login.ErrorCode);
            return new FinanzOnlineSessionAccessResult
            {
                Success = false,
                FailureKind = failureKind,
                ErrorCode = login.ErrorCode,
                ErrorMessage = login.ErrorMessage
            };
        }

        var expires = login.ExpiresAtUtc ?? DateTimeOffset.UtcNow.AddMinutes(15);
        _cache[key] = (login.SessionToken, expires);
        _logger.LogInformation("FinanzOnline session established Mode={Mode} Tenant={Tenant} Branch={Branch} Register={Register} CorrelationId={CorrelationId}",
            request.Mode, request.Scope.TenantId ?? "", request.Scope.BranchId ?? "", request.Scope.RegisterId, request.Correlation.CorrelationId);

        return new FinanzOnlineSessionAccessResult
        {
            Success = true,
            SessionToken = login.SessionToken,
            ExpiresAtUtc = expires,
            FromCache = false
        };
    }

    public async Task InvalidateSessionAsync(FinanzOnlineSessionLogoutRequest request, CancellationToken cancellationToken = default)
    {
        var key = BuildCacheKey(request.Mode, request.Scope);
        _cache.TryRemove(key, out _);
        await LogoutAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static string BuildCacheKey(FinanzOnlineIntegrationMode mode, FinanzOnlineScope scope)
    {
        return $"{mode}:{scope.TenantId}:{scope.BranchId}:{scope.RegisterId}";
    }

    private static FinanzOnlineSessionFailureKind ResolveFailureKind(string? errorCode)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
            return FinanzOnlineSessionFailureKind.Retryable;
        if (errorCode.StartsWith("FON_RC_", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(errorCode.AsSpan("FON_RC_".Length), out var rc))
        {
            if (rc == -2 || rc == -3)
                return FinanzOnlineSessionFailureKind.Retryable;
            return FinanzOnlineSessionFailureKind.Permanent;
        }
        if (errorCode.Contains("401", StringComparison.OrdinalIgnoreCase) ||
            errorCode.Contains("403", StringComparison.OrdinalIgnoreCase) ||
            errorCode.Contains("CREDENTIALS", StringComparison.OrdinalIgnoreCase) ||
            errorCode.Contains("SOAP_PARTICIPANT", StringComparison.OrdinalIgnoreCase) ||
            errorCode.Contains("MALFORMED", StringComparison.OrdinalIgnoreCase))
            return FinanzOnlineSessionFailureKind.Permanent;
        if (errorCode.Contains("TRANSIENT", StringComparison.OrdinalIgnoreCase) ||
            errorCode.Contains("SOAP_FAULT", StringComparison.OrdinalIgnoreCase) ||
            errorCode.Contains("HTTP_5", StringComparison.OrdinalIgnoreCase) ||
            errorCode.Contains("HTTP_429", StringComparison.OrdinalIgnoreCase))
            return FinanzOnlineSessionFailureKind.Retryable;
        return FinanzOnlineSessionFailureKind.Permanent;
    }
}
