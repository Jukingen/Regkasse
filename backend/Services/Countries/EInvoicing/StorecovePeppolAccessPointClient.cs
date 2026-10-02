using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Countries.EInvoicing;

/// <summary>
/// Storecove document submission client. Selected only when <c>Peppol:Provider=storecove</c>.
/// <c>PeppolSubmissionService</c> calls it only for the Reserved-exit canary tenant in TEST.
/// <c>LIVE</c> does not open a socket. HTTP 2xx is <see cref="PeppolSubmissionStatus.Sent"/>, not Ack.
/// </summary>
public sealed class StorecovePeppolAccessPointClient : IPeppolAccessPointClient
{
    public const string DocumentSubmissionsPath = "document_submissions";
    public const string ParticipantNotConfiguredCode = "peppol-participant-not-configured";
    public const string LiveNotAllowedCode = "peppol-live-not-allowed";

    private readonly PeppolOptions _options;
    private readonly HttpClient _http;
    private readonly IDbContextFactory<AppDbContext>? _dbFactory;

    public StorecovePeppolAccessPointClient(
        IOptions<PeppolOptions> options,
        HttpClient http,
        IDbContextFactory<AppDbContext>? dbFactory = null)
    {
        _options = options.Value;
        _http = http;
        _dbFactory = dbFactory;
    }

    public Task<PeppolTransportResult> SubmitAsync(
        PeppolSubmitRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendAsync(HttpMethod.Post, DocumentSubmissionsPath, request, body: null, cancellationToken);
    }

    public Task<PeppolTransportResult> GetStatusAsync(
        string submissionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(submissionId))
            throw new ArgumentException("Storecove submission id is required.", nameof(submissionId));

        var path = DocumentSubmissionsPath + "/" + Uri.EscapeDataString(submissionId.Trim());
        return SendAsync(HttpMethod.Get, path, submit: null, body: null, cancellationToken);
    }

    private async Task<PeppolTransportResult> SendAsync(
        HttpMethod method,
        string path,
        PeppolSubmitRequest? submit,
        string? body,
        CancellationToken cancellationToken)
    {
        EnsureCanCall();
        if (method == HttpMethod.Post)
        {
            var identity = await RequireParticipantIdentityAsync(submit!, cancellationToken).ConfigureAwait(false);
            body = BuildSubmissionJson(submit!, identity);
        }

        var url = _options.Storecove.BaseUrl.TrimEnd('/') + "/" + path;

        try
        {
            using var message = new HttpRequestMessage(method, url);
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Storecove.ApiKey);
            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (body is not null)
                message.Content = new StringContent(body, Encoding.UTF8, "application/json");

            using var response = await _http.SendAsync(message, cancellationToken).ConfigureAwait(false);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new PeppolTransportException(
                    $"Storecove returned HTTP {(int)response.StatusCode}.",
                    statusCode: (int)response.StatusCode);
            }

            var guid = TryReadString(payload, "guid");
            var providerState = TryReadString(payload, "state") ?? TryReadString(payload, "status");
            return new PeppolTransportResult(
                PeppolSubmissionStatus.Sent,
                providerState ?? ((int)response.StatusCode).ToString(),
                guid,
                TryReadProviderCode(payload));
        }
        catch (PeppolTransportException)
        {
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            throw new PeppolTransportException("Storecove request failed.", ex);
        }
    }

    private void EnsureCanCall()
    {
        var environment = _options.Storecove.Environment?.Trim() ?? string.Empty;
        if (!string.Equals(environment, "TEST", StringComparison.Ordinal))
        {
            throw new PeppolTransportException(
                "Storecove HTTP is allowed only when Peppol:Storecove:Environment is exactly TEST.",
                code: LiveNotAllowedCode);
        }

        if (string.IsNullOrWhiteSpace(_options.Storecove.BaseUrl)
            || string.IsNullOrWhiteSpace(_options.Storecove.ApiKey))
        {
            throw new PeppolTransportException(
                "Storecove base URL or API key is missing. The document was not sent.");
        }
    }

    private async Task<StorecoveParticipantIdentity> RequireParticipantIdentityAsync(
        PeppolSubmitRequest request,
        CancellationToken cancellationToken)
    {
        if (_dbFactory is null || request.TenantId == Guid.Empty)
            throw ParticipantNotConfigured();

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var rows = await db.PeppolParticipants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(row => row.TenantId == request.TenantId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        PeppolParticipant? match = null;
        if (!string.IsNullOrWhiteSpace(request.ParticipantId))
        {
            var participantId = request.ParticipantId.Trim();
            match = rows.FirstOrDefault(row =>
                string.Equals(row.ParticipantId, participantId, StringComparison.Ordinal));
        }
        else if (rows.Count == 1)
        {
            match = rows[0];
        }

        if (match is null
            || string.IsNullOrWhiteSpace(match.LegalEntityId)
            || string.IsNullOrWhiteSpace(match.EIdentifierScheme)
            || string.IsNullOrWhiteSpace(match.EIdentifierValue))
        {
            throw ParticipantNotConfigured();
        }

        return new StorecoveParticipantIdentity(
            match.LegalEntityId.Trim(),
            match.EIdentifierScheme.Trim(),
            match.EIdentifierValue.Trim());
    }

    private static PeppolTransportException ParticipantNotConfigured() =>
        new(
            "Peppol participant identity is not configured. The document was not sent.",
            code: ParticipantNotConfiguredCode);

    internal static string BuildSubmissionJson(PeppolSubmitRequest request, StorecoveParticipantIdentity identity)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("legalEntityId", identity.LegalEntityId);
            if (Guid.TryParse(request.SubmissionId, out var idempotency))
                writer.WriteString("idempotencyGuid", idempotency);

            writer.WritePropertyName("routing");
            writer.WriteStartObject();
            writer.WritePropertyName("eIdentifiers");
            writer.WriteStartArray();
            writer.WriteStartObject();
            writer.WriteString("scheme", identity.Scheme);
            writer.WriteString("id", identity.Value);
            writer.WriteEndObject();
            writer.WriteEndArray();
            writer.WriteEndObject();

            writer.WritePropertyName("document");
            writer.WriteStartObject();
            writer.WriteString("documentType", "invoice");
            writer.WritePropertyName("rawDocumentData");
            writer.WriteStartObject();
            writer.WriteString(
                "document",
                Convert.ToBase64String(Encoding.UTF8.GetBytes(request.UblXml ?? string.Empty)));
            writer.WriteBoolean("parse", false);
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// Storecove submission <c>code</c> only. Not the response body.
    /// Transient codes used by the ACK poller are <c>STORE_INTERNAL</c>, <c>TIMEOUT</c>, and <c>RATE_LIMIT</c>.
    /// </summary>
    internal static string? TryReadProviderCode(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            var root = doc.RootElement;
            var code = ReadCode(root, "code") ?? ReadCode(root, "errorCode");
            if (code is null
                && root.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.Object)
            {
                code = ReadCode(error, "code");
            }

            if (code is null
                && root.TryGetProperty("errors", out var errors)
                && errors.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in errors.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                        continue;
                    code = ReadCode(item, "code");
                    if (code is not null)
                        break;
                }
            }

            return code;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadCode(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
            return null;

        var text = value.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(text) || text.Length > 64 || text.Contains('<') || text.Contains(' '))
            return null;

        return text;
    }

    private static string? TryReadString(string json, string name)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty(name, out var value)
                || value.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var text = value.GetString();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// Picks the Access Point client from <c>Peppol:Provider</c>.
/// <c>storecove</c> is the Storecove client. Every other value, including
/// <c>not-configured</c>, is <see cref="HostedPeppolAccessPointClient"/>,
/// which does not open a socket unless provider is <c>hosted</c> and a base URL is set.
/// </summary>
/// <summary>Operator-supplied Storecove identity. Scheme is never inferred.</summary>
internal readonly record struct StorecoveParticipantIdentity(string LegalEntityId, string Scheme, string Value);

public static class PeppolAccessPointClientFactory
{
    public static IPeppolAccessPointClient Select(
        string? provider,
        HostedPeppolAccessPointClient hosted,
        StorecovePeppolAccessPointClient storecove)
    {
        ArgumentNullException.ThrowIfNull(hosted);
        ArgumentNullException.ThrowIfNull(storecove);
        return string.Equals(provider?.Trim(), "storecove", StringComparison.OrdinalIgnoreCase)
            ? storecove
            : hosted;
    }
}
