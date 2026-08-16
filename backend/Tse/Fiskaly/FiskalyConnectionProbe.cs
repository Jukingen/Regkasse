using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Tse.Fiskaly;

/// <summary>
/// Live SIGN AT smoke: auth token, optional SCU (TSS analog), optional cash register (client analog).
/// Does not initialize resources (no FinanzOnline registration).
/// </summary>
public sealed class FiskalyConnectionProbe : IFiskalyConnectionProbe
{
    public const string DefaultTestVatId = "ATU73948115";

    private static readonly Regex AustrianVatId = new(@"^ATU\d{8}$", RegexOptions.CultureInvariant);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<FiskalyOptions> _options;
    private readonly ILogger<FiskalyConnectionProbe> _logger;

    public FiskalyConnectionProbe(
        HttpClient httpClient,
        IOptionsMonitor<FiskalyOptions> options,
        ILogger<FiskalyConnectionProbe> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;

        var baseUrl = options.CurrentValue.BaseUrl;
        if (!string.IsNullOrWhiteSpace(baseUrl))
            _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    }

    public async Task<FiskalyConnectionProbeResult> ProbeAsync(
        FiskalyConnectionProbeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var opts = _options.CurrentValue;
        var apiBaseUrl = string.IsNullOrWhiteSpace(opts.BaseUrl)
            ? "https://rksv.fiskaly.com/api/v1"
            : opts.BaseUrl.TrimEnd('/');

        var auth = await AuthenticateAsync(opts, cancellationToken).ConfigureAwait(false);
        if (!auth.Step.Success || string.IsNullOrWhiteSpace(auth.AccessToken))
        {
            _logger.LogWarning("Fiskaly connection probe: authentication failed ({Message})", auth.Step.Message);
            return new FiskalyConnectionProbeResult
            {
                Success = false,
                Authentication = auth.Step,
                ScuCreation = Skipped("ScuCreation", "Authentication did not succeed."),
                CashRegisterCreation = Skipped("CashRegisterCreation", "Authentication did not succeed."),
                ApiBaseUrl = apiBaseUrl
            };
        }

        _logger.LogInformation(
            "Fiskaly connection probe: authentication succeeded, tokenExpiresAt={ExpiresAt}",
            auth.ExpiresAt);

        if (!request.CreateResources)
        {
            return new FiskalyConnectionProbeResult
            {
                Success = true,
                Authentication = auth.Step,
                ScuCreation = Skipped("ScuCreation", "CreateResources is false."),
                CashRegisterCreation = Skipped("CashRegisterCreation", "CreateResources is false."),
                ApiBaseUrl = apiBaseUrl
            };
        }

        var vatId = NormalizeVatId(request.VatId) ?? DefaultTestVatId;
        var scu = await CreateScuAsync(auth.AccessToken, vatId, cancellationToken).ConfigureAwait(false);
        if (scu.Step.Success)
        {
            _logger.LogInformation(
                "Fiskaly connection probe: SCU created {ScuId}",
                scu.ResourceId);
        }
        else
        {
            _logger.LogWarning(
                "Fiskaly connection probe: SCU creation failed ({Message})",
                scu.Step.Message);
        }

        var cashRegister = await CreateCashRegisterAsync(auth.AccessToken, cancellationToken)
            .ConfigureAwait(false);
        if (cashRegister.Step.Success)
        {
            _logger.LogInformation(
                "Fiskaly connection probe: cash register created {CashRegisterId}",
                cashRegister.ResourceId);
        }
        else
        {
            _logger.LogWarning(
                "Fiskaly connection probe: cash register creation failed ({Message})",
                cashRegister.Step.Message);
        }

        return new FiskalyConnectionProbeResult
        {
            Success = auth.Step.Success && scu.Step.Success && cashRegister.Step.Success,
            Authentication = auth.Step,
            ScuCreation = scu.Step,
            CashRegisterCreation = cashRegister.Step,
            ScuId = scu.ResourceId,
            CashRegisterId = cashRegister.ResourceId,
            VatIdUsed = vatId,
            ApiBaseUrl = apiBaseUrl
        };
    }

    private async Task<(FiskalyConnectionStepResult Step, string? AccessToken, DateTimeOffset? ExpiresAt)> AuthenticateAsync(
        FiskalyOptions opts,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(opts.ApiKey) || string.IsNullOrWhiteSpace(opts.ApiSecret))
        {
            return (Failed("Authentication", null, "Fiskaly:ApiKey / Fiskaly:ApiSecret are not configured."), null, null);
        }

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                "auth",
                new FiskalyAuthRequestDto { ApiKey = opts.ApiKey, ApiSecret = opts.ApiSecret },
                cancellationToken).ConfigureAwait(false);

            var body = await ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return (Failed("Authentication", (int)response.StatusCode, Truncate(body)), null, null);
            }

            var dto = JsonSerializer.Deserialize<FiskalyAuthResponseDto>(body, JsonOptions);
            if (string.IsNullOrWhiteSpace(dto?.AccessToken))
            {
                return (Failed("Authentication", (int)response.StatusCode, "Auth succeeded but access_token was empty."), null, null);
            }

            return (
                Succeeded(
                    "Authentication",
                    (int)response.StatusCode,
                    dto.ExpiresAt is { } expires
                        ? $"Token received; expires at {expires:O}."
                        : "Token received."),
                dto.AccessToken,
                dto.ExpiresAt);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fiskaly connection probe: authentication request failed.");
            return (Failed("Authentication", null, ex.Message), null, null);
        }
    }

    private async Task<(FiskalyConnectionStepResult Step, string? ResourceId)> CreateScuAsync(
        string accessToken,
        string vatId,
        CancellationToken cancellationToken)
    {
        if (!AustrianVatId.IsMatch(vatId))
        {
            return (Failed("ScuCreation", null, $"Invalid VAT id '{vatId}'. Expected ATU########."), null);
        }

        var scuId = Guid.NewGuid();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, $"signature-creation-unit/{scuId:D}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = JsonContent.Create(new
            {
                legal_entity_id = new { vat_id = vatId },
                legal_entity_name = "Regkasse Development Probe",
                metadata = new Dictionary<string, string>
                {
                    ["source"] = "regkasse-dev-probe"
                }
            });

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var body = await ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return (Failed("ScuCreation", (int)response.StatusCode, Truncate(body)), null);
            }

            var dto = JsonSerializer.Deserialize<FiskalyResourceResponseDto>(body, JsonOptions);
            var id = string.IsNullOrWhiteSpace(dto?.Id) ? scuId.ToString("D") : dto.Id;
            var state = string.IsNullOrWhiteSpace(dto?.State) ? "CREATED" : dto.State;
            return (Succeeded("ScuCreation", (int)response.StatusCode, $"SCU {id} state={state}."), id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fiskaly connection probe: SCU create failed.");
            return (Failed("ScuCreation", null, ex.Message), null);
        }
    }

    private async Task<(FiskalyConnectionStepResult Step, string? ResourceId)> CreateCashRegisterAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        var cashRegisterId = Guid.NewGuid();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, $"cash-register/{cashRegisterId:D}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = JsonContent.Create(new
            {
                description = "Regkasse Development Probe",
                metadata = new Dictionary<string, string>
                {
                    ["source"] = "regkasse-dev-probe"
                }
            });

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var body = await ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return (Failed("CashRegisterCreation", (int)response.StatusCode, Truncate(body)), null);
            }

            var dto = JsonSerializer.Deserialize<FiskalyResourceResponseDto>(body, JsonOptions);
            var id = string.IsNullOrWhiteSpace(dto?.Id) ? cashRegisterId.ToString("D") : dto.Id;
            var state = string.IsNullOrWhiteSpace(dto?.State) ? "CREATED" : dto.State;
            return (Succeeded("CashRegisterCreation", (int)response.StatusCode, $"Cash register {id} state={state}."), id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fiskaly connection probe: cash register create failed.");
            return (Failed("CashRegisterCreation", null, ex.Message), null);
        }
    }

    private static string? NormalizeVatId(string? vatId)
    {
        if (string.IsNullOrWhiteSpace(vatId))
            return null;

        return vatId.Trim().ToUpperInvariant();
    }

    private static async Task<string> ReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        return response.Content is null
            ? string.Empty
            : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string Truncate(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return "(empty response)";

        var trimmed = body.Trim();
        return trimmed.Length <= 500 ? trimmed : trimmed[..500] + "…";
    }

    private static FiskalyConnectionStepResult Succeeded(string name, int? status, string message) => new()
    {
        Name = name,
        Status = "Succeeded",
        HttpStatus = status,
        Message = message
    };

    private static FiskalyConnectionStepResult Failed(string name, int? status, string message) => new()
    {
        Name = name,
        Status = "Failed",
        HttpStatus = status,
        Message = message
    };

    private static FiskalyConnectionStepResult Skipped(string name, string message) => new()
    {
        Name = name,
        Status = "Skipped",
        Message = message
    };

    private sealed class FiskalyAuthRequestDto
    {
        [JsonPropertyName("api_key")]
        public string ApiKey { get; set; } = string.Empty;

        [JsonPropertyName("api_secret")]
        public string ApiSecret { get; set; } = string.Empty;
    }

    private sealed class FiskalyAuthResponseDto
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("expires_at")]
        public DateTimeOffset? ExpiresAt { get; set; }
    }

    private sealed class FiskalyResourceResponseDto
    {
        [JsonPropertyName("_id")]
        public string? Id { get; set; }

        [JsonPropertyName("state")]
        public string? State { get; set; }
    }
}
