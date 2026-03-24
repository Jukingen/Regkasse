using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services
{
    public class FinanzOnlineService : IFinanzOnlineService
    {
        private readonly AppDbContext _context;

        public FinanzOnlineService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> IsEnabledAsync()
        {
            try
            {
                var companySettings = await _context.CompanySettings.FirstOrDefaultAsync();
                return companySettings?.FinanzOnlineEnabled ?? false;
            }
            catch
            {
                return false;
            }
        }

        public async Task<FinanzOnlineConfig> GetConfigAsync()
        {
            try
            {
                var companySettings = await _context.CompanySettings.FirstOrDefaultAsync();
                if (companySettings == null)
                {
                    return new FinanzOnlineConfig();
                }

                return new FinanzOnlineConfig
                {
                    ApiUrl = companySettings.FinanzOnlineApiUrl ?? "",
                    Username = companySettings.FinanzOnlineUsername ?? "",
                    Password = companySettings.FinanzOnlinePassword ?? "",
                    AutoSubmit = companySettings.FinanzOnlineAutoSubmit,
                    SubmitIntervalMinutes = companySettings.FinanzOnlineSubmitInterval,
                    MaxRetryAttempts = companySettings.FinanzOnlineRetryAttempts,
                    EnableValidation = companySettings.FinanzOnlineEnableValidation
                };
            }
            catch
            {
                return new FinanzOnlineConfig();
            }
        }

        public async Task<bool> UpdateConfigAsync(FinanzOnlineConfig config)
        {
            try
            {
                var companySettings = await _context.CompanySettings.FirstOrDefaultAsync();
                if (companySettings == null)
                {
                    companySettings = new CompanySettings();
                    _context.CompanySettings.Add(companySettings);
                }

                companySettings.FinanzOnlineApiUrl = config.ApiUrl;
                companySettings.FinanzOnlineUsername = config.Username;
                companySettings.FinanzOnlinePassword = config.Password;
                companySettings.FinanzOnlineAutoSubmit = config.AutoSubmit;
                companySettings.FinanzOnlineSubmitInterval = config.SubmitIntervalMinutes;
                companySettings.FinanzOnlineRetryAttempts = config.MaxRetryAttempts;
                companySettings.FinanzOnlineEnableValidation = config.EnableValidation;

                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<FinanzOnlineStatus> GetStatusAsync()
        {
            try
            {
                var isEnabled = await IsEnabledAsync();
                if (!isEnabled)
                {
                    return new FinanzOnlineStatus
                    {
                        IsEnabled = false,
                        Status = "Disabled"
                    };
                }

                var companySettings = await _context.CompanySettings.FirstOrDefaultAsync();
                var lastSync = companySettings?.LastFinanzOnlineSync;

                return new FinanzOnlineStatus
                {
                    IsEnabled = true,
                    IsConnected = true, // Simulate connection
                    Status = "Connected",
                    LastConnectionTime = lastSync ?? DateTime.UtcNow,
                    LastSubmissionTime = lastSync ?? DateTime.UtcNow,
                    PendingSubmissions = companySettings?.PendingInvoices ?? 0,
                    FailedSubmissions = 0
                };
            }
            catch
            {
                return new FinanzOnlineStatus
                {
                    IsEnabled = false,
                    Status = "Error"
                };
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var config = await GetConfigAsync();
                if (string.IsNullOrEmpty(config.ApiUrl) || string.IsNullOrEmpty(config.Username))
                {
                    return false;
                }

                // Simulate connection test
                await Task.Delay(1000);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<FinanzOnlineSubmitResponse> SubmitInvoiceAsync(Invoice invoice)
        {
            var submittedAt = DateTime.UtcNow;
            var submission = new FinanzOnlineSubmission
            {
                Id = Guid.NewGuid(),
                InvoiceId = invoice.Id,
                SubmittedAt = submittedAt,
                RequestPayloadJson = JsonSerializer.Serialize(new { invoice.Id, invoice.InvoiceNumber, invoice.CashRegisterId }),
                ResponseStatusCode = "",
                ResponseBodyJson = "{}",
                Success = false,
                ErrorMessage = null
            };

            try
            {
                // Simulate FinanzOnline submission (replace with real API call in production)
                var referenceId = $"FIN_{submittedAt:yyyyMMddHHmmss}_{invoice.Id:N}";

                var companySettings = await _context.CompanySettings.FirstOrDefaultAsync();
                if (companySettings != null)
                {
                    companySettings.LastFinanzOnlineSync = submittedAt;
                    companySettings.PendingInvoices = Math.Max(0, (companySettings.PendingInvoices ?? 0) - 1);
                }

                submission.Success = true;
                submission.ResponseStatusCode = "200";
                submission.ResponseBodyJson = JsonSerializer.Serialize(new { referenceId, status = "Submitted" });
                _context.FinanzOnlineSubmissions.Add(submission);
                await _context.SaveChangesAsync();

                return new FinanzOnlineSubmitResponse
                {
                    Success = true,
                    ReferenceId = referenceId,
                    Status = "Submitted",
                    SubmittedAt = submittedAt,
                    FailureKind = FinanzOnlineFailureKind.None
                };
            }
            catch (Exception ex)
            {
                var failureKind = ClassifyFailure(ex);
                submission.ErrorMessage = TruncateErrorMessage(ex.Message, 500);
                submission.ResponseStatusCode = "0";
                submission.ResponseBodyJson = JsonSerializer.Serialize(new { error = submission.ErrorMessage, failureKind = failureKind.ToString() });
                _context.FinanzOnlineSubmissions.Add(submission);

                var errorRecord = new FinanzOnlineError
                {
                    Id = Guid.NewGuid(),
                    ErrorType = "Submission",
                    ErrorMessage = submission.ErrorMessage,
                    ReferenceId = invoice.Id.ToString(),
                    OccurredAt = submittedAt,
                    InvoiceNumber = invoice.InvoiceNumber,
                    CashRegisterId = invoice.CashRegisterId,
                    RetryCount = 0,
                    Status = "Active"
                };
                _context.FinanzOnlineErrors.Add(errorRecord);
                await _context.SaveChangesAsync();

                return new FinanzOnlineSubmitResponse
                {
                    Success = false,
                    ErrorMessage = submission.ErrorMessage,
                    Status = "Failed",
                    SubmittedAt = submittedAt,
                    FailureKind = failureKind
                };
            }
        }

        /// <summary>Classify failure for retry/alerting: Transient (retry), Permanent (do not retry), Unknown.</summary>
        private static FinanzOnlineFailureKind ClassifyFailure(Exception ex)
        {
            if (ex is HttpRequestException || ex is TaskCanceledException || ex is OperationCanceledException)
                return FinanzOnlineFailureKind.Transient;
            var msg = (ex.Message ?? "").ToLowerInvariant();
            if (msg.Contains("duplicate") || msg.Contains("already submitted") || msg.Contains("validation") || msg.Contains("forbidden"))
                return FinanzOnlineFailureKind.Permanent;
            return FinanzOnlineFailureKind.Unknown;
        }

        private static string TruncateErrorMessage(string message, int maxLen)
        {
            if (string.IsNullOrEmpty(message)) return "";
            return message.Length <= maxLen ? message : message.Substring(0, maxLen - 3) + "...";
        }

        public async Task<FinanzOnlineSubmitResponse> SubmitDailyClosingAsync(DailyClosing dailyClosing)
        {
            try
            {
                // Simulate FinanzOnline daily closing submission (Vienna business date label, not UTC calendar day of stored instant).
                var referenceId =
                    $"FIN_DAILY_{PostgreSqlUtcDateTime.FormatViennaUtcInstantAsYyyyMmDd(dailyClosing.ClosingDate)}_{dailyClosing.CashRegisterId}";
                
                // Update company settings
                var companySettings = await _context.CompanySettings.FirstOrDefaultAsync();
                if (companySettings != null)
                {
                    companySettings.LastFinanzOnlineSync = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }

                return new FinanzOnlineSubmitResponse
                {
                    Success = true,
                    ReferenceId = referenceId,
                    Status = "Submitted",
                    SubmittedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                return new FinanzOnlineSubmitResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Status = "Failed",
                    SubmittedAt = DateTime.UtcNow
                };
            }
        }

        public async Task<FinanzOnlineSubmitResponse> SubmitMonthlyClosingAsync(DailyClosing monthlyClosing)
        {
            try
            {
                // Simulate FinanzOnline monthly closing submission (Vienna local year-month, aligned with daily/yearly helpers).
                var referenceId =
                    $"FIN_MONTHLY_{PostgreSqlUtcDateTime.FormatViennaUtcInstantAsYyyyMm(monthlyClosing.ClosingDate)}_{monthlyClosing.CashRegisterId}";
                
                return new FinanzOnlineSubmitResponse
                {
                    Success = true,
                    ReferenceId = referenceId,
                    Status = "Submitted",
                    SubmittedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                return new FinanzOnlineSubmitResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Status = "Failed",
                    SubmittedAt = DateTime.UtcNow
                };
            }
        }

        public async Task<FinanzOnlineSubmitResponse> SubmitYearlyClosingAsync(DailyClosing yearlyClosing)
        {
            try
            {
                // Simulate FinanzOnline yearly closing submission (Vienna local year).
                var referenceId =
                    $"FIN_YEARLY_{PostgreSqlUtcDateTime.FormatViennaUtcInstantAsYyyy(yearlyClosing.ClosingDate)}_{yearlyClosing.CashRegisterId}";
                
                return new FinanzOnlineSubmitResponse
                {
                    Success = true,
                    ReferenceId = referenceId,
                    Status = "Submitted",
                    SubmittedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                return new FinanzOnlineSubmitResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Status = "Failed",
                    SubmittedAt = DateTime.UtcNow
                };
            }
        }

        public async Task<List<FinanzOnlineError>> GetErrorsAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                var query = _context.FinanzOnlineErrors.AsQueryable();

                // Calendar-day half-open bounds for error timestamps (same semantics as other admin date filters).
                var (lo, hi) = PostgreSqlUtcDateTime.CalendarHalfOpenInstantBounds(fromDate, toDate);
                if (lo.HasValue)
                    query = query.Where(e => e.OccurredAt >= lo.Value);
                if (hi.HasValue)
                    query = query.Where(e => e.OccurredAt < hi.Value);

                var errors = await query
                    .OrderByDescending(e => e.OccurredAt)
                    .Take(100)
                    .ToListAsync();

                return errors;
            }
            catch
            {
                return new List<FinanzOnlineError>();
            }
        }

        public async Task<bool> EnableAutoSubmitAsync(bool enabled)
        {
            try
            {
                var companySettings = await _context.CompanySettings.FirstOrDefaultAsync();
                if (companySettings != null)
                {
                    companySettings.FinanzOnlineAutoSubmit = enabled;
                    await _context.SaveChangesAsync();
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UpdateRetrySettingsAsync(int maxRetries, int retryDelayMinutes)
        {
            try
            {
                var companySettings = await _context.CompanySettings.FirstOrDefaultAsync();
                if (companySettings != null)
                {
                    companySettings.FinanzOnlineRetryAttempts = maxRetries;
                    // Note: retryDelayMinutes would need to be added to CompanySettings model
                    await _context.SaveChangesAsync();
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UpdateValidationSettingsAsync(bool enableValidation)
        {
            try
            {
                var companySettings = await _context.CompanySettings.FirstOrDefaultAsync();
                if (companySettings != null)
                {
                    companySettings.FinanzOnlineEnableValidation = enableValidation;
                    await _context.SaveChangesAsync();
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
