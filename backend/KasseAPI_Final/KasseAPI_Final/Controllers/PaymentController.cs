using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Controllers.Base;

namespace KasseAPI_Final.Controllers
{
    /// <summary>
    /// Ödeme işlemleri için controller - Service layer kullanarak
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PaymentController : BaseController
    {
        private readonly IPaymentService _paymentService;

        public PaymentController(IPaymentService paymentService, ILogger<PaymentController> logger) 
            : base(logger)
        {
            _paymentService = paymentService;
        }

        /// <summary>
        /// Mevcut ödeme yöntemlerini getir
        /// </summary>
        [HttpGet("methods")]
        [Authorize] // Authentication gerekli
        public IActionResult GetPaymentMethods()
        {
            try
            {
                // Kullanıcı ID'sini al
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return ErrorResponse("User not authenticated", 401);
                }

                var paymentMethods = new[]
                {
                    new { id = "cash", name = "Nakit", type = "cash", icon = "cash-outline" },
                    new { id = "card", name = "Kart", type = "card", icon = "card-outline" },
                    new { id = "voucher", name = "Kupon", type = "voucher", icon = "ticket-outline" },
                    new { id = "transfer", name = "Havale", type = "transfer", icon = "swap-horizontal-outline" }
                };

                return SuccessResponse(paymentMethods, "Payment methods retrieved successfully");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "Get Payment Methods");
            }
        }

        /// <summary>
        /// Yeni ödeme oluştur
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequest request)
        {
            try
            {
                var validationResult = ValidateModel();
                if (validationResult != null)
                {
                    return validationResult;
                }

                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return ErrorResponse("User not authenticated", 401);
                }

                var result = await _paymentService.CreatePaymentAsync(request, userId);
                
                if (result.Success)
                {
                    return CreatedAtAction(nameof(GetPayment), new { id = result.Payment!.Id }, 
                        SuccessResponse(result, "Payment created successfully"));
                }

                return ErrorResponse(result.Message, 400, result.Errors);
            }
            catch (Exception ex)
            {
                return HandleException(ex, "Create Payment");
            }
        }

        /// <summary>
        /// Ödeme detaylarını getir
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetPayment(Guid id)
        {
            try
            {
                var payment = await _paymentService.GetPaymentAsync(id);
                
                if (payment == null)
                {
                    return ErrorResponse($"Payment with ID {id} not found", 404);
                }

                return SuccessResponse(payment, "Payment retrieved successfully");
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"Get Payment with ID {id}");
            }
        }

        /// <summary>
        /// Müşteri ödemelerini getir
        /// </summary>
        [HttpGet("customer/{customerId}")]
        public async Task<IActionResult> GetCustomerPayments(Guid customerId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var (validPageNumber, validPageSize) = ValidatePagination(pageNumber, pageSize);
                
                var payments = await _paymentService.GetCustomerPaymentsAsync(customerId, validPageNumber, validPageSize);
                
                var response = new
                {
                    items = payments,
                    pagination = new
                    {
                        pageNumber = validPageNumber,
                        pageSize = validPageSize,
                        customerId = customerId
                    }
                };

                return SuccessResponse(response, $"Retrieved payments for customer {customerId}");
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"Get Customer Payments for customer {customerId}");
            }
        }

        /// <summary>
        /// Ödeme yöntemine göre ödemeleri getir
        /// </summary>
        [HttpGet("method/{paymentMethod}")]
        public async Task<IActionResult> GetPaymentsByMethod(string paymentMethod, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var (validPageNumber, validPageSize) = ValidatePagination(pageNumber, pageSize);
                
                var payments = await _paymentService.GetPaymentsByMethodAsync(paymentMethod, validPageNumber, validPageSize);
                
                var response = new
                {
                    items = payments,
                    pagination = new
                    {
                        pageNumber = validPageNumber,
                        pageSize = validPageSize,
                        paymentMethod = paymentMethod
                    }
                };

                return SuccessResponse(response, $"Retrieved payments by method {paymentMethod}");
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"Get Payments by Method {paymentMethod}");
            }
        }

        /// <summary>
        /// Tarih aralığına göre ödemeleri getir
        /// </summary>
        [HttpGet("date-range")]
        public async Task<IActionResult> GetPaymentsByDateRange(
            [FromQuery] DateTime startDate, 
            [FromQuery] DateTime endDate,
            [FromQuery] int pageNumber = 1, 
            [FromQuery] int pageSize = 20)
        {
            try
            {
                if (startDate > endDate)
                {
                    return ErrorResponse("Start date cannot be after end date", 400);
                }

                var (validPageNumber, validPageSize) = ValidatePagination(pageNumber, pageSize);
                
                var payments = await _paymentService.GetPaymentsByDateRangeAsync(startDate, endDate, validPageNumber, validPageSize);
                
                var response = new
                {
                    items = payments,
                    pagination = new
                    {
                        pageNumber = validPageNumber,
                        pageSize = validPageSize,
                        startDate = startDate,
                        endDate = endDate
                    }
                };

                return SuccessResponse(response, $"Retrieved payments from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"Get Payments by Date Range from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
            }
        }

        /// <summary>
        /// Ödeme iptal et
        /// </summary>
        [HttpPost("{id}/cancel")]
        public async Task<IActionResult> CancelPayment(Guid id, [FromBody] CancelPaymentRequest request)
        {
            try
            {
                var validationResult = ValidateModel();
                if (validationResult != null)
                {
                    return validationResult;
                }

                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return ErrorResponse("User not authenticated", 401);
                }

                var result = await _paymentService.CancelPaymentAsync(id, request.Reason, userId);
                
                if (result.Success)
                {
                    return SuccessResponse(result, "Payment cancelled successfully");
                }

                return ErrorResponse(result.Message, 400, result.Errors);
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"Cancel Payment with ID {id}");
            }
        }

        /// <summary>
        /// Ödeme iade et
        /// </summary>
        [HttpPost("{id}/refund")]
        public async Task<IActionResult> RefundPayment(Guid id, [FromBody] RefundPaymentRequest request)
        {
            try
            {
                var validationResult = ValidateModel();
                if (validationResult != null)
                {
                    return validationResult;
                }

                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return ErrorResponse("User not authenticated", 401);
                }

                var result = await _paymentService.RefundPaymentAsync(id, request.Amount, request.Reason, userId);
                
                if (result.Success)
                {
                    return SuccessResponse(result, "Refund processed successfully");
                }

                return ErrorResponse(result.Message, 400, result.Errors);
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"Refund Payment with ID {id}");
            }
        }

        /// <summary>
        /// Ödeme istatistiklerini getir
        /// </summary>
        [HttpGet("statistics")]
        public async Task<IActionResult> GetPaymentStatistics(
            [FromQuery] DateTime startDate, 
            [FromQuery] DateTime endDate)
        {
            try
            {
                if (startDate > endDate)
                {
                    return ErrorResponse("Start date cannot be after end date", 400);
                }

                var statistics = await _paymentService.GetPaymentStatisticsAsync(startDate, endDate);
                
                return SuccessResponse(statistics, $"Retrieved payment statistics from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"Get Payment Statistics from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
            }
        }

        /// <summary>
        /// TSE imzası oluştur
        /// </summary>
        [HttpPost("{id}/tse-signature")]
        [Authorize(Roles = "Administrator,Kasiyer")]
        public async Task<IActionResult> GenerateTseSignature(Guid id)
        {
            try
            {
                var payment = await _paymentService.GetPaymentAsync(id);
                if (payment == null)
                {
                    return ErrorResponse($"Payment with ID {id} not found", 404);
                }

                var signature = await _paymentService.GenerateTseSignatureAsync(payment);
                
                return SuccessResponse(new { tseSignature = signature }, "TSE signature generated successfully");
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"Generate TSE Signature for Payment with ID {id}");
            }
        }
    }

    /// <summary>
    /// Ödeme iptal request modeli
    /// </summary>
    public class CancelPaymentRequest
    {
        [Required]
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Ödeme iade request modeli
    /// </summary>
    public class RefundPaymentRequest
    {
        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
        public decimal Amount { get; set; }
        
        [Required]
        public string Reason { get; set; } = string.Empty;
    }
}
