using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
using KasseAPI_Final.Time;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Admin: FinanzOnline outbox operational visibility (SOAP pipeline). Raw payload XML/JSON and credentials are never exposed.
/// Legacy payment-row reconciliation: <see cref="FinanzOnlineReconciliationController"/>.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/finanzonline-outbox")]
[Produces("application/json")]
public sealed class FinanzOnlineOutboxAdminController : ControllerBase
{
    private const int MaxListLimit = 500;
    private const int LastResponsePreviewMaxChars = 400;

    /// <summary>Snake_case and PascalCase (matches <see cref="FinanzOnlineOutboxStatuses"/>) aliases.</summary>
    private static readonly string[] KnownBuckets =
    {
        "all",
        "in_flight", "active", "pending", "processing",
        "awaiting_protocol", nameof(FinanzOnlineOutboxStatuses.AwaitingProtocol),
        "retryable", nameof(FinanzOnlineOutboxStatuses.RetryableFailure),
        "permanent_failure", nameof(FinanzOnlineOutboxStatuses.PermanentFailure),
        "protocol_failure", nameof(FinanzOnlineOutboxStatuses.ProtocolFailure),
        "manual_review", nameof(FinanzOnlineOutboxStatuses.ManualReviewRequired),
        "protocol_success", nameof(FinanzOnlineOutboxStatuses.ProtocolSuccess),
        "dead_letter", nameof(FinanzOnlineOutboxStatuses.DeadLetter)
    };

    private readonly AppDbContext _context;
    private readonly ILogger<FinanzOnlineOutboxAdminController> _logger;

    public FinanzOnlineOutboxAdminController(AppDbContext context, ILogger<FinanzOnlineOutboxAdminController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// GET: List outbox messages (SOAP pipeline). Filters: <c>bucket</c> (alias groups) or <c>status</c> (exact CSV);
    /// <c>mode</c> (TEST/PROD); <c>correlationId</c> (exact). No payload XML, credentials, or session ids.
    /// </summary>
    [HttpGet]
    [HasPermission(AppPermissions.FinanzOnlineView)]
    public async Task<ActionResult<FinanzOnlineOutboxListResponse>> GetList(
        [FromQuery] string? bucket = null,
        [FromQuery] string? status = null,
        [FromQuery] string? mode = null,
        [FromQuery] string? correlationId = null,
        [FromQuery] string? aggregateType = null,
        [FromQuery] string? businessKey = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var q = _context.FinanzOnlineOutboxMessages.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(mode))
            {
                var m = mode.Trim();
                q = q.Where(x => x.Mode == m);
            }

            if (!string.IsNullOrWhiteSpace(correlationId))
            {
                var cid = correlationId.Trim();
                q = q.Where(x => x.CorrelationId == cid);
            }

            if (!string.IsNullOrWhiteSpace(aggregateType))
                q = q.Where(x => x.AggregateType == aggregateType.Trim());

            if (!string.IsNullOrWhiteSpace(businessKey))
            {
                var bk = businessKey.Trim();
                q = q.Where(x => x.BusinessKey == bk);
            }

            var (lo, hi) = PostgreSqlUtcDateTime.CalendarHalfOpenInstantBounds(fromUtc, toUtc);
            if (lo.HasValue)
                q = q.Where(x => x.CreatedAt >= lo.Value);
            if (hi.HasValue)
                q = q.Where(x => x.CreatedAt < hi.Value);

            if (!string.IsNullOrWhiteSpace(status))
            {
                var statuses = status.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                q = q.Where(x => statuses.Contains(x.Status));
            }
            else if (!string.IsNullOrWhiteSpace(bucket))
            {
                var b = bucket.Trim();
                if (!IsKnownBucket(b))
                    return BadRequest(new { message = "Unknown bucket filter.", code = "FINANZONLINE_OUTBOX_INVALID_BUCKET", allowed = KnownBuckets });
                q = ApplyBucketFilter(q, NormalizeBucket(b));
            }

            var total = await q.CountAsync(cancellationToken).ConfigureAwait(false);

            var take = Math.Clamp(limit, 1, MaxListLimit);
            var rows = await q
                .OrderByDescending(x => x.CreatedAt)
                .Take(take)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var items = rows.Select(MapToDto).ToList();

            return Ok(new FinanzOnlineOutboxListResponse
            {
                Total = total,
                Items = items
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FinanzOnline outbox list failed");
            return StatusCode(500, new { message = "Outbox list failed.", code = "FINANZONLINE_OUTBOX_LIST_ERROR" });
        }
    }

    /// <summary>
    /// GET: Single outbox row by id (redacted; no raw payload).
    /// </summary>
    [HttpGet("{id:guid}")]
    [HasPermission(AppPermissions.FinanzOnlineView)]
    public async Task<ActionResult<FinanzOnlineOutboxItemDto>> GetOne(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await _context.FinanzOnlineOutboxMessages
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (row == null)
            return NotFound(new { message = "Outbox message not found.", code = "FINANZONLINE_OUTBOX_NOT_FOUND" });

        return Ok(MapToDto(row));
    }

    private static bool IsKnownBucket(string bucket) =>
        KnownBuckets.Contains(bucket, StringComparer.OrdinalIgnoreCase);

    /// <summary>Maps PascalCase / snake_case bucket names to a canonical switch key.</summary>
    private static string NormalizeBucket(string bucket)
    {
        var b = bucket.Trim();
        return b.ToUpperInvariant() switch
        {
            "AWAITINGPROTOCOL" or "AWAITING_PROTOCOL" => "awaiting_protocol",
            "RETRYABLEFAILURE" or "RETRYABLE" => "retryable",
            "PERMANENTFAILURE" or "PERMANENT_FAILURE" => "permanent_failure",
            "PROTOCOLFAILURE" or "PROTOCOL_FAILURE" => "protocol_failure",
            "MANUALREVIEWREQUIRED" or "MANUAL_REVIEW" => "manual_review",
            "PROTOCOLSUCCESS" or "PROTOCOL_SUCCESS" => "protocol_success",
            "DEADLETTER" or "DEAD_LETTER" => "dead_letter",
            "PENDING" => "pending",
            "PROCESSING" => "processing",
            _ => b.ToLowerInvariant()
        };
    }

    private static IQueryable<FinanzOnlineOutboxMessage> ApplyBucketFilter(
        IQueryable<FinanzOnlineOutboxMessage> q,
        string bucket)
    {
        var b = bucket.ToLowerInvariant();
        return b switch
        {
            "all" => q,
            "in_flight" or "active" => q.Where(x =>
                x.Status == FinanzOnlineOutboxStatuses.Pending ||
                x.Status == FinanzOnlineOutboxStatuses.Processing),
            "pending" => q.Where(x => x.Status == FinanzOnlineOutboxStatuses.Pending),
            "processing" => q.Where(x => x.Status == FinanzOnlineOutboxStatuses.Processing),
            "awaiting_protocol" => q.Where(x => x.Status == FinanzOnlineOutboxStatuses.AwaitingProtocol),
            "retryable" => q.Where(x => x.Status == FinanzOnlineOutboxStatuses.RetryableFailure),
            "permanent_failure" => q.Where(x => x.Status == FinanzOnlineOutboxStatuses.PermanentFailure),
            "protocol_failure" => q.Where(x => x.Status == FinanzOnlineOutboxStatuses.ProtocolFailure),
            "manual_review" => q.Where(x => x.Status == FinanzOnlineOutboxStatuses.ManualReviewRequired),
            "protocol_success" => q.Where(x => x.Status == FinanzOnlineOutboxStatuses.ProtocolSuccess),
            "dead_letter" => q.Where(x => x.Status == FinanzOnlineOutboxStatuses.DeadLetter),
            _ => q
        };
    }

    private static FinanzOnlineOutboxItemDto MapToDto(FinanzOnlineOutboxMessage x)
    {
        var registerId = TryGetRegisterIdFromPayload(x.PayloadJson);
        return new FinanzOnlineOutboxItemDto
        {
            OutboxId = x.Id,
            AggregateType = x.AggregateType,
            AggregateId = x.AggregateId,
            MessageType = x.MessageType,
            BusinessKey = x.BusinessKey,
            CorrelationId = x.CorrelationId,
            TenantId = x.TenantId,
            BranchId = x.BranchId,
            RegisterId = registerId,
            Scope = new FinanzOnlineOutboxScopeDto
            {
                TenantId = x.TenantId,
                BranchId = x.BranchId,
                RegisterId = registerId
            },
            Mode = x.Mode,
            Status = x.Status,
            OperatorStatusLabel = MapOperatorLabel(x.Status),
            OperatorFailureHint = MapOperatorFailureHint(x.FailureCategory, x.LastErrorCode, x.Status),
            AttemptCount = x.AttemptCount,
            NextAttemptAtUtc = x.NextAttemptAt,
            LastErrorCode = x.LastErrorCode,
            LastErrorSummary = x.LastErrorMessage,
            FailureCategory = x.FailureCategory,
            TransmissionId = x.TransmissionId,
            ExternalReferenceId = x.ExternalReferenceId,
            ExternalStatus = x.ExternalStatus,
            ProtocolCode = x.ProtocolCode,
            ProtocolSummary = x.ProtocolSummary,
            ProtocolPayloadHash = x.ProtocolPayloadHash,
            LastResponsePreview = TruncatePreview(x.LastResponseJson),
            CreatedAtUtc = x.CreatedAt,
            ProcessedAtUtc = x.ProcessedAt,
            IsTerminal = IsTerminalStatus(x.Status),
            IdempotencyKeySuffix = IdempotencySuffix(x.IdempotencyKey)
        };
    }

    /// <summary>Last 12 hex chars of idempotency key for correlation without exposing full key.</summary>
    private static string? IdempotencySuffix(string? key)
    {
        if (string.IsNullOrEmpty(key) || key.Length < 12)
            return null;
        return key[^12..];
    }

    private static bool IsTerminalStatus(string status)
    {
        return status is FinanzOnlineOutboxStatuses.ProtocolSuccess
            or FinanzOnlineOutboxStatuses.ProtocolFailure
            or FinanzOnlineOutboxStatuses.PermanentFailure
            or FinanzOnlineOutboxStatuses.ManualReviewRequired
            or FinanzOnlineOutboxStatuses.DeadLetter;
    }

    private static string MapOperatorLabel(string status)
    {
        return status switch
        {
            FinanzOnlineOutboxStatuses.Pending => "Queued",
            FinanzOnlineOutboxStatuses.Processing => "Processing",
            FinanzOnlineOutboxStatuses.RetryableFailure => "Retryable failure",
            FinanzOnlineOutboxStatuses.AwaitingProtocol => "Awaiting protocol",
            FinanzOnlineOutboxStatuses.ProtocolSuccess => "Protocol success",
            FinanzOnlineOutboxStatuses.ProtocolFailure => "Protocol failure",
            FinanzOnlineOutboxStatuses.PermanentFailure => "Permanent failure",
            FinanzOnlineOutboxStatuses.ManualReviewRequired => "Manual review required",
            FinanzOnlineOutboxStatuses.DeadLetter => "Dead letter",
            _ => status
        };
    }

    /// <summary>Short operator-facing hint; does not replace LastErrorSummary.</summary>
    private static string? MapOperatorFailureHint(string? failureCategory, string? lastErrorCode, string status)
    {
        if (!string.IsNullOrWhiteSpace(lastErrorCode))
        {
            var c = lastErrorCode.Trim();
            if (c.StartsWith("RKDB_", StringComparison.OrdinalIgnoreCase) ||
                c.StartsWith("SOAP_", StringComparison.OrdinalIgnoreCase) ||
                c.StartsWith("HTTP_", StringComparison.OrdinalIgnoreCase))
                return "Remote / RKDB error — see last error code.";
        }

        if (string.IsNullOrWhiteSpace(failureCategory))
            return null;

        return failureCategory switch
        {
            FinanzOnlineFailureCategories.RetryableTransient => "Transient — worker may retry.",
            FinanzOnlineFailureCategories.Authorization => "Auth / session — check credentials.",
            FinanzOnlineFailureCategories.Session => "Session failure.",
            FinanzOnlineFailureCategories.AwaitingProtocol => "Waiting for protocol poll.",
            FinanzOnlineFailureCategories.ManualReview => "Needs manual review.",
            FinanzOnlineFailureCategories.PermanentBusiness => status is FinanzOnlineOutboxStatuses.ProtocolFailure
                ? "Protocol rejected."
                : "Permanent business failure.",
            _ => null
        };
    }

    private static string? TryGetRegisterIdFromPayload(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;
            if (!TryGetScope(root, out var scope))
                return null;
            if (scope.TryGetProperty("RegisterId", out var rid))
                return rid.GetString();
            if (scope.TryGetProperty("registerId", out rid))
                return rid.GetString();
        }
        catch
        {
            // ignore malformed historical payload
        }
        return null;
    }

    private static bool TryGetScope(JsonElement root, out JsonElement scope)
    {
        if (root.TryGetProperty("Scope", out scope))
            return true;
        if (root.TryGetProperty("scope", out scope))
            return true;
        scope = default;
        return false;
    }

    private static string? TruncatePreview(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        var t = json.Trim();
        if (t.Length <= LastResponsePreviewMaxChars)
            return t;
        return t.Substring(0, LastResponsePreviewMaxChars - 3) + "...";
    }
}

public sealed class FinanzOnlineOutboxListResponse
{
    public int Total { get; set; }
    public IReadOnlyList<FinanzOnlineOutboxItemDto> Items { get; set; } = Array.Empty<FinanzOnlineOutboxItemDto>();
}

public sealed class FinanzOnlineOutboxScopeDto
{
    public string? TenantId { get; set; }
    public string? BranchId { get; set; }
    public string? RegisterId { get; set; }
}

public sealed class FinanzOnlineOutboxItemDto
{
    public Guid OutboxId { get; set; }
    public string AggregateType { get; set; } = string.Empty;
    public Guid AggregateId { get; set; }
    public string MessageType { get; set; } = string.Empty;
    public string BusinessKey { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    /// <summary>Flat scope fields (same as <see cref="Scope"/>).</summary>
    public string? TenantId { get; set; }
    public string? BranchId { get; set; }
    public string? RegisterId { get; set; }
    /// <summary>Register / tenant / branch from enqueue + payload (no secrets).</summary>
    public FinanzOnlineOutboxScopeDto? Scope { get; set; }
    public string Mode { get; set; } = "TEST";
    public string Status { get; set; } = string.Empty;
    public string OperatorStatusLabel { get; set; } = string.Empty;
    /// <summary>Brief hint for operators; technical detail remains in <see cref="LastErrorCode"/> / <see cref="LastErrorSummary"/>.</summary>
    public string? OperatorFailureHint { get; set; }
    public int AttemptCount { get; set; }
    public DateTime NextAttemptAtUtc { get; set; }
    public string? LastErrorCode { get; set; }
    public string? LastErrorSummary { get; set; }
    public string? FailureCategory { get; set; }
    public string? TransmissionId { get; set; }
    public string? ExternalReferenceId { get; set; }
    public string? ExternalStatus { get; set; }
    public string? ProtocolCode { get; set; }
    public string? ProtocolSummary { get; set; }
    public string? ProtocolPayloadHash { get; set; }
    /// <summary>Truncated JSON preview of last remote response (no raw SOAP/XML).</summary>
    public string? LastResponsePreview { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public bool IsTerminal { get; set; }
    public string? IdempotencyKeySuffix { get; set; }
}
