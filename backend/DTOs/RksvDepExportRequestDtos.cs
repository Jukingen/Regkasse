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

    public Guid CashRegisterId { get; set; }

    public string RegisterNumber { get; set; } = string.Empty;

    public DateTime FromUtc { get; set; }

    public DateTime ToUtc { get; set; }
}
