namespace KasseAPI_Final.Services;

public interface IFiscalExportDownloadTicketStore
{
    /// <summary>Stores ticket; returns opaque id for GET download.</summary>
    Guid CreateTicket(FiscalExportDownloadTicket ticket);

    /// <summary>Retrieves and removes ticket (single use).</summary>
    bool TryConsume(Guid exportId, out FiscalExportDownloadTicket? ticket);
}
