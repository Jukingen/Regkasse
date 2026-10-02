using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using System.Security.Claims;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Countries.EInvoicing;
using KasseAPI_Final.Services.FeatureFlags;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PeppolParticipantSchemaTests
{
    [Fact]
    public void PeppolParticipants_HasNoCredentialsColumn()
    {
        using var db = CreateDb(Guid.NewGuid());
        var entity = db.Model.FindEntityType(typeof(PeppolParticipant));
        Assert.NotNull(entity);
        var names = entity!.GetProperties()
            .Select(property =>
                property.PropertyInfo?.GetCustomAttribute<ColumnAttribute>()?.Name
                ?? property.Name)
            .ToArray();

        Assert.DoesNotContain(names, name =>
            name.Contains("credential", StringComparison.OrdinalIgnoreCase)
            || name.Contains("api_key", StringComparison.OrdinalIgnoreCase)
            || name.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || name.Contains("password", StringComparison.OrdinalIgnoreCase)
            || name.Contains("certificate", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("participant_id", names);
        Assert.Contains("ap_environment", names);
        Assert.Contains("legal_entity_id", names);
        Assert.Contains("eidentifier_scheme", names);
        Assert.Contains("eidentifier_value", names);
    }

    [Fact]
    public void EInvoicingPeppol_StaysReserved_AndOutOfAll()
    {
        Assert.Equal([FeatureFlagNames.EInvoicingPeppol], FeatureFlagNames.Reserved);
        Assert.DoesNotContain(FeatureFlagNames.EInvoicingPeppol, FeatureFlagNames.All);
        Assert.Equal(12, FeatureFlagNames.All.Count);
    }

    [Fact]
    public async Task GetParticipants_OtherTenant_Returns404()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await using var db = CreateDb(tenantA);
        db.Tenants.Add(Tenant(tenantA, "a"));
        db.Tenants.Add(Tenant(tenantB, "b"));
        db.PeppolParticipants.Add(new PeppolParticipant
        {
            Id = Guid.NewGuid(),
            TenantId = tenantB,
            ParticipantId = "iso6523-actorid-upis::9915:DE123",
            ApEnvironment = PeppolApEnvironments.Test,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var controller = Controller(db, tenantA);
        var result = await controller.List(tenantB, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task SubmitAsync_WhenPeppolReserved_WritesQueuedRow_AndDoesNotCallHttp()
    {
        var tenantId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var now = DateTime.UtcNow;
        db.Tenants.Add(Tenant(tenantId, "eu"));
        db.CashRegisters.Add(new CashRegister
        {
            Id = registerId,
            TenantId = tenantId,
            RegisterNumber = "1",
            IsActive = true,
            Status = RegisterStatus.Open,
            CreatedAt = now,
        });
        db.Invoices.Add(new Invoice
        {
            Id = invoiceId,
            TenantId = tenantId,
            InvoiceNumber = "EU-2026-83",
            InvoiceDate = now,
            DueDate = now.AddDays(14),
            Status = InvoiceStatus.Paid,
            Subtotal = 100m,
            TaxAmount = 20m,
            TotalAmount = 120m,
            PaidAmount = 120m,
            RemainingAmount = 0,
            CompanyName = "Seller GmbH",
            CompanyTaxNumber = "ATU12345678",
            CompanyAddress = "Wien",
            TseSignature = "sig",
            KassenId = "1",
            TseTimestamp = now,
            CashRegisterId = registerId,
            CreatedAt = now,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var handler = new CountingHandler();
        var flags = new Mock<IFeatureFlagService>();
        flags.Setup(flag => flag.IsEnabled(FeatureFlagNames.EInvoicingEn16931, It.IsAny<string?>())).Returns(true);
        var options = Options.Create(new KasseAPI_Final.Configuration.PeppolOptions
        {
            AccessPointMode = "hosted",
            Provider = "hosted",
            BaseUrl = "https://ap.example/v1",
        });
        var service = new PeppolSubmissionService(
            new En16931UblXmlBuilder(flags.Object),
            options,
            new MockPeppolAccessPointClient(),
            new HostedPeppolAccessPointClient(options, new HttpClient(handler)),
            new InMemoryPeppolSubmissionStore(),
            featureFlags: flags.Object,
            db: db);

        var row = await service.SubmitAsync(tenantId, Document(), invoiceId: invoiceId);

        Assert.Equal(PeppolSubmissionStatus.Queued, row.Status);
        Assert.Equal(PeppolSubmissionService.ReservedFailureReason, row.Detail);
        Assert.Equal(0, handler.Calls);
        var stored = await db.EinvoiceSubmissions.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(EinvoiceSubmissionStatuses.Queued, stored.Status);
        Assert.Equal(PeppolSubmissionService.ReservedFailureReason, stored.FailureReason);
        Assert.Equal(tenantId, stored.TenantId);
        Assert.Equal(invoiceId, stored.InvoiceId);
        Assert.NotEqual(Guid.Empty, stored.CorrelationId);
        Assert.Null(stored.AttemptedAtUtc);
    }

    private static AdminPeppolParticipantsController Controller(AppDbContext db, Guid ambient)
    {
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(), It.IsAny<string?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());
        var controller = new AdminPeppolParticipantsController(
            db,
            TenantTestDoubles.TenantAccessorReturning(ambient),
            audit.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "admin"),
                ],
                "test")),
            },
        };
        return controller;
    }

    private static AppDbContext CreateDb(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"Peppol_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
    }

    private static Tenant Tenant(Guid id, string slug) => new()
    {
        Id = id,
        Name = slug,
        Slug = slug,
        IsActive = true,
    };

    private static InvoiceDocumentDto Document() => new()
    {
        CountryCode = "EU_DEFAULT",
        InvoiceNumber = "EU-2026-83",
        InvoiceDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
        Currency = "EUR",
        SellerName = "Seller GmbH",
        SellerVatId = "ATU12345678",
        SellerCountry = "AT",
        BuyerName = "Buyer BV",
        BuyerVatId = "DE123456789",
        BuyerCountry = "DE",
        NetAmount = 100m,
        TaxAmount = 20m,
        GrossAmount = 120m,
        VatCategory = "S",
        VatPercent = 20m,
    };

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            throw new InvalidOperationException("Outbound HTTP is not allowed.");
        }
    }
}
