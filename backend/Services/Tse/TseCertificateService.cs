using System.Security.Cryptography.X509Certificates;
using System.Text;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Tse;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Tse;

public sealed class TseCertificateService : ITseCertificateService
{
    private const string AuditEntityType = "TseDevice";

    private readonly AppDbContext _db;
    private readonly ITseKeyProvider _keyProvider;
    private readonly IActivityEventPublisher _activity;
    private readonly IAuditLogService _auditLog;
    private readonly IOptionsMonitor<TseOptions> _tseOptions;
    private readonly ILogger<TseCertificateService> _logger;

    public TseCertificateService(
        AppDbContext db,
        ITseKeyProvider keyProvider,
        IActivityEventPublisher activity,
        IAuditLogService auditLog,
        IOptionsMonitor<TseOptions> tseOptions,
        ILogger<TseCertificateService> logger)
    {
        _db = db;
        _keyProvider = keyProvider;
        _activity = activity;
        _auditLog = auditLog;
        _tseOptions = tseOptions;
        _logger = logger;
    }

    public async Task<TseCertificateInfoDto?> GetCertificateInfoAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        var device = await _db.TseDevices.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken)
            .ConfigureAwait(false);
        if (device is null)
            return null;

        return BuildInfo(device, TryLoadParsedCert(device));
    }

    public async Task<TseCertificateValidationResultDto> ValidateCertificateAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        var info = await GetCertificateInfoAsync(deviceId, cancellationToken).ConfigureAwait(false);
        if (info is null)
        {
            return new TseCertificateValidationResultDto
            {
                IsValid = false,
                Status = nameof(TseCertLifecycleStatus.Invalid),
                Message = "Device not found.",
                Errors = ["Device not found."],
            };
        }

        var errors = new List<string>();
        if (info.IsRevoked)
            errors.Add("Certificate is marked revoked.");
        if (info.IsExpired)
            errors.Add("Certificate is expired.");
        if (info.ExpiresAt is null && info.IssuedAt is null
            && string.Equals(info.Status, nameof(TseCertLifecycleStatus.Invalid), StringComparison.Ordinal))
            errors.Add("No certificate metadata available for this device.");

        var isValid = errors.Count == 0
                      && info.Status is nameof(TseCertLifecycleStatus.Valid)
                          or nameof(TseCertLifecycleStatus.ExpiringSoon);

        return new TseCertificateValidationResultDto
        {
            IsValid = isValid,
            Status = info.Status,
            Message = isValid
                ? "Certificate validation passed."
                : string.Join(" ", errors),
            Certificate = info,
            Errors = errors,
        };
    }

    public async Task<TseCertificateRenewalResultDto> RenewCertificateAsync(
        Guid deviceId,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        var device = await _db.TseDevices
            .FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken)
            .ConfigureAwait(false);
        if (device is null)
        {
            return new TseCertificateRenewalResultDto
            {
                Success = false,
                Outcome = "NotFound",
                Message = "Device not found.",
            };
        }

        if (string.Equals(device.CertificateStatus, "REVOKED", StringComparison.OrdinalIgnoreCase))
        {
            return new TseCertificateRenewalResultDto
            {
                Success = false,
                Outcome = "Revoked",
                Message = "Cannot renew a revoked device certificate. Provision a new device instead.",
            };
        }

        // Prefer syncing metadata from the process key provider (fiskaly config / soft cert).
        var synced = TrySyncFromKeyProvider(device);
        if (synced)
        {
            device.ScheduledRenewalAt = null;
            device.ExpiryWarningSentAt = null;
            device.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var info = BuildInfo(device, TryLoadParsedCert(device));
            await TryAuditAsync(
                    "TSE_CERTIFICATE_SYNCED",
                    actorUserId ?? "system",
                    device.TenantId,
                    device.Id,
                    "TSE certificate metadata synced from key provider.",
                    cancellationToken)
                .ConfigureAwait(false);

            if (device.TenantId is { } tid && tid != Guid.Empty)
            {
                await _activity.TryPublishAsync(
                        tid,
                        ActivityEventType.TseCertificateRenewed,
                        new
                        {
                            DeviceId = device.Id.ToString("D"),
                            ExpiresAt = device.ExpiresAt,
                            Source = "KeyProviderSync",
                        },
                        actorUserId: actorUserId ?? "system",
                        dedupKey: $"tse-cert-renewed:{device.Id:N}:{DateTime.UtcNow:yyyyMMddHH}",
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }

            return new TseCertificateRenewalResultDto
            {
                Success = true,
                Outcome = "SyncedFromKeyProvider",
                Message =
                    "Certificate metadata refreshed from the configured TSE key provider. "
                    + "Vendor private-key rotation (fiskaly dashboard / SigningCertificateDerBase64) is still required when the cert itself changed.",
                Certificate = info,
            };
        }

        // Soft/demo: synthesize a fresh validity window on the device row when no key material exists.
        var opts = _tseOptions.CurrentValue;
        if (opts.UseSoftTseWhenNoDevice
            || opts.IsFakeSigningMode
            || opts.IsOff)
        {
            device.IssuedAt = DateTime.UtcNow;
            device.ExpiresAt = DateTime.UtcNow.AddYears(1);
            device.CertificateStatus = "VALID";
            device.ScheduledRenewalAt = null;
            device.ExpiryWarningSentAt = null;
            device.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return new TseCertificateRenewalResultDto
            {
                Success = true,
                Outcome = "SoftMetadataRenewed",
                Message =
                    "Soft/Demo mode: device certificate dates were rotated locally. No vendor certificate was issued.",
                Certificate = BuildInfo(device, null),
            };
        }

        // Real mode without loadable cert: schedule ops renewal for tomorrow if unset.
        var scheduleAt = device.ScheduledRenewalAt is { } existing && existing > DateTime.UtcNow
            ? existing
            : DateTime.UtcNow.Date.AddDays(1);

        device.ScheduledRenewalAt = scheduleAt;
        device.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (device.TenantId is { } tenantId && tenantId != Guid.Empty)
        {
            await _activity.TryPublishAsync(
                    tenantId,
                    ActivityEventType.TseCertificateRenewalScheduled,
                    new
                    {
                        DeviceId = device.Id.ToString("D"),
                        ScheduledRenewalAt = scheduleAt,
                    },
                    actorUserId: actorUserId ?? "system",
                    dedupKey: $"tse-cert-schedule:{device.Id:N}:{scheduleAt:yyyyMMdd}",
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        return new TseCertificateRenewalResultDto
        {
            Success = false,
            Outcome = "ScheduledPendingVendor",
            Message =
                "No local signing certificate material is available to sync. "
                + $"Renewal was scheduled for {scheduleAt:u}. Update fiskaly SigningCertificateDerBase64 (or vendor SCU), then call renew/sync again.",
            Certificate = BuildInfo(device, null),
        };
    }

    public async Task<TseCertificateRenewalResultDto> ScheduleCertificateRenewalAsync(
        Guid deviceId,
        DateTime renewalDateUtc,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (renewalDateUtc.Kind == DateTimeKind.Unspecified)
            renewalDateUtc = DateTime.SpecifyKind(renewalDateUtc, DateTimeKind.Utc);
        else
            renewalDateUtc = renewalDateUtc.ToUniversalTime();

        if (renewalDateUtc < DateTime.UtcNow.AddMinutes(-5))
        {
            return new TseCertificateRenewalResultDto
            {
                Success = false,
                Outcome = "InvalidDate",
                Message = "renewalDateUtc must be in the future.",
            };
        }

        var device = await _db.TseDevices
            .FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken)
            .ConfigureAwait(false);
        if (device is null)
        {
            return new TseCertificateRenewalResultDto
            {
                Success = false,
                Outcome = "NotFound",
                Message = "Device not found.",
            };
        }

        device.ScheduledRenewalAt = renewalDateUtc;
        device.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await TryAuditAsync(
                "TSE_CERTIFICATE_RENEWAL_SCHEDULED",
                actorUserId ?? "system",
                device.TenantId,
                device.Id,
                $"Certificate renewal scheduled for {renewalDateUtc:u}.",
                cancellationToken)
            .ConfigureAwait(false);

        if (device.TenantId is { } tid && tid != Guid.Empty)
        {
            await _activity.TryPublishAsync(
                    tid,
                    ActivityEventType.TseCertificateRenewalScheduled,
                    new
                    {
                        DeviceId = device.Id.ToString("D"),
                        ScheduledRenewalAt = renewalDateUtc,
                    },
                    actorUserId: actorUserId ?? "system",
                    dedupKey: $"tse-cert-schedule:{device.Id:N}:{renewalDateUtc:yyyyMMddHHmm}",
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        return new TseCertificateRenewalResultDto
        {
            Success = true,
            Outcome = "Scheduled",
            Message = $"Certificate renewal scheduled for {renewalDateUtc:u}.",
            Certificate = BuildInfo(device, TryLoadParsedCert(device)),
        };
    }

    public async Task ProcessExpiryWarningsAsync(CancellationToken cancellationToken = default)
    {
        var warnDays = Math.Clamp(_tseOptions.CurrentValue.CertificateExpiringSoonDays, 1, 90);
        var now = DateTime.UtcNow;
        var windowEnd = now.AddDays(warnDays);

        var devices = await _db.TseDevices
            .Where(d => d.IsActive && d.ExpiresAt != null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var device in devices)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (device.ExpiresAt is not { } exp)
                continue;

            var tenantId = device.TenantId;
            if (tenantId is null || tenantId == Guid.Empty)
                continue;

            var alreadyWarned = device.ExpiryWarningSentAt is { } sent
                                && sent > now.AddDays(-warnDays);

            if (exp <= now)
            {
                if (alreadyWarned && string.Equals(device.CertificateStatus, "EXPIRED", StringComparison.OrdinalIgnoreCase))
                    continue;

                device.CertificateStatus = "EXPIRED";
                device.ExpiryWarningSentAt = now;
                device.UpdatedAt = now;

                await _activity.TryPublishAsync(
                        tenantId.Value,
                        ActivityEventType.TseCertificateExpired,
                        new
                        {
                            DeviceId = device.Id.ToString("D"),
                            SerialNumber = device.SerialNumber,
                            ExpiresAt = exp,
                        },
                        actorUserId: "system",
                        dedupKey: $"tse-cert-expired:{device.Id:N}:{exp:yyyyMMdd}",
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            if (exp <= windowEnd)
            {
                if (alreadyWarned)
                    continue;

                device.ExpiryWarningSentAt = now;
                device.UpdatedAt = now;

                await _activity.TryPublishAsync(
                        tenantId.Value,
                        ActivityEventType.TseCertificateExpiringSoon,
                        new
                        {
                            DeviceId = device.Id.ToString("D"),
                            SerialNumber = device.SerialNumber,
                            ExpiresAt = exp,
                            DaysRemaining = Math.Ceiling((exp - now).TotalDays),
                        },
                        actorUserId: "system",
                        dedupKey: $"tse-cert-expiring:{device.Id:N}:{exp:yyyyMMdd}",
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private bool TrySyncFromKeyProvider(TseDevice device)
    {
        try
        {
            var bytes = _keyProvider.GetCertificateBytes();
            if (bytes is null || bytes.Length == 0)
                return false;

            using var cert = LoadX509(bytes);
            if (cert is null)
                return false;

            device.Certificate = Convert.ToBase64String(cert.Export(X509ContentType.Cert));
            device.IssuedAt = cert.NotBefore.ToUniversalTime();
            device.ExpiresAt = cert.NotAfter.ToUniversalTime();
            device.CertificateStatus = cert.NotAfter.ToUniversalTime() <= DateTime.UtcNow
                ? "EXPIRED"
                : "VALID";
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Key provider certificate sync failed for device {DeviceId}", device.Id);
            return false;
        }
    }

    private ParsedCert? TryLoadParsedCert(TseDevice device)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(device.Certificate)
                && LooksLikeCertMaterial(device.Certificate!))
            {
                var bytes = DecodeCertMaterial(device.Certificate!);
                using var cert = LoadX509(bytes);
                if (cert is not null)
                    return ParsedCert.From(cert);
            }

            var providerBytes = _keyProvider.GetCertificateBytes();
            if (providerBytes is { Length: > 0 })
            {
                using var cert = LoadX509(providerBytes);
                if (cert is not null)
                    return ParsedCert.From(cert) with { Source = "KeyProvider" };
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse TSE certificate for device {DeviceId}", device.Id);
        }

        return null;
    }

    private TseCertificateInfoDto BuildInfo(TseDevice device, ParsedCert? parsed)
    {
        var warnDays = Math.Clamp(_tseOptions.CurrentValue.CertificateExpiringSoonDays, 1, 90);
        var now = DateTime.UtcNow;

        var issued = parsed?.NotBefore ?? device.IssuedAt;
        var expires = parsed?.NotAfter ?? device.ExpiresAt;
        var isRevoked = string.Equals(device.CertificateStatus, "REVOKED", StringComparison.OrdinalIgnoreCase)
                        || device.HealthStatus == TseHealthStatus.Revoked;
        var isExpired = expires is { } e && e <= now
                        || string.Equals(device.CertificateStatus, "EXPIRED", StringComparison.OrdinalIgnoreCase);

        var status = ResolveStatus(isRevoked, isExpired, expires, now, warnDays, parsed is not null || expires is not null);
        var warnings = new List<TseCertificateWarningDto>();

        if (isRevoked)
        {
            warnings.Add(new TseCertificateWarningDto
            {
                Code = "REVOKED",
                Severity = "Critical",
                Message = "Certificate / device is marked revoked.",
            });
        }
        else if (isExpired)
        {
            warnings.Add(new TseCertificateWarningDto
            {
                Code = "EXPIRED",
                Severity = "Critical",
                Message = "Certificate has expired. Fiscal signing may fail.",
            });
        }
        else if (status == TseCertLifecycleStatus.ExpiringSoon && expires is { } expSoon)
        {
            warnings.Add(new TseCertificateWarningDto
            {
                Code = "EXPIRING_SOON",
                Severity = "Warning",
                Message = $"Certificate expires in {Math.Ceiling((expSoon - now).TotalDays)} day(s) ({expSoon:u}).",
            });
        }

        if (device.ScheduledRenewalAt is { } scheduled)
        {
            warnings.Add(new TseCertificateWarningDto
            {
                Code = "RENEWAL_SCHEDULED",
                Severity = "Info",
                Message = $"Certificate renewal is scheduled for {scheduled:u}.",
            });
        }

        if (parsed is null && expires is null)
        {
            warnings.Add(new TseCertificateWarningDto
            {
                Code = "NO_MATERIAL",
                Severity = "Warning",
                Message =
                    "No parseable certificate material on the device row and key provider returned none. Status is metadata-only.",
            });
        }

        return new TseCertificateInfoDto
        {
            DeviceRowId = device.Id,
            VendorDeviceId = device.DeviceId,
            SerialNumber = device.SerialNumber,
            CertificateSerialNumber = parsed?.SerialNumber ?? _keyProvider.GetCertificateSerialNumber(),
            Thumbprint = parsed?.Thumbprint ?? _keyProvider.GetCurrentCertificateThumbprint(),
            Issuer = parsed?.Issuer,
            Subject = parsed?.Subject,
            IssuedAt = issued,
            ExpiresAt = expires,
            TimeUntilExpiryDays = expires is { } ex
                ? Math.Round((ex - now).TotalDays, 2)
                : null,
            IsExpired = isExpired,
            IsRevoked = isRevoked,
            Status = status.ToString(),
            Source = parsed?.Source ?? (expires is not null ? "DeviceMetadata" : "Unavailable"),
            ScheduledRenewalAt = device.ScheduledRenewalAt,
            Warnings = warnings,
        };
    }

    private static TseCertLifecycleStatus ResolveStatus(
        bool isRevoked,
        bool isExpired,
        DateTime? expires,
        DateTime now,
        int warnDays,
        bool hasAnyCertSignal)
    {
        if (isRevoked)
            return TseCertLifecycleStatus.Revoked;
        if (isExpired)
            return TseCertLifecycleStatus.Expired;
        if (!hasAnyCertSignal)
            return TseCertLifecycleStatus.Invalid;
        if (expires is { } e && e <= now.AddDays(warnDays))
            return TseCertLifecycleStatus.ExpiringSoon;
        return TseCertLifecycleStatus.Valid;
    }

    private static bool LooksLikeCertMaterial(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        if (value.Contains("BEGIN CERTIFICATE", StringComparison.OrdinalIgnoreCase))
            return true;
        // Thumbprint-only strings are short hex; real Base64 DER is much longer.
        if (value.Length < 80)
            return false;
        try
        {
            _ = Convert.FromBase64String(value.Trim());
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static byte[] DecodeCertMaterial(string value)
    {
        if (value.Contains("BEGIN CERTIFICATE", StringComparison.OrdinalIgnoreCase))
            return Encoding.UTF8.GetBytes(value);

        return Convert.FromBase64String(value.Trim());
    }

    private static X509Certificate2? LoadX509(byte[] bytes)
    {
        try
        {
            var pem = Encoding.UTF8.GetString(bytes);
            if (pem.Contains("-----BEGIN", StringComparison.Ordinal))
                return X509Certificate2.CreateFromPem(pem);

#pragma warning disable SYSLIB0057
            return new X509Certificate2(bytes);
#pragma warning restore SYSLIB0057
        }
        catch
        {
            return null;
        }
    }

    private async Task TryAuditAsync(
        string action,
        string userId,
        Guid? tenantId,
        Guid entityId,
        string description,
        CancellationToken cancellationToken)
    {
        try
        {
            await _auditLog.LogSystemOperationAsync(
                action,
                AuditEntityType,
                userId: userId,
                userRole: "SuperAdmin",
                description: description,
                status: AuditLogStatus.Success,
                entityId: entityId,
                tenantId: tenantId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TSE certificate audit failed for {Action} {EntityId}", action, entityId);
        }

        _ = cancellationToken;
    }

    private sealed record ParsedCert(
        string? SerialNumber,
        string? Thumbprint,
        string? Issuer,
        string? Subject,
        DateTime NotBefore,
        DateTime NotAfter,
        string Source)
    {
        public static ParsedCert From(X509Certificate2 cert, string source = "DeviceColumn") =>
            new(
                cert.SerialNumber,
                cert.Thumbprint?.ToUpperInvariant(),
                cert.Issuer,
                cert.Subject,
                cert.NotBefore.ToUniversalTime(),
                cert.NotAfter.ToUniversalTime(),
                source);
    }
}
