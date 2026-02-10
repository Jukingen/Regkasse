using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using KasseAPI_Final.Services;
using KasseAPI_Final.DTOs;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ReceiptsController : ControllerBase
    {
        private readonly IReceiptService _receiptService;
        private readonly ILogger<ReceiptsController> _logger;

        public ReceiptsController(IReceiptService receiptService, ILogger<ReceiptsController> logger)
        {
            _receiptService = receiptService;
            _logger = logger;
        }

        // GET: api/receipts/{receiptId}
        [HttpGet("{receiptId}")]
        public async Task<ActionResult<ReceiptDTO>> GetReceipt(Guid receiptId)
        {
            try
            {
                var receipt = await _receiptService.GetReceiptAsync(receiptId);
                if (receipt == null)
                {
                    return NotFound(new { message = "Receipt not found" });
                }
                return Ok(receipt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving receipt {ReceiptId}", receiptId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // POST: api/receipts/create-from-payment/{paymentId}
        // Useful if the frontend has a payment ID but implementation details differ
        [HttpPost("create-from-payment/{paymentId}")]
        public async Task<ActionResult<ReceiptDTO>> CreateFromPayment(Guid paymentId)
        {
             try
            {
                var receipt = await _receiptService.CreateReceiptFromPaymentAsync(paymentId);
                return Ok(receipt);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "Payment not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating receipt from payment {PaymentId}", paymentId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
    }
}
