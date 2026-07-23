using System.Security.Cryptography;
using System.Text;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.TwoFactor;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

public sealed class DownloadSecurityEvaluateRequest
{
    public required string UserId { get; init; }
    public required string UserRole { get; init; }
    public Guid? TenantId { get; init; }
    public required string ExportKind { get; init; }
    public string? ResourceId { get; init; }
    public long? FileSizeBytes { get; init; }
    public bool PrivacyAck { get; init; }
    public string? TwoFactorCode { get; init; }
    public Guid? ApprovalId { get; init; }
    public string? DownloadTicket { get; init; }
    public bool IsSuperAdmin { get; init; }
}

public sealed class DownloadSecurityEvaluateResult
{
    public bool Allowed { get; init; }
    public int StatusCode { get; init; } = 200;
    public string? Code { get; init; }
    public string? Message { get; init; }
    public Guid? ApprovalId { get; init; }
    public string? DownloadTicket { get; init; }
    public DateTime? TicketExpiresAtUtc { get; init; }
    public object? Body => Code == null
        ? null
        : new
        {
            code = Code,
            message = Message,
            approvalId = ApprovalId,
            downloadTicket = DownloadTicket,
            ticketExpiresAtUtc = TicketExpiresAtUtc,
        };

    public static DownloadSecurityEvaluateResult Ok(string? ticket = null, DateTime? expires = null) =>
        new()
        {
            Allowed = true,
            DownloadTicket = ticket,
            TicketExpiresAtUtc = expires,
        };

    public static DownloadSecurityEvaluateResult Deny(int status, string code, string message, Guid? approvalId = null) =>
        new()
        {
            Allowed = false,
            StatusCode = status,
            Code = code,
            Message = message,
            ApprovalId = approvalId,
        };
}

public sealed class DownloadSecurityPolicyDto
{
    public int MaxDownloadsPerUserPerDay { get; init; }
    public long MaxFileSizeBytes { get; init; }
    public int DownloadLinkTtlHours { get; init; }
    public bool RequireApprovalForSensitiveExports { get; init; }
    public bool RequireTwoFactorForCriticalExports { get; init; }
    public bool SuperAdminMaySelfApprove { get; init; }
    public IReadOnlyList<string> SensitiveExportKinds { get; init; } = [];
    public IReadOnlyList<string> CriticalTwoFactorKinds { get; init; } = [];
}

public sealed class SensitiveExportApprovalDto
{
    public Guid Id { get; init; }
    public Guid? TenantId { get; init; }
    public string ExportKind { get; init; } = string.Empty;
    public string RequesterUserId { get; init; } = string.Empty;
    public string? Reason { get; init; }
    public string? ResourceId { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime RequestedAt { get; init; }
    public string? ResolvedByUserId { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public string? ResolutionNote { get; init; }
    public DateTime? ValidUntil { get; init; }
}

public interface IDownloadSecurityService
{
    DownloadSecurityPolicyDto GetPolicy();

    Task<DownloadSecurityEvaluateResult> EvaluateAsync(
        DownloadSecurityEvaluateRequest request,
        CancellationToken cancellationToken = default);

    Task<SensitiveExportApprovalDto> RequestApprovalAsync(
        string requesterUserId,
        Guid? tenantId,
        string exportKind,
        string? resourceId,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SensitiveExportApprovalDto>> ListMineAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SensitiveExportApprovalDto>> ListPendingAsync(
        CancellationToken cancellationToken = default);

    Task<(bool Ok, string? Code, string? Message, SensitiveExportApprovalDto? Dto)> ApproveAsync(
        Guid id,
        string resolverUserId,
        string? note,
        CancellationToken cancellationToken = default);

    Task<(bool Ok, string? Code, string? Message, SensitiveExportApprovalDto? Dto)> RejectAsync(
        Guid id,
        string resolverUserId,
        string? note,
        CancellationToken cancellationToken = default);

    Task LogDownloadAuditAsync(
        string userId,
        string userRole,
        Guid? tenantId,
        string exportKind,
        string fileName,
        long? fileSizeBytes,
        string? resourceId,
        string? correlationId,
        CancellationToken cancellationToken = default);
}

public sealed class DownloadSecurityService : IDownloadSecurityService
{
    public const string HeaderPrivacyAck = "X-Sensitive-Export-Ack";
    public const string HeaderTwoFactor = "X-2FA-Code";
    public const string HeaderApprovalId = "X-Sensitive-Export-Approval-Id";
    public const string HeaderDownloadTicket = "X-Download-Ticket";
    public const string QueryDownloadTicket = "downloadTicket";

    private readonly AppDbContext _db;
    private readonly DownloadSecurityOptions _options;
    private readonly ITwoFactorService _twoFactor;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditLogService _audit;
    private readonly ILogger<DownloadSecurityService> _logger;

    public DownloadSecurityService(
        AppDbContext db,
        IOptions<DownloadSecurityOptions> options,
        ITwoFactorService twoFactor,
        UserManager<ApplicationUser> userManager,
        IAuditLogService audit,
        ILogger<DownloadSecurityService> logger)
    {
        _db = db;
        _options = options.Value;
        _twoFactor = twoFactor;
        _userManager = userManager;
        _audit = audit;
        _logger = logger;
    }

    public DownloadSecurityPolicyDto GetPolicy() =>
        new()
        {
            MaxDownloadsPerUserPerDay = Math.Max(1, _options.MaxDownloadsPerUserPerDay),
            MaxFileSizeBytes = Math.Max(1, _options.MaxFileSizeBytes),
            DownloadLinkTtlHours = Math.Max(1, _options.DownloadLinkTtlHours),
            RequireApprovalForSensitiveExports = _options.RequireApprovalForSensitiveExports,
            RequireTwoFactorForCriticalExports = _options.RequireTwoFactorForCriticalExports,
            SuperAdminMaySelfApprove = _options.SuperAdminMaySelfApprove,
            SensitiveExportKinds = SensitiveExportKinds.All.ToList(),
            CriticalTwoFactorKinds = SensitiveExportKinds.All
                .Where(SensitiveExportKinds.RequiresCriticalTwoFactor)
                .ToList(),
        };

    public async Task<DownloadSecurityEvaluateResult> EvaluateAsync(
        DownloadSecurityEvaluateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
            return DownloadSecurityEvaluateResult.Deny(401, "DOWNLOAD_AUTH_REQUIRED", "Authentication required.");

        var kind = (request.ExportKind ?? string.Empty).Trim();
        var isSensitive = SensitiveExportKinds.IsValid(kind);

        if (request.FileSizeBytes is > 0 && request.FileSizeBytes.Value > _options.MaxFileSizeBytes)
        {
            return DownloadSecurityEvaluateResult.Deny(
                413,
                "DOWNLOAD_FILE_TOO_LARGE",
                $"File exceeds max download size of {_options.MaxFileSizeBytes} bytes.");
        }

        var dayStart = DateTime.UtcNow.Date;
        var dayEnd = dayStart.AddDays(1);
        var todayCount = await _db.DownloadHistories.AsNoTracking()
            .CountAsync(
                h => h.UserId == request.UserId
                     && h.DownloadedAt >= dayStart
                     && h.DownloadedAt < dayEnd,
                cancellationToken)
            .ConfigureAwait(false);

        if (todayCount >= Math.Max(1, _options.MaxDownloadsPerUserPerDay))
        {
            return DownloadSecurityEvaluateResult.Deny(
                429,
                "DOWNLOAD_DAILY_LIMIT",
                $"Daily download limit of {_options.MaxDownloadsPerUserPerDay} reached.");
        }

        if (!isSensitive)
            return DownloadSecurityEvaluateResult.Ok();

        // Valid time-limited ticket skips ack / approval / 2FA for the same resource.
        if (!string.IsNullOrWhiteSpace(request.DownloadTicket))
        {
            var ticketOk = await TryConsumeTicketAsync(
                request.DownloadTicket.Trim(),
                request.UserId,
                kind,
                request.ResourceId,
                markUsed: false,
                cancellationToken).ConfigureAwait(false);
            if (ticketOk)
                return DownloadSecurityEvaluateResult.Ok(request.DownloadTicket.Trim());
            return DownloadSecurityEvaluateResult.Deny(
                403,
                "DOWNLOAD_TICKET_INVALID",
                "Download ticket is invalid, expired, or already used.");
        }

        if (SensitiveExportKinds.RequiresPrivacyAck(kind) && !request.PrivacyAck)
        {
            return DownloadSecurityEvaluateResult.Deny(
                403,
                "SENSITIVE_EXPORT_ACK_REQUIRED",
                "Privacy acknowledgement is required for this sensitive export (header X-Sensitive-Export-Ack: true).");
        }

        Guid? approvalId = request.ApprovalId;
        if (_options.RequireApprovalForSensitiveExports)
        {
            if (request.IsSuperAdmin && _options.SuperAdminMaySelfApprove)
            {
                // Super Admin may proceed without a separate approval row.
            }
            else
            {
                var approval = await FindValidApprovalAsync(
                    approvalId,
                    request.UserId,
                    kind,
                    request.ResourceId,
                    cancellationToken).ConfigureAwait(false);
                if (approval == null)
                {
                    return DownloadSecurityEvaluateResult.Deny(
                        403,
                        "SENSITIVE_EXPORT_APPROVAL_REQUIRED",
                        "Super Admin approval is required before downloading this sensitive export.");
                }

                approvalId = approval.Id;
            }
        }

        if (_options.RequireTwoFactorForCriticalExports
            && SensitiveExportKinds.RequiresCriticalTwoFactor(kind))
        {
            if (string.IsNullOrWhiteSpace(request.TwoFactorCode))
            {
                return DownloadSecurityEvaluateResult.Deny(
                    403,
                    "SENSITIVE_EXPORT_2FA_REQUIRED",
                    "Two-factor authentication code is required (header X-2FA-Code).");
            }

            var user = await _userManager.FindByIdAsync(request.UserId).ConfigureAwait(false);
            if (user == null)
                return DownloadSecurityEvaluateResult.Deny(401, "DOWNLOAD_AUTH_REQUIRED", "User not found.");

            var ok = await _twoFactor
                .VerifyTwoFactorTokenAsync(user, request.TwoFactorCode.Trim(), cancellationToken)
                .ConfigureAwait(false);
            if (!ok)
            {
                return DownloadSecurityEvaluateResult.Deny(
                    403,
                    "SENSITIVE_EXPORT_2FA_INVALID",
                    "Invalid two-factor authentication code.");
            }
        }

        var (ticket, expires) = await MintTicketAsync(
            request.UserId,
            request.TenantId,
            kind,
            request.ResourceId,
            approvalId,
            cancellationToken).ConfigureAwait(false);

        return DownloadSecurityEvaluateResult.Ok(ticket, expires);
    }

    public async Task<SensitiveExportApprovalDto> RequestApprovalAsync(
        string requesterUserId,
        Guid? tenantId,
        string exportKind,
        string? resourceId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        if (!SensitiveExportKinds.IsValid(exportKind))
            throw new ArgumentException("Invalid export kind.", nameof(exportKind));

        var row = new SensitiveExportApproval
        {
            TenantId = tenantId,
            ExportKind = exportKind.Trim(),
            RequesterUserId = requesterUserId,
            ResourceId = string.IsNullOrWhiteSpace(resourceId) ? null : resourceId.Trim(),
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            Status = SensitiveExportApprovalStatuses.Pending,
            RequestedAt = DateTime.UtcNow,
        };
        _db.SensitiveExportApprovals.Add(row);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await _audit.LogSystemOperationAsync(
                action: "SENSITIVE_EXPORT_APPROVAL_REQUESTED",
                entityType: "SensitiveExportApproval",
                userId: requesterUserId,
                userRole: "Unknown",
                description: $"Sensitive export approval requested ({row.ExportKind}).",
                requestData: new { row.ExportKind, row.ResourceId, row.Reason },
                actionType: AuditEventType.SensitiveExportApprovalRequested,
                entityId: row.Id,
                tenantId: tenantId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to audit sensitive export approval request {Id}", row.Id);
        }

        return ToDto(row);
    }

    public async Task<IReadOnlyList<SensitiveExportApprovalDto>> ListMineAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.SensitiveExportApprovals.AsNoTracking()
            .Where(a => a.RequesterUserId == userId)
            .OrderByDescending(a => a.RequestedAt)
            .Take(100)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<SensitiveExportApprovalDto>> ListPendingAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.SensitiveExportApprovals.AsNoTracking()
            .Where(a => a.Status == SensitiveExportApprovalStatuses.Pending)
            .OrderBy(a => a.RequestedAt)
            .Take(200)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(ToDto).ToList();
    }

    public async Task<(bool Ok, string? Code, string? Message, SensitiveExportApprovalDto? Dto)> ApproveAsync(
        Guid id,
        string resolverUserId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        var row = await _db.SensitiveExportApprovals
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (row == null)
            return (false, "NOT_FOUND", "Approval request not found.", null);
        if (row.Status != SensitiveExportApprovalStatuses.Pending)
            return (false, "INVALID_STATUS", "Only pending requests can be approved.", null);

        row.Status = SensitiveExportApprovalStatuses.Approved;
        row.ResolvedByUserId = resolverUserId;
        row.ResolvedAt = DateTime.UtcNow;
        row.ResolutionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        row.ValidUntil = DateTime.UtcNow.AddHours(Math.Max(1, _options.DownloadLinkTtlHours));
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await _audit.LogSystemOperationAsync(
                action: "SENSITIVE_EXPORT_APPROVAL_APPROVED",
                entityType: "SensitiveExportApproval",
                userId: resolverUserId,
                userRole: Roles.SuperAdmin,
                description: $"Sensitive export approved ({row.ExportKind}).",
                requestData: new { row.ExportKind, row.ResourceId, requester = row.RequesterUserId },
                actionType: AuditEventType.SensitiveExportApprovalApproved,
                entityId: row.Id,
                tenantId: row.TenantId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to audit sensitive export approval {Id}", row.Id);
        }

        return (true, null, null, ToDto(row));
    }

    public async Task<(bool Ok, string? Code, string? Message, SensitiveExportApprovalDto? Dto)> RejectAsync(
        Guid id,
        string resolverUserId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        var row = await _db.SensitiveExportApprovals
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (row == null)
            return (false, "NOT_FOUND", "Approval request not found.", null);
        if (row.Status != SensitiveExportApprovalStatuses.Pending)
            return (false, "INVALID_STATUS", "Only pending requests can be rejected.", null);

        row.Status = SensitiveExportApprovalStatuses.Rejected;
        row.ResolvedByUserId = resolverUserId;
        row.ResolvedAt = DateTime.UtcNow;
        row.ResolutionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await _audit.LogSystemOperationAsync(
                action: "SENSITIVE_EXPORT_APPROVAL_REJECTED",
                entityType: "SensitiveExportApproval",
                userId: resolverUserId,
                userRole: Roles.SuperAdmin,
                description: $"Sensitive export rejected ({row.ExportKind}).",
                requestData: new { row.ExportKind, row.ResourceId, requester = row.RequesterUserId },
                actionType: AuditEventType.SensitiveExportApprovalRejected,
                entityId: row.Id,
                tenantId: row.TenantId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to audit sensitive export rejection {Id}", row.Id);
        }

        return (true, null, null, ToDto(row));
    }

    public async Task LogDownloadAuditAsync(
        string userId,
        string userRole,
        Guid? tenantId,
        string exportKind,
        string fileName,
        long? fileSizeBytes,
        string? resourceId,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var actionType = string.Equals(exportKind, SensitiveExportKinds.SystemBackup, StringComparison.OrdinalIgnoreCase)
            ? AuditEventType.SystemBackupDownloaded
            : string.Equals(exportKind, SensitiveExportKinds.AuditLogExport, StringComparison.OrdinalIgnoreCase)
                ? AuditEventType.AuditLogExportDownloaded
                : string.Equals(exportKind, SensitiveExportKinds.GdprDataExport, StringComparison.OrdinalIgnoreCase)
                    ? AuditEventType.GdprDataExportDownloaded
                    : AuditEventType.FileDownloaded;

        try
        {
            await _audit.LogSystemOperationAsync(
                action: "FILE_DOWNLOAD",
                entityType: "Download",
                userId: userId,
                userRole: userRole,
                description: $"File download ({exportKind}): {fileName}",
                requestData: new { exportKind, fileName, fileSizeBytes, resourceId },
                correlationIdOverride: correlationId,
                actionType: actionType,
                tenantId: tenantId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write download audit for {FileName}", fileName);
        }
    }

    private async Task<SensitiveExportApproval?> FindValidApprovalAsync(
        Guid? approvalId,
        string userId,
        string kind,
        string? resourceId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var q = _db.SensitiveExportApprovals.AsNoTracking()
            .Where(a =>
                a.Status == SensitiveExportApprovalStatuses.Approved
                && a.RequesterUserId == userId
                && a.ExportKind == kind
                && a.ValidUntil != null
                && a.ValidUntil > now);

        if (approvalId.HasValue)
            q = q.Where(a => a.Id == approvalId.Value);
        if (!string.IsNullOrWhiteSpace(resourceId))
            q = q.Where(a => a.ResourceId == null || a.ResourceId == resourceId);

        return await q
            .OrderByDescending(a => a.ResolvedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<(string Token, DateTime Expires)> MintTicketAsync(
        string userId,
        Guid? tenantId,
        string kind,
        string? resourceId,
        Guid? approvalId,
        CancellationToken cancellationToken)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var expires = DateTime.UtcNow.AddHours(Math.Max(1, _options.DownloadLinkTtlHours));
        var row = new DownloadSecurityTicket
        {
            TokenHash = HashToken(raw),
            UserId = userId,
            TenantId = tenantId,
            ExportKind = kind,
            ResourceId = string.IsNullOrWhiteSpace(resourceId) ? null : resourceId.Trim(),
            ApprovalId = approvalId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expires,
        };
        _db.DownloadSecurityTickets.Add(row);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return (raw, expires);
    }

    private async Task<bool> TryConsumeTicketAsync(
        string rawToken,
        string userId,
        string kind,
        string? resourceId,
        bool markUsed,
        CancellationToken cancellationToken)
    {
        var hash = HashToken(rawToken);
        var row = await _db.DownloadSecurityTickets
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken)
            .ConfigureAwait(false);
        if (row == null)
            return false;
        if (!string.Equals(row.UserId, userId, StringComparison.Ordinal))
            return false;
        if (!string.Equals(row.ExportKind, kind, StringComparison.OrdinalIgnoreCase))
            return false;
        if (row.ExpiresAt <= DateTime.UtcNow)
            return false;
        if (row.UsedAt != null)
            return false;
        if (!string.IsNullOrWhiteSpace(resourceId)
            && !string.IsNullOrWhiteSpace(row.ResourceId)
            && !string.Equals(row.ResourceId, resourceId, StringComparison.Ordinal))
            return false;

        if (markUsed)
        {
            row.UsedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    private static string HashToken(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static SensitiveExportApprovalDto ToDto(SensitiveExportApproval row) =>
        new()
        {
            Id = row.Id,
            TenantId = row.TenantId,
            ExportKind = row.ExportKind,
            RequesterUserId = row.RequesterUserId,
            Reason = row.Reason,
            ResourceId = row.ResourceId,
            Status = row.Status,
            RequestedAt = row.RequestedAt,
            ResolvedByUserId = row.ResolvedByUserId,
            ResolvedAt = row.ResolvedAt,
            ResolutionNote = row.ResolutionNote,
            ValidUntil = row.ValidUntil,
        };
}
