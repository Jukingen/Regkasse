using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Registrierkasse_API.Models;
using Registrierkasse_API.Services;
using System.Security.Claims;

namespace Registrierkasse_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class InvoiceController : ControllerBase
    {
        private readonly IInvoiceService _invoiceService;
        private readonly IPdfService _pdfService;
        private readonly IEmailService _emailService;
        private readonly ILogger<InvoiceController> _logger;
        private readonly ITseService _tseService; // Added ITseService
        private readonly IAuditService _auditService; // AuditService eklendi

        public InvoiceController(
            IInvoiceService invoiceService, 
            IPdfService pdfService,
            IEmailService emailService,
            ILogger<InvoiceController> logger,
            ITseService tseService,
            IAuditService auditService) // AuditService constructor'a eklendi
        {
            _invoiceService = invoiceService;
            _pdfService = pdfService;
            _emailService = emailService;
            _logger = logger;
            _tseService = tseService; // Initialize ITseService
            _auditService = auditService;
        }

        /// <summary>
        /// Fatura oluştur
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Cashier,Manager,Admin")]
        public async Task<IActionResult> CreateInvoice([FromBody] InvoiceCreateRequest request)
        {
            try
            {
                request.CreatedById = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
                
                var response = await _invoiceService.CreateInvoiceAsync(request);

                // Türkçe Açıklama: Fatura oluşturma işlemi sonrası transaction logu eklenir
                await _auditService.LogActionAsync(
                    action: "CREATE",
                    entityType: "Invoice",
                    entityId: response?.Invoice?.Id.ToString(),
                    oldValues: null,
                    newValues: response?.Invoice,
                    description: $"Invoice created. Number: {response?.Invoice?.InvoiceNumber}, Amount: {response?.Invoice?.TotalAmount}"
                );
                
                return Ok(response);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("TSE"))
            {
                _logger.LogWarning("TSE hatası: {Message}", ex.Message);
                return BadRequest(new { 
                    error = "TSE_ERROR", 
                    message = ex.Message,
                    details = "TSE cihazı bağlı değil veya geçersiz. Lütfen TSE cihazını kontrol edin."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatura oluşturma hatası");
                return StatusCode(500, new { error = "Fatura oluşturulamadı", details = ex.Message });
            }
        }

        /// <summary>
        /// Fatura detaylarını getir
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<Invoice>> GetInvoice(string id)
        {
            try
            {
                var invoice = await _invoiceService.GetInvoiceByIdAsync(id);
                return Ok(invoice);
            }
            catch (ArgumentException)
            {
                return NotFound(new { error = "Invoice not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get invoice {Id}", id);
                return BadRequest(new { error = "Failed to get invoice", details = ex.Message });
            }
        }

        /// <summary>
        /// Fatura numarasına göre getir
        /// </summary>
        [HttpGet("number/{invoiceNumber}")]
        public async Task<ActionResult<Invoice>> GetInvoiceByNumber(string invoiceNumber)
        {
            try
            {
                var invoice = await _invoiceService.GetInvoiceByNumberAsync(invoiceNumber);
                return Ok(invoice);
            }
            catch (ArgumentException)
            {
                return NotFound(new { error = "Invoice not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get invoice by number {Number}", invoiceNumber);
                return BadRequest(new { error = "Failed to get invoice", details = ex.Message });
            }
        }

        /// <summary>
        /// Faturaları listele (filtreli)
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<Invoice>>> GetInvoices([FromQuery] InvoiceFilterRequest filter)
        {
            try
            {
                var invoices = await _invoiceService.GetInvoicesAsync(filter);
                return Ok(invoices);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get invoices");
                return BadRequest(new { error = "Failed to get invoices", details = ex.Message });
            }
        }

        /// <summary>
        /// Fatura güncelle
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Cashier,Manager,Admin")]
        public async Task<ActionResult<Invoice>> UpdateInvoice(string id, [FromBody] InvoiceUpdateRequest request)
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                request.UpdatedById = userId;

                // Eski değerleri al
                var oldInvoice = await _invoiceService.GetInvoiceByIdAsync(id);
                var invoice = await _invoiceService.UpdateInvoiceAsync(id, request);

                // Türkçe Açıklama: Fatura güncelleme işlemi sonrası transaction logu eklenir
                await _auditService.LogActionAsync(
                    action: "UPDATE",
                    entityType: "Invoice",
                    entityId: id,
                    oldValues: oldInvoice,
                    newValues: invoice,
                    description: $"Invoice updated. Number: {invoice?.InvoiceNumber}, Amount: {invoice?.TotalAmount}"
                );
                return Ok(invoice);
            }
            catch (ArgumentException)
            {
                return NotFound(new { error = "Invoice not found" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update invoice {Id}", id);
                return BadRequest(new { error = "Failed to update invoice", details = ex.Message });
            }
        }

        /// <summary>
        /// Fatura sil
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<ActionResult> DeleteInvoice(string id)
        {
            try
            {
                // Eski değerleri al
                var oldInvoice = await _invoiceService.GetInvoiceByIdAsync(id);
                await _invoiceService.DeleteInvoiceAsync(id);

                // Türkçe Açıklama: Fatura silme işlemi sonrası transaction logu eklenir
                await _auditService.LogActionAsync(
                    action: "DELETE",
                    entityType: "Invoice",
                    entityId: id,
                    oldValues: oldInvoice,
                    newValues: null,
                    description: $"Invoice deleted. Number: {oldInvoice?.InvoiceNumber}, Amount: {oldInvoice?.TotalAmount}"
                );
                return NoContent();
            }
            catch (ArgumentException)
            {
                return NotFound(new { error = "Invoice not found" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete invoice {Id}", id);
                return BadRequest(new { error = "Failed to delete invoice", details = ex.Message });
            }
        }

        /// <summary>
        /// Fatura gönder
        /// </summary>
        [HttpPost("{id}/send")]
        [Authorize(Roles = "Cashier,Manager,Admin")]
        public async Task<ActionResult<Invoice>> SendInvoice(string id, [FromBody] InvoiceSendRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                request.SentById = userId;

                var invoice = await _invoiceService.SendInvoiceAsync(id, request);
                return Ok(invoice);
            }
            catch (ArgumentException)
            {
                return NotFound(new { error = "Invoice not found" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send invoice {Id}", id);
                return BadRequest(new { error = "Failed to send invoice", details = ex.Message });
            }
        }

        /// <summary>
        /// Ödeme kaydet
        /// </summary>
        [HttpPost("{id}/payment")]
        [Authorize(Roles = "Cashier,Manager,Admin")]
        public async Task<ActionResult<Invoice>> RecordPayment(string id, [FromBody] InvoicePaymentRequest request)
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                request.ProcessedById = userId;

                // Eski değerleri al
                var oldInvoice = await _invoiceService.GetInvoiceByIdAsync(id);
                var invoice = await _invoiceService.MarkAsPaidAsync(id, request);

                // Türkçe Açıklama: Ödeme işlemi sonrası transaction logu eklenir
                await _auditService.LogActionAsync(
                    action: "PAYMENT",
                    entityType: "Invoice",
                    entityId: id,
                    oldValues: oldInvoice,
                    newValues: invoice,
                    description: $"Invoice payment recorded. Number: {invoice?.InvoiceNumber}, Amount: {invoice?.TotalAmount}"
                );
                return Ok(invoice);
            }
            catch (ArgumentException)
            {
                return NotFound(new { error = "Invoice not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record payment for invoice {Id}", id);
                return BadRequest(new { error = "Failed to record payment", details = ex.Message });
            }
        }

        /// <summary>
        /// Fatura iptal et
        /// </summary>
        [HttpPost("{id}/cancel")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<ActionResult<Invoice>> CancelInvoice(string id, [FromBody] CancelInvoiceRequest request)
        {
            try
            {
                // Eski değerleri al
                var oldInvoice = await _invoiceService.GetInvoiceByIdAsync(id);
                var invoice = await _invoiceService.CancelInvoiceAsync(id, request.Reason);

                // Türkçe Açıklama: Fatura iptal işlemi sonrası transaction logu eklenir
                await _auditService.LogActionAsync(
                    action: "CANCEL",
                    entityType: "Invoice",
                    entityId: id,
                    oldValues: oldInvoice,
                    newValues: invoice,
                    description: $"Invoice cancelled. Number: {invoice?.InvoiceNumber}, Amount: {invoice?.TotalAmount}"
                );
                return Ok(invoice);
            }
            catch (ArgumentException)
            {
                return NotFound(new { error = "Invoice not found" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel invoice {Id}", id);
                return BadRequest(new { error = "Failed to cancel invoice", details = ex.Message });
            }
        }

        /// <summary>
        /// Fatura PDF'i indir
        /// </summary>
        [HttpGet("{id}/pdf")]
        public async Task<IActionResult> DownloadInvoicePdf(string id)
        {
            try
            {
                var pdfBytes = await _pdfService.GenerateInvoicePdfAsync(id);
                var invoice = await _invoiceService.GetInvoiceByIdAsync(id);
                
                return File(pdfBytes, "application/pdf", $"invoice-{invoice.InvoiceNumber}.pdf");
            }
            catch (ArgumentException)
            {
                return NotFound(new { error = "Invoice not found" });
            }
            catch (NotImplementedException)
            {
                return BadRequest(new { error = "PDF generation not implemented yet" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate PDF for invoice {Id}", id);
                return BadRequest(new { error = "Failed to generate PDF", details = ex.Message });
            }
        }

        /// <summary>
        /// Fatura CSV çıktısı indir
        /// </summary>
        [HttpGet("{id}/csv")]
        public async Task<IActionResult> DownloadInvoiceCsv(string id)
        {
            try
            {
                var invoice = await _invoiceService.GetInvoiceByIdAsync(id);
                if (invoice == null)
                    return NotFound(new { error = "Invoice not found" });

                var csv = GenerateInvoiceCsv(invoice);
                return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"invoice-{invoice.InvoiceNumber}.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate CSV for invoice {Id}", id);
                return BadRequest(new { error = "Failed to generate CSV", details = ex.Message });
            }
        }

        // Türkçe açıklama: Fatura için CSV çıktısı üreten yardımcı fonksiyon
        private string GenerateInvoiceCsv(Invoice invoice)
        {
            var items = invoice.InvoiceItems != null 
                ? System.Text.Json.JsonSerializer.Deserialize<List<InvoiceItem>>(invoice.InvoiceItems.ToString()) 
                : new List<InvoiceItem>();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Product,Description,Quantity,UnitPrice,Tax,Total");
            foreach (var item in items)
            {
                sb.AppendLine($"{item.ProductName},{item.Description},{item.Quantity},{item.UnitPrice},{item.TaxAmount},{item.TotalAmount}");
            }
            sb.AppendLine($",,,Subtotal,,{invoice.Subtotal}");
            sb.AppendLine($",,,Tax,,{invoice.TaxAmount}");
            sb.AppendLine($",,,Total,,{invoice.TotalAmount}");
            return sb.ToString();
        }

        /// <summary>
        /// Fatura PDF'i email ile gönder
        /// </summary>
        [HttpPost("{id}/email")]
        [Authorize(Roles = "Cashier,Manager,Admin")]
        public async Task<ActionResult> SendInvoiceEmail(string id, [FromBody] SendPdfRequest request)
        {
            try
            {
                var invoice = await _invoiceService.GetInvoiceByIdAsync(id);
                var pdfBytes = await _pdfService.GenerateInvoicePdfAsync(invoice);

                var success = await _emailService.SendInvoiceEmailAsync(invoice, request.Email, pdfBytes);

                if (success)
                {
                    return Ok(new { message = "Invoice PDF sent successfully" });
                }
                else
                {
                    return BadRequest(new { error = "Failed to send invoice PDF" });
                }
            }
            catch (ArgumentException)
            {
                return NotFound(new { error = "Invoice not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send invoice PDF for {Id}", id);
                return BadRequest(new { error = "Failed to send invoice PDF", details = ex.Message });
            }
        }

        /// <summary>
        /// Ödeme hatırlatması gönder
        /// </summary>
        [HttpPost("{id}/send-reminder")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<ActionResult> SendPaymentReminder(string id, [FromBody] SendReminderRequest request)
        {
            try
            {
                var invoice = await _invoiceService.GetInvoiceByIdAsync(id);
                var success = await _emailService.SendInvoiceReminderAsync(invoice, request.Email);

                if (success)
                {
                    return Ok(new { message = "Payment reminder sent successfully" });
                }
                else
                {
                    return BadRequest(new { error = "Failed to send payment reminder" });
                }
            }
            catch (ArgumentException)
            {
                return NotFound(new { error = "Invoice not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send payment reminder for {Id}", id);
                return BadRequest(new { error = "Failed to send payment reminder", details = ex.Message });
            }
        }

        /// <summary>
        /// Ödeme onayı gönder
        /// </summary>
        [HttpPost("{id}/send-payment-confirmation")]
        [Authorize(Roles = "Cashier,Manager,Admin")]
        public async Task<ActionResult> SendPaymentConfirmation(string id, [FromBody] SendConfirmationRequest request)
        {
            try
            {
                var invoice = await _invoiceService.GetInvoiceByIdAsync(id);
                var success = await _emailService.SendPaymentConfirmationAsync(invoice, request.Email);

                if (success)
                {
                    return Ok(new { message = "Payment confirmation sent successfully" });
                }
                else
                {
                    return BadRequest(new { error = "Failed to send payment confirmation" });
                }
            }
            catch (ArgumentException)
            {
                return NotFound(new { error = "Invoice not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send payment confirmation for {Id}", id);
                return BadRequest(new { error = "Failed to send payment confirmation", details = ex.Message });
            }
        }

        /// <summary>
        /// FinanzOnline'a gönder
        /// </summary>
        [HttpPost("{id}/finanzonline")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<ActionResult> SubmitToFinanzOnline(string id)
        {
            try
            {
                var success = await _invoiceService.SubmitToFinanzOnlineAsync(id);

                if (success)
                {
                    return Ok(new { message = "Invoice submitted to FinanzOnline successfully" });
                }
                else
                {
                    return BadRequest(new { error = "Failed to submit to FinanzOnline" });
                }
            }
            catch (ArgumentException)
            {
                return NotFound(new { error = "Invoice not found" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to submit invoice to FinanzOnline {Id}", id);
                return BadRequest(new { error = "Failed to submit to FinanzOnline", details = ex.Message });
            }
        }

        /// <summary>
        /// Gecikmiş faturaları getir
        /// </summary>
        [HttpGet("overdue")]
        public async Task<ActionResult<List<Invoice>>> GetOverdueInvoices()
        {
            try
            {
                var invoices = await _invoiceService.GetOverdueInvoicesAsync();
                return Ok(invoices);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get overdue invoices");
                return BadRequest(new { error = "Failed to get overdue invoices", details = ex.Message });
            }
        }

        /// <summary>
        /// Fatura istatistikleri
        /// </summary>
        [HttpGet("statistics")]
        public async Task<ActionResult<InvoiceStatistics>> GetInvoiceStatistics(
            [FromQuery] DateTime startDate, 
            [FromQuery] DateTime endDate)
        {
            try
            {
                var statistics = await _invoiceService.GetInvoiceStatisticsAsync(startDate, endDate);
                return Ok(statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get invoice statistics");
                return BadRequest(new { error = "Failed to get statistics", details = ex.Message });
            }
        }

        /// <summary>
        /// Fatura şablonlarını getir
        /// </summary>
        [HttpGet("templates")]
        public async Task<ActionResult<List<InvoiceTemplate>>> GetInvoiceTemplates()
        {
            try
            {
                // Bu kısım InvoiceTemplateService ile implement edilecek
                return Ok(new List<InvoiceTemplate>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get invoice templates");
                return BadRequest(new { error = "Failed to get templates", details = ex.Message });
            }
        }

        /// <summary>
        /// Email bağlantısını test et
        /// </summary>
        [HttpPost("test-email")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> TestEmailConnection()
        {
            try
            {
                var success = await _emailService.TestEmailConnectionAsync();

                if (success)
                {
                    return Ok(new { message = "Email connection test successful" });
                }
                else
                {
                    return BadRequest(new { error = "Email connection test failed" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email connection test failed");
                return BadRequest(new { error = "Email connection test failed", details = ex.Message });
            }
        }

        /// <summary>
        /// Toplu işlemler
        /// </summary>
        [HttpPost("bulk-actions")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<ActionResult> BulkActions([FromBody] BulkInvoiceActionRequest request)
        {
            try
            {
                var results = new List<BulkActionResult>();

                foreach (var invoiceId in request.InvoiceIds)
                {
                    try
                    {
                        switch (request.Action.ToLower())
                        {
                            case "send":
                                await _invoiceService.SendInvoiceAsync(invoiceId.ToString(), new InvoiceSendRequest
                                {
                                    SentById = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "system"
                                });
                                results.Add(new BulkActionResult { InvoiceId = invoiceId, Success = true });
                                break;

                            case "cancel":
                                await _invoiceService.CancelInvoiceAsync(invoiceId.ToString(), "Bulk cancellation");
                                results.Add(new BulkActionResult { InvoiceId = invoiceId, Success = true });
                                break;

                            case "submit-finanzonline":
                                var success = await _invoiceService.SubmitToFinanzOnlineAsync(invoiceId.ToString());
                                results.Add(new BulkActionResult { InvoiceId = invoiceId, Success = success });
                                break;

                            default:
                                results.Add(new BulkActionResult { InvoiceId = invoiceId, Success = false, Error = "Invalid action" });
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        results.Add(new BulkActionResult { InvoiceId = invoiceId, Success = false, Error = ex.Message });
                    }
                }

                return Ok(new { results });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bulk invoice actions failed");
                return BadRequest(new { error = "Bulk actions failed", details = ex.Message });
            }
        }

        /// <summary>
        /// TSE durumunu kontrol et
        /// </summary>
        [HttpGet("tse-status")]
        public async Task<IActionResult> GetTseStatus()
        {
            try
            {
                var tseStatus = await _tseService.GetStatusAsync();
                
                var response = new
                {
                    isConnected = tseStatus.IsConnected,
                    serialNumber = tseStatus.SerialNumber,
                    certificateStatus = tseStatus.CertificateStatus,
                    memoryStatus = tseStatus.MemoryStatus,
                    lastSignatureTime = tseStatus.LastSignatureTime,
                    canCreateInvoices = tseStatus.IsConnected && tseStatus.CertificateStatus == "VALID",
                    errorMessage = !tseStatus.IsConnected ? "TSE cihazı bağlı değil" : 
                                 tseStatus.CertificateStatus != "VALID" ? "TSE sertifikası geçersiz" : null
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TSE durumu alma hatası");
                return StatusCode(500, new { 
                    error = "TSE durumu alınamadı", 
                    details = ex.Message,
                    canCreateInvoices = false,
                    errorMessage = "TSE durumu kontrol edilemiyor"
                });
            }
        }
    }

    public class CancelInvoiceRequest
    {
        public string Reason { get; set; } = string.Empty;
    }

    public class SendPdfRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    public class SendReminderRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    public class SendConfirmationRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    public class BulkInvoiceActionRequest
    {
        public List<Guid> InvoiceIds { get; set; } = new();
        public string Action { get; set; } = string.Empty; // send, cancel, submit-finanzonline
    }

    public class BulkActionResult
    {
        public Guid InvoiceId { get; set; }
        public bool Success { get; set; }
        public string? Error { get; set; }
    }
} 
