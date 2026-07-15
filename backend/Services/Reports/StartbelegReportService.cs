using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Reports;

public interface IStartbelegReportService
{
    Task<string> GeneratePlainTextReportAsync(
        ReceiptDTO receipt,
        CancellationToken cancellationToken = default);
}

public sealed class StartbelegReportService : IStartbelegReportService
{
    private readonly IRksvReportTextService _reportText;

    public StartbelegReportService(IRksvReportTextService reportText) =>
        _reportText = reportText;

    public Task<string> GeneratePlainTextReportAsync(
        ReceiptDTO receipt,
        CancellationToken cancellationToken = default) =>
        RksvSpecialReceiptReportSupport.RenderAsync(
            _reportText,
            receipt,
            RksvSpecialReceiptKinds.Startbeleg,
            cancellationToken);
}
