using KasseAPI_Final.Models.RestoreVerification;

namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>Geri yüklenen DB üzerinde in-process duman çalıştırmasının çıktısı.</summary>
public sealed class RestoredDatabaseApplicationSmokeOutcome
{
    public RestoreDrillApplicationSmokeResultKind Kind { get; init; }

    public string? Detail { get; init; }

    public long DurationMs { get; init; }

    public DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset CompletedAtUtc { get; init; }

    public IReadOnlyList<RestoredDatabaseApplicationSmokeCheckRow> Checks { get; init; } =
        Array.Empty<RestoredDatabaseApplicationSmokeCheckRow>();
}

public sealed class RestoredDatabaseApplicationSmokeCheckRow
{
    public string Id { get; init; } = "";

    public bool Passed { get; init; }

    public string? Detail { get; init; }
}
