using System.Text;
using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed class LicenseExportResult
{
    public required byte[] Content { get; init; }
    public required string ContentType { get; init; }
    public required string FileName { get; init; }
}

public interface ILicenseExportService
{
    /// <summary>
    /// Single issued license as plain text.
    /// Filename: <c>license_{tenantSlug}_{stamp}.txt</c>.
    /// </summary>
    Task<LicenseExportResult?> ExportSingleAsync(
        Guid issuedLicenseId,
        string? tenantSlug,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk licenses as JSON or CSV via <see cref="ILicenseExportReportService"/>.
    /// Filename: <c>licenses_{tenantSlug}_{stamp}.{json|csv}</c>.
    /// </summary>
    Task<LicenseExportResult> ExportMultipleAsync(
        string? tenantSlug,
        string format,
        LicenseExportFilters filters,
        CancellationToken cancellationToken = default);
}

/// <summary>License file downloads with canonical names.</summary>
public sealed class LicenseExportService : ILicenseExportService
{
    private readonly AppDbContext _db;
    private readonly ILicenseExportReportService _report;
    private readonly IFileNamingService _fileNaming;
    private readonly ILogger<LicenseExportService> _logger;

    public LicenseExportService(
        AppDbContext db,
        ILicenseExportReportService report,
        IFileNamingService fileNaming,
        ILogger<LicenseExportService> logger)
    {
        _db = db;
        _report = report;
        _fileNaming = fileNaming;
        _logger = logger;
    }

    public async Task<LicenseExportResult?> ExportSingleAsync(
        Guid issuedLicenseId,
        string? tenantSlug,
        CancellationToken cancellationToken = default)
    {
        var row = await _db.IssuedLicenses.AsNoTracking()
            .Where(il => il.Id == issuedLicenseId && !il.IsDeleted)
            .Select(il => new
            {
                il.LicenseKey,
                il.CustomerName,
                il.ExpiryAtUtc,
                il.SignedJwt,
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
            return null;

        var slugHint = !string.IsNullOrWhiteSpace(tenantSlug)
            ? tenantSlug
            : TrySlugFromCustomerName(row.CustomerName);

        var fileName = _fileNaming.GenerateFileName(
            LicenseExportFileNames.SinglePrefix,
            "txt",
            tenantSlug: slugHint);
        var sb = new StringBuilder();
        sb.AppendLine(row.LicenseKey);
        if (!string.IsNullOrWhiteSpace(row.SignedJwt))
            sb.AppendLine(row.SignedJwt);
        sb.AppendLine($"# customer={row.CustomerName}");
        sb.AppendLine($"# expiryUtc={row.ExpiryAtUtc:o}");

        _logger.LogInformation(
            "License single export created. IssuedLicenseId={Id}, FileName={FileName}",
            issuedLicenseId,
            fileName);

        return new LicenseExportResult
        {
            Content = Encoding.UTF8.GetBytes(sb.ToString()),
            ContentType = LicenseExportFileNames.SingleContentType,
            FileName = fileName,
        };
    }

    public async Task<LicenseExportResult> ExportMultipleAsync(
        string? tenantSlug,
        string format,
        LicenseExportFilters filters,
        CancellationToken cancellationToken = default)
    {
        var normalized = LicenseExportFileNames.NormalizeMultipleExtension(format);
        var fileName = _fileNaming.GenerateFileName(
            LicenseExportFileNames.MultiplePrefix,
            normalized,
            tenantSlug: tenantSlug);

        byte[] content;
        if (normalized == "csv")
        {
            content = await _report.BuildCsvAsync(filters, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            (content, _) = await _report.BuildJsonAsync(filters, cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation(
            "License bulk export created. Format={Format}, FileName={FileName}",
            normalized,
            fileName);

        return new LicenseExportResult
        {
            Content = content,
            ContentType = LicenseExportFileNames.ContentTypeForMultipleFormat(normalized),
            FileName = fileName,
        };
    }

    /// <summary>Best-effort slug from customer name when no tenant context (e.g. REGK key segment).</summary>
    internal static string? TrySlugFromCustomerName(string? customerName)
    {
        if (string.IsNullOrWhiteSpace(customerName))
            return null;
        return customerName.Trim();
    }
}
