using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs.Rksv;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Reports;
using KasseAPI_Final.Services.Rksv;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Tse;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RksvJahresbelegDomainServiceTests
{
    private static AppDbContext CreateContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"RksvJahresbelegSvc_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
    }

    private static JahresbelegService CreateService(AppDbContext ctx, ITseService? tse = null)
    {
        tse ??= CreateTseMock().Object;
        var configuration = new ConfigurationBuilder().Build();
        var hostEnvironment = TenantTestDoubles.ProductionHostEnvironment;

        return new JahresbelegService(
            ctx,
            tse,
            Mock.Of<ITseKeyProvider>(p => p.GetCurrentCertificateThumbprint() == "thumb-test"),
            new MonatsbelegService(
                ctx,
                DailyClosingTestDoubles.Create(ctx),
                tse,
                Mock.Of<ITseKeyProvider>(p => p.GetCurrentCertificateThumbprint() == "thumb-test"),
                new RksvEnvironmentService(configuration, hostEnvironment),
                new NullCurrentUserService(),
                Mock.Of<ILogger<MonatsbelegService>>()),
            new RksvEnvironmentService(configuration, hostEnvironment),
            new NullCurrentUserService(),
            Mock.Of<ILogger<JahresbelegService>>());
    }

    private static Mock<ITseService> CreateTseMock()
    {
        var mock = new Mock<ITseService>();
        mock.Setup(x => x.CreateYearlyClosingSignatureAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<decimal>(),
                It.IsAny<int>()))
            .ReturnsAsync("eyJhbGciOiJFUzI1NiJ9.eyJ.test.domain.jahr");
        return mock;
    }

    [Fact]
    public async Task CreateJahresbelegAsync_AggregatesMonatsbelegRows()
    {
        var tenantId = LegacyDefaultTenantIds.Primary;
        await using var ctx = CreateContext(tenantId);
        TenantTestDoubles.EnsureDefaultTenant(ctx);

        var regId = Guid.NewGuid();
        const int year = 2024;

        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = tenantId,
            Id = regId,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });

        for (var m = 1; m <= 3; m++)
        {
            ctx.Monatsbelege.Add(new Monatsbeleg
            {
                TenantId = tenantId,
                CashRegisterId = regId,
                Year = year,
                Month = m,
                TotalGross = 100m,
                TotalTax = 20m,
                TransactionCount = 2,
                Environment = "Demo",
                CreatedByUserId = "u1",
            });
        }

        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.CreateJahresbelegAsync(regId, year);

        Assert.Equal(300m, result.TotalGross);
        Assert.Equal(6, result.TransactionCount);
        Assert.False(result.IsDecemberMonatsbeleg);
        Assert.Equal(3, JahresbelegYearlyAggregator.DeserializeMonthlyReferences(result.MonthlyReferences).Count);
    }

    [Fact]
    public async Task CreateFromDecemberMonatsbelegAsync_ReusesDecemberSignature()
    {
        var tenantId = LegacyDefaultTenantIds.Primary;
        await using var ctx = CreateContext(tenantId);
        TenantTestDoubles.EnsureDefaultTenant(ctx);

        var regId = Guid.NewGuid();
        const int year = 2024;
        var decemberId = Guid.NewGuid();

        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = tenantId,
            Id = regId,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });

        ctx.Monatsbelege.Add(new Monatsbeleg
        {
            Id = decemberId,
            TenantId = tenantId,
            CashRegisterId = regId,
            Year = year,
            Month = 12,
            TotalGross = 500m,
            TotalTax = 100m,
            TransactionCount = 10,
            TseSignature = "december-sig",
            TseSignatureTimestamp = "2024-12-31T00:00:00Z",
            TseCertificateThumbprint = "cert-dec",
            PreviousSignature = "prev-dec",
            SignatureChainLength = 5,
            Environment = "Demo",
            CreatedByUserId = "u1",
        });

        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.CreateFromDecemberMonatsbelegAsync(regId, year);

        Assert.True(result.IsDecemberMonatsbeleg);
        Assert.Equal("december-sig", result.TseSignature);
        Assert.Equal(500m, result.TotalGross);
        Assert.Equal(5, result.SignatureChainLength);
    }

    [Fact]
    public async Task JahresbelegExistsAsync_ReturnsTrueWhenPresent()
    {
        var tenantId = LegacyDefaultTenantIds.Primary;
        await using var ctx = CreateContext(tenantId);
        TenantTestDoubles.EnsureDefaultTenant(ctx);

        var regId = Guid.NewGuid();
        ctx.Jahresbelege.Add(new Jahresbeleg
        {
            TenantId = tenantId,
            CashRegisterId = regId,
            Year = 2023,
            TotalGross = 1m,
            Environment = "Demo",
            CreatedByUserId = "u1",
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        Assert.True(await svc.JahresbelegExistsAsync(regId, 2023));
        Assert.False(await svc.JahresbelegExistsAsync(regId, 2024));
    }
}
