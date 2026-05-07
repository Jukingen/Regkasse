using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public partial class PaymentService
{
    private static readonly IDataProtectionProvider FallbackOfflinePayloadProtection =
        DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(Path.GetTempPath(), "regkasse-offline-intent-dp")));

    private static readonly JsonSerializerOptions OfflineIntentJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private async Task<PaymentResult> TryQueueServerNonFiscalOfflineAsync(
        CreatePaymentRequest request,
        string userId,
        Guid resolvedCashRegisterId)
    {
        var max = Math.Clamp(_tseOptions.MaxOfflineTransactionsPerCashRegister, 1, 10000);
        var pendingCount = await _context.OfflineTransactions.CountAsync(x =>
                x.CashRegisterId == resolvedCashRegisterId &&
                x.Status == OfflineTransactionStatus.NonFiscalPending)
            .ConfigureAwait(false);

        if (pendingCount >= max)
        {
            _logger.LogWarning(
                "Non-fiscal offline queue full for RegisterId={RegisterId} Count={Count} Max={Max}",
                resolvedCashRegisterId,
                pendingCount,
                max);
            return new PaymentResult
            {
                Success = false,
                Message = "Offline queue limit reached for this cash register.",
                Errors = { "Too many pending non-fiscal transactions; wait for replay or contact support." },
                DiagnosticCode = "OFFLINE_QUEUE_FULL",
                IsDeterministicFailure = false
            };
        }

        var payloadRaw = JsonSerializer.Serialize(request, OfflineIntentJsonOptions);
        var protector = OfflineVoucherPayloadProtector.CreateProtector(_dataProtectionProvider);
        var prepared = OfflineVoucherPayloadProtector.PrepareForPersistence(payloadRaw, protector);

        var row = new OfflineTransaction
        {
            CashRegisterId = resolvedCashRegisterId,
            PayloadJson = prepared.StoredPayloadJson,
            PayloadSecretsProtected = prepared.ProtectedBase64,
            PayloadHash = prepared.PayloadHashHex,
            ServerReceivedAtUtc = DateTime.UtcNow,
            OfflineCreatedAtUtc = DateTime.UtcNow,
            Status = OfflineTransactionStatus.NonFiscalPending,
            CreatedBy = userId,
            RetryCount = 0
        };

        _context.OfflineTransactions.Add(row);
        await _context.SaveChangesAsync().ConfigureAwait(false);

        _logger.LogInformation(
            "Accepted NonFiscalPending offline intent OfflineTransactionId={OfflineId} RegisterId={RegisterId} UserId={UserId}",
            row.Id,
            resolvedCashRegisterId,
            userId);

        return new PaymentResult
        {
            Success = true,
            Message = "Payment queued for fiscal signing when TSE is available.",
            NonFiscalOfflineQueued = true,
            OfflineTransactionId = row.Id,
            InvoicePersisted = false,
            DiagnosticCode = "NON_FISCAL_QUEUED"
        };
    }
}
