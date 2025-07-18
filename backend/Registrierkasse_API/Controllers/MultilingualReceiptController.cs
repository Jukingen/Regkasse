using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Registrierkasse_API.Services;
using Registrierkasse_API.Models;

namespace Registrierkasse_API.Controllers
{
    /// <summary>
    /// Çok dilli fiş controller'ı - Avusturya yasal zorunluluklarına uygun
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MultilingualReceiptController : ControllerBase
    {
        private readonly MultilingualReceiptService _receiptService;
        private readonly LocalizationService _localizationService;
        private readonly ILogger<MultilingualReceiptController> _logger;

        public MultilingualReceiptController(
            MultilingualReceiptService receiptService,
            LocalizationService localizationService,
            ILogger<MultilingualReceiptController> logger)
        {
            _receiptService = receiptService;
            _localizationService = localizationService;
            _logger = logger;
        }

        /// <summary>
        /// Belirli bir dilde fiş oluşturur
        /// </summary>
        [HttpPost("generate/{language}")]
        public async Task<IActionResult> GenerateReceipt([FromBody] Models.Receipt receipt, string language = "de-DE")
        {
            try
            {
                // Dil kontrolü
                if (!_localizationService.IsValidLanguage(language))
                {
                    _logger.LogWarning($"Invalid language requested: {language}, using default");
                    language = _localizationService.GetDefaultLanguage();
                }

                // Çok dilli fiş oluştur
                var receiptContent = _receiptService.GenerateMultilingualReceipt(receipt, language);

                _logger.LogInformation($"Receipt generated for {receipt.ReceiptNumber} in {language}");

                return Ok(new
                {
                    receipt_number = receipt.ReceiptNumber,
                    language = language,
                    content = receiptContent,
                    generated_at = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating receipt for {receipt.ReceiptNumber} in {language}");
                return StatusCode(500, new { error = "Receipt generation failed" });
            }
        }

        /// <summary>
        /// Tüm dillerde fiş oluşturur (denetim için)
        /// </summary>
        [HttpPost("generate-all")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GenerateAllLanguageReceipts([FromBody] Models.Receipt receipt)
        {
            try
            {
                // Tüm dillerde fiş oluştur
                var allReceipts = _receiptService.GenerateAllLanguageReceipts(receipt);

                _logger.LogInformation($"All language receipts generated for {receipt.ReceiptNumber}");

                return Ok(new
                {
                    receipt_number = receipt.ReceiptNumber,
                    receipts = allReceipts,
                    generated_at = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating all language receipts for {receipt.ReceiptNumber}");
                return StatusCode(500, new { error = "Multi-language receipt generation failed" });
            }
        }

        /// <summary>
        /// PDF formatında fiş oluşturur
        /// </summary>
        [HttpPost("generate-pdf/{language}")]
        public async Task<IActionResult> GeneratePdfReceipt([FromBody] Models.Receipt receipt, string language = "de-DE")
        {
            try
            {
                // Dil kontrolü
                if (!_localizationService.IsValidLanguage(language))
                {
                    _logger.LogWarning($"Invalid language requested: {language}, using default");
                    language = _localizationService.GetDefaultLanguage();
                }

                // PDF oluştur
                var pdfBytes = _receiptService.GenerateMultilingualPdf(receipt, language);

                _logger.LogInformation($"PDF receipt generated for {receipt.ReceiptNumber} in {language}");

                return File(pdfBytes, "application/pdf", $"receipt_{receipt.ReceiptNumber}_{language}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating PDF receipt for {receipt.ReceiptNumber} in {language}");
                return StatusCode(500, new { error = "PDF generation failed" });
            }
        }

        /// <summary>
        /// Desteklenen dilleri döndürür
        /// </summary>
        [HttpGet("languages")]
        public IActionResult GetSupportedLanguages()
        {
            try
            {
                var languages = _localizationService.GetSupportedLanguages();
                var defaultLanguage = _localizationService.GetDefaultLanguage();

                return Ok(new
                {
                    supported_languages = languages,
                    default_language = defaultLanguage,
                    language_names = new Dictionary<string, string>
                    {
                        ["de-DE"] = "Deutsch",
                        ["en"] = "English",
                        ["tr"] = "Türkçe"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting supported languages");
                return StatusCode(500, new { error = "Failed to get supported languages" });
            }
        }

        /// <summary>
        /// Fiş template'ini döndürür
        /// </summary>
        [HttpGet("template/{language}")]
        public IActionResult GetReceiptTemplate(string language = "de-DE")
        {
            try
            {
                // Dil kontrolü
                if (!_localizationService.IsValidLanguage(language))
                {
                    _logger.LogWarning($"Invalid language requested: {language}, using default");
                    language = _localizationService.GetDefaultLanguage();
                }

                // Örnek fiş oluştur
                var sampleReceipt = new Models.Receipt
                {
                    ReceiptNumber = "AT-DEMO-20241201-12345678",
                    ReceiptDate = DateTime.UtcNow,
                    TotalAmount = 120.00m,
                    TaxAmount = 20.00m,
                    Subtotal = 100.00m,
                    PaymentMethod = "cash",
                    TseSignature = "DEMO-TSE-SIGNATURE-123456789",
                    KassenId = "DEMO-KASSE-001"
                };

                // Template oluştur
                var templateContent = _receiptService.GenerateMultilingualReceipt(sampleReceipt, language);

                return Ok(new
                {
                    language = language,
                    template = templateContent,
                    sample_data = sampleReceipt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting receipt template for {language}");
                return StatusCode(500, new { error = "Failed to get receipt template" });
            }
        }
    }
} 