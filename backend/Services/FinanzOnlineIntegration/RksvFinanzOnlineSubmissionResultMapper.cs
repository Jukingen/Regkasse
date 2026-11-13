using System.Text.Json;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

/// <summary>
/// Maps rkdb / registrierkassen SOAP outcomes to <see cref="RksvFinanzOnlineSubmissionResult"/>
/// and classifies transient vs permanent errors for the outbox handler.
/// </summary>
public static class RksvFinanzOnlineSubmissionResultMapper
{
    public const string OutboundDisabled = "RKS_OUTBOUND_DISABLED";
    public const string MonatsbelegNotImplemented = "RKS_MONATSBELEG_NOT_IMPLEMENTED";
    /// <summary>Compliance P1-1: Monatsbeleg (non-December) is not filed via FON belegpruefung outbox.</summary>
    public const string MonatsbelegNotRequired = "RKS_MONATSBELEG_NOT_REQUIRED";
    public const string ModeResolveFailed = "RKS_MODE_RESOLVE_FAILED";

    public static RksvFinanzOnlineSubmissionResult FromRegistrierkassenResponse(
        FinanzOnlineRegisterSubmissionResponse response,
        string receiptKind)
    {
        ArgumentNullException.ThrowIfNull(response);

        var snapshot = JsonSerializer.Serialize(new
        {
            client = nameof(RksvFinanzOnlineSubmissionClient),
            receiptKind,
            success = response.Success,
            status = response.Status,
            protocolCode = response.ProtocolCode,
            transmissionIdLength = response.TransmissionId?.Length ?? 0,
            referenceIdLength = response.ReferenceId?.Length ?? 0,
            errorCode = response.ErrorCode,
            // Never include raw SOAP body or credentials.
        });

        if (response.Success)
        {
            var status = string.IsNullOrWhiteSpace(response.Status) ? "Submitted" : response.Status.Trim();
            // Synchronous belegpruefung success → Verified when BMF returns rc=0 / empty messages.
            var verification = string.Equals(status, "Rejected", StringComparison.OrdinalIgnoreCase)
                ? "Rejected"
                : RksvSpecialReceiptFinanzOnlineSubmissionStatuses.Verified;

            var external = FirstNonEmpty(response.TransmissionId, response.ReferenceId, response.ProtocolCode);
            return new RksvFinanzOnlineSubmissionResult
            {
                Success = true,
                ExternalReference = Truncate(external, 120),
                VerificationStatus = verification,
                ErrorCode = null,
                ErrorMessage = string.IsNullOrWhiteSpace(response.ErrorMessage)
                    ? null
                    : Truncate(response.ErrorMessage, 500),
                RawResponseSnapshot = snapshot,
            };
        }

        return new RksvFinanzOnlineSubmissionResult
        {
            Success = false,
            ExternalReference = Truncate(FirstNonEmpty(response.TransmissionId, response.ReferenceId), 120),
            VerificationStatus = string.Equals(response.Status, "Rejected", StringComparison.OrdinalIgnoreCase)
                ? "Rejected"
                : null,
            ErrorCode = string.IsNullOrWhiteSpace(response.ErrorCode)
                ? "RKDB_SUBMIT_FAILED"
                : response.ErrorCode.Trim(),
            ErrorMessage = Truncate(response.ErrorMessage ?? "FinanzOnline rkdb submission failed.", 500),
            RawResponseSnapshot = snapshot,
        };
    }

    /// <summary>
    /// Whether the outbox should retry. Aligns with
    /// <see cref="RksvSpecialReceiptFinanzOnlineOutboxHandler"/> classification rules.
    /// </summary>
    public static bool IsTransientErrorCode(string? errorCode)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
            return true;

        if (errorCode.StartsWith("FAKE_", StringComparison.OrdinalIgnoreCase))
            return true;

        if (errorCode.Contains("HTTP_5", StringComparison.OrdinalIgnoreCase)
            || errorCode.Contains("HTTP_429", StringComparison.OrdinalIgnoreCase)
            || errorCode.Contains("TRANSIENT", StringComparison.OrdinalIgnoreCase)
            || errorCode.Contains("TIMEOUT", StringComparison.OrdinalIgnoreCase)
            || errorCode.Contains("NETWORK", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>Permanent business / config codes that must not retry.</summary>
    public static bool IsPermanentErrorCode(string? errorCode)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
            return false;

        if (string.Equals(errorCode, RksvFinanzOnlineSubmissionKnownErrorCodes.SubmissionDisabled, StringComparison.OrdinalIgnoreCase)
            || string.Equals(errorCode, RksvFinanzOnlineSubmissionKnownErrorCodes.ConfigIncomplete, StringComparison.OrdinalIgnoreCase)
            || string.Equals(errorCode, RksvFinanzOnlineSubmissionKnownErrorCodes.SoapTransportNotImplemented, StringComparison.OrdinalIgnoreCase)
            || string.Equals(errorCode, OutboundDisabled, StringComparison.OrdinalIgnoreCase)
            || string.Equals(errorCode, RksvFinanzOnlineBelegMapper.BelegInvalidErrorCode, StringComparison.OrdinalIgnoreCase)
            || string.Equals(errorCode, MonatsbelegNotImplemented, StringComparison.OrdinalIgnoreCase)
            || string.Equals(errorCode, MonatsbelegNotRequired, StringComparison.OrdinalIgnoreCase)
            || string.Equals(errorCode, ModeResolveFailed, StringComparison.OrdinalIgnoreCase))
            return true;

        if (errorCode.Contains("SESSION", StringComparison.OrdinalIgnoreCase)
            || errorCode.Contains("401", StringComparison.OrdinalIgnoreCase)
            || errorCode.Contains("403", StringComparison.OrdinalIgnoreCase)
            || errorCode.Contains("UNAUTHORIZED", StringComparison.OrdinalIgnoreCase)
            || errorCode.Contains("CREDENTIALS", StringComparison.OrdinalIgnoreCase)
            || errorCode.Contains("RKDB_XML_STRUCTURE_INVALID", StringComparison.OrdinalIgnoreCase)
            || errorCode.Contains("RKDB_COMMAND_INVALID", StringComparison.OrdinalIgnoreCase)
            || errorCode.Contains("RKDB_MODE_NOT_SUPPORTED", StringComparison.OrdinalIgnoreCase)
            || errorCode.Contains("RKDB_XML_PAYLOAD_REQUIRED", StringComparison.OrdinalIgnoreCase)
            || errorCode.StartsWith("RKDB_RC_", StringComparison.OrdinalIgnoreCase)
            || errorCode.Contains("SOAP_FAULT", StringComparison.OrdinalIgnoreCase)
            || errorCode.Contains("MALFORMED_RESPONSE", StringComparison.OrdinalIgnoreCase)
            || errorCode.Contains("MODE_NOT_ALLOWED", StringComparison.OrdinalIgnoreCase))
            return true;

        return !IsTransientErrorCode(errorCode);
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }

        return null;
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value))
            return value;
        return value.Length <= max ? value : value[..max];
    }
}
