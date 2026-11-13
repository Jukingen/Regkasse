using System.Text.Json;
using System.Text.Json.Serialization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Limits;
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
        if (IsVoucherPaymentForOfflineQueue(request))
            throw new InvalidOperationException("Voucher payments cannot be processed offline.");

        if (!LicenseEnforcementPolicy.ShouldDisableEnforcement(
                _hostEnvironment,
                _tseOptions,
                _developmentModeService,
                _licenseOptions)
            && _tenantLimitGuard != null)
        {
            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync().ConfigureAwait(false);
            if (tenantId != Guid.Empty)
            {
                try
                {
                    await _tenantLimitGuard
                        .EnsureCanQueueOfflineTransactionAsync(tenantId)
                        .ConfigureAwait(false);
                }
                catch (LimitExceededException ex)
                {
                    _logger.LogWarning(
                        "Non-fiscal offline queue tenant limit TenantId={TenantId} LimitKey={LimitKey} Count={Current} Max={Max}",
                        tenantId,
                        ex.LimitKey,
                        ex.CurrentValue,
                        ex.Limit);
                    return new PaymentResult
                    {
                        Success = false,
                        Message = ex.Message,
                        Errors = { ex.Message },
                        DiagnosticCode = LimitExceededException.ErrorCodeValue,
                        LimitError = ex.ToErrorDto(),
                        IsDeterministicFailure = true
                    };
                }
            }
        }

        var payloadRaw = JsonSerializer.Serialize(request, OfflineIntentJsonOptions);
        var protector = OfflineVoucherPayloadProtector.CreateProtector(_dataProtectionProvider);
        var aesKey = OfflineVoucherEncryptionOptions.TryResolveKeyBytes(_offlineVoucherEncryption.Value);
        var prepared = OfflineVoucherPayloadProtector.PrepareForPersistence(payloadRaw, protector, aesKey);

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

    /// <summary>Non-fiscal offline intents must never carry Gutschein plaintext for replay (RKSV / GDPR).</summary>
    private static bool IsVoucherPaymentForOfflineQueue(CreatePaymentRequest request)
    {
        var method = request.Payment.Method?.Trim();
        var voucherMethod = string.Equals(method, "voucher", StringComparison.OrdinalIgnoreCase);
        var hasCode = !string.IsNullOrWhiteSpace(request.Payment.VoucherCode);
        var hasRedemptions = request.Payment.VoucherRedemptions is { Count: > 0 };
        return voucherMethod || hasCode || hasRedemptions;
    }
}
