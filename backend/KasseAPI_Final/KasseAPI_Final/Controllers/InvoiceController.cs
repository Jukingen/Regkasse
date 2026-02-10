using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json;

namespace KasseAPI_Final.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class InvoiceController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<InvoiceController> _logger;

        public InvoiceController(AppDbContext context, ILogger<InvoiceController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/Invoice
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Invoice>>> GetInvoices()
        {
            try
            {
                var invoices = await _context.Invoices
                    .Where(i => i.IsActive)
                    .OrderByDescending(i => i.InvoiceDate)
                    .ToListAsync();

                _logger.LogInformation("Retrieved {Count} invoices", invoices.Count);
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
                    return NotFound("Fatura bulunamadı");
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

                // Validation
                if (string.IsNullOrWhiteSpace(request.CompanyName))
                {
                    return BadRequest("Firma adı gerekli");
                }

                if (string.IsNullOrWhiteSpace(request.CompanyTaxNumber))
                {
                    return BadRequest("Firma vergi numarası gerekli");
                }

                // ATU format validation
                if (!request.CompanyTaxNumber.StartsWith("ATU") || request.CompanyTaxNumber.Length != 11)
                {
                    return BadRequest("Firma vergi numarası ATU formatında olmalı (ATU12345678)");
                }

                if (string.IsNullOrWhiteSpace(request.TseSignature))
                {
                    return BadRequest("TSE imzası gerekli");
                }

                if (string.IsNullOrWhiteSpace(request.KassenId))
                {
                    return BadRequest("Kasa ID gerekli");
                }

                // Generate invoice number if not provided
                var invoiceNumber = string.IsNullOrEmpty(request.InvoiceNumber) 
                    ? GenerateInvoiceNumber() 
                    : request.InvoiceNumber;

                // Check invoice number uniqueness
                var existingInvoice = await _context.Invoices
                    .FirstOrDefaultAsync(i => i.InvoiceNumber == invoiceNumber && i.IsActive);
                
                if (existingInvoice != null)
                {
                    return BadRequest("Bu fatura numarası zaten kullanılıyor");
                }

                // Check TSE signature uniqueness
                var existingTseSignature = await _context.Invoices
                    .FirstOrDefaultAsync(i => i.TseSignature == request.TseSignature && i.IsActive);
                
                if (existingTseSignature != null)
                {
                    return BadRequest("Bu TSE imzası zaten kullanılıyor");
                }

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

                _logger.LogInformation("Invoice created with ID: {Id}, Number: {Number}", invoice.Id, invoice.InvoiceNumber);
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
                if (existingInvoice == null || !existingInvoice.IsActive)
                {
                    return NotFound("Fatura bulunamadı");
                }

                // Validation
                if (string.IsNullOrWhiteSpace(request.CompanyName))
                {
                    return BadRequest("Firma adı gerekli");
                }

                if (string.IsNullOrWhiteSpace(request.CompanyTaxNumber))
                {
                    return BadRequest("Firma vergi numarası gerekli");
                }

                // ATU format validation
                if (!request.CompanyTaxNumber.StartsWith("ATU") || request.CompanyTaxNumber.Length != 11)
                {
                    return BadRequest("Firma vergi numarası ATU formatında olmalı (ATU12345678)");
                }

                // Update properties
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

                _logger.LogInformation("Invoice updated with ID: {Id}", id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating invoice with ID: {Id}", id);
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
                if (invoice == null || !invoice.IsActive)
                {
                    return NotFound("Fatura bulunamadı");
                }

                // Soft delete
                invoice.IsActive = false;
                invoice.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Invoice deleted (soft) with ID: {Id}", id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting invoice with ID: {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/Invoice/search?query=...
        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<Invoice>>> SearchInvoices([FromQuery] string query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return BadRequest("Arama sorgusu gerekli");
                }

                var invoices = await _context.Invoices
                    .Where(i => i.IsActive && 
                               (i.InvoiceNumber.Contains(query) || 
                                i.CustomerName.Contains(query) || 
                                i.CompanyName.Contains(query)))
                    .OrderByDescending(i => i.InvoiceDate)
                    .Take(20)
                    .ToListAsync();

                return Ok(invoices);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching invoices with query: {Query}", query);
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/Invoice/status/{status}
        [HttpGet("status/{status}")]
        public async Task<ActionResult<IEnumerable<Invoice>>> GetInvoicesByStatus(InvoiceStatus status)
        {
            try
            {
                var invoices = await _context.Invoices
                    .Where(i => i.IsActive && i.Status == status)
                    .OrderByDescending(i => i.InvoiceDate)
                    .ToListAsync();

                return Ok(invoices);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving invoices by status: {Status}", status);
                return StatusCode(500, "Internal server error");
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
}
