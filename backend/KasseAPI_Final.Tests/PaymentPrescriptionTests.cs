using System.Text.Json;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.VerticalProfiles;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PaymentPrescriptionTests
{
    [Fact]
    public async Task CreatePayment_PersistsPrescription_WhenPatientRecordEnabled()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) =
            await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);

        var audit = CreateAuditMock();
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options
            {
                Audit = audit,
                VerticalProfiles = Profile(patientRecord: true),
            });

        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId);
        request.PrescriptionReference = "  RX-4411  ";

        var result = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
        var payment = await ctx.PaymentDetails.AsNoTracking().SingleAsync();
        Assert.Equal("RX-4411", payment.PrescriptionReference);
        Assert.Equal(123, (int)AuditEventType.PaymentWithPrescription);
        audit.Verify(
            logger => logger.LogSystemOperationAsync(
                "PAYMENT_WITH_PRESCRIPTION",
                "Payment",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                AuditLogStatus.Success,
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                AuditEventType.PaymentWithPrescription,
                payment.Id,
                It.IsAny<Guid?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task CreatePayment_RejectsPrescription_WhenPatientRecordDisabled()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) =
            await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);

        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options
            {
                VerticalProfiles = Profile(patientRecord: false),
            });

        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId);
        request.PrescriptionReference = "RX-NOPE";

        var result = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal("PROFILE_FEATURE_DISABLED", result.DiagnosticCode);
        Assert.Equal("patientRecord", result.ProfileFeature);
        Assert.True(result.IsDeterministicFailure);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
    }

    private static IVerticalProfileService Profile(bool patientRecord)
    {
        var mock = new Mock<IVerticalProfileService>();
        mock.Setup(service => service.GetForCurrentTenantAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EffectiveVerticalProfileDto(
                patientRecord ? VerticalProfileIds.Vet : VerticalProfileIds.Gastronomy,
                "test",
                JsonSerializer.SerializeToElement(new Dictionary<string, bool>
                {
                    ["patientRecord"] = patientRecord,
                }),
                JsonDocument.Parse("{}").RootElement.Clone(),
                JsonDocument.Parse("{}").RootElement.Clone(),
                "standard",
                JsonDocument.Parse("{}").RootElement.Clone()));
        return mock.Object;
    }

    private static Mock<IAuditLogService> CreateAuditMock()
    {
        var audit = new Mock<IAuditLogService>();
        audit.Setup(logger => logger.LogPaymentOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<decimal?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<object?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(), It.IsAny<double?>()))
            .ReturnsAsync(new AuditLog());
        audit.Setup(logger => logger.LogSystemOperationAsync(
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
        return audit;
    }
}
