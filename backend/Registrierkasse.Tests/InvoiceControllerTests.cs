using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Registrierkasse_API;
using Registrierkasse_API.Data;
using Registrierkasse_API.Models;
using Registrierkasse_API.Services;
using Xunit;

namespace Registrierkasse.Tests;

public class InvoiceControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly AppDbContext _context;
    private readonly ITseService _tseService;

    public InvoiceControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        var scope = _factory.Services.CreateScope();
        _context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _tseService = scope.ServiceProvider.GetRequiredService<ITseService>();

        // Test veritabanını hazırla
        SeedTestData();
    }

    [Fact]
    public async Task CreateInvoice_WithValidData_ReturnsCreated()
    {
        // Arrange
        var token = await GetAuthToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var invoiceRequest = new
        {
            Items = new[]
            {
                new
                {
                    ProductId = "test-product-1",
                    Quantity = 2,
                    TaxType = "standard"
                }
            },
            Payment = new
            {
                Method = "card",
                TseRequired = true
            }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(invoiceRequest),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/invoices", content);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var responseContent = await response.Content.ReadAsStringAsync();
        var invoice = JsonSerializer.Deserialize<InvoiceResponse>(responseContent);
        Assert.NotNull(invoice);
        Assert.NotNull(invoice.ReceiptNumber);
        Assert.NotNull(invoice.TseSignature);
        Assert.True(invoice.IsPrinted);
    }

    [Fact]
    public async Task GetInvoice_WithValidId_ReturnsOk()
    {
        // Arrange
        var token = await GetAuthToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Önce bir fatura oluştur
        var invoice = await CreateTestInvoice();

        // Act
        var response = await _client.GetAsync($"/api/invoices/{invoice.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var responseContent = await response.Content.ReadAsStringAsync();
        var retrievedInvoice = JsonSerializer.Deserialize<InvoiceResponse>(responseContent);
        Assert.NotNull(retrievedInvoice);
        Assert.Equal(invoice.Id, retrievedInvoice.Id);
    }

    [Fact]
    public async Task GetInvoices_ReturnsOk()
    {
        // Arrange
        var token = await GetAuthToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Test faturaları oluştur
        await CreateTestInvoice();
        await CreateTestInvoice();

        // Act
        var response = await _client.GetAsync("/api/invoices");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var responseContent = await response.Content.ReadAsStringAsync();
        var invoices = JsonSerializer.Deserialize<List<InvoiceResponse>>(responseContent);
        Assert.NotNull(invoices);
        Assert.True(invoices.Count >= 2);
    }

    private async Task<string> GetAuthToken()
    {
        var loginRequest = new
        {
            Email = "test@example.com",
            Password = "Test123!"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(loginRequest),
            Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync("/api/auth/login", content);
        var responseContent = await response.Content.ReadAsStringAsync();
        var tokenResponse = JsonSerializer.Deserialize<LoginResponse>(responseContent);
        return tokenResponse.Token;
    }

    private async Task<Invoice> CreateTestInvoice()
    {
        var invoice = new Invoice
        {
            ReceiptNumber = $"AT-TEST-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid()}",
            TseSignature = "TEST_SIGNATURE",
            IsPrinted = true,
            TaxDetails = new Dictionary<string, decimal>
            {
                { "standard", 20.00m },
                { "reduced", 10.00m },
                { "special", 13.00m }
            }
        };

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();
        return invoice;
    }

    private void SeedTestData()
    {
        // Test kullanıcısı oluştur
        var testUser = new ApplicationUser
        {
            UserName = "test@example.com",
            Email = "test@example.com",
            EmailConfirmed = true,
            FirstName = "Test",
            LastName = "User"
        };

        // Kullanıcı zaten varsa ekleme
        if (!_context.Users.Any(u => u.Email == testUser.Email))
        {
            _context.Users.Add(testUser);
            _context.SaveChanges();
        }

        // Test ürünü oluştur
        var testProduct = new Product
        {
            Id = "test-product-1",
            Name = "Test Product",
            Price = 10.00m,
            TaxType = "standard"
        };

        // Ürün zaten varsa ekleme
        if (!_context.Products.Any(p => p.Id == testProduct.Id))
        {
            _context.Products.Add(testProduct);
            _context.SaveChanges();
        }
    }
}

// Response modelleri TestModels.cs'e taşındı 