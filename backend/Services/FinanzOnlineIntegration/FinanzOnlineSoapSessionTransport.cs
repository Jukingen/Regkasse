using System.Net;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

/// <summary>
/// FinanzOnline Session-Webservice: SOAP 1.1 document/literal çağrıları (WSDL: sessionService, targetNamespace yapılandırılabilir).
/// WSDL ile uyum için el ile SOAP zarfı üretilir; üretilen kod (svcutil) kullanılmaz.
/// </summary>
public sealed class SoapFinanzOnlineSessionTransport : IFinanzOnlineSessionTransport
{
    private static readonly XNamespace SoapEnv = "http://schemas.xmlsoap.org/soap/envelope/";

    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<FinanzOnlineSessionOptions> _options;
    private readonly IFinanzOnlineConnectivitySource _connectivitySource;
    private readonly ILogger<SoapFinanzOnlineSessionTransport> _logger;

    public SoapFinanzOnlineSessionTransport(
        HttpClient httpClient,
        IOptionsMonitor<FinanzOnlineSessionOptions> options,
        IFinanzOnlineConnectivitySource connectivitySource,
        ILogger<SoapFinanzOnlineSessionTransport> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _connectivitySource = connectivitySource;
        _logger = logger;
    }

    public async Task<FinanzOnlineSessionTransportResponse> LoginAsync(
        FinanzOnlineSessionTransportRequest request,
        CancellationToken cancellationToken = default)
    {
        var opts = _options.CurrentValue;
        var endpoint = await ResolveSoapEndpointAsync(opts, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return new FinanzOnlineSessionTransportResponse
            {
                Success = false,
                ErrorCode = "SOAP_ENDPOINT_NOT_CONFIGURED",
                ErrorMessage = "FinanzOnline session SOAP endpoint (BaseUrl) is not configured."
            };
        }

        var creds = request.Credentials;
        var tid = creds.TelematikId?.Trim() ?? string.Empty;
        var benid = creds.Username?.Trim() ?? string.Empty;
        var pin = creds.Password ?? string.Empty;
        var herstellerid = creds.HerstellerId?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(tid) || string.IsNullOrEmpty(herstellerid))
        {
            return new FinanzOnlineSessionTransportResponse
            {
                Success = false,
                ErrorCode = "SOAP_PARTICIPANT_IDS_NOT_CONFIGURED",
                ErrorMessage = "TelematikId and HerstellerId are required for SOAP session login."
            };
        }

        var ns = XNamespace.Get(opts.SoapNamespace.Trim());
        var loginRequest = new XElement(ns + "loginRequest",
            new XElement(ns + "tid", tid),
            new XElement(ns + "benid", benid),
            new XElement(ns + "pin", pin),
            new XElement(ns + "herstellerid", herstellerid));

        var envelope = new XElement(SoapEnv + "Envelope",
            new XAttribute(XNamespace.Xmlns + "soapenv", SoapEnv.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "fon", ns.NamespaceName),
            new XElement(SoapEnv + "Body", loginRequest));

        return await SendSoapAsync(
            endpoint,
            soapAction: "login",
            envelope,
            request.Correlation.CorrelationId,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<FinanzOnlineSessionTransportResponse> LogoutAsync(
        FinanzOnlineSessionTransportRequest request,
        CancellationToken cancellationToken = default)
    {
        var opts = _options.CurrentValue;
        var endpoint = await ResolveSoapEndpointAsync(opts, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return new FinanzOnlineSessionTransportResponse
            {
                Success = false,
                ErrorCode = "SOAP_ENDPOINT_NOT_CONFIGURED",
                ErrorMessage = "FinanzOnline session SOAP endpoint (BaseUrl) is not configured."
            };
        }

        var creds = request.Credentials;
        var tid = creds.TelematikId?.Trim() ?? string.Empty;
        var benid = creds.Username?.Trim() ?? string.Empty;
        var sessionId = request.SessionToken?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(tid) || string.IsNullOrEmpty(sessionId))
        {
            return new FinanzOnlineSessionTransportResponse
            {
                Success = false,
                ErrorCode = "SOAP_LOGOUT_INCOMPLETE",
                ErrorMessage = "TelematikId and session id are required for SOAP logout."
            };
        }

        var ns = XNamespace.Get(opts.SoapNamespace.Trim());
        var logoutRequest = new XElement(ns + "logoutRequest",
            new XElement(ns + "tid", tid),
            new XElement(ns + "benid", benid),
            new XElement(ns + "id", sessionId));

        var envelope = new XElement(SoapEnv + "Envelope",
            new XAttribute(XNamespace.Xmlns + "soapenv", SoapEnv.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "fon", ns.NamespaceName),
            new XElement(SoapEnv + "Body", logoutRequest));

        return await SendSoapAsync(
            endpoint,
            soapAction: "logout",
            envelope,
            request.Correlation.CorrelationId,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<FinanzOnlineSessionTransportResponse> SendSoapAsync(
        string endpoint,
        string soapAction,
        XElement envelope,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var xml = new StringBuilder();
        xml.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        xml.Append(envelope.ToString(SaveOptions.DisableFormatting));

        using var msg = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(xml.ToString(), Encoding.UTF8, "text/xml")
        };
        msg.Headers.TryAddWithoutValidation("SOAPAction", $"\"{soapAction}\"");

        try
        {
            using var res = await _httpClient.SendAsync(msg, cancellationToken).ConfigureAwait(false);
            var text = await res.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "FinanzOnline SOAP session HTTP failure Action={SoapAction} Status={Status} CorrelationId={CorrelationId}",
                    soapAction,
                    (int)res.StatusCode,
                    correlationId);
                return new FinanzOnlineSessionTransportResponse
                {
                    Success = false,
                    StatusCode = res.StatusCode,
                    ErrorCode = $"HTTP_{(int)res.StatusCode}",
                    ErrorMessage = TruncateForOperator(string.IsNullOrWhiteSpace(text) ? "SOAP HTTP request failed." : text)
                };
            }

            return ParseSoapEnvelope(text, soapAction, res.StatusCode, correlationId);
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is OperationCanceledException)
        {
            _logger.LogWarning(ex, "FinanzOnline SOAP session transient failure Action={SoapAction} CorrelationId={CorrelationId}", soapAction, correlationId);
            return new FinanzOnlineSessionTransportResponse
            {
                Success = false,
                ErrorCode = "TRANSIENT_NETWORK_FAILURE",
                ErrorMessage = "Transient network failure."
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FinanzOnline SOAP session unexpected failure Action={SoapAction} CorrelationId={CorrelationId}", soapAction, correlationId);
            return new FinanzOnlineSessionTransportResponse
            {
                Success = false,
                ErrorCode = "SOAP_CLIENT_ERROR",
                ErrorMessage = "Unexpected error while calling session service."
            };
        }
    }

    private FinanzOnlineSessionTransportResponse ParseSoapEnvelope(
        string text,
        string soapAction,
        HttpStatusCode httpStatus,
        string correlationId)
    {
        try
        {
            var doc = XDocument.Parse(text, LoadOptions.PreserveWhitespace);
            var fault = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Fault");
            if (fault != null)
            {
                var faultString = fault.Descendants().FirstOrDefault(e => e.Name.LocalName == "faultstring")?.Value
                    ?? fault.Descendants().FirstOrDefault(e => e.Name.LocalName == "Reason")?.Value
                    ?? "SOAP Fault";
                _logger.LogWarning(
                    "FinanzOnline SOAP Fault Action={SoapAction} CorrelationId={CorrelationId} FaultSummary={FaultSummary}",
                    soapAction,
                    correlationId,
                    TruncateForLog(faultString));
                return new FinanzOnlineSessionTransportResponse
                {
                    Success = false,
                    StatusCode = httpStatus,
                    ErrorCode = "SOAP_FAULT",
                    ErrorMessage = TruncateForOperator(faultString),
                    IsMalformedResponse = false
                };
            }

            if (string.Equals(soapAction, "login", StringComparison.OrdinalIgnoreCase))
            {
                var loginResponse = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "loginResponse");
                if (loginResponse == null)
                {
                    _logger.LogWarning("FinanzOnline SOAP login: missing loginResponse CorrelationId={CorrelationId}", correlationId);
                    return Malformed();
                }

                var rcText = loginResponse.Elements().FirstOrDefault(e => e.Name.LocalName == "rc")?.Value;
                var msgText = loginResponse.Elements().FirstOrDefault(e => e.Name.LocalName == "msg")?.Value;
                var idText = loginResponse.Elements().FirstOrDefault(e => e.Name.LocalName == "id")?.Value;

                if (!int.TryParse(rcText, out var rc))
                {
                    _logger.LogWarning("FinanzOnline SOAP login: invalid rc CorrelationId={CorrelationId}", correlationId);
                    return Malformed();
                }

                if (rc == 0 && !string.IsNullOrWhiteSpace(idText))
                {
                    _logger.LogInformation(
                        "FinanzOnline SOAP login ok CorrelationId={CorrelationId} SessionIdLength={SessionIdLength}",
                        correlationId,
                        idText!.Length);
                    return new FinanzOnlineSessionTransportResponse
                    {
                        Success = true,
                        StatusCode = httpStatus,
                        SessionToken = idText.Trim(),
                        ExpiresAtUtc = null
                    };
                }

                var code = $"FON_RC_{rc}";
                _logger.LogWarning(
                    "FinanzOnline SOAP login rejected CorrelationId={CorrelationId} Rc={Rc} MessageLength={MessageLength}",
                    correlationId,
                    rc,
                    msgText?.Length ?? 0);
                return new FinanzOnlineSessionTransportResponse
                {
                    Success = false,
                    StatusCode = httpStatus,
                    ErrorCode = code,
                    ErrorMessage = string.IsNullOrWhiteSpace(msgText) ? $"Session service returned rc={rc}." : TruncateForOperator(msgText!)
                };
            }

            if (string.Equals(soapAction, "logout", StringComparison.OrdinalIgnoreCase))
            {
                var logoutResponse = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "logoutResponse");
                if (logoutResponse == null)
                {
                    _logger.LogWarning("FinanzOnline SOAP logout: missing logoutResponse CorrelationId={CorrelationId}", correlationId);
                    return Malformed();
                }

                var rcText = logoutResponse.Elements().FirstOrDefault(e => e.Name.LocalName == "rc")?.Value;
                var msgText = logoutResponse.Elements().FirstOrDefault(e => e.Name.LocalName == "msg")?.Value;
                if (!int.TryParse(rcText, out var rc))
                    return Malformed();

                if (rc == 0)
                {
                    _logger.LogInformation("FinanzOnline SOAP logout ok CorrelationId={CorrelationId}", correlationId);
                    return new FinanzOnlineSessionTransportResponse { Success = true, StatusCode = httpStatus };
                }

                var code = $"FON_RC_{rc}";
                _logger.LogWarning(
                    "FinanzOnline SOAP logout rejected CorrelationId={CorrelationId} Rc={Rc}",
                    correlationId,
                    rc);
                return new FinanzOnlineSessionTransportResponse
                {
                    Success = false,
                    StatusCode = httpStatus,
                    ErrorCode = code,
                    ErrorMessage = string.IsNullOrWhiteSpace(msgText) ? $"Session service returned rc={rc}." : TruncateForOperator(msgText!)
                };
            }

            return Malformed();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FinanzOnline SOAP parse failure CorrelationId={CorrelationId}", correlationId);
            return Malformed();
        }

        FinanzOnlineSessionTransportResponse Malformed() =>
            new()
            {
                Success = false,
                StatusCode = httpStatus,
                IsMalformedResponse = true,
                ErrorCode = "MALFORMED_RESPONSE",
                ErrorMessage = "Unable to parse session SOAP response."
            };
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

    private async Task<string> ResolveSoapEndpointAsync(FinanzOnlineSessionOptions opts, CancellationToken cancellationToken)
    {
        var snap = await _connectivitySource.GetCompanySettingsSnapshotAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(snap?.BaseUrl))
            return snap.BaseUrl!.Trim();
        return (opts.BaseUrl ?? string.Empty).Trim();
    }
}
