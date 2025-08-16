using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using KasseAPI_Final.Services;

namespace KasseAPI_Final.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<TestController> _logger;
        private readonly IReceiptService _receiptService;

        public TestController(AppDbContext context, ILogger<TestController> logger, IReceiptService receiptService)
        {
            _context = context;
            _logger = logger;
            _receiptService = receiptService;
        }

        // GET: api/test/health
        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new
            {
                status = "OK",
                timestamp = DateTime.UtcNow,
                message = "Kasse API is running - FULL VERSION with Database",
                version = "2.0.0",
                features = new[]
                {
                    "Authentication (JWT)",
                    "User Management",
                    "Product Management",
                    "Customer Management",
                    "Invoice Management",
                    "PostgreSQL Database",
                    "Entity Framework Core"
                }
            });
        }

        // GET: api/test/ping
        [HttpGet("ping")]
        public IActionResult Ping()
        {
            return Ok("pong");
        }

        // GET: api/test/info
        [HttpGet("info")]
        public IActionResult Info()
        {
            return Ok(new
            {
                api = "Kasse API",
                version = "2.0.0",
                mode = "full-production",
                timestamp = DateTime.UtcNow,
                database = "PostgreSQL",
                orm = "Entity Framework Core",
                authentication = "JWT + Identity"
            });
        }

        // GET: api/test/database
        [HttpGet("database")]
        public async Task<IActionResult> DatabaseStatus()
        {
            try
            {
                var canConnect = await _context.Database.CanConnectAsync();
                var connectionString = _context.Database.GetConnectionString();
                
                return Ok(new
                {
                    status = canConnect ? "Connected" : "Disconnected",
                    canConnect = canConnect,
                    provider = _context.Database.ProviderName,
                    connectionString = connectionString?.Substring(0, Math.Min(50, connectionString?.Length ?? 0)) + "...",
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    status = "Error",
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        // POST: api/test/cart-reset-simulation - Ödeme sonrası sepet sıfırlama simülasyonu
        [HttpPost("cart-reset-simulation")]
        public async Task<IActionResult> SimulateCartResetAfterPayment([FromBody] CartResetSimulationRequest request)
        {
            try
            {
                _logger.LogInformation("Simulating cart reset after payment: TableNumber={TableNumber}, ItemsCount={ItemsCount}", 
                    request.TableNumber, request.ItemsCount);

                // Test sepeti oluştur
                var testCart = new Cart
                {
                    CartId = Guid.NewGuid().ToString(),
                    TableNumber = request.TableNumber,
                    WaiterName = "Test Waiter",
                    Status = CartStatus.Active,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddHours(24),
                    Notes = "Test cart for reset simulation",
                    UserId = "test-user"
                };

                _context.Carts.Add(testCart);

                // Test ürünleri ekle
                for (int i = 1; i <= request.ItemsCount; i++)
                {
                    var cartItem = new CartItem
                    {
                        Id = Guid.NewGuid(),
                        CartId = testCart.CartId,
                        ProductId = Guid.NewGuid(),
                        Quantity = i,
                        UnitPrice = 10.00m * i,
                        Notes = $"Test item {i}",
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.CartItems.Add(cartItem);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Test cart created: CartId={CartId}, ItemsCount={ItemsCount}", 
                    testCart.CartId, request.ItemsCount);

                return Ok(new
                {
                    message = "Test cart created successfully for reset simulation",
                    cartId = testCart.CartId,
                    tableNumber = testCart.TableNumber,
                    itemsCount = request.ItemsCount,
                    status = "ready_for_reset_test",
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating test cart for reset simulation");
                return StatusCode(500, new
                {
                    status = "Error",
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        // GET: api/test/stats
        [HttpGet("stats")]
        public async Task<IActionResult> DatabaseStats()
        {
            try
            {
                var userCount = await _context.Users.CountAsync();
                var productCount = await _context.Products.CountAsync();
                var customerCount = await _context.Customers.CountAsync();
                var invoiceCount = await _context.Invoices.CountAsync();

                return Ok(new
                {
                    users = userCount,
                    products = productCount,
                    customers = customerCount,
                    invoices = invoiceCount,
                    total = userCount + productCount + customerCount + invoiceCount,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    status = "Error",
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        // GET: api/test/sample-data
        [HttpGet("sample-data")]
        public async Task<IActionResult> CreateSampleData()
        {
            try
            {
                // Check if sample data already exists
                if (await _context.Products.AnyAsync() || 
                    await _context.Customers.AnyAsync() || 
                    await _context.Invoices.AnyAsync())
                {
                    return BadRequest("Sample data already exists");
                }

                // Create sample products
                var products = new List<Product>
                {
                    new Product
                    {
                        Name = "Sample Product 1",
                        Description = "This is a sample product for testing",
                        Price = 19.99m,
                        Cost = 15.00m,
                        TaxType = TaxType.Standard,
                        TaxRate = 20.0m,
                        Barcode = "1234567890123",
                        Category = "Electronics",
                        StockQuantity = 100,
                        MinStockLevel = 10,
                        Unit = "piece"
                    },
                    new Product
                    {
                        Name = "Sample Product 2",
                        Description = "Another sample product for testing",
                        Price = 29.99m,
                        Cost = 22.00m,
                        TaxType = TaxType.Reduced,
                        TaxRate = 10.0m,
                        Barcode = "9876543210987",
                        Category = "Books",
                        StockQuantity = 50,
                        MinStockLevel = 5,
                        Unit = "piece"
                    }
                };

                // Create sample customers
                var customers = new List<Customer>
                {
                    new Customer
                    {
                        Name = "John Doe",
                        CustomerNumber = "CUST001",
                        Email = "john.doe@example.com",
                        Phone = "+43 123 456 789",
                        Address = "Vienna, Austria",
                        TaxNumber = "ATU12345678",
                        Category = CustomerCategory.Regular,
                        IsVip = false
                    },
                    new Customer
                    {
                        Name = "Jane Smith",
                        CustomerNumber = "CUST002",
                        Email = "jane.smith@example.com",
                        Phone = "+43 987 654 321",
                        Address = "Salzburg, Austria",
                        TaxNumber = "ATU87654321",
                        Category = CustomerCategory.VIP,
                        IsVip = true
                    }
                };

                // Add to context
                _context.Products.AddRange(products);
                _context.Customers.AddRange(customers);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Sample data created successfully",
                    productsCreated = products.Count,
                    customersCreated = customers.Count,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    status = "Error",
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        // GET: api/test/printer - Yazıcı entegrasyonunu test et
        [HttpGet("printer")]
        [Authorize(Roles = "Administrator,Manager,Cashier")]
        public async Task<ActionResult<object>> TestPrinterIntegration()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Unknown";
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";

                _logger.LogInformation("Printer integration test requested by user {UserId} with role {UserRole}", userId, userRole);

                // Test printer service
                var availablePrinters = _receiptService.GetAvailablePrinters();
                var defaultPrinterStatus = await _receiptService.GetPrinterStatusAsync();
                var connectionTest = await _receiptService.TestPrinterConnectionAsync();

                // Test receipt generation
                var testPayment = new PaymentDetails
                {
                    Id = Guid.NewGuid(),
                    Amount = 25.50m,
                    PaymentMethod = PaymentMethod.Cash,
                    PaymentDate = DateTime.UtcNow,
                    Status = PaymentStatus.Completed,
                    TransactionId = "TEST-TXN-001",
                    Reference = "Test Payment"
                };

                var testInvoice = new Invoice
                {
                    Id = Guid.NewGuid(),
                    InvoiceNumber = "TEST-INV-001",
                    InvoiceDate = DateTime.UtcNow,
                    Subtotal = 21.25m,
                    TaxAmount = 4.25m,
                    TotalAmount = 25.50m,
                    TseSignature = "TEST-SIGNATURE-001",
                    TseTimestamp = DateTime.UtcNow
                };

                var testCart = new Cart
                {
                    Id = Guid.NewGuid(),
                    Items = new List<CartItem>
                    {
                        new CartItem
                        {
                            Id = Guid.NewGuid(),
                            ProductId = Guid.NewGuid(),
                            Product = new Product { Name = "Test Product", Price = 21.25m },
                            Quantity = 1,
                            UnitPrice = 21.25m
                        }
                    }
                };

                var receiptContent = await _receiptService.GenerateReceiptAsync(testPayment, testInvoice, testCart);
                var digitalReceipt = await _receiptService.GenerateDigitalReceiptAsync(testPayment, testInvoice, testCart);

                var testResult = new
                {
                    TestType = "Printer Integration Test",
                    Timestamp = DateTime.UtcNow,
                    User = new { Id = userId, Role = userRole },
                    
                    PrinterTests = new
                    {
                        AvailablePrinters = availablePrinters,
                        DefaultPrinterStatus = defaultPrinterStatus.ToString(),
                        ConnectionTest = connectionTest,
                        Message = connectionTest ? "Printer connection successful" : "Printer connection failed"
                    },
                    
                    ReceiptTests = new
                    {
                        ReceiptGenerated = !string.IsNullOrEmpty(receiptContent),
                        DigitalReceiptGenerated = !string.IsNullOrEmpty(digitalReceipt),
                        ReceiptLength = receiptContent?.Length ?? 0,
                        DigitalReceiptLength = digitalReceipt?.Length ?? 0
                    },
                    
                    TestStatus = connectionTest ? "PASSED" : "FAILED",
                    Message = connectionTest ? 
                        "Printer integration test completed successfully" : 
                        "Printer integration test failed - check printer connection"
                };

                if (connectionTest)
                {
                    _logger.LogInformation("Printer integration test PASSED for user {UserId}", userId);
                }
                else
                {
                    _logger.LogWarning("Printer integration test FAILED for user {UserId}", userId);
                }

                return Ok(testResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during printer integration test");
                return StatusCode(500, new { 
                    message = "Error during printer integration test", 
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }
    }

    // Test request models
    public class CartResetSimulationRequest
    {
        public int TableNumber { get; set; } = 1;
        public int ItemsCount { get; set; } = 3;
    }
}
