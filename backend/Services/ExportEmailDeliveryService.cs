using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

public sealed class ExportEmailSendInput
{
    public required Guid TenantId { get; init; }
    public required string UserId { get; init; }
    public required string To { get; init; }
    public required string Subject { get; init; }
    public string? Message { get; init; }
    public DateTime? ScheduledForUtc { get; init; }
    public string? SourceKind { get; init; }
    public Guid? SourceId { get; init; }
    public bool PreferLink { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required byte[] Content { get; init; }
}

public interface IExportEmailDeliveryService
{
    Task<SendExportEmailResponse> SendOrScheduleAsync(
        ExportEmailSendInput input,
        CancellationToken cancellationToken = default);

    Task<ExportEmailDeliveryListResponse> ListAsync(
        Guid tenantId,
        string? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<SendExportEmailResponse?> CancelScheduledAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<int> ProcessDueSchedulesAsync(CancellationToken cancellationToken = default);

    Task<(Stream Stream, string FileName, string ContentType)?> TryOpenDownloadByTokenAsync(
        string rawToken,
        CancellationToken cancellationToken = default);
}

public static class ExportEmailDeliveryRules
{
    public static bool ShouldUseLink(long fileSizeBytes, long maxAttachmentBytes, bool preferLink) =>
        preferLink || fileSizeBytes > maxAttachmentBytes;

    public static string BuildPublicDownloadLink(ExportEmailOptions options, string rawToken)
    {
        var baseUrl = (options.PublicApiBaseUrl ?? "https://api.regkasse.at").Trim().TrimEnd('/');
        var path = (options.DownloadPathTemplate ?? "/data/export-email/{token}").Trim();
        if (!path.StartsWith('/'))
            path = "/" + path;
        path = path.Replace("{token}", Uri.EscapeDataString(rawToken), StringComparison.Ordinal);
        return baseUrl + path;
    }
}

public sealed class ExportEmailDeliveryService : IExportEmailDeliveryService
{
    private static readonly Regex EmailRx = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly AppDbContext _db;
    private readonly IExportEmailSmtpService _smtp;
    private readonly IHostEnvironment _env;
    private readonly ExportEmailOptions _options;
    private readonly ILogger<ExportEmailDeliveryService> _logger;

    public ExportEmailDeliveryService(
        AppDbContext db,
        IExportEmailSmtpService smtp,
        IHostEnvironment env,
        IOptions<ExportEmailOptions> options,
        ILogger<ExportEmailDeliveryService> logger)
    {
        _db = db;
        _smtp = smtp;
        _env = env;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SendExportEmailResponse> SendOrScheduleAsync(
        ExportEmailSendInput input,
        CancellationToken cancellationToken = default)
    {
        var to = input.To.Trim();
        if (!EmailRx.IsMatch(to))
            throw new ArgumentException("Invalid recipient email.", nameof(input));

        var subject = input.Subject.Trim();
        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("Subject is required.", nameof(input));

        var fileName = string.IsNullOrWhiteSpace(input.FileName) ? "export.bin" : input.FileName.Trim();
        var contentType = string.IsNullOrWhiteSpace(input.ContentType)
            ? "application/octet-stream"
            : input.ContentType.Trim();
        var content = input.Content ?? Array.Empty<byte>();
        if (content.Length == 0)
            throw new ArgumentException("Export content is empty.", nameof(input));

        var maxAttach = Math.Max(1024, _options.MaxAttachmentBytes);
        var useLink = ExportEmailDeliveryRules.ShouldUseLink(content.Length, maxAttach, input.PreferLink);
        var deliveryMode = useLink ? ExportEmailDeliveryModes.Link : ExportEmailDeliveryModes.Attachment;

        var now = DateTime.UtcNow;
        var scheduledFor = input.ScheduledForUtc;
        if (scheduledFor.HasValue)
        {
            if (scheduledFor.Value.Kind == DateTimeKind.Unspecified)
                scheduledFor = DateTime.SpecifyKind(scheduledFor.Value, DateTimeKind.Utc);
            else
                scheduledFor = scheduledFor.Value.ToUniversalTime();

            if (scheduledFor.Value <= now.AddMinutes(1))
                scheduledFor = null;
        }

        var row = new ExportEmailDelivery
        {
            TenantId = input.TenantId,
            UserId = input.UserId,
            RecipientEmail = to,
            Subject = subject.Length > 500 ? subject[..500] : subject,
            Message = string.IsNullOrWhiteSpace(input.Message) ? null : input.Message.Trim(),
            FileName = fileName,
            ContentType = contentType.Length > 128 ? contentType[..128] : contentType,
            FileSizeBytes = content.LongLength,
            DeliveryMode = deliveryMode,
            SourceKind = string.IsNullOrWhiteSpace(input.SourceKind) ? null : input.SourceKind.Trim(),
            SourceId = input.SourceId,
            ScheduledForUtc = scheduledFor,
            Status = scheduledFor.HasValue
                ? ExportEmailDeliveryStatuses.Scheduled
                : ExportEmailDeliveryStatuses.Pending,
            CreatedAtUtc = now,
        };

        var storageRoot = ResolveStorageRoot();
        Directory.CreateDirectory(storageRoot);
        var relative = Path.Combine(input.TenantId.ToString("N"), $"{row.Id:N}_{SanitizeFileName(fileName)}");
        var absolute = Path.Combine(storageRoot, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        await File.WriteAllBytesAsync(absolute, content, cancellationToken).ConfigureAwait(false);
        row.ArtifactRelativePath = relative.Replace('\\', '/');

        if (useLink)
        {
            var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            row.DownloadTokenHash = HashToken(rawToken);
            var ttlHours = Math.Clamp(_options.DownloadLinkTtlHours, 1, 168);
            row.DownloadExpiresAtUtc = now.AddHours(ttlHours);
            await File.WriteAllTextAsync(absolute + ".token", rawToken, cancellationToken).ConfigureAwait(false);
        }

        _db.ExportEmailDeliveries.Add(row);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (scheduledFor.HasValue)
            return ToResponse(row, "Export email scheduled.");

        await DeliverRowAsync(row, cancellationToken).ConfigureAwait(false);
        return ToResponse(row, null);
    }

    public async Task<ExportEmailDeliveryListResponse> ListAsync(
        Guid tenantId,
        string? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var q = _db.ExportEmailDeliveries.AsNoTracking().Where(x => x.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(status))
        {
            var s = status.Trim().ToLowerInvariant();
            q = q.Where(x => x.Status == s);
        }

        var total = await q.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await q
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ExportEmailDeliveryListItemDto
            {
                Id = x.Id,
                RecipientEmail = x.RecipientEmail,
                Subject = x.Subject,
                FileName = x.FileName,
                FileSizeBytes = x.FileSizeBytes,
                DeliveryMode = x.DeliveryMode,
                Status = x.Status,
                SourceKind = x.SourceKind,
                SourceId = x.SourceId,
                ScheduledForUtc = x.ScheduledForUtc,
                SentAtUtc = x.SentAtUtc,
                CreatedAtUtc = x.CreatedAtUtc,
                ErrorMessage = x.ErrorMessage,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new ExportEmailDeliveryListResponse
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<SendExportEmailResponse?> CancelScheduledAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var row = await _db.ExportEmailDeliveries
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
            return null;
        if (row.Status != ExportEmailDeliveryStatuses.Scheduled)
            throw new InvalidOperationException("Only scheduled deliveries can be cancelled.");

        row.Status = ExportEmailDeliveryStatuses.Cancelled;
        row.ErrorMessage = null;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToResponse(row, "Scheduled export email cancelled.");
    }

    public async Task<int> ProcessDueSchedulesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var due = await _db.ExportEmailDeliveries
            .Where(x =>
                x.Status == ExportEmailDeliveryStatuses.Scheduled
                && x.ScheduledForUtc != null
                && x.ScheduledForUtc <= now)
            .OrderBy(x => x.ScheduledForUtc)
            .Take(20)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var sent = 0;
        foreach (var row in due)
        {
            try
            {
                await DeliverRowAsync(row, cancellationToken).ConfigureAwait(false);
                if (row.Status == ExportEmailDeliveryStatuses.Sent)
                    sent++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed scheduled export email {Id}", row.Id);
            }
        }

        return sent;
    }

    public async Task<(Stream Stream, string FileName, string ContentType)?> TryOpenDownloadByTokenAsync(
        string rawToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken) || rawToken.Length > 128)
            return null;

        var hash = HashToken(rawToken.Trim());
        var now = DateTime.UtcNow;
        var row = await _db.ExportEmailDeliveries
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x =>
                    x.DownloadTokenHash == hash
                    && x.DeliveryMode == ExportEmailDeliveryModes.Link
                    && x.Status == ExportEmailDeliveryStatuses.Sent
                    && x.DownloadExpiresAtUtc != null
                    && x.DownloadExpiresAtUtc > now,
                cancellationToken)
            .ConfigureAwait(false);

        if (row?.ArtifactRelativePath is null)
            return null;

        var absolute = Path.Combine(ResolveStorageRoot(), row.ArtifactRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(absolute))
            return null;

        var stream = new FileStream(absolute, FileMode.Open, FileAccess.Read, FileShare.Read);
        return (stream, row.FileName, row.ContentType);
    }

    private async Task DeliverRowAsync(ExportEmailDelivery row, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(row.ArtifactRelativePath))
        {
            row.Status = ExportEmailDeliveryStatuses.Failed;
            row.ErrorMessage = "Missing export artifact.";
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var absolute = Path.Combine(
            ResolveStorageRoot(),
            row.ArtifactRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(absolute))
        {
            row.Status = ExportEmailDeliveryStatuses.Failed;
            row.ErrorMessage = "Export artifact file not found.";
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var bytes = await File.ReadAllBytesAsync(absolute, cancellationToken).ConfigureAwait(false);
        byte[]? attachBytes = null;
        string? attachName = null;
        var body = BuildBody(row, absolute, bytes, ref attachBytes, ref attachName);

        try
        {
            if (!_smtp.IsConfigured)
                throw new InvalidOperationException("SMTP is not configured.");

            await _smtp
                .SendAsync(
                    [row.RecipientEmail],
                    row.Subject,
                    body,
                    attachName,
                    attachBytes,
                    attachName is null ? null : row.ContentType,
                    cancellationToken)
                .ConfigureAwait(false);

            row.Status = ExportEmailDeliveryStatuses.Sent;
            row.SentAtUtc = DateTime.UtcNow;
            row.ErrorMessage = null;
        }
        catch (Exception ex)
        {
            row.Status = ExportEmailDeliveryStatuses.Failed;
            row.ErrorMessage = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
            _logger.LogWarning(ex, "Export email delivery failed for {Id}", row.Id);
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private string BuildBody(
        ExportEmailDelivery r,
        string absolute,
        byte[] bytes,
        ref byte[]? attachment,
        ref string? attachmentFileName)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(r.Message))
        {
            sb.AppendLine(r.Message.Trim());
            sb.AppendLine();
        }

        sb.AppendLine($"Datei: {r.FileName}");
        sb.AppendLine($"Größe: {FormatSize(r.FileSizeBytes)}");

        if (r.DeliveryMode == ExportEmailDeliveryModes.Link)
        {
            attachment = null;
            attachmentFileName = null;
            var tokenPath = absolute + ".token";
            string? rawToken = null;
            if (File.Exists(tokenPath))
                rawToken = File.ReadAllText(tokenPath).Trim();

            if (string.IsNullOrWhiteSpace(rawToken))
            {
                rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
                r.DownloadTokenHash = HashToken(rawToken);
                var ttlHours = Math.Clamp(_options.DownloadLinkTtlHours, 1, 168);
                r.DownloadExpiresAtUtc = DateTime.UtcNow.AddHours(ttlHours);
                File.WriteAllText(tokenPath, rawToken);
            }

            var link = ExportEmailDeliveryRules.BuildPublicDownloadLink(_options, rawToken);
            sb.AppendLine();
            sb.AppendLine("Die Datei ist zu groß für einen E-Mail-Anhang (oder Link-Versand wurde gewählt).");
            sb.AppendLine("Download-Link (zeitlich begrenzt):");
            sb.AppendLine(link);
            if (r.DownloadExpiresAtUtc.HasValue)
                sb.AppendLine($"Gültig bis (UTC): {r.DownloadExpiresAtUtc:yyyy-MM-dd HH:mm}");
        }
        else
        {
            attachment = bytes;
            attachmentFileName = r.FileName;
            sb.AppendLine();
            sb.AppendLine("Der Export ist dieser E-Mail als Anhang beigefügt.");
        }

        sb.AppendLine();
        sb.AppendLine("— Regkasse");
        return sb.ToString();
    }

    private string ResolveStorageRoot()
    {
        var relative = string.IsNullOrWhiteSpace(_options.StorageRelativePath)
            ? "App_Data/export-email"
            : _options.StorageRelativePath.Trim();
        return Path.IsPathRooted(relative)
            ? relative
            : Path.GetFullPath(Path.Combine(_env.ContentRootPath, relative));
    }

    private static SendExportEmailResponse ToResponse(ExportEmailDelivery row, string? message) =>
        new()
        {
            Id = row.Id,
            Status = row.Status,
            DeliveryMode = row.DeliveryMode,
            ScheduledForUtc = row.ScheduledForUtc,
            SentAtUtc = row.SentAtUtc,
            RecipientEmail = row.RecipientEmail,
            FileName = row.FileName,
            FileSizeBytes = row.FileSizeBytes,
            Message = message,
        };

    private static string HashToken(string rawToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        var cleaned = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "export.bin" : cleaned;
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        var mb = bytes / (1024d * 1024d);
        if (mb >= 1) return $"{mb:0.#} MB";
        return $"{bytes / 1024d:0.#} KB";
    }
}
