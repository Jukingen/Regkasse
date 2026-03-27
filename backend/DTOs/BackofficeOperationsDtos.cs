namespace KasseAPI_Final.DTOs;

/// <summary>
/// Backoffice Nachdruck-Bestätigung: Begründungspflicht, optional Gerät/Profil/Notiz, optional Idempotenz-Schlüssel für Audit-Korrelation.
/// </summary>
public class ReceiptReprintRequest
{
    /// <summary>Pflicht: einer der <see cref="ReceiptReprintReasonCodes"/> Werte.</summary>
    public string? ReprintReasonCode { get; set; }

    /// <summary>Freitext nur bei OTHER oder ergänzend (max. Länge serverseitig begrenzt).</summary>
    public string? ReasonDetail { get; set; }

    public string? DeviceId { get; set; }
    public string? PrinterProfileId { get; set; }
    public string? Note { get; set; }

    /// <summary>Optional: gleicher Schlüssel → idempotente Audit-Zeile (keine doppelte Ausführung in DB, nur Korrelation).</summary>
    public string? IdempotencyKey { get; set; }
}

/// <summary>
/// Antwort: Erfolg/Fehler getrennt, Audit-Referenz, Beleg + Routing (kein neuer Beleg).
/// </summary>
public class ReceiptReprintResponse
{
    /// <summary>Success oder Failed.</summary>
    public string Outcome { get; set; } = "Success";

    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>Persistierte Audit-Zeile (Payment-Operation), für Incident/Audit-UI.</summary>
    public string? AuditLogId { get; set; }

    /// <summary>Festes Ereignis für Reporting-Filter (z. B. ReceiptReprintConfirmed).</summary>
    public string? ReportableEventType { get; set; }

    public ReceiptDTO? Receipt { get; set; }
    public PrintRoutingContext Routing { get; set; } = new();
}

/// <summary>
/// Placeholder for future device → printer resolution; currently reflection/stub.
/// </summary>
public class PrintRoutingContext
{
    public string? DeviceId { get; set; }
    public string? PrinterProfileId { get; set; }
    public bool Resolved { get; set; }

    /// <summary>True solange keine Hardware-Registry angebunden ist.</summary>
    public bool IsSimulated { get; set; } = true;
}

/// <summary>
/// Available print-routing options list (MVP: static stub).
/// </summary>
public class PrintRoutingOptionsResponse
{
    public List<PrintRoutingDeviceOption> Devices { get; set; } = new();
}

public class PrintRoutingDeviceOption
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Kind { get; set; } = "default";

    /// <summary>Stub-Gerät ohne echtes Druck-Routing.</summary>
    public bool IsSimulated { get; set; } = true;
}

/// <summary>
/// Internes Ergebnis von <see cref="IPaymentService.ConfirmReceiptReprintAsync"/> (Controller mappt auf HTTP + <see cref="ReceiptReprintResponse"/>).
/// </summary>
public sealed class ReceiptReprintOperationResult
{
    public bool Success { get; init; }
    public bool NotFound { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? AuditLogId { get; init; }
    public ReceiptDTO? Receipt { get; init; }
    public PrintRoutingContext Routing { get; init; } = new();
}
