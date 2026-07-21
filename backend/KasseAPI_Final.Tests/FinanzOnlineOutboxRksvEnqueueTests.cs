using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FinanzOnlineOutboxRksvEnqueueTests
{
    [Fact]
    public async Task EnqueueSubmissionAsync_SameIdempotencySecondCall_ReturnsSameRow_WithoutDuplicatePending()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"FonOutboxIdem_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        await using var ctx = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
        var svc = new FinanzOnlineOutboxService(ctx, new Mock<ILogger<FinanzOnlineOutboxService>>().Object);
        var receiptId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var inner = new RksvSpecialReceiptFinanzOnlineOutboxPayloadBody
        {
            Kind = "Startbeleg",
            PaymentId = paymentId,
            ReceiptId = receiptId,
            CashRegisterId = Guid.NewGuid(),
            ReceiptNumber = "R1",
            QrPayload = "qr",
        };
        var innerJson = System.Text.Json.JsonSerializer.Serialize(inner, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        });
        var innerHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(innerJson))).ToLowerInvariant();
        var businessKey = $"rksv|{receiptId:N}|Startbeleg";
        var payload = new FinanzOnlineOutboxPayload
        {
            Mode = FinanzOnlineIntegrationMode.TEST,
            Scope = new FinanzOnlineScope { TenantId = "t1", RegisterId = "REG" },
            Correlation = new FinanzOnlineCorrelationContext
            {
                BusinessKey = businessKey,
                PayloadHash = innerHash,
                CorrelationId = paymentId.ToString("N"),
            },
            SubmissionKind = FinanzOnlineSubmissionKind.Register,
            PayloadJson = innerJson,
        };

        var first = await svc.EnqueueSubmissionAsync(
            "RksvSpecialReceipt",
            receiptId,
            FinanzOnlineRksvSpecialReceiptOutboxMessageTypes.RksvStartbelegSubmission,
            businessKey,
            payload,
            persistImmediately: false);
        var second = await svc.EnqueueSubmissionAsync(
            "RksvSpecialReceipt",
            receiptId,
            FinanzOnlineRksvSpecialReceiptOutboxMessageTypes.RksvStartbelegSubmission,
            businessKey,
            payload,
            persistImmediately: false);

        Assert.Same(first, second);
        await ctx.SaveChangesAsync();
        Assert.Equal(1, await ctx.FinanzOnlineOutboxMessages.CountAsync());
        var row = await ctx.FinanzOnlineOutboxMessages.AsNoTracking().SingleAsync();
        Assert.Equal(FinanzOnlineOutboxStatuses.Pending, row.Status);
    }
}
