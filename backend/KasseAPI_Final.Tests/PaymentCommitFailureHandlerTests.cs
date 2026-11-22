using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Extensions;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PaymentCommitFailureHandlerTests
{
    [Fact]
    public async Task Map_ConcurrencyConflict_ReturnsDailyAllowanceWithoutSaving()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var sut = Create(ctx);

        var mapped = await sut.MapFiscalCommitFailureAsync(
            new DbUpdateConcurrencyException("allowance"), "idem-1", Guid.NewGuid());

        Assert.False(mapped.Rethrow);
        Assert.Equal("BENEFIT_DAILY_ALLOWANCE_CONFLICT", mapped.Result!.DiagnosticCode);
        Assert.False(mapped.Result.Success);
        await AssertNoWrite(ctx);
    }

    [Fact]
    public async Task Map_IdempotencyRace_ReturnsExistingPaymentAndInvoiceFlag()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var payment = new PaymentDetails
        {
            IdempotencyKey = "idem-race",
            TseSignature = "sig",
            ReceiptNumber = "R-1",
            TotalAmount = 12m,
            TimeSyncWarning = true,
            CashRegisterId = Guid.NewGuid()
        };
        ctx.PaymentDetails.Add(payment);
        ctx.Invoices.Add(new Invoice
        {
            TenantId = SystemTenantIds.Platform,
            SourcePaymentId = payment.Id,
            InvoiceNumber = "R-1"
        });
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();
        var qrCalls = 0;
        var sut = Create(ctx, (existing, tseRequired) =>
        {
            qrCalls++;
            Assert.Equal(payment.Id, existing.Id);
            Assert.True(tseRequired);
            return Task.FromResult(("qr-replay", false, "Device"));
        });

        var mapped = await sut.MapFiscalCommitFailureAsync(Unique("IX_payment_details_idempotency_key"), "idem-race", Guid.NewGuid());

        Assert.False(mapped.Rethrow);
        Assert.True(mapped.Result!.Success);
        Assert.True(mapped.Result.IdempotentReplay);
        Assert.True(mapped.Result.InvoicePersisted);
        Assert.Equal(payment.Id, mapped.Result.PaymentId);
        Assert.Equal("sig", mapped.Result.TseSignature);
        Assert.Equal("qr-replay", mapped.Result.QrPayload);
        Assert.Equal("Device", mapped.Result.TseProvider);
        Assert.True(mapped.Result.TimeSyncWarning);
        Assert.Equal(1, qrCalls);
        Assert.Equal(1, await ctx.PaymentDetails.AsNoTracking().CountAsync());
        Assert.Null(ctx.Database.CurrentTransaction);
        Assert.Empty(ctx.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Map_VoucherLedgerIdempotency_ReturnsConflictWhenPaymentIsMissing()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var sut = Create(ctx);

        var mapped = await sut.MapFiscalCommitFailureAsync(
            Unique("IX_voucher_ledger_entries_idempotency_key"), "missing-key", Guid.NewGuid());

        Assert.False(mapped.Rethrow);
        Assert.Equal("VOUCHER_LEDGER_IDEMPOTENCY_CONFLICT", mapped.Result!.DiagnosticCode);
        Assert.True(mapped.Result.IsDeterministicFailure);
        await AssertNoWrite(ctx);
    }

    [Fact]
    public async Task Map_DuplicateIdempotencyWithoutRow_ReturnsDuplicateKey()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var sut = Create(ctx);

        var mapped = await sut.MapFiscalCommitFailureAsync(
            Unique("IX_payment_details_idempotency_key"), "missing-key", Guid.NewGuid());

        Assert.False(mapped.Rethrow);
        Assert.Equal("DUPLICATE_IDEMPOTENCY_KEY", mapped.Result!.DiagnosticCode);
        await AssertNoWrite(ctx);
    }

    [Fact]
    public async Task Map_BenefitUniqueRace_ReturnsDailyAllowance()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var customerId = Guid.NewGuid();
        var sut = Create(ctx);

        var mapped = await sut.MapFiscalCommitFailureAsync(Unique("IX_benefit_daily_usage"), null, customerId);

        Assert.False(mapped.Rethrow);
        Assert.Equal("BENEFIT_DAILY_ALLOWANCE_CONFLICT", mapped.Result!.DiagnosticCode);
        await AssertNoWrite(ctx);
    }

    [Fact]
    public async Task Map_ForeignKeyAndReceiptConflicts_ReturnDeterministicCodes()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var sut = Create(ctx);

        var foreignKey = await sut.MapFiscalCommitFailureAsync(Unique("FK_payment_customer", "23503"), null, Guid.NewGuid());
        var receipt = await sut.MapFiscalCommitFailureAsync(Unique("IX_receipts_receipt_number"), null, Guid.NewGuid());

        Assert.Equal("FOREIGN_KEY_VIOLATION", foreignKey.Result!.DiagnosticCode);
        Assert.Equal("RECEIPT_NUMBER_CONFLICT", receipt.Result!.DiagnosticCode);
        Assert.False(foreignKey.Rethrow);
        Assert.False(receipt.Rethrow);
        await AssertNoWrite(ctx);
    }

    [Fact]
    public async Task Map_UnmappedDatabaseAndUnknownExceptions_Rethrow()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var sut = Create(ctx);

        var database = await sut.MapFiscalCommitFailureAsync(new DbUpdateException("save failed"), null, Guid.NewGuid());
        var unknown = await sut.MapFiscalCommitFailureAsync(new InvalidOperationException("boom"), null, Guid.NewGuid());

        Assert.True(database.Rethrow);
        Assert.Null(database.Result);
        Assert.True(unknown.Rethrow);
        Assert.Null(unknown.Result);
        await AssertNoWrite(ctx);
    }

    [Fact]
    public async Task Map_ExtensionRejection_ReturnsExtensionFailure()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var sut = Create(ctx);

        var mapped = await sut.MapFiscalCommitFailureAsync(
            new ExtensionFailedException("loyalty", "blocked"), null, Guid.NewGuid());

        Assert.False(mapped.Rethrow);
        Assert.Equal("EXTENSION_FAILED", mapped.Result!.DiagnosticCode);
        Assert.Equal("blocked", mapped.Result.Message);
        Assert.Contains("blocked", mapped.Result.Errors);
        await AssertNoWrite(ctx);
    }

    private static PaymentCommitFailureHandler Create(
        AppDbContext context,
        PaymentQrPayloadBuilder? buildQr = null) =>
        new(
            context,
            NullLogger<PaymentService>.Instance,
            buildQr ?? ((_, _) => Task.FromResult(("qr", false, "Device"))));

    private static DbUpdateException Unique(string constraint, string sqlState = "23505")
    {
        var postgres = new PostgresException(
            "duplicate",
            "ERROR",
            "ERROR",
            sqlState,
            constraintName: constraint);
        return new DbUpdateException("save failed", postgres);
    }

    private static async Task AssertNoWrite(AppDbContext context)
    {
        Assert.Null(context.Database.CurrentTransaction);
        Assert.Empty(context.ChangeTracker.Entries());
        Assert.Equal(0, await context.PaymentDetails.AsNoTracking().CountAsync());
    }
}
