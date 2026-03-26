using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

public enum FinanzOnlineIntegrationMode
{
    TEST = 0,
    PROD = 1
}

public sealed class FinanzOnlineScope
{
    public string? TenantId { get; set; }
    public string? BranchId { get; set; }
    public string RegisterId { get; set; } = string.Empty;
}

public sealed class FinanzOnlineCorrelationContext
{
    public string BusinessKey { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string? CausationId { get; set; }
}

public sealed class FinanzOnlineCredentials
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class FinanzOnlineSessionLoginRequest
{
    public FinanzOnlineIntegrationMode Mode { get; set; } = FinanzOnlineIntegrationMode.TEST;
    public FinanzOnlineScope Scope { get; set; } = new();
    public FinanzOnlineCredentials Credentials { get; set; } = new();
    public FinanzOnlineCorrelationContext Correlation { get; set; } = new();
}

public sealed class FinanzOnlineSessionLoginResponse
{
    public bool Success { get; set; }
    public string? SessionToken { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset? ExpiresAtUtc { get; set; }
}

public sealed class FinanzOnlineSessionLogoutRequest
{
    public FinanzOnlineIntegrationMode Mode { get; set; } = FinanzOnlineIntegrationMode.TEST;
    public FinanzOnlineScope Scope { get; set; } = new();
    public string SessionToken { get; set; } = string.Empty;
    public FinanzOnlineCorrelationContext Correlation { get; set; } = new();
}

public sealed class FinanzOnlineSessionLogoutResponse
{
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}

public enum FinanzOnlineSubmissionKind
{
    Register = 0,
    SignatureUnit = 1
}

public sealed class FinanzOnlineRegisterSubmissionRequest
{
    public FinanzOnlineIntegrationMode Mode { get; set; } = FinanzOnlineIntegrationMode.TEST;
    public FinanzOnlineScope Scope { get; set; } = new();
    public FinanzOnlineCorrelationContext Correlation { get; set; } = new();
    public FinanzOnlineCredentials Credentials { get; set; } = new();
    public FinanzOnlineSubmissionKind SubmissionKind { get; set; } = FinanzOnlineSubmissionKind.Register;
    public string PayloadJson { get; set; } = "{}";
}

public sealed class FinanzOnlineRegisterSubmissionResponse
{
    public bool Success { get; set; }
    public string? TransmissionId { get; set; }
    public string? ReferenceId { get; set; }
    public string? Status { get; set; }
    public string? ProtocolCode { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class FinanzOnlineTransmissionStatusQueryRequest
{
    public FinanzOnlineIntegrationMode Mode { get; set; } = FinanzOnlineIntegrationMode.TEST;
    public FinanzOnlineScope Scope { get; set; } = new();
    public FinanzOnlineCorrelationContext Correlation { get; set; } = new();
    public FinanzOnlineCredentials Credentials { get; set; } = new();
    public string TransmissionId { get; set; } = string.Empty;
}

public sealed class FinanzOnlineTransmissionProtocolEntry
{
    public DateTimeOffset TimestampUtc { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class FinanzOnlineTransmissionStatusQueryResponse
{
    public bool Success { get; set; }
    public string? TransmissionId { get; set; }
    public string? Status { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public IReadOnlyList<FinanzOnlineTransmissionProtocolEntry> Protocol { get; set; } = Array.Empty<FinanzOnlineTransmissionProtocolEntry>();
}

public sealed class FinanzOnlineMappedCommand
{
    public string OperationName { get; set; } = string.Empty;
    public string TransportPayload { get; set; } = string.Empty;
    public string TransportFormat { get; set; } = "application/json";
    public IReadOnlyDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
}

public interface IFinanzOnlineSessionClient
{
    Task<FinanzOnlineSessionLoginResponse> LoginAsync(FinanzOnlineSessionLoginRequest request, CancellationToken cancellationToken = default);
    Task<FinanzOnlineSessionLogoutResponse> LogoutAsync(FinanzOnlineSessionLogoutRequest request, CancellationToken cancellationToken = default);
}

public interface IFinanzOnlineRegistrierkassenClient
{
    Task<FinanzOnlineRegisterSubmissionResponse> SubmitAsync(
        FinanzOnlineRegisterSubmissionRequest request,
        FinanzOnlineMappedCommand mappedCommand,
        string sessionToken,
        CancellationToken cancellationToken = default);
}

public interface IFinanzOnlineTransmissionQueryClient
{
    Task<FinanzOnlineTransmissionStatusQueryResponse> QueryStatusAsync(FinanzOnlineTransmissionStatusQueryRequest request, CancellationToken cancellationToken = default);
}

public interface IFinanzOnlineCommandMapper
{
    FinanzOnlineMappedCommand MapRegisterSubmission(FinanzOnlineRegisterSubmissionRequest request);
    FinanzOnlineMappedCommand MapTransmissionStatusQuery(FinanzOnlineTransmissionStatusQueryRequest request);
}

public interface IFinanzOnlineSubmissionService
{
    Task<FinanzOnlineRegisterSubmissionResponse> SubmitAsync(FinanzOnlineRegisterSubmissionRequest request, CancellationToken cancellationToken = default);
}
