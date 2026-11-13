using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Export;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FiskalyBatchServiceTests
{
    [Fact]
    public void GetLimits_ClampsToFifty()
    {
        var sut = CreateSut(receipts: Mock.Of<IFiskalyReceiptService>(MockBehavior.Strict), batchMax: 200);
        var limits = sut.GetLimits();
        Assert.Equal(50, limits.MaxItems);
        Assert.Equal(10, limits.WarnAtItems);
    }

    [Fact]
    public async Task Storno_RejectsOverMax()
    {
        var sut = CreateSut(receipts: Mock.Of<IFiskalyReceiptService>(MockBehavior.Strict), batchMax: 2);
        var items = Enumerable.Range(0, 3).Select(_ => new FiskalyBatchStornoItemRequest
        {
            CashRegisterId = Guid.NewGuid(),
            OriginalReceiptId = Guid.NewGuid()
        }).ToList();

        var ex = await Assert.ThrowsAsync<FiskalyBatchException>(() =>
            sut.StornoAsync(new FiskalyBatchStornoRequest { Items = items, Reason = "Batch storno test" }, "u1", false));
        Assert.Equal(FiskalyBatchErrorCodes.BatchTooLarge, ex.Code);
        Assert.Equal(2, ex.MaxItems);
    }

    [Fact]
    public async Task Storno_ProcessesSequentiallyAndDeduplicates()
    {
        var registerId = Guid.NewGuid();
        var paymentA = Guid.NewGuid();
        var paymentB = Guid.NewGuid();
        var receipts = new Mock<IFiskalyReceiptService>(MockBehavior.Strict);
        receipts
            .Setup(s => s.CancelReceiptAsync(registerId, paymentA, "Batch storno test", "u1", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FiskalyReceiptOperationResult.Ok(new FiskalyReceiptDataDto { ReceiptId = "c1", ReceiptNumber = "S-1" }));
        receipts
            .Setup(s => s.CancelReceiptAsync(registerId, paymentB, "Batch storno test", "u1", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FiskalyReceiptOperationResult.Fail(400, FiskalyReceiptErrorCodes.StornoFailed, "already cancelled"));

        var sut = CreateSut(receipts: receipts.Object);
        var result = await sut.StornoAsync(
            new FiskalyBatchStornoRequest
            {
                Reason = "Batch storno test",
                Items =
                [
                    new() { CashRegisterId = registerId, OriginalReceiptId = paymentA, ReceiptNumber = "R-A" },
                    new() { CashRegisterId = registerId, OriginalReceiptId = paymentA, ReceiptNumber = "R-A" },
                    new() { CashRegisterId = registerId, OriginalReceiptId = paymentB, ReceiptNumber = "R-B" }
                ]
            },
            "u1",
            false);

        Assert.Equal(2, result.Total);
        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(1, result.FailedCount);
        receipts.Verify(
            s => s.CancelReceiptAsync(registerId, paymentA, "Batch storno test", "u1", false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Sonderbelege_RejectsSchlussbeleg()
    {
        var sut = CreateSut(receipts: Mock.Of<IFiskalyReceiptService>(MockBehavior.Strict));
        var ex = await Assert.ThrowsAsync<FiskalyBatchException>(() =>
            sut.SonderbelegeAsync(
                new FiskalyBatchSonderbelegeRequest
                {
                    Kind = FiskalyOperationTypes.Schlussbeleg,
                    CashRegisterIds = [Guid.NewGuid()]
                },
                "u1",
                false));
        Assert.Equal(FiskalyBatchErrorCodes.BatchForbiddenKind, ex.Code);
    }

    [Fact]
    public async Task Sonderbelege_Startbeleg_CallsReceiptServicePerRegister()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var receipts = new Mock<IFiskalyReceiptService>();
        receipts
            .Setup(s => s.CreateStartbelegAsync(It.IsAny<Guid>(), It.IsAny<string?>(), "u1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(FiskalyReceiptOperationResult.Ok(new FiskalyReceiptDataDto { ReceiptId = "s", ReceiptNumber = "1" }));

        var sut = CreateSut(receipts: receipts.Object);
        var result = await sut.SonderbelegeAsync(
            new FiskalyBatchSonderbelegeRequest
            {
                Kind = FiskalyOperationTypes.Startbeleg,
                CashRegisterIds = [a, b]
            },
            "u1",
            false);

        Assert.Equal(2, result.SuccessCount);
        receipts.Verify(s => s.CreateStartbelegAsync(a, null, "u1", It.IsAny<CancellationToken>()), Times.Once);
        receipts.Verify(s => s.CreateStartbelegAsync(b, null, "u1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DepExport_Manager_ThrowsNotFound()
    {
        var sut = CreateSut(receipts: Mock.Of<IFiskalyReceiptService>(MockBehavior.Strict));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            sut.DepExportAsync(
                new FiskalyBatchDepExportRequest
                {
                    TenantIds = [Guid.NewGuid()],
                    FromUtc = DateTime.UtcNow.AddDays(-7),
                    ToUtc = DateTime.UtcNow
                },
                "u1",
                actorIsSuperAdmin: false));
    }

    [Fact]
    public async Task DepExport_SuperAdmin_ZipsSuccessfulExports()
    {
        var tenant = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        var (db, accessor) = CreateDb(tenant);
        await using var _ = db;
        db.Tenants.Add(new Tenant
        {
            Id = tenant,
            Name = "Cafe",
            Slug = "cafe",
            Status = TenantStatuses.Active,
            IsActive = true
        });
        db.CashRegisters.Add(new CashRegister
        {
            Id = registerId,
            TenantId = tenant,
            RegisterNumber = "K-1",
            Location = "Wien",
            Status = RegisterStatus.Closed,
            IsActive = true,
            LastBalanceUpdate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var dep = new Mock<IRksvDepExportService>();
        dep.Setup(s => s.GenerateDepExportAsync(
                registerId,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                true,
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RksvDepExportRootDto());

        var history = new Mock<IDepExportHistoryService>();
        history
            .Setup(s => s.BuildFileNameAsync(tenant, registerId, It.IsAny<CancellationToken>(), It.IsAny<DateTime?>()))
            .ReturnsAsync("dep-export_cafe_K-1.json");
        history
            .Setup(s => s.RecordCompletedAsync(It.IsAny<DepExportHistoryRecordRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DepExportHistory { Id = Guid.NewGuid(), FileName = "dep-export_cafe_K-1.json" });

        var sut = CreateSut(db, accessor, Mock.Of<IFiskalyReceiptService>(MockBehavior.Strict), dep.Object, history.Object);
        var result = await sut.DepExportAsync(
            new FiskalyBatchDepExportRequest
            {
                TenantIds = [tenant],
                FromUtc = DateTime.UtcNow.AddDays(-1),
                ToUtc = DateTime.UtcNow
            },
            "sa-1",
            actorIsSuperAdmin: true);

        Assert.Equal(1, result.Summary.SuccessCount);
        Assert.True(result.ZipBytes.Length > 0);
        using var zip = new ZipArchive(new MemoryStream(result.ZipBytes), ZipArchiveMode.Read);
        Assert.Contains(zip.Entries, e => e.Name.EndsWith(".json", StringComparison.Ordinal));
        Assert.Contains(zip.Entries, e => e.Name == "manifest.json");
    }

    [Fact]
    public void BatchController_RequiresExpectedPermissions()
    {
        var type = typeof(AdminFiskalyBatchController);
        Assert.Contains(
            AppPermissions.FiskalyOperationsCancel,
            type.GetMethod(nameof(AdminFiskalyBatchController.Storno))!
                .GetCustomAttributes<HasPermissionAttribute>()
                .Select(a => a.Permission));
        Assert.Contains(
            AppPermissions.SystemCritical,
            type.GetMethod(nameof(AdminFiskalyBatchController.DepExport))!
                .GetCustomAttributes<HasPermissionAttribute>()
                .Select(a => a.Permission));
    }

    [Fact]
    public async Task BatchController_Storno_NullBody_ReturnsValidationError()
    {
        var batch = new Mock<IFiskalyBatchService>(MockBehavior.Strict);
        var controller = new AdminFiskalyBatchController(batch.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "u1"), new Claim(ClaimTypes.Role, Roles.Manager)],
                    "Test"))
            }
        };

        var result = await controller.Storno(null, CancellationToken.None);
        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        var body = Assert.IsType<FiskalyBatchErrorDto>(bad.Value);
        Assert.Equal(FiskalyBatchErrorCodes.BatchValidation, body.Code);
        batch.VerifyNoOtherCalls();
    }

    private static FiskalyBatchService CreateSut(
        IFiskalyReceiptService receipts,
        int batchMax = 50,
        IRksvDepExportService? dep = null,
        IDepExportHistoryService? history = null)
    {
        var tenant = Guid.NewGuid();
        var (db, accessor) = CreateDb(tenant);
        return CreateSut(db, accessor, receipts, dep, history, batchMax);
    }

    private static FiskalyBatchService CreateSut(
        AppDbContext db,
        ICurrentTenantAccessor accessor,
        IFiskalyReceiptService receipts,
        IRksvDepExportService? dep = null,
        IDepExportHistoryService? history = null,
        int batchMax = 50)
    {
        var options = Options.Create(new FiskalyOptions { BatchMaxItems = batchMax, BatchWarnAtItems = 10 });
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

        return new FiskalyBatchService(
            options,
            receipts,
            dep ?? Mock.Of<IRksvDepExportService>(MockBehavior.Strict),
            history ?? Mock.Of<IDepExportHistoryService>(MockBehavior.Strict),
            db,
            accessor,
            audit.Object,
            NullLogger<FiskalyBatchService>.Instance);
    }

    private static (AppDbContext Db, ICurrentTenantAccessor Accessor) CreateDb(Guid ambientTenant)
    {
        var accessor = TenantTestDoubles.TenantAccessorReturning(ambientTenant);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"fiskaly_batch_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return (new AppDbContext(options, accessor), accessor);
    }
}
