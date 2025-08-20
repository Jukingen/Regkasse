using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using KasseAPI_Final.Controllers.Base;
using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using System.Security.Claims;

namespace KasseAPI_Final.Controllers
{
	/// <summary>
	/// Test amaçlı yardımcı uç noktalar
	/// </summary>
	[Route("api/[controller]")]
	[ApiController]
	[Authorize(Roles = "Admin")]
	public class TestController : BaseController
	{
		private readonly IGenericRepository<Customer> _customerRepository;
		private readonly IGenericRepository<Product> _productRepository;
		private readonly IPaymentService _paymentService;

		public TestController(
			IGenericRepository<Customer> customerRepository,
			IGenericRepository<Product> productRepository,
			IPaymentService paymentService,
			ILogger<TestController> logger) : base(logger)
		{
			_customerRepository = customerRepository;
			_productRepository = productRepository;
			_paymentService = paymentService;
		}

		/// <summary>
		/// Tek çağrıda: müşteri + ürün oluşturur ve ödeme yaratır (hızlı test akışı)
		/// </summary>
		[HttpPost("quick-payment")]
		public async Task<IActionResult> CreateQuickPayment([FromQuery] bool tseRequired = false)
		{
			try
			{
				// Türkçe Açıklama: Kimliği doğrulanmış kullanıcı ID'sini al
				var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
				if (string.IsNullOrEmpty(userId))
				{
					return ErrorResponse("User not authenticated", 401);
				}

				// Türkçe Açıklama: Hızlı test için rastgele Steuernummer (ATU########) üret
				var rnd = Random.Shared.Next(0, 100_000_000);
				var steuernummer = $"ATU{rnd:00000000}";

				// Türkçe Açıklama: Hızlı test için rastgele müşteri oluştur
				var customer = new Customer
				{
					Name = "Test Kunde",
					Email = $"test.{Guid.NewGuid():N}@example.com",
					CustomerNumber = $"T{Guid.NewGuid():N}".Substring(0, 12),
					TaxNumber = steuernummer,
					CreatedBy = userId,
					IsActive = true
				};
				customer = await _customerRepository.AddAsync(customer);

				// Türkçe Açıklama: Hızlı test için stoklu ürün oluştur
				// Türkçe Açıklama: Benzersiz barkod üret (unique index var)
				var barcode = $"BC{Guid.NewGuid():N}".Substring(0, 14);
				var product = new Product
				{
					Name = "Test Produkt",
					Price = 10.00m,
					StockQuantity = 100,
					Category = "Test",
					Barcode = barcode,
					TaxType = TaxType.Standard,
					TaxRate = 0.20m,
					CreatedBy = userId,
					IsActive = true
				};
				product = await _productRepository.AddAsync(product);

				// Türkçe Açıklama: Ödeme request'ini hazırla
				var request = new CreatePaymentRequest
				{
					CustomerId = customer.Id,
					Items = new List<PaymentItemRequest>
					{
						new PaymentItemRequest
						{
							ProductId = product.Id,
							Quantity = 1,
							TaxType = "standard"
						}
					},
					Payment = new PaymentMethodRequest
					{
						Method = "cash",
						TseRequired = tseRequired
					},
					TableNumber = 1,
					CashierId = "test-controller",
					TotalAmount = product.Price,
					Steuernummer = steuernummer,
					KassenId = "KASSE-001",
					Notes = "TestController quick payment"
				};

				// Türkçe Açıklama: Ödemeyi oluştur
				var result = await _paymentService.CreatePaymentAsync(request, userId);
				if (!result.Success)
				{
					return ErrorResponse(result.Message, 400, result.Errors);
				}

				return SuccessResponse(new
				{
					customer = new { customer.Id, customer.Name, customer.Email },
					product = new { product.Id, product.Name, product.Price },
					payment = new
					{
						result.Payment!.Id,
						result.Payment.TotalAmount,
						result.Payment.TseSignature,
						result.Payment.ReceiptNumber
					}
				}, "Quick payment created successfully");
			}
			catch (Exception ex)
			{
				return HandleException(ex, "TestController.CreateQuickPayment");
			}
		}
	}
}


