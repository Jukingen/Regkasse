using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Tse;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReceiptsController : ControllerBase
    {
        private readonly IReceiptService _receiptService;
        private readonly ILogger<ReceiptsController> _logger;

        private readonly ITseVerificationService _tseVerification;

        public ReceiptsController(
            IReceiptService receiptService,
            ITseVerificationService tseVerification,
            ILogger<ReceiptsController> logger)
        {
            _receiptService = receiptService;
            _tseVerification = tseVerification;
            _logger = logger;
        }

        // GET: api/Receipts/list (read-only; requires SaleView)
        [HasPermission(AppPermissions.SaleView)]
        [HttpGet("list")]
        public async Task<ActionResult<PagedResult<ReceiptListItemDto>>> GetReceiptList(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            [FromQuery] string? sort = null,
            [FromQuery] string? receiptNumber = null,
            [FromQuery] string? cashRegisterId = null,
            [FromQuery] string? cashierId = null,
            [FromQuery] DateTime? issuedFrom = null,
            [FromQuery] DateTime? issuedTo = null)
        {
            try
            {
                var result = await _receiptService.GetReceiptListAsync(page, pageSize, sort, receiptNumber, cashRegisterId, cashierId, issuedFrom, issuedTo);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing receipts");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>Persisted fiscal receipt for a payment (canonical; no lazy generation).</summary>
        [HasPermission(AppPermissions.SaleView)]
        [HttpGet("by-payment/{paymentId}")]
        public async Task<ActionResult<ReceiptDTO>> GetReceiptByPaymentId(Guid paymentId)
        {
            try
            {
                var receipt = await _receiptService.GetReceiptByPaymentIdAsync(paymentId);
                if (receipt == null)
                    return NotFound(new { message = "No persisted receipt for this payment" });
                return Ok(receipt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving receipt for payment {PaymentId}", paymentId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/receipts/{receiptId} (read-only; requires SaleView)
        [HasPermission(AppPermissions.SaleView)]
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

        // POST: api/receipts/create-from-payment/{paymentId} (write; requires SaleCreate)
        [HasPermission(AppPermissions.SaleCreate)]
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
        /// GET: api/receipts/{receiptId}/signature-debug (read-only; requires SaleView)
        /// Integration test: payment create → receipt fetch → signature-debug → Verify PASS
        /// </summary>
        [HasPermission(AppPermissions.SaleView)]
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

                var verification = await _tseVerification.VerifySignatureAsync(sig.SignatureValue);
                return Ok(new
                {
                    receiptId,
                    algorithm = sig.Algorithm,
                    serialNumber = sig.SerialNumber,
                    timestamp = sig.Timestamp,
                    prevSignatureValue = sig.PrevSignatureValue,
                    signatureValue = sig.SignatureValue,
                    verifyResult = verification.ToVerifyResultCode(),
                    isSimulated = verification.IsSimulated,
                    verificationMessage = verification.Message
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
