using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Linq;
using System.Security.Claims;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KasseAPI_Final.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class InvoiceController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<InvoiceController> _logger;
        private readonly CompanyProfileOptions _companyProfile;

        public InvoiceController(
            AppDbContext context,
            ILogger<InvoiceController> logger,
            IOptions<CompanyProfileOptions> companyProfile)
        {
            _context = context;
            _logger = logger;
            _companyProfile = companyProfile.Value;
            // License configuration for QuestPDF (Community)
            QuestPDF.Settings.License = LicenseType.Community;
        }

        // GET: api/Invoice/list
        // Date boundary: from is inclusive (>= start-of-day UTC), to is inclusive via exclusive-next-day (< to+1day UTC)
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
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 50;
                if (pageSize > 200) pageSize = 200;

                var queryable = _context.Invoices.AsNoTracking().Where(i => i.IsActive);

                if (from.HasValue)
                    queryable = queryable.Where(i => i.InvoiceDate >= from.Value.ToUniversalTime());
                if (to.HasValue)
                    queryable = queryable.Where(i => i.InvoiceDate < to.Value.ToUniversalTime().AddDays(1));
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
                        KassenId = i.KassenId,
                        TseSignature = i.TseSignature,
                        DocumentType = i.DocumentType,
                        OriginalInvoiceId = i.OriginalInvoiceId
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
        // POS-backed listing — sources data from PaymentDetails (actual POS transactions)
        // Date boundary: from >= fromUtc, to < nextDayStartUtc
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
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 50;
                if (pageSize > 200) pageSize = 200;

                var queryable = _context.PaymentDetails.AsNoTracking().Where(p => p.IsActive);

                // Date filtering on CreatedAt (payment timestamp)
                if (from.HasValue)
                {
                    var fromUtc = from.Value.Kind == DateTimeKind.Utc
                        ? from.Value.Date
                        : from.Value.ToUniversalTime().Date;
                    queryable = queryable.Where(p => p.CreatedAt >= fromUtc);
                }
                if (to.HasValue)
                {
                    var toUtc = to.Value.Kind == DateTimeKind.Utc
                        ? to.Value.Date.AddDays(1)
                        : to.Value.ToUniversalTime().Date.AddDays(1);
                    queryable = queryable.Where(p => p.CreatedAt < toUtc);
                }

                if (!string.IsNullOrWhiteSpace(cashRegisterId))
                    queryable = queryable.Where(p => p.KassenId == cashRegisterId);

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
                    "totalamount"   => isAsc ? queryable.OrderBy(p => p.TotalAmount) : queryable.OrderByDescending(p => p.TotalAmount),
                    _               => isAsc ? queryable.OrderBy(p => p.CreatedAt) : queryable.OrderByDescending(p => p.CreatedAt)
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
                        KassenId = p.KassenId,
                        TseSignature = p.TseSignature
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

        // GET: api/Invoice/export
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

                if (from.HasValue) queryable = queryable.Where(i => i.InvoiceDate >= from.Value.ToUniversalTime());
                if (to.HasValue) queryable = queryable.Where(i => i.InvoiceDate < to.Value.ToUniversalTime().AddDays(1));
                if (status.HasValue) queryable = queryable.Where(i => i.Status == status.Value);
                if (cashRegisterId.HasValue) queryable = queryable.Where(i => i.CashRegisterId == cashRegisterId.Value);

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
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(";") || value.Contains("\"") || value.Contains("\n"))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }
            return value;
        }

        // GET: api/Invoice
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

                // 2. Date range filter
                if (from.HasValue)
                    queryable = queryable.Where(i => i.InvoiceDate >= from.Value.ToUniversalTime());
                if (to.HasValue)
                    queryable = queryable.Where(i => i.InvoiceDate <= to.Value.ToUniversalTime().AddDays(1)); // Include end date

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

        // GET: api/Invoice/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Invoice>> GetInvoice(Guid id)
        {
            try
            {
                var invoice = await _context.Invoices.FindAsync(id);

                if (invoice == null || !invoice.IsActive)
                {
                    var posInvoice = await _context.PaymentDetails.FindAsync(id);
                    if (posInvoice == null || !posInvoice.IsActive)
                    {
                        return NotFound("Fatura bulunamadı");
                    }
                    
                    invoice = new Invoice
                    {
                        Id = posInvoice.Id,
                        InvoiceNumber = posInvoice.ReceiptNumber,
                        InvoiceDate = posInvoice.CreatedAt,
                        DueDate = posInvoice.CreatedAt,
                        Status = InvoiceStatus.Paid,
                        Subtotal = posInvoice.TotalAmount - posInvoice.TaxAmount,
                        TaxAmount = posInvoice.TaxAmount,
                        TotalAmount = posInvoice.TotalAmount,
                        PaidAmount = posInvoice.TotalAmount,
                        RemainingAmount = 0,
                        CustomerName = posInvoice.CustomerName,
                        CustomerTaxNumber = posInvoice.Steuernummer,
                        CompanyName = _companyProfile.CompanyName ?? string.Empty, // Filled from CompanyProfile
                        CompanyTaxNumber = _companyProfile.TaxNumber ?? string.Empty,
                        CompanyAddress = $"{_companyProfile.Street} {_companyProfile.ZipCode} {_companyProfile.City}".Trim(),
                        TseSignature = posInvoice.TseSignature,
                        KassenId = posInvoice.KassenId,
                        TseTimestamp = posInvoice.TseTimestamp,
                        PaymentMethod = posInvoice.PaymentMethod,
                        InvoiceItems = posInvoice.PaymentItems,
                        TaxDetails = posInvoice.TaxDetails,
                        IsActive = true
                    };
                }

                return Ok(invoice);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving invoice with ID: {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // POST: api/Invoice
        [HttpPost]
        public async Task<ActionResult<Invoice>> CreateInvoice(CreateInvoiceRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                if (string.IsNullOrWhiteSpace(request.CompanyName)) return BadRequest("Firma adı gerekli");
                if (string.IsNullOrWhiteSpace(request.CompanyTaxNumber)) return BadRequest("Firma vergi numarası gerekli");
                if (!request.CompanyTaxNumber.StartsWith("ATU") || request.CompanyTaxNumber.Length != 11) return BadRequest("Firma vergi numarası ATU formatında olmalı");

                var invoiceNumber = string.IsNullOrEmpty(request.InvoiceNumber) ? GenerateInvoiceNumber() : request.InvoiceNumber;

                var existingInvoice = await _context.Invoices.FirstOrDefaultAsync(i => i.InvoiceNumber == invoiceNumber && i.IsActive);
                if (existingInvoice != null) return BadRequest("Bu fatura numarası zaten kullanılıyor");

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
                    KassenId = request.KassenId,
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

        // PUT: api/Invoice/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateInvoice(Guid id, UpdateInvoiceRequest request)
        {
            try
            {
                var existingInvoice = await _context.Invoices.FindAsync(id);
                if (existingInvoice == null || !existingInvoice.IsActive) return NotFound("Fatura bulunamadı");

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

        // DELETE: api/Invoice/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteInvoice(Guid id)
        {
            try
            {
                var invoice = await _context.Invoices.FindAsync(id);
                if (invoice == null || !invoice.IsActive) return NotFound("Fatura bulunamadı");

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

        // POST: api/Invoice/5/duplicate
        [HttpPost("{id}/duplicate")]
        public async Task<ActionResult<Invoice>> DuplicateInvoice(Guid id)
        {
            try
            {
                var original = await _context.Invoices.FindAsync(id);
                if (original == null || !original.IsActive) return NotFound("Orijinal fatura bulunamadı");

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

        // POST: api/Invoice/{id}/credit-note
        // Creates a reversal (Gutschrift/Stornobeleg) linked to the original invoice.
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

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                var creditNote = new Invoice
                {
                    Id = Guid.NewGuid(),
                    InvoiceNumber = GenerateInvoiceNumber(),
                    InvoiceDate = DateTime.UtcNow,
                    DueDate = DateTime.UtcNow,
                    Status = InvoiceStatus.CreditNote,
                    DocumentType = DocumentType.CreditNote,
                    OriginalInvoiceId = original.Id,
                    StornoReasonCode = request.ReasonCode,
                    StornoReasonText = request.ReasonText,

                    // Negate amounts for reversal
                    Subtotal = -original.Subtotal,
                    TaxAmount = -original.TaxAmount,
                    TotalAmount = -original.TotalAmount,
                    PaidAmount = -original.TotalAmount,
                    RemainingAmount = 0,

                    // Copy customer & company info
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

                    // TSE placeholder — real signing to be plugged in later
                    TseSignature = string.Empty,
                    KassenId = original.KassenId,
                    TseTimestamp = DateTime.UtcNow,
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

        // GET: api/Invoice/5/pdf
        [HttpGet("{id}/pdf")]
        public async Task<IActionResult> GetInvoicePdf(Guid id, [FromQuery] bool copy = false)
        {
            try
            {
                var invoice = await _context.Invoices.FindAsync(id);
                if (invoice == null || !invoice.IsActive)
                {
                    var posInvoice = await _context.PaymentDetails.FindAsync(id);
                    if (posInvoice == null || !posInvoice.IsActive)
                    {
                        return NotFound("Fatura bulunamadı");
                    }
                    
                    invoice = new Invoice
                    {
                        Id = posInvoice.Id,
                        InvoiceNumber = posInvoice.ReceiptNumber,
                        InvoiceDate = posInvoice.CreatedAt,
                        DueDate = posInvoice.CreatedAt,
                        Status = InvoiceStatus.Paid,
                        Subtotal = posInvoice.TotalAmount - posInvoice.TaxAmount,
                        TaxAmount = posInvoice.TaxAmount,
                        TotalAmount = posInvoice.TotalAmount,
                        PaidAmount = posInvoice.TotalAmount,
                        RemainingAmount = 0,
                        CustomerName = posInvoice.CustomerName,
                        CustomerTaxNumber = posInvoice.Steuernummer,
                        CompanyName = _companyProfile.CompanyName ?? string.Empty, // Filled from CompanyProfile
                        CompanyTaxNumber = _companyProfile.TaxNumber ?? string.Empty,
                        CompanyAddress = $"{_companyProfile.Street} {_companyProfile.ZipCode} {_companyProfile.City}".Trim(),
                        TseSignature = posInvoice.TseSignature,
                        KassenId = posInvoice.KassenId,
                        TseTimestamp = posInvoice.TseTimestamp,
                        PaymentMethod = posInvoice.PaymentMethod,
                        InvoiceItems = posInvoice.PaymentItems,
                        TaxDetails = posInvoice.TaxDetails,
                        IsActive = true
                    };
                }

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
                                text.Span("INVOICE").FontSize(20).SemiBold();
                                if (copy)
                                {
                                    text.EmptyLine();
                                    text.Span("COPY / KOPIE").FontSize(14).FontColor(Colors.Red.Medium);
                                }
                            });
                        });

                        page.Content().PaddingVertical(1, Unit.Centimetre).Column(col =>
                        {
                            // Info Grid
                            col.Item().Row(row =>
                            {
                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().Text("Bill To:").SemiBold();
                                    c.Item().Text(invoice.CustomerName ?? "Cash Customer");
                                    if(!string.IsNullOrEmpty(invoice.CustomerAddress)) c.Item().Text(invoice.CustomerAddress);
                                    if(!string.IsNullOrEmpty(invoice.CustomerTaxNumber)) c.Item().Text(invoice.CustomerTaxNumber);
                                });

                                row.RelativeItem().AlignRight().Column(c =>
                                {
                                    c.Item().Text($"Invoice #: {invoice.InvoiceNumber}");
                                    c.Item().Text($"Date: {invoice.InvoiceDate:dd.MM.yyyy}");
                                    c.Item().Text($"Status: {invoice.Status}");
                                    c.Item().Text($"KassenID: {invoice.KassenId}");
                                });
                            });

                            col.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                            // Items Table
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3); // Name
                                    columns.RelativeColumn();  // Qty
                                    columns.RelativeColumn();  // Price
                                    columns.RelativeColumn();  // Tax
                                    columns.RelativeColumn();  // Total
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(CellStyle).Text("Description");
                                    header.Cell().Element(CellStyle).AlignRight().Text("Qty");
                                    header.Cell().Element(CellStyle).AlignRight().Text("Price");
                                    header.Cell().Element(CellStyle).AlignRight().Text("VAT");
                                    header.Cell().Element(CellStyle).AlignRight().Text("Total");

                                    static IContainer CellStyle(IContainer container) => 
                                        container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5).DefaultTextStyle(x => x.SemiBold());
                                });

                                // Deserialize Items
                                if (invoice.InvoiceItems?.RootElement.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var item in invoice.InvoiceItems.RootElement.EnumerateArray())
                                    {
                                        var name = item.TryGetProperty("productName", out var n) ? n.GetString() : "Item";
                                        var qty = item.TryGetProperty("quantity", out var q) ? q.GetInt32() : 0;
                                        var price = item.TryGetProperty("unitPrice", out var p) ? p.GetDecimal() : 0;
                                        var total = item.TryGetProperty("totalPrice", out var t) ? t.GetDecimal() : 0;
                                        var taxStart = item.TryGetProperty("taxRate", out var tr) ? tr.GetDecimal() : 0;
                                        var taxRate = taxStart > 1 ? taxStart / 100 : taxStart; // Simple heuristic if stored as 20 vs 0.2

                                        table.Cell().Element(BodyCellStyle).Text(name);
                                        table.Cell().Element(BodyCellStyle).AlignRight().Text(qty.ToString());
                                        table.Cell().Element(BodyCellStyle).AlignRight().Text($"{price:F2}");
                                        table.Cell().Element(BodyCellStyle).AlignRight().Text($"{taxRate:P0}");
                                        table.Cell().Element(BodyCellStyle).AlignRight().Text($"{total:F2}");
                                    }
                                }

                                static IContainer BodyCellStyle(IContainer container) => 
                                    container.PaddingVertical(5);
                            });

                            col.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                            // Totals
                            col.Item().AlignRight().Column(c =>
                            {
                                c.Item().Text($"Subtotal: {invoice.Subtotal:C}");
                                c.Item().Text($"Tax: {invoice.TaxAmount:C}");
                                c.Item().Text($"Total: {invoice.TotalAmount:C}").Bold().FontSize(12);
                            });

                            col.Item().PaddingVertical(10);

                            // RKSV / Footer
                            if (!string.IsNullOrEmpty(invoice.TseSignature))
                            {
                                col.Item().Background(Colors.Grey.Lighten4).Padding(10).Column(c =>
                                {
                                    c.Item().Text("RKSV Signature (TSE)").FontSize(8).SemiBold();
                                    c.Item().Text(invoice.TseSignature).FontFamily("Consolas").FontSize(8);
                                    c.Item().Text($"Timestamp: {invoice.TseTimestamp:O}").FontSize(8);
                                });
                            }
                        });

                        page.Footer().AlignCenter().Text(x =>
                        {
                            x.Span("Page ");
                            x.CurrentPageNumber();
                        });
                    });
                });

                var pdfBytes = document.GeneratePdf();
                return File(pdfBytes, "application/pdf", $"Invoice-{invoice.InvoiceNumber}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating PDF for invoice {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/Invoice/search?query=...
        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<Invoice>>> SearchInvoices([FromQuery] string query)
        {
             // Backward compatibility wrapper around GetInvoices
             return await GetInvoices(query: query, page: 1, pageSize: 20);
        }

        // GET: api/Invoice/status/{status}
        [HttpGet("status/{status}")]
        public async Task<ActionResult<IEnumerable<Invoice>>> GetInvoicesByStatus(InvoiceStatus status)
        {
            // Backward compatibility wrapper around GetInvoices
            return await GetInvoices(status: status, page: 1, pageSize: 50);
        }

        // POST: api/Invoice/backfill-from-payments
        // One-time (idempotent) backfill: create Invoice rows for PaymentDetails that have no matching Invoice yet.
        // Safe to call repeatedly — already-backfilled payments are skipped via SourcePaymentId lookup.
        // Required role: Admin
        // Responses: 200 OK (success + counts), 401 Unauthorized (no token), 403 Forbidden (non-Admin role)
        [HttpPost("backfill-from-payments")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> BackfillInvoicesFromPayments()
        {
            int inserted = 0, skipped = 0, failed = 0;

            try
            {
                var companyAddress = $"{_companyProfile.Street}, {_companyProfile.ZipCode} {_companyProfile.City}";

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

                // Build a cash register mapping directory to resolve KassenId to Guid efficiently
                var cashRegisters = await _context.CashRegisters.AsNoTracking().ToListAsync();
                var crIdMap = cashRegisters.ToDictionary(cr => cr.Id.ToString(), cr => cr.Id);
                var crNumMap = cashRegisters.ToDictionary(cr => cr.RegisterNumber, cr => cr.Id);

                foreach (var payment in payments)
                {
                    if (existingSourceIds.Contains(payment.Id))
                    {
                        skipped++;
                        continue;
                    }

                    try
                    {
                        Guid? resolvedCashRegisterId = null;
                        if (!string.IsNullOrWhiteSpace(payment.KassenId))
                        {
                            if (crIdMap.TryGetValue(payment.KassenId, out var mappedId))
                            {
                                resolvedCashRegisterId = mappedId;
                            }
                            else if (crNumMap.TryGetValue(payment.KassenId, out var mappedIdByNum))
                            {
                                resolvedCashRegisterId = mappedIdByNum;
                            }
                        }

                        if (resolvedCashRegisterId == null)
                        {
                            _logger.LogWarning("Backfill: Could not resolve valid CashRegisterId for KassenId {KassenId} on Payment {PaymentId}", payment.KassenId, payment.Id);
                        }

                        var invoice = new Invoice
                        {
                            Id = Guid.NewGuid(),
                            SourcePaymentId = payment.Id,
                            InvoiceNumber = string.IsNullOrEmpty(payment.ReceiptNumber)
                                ? $"BF-{payment.Id:N}"
                                : payment.ReceiptNumber,
                            InvoiceDate = payment.CreatedAt,
                            DueDate = payment.CreatedAt,
                            Status = InvoiceStatus.Paid,
                            Subtotal = payment.TotalAmount - payment.TaxAmount,
                            TaxAmount = payment.TaxAmount,
                            TotalAmount = payment.TotalAmount,
                            PaidAmount = payment.TotalAmount,
                            RemainingAmount = 0,
                            CustomerName = payment.CustomerName,
                            CustomerTaxNumber = payment.Steuernummer,
                            CompanyName = _companyProfile.CompanyName,
                            CompanyTaxNumber = _companyProfile.TaxNumber,
                            CompanyAddress = companyAddress,
                            TseSignature = payment.TseSignature ?? string.Empty,
                            KassenId = payment.KassenId,
                            TseTimestamp = payment.TseTimestamp,
                            CashRegisterId = resolvedCashRegisterId,
                            PaymentMethod = payment.PaymentMethod,
                            PaymentReference = payment.TransactionId,
                            PaymentDate = payment.CreatedAt,
                            InvoiceItems = payment.PaymentItems,
                            TaxDetails = payment.TaxDetails,
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
        public string KassenId { get; set; } = string.Empty;
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
}
