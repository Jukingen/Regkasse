using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasseAPI_Final.Services;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Controllers
{
    // English Description: Controller for managing printer operations and status
    // Türkçe Açıklama: Yazıcı işlemleri ve durumu yönetimi için controller
    
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class PrinterController : ControllerBase
    {
        private readonly IReceiptService _receiptService;
        private readonly ILogger<PrinterController> _logger;

        public PrinterController(IReceiptService receiptService, ILogger<PrinterController> logger)
        {
            _receiptService = receiptService;
            _logger = logger;
        }

        // GET: api/printer/status - Yazıcı durumunu kontrol et
        [HttpGet("status")]
        [Authorize(Roles = "Administrator,Manager,Cashier")]
        public async Task<ActionResult<PrinterStatusResponse>> GetPrinterStatus()
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Unknown";
                var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "Unknown";

                _logger.LogInformation("Printer status requested by user {UserId} with role {UserRole}", userId, userRole);

                // Get available printers
                var availablePrinters = _receiptService.GetAvailablePrinters();
                
                // Get default printer status
                var defaultPrinterStatus = await _receiptService.GetPrinterStatusAsync();
                
                // Test printer connection
                var connectionTest = await _receiptService.TestPrinterConnectionAsync();

                var response = new PrinterStatusResponse
                {
                    AvailablePrinters = availablePrinters,
                    DefaultPrinter = availablePrinters.FirstOrDefault() ?? "No printer available",
                    DefaultPrinterStatus = defaultPrinterStatus.ToString(),
                    ConnectionTest = connectionTest,
                    LastChecked = DateTime.UtcNow,
                    Message = connectionTest ? "Printer connection successful" : "Printer connection failed"
                };

                _logger.LogInformation("Printer status retrieved successfully for user {UserId}. Default printer: {Printer}, Status: {Status}, Connection: {Connection}", 
                    userId, response.DefaultPrinter, response.DefaultPrinterStatus, response.ConnectionTest);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting printer status");
                return StatusCode(500, new { message = "Error retrieving printer status", error = ex.Message });
            }
        }

        // GET: api/printer/printers - Mevcut yazıcıları listele
        [HttpGet("printers")]
        [Authorize(Roles = "Administrator,Manager,Cashier")]
        public ActionResult<PrinterListResponse> GetAvailablePrinters()
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Unknown";
                var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "Unknown";

                _logger.LogInformation("Available printers requested by user {UserId} with role {UserRole}", userId, userRole);

                var printers = _receiptService.GetAvailablePrinters();
                
                var response = new PrinterListResponse
                {
                    Printers = printers,
                    Count = printers.Count,
                    Message = $"Found {printers.Count} available printers"
                };

                _logger.LogInformation("Available printers retrieved successfully for user {UserId}. Count: {Count}", userId, printers.Count);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available printers");
                return StatusCode(500, new { message = "Error retrieving available printers", error = ex.Message });
            }
        }

        // POST: api/printer/test - Yazıcı bağlantısını test et
        [HttpPost("test")]
        [Authorize(Roles = "Administrator,Manager,Cashier")]
        public async Task<ActionResult<PrinterTestResponse>> TestPrinterConnection([FromBody] PrinterTestRequest request)
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Unknown";
                var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "Unknown";

                _logger.LogInformation("Printer connection test requested by user {UserId} with role {UserRole} for printer: {PrinterName}", 
                    userId, userRole, request.PrinterName ?? "Default");

                // Test printer connection
                var connectionTest = await _receiptService.TestPrinterConnectionAsync(request.PrinterName);
                
                // Get printer status
                var printerStatus = await _receiptService.GetPrinterStatusAsync(request.PrinterName);

                var response = new PrinterTestResponse
                {
                    PrinterName = request.PrinterName ?? "Default",
                    ConnectionSuccessful = connectionTest,
                    PrinterStatus = printerStatus.ToString(),
                    TestedAt = DateTime.UtcNow,
                    Message = connectionTest ? "Printer connection test successful" : "Printer connection test failed"
                };

                if (connectionTest)
                {
                    _logger.LogInformation("Printer connection test successful for user {UserId}. Printer: {Printer}, Status: {Status}", 
                        userId, response.PrinterName, response.PrinterStatus);
                }
                else
                {
                    _logger.LogWarning("Printer connection test failed for user {UserId}. Printer: {Printer}, Status: {Status}", 
                        userId, response.PrinterName, response.PrinterStatus);
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing printer connection");
                return StatusCode(500, new { message = "Error testing printer connection", error = ex.Message });
            }
        }

        // POST: api/printer/print-test - Test sayfası yazdır
        [HttpPost("print-test")]
        [Authorize(Roles = "Administrator,Manager,Cashier")]
        public async Task<ActionResult<PrinterTestPrintResponse>> PrintTestPage([FromBody] PrinterTestPrintRequest request)
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Unknown";
                var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "Unknown";

                _logger.LogInformation("Test page print requested by user {UserId} with role {UserRole} for printer: {PrinterName}", 
                    userId, userRole, request.PrinterName ?? "Default");

                // Generate test content
                var testContent = GenerateTestPageContent();
                
                // Attempt to print test page
                var printSuccess = await _receiptService.PrintReceiptAsync(testContent, request.PrinterName);

                var response = new PrinterTestPrintResponse
                {
                    PrinterName = request.PrinterName ?? "Default",
                    PrintSuccessful = printSuccess,
                    TestedAt = DateTime.UtcNow,
                    Message = printSuccess ? "Test page printed successfully" : "Test page printing failed"
                };

                if (printSuccess)
                {
                    _logger.LogInformation("Test page printed successfully for user {UserId}. Printer: {Printer}", userId, response.PrinterName);
                }
                else
                {
                    _logger.LogWarning("Test page printing failed for user {UserId}. Printer: {Printer}", userId, response.PrinterName);
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error printing test page");
                return StatusCode(500, new { message = "Error printing test page", error = ex.Message });
            }
        }

        // Test sayfası içeriği oluştur
        private string GenerateTestPageContent()
        {
            var testContent = new System.Text.StringBuilder();
            
            testContent.AppendLine("=".PadRight(40, '='));
            testContent.AppendLine("           TEST PAGE");
            testContent.AppendLine("=".PadRight(40, '='));
            testContent.AppendLine();
            testContent.AppendLine("This is a test page to verify printer functionality.");
            testContent.AppendLine();
            testContent.AppendLine($"Generated at: {DateTime.UtcNow:dd.MM.yyyy HH:mm:ss} UTC");
            testContent.AppendLine($"Test ID: {Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()}");
            testContent.AppendLine();
            testContent.AppendLine("Printer Test Information:");
            testContent.AppendLine("- Font: OCRA-B (if supported)");
            testContent.AppendLine("- Paper width: 80mm");
            testContent.AppendLine("- Auto-cut: Enabled");
            testContent.AppendLine();
            testContent.AppendLine("If you can read this page, your printer is working correctly.");
            testContent.AppendLine();
            testContent.AppendLine("=".PadRight(40, '='));
            
            return testContent.ToString();
        }
    }

    // Response models
    public class PrinterStatusResponse
    {
        public List<string> AvailablePrinters { get; set; } = new List<string>();
        public string DefaultPrinter { get; set; } = string.Empty;
        public string DefaultPrinterStatus { get; set; } = string.Empty;
        public bool ConnectionTest { get; set; }
        public DateTime LastChecked { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class PrinterListResponse
    {
        public List<string> Printers { get; set; } = new List<string>();
        public int Count { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class PrinterTestRequest
    {
        public string? PrinterName { get; set; }
    }

    public class PrinterTestResponse
    {
        public string PrinterName { get; set; } = string.Empty;
        public bool ConnectionSuccessful { get; set; }
        public string PrinterStatus { get; set; } = string.Empty;
        public DateTime TestedAt { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class PrinterTestPrintRequest
    {
        public string? PrinterName { get; set; }
    }

    public class PrinterTestPrintResponse
    {
        public string PrinterName { get; set; } = string.Empty;
        public bool PrintSuccessful { get; set; }
        public DateTime TestedAt { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
