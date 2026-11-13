using System.ComponentModel.DataAnnotations;
using KasseAPI_Final.Models.Enums;

namespace KasseAPI_Final.Services.Communication;

public sealed class BulkEmailRequest
{
    /// <summary>Optional explicit tenant ids. When null/empty, all tenants matching filters are targeted.</summary>
    public List<Guid>? TenantIds { get; set; }

    [Required]
    [MaxLength(500)]
    public string Subject { get; set; } = string.Empty;

    /// <summary>HTML body supported.</summary>
    [Required]
    [MaxLength(100_000)]
    public string Body { get; set; } = string.Empty;

    public LicenseType? FilterByLicenseType { get; set; }

    public TenantStatus? FilterByStatus { get; set; }
}

public sealed class BulkEmailResult
{
    public int TotalAttempted { get; set; }
    public int TotalSent { get; set; }
    public int TotalFailed { get; set; }
    public List<string> FailedEmails { get; set; } = [];
}

public sealed class BulkEmailPreviewRequest
{
    public List<Guid>? TenantIds { get; set; }
    public LicenseType? FilterByLicenseType { get; set; }
    public TenantStatus? FilterByStatus { get; set; }
}

public sealed class BulkEmailPreviewResult
{
    public int RecipientCount { get; set; }
    public int TenantCount { get; set; }
}

public interface IBulkEmailService
{
    Task<BulkEmailResult> SendBulkAsync(BulkEmailRequest request, CancellationToken cancellationToken = default);

    Task<BulkEmailPreviewResult> PreviewAsync(
        BulkEmailPreviewRequest request,
        CancellationToken cancellationToken = default);
}

public interface IBulkEmailRateLimiter
{
    /// <summary>Returns error message when the per-minute cap would be exceeded.</summary>
    string? TryAcquireOrError(int emailCount);
}
