using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Registrierkasse_API.Controllers
{
    /// <summary>
    /// Demo/Test ortamı için özel API - Gerçek veri üretmez, test akışlarını simüle eder
    /// </summary>
    [ApiController]
    [Route("api/demo-test")]
    public class DemoTestController : ControllerBase
    {
        /// <summary>
        /// Demo POS cihazı aktivasyonu (test imzası ve anahtar ile)
        /// </summary>
        [HttpPost("activate-pos")]
        public IActionResult ActivatePos([FromBody] DemoPosActivationRequest req)
        {
            // Türkçe açıklama: Demo modda cihaz aktivasyonu simüle edilir
            return Ok(new
            {
                success = true,
                message = "Demo POS cihazı başarıyla aktive edildi.",
                deviceSerial = req.DeviceSerial,
                secureKey = req.SecureKey,
                testSignature = "TEST_SIGNATURE_123456"
            });
        }

        /// <summary>
        /// Demo satış işlemi (test fişi ve imza ile)
        /// </summary>
        [HttpPost("sale")]
        public IActionResult DemoSale([FromBody] DemoSaleRequest req)
        {
            // Türkçe açıklama: Demo modda test satış ve fiş üretimi simüle edilir
            var now = DateTime.UtcNow;
            return Ok(new
            {
                receiptNumber = $"DEMO-{now:yyyyMMddHHmmss}",
                kassenId = "DEMO-KASSE-001",
                belegDatum = now.ToString("dd.MM.yyyy"),
                uhrzeit = now.ToString("HH:mm:ss"),
                tseSignature = "DEMO_SIGNATURE_TEST",
                taxDetails = new { standard = 20, reduced = 10, special = 13 },
                qrCodeData = "DEMO|QR|DATA|TEST"
            });
        }

        /// <summary>
        /// Demo DEP kaydı oluşturur
        /// </summary>
        [HttpPost("dep-entry")]
        public IActionResult DemoDepEntry([FromBody] object entry)
        {
            // Türkçe açıklama: Demo modda DEP kaydı simüle edilir
            return Ok(new { success = true, depId = Guid.NewGuid().ToString(), message = "Demo DEP kaydı oluşturuldu." });
        }

        /// <summary>
        /// Demo yılsonu veya günlük export (BMF formatında test dosyası)
        /// </summary>
        [HttpGet("export/{type}")]
        public IActionResult DemoExport(string type)
        {
            // Türkçe açıklama: Demo modda export işlemi simüle edilir
            var content = $"DEMO_{type.ToUpper()}_EXPORT_FILE";
            var bytes = System.Text.Encoding.UTF8.GetBytes(content);
            return File(bytes, "text/plain", $"demo_{type}_export.txt");
        }
    }

    // Demo API için örnek request modelleri
    public class DemoPosActivationRequest
    {
        public string DeviceSerial { get; set; }
        public string SecureKey { get; set; }
    }
    public class DemoSaleRequest
    {
        public decimal Total { get; set; }
        public string PaymentMethod { get; set; }
    }
} 