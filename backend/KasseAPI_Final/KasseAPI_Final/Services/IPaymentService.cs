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
        Task<PaymentResult> CreatePaymentAsync(CreatePaymentRequest request, string userId);
        
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
        /// Ödeme iptal et
        /// </summary>
        Task<PaymentResult> CancelPaymentAsync(Guid paymentId, string reason, string userId);
        
        /// <summary>
        /// Ödeme iade et
        /// </summary>
        Task<PaymentResult> RefundPaymentAsync(Guid paymentId, decimal amount, string reason, string userId);
        
        /// <summary>
        /// Ödeme istatistiklerini getir
        /// </summary>
        Task<PaymentStatistics> GetPaymentStatisticsAsync(DateTime startDate, DateTime endDate);
        
        /// <summary>
        /// Get formatted receipt data for payment
        /// </summary>
        Task<ReceiptDTO?> GetReceiptDataAsync(Guid paymentId);
        
        /// <summary>
        /// TSE imzası oluştur
        /// </summary>
        Task<string> GenerateTseSignatureAsync(PaymentDetails payment);
        
        /// <summary>
        /// FinanzOnline entegrasyonu
        /// </summary>
        Task<bool> SendToFinanzOnlineAsync(PaymentDetails payment);
    }
}
