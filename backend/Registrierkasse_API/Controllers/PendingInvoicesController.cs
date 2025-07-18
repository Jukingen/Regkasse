using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Registrierkasse_API.Services;
using System.Threading.Tasks;

namespace Registrierkasse_API.Controllers
{
    [Authorize(Roles = "Administrator,Manager")]
    [Route("api/[controller]")]
    [ApiController]
    public class PendingInvoicesController : ControllerBase
    {
        private readonly IPendingInvoicesService _pendingService;
        private readonly ILogger<PendingInvoicesController> _logger;

        public PendingInvoicesController(
            IPendingInvoicesService pendingService,
            ILogger<PendingInvoicesController> logger)
        {
            _pendingService = pendingService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetPendingInvoices()
        {
            try
            {
                var pendingInvoices = await _pendingService.GetPendingInvoicesAsync();
                var pendingCount = await _pendingService.GetPendingCountAsync();

                var response = new
                {
                    pendingCount = pendingCount,
                    invoices = pendingInvoices.Select(i => new
                    {
                        id = i.Id,
                        invoiceNumber = i.InvoiceNumber,
                        invoiceDate = i.InvoiceDate,
                        totalAmount = i.TotalAmount,
                        customerName = i.CustomerName,
                        status = i.Status.ToString()
                    })
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bekleyen faturalar alma hatası");
                return StatusCode(500, new { error = "Bekleyen faturalar alınamadı" });
            }
        }

        [HttpPost("submit")]
        public async Task<IActionResult> SubmitPendingInvoices()
        {
            try
            {
                var success = await _pendingService.SubmitPendingInvoicesAsync();
                
                if (success)
                {
                    return Ok(new { message = "Bekleyen faturalar başarıyla gönderildi" });
                }
                else
                {
                    return BadRequest(new { error = "Bazı faturalar gönderilemedi" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bekleyen faturalar gönderimi hatası");
                return StatusCode(500, new { error = "Bekleyen faturalar gönderilemedi" });
            }
        }

        [HttpPost("{id}/retry")]
        public async Task<IActionResult> RetryInvoice(Guid id)
        {
            try
            {
                var success = await _pendingService.RetryFailedSubmissionAsync(id);
                
                if (success)
                {
                    return Ok(new { message = "Fatura yeniden gönderimi başarılı" });
                }
                else
                {
                    return BadRequest(new { error = "Fatura yeniden gönderimi başarısız" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatura yeniden gönderimi hatası: {InvoiceId}", id);
                return StatusCode(500, new { error = "Fatura yeniden gönderilemedi" });
            }
        }

        [HttpGet("count")]
        public async Task<IActionResult> GetPendingCount()
        {
            try
            {
                var count = await _pendingService.GetPendingCountAsync();
                return Ok(new { pendingCount = count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bekleyen fatura sayısı alma hatası");
                return StatusCode(500, new { error = "Bekleyen fatura sayısı alınamadı" });
            }
        }

        [HttpPost("clear-old")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> ClearOldPendingInvoices([FromQuery] int daysOld = 30)
        {
            try
            {
                var success = await _pendingService.ClearOldPendingInvoicesAsync(daysOld);
                
                if (success)
                {
                    return Ok(new { message = $"Eski bekleyen faturalar temizlendi ({daysOld} günden eski)" });
                }
                else
                {
                    return BadRequest(new { error = "Eski faturalar temizlenemedi" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Eski bekleyen faturalar temizleme hatası");
                return StatusCode(500, new { error = "Eski faturalar temizlenemedi" });
            }
        }
    }
} 