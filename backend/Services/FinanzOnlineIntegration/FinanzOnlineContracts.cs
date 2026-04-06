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
    /// <summary>SOAP benid (FinanzOnline Benutzer-ID); maps from configuration username.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>SOAP pin (user PIN); maps from configuration password.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>SOAP tid (participant / Telematik-ID).</summary>
    public string? TelematikId { get; set; }

    /// <summary>SOAP herstellerid (manufacturer / participant UID per session schema).</summary>
    public string? HerstellerId { get; set; }
}

public sealed class FinanzOnlineSessionLoginRequest
{
    public FinanzOnlineIntegrationMode Mode { get; set; } = FinanzOnlineIntegrationMode.TEST;
    public FinanzOnlineScope Scope { get; set; } = new();
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

public enum FinanzOnlineSessionFailureKind
{
    None = 0,
    Retryable = 1,
    Permanent = 2
}

public sealed class FinanzOnlineSessionAccessResult
{
    public bool Success { get; set; }
    public string? SessionToken { get; set; }
    public DateTimeOffset? ExpiresAtUtc { get; set; }
    public bool FromCache { get; set; }
    public FinanzOnlineSessionFailureKind FailureKind { get; set; } = FinanzOnlineSessionFailureKind.None;
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}

public enum FinanzOnlineSubmissionKind
{
    Register = 0,
    SignatureUnit = 1
}

/// <summary>
/// RKDB inner payload: <c>belegpruefung</c> (TEST-only subset in this codebase). Not a legal compliance guarantee.
/// </summary>
public sealed class FinanzOnlineRkdbBelegpruefungCommand
{
    /// <summary>DEP line string (BMF pattern / length constraints).</summary>
    public string Beleg { get; set; } = string.Empty;

    public int PaketNr { get; set; } = 1;
    public int SatzNr { get; set; } = 1;

    /// <summary>UTC creation time for <c>ts_erstellung</c>; defaults at mapping time if null.</summary>
    public DateTimeOffset? TsErstellungUtc { get; set; }

    /// <summary>Optional; max length enforced by validator.</summary>
    public string? Kundeninfo { get; set; }
}

public sealed class FinanzOnlineRegisterSubmissionRequest
{
    public FinanzOnlineIntegrationMode Mode { get; set; } = FinanzOnlineIntegrationMode.TEST;
    public FinanzOnlineScope Scope { get; set; } = new();
    public FinanzOnlineCorrelationContext Correlation { get; set; } = new();
    public FinanzOnlineCredentials Credentials { get; set; } = new();
    public FinanzOnlineSubmissionKind SubmissionKind { get; set; } = FinanzOnlineSubmissionKind.Register;
    public string PayloadJson { get; set; } = "{}";

    /// <summary>When set (TEST), maps to <see cref="FinanzOnlineMappedCommand.RkdbPayloadXml"/> for rkdb SOAP.</summary>
    public FinanzOnlineRkdbBelegpruefungCommand? RkdbBelegpruefung { get; set; }
}

public sealed class FinanzOnlineRegisterSubmissionResponse
{
    public bool Success { get; set; }
    public string? TransmissionId { get; set; }
    public string? ReferenceId { get; set; }
    public string? Status { get; set; }
    public string? ProtocolCode { get; set; }

    /// <summary>Optional human-readable summary persisted on outbox after submit (simulation may populate).</summary>
    public string? ProtocolSummary { get; set; }

    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>From rkdbResponse ts_erstellung; required for status_kasse protocol query.</summary>
    public string? RkdbTsErstellungIso { get; set; }

    /// <summary>From first result/satznr; used for status_kasse reconciliation.</summary>
    public int? RkdbSatzNr { get; set; }
}

public sealed class FinanzOnlineTransmissionStatusQueryRequest
{
    public FinanzOnlineIntegrationMode Mode { get; set; } = FinanzOnlineIntegrationMode.TEST;
    public FinanzOnlineScope Scope { get; set; } = new();
    public FinanzOnlineCorrelationContext Correlation { get; set; } = new();
    public FinanzOnlineCredentials Credentials { get; set; } = new();
    public string TransmissionId { get; set; } = string.Empty;

    /// <summary>Original submission ts_erstellung (ISO); required for real SOAP status_kasse query.</summary>
    public string? RkdbTsErstellungIso { get; set; }

    /// <summary>Satznummer from submission; defaults to 1.</summary>
    public int RkdbSatzNr { get; set; } = 1;

    /// <summary>Optional FASTNR (9 digits) for status_kasse when available.</summary>
    public string? ExternalReferenceFastNr { get; set; }
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

    /// <summary>
    /// Optional inner XML for the rkdb SOAP element (regKasse.xsd). If null, TransportPayload is used when it is well-formed XML for &lt;rkdb&gt; content.
    /// </summary>
    public string? RkdbPayloadXml { get; set; }

    /// <summary>Set when RKDB XML could not be built from the request (validation); submission layer returns a clear error before transport.</summary>
    public string? RkdbBuildError { get; set; }

    /// <summary>Machine code for <see cref="RkdbBuildError"/> (e.g. RKDB_COMMAND_INVALID, RKDB_XML_STRUCTURE_INVALID).</summary>
    public string? RkdbValidationErrorCode { get; set; }
}

public interface IFinanzOnlineSessionClient
{
    Task<FinanzOnlineSessionLoginResponse> LoginAsync(FinanzOnlineSessionLoginRequest request, CancellationToken cancellationToken = default);
    Task<FinanzOnlineSessionLogoutResponse> LogoutAsync(FinanzOnlineSessionLogoutRequest request, CancellationToken cancellationToken = default);
    Task<FinanzOnlineSessionAccessResult> GetValidSessionAsync(FinanzOnlineSessionLoginRequest request, CancellationToken cancellationToken = default);
    Task InvalidateSessionAsync(FinanzOnlineSessionLogoutRequest request, CancellationToken cancellationToken = default);
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
