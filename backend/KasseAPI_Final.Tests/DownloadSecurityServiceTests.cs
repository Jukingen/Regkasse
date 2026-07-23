using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.TwoFactor;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DownloadSecurityServiceTests
{
    private static AppDbContext CreateDb(Guid? tenantId = null)
    {
        var tid = tenantId ?? Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DownloadSecurity_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, new FixedTenantAccessor(tid));
    }

    private sealed class FixedTenantAccessor(Guid tenantId) : ICurrentTenantAccessor
    {
        public Guid? TenantId { get; set; } = tenantId;
        public string? TenantSlug { get; set; } = "cafe";
    }

    private static DownloadSecurityService CreateService(
        AppDbContext db,
        DownloadSecurityOptions? opts = null,
        ITwoFactorService? twoFactor = null)
    {
        var options = Options.Create(opts ?? new DownloadSecurityOptions
        {
            MaxDownloadsPerUserPerDay = 2,
            MaxFileSizeBytes = 1024,
            DownloadLinkTtlHours = 24,
            RequireApprovalForSensitiveExports = true,
            RequireTwoFactorForCriticalExports = true,
            SuperAdminMaySelfApprove = true,
        });
        var tf = twoFactor ?? Mock.Of<ITwoFactorService>();
        var users = new Mock<UserManager<ApplicationUser>>(
            Mock.Of<IUserStore<ApplicationUser>>(),
            null!, null!, null!, null!, null!, null!, null!, null!);
        return new DownloadSecurityService(
            db,
            options,
            tf,
            users.Object,
            Mock.Of<IAuditLogService>(),
            NullLogger<DownloadSecurityService>.Instance);
    }

    [Fact]
    public async Task Evaluate_blocks_when_daily_limit_exceeded()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var userId = Guid.NewGuid().ToString("D");
        for (var i = 0; i < 2; i++)
        {
            db.DownloadHistories.Add(new DownloadHistory
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = userId,
                FileName = $"f{i}.json",
                FileType = "json",
                DownloadedAt = DateTime.UtcNow,
            });
        }
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var result = await svc.EvaluateAsync(new DownloadSecurityEvaluateRequest
        {
            UserId = userId,
            UserRole = "Manager",
            TenantId = tenantId,
            ExportKind = "tenant-backup",
            PrivacyAck = true,
            IsSuperAdmin = false,
        });

        Assert.False(result.Allowed);
        Assert.Equal("DOWNLOAD_DAILY_LIMIT", result.Code);
        Assert.Equal(429, result.StatusCode);
    }

    [Fact]
    public async Task Evaluate_blocks_oversized_file()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);
        var result = await svc.EvaluateAsync(new DownloadSecurityEvaluateRequest
        {
            UserId = Guid.NewGuid().ToString("D"),
            UserRole = "Manager",
            ExportKind = "tenant-backup",
            FileSizeBytes = 2048,
            PrivacyAck = true,
            IsSuperAdmin = false,
        });

        Assert.False(result.Allowed);
        Assert.Equal("DOWNLOAD_FILE_TOO_LARGE", result.Code);
        Assert.Equal(413, result.StatusCode);
    }

    [Fact]
    public async Task Evaluate_requires_privacy_ack_for_sensitive()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);
        var result = await svc.EvaluateAsync(new DownloadSecurityEvaluateRequest
        {
            UserId = Guid.NewGuid().ToString("D"),
            UserRole = Roles.SuperAdmin,
            ExportKind = SensitiveExportKinds.GdprDataExport,
            PrivacyAck = false,
            IsSuperAdmin = true,
        });

        Assert.False(result.Allowed);
        Assert.Equal("SENSITIVE_EXPORT_ACK_REQUIRED", result.Code);
    }

    [Fact]
    public async Task Evaluate_super_admin_self_approve_gdpr_without_approval_row()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);
        var result = await svc.EvaluateAsync(new DownloadSecurityEvaluateRequest
        {
            UserId = Guid.NewGuid().ToString("D"),
            UserRole = Roles.SuperAdmin,
            ExportKind = SensitiveExportKinds.GdprDataExport,
            PrivacyAck = true,
            IsSuperAdmin = true,
        });

        Assert.True(result.Allowed);
        Assert.False(string.IsNullOrWhiteSpace(result.DownloadTicket));
        Assert.NotNull(result.TicketExpiresAtUtc);
    }

    [Fact]
    public async Task Evaluate_requires_approval_for_non_super_admin()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);
        var result = await svc.EvaluateAsync(new DownloadSecurityEvaluateRequest
        {
            UserId = Guid.NewGuid().ToString("D"),
            UserRole = "Manager",
            ExportKind = SensitiveExportKinds.GdprDataExport,
            PrivacyAck = true,
            IsSuperAdmin = false,
        });

        Assert.False(result.Allowed);
        Assert.Equal("SENSITIVE_EXPORT_APPROVAL_REQUIRED", result.Code);
    }

    [Fact]
    public async Task Evaluate_requires_2fa_for_system_backup()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);
        var result = await svc.EvaluateAsync(new DownloadSecurityEvaluateRequest
        {
            UserId = Guid.NewGuid().ToString("D"),
            UserRole = Roles.SuperAdmin,
            ExportKind = SensitiveExportKinds.SystemBackup,
            PrivacyAck = true,
            IsSuperAdmin = true,
        });

        Assert.False(result.Allowed);
        Assert.Equal("SENSITIVE_EXPORT_2FA_REQUIRED", result.Code);
    }
}
