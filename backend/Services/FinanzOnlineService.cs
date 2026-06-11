using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Security.Cryptography;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services
{
    public class FinanzOnlineService : IFinanzOnlineService
    {
        private readonly AppDbContext _context;
        private readonly IFinanzOnlineOutboxService _outboxService;
        private readonly IOptionsMonitor<FinanzOnlineCutoverGuardOptions> _cutoverOptions;
        private readonly IHostEnvironment _hostEnvironment;
        private readonly ILogger<FinanzOnlineService> _logger;
        private readonly IFinanzOnlineAdminConnectivityService _adminConnectivity;
        private readonly ISettingsTenantResolver _settingsTenantResolver;

        public FinanzOnlineService(
            AppDbContext context,
            IFinanzOnlineOutboxService outboxService,
            IOptionsMonitor<FinanzOnlineCutoverGuardOptions> cutoverOptions,
            IHostEnvironment hostEnvironment,
            ILogger<FinanzOnlineService> logger,
            IFinanzOnlineAdminConnectivityService adminConnectivity,
            ISettingsTenantResolver settingsTenantResolver)
        {
            _context = context;
            _outboxService = outboxService;
            _cutoverOptions = cutoverOptions;
            _hostEnvironment = hostEnvironment;
            _logger = logger;
            _adminConnectivity = adminConnectivity;
            _settingsTenantResolver = settingsTenantResolver;
        }

        public async Task<bool> IsEnabledAsync()
        {
            try
            {
                var companySettings = await GetCompanySettingsForEffectiveTenantAsync().ConfigureAwait(false);
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
                var companySettings = await GetCompanySettingsForEffectiveTenantAsync().ConfigureAwait(false);
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
                var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync().ConfigureAwait(false);
                var companySettings = await _context.CompanySettings
                    .FirstOrDefaultAsync(c => c.TenantId == tenantId)
                    .ConfigureAwait(false);
                if (companySettings == null)
                {
                    companySettings = new CompanySettings { TenantId = tenantId };
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
                        Status = "Disabled",
                        IsConnected = false,
                        ErrorMessage = "FinanzOnline disabled in company settings."
                    };
                }

                var companySettings = await GetCompanySettingsForEffectiveTenantAsync().ConfigureAwait(false);
                var lastSync = companySettings?.LastFinanzOnlineSync ?? DateTime.UtcNow;

                var tseDevice = await _context.TseDevices
                    .AsNoTracking()
                    .Where(t => t.IsActive && t.FinanzOnlineEnabled)
                    .FirstOrDefaultAsync()
                    .ConfigureAwait(false);

                if (tseDevice == null)
                {
                    return new FinanzOnlineStatus
                    {
                        IsEnabled = true,
                        IsConnected = false,
                        Status = "NoActiveDevice",
                        LastConnectionTime = lastSync,
                        LastSubmissionTime = lastSync,
                        PendingSubmissions = companySettings?.PendingInvoices ?? 0,
                        FailedSubmissions = 0,
                        ErrorMessage = "No active TSE with FinanzOnline enabled; use GET /api/FinanzOnline/status for diagnostics."
                    };
                }

                var snap = await _adminConnectivity
                    .BuildStatusAsync(tseDevice, CancellationToken.None)
                    .ConfigureAwait(false);

                return new FinanzOnlineStatus
                {
                    IsEnabled = true,
                    IsConnected = snap.IsConnected,
                    Status = snap.IsAuthoritative
                        ? (snap.IsConnected ? "Connected" : "Disconnected")
                        : (snap.FinanzOnlineTransportsSimulated ? "Simulated" : "ProbePending"),
                    LastConnectionTime = lastSync,
                    LastSubmissionTime = lastSync,
                    PendingSubmissions = snap.PendingInvoices,
                    FailedSubmissions = 0,
                    ErrorMessage = snap.ErrorMessage ?? string.Empty
                };
            }
            catch
            {
                return new FinanzOnlineStatus
                {
                    IsEnabled = false,
                    Status = "Error",
                    IsConnected = false
                };
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var config = await GetConfigAsync().ConfigureAwait(false);
                if (string.IsNullOrEmpty(config.ApiUrl) || string.IsNullOrEmpty(config.Username))
                {
                    return false;
                }

                var tseDevice = await _context.TseDevices
                    .AsNoTracking()
                    .Where(t => t.IsActive && t.FinanzOnlineEnabled)
                    .FirstOrDefaultAsync()
                    .ConfigureAwait(false);

                if (tseDevice == null)
                {
                    return false;
                }

                var test = await _adminConnectivity
                    .RunTestConnectionAsync(tseDevice, CancellationToken.None)
                    .ConfigureAwait(false);

                return test.Success;
            }
            catch
            {
                return false;
            }
        }

        public async Task<FinanzOnlineSubmitResponse> SubmitInvoiceAsync(Invoice invoice)
        {
            var submittedAt = DateTime.UtcNow;
            var requestPayload = JsonSerializer.Serialize(new { invoice.Id, invoice.InvoiceNumber, invoice.CashRegisterId });
            var submission = new FinanzOnlineSubmission
            {
                Id = Guid.NewGuid(),
                InvoiceId = invoice.Id,
                SubmittedAt = submittedAt,
                RequestPayloadJson = requestPayload,
                ResponseStatusCode = "",
                ResponseBodyJson = "{}",
                Success = false,
                ErrorMessage = null
            };

            try
            {
                var config = await GetConfigAsync().ConfigureAwait(false);
                var payloadHash = ComputeSha256Hex(requestPayload);
                var mode = ResolveMode(config.Environment);
                var businessKey = string.IsNullOrWhiteSpace(invoice.InvoiceNumber) ? invoice.Id.ToString("N") : invoice.InvoiceNumber;
                var rkdbBelegpruefung = await TryResolveRkdbBelegpruefungAsync(invoice).ConfigureAwait(false);
                var request = new FinanzOnlineRegisterSubmissionRequest
                {
                    Mode = mode,
                    Scope = new FinanzOnlineScope
                    {
                        RegisterId = invoice.KassenId ?? invoice.CashRegisterId.ToString("N")
                    },
                    Correlation = new FinanzOnlineCorrelationContext
                    {
                        BusinessKey = businessKey,
                        PayloadHash = payloadHash,
                        CorrelationId = invoice.Id.ToString("N")
                    },
                    SubmissionKind = FinanzOnlineSubmissionKind.Register,
                    PayloadJson = requestPayload,
                    RkdbBelegpruefung = rkdbBelegpruefung
                };

                await _outboxService.EnqueueSubmissionAsync(
                    aggregateType: "Invoice",
                    aggregateId: invoice.Id,
                    messageType: "RegistrierkassenSubmission",
                    businessKey: businessKey,
                    payload: new FinanzOnlineOutboxPayload
                    {
                        Mode = mode,
                        Scope = request.Scope,
                        Correlation = request.Correlation,
                        SubmissionKind = request.SubmissionKind,
                        PayloadJson = request.PayloadJson,
                        RkdbBelegpruefung = rkdbBelegpruefung
                    }).ConfigureAwait(false);

                var companySettings = await GetCompanySettingsForEffectiveTenantAsync().ConfigureAwait(false);
                if (companySettings != null)
                {
                    companySettings.LastFinanzOnlineSync = submittedAt;
                    companySettings.PendingInvoices = Math.Max(0, (companySettings.PendingInvoices ?? 0) - 1);
                }

                submission.Success = false;
                submission.ResponseStatusCode = "202";
                submission.ResponseBodyJson = JsonSerializer.Serialize(new
                {
                    status = "Queued",
                    queuedAt = submittedAt
                });
                submission.ErrorMessage = null;
                _context.FinanzOnlineSubmissions.Add(submission);
                await _context.SaveChangesAsync();

                return new FinanzOnlineSubmitResponse
                {
                    Success = false,
                    ReferenceId = null,
                    Status = "Pending",
                    SubmittedAt = submittedAt,
                    ErrorMessage = "Queued for asynchronous delivery.",
                    FailureKind = FinanzOnlineFailureKind.Transient
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

        private async Task<CompanySettings?> GetCompanySettingsForEffectiveTenantAsync(
            CancellationToken cancellationToken = default)
        {
            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken)
                .ConfigureAwait(false);
            return await _context.CompanySettings
                .FirstOrDefaultAsync(c => c.TenantId == tenantId, cancellationToken)
                .ConfigureAwait(false);
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

        private FinanzOnlineIntegrationMode ResolveMode(string? mode)
        {
            var requestedProd =
                string.Equals(mode, "Production", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, "Prod", StringComparison.OrdinalIgnoreCase);
            if (!requestedProd)
                return FinanzOnlineIntegrationMode.TEST;

            var guard = _cutoverOptions.CurrentValue;
            var approved = guard.AllowProdMode &&
                           (!guard.RequireExplicitProdApproval || !string.IsNullOrWhiteSpace(guard.ProdApprovalToken));
            if (!approved)
            {
                _logger.LogWarning(
                    "FinanzOnline PROD mode request blocked by cutover guard. Environment={EnvironmentName}",
                    _hostEnvironment.EnvironmentName);
                throw new InvalidOperationException("PROD mode is blocked by cutover guard configuration.");
            }

            _logger.LogWarning(
                "FinanzOnline PROD mode enabled by explicit cutover guard. Environment={EnvironmentName}",
                _hostEnvironment.EnvironmentName);
            return FinanzOnlineIntegrationMode.PROD;
        }

        private static string ComputeSha256Hex(string payload)
        {
            var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload ?? string.Empty));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        public async Task<FinanzOnlineSubmitResponse> SubmitDailyClosingAsync(DailyClosing dailyClosing)
        {
            try
            {
                // Simulate FinanzOnline daily closing submission (Vienna business date label, not UTC calendar day of stored instant).
                var referenceId =
                    $"FIN_DAILY_{PostgreSqlUtcDateTime.FormatViennaUtcInstantAsYyyyMmDd(dailyClosing.ClosingDate)}_{dailyClosing.CashRegisterId}";
                
                // Update company settings
                var companySettings = await GetCompanySettingsForEffectiveTenantAsync().ConfigureAwait(false);
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
                var companySettings = await GetCompanySettingsForEffectiveTenantAsync().ConfigureAwait(false);
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
                var companySettings = await GetCompanySettingsForEffectiveTenantAsync().ConfigureAwait(false);
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
                var companySettings = await GetCompanySettingsForEffectiveTenantAsync().ConfigureAwait(false);
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

        /// <summary>
        /// Receipt QrCodePayload includes trailing compact JWS, so it does not match the RKDB beleg DEP segment pattern alone.
        /// </summary>
        private async Task<FinanzOnlineRkdbBelegpruefungCommand?> TryResolveRkdbBelegpruefungAsync(Invoice invoice)
        {
            if (!invoice.SourcePaymentId.HasValue)
                return null;

            var receipt = await _context.Set<Receipt>()
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.PaymentId == invoice.SourcePaymentId.Value)
                .ConfigureAwait(false);
            if (receipt == null)
                return null;

            var candidate = receipt.QrCodePayload?.Trim();
            if (!FinanzOnlineRkdbBelegpruefungValidator.IsValidDepCandidate(candidate))
                return null;

            return new FinanzOnlineRkdbBelegpruefungCommand
            {
                Beleg = candidate!,
                PaketNr = 1,
                SatzNr = 1,
                TsErstellungUtc = new DateTimeOffset(DateTime.SpecifyKind(receipt.IssuedAt, DateTimeKind.Utc))
            };
        }
    }
}
