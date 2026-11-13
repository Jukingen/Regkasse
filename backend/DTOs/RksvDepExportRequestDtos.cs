namespace KasseAPI_Final.DTOs;

/// <summary>Query/body parameters for RKSV §7 DEP export (BMF JSON).</summary>
public sealed class RksvDepExportRequestDto
{
    public Guid CashRegisterId { get; set; }

    public DateTime FromUtc { get; set; }

    public DateTime ToUtc { get; set; }

    /// <summary>json (default, inline envelope) | jsonDownload (file attachment).</summary>
    public string Format { get; set; } = "json";

    public bool IncludeSpecialReceipts { get; set; } = true;

    public bool IncludeDailyClosings { get; set; } = true;

    /// <summary>Disclaimer language for embedded notice: de (default) | en.</summary>
    public string Lang { get; set; } = "de";
}

/// <summary>Inline JSON response: legal notice + BMF DEP root.</summary>
public sealed class RksvDepExportEnvelopeDto
{
    public string LegalNotice { get; set; } = string.Empty;

    public Models.Export.RksvDepExportRootDto Dep { get; set; } = new();

    public int BelegCount { get; set; }

    public int BelegeGruppeCount { get; set; }

    public Guid CashRegisterId { get; set; }

    public string RegisterNumber { get; set; } = string.Empty;

    public DateTime FromUtc { get; set; }

    public DateTime ToUtc { get; set; }

    public bool IsDemo { get; set; }

    /// <summary>True when RKSV demo mode or TSE simulation is active.</summary>
    public bool IsSimulated { get; set; }

    /// <summary>Operator note when <see cref="IsSimulated"/>; null in production.</summary>
    public string? SimulationNote { get; set; }

    public string Environment { get; set; } = string.Empty;

    public bool FormatValidated { get; set; }

    public Models.Export.RksvDepExportValidationResult? FormatValidation { get; set; }

    /// <summary>Pre-F5 compact JWS count (JSON Belegdaten payload instead of §9 machine code).</summary>
    public int LegacyJwsCount { get; set; }

    /// <summary>F5-compliant compact JWS count (payload starts with <c>_R1-</c>).</summary>
    public int F5CompliantJwsCount { get; set; }

    /// <summary>Present when <see cref="LegacyJwsCount"/> &gt; 0.</summary>
    public string? LegacyJwsWarning { get; set; }

    /// <summary>True when no legacy JWS entries are present in the export.</summary>
    public bool PrueftoolCompatible { get; set; } = true;

    /// <summary>History / export id for <c>GET …/download/{id}</c> (same as <see cref="HistoryId"/>).</summary>
    public Guid? ExportId { get; set; }

    /// <summary>History row id for subsequent download via <c>/download/{historyId}</c>.</summary>
    public Guid? HistoryId { get; set; }

    /// <summary>Canonical download file name stored with the history row.</summary>
    public string? FileName { get; set; }

    /// <summary>Relative download URL for the stored JSON (auth required).</summary>
    public string? DownloadUrl { get; set; }

    /// <summary>When the hot download copy expires (UTC); archive may remain longer.</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Stored file size in bytes when persisted.</summary>
    public long? FileSizeBytes { get; set; }
}
