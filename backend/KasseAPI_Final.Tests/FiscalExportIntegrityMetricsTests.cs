using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Export;
using KasseAPI_Final.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public class FiscalExportIntegrityMetricsTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"FiscalExportIntegrity_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task ExportIntegrity_Metrics_AreComputed()
    {
        await using var context = CreateContext();

        var cashRegisterId = Guid.NewGuid();
        context.CashRegisters.Add(new CashRegister
        {
            Id = cashRegisterId,
            RegisterNumber = "KASSE-01",
            Location = "T",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });

        var fromUtc = DateTime.UtcNow.AddHours(-1);
        var toUtc = DateTime.UtcNow.AddHours(1);

        // Offline intents (2 synced, 1 pending, 1 failed) inside export window.
        var offlineSynced1 = new OfflineTransaction
        {
            Id = Guid.NewGuid(),
            CashRegisterId = cashRegisterId,
            CreatedAt = DateTime.UtcNow.AddMinutes(-30),
            ServerReceivedAtUtc = DateTime.UtcNow.AddMinutes(-30),
            OfflineCreatedAtUtc = DateTime.UtcNow.AddMinutes(-30),
            PayloadJson = "{}",
            PayloadHash = "hash1",
            Status = OfflineTransactionStatus.Synced,
            SyncedPaymentId = null,
            FiscalizedAtUtc = DateTime.UtcNow.AddMinutes(-29),
            ClockDriftWarning = false,
            SequenceGapDetected = false,
            SequenceDuplicateDetected = false,
            IsActive = true
        };
        var offlineSynced2 = new OfflineTransaction
        {
            Id = Guid.NewGuid(),
            CashRegisterId = cashRegisterId,
            CreatedAt = DateTime.UtcNow.AddMinutes(-25),
            ServerReceivedAtUtc = DateTime.UtcNow.AddMinutes(-25),
            OfflineCreatedAtUtc = DateTime.UtcNow.AddMinutes(-25),
            PayloadJson = "{}",
            PayloadHash = "hash2",
            Status = OfflineTransactionStatus.Synced,
            SyncedPaymentId = null,
            FiscalizedAtUtc = DateTime.UtcNow.AddMinutes(-24),
            ClockDriftWarning = false,
            SequenceGapDetected = false,
            SequenceDuplicateDetected = false,
            IsActive = true
        };
        var offlinePending = new OfflineTransaction
        {
            Id = Guid.NewGuid(),
            CashRegisterId = cashRegisterId,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            ServerReceivedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            OfflineCreatedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            PayloadJson = "{}",
            PayloadHash = "hashPending",
            Status = OfflineTransactionStatus.Pending,
            SyncedPaymentId = null,
            FiscalizedAtUtc = null,
            ClockDriftWarning = true,
            SequenceGapDetected = true,
            SequenceDuplicateDetected = false,
            IsActive = true
        };
        var offlineFailed = new OfflineTransaction
        {
            Id = Guid.NewGuid(),
            CashRegisterId = cashRegisterId,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            ServerReceivedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            OfflineCreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            PayloadJson = "{}",
            PayloadHash = "hashFailed",
            Status = OfflineTransactionStatus.Failed,
            SyncedPaymentId = null,
            FiscalizedAtUtc = null,
            ClockDriftWarning = false,
            SequenceGapDetected = false,
            SequenceDuplicateDetected = false,
            LastErrorCode = "TEST_FAILED",
            LastErrorMessageSafe = "Test error",
            RetryCount = 1,
            LastReplayAttemptAt = DateTime.UtcNow.AddMinutes(-4),
            IsActive = true
        };

        context.OfflineTransactions.AddRange(offlineSynced1, offlineSynced2, offlinePending, offlineFailed);

        // Payment details + receipts for synced offline intents only.
        var payment1Id = Guid.NewGuid();
        var payment2Id = Guid.NewGuid();

        var payment1 = new PaymentDetails
        {
            Id = payment1Id,
            CustomerId = Guid.NewGuid(),
            CustomerName = "Cust1",
            TableNumber = 1,
            CashierId = "u1",
            TotalAmount = 1.23m,
            TaxAmount = 0.23m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            TseSignature = "tse1",
            TseTimestamp = DateTime.UtcNow,
            CashRegisterId = cashRegisterId,
            ReceiptNumber = "AT-KASSE-01-20260101-1",
            IsActive = true,
            OfflineTransactionId = offlineSynced1.Id,
            OfflineTransaction = offlineSynced1
        };
        var payment2 = new PaymentDetails
        {
            Id = payment2Id,
            CustomerId = Guid.NewGuid(),
            CustomerName = "Cust2",
            TableNumber = 1,
            CashierId = "u1",
            TotalAmount = 2.23m,
            TaxAmount = 0.23m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            TseSignature = "tse2",
            TseTimestamp = DateTime.UtcNow,
            CashRegisterId = cashRegisterId,
            ReceiptNumber = "AT-KASSE-01-20260101-2",
            IsActive = true,
            OfflineTransactionId = offlineSynced2.Id,
            OfflineTransaction = offlineSynced2
        };

        offlineSynced1.SyncedPaymentId = payment1Id;
        offlineSynced2.SyncedPaymentId = payment2Id;

        context.PaymentDetails.AddRange(payment1, payment2);

        var receipt1Id = Guid.NewGuid();
        var receipt2Id = Guid.NewGuid();

        // Two receipts with continuous SEQ and valid signature chain.
        context.Receipts.AddRange(
            new Receipt
            {
                ReceiptId = receipt1Id,
                PaymentId = payment1Id,
                CashRegisterId = cashRegisterId,
                ReceiptNumber = "AT-KASSE-01-20260318-1",
                IssuedAt = fromUtc.AddMinutes(10),
                SubTotal = 1m,
                TaxTotal = 0.2m,
                GrandTotal = 1.2m,
                SignatureValue = "sig1",
                PrevSignatureValue = null,
                CreatedAt = fromUtc.AddMinutes(10)
            },
            new Receipt
            {
                ReceiptId = receipt2Id,
                PaymentId = payment2Id,
                CashRegisterId = cashRegisterId,
                ReceiptNumber = "AT-KASSE-01-20260318-2",
                IssuedAt = fromUtc.AddMinutes(20),
                SubTotal = 2m,
                TaxTotal = 0.2m,
                GrandTotal = 2.2m,
                SignatureValue = "sig2",
                PrevSignatureValue = "sig1",
                CreatedAt = fromUtc.AddMinutes(20)
            });

        await context.SaveChangesAsync();

        var exportService = new FiscalExportService(context, new Mock<ILogger<FiscalExportService>>().Object);
        var export = await exportService.BuildExportAsync(cashRegisterId, fromUtc, toUtc, includeCsv: false);

        Assert.NotNull(export.Integrity);
        Assert.True(export.Integrity.SignatureChainValid);
        Assert.True(export.Integrity.ReceiptSignatureLinkageOkInExportOrder);
        Assert.True(export.Integrity.SequenceContinuous);
        Assert.True(export.Integrity.BelegSequenceContiguousInExportedOrderPerDay);
        Assert.NotEmpty(export.ExportScopeWarnings);
        Assert.False(export.ReceiptsTruncated);
        Assert.Equal(2, export.TotalReceiptsMatchingPeriod);
        Assert.Equal(4, export.Integrity.TotalOfflineTransactions);
        Assert.Equal(2, export.Integrity.SyncedOfflineTransactions);
        Assert.Equal(1, export.Integrity.FailedOfflineTransactions);
        Assert.Equal(1, export.Integrity.OfflineReplayGaps);
    }

    [Fact]
    public async Task ExportIntegrity_SignatureChainInvalid_FlagsFalse()
    {
        await using var context = CreateContext();

        var cashRegisterId = Guid.NewGuid();
        context.CashRegisters.Add(new CashRegister
        {
            Id = cashRegisterId,
            RegisterNumber = "KASSE-01",
            Location = "T",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });

        var fromUtc = DateTime.UtcNow.AddHours(-1);
        var toUtc = DateTime.UtcNow.AddHours(1);

        // Offline intents for synced receipts (required for Include(...) chain).
        var offline1 = new OfflineTransaction
        {
            Id = Guid.NewGuid(),
            CashRegisterId = cashRegisterId,
            CreatedAt = DateTime.UtcNow.AddMinutes(-30),
            ServerReceivedAtUtc = DateTime.UtcNow.AddMinutes(-30),
            OfflineCreatedAtUtc = DateTime.UtcNow.AddMinutes(-30),
            PayloadJson = "{}",
            PayloadHash = "h1",
            Status = OfflineTransactionStatus.Synced,
            FiscalizedAtUtc = DateTime.UtcNow.AddMinutes(-29),
            ClockDriftWarning = false,
            SequenceGapDetected = false,
            SequenceDuplicateDetected = false,
            IsActive = true
        };
        var offline2 = new OfflineTransaction
        {
            Id = Guid.NewGuid(),
            CashRegisterId = cashRegisterId,
            CreatedAt = DateTime.UtcNow.AddMinutes(-20),
            ServerReceivedAtUtc = DateTime.UtcNow.AddMinutes(-20),
            OfflineCreatedAtUtc = DateTime.UtcNow.AddMinutes(-20),
            PayloadJson = "{}",
            PayloadHash = "h2",
            Status = OfflineTransactionStatus.Synced,
            FiscalizedAtUtc = DateTime.UtcNow.AddMinutes(-19),
            ClockDriftWarning = false,
            SequenceGapDetected = false,
            SequenceDuplicateDetected = false,
            IsActive = true
        };

        context.OfflineTransactions.AddRange(offline1, offline2);

        var payment1Id = Guid.NewGuid();
        var payment2Id = Guid.NewGuid();
        var payment1 = new PaymentDetails
        {
            Id = payment1Id,
            CustomerId = Guid.NewGuid(),
            CustomerName = "Cust1",
            TableNumber = 1,
            CashierId = "u1",
            TotalAmount = 1.23m,
            TaxAmount = 0.23m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            TseSignature = "tse1",
            TseTimestamp = DateTime.UtcNow,
            CashRegisterId = cashRegisterId,
            ReceiptNumber = "AT-KASSE-01-20260101-1",
            IsActive = true,
            OfflineTransactionId = offline1.Id,
            OfflineTransaction = offline1
        };
        var payment2 = new PaymentDetails
        {
            Id = payment2Id,
            CustomerId = Guid.NewGuid(),
            CustomerName = "Cust2",
            TableNumber = 1,
            CashierId = "u1",
            TotalAmount = 2.23m,
            TaxAmount = 0.23m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            TseSignature = "tse2",
            TseTimestamp = DateTime.UtcNow,
            CashRegisterId = cashRegisterId,
            ReceiptNumber = "AT-KASSE-01-20260101-2",
            IsActive = true,
            OfflineTransactionId = offline2.Id,
            OfflineTransaction = offline2
        };
        offline1.SyncedPaymentId = payment1Id;
        offline2.SyncedPaymentId = payment2Id;
        context.PaymentDetails.AddRange(payment1, payment2);

        // Intentionally break signature chain: receipt2 prev != receipt1 signature.
        context.Receipts.AddRange(
            new Receipt
            {
                ReceiptId = Guid.NewGuid(),
                PaymentId = payment1Id,
                CashRegisterId = cashRegisterId,
                ReceiptNumber = "AT-KASSE-01-20260318-1",
                IssuedAt = fromUtc.AddMinutes(10),
                SubTotal = 1m,
                TaxTotal = 0.2m,
                GrandTotal = 1.2m,
                SignatureValue = "sig1",
                PrevSignatureValue = null,
                CreatedAt = fromUtc.AddMinutes(10)
            },
            new Receipt
            {
                ReceiptId = Guid.NewGuid(),
                PaymentId = payment2Id,
                CashRegisterId = cashRegisterId,
                ReceiptNumber = "AT-KASSE-01-20260318-2",
                IssuedAt = fromUtc.AddMinutes(20),
                SubTotal = 2m,
                TaxTotal = 0.2m,
                GrandTotal = 2.2m,
                SignatureValue = "sig2",
                PrevSignatureValue = "WRONG",
                CreatedAt = fromUtc.AddMinutes(20)
            });

        await context.SaveChangesAsync();

        var exportService = new FiscalExportService(context, new Mock<ILogger<FiscalExportService>>().Object);
        var export = await exportService.BuildExportAsync(cashRegisterId, fromUtc, toUtc, includeCsv: false);

        Assert.NotNull(export.Integrity);
        Assert.False(export.Integrity.SignatureChainValid);
        Assert.False(export.Integrity.ReceiptSignatureLinkageOkInExportOrder);
    }

    [Fact]
    public async Task ExportScope_WhenReceiptExistsBeforeWindow_IncludesBoundaryWarning()
    {
        await using var context = CreateContext();
        var cashRegisterId = Guid.NewGuid();
        context.CashRegisters.Add(new CashRegister
        {
            Id = cashRegisterId,
            RegisterNumber = "K-01",
            Location = "T",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });

        var fromUtc = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var toUtc = fromUtc.AddHours(2);

        var earlyPaymentId = Guid.NewGuid();
        context.PaymentDetails.Add(new PaymentDetails
        {
            Id = earlyPaymentId,
            CustomerId = Guid.NewGuid(),
            CustomerName = "C",
            TableNumber = 1,
            CashierId = "u1",
            TotalAmount = 1m,
            TaxAmount = 0.1m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            TseSignature = "t",
            TseTimestamp = DateTime.UtcNow,
            CashRegisterId = cashRegisterId,
            ReceiptNumber = "AT-K-01-20260501-1",
            IsActive = true
        });
        context.Receipts.Add(new Receipt
        {
            ReceiptId = Guid.NewGuid(),
            PaymentId = earlyPaymentId,
            CashRegisterId = cashRegisterId,
            ReceiptNumber = "AT-K-01-20260501-1",
            IssuedAt = fromUtc.AddHours(-1),
            SubTotal = 1m,
            TaxTotal = 0.1m,
            GrandTotal = 1.1m,
            SignatureValue = "s0",
            CreatedAt = fromUtc.AddHours(-1)
        });

        var p1 = Guid.NewGuid();
        context.PaymentDetails.Add(new PaymentDetails
        {
            Id = p1,
            CustomerId = Guid.NewGuid(),
            CustomerName = "C2",
            TableNumber = 1,
            CashierId = "u1",
            TotalAmount = 1m,
            TaxAmount = 0.1m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            TseSignature = "t",
            TseTimestamp = DateTime.UtcNow,
            CashRegisterId = cashRegisterId,
            ReceiptNumber = "AT-K-01-20260601-1",
            IsActive = true
        });
        context.Receipts.Add(new Receipt
        {
            ReceiptId = Guid.NewGuid(),
            PaymentId = p1,
            CashRegisterId = cashRegisterId,
            ReceiptNumber = "AT-K-01-20260601-1",
            IssuedAt = fromUtc.AddMinutes(30),
            SubTotal = 1m,
            TaxTotal = 0.1m,
            GrandTotal = 1.1m,
            SignatureValue = "s1",
            PrevSignatureValue = null,
            CreatedAt = fromUtc.AddMinutes(30)
        });

        await context.SaveChangesAsync();

        var export = await new FiscalExportService(context, new Mock<ILogger<FiscalExportService>>().Object)
            .BuildExportAsync(cashRegisterId, fromUtc, toUtc, false);

        Assert.Contains(export.ExportScopeWarnings, w => w.Contains("before the export start", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(export.Integrity.IntegrityDiagnosticNotes,
            n => n.Contains("before the export window", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Misuse guard: ExportScopeWarnings and NotLegalProofNotice must never be empty; payload must explicitly state NOT LEGAL PROOF.
    /// </summary>
    [Fact]
    public async Task Export_MisuseGuard_ExportScopeWarningsAndNotLegalProofNotice_NeverEmptyAndContainNotLegalProof()
    {
        await using var context = CreateContext();
        var cashRegisterId = Guid.NewGuid();
        context.CashRegisters.Add(new CashRegister
        {
            Id = cashRegisterId,
            RegisterNumber = "KASSE-01",
            Location = "T",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var fromUtc = DateTime.UtcNow.AddDays(-1);
        var toUtc = DateTime.UtcNow.AddDays(1);
        var exportService = new FiscalExportService(context, new Mock<ILogger<FiscalExportService>>().Object);
        var export = await exportService.BuildExportAsync(cashRegisterId, fromUtc, toUtc, includeCsv: false);

        Assert.NotNull(export.ExportScopeWarnings);
        Assert.NotEmpty(export.ExportScopeWarnings);
        Assert.True(
            export.ExportScopeWarnings.Any(w => w.Contains("NOT LEGAL PROOF", StringComparison.OrdinalIgnoreCase)),
            "ExportScopeWarnings must contain an entry with 'NOT LEGAL PROOF'.");

        Assert.False(string.IsNullOrEmpty(export.NotLegalProofNotice));
        Assert.Contains("NOT LEGAL PROOF", export.NotLegalProofNotice, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(FiscalExportService.NotLegalProofNoticeText, export.NotLegalProofNotice);
        Assert.Equal("operational_preview", export.ExportProfile);
        Assert.Contains("Operational preview profile", export.ExportProfileIntentNotice, StringComparison.OrdinalIgnoreCase);
    }
}

