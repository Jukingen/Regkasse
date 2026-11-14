using System.Net.Http.Headers;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Streamed tabular export. Super Admin receives every tenant. Every other caller is limited to the ambient tenant by the EF query filters.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/export")]
public sealed class AdminTenantDataExportController : ControllerBase
{
    private readonly ITenantDataExportService _export;
    private readonly IAuthorizationService _authorization;

    public AdminTenantDataExportController(ITenantDataExportService export, IAuthorizationService authorization)
    {
        _export = export;
        _authorization = authorization;
    }

    /// <summary>GET /api/admin/export/{type}?format=csv|xlsx</summary>
    [HttpGet("{type}")]
    [Produces("text/csv", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Export(
        string type,
        [FromQuery] string format = "csv",
        CancellationToken cancellationToken = default)
    {
        if (!TenantDataExportService.TryNormalizeType(type, out var normalizedType))
        {
            return BadRequest(new
            {
                message = "Export type must be products, customers, receipts, payments, users, orders, audit_logs, inventory, or tse_signatures.",
                code = "TENANT_EXPORT_TYPE",
            });
        }

        if (!TenantDataExportService.TryNormalizeFormat(format, out var normalizedFormat))
        {
            return BadRequest(new
            {
                message = "Export format must be csv or xlsx.",
                code = "TENANT_EXPORT_FORMAT",
            });
        }

        var permission = PermissionFor(normalizedType);
        var allowed = await _authorization
            .AuthorizeAsync(User, resource: null, PermissionCatalog.PolicyPrefix + permission)
            .ConfigureAwait(false);
        if (!allowed.Succeeded)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Forbidden",
                code = "TENANT_EXPORT_FORBIDDEN",
            });
        }

        if (normalizedType == "tse_signatures" && !User.IsInRole(Roles.SuperAdmin))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Forbidden",
                code = "TENANT_EXPORT_FORBIDDEN",
            });
        }

        var includeAllTenants = User.IsInRole(Roles.SuperAdmin);
        var fileName = TenantDataExportService.FileNameFor(normalizedType, normalizedFormat, DateTime.UtcNow);
        var contentType = TenantDataExportService.ContentTypeFor(normalizedFormat);

        return new StreamedFileResult(contentType, fileName, (stream, token) =>
            _export.WriteAsync(stream, normalizedType, normalizedFormat, includeAllTenants, token));
    }

    private static string PermissionFor(string type) => type switch
    {
        "products" => AppPermissions.ProductView,
        "customers" => AppPermissions.CustomerView,
        "receipts" => AppPermissions.SaleView,
        "payments" => AppPermissions.PaymentView,
        "users" => AppPermissions.UserView,
        "orders" => AppPermissions.OrderView,
        "audit_logs" => AppPermissions.AuditView,
        "inventory" => AppPermissions.InventoryView,
        "tse_signatures" => AppPermissions.SystemCritical,
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    private sealed class StreamedFileResult : IActionResult
    {
        private readonly string _contentType;
        private readonly string _fileName;
        private readonly Func<Stream, CancellationToken, Task> _write;

        public StreamedFileResult(string contentType, string fileName, Func<Stream, CancellationToken, Task> write)
        {
            _contentType = contentType;
            _fileName = fileName;
            _write = write;
        }

        public async Task ExecuteResultAsync(ActionContext context)
        {
            var response = context.HttpContext.Response;
            response.StatusCode = StatusCodes.Status200OK;
            response.ContentType = _contentType;
            var disposition = new ContentDispositionHeaderValue("attachment")
            {
                FileNameStar = _fileName,
            };
            response.Headers.ContentDisposition = disposition.ToString();
            await _write(response.Body, context.HttpContext.RequestAborted).ConfigureAwait(false);
        }
    }
}
