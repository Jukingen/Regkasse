using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;

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
            try
            {
                // Simulate FinanzOnline submission
                var referenceId = $"FIN_{DateTime.UtcNow:yyyyMMddHHmmss}_{invoice.Id}";
                
                // Update company settings
                var companySettings = await _context.CompanySettings.FirstOrDefaultAsync();
                if (companySettings != null)
                {
                    companySettings.LastFinanzOnlineSync = DateTime.UtcNow;
                    companySettings.PendingInvoices = Math.Max(0, (companySettings.PendingInvoices ?? 0) - 1);
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

        public async Task<FinanzOnlineSubmitResponse> SubmitDailyClosingAsync(DailyClosing dailyClosing)
        {
            try
            {
                // Simulate FinanzOnline daily closing submission
                var referenceId = $"FIN_DAILY_{dailyClosing.ClosingDate:yyyyMMdd}_{dailyClosing.CashRegisterId}";
                
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
                // Simulate FinanzOnline monthly closing submission
                var referenceId = $"FIN_MONTHLY_{monthlyClosing.ClosingDate:yyyyMM}_{monthlyClosing.CashRegisterId}";
                
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
                // Simulate FinanzOnline yearly closing submission
                var referenceId = $"FIN_YEARLY_{yearlyClosing.ClosingDate:yyyy}_{yearlyClosing.CashRegisterId}";
                
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

                if (fromDate.HasValue)
                    query = query.Where(e => e.OccurredAt >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(e => e.OccurredAt <= toDate.Value);

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
