using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AutoTagesabschlussServiceTests
{
    private static readonly DateTime AfterCutoffCest =
        new(2026, 9, 14, 1, 30, 0, DateTimeKind.Utc); // 03:30 Vienna, yesterday = 2026-09-13

    private static readonly DateTime BeforeCutoffCest =
        new(2026, 9, 14, 0, 50, 0, DateTimeKind.Utc); // 02:50 Vienna

    [Fact]
    public async Task RunFallbackAsync_WhenDisabled_ReturnsZero()
    {
        var harness = await CreateHarnessAsync(utcNow: AfterCutoffCest, optionsEnabled: false);
        await using var _ = harness.Context;

        var closed = await harness.Sut.RunFallbackAsync(AfterCutoffCest);

        Assert.Equal(0, closed);
        harness.Tagesabschluss.Verify(
            t => t.PerformDailyClosingAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<DailyClosingPerformOptions>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task RunFallbackAsync_BeforeCutoff_DoesNotClose()
    {
        var harness = await CreateHarnessAsync(utcNow: BeforeCutoffCest, seedInvoice: true);
        await using var _ = harness.Context;

        var closed = await harness.Sut.RunFallbackAsync(BeforeCutoffCest);

        Assert.Equal(0, closed);
        harness.Tagesabschluss.Verify(
            t => t.PerformDailyClosingAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<DailyClosingPerformOptions>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task RunFallbackAsync_WithoutInvoices_Skips()
    {
        var harness = await CreateHarnessAsync(utcNow: AfterCutoffCest, seedInvoice: false);
        await using var _ = harness.Context;

        var closed = await harness.Sut.RunFallbackAsync(AfterCutoffCest);

        Assert.Equal(0, closed);
        harness.Tagesabschluss.Verify(
            t => t.PerformDailyClosingAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<DailyClosingPerformOptions>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task RunFallbackAsync_WhenAlreadyClosed_Skips()
    {
        var harness = await CreateHarnessAsync(utcNow: AfterCutoffCest, seedInvoice: true, alreadyClosed: true);
        await using var _ = harness.Context;

        var closed = await harness.Sut.RunFallbackAsync(AfterCutoffCest);

        Assert.Equal(0, closed);
        harness.Tagesabschluss.Verify(
            t => t.PerformDailyClosingAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<DailyClosingPerformOptions>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task RunFallbackAsync_EligibleDay_CallsPerformWithAutomaticYesterday()
    {
        DailyClosingPerformOptions? captured = null;
        DateTime? capturedDay = null;
        string? capturedReason = null;
        string? capturedUser = null;

        var harness = await CreateHarnessAsync(
            utcNow: AfterCutoffCest,
            seedInvoice: true,
            onPerform: (userId, _, options, closingDate, reason) =>
            {
                capturedUser = userId;
                captured = options;
                capturedDay = closingDate;
                capturedReason = reason;
            });
        await using var _ = harness.Context;

        var closed = await harness.Sut.RunFallbackAsync(AfterCutoffCest);

        Assert.Equal(1, closed);
        Assert.Equal("cashier-1", capturedUser);
        Assert.NotNull(captured);
        Assert.Equal(DailyClosingTriggers.Automatic, captured!.Trigger);
        Assert.Equal(AutoTagesabschlussSettings.NoCashCountNote, captured.CashCountNote);
        Assert.False(captured.OpenOrdersForced);
        Assert.Equal(0, captured.OpenOrdersCount);
        var expectedDay = AutoTagesabschlussCutoff.GetYesterdayBusinessDay(AfterCutoffCest);
        Assert.Equal(expectedDay, capturedDay);
        Assert.Equal(AutoTagesabschlussSettings.AutomaticLateReason, capturedReason);
    }

    [Fact]
    public async Task RunFallbackAsync_OpenOrdersBlock_SkipsAndPublishesWarning()
    {
        var harness = await CreateHarnessAsync(
            utcNow: AfterCutoffCest,
            seedInvoice: true,
            openOrdersPolicy: AutoTagesabschlussOpenOrdersPolicies.Block,
            seedOpenTable: true);
        await using var _ = harness.Context;

        var closed = await harness.Sut.RunFallbackAsync(AfterCutoffCest);

        Assert.Equal(0, closed);
        harness.Tagesabschluss.Verify(
            t => t.PerformDailyClosingAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<DailyClosingPerformOptions>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>()),
            Times.Never);
        harness.Activity.Verify(
            a => a.PublishAsync(
                It.Is<ActivityEventPublishRequest>(r =>
                    r.Type == ActivityEventType.DailyClosingOpenOrdersWarning
                    && r.ActorUserId == AutoTagesabschlussService.SystemActorUserId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunFallbackAsync_OpenOrdersForce_StillClosesWithForcedFlag()
    {
        DailyClosingPerformOptions? captured = null;
        var harness = await CreateHarnessAsync(
            utcNow: AfterCutoffCest,
            seedInvoice: true,
            openOrdersPolicy: AutoTagesabschlussOpenOrdersPolicies.ForceWithWarning,
            seedOpenTable: true,
            onPerform: (_, _, options, _, _) => captured = options);
        await using var _ = harness.Context;

        var closed = await harness.Sut.RunFallbackAsync(AfterCutoffCest);

        Assert.Equal(1, closed);
        Assert.NotNull(captured);
        Assert.True(captured!.OpenOrdersForced);
        Assert.True(captured.OpenOrdersCount > 0);
        harness.Activity.Verify(
            a => a.PublishAsync(
                It.Is<ActivityEventPublishRequest>(r =>
                    r.Type == ActivityEventType.DailyClosingOpenOrdersWarning),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunFallbackAsync_LastDayOfMonth_CreatesMonatsbeleg()
    {
        // 2026-10-01 01:30 UTC = 03:30 CEST → yesterday 2026-09-30
        var utcNow = new DateTime(2026, 10, 1, 1, 30, 0, DateTimeKind.Utc);
        var harness = await CreateHarnessAsync(utcNow: utcNow, seedInvoice: true);
        await using var _ = harness.Context;

        var closed = await harness.Sut.RunFallbackAsync(utcNow);

        Assert.Equal(1, closed);
        harness.Monatsbeleg.Verify(
            m => m.CreateMonatsbelegClosingAsync(
                It.IsAny<string>(),
                It.Is<CreateMonatsbelegClosingRequest>(r => r.Year == 2026 && r.Month == 9),
                It.IsAny<CancellationToken>()),
            Times.Once);
        harness.Jahresbeleg.Verify(
            j => j.CreateJahresbelegClosingAsync(
                It.IsAny<string>(),
                It.IsAny<CreateJahresbelegClosingRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private sealed record Harness(
        AppDbContext Context,
        AutoTagesabschlussService Sut,
        Mock<ITagesabschlussService> Tagesabschluss,
        Mock<IActivityEventService> Activity,
        Mock<IMonatsbelegClosingService> Monatsbeleg,
        Mock<IJahresbelegClosingService> Jahresbeleg);

    private static async Task<Harness> CreateHarnessAsync(
        DateTime utcNow,
        bool optionsEnabled = true,
        bool seedInvoice = false,
        bool alreadyClosed = false,
        bool seedOpenTable = false,
        string? openOrdersPolicy = null,
        Action<string, Guid, DailyClosingPerformOptions, DateTime?, string?>? onPerform = null)
    {
        var tenantId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        var accessor = TenantTestDoubles.TenantAccessorReturning(tenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AutoTa_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var ctx = new AppDbContext(options, accessor);

        ctx.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Cafe Auto",
            Slug = $"auto-{tenantId:N}"[..12],
            Status = TenantStatuses.Active,
            IsActive = true,
        });
        ctx.CashRegisters.Add(new CashRegister
        {
            Id = registerId,
            TenantId = tenantId,
            RegisterNumber = "K1",
            Location = "Haupt",
            StartingBalance = 0,
            CurrentBalance = 50,
            LastBalanceUpdate = utcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = "cashier-1",
            CreatedAt = utcNow,
            IsActive = true,
        });

        if (openOrdersPolicy != null)
        {
            ctx.CompanySettings.Add(new CompanySettings
            {
                TenantId = tenantId,
                CompanyName = "Cafe Auto",
                CompanyAddress = "Wien",
                CompanyTaxNumber = "ATU12345678",
                AutoTagesabschluss = new AutoTagesabschlussSettings
                {
                    Enabled = true,
                    HourVienna = 3,
                    MinuteVienna = 0,
                    OpenOrdersPolicy = openOrdersPolicy,
                },
            });
        }

        var businessDay = AutoTagesabschlussCutoff.GetYesterdayBusinessDay(utcNow);
        var (dayStartUtc, _) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(businessDay);
        var invoiceAt = dayStartUtc.AddHours(12);

        if (seedInvoice)
        {
            ctx.Invoices.Add(new Invoice
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CashRegisterId = registerId,
                InvoiceNumber = "INV-AUTO",
                InvoiceDate = invoiceAt,
                DueDate = invoiceAt,
                Subtotal = 10m,
                TaxAmount = 2m,
                TotalAmount = 12m,
                PaidAmount = 12m,
                RemainingAmount = 0m,
                CompanyName = "Cafe Auto",
                CompanyTaxNumber = "ATU12345678",
                CompanyAddress = "Wien",
                TseSignature = "sig",
                KassenId = "K1",
                TseTimestamp = invoiceAt,
                TaxDetails = JsonDocument.Parse("{}"),
                InvoiceItems = JsonDocument.Parse("[]"),
                Status = InvoiceStatus.Paid,
                CreatedAt = invoiceAt,
                IsActive = true,
            });
        }

        if (alreadyClosed)
        {
            ctx.DailyClosings.Add(new DailyClosing
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CashRegisterId = registerId,
                UserId = "cashier-1",
                ClosingDate = PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(businessDay),
                ClosingType = "Daily",
                TotalAmount = 12m,
                TotalTaxAmount = 2m,
                TransactionCount = 1,
                TseSignature = "sig",
                Status = "Completed",
                CreatedAt = utcNow,
            });
        }

        if (seedOpenTable)
        {
            ctx.UserTenantMemberships.Add(new UserTenantMembership
            {
                TenantId = tenantId,
                UserId = "cashier-1",
                IsActive = true,
            });
            ctx.TableOrders.Add(new TableOrder
            {
                TableOrderId = "TO-OPEN",
                TableNumber = 4,
                UserId = "cashier-1",
                Status = TableOrderStatus.Active,
                Subtotal = 8m,
                TaxAmount = 2m,
                TotalAmount = 10m,
                OrderStartTime = invoiceAt,
                CreatedAt = invoiceAt,
                IsActive = true,
            });
        }

        await ctx.SaveChangesAsync();

        var tagesabschluss = new Mock<ITagesabschlussService>();
        tagesabschluss
            .Setup(t => t.PerformDailyClosingAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<DailyClosingPerformOptions>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>()))
            .Callback<string, Guid, DailyClosingPerformOptions, DateTime?, string?>(
                (userId, cashRegisterId, options, closingDate, reason) =>
                    onPerform?.Invoke(userId, cashRegisterId, options, closingDate, reason))
            .ReturnsAsync(new TagesabschlussResult
            {
                Success = true,
                ClosingId = Guid.NewGuid(),
            });

        var shift = new Mock<ICashRegisterShiftService>();
        shift.Setup(s => s.TryForceCloseCashRegisterAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CashRegisterCloseResult.Success());

        var monatsbeleg = new Mock<IMonatsbelegClosingService>();
        monatsbeleg
            .Setup(m => m.CreateMonatsbelegClosingAsync(
                It.IsAny<string>(),
                It.IsAny<CreateMonatsbelegClosingRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MonatsbelegClosingResult { Success = true });

        var jahresbeleg = new Mock<IJahresbelegClosingService>();
        jahresbeleg
            .Setup(j => j.CreateJahresbelegClosingAsync(
                It.IsAny<string>(),
                It.IsAny<CreateJahresbelegClosingRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JahresbelegClosingResult { Success = true });

        var activity = new Mock<IActivityEventService>();
        activity
            .Setup(a => a.PublishAsync(It.IsAny<ActivityEventPublishRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ActivityEventPublishRequest req, CancellationToken _) => new ActivityEvent
            {
                Id = Guid.NewGuid(),
                TenantId = req.TenantId,
                Type = req.Type,
                Title = req.Title,
                Severity = "Warning",
            });

        var sut = new AutoTagesabschlussService(
            ctx,
            tagesabschluss.Object,
            shift.Object,
            monatsbeleg.Object,
            jahresbeleg.Object,
            accessor,
            activity.Object,
            Options.Create(new AutoTagesabschlussOptions
            {
                Enabled = optionsEnabled,
                DefaultHourVienna = 3,
                DefaultMinuteVienna = 0,
            }),
            NullLogger<AutoTagesabschlussService>.Instance);

        return new Harness(ctx, sut, tagesabschluss, activity, monatsbeleg, jahresbeleg);
    }
}
