using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.FeatureFlags;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AdminKassenSicherheitControllerTests
{
    [Fact]
    public void Enums_Audit100Through103_AndActivity252()
    {
        Assert.Equal(100, (int)AuditEventType.KsDeTssCreated);
        Assert.Equal(101, (int)AuditEventType.KsDeTxStarted);
        Assert.Equal(102, (int)AuditEventType.KsDeTxFinished);
        Assert.Equal(103, (int)AuditEventType.KsDeExportCreated);
        Assert.Equal(252, (int)ActivityEventType.KsDeTxFinished);
    }

    [Fact]
    public void Controller_RequiresSystemCritical_ManagerDoesNotHaveIt()
    {
        var permission = Assert.Single(
            typeof(AdminKassenSicherheitController).GetCustomAttributes(typeof(HasPermissionAttribute), inherit: true)
                .Cast<HasPermissionAttribute>());
        Assert.Equal(AppPermissions.SystemCritical, permission.Permission);
        Assert.False(RolePermissionMatrix.RoleHasPermission(Roles.Manager, AppPermissions.SystemCritical));
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.SuperAdmin, AppPermissions.SystemCritical));
    }

    [Fact]
    public async Task Status_MissingTenant_Returns404()
    {
        var (controller, _) = Create(Guid.NewGuid());
        var missing = await controller.GetStatus(null, CancellationToken.None);
        Assert.IsType<NotFoundResult>(missing.Result);

        var unknown = await controller.GetStatus(Guid.NewGuid(), CancellationToken.None);
        Assert.IsType<NotFoundResult>(unknown.Result);
    }

    [Fact]
    public async Task Status_ExistingTenant_Returns200_ConfigDefaultOff()
    {
        var tenantId = Guid.NewGuid();
        var (controller, flags) = Create(tenantId, flagEnabled: false, source: FeatureFlagSources.Config, configDefault: false);
        var result = await controller.GetStatus(tenantId, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<KassenSicherheitStatusDto>(ok.Value);
        Assert.Equal(tenantId, dto.TenantId);
        Assert.False(dto.FlagEnabled);
        Assert.False(dto.HasTenantOverride);
        Assert.Equal("not-configured", dto.Provider);
        flags.Verify(f => f.SetEnabledAsync(
            It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Config_FirstFill_SavesIds_AndAudits100_SecondUpdateDoesNot()
    {
        var tenantId = Guid.NewGuid();
        var (controller, db, audits) = CreateWithDb(tenantId);
        var saved = await controller.PutConfig(
            new KassenSicherheitConfigRequest(tenantId, "  tss-1  ", "client-1"),
            CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(saved.Result);
        var dto = Assert.IsType<KassenSicherheitStatusDto>(ok.Value);
        Assert.Equal("tss-1", dto.DeTssId);
        Assert.Equal("client-1", dto.DeClientId);
        Assert.Contains(AuditEventType.KsDeTssCreated, audits);

        audits.Clear();
        await controller.PutConfig(
            new KassenSicherheitConfigRequest(tenantId, "tss-2", "client-1"),
            CancellationToken.None);
        Assert.DoesNotContain(AuditEventType.KsDeTssCreated, audits);

        var row = await db.CompanySettings.IgnoreQueryFilters().SingleAsync(s => s.TenantId == tenantId);
        Assert.Equal("tss-2", row.DeTssId);
    }

    [Fact]
    public async Task Recent_ReturnsOnlyDeRowsForThatTenant()
    {
        var tenantId = Guid.NewGuid();
        var other = Guid.NewGuid();
        var (controller, db, _) = CreateWithDb(tenantId, other);
        var ownRegister = Guid.NewGuid();
        var otherRegister = Guid.NewGuid();
        db.CashRegisters.Add(Register(ownRegister, tenantId, "1"));
        db.CashRegisters.Add(Register(otherRegister, other, "2"));
        db.PaymentDetails.Add(Payment(ownRegister, "DE", "DE-a-1-2", "tx-new", DateTime.UtcNow));
        db.PaymentDetails.Add(Payment(ownRegister, "DE", "DE-a-1-1", "tx-old", DateTime.UtcNow.AddMinutes(-5)));
        db.PaymentDetails.Add(Payment(ownRegister, "AT", "AT-1", "tx-at", DateTime.UtcNow));
        db.PaymentDetails.Add(Payment(otherRegister, "DE", "DE-b-1-1", "tx-other", DateTime.UtcNow));
        await db.SaveChangesAsync();

        var result = await controller.Recent(tenantId, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var rows = Assert.IsAssignableFrom<IReadOnlyList<KassenSicherheitRecentTransactionDto>>(ok.Value);
        Assert.Equal(2, rows.Count);
        Assert.Equal("tx-new", rows[0].TransactionId);
        Assert.Equal("DE-a-1-2", rows[0].ReceiptNumber);
        Assert.DoesNotContain(rows, r => r.TransactionId == "tx-at" || r.TransactionId == "tx-other");
    }

    [Fact]
    public async Task Export_ReturnsPending_AndAudits103_WithoutZip()
    {
        var tenantId = Guid.NewGuid();
        var (controller, _, audits) = CreateWithDb(tenantId);
        var result = await controller.Export(new KassenSicherheitExportRequest(tenantId), CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<KassenSicherheitExportResultDto>(ok.Value);
        Assert.Equal("PENDING", dto.Status);
        Assert.Contains(AuditEventType.KsDeExportCreated, audits);
        Assert.DoesNotContain(audits, a => a == AuditEventType.KsDeTxStarted || a == AuditEventType.KsDeTxFinished);
    }

    [Fact]
    public void ConfigDefault_KassenSicherheitDe_IsFalse()
    {
        Assert.False(new FeatureFlagsOptions().Fiscal.KassenSicherheitDe);
    }

    private static (AdminKassenSicherheitController Controller, Mock<IFeatureFlagService> Flags) Create(
        Guid tenantId,
        bool flagEnabled = false,
        string source = FeatureFlagSources.Config,
        bool configDefault = false)
    {
        var (controller, _, flags, _) = CreateCore(tenantId, flagEnabled: flagEnabled, source: source, configDefault: configDefault);
        return (controller, flags);
    }

    private static (AdminKassenSicherheitController Controller, AppDbContext Db, List<AuditEventType?> Audits) CreateWithDb(
        Guid tenantId,
        Guid? otherTenantId = null)
    {
        var (controller, db, _, audits) = CreateCore(tenantId, otherTenantId);
        return (controller, db, audits);
    }

    private static (AdminKassenSicherheitController Controller, AppDbContext Db, Mock<IFeatureFlagService> Flags, List<AuditEventType?> Audits) CreateCore(
        Guid tenantId,
        Guid? otherTenantId = null,
        bool flagEnabled = false,
        string source = FeatureFlagSources.Config,
        bool configDefault = false)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"KsCanary_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(null));
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Canary", Slug = "canary", IsActive = true });
        if (otherTenantId is Guid other)
            db.Tenants.Add(new Tenant { Id = other, Name = "Other", Slug = "other", IsActive = true });
        db.CompanySettings.Add(Settings(tenantId));
        db.SaveChanges();

        var flags = new Mock<IFeatureFlagService>();
        flags.Setup(f => f.GetStatusesAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FeatureFlagStatusDto>
            {
                new()
                {
                    Name = FeatureFlagNames.FiscalKassenSicherheitDe,
                    Enabled = flagEnabled,
                    ConfigDefault = configDefault,
                    Source = source,
                    TenantId = tenantId.ToString("D"),
                },
            });

        var audits = new List<AuditEventType?>();
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(), It.IsAny<string?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()))
            .Callback((
                string _,
                string _,
                string _,
                string _,
                string? _,
                string? _,
                AuditLogStatus _,
                string? _,
                object? _,
                object? _,
                string? _,
                ImpersonationAuditContext.Snapshot? _,
                AuditEventType? actionType,
                Guid? _,
                Guid? _,
                object? _,
                object? _,
                string? _) => audits.Add(actionType))
            .ReturnsAsync(new AuditLog());

        var controller = new AdminKassenSicherheitController(
            db,
            flags.Object,
            Options.Create(new KassenSicherheitOptions { Provider = "not-configured", Environment = "LIVE" }),
            audit.Object,
            NullLogger<AdminKassenSicherheitController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "super-admin")],
                    "test")),
            },
        };
        return (controller, db, flags, audits);
    }

    private static CompanySettings Settings(Guid tenantId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        CompanyName = "Canary GmbH",
        CompanyAddress = "Berlin",
        CompanyTaxNumber = "DE123456789",
        Country = "DE",
        Currency = "EUR",
        Language = "de",
        TimeZone = "Europe/Berlin",
        DateFormat = "dd.MM.yyyy",
        TimeFormat = "HH:mm",
        TaxCalculationMethod = "inclusive",
        InvoiceNumbering = "INV",
        ReceiptNumbering = "R",
        DefaultPaymentMethod = "Cash",
        BusinessHours = new Dictionary<string, string>(),
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
    };

    private static CashRegister Register(Guid id, Guid tenantId, string number) => new()
    {
        Id = id,
        TenantId = tenantId,
        RegisterNumber = number,
        Location = "Shop",
        Status = RegisterStatus.Open,
        LastBalanceUpdate = DateTime.UtcNow,
        IsActive = true,
    };

    private static PaymentDetails Payment(Guid registerId, string country, string beleg, string tx, DateTime created) => new()
    {
        Id = Guid.NewGuid(),
        CashRegisterId = registerId,
        CustomerId = Guid.NewGuid(),
        CustomerName = "Guest",
        CashierId = "cashier",
        Steuernummer = "ATU12345678",
        ReceiptNumber = beleg,
        TransactionId = tx,
        CountryCodeAtIssue = country,
        TseSignature = "sig",
        PaymentMethodRaw = "0",
        CreatedAt = created,
        IsActive = true,
    };
}
