using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

public sealed class FinanzOnlineTransmissionQueryOptions
{
    public const string SectionName = "FinanzOnline:TransmissionQuery";
    public bool UseSimulation { get; set; } = true;
    public bool EnableRealTestQuery { get; set; } = false;

    /// <summary>Reserved; real query uses <see cref="FinanzOnlineRegistrierkassenOptions.BaseUrl"/> (same rkdb SOAP endpoint).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Unused; kept for config backward compatibility.</summary>
    public string QueryPath { get; set; } = "/registrierkassen/query";

    public int RequestTimeoutSeconds { get; set; } = 20;
}

public interface IFinanzOnlineTransmissionQueryTransport
{
    Task<FinanzOnlineTransmissionStatusQueryResponse> QueryAsync(
        FinanzOnlineTransmissionStatusQueryRequest request,
        FinanzOnlineMappedCommand mappedCommand,
        string sessionToken,
        CancellationToken cancellationToken = default);
}

public sealed class TestModeFinanzOnlineTransmissionQueryClient : IFinanzOnlineTransmissionQueryClient
{
    private readonly IOptionsMonitor<FinanzOnlineTransmissionQueryOptions> _options;
    private readonly IFinanzOnlineSessionClient _sessionClient;
    private readonly IFinanzOnlineCommandMapper _mapper;
    private readonly IFinanzOnlineTransmissionQueryTransport _transport;
    private readonly ILogger<TestModeFinanzOnlineTransmissionQueryClient> _logger;

    public TestModeFinanzOnlineTransmissionQueryClient(
        IOptionsMonitor<FinanzOnlineTransmissionQueryOptions> options,
        IFinanzOnlineSessionClient sessionClient,
        IFinanzOnlineCommandMapper mapper,
        IFinanzOnlineTransmissionQueryTransport transport,
        ILogger<TestModeFinanzOnlineTransmissionQueryClient> logger)
    {
        _options = options;
        _sessionClient = sessionClient;
        _mapper = mapper;
        _transport = transport;
        _logger = logger;
    }

    public async Task<FinanzOnlineTransmissionStatusQueryResponse> QueryStatusAsync(FinanzOnlineTransmissionStatusQueryRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Mode != FinanzOnlineIntegrationMode.TEST)
        {
            return new FinanzOnlineTransmissionStatusQueryResponse
            {
                Success = false,
                TransmissionId = request.TransmissionId,
                ErrorCode = "MODE_NOT_ALLOWED",
                ErrorMessage = "Transmission query adapter supports TEST mode only."
            };
        }

        if (!_options.CurrentValue.EnableRealTestQuery)
        {
            return new FinanzOnlineTransmissionStatusQueryResponse
            {
                Success = false,
                TransmissionId = request.TransmissionId,
                ErrorCode = "TEST_REAL_QUERY_DISABLED",
                ErrorMessage = "Real TEST transmission query is disabled."
            };
        }

        var contextErrors = FinanzOnlineStatusKasseQueryContextValidator.ValidateForStatusKasse(request);
        if (contextErrors.Count > 0)
        {
            return new FinanzOnlineTransmissionStatusQueryResponse
            {
                Success = false,
                TransmissionId = request.TransmissionId,
                ErrorCode = "RKDB_QUERY_CONTEXT_INVALID",
                ErrorMessage = string.Join("; ", contextErrors)
            };
        }

        var session = await _sessionClient.GetValidSessionAsync(new FinanzOnlineSessionLoginRequest
        {
            Mode = request.Mode,
            Scope = request.Scope,
            Correlation = request.Correlation
        }, cancellationToken).ConfigureAwait(false);

        if (!session.Success || string.IsNullOrWhiteSpace(session.SessionToken))
        {
            return new FinanzOnlineTransmissionStatusQueryResponse
            {
                Success = false,
                TransmissionId = request.TransmissionId,
                ErrorCode = session.ErrorCode ?? "SESSION_UNAVAILABLE",
                ErrorMessage = session.ErrorMessage ?? "Session unavailable."
            };
        }

        try
        {
            var mapped = _mapper.MapTransmissionStatusQuery(request);
            var response = await _transport.QueryAsync(request, mapped, session.SessionToken, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("FinanzOnline TEST query TransmissionId={TransmissionId} Status={Status} ErrorCode={ErrorCode}",
                request.TransmissionId, response.Status ?? "", response.ErrorCode ?? "");
            return response;
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is OperationCanceledException)
        {
            return new FinanzOnlineTransmissionStatusQueryResponse
            {
                Success = false,
                TransmissionId = request.TransmissionId,
                ErrorCode = "TRANSIENT_QUERY_FAILURE",
                ErrorMessage = "Transient transmission query failure."
            };
        }
    }

    public static string ComputeProtocolHash(IReadOnlyList<FinanzOnlineTransmissionProtocolEntry> protocol)
    {
        var json = JsonSerializer.Serialize(protocol);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
