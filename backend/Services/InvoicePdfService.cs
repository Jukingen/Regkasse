using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Email;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KasseAPI_Final.Services;

public sealed class InvoicePdfService : IInvoicePdfService
{
    private readonly AppDbContext _context;
    private readonly IInvoiceService _invoiceService;
    private readonly IInvoiceEmailService _invoiceEmailService;
    private readonly IAuditLogService _auditLogService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<InvoicePdfService> _logger;

    static InvoicePdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public InvoicePdfService(
        AppDbContext context,
        IInvoiceService invoiceService,
        IInvoiceEmailService invoiceEmailService,
        IAuditLogService auditLogService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<InvoicePdfService> logger)
    {
        _context = context;
        _invoiceService = invoiceService;
        _invoiceEmailService = invoiceEmailService;
        _auditLogService = auditLogService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<byte[]> GenerateInvoicePdfAsync(
        Guid invoiceId,
        bool copy = false,
        CancellationToken cancellationToken = default)
    {
        var invoice = await ResolveInvoiceAsync(invoiceId, cancellationToken).ConfigureAwait(false);
        return BuildPdf(invoice, copy);
    }

    public async Task<Stream> GetInvoicePdfStreamAsync(
        Guid invoiceId,
        bool copy = false,
        CancellationToken cancellationToken = default)
    {
        var pdf = await GenerateInvoicePdfAsync(invoiceId, copy, cancellationToken).ConfigureAwait(false);
        var stream = new MemoryStream(pdf);
        stream.Position = 0;
        return stream;
    }

    public async Task<bool> ResendInvoiceEmailAsync(
        Guid invoiceId,
        string? recipientEmail,
        CancellationToken cancellationToken = default)
    {
        var invoice = await ResolveInvoiceAsync(invoiceId, cancellationToken).ConfigureAwait(false);
        var actorUserId = _httpContextAccessor.HttpContext?.User.GetActorUserId() ?? "system";
        var actorRole = _httpContextAccessor.HttpContext?.User.GetActorRole() ?? "Unknown";

        var recipient = recipientEmail?.Trim();
        if (string.IsNullOrEmpty(recipient))
            recipient = invoice.CustomerEmail?.Trim();

        if (string.IsNullOrEmpty(recipient))
        {
            _logger.LogInformation("Invoice resend skipped: no recipient for invoice {InvoiceId}.", invoiceId);
            return false;
        }

        if (!_invoiceEmailService.IsConfigured)
        {
            _logger.LogWarning("Invoice resend skipped: SMTP not configured for invoice {InvoiceId}.", invoiceId);
            return false;
        }

        var pdf = BuildPdf(invoice, copy: false);
        var emailSent = await _invoiceEmailService.TrySendInvoiceAsync(invoice, pdf, recipient, cancellationToken)
            .ConfigureAwait(false);

        if (!emailSent)
            return false;

        try
        {
            await _auditLogService.LogSystemOperationAsync(
                AuditLogActions.INVOICE_RESENT,
                AuditLogEntityTypes.INVOICE,
                actorUserId,
                actorRole,
                description: $"Invoice {invoice.InvoiceNumber} resent to {recipient}.",
                requestData: new { InvoiceId = invoiceId, Recipient = recipient },
                entityId: invoiceId,
                actionType: AuditEventType.InvoiceResent).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invoice {InvoiceId} was emailed but audit log write failed.", invoiceId);
        }

        return true;
    }

    private async Task<Invoice> ResolveInvoiceAsync(Guid id, CancellationToken cancellationToken)
    {
        var invoice = await _context.Invoices
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id && i.IsActive, cancellationToken)
            .ConfigureAwait(false);

        if (invoice != null)
            return invoice;

        var posPayment = await _context.PaymentDetails
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive, cancellationToken)
            .ConfigureAwait(false);

        if (posPayment == null)
            throw new KeyNotFoundException($"Invoice {id} not found.");

        return await _invoiceService.ResolveInvoiceFromPaymentAsync(posPayment, cancellationToken).ConfigureAwait(false);
    }

    private static byte[] BuildPdf(Invoice invoice, bool copy)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(invoice.CompanyName).SemiBold().FontSize(16);
                        col.Item().Text(invoice.CompanyAddress);
                        col.Item().Text($"VAT/UID: {invoice.CompanyTaxNumber}");
                    });

                    row.ConstantItem(100).AlignRight().Text(text =>
                    {
                        text.Span("RECHNUNG").FontSize(20).SemiBold();
                        if (copy)
                        {
                            text.EmptyLine();
                            text.Span("KOPIE / COPY").FontSize(14).FontColor(Colors.Red.Medium);
                        }
                    });
                });

                page.Content().PaddingVertical(1, Unit.Centimetre).Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Rechnungsempfänger:").SemiBold();
                            c.Item().Text(invoice.CustomerName ?? "Barzahlung");
                            if (!string.IsNullOrEmpty(invoice.CustomerAddress))
                                c.Item().Text(invoice.CustomerAddress);
                            if (!string.IsNullOrEmpty(invoice.CustomerTaxNumber))
                                c.Item().Text(invoice.CustomerTaxNumber);
                        });

                        row.RelativeItem().AlignRight().Column(c =>
                        {
                            c.Item().Text($"Rechnung Nr.: {invoice.InvoiceNumber}");
                            c.Item().Text($"Datum: {invoice.InvoiceDate:dd.MM.yyyy}");
                            c.Item().Text($"Status: {invoice.Status}");
                            c.Item().Text($"Kassen-ID: {invoice.KassenId}");
                        });
                    });

                    col.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(CellStyle).Text("Artikel");
                            header.Cell().Element(CellStyle).AlignRight().Text("Menge");
                            header.Cell().Element(CellStyle).AlignRight().Text("Preis");
                            header.Cell().Element(CellStyle).AlignRight().Text("MwSt.");
                            header.Cell().Element(CellStyle).AlignRight().Text("Gesamt");

                            static IContainer CellStyle(IContainer container) =>
                                container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5)
                                    .DefaultTextStyle(x => x.SemiBold());
                        });

                        var pdfLineItems = ParseLineItems(invoice.InvoiceItems);
                        foreach (var pi in pdfLineItems)
                        {
                            var displayName = string.IsNullOrWhiteSpace(pi.ProductName) ? "Artikel" : pi.ProductName;
                            table.Cell().Element(BodyCellStyle).Text(displayName);
                            table.Cell().Element(BodyCellStyle).AlignRight().Text(pi.Quantity.ToString());
                            table.Cell().Element(BodyCellStyle).AlignRight().Text($"{pi.UnitPrice:F2}");
                            table.Cell().Element(BodyCellStyle).AlignRight().Text($"{pi.TaxRate:P0}");
                            table.Cell().Element(BodyCellStyle).AlignRight().Text($"{pi.TotalPrice:F2}");
                        }

                        static IContainer BodyCellStyle(IContainer container) => container.PaddingVertical(5);
                    });

                    col.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    col.Item().AlignRight().Column(c =>
                    {
                        c.Item().Text($"Netto: {invoice.Subtotal:N2} EUR");
                        c.Item().Text($"MwSt.: {invoice.TaxAmount:N2} EUR");
                        c.Item().Text($"Gesamt: {invoice.TotalAmount:N2} EUR").Bold().FontSize(12);
                    });

                    col.Item().PaddingVertical(10);

                    if (!string.IsNullOrEmpty(invoice.TseSignature))
                    {
                        col.Item().Background(Colors.Grey.Lighten4).Padding(10).Column(c =>
                        {
                            c.Item().Text("RKSV Signatur (TSE)").FontSize(8).SemiBold();
                            c.Item().Text(invoice.TseSignature).FontFamily("Consolas").FontSize(8);
                            c.Item().Text($"Zeitstempel: {invoice.TseTimestamp:O}").FontSize(8);
                        });
                    }
                });

                page.Footer().AlignCenter().Column(c =>
                {
                    c.Item().Text("Generiert mit Regkasse – RKSV konformes Kassensystem").FontSize(8);
                    c.Item().DefaultTextStyle(x => x.FontSize(8)).Text(x =>
                    {
                        x.Span("Seite ");
                        x.CurrentPageNumber();
                    });
                });
            });
        });

        return document.GeneratePdf();
    }

    private static IReadOnlyList<PaymentItem> ParseLineItems(JsonDocument? invoiceItems)
    {
        if (invoiceItems?.RootElement.ValueKind != JsonValueKind.Array)
            return Array.Empty<PaymentItem>();

        try
        {
            return JsonSerializer.Deserialize<List<PaymentItem>>(invoiceItems.RootElement.GetRawText())
                   ?? (IReadOnlyList<PaymentItem>)Array.Empty<PaymentItem>();
        }
        catch (JsonException)
        {
            return Array.Empty<PaymentItem>();
        }
    }
}
