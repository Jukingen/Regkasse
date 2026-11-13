using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Reads sensitive-export security headers / query from the current HTTP request.</summary>
public static class DownloadSecurityHttp
{
    public static bool ReadPrivacyAck(HttpRequest request)
    {
        if (!request.Headers.TryGetValue(DownloadSecurityService.HeaderPrivacyAck, out var values))
            return false;
        var v = values.ToString().Trim();
        return string.Equals(v, "true", StringComparison.OrdinalIgnoreCase)
               || string.Equals(v, "1", StringComparison.OrdinalIgnoreCase)
               || string.Equals(v, "yes", StringComparison.OrdinalIgnoreCase);
    }

    public static string? ReadTwoFactorCode(HttpRequest request) =>
        request.Headers.TryGetValue(DownloadSecurityService.HeaderTwoFactor, out var values)
            ? values.ToString()
            : null;

    public static Guid? ReadApprovalId(HttpRequest request)
    {
        if (!request.Headers.TryGetValue(DownloadSecurityService.HeaderApprovalId, out var values))
            return null;
        return Guid.TryParse(values.ToString(), out var id) ? id : null;
    }

    public static string? ReadDownloadTicket(HttpRequest request)
    {
        if (request.Query.TryGetValue(DownloadSecurityService.QueryDownloadTicket, out var q)
            && !string.IsNullOrWhiteSpace(q))
            return q.ToString();
        if (request.Headers.TryGetValue(DownloadSecurityService.HeaderDownloadTicket, out var h)
            && !string.IsNullOrWhiteSpace(h))
            return h.ToString();
        return null;
    }

    public static IActionResult ToActionResult(ControllerBase controller, DownloadSecurityEvaluateResult result)
    {
        if (result.Allowed)
            return controller.Ok();
        return controller.StatusCode(result.StatusCode, result.Body);
    }
}
