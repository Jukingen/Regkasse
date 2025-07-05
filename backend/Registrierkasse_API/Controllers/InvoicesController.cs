using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Registrierkasse.Data;
using Microsoft.EntityFrameworkCore;

namespace Registrierkasse.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class InvoicesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public InvoicesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetInvoices()
        {
            try
            {
                var invoices = await _context.Invoices
                    .Include(i => i.Customer)
                    .Select(i => new
                    {
                        id = i.Id,
                        invoiceNumber = i.InvoiceNumber,
                        receiptNumber = i.ReceiptNumber,
                        tseSignature = i.TseSignature,
                        isPrinted = i.IsPrinted,
                        invoiceDate = i.InvoiceDate,
                        totalAmount = i.TotalAmount,
                        taxAmount = i.TaxAmount,
                        paymentMethod = i.PaymentMethod.ToString(),
                        paymentStatus = i.PaymentStatus,
                        status = i.Status,
                        customer = i.Customer != null ? new
                        {
                            firstName = i.Customer.FirstName,
                            lastName = i.Customer.LastName,
                            email = i.Customer.Email
                        } : null
                    })
                    .ToListAsync();

                return Ok(invoices);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve invoices", details = ex.Message });
            }
        }
    }
} 