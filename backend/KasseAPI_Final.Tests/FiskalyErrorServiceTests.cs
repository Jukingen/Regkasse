using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Caching;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using System.Reflection;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FiskalyErrorServiceTests
{
    [Fact]
    public void Build_ComputesKpisTopErrorsAndWeekly()
    {
        var tenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var tenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var from = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 8, 3, 23, 59, 59, DateTimeKind.Utc);
        var rows = new[]
        {
            Row(tenantA, "TIMEOUT", "normal", from.AddHours(1), "A", review: "open"),
            Row(tenantA, "TIMEOUT", "cancel", from.AddHours(2), "A", review: "open"),
            Row(tenantB, "VALIDATION", "normal", from.AddDays(2), "B", review: "resolved"),
        };

        var dto = FiskalyErrorService.Build(from, to, null, totalOperations: 10, rows);

        Assert.Equal(3, dto.Kpis.TotalErrors);
        Assert.Equal(30, dto.Kpis.ErrorRatePercent);
        Assert.Equal("TIMEOUT", dto.Kpis.MostCommonErrorCode);
        Assert.Equal(2, dto.Kpis.MostCommonErrorCount);
        Assert.Equal(tenantA, dto.Kpis.TenantWithMostErrorsId);
        Assert.Equal(2, dto.Kpis.TenantWithMostErrorsCount);
        Assert.Equal(2, dto.Kpis.OpenCount);
        Assert.Equal(1, dto.Kpis.ResolvedCount);
        Assert.Equal(3, dto.Daily.Count);
        Assert.Equal(2, dto.Daily[0].Count);
        Assert.Equal(0, dto.Daily[1].Count);
        Assert.Equal(2, dto.TopErrors.Count);
        Assert.NotEmpty(dto.Weekly);
        Assert.Equal(3, dto.Weekly.Sum(w => w.Count));
    }

    [Fact]
    public void Build_Empty_ZeroRate()
    {
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var dto = FiskalyErrorService.Build(from, to, null, 0, []);
        Assert.Equal(0, dto.Kpis.TotalErrors);
        Assert.Equal(0, dto.Kpis.ErrorRatePercent);
        Assert.Null(dto.Kpis.MostCommonErrorCode);
        Assert.Equal(2, dto.Daily.Count);
        Assert.NotEmpty(dto.Weekly);
    }

    [Fact]
    public async Task ListAndStats_Manager_SeesOnlyAmbientTenantFailedRows()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var (db, accessor) = CreateDb(tenantA);
        await using var _ = db;
        db.FiskalyOperationHistories.Add(History(tenantA, FiskalyOperationTypes.Normal, "Failed", "E1"));
        db.FiskalyOperationHistories.Add(History(tenantA, FiskalyOperationTypes.Normal, "Success", null));
        db.FiskalyOperationHistories.Add(History(tenantB, FiskalyOperationTypes.Cancel, "Failed", "E2"));
        await db.SaveChangesAsync();

        var sut = CreateSut(db, accessor);
        var stats = await sut.GetStatsAsync(new FiskalyErrorQuery { TenantId = tenantB }, actorIsSuperAdmin: false);
        var list = await sut.ListAsync(new FiskalyErrorQuery { TenantId = tenantB }, actorIsSuperAdmin: false);

        Assert.Equal(1, stats.Kpis.TotalErrors);
        Assert.Equal(50, stats.Kpis.ErrorRatePercent);
        Assert.Equal("E1", stats.Kpis.MostCommonErrorCode);
        Assert.Equal(tenantA, stats.TenantId);
        Assert.Single(list.Items);
        Assert.Equal("E1", list.Items[0].ErrorCode);
    }

    [Fact]
    public async Task Stats_SuperAdmin_SeesAllThenFiltersTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var (db, accessor) = CreateDb(tenantA);
        await using var _ = db;
        db.FiskalyOperationHistories.Add(History(tenantA, FiskalyOperationTypes.Normal, "Failed", "A"));
        db.FiskalyOperationHistories.Add(History(tenantB, FiskalyOperationTypes.Nullbeleg, "Failed", "B"));
        await db.SaveChangesAsync();

        var sut = CreateSut(db, accessor);
        var all = await sut.GetStatsAsync(new FiskalyErrorQuery(), actorIsSuperAdmin: true);
        Assert.Equal(2, all.Kpis.TotalErrors);
        Assert.Null(all.TenantId);

        var filtered = await sut.GetStatsAsync(
            new FiskalyErrorQuery { TenantId = tenantB },
            actorIsSuperAdmin: true);
        Assert.Equal(1, filtered.Kpis.TotalErrors);
        Assert.Equal(tenantB, filtered.TenantId);
        Assert.Equal("B", filtered.Kpis.MostCommonErrorCode);
    }

    [Fact]
    public async Task List_FiltersSearchReviewAndErrorCode()
    {
        var tenant = Guid.NewGuid();
        var (db, accessor) = CreateDb(tenant);
        await using var _ = db;
        db.FiskalyOperationHistories.Add(History(tenant, FiskalyOperationTypes.Normal, "Failed", "E_CLIENT_ERROR", review: FiskalyErrorReviewStatuses.Open, receipt: "R-100", message: "bad credentials"));
        db.FiskalyOperationHistories.Add(History(tenant, FiskalyOperationTypes.Cancel, "Failed", "TIMEOUT", review: FiskalyErrorReviewStatuses.Resolved, receipt: "R-200", message: "timeout"));
        await db.SaveChangesAsync();

        var sut = CreateSut(db, accessor);
        var byReceipt = await sut.ListAsync(new FiskalyErrorQuery { Search = "R-100" }, actorIsSuperAdmin: false);
        Assert.Single(byReceipt.Items);
        Assert.Equal("E_CLIENT_ERROR", byReceipt.Items[0].ErrorCode);
        Assert.NotNull(byReceipt.Items[0].KnownSolution);

        var byMessage = await sut.ListAsync(new FiskalyErrorQuery { Search = "timeout" }, actorIsSuperAdmin: false);
        Assert.Single(byMessage.Items);

        var byReview = await sut.ListAsync(
            new FiskalyErrorQuery { ReviewStatus = FiskalyErrorReviewStatuses.Resolved },
            actorIsSuperAdmin: false);
        Assert.Single(byReview.Items);
        Assert.Equal("TIMEOUT", byReview.Items[0].ErrorCode);

        var byCode = await sut.ListAsync(new FiskalyErrorQuery { ErrorCode = "E_CLIENT_ERROR" }, actorIsSuperAdmin: false);
        Assert.Single(byCode.Items);
    }

    [Fact]
    public async Task GetById_SanitizesPayload_AndReturnsKnownSolution()
    {
        var tenant = Guid.NewGuid();
        var id = Guid.NewGuid();
        var (db, accessor) = CreateDb(tenant);
        await using var _ = db;
        db.FiskalyOperationHistories.Add(History(
            tenant,
            FiskalyOperationTypes.Normal,
            "Failed",
            FiskalyKnownErrorSolutions.ClientError,
            id: id,
            requestJson: """{"apiKey":"super-secret","cashRegisterId":"11111111-1111-1111-1111-111111111111"}""",
            responseJson: """{"stackTrace":"at Fiskaly.Sign() in Sign.cs:line 12","message":"denied"}"""));
        await db.SaveChangesAsync();

        var sut = CreateSut(db, accessor);
        var detail = await sut.GetByIdAsync(id, actorIsSuperAdmin: false);
        Assert.NotNull(detail);
        Assert.Equal(FiskalyKnownErrorSolutions.ClientError, detail!.KnownSolution?.Code);
        Assert.Contains("***", detail.RequestPayloadJson);
        Assert.DoesNotContain("super-secret", detail.RequestPayloadJson);
        Assert.Equal("at Fiskaly.Sign() in Sign.cs:line 12", detail.StackTrace);
    }

    [Fact]
    public async Task GetById_OtherTenantOrSuccess_ReturnsNull()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var successId = Guid.NewGuid();
        var (db, accessor) = CreateDb(tenantA);
        await using var _ = db;
        db.FiskalyOperationHistories.Add(History(tenantB, FiskalyOperationTypes.Cancel, "Failed", "E2", id: otherId));
        db.FiskalyOperationHistories.Add(History(tenantA, FiskalyOperationTypes.Normal, "Success", null, id: successId));
        await db.SaveChangesAsync();

        var sut = CreateSut(db, accessor);
        Assert.Null(await sut.GetByIdAsync(otherId, actorIsSuperAdmin: false));
        Assert.Null(await sut.GetByIdAsync(successId, actorIsSuperAdmin: false));
        Assert.NotNull(await sut.GetByIdAsync(otherId, actorIsSuperAdmin: true));
    }

    [Fact]
    public async Task SetReview_UpdatesFailedRow_AndHidesOtherTenantFromManager()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var id = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var (db, accessor) = CreateDb(tenantA);
        await using var _ = db;
        db.FiskalyOperationHistories.Add(History(tenantA, FiskalyOperationTypes.Normal, "Failed", "E1", id: id));
        db.FiskalyOperationHistories.Add(History(tenantB, FiskalyOperationTypes.Cancel, "Failed", "E2", id: otherId));
        await db.SaveChangesAsync();

        var sut = CreateSut(db, accessor);
        var updated = await sut.SetReviewStatusAsync(id, "resolved", "actor-1", actorIsSuperAdmin: false);
        Assert.Equal(FiskalyErrorReviewStatuses.Resolved, updated.ReviewStatus);
        Assert.Equal("actor-1", updated.ReviewedByUserId);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            sut.SetReviewStatusAsync(otherId, "known_issue", "actor-1", actorIsSuperAdmin: false));

        var known = await sut.SetReviewStatusAsync(id, "known_issue", "actor-1", actorIsSuperAdmin: true);
        Assert.Equal(FiskalyErrorReviewStatuses.KnownIssue, known.ReviewStatus);
    }

    [Fact]
    public async Task SetReview_SuccessRow_Throws()
    {
        var tenant = Guid.NewGuid();
        var id = Guid.NewGuid();
        var (db, accessor) = CreateDb(tenant);
        await using var _ = db;
        db.FiskalyOperationHistories.Add(History(tenant, FiskalyOperationTypes.Normal, "Success", null, id: id));
        await db.SaveChangesAsync();

        var sut = CreateSut(db, accessor);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.SetReviewStatusAsync(id, "resolved", "actor-1", actorIsSuperAdmin: false));
    }

    [Fact]
    public async Task Export_CsvAndPdf_ReturnFiles()
    {
        var tenant = Guid.NewGuid();
        var (db, accessor) = CreateDb(tenant);
        await using var _ = db;
        db.FiskalyOperationHistories.Add(History(tenant, FiskalyOperationTypes.Startbeleg, "Failed", "FON_DOWN"));
        await db.SaveChangesAsync();

        var sut = CreateSut(db, accessor);
        var csv = await sut.ExportAsync(new FiskalyErrorQuery(), "csv", actorIsSuperAdmin: false);
        Assert.Contains("text/csv", csv.ContentType, StringComparison.OrdinalIgnoreCase);
        var text = System.Text.Encoding.UTF8.GetString(csv.Bytes);
        Assert.Contains("totalErrors", text);
        Assert.Contains("FON_DOWN", text);

        var pdf = await sut.ExportAsync(new FiskalyErrorQuery(), "pdf", actorIsSuperAdmin: false);
        Assert.Equal("application/pdf", pdf.ContentType);
        Assert.EndsWith(".pdf", pdf.FileName);
        Assert.True(pdf.Bytes.Length > 100);
        Assert.Equal('%', (char)pdf.Bytes[0]);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.ExportAsync(new FiskalyErrorQuery(), "xlsx", actorIsSuperAdmin: false));
    }

    [Fact]
    public void KnownSolutions_ResolveCanonicalCodes()
    {
        Assert.Equal(FiskalyKnownErrorSolutions.ClientError, FiskalyKnownErrorSolutions.Resolve("client_error")?.Code);
        Assert.Equal("Check the SCU ID.", FiskalyKnownErrorSolutions.Resolve("E_SCU_NOT_FOUND")?.Hint);
        Assert.Equal("Check the receipt ID.", FiskalyKnownErrorSolutions.Resolve("E_RECEIPT_NOT_FOUND")?.Hint);
        Assert.Equal("Refresh the Fiskaly session.", FiskalyKnownErrorSolutions.Resolve("E_UNAUTHORIZED")?.Hint);
        Assert.Null(FiskalyKnownErrorSolutions.Resolve("UNKNOWN"));
    }

    [Fact]
    public void PayloadSanitizer_RedactsSecrets()
    {
        var json = """{"apiKey":"abc","nested":{"api_secret":"xyz"},"ok":1}""";
        var sanitized = FiskalyPayloadSanitizer.SanitizeJson(json);
        Assert.NotNull(sanitized);
        Assert.DoesNotContain("abc", sanitized);
        Assert.DoesNotContain("xyz", sanitized);
        Assert.Contains("***", sanitized);
        Assert.Contains("\"ok\":1", sanitized);
        Assert.Equal(
            "at Foo.Bar() in Foo.cs:line 9",
            FiskalyPayloadSanitizer.ExtractStackTrace("""{"stackTrace":"at Foo.Bar() in Foo.cs:line 9"}""", null));
    }

    [Fact]
    public void ErrorsController_RequiresExpectedPermissions()
    {
        var list = typeof(Controllers.AdminFiskalyErrorsController)
            .GetMethod(nameof(Controllers.AdminFiskalyErrorsController.List));
        var stats = typeof(Controllers.AdminFiskalyErrorsController)
            .GetMethod(nameof(Controllers.AdminFiskalyErrorsController.Stats));
        var detail = typeof(Controllers.AdminFiskalyErrorsController)
            .GetMethod(nameof(Controllers.AdminFiskalyErrorsController.GetById));
        var export = typeof(Controllers.AdminFiskalyErrorsController)
            .GetMethod(nameof(Controllers.AdminFiskalyErrorsController.Export));
        var resolve = typeof(Controllers.AdminFiskalyErrorsController)
            .GetMethod(nameof(Controllers.AdminFiskalyErrorsController.Resolve));
        Assert.Contains(
            AppPermissions.FiskalyHistoryView,
            list!.GetCustomAttributes<HasPermissionAttribute>().Select(a => a.Permission));
        Assert.Contains(
            AppPermissions.FiskalyHistoryView,
            stats!.GetCustomAttributes<HasPermissionAttribute>().Select(a => a.Permission));
        Assert.Contains(
            AppPermissions.FiskalyHistoryView,
            detail!.GetCustomAttributes<HasPermissionAttribute>().Select(a => a.Permission));
        Assert.Contains(
            AppPermissions.FiskalyHistoryView,
            export!.GetCustomAttributes<HasPermissionAttribute>().Select(a => a.Permission));
        Assert.Contains(
            AppPermissions.FiskalyHistoryRetry,
            resolve!.GetCustomAttributes<HasPermissionAttribute>().Select(a => a.Permission));
    }

    private static FiskalyErrorService CreateSut(AppDbContext db, ICurrentTenantAccessor accessor)
    {
        var audit = new Mock<IAuditLogService>();
        audit
            .Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());
        return new FiskalyErrorService(db, accessor, audit.Object, PassthroughCache());
    }

    private static ICacheService PassthroughCache()
    {
        var cache = new Mock<ICacheService>();
        cache.Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<FiskalyErrorStatsDto>>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, Task<FiskalyErrorStatsDto>>, TimeSpan?, CancellationToken>(
                (_, factory, _, ct) => factory(ct));
        cache.Setup(c => c.RemoveByPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return cache.Object;
    }

    private static (AppDbContext Db, ICurrentTenantAccessor Accessor) CreateDb(Guid ambientTenant)
    {
        var accessor = TenantTestDoubles.TenantAccessorReturning(ambientTenant);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"fiskaly_err_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return (new AppDbContext(options, accessor), accessor);
    }

    private static FiskalyErrorService.ErrorRow Row(
        Guid tenantId,
        string errorCode,
        string operation,
        DateTime created,
        string tenantName,
        string review = "open") =>
        new(
            Guid.NewGuid(),
            created,
            created.AddMilliseconds(40),
            operation,
            FiskalyOperationHistoryStatuses.Failed,
            errorCode,
            errorCode,
            tenantId,
            tenantName,
            "user",
            "user",
            Guid.NewGuid(),
            "R-1",
            "Kasse",
            review,
            null,
            null);

    private static FiskalyOperationHistory History(
        Guid tenantId,
        string operation,
        string status,
        string? errorCode,
        string review = FiskalyErrorReviewStatuses.Open,
        Guid? id = null,
        string? receipt = null,
        string? message = null,
        string? requestJson = null,
        string? responseJson = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            TenantId = tenantId,
            TenantName = "Tenant",
            OperationType = operation,
            Status = status,
            CashRegisterId = Guid.NewGuid(),
            ReceiptNumber = receipt ?? operation,
            UserId = "user",
            UserDisplayName = "user",
            ErrorCode = errorCode,
            ErrorMessage = message ?? errorCode,
            ErrorReviewStatus = review,
            RequestPayloadJson = requestJson ?? """{"cashRegisterId":"00000000-0000-0000-0000-000000000001"}""",
            ResponsePayloadJson = responseJson,
            CreatedAtUtc = DateTime.UtcNow,
            CompletedAtUtc = status == "Failed" ? DateTime.UtcNow : null
        };
}
