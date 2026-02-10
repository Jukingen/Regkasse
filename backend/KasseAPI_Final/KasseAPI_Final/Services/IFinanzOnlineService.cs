using System;
using System.Threading.Tasks;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services
{
    public interface IFinanzOnlineService
    {
        Task<bool> IsEnabledAsync();
        Task<FinanzOnlineConfig> GetConfigAsync();
        Task<bool> UpdateConfigAsync(FinanzOnlineConfig config);
        Task<FinanzOnlineStatus> GetStatusAsync();
        Task<bool> TestConnectionAsync();
        Task<FinanzOnlineSubmitResponse> SubmitInvoiceAsync(Invoice invoice);
        Task<FinanzOnlineSubmitResponse> SubmitDailyClosingAsync(DailyClosing dailyClosing);
        Task<FinanzOnlineSubmitResponse> SubmitMonthlyClosingAsync(DailyClosing monthlyClosing);
        Task<FinanzOnlineSubmitResponse> SubmitYearlyClosingAsync(DailyClosing yearlyClosing);
        Task<List<FinanzOnlineError>> GetErrorsAsync(DateTime? fromDate = null, DateTime? toDate = null);
        Task<bool> EnableAutoSubmitAsync(bool enabled);
        Task<bool> UpdateRetrySettingsAsync(int maxRetries, int retryDelayMinutes);
        Task<bool> UpdateValidationSettingsAsync(bool enableValidation);
    }

    public class FinanzOnlineConfig
    {
        public string ApiUrl { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public bool AutoSubmit { get; set; } = false;
        public int SubmitIntervalMinutes { get; set; } = 60;
        public int MaxRetryAttempts { get; set; } = 3;
        public int RetryDelayMinutes { get; set; } = 5;
        public bool EnableValidation { get; set; } = true;
        public string Environment { get; set; } = "Test"; // Test, Production
    }

    public class FinanzOnlineStatus
    {
        public bool IsConnected { get; set; }
        public bool IsEnabled { get; set; }
        public string Status { get; set; } = string.Empty; // Connected, Disconnected, Error, Maintenance
        public DateTime LastConnectionTime { get; set; }
        public DateTime LastSubmissionTime { get; set; }
        public int PendingSubmissions { get; set; }
        public int FailedSubmissions { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public class FinanzOnlineSubmitResponse
    {
        public bool Success { get; set; }
        public string? ReferenceId { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime SubmittedAt { get; set; }
        public string Status { get; set; } = string.Empty; // Submitted, Failed, Pending
    }
}
