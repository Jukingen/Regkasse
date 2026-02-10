using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;

namespace Registrierkasse.Tests
{
    // Türkçe Açıklama: Ödeme iptal özelliği için unit testler
    public class PaymentCancellationTests : IDisposable
    {
        private readonly Mock<ILogger<PaymentController>> _mockLogger;
        private readonly Mock<IAuditLogService> _mockAuditLogService;
        private readonly Mock<IReceiptService> _mockReceiptService;
        private readonly AppDbContext _context;
        private readonly PaymentController _controller;

        public PaymentCancellationTests()
        {
            // Test veritabanı kurulumu
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options);
            _mockLogger = new Mock<ILogger<PaymentController>>();
            _mockAuditLogService = new Mock<IAuditLogService>();
            _mockReceiptService = new Mock<IReceiptService>();

            _controller = new PaymentController(
                _context, 
                _mockLogger.Object, 
                _mockReceiptService.Object, 
                _mockAuditLogService.Object
            );

            // Test verilerini hazırla
            SetupTestData();
        }

        private void SetupTestData()
        {
            // Test ürünü ekle
            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Test Product",
                Price = 10.00m,
                StockQuantity = 100,
                IsActive = true
            };
            _context.Products.Add(product);

            // Test sepeti ekle
            var cart = new Cart
            {
                CartId = "test_cart_001",
                Items = new List<CartItem>
                {
                    new CartItem
                    {
                        Id = Guid.NewGuid(),
                        ProductId = product.Id,
                        Quantity = 2,
                        UnitPrice = 10.00m,
                        TotalAmount = 20.00m
                    }
                }
            };
            _context.Carts.Add(cart);

            // Test ödeme session'ı ekle
            var paymentSession = new PaymentSession
            {
                SessionId = "test_session_001",
                CartId = cart.CartId,
                UserId = "test_user",
                UserRole = "Cashier",
                TotalAmount = 20.00m,
                PaymentMethod = PaymentMethod.Cash,
                Status = PaymentSessionStatus.Initiated,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                TseRequired = false
            };
            _context.PaymentSessions.Add(paymentSession);

            _context.SaveChanges();
        }

        [Fact]
        public async Task CancelPayment_ValidSession_ReturnsSuccess()
        {
            // Arrange
            var request = new PaymentCancelRequest
            {
                PaymentSessionId = "test_session_001",
                CancellationReason = "Test cancellation"
            };

            // Mock user claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "test_user"),
                new Claim(ClaimTypes.Role, "Cashier")
            };
            var identity = new ClaimsIdentity(claims);
            var principal = new ClaimsPrincipal(identity);
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };

            // Act
            var result = await _controller.CancelPayment(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<PaymentCancelResponse>(okResult.Value);
            
            Assert.True(response.Success);
            Assert.Equal("test_session_001", response.PaymentSessionId);
            Assert.Equal("Test cancellation", response.CancellationReason);
            Assert.Equal("test_user", response.CancelledBy);
        }

        [Fact]
        public async Task CancelPayment_NonExistentSession_ReturnsNotFound()
        {
            // Arrange
            var request = new PaymentCancelRequest
            {
                PaymentSessionId = "non_existent_session",
                CancellationReason = "Test cancellation"
            };

            // Mock user claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "test_user"),
                new Claim(ClaimTypes.Role, "Cashier")
            };
            var identity = new ClaimsIdentity(claims);
            var principal = new ClaimsPrincipal(identity);
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };

            // Act
            var result = await _controller.CancelPayment(request);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result.Result);
        }

        [Fact]
        public async Task CancelPayment_AlreadyCancelledSession_ReturnsBadRequest()
        {
            // Arrange
            var session = await _context.PaymentSessions.FirstAsync();
            session.Status = PaymentSessionStatus.Cancelled;
            await _context.SaveChangesAsync();

            var request = new PaymentCancelRequest
            {
                PaymentSessionId = session.SessionId,
                CancellationReason = "Test cancellation"
            };

            // Mock user claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "test_user"),
                new Claim(ClaimTypes.Role, "Cashier")
            };
            var identity = new ClaimsIdentity(claims);
            var principal = new ClaimsPrincipal(identity);
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };

            // Act
            var result = await _controller.CancelPayment(request);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task CancelPayment_CompletedSession_ReturnsBadRequest()
        {
            // Arrange
            var session = await _context.PaymentSessions.FirstAsync();
            session.Status = PaymentSessionStatus.Completed;
            await _context.SaveChangesAsync();

            var request = new PaymentCancelRequest
            {
                PaymentSessionId = session.SessionId,
                CancellationReason = "Test cancellation"
            };

            // Mock user claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "test_user"),
                new Claim(ClaimTypes.Role, "Cashier")
            };
            var identity = new ClaimsIdentity(claims);
            var principal = new ClaimsPrincipal(identity);
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };

            // Act
            var result = await _controller.CancelPayment(request);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task CancelPayment_UpdatesStockQuantities()
        {
            // Arrange
            var request = new PaymentCancelRequest
            {
                PaymentSessionId = "test_session_001",
                CancellationReason = "Test cancellation"
            };

            // Mock user claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "test_user"),
                new Claim(ClaimTypes.Role, "Cashier")
            };
            var identity = new ClaimsIdentity(claims);
            var principal = new ClaimsPrincipal(identity);
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };

            var initialStock = await _context.Products.FirstAsync();

            // Act
            var result = await _controller.CancelPayment(request);

            // Assert
            Assert.IsType<OkObjectResult>(result.Result);
            
            // Stok miktarının geri eklendiğini kontrol et
            var updatedProduct = await _context.Products.FirstAsync();
            Assert.Equal(initialStock.StockQuantity + 2, updatedProduct.StockQuantity);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }
    }
}
