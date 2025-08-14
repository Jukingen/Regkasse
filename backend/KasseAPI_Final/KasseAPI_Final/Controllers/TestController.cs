using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<TestController> _logger;

        public TestController(AppDbContext context, ILogger<TestController> logger)
        {
            _context = context;
            _logger = logger;
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
    }
}
