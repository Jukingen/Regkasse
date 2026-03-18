using KasseAPI_Final.Models;
using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services
{
    /// <summary>
    /// Ödeme işlemleri için service interface
    /// </summary>
    public interface IPaymentService
    {
        /// <summary>
        /// Yeni ödeme oluştur
        /// </summary>
        Task<PaymentResult> CreatePaymentAsync(CreatePaymentRequest request, string userId, Guid? offlineTransactionId = null);
        
        /// <summary>
        /// Ödeme detaylarını getir
        /// </summary>
        Task<PaymentDetails?> GetPaymentAsync(Guid paymentId);
        
        /// <summary>
        /// Müşteri ödemelerini getir
        /// </summary>
        Task<IEnumerable<PaymentDetails>> GetCustomerPaymentsAsync(Guid customerId, int pageNumber = 1, int pageSize = 20);
        
        /// <summary>
        /// Ödeme yöntemine göre ödemeleri getir
        /// </summary>
        Task<IEnumerable<PaymentDetails>> GetPaymentsByMethodAsync(string paymentMethod, int pageNumber = 1, int pageSize = 20);
        
        /// <summary>
        /// Tarih aralığına göre ödemeleri getir
        /// </summary>
        Task<IEnumerable<PaymentDetails>> GetPaymentsByDateRangeAsync(DateTime startDate, DateTime endDate, int pageNumber = 1, int pageSize = 20);
        
        /// <summary>
        /// Ödeme iptal et. Sprint 6: optional idempotencyKey for retry-safe cancel.
        /// </summary>
        Task<PaymentResult> CancelPaymentAsync(Guid paymentId, string reason, string userId, string? idempotencyKey = null);
        
        /// <summary>
        /// Ödeme iade et. Sprint 6: optional idempotencyKey for retry-safe refund (no duplicate BelegNr/stock).
        /// </summary>
        Task<PaymentResult> RefundPaymentAsync(Guid paymentId, decimal amount, string reason, string userId, string? idempotencyKey = null);
        
        /// <summary>
        /// Ödeme istatistiklerini getir
        /// </summary>
        Task<PaymentStatistics> GetPaymentStatisticsAsync(DateTime startDate, DateTime endDate);
        
        /// <summary>
        /// Get formatted receipt data for payment. When userId is provided, audit is written for ReceiptGenerated or ReceiptReprinted.
        /// </summary>
        Task<ReceiptDTO?> GetReceiptDataAsync(Guid paymentId, string? userId = null);
        
        /// <summary>
        /// TSE imzası oluştur
        /// </summary>
        Task<string> GenerateTseSignatureAsync(PaymentDetails payment);
        
        /// <summary>
        /// FinanzOnline entegrasyonu
        /// </summary>
        Task<bool> SendToFinanzOnlineAsync(PaymentDetails payment);

        /// <summary>
        /// Ödeme için QR payload (RKSV/NON_FISCAL) üretir. DB'de saklanmaz, her çağrıda hesaplanır.
        /// </summary>
        Task<(string? QrPayload, DateTime? UpdatedAt)?> GetQrPayloadForPaymentAsync(Guid paymentId);

        /// <summary>
        /// Read-only eligibility preview for POS: which benefits would apply for this customer and cart, and which are blocked and why.
        /// Does not persist anything (no BenefitDailyUsage write, no payment). Distinct from assignment summary (count only).
        /// </summary>
        Task<BenefitEligibilityPreviewResponse?> ComputeBenefitEligibilityPreviewAsync(BenefitEligibilityPreviewRequest request);
    }
}
