using System.Text;
using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Export;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.Rksv;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Tse;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DepExportValidationServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DepExportValidation_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, new FixedTenantAccessor(SystemTenantIds.Platform));
    }

    private sealed class FixedTenantAccessor(Guid tenantId) : ICurrentTenantAccessor
    {
        public Guid? TenantId { get; set; } = tenantId;
        public string? TenantSlug { get; set; }
    }

    private static RksvDepExportService CreateDepExportService(AppDbContext db)
    {
        var env = new Mock<IRksvEnvironmentService>();
        env.Setup(x => x.IsDemoMode()).Returns(true);
        env.Setup(x => x.IsProductionMode()).Returns(false);
        env.Setup(x => x.IsTseSimulated()).Returns(true);

        return new RksvDepExportService(
            db,
            new SoftwareTseKeyProvider(),
            env.Object,
            Mock.Of<IRksvDepPrueftoolRunner>(),
            Mock.Of<ILogger<RksvDepExportService>>());
    }

    private static DepExportValidationService CreateService(
        AppDbContext db,
        Mock<IActivityEventService>? activity = null) =>
        new(
            db,
            CreateDepExportService(db),
            (activity ?? new Mock<IActivityEventService>()).Object,
            Mock.Of<ILogger<DepExportValidationService>>());

    private static string ToBmfJson(RksvDepExportRootDto root) =>
        JsonSerializer.Serialize(root);

    private static string MakeCompactJws(string payloadText)
    {
        static string B64Url(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        var header = B64Url(Encoding.UTF8.GetBytes("""{"alg":"ES256"}"""));
        var payload = B64Url(Encoding.UTF8.GetBytes(payloadText));
        return $"{header}.{payload}.c2ln";
    }

    private static RksvDepExportRootDto ValidRoot(params string[] jws) =>
        new()
        {
            BelegeGruppe =
            [
                new RksvDepBelegeGruppeDto
                {
                    Signaturzertifikat = "CERT",
                    Zertifizierungsstellen = ["CA"],
                    BelegeKompakt = jws.ToList(),
                },
            ],
        };

    private static async Task<DepExportHistory> SeedCompletedExportAsync(
        AppDbContext db,
        int signatureCount = 1,
        string? storagePath = null)
    {
        TenantTestDoubles.EnsurePlatformTenant(db);
        var row = new DepExportHistory
        {
            TenantId = SystemTenantIds.Platform,
            CashRegisterId = Guid.NewGuid(),
            FromUtc = DateTime.UtcNow.AddDays(-30),
            ToUtc = DateTime.UtcNow,
            ExportedAt = DateTime.UtcNow,
            ExportedByUserId = "user-1",
            FileName = "dep-export_test.json",
            FileSizeBytes = 100,
            SignatureCount = signatureCount,
            GroupCount = 1,
            Status = DepExportStatus.Completed.ToString(),
            StoragePath = storagePath,
            ValidationStatus = DepExportValidationStatuses.Pending,
        };
        db.DepExportHistories.Add(row);
        await db.SaveChangesAsync();
        return row;
    }

    [Fact]
    public void BuildJsonStructureCheck_Passes_WhenFormatValid()
    {
        var format = new RksvDepExportValidationResult
        {
            IsValid = true,
            BelegeGruppeCount = 1,
            BelegCount = 2,
        };

        var check = DepExportValidationService.BuildJsonStructureCheck(format);

        Assert.True(check.Passed);
        Assert.Equal("JSON Structure", check.Name);
    }

    [Fact]
    public void BuildJsonStructureCheck_Fails_WhenBelegeGruppeMissing()
    {
        var format = new RksvDepExportValidationResult
        {
            IsValid = false,
            Errors = ["Missing Belege-Gruppe property."],
        };

        var check = DepExportValidationService.BuildJsonStructureCheck(format);

        Assert.False(check.Passed);
    }

    [Fact]
    public void BuildSignatureChainCheck_Fails_OnCountMismatch()
    {
        var json = ToBmfJson(ValidRoot("a.b.c"));
        var format = new RksvDepExportValidationResult { IsValid = true, BelegCount = 1 };

        var check = DepExportValidationService.BuildSignatureChainCheck(json, format, expectedSignatureCount: 5);

        Assert.False(check.Passed);
        Assert.Contains("mismatch", check.Details, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildCertificateCheck_Fails_WithoutGroups()
    {
        var format = new RksvDepExportValidationResult { IsValid = true };
        var check = DepExportValidationService.BuildCertificateCheck("{}", format);

        Assert.False(check.Passed);
    }

    [Fact]
    public void BuildTaxRateCheck_Fails_OnNegativeAmounts()
    {
        var payload = JsonSerializer.Serialize(new BelegdatenPayload
        {
            BetragSatzNormal = -1m,
        });
        var json = ToBmfJson(ValidRoot(MakeCompactJws(payload)));

        var check = DepExportValidationService.BuildTaxRateCheck(json);

        Assert.False(check.Passed);
        Assert.Contains("negative", check.Details, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildTaxRateCheck_Passes_OnNonNegativeAmounts()
    {
        var payload = JsonSerializer.Serialize(new BelegdatenPayload
        {
            BetragSatzNormal = 10.5m,
            BetragSatzErmaessigt1 = 0m,
        });
        var json = ToBmfJson(ValidRoot(MakeCompactJws(payload)));

        var check = DepExportValidationService.BuildTaxRateCheck(json);

        Assert.True(check.Passed);
    }

    [Fact]
    public void TryDecodeJwsPayload_DecodesMiddleSegment()
    {
        var decoded = DepExportValidationService.TryDecodeJwsPayload(MakeCompactJws("""{"x":1}"""));
        Assert.Equal("""{"x":1}""", decoded);
    }

    [Fact]
    public async Task ValidateExportAsync_PersistsPassedStatus()
    {
        await using var db = CreateDb();
        var row = await SeedCompletedExportAsync(db, signatureCount: 1);
        var jws = MakeCompactJws(JsonSerializer.Serialize(new BelegdatenPayload { BetragSatzNormal = 1m }));
        var json = ToBmfJson(ValidRoot(jws));
        var activity = new Mock<IActivityEventService>();

        var result = await CreateService(db, activity).ValidateExportAsync(row.Id, json);

        Assert.True(result.IsValid);
        Assert.Equal(4, result.Checks.Count);
        Assert.All(result.Checks, c => Assert.True(c.Passed));

        await db.Entry(row).ReloadAsync();
        Assert.Equal(DepExportValidationStatuses.Passed, row.ValidationStatus);
        Assert.NotNull(row.ValidatedAt);
        Assert.False(string.IsNullOrWhiteSpace(row.ValidationReportJson));
        activity.Verify(
            a => a.PublishAsync(It.IsAny<ActivityEventPublishRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ValidateExportAsync_NotifiesOnFailure()
    {
        await using var db = CreateDb();
        var row = await SeedCompletedExportAsync(db, signatureCount: 1);
        var activity = new Mock<IActivityEventService>();

        var result = await CreateService(db, activity).ValidateExportAsync(row.Id, "{}");

        Assert.False(result.IsValid);
        await db.Entry(row).ReloadAsync();
        Assert.Equal(DepExportValidationStatuses.Failed, row.ValidationStatus);
        activity.Verify(
            a => a.PublishAsync(
                It.Is<ActivityEventPublishRequest>(r => r.Type == ActivityEventType.DepExportValidationFailed),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetValidationReportAsync_AggregatesStatuses()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsurePlatformTenant(db);
        db.DepExportHistories.AddRange(
            new DepExportHistory
            {
                TenantId = SystemTenantIds.Platform,
                CashRegisterId = Guid.NewGuid(),
                FromUtc = DateTime.UtcNow.AddDays(-10),
                ToUtc = DateTime.UtcNow.AddDays(-5),
                ExportedAt = DateTime.UtcNow.AddDays(-4),
                ExportedByUserId = "u",
                FileName = "a.json",
                FileSizeBytes = 1,
                SignatureCount = 1,
                GroupCount = 1,
                Status = DepExportStatus.Completed.ToString(),
                ValidationStatus = DepExportValidationStatuses.Passed,
            },
            new DepExportHistory
            {
                TenantId = SystemTenantIds.Platform,
                CashRegisterId = Guid.NewGuid(),
                FromUtc = DateTime.UtcNow.AddDays(-3),
                ToUtc = DateTime.UtcNow,
                ExportedAt = DateTime.UtcNow.AddDays(-1),
                ExportedByUserId = "u",
                FileName = "b.json",
                FileSizeBytes = 1,
                SignatureCount = 1,
                GroupCount = 1,
                Status = DepExportStatus.Completed.ToString(),
                ValidationStatus = DepExportValidationStatuses.Failed,
            });
        await db.SaveChangesAsync();

        var report = await CreateService(db).GetValidationReportAsync(SystemTenantIds.Platform);

        Assert.Equal(2, report.TotalExports);
        Assert.Equal(1, report.PassedCount);
        Assert.Equal(1, report.FailedCount);
        Assert.False(report.AllValidatedPassed);
    }

    [Fact]
    public async Task IsExportValidAsync_ReturnsTrueOnlyWhenPassed()
    {
        await using var db = CreateDb();
        var row = await SeedCompletedExportAsync(db);
        row.ValidationStatus = DepExportValidationStatuses.Passed;
        await db.SaveChangesAsync();

        Assert.True(await CreateService(db).IsExportValidAsync(row.Id));
        Assert.False(await CreateService(db).IsExportValidAsync(Guid.NewGuid()));
    }
}
