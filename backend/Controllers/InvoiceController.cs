using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Fiscal;
using KasseAPI_Final.Localization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Localization;
using KasseAPI_Final.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class InvoiceController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<InvoiceController> _logger;
        private readonly ICompanyProfileProvider _companyProfileProvider;
        private readonly IInvoiceService _invoiceService;
        private readonly IReceiptSequenceService _receiptSequenceService;
        private readonly ITseService _tseService;
        private readonly IInvoicePdfService _invoicePdfService;
        private readonly IApiMessageLocalizer _messages;

        public InvoiceController(
            AppDbContext context,
            ILogger<InvoiceController> logger,
            ICompanyProfileProvider companyProfileProvider,
            IInvoiceService invoiceService,
            IReceiptSequenceService receiptSequenceService,
            ITseService tseService,
            IInvoicePdfService invoicePdfService,
            IApiMessageLocalizer messages)
        {
            _context = context;
            _logger = logger;
            _companyProfileProvider = companyProfileProvider;
            _invoiceService = invoiceService;
            _receiptSequenceService = receiptSequenceService;
            _tseService = tseService;
            _invoicePdfService = invoicePdfService;
            _messages = messages;
        }

        // GET: api/Invoice/list
        [HasPermission(AppPermissions.InvoiceView)]
        [HttpGet("list")]
        public async Task<ActionResult<PagedResult<InvoiceListItemDto>>> GetInvoicesList(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] InvoiceStatus? status = null,
            [FromQuery] string? query = null,
            [FromQuery] string sortBy = "invoiceDate",
            [FromQuery] string sortDir = "desc",
            [FromQuery] Guid? cashRegisterId = null)
        {
            try
            {
                if (page < 1)
                    page = 1;
                if (pageSize < 1)
                    pageSize = 50;
                if (pageSize > 200)
                    pageSize = 200;

                var queryable = _context.Invoices.AsNoTracking().Where(i => i.IsActive);

                if (from.HasValue)
                {
                    var fromDay = PostgreSqlUtcDateTime.ViennaCalendarMidnightContainingInstant(from.Value);
                    var (fromUtc, _) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(fromDay);
                    queryable = queryable.Where(i => i.InvoiceDate >= fromUtc);
                }
                if (to.HasValue)
                {
                    var toDay = PostgreSqlUtcDateTime.ViennaCalendarMidnightContainingInstant(to.Value);
                    var (_, toExclusiveUtc) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(toDay);
                    queryable = queryable.Where(i => i.InvoiceDate < toExclusiveUtc);
                }
                if (status.HasValue)
                    queryable = queryable.Where(i => i.Status == status.Value);
                if (cashRegisterId.HasValue)
                    queryable = queryable.Where(i => i.CashRegisterId == cashRegisterId.Value);

                if (!string.IsNullOrWhiteSpace(query))
                {
                    query = query.Trim().ToLower();
                    if (query.Length >= 2)
                    {
                        queryable = queryable.Where(i =>
                            EF.Functions.ILike(i.InvoiceNumber, $"%{query}%") ||
                            (i.CustomerName != null && EF.Functions.ILike(i.CustomerName, $"%{query}%")) ||
                            (i.CompanyName != null && EF.Functions.ILike(i.CompanyName, $"%{query}%")));
                    }
                }

                var totalCount = await queryable.CountAsync();

                bool isAsc = sortDir?.ToLower() == "asc";
                queryable = sortBy.ToLower() switch
                {
                    "invoicenumber" => isAsc ? queryable.OrderBy(i => i.InvoiceNumber) : queryable.OrderByDescending(i => i.InvoiceNumber),
                    "totalamount" => isAsc ? queryable.OrderBy(i => i.TotalAmount) : queryable.OrderByDescending(i => i.TotalAmount),
                    "status" => isAsc ? queryable.OrderBy(i => i.Status) : queryable.OrderByDescending(i => i.Status),
                    _ => isAsc ? queryable.OrderBy(i => i.InvoiceDate) : queryable.OrderByDescending(i => i.InvoiceDate)
                };

                var items = await queryable
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(i => new InvoiceListItemDto
                    {
                        Id = i.Id,
                        InvoiceNumber = i.InvoiceNumber,
                        InvoiceDate = i.InvoiceDate,
                        CustomerName = i.CustomerName,
                        CompanyName = i.CompanyName,
                        TotalAmount = i.TotalAmount,
                        Status = i.Status,
                        CashRegisterId = i.CashRegisterId,
                        KassenId = i.KassenId,
                        TseSignature = i.TseSignature,
                        DocumentType = i.DocumentType,
                        OriginalInvoiceId = i.OriginalInvoiceId,
                        ListRowOrigin = "PersistedInvoice"
                    })
                    .ToListAsync();

                return Ok(new PagedResult<InvoiceListItemDto>
                {
                    Items = items,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing invoices");
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/Invoice/pos-list
        [HasPermission(AppPermissions.InvoiceView)]
        [HttpGet("pos-list")]
        public async Task<ActionResult<PagedResult<InvoiceListItemDto>>> GetPosInvoicesList(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] string? query = null,
            [FromQuery] string sortBy = "invoiceDate",
            [FromQuery] string sortDir = "desc",
            [FromQuery] string? cashRegisterId = null)
        {
            try
            {
                if (page < 1)
                    page = 1;
                if (pageSize < 1)
                    pageSize = 50;
                if (pageSize > 200)
                    pageSize = 200;

                var queryable = _context.PaymentDetails.AsNoTracking().Where(p => p.IsActive);

                // Date filtering on CreatedAt (payment timestamp); Austria calendar-day bounds in UTC for Npgsql.
                if (from.HasValue)
                {
                    var fromDay = PostgreSqlUtcDateTime.ViennaCalendarMidnightContainingInstant(from.Value);
                    var (fromUtc, _) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(fromDay);
                    queryable = queryable.Where(p => p.CreatedAt >= fromUtc);
                }
                if (to.HasValue)
                {
                    var toDay = PostgreSqlUtcDateTime.ViennaCalendarMidnightContainingInstant(to.Value);
                    var (_, toExclusiveUtc) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(toDay);
                    queryable = queryable.Where(p => p.CreatedAt < toExclusiveUtc);
                }

                if (!string.IsNullOrWhiteSpace(cashRegisterId))
                {
                    var crFilter = cashRegisterId.Trim();
                    if (Guid.TryParse(crFilter, out var crGuid))
                        queryable = queryable.Where(p => p.CashRegisterId == crGuid);
                    else
                        queryable = queryable.Where(p => _context.CashRegisters.Any(cr => cr.Id == p.CashRegisterId && cr.RegisterNumber == crFilter));
                }

                if (!string.IsNullOrWhiteSpace(query))
                {
                    var q = query.Trim().ToLower();
                    if (q.Length >= 2)
                    {
                        queryable = queryable.Where(p =>
                            EF.Functions.ILike(p.ReceiptNumber, $"%{q}%") ||
                            EF.Functions.ILike(p.CustomerName, $"%{q}%"));
                    }
                }

                var totalCount = await queryable.CountAsync();

                bool isAsc = sortDir?.ToLower() == "asc";
                queryable = sortBy.ToLower() switch
                {
                    "invoicenumber" => isAsc ? queryable.OrderBy(p => p.ReceiptNumber) : queryable.OrderByDescending(p => p.ReceiptNumber),
                    "totalamount" => isAsc ? queryable.OrderBy(p => p.TotalAmount) : queryable.OrderByDescending(p => p.TotalAmount),
                    _ => isAsc ? queryable.OrderBy(p => p.CreatedAt) : queryable.OrderByDescending(p => p.CreatedAt)
                };

                var items = await queryable
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(p => new InvoiceListItemDto
                    {
                        Id = p.Id,
                        InvoiceNumber = p.ReceiptNumber,
                        InvoiceDate = p.CreatedAt,
                        CustomerName = p.CustomerName,
                        CompanyName = string.Empty,
                        TotalAmount = p.TotalAmount,
                        Status = InvoiceStatus.Paid, // POS transactions are always completed/paid
                        CashRegisterId = p.CashRegisterId,
                        KassenId = _context.CashRegisters.Where(cr => cr.Id == p.CashRegisterId).Select(cr => cr.RegisterNumber).FirstOrDefault() ?? "",
                        TseSignature = p.TseSignature,
                        ListRowOrigin = "PaymentDerivedListRow"
                    })
                    .ToListAsync();

                return Ok(new PagedResult<InvoiceListItemDto>
                {
                    Items = items,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing POS invoices");
                return StatusCode(500, "Internal server error");
            }
        }

        [HasPermission(AppPermissions.InvoiceExport)]
        [HttpGet("export")]
        public async Task<IActionResult> ExportInvoices(
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] InvoiceStatus? status = null,
            [FromQuery] string? query = null,
            [FromQuery] string sortBy = "invoiceDate",
            [FromQuery] string sortDir = "desc",
            [FromQuery] Guid? cashRegisterId = null)
        {
            try
            {
                var queryable = _context.Invoices.AsNoTracking().Where(i => i.IsActive);

                if (from.HasValue)
                {
                    var fromDay = PostgreSqlUtcDateTime.ViennaCalendarMidnightContainingInstant(from.Value);
                    var (fromUtc, _) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(fromDay);
                    queryable = queryable.Where(i => i.InvoiceDate >= fromUtc);
                }
                if (to.HasValue)
                {
                    var toDay = PostgreSqlUtcDateTime.ViennaCalendarMidnightContainingInstant(to.Value);
                    var (_, toExclusiveUtc) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(toDay);
                    queryable = queryable.Where(i => i.InvoiceDate < toExclusiveUtc);
                }
                if (status.HasValue)
                    queryable = queryable.Where(i => i.Status == status.Value);
                if (cashRegisterId.HasValue)
                    queryable = queryable.Where(i => i.CashRegisterId == cashRegisterId.Value);

                if (!string.IsNullOrWhiteSpace(query))
                {
                    query = query.Trim().ToLower();
                    if (query.Length >= 2)
                    {
                        queryable = queryable.Where(i =>
                            EF.Functions.ILike(i.InvoiceNumber, $"%{query}%") ||
                            (i.CustomerName != null && EF.Functions.ILike(i.CustomerName, $"%{query}%")) ||
                            (i.CompanyName != null && EF.Functions.ILike(i.CompanyName, $"%{query}%")));
                    }
                }

                bool isAsc = sortDir?.ToLower() == "asc";
                queryable = sortBy.ToLower() switch
                {
                    "invoicenumber" => isAsc ? queryable.OrderBy(i => i.InvoiceNumber) : queryable.OrderByDescending(i => i.InvoiceNumber),
                    "totalamount" => isAsc ? queryable.OrderBy(i => i.TotalAmount) : queryable.OrderByDescending(i => i.TotalAmount),
                    "status" => isAsc ? queryable.OrderBy(i => i.Status) : queryable.OrderByDescending(i => i.Status),
                    _ => isAsc ? queryable.OrderBy(i => i.InvoiceDate) : queryable.OrderByDescending(i => i.InvoiceDate)
                };

                var stream = new MemoryStream();
                var writer = new StreamWriter(stream, System.Text.Encoding.UTF8);

                await writer.WriteLineAsync("InvoiceNumber;InvoiceDate;CustomerName;CompanyName;TotalAmount;Status;DocumentType;OriginalInvoiceId;KassenId;TseSignature");

                foreach (var i in queryable)
                {
                    var line = $"{i.InvoiceNumber};{i.InvoiceDate:yyyy-MM-dd HH:mm};{EscapeCsv(i.CustomerName)};{EscapeCsv(i.CompanyName)};{i.TotalAmount:F2};{i.Status};{i.DocumentType};{i.OriginalInvoiceId};{i.KassenId};{i.TseSignature}";
                    await writer.WriteLineAsync(line);
                }

                await writer.FlushAsync();
                stream.Position = 0;

                return File(stream, "text/csv", $"invoices_{DateTime.Now:yyyyMMdd_HHmm}.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting invoices");
                return StatusCode(500, "Internal server error");
            }
        }

        private string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            if (value.Contains(";") || value.Contains("\"") || value.Contains("\n"))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }
            return value;
        }

        [HasPermission(AppPermissions.InvoiceView)]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Invoice>>> GetInvoices(
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] InvoiceStatus? status = null,
            [FromQuery] string? query = null)
        {
            try
            {
                var queryable = _context.Invoices.AsQueryable();

                // 1. Soft delete filter
                queryable = queryable.Where(i => i.IsActive);

                // 2. Date range filter (Austria calendar-day bounds, UTC for Npgsql)
                if (from.HasValue)
                {
                    var fromDay = PostgreSqlUtcDateTime.ViennaCalendarMidnightContainingInstant(from.Value);
                    var (fromUtc, _) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(fromDay);
                    queryable = queryable.Where(i => i.InvoiceDate >= fromUtc);
                }
                if (to.HasValue)
                {
                    var toDay = PostgreSqlUtcDateTime.ViennaCalendarMidnightContainingInstant(to.Value);
                    var (_, toExclusiveUtc) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(toDay);
                    queryable = queryable.Where(i => i.InvoiceDate < toExclusiveUtc);
                }

                // 3. Status filter
                if (status.HasValue)
                    queryable = queryable.Where(i => i.Status == status.Value);

                // 4. Text search
                if (!string.IsNullOrWhiteSpace(query))
                {
                    query = query.ToLower();
                    queryable = queryable.Where(i =>
                        i.InvoiceNumber.ToLower().Contains(query) ||
                        (i.CustomerName != null && i.CustomerName.ToLower().Contains(query)) ||
                        (i.CompanyName != null && i.CompanyName.ToLower().Contains(query)));
                }

                // 6. Sorting & Pagination
                var invoices = await queryable
                    .OrderByDescending(i => i.InvoiceDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                _logger.LogInformation("Retrieved {Count} invoices (Page {Page})", invoices.Count, page);
                return Ok(invoices);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving invoices");
                return StatusCode(500, "Internal server error");
            }
        }

        [HasPermission(AppPermissions.InvoiceView)]
        [HttpGet("{id}")]
        public async Task<ActionResult<Invoice>> GetInvoice(Guid id)
        {
            try
            {
                var invoice = await _context.Invoices.FindAsync(id);

                if (invoice == null || !invoice.IsActive)
                {
                    var posPayment = await _context.PaymentDetails.FindAsync(id);
                    if (posPayment == null || !posPayment.IsActive)
                        return NotFound(_messages.Get(ApiMessageKeys.InvoiceNotFound));

                    invoice = await _invoiceService.ResolveInvoiceFromPaymentAsync(
                        posPayment, HttpContext.RequestAborted);
                }

                return Ok(invoice);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving invoice with ID: {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HasPermission(AppPermissions.InvoiceManage)]
        [HttpPost]
        public async Task<ActionResult<Invoice>> CreateInvoice(CreateInvoiceRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                if (string.IsNullOrWhiteSpace(request.CompanyName))
                    return BadRequest(_messages.Get(ApiMessageKeys.CompanyNameRequired));
                if (string.IsNullOrWhiteSpace(request.CompanyTaxNumber))
                    return BadRequest(_messages.Get(ApiMessageKeys.CompanyTaxNumberRequired));
                if (!request.CompanyTaxNumber.StartsWith("ATU") || request.CompanyTaxNumber.Length != 11)
                    return BadRequest(_messages.Get(ApiMessageKeys.CompanyTaxNumberInvalidFormat));
                if (request.CashRegisterId == Guid.Empty)
                    return BadRequest("CashRegisterId is required.");
                var cashRegCreate = await _context.CashRegisters.AsNoTracking().FirstOrDefaultAsync(r => r.Id == request.CashRegisterId);
                if (cashRegCreate == null)
                    return BadRequest("Cash register not found for CashRegisterId.");

                var invoiceNumber = string.IsNullOrEmpty(request.InvoiceNumber) ? GenerateInvoiceNumber() : request.InvoiceNumber;

                var existingInvoice = await _context.Invoices.FirstOrDefaultAsync(i => i.InvoiceNumber == invoiceNumber && i.IsActive);
                if (existingInvoice != null)
                    return BadRequest("Bu fatura numarası zaten kullanılıyor");

                var invoice = new Invoice
                {
                    Id = Guid.NewGuid(),
                    InvoiceNumber = invoiceNumber,
                    InvoiceDate = request.InvoiceDate,
                    DueDate = request.DueDate,
                    Status = InvoiceStatus.Draft,
                    Subtotal = request.Subtotal,
                    TaxAmount = request.TaxAmount,
                    TotalAmount = request.TotalAmount,
                    PaidAmount = 0,
                    RemainingAmount = request.TotalAmount,
                    CustomerName = request.CustomerName,
                    CustomerEmail = request.CustomerEmail,
                    CustomerPhone = request.CustomerPhone,
                    CustomerAddress = request.CustomerAddress,
                    CustomerTaxNumber = request.CustomerTaxNumber,
                    CompanyName = request.CompanyName,
                    CompanyTaxNumber = request.CompanyTaxNumber,
                    CompanyAddress = request.CompanyAddress,
                    CompanyPhone = request.CompanyPhone,
                    CompanyEmail = request.CompanyEmail,
                    TseSignature = request.TseSignature,
                    KassenId = cashRegCreate.RegisterNumber,
                    CashRegisterId = request.CashRegisterId,
                    TseTimestamp = request.TseTimestamp,
                    PaymentMethod = request.PaymentMethod,
                    PaymentReference = request.PaymentReference,
                    InvoiceItems = request.InvoiceItems,
                    TaxDetails = request.TaxDetails,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                _context.Invoices.Add(invoice);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Invoice created with ID: {Id}", invoice.Id);
                return CreatedAtAction(nameof(GetInvoice), new { id = invoice.Id }, invoice);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating invoice");
                return StatusCode(500, "Internal server error");
            }
        }

        [HasPermission(AppPermissions.InvoiceManage)]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateInvoice(Guid id, UpdateInvoiceRequest request)
        {
            try
            {
                var existingInvoice = await _context.Invoices.FindAsync(id);
                if (existingInvoice == null || !existingInvoice.IsActive)
                    return NotFound(_messages.Get(ApiMessageKeys.InvoiceNotFound));

                // Fiscal immutability: POS-originated invoices must not be updated (TSE/receipt chain integrity).
                if (existingInvoice.SourcePaymentId.HasValue)
                {
                    _logger.LogWarning("Update rejected: invoice {InvoiceId} originated from POS payment {PaymentId}. Fiscal records are immutable.", id, existingInvoice.SourcePaymentId.Value);
                    return BadRequest(new { message = "Invoice originated from POS payment and cannot be updated for fiscal compliance." });
                }

                // Finalized invoices (Paid, Sent, CreditNote, Cancelled) are immutable.
                if (existingInvoice.Status == InvoiceStatus.Paid || existingInvoice.Status == InvoiceStatus.Sent ||
                    existingInvoice.Status == InvoiceStatus.CreditNote || existingInvoice.Status == InvoiceStatus.Cancelled)
                {
                    _logger.LogWarning("Update rejected: invoice {InvoiceId} is finalized (Status={Status}). Finalized invoices cannot be updated.", id, existingInvoice.Status);
                    return BadRequest(new { message = "Invoice cannot be modified after finalization." });
                }

                existingInvoice.InvoiceDate = request.InvoiceDate;
                existingInvoice.DueDate = request.DueDate;
                existingInvoice.Status = request.Status;
                existingInvoice.Subtotal = request.Subtotal;
                existingInvoice.TaxAmount = request.TaxAmount;
                existingInvoice.TotalAmount = request.TotalAmount;
                existingInvoice.RemainingAmount = request.TotalAmount - existingInvoice.PaidAmount;
                existingInvoice.CustomerName = request.CustomerName;
                existingInvoice.CustomerEmail = request.CustomerEmail;
                existingInvoice.CustomerPhone = request.CustomerPhone;
                existingInvoice.CustomerAddress = request.CustomerAddress;
                existingInvoice.CustomerTaxNumber = request.CustomerTaxNumber;
                existingInvoice.CompanyName = request.CompanyName;
                existingInvoice.CompanyTaxNumber = request.CompanyTaxNumber;
                existingInvoice.CompanyAddress = request.CompanyAddress;
                existingInvoice.CompanyPhone = request.CompanyPhone;
                existingInvoice.CompanyEmail = request.CompanyEmail;
                existingInvoice.PaymentMethod = request.PaymentMethod;
                existingInvoice.PaymentReference = request.PaymentReference;
                existingInvoice.InvoiceItems = request.InvoiceItems;
                existingInvoice.TaxDetails = request.TaxDetails;
                existingInvoice.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating invoice {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HasPermission(AppPermissions.InvoiceManage)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteInvoice(Guid id)
        {
            try
            {
                var invoice = await _context.Invoices.FindAsync(id);
                if (invoice == null || !invoice.IsActive)
                    return NotFound(_messages.Get(ApiMessageKeys.InvoiceNotFound));

                // Fiscal immutability: POS-originated and finalized invoices must not be soft-deleted (audit integrity).
                if (invoice.SourcePaymentId.HasValue)
                {
                    _logger.LogWarning("Delete rejected: invoice {InvoiceId} originated from POS payment {PaymentId}. Fiscal records are immutable.", id, invoice.SourcePaymentId.Value);
                    return BadRequest(new { message = "Invoice originated from POS payment and cannot be deleted for fiscal compliance." });
                }
                if (invoice.Status == InvoiceStatus.Paid || invoice.Status == InvoiceStatus.Sent ||
                    invoice.Status == InvoiceStatus.CreditNote || invoice.Status == InvoiceStatus.Cancelled)
                {
                    _logger.LogWarning("Delete rejected: invoice {InvoiceId} is finalized (Status={Status}).", id, invoice.Status);
                    return BadRequest(new { message = "Invoice cannot be deleted after finalization." });
                }

                invoice.IsActive = false;
                invoice.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting invoice {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HasPermission(AppPermissions.InvoiceManage)]
        [HttpPost("{id}/duplicate")]
        public async Task<ActionResult<Invoice>> DuplicateInvoice(Guid id)
        {
            try
            {
                var original = await _context.Invoices.FindAsync(id);
                if (original == null || !original.IsActive)
                    return NotFound(_messages.Get(ApiMessageKeys.OriginalInvoiceNotFound));

                var newInvoice = new Invoice
                {
                    Id = Guid.NewGuid(),
                    InvoiceNumber = GenerateInvoiceNumber(),
                    InvoiceDate = DateTime.Now,
                    DueDate = DateTime.Now.AddDays(30),
                    Status = InvoiceStatus.Draft, // Status reset
                    Subtotal = original.Subtotal,
                    TaxAmount = original.TaxAmount,
                    TotalAmount = original.TotalAmount,
                    PaidAmount = 0,
                    RemainingAmount = original.TotalAmount,

                    // Copy Customer & Company info
                    CustomerName = original.CustomerName,
                    CustomerEmail = original.CustomerEmail,
                    CustomerPhone = original.CustomerPhone,
                    CustomerAddress = original.CustomerAddress,
                    CustomerTaxNumber = original.CustomerTaxNumber,
                    CompanyName = original.CompanyName,
                    CompanyTaxNumber = original.CompanyTaxNumber,
                    CompanyAddress = original.CompanyAddress,
                    CompanyPhone = original.CompanyPhone,
                    CompanyEmail = original.CompanyEmail,

                    // New TSE Signature (placeholder until finalized)
                    TseSignature = string.Empty,
                    KassenId = original.KassenId,
                    CashRegisterId = original.CashRegisterId,
                    TseTimestamp = DateTime.Now,

                    InvoiceItems = original.InvoiceItems,
                    TaxDetails = original.TaxDetails,

                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                _context.Invoices.Add(newInvoice);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Invoice duplicated. Original: {OriginalId}, New: {NewId}", id, newInvoice.Id);
                return CreatedAtAction(nameof(GetInvoice), new { id = newInvoice.Id }, newInvoice);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error duplicating invoice {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HasPermission(AppPermissions.InvoiceManage)]
        [HttpPost("{id}/credit-note")]
        public async Task<ActionResult<Invoice>> CreateCreditNote(Guid id, [FromBody] CreateCreditNoteRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var original = await _context.Invoices.FindAsync(id);
                if (original == null || !original.IsActive)
                    return NotFound("Original invoice not found.");

                // Only allow credit notes for Paid or Sent invoices
                if (original.Status != InvoiceStatus.Paid && original.Status != InvoiceStatus.Sent)
                    return BadRequest("Credit notes can only be created for Paid or Sent invoices.");

                // Prevent duplicate storno for the same original
                var existingCreditNote = await _context.Invoices
                    .AnyAsync(i => i.OriginalInvoiceId == id && i.DocumentType == DocumentType.CreditNote && i.IsActive);
                if (existingCreditNote)
                    return Conflict("A credit note already exists for this invoice.");

                var userId = User.GetActorUserId();

                // Sprint 3: Allocate fiscal BelegNr for storno (credit note) and sign with TSE
                if (original.CashRegisterId == Guid.Empty)
                    return BadRequest("Invoice has no CashRegisterId; cannot create credit note.");
                var kassenId = original.KassenId ?? string.Empty;
                if (string.IsNullOrWhiteSpace(kassenId))
                    return BadRequest("Invoice has no fiscal KassenId (register number); cannot sign storno.");
                var stornoBelegNr = await _receiptSequenceService.AllocateNextBelegNrAsync(original.CashRegisterId, kassenId, DateTime.UtcNow);
                var cashRegisterId = original.CashRegisterId;
                var negatedTotal = -original.TotalAmount;
                string tseSignature = string.Empty;
                var tseTimestamp = DateTime.UtcNow;
                try
                {
                    var taxDetailsJson = original.TaxDetails?.RootElement.ValueKind == JsonValueKind.Object
                        ? original.TaxDetails.RootElement.GetRawText()
                        : "{}";
                    var sigResult = await FiscalTseSigning.SignAsync(
                        _tseService,
                        new FiscalSigningRequest(
                            cashRegisterId,
                            stornoBelegNr,
                            negatedTotal,
                            kassenId,
                            PrevSignatureValue: null,
                            Timestamp: tseTimestamp,
                            TaxDetailsJson: taxDetailsJson));
                    tseSignature = sigResult.CompactJws;
                    _logger.LogInformation("TSE signature generated for storno BelegNr {BelegNr}", stornoBelegNr);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to generate TSE signature for storno (invoice {OriginalId})", original.Id);
                    return StatusCode(500, "TSE signature generation failed for credit note.");
                }

                var creditNote = new Invoice
                {
                    Id = Guid.NewGuid(),
                    InvoiceNumber = stornoBelegNr,
                    InvoiceDate = DateTime.UtcNow,
                    DueDate = DateTime.UtcNow,
                    Status = InvoiceStatus.CreditNote,
                    DocumentType = DocumentType.CreditNote,
                    OriginalInvoiceId = original.Id,
                    StornoReasonCode = request.ReasonCode,
                    StornoReasonText = request.ReasonText,

                    Subtotal = -original.Subtotal,
                    TaxAmount = -original.TaxAmount,
                    TotalAmount = negatedTotal,
                    PaidAmount = negatedTotal,
                    RemainingAmount = 0,

                    CustomerName = original.CustomerName,
                    CustomerEmail = original.CustomerEmail,
                    CustomerPhone = original.CustomerPhone,
                    CustomerAddress = original.CustomerAddress,
                    CustomerTaxNumber = original.CustomerTaxNumber,
                    CompanyName = original.CompanyName,
                    CompanyTaxNumber = original.CompanyTaxNumber,
                    CompanyAddress = original.CompanyAddress,
                    CompanyPhone = original.CompanyPhone,
                    CompanyEmail = original.CompanyEmail,

                    TseSignature = tseSignature,
                    KassenId = kassenId,
                    TseTimestamp = tseTimestamp,
                    CashRegisterId = original.CashRegisterId,

                    InvoiceItems = original.InvoiceItems,
                    TaxDetails = original.TaxDetails,

                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId,
                    IsActive = true
                };

                _context.Invoices.Add(creditNote);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Credit note {CreditNoteId} created for original invoice {OriginalId} by user {UserId}",
                    creditNote.Id, original.Id, userId);

                return CreatedAtAction(nameof(GetInvoice), new { id = creditNote.Id }, creditNote);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating credit note for invoice {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HasPermission(AppPermissions.InvoiceView)]
        [HttpGet("{id}/pdf")]
        [Produces("application/pdf")]
        public async Task<IActionResult> GetInvoicePdf(Guid id)
        {
            try
            {
                var pdf = await _invoicePdfService.GenerateInvoicePdfAsync(id, cancellationToken: HttpContext.RequestAborted);
                return File(pdf, "application/pdf", $"Rechnung_{id}.pdf");
            }
            catch (KeyNotFoundException)
            {
                return NotFound("Invoice not found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating PDF for invoice {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HasPermission(AppPermissions.InvoiceView)]
        [HttpGet("{id}/preview")]
        [Produces("application/pdf")]
        public async Task<IActionResult> PreviewInvoice(Guid id)
        {
            try
            {
                var pdf = await _invoicePdfService.GenerateInvoicePdfAsync(id, cancellationToken: HttpContext.RequestAborted);
                return File(pdf, "application/pdf");
            }
            catch (KeyNotFoundException)
            {
                return NotFound("Invoice not found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating invoice preview for {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HasPermission(AppPermissions.InvoiceView)]
        [HttpPost("{id}/resend")]
        public async Task<IActionResult> ResendInvoice(Guid id, [FromBody] ResendInvoiceRequest? request)
        {
            try
            {
                if (request != null && !TryValidateModel(request))
                    return ValidationProblem(ModelState);

                var result = await _invoicePdfService.ResendInvoiceEmailAsync(
                    id,
                    request?.RecipientEmail,
                    HttpContext.RequestAborted);

                if (!result)
                    return BadRequest(new { error = "Failed to send email" });

                return Ok(new { message = "Invoice resent successfully" });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { error = "Invoice not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resending invoice email for {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HasPermission(AppPermissions.InvoiceView)]
        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<Invoice>>> SearchInvoices([FromQuery] string query)
        {
            // Backward compatibility wrapper around GetInvoices
            return await GetInvoices(query: query, page: 1, pageSize: 20);
        }

        [HasPermission(AppPermissions.InvoiceView)]
        [HttpGet("status/{status}")]
        public async Task<ActionResult<IEnumerable<Invoice>>> GetInvoicesByStatus(InvoiceStatus status)
        {
            // Backward compatibility wrapper around GetInvoices
            return await GetInvoices(status: status, page: 1, pageSize: 50);
        }

        // POST: api/Invoice/backfill-from-payments
        // One-time (idempotent) backfill: create Invoice rows for PaymentDetails that have no matching Invoice yet.
        // Safe to call repeatedly — already-backfilled payments are skipped via SourcePaymentId lookup.
        // Required: permission system.critical (typically SuperAdmin).
        // Responses: 200 OK (success + counts), 401 Unauthorized (no token), 403 Forbidden (insufficient permission)
        [HttpPost("backfill-from-payments")]
        [HasPermission(AppPermissions.SystemCritical)]
        public async Task<IActionResult> BackfillInvoicesFromPayments()
        {
            int inserted = 0, skipped = 0, failed = 0;

            try
            {
                // Load only active payments with a ReceiptNumber (real POS transactions)
                var payments = await _context.PaymentDetails
                    .AsNoTracking()
                    .Where(p => p.IsActive && p.ReceiptNumber != null && p.ReceiptNumber != string.Empty)
                    .OrderBy(p => p.CreatedAt)
                    .ToListAsync();

                // Load existing SourcePaymentIds in one query for efficient lookup
                var existingSourceIds = await _context.Invoices
                    .AsNoTracking()
                    .Where(i => i.SourcePaymentId != null)
                    .Select(i => i.SourcePaymentId!.Value)
                    .ToHashSetAsync();

                foreach (var payment in payments)
                {
                    if (existingSourceIds.Contains(payment.Id))
                    {
                        skipped++;
                        continue;
                    }

                    try
                    {
                        if (payment.CashRegisterId == Guid.Empty)
                        {
                            _logger.LogWarning("Backfill: Payment {PaymentId} has no CashRegisterId; skipping", payment.Id);
                            skipped++;
                            continue;
                        }

                        var regNum = await _context.CashRegisters.AsNoTracking()
                            .Where(r => r.Id == payment.CashRegisterId)
                            .Select(r => r.RegisterNumber)
                            .FirstOrDefaultAsync();
                        if (string.IsNullOrEmpty(regNum))
                        {
                            _logger.LogWarning("Backfill: Cash register {Id} missing for Payment {PaymentId}", payment.CashRegisterId, payment.Id);
                            skipped++;
                            continue;
                        }

                        var derived = await _invoiceService.ResolveInvoiceFromPaymentAsync(
                            payment, HttpContext.RequestAborted);
                        var invoice = new Invoice
                        {
                            Id = Guid.NewGuid(),
                            SourcePaymentId = payment.Id,
                            InvoiceNumber = string.IsNullOrEmpty(payment.ReceiptNumber)
                                ? $"BF-{payment.Id:N}"
                                : payment.ReceiptNumber,
                            InvoiceDate = derived.InvoiceDate,
                            DueDate = derived.DueDate,
                            Status = derived.Status,
                            Subtotal = derived.Subtotal,
                            TaxAmount = derived.TaxAmount,
                            TotalAmount = derived.TotalAmount,
                            PaidAmount = derived.PaidAmount,
                            RemainingAmount = derived.RemainingAmount,
                            CustomerName = derived.CustomerName,
                            CustomerTaxNumber = derived.CustomerTaxNumber,
                            CompanyName = derived.CompanyName,
                            CompanyTaxNumber = derived.CompanyTaxNumber,
                            CompanyAddress = derived.CompanyAddress,
                            CompanyPhone = derived.CompanyPhone,
                            CompanyEmail = derived.CompanyEmail,
                            TseSignature = derived.TseSignature,
                            KassenId = regNum,
                            TseTimestamp = derived.TseTimestamp,
                            CashRegisterId = derived.CashRegisterId,
                            PaymentMethod = derived.PaymentMethod,
                            PaymentReference = derived.PaymentReference,
                            PaymentDate = derived.PaymentDate,
                            InvoiceItems = derived.InvoiceItems,
                            TaxDetails = derived.TaxDetails,
                            CreatedAt = DateTime.UtcNow,
                            IsActive = true
                        };

                        _context.Invoices.Add(invoice);
                        await _context.SaveChangesAsync();
                        inserted++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Backfill: failed to insert invoice for PaymentId={PaymentId}", payment.Id);
                        failed++;
                        // Detach the failed entity so subsequent inserts are not blocked
                        _context.ChangeTracker.Clear();
                    }
                }

                _logger.LogInformation(
                    "Backfill complete. Inserted={Inserted}, Skipped={Skipped}, Failed={Failed}",
                    inserted, skipped, failed);

                return Ok(new
                {
                    success = true,
                    inserted,
                    skipped,
                    failed,
                    message = $"Backfill complete: {inserted} inserted, {skipped} already existed, {failed} failed."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Backfill failed after {Inserted} inserts", inserted);
                return StatusCode(500, new { success = false, inserted, skipped, failed, error = ex.Message });
            }
        }

        private string GenerateInvoiceNumber()
        {
            var date = DateTime.Now.ToString("yyyyMMdd");
            var random = new Random();
            var sequence = random.Next(1000, 9999);
            return $"INV-{date}-{sequence}";
        }
    }

    public class CreateInvoiceRequest
    {
        public string? InvoiceNumber { get; set; }
        public DateTime InvoiceDate { get; set; } = DateTime.Now;
        public DateTime DueDate { get; set; } = DateTime.Now.AddDays(30);
        public decimal Subtotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerEmail { get; set; }
        public string? CustomerPhone { get; set; }
        public string? CustomerAddress { get; set; }
        public string? CustomerTaxNumber { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string CompanyTaxNumber { get; set; } = string.Empty;
        public string CompanyAddress { get; set; } = string.Empty;
        public string? CompanyPhone { get; set; }
        public string? CompanyEmail { get; set; }
        public string TseSignature { get; set; } = string.Empty;
        /// <summary>Fiscal Kassen-ID is taken from CashRegister.RegisterNumber when saving.</summary>
        public string KassenId { get; set; } = string.Empty;
        public Guid CashRegisterId { get; set; }
        public DateTime TseTimestamp { get; set; } = DateTime.Now;
        public PaymentMethod? PaymentMethod { get; set; }
        public string? PaymentReference { get; set; }
        public JsonDocument? InvoiceItems { get; set; }
        public JsonDocument TaxDetails { get; set; } = JsonDocument.Parse("{}");
    }

    public class UpdateInvoiceRequest
    {
        public DateTime InvoiceDate { get; set; }
        public DateTime DueDate { get; set; }
        public InvoiceStatus Status { get; set; }
        public decimal Subtotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerEmail { get; set; }
        public string? CustomerPhone { get; set; }
        public string? CustomerAddress { get; set; }
        public string? CustomerTaxNumber { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string CompanyTaxNumber { get; set; } = string.Empty;
        public string CompanyAddress { get; set; } = string.Empty;
        public string? CompanyPhone { get; set; }
        public string? CompanyEmail { get; set; }
        public PaymentMethod? PaymentMethod { get; set; }
        public string? PaymentReference { get; set; }
        public JsonDocument? InvoiceItems { get; set; }
        public JsonDocument TaxDetails { get; set; } = JsonDocument.Parse("{}");
    }

    public class CreateCreditNoteRequest
    {
        [Required]
        [StringLength(50)]
        public string ReasonCode { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string ReasonText { get; set; } = string.Empty;
    }

    public class ResendInvoiceRequest
    {
        [EmailAddress]
        public string? RecipientEmail { get; set; }
    }
}
