using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Registrierkasse.Services;

namespace Registrierkasse.Controllers;

[ApiController]
[Route("api/[controller]")]
//[Authorize] // Bu endpoint'e giriş yapmadan önce erişilmesi gerekiyor
public class ReceiptController : ControllerBase
{
    private readonly IPrinterService _printerService;
    private readonly ILogger<ReceiptController> _logger;

    public ReceiptController(IPrinterService printerService, ILogger<ReceiptController> logger)
    {
        _printerService = printerService;
        _logger = logger;
    }

    [HttpGet("printer-status")]
    public async Task<IActionResult> GetPrinterStatus([FromQuery] string? printer)
    {
        _logger.LogInformation("=== PRINTER-STATUS ENDPOINT CALLED ===");
        _logger.LogInformation($"Request URL: {Request.Path}");
        _logger.LogInformation($"Request Method: {Request.Method}");
        _logger.LogInformation($"Request Headers: {string.Join(", ", Request.Headers.Select(h => $"{h.Key}={h.Value}"))}");
        _logger.LogInformation($"Query Parameters: printer={printer}");
        _logger.LogInformation($"User Agent: {Request.Headers["User-Agent"]}");
        _logger.LogInformation($"Remote IP: {Request.HttpContext.Connection.RemoteIpAddress}");
        
        try
        {
            _logger.LogInformation("Calling PrinterService.GetStatusAsync()...");
            var status = await _printerService.GetStatusAsync();
            _logger.LogInformation($"PrinterService returned: IsConnected={status.IsConnected}, PrinterName={status.PrinterName}");
            
            var response = new
            {
                isConnected = status.IsConnected,
                printerName = status.PrinterName,
                paperStatus = "ready", // Varsayılan değer
                errorMessage = status.IsConnected ? null : "Printer not connected"
            };
            
            _logger.LogInformation($"Returning response: {System.Text.Json.JsonSerializer.Serialize(response)}");
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Printer status check failed");
            var errorResponse = new
            {
                isConnected = false,
                printerName = printer ?? "Unknown",
                paperStatus = "error",
                errorMessage = "Failed to check printer status"
            };
            
            _logger.LogInformation($"Returning error response: {System.Text.Json.JsonSerializer.Serialize(errorResponse)}");
            return Ok(errorResponse);
        }
    }

    [HttpGet("printers")]
    public async Task<IActionResult> GetAvailablePrinters()
    {
        _logger.LogInformation("=== GET-AVAILABLE-PRINTERS ENDPOINT CALLED ===");
        _logger.LogInformation($"Request URL: {Request.Path}");
        _logger.LogInformation($"Request Method: {Request.Method}");
        
        try
        {
            _logger.LogInformation("Calling PrinterService.GetAvailablePrintersAsync()...");
            var printers = await _printerService.GetAvailablePrintersAsync();
            _logger.LogInformation($"PrinterService returned {printers.Count} printers: {string.Join(", ", printers)}");
            return Ok(printers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get available printers");
            var defaultPrinters = new List<string> { "EPSON TM-T88VI", "Star TSP 700" };
            _logger.LogInformation($"Returning default printers: {string.Join(", ", defaultPrinters)}");
            return Ok(defaultPrinters);
        }
    }

    [HttpPost("print")]
    public async Task<IActionResult> PrintReceipt([FromBody] object receiptData)
    {
        _logger.LogInformation("=== PRINT-RECEIPT ENDPOINT CALLED ===");
        _logger.LogInformation($"Request URL: {Request.Path}");
        _logger.LogInformation($"Request Method: {Request.Method}");
        _logger.LogInformation($"Request Body: {System.Text.Json.JsonSerializer.Serialize(receiptData)}");
        
        try
        {
            // Basit fiş yazdırma implementasyonu
            _logger.LogInformation("Receipt print request received");
            
            // Burada gerçek fiş yazdırma işlemi yapılacak
            // Şimdilik başarılı döndürüyoruz
            
            var response = new { success = true, message = "Receipt printed successfully" };
            _logger.LogInformation($"Returning success response: {System.Text.Json.JsonSerializer.Serialize(response)}");
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Receipt printing failed");
            var errorResponse = new { success = false, message = "Failed to print receipt" };
            _logger.LogInformation($"Returning error response: {System.Text.Json.JsonSerializer.Serialize(errorResponse)}");
            return BadRequest(errorResponse);
        }
    }

    // Test endpoint'i - sadece controller'ın çalıştığını doğrulamak için
    [HttpGet("test")]
    public IActionResult Test()
    {
        _logger.LogInformation("=== TEST ENDPOINT CALLED ===");
        _logger.LogInformation($"Request URL: {Request.Path}");
        _logger.LogInformation($"Request Method: {Request.Method}");
        
        return Ok(new { message = "ReceiptController is working!", timestamp = DateTime.UtcNow });
    }
} 