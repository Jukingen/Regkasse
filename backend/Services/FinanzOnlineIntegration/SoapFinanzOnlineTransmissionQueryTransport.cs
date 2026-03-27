using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

/// <summary>
/// Protokoll-/Übermittlungsstatus: aynı rkdb SOAP endpoint üzerinden regKasseWs.xsd içindeki <c>status_kasse</c> isteği.
/// </summary>
public sealed class SoapFinanzOnlineTransmissionQueryTransport : IFinanzOnlineTransmissionQueryTransport
{
    private static readonly XNamespace SoapEnv = "http://schemas.xmlsoap.org/soap/envelope/";

    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<FinanzOnlineRegistrierkassenOptions> _rkdbOptions;
    private readonly IFinanzOnlineConnectivitySource _connectivitySource;
    private readonly IFinanzOnlineCredentialProvider _credentialProvider;
    private readonly ILogger<SoapFinanzOnlineTransmissionQueryTransport> _logger;

    public SoapFinanzOnlineTransmissionQueryTransport(
        HttpClient httpClient,
        IOptionsMonitor<FinanzOnlineRegistrierkassenOptions> rkdbOptions,
        IFinanzOnlineConnectivitySource connectivitySource,
        IFinanzOnlineCredentialProvider credentialProvider,
        ILogger<SoapFinanzOnlineTransmissionQueryTransport> logger)
    {
        _httpClient = httpClient;
        _rkdbOptions = rkdbOptions;
        _connectivitySource = connectivitySource;
        _credentialProvider = credentialProvider;
        _logger = logger;
    }

    public async Task<FinanzOnlineTransmissionStatusQueryResponse> QueryAsync(
        FinanzOnlineTransmissionStatusQueryRequest request,
        FinanzOnlineMappedCommand mappedCommand,
        string sessionToken,
        CancellationToken cancellationToken = default)
    {
        var preflight = FinanzOnlineStatusKasseQueryContextValidator.ValidateForStatusKasse(request);
        if (preflight.Count > 0)
        {
            return new FinanzOnlineTransmissionStatusQueryResponse
            {
                Success = false,
                TransmissionId = request.TransmissionId,
                ErrorCode = "RKDB_QUERY_CONTEXT_INVALID",
                ErrorMessage = string.Join("; ", preflight)
            };
        }

        var opts = _rkdbOptions.CurrentValue;
        var endpoint = await ResolveSoapEndpointAsync(opts.BaseUrl, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return new FinanzOnlineTransmissionStatusQueryResponse
            {
                Success = false,
                TransmissionId = request.TransmissionId,
                ErrorCode = "SOAP_ENDPOINT_NOT_CONFIGURED",
                ErrorMessage = "FinanzOnline rkdb SOAP endpoint (BaseUrl) is not configured."
            };
        }

        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            return new FinanzOnlineTransmissionStatusQueryResponse
            {
                Success = false,
                TransmissionId = request.TransmissionId,
                ErrorCode = "SESSION_UNAVAILABLE",
                ErrorMessage = "Session id is required for protocol query."
            };
        }

        var creds = await _credentialProvider.ResolveAsync(request.Mode, request.Scope, cancellationToken).ConfigureAwait(false);
        if (!creds.Success || string.IsNullOrWhiteSpace(creds.Credentials.TelematikId) ||
            string.IsNullOrWhiteSpace(creds.Credentials.Username))
        {
            return new FinanzOnlineTransmissionStatusQueryResponse
            {
                Success = false,
                TransmissionId = request.TransmissionId,
                ErrorCode = creds.ErrorCode ?? "CREDENTIALS_NOT_CONFIGURED",
                ErrorMessage = creds.ErrorMessage ?? "Credentials required for rkdb (tid, benid)."
            };
        }

        var ns = XNamespace.Get(string.IsNullOrWhiteSpace(opts.SoapNamespace) ? "https://finanzonline.bmf.gv.at/rkdb" : opts.SoapNamespace.Trim());
        var artUebermittlung = request.Mode == FinanzOnlineIntegrationMode.TEST ? "T" : "P";

        var kassenId = request.Scope.RegisterId!.Trim();
        var statusKasse = new XElement(ns + "status_kasse");
        var fast = request.ExternalReferenceFastNr?.Trim();
        if (!string.IsNullOrEmpty(fast) && fast.Length == 9 && fast.All(char.IsDigit))
            statusKasse.Add(new XElement(ns + "fastnr", fast));

        statusKasse.Add(
            new XElement(ns + "paket_nr", request.TransmissionId.Trim()),
            new XElement(ns + "ts_erstellung", request.RkdbTsErstellungIso!.Trim()),
            new XElement(ns + "satznr", Math.Clamp(request.RkdbSatzNr < 1 ? 1 : request.RkdbSatzNr, 1, 999_999_999)),
            new XElement(ns + "kassenidentifikationsnummer", kassenId));

        var rkdbRequest = new XElement(ns + "rkdbRequest",
            new XElement(ns + "tid", creds.Credentials.TelematikId.Trim()),
            new XElement(ns + "benid", creds.Credentials.Username.Trim()),
            new XElement(ns + "id", sessionToken.Trim()),
            new XElement(ns + "art_uebermittlung", artUebermittlung),
            statusKasse);

        var envelope = new XElement(SoapEnv + "Envelope",
            new XAttribute(XNamespace.Xmlns + "soapenv", SoapEnv.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "rk", ns.NamespaceName),
            new XElement(SoapEnv + "Body", rkdbRequest));

        var xml = new StringBuilder();
        xml.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        xml.Append(envelope.ToString(SaveOptions.DisableFormatting));

        using var msg = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(xml.ToString(), Encoding.UTF8, "text/xml")
        };
        var action = string.IsNullOrWhiteSpace(opts.SoapAction) ? "rkdb" : opts.SoapAction.Trim();
        msg.Headers.TryAddWithoutValidation("SOAPAction", $"\"{action}\"");

        _logger.LogInformation(
            "FinanzOnline protocol query SOAP CorrelationId={CorrelationId} BusinessKey={BusinessKey} Operation={Operation}",
            request.Correlation.CorrelationId,
            request.Correlation.BusinessKey,
            mappedCommand.OperationName);

        try
        {
            using var res = await _httpClient.SendAsync(msg, cancellationToken).ConfigureAwait(false);
            var text = await res.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
            {
                var code = $"HTTP_{(int)res.StatusCode}";
                _logger.LogWarning(
                    "FinanzOnline protocol query HTTP failure Status={Status} CorrelationId={CorrelationId}",
                    (int)res.StatusCode,
                    request.Correlation.CorrelationId);
                return new FinanzOnlineTransmissionStatusQueryResponse
                {
                    Success = false,
                    TransmissionId = request.TransmissionId,
                    ErrorCode = code,
                    ErrorMessage = TruncateForOperator(string.IsNullOrWhiteSpace(text) ? "Protocol query HTTP failed." : text)
                };
            }

            return ParseRkdbQueryResponse(text, request.TransmissionId, request.Correlation.CorrelationId);
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is OperationCanceledException)
        {
            _logger.LogWarning(ex, "FinanzOnline protocol query transient CorrelationId={CorrelationId}", request.Correlation.CorrelationId);
            return new FinanzOnlineTransmissionStatusQueryResponse
            {
                Success = false,
                TransmissionId = request.TransmissionId,
                ErrorCode = "TRANSIENT_QUERY_FAILURE",
                ErrorMessage = "Transient transmission query failure."
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FinanzOnline protocol query unexpected CorrelationId={CorrelationId}", request.Correlation.CorrelationId);
            return new FinanzOnlineTransmissionStatusQueryResponse
            {
                Success = false,
                TransmissionId = request.TransmissionId,
                ErrorCode = "SOAP_CLIENT_ERROR",
                ErrorMessage = "Unexpected error while calling protocol query."
            };
        }
    }

    private FinanzOnlineTransmissionStatusQueryResponse ParseRkdbQueryResponse(string text, string transmissionId, string correlationId)
    {
        try
        {
            var doc = XDocument.Parse(text, LoadOptions.PreserveWhitespace);
            var fault = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Fault");
            if (fault != null)
            {
                var faultString = fault.Descendants().FirstOrDefault(e => e.Name.LocalName == "faultstring")?.Value ?? "SOAP Fault";
                _logger.LogWarning(
                    "FinanzOnline protocol query SOAP Fault CorrelationId={CorrelationId} Summary={Summary}",
                    correlationId,
                    TruncateForLog(faultString));
                return new FinanzOnlineTransmissionStatusQueryResponse
                {
                    Success = false,
                    TransmissionId = transmissionId,
                    ErrorCode = "SOAP_FAULT",
                    ErrorMessage = TruncateForOperator(faultString)
                };
            }

            var response = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "rkdbResponse");
            if (response == null)
            {
                return new FinanzOnlineTransmissionStatusQueryResponse
                {
                    Success = false,
                    TransmissionId = transmissionId,
                    ErrorCode = "MALFORMED_RESPONSE",
                    ErrorMessage = "Unable to parse rkdb response (missing rkdbResponse)."
                };
            }

            var info = response.Elements().FirstOrDefault(e => e.Name.LocalName == "info")?.Value?.Trim();
            var protocol = new List<FinanzOnlineTransmissionProtocolEntry>();
            var tsBase = DateTimeOffset.UtcNow;

            foreach (var result in response.Elements().Where(e => e.Name.LocalName == "result"))
            {
                foreach (var rkdbMsg in result.Descendants().Where(e => e.Name.LocalName == "rkdbMessage"))
                {
                    var rc = rkdbMsg.Elements().FirstOrDefault(e => e.Name.LocalName == "rc")?.Value?.Trim() ?? string.Empty;
                    var m = rkdbMsg.Elements().FirstOrDefault(e => e.Name.LocalName == "msg")?.Value ?? string.Empty;
                    var ok = IsSuccessRc(rc);
                    protocol.Add(new FinanzOnlineTransmissionProtocolEntry
                    {
                        TimestampUtc = tsBase,
                        Level = ok ? "Info" : "Error",
                        Message = string.IsNullOrEmpty(m) ? rc : $"{rc}: {m}"
                    });
                }
            }

            var hasError = protocol.Any(p => string.Equals(p.Level, "Error", StringComparison.OrdinalIgnoreCase));
            if (hasError)
            {
                var first = protocol.First(p => string.Equals(p.Level, "Error", StringComparison.OrdinalIgnoreCase));
                return new FinanzOnlineTransmissionStatusQueryResponse
                {
                    Success = true,
                    TransmissionId = transmissionId,
                    Status = "failed",
                    ErrorCode = "RKDB_PROTOCOL_RC",
                    ErrorMessage = TruncateForOperator(first.Message),
                    Protocol = protocol
                };
            }

            var awaiting = !string.IsNullOrWhiteSpace(info) &&
                info.Contains("asynchron", StringComparison.OrdinalIgnoreCase);
            var status = awaiting ? "awaitingprotocol" : "completed";

            return new FinanzOnlineTransmissionStatusQueryResponse
            {
                Success = true,
                TransmissionId = transmissionId,
                Status = status,
                Protocol = protocol,
                ErrorMessage = string.IsNullOrWhiteSpace(info) ? null : TruncateForOperator(info)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FinanzOnline protocol query parse failure CorrelationId={CorrelationId}", correlationId);
            return new FinanzOnlineTransmissionStatusQueryResponse
            {
                Success = false,
                TransmissionId = transmissionId,
                ErrorCode = "MALFORMED_RESPONSE",
                ErrorMessage = "Unable to parse protocol query response."
            };
        }
    }

    private static bool IsSuccessRc(string rc)
    {
        if (string.IsNullOrEmpty(rc))
            return false;
        if (string.Equals(rc, "0", StringComparison.Ordinal))
            return true;
        if (string.Equals(rc, "OK", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    private static string TruncateForOperator(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        const int max = 900;
        return s.Length <= max ? s : s.Substring(0, max - 3) + "...";
    }

    private static string TruncateForLog(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        const int max = 200;
        return s.Length <= max ? s : s.Substring(0, max - 3) + "...";
    }

    private async Task<string> ResolveSoapEndpointAsync(string configuredBaseUrl, CancellationToken cancellationToken)
    {
        var snap = await _connectivitySource.GetCompanySettingsSnapshotAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(snap?.BaseUrl))
            return snap.BaseUrl!.Trim();
        return (configuredBaseUrl ?? string.Empty).Trim();
    }
}
