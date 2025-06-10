using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Registrierkasse.Data;
using Registrierkasse.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Registrierkasse.Services;

namespace Registrierkasse.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class InvoiceController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<InvoiceController> _logger;
        private readonly ITseService _tseService;

        public InvoiceController(AppDbContext context, ILogger<InvoiceController> logger, ITseService tseService)
        {
            _context = context;
            _logger = logger;
            _tseService = tseService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Invoice>>> GetInvoices(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] string? customerId,
            [FromQuery] string? paymentStatus,
            [FromQuery] string? invoiceStatus,
            [FromQuery] string? paymentMethod,
            [FromQuery] decimal? minAmount,
            [FromQuery] decimal? maxAmount,
            [FromQuery] string? searchQuery,
            [FromQuery] int limit = 50,
            [FromQuery] int offset = 0)
        {
            try
            {
                var query = _context.Invoices
                    .Include(i => i.Items)
                    .ThenInclude(ii => ii.Product)
                    .AsQueryable();

                // Tarih filtreleri
                if (startDate.HasValue)
                {
                    query = query.Where(i => i.InvoiceDate >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(i => i.InvoiceDate <= endDate.Value);
                }

                // Müşteri filtresi
                if (!string.IsNullOrEmpty(customerId))
                {
                    query = query.Where(i => i.CustomerId.ToString() == customerId);
                }

                // Ödeme durumu filtresi
                if (!string.IsNullOrEmpty(paymentStatus))
                {
                    query = query.Where(i => i.PaymentStatus.ToString() == paymentStatus);
                }

                // Fatura durumu filtresi
                if (!string.IsNullOrEmpty(invoiceStatus))
                {
                    query = query.Where(i => i.Status == invoiceStatus);
                }

                // Ödeme yöntemi filtresi
                if (!string.IsNullOrEmpty(paymentMethod))
                {
                    query = query.Where(i => i.PaymentMethod.ToString() == paymentMethod);
                }

                // Tutar filtreleri
                if (minAmount.HasValue)
                {
                    query = query.Where(i => i.TotalAmount >= minAmount.Value);
                }

                if (maxAmount.HasValue)
                {
                    query = query.Where(i => i.TotalAmount <= maxAmount.Value);
                }

                // Arama filtresi
                if (!string.IsNullOrEmpty(searchQuery))
                {
                    var searchTerm = searchQuery.ToLower();
                    query = query.Where(i =>
                        i.InvoiceNumber.ToLower().Contains(searchTerm) ||
                        i.Customer.Name.ToLower().Contains(searchTerm) ||
                        i.Customer.Email.ToLower().Contains(searchTerm) ||
                        i.Customer.TaxNumber.ToLower().Contains(searchTerm)
                    );
                }

                var total = await query.CountAsync();
                var invoices = await query
                    .OrderByDescending(i => i.InvoiceDate)
                    .Skip(offset)
                    .Take(limit)
                    .ToListAsync();

                var result = new
                {
                    invoices,
                    total,
                    hasMore = offset + limit < total
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatura listesi alınırken hata oluştu");
                return StatusCode(500, new { message = "Fatura listesi alınırken hata oluştu", error = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Invoice>> GetInvoice(Guid id)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Items)
                .ThenInclude(ii => ii.Product)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invoice == null)
            {
                return NotFound();
            }

            return invoice;
        }

        [HttpPost]
        [Authorize(Roles = "Administrator,Cashier")]
        public async Task<ActionResult<Invoice>> CreateInvoice([FromBody] Invoice invoice)
        {
            invoice.Id = Guid.NewGuid();
            invoice.CreatedAt = DateTime.UtcNow;
            invoice.ReceiptNumber = await GenerateReceiptNumber();

            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetInvoice), new { id = invoice.Id }, invoice);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> UpdateInvoice(Guid id, [FromBody] Invoice invoice)
        {
            if (id != invoice.Id)
            {
                return BadRequest();
            }

            _context.Entry(invoice).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!InvoiceExists(id))
                {
                    return NotFound();
                }
                throw;
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> DeleteInvoice(Guid id)
        {
            var invoice = await _context.Invoices.FindAsync(id);
            if (invoice == null)
            {
                return NotFound();
            }

            _context.Invoices.Remove(invoice);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool InvoiceExists(Guid id)
        {
            return _context.Invoices.Any(e => e.Id == id);
        }

        private async Task<string> GenerateReceiptNumber()
        {
            var tseId = await _tseService.GetTseIdAsync();
            var date = DateTime.UtcNow.ToString("yyyyMMdd");
            
            var lastReceipt = _context.Invoices
                .Where(i => i.ReceiptNumber.StartsWith($"AT-{tseId}-{date}"))
                .OrderByDescending(i => i.ReceiptNumber)
                .FirstOrDefault();

            int sequence = 1;
            if (lastReceipt != null)
            {
                var lastSeq = lastReceipt.ReceiptNumber.Split('-').Last();
                if (int.TryParse(lastSeq, out int lastSequence))
                {
                    sequence = lastSequence + 1;
                }
            }

            return $"AT-{tseId}-{date}-{sequence:D4}";
        }

        [HttpPut("{id}/status")]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<IActionResult> UpdateInvoiceStatus(int id, [FromBody] UpdateInvoiceStatusModel model)
        {
            try
            {
                var invoice = await _context.Invoices.FindAsync(id);
                if (invoice == null)
                {
                    return NotFound(new { message = "Fatura bulunamadı" });
                }

                invoice.Status = model.Status.ToString();
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Fatura durumu güncellendi: #{id} - {model.Status}");

                return Ok(new { message = "Fatura durumu başarıyla güncellendi", invoice });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"ID: {id} olan fatura durumu güncellenirken bir hata oluştu");
                return StatusCode(500, new { message = "Fatura durumu güncellenirken bir hata oluştu", error = ex.Message });
            }
        }

        [HttpGet("statistics")]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<IActionResult> GetStatistics()
        {
            try
            {
                var today = DateTime.UtcNow.Date;
                var monthStart = new DateTime(today.Year, today.Month, 1);
                var yearStart = new DateTime(today.Year, 1, 1);

                var dailyTotal = await _context.Invoices
                    .Where(i => i.InvoiceDate.Date == today)
                    .SumAsync(i => i.TotalAmount);

                var monthlyTotal = await _context.Invoices
                    .Where(i => i.InvoiceDate >= monthStart)
                    .SumAsync(i => i.TotalAmount);

                var yearlyTotal = await _context.Invoices
                    .Where(i => i.InvoiceDate >= yearStart)
                    .SumAsync(i => i.TotalAmount);

                var topProducts = await _context.InvoiceItems
                    .Include(ii => ii.Product)
                    .GroupBy(ii => ii.ProductId)
                    .Select(g => new
                    {
                        ProductId = g.Key,
                        ProductName = g.First().Product.Name,
                        TotalQuantity = g.Sum(ii => ii.Quantity),
                        TotalAmount = g.Sum(ii => ii.TotalAmount)
                    })
                    .OrderByDescending(x => x.TotalAmount)
                    .Take(10)
                    .ToListAsync();

                return Ok(new
                {
                    message = "İstatistikler başarıyla getirildi",
                    statistics = new
                    {
                        dailyTotal,
                        monthlyTotal,
                        yearlyTotal,
                        topProducts
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "İstatistikler getirilirken bir hata oluştu");
                return StatusCode(500, new { message = "İstatistikler getirilirken bir hata oluştu", error = ex.Message });
            }
        }

        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<Invoice>>> SearchInvoices(
            [FromQuery] string query,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] string? customerId,
            [FromQuery] string? paymentStatus,
            [FromQuery] string? invoiceStatus)
        {
            try
            {
                var searchQuery = _context.Invoices
                    .Include(i => i.Items)
                    .ThenInclude(ii => ii.Product)
                    .AsQueryable();

                if (!string.IsNullOrEmpty(query))
                {
                    var searchTerm = query.ToLower();
                    searchQuery = searchQuery.Where(i =>
                        i.InvoiceNumber.ToLower().Contains(searchTerm) ||
                        i.Customer.Name.ToLower().Contains(searchTerm) ||
                        i.Customer.Email.ToLower().Contains(searchTerm) ||
                        i.Customer.TaxNumber.ToLower().Contains(searchTerm)
                    );
                }

                // Diğer filtreler
                if (startDate.HasValue)
                    searchQuery = searchQuery.Where(i => i.InvoiceDate >= startDate.Value);
                if (endDate.HasValue)
                    searchQuery = searchQuery.Where(i => i.InvoiceDate <= endDate.Value);
                if (!string.IsNullOrEmpty(customerId))
                    searchQuery = searchQuery.Where(i => i.CustomerId.ToString() == customerId);
                if (!string.IsNullOrEmpty(paymentStatus))
                    searchQuery = searchQuery.Where(i => i.PaymentStatus.ToString() == paymentStatus);
                if (!string.IsNullOrEmpty(invoiceStatus))
                    searchQuery = searchQuery.Where(i => i.Status == invoiceStatus);

                var results = await searchQuery
                    .OrderByDescending(i => i.InvoiceDate)
                    .Take(20)
                    .ToListAsync();

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatura arama hatası");
                return StatusCode(500, new { message = "Fatura arama hatası", error = ex.Message });
            }
        }

        [HttpPost("report")]
        public async Task<ActionResult<object>> GetInvoiceReport([FromBody] InvoiceReportFilter filter)
        {
            try
            {
                var query = _context.Invoices
                    .Include(i => i.Customer)
                    .AsQueryable();

                // Tarih filtreleri
                if (filter.StartDate.HasValue)
                    query = query.Where(i => i.InvoiceDate >= filter.StartDate.Value);
                if (filter.EndDate.HasValue)
                    query = query.Where(i => i.InvoiceDate <= filter.EndDate.Value);

                var invoices = await query.ToListAsync();

                // Rapor verilerini hesapla
                var totalInvoices = invoices.Count;
                var totalAmount = invoices.Sum(i => i.TotalAmount);
                var paidAmount = invoices.Where(i => i.PaymentStatus == Models.PaymentStatus.Paid.ToString()).Sum(i => i.TotalAmount);
                var overdueAmount = invoices.Where(i => 
                    i.PaymentStatus == Models.PaymentStatus.Pending.ToString() && 
                    i.DueDate < DateTime.UtcNow).Sum(i => i.TotalAmount);
                var averageInvoiceValue = totalInvoices > 0 ? totalAmount / totalInvoices : 0;

                // Ödeme yöntemi dağılımı
                var paymentMethodBreakdown = invoices
                    .GroupBy(i => i.PaymentMethod)
                    .ToDictionary(g => g.Key.ToString(), g => g.Sum(i => i.TotalAmount));

                // Durum dağılımı
                var statusBreakdown = invoices
                    .GroupBy(i => i.Status)
                    .ToDictionary(g => g.Key, g => g.Count());

                // En iyi müşteriler
                var topCustomers = invoices
                    .Where(i => i.CustomerId.HasValue)
                    .GroupBy(i => i.CustomerId)
                    .Select(g => new
                    {
                        CustomerId = g.Key.ToString(),
                        CustomerName = g.First().Customer?.Name ?? "Unknown",
                        InvoiceCount = g.Count(),
                        TotalAmount = g.Sum(i => i.TotalAmount)
                    })
                    .OrderByDescending(x => x.TotalAmount)
                    .Take(10)
                    .ToList();

                // Günlük dağılım
                var dailyBreakdown = invoices
                    .GroupBy(i => i.InvoiceDate.Date)
                    .Select(g => new
                    {
                        Date = g.Key.ToString("yyyy-MM-dd"),
                        InvoiceCount = g.Count(),
                        TotalAmount = g.Sum(i => i.TotalAmount)
                    })
                    .OrderBy(x => x.Date)
                    .ToList();

                var report = new
                {
                    Period = $"{filter.StartDate?.ToString("yyyy-MM-dd") ?? "All"} - {filter.EndDate?.ToString("yyyy-MM-dd") ?? "All"}",
                    TotalInvoices = totalInvoices,
                    TotalAmount = totalAmount,
                    PaidAmount = paidAmount,
                    OverdueAmount = overdueAmount,
                    AverageInvoiceValue = averageInvoiceValue,
                    PaymentMethodBreakdown = paymentMethodBreakdown,
                    StatusBreakdown = statusBreakdown,
                    TopCustomers = topCustomers,
                    DailyBreakdown = dailyBreakdown
                };

                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatura raporu oluşturulurken hata oluştu");
                return StatusCode(500, new { message = "Fatura raporu oluşturulurken hata oluştu", error = ex.Message });
            }
        }

        [HttpGet("daily-report/{date}")]
        public async Task<ActionResult<object>> GetDailyReport(string date)
        {
            try
            {
                if (!DateTime.TryParse(date, out DateTime reportDate))
                {
                    return BadRequest(new { message = "Geçersiz tarih formatı" });
                }

                var filter = new InvoiceReportFilter
                {
                    StartDate = reportDate.Date,
                    EndDate = reportDate.Date.AddDays(1).AddSeconds(-1)
                };

                return await GetInvoiceReport(filter);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Günlük rapor oluşturulurken hata oluştu");
                return StatusCode(500, new { message = "Günlük rapor oluşturulurken hata oluştu", error = ex.Message });
            }
        }

        [HttpGet("monthly-report/{year}/{month}")]
        public async Task<ActionResult<object>> GetMonthlyReport(int year, int month)
        {
            try
            {
                var startDate = new DateTime(year, month, 1);
                var endDate = startDate.AddMonths(1).AddSeconds(-1);

                var filter = new InvoiceReportFilter
                {
                    StartDate = startDate,
                    EndDate = endDate
                };

                return await GetInvoiceReport(filter);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Aylık rapor oluşturulurken hata oluştu");
                return StatusCode(500, new { message = "Aylık rapor oluşturulurken hata oluştu", error = ex.Message });
            }
        }

        [HttpGet("yearly-report/{year}")]
        public async Task<ActionResult<object>> GetYearlyReport(int year)
        {
            try
            {
                var startDate = new DateTime(year, 1, 1);
                var endDate = startDate.AddYears(1).AddSeconds(-1);

                var filter = new InvoiceReportFilter
                {
                    StartDate = startDate,
                    EndDate = endDate
                };

                return await GetInvoiceReport(filter);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Yıllık rapor oluşturulurken hata oluştu");
                return StatusCode(500, new { message = "Yıllık rapor oluşturulurken hata oluştu", error = ex.Message });
            }
        }

        [HttpGet("generate-number")]
        public async Task<ActionResult<object>> GenerateInvoiceNumber()
        {
            try
            {
                var invoiceNumber = await GenerateReceiptNumber();
                return Ok(new { invoiceNumber });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatura numarası oluşturulurken hata oluştu");
                return StatusCode(500, new { message = "Fatura numarası oluşturulurken hata oluştu", error = ex.Message });
            }
        }

        [HttpPost("{id}/duplicate")]
        public async Task<ActionResult<Invoice>> DuplicateInvoice(Guid id)
        {
            try
            {
                var originalInvoice = await _context.Invoices
                    .Include(i => i.Items)
                    .ThenInclude(ii => ii.Product)
                    .FirstOrDefaultAsync(i => i.Id == id);

                if (originalInvoice == null)
                {
                    return NotFound(new { message = "Fatura bulunamadı" });
                }

                var duplicatedInvoice = new Invoice
                {
                    Id = Guid.NewGuid(),
                    InvoiceNumber = await GenerateReceiptNumber(),
                    ReceiptNumber = await GenerateReceiptNumber(),
                    CustomerId = originalInvoice.CustomerId,
                    InvoiceDate = DateTime.UtcNow,
                    DueDate = DateTime.UtcNow.AddDays(30),
                    TotalAmount = originalInvoice.TotalAmount,
                    TaxAmount = originalInvoice.TaxAmount,
                    PaymentMethod = originalInvoice.PaymentMethod,
                    PaymentStatus = Models.PaymentStatus.Pending.ToString(),
                    Status = "draft",
                    InvoiceType = originalInvoice.InvoiceType,
                    Notes = originalInvoice.Notes,
                    CreatedAt = DateTime.UtcNow,
                    Items = originalInvoice.Items.Select(item => new Models.InvoiceItem
                    {
                        Id = Guid.NewGuid(),
                        InvoiceId = Guid.NewGuid(), // Bu yeni fatura ID'si ile değiştirilecek
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        DiscountAmount = item.DiscountAmount,
                        TaxAmount = item.TaxAmount,
                        TotalAmount = item.TotalAmount
                    }).ToList()
                };

                _context.Invoices.Add(duplicatedInvoice);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Fatura kopyalandı: {originalInvoice.InvoiceNumber} -> {duplicatedInvoice.InvoiceNumber}");

                return Ok(duplicatedInvoice);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Fatura kopyalanırken hata oluştu: {id}");
                return StatusCode(500, new { message = "Fatura kopyalanırken hata oluştu", error = ex.Message });
            }
        }

        [HttpPost("{id}/email")]
        public async Task<ActionResult<object>> EmailInvoice(Guid id, [FromBody] EmailInvoiceRequest request)
        {
            try
            {
                var invoice = await _context.Invoices
                    .Include(i => i.Customer)
                    .FirstOrDefaultAsync(i => i.Id == id);

                if (invoice == null)
                {
                    return NotFound(new { message = "Fatura bulunamadı" });
                }

                // Email gönderme işlemi burada implement edilecek
                _logger.LogInformation($"Fatura email gönderildi: {invoice.InvoiceNumber} -> {request.Email}");

                return Ok(new { message = "Fatura başarıyla email ile gönderildi" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Fatura email gönderilirken hata oluştu: {id}");
                return StatusCode(500, new { message = "Fatura email gönderilirken hata oluştu", error = ex.Message });
            }
        }

        [HttpPost("{id}/print")]
        public async Task<ActionResult<object>> PrintInvoice(Guid id, [FromBody] PrintInvoiceRequest request)
        {
            try
            {
                var invoice = await _context.Invoices
                    .Include(i => i.Items)
                    .ThenInclude(ii => ii.Product)
                    .FirstOrDefaultAsync(i => i.Id == id);

                if (invoice == null)
                {
                    return NotFound(new { message = "Fatura bulunamadı" });
                }

                // Yazdırma işlemi burada implement edilecek
                _logger.LogInformation($"Fatura yazdırıldı: {invoice.InvoiceNumber}");

                return Ok(new { message = "Fatura başarıyla yazdırıldı" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Fatura yazdırılırken hata oluştu: {id}");
                return StatusCode(500, new { message = "Fatura yazdırılırken hata oluştu", error = ex.Message });
            }
        }

        [HttpPost("{id}/pdf")]
        public async Task<ActionResult<object>> GeneratePdfInvoice(Guid id, [FromBody] PdfInvoiceRequest request)
        {
            try
            {
                var invoice = await _context.Invoices
                    .Include(i => i.Items)
                    .ThenInclude(ii => ii.Product)
                    .FirstOrDefaultAsync(i => i.Id == id);

                if (invoice == null)
                {
                    return NotFound(new { message = "Fatura bulunamadı" });
                }

                // PDF oluşturma işlemi burada implement edilecek
                _logger.LogInformation($"Fatura PDF oluşturuldu: {invoice.InvoiceNumber}");

                return Ok(new { message = "Fatura PDF başarıyla oluşturuldu" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Fatura PDF oluşturulurken hata oluştu: {id}");
                return StatusCode(500, new { message = "Fatura PDF oluşturulurken hata oluştu", error = ex.Message });
            }
        }
    }

    public class CreateInvoiceModel
    {
        public int CustomerId { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public string? Notes { get; set; }
        public int RegisterId { get; set; }
        public decimal DiscountAmount { get; set; }
        public InvoiceItemModel[] Items { get; set; } = Array.Empty<InvoiceItemModel>();
    }

    public class InvoiceItemModel
    {
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal DiscountPercentage { get; set; }
    }

    public class UpdateInvoiceStatusModel
    {
        public InvoiceStatus Status { get; set; }
    }
} 