using System.Net.Mail;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Constants;
using KasseAPI_Final.Services.Activity;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.DemoRequests;

public interface IDemoRequestService
{
    Task<DemoRequestCreateResult> CreateAsync(CreateDemoRequestDto? body, string? clientIp, CancellationToken cancellationToken = default);

    Task<DemoRequestListDto> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default);
}

public enum DemoRequestCreateStatus
{
    Created,
    Invalid,
    RateLimited,
}

public sealed class DemoRequestCreateResult
{
    public DemoRequestCreateStatus Status { get; init; }
    public DemoRequestCreatedDto? Created { get; init; }
    public DemoRequestErrorDto? Error { get; init; }
}

public sealed class DemoRequestService : IDemoRequestService
{
    public const int MaxMessageLength = 4000;
    public const string ValidationCode = "VALIDATION_ERROR";

    private readonly AppDbContext _db;
    private readonly DemoRequestRateLimiter _rateLimiter;
    private readonly IActivityEventPublisher _activity;

    public DemoRequestService(
        AppDbContext db,
        DemoRequestRateLimiter rateLimiter,
        IActivityEventPublisher activity)
    {
        _db = db;
        _rateLimiter = rateLimiter;
        _activity = activity;
    }

    public async Task<DemoRequestCreateResult> CreateAsync(
        CreateDemoRequestDto? body,
        string? clientIp,
        CancellationToken cancellationToken = default)
    {
        var error = Validate(body);
        if (error is not null)
        {
            return new DemoRequestCreateResult
            {
                Status = DemoRequestCreateStatus.Invalid,
                Error = error,
            };
        }

        if (!_rateLimiter.TryConsume(clientIp ?? "unknown"))
        {
            return new DemoRequestCreateResult
            {
                Status = DemoRequestCreateStatus.RateLimited,
                Error = new DemoRequestErrorDto
                {
                    Code = DemoRequestRateLimiter.RateLimitedCode,
                    Message = "Demo request rate limit exceeded.",
                },
            };
        }

        var now = DateTime.UtcNow;
        var row = new DemoRequest
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            IsActive = true,
            Name = body!.Name!.Trim(),
            Email = body.Email!.Trim(),
            Company = string.IsNullOrWhiteSpace(body.Company) ? null : body.Company.Trim(),
            Industry = body.Industry!.Trim(),
            Message = string.IsNullOrWhiteSpace(body.Message) ? null : body.Message.Trim(),
            Status = DemoRequestStatuses.New,
        };

        _db.DemoRequests.Add(row);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _activity.TryPublishAsync(
            new ActivityEventPublishRequest(
                SystemTenantIds.Platform,
                ActivityEventType.DemoRequestReceived,
                "Demo request received",
                EntityType: "demo_request",
                EntityId: row.Id.ToString(),
                Metadata: new Dictionary<string, object>
                {
                    ["requestId"] = row.Id,
                    ["email"] = row.Email,
                    ["industry"] = row.Industry,
                }),
            cancellationToken).ConfigureAwait(false);

        return new DemoRequestCreateResult
        {
            Status = DemoRequestCreateStatus.Created,
            Created = new DemoRequestCreatedDto
            {
                Id = row.Id,
                Status = row.Status,
            },
        };
    }

    public async Task<DemoRequestListDto> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var safePage = page < 1 ? 1 : page;
        var safeSize = pageSize < 1 ? 20 : Math.Min(pageSize, 100);

        var query = _db.DemoRequests.AsNoTracking().Where(row => row.IsActive);
        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .OrderByDescending(row => row.CreatedAtUtc)
            .Skip((safePage - 1) * safeSize)
            .Take(safeSize)
            .Select(row => new DemoRequestListItemDto
            {
                Id = row.Id,
                Name = row.Name,
                Email = row.Email,
                Company = row.Company,
                Industry = row.Industry,
                Status = row.Status,
                CreatedAtUtc = row.CreatedAtUtc,
                Message = row.Message,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new DemoRequestListDto
        {
            Items = items,
            Total = total,
            Page = safePage,
            PageSize = safeSize,
        };
    }

    internal static DemoRequestErrorDto? Validate(CreateDemoRequestDto? body)
    {
        if (body is null)
        {
            return new DemoRequestErrorDto
            {
                Code = ValidationCode,
                Message = "Request body is required.",
            };
        }

        if (string.IsNullOrWhiteSpace(body.Name) || body.Name.Trim().Length > 255)
        {
            return new DemoRequestErrorDto
            {
                Code = ValidationCode,
                Message = "Name is required.",
            };
        }

        var email = body.Email?.Trim() ?? string.Empty;
        if (email.Length is 0 or > 255 || !IsEmail(email))
        {
            return new DemoRequestErrorDto
            {
                Code = ValidationCode,
                Message = "Email is invalid.",
            };
        }

        if (body.Company is { Length: > 255 })
        {
            return new DemoRequestErrorDto
            {
                Code = ValidationCode,
                Message = "Company is too long.",
            };
        }

        var industry = body.Industry?.Trim() ?? string.Empty;
        if (!VerticalProfileIds.All.Contains(industry))
        {
            return new DemoRequestErrorDto
            {
                Code = ValidationCode,
                Message = "Industry is not a known profile.",
            };
        }

        if (body.Message is { Length: > MaxMessageLength })
        {
            return new DemoRequestErrorDto
            {
                Code = ValidationCode,
                Message = "Message is too long.",
            };
        }

        return null;
    }

    private static bool IsEmail(string value)
    {
        try
        {
            var parsed = new MailAddress(value);
            return parsed.Address.Equals(value, StringComparison.OrdinalIgnoreCase)
                && value.Contains('@', StringComparison.Ordinal)
                && value.Split('@')[1].Contains('.', StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
