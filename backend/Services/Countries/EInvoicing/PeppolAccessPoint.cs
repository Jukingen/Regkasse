using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Countries.EInvoicing;

public enum PeppolSubmissionStatus
{
    Queued = 0,
    Validated = 1,
    Sent = 2,
    Ack = 3,
    Failed = 4,
}

public sealed record PeppolSubmission(
    string Id,
    Guid TenantId,
    string InvoiceNumber,
    PeppolSubmissionStatus Status,
    string? ParticipantId,
    string? Detail);

public interface IPeppolSubmissionStore
{
    void Save(PeppolSubmission submission);
    PeppolSubmission? Get(string id);
}

public sealed class InMemoryPeppolSubmissionStore : IPeppolSubmissionStore
{
    private readonly ConcurrentDictionary<string, PeppolSubmission> _rows = new();

    public void Save(PeppolSubmission submission) => _rows[submission.Id] = submission;

    public PeppolSubmission? Get(string id) =>
        _rows.TryGetValue(id, out var row) ? row : null;
}

public sealed record PeppolSubmitRequest(string SubmissionId, string UblXml, string ParticipantId);

public sealed record PeppolTransportResult(PeppolSubmissionStatus Status, string? Detail);

public interface IPeppolAccessPointClient
{
    Task<PeppolTransportResult> SubmitAsync(PeppolSubmitRequest request, CancellationToken cancellationToken = default);

    Task<PeppolTransportResult> GetStatusAsync(string submissionId, CancellationToken cancellationToken = default);
}

public sealed class PeppolAccessPointNotConfiguredException : InvalidOperationException
{
    public PeppolAccessPointNotConfiguredException(string message) : base(message) { }
}

/// <summary>In-process Access Point. Submit is Sent; the next status read is Ack.</summary>
public sealed class MockPeppolAccessPointClient : IPeppolAccessPointClient
{
    private readonly ConcurrentDictionary<string, int> _polls = new();

    public Task<PeppolTransportResult> SubmitAsync(
        PeppolSubmitRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(request.UblXml);
        _polls[request.SubmissionId] = 0;
        return Task.FromResult(new PeppolTransportResult(PeppolSubmissionStatus.Sent, "mock-accepted"));
    }

    public Task<PeppolTransportResult> GetStatusAsync(
        string submissionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var n = _polls.AddOrUpdate(submissionId, 1, (_, current) => current + 1);
        var status = n >= 1 ? PeppolSubmissionStatus.Ack : PeppolSubmissionStatus.Sent;
        return Task.FromResult(new PeppolTransportResult(status, "mock"));
    }
}

/// <summary>Hosted Access Point HTTP client. Empty base URL never opens a socket.</summary>
public sealed class HostedPeppolAccessPointClient : IPeppolAccessPointClient
{
    private readonly PeppolOptions _options;
    private readonly HttpClient _http;

    public HostedPeppolAccessPointClient(IOptions<PeppolOptions> options, HttpClient http)
    {
        _options = options.Value;
        _http = http;
    }

    public Task<PeppolTransportResult> SubmitAsync(
        PeppolSubmitRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, "submissions", request.UblXml, cancellationToken);

    public Task<PeppolTransportResult> GetStatusAsync(
        string submissionId,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Get, "submissions/" + Uri.EscapeDataString(submissionId), null, cancellationToken);

    private async Task<PeppolTransportResult> SendAsync(
        HttpMethod method,
        string path,
        string? body,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(_options.AccessPointMode, "hosted", StringComparison.OrdinalIgnoreCase))
            throw new PeppolAccessPointNotConfiguredException("Peppol AccessPointMode=own is not implemented.");
        if (!string.Equals(_options.Provider, "hosted", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            throw new PeppolAccessPointNotConfiguredException(
                "Peppol hosted Access Point is not configured. XML is not sent.");
        }

        var url = _options.BaseUrl.TrimEnd('/') + "/" + path;
        using var message = new HttpRequestMessage(method, url);
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        if (body is not null)
            message.Content = new StringContent(body, Encoding.UTF8, "application/xml");

        using var response = await _http.SendAsync(message, cancellationToken).ConfigureAwait(false);
        var status = response.IsSuccessStatusCode
            ? (method == HttpMethod.Get ? PeppolSubmissionStatus.Ack : PeppolSubmissionStatus.Sent)
            : PeppolSubmissionStatus.Failed;
        return new PeppolTransportResult(status, ((int)response.StatusCode).ToString());
    }
}
