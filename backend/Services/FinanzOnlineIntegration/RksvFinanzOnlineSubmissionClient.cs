using System.Text.Json;
using KasseAPI_Final.Models;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

/// <summary>Stable error codes returned by <see cref="RksvFinanzOnlineSubmissionClient"/> (and consumed by outbox handlers).</summary>
public static class RksvFinanzOnlineSubmissionKnownErrorCodes
{
    public const string SubmissionDisabled = "RKS_SUBMISSION_DISABLED";

    public const string ConfigIncomplete = "RKS_SUBMISSION_CONFIG_INCOMPLETE";

    /// <summary>SOAP/RKSV FinanzOnline mapping is not wired in this build; no outbound HTTP is performed.</summary>
    public const string SoapTransportNotImplemented = "RKS_SOAP_TRANSPORT_NOT_IMPLEMENTED";
}

/// <summary>Target FinanzOnline/BMF deployment for RKSV submission (configuration only; does not imply legal completeness).</summary>
public enum RksvFinanzOnlineSubmissionDeploymentEnvironment
{
    Test = 0,
    Production = 1,
}

/// <summary>Request payload for RKSV Startbeleg/Jahresbeleg FinanzOnline submission (no credentials; caller supplies identifiers only).</summary>
public sealed class RksvFinanzOnlineSubmissionPayload
{
    /// <summary>Effective tenant id (string form, e.g. GUID N).</summary>
    public string? TenantId { get; set; }

    /// <summary>Optional company tax number (ATU…); not a secret.</summary>
    public string? CompanyTaxNumber { get; set; }

    public Guid CashRegisterId { get; set; }

    public string RegisterNumber { get; set; } = string.Empty;

    public string ReceiptNumber { get; set; } = string.Empty;

    /// <summary>RKSV machine-readable receipt / QR payload.</summary>
    public string QrPayload { get; set; } = string.Empty;

    public string? CertificateSerial { get; set; }

    public DateTimeOffset TimestampUtc { get; set; }
}

/// <summary>Outcome of an RKSV FinanzOnline submission attempt (no secrets).</summary>
public sealed class RksvFinanzOnlineSubmissionResult
{
    public bool Success { get; set; }

    public string? ExternalReference { get; set; }

    /// <summary>BMF-side verification state when known (e.g. Pending, Verified); fake client uses configured values.</summary>
    public string? VerificationStatus { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>Non-sensitive excerpt for audit (e.g. JSON envelope without credentials).</summary>
    public string? RawResponseSnapshot { get; set; }
}

/// <summary>
/// Abstraction for submitting RKSV special receipts to FinanzOnline.
/// Implementations may be in-memory fakes, guarded HTTP skeletons, or future SOAP clients; none of these alone constitute legal BMF compliance.
/// </summary>
public interface IRksvFinanzOnlineSubmissionClient
{
    Task<RksvFinanzOnlineSubmissionResult> SubmitStartbelegAsync(
        RksvFinanzOnlineSubmissionPayload payload,
        CancellationToken cancellationToken = default);

    Task<RksvFinanzOnlineSubmissionResult> SubmitJahresbelegAsync(
        RksvFinanzOnlineSubmissionPayload payload,
        CancellationToken cancellationToken = default);
}

public enum RksvFinanzOnlineSubmissionClientKind
{
    /// <summary>In-process fake for development and tests.</summary>
    Fake = 0,

    /// <summary>Reserved legacy placeholder; throws on submit.</summary>
    NotImplemented = 1,

    /// <summary>Guarded real/skeleton client (see <see cref="RksvFinanzOnlineSubmissionClient"/>); outbound SOAP not implemented in this repository revision.</summary>
    Real = 2,
}

/// <summary>Configuration for <see cref="IRksvFinanzOnlineSubmissionClient"/> binding (no secrets; credential material is referenced by name/key only).</summary>
public sealed class RksvFinanzOnlineSubmissionClientOptions
{
    public const string SectionName = "FinanzOnline:RksvSubmission";

    public RksvFinanzOnlineSubmissionClientKind ClientKind { get; set; } = RksvFinanzOnlineSubmissionClientKind.Fake;

    /// <summary>When false, <see cref="RksvFinanzOnlineSubmissionClient"/> does not attempt outbound traffic and returns <see cref="RksvFinanzOnlineSubmissionKnownErrorCodes.SubmissionDisabled"/>.</summary>
    public bool Enabled { get; set; }

    /// <summary>Absolute HTTPS endpoint for future SOAP traffic (host logged only; do not embed credentials in the URL).</summary>
    public string? EndpointUrl { get; set; }

    /// <summary>BMF/FinanzOnline tier selector (configuration only; binds to <c>FinanzOnline:RksvSubmission:Environment</c>).</summary>
    public RksvFinanzOnlineSubmissionDeploymentEnvironment Environment { get; set; } =
        RksvFinanzOnlineSubmissionDeploymentEnvironment.Test;

    /// <summary>Outbound request timeout when SOAP is implemented (1–600 seconds).</summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Non-secret reference: configuration path or key name where participant/user credentials are supplied at runtime (e.g. <c>FinanzOnline:Session:ParticipantId</c> pattern in your deployment).
    /// </summary>
    public string? ParticipantCredentialsConfigurationKey { get; set; }

    /// <summary>Non-secret reference: secret store entry name for the mTLS client certificate material (not the PEM/PFX body).</summary>
    public string? ClientCertificateSecretName { get; set; }

    /// <summary>
    /// When true, future implementations may open outbound HTTPS to <see cref="EndpointUrl"/> once SOAP is implemented.
    /// This build never performs network I/O regardless of this flag (skeleton only).
    /// </summary>
    public bool AllowOutboundNetworkCalls { get; set; }

    /// <summary>When <see cref="ClientKind"/> is <see cref="RksvFinanzOnlineSubmissionClientKind.Fake"/>, controls returned success.</summary>
    public bool FakeSuccess { get; set; } = true;

    public string? FakeExternalReference { get; set; }

    public string? FakeVerificationStatus { get; set; }

    public string? FakeErrorCode { get; set; }

    public string? FakeErrorMessage { get; set; }
}

/// <summary>Non-network fake client for RKSV FinanzOnline submission flows.</summary>
public sealed class FakeRksvFinanzOnlineSubmissionClient : IRksvFinanzOnlineSubmissionClient
{
    private readonly IOptionsMonitor<RksvFinanzOnlineSubmissionClientOptions> _options;
    private readonly ILogger<FakeRksvFinanzOnlineSubmissionClient> _logger;

    public FakeRksvFinanzOnlineSubmissionClient(
        IOptionsMonitor<RksvFinanzOnlineSubmissionClientOptions> options,
        ILogger<FakeRksvFinanzOnlineSubmissionClient> logger)
    {
        _options = options;
        _logger = logger;
    }

    public Task<RksvFinanzOnlineSubmissionResult> SubmitStartbelegAsync(
        RksvFinanzOnlineSubmissionPayload payload,
        CancellationToken cancellationToken = default) =>
        SubmitCoreAsync("Startbeleg", payload, cancellationToken);

    public Task<RksvFinanzOnlineSubmissionResult> SubmitJahresbelegAsync(
        RksvFinanzOnlineSubmissionPayload payload,
        CancellationToken cancellationToken = default) =>
        SubmitCoreAsync("Jahresbeleg", payload, cancellationToken);

    private Task<RksvFinanzOnlineSubmissionResult> SubmitCoreAsync(
        string receiptKindLabel,
        RksvFinanzOnlineSubmissionPayload payload,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var opts = _options.CurrentValue;
        var regShort = payload.CashRegisterId.ToString("N");
        if (regShort.Length > 8)
            regShort = regShort[..8];
        var refId = string.IsNullOrWhiteSpace(opts.FakeExternalReference)
            ? $"FAKE-RKS-{receiptKindLabel}-{regShort}"
            : opts.FakeExternalReference;

        // Log only non-sensitive identifiers (no QR body, no tax secrets beyond public tax number format).
        _logger.LogInformation(
            "Fake RKSV FinanzOnline submission receiptKind={ReceiptKind} cashRegisterId={CashRegisterId} registerNumber={RegisterNumber} receiptNumber={ReceiptNumber}",
            receiptKindLabel,
            payload.CashRegisterId,
            payload.RegisterNumber,
            payload.ReceiptNumber);

        if (!opts.FakeSuccess)
        {
            var snap = JsonSerializer.Serialize(new
            {
                client = nameof(FakeRksvFinanzOnlineSubmissionClient),
                receiptKind = receiptKindLabel,
                success = false,
                cashRegisterId = payload.CashRegisterId,
                receiptNumber = payload.ReceiptNumber,
            });
            return Task.FromResult(new RksvFinanzOnlineSubmissionResult
            {
                Success = false,
                ExternalReference = null,
                VerificationStatus = opts.FakeVerificationStatus ?? "Rejected",
                ErrorCode = opts.FakeErrorCode ?? "FAKE_RKSV_SUBMISSION_FAILED",
                ErrorMessage = opts.FakeErrorMessage ?? "Configured fake failure.",
                RawResponseSnapshot = snap,
            });
        }

        var okSnap = JsonSerializer.Serialize(new
        {
            client = nameof(FakeRksvFinanzOnlineSubmissionClient),
            receiptKind = receiptKindLabel,
            success = true,
            cashRegisterId = payload.CashRegisterId,
            receiptNumber = payload.ReceiptNumber,
            externalReference = refId,
            verificationStatus = opts.FakeVerificationStatus ?? "Verified",
        });
        return Task.FromResult(new RksvFinanzOnlineSubmissionResult
        {
            Success = true,
            ExternalReference = refId,
            VerificationStatus = opts.FakeVerificationStatus ?? "Verified",
            ErrorCode = null,
            ErrorMessage = null,
            RawResponseSnapshot = okSnap,
        });
    }
}

/// <summary>Placeholder for future BMF/FinanzOnline transport; no network calls.</summary>
public sealed class NotImplementedRksvFinanzOnlineSubmissionClient : IRksvFinanzOnlineSubmissionClient
{
    public Task<RksvFinanzOnlineSubmissionResult> SubmitStartbelegAsync(
        RksvFinanzOnlineSubmissionPayload payload,
        CancellationToken cancellationToken = default)
    {
        _ = payload;
        _ = cancellationToken;
        throw new NotImplementedException(
            "Legacy NotImplemented RKSV client. Use ClientKind=Fake, ClientKind=Real (guarded skeleton), or implement full SOAP transport.");
    }

    public Task<RksvFinanzOnlineSubmissionResult> SubmitJahresbelegAsync(
        RksvFinanzOnlineSubmissionPayload payload,
        CancellationToken cancellationToken = default)
    {
        _ = payload;
        _ = cancellationToken;
        throw new NotImplementedException(
            "Legacy NotImplemented RKSV client. Use ClientKind=Fake, ClientKind=Real (guarded skeleton), or implement full SOAP transport.");
    }
}

/// <summary>
/// Guarded FinanzOnline/BMF RKSV submission client (skeleton).
/// <list type="bullet">
/// <item><description>Not legally complete; does not implement official SOAP/RKSV FinanzOnline mapping.</description></item>
/// <item><description>No outbound HTTP is performed in this repository revision (transport placeholder only).</description></item>
/// <item><description>Secrets are never read here—only non-secret reference names from configuration.</description></item>
/// </list>
/// </summary>
public sealed class RksvFinanzOnlineSubmissionClient : IRksvFinanzOnlineSubmissionClient
{
    private readonly IOptionsMonitor<RksvFinanzOnlineSubmissionClientOptions> _options;
    private readonly ILogger<RksvFinanzOnlineSubmissionClient> _logger;

    public RksvFinanzOnlineSubmissionClient(
        IOptionsMonitor<RksvFinanzOnlineSubmissionClientOptions> options,
        ILogger<RksvFinanzOnlineSubmissionClient> logger)
    {
        _options = options;
        _logger = logger;
    }

    public Task<RksvFinanzOnlineSubmissionResult> SubmitStartbelegAsync(
        RksvFinanzOnlineSubmissionPayload payload,
        CancellationToken cancellationToken = default) =>
        SubmitCoreAsync("Startbeleg", payload, cancellationToken);

    public Task<RksvFinanzOnlineSubmissionResult> SubmitJahresbelegAsync(
        RksvFinanzOnlineSubmissionPayload payload,
        CancellationToken cancellationToken = default) =>
        SubmitCoreAsync("Jahresbeleg", payload, cancellationToken);

    private Task<RksvFinanzOnlineSubmissionResult> SubmitCoreAsync(
        string receiptKind,
        RksvFinanzOnlineSubmissionPayload payload,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var o = _options.CurrentValue;

        if (!o.Enabled)
        {
            _logger.LogInformation(
                "RKSV FinanzOnline submission skipped (FinanzOnline:RksvSubmission:Enabled=false). receiptKind={ReceiptKind} cashRegisterId={CashRegisterId} registerNumber={RegisterNumber} receiptNumber={ReceiptNumber}",
                receiptKind,
                payload.CashRegisterId,
                payload.RegisterNumber,
                payload.ReceiptNumber);
            return Task.FromResult(BuildDisabledResult(receiptKind, payload));
        }

        var validation = ValidateEnabledOptions(o);
        if (!validation.IsValid)
        {
            _logger.LogWarning(
                "RKSV FinanzOnline submission rejected (configuration incomplete). receiptKind={ReceiptKind} reason={Reason} hasEndpoint={HasEndpoint} hasParticipantRef={HasParticipantRef} hasCertificateSecretRef={HasCertificateRef} timeoutSeconds={TimeoutSeconds}",
                receiptKind,
                validation.Reason,
                !string.IsNullOrWhiteSpace(o.EndpointUrl),
                !string.IsNullOrWhiteSpace(o.ParticipantCredentialsConfigurationKey),
                !string.IsNullOrWhiteSpace(o.ClientCertificateSecretName),
                o.TimeoutSeconds);
            return Task.FromResult(new RksvFinanzOnlineSubmissionResult
            {
                Success = false,
                ErrorCode = RksvFinanzOnlineSubmissionKnownErrorCodes.ConfigIncomplete,
                ErrorMessage = validation.Reason,
                VerificationStatus = null,
                RawResponseSnapshot = JsonSerializer.Serialize(new
                {
                    client = nameof(RksvFinanzOnlineSubmissionClient),
                    receiptKind,
                    cashRegisterId = payload.CashRegisterId,
                    receiptNumber = payload.ReceiptNumber,
                    error = RksvFinanzOnlineSubmissionKnownErrorCodes.ConfigIncomplete,
                }),
            });
        }

        _ = Uri.TryCreate(o.EndpointUrl!.Trim(), UriKind.Absolute, out var endpointUri);
        var endpointHost = endpointUri?.Host ?? "unknown-host";

        if (o.Environment == RksvFinanzOnlineSubmissionDeploymentEnvironment.Production)
        {
            _logger.LogWarning(
                "RKSV FinanzOnline Environment=Production selected; outbound SOAP is not implemented in this build (no network I/O). receiptKind={ReceiptKind} endpointHost={EndpointHost} allowOutboundNetworkCalls={AllowOutbound}",
                receiptKind,
                endpointHost,
                o.AllowOutboundNetworkCalls);
        }
        else
        {
            _logger.LogInformation(
                "RKSV FinanzOnline submission skeleton (no SOAP, no HTTP). receiptKind={ReceiptKind} environment={Environment} endpointHost={EndpointHost} timeoutSeconds={TimeoutSeconds} allowOutboundNetworkCalls={AllowOutbound}",
                receiptKind,
                o.Environment,
                endpointHost,
                o.TimeoutSeconds,
                o.AllowOutboundNetworkCalls);
        }

        return Task.FromResult(new RksvFinanzOnlineSubmissionResult
        {
            Success = false,
            ErrorCode = RksvFinanzOnlineSubmissionKnownErrorCodes.SoapTransportNotImplemented,
            ErrorMessage =
                "RKSV FinanzOnline SOAP submit is not implemented in this build. This client is a guarded skeleton only and is not legally complete.",
            VerificationStatus = null,
            RawResponseSnapshot = JsonSerializer.Serialize(new
            {
                client = nameof(RksvFinanzOnlineSubmissionClient),
                receiptKind,
                environment = o.Environment.ToString(),
                endpointHost,
                note = "No outbound HTTP performed in this revision.",
            }),
        });
    }

    private static RksvFinanzOnlineSubmissionResult BuildDisabledResult(string receiptKind, RksvFinanzOnlineSubmissionPayload payload)
    {
        var snap = JsonSerializer.Serialize(new
        {
            client = nameof(RksvFinanzOnlineSubmissionClient),
            receiptKind,
            cashRegisterId = payload.CashRegisterId,
            receiptNumber = payload.ReceiptNumber,
            error = RksvFinanzOnlineSubmissionKnownErrorCodes.SubmissionDisabled,
        });
        return new RksvFinanzOnlineSubmissionResult
        {
            Success = false,
            ErrorCode = RksvFinanzOnlineSubmissionKnownErrorCodes.SubmissionDisabled,
            ErrorMessage = "FinanzOnline RKSV submission is disabled (FinanzOnline:RksvSubmission:Enabled=false).",
            VerificationStatus = RksvSpecialReceiptFinanzOnlineSubmissionStatuses.ManualVerificationRequired,
            RawResponseSnapshot = snap,
        };
    }

    private static (bool IsValid, string Reason) ValidateEnabledOptions(RksvFinanzOnlineSubmissionClientOptions o)
    {
        if (string.IsNullOrWhiteSpace(o.EndpointUrl))
            return (false, "FinanzOnline:RksvSubmission:EndpointUrl is required when Enabled=true.");
        if (!Uri.TryCreate(o.EndpointUrl.Trim(), UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return (false, "FinanzOnline:RksvSubmission:EndpointUrl must be an absolute HTTPS URI when Enabled=true.");
        if (o.TimeoutSeconds < 1 || o.TimeoutSeconds > 600)
            return (false, "FinanzOnline:RksvSubmission:TimeoutSeconds must be between 1 and 600 when Enabled=true.");
        if (string.IsNullOrWhiteSpace(o.ParticipantCredentialsConfigurationKey))
            return (false, "FinanzOnline:RksvSubmission:ParticipantCredentialsConfigurationKey is required when Enabled=true (reference name only).");
        if (string.IsNullOrWhiteSpace(o.ClientCertificateSecretName))
            return (false, "FinanzOnline:RksvSubmission:ClientCertificateSecretName is required when Enabled=true (secret name reference only).");
        return (true, string.Empty);
    }
}
