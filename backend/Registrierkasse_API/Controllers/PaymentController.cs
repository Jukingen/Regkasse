using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Registrierkasse_API.Models;
using Registrierkasse_API.Services;
using System.ComponentModel.DataAnnotations;

namespace Registrierkasse_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PaymentController : ControllerBase
    {
        private readonly ILogger<PaymentController> _logger;
        private readonly IInvoiceService _invoiceService;

        public PaymentController(
            ILogger<PaymentController> logger,
            IInvoiceService invoiceService)
        {
            _logger = logger;
            _invoiceService = invoiceService;
        }

        /// <summary>
        /// Ödeme yöntemlerini getir
        /// </summary>
        [HttpGet("methods")]
        public ActionResult<List<PaymentMethodInfo>> GetPaymentMethods()
        {
            var methods = new List<PaymentMethodInfo>
            {
                new PaymentMethodInfo { Id = "cash", Name = "Nakit", Type = "cash", Icon = "cash" },
                new PaymentMethodInfo { Id = "card", Name = "Kart", Type = "card", Icon = "card" },
                new PaymentMethodInfo { Id = "voucher", Name = "Kupon", Type = "voucher", Icon = "voucher" },
                new PaymentMethodInfo { Id = "transfer", Name = "Havale", Type = "transfer", Icon = "transfer" }
            };

            return Ok(methods);
        }

        /// <summary>
        /// Ödeme işlemi
        /// </summary>
        [HttpPost("process")]
        [Authorize(Roles = "Cashier,Manager,Admin")]
        public async Task<ActionResult<PaymentResponse>> ProcessPayment([FromBody] PaymentRequest request)
        {
            try
            {
                _logger.LogInformation("Processing payment for {ItemCount} items", request.Items.Count);

                // Ödeme işlemi simülasyonu
                var paymentId = Guid.NewGuid().ToString();
                
                var response = new PaymentResponse
                {
                    Success = true,
                    PaymentId = paymentId,
                    Message = "Payment processed successfully",
                    Receipt = new PaymentReceipt
                    {
                        Id = paymentId,
                        ReceiptNumber = $"AT-{DateTime.UtcNow:yyyyMMdd}-{paymentId.Substring(0, 8)}",
                        TotalAmount = request.Items.Sum(item => item.TotalAmount),
                        PaymentMethod = request.Payment.Method,
                        Items = request.Items,
                        CreatedAt = DateTime.UtcNow
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Payment processing failed");
                return BadRequest(new PaymentResponse
                {
                    Success = false,
                    Message = "Payment processing failed",
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Ödeme geçmişi
        /// </summary>
        [HttpGet("history")]
        public async Task<ActionResult<List<PaymentResponse>>> GetPaymentHistory(
            [FromQuery] int limit = 50, 
            [FromQuery] int offset = 0)
        {
            try
            {
                // Ödeme geçmişi simülasyonu
                var history = new List<PaymentResponse>();
                
                // Burada gerçek ödeme geçmişi veritabanından çekilecek
                // Şimdilik boş liste döndürüyoruz
                
                return Ok(history);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get payment history");
                return BadRequest(new { error = "Failed to get payment history", details = ex.Message });
            }
        }
    }

    public class PaymentMethodInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
    }

    public class PaymentRequest
    {
        public List<PaymentItem> Items { get; set; } = new();
        public PaymentInfo Payment { get; set; } = new();
        public string? CustomerId { get; set; }
    }

    public class PaymentItem
    {
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalAmount { get; set; }
        public string TaxType { get; set; } = "standard";
    }

    public class PaymentInfo
    {
        public string Method { get; set; } = "cash";
        public decimal Amount { get; set; }
        public bool TseRequired { get; set; } = true;
    }

    public class PaymentResponse
    {
        public bool Success { get; set; }
        public string PaymentId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Error { get; set; }
        public PaymentReceipt? Receipt { get; set; }
    }

    public class PaymentReceipt
    {
        public string Id { get; set; } = string.Empty;
        public string ReceiptNumber { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public List<PaymentItem> Items { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }
} 