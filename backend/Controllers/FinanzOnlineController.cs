using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Localization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
using KasseAPI_Final.Services.Localization;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers
{
    /// <summary>
    /// FinanzOnline <b>configuration, connectivity diagnostics, and historical artifacts</b> (config, status probe, errors table, submission rows per invoice).
    /// Not the authoritative surface for per-message BMF pipeline state — use <see cref="FinanzOnlineOutboxAdminController"/> for that.
    /// </summary>
    [HasPermission(AppPermissions.SettingsView)]
    [ApiController]
    [Route("api/[controller]")]
    public class FinanzOnlineController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<FinanzOnlineController> _logger;
        private readonly IFinanzOnlineAdminConnectivityService _adminConnectivity;
        private readonly ISettingsTenantResolver _settingsTenantResolver;
        private readonly IApiMessageLocalizer _messages;

        public FinanzOnlineController(
            AppDbContext context,
            ILogger<FinanzOnlineController> logger,
            IFinanzOnlineAdminConnectivityService adminConnectivity,
            ISettingsTenantResolver settingsTenantResolver,
            IApiMessageLocalizer messages)
        {
            _context = context;
            _logger = logger;
            _adminConnectivity = adminConnectivity;
            _settingsTenantResolver = settingsTenantResolver;
            _messages = messages;
        }

        // GET: api/finanzonline/config
        [HttpGet("config")]
        [HasPermission(AppPermissions.FinanzOnlineView)]
        public async Task<ActionResult<FinanzOnlineConfigResponse>> GetConfig()
        {
            try
            {
                var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(
                    HttpContext?.RequestAborted ?? CancellationToken.None);
                var companySettings = await _context.CompanySettings
                    .FirstOrDefaultAsync(c => c.TenantId == tenantId, HttpContext?.RequestAborted ?? CancellationToken.None);
                var tseDevice = await _context.TseDevices
                    .Where(t => t.IsActive && t.FinanzOnlineEnabled)
                    .FirstOrDefaultAsync();

                if (companySettings == null)
                {
                    return NotFound(new { message = _messages.Get(ApiMessageKeys.CompanySettingsNotFound) });
                }

                return Ok(new FinanzOnlineConfigResponse
                {
                    ApiUrl = companySettings.FinanzOnlineApiUrl ?? "",
                    Username = companySettings.FinanzOnlineUsername ?? "",
                    AutoSubmit = companySettings.FinanzOnlineAutoSubmit,
                    SubmitInterval = companySettings.FinanzOnlineSubmitInterval,
                    RetryAttempts = companySettings.FinanzOnlineRetryAttempts,
                    EnableValidation = companySettings.FinanzOnlineEnableValidation,
                    IsEnabled = tseDevice?.FinanzOnlineEnabled ?? false
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FinanzOnline config fetch failed");
                return StatusCode(500, new { message = _messages.Get(ApiMessageKeys.FinanzOnlineConfigFetchError) });
            }
        }

        // PUT: api/finanzonline/config
        [HttpPut("config")]
        [HasPermission(AppPermissions.FinanzOnlineManage)]
        public async Task<ActionResult<FinanzOnlineConfigResponse>> UpdateConfig([FromBody] FinanzOnlineConfigRequest request)
        {
            try
            {
                var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(
                    HttpContext?.RequestAborted ?? CancellationToken.None);
                var companySettings = await _context.CompanySettings
                    .FirstOrDefaultAsync(c => c.TenantId == tenantId, HttpContext?.RequestAborted ?? CancellationToken.None);
                if (companySettings == null)
                {
                    return NotFound(new { message = _messages.Get(ApiMessageKeys.CompanySettingsNotFound) });
                }

                // Konfigürasyon güncelleme
                companySettings.FinanzOnlineApiUrl = request.ApiUrl;
                companySettings.FinanzOnlineUsername = request.Username;
                companySettings.FinanzOnlineAutoSubmit = request.AutoSubmit;
                companySettings.FinanzOnlineSubmitInterval = request.SubmitInterval;
                companySettings.FinanzOnlineRetryAttempts = request.RetryAttempts;
                companySettings.FinanzOnlineEnableValidation = request.EnableValidation;
                companySettings.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("FinanzOnline config updated by user");

                return Ok(new FinanzOnlineConfigResponse
                {
                    ApiUrl = companySettings.FinanzOnlineApiUrl ?? "",
                    Username = companySettings.FinanzOnlineUsername ?? "",
                    AutoSubmit = companySettings.FinanzOnlineAutoSubmit,
                    SubmitInterval = companySettings.FinanzOnlineSubmitInterval,
                    RetryAttempts = companySettings.FinanzOnlineRetryAttempts,
                    EnableValidation = companySettings.FinanzOnlineEnableValidation,
                    IsEnabled = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FinanzOnline config update failed");
                return StatusCode(500, new { message = _messages.Get(ApiMessageKeys.FinanzOnlineConfigUpdateError) });
            }
        }

        [HasPermission(AppPermissions.FinanzOnlineView)]
        [HttpGet("status")]
        public async Task<ActionResult<FinanzOnlineStatusResponse>> GetStatus()
        {
            try
            {
                var tseDevice = await _context.TseDevices
                    .Where(t => t.IsActive && t.FinanzOnlineEnabled)
                    .FirstOrDefaultAsync();

                if (tseDevice == null)
                {
                    return Ok(new FinanzOnlineStatusResponse
                    {
                        IsConnected = false,
                        ApiVersion = "",
                        LastSync = "",
                        PendingInvoices = 0,
                        PendingReports = 0,
                        ErrorMessage = "FinanzOnline etkin değil",
                        IsAuthoritative = false,
                        IsSimulated = false,
                        DiagnosticWarning =
                            "No active TSE device with FinanzOnline enabled — connectivity flags are not applicable."
                    });
                }

                var snapshot = await _adminConnectivity.BuildStatusAsync(tseDevice, HttpContext.RequestAborted);
                return Ok(ToStatusResponse(snapshot));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FinanzOnline status check failed");
                return StatusCode(500, new { message = "FinanzOnline durumu kontrol edilemedi" });
            }
        }

        // POST: api/finanzonline/submit-invoice
        // Deprecated legacy endpoint (simulated submit flow).
        // Primary operational model: outbox + normal payment submit (IFinanzOnlineService.SubmitInvoiceAsync).
        //   - GET  /api/admin/finanzonline-outbox
        // Legacy payment-row triage (until removed):
        //   - GET  /api/admin/finanzonline-reconciliation
        //   - POST /api/admin/finanzonline-reconciliation/retry/{paymentId}
        // Kept only for backward compatibility.
        [Obsolete("Deprecated endpoint. Use outbox pipeline (GET /api/admin/finanzonline-outbox) and normal fiscal submit; legacy payment list: GET /api/admin/finanzonline-reconciliation.")]
        [HttpPost("submit-invoice")]
        [HasPermission(AppPermissions.FinanzOnlineSubmit)]
        public async Task<ActionResult<FinanzOnlineSubmitResponse>> SubmitInvoice([FromBody] FinanzOnlineSubmitRequest request)
        {
            _logger.LogWarning(
                "Deprecated endpoint called: POST /api/FinanzOnline/submit-invoice. " +
                "Prefer GET /api/admin/finanzonline-outbox for SOAP/outbox state; legacy payment reconciliation: " +
                "GET /api/admin/finanzonline-reconciliation and POST .../retry/{{paymentId}}."
            );

            var submission = new FinanzOnlineSubmission
            {
                SubmittedAt = DateTime.UtcNow,
                RequestPayloadJson = JsonSerializer.Serialize(request)
            };

            try
            {
                // TODO: scope – tenant/branch if multi-tenant; submit is typically tenant-scoped.
                // Find invoice to link if possible (optional, but good for tracking)
                var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.InvoiceNumber == request.InvoiceNumber);
                if (invoice != null)
                    submission.InvoiceId = invoice.Id;

                var tseDevice = await _context.TseDevices
                    .Where(t => t.IsActive && t.FinanzOnlineEnabled && t.IsConnected)
                    .FirstOrDefaultAsync();

                if (tseDevice == null)
                {
                    submission.Success = false;
                    submission.ResponseStatusCode = "400";
                    submission.ErrorMessage = "FinanzOnline etkin değil veya TSE cihazı bağlı değil";
                    _context.FinanzOnlineSubmissions.Add(submission);
                    await _context.SaveChangesAsync();

                    return BadRequest(new { message = submission.ErrorMessage });
                }

                submission.Success = false;
                submission.ResponseStatusCode = "410";
                submission.ErrorMessage =
                    "Deprecated endpoint does not perform live FinanzOnline submission; use outbox pipeline (GET /api/admin/finanzonline-outbox).";
                var reject = new FinanzOnlineSubmitResponse
                {
                    Success = false,
                    Message =
                        "This endpoint is deprecated and does not submit to FinanzOnline. Use the operational outbox and GET /api/admin/finanzonline-reconciliation for FO state.",
                    SubmissionId = string.Empty,
                    Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss")
                };
                submission.ResponseBodyJson = JsonSerializer.Serialize(reject);
                _context.FinanzOnlineSubmissions.Add(submission);
                await _context.SaveChangesAsync();

                return BadRequest(reject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FinanzOnline invoice submission failed");

                submission.Success = false;
                submission.ResponseStatusCode = "500";
                submission.ErrorMessage = ex.Message;
                _context.FinanzOnlineSubmissions.Add(submission);
                await _context.SaveChangesAsync();

                return StatusCode(500, new { message = _messages.Get(ApiMessageKeys.InvoiceSubmissionFailed) });
            }
        }

        [HasPermission(AppPermissions.FinanzOnlineView)]
        [HttpGet("errors")]
        public async Task<ActionResult<FinanzOnlineErrorsListResponse>> GetErrors()
        {
            try
            {
                var errors = await _context.FinanzOnlineErrors
                    .AsNoTracking()
                    .OrderByDescending(e => e.OccurredAt)
                    .Take(50)
                    .Select(e => new FinanzOnlineErrorResponse
                    {
                        Id = e.Id,
                        Code = e.ErrorType,
                        Message = e.ErrorMessage,
                        Timestamp = e.OccurredAt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss"),
                        InvoiceNumber = e.InvoiceNumber ?? string.Empty,
                        RetryCount = e.RetryCount
                    })
                    .ToListAsync();

                return Ok(new FinanzOnlineErrorsListResponse
                {
                    Items = errors,
                    IsAuthoritative = true,
                    IsSimulated = false,
                    DiagnosticWarning =
                        "Authoritative for persisted FinanzOnlineErrors rows only (last 50). Not a live BMF feed; for per-payment FO state use GET /api/admin/finanzonline-reconciliation."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FinanzOnline errors fetch failed");
                return StatusCode(500, new { message = _messages.Get(ApiMessageKeys.FinanzOnlineErrorsFetchError) });
            }
        }

        [HasPermission(AppPermissions.FinanzOnlineManage)]
        [HttpPost("test-connection")]
        public async Task<ActionResult<FinanzOnlineTestResponse>> TestConnection()
        {
            try
            {
                var tseDevice = await _context.TseDevices
                    .Where(t => t.IsActive && t.FinanzOnlineEnabled)
                    .FirstOrDefaultAsync();

                if (tseDevice == null)
                {
                    return BadRequest(new { message = _messages.Get(ApiMessageKeys.FinanzOnlineNotEnabled) });
                }

                var testSnapshot = await _adminConnectivity.RunTestConnectionAsync(tseDevice, HttpContext.RequestAborted);
                return Ok(ToTestResponse(testSnapshot));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FinanzOnline connection test failed");
                return StatusCode(500, new { message = _messages.Get(ApiMessageKeys.FinanzOnlineConnectionTestFailed) });
            }
        }

        [HasPermission(AppPermissions.FinanzOnlineView)]
        [HttpGet("history/{invoiceId}")]
        public async Task<ActionResult<IEnumerable<FinanzOnlineSubmission>>> GetSubmissionHistory(Guid invoiceId)
        {
            try
            {
                var history = await _context.FinanzOnlineSubmissions
                    .Where(s => s.InvoiceId == invoiceId)
                    .OrderByDescending(s => s.SubmittedAt)
                    .ToListAsync();

                return Ok(history);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving FinanzOnline history for invoice {InvoiceId}", invoiceId);
                return StatusCode(500, new { message = _messages.Get(ApiMessageKeys.HistoryFetchError) });
            }
        }

        private static FinanzOnlineStatusResponse ToStatusResponse(FinanzOnlineAdministrativeStatusSnapshot s) =>
            new()
            {
                IsConnected = s.IsConnected,
                ApiVersion = s.ApiVersion,
                LastSync = s.LastSync,
                PendingInvoices = s.PendingInvoices,
                PendingReports = s.PendingReports,
                ErrorMessage = s.ErrorMessage,
                FinanzOnlineTransportsSimulated = s.FinanzOnlineTransportsSimulated,
                EnableRealTestSubmission = s.EnableRealTestSubmission,
                TransportDiagnostics = s.TransportDiagnostics,
                SessionProbeSucceeded = s.SessionProbeSucceeded,
                SessionProbeTimestamp = s.SessionProbeTimestamp,
                SessionProbeIntegrationMode = s.SessionProbeIntegrationMode,
                IsAuthoritative = s.IsAuthoritative,
                IsSimulated = s.FinanzOnlineTransportsSimulated,
                DiagnosticWarning = BuildStatusDiagnosticWarning(s)
            };

        private static FinanzOnlineTestResponse ToTestResponse(FinanzOnlineAdministrativeTestSnapshot s) =>
            new()
            {
                Success = s.Success,
                Message = s.Message,
                ApiVersion = s.ApiVersion,
                ResponseTime = s.ResponseTime,
                Timestamp = s.Timestamp,
                FinanzOnlineTransportsSimulated = s.FinanzOnlineTransportsSimulated,
                EnableRealTestSubmission = s.EnableRealTestSubmission,
                TransportDiagnostics = s.TransportDiagnostics,
                ProbeIntegrationMode = s.ProbeIntegrationMode,
                IsAuthoritative = s.IsAuthoritative,
                IsSimulated = s.FinanzOnlineTransportsSimulated,
                DiagnosticWarning = s.IsAuthoritative
                    ? null
                    : "Diagnostic only — no live SOAP session probe ran (simulated transports)."
            };

        private static string? BuildStatusDiagnosticWarning(FinanzOnlineAdministrativeStatusSnapshot s)
        {
            if (s.IsAuthoritative)
            {
                return null;
            }

            if (s.FinanzOnlineTransportsSimulated)
            {
                return "Diagnostic only — at least one FinanzOnline transport is simulated; IsConnected is not live BMF.";
            }

            return "Diagnostic only — no cached SOAP session probe yet; run POST /api/FinanzOnline/test-connection, then refresh.";
        }
    }

    // Request/Response Models
    public class FinanzOnlineConfigRequest
    {
        [Required]
        public string ApiUrl { get; set; } = string.Empty;

        [Required]
        public string Username { get; set; } = string.Empty;

        public bool AutoSubmit { get; set; } = false;
        public int SubmitInterval { get; set; } = 60; // dakika
        public int RetryAttempts { get; set; } = 3;
        public bool EnableValidation { get; set; } = true;
    }

    public class FinanzOnlineConfigResponse
    {
        public string ApiUrl { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public bool AutoSubmit { get; set; }
        public int SubmitInterval { get; set; }
        public int RetryAttempts { get; set; }
        public bool EnableValidation { get; set; }
        public bool IsEnabled { get; set; }
    }

    public class FinanzOnlineStatusResponse
    {
        public bool IsConnected { get; set; }
        public string ApiVersion { get; set; } = string.Empty;
        public string LastSync { get; set; } = string.Empty;
        public int PendingInvoices { get; set; }
        public int PendingReports { get; set; }
        public string? ErrorMessage { get; set; }
        public bool FinanzOnlineTransportsSimulated { get; set; }
        public bool EnableRealTestSubmission { get; set; }
        public string TransportDiagnostics { get; set; } = string.Empty;
        public bool? SessionProbeSucceeded { get; set; }
        public string? SessionProbeTimestamp { get; set; }
        public string? SessionProbeIntegrationMode { get; set; }
        /// <summary>True when <see cref="IsConnected"/> is backed by a cached SOAP session probe (live transport, not simulated).</summary>
        public bool IsAuthoritative { get; set; }
        /// <summary>True when FinanzOnline SOAP transports are in simulation mode (same signal as <see cref="FinanzOnlineTransportsSimulated"/>).</summary>
        public bool IsSimulated { get; set; }
        /// <summary>English operator hint when <see cref="IsAuthoritative"/> is false; null when authoritative.</summary>
        public string? DiagnosticWarning { get; set; }
    }

    public class FinanzOnlineSubmitRequest
    {
        [Required]
        public string InvoiceNumber { get; set; } = string.Empty;

        [Required]
        public decimal TotalAmount { get; set; }

        [Required]
        public string TseSignature { get; set; } = string.Empty;

        [Required]
        public string TaxDetails { get; set; } = string.Empty;

        [Required]
        public DateTime InvoiceDate { get; set; }

        [Required]
        public string KassenId { get; set; } = string.Empty;
    }

    public class FinanzOnlineSubmitResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string SubmissionId { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
    }

    public class FinanzOnlineErrorResponse
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
        public string InvoiceNumber { get; set; } = string.Empty;
        public int RetryCount { get; set; }
    }

    public class FinanzOnlineErrorsListResponse
    {
        public List<FinanzOnlineErrorResponse> Items { get; set; } = new();
        /// <summary>True: rows are persisted FinanzOnlineErrors facts (still not a live BMF stream).</summary>
        public bool IsAuthoritative { get; set; }
        public bool IsSimulated { get; set; }
        public string? DiagnosticWarning { get; set; }
    }

    public class FinanzOnlineTestResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ApiVersion { get; set; } = string.Empty;
        public int ResponseTime { get; set; }
        public string Timestamp { get; set; } = string.Empty;
        public bool FinanzOnlineTransportsSimulated { get; set; }
        public bool EnableRealTestSubmission { get; set; }
        public string TransportDiagnostics { get; set; } = string.Empty;
        public string? ProbeIntegrationMode { get; set; }
        public bool IsAuthoritative { get; set; }
        public bool IsSimulated { get; set; }
        public string? DiagnosticWarning { get; set; }
    }
}
