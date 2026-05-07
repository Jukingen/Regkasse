using System.Globalization;
using System.Text;
using System.Text.Json;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers.Base;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Admin visibility and manual replay for server-side offline payment intents (non-fiscal queue).
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/offline-transactions")]
[Produces("application/json")]
public class AdminOfflineTransactionsController : BaseController
{
    private readonly AppDbContext _context;
    private readonly IOfflineTransactionService _offlineService;
    private readonly ISettingsTenantResolver _settingsTenantResolver;

    public AdminOfflineTransactionsController(
        AppDbContext context,
        IOfflineTransactionService offlineService,
        ISettingsTenantResolver settingsTenantResolver,
        ILogger<AdminOfflineTransactionsController> logger)
        : base(logger)
    {
        _context = context;
        _offlineService = offlineService;
        _settingsTenantResolver = settingsTenantResolver;
    }

    /// <summary>Dashboard widget: pending/failed counts and last replay instant.</summary>
    [HttpGet("summary")]
    [HasPermission(AppPermissions.PaymentView)]
    public async Task<ActionResult<AdminOfflineTransactionsSummaryDto>> GetSummary(CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
            var baseQuery = TenantScopedOfflineQuery(tenantId);

            var pending = await baseQuery.AsNoTracking()
                .CountAsync(
                    x => x.Status == OfflineTransactionStatus.Pending ||
                         x.Status == OfflineTransactionStatus.NonFiscalPending,
                    cancellationToken);

            var failed = await baseQuery.AsNoTracking()
                .CountAsync(x => x.Status == OfflineTransactionStatus.Failed, cancellationToken);

            var lastReplay = await baseQuery.AsNoTracking()
                .Where(x => x.LastReplayAttemptAt != null)
                .MaxAsync(x => (DateTime?)x.LastReplayAttemptAt, cancellationToken);

            return Ok(new AdminOfflineTransactionsSummaryDto
            {
                PendingCount = pending,
                FailedCount = failed,
                LastReplayAtUtc = lastReplay
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin offline transactions summary failed");
            return StatusCode(500, new { message = "Failed to load offline transaction summary", code = "ADMIN_OFFLINE_SUMMARY_ERROR" });
        }
    }

    /// <summary>
    /// Filterable list: statusGroup = pending | completed | failed | all.
    /// Date bounds apply to <see cref="OfflineTransaction.ServerReceivedAtUtc"/> (UTC).
    /// </summary>
    [HttpGet]
    [HasPermission(AppPermissions.PaymentView)]
    public async Task<ActionResult<AdminOfflineTransactionsListResponse>> GetList(
        [FromQuery] string? statusGroup = null,
        [FromQuery] Guid? cashRegisterId = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 200);

        try
        {
            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);

            var joined = from o in TenantScopedOfflineQuery(tenantId).AsNoTracking()
                join cr in _context.CashRegisters.AsNoTracking() on o.CashRegisterId equals cr.Id
                where cr.TenantId == tenantId
                select new { Offline = o, Register = cr };

            var filtered = joined.AsQueryable();

            if (cashRegisterId.HasValue && cashRegisterId.Value != Guid.Empty)
                filtered = filtered.Where(x => x.Offline.CashRegisterId == cashRegisterId.Value);

            if (fromUtc.HasValue)
                filtered = filtered.Where(x => x.Offline.ServerReceivedAtUtc >= fromUtc.Value);

            if (toUtc.HasValue)
                filtered = filtered.Where(x => x.Offline.ServerReceivedAtUtc <= toUtc.Value);

            var g = (statusGroup ?? "all").Trim().ToLowerInvariant();
            if (g == "pending")
            {
                filtered = filtered.Where(x =>
                    x.Offline.Status == OfflineTransactionStatus.Pending ||
                    x.Offline.Status == OfflineTransactionStatus.NonFiscalPending);
            }
            else if (g == "completed")
            {
                filtered = filtered.Where(x => x.Offline.Status == OfflineTransactionStatus.Synced);
            }
            else if (g == "failed")
            {
                filtered = filtered.Where(x => x.Offline.Status == OfflineTransactionStatus.Failed);
            }

            var total = await filtered.CountAsync(cancellationToken);

            var pageRows = await filtered
                .OrderByDescending(x => x.Offline.ServerReceivedAtUtc)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var items = pageRows.Select(x =>
            {
                var (amount, method) = TryReadPaymentSummary(x.Offline.PayloadJson);
                var label = $"{x.Register.RegisterNumber} · {x.Register.Location}";
                return new AdminOfflineTransactionRowDto
                {
                    Id = x.Offline.Id,
                    CashRegisterId = x.Offline.CashRegisterId,
                    CashRegisterLabel = label,
                    ServerReceivedAtUtc = x.Offline.ServerReceivedAtUtc,
                    Amount = amount,
                    PaymentMethod = method,
                    Status = x.Offline.Status.ToString(),
                    RetryCount = x.Offline.RetryCount,
                    LastErrorCode = x.Offline.LastErrorCode,
                    LastErrorMessageSafe = x.Offline.LastErrorMessageSafe,
                    SyncedPaymentId = x.Offline.SyncedPaymentId
                };
            }).ToList();

            return Ok(new AdminOfflineTransactionsListResponse
            {
                Items = items,
                TotalCount = total,
                PageNumber = pageNumber,
                PageSize = pageSize
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin offline transactions list failed");
            return StatusCode(500, new { message = "Failed to list offline transactions", code = "ADMIN_OFFLINE_LIST_ERROR" });
        }
    }

    /// <summary>CSV export of failed transactions for the same tenant and optional filters.</summary>
    [HttpGet("export-failed")]
    [HasPermission(AppPermissions.PaymentView)]
    public async Task<IActionResult> ExportFailed(
        [FromQuery] Guid? cashRegisterId = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);

            var joined = from o in TenantScopedOfflineQuery(tenantId).AsNoTracking()
                join cr in _context.CashRegisters.AsNoTracking() on o.CashRegisterId equals cr.Id
                where cr.TenantId == tenantId && o.Status == OfflineTransactionStatus.Failed
                select new { Offline = o, Register = cr };

            var filtered = joined.AsQueryable();

            if (cashRegisterId.HasValue && cashRegisterId.Value != Guid.Empty)
                filtered = filtered.Where(x => x.Offline.CashRegisterId == cashRegisterId.Value);

            if (fromUtc.HasValue)
                filtered = filtered.Where(x => x.Offline.ServerReceivedAtUtc >= fromUtc.Value);

            if (toUtc.HasValue)
                filtered = filtered.Where(x => x.Offline.ServerReceivedAtUtc <= toUtc.Value);

            var rows = await filtered
                .OrderByDescending(x => x.Offline.ServerReceivedAtUtc)
                .ToListAsync(cancellationToken);

            var sb = new StringBuilder();
            sb.AppendLine("id,cashRegisterId,cashRegisterLabel,serverReceivedAtUtc,amountEUR,paymentMethod,retryCount,lastErrorCode,lastErrorMessageSafe");

            foreach (var x in rows)
            {
                var (amount, method) = TryReadPaymentSummary(x.Offline.PayloadJson);
                var label = $"{x.Register.RegisterNumber} · {x.Register.Location}";
                sb.Append(CsvEscape(x.Offline.Id.ToString())).Append(',');
                sb.Append(CsvEscape(x.Offline.CashRegisterId.ToString())).Append(',');
                sb.Append(CsvEscape(label)).Append(',');
                sb.Append(CsvEscape(x.Offline.ServerReceivedAtUtc.ToString("o", CultureInfo.InvariantCulture))).Append(',');
                sb.Append(CsvEscape(amount.ToString(CultureInfo.InvariantCulture))).Append(',');
                sb.Append(CsvEscape(method)).Append(',');
                sb.Append(x.Offline.RetryCount).Append(',');
                sb.Append(CsvEscape(x.Offline.LastErrorCode ?? "")).Append(',');
                sb.AppendLine(CsvEscape(x.Offline.LastErrorMessageSafe ?? ""));
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = $"offline-failed-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
            return File(bytes, "text/csv; charset=utf-8", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin offline transactions CSV export failed");
            return StatusCode(500, new { message = "Failed to export failed offline transactions", code = "ADMIN_OFFLINE_EXPORT_ERROR" });
        }
    }

    /// <summary>Replay one offline intent (pending or failed).</summary>
    [HttpPost("{id:guid}/retry")]
    [HasPermission(AppPermissions.PaymentView)]
    public async Task<ActionResult<AdminOfflineTransactionRetryResponseDto>> RetryOne(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "User not authenticated" });

        try
        {
            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);

            var row = await _context.OfflineTransactions.AsNoTracking()
                .Where(o => o.Id == id)
                .Join(
                    _context.CashRegisters.AsNoTracking(),
                    o => o.CashRegisterId,
                    cr => cr.Id,
                    (o, cr) => new { Offline = o, TenantId = cr.TenantId })
                .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);

            if (row == null)
                return NotFound(new { message = "Offline transaction not found", code = "ADMIN_OFFLINE_NOT_FOUND" });

            if (row.Offline.Status == OfflineTransactionStatus.Synced)
                return BadRequest(new { message = "Already synced; retry not applicable.", code = "ADMIN_OFFLINE_ALREADY_SYNCED" });

            var entity = await _context.OfflineTransactions
                .FirstAsync(o => o.Id == id, cancellationToken);

            var item = BuildReplayItem(entity);
            var req = new ReplayOfflineTransactionsRequest { Transactions = new List<ReplayOfflineTransactionItem> { item } };

            var role = GetCurrentUserRole() ?? "Admin";
            var response = await _offlineService.ReplayOfflineTransactionsAsync(req, userId, role);

            return Ok(new AdminOfflineTransactionRetryResponseDto
            {
                ReplayBatchCorrelationId = response.ReplayBatchCorrelationId,
                Items = response.Items.ToList(),
                QueuedCount = 1
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin offline transaction retry failed for {Id}", id);
            return StatusCode(500, new { message = "Retry failed", code = "ADMIN_OFFLINE_RETRY_ERROR" });
        }
    }

    /// <summary>Replay all failed offline intents for this tenant (FIFO by server receive time).</summary>
    [HttpPost("retry-all")]
    [HasPermission(AppPermissions.PaymentView)]
    public async Task<ActionResult<AdminOfflineTransactionRetryResponseDto>> RetryAllFailed(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "User not authenticated" });

        try
        {
            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);

            var failedIds = await (
                    from o in _context.OfflineTransactions.AsNoTracking()
                    join cr in _context.CashRegisters.AsNoTracking() on o.CashRegisterId equals cr.Id
                    where cr.TenantId == tenantId && o.Status == OfflineTransactionStatus.Failed
                    orderby o.ServerReceivedAtUtc
                    select o.Id)
                .ToListAsync(cancellationToken);

            if (failedIds.Count == 0)
            {
                return Ok(new AdminOfflineTransactionRetryResponseDto
                {
                    ReplayBatchCorrelationId = null,
                    Items = Array.Empty<ReplayOfflineTransactionsResponseItem>(),
                    QueuedCount = 0
                });
            }

            var entities = await _context.OfflineTransactions
                .Where(o => failedIds.Contains(o.Id))
                .OrderBy(o => o.ServerReceivedAtUtc)
                .ToListAsync(cancellationToken);

            var transactions = entities.Select(BuildReplayItem).ToList();
            var req = new ReplayOfflineTransactionsRequest { Transactions = transactions };

            var role = GetCurrentUserRole() ?? "Admin";
            var response = await _offlineService.ReplayOfflineTransactionsAsync(req, userId, role);

            return Ok(new AdminOfflineTransactionRetryResponseDto
            {
                ReplayBatchCorrelationId = response.ReplayBatchCorrelationId,
                Items = response.Items.ToList(),
                QueuedCount = transactions.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin offline retry-all failed");
            return StatusCode(500, new { message = "Retry-all failed", code = "ADMIN_OFFLINE_RETRY_ALL_ERROR" });
        }
    }

    private IQueryable<OfflineTransaction> TenantScopedOfflineQuery(Guid tenantId)
    {
        return _context.OfflineTransactions
            .Where(o => _context.CashRegisters.Any(cr => cr.Id == o.CashRegisterId && cr.TenantId == tenantId));
    }

    private static ReplayOfflineTransactionItem BuildReplayItem(OfflineTransaction row)
    {
        using var doc = JsonDocument.Parse(row.PayloadJson);
        return new ReplayOfflineTransactionItem
        {
            OfflineTransactionId = row.Id,
            CreatedAtUtc = row.OfflineCreatedAtUtc,
            CashRegisterId = row.CashRegisterId,
            Payload = doc.RootElement.Clone(),
            DeviceId = row.DeviceId,
            ClientSequenceNumber = row.ClientSequenceNumber
        };
    }

    private static (decimal Amount, string PaymentMethod) TryReadPaymentSummary(string payloadJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;
            decimal total = 0;
            if (root.TryGetProperty("totalAmount", out var ta) && ta.TryGetDecimal(out var d))
                total = d;

            var method = "unknown";
            if (root.TryGetProperty("payment", out var pay) && pay.TryGetProperty("method", out var mEl))
            {
                var ms = mEl.GetString();
                if (!string.IsNullOrEmpty(ms))
                    method = ms.Trim().ToLowerInvariant();
            }

            return (total, method);
        }
        catch
        {
            return (0, "unknown");
        }
    }

    private static string CsvEscape(string? value)
    {
        var v = value ?? "";
        if (v.Contains('"') || v.Contains(',') || v.Contains('\n') || v.Contains('\r'))
            return "\"" + v.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        return v;
    }
}
