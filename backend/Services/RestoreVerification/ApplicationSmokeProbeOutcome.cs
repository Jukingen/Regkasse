namespace KasseAPI_Final.Services.RestoreVerification;

public sealed class ApplicationSmokeProbeOutcome
{
    public bool Success { get; init; }

    public int? HttpStatusCode { get; init; }

    public long DurationMs { get; init; }

    public DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset CompletedAtUtc { get; init; }

    /// <summary>Host/parola yok; yol veya kısa hata.</summary>
    public string? RequestPath { get; init; }

    public string? Error { get; init; }
}
