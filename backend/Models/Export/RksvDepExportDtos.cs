using System.Text.Json.Serialization;

namespace KasseAPI_Final.Models.Export;

/// <summary>BMF RKSV DEP export root (Anlage Z3). Property names must match BMF JSON exactly.</summary>
public sealed class RksvDepExportRootDto
{
    [JsonPropertyName("Belege-Gruppe")]
    public List<RksvDepBelegeGruppeDto> BelegeGruppe { get; set; } = new();
}

/// <summary>One certificate group: all compact JWS receipts signed with the same Signaturzertifikat.</summary>
public sealed class RksvDepBelegeGruppeDto
{
    [JsonPropertyName("Signaturzertifikat")]
    public string Signaturzertifikat { get; set; } = string.Empty;

    [JsonPropertyName("Zertifizierungsstellen")]
    public List<string> Zertifizierungsstellen { get; set; } = new();

    [JsonPropertyName("Belege-kompakt")]
    public List<string> BelegeKompakt { get; set; } = new();
}

/// <summary>Service result: BMF payload plus export metadata for filenames and audit.</summary>
public sealed class RksvDepExportBuildResult
{
    public RksvDepExportRootDto Root { get; set; } = new();

    public Guid CashRegisterId { get; set; }

    public string RegisterNumber { get; set; } = string.Empty;

    public DateTime FromUtc { get; set; }

    public DateTime ToUtc { get; set; }

    public int BelegCount { get; set; }

    public int BelegeGruppeCount { get; set; }

    public bool IsDemo { get; set; }

    public string Environment { get; set; } = string.Empty;

    public bool FormatValidated { get; set; }

    /// <summary>RKSV § 8 disclaimer (not legally binding proof).</summary>
    public string LegalNotice { get; set; } = string.Empty;
}

/// <summary>Structural BMF DEP JSON validation result (does not invoke Prüftool).</summary>
public sealed class RksvDepExportValidationResult
{
    public bool IsValid { get; set; }

    public bool IsDemo { get; set; }

    public string Environment { get; set; } = string.Empty;

    public int BelegeGruppeCount { get; set; }

    public int BelegCount { get; set; }

    public IReadOnlyList<string> Errors { get; set; } = Array.Empty<string>();

    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
}

/// <summary>BMF Prüftool verification result for a DEP export + crypto material pair.</summary>
public sealed class RksvDepPrueftoolResult
{
    public bool Success { get; set; }

    public bool IsDemo { get; set; }

    public bool Skipped { get; set; }

    public string Environment { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? VerificationState { get; set; }

    public string ToolOutput { get; set; } = string.Empty;

    public DateTime ValidatedAtUtc { get; set; }
}
