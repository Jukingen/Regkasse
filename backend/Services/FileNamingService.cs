using KasseAPI_Final.Tenancy;

namespace KasseAPI_Final.Services;

/// <summary>
/// Central download / attachment file naming:
/// <c>{prefix}_{tenantSlug}[_{registerId}][_{additional}]_{yyyyMMdd_HHmmss}.{extension}</c>
/// </summary>
public interface IFileNamingService
{
    /// <summary>
    /// Builds a filesystem-safe file name from the ambient tenant slug (or override / <c>unknown</c>).
    /// </summary>
    /// <param name="prefix">Domain prefix (e.g. <c>dep-export</c>, <c>product</c>).</param>
    /// <param name="extension">Extension without or with leading dot (e.g. <c>json</c> / <c>.json</c>).</param>
    /// <param name="registerId">Optional cash-register segment.</param>
    /// <param name="additional">Optional extra segment (period, date range, etc.).</param>
    /// <param name="at">Optional stamp time (defaults to local now).</param>
    /// <param name="tenantSlug">Optional slug override when ambient accessor has no slug.</param>
    string GenerateFileName(
        string prefix,
        string extension,
        string? registerId = null,
        string? additional = null,
        DateTime? at = null,
        string? tenantSlug = null);
}

/// <inheritdoc />
public sealed class FileNamingService : IFileNamingService
{
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public FileNamingService(ICurrentTenantAccessor tenantAccessor)
    {
        _tenantAccessor = tenantAccessor;
    }

    /// <inheritdoc />
    public string GenerateFileName(
        string prefix,
        string extension,
        string? registerId = null,
        string? additional = null,
        DateTime? at = null,
        string? tenantSlug = null)
    {
        var slug = ExportFileNameSegments.Sanitize(
            !string.IsNullOrWhiteSpace(tenantSlug) ? tenantSlug : _tenantAccessor.TenantSlug,
            "unknown");
        var prefixSeg = ExportFileNameSegments.Sanitize(prefix, "file");
        var timestamp = ExportFileNameSegments.LocalStamp(at);
        var ext = NormalizeExtension(extension);

        var registerPart = string.IsNullOrWhiteSpace(registerId)
            ? ""
            : $"_{ExportFileNameSegments.Sanitize(registerId, "register")}";
        var additionalPart = string.IsNullOrWhiteSpace(additional)
            ? ""
            : $"_{ExportFileNameSegments.Sanitize(additional, "extra")}";

        return $"{prefixSeg}_{slug}{registerPart}{additionalPart}_{timestamp}.{ext}";
    }

    private static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return "bin";

        var ext = extension.Trim();
        if (ext.StartsWith(".", StringComparison.Ordinal))
            ext = ext[1..];

        return ExportFileNameSegments.Sanitize(ext, "bin").ToLowerInvariant();
    }
}
