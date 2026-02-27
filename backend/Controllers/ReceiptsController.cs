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

        private readonly ITseService _tseService;

        public ReceiptsController(IReceiptService receiptService, ITseService tseService, ILogger<ReceiptsController> logger)
        {
            _receiptService = receiptService;
            _tseService = tseService;
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

        /// <summary>
        /// GET: api/receipts/{receiptId}/signature-debug
        /// Integration test: payment create → receipt fetch → signature-debug → Verify PASS
        /// </summary>
        [HttpGet("{receiptId}/signature-debug")]
        public async Task<ActionResult<object>> GetSignatureDebug(Guid receiptId)
        {
            try
            {
                var receipt = await _receiptService.GetReceiptAsync(receiptId);
                if (receipt == null)
                    return NotFound(new { message = "Receipt not found" });

                var sig = receipt.Signature;
                if (sig == null || string.IsNullOrEmpty(sig.SignatureValue))
                    return Ok(new { receiptId, signatureValue = (string?)null, prevSignatureValue = (string?)null, verifyResult = "SKIP", message = "No TSE signature" });

                var valid = await _tseService.ValidateTseSignatureAsync(sig.SignatureValue);
                return Ok(new
                {
                    receiptId,
                    algorithm = sig.Algorithm,
                    serialNumber = sig.SerialNumber,
                    timestamp = sig.Timestamp,
                    prevSignatureValue = sig.PrevSignatureValue,
                    signatureValue = sig.SignatureValue,
                    verifyResult = valid ? "PASS" : "FAIL"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in signature-debug for {ReceiptId}", receiptId);
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}
