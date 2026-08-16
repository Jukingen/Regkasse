using System.Net;

namespace KasseAPI_Final.Tse.Fiskaly;

/// <summary>fiskaly SIGN AT HTTP error (never includes API secrets).</summary>
public sealed class FiskalyApiException : InvalidOperationException
{
    public FiskalyApiException(
        string message,
        HttpStatusCode? statusCode = null,
        string? requestId = null,
        string? environment = null)
        : base(message)
    {
        StatusCode = statusCode;
        RequestId = requestId;
        Environment = environment;
    }

    public HttpStatusCode? StatusCode { get; }

    public string? RequestId { get; }

    /// <summary>fiskaly <c>_env</c> when present (TEST / LIVE).</summary>
    public string? Environment { get; }
}
