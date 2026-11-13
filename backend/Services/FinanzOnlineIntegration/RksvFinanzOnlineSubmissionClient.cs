using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KasseAPI_Final.Models;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

/// <summary>Stable error codes returned by <see cref="RksvFinanzOnlineSubmissionClient"/> (and consumed by outbox handlers).</summary>
public static class RksvFinanzOnlineSubmissionKnownErrorCodes
{
    public const string SubmissionDisabled = "RKS_SUBMISSION_DISABLED";

    public const string ConfigIncomplete = "RKS_SUBMISSION_CONFIG_INCOMPLETE";

    /// <summary>Legacy skeleton code — Real client no longer returns this when SOAP is wired.</summary>
    public const string SoapTransportNotImplemented = "RKS_SOAP_TRANSPORT_NOT_IMPLEMENTED";

    public const string OutboundDisabled = RksvFinanzOnlineSubmissionResultMapper.OutboundDisabled;

    public const string BelegInvalid = RksvFinanzOnlineBelegMapper.BelegInvalidErrorCode;

    public const string MonatsbelegNotImplemented = RksvFinanzOnlineSubmissionResultMapper.MonatsbelegNotImplemented;

    /// <summary>P1-1: monthly Monatsbeleg is not required on the FON belegpruefung outbox (Jahresbeleg covers December).</summary>
    public const string MonatsbelegNotRequired = RksvFinanzOnlineSubmissionResultMapper.MonatsbelegNotRequired;
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

    /// <summary>RKSV machine-readable receipt / QR payload (wire or compact JWS).</summary>
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
/// Abstraction for submitting RKSV special receipts to FinanzOnline via BMF <c>rkdb</c> / <c>belegpruefung</c>.
/// </summary>
public interface IRksvFinanzOnlineSubmissionClient
{
    Task<RksvFinanzOnlineSubmissionResult> SubmitStartbelegAsync(
        RksvFinanzOnlineSubmissionPayload payload,
        CancellationToken cancellationToken = default);

    Task<RksvFinanzOnlineSubmissionResult> SubmitJahresbelegAsync(
        RksvFinanzOnlineSubmissionPayload payload,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Monatsbeleg FinanzOnline submission (P1-1). Signature reserved; default implementations return not-implemented.
    /// </summary>
    Task<RksvFinanzOnlineSubmissionResult> SubmitMonatsbelegAsync(
        RksvFinanzOnlineSubmissionPayload payload,
        CancellationToken cancellationToken = default);
}

public enum RksvFinanzOnlineSubmissionClientKind
{
    /// <summary>In-process fake for development and tests.</summary>
    Fake = 0,

    /// <summary>Reserved legacy placeholder; throws on submit.</summary>
    NotImplemented = 1,

    /// <summary>Real client: session + <see cref="SoapFinanzOnlineRegistrierkassenTransport"/> via <see cref="IFinanzOnlineSubmissionService"/>.</summary>
    Real = 2,
}

/// <summary>Configuration for <see cref="IRksvFinanzOnlineSubmissionClient"/> binding (no secrets; credential material is referenced by name/key only).</summary>
public sealed class RksvFinanzOnlineSubmissionClientOptions
{
    public const string SectionName = "FinanzOnline:RksvSubmission";

    public RksvFinanzOnlineSubmissionClientKind ClientKind { get; set; } = RksvFinanzOnlineSubmissionClientKind.Fake;

    /// <summary>When false, Real client does not attempt outbound traffic and returns <see cref="RksvFinanzOnlineSubmissionKnownErrorCodes.SubmissionDisabled"/>.</summary>
    public bool Enabled { get; set; }

    /// <summary>Absolute HTTPS endpoint reference (host logged only; transport uses Registrierkassen BaseUrl).</summary>
    public string? EndpointUrl { get; set; }

    /// <summary>BMF/FinanzOnline tier selector (configuration only; binds to <c>FinanzOnline:RksvSubmission:Environment</c>).</summary>
    public RksvFinanzOnlineSubmissionDeploymentEnvironment Environment { get; set; } =
        RksvFinanzOnlineSubmissionDeploymentEnvironment.Test;

    /// <summary>Outbound request timeout hint (1–600 seconds).</summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Non-secret reference: configuration path or key name where participant/user credentials are supplied at runtime.
    /// </summary>
    public string? ParticipantCredentialsConfigurationKey { get; set; }

    /// <summary>Non-secret reference: secret store entry name for optional mTLS client certificate material.</summary>
    public string? ClientCertificateSecretName { get; set; }

    /// <summary>
    /// When false, Real client refuses outbound SOAP even if Enabled (feature flag / safety gate).
    /// </summary>
    public bool AllowOutboundNetworkCalls { get; set; }

    /// <summary>Production escape hatch: allow Fake client (logged Critical). Default false.</summary>
    public bool AllowFakeClientInProduction { get; set; }

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

    public Task<RksvFinanzOnlineSubmissionResult> SubmitMonatsbelegAsync(
        RksvFinanzOnlineSubmissionPayload payload,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        _logger.LogInformation(
            "Fake Monatsbeleg FinanzOnline submit not required (P1-1). cashRegisterId={CashRegisterId} receiptNumber={ReceiptNumber}",
            payload.CashRegisterId,
            payload.ReceiptNumber);
        return Task.FromResult(new RksvFinanzOnlineSubmissionResult
        {
            Success = false,
            ErrorCode = RksvFinanzOnlineSubmissionKnownErrorCodes.MonatsbelegNotRequired,
            ErrorMessage =
                "Monatsbeleg (Jan–Nov) is not submitted via FinanzOnline belegpruefung outbox. " +
                "December is filed as Jahresbeleg. See docs/MONATSBELEG_FINANZONLINE_DECISION.md.",
            VerificationStatus = RksvSpecialReceiptFinanzOnlineSubmissionStatuses.NotRequired,
            RawResponseSnapshot = JsonSerializer.Serialize(new
            {
                client = nameof(FakeRksvFinanzOnlineSubmissionClient),
                receiptKind = "Monatsbeleg",
                error = RksvFinanzOnlineSubmissionKnownErrorCodes.MonatsbelegNotRequired,
                decisionDoc = "docs/MONATSBELEG_FINANZONLINE_DECISION.md",
            }),
        });
    }

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

/// <summary>Placeholder for legacy NotImplemented ClientKind; no network calls.</summary>
public sealed class NotImplementedRksvFinanzOnlineSubmissionClient : IRksvFinanzOnlineSubmissionClient
{
    public Task<RksvFinanzOnlineSubmissionResult> SubmitStartbelegAsync(
        RksvFinanzOnlineSubmissionPayload payload,
        CancellationToken cancellationToken = default)
    {
        _ = payload;
        _ = cancellationToken;
        throw new NotImplementedException(
            "Legacy NotImplemented RKSV client. Use ClientKind=Fake or ClientKind=Real.");
    }

    public Task<RksvFinanzOnlineSubmissionResult> SubmitJahresbelegAsync(
        RksvFinanzOnlineSubmissionPayload payload,
        CancellationToken cancellationToken = default)
    {
        _ = payload;
        _ = cancellationToken;
        throw new NotImplementedException(
            "Legacy NotImplemented RKSV client. Use ClientKind=Fake or ClientKind=Real.");
    }

    public Task<RksvFinanzOnlineSubmissionResult> SubmitMonatsbelegAsync(
        RksvFinanzOnlineSubmissionPayload payload,
        CancellationToken cancellationToken = default)
    {
        _ = payload;
        _ = cancellationToken;
        throw new NotImplementedException(
            "Legacy NotImplemented RKSV client. Use ClientKind=Fake or ClientKind=Real.");
    }
}

/// <summary>
/// Real RKSV FinanzOnline submission: QR/JWS → <c>belegpruefung</c> → session →
/// <see cref="SoapFinanzOnlineRegistrierkassenTransport"/> via <see cref="IFinanzOnlineSubmissionService"/>.
/// </summary>
public sealed class RksvFinanzOnlineSubmissionClient : IRksvFinanzOnlineSubmissionClient
{
    private readonly IOptionsMonitor<RksvFinanzOnlineSubmissionClientOptions> _options;
    private readonly IOptionsMonitor<FinanzOnlineModeOptions> _modeOptions;
    private readonly IOptionsMonitor<FinanzOnlineCutoverGuardOptions> _cutoverOptions;
    private readonly IFinanzOnlineSubmissionService _submissionService;
    private readonly ILogger<RksvFinanzOnlineSubmissionClient> _logger;

    public RksvFinanzOnlineSubmissionClient(
        IOptionsMonitor<RksvFinanzOnlineSubmissionClientOptions> options,
        IOptionsMonitor<FinanzOnlineModeOptions> modeOptions,
        IOptionsMonitor<FinanzOnlineCutoverGuardOptions> cutoverOptions,
        IFinanzOnlineSubmissionService submissionService,
        ILogger<RksvFinanzOnlineSubmissionClient> logger)
    {
        _options = options;
        _modeOptions = modeOptions;
        _cutoverOptions = cutoverOptions;
        _submissionService = submissionService;
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

    public Task<RksvFinanzOnlineSubmissionResult> SubmitMonatsbelegAsync(
        RksvFinanzOnlineSubmissionPayload payload,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        _logger.LogInformation(
            "Monatsbeleg FinanzOnline submit not required (P1-1). cashRegisterId={CashRegisterId} receiptNumber={ReceiptNumber}",
            payload.CashRegisterId,
            payload.ReceiptNumber);
        return Task.FromResult(new RksvFinanzOnlineSubmissionResult
        {
            Success = false,
            ErrorCode = RksvFinanzOnlineSubmissionKnownErrorCodes.MonatsbelegNotRequired,
            ErrorMessage =
                "Monatsbeleg (Jan–Nov) is not submitted via FinanzOnline belegpruefung outbox. " +
                "December is filed as Jahresbeleg. See docs/MONATSBELEG_FINANZONLINE_DECISION.md.",
            VerificationStatus = RksvSpecialReceiptFinanzOnlineSubmissionStatuses.NotRequired,
            RawResponseSnapshot = JsonSerializer.Serialize(new
            {
                client = nameof(RksvFinanzOnlineSubmissionClient),
                receiptKind = "Monatsbeleg",
                error = RksvFinanzOnlineSubmissionKnownErrorCodes.MonatsbelegNotRequired,
                decisionDoc = "docs/MONATSBELEG_FINANZONLINE_DECISION.md",
            }),
        });
    }

    private async Task<RksvFinanzOnlineSubmissionResult> SubmitCoreAsync(
        string receiptKind,
        RksvFinanzOnlineSubmissionPayload payload,
        CancellationToken cancellationToken)
    {
        var o = _options.CurrentValue;

        if (!o.Enabled)
        {
            _logger.LogInformation(
                "RKSV FinanzOnline submission skipped (Enabled=false). receiptKind={ReceiptKind} cashRegisterId={CashRegisterId} receiptNumber={ReceiptNumber}",
                receiptKind,
                payload.CashRegisterId,
                payload.ReceiptNumber);
            return BuildDisabledResult(receiptKind, payload);
        }

        if (!o.AllowOutboundNetworkCalls)
        {
            _logger.LogWarning(
                "RKSV FinanzOnline submission blocked (AllowOutboundNetworkCalls=false). receiptKind={ReceiptKind} cashRegisterId={CashRegisterId}",
                receiptKind,
                payload.CashRegisterId);
            return new RksvFinanzOnlineSubmissionResult
            {
                Success = false,
                ErrorCode = RksvFinanzOnlineSubmissionKnownErrorCodes.OutboundDisabled,
                ErrorMessage = "Outbound FinanzOnline calls are disabled (FinanzOnline:RksvSubmission:AllowOutboundNetworkCalls=false).",
                VerificationStatus = null,
                RawResponseSnapshot = JsonSerializer.Serialize(new
                {
                    client = nameof(RksvFinanzOnlineSubmissionClient),
                    receiptKind,
                    error = RksvFinanzOnlineSubmissionKnownErrorCodes.OutboundDisabled,
                }),
            };
        }

        var validation = ValidateEnabledOptions(o);
        if (!validation.IsValid)
        {
            _logger.LogWarning(
                "RKSV FinanzOnline submission rejected (configuration incomplete). receiptKind={ReceiptKind} reason={Reason}",
                receiptKind,
                validation.Reason);
            return new RksvFinanzOnlineSubmissionResult
            {
                Success = false,
                ErrorCode = RksvFinanzOnlineSubmissionKnownErrorCodes.ConfigIncomplete,
                ErrorMessage = validation.Reason,
                RawResponseSnapshot = JsonSerializer.Serialize(new
                {
                    client = nameof(RksvFinanzOnlineSubmissionClient),
                    receiptKind,
                    error = RksvFinanzOnlineSubmissionKnownErrorCodes.ConfigIncomplete,
                }),
            };
        }

        if (!RksvFinanzOnlineBelegMapper.TryResolveBeleg(payload.QrPayload, out var beleg, out var belegError))
        {
            _logger.LogWarning(
                "RKSV FinanzOnline beleg mapping failed. receiptKind={ReceiptKind} cashRegisterId={CashRegisterId} receiptNumber={ReceiptNumber}",
                receiptKind,
                payload.CashRegisterId,
                payload.ReceiptNumber);
            return new RksvFinanzOnlineSubmissionResult
            {
                Success = false,
                ErrorCode = RksvFinanzOnlineSubmissionKnownErrorCodes.BelegInvalid,
                ErrorMessage = belegError,
                RawResponseSnapshot = JsonSerializer.Serialize(new
                {
                    client = nameof(RksvFinanzOnlineSubmissionClient),
                    receiptKind,
                    error = RksvFinanzOnlineSubmissionKnownErrorCodes.BelegInvalid,
                    belegLength = payload.QrPayload?.Length ?? 0,
                }),
            };
        }

        FinanzOnlineIntegrationMode mode;
        string modeLabel;
        try
        {
            mode = FinanzOnlineModeResolver.ResolveOutboxMode(
                _modeOptions.CurrentValue.Mode,
                _cutoverOptions.CurrentValue,
                out modeLabel);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "RKSV FinanzOnline mode resolve failed receiptKind={ReceiptKind}", receiptKind);
            return new RksvFinanzOnlineSubmissionResult
            {
                Success = false,
                ErrorCode = RksvFinanzOnlineSubmissionResultMapper.ModeResolveFailed,
                ErrorMessage = Truncate(ex.Message, 500),
                RawResponseSnapshot = JsonSerializer.Serialize(new
                {
                    client = nameof(RksvFinanzOnlineSubmissionClient),
                    receiptKind,
                    error = RksvFinanzOnlineSubmissionResultMapper.ModeResolveFailed,
                }),
            };
        }

        _ = Uri.TryCreate(o.EndpointUrl!.Trim(), UriKind.Absolute, out var endpointUri);
        var endpointHost = endpointUri?.Host ?? "unknown-host";

        _logger.LogInformation(
            "RKSV FinanzOnline belegpruefung submit receiptKind={ReceiptKind} mode={Mode} endpointHost={EndpointHost} cashRegisterId={CashRegisterId} receiptNumber={ReceiptNumber} belegLength={BelegLength}",
            receiptKind,
            modeLabel,
            endpointHost,
            payload.CashRegisterId,
            payload.ReceiptNumber,
            beleg.Length);

        var businessKey = $"rksv|{payload.ReceiptNumber}|{receiptKind}";
        var payloadHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes($"{payload.CashRegisterId:N}|{payload.ReceiptNumber}|{beleg.Length}")))
            .ToLowerInvariant();

        var request = new FinanzOnlineRegisterSubmissionRequest
        {
            Mode = mode,
            Scope = new FinanzOnlineScope
            {
                TenantId = payload.TenantId,
                RegisterId = string.IsNullOrWhiteSpace(payload.RegisterNumber)
                    ? payload.CashRegisterId.ToString("N")
                    : payload.RegisterNumber,
            },
            Correlation = new FinanzOnlineCorrelationContext
            {
                BusinessKey = businessKey,
                PayloadHash = payloadHash,
                CorrelationId = payload.CashRegisterId.ToString("N"),
            },
            SubmissionKind = FinanzOnlineSubmissionKind.Register,
            PayloadJson = "{}",
            RkdbBelegpruefung = new FinanzOnlineRkdbBelegpruefungCommand
            {
                Beleg = beleg,
                PaketNr = 1,
                SatzNr = 1,
                TsErstellungUtc = payload.TimestampUtc == default
                    ? DateTimeOffset.UtcNow
                    : payload.TimestampUtc,
                Kundeninfo = string.IsNullOrWhiteSpace(payload.CompanyTaxNumber)
                    ? null
                    : Truncate(payload.CompanyTaxNumber, 500),
            },
        };

        try
        {
            var response = await _submissionService
                .SubmitAsync(request, cancellationToken)
                .ConfigureAwait(false);
            return RksvFinanzOnlineSubmissionResultMapper.FromRegistrierkassenResponse(response, receiptKind);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "RKSV FinanzOnline submit threw. receiptKind={ReceiptKind} cashRegisterId={CashRegisterId}",
                receiptKind,
                payload.CashRegisterId);
            return new RksvFinanzOnlineSubmissionResult
            {
                Success = false,
                ErrorCode = "TRANSIENT_NETWORK_FAILURE",
                ErrorMessage = Truncate(ex.Message, 500),
                RawResponseSnapshot = JsonSerializer.Serialize(new
                {
                    client = nameof(RksvFinanzOnlineSubmissionClient),
                    receiptKind,
                    error = "TRANSIENT_NETWORK_FAILURE",
                }),
            };
        }
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
        // Client certificate is optional — session SOAP uses tid/benid/pin; mTLS is Ops-dependent.
        return (true, string.Empty);
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
