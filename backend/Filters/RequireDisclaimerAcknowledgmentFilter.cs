using KasseAPI_Final.Configuration;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Filters;

/// <summary>
/// Ensures RKSV § 8 export disclaimer was acknowledged (<see cref="FiscalExportDisclaimerHeaders.AcknowledgedHeaderName"/>).
/// </summary>
public sealed class RequireDisclaimerAcknowledgmentFilter : IAsyncActionFilter
{
    private readonly IOptions<FiscalExportOptions> _options;
    private readonly ILogger<RequireDisclaimerAcknowledgmentFilter> _logger;

    public RequireDisclaimerAcknowledgmentFilter(
        IOptions<FiscalExportOptions> options,
        ILogger<RequireDisclaimerAcknowledgmentFilter> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var opt = _options.Value;
        if (!opt.RequireDisclaimerAcknowledgment)
        {
            await next();
            return;
        }

        if (FiscalExportDisclaimerHeaders.IsAcknowledged(context.HttpContext.Request))
        {
            await next();
            return;
        }

        if (opt.LogFailedAttempts)
        {
            var ctx = context.HttpContext;
            var userId = ctx.User.GetActorUserId() ?? "unknown";
            var ip = ctx.Connection.RemoteIpAddress?.ToString();
            _logger.LogWarning(
                "Fiscal export invoked without disclaimer acknowledgment (possible bypass attempt): userId={UserId}, ip={ClientIp}, method={Method}, path={Path}",
                userId,
                ip ?? "unknown",
                ctx.Request.Method,
                ctx.Request.Path.Value ?? string.Empty);
        }

        context.Result = new ObjectResult(new FiscalExportDisclaimerRequiredResponseDto())
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
    }
}
