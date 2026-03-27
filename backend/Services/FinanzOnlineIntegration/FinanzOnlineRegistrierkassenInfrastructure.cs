using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

public sealed class FinanzOnlineRegistrierkassenOptions
{
    public const string SectionName = "FinanzOnline:Registrierkassen";
    public bool UseSimulation { get; set; } = true;
    public bool EnableRealTestSubmission { get; set; } = false;

    /// <summary>SOAP endpoint for rkdb operation (WSDL soap:address), e.g. https://finanzonline.bmf.gv.at/fonws/ws/rkdb</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>WSDL targetNamespace for regKasseService / rkdb.</summary>
    public string SoapNamespace { get; set; } = "https://finanzonline.bmf.gv.at/rkdb";

    public string SoapAction { get; set; } = "rkdb";
    public int RequestTimeoutSeconds { get; set; } = 30;
}

public interface IFinanzOnlineRegistrierkassenTransport
{
    Task<FinanzOnlineRegisterSubmissionResponse> SubmitAsync(
        FinanzOnlineRegisterSubmissionRequest request,
        FinanzOnlineMappedCommand mappedCommand,
        string sessionToken,
        CancellationToken cancellationToken = default);
}

public sealed class TestModeFinanzOnlineRegistrierkassenClient : IFinanzOnlineRegistrierkassenClient
{
    private readonly IOptionsMonitor<FinanzOnlineRegistrierkassenOptions> _options;
    private readonly IFinanzOnlineRegistrierkassenTransport _transport;
    private readonly ILogger<TestModeFinanzOnlineRegistrierkassenClient> _logger;

    public TestModeFinanzOnlineRegistrierkassenClient(
        IOptionsMonitor<FinanzOnlineRegistrierkassenOptions> options,
        IFinanzOnlineRegistrierkassenTransport transport,
        ILogger<TestModeFinanzOnlineRegistrierkassenClient> logger)
    {
        _options = options;
        _transport = transport;
        _logger = logger;
    }

    public async Task<FinanzOnlineRegisterSubmissionResponse> SubmitAsync(
        FinanzOnlineRegisterSubmissionRequest request,
        FinanzOnlineMappedCommand mappedCommand,
        string sessionToken,
        CancellationToken cancellationToken = default)
    {
        if (request.Mode != FinanzOnlineIntegrationMode.TEST)
        {
            _logger.LogWarning("FinanzOnline TEST adapter rejected non-TEST mode. CorrelationId={CorrelationId} Mode={Mode}",
                request.Correlation.CorrelationId, request.Mode);
            return new FinanzOnlineRegisterSubmissionResponse
            {
                Success = false,
                ErrorCode = "MODE_NOT_ALLOWED",
                ErrorMessage = "This adapter supports TEST mode only."
            };
        }

        var options = _options.CurrentValue;
        if (!options.EnableRealTestSubmission)
        {
            return new FinanzOnlineRegisterSubmissionResponse
            {
                Success = false,
                ErrorCode = "TEST_REAL_SUBMISSION_DISABLED",
                ErrorMessage = "Real TEST submission is disabled."
            };
        }

        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            return new FinanzOnlineRegisterSubmissionResponse
            {
                Success = false,
                ErrorCode = "SESSION_REQUIRED",
                ErrorMessage = "Session token is required."
            };
        }

        _logger.LogInformation("FinanzOnline TEST submission start Operation={Operation} BusinessKey={BusinessKey} CorrelationId={CorrelationId}",
            mappedCommand.OperationName, request.Correlation.BusinessKey, request.Correlation.CorrelationId);

        var result = await _transport.SubmitAsync(request, mappedCommand, sessionToken, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("FinanzOnline TEST submission finished Success={Success} Status={Status} ErrorCode={ErrorCode} CorrelationId={CorrelationId}",
            result.Success, result.Status ?? "", result.ErrorCode ?? "", request.Correlation.CorrelationId);
        return result;
    }
}
