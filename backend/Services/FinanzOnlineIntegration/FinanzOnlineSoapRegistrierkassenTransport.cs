using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

/// <summary>
/// FinanzOnline Registrierkassen / rkdb SOAP 1.1 (WSDL: regKasseService.wsdl, operation rkdb).
/// El ile SOAP zarfı; svcutil üretimi yok. İçerik BMF regKasse.xsd ile uyumlu olmalıdır.
/// </summary>
public sealed class SoapFinanzOnlineRegistrierkassenTransport : IFinanzOnlineRegistrierkassenTransport
{
    private static readonly XNamespace SoapEnv = "http://schemas.xmlsoap.org/soap/envelope/";

    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<FinanzOnlineRegistrierkassenOptions> _options;
    private readonly IFinanzOnlineConnectivitySource _connectivitySource;
    private readonly IFinanzOnlineCredentialProvider _credentialProvider;
    private readonly ILogger<SoapFinanzOnlineRegistrierkassenTransport> _logger;

    public SoapFinanzOnlineRegistrierkassenTransport(
        HttpClient httpClient,
        IOptionsMonitor<FinanzOnlineRegistrierkassenOptions> options,
        IFinanzOnlineConnectivitySource connectivitySource,
        IFinanzOnlineCredentialProvider credentialProvider,
        ILogger<SoapFinanzOnlineRegistrierkassenTransport> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _connectivitySource = connectivitySource;
        _credentialProvider = credentialProvider;
        _logger = logger;
    }

    public async Task<FinanzOnlineRegisterSubmissionResponse> SubmitAsync(
        FinanzOnlineRegisterSubmissionRequest request,
        FinanzOnlineMappedCommand mappedCommand,
        string sessionToken,
        CancellationToken cancellationToken = default)
    {
        var opts = _options.CurrentValue;
        var endpoint = await ResolveSoapEndpointAsync(opts.BaseUrl, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return new FinanzOnlineRegisterSubmissionResponse
            {
                Success = false,
                ErrorCode = "SOAP_ENDPOINT_NOT_CONFIGURED",
                ErrorMessage = "FinanzOnline rkdb SOAP endpoint (BaseUrl) is not configured."
            };
        }

        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            return new FinanzOnlineRegisterSubmissionResponse
            {
                Success = false,
                ErrorCode = "SESSION_REQUIRED",
                ErrorMessage = "Session id is required for rkdb submission."
            };
        }

        var creds = await _credentialProvider.ResolveAsync(request.Mode, request.Scope, cancellationToken).ConfigureAwait(false);
        if (!creds.Success || string.IsNullOrWhiteSpace(creds.Credentials.TelematikId) ||
            string.IsNullOrWhiteSpace(creds.Credentials.Username))
        {
            return new FinanzOnlineRegisterSubmissionResponse
            {
                Success = false,
                ErrorCode = creds.ErrorCode ?? "CREDENTIALS_NOT_CONFIGURED",
                ErrorMessage = creds.ErrorMessage ?? "Credentials required for rkdb (tid, benid)."
            };
        }

        var ns = XNamespace.Get(opts.SoapNamespace.Trim());
        var artUebermittlung = request.Mode == FinanzOnlineIntegrationMode.TEST ? "T" : "P";

        var rkdbInner = BuildRkdbElement(mappedCommand, ns);
        if (rkdbInner == null)
        {
            return new FinanzOnlineRegisterSubmissionResponse
            {
                Success = false,
                ErrorCode = "RKDB_XML_PAYLOAD_REQUIRED",
                ErrorMessage = "Provide FinanzOnlineMappedCommand.RkdbPayloadXml or XML TransportPayload for rkdb content (see BMF regKasse.xsd)."
            };
        }

        var rkdbRequest = new XElement(ns + "rkdbRequest",
            new XElement(ns + "tid", creds.Credentials.TelematikId.Trim()),
            new XElement(ns + "benid", creds.Credentials.Username.Trim()),
            new XElement(ns + "id", sessionToken.Trim()),
            new XElement(ns + "art_uebermittlung", artUebermittlung),
            rkdbInner);

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

        try
        {
            using var res = await _httpClient.SendAsync(msg, cancellationToken).ConfigureAwait(false);
            var text = await res.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "FinanzOnline rkdb HTTP failure Status={Status} CorrelationId={CorrelationId} BusinessKey={BusinessKey}",
                    (int)res.StatusCode,
                    request.Correlation.CorrelationId,
                    request.Correlation.BusinessKey);
                return new FinanzOnlineRegisterSubmissionResponse
                {
                    Success = false,
                    ErrorCode = $"HTTP_{(int)res.StatusCode}",
                    ErrorMessage = TruncateForOperator(string.IsNullOrWhiteSpace(text) ? "rkdb HTTP request failed." : text)
                };
            }

            return ParseRkdbResponse(text, request.Correlation.CorrelationId, request.Correlation.BusinessKey);
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is OperationCanceledException)
        {
            _logger.LogWarning(ex, "FinanzOnline rkdb transient failure CorrelationId={CorrelationId}", request.Correlation.CorrelationId);
            return new FinanzOnlineRegisterSubmissionResponse
            {
                Success = false,
                ErrorCode = "TRANSIENT_NETWORK_FAILURE",
                ErrorMessage = "Transient network failure."
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FinanzOnline rkdb unexpected failure CorrelationId={CorrelationId}", request.Correlation.CorrelationId);
            return new FinanzOnlineRegisterSubmissionResponse
            {
                Success = false,
                ErrorCode = "SOAP_CLIENT_ERROR",
                ErrorMessage = "Unexpected error while calling rkdb service."
            };
        }
    }

    private static XElement? BuildRkdbElement(FinanzOnlineMappedCommand mapped, XNamespace ns)
    {
        var raw = mapped.RkdbPayloadXml;
        if (string.IsNullOrWhiteSpace(raw) && !string.IsNullOrWhiteSpace(mapped.TransportPayload) &&
            mapped.TransportPayload.TrimStart().StartsWith("<", StringComparison.Ordinal))
        {
            raw = mapped.TransportPayload;
        }

        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            var trimmed = raw.Trim();
            var el = XElement.Parse(trimmed);
            if (string.Equals(el.Name.LocalName, "rkdb", StringComparison.Ordinal))
            {
                if (el.Name.Namespace == ns)
                    return el;
                return new XElement(ns + "rkdb", el.Elements());
            }

            return new XElement(ns + "rkdb", el);
        }
        catch
        {
            return null;
        }
    }

    private FinanzOnlineRegisterSubmissionResponse ParseRkdbResponse(string text, string correlationId, string businessKey)
    {
        try
        {
            var doc = XDocument.Parse(text, LoadOptions.PreserveWhitespace);
            var fault = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Fault");
            if (fault != null)
            {
                var faultString = fault.Descendants().FirstOrDefault(e => e.Name.LocalName == "faultstring")?.Value
                    ?? "SOAP Fault";
                _logger.LogWarning(
                    "FinanzOnline rkdb SOAP Fault CorrelationId={CorrelationId} BusinessKey={BusinessKey} Summary={Summary}",
                    correlationId,
                    businessKey,
                    TruncateForLog(faultString));
                return new FinanzOnlineRegisterSubmissionResponse
                {
                    Success = false,
                    ErrorCode = "SOAP_FAULT",
                    ErrorMessage = TruncateForOperator(faultString)
                };
            }

            var response = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "rkdbResponse");
            if (response == null)
            {
                _logger.LogWarning("FinanzOnline rkdb: missing rkdbResponse CorrelationId={CorrelationId}", correlationId);
                return new FinanzOnlineRegisterSubmissionResponse
                {
                    Success = false,
                    ErrorCode = "MALFORMED_RESPONSE",
                    ErrorMessage = "Unable to parse rkdb response."
                };
            }

            var paketNr = response.Elements().FirstOrDefault(e => e.Name.LocalName == "paket_nr")?.Value?.Trim();
            var fastNr = response.Elements().FirstOrDefault(e => e.Name.LocalName == "fastnr")?.Value?.Trim();
            var info = response.Elements().FirstOrDefault(e => e.Name.LocalName == "info")?.Value?.Trim();
            var tsErstellung = response.Elements().FirstOrDefault(e => e.Name.LocalName == "ts_erstellung")?.Value?.Trim();
            int? satzNrFromResult = null;
            foreach (var result in response.Elements().Where(e => e.Name.LocalName == "result"))
            {
                var sn = result.Elements().FirstOrDefault(e => e.Name.LocalName == "satznr")?.Value?.Trim();
                if (int.TryParse(sn, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                {
                    satzNrFromResult = parsed;
                    break;
                }
            }

            var messages = new List<(string Rc, string Msg)>();
            foreach (var result in response.Elements().Where(e => e.Name.LocalName == "result"))
            {
                foreach (var rkdbMsg in result.Descendants().Where(e => e.Name.LocalName == "rkdbMessage"))
                {
                    var rc = rkdbMsg.Elements().FirstOrDefault(e => e.Name.LocalName == "rc")?.Value?.Trim() ?? string.Empty;
                    var m = rkdbMsg.Elements().FirstOrDefault(e => e.Name.LocalName == "msg")?.Value ?? string.Empty;
                    messages.Add((rc, m));
                }
            }

            var allOk = messages.Count == 0 || messages.All(m => IsSuccessRc(m.Rc));
            if (!allOk)
            {
                var firstBad = messages.First(m => !IsSuccessRc(m.Rc));
                _logger.LogWarning(
                    "FinanzOnline rkdb business rejection CorrelationId={CorrelationId} BusinessKey={BusinessKey} Rc={Rc} MessageLength={MessageLength}",
                    correlationId,
                    businessKey,
                    firstBad.Rc,
                    firstBad.Msg.Length);
                return new FinanzOnlineRegisterSubmissionResponse
                {
                    Success = false,
                    ErrorCode = $"RKDB_RC_{firstBad.Rc}",
                    ErrorMessage = TruncateForOperator(firstBad.Msg),
                    ProtocolCode = firstBad.Rc,
                    TransmissionId = paketNr,
                    ReferenceId = fastNr,
                    Status = "Rejected",
                    RkdbTsErstellungIso = tsErstellung,
                    RkdbSatzNr = satzNrFromResult
                };
            }

            var awaiting = !string.IsNullOrWhiteSpace(info) &&
                info.Contains("asynchron", StringComparison.OrdinalIgnoreCase);
            var status = awaiting ? "AwaitingProtocol" : (string.IsNullOrWhiteSpace(paketNr) ? "Submitted" : "Submitted");

            _logger.LogInformation(
                "FinanzOnline rkdb accepted CorrelationId={CorrelationId} BusinessKey={BusinessKey} PaketNrLength={PaketNrLength} MessageCount={MessageCount} Status={Status}",
                correlationId,
                businessKey,
                paketNr?.Length ?? 0,
                messages.Count,
                status);

            return new FinanzOnlineRegisterSubmissionResponse
            {
                Success = true,
                TransmissionId = paketNr,
                ReferenceId = fastNr,
                Status = status,
                ProtocolCode = messages.Count > 0 ? messages[0].Rc : "0",
                ErrorMessage = string.IsNullOrWhiteSpace(info) ? null : TruncateForOperator(info),
                RkdbTsErstellungIso = tsErstellung,
                RkdbSatzNr = satzNrFromResult
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FinanzOnline rkdb parse failure CorrelationId={CorrelationId}", correlationId);
            return new FinanzOnlineRegisterSubmissionResponse
            {
                Success = false,
                ErrorCode = "MALFORMED_RESPONSE",
                ErrorMessage = "Unable to parse rkdb SOAP response."
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
        if (string.IsNullOrEmpty(s))
            return string.Empty;
        const int max = 900;
        return s.Length <= max ? s : s.Substring(0, max - 3) + "...";
    }

    private static string TruncateForLog(string s)
    {
        if (string.IsNullOrEmpty(s))
            return string.Empty;
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
