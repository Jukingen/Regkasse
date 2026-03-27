using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

public sealed class SimulatedFinanzOnlineSessionClient : IFinanzOnlineSessionClient
{
    private readonly ILogger<SimulatedFinanzOnlineSessionClient> _logger;
    private readonly Dictionary<string, (string Token, DateTimeOffset ExpiresAtUtc)> _sessions = new();

    public SimulatedFinanzOnlineSessionClient(ILogger<SimulatedFinanzOnlineSessionClient> logger)
    {
        _logger = logger;
    }

    public Task<FinanzOnlineSessionLoginResponse> LoginAsync(FinanzOnlineSessionLoginRequest request, CancellationToken cancellationToken = default)
    {
        var token = $"sim-fo-{request.Correlation.CorrelationId}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        var cacheKey = BuildKey(request.Mode, request.Scope);
        _sessions[cacheKey] = (token, DateTimeOffset.UtcNow.AddMinutes(15));
        _logger.LogDebug("Simulated FinanzOnline session login for BusinessKey={BusinessKey}", request.Correlation.BusinessKey);
        return Task.FromResult(new FinanzOnlineSessionLoginResponse
        {
            Success = true,
            SessionToken = token,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(15)
        });
    }

    public Task<FinanzOnlineSessionLogoutResponse> LogoutAsync(FinanzOnlineSessionLogoutRequest request, CancellationToken cancellationToken = default)
    {
        _sessions.Remove(BuildKey(request.Mode, request.Scope));
        _logger.LogDebug("Simulated FinanzOnline session logout for CorrelationId={CorrelationId}", request.Correlation.CorrelationId);
        return Task.FromResult(new FinanzOnlineSessionLogoutResponse { Success = true });
    }

    public async Task<FinanzOnlineSessionAccessResult> GetValidSessionAsync(FinanzOnlineSessionLoginRequest request, CancellationToken cancellationToken = default)
    {
        var key = BuildKey(request.Mode, request.Scope);
        if (_sessions.TryGetValue(key, out var session) && session.ExpiresAtUtc > DateTimeOffset.UtcNow)
        {
            return new FinanzOnlineSessionAccessResult
            {
                Success = true,
                SessionToken = session.Token,
                ExpiresAtUtc = session.ExpiresAtUtc,
                FromCache = true
            };
        }

        var login = await LoginAsync(request, cancellationToken).ConfigureAwait(false);
        if (!login.Success || string.IsNullOrWhiteSpace(login.SessionToken))
        {
            return new FinanzOnlineSessionAccessResult
            {
                Success = false,
                ErrorCode = login.ErrorCode,
                ErrorMessage = login.ErrorMessage,
                FailureKind = FinanzOnlineSessionFailureKind.Retryable
            };
        }

        return new FinanzOnlineSessionAccessResult
        {
            Success = true,
            SessionToken = login.SessionToken,
            ExpiresAtUtc = login.ExpiresAtUtc,
            FromCache = false
        };
    }

    public async Task InvalidateSessionAsync(FinanzOnlineSessionLogoutRequest request, CancellationToken cancellationToken = default)
    {
        _sessions.Remove(BuildKey(request.Mode, request.Scope));
        await LogoutAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static string BuildKey(FinanzOnlineIntegrationMode mode, FinanzOnlineScope scope)
    {
        return $"{mode}:{scope.TenantId}:{scope.BranchId}:{scope.RegisterId}";
    }
}

public sealed class SimulatedFinanzOnlineRegistrierkassenClient : IFinanzOnlineRegistrierkassenClient
{
    private readonly ILogger<SimulatedFinanzOnlineRegistrierkassenClient> _logger;

    public SimulatedFinanzOnlineRegistrierkassenClient(ILogger<SimulatedFinanzOnlineRegistrierkassenClient> logger)
    {
        _logger = logger;
    }

    public Task<FinanzOnlineRegisterSubmissionResponse> SubmitAsync(
        FinanzOnlineRegisterSubmissionRequest request,
        FinanzOnlineMappedCommand mappedCommand,
        string sessionToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            return Task.FromResult(new FinanzOnlineRegisterSubmissionResponse
            {
                Success = false,
                ErrorCode = "SESSION_REQUIRED",
                ErrorMessage = "Session token is required."
            });
        }

        var now = DateTime.UtcNow;
        var transmissionId = $"SIM-TX-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
        var referenceId = $"FIN_{now:yyyyMMddHHmmss}_{request.Correlation.BusinessKey}";
        _logger.LogDebug("Simulated FinanzOnline submission Operation={Operation} BusinessKey={BusinessKey}",
            mappedCommand.OperationName, request.Correlation.BusinessKey);

        return Task.FromResult(new FinanzOnlineRegisterSubmissionResponse
        {
            Success = true,
            TransmissionId = transmissionId,
            ReferenceId = referenceId,
            Status = "Submitted",
            ProtocolCode = "SIM_ACCEPTED"
        });
    }
}

public sealed class SimulatedFinanzOnlineTransmissionQueryClient : IFinanzOnlineTransmissionQueryClient
{
    public Task<FinanzOnlineTransmissionStatusQueryResponse> QueryStatusAsync(FinanzOnlineTransmissionStatusQueryRequest request, CancellationToken cancellationToken = default)
    {
        var protocol = new List<FinanzOnlineTransmissionProtocolEntry>
        {
            new()
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                Level = "Info",
                Message = "Simulated protocol entry."
            }
        };

        return Task.FromResult(new FinanzOnlineTransmissionStatusQueryResponse
        {
            Success = true,
            TransmissionId = request.TransmissionId,
            Status = "Submitted",
            Protocol = protocol
        });
    }
}

public sealed class DefaultFinanzOnlineCommandMapper : IFinanzOnlineCommandMapper
{
    private readonly IOptionsMonitor<FinanzOnlineRegistrierkassenOptions> _rkdbOptions;

    public DefaultFinanzOnlineCommandMapper(IOptionsMonitor<FinanzOnlineRegistrierkassenOptions> rkdbOptions)
    {
        _rkdbOptions = rkdbOptions;
    }

    public FinanzOnlineMappedCommand MapRegisterSubmission(FinanzOnlineRegisterSubmissionRequest request)
    {
        var metadata = new Dictionary<string, string>
        {
            ["mode"] = request.Mode.ToString(),
            ["registerId"] = request.Scope.RegisterId,
            ["businessKey"] = request.Correlation.BusinessKey,
            ["payloadHash"] = request.Correlation.PayloadHash,
            ["correlationId"] = request.Correlation.CorrelationId
        };

        var mapped = new FinanzOnlineMappedCommand
        {
            OperationName = request.SubmissionKind == FinanzOnlineSubmissionKind.SignatureUnit
                ? "Registrierkassen.SignatureUnit.Submit"
                : "Registrierkassen.Register.Submit",
            TransportPayload = request.PayloadJson,
            TransportFormat = "application/json",
            Metadata = metadata
        };

        if (request.RkdbBelegpruefung != null)
        {
            if (request.Mode != FinanzOnlineIntegrationMode.TEST)
            {
                mapped.RkdbValidationErrorCode = "RKDB_MODE_NOT_SUPPORTED";
                mapped.RkdbBuildError = "RKDB belegpruefung mapping is only supported for TEST mode in this release.";
                mapped.TransportPayload = string.Empty;
                return mapped;
            }

            var errors = FinanzOnlineRkdbBelegpruefungValidator.Validate(request.RkdbBelegpruefung);
            if (errors.Count > 0)
            {
                mapped.RkdbValidationErrorCode = "RKDB_COMMAND_INVALID";
                mapped.RkdbBuildError = string.Join("; ", errors);
                mapped.TransportPayload = string.Empty;
                return mapped;
            }

            var ns = string.IsNullOrWhiteSpace(_rkdbOptions.CurrentValue.SoapNamespace)
                ? "https://finanzonline.bmf.gv.at/rkdb"
                : _rkdbOptions.CurrentValue.SoapNamespace.Trim();

            var xml = FinanzOnlineRkdbBelegpruefungXmlBuilder.Build(ns, request.RkdbBelegpruefung);
            var structErrors = FinanzOnlineRkdbInnerXmlStructureValidator.ValidateBelegpruefungDocument(xml, ns);
            if (structErrors.Count > 0)
            {
                mapped.RkdbValidationErrorCode = "RKDB_XML_STRUCTURE_INVALID";
                mapped.RkdbBuildError = string.Join("; ", structErrors);
                mapped.TransportPayload = string.Empty;
                return mapped;
            }

            mapped.RkdbPayloadXml = xml;
            mapped.TransportFormat = "application/xml+rksv-rkdb-inner";
            mapped.TransportPayload = string.Empty;
            metadata["rkdbPayloadKind"] = "belegpruefung";
            metadata["rkdbStructureValidated"] = "belegpruefung_v1";
        }

        return mapped;
    }

    public FinanzOnlineMappedCommand MapTransmissionStatusQuery(FinanzOnlineTransmissionStatusQueryRequest request)
    {
        return new FinanzOnlineMappedCommand
        {
            OperationName = "Registrierkassen.rkdb.status_kasse",
            TransportPayload = string.Empty,
            TransportFormat = "application/xml+rksv-rkdb-status",
            Metadata = new Dictionary<string, string>
            {
                ["mode"] = request.Mode.ToString(),
                ["registerId"] = request.Scope.RegisterId,
                ["businessKey"] = request.Correlation.BusinessKey,
                ["payloadHash"] = request.Correlation.PayloadHash,
                ["correlationId"] = request.Correlation.CorrelationId
            }
        };
    }
}

public sealed class FinanzOnlineSubmissionService : IFinanzOnlineSubmissionService
{
    private readonly IFinanzOnlineSessionClient _sessionClient;
    private readonly IFinanzOnlineRegistrierkassenClient _registrierkassenClient;
    private readonly IFinanzOnlineCommandMapper _commandMapper;
    private readonly IOptionsMonitor<FinanzOnlineRegistrierkassenOptions> _registrierkassenOptions;

    public FinanzOnlineSubmissionService(
        IFinanzOnlineSessionClient sessionClient,
        IFinanzOnlineRegistrierkassenClient registrierkassenClient,
        IFinanzOnlineCommandMapper commandMapper,
        IOptionsMonitor<FinanzOnlineRegistrierkassenOptions> registrierkassenOptions)
    {
        _sessionClient = sessionClient;
        _registrierkassenClient = registrierkassenClient;
        _commandMapper = commandMapper;
        _registrierkassenOptions = registrierkassenOptions;
    }

    public async Task<FinanzOnlineRegisterSubmissionResponse> SubmitAsync(FinanzOnlineRegisterSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        var mapped = _commandMapper.MapRegisterSubmission(request);

        var rkOpts = _registrierkassenOptions.CurrentValue;
        if (!rkOpts.UseSimulation && rkOpts.EnableRealTestSubmission && request.Mode == FinanzOnlineIntegrationMode.TEST)
        {
            if (!string.IsNullOrWhiteSpace(mapped.RkdbBuildError))
            {
                return new FinanzOnlineRegisterSubmissionResponse
                {
                    Success = false,
                    ErrorCode = mapped.RkdbValidationErrorCode ?? "RKDB_PAYLOAD_INVALID",
                    ErrorMessage = mapped.RkdbBuildError
                };
            }

            if (string.IsNullOrWhiteSpace(mapped.RkdbPayloadXml))
            {
                return new FinanzOnlineRegisterSubmissionResponse
                {
                    Success = false,
                    ErrorCode = "RKDB_XML_PAYLOAD_REQUIRED",
                    ErrorMessage = "Real TEST rkdb submission requires RkdbPayloadXml (e.g. belegpruefung). JSON-only payloads are not accepted."
                };
            }
        }

        var session = await _sessionClient.GetValidSessionAsync(new FinanzOnlineSessionLoginRequest
        {
            Mode = request.Mode,
            Scope = request.Scope,
            Correlation = request.Correlation
        }, cancellationToken).ConfigureAwait(false);

        if (!session.Success || string.IsNullOrWhiteSpace(session.SessionToken))
        {
            return new FinanzOnlineRegisterSubmissionResponse
            {
                Success = false,
                ErrorCode = session.ErrorCode ?? "LOGIN_FAILED",
                ErrorMessage = session.ErrorMessage ?? "Session login failed."
            };
        }

        try
        {
            return await _registrierkassenClient
                .SubmitAsync(request, mapped, session.SessionToken, cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            await _sessionClient.InvalidateSessionAsync(new FinanzOnlineSessionLogoutRequest
            {
                Mode = request.Mode,
                Scope = request.Scope,
                SessionToken = session.SessionToken,
                Correlation = request.Correlation
            }, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }
}
