using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(AppDbContext context, ILogger<PaymentController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/payment
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PaymentDetails>>> GetPayments()
        {
            try
            {
                var payments = await _context.PaymentDetails
                    .Include(p => p.Invoice)
                    .Include(p => p.Customer)
                    .OrderByDescending(p => p.PaymentDate)
                    .ToListAsync();

                return Ok(payments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payments");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/payment/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<PaymentDetails>> GetPayment(Guid id)
        {
            try
            {
                var payment = await _context.PaymentDetails
                    .Include(p => p.Invoice)
                    .Include(p => p.Customer)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (payment == null)
                {
                    return NotFound(new { message = "Payment not found" });
                }

                return Ok(payment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payment {PaymentId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // POST: api/payment
        [HttpPost]
        public async Task<ActionResult<PaymentDetails>> CreatePayment([FromBody] CreatePaymentRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Invoice'ı kontrol et
                var invoice = await _context.Invoices.FindAsync(request.InvoiceId);
                if (invoice == null)
                {
                    return NotFound(new { message = "Invoice not found" });
                }

                // Ödeme tutarı kontrol et
                if (request.Amount <= 0)
                {
                    return BadRequest(new { message = "Payment amount must be greater than zero" });
                }

                // Ödeme yöntemi kontrol et
                if (!Enum.IsDefined(typeof(PaymentMethod), request.PaymentMethod))
                {
                    return BadRequest(new { message = "Invalid payment method" });
                }

                var payment = new PaymentDetails
                {
                    InvoiceId = request.InvoiceId,
                    CustomerId = request.CustomerId,
                    Amount = request.Amount,
                    PaymentMethod = request.PaymentMethod,
                    PaymentDate = DateTime.UtcNow,
                    Status = PaymentStatus.Completed,
                    Reference = request.Reference,
                    Notes = request.Notes,
                    TransactionId = request.TransactionId
                };

                _context.PaymentDetails.Add(payment);

                // Invoice'ın ödenen tutarını güncelle
                invoice.PaidAmount += request.Amount;
                invoice.RemainingAmount = invoice.TotalAmount - invoice.PaidAmount;
                invoice.Status = invoice.RemainingAmount <= 0 ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;
                invoice.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Oluşturulan payment'ı döndür
                var createdPayment = await _context.PaymentDetails
                    .Include(p => p.Invoice)
                    .FirstOrDefaultAsync(p => p.Id == payment.Id);

                return CreatedAtAction(nameof(GetPayment), new { id = createdPayment.Id }, createdPayment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating payment");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // PUT: api/payment/{id}/status
        [HttpPut("{id}/status")]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<IActionResult> UpdatePaymentStatus(Guid id, [FromBody] UpdatePaymentStatusRequest request)
        {
            try
            {
                var payment = await _context.PaymentDetails.FindAsync(id);
                if (payment == null)
                {
                    return NotFound(new { message = "Payment not found" });
                }

                payment.Status = request.Status;
                payment.UpdatedAt = DateTime.UtcNow;

                // Eğer ödeme iptal edildiyse, invoice'ı güncelle
                if (request.Status == PaymentStatus.Cancelled)
                {
                    var invoice = await _context.Invoices.FindAsync(payment.InvoiceId);
                    if (invoice != null)
                    {
                        invoice.PaidAmount -= payment.Amount;
                        invoice.RemainingAmount = invoice.TotalAmount - invoice.PaidAmount;
                        invoice.Status = invoice.RemainingAmount <= 0 ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;
                        invoice.UpdatedAt = DateTime.UtcNow;
                    }
                }

                await _context.SaveChangesAsync();

                return Ok(new { message = "Payment status updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating payment status {PaymentId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // DELETE: api/payment/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> DeletePayment(Guid id)
        {
            try
            {
                var payment = await _context.PaymentDetails.FindAsync(id);
                if (payment == null)
                {
                    return NotFound(new { message = "Payment not found" });
                }

                // Ödeme iptal edildiyse, invoice'ı güncelle
                if (payment.Status == PaymentStatus.Completed)
                {
                    var invoice = await _context.Invoices.FindAsync(payment.InvoiceId);
                    if (invoice != null)
                    {
                        invoice.PaidAmount -= payment.Amount;
                        invoice.RemainingAmount = invoice.TotalAmount - invoice.PaidAmount;
                        invoice.Status = invoice.RemainingAmount <= 0 ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;
                        invoice.UpdatedAt = DateTime.UtcNow;
                    }
                }

                _context.PaymentDetails.Remove(payment);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Payment deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting payment {PaymentId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/payment/invoice/{invoiceId}
        [HttpGet("invoice/{invoiceId}")]
        public async Task<ActionResult<IEnumerable<PaymentDetails>>> GetInvoicePayments(Guid invoiceId)
        {
            try
            {
                var payments = await _context.PaymentDetails
                    .Where(p => p.InvoiceId == invoiceId)
                    .OrderByDescending(p => p.PaymentDate)
                    .ToListAsync();

                return Ok(payments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting invoice payments {InvoiceId}", invoiceId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/payment/customer/{customerId}
        [HttpGet("customer/{customerId}")]
        public async Task<ActionResult<IEnumerable<PaymentDetails>>> GetCustomerPayments(Guid customerId)
        {
            try
            {
                var payments = await _context.PaymentDetails
                    .Where(p => p.CustomerId == customerId)
                    .Include(p => p.Invoice)
                    .OrderByDescending(p => p.PaymentDate)
                    .ToListAsync();

                return Ok(payments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting customer payments {CustomerId}", customerId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/payment/method/{method}
        [HttpGet("method/{method}")]
        public async Task<ActionResult<IEnumerable<PaymentDetails>>> GetPaymentsByMethod(PaymentMethod method)
        {
            try
            {
                var payments = await _context.PaymentDetails
                    .Where(p => p.PaymentMethod == method)
                    .Include(p => p.Invoice)
                    .OrderByDescending(p => p.PaymentDate)
                    .ToListAsync();

                return Ok(payments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payments by method {Method}", method);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
    }

    // DTOs
    public class CreatePaymentRequest
    {
        [Required]
        public Guid InvoiceId { get; set; }

        public Guid? CustomerId { get; set; }

        [Required]
        public decimal Amount { get; set; }

        [Required]
        public PaymentMethod PaymentMethod { get; set; }

        [MaxLength(100)]
        public string? Reference { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        [MaxLength(100)]
        public string? TransactionId { get; set; }
    }

    public class UpdatePaymentStatusRequest
    {
        [Required]
        public PaymentStatus Status { get; set; }
    }
}
