using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Reports;

public interface INullbelegReportService
{
    Task<string> GeneratePlainTextReportAsync(
        ReceiptDTO receipt,
        CancellationToken cancellationToken = default);
}

public sealed class NullbelegReportService : INullbelegReportService
{
    private readonly IRksvReportTextService _reportText;

    public NullbelegReportService(IRksvReportTextService reportText) =>
        _reportText = reportText;

    public Task<string> GeneratePlainTextReportAsync(
        ReceiptDTO receipt,
        CancellationToken cancellationToken = default) =>
        RksvSpecialReceiptReportSupport.RenderAsync(
            _reportText,
            receipt,
            RksvSpecialReceiptKinds.Nullbeleg,
            cancellationToken);
}
