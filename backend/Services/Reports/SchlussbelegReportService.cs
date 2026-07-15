using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Reports;

public interface ISchlussbelegReportService
{
    Task<string> GeneratePlainTextReportAsync(
        ReceiptDTO receipt,
        CancellationToken cancellationToken = default);
}

public sealed class SchlussbelegReportService : ISchlussbelegReportService
{
    private readonly IRksvReportTextService _reportText;

    public SchlussbelegReportService(IRksvReportTextService reportText) =>
        _reportText = reportText;

    public Task<string> GeneratePlainTextReportAsync(
        ReceiptDTO receipt,
        CancellationToken cancellationToken = default) =>
        RksvSpecialReceiptReportSupport.RenderAsync(
            _reportText,
            receipt,
            RksvSpecialReceiptKinds.Schlussbeleg,
            cancellationToken);
}
