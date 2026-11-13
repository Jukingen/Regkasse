using System.Security.Cryptography;
using System.Text.RegularExpressions;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.TwoFactor;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.CriticalActions;

public sealed class CriticalActionApprovalPayload
{
    public required string UserId { get; init; }
    public required CriticalActionType ActionType { get; init; }
    public required string PathHint { get; init; }
    public DateTime ExpiresAtUtc { get; init; }
    public string? IssuedByUserId { get; init; }
}

public sealed class CriticalActionPendingRequest
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string RequesterUserId { get; init; }
    public required CriticalActionType ActionType { get; init; }
    public required string PathHint { get; init; }
    public string? Reason { get; init; }
    public DateTime RequestedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; init; }
    public string Status { get; set; } = CriticalActionPendingStatuses.Pending;
}

public static class CriticalActionPendingStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Expired = "Expired";
}

public interface ICriticalActionApprovalService
{
    /// <summary>Returns the matching critical action for a request, or null when not gated.</summary>
    CriticalActionType? MatchCriticalAction(string method, string? path);

    Task<(bool Ok, string? Token, string? ErrorCode, string? Message)> IssueWithTwoFactorAsync(
        string userId,
        CriticalActionType actionType,
        string pathHint,
        string twoFactorCode,
        CancellationToken cancellationToken = default);

    Task<(bool Ok, Guid RequestId, string? ErrorCode, string? Message)> RequestSuperAdminApprovalAsync(
        string requesterUserId,
        CriticalActionType actionType,
        string pathHint,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<(bool Ok, string? Token, string? ErrorCode, string? Message)> ApprovePendingAsync(
        Guid requestId,
        string approverUserId,
        CancellationToken cancellationToken = default);

    Task<(bool Ok, string? ErrorCode, string? Message)> RejectPendingAsync(
        Guid requestId,
        string approverUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CriticalActionPendingRequest>> ListPendingAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates and consumes a single-use approval token for the given user and path.
    /// </summary>
    Task<bool> VerifyApprovalAsync(
        string userId,
        string approvalToken,
        string requestPath,
        CancellationToken cancellationToken = default);
}

public sealed class CriticalActionApprovalService : ICriticalActionApprovalService
{
    private const string TokenCachePrefix = "critical-action:token:";
    private static readonly Regex TenantRootDeleteRegex = new(
        @"^/api/admin/tenants/[0-9a-fA-F-]{36}/?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IMemoryCache _cache;
    private readonly IOptionsMonitor<CriticalActionOptions> _options;
    private readonly ITwoFactorService _twoFactor;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IApprovalService _approvals;
    private readonly ILogger<CriticalActionApprovalService> _logger;

    public CriticalActionApprovalService(
        IMemoryCache cache,
        IOptionsMonitor<CriticalActionOptions> options,
        ITwoFactorService twoFactor,
        UserManager<ApplicationUser> userManager,
        IApprovalService approvals,
        ILogger<CriticalActionApprovalService> logger)
    {
        _cache = cache;
        _options = options;
        _twoFactor = twoFactor;
        _userManager = userManager;
        _approvals = approvals;
        _logger = logger;
    }

    public CriticalActionType? MatchCriticalAction(string method, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var normalizedPath = path.Trim();
        var normalizedMethod = (method ?? string.Empty).Trim().ToUpperInvariant();
        if (HttpMethods.IsGet(normalizedMethod)
            || HttpMethods.IsHead(normalizedMethod)
            || HttpMethods.IsOptions(normalizedMethod))
        {
            return null;
        }

        // Never gate the critical-action / approvals APIs themselves.
        if (normalizedPath.StartsWith("/api/admin/critical-actions", StringComparison.OrdinalIgnoreCase)
            || normalizedPath.StartsWith("/api/admin/approvals", StringComparison.OrdinalIgnoreCase))
            return null;

        foreach (var rule in _options.CurrentValue.ResolvePathRules())
        {
            if (!RuleMatches(rule, normalizedMethod, normalizedPath))
                continue;
            return rule.ActionType;
        }

        return null;
    }

    public async Task<(bool Ok, string? Token, string? ErrorCode, string? Message)> IssueWithTwoFactorAsync(
        string userId,
        CriticalActionType actionType,
        string pathHint,
        string twoFactorCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return (false, null, "AUTH_REQUIRED", "Authentication required.");

        var user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false);
        if (user is null)
            return (false, null, "USER_NOT_FOUND", "User not found.");

        var valid = await _twoFactor
            .VerifyTwoFactorTokenAsync(user, twoFactorCode, cancellationToken)
            .ConfigureAwait(false);
        if (!valid)
            return (false, null, "INVALID_2FA", "Invalid two-factor code.");

        var token = IssueToken(new CriticalActionApprovalPayload
        {
            UserId = userId,
            ActionType = actionType,
            PathHint = pathHint?.Trim() ?? string.Empty,
            ExpiresAtUtc = DateTime.UtcNow.Add(GetTokenTtl()),
            IssuedByUserId = userId,
        });

        _logger.LogInformation(
            "Critical action approval token issued via 2FA for user {UserId} action {ActionType}",
            userId,
            actionType);

        return (true, token, null, null);
    }

    public async Task<(bool Ok, Guid RequestId, string? ErrorCode, string? Message)> RequestSuperAdminApprovalAsync(
        string requesterUserId,
        CriticalActionType actionType,
        string pathHint,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var result = await _approvals
            .RequestApprovalAsync(
                requesterUserId,
                new DTOs.CreateApprovalRequestDto
                {
                    ActionType = actionType,
                    PathHint = pathHint,
                    Reason = reason,
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (!result.Ok || result.Dto is null)
            return (false, Guid.Empty, result.ErrorCode, result.Message);

        return (true, result.Dto.Id, null, null);
    }

    public async Task<(bool Ok, string? Token, string? ErrorCode, string? Message)> ApprovePendingAsync(
        Guid requestId,
        string approverUserId,
        CancellationToken cancellationToken = default)
    {
        var result = await _approvals
            .ApproveAsync(requestId, approverUserId, notes: null, cancellationToken)
            .ConfigureAwait(false);
        return (result.Ok, result.ApprovalToken, result.ErrorCode, result.Message);
    }

    public async Task<(bool Ok, string? ErrorCode, string? Message)> RejectPendingAsync(
        Guid requestId,
        string approverUserId,
        CancellationToken cancellationToken = default)
    {
        var result = await _approvals
            .RejectAsync(requestId, approverUserId, notes: null, cancellationToken)
            .ConfigureAwait(false);
        return (result.Ok, result.ErrorCode, result.Message);
    }

    public async Task<IReadOnlyList<CriticalActionPendingRequest>> ListPendingAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _approvals.ListPendingAsync(cancellationToken).ConfigureAwait(false);
        return rows.Select(r => new CriticalActionPendingRequest
        {
            Id = r.Id,
            RequesterUserId = r.RequestedBy,
            ActionType = Enum.TryParse<CriticalActionType>(r.ActionType, true, out var at)
                ? at
                : CriticalActionType.MassPermissionUpdate,
            PathHint = r.PathHint ?? string.Empty,
            Reason = r.Reason,
            RequestedAtUtc = r.RequestedAt,
            ExpiresAtUtc = r.ExpiresAt,
            Status = r.Status,
        }).ToList();
    }

    public Task<bool> VerifyApprovalAsync(
        string userId,
        string approvalToken,
        string requestPath,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(approvalToken))
            return Task.FromResult(false);

        var cacheKey = TokenCachePrefix + approvalToken.Trim();
        if (!_cache.TryGetValue(cacheKey, out CriticalActionApprovalPayload? payload) || payload is null)
            return Task.FromResult(false);

        // Single-use: remove before validation so concurrent reuse fails.
        _cache.Remove(cacheKey);

        if (payload.ExpiresAtUtc <= DateTime.UtcNow)
            return Task.FromResult(false);

        if (!string.Equals(payload.UserId, userId, StringComparison.Ordinal))
            return Task.FromResult(false);

        var matched = MatchCriticalAction(
            ResolveMethodForPath(requestPath),
            requestPath);

        if (matched is not null && matched != payload.ActionType)
        {
            var interchangeable =
                (matched == CriticalActionType.CurrencyChange
                 || matched == CriticalActionType.CountryChange)
                && (payload.ActionType == CriticalActionType.CurrencyChange
                    || payload.ActionType == CriticalActionType.CountryChange);

            if (!interchangeable && !PathCompatible(payload.PathHint, requestPath))
                return Task.FromResult(false);
        }
        else if (matched is null && !PathCompatible(payload.PathHint, requestPath))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    private string IssueToken(CriticalActionApprovalPayload payload)
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(tokenBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var ttl = payload.ExpiresAtUtc - DateTime.UtcNow;
        if (ttl < TimeSpan.FromSeconds(30))
            ttl = TimeSpan.FromSeconds(30);

        _cache.Set(TokenCachePrefix + token, payload, ttl);
        return token;
    }

    private TimeSpan GetTokenTtl()
    {
        var minutes = Math.Clamp(_options.CurrentValue.ApprovalTokenTtlMinutes, 2, 30);
        return TimeSpan.FromMinutes(minutes);
    }

    private static bool RuleMatches(CriticalActionPathRule rule, string method, string path)
    {
        if (rule.Methods.Count > 0
            && !rule.Methods.Any(m => string.Equals(m, method, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(rule.PathContains)
            || path.IndexOf(rule.PathContains, StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(rule.PathContainsSecondary)
            && path.IndexOf(rule.PathContainsSecondary, StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(rule.PathEndsWith)
            && !path.EndsWith(rule.PathEndsWith, StringComparison.OrdinalIgnoreCase)
            && !path.TrimEnd('/').EndsWith(rule.PathEndsWith.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (rule.PathMatchesTenantRootDelete && !TenantRootDeleteRegex.IsMatch(path.TrimEnd('/')))
            return false;

        return true;
    }

    private static string ResolveMethodForPath(string path)
    {
        if (path.Contains("/permanent", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/hard", StringComparison.OrdinalIgnoreCase)
            || TenantRootDeleteRegex.IsMatch(path.TrimEnd('/'))
            || path.Contains("/dep-export/schedule/", StringComparison.OrdinalIgnoreCase))
        {
            return "DELETE";
        }

        if (path.Contains("/decommission", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/backup/settings", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/license", StringComparison.OrdinalIgnoreCase))
        {
            return "PUT";
        }

        return "POST";
    }

    private static bool PathCompatible(string pathHint, string requestPath)
    {
        if (string.IsNullOrWhiteSpace(pathHint))
            return true;

        var hint = pathHint.Trim();
        var path = requestPath?.Trim() ?? string.Empty;
        return path.Contains(hint, StringComparison.OrdinalIgnoreCase)
               || hint.Contains(path, StringComparison.OrdinalIgnoreCase);
    }
}
