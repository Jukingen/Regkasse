using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Rksv;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Time;
using KasseAPI_Final.Tse.Fiskaly;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FiskalyReceiptServiceTests
{
    [Fact]
    public async Task CreateNormal_DelegatesToSignTestAndMapsEnvelope()
    {
        var registerId = Guid.NewGuid();
        var signTest = new Mock<IFiskalySignTestService>();
        signTest
            .Setup(s => s.SignAsync(
                It.Is<FiskalySignTestRequest>(r =>
                    r.CashRegisterId == registerId
                    && r.Scenario == FiskalySignTestScenarioIds.Normal
                    && r.Amount == 12.50m
                    && r.VatRate == "STANDARD"),
                "sa-1",
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(FiskalySetupOperationResult<FiskalySignTestResultDto>.Ok(new FiskalySignTestResultDto
            {
                Success = true,
                ReceiptId = "fiskaly-1",
                ReceiptNumber = "7",
                QrCodeData = "R1-AT3_qr"
            }));

        var sut = CreateSut(signTest: signTest.Object);
        var result = await sut.CreateNormalReceiptAsync(registerId, 12.50m, "STANDARD", "sa-1", true);

        Assert.True(result.Success);
        Assert.Equal("fiskaly-1", result.Data?.ReceiptId);
        Assert.Equal("7", result.Data?.ReceiptNumber);
        Assert.Equal("R1-AT3_qr", result.Data?.QrCode);
    }

    [Fact]
    public async Task CreateNormal_LiveBlocked_MapsErrorCode()
    {
        var signTest = new Mock<IFiskalySignTestService>();
        signTest
            .Setup(s => s.SignAsync(
                It.IsAny<FiskalySignTestRequest>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(FiskalySetupOperationResult<FiskalySignTestResultDto>.Fail(
                400,
                "Test signing is not allowed against LIVE fiskaly.",
                FiskalyReceiptErrorCodes.FiskalyLiveBlocked));

        var sut = CreateSut(signTest: signTest.Object);
        var result = await sut.CreateNormalReceiptAsync(Guid.NewGuid(), null, null, "sa-1", true);

        Assert.False(result.Success);
        Assert.Equal(FiskalyReceiptErrorCodes.FiskalyLiveBlocked, result.Error?.Code);
        Assert.Contains("LIVE", result.Error?.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cancel_MissingPayment_Returns404()
    {
        var payments = new Mock<IPaymentService>();
        payments.Setup(p => p.GetPaymentAsync(It.IsAny<Guid>())).ReturnsAsync((PaymentDetails?)null);

        var sut = CreateSut(payments: payments.Object);
        var result = await sut.CancelReceiptAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Kundenwunsch Storno",
            "sa-1",
            true);

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal(FiskalyReceiptErrorCodes.PaymentNotFound, result.Error?.Code);
        payments.Verify(
            p => p.CancelPaymentAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationReasonCode>(),
                It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task Cancel_Success_MapsPaymentResult()
    {
        var registerId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var stornoId = Guid.NewGuid();
        var payments = new Mock<IPaymentService>();
        payments
            .Setup(p => p.GetPaymentAsync(paymentId))
            .ReturnsAsync(new PaymentDetails { Id = paymentId, CashRegisterId = registerId });
        payments
            .Setup(p => p.CancelPaymentAsync(
                paymentId,
                "Kundenwunsch Storno",
                "sa-1",
                null,
                CancellationReasonCode.Other,
                null))
            .ReturnsAsync(new PaymentResult
            {
                Success = true,
                PaymentId = stornoId,
                Payment = new PaymentDetails
                {
                    Id = stornoId,
                    CashRegisterId = registerId,
                    ReceiptNumber = "AT-001-20260828-2"
                },
                TseSignature = "jws",
                QrPayload = "R1-AT3_cancel"
            });

        var sut = CreateSut(payments: payments.Object);
        var result = await sut.CancelReceiptAsync(registerId, paymentId, "Kundenwunsch Storno", "sa-1", true);

        Assert.True(result.Success);
        Assert.Equal("AT-001-20260828-2", result.Data?.ReceiptNumber);
        Assert.Equal("jws", result.Data?.Signature);
        Assert.Equal("R1-AT3_cancel", result.Data?.QrCode);
    }

    [Fact]
    public async Task CreateStartbeleg_DelegatesToRksvService()
    {
        var registerId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var special = new Mock<IRksvSpecialReceiptService>();
        special
            .Setup(s => s.CreateStartbelegAsync(
                It.Is<CreateStartbelegRequest>(r => r.CashRegisterId == registerId),
                "sa-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateStartbelegResponse
            {
                ReceiptId = receiptId,
                ReceiptNumber = "AT-1",
                QrData = "R1-AT3_start"
            });

        var sut = CreateSut(special: special.Object);
        var result = await sut.CreateStartbelegAsync(registerId, "Admin Startbeleg", "sa-1");

        Assert.True(result.Success);
        Assert.Equal(receiptId.ToString("D"), result.Data?.ReceiptId);
        Assert.Equal("R1-AT3_start", result.Data?.QrCode);
    }

    [Fact]
    public async Task CreateSchlussbeleg_GuardConflict_Returns409()
    {
        var special = new Mock<IRksvSpecialReceiptService>();
        special
            .Setup(s => s.CreateSchlussbelegAsync(
                It.IsAny<CreateSchlussbelegRequest>(),
                "sa-1",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RksvOperationGuardException(
                RksvGuardErrorCodes.DuplicateSchlussbeleg,
                "Schlussbeleg already exists."));

        var sut = CreateSut(special: special.Object);
        var result = await sut.CreateSchlussbelegAsync(Guid.NewGuid(), "decommission", "sa-1");

        Assert.False(result.Success);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(RksvGuardErrorCodes.DuplicateSchlussbeleg, result.Error?.Code);
    }

    [Fact]
    public async Task CreateTagesabschluss_MissingClosing_Returns404()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateClosingDb(tenantId);
        var sut = CreateSut(db: db, fiskalyTse: MockFiskalyReady().Object, fiskalyOptions: EnabledFiskalyOptions());

        var result = await sut.CreateTagesabschlussAsync(Guid.NewGuid(), "sa-1", true);

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal(FiskalyReceiptErrorCodes.ClosingNotFound, result.Error?.Code);
    }

    [Fact]
    public async Task CreateTagesabschluss_AlreadySubmitted_IsIdempotent()
    {
        var tenantId = Guid.NewGuid();
        var closing = SeedDailyClosing(tenantId, tseSignature: "local-jws");
        closing.FiskalyStatus = DailyClosingFiskalyStatuses.Submitted;
        closing.FiskalyReceiptId = Guid.NewGuid().ToString("D");
        await using var db = CreateClosingDb(tenantId);
        db.DailyClosings.Add(closing);
        await db.SaveChangesAsync();

        var fiskaly = MockFiskalyReady();
        var sut = CreateSut(db: db, fiskalyTse: fiskaly.Object, fiskalyOptions: EnabledFiskalyOptions());
        var result = await sut.CreateTagesabschlussAsync(closing.Id, "sa-1", true);

        Assert.True(result.Success);
        Assert.Equal(closing.FiskalyReceiptId, result.Data?.ReceiptId);
        Assert.Equal("local-jws", result.Data?.Signature);
        fiskaly.Verify(
            f => f.SignTransactionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<FiskalyTransactionData>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateTagesabschluss_Success_PreservesLocalTseSignature()
    {
        var tenantId = Guid.NewGuid();
        var closing = SeedDailyClosing(tenantId, tseSignature: "local-jws");
        await using var db = CreateClosingDb(tenantId);
        db.DailyClosings.Add(closing);
        await db.SaveChangesAsync();

        var remoteId = Guid.NewGuid().ToString("D");
        var fiskaly = MockFiskalyReady();
        fiskaly
            .Setup(f => f.SignTransactionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<FiskalyTransactionData>(d =>
                    d.ReceiptType == "NORMAL" && d.TotalAmount == 0m && d.VatRate == "NULL"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FiskalySignedReceipt(
                remoteId,
                closing.CashRegisterId.ToString("D"),
                "SIGNED",
                "R1-AT3_ta",
                "42",
                "TEST",
                Signed: true,
                ReceiptType: "NORMAL"));

        var sut = CreateSut(db: db, fiskalyTse: fiskaly.Object, fiskalyOptions: EnabledFiskalyOptions());
        var result = await sut.CreateTagesabschlussAsync(closing.Id, "sa-1", true);

        Assert.True(result.Success);
        Assert.Equal(remoteId, result.Data?.ReceiptId);
        Assert.Equal("local-jws", result.Data?.Signature);
        Assert.Equal("R1-AT3_ta", result.Data?.QrCode);

        var stored = await db.DailyClosings.AsNoTracking().SingleAsync(c => c.Id == closing.Id);
        Assert.Equal(DailyClosingFiskalyStatuses.Submitted, stored.FiskalyStatus);
        Assert.Equal(remoteId, stored.FiskalyReceiptId);
        Assert.Equal("local-jws", stored.TseSignature);
        Assert.NotNull(stored.FiskalySubmittedAtUtc);
    }

    [Fact]
    public async Task CreateTagesabschluss_FiskalyFailure_StampsFailedAndKeepsTseSignature()
    {
        var tenantId = Guid.NewGuid();
        var closing = SeedDailyClosing(tenantId, tseSignature: "local-jws");
        await using var db = CreateClosingDb(tenantId);
        db.DailyClosings.Add(closing);
        await db.SaveChangesAsync();

        var fiskaly = MockFiskalyReady();
        fiskaly
            .Setup(f => f.SignTransactionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<FiskalyTransactionData>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FiskalyApiException("fiskaly rejected receipt"));

        var sut = CreateSut(db: db, fiskalyTse: fiskaly.Object, fiskalyOptions: EnabledFiskalyOptions());
        var result = await sut.CreateTagesabschlussAsync(closing.Id, "sa-1", true);

        Assert.False(result.Success);
        var stored = await db.DailyClosings.AsNoTracking().SingleAsync(c => c.Id == closing.Id);
        Assert.Equal(DailyClosingFiskalyStatuses.Failed, stored.FiskalyStatus);
        Assert.Equal("local-jws", stored.TseSignature);
        Assert.False(string.IsNullOrWhiteSpace(stored.FiskalyError));
    }

    [Fact]
    public async Task CreateTagesabschluss_ByRegisterAndDate_FindsExistingClosing()
    {
        var tenantId = Guid.NewGuid();
        var closing = SeedDailyClosing(tenantId, tseSignature: "local-jws");
        await using var db = CreateClosingDb(tenantId);
        db.DailyClosings.Add(closing);
        await db.SaveChangesAsync();

        var remoteId = Guid.NewGuid().ToString("D");
        var fiskaly = MockFiskalyReady();
        fiskaly
            .Setup(f => f.SignTransactionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<FiskalyTransactionData>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FiskalySignedReceipt(
                remoteId,
                closing.CashRegisterId.ToString("D"),
                "SIGNED",
                "R1-AT3_ta",
                "42",
                "TEST",
                Signed: true,
                ReceiptType: "NORMAL"));

        var sut = CreateSut(db: db, fiskalyTse: fiskaly.Object, fiskalyOptions: EnabledFiskalyOptions());
        var result = await sut.CreateTagesabschlussAsync(
            closing.CashRegisterId,
            closing.ClosingDate,
            "sa-1",
            true);

        Assert.True(result.Success);
        Assert.Equal(remoteId, result.Data?.ReceiptId);
        Assert.Equal("local-jws", result.Data?.Signature);
    }

    [Fact]
    public async Task CreateTagesabschluss_ByRegister_MissingClosing_Returns404()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateClosingDb(tenantId);
        var sut = CreateSut(db: db, fiskalyTse: MockFiskalyReady().Object, fiskalyOptions: EnabledFiskalyOptions());

        var result = await sut.CreateTagesabschlussAsync(Guid.NewGuid(), null, "sa-1", true);

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal(FiskalyReceiptErrorCodes.ClosingNotFound, result.Error?.Code);
    }

    [Fact]
    public async Task CreateTagesabschluss_MissingTseSignature_Returns400()
    {
        var tenantId = Guid.NewGuid();
        var closing = SeedDailyClosing(tenantId, tseSignature: "   ");
        await using var db = CreateClosingDb(tenantId);
        db.DailyClosings.Add(closing);
        await db.SaveChangesAsync();

        var sut = CreateSut(db: db, fiskalyTse: MockFiskalyReady().Object, fiskalyOptions: EnabledFiskalyOptions());
        var result = await sut.CreateTagesabschlussAsync(closing.Id, "sa-1", true);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(FiskalyReceiptErrorCodes.TseSignatureRequired, result.Error?.Code);
    }

    [Fact]
    public async Task CreateTagesabschluss_FutureDate_Returns400()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateClosingDb(tenantId);
        var sut = CreateSut(db: db, fiskalyTse: MockFiskalyReady().Object, fiskalyOptions: EnabledFiskalyOptions());

        var future = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified().AddDays(2);
        var result = await sut.CreateTagesabschlussAsync(Guid.NewGuid(), future, "sa-1", true);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(FiskalyReceiptErrorCodes.FutureClosingDate, result.Error?.Code);
    }

    [Fact]
    public void ErrorMapper_Unauthorized_IsAuthFailed()
    {
        var ex = new FiskalyApiException(
            "fiskaly auth failed (401): invalid key",
            System.Net.HttpStatusCode.Unauthorized,
            "req-1",
            "TEST");

        var mapped = FiskalyReceiptErrorMapper.FromException(ex);

        Assert.Equal(FiskalyReceiptErrorCodes.FiskalyAuthFailed, mapped.Code);
        Assert.Equal(ex.Message, mapped.Message);
        Assert.Contains("HTTP 401", mapped.Details);
        Assert.Contains("req-1", mapped.Details);
    }

    private static FiskalyReceiptService CreateSut(
        IFiskalySignTestService? signTest = null,
        IPaymentService? payments = null,
        IRksvSpecialReceiptService? special = null,
        AppDbContext? db = null,
        IFiskalyTseService? fiskalyTse = null,
        IOptionsMonitor<FiskalyOptions>? fiskalyOptions = null)
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

        return new FiskalyReceiptService(
            signTest ?? Mock.Of<IFiskalySignTestService>(),
            payments ?? Mock.Of<IPaymentService>(),
            special ?? Mock.Of<IRksvSpecialReceiptService>(),
            audit.Object,
            NullLogger<FiskalyReceiptService>.Instance,
            db: db,
            fiskalyTse: fiskalyTse,
            fiskalyOptions: fiskalyOptions);
    }

    private static AppDbContext CreateClosingDb(Guid tenantId) =>
        new(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"FiskalyTa_{Guid.NewGuid():N}")
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options,
            TenantTestDoubles.TenantAccessorReturning(tenantId));

    private static DailyClosing SeedDailyClosing(Guid tenantId, string tseSignature)
    {
        var registerId = Guid.NewGuid();
        return new DailyClosing
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CashRegisterId = registerId,
            UserId = "sa-1",
            ClosingDate = PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(
                PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified()),
            ClosingType = "Daily",
            DayKind = DailyClosingDayKinds.Normal,
            TotalAmount = 100m,
            TotalTaxAmount = 20m,
            TransactionCount = 3,
            TseSignature = tseSignature,
            Status = "Completed",
            CreatedAt = DateTime.UtcNow
        };
    }

    private static Mock<IFiskalyTseService> MockFiskalyReady()
    {
        var fiskaly = new Mock<IFiskalyTseService>();
        fiskaly
            .Setup(f => f.IsReadyToSignAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return fiskaly;
    }

    private static IOptionsMonitor<FiskalyOptions> EnabledFiskalyOptions()
    {
        var monitor = new Mock<IOptionsMonitor<FiskalyOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(new FiskalyOptions
        {
            Enabled = true,
            ApiKey = "key",
            ApiSecret = "secret"
        });
        return monitor.Object;
    }
}
