using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Services
{
    /// <summary>
    /// Read-only payment queries and statistics. No fiscal writes.
    /// </summary>
    public interface IPaymentQueryService
    {
        Task<PaymentDetails?> GetPaymentAsync(Guid paymentId);

        Task<IEnumerable<PaymentDetails>> GetCustomerPaymentsAsync(Guid customerId, int pageNumber = 1, int pageSize = 20);

        Task<IEnumerable<PaymentDetails>> GetPaymentsByMethodAsync(string paymentMethod, int pageNumber = 1, int pageSize = 20);

        Task<IEnumerable<PaymentDetails>> GetPaymentsByDateRangeAsync(DateTime startDate, DateTime endDate, int pageNumber = 1, int pageSize = 20);

        Task<PaymentStatistics> GetPaymentStatisticsAsync(DateTime startDate, DateTime endDate);
    }

    public sealed class PaymentQueryService : IPaymentQueryService
    {
        private readonly IGenericRepository<PaymentDetails> _paymentRepository;
        private readonly AppDbContext _context;
        private readonly IPaymentMethodCatalogService _paymentMethodCatalog;
        private readonly ISettingsTenantResolver _settingsTenantResolver;
        private readonly ILogger _logger;

        public PaymentQueryService(
            IGenericRepository<PaymentDetails> paymentRepository,
            AppDbContext context,
            IPaymentMethodCatalogService paymentMethodCatalog,
            ISettingsTenantResolver settingsTenantResolver,
            ILogger logger)
        {
            _paymentRepository = paymentRepository;
            _context = context;
            _paymentMethodCatalog = paymentMethodCatalog;
            _settingsTenantResolver = settingsTenantResolver;
            _logger = logger;
        }

        /// <summary>
        /// Ödeme detaylarını getir
        /// </summary>
        public async Task<PaymentDetails?> GetPaymentAsync(Guid paymentId)
        {
            try
            {
                var payment = await _paymentRepository.GetByIdAsync(paymentId);
                if (payment == null)
                {
                    _logger.LogDebug("Payment not found with ID: {PaymentId}", paymentId);
                    return null;
                }

                if (!await PaymentBelongsToEffectiveTenantAsync(payment))
                {
                    _logger.LogDebug("Payment {PaymentId} is not in the effective tenant", paymentId);
                    return null;
                }

                _logger.LogDebug("Payment retrieved successfully: {PaymentId}", paymentId);
                return payment;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payment {PaymentId}", paymentId);
                return null;
            }
        }

        /// <summary>
        /// Müşteri ödemelerini getir
        /// </summary>
        public async Task<IEnumerable<PaymentDetails>> GetCustomerPaymentsAsync(Guid customerId, int pageNumber = 1, int pageSize = 20)
        {
            try
            {
                // Sayfa boyutu validasyonu
                if (pageSize <= 0 || pageSize > 100)
                {
                    _logger.LogWarning("Invalid page size: {PageSize}. Using default value 20", pageSize);
                    pageSize = 20;
                }

                if (pageNumber <= 0)
                {
                    _logger.LogWarning("Invalid page number: {PageNumber}. Using default value 1", pageNumber);
                    pageNumber = 1;
                }

                var (items, totalCount) = await _paymentRepository.GetPagedAsync(
                    pageNumber,
                    pageSize,
                    p => p.CustomerId == customerId && p.IsActive,
                    p => p.CreatedAt,
                    false);

                _logger.LogDebug("Retrieved {Count} payments for customer {CustomerId} (page {PageNumber}/{TotalPages})",
                    items.Count(), customerId, pageNumber, Math.Ceiling((double)totalCount / pageSize));

                return items;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payments for customer {CustomerId}", customerId);
                return Enumerable.Empty<PaymentDetails>();
            }
        }

        /// <summary>
        /// Ödeme yöntemine göre ödemeleri getir
        /// </summary>
        public async Task<IEnumerable<PaymentDetails>> GetPaymentsByMethodAsync(string paymentMethod, int pageNumber = 1, int pageSize = 20)
        {
            try
            {
                // Sayfa boyutu validasyonu
                if (pageSize <= 0 || pageSize > 100)
                {
                    _logger.LogWarning("Invalid page size: {PageSize}. Using default value 20", pageSize);
                    pageSize = 20;
                }

                if (pageNumber <= 0)
                {
                    _logger.LogWarning("Invalid page number: {PageNumber}. Using default value 1", pageNumber);
                    pageNumber = 1;
                }

                var methodRaw = await _paymentMethodCatalog.ResolveRawForFilterAsync(paymentMethod);

                var (items, totalCount) = await _paymentRepository.GetPagedAsync(
                    pageNumber,
                    pageSize,
                    p => p.PaymentMethodRaw == methodRaw && p.IsActive,
                    p => p.CreatedAt,
                    false);

                _logger.LogDebug("Retrieved {Count} payments for method {PaymentMethod} (page {PageNumber}/{TotalPages})",
                    items.Count(), paymentMethod, pageNumber, Math.Ceiling((double)totalCount / pageSize));

                return items;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payments by method {PaymentMethod}", paymentMethod);
                return Enumerable.Empty<PaymentDetails>();
            }
        }

        /// <summary>
        /// Tarih aralığına göre ödemeleri getir
        /// </summary>
        public async Task<IEnumerable<PaymentDetails>> GetPaymentsByDateRangeAsync(DateTime startDate, DateTime endDate, int pageNumber = 1, int pageSize = 20)
        {
            try
            {
                // Tarih validasyonu
                if (startDate > endDate)
                {
                    _logger.LogWarning("Invalid date range: startDate {StartDate} is after endDate {EndDate}", startDate, endDate);
                    return Enumerable.Empty<PaymentDetails>();
                }

                // Maksimum tarih aralığı kontrolü (7 yıl)
                var maxDateRange = TimeSpan.FromDays(365 * 7);
                if (endDate - startDate > maxDateRange)
                {
                    _logger.LogWarning("Date range too large: {Days} days. Maximum allowed: {MaxDays} days",
                        (endDate - startDate).Days, maxDateRange.Days);
                    return Enumerable.Empty<PaymentDetails>();
                }

                // Sayfa boyutu validasyonu
                if (pageSize <= 0 || pageSize > 100)
                {
                    _logger.LogWarning("Invalid page size: {PageSize}. Using default value 20", pageSize);
                    pageSize = 20;
                }

                if (pageNumber <= 0)
                {
                    _logger.LogWarning("Invalid page number: {PageNumber}. Using default value 1", pageNumber);
                    pageNumber = 1;
                }

                var (fromUtc, toExclusiveUtc) =
                    PostgreSqlUtcDateTime.AustriaInclusiveCalendarRangeUtc(startDate, endDate);

                var (items, totalCount) = await _paymentRepository.GetPagedAsync(
                    pageNumber,
                    pageSize,
                    p => p.CreatedAt >= fromUtc && p.CreatedAt < toExclusiveUtc && p.IsActive,
                    p => p.CreatedAt,
                    false);

                _logger.LogDebug("Retrieved {Count} payments for date range {StartDate} to {EndDate} (page {PageNumber}/{TotalPages})",
                    items.Count(), startDate, endDate, pageNumber, Math.Ceiling((double)totalCount / pageSize));

                return items;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payments by date range {StartDate} to {EndDate}", startDate, endDate);
                return Enumerable.Empty<PaymentDetails>();
            }
        }

        /// <summary>
        /// Get payment statistics for date range
        /// </summary>
        public async Task<PaymentStatistics> GetPaymentStatisticsAsync(DateTime startDate, DateTime endDate)
        {
            // Initialize DTO with request dates immediately (never returns DateTime.MinValue)
            var statistics = new PaymentStatistics
            {
                StartDate = startDate,
                EndDate = endDate,
                TotalPayments = 0,
                TotalAmount = 0,
                AverageAmount = 0,
                PaymentsByMethod = new Dictionary<string, int>(),
                AmountByMethod = new Dictionary<string, decimal>(),
                PaymentsByTaxType = new Dictionary<string, int>(),
                TseSignedPayments = 0,
                TseSignedAmount = 0
            };

            var (fromUtc, toExclusiveUtc) =
                PostgreSqlUtcDateTime.AustriaInclusiveCalendarRangeUtc(startDate, endDate);

            var effectiveTenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();
            // Query using PaymentMethodRaw - no InvalidCastException since it's varchar
            var payments = await _context.PaymentDetails
                .Where(p => p.CreatedAt >= fromUtc && p.CreatedAt < toExclusiveUtc && p.IsActive)
                .Where(p => _context.CashRegisters.ForResolvedTenantScope().Any(cr => cr.Id == p.CashRegisterId && cr.TenantId == effectiveTenantId))
                .ToListAsync();

            // Diagnostic log to confirm PaymentMethodRaw reads as string
            if (payments.Any())
            {
                var firstPayment = payments.First();
                _logger.LogInformation("DIAGNOSTIC: PaymentMethodRaw type={Type}, value={Value}",
                    firstPayment.PaymentMethodRaw?.GetType().Name ?? "null",
                    firstPayment.PaymentMethodRaw ?? "null");
            }

            if (!payments.Any())
            {
                _logger.LogInformation("No payments found for period {StartDate} to {EndDate}", startDate, endDate);
                return statistics;
            }

            // Calculate basic statistics
            statistics.TotalPayments = payments.Count;
            statistics.TotalAmount = payments.Sum(p => p.TotalAmount);
            statistics.AverageAmount = payments.Average(p => p.TotalAmount);

            // Group by payment method - parse varchar numeric strings to enum names
            var paymentMethodGroups = payments
                .Select(p => new
                {
                    Payment = p,
                    MethodName = ParsePaymentMethodName(p.PaymentMethodRaw)
                })
                .GroupBy(x => x.MethodName)
                .ToList();

            statistics.PaymentsByMethod = paymentMethodGroups
                .ToDictionary(g => g.Key, g => g.Count());

            statistics.AmountByMethod = paymentMethodGroups
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Payment.TotalAmount));

            // Group by tax type (safe JSON parsing)
            foreach (var payment in payments)
            {
                try
                {
                    if (payment.PaymentItems != null && payment.PaymentItems.RootElement.ValueKind != JsonValueKind.Undefined)
                    {
                        var jsonText = payment.PaymentItems.RootElement.GetRawText();
                        var paymentItems = JsonSerializer.Deserialize<List<PaymentItem>>(jsonText);

                        if (paymentItems != null)
                        {
                            foreach (var item in paymentItems)
                            {
                                var taxType = item.TaxType.ToString();
                                if (!statistics.PaymentsByTaxType.ContainsKey(taxType))
                                    statistics.PaymentsByTaxType[taxType] = 0;
                                statistics.PaymentsByTaxType[taxType]++;
                            }
                        }
                    }
                }
                catch (Exception jsonEx)
                {
                    _logger.LogWarning(jsonEx, "Failed to deserialize payment items for payment {PaymentId}", payment.Id);
                }
            }

            // TSE statistics
            var tsePayments = payments.Where(p => !string.IsNullOrEmpty(p.TseSignature)).ToList();
            statistics.TseSignedPayments = tsePayments.Count;
            statistics.TseSignedAmount = tsePayments.Sum(p => p.TotalAmount);

            _logger.LogInformation("Payment statistics: {StartDate} to {EndDate}, {Count} payments, {Amount} total",
                startDate, endDate, statistics.TotalPayments, statistics.TotalAmount);

            return statistics;
            // No try-catch - exceptions propagate to controller for proper 500 response
        }

        /// <summary>
        /// Parse varchar payment method string ('0', '1', etc.) to readable enum name
        /// </summary>
        private static string ParsePaymentMethodName(string rawValue)
        {
            if (int.TryParse(rawValue, out int methodInt) && Enum.IsDefined(typeof(PaymentMethod), methodInt))
            {
                return ((PaymentMethod)methodInt).ToString();
            }
            return "Unknown";
        }

        /// <summary>
        /// True when the payment's cash register belongs to the effective tenant (settings snapshot).
        /// </summary>
        private async Task<bool> PaymentBelongsToEffectiveTenantAsync(PaymentDetails payment, CancellationToken cancellationToken = default)
        {
            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
            return await _context.CashRegisters.AsNoTracking().ForResolvedTenantScope()
                .AnyAsync(cr => cr.Id == payment.CashRegisterId && cr.TenantId == tenantId, cancellationToken);
        }
    }
}
