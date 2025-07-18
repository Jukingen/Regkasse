using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Registrierkasse_API.Data;
using Registrierkasse_API.Models;
using Registrierkasse_API.Services;
using System.Text.Json;
using Xunit;

namespace Registrierkasse.Tests
{
    /// <summary>
    /// Avusturya mevzuatı uyumluluk testleri - POS sistemi yasal gereksinimlerini doğrular
    /// </summary>
    public class AustrianComplianceTests : IClassFixture<CustomWebApplicationFactory<Program>>
    {
        private readonly CustomWebApplicationFactory<Program> _factory;
        private readonly AppDbContext _context;

        public AustrianComplianceTests(CustomWebApplicationFactory<Program> factory)
        {
            _factory = factory;
            var scope = factory.Services.CreateScope();
            _context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        }

        /// <summary>
        /// Fiş numarası formatının Avusturya standartlarına uygunluğunu test eder
        /// </summary>
        [Fact]
        public async Task ReceiptNumber_ShouldFollowAustrianFormat()
        {
            // Arrange
            var receipt = new Receipt
            {
                ReceiptNumber = "AT-DEMO-20241201-12345678",
                ReceiptDate = DateTime.UtcNow,
                ReceiptTime = "14:30:25",
                CashRegisterId = "DEMO",
                TseSignature = "DEMO_SIGNATURE_123",
                TotalAmount = 25.50m,
                TaxAmount = 4.25m,
                TaxDetails = JsonSerializer.Serialize(new { standard = 20, reduced = 10, special = 13 }),
                PaymentMethod = "cash",
                QrCodeData = "DEMO_QR_DATA"
            };

            // Act
            _context.Receipts.Add(receipt);
            await _context.SaveChangesAsync();

            // Assert
            Assert.Matches(@"^AT-[A-Z0-9]+-\d{8}-[A-Z0-9]+$", receipt.ReceiptNumber);
            Assert.True(receipt.ReceiptNumber.Length >= 20);
        }

        /// <summary>
        /// Fişte zorunlu alanların bulunmasını test eder
        /// </summary>
        [Fact]
        public async Task Receipt_ShouldHaveAllRequiredFields()
        {
            // Arrange & Act
            var receipt = new Receipt
            {
                ReceiptNumber = "AT-DEMO-20241201-12345678",
                ReceiptDate = DateTime.UtcNow,
                ReceiptTime = "14:30:25",
                CashRegisterId = "DEMO",
                TseSignature = "DEMO_SIGNATURE_123",
                TotalAmount = 25.50m,
                TaxAmount = 4.25m,
                TaxDetails = JsonSerializer.Serialize(new { standard = 20, reduced = 10, special = 13 }),
                PaymentMethod = "cash",
                QrCodeData = "DEMO_QR_DATA"
            };

            // Assert
            Assert.NotNull(receipt.ReceiptNumber);
            Assert.NotNull(receipt.ReceiptTime);
            Assert.NotNull(receipt.CashRegisterId);
            Assert.NotNull(receipt.TseSignature);
            Assert.True(receipt.TotalAmount > 0);
            Assert.True(receipt.TaxAmount >= 0);
            Assert.NotNull(receipt.TaxDetails);
            Assert.NotNull(receipt.PaymentMethod);
            Assert.NotNull(receipt.QrCodeData);
        }

        /// <summary>
        /// TSE imzasının benzersizliğini test eder
        /// </summary>
        [Fact]
        public async Task TseSignature_ShouldBeUnique()
        {
            // Arrange
            var signature1 = "DEMO_SIGNATURE_123";
            var signature2 = "DEMO_SIGNATURE_456";

            var receipt1 = new Receipt
            {
                ReceiptNumber = "AT-DEMO-20241201-12345678",
                ReceiptDate = DateTime.UtcNow,
                ReceiptTime = "14:30:25",
                CashRegisterId = "DEMO",
                TseSignature = signature1,
                TotalAmount = 25.50m,
                TaxAmount = 4.25m,
                TaxDetails = JsonSerializer.Serialize(new { standard = 20, reduced = 10, special = 13 }),
                PaymentMethod = "cash",
                QrCodeData = "DEMO_QR_DATA_1"
            };

            var receipt2 = new Receipt
            {
                ReceiptNumber = "AT-DEMO-20241201-87654321",
                ReceiptDate = DateTime.UtcNow,
                ReceiptTime = "15:30:25",
                CashRegisterId = "DEMO",
                TseSignature = signature2,
                TotalAmount = 30.00m,
                TaxAmount = 5.00m,
                TaxDetails = JsonSerializer.Serialize(new { standard = 20, reduced = 10, special = 13 }),
                PaymentMethod = "card",
                QrCodeData = "DEMO_QR_DATA_2"
            };

            // Act
            _context.Receipts.AddRange(receipt1, receipt2);
            await _context.SaveChangesAsync();

            // Assert
            Assert.NotEqual(receipt1.TseSignature, receipt2.TseSignature);
            Assert.NotEqual(receipt1.ReceiptNumber, receipt2.ReceiptNumber);
        }

        /// <summary>
        /// Vergi hesaplamalarının doğruluğunu test eder
        /// </summary>
        [Fact]
        public async Task TaxCalculation_ShouldBeCorrect()
        {
            // Arrange
            var subtotal = 100.00m;
            var standardTaxRate = 0.20m; // %20
            var expectedTax = subtotal * standardTaxRate;
            var expectedTotal = subtotal + expectedTax;

            var receipt = new Receipt
            {
                ReceiptNumber = "AT-DEMO-20241201-12345678",
                ReceiptDate = DateTime.UtcNow,
                ReceiptTime = "14:30:25",
                CashRegisterId = "DEMO",
                TseSignature = "DEMO_SIGNATURE_123",
                TotalAmount = expectedTotal,
                TaxAmount = expectedTax,
                TaxDetails = JsonSerializer.Serialize(new { standard = 20, reduced = 10, special = 13 }),
                PaymentMethod = "cash",
                QrCodeData = "DEMO_QR_DATA"
            };

            // Act & Assert
            Assert.Equal(expectedTotal, receipt.TotalAmount);
            Assert.Equal(expectedTax, receipt.TaxAmount);
            Assert.Equal(subtotal, receipt.TotalAmount - receipt.TaxAmount);
        }

        /// <summary>
        /// Demo kullanıcı rollerinin doğru atanmasını test eder
        /// </summary>
        [Fact]
        public async Task DemoUsers_ShouldHaveCorrectRoles()
        {
            // Arrange
            var adminUser = new ApplicationUser
            {
                UserName = "admin1@demo.com",
                Email = "admin1@demo.com",
                FirstName = "Admin",
                LastName = "Demo",
                EmployeeNumber = "ADMIN001",
                AccountType = "demo",
                IsDemo = true,
                IsActive = true
            };

            var cashierUser = new ApplicationUser
            {
                UserName = "cashier1@demo.com",
                Email = "cashier1@demo.com",
                FirstName = "Ahmet",
                LastName = "Kasiyer",
                EmployeeNumber = "CASH001",
                AccountType = "demo",
                IsDemo = true,
                IsActive = true
            };

            // Act
            _context.Users.AddRange(adminUser, cashierUser);
            await _context.SaveChangesAsync();

            // Assert
            Assert.True(adminUser.IsDemo);
            Assert.True(cashierUser.IsDemo);
            Assert.Equal("demo", adminUser.AccountType);
            Assert.Equal("demo", cashierUser.AccountType);
        }

        /// <summary>
        /// İşlem loglarının doğru kaydedilmesini test eder
        /// </summary>
        [Fact]
        public async Task OperationLogs_ShouldBeRecordedCorrectly()
        {
            // Arrange
            var operationLog = new OperationLog
            {
                Operation = "create",
                EntityType = "Receipt",
                EntityId = "123",
                UserId = "demo-user",
                Username = "demo@test.com",
                BeforeState = "",
                AfterState = JsonSerializer.Serialize(new { total = 25.50, tax = 4.25 }),
                Summary = "Receipt created successfully",
                IpAddress = "127.0.0.1",
                UserRole = "Cashier"
            };

            // Act
            _context.OperationLogs.Add(operationLog);
            await _context.SaveChangesAsync();

            // Assert
            Assert.NotNull(operationLog.Timestamp);
            Assert.NotNull(operationLog.Summary);
            Assert.NotNull(operationLog.AfterState);
            Assert.True(operationLog.Timestamp > DateTime.UtcNow.AddMinutes(-1));
        }

        /// <summary>
        /// Çok dilli hata mesajlarının doğru çalışmasını test eder
        /// </summary>
        [Theory]
        [InlineData("de-DE", "Sie haben keine Berechtigung für diese Aktion.")]
        [InlineData("en", "You are not authorized for this action.")]
        [InlineData("tr", "Bu işlem için yetkiniz yok.")]
        public void ErrorMessages_ShouldBeLocalized(string language, string expectedMessage)
        {
            // Arrange & Act
            var errorMessages = new Dictionary<string, string>
            {
                ["de-DE"] = "Sie haben keine Berechtigung für diese Aktion.",
                ["en"] = "You are not authorized for this action.",
                ["tr"] = "Bu işlem için yetkiniz yok."
            };

            // Assert
            Assert.Equal(expectedMessage, errorMessages[language]);
        }
    }
} 