using System.Text.RegularExpressions;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Tse.Fiskaly;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Tenant/cash-register fiskaly SIGN AT resource manager (SCU + cash register + receipt sign).
/// </summary>
public sealed class FiskalyTseService : IFiskalyTseService
{
    private static readonly Regex AustrianVatId = new(@"^ATU\d{8}$", RegexOptions.CultureInvariant);

    private readonly IFiskalyClient _client;
    private readonly IOptionsMonitor<FiskalyOptions> _options;
    private readonly AppDbContext _db;
    private readonly ILogger<FiskalyTseService> _logger;

    public FiskalyTseService(
        IFiskalyClient client,
        IOptionsMonitor<FiskalyOptions> options,
        AppDbContext db,
        ILogger<FiskalyTseService> logger)
    {
        _client = client;
        _options = options;
        _db = db;
        _logger = logger;
    }

    public async Task<FiskalyAuthResult> AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        EnsureCredentials();
        try
        {
            return await _client.AuthenticateAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (FiskalyApiException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "fiskaly AuthenticateAsync failed in {Environment}", DescribeEnvironment());
            throw new FiskalyApiException($"fiskaly authentication failed ({DescribeEnvironment()}): {ex.Message}");
        }
    }

    public async Task<FiskalyScuInfo> CreateTssAsync(
        string tssId,
        string? vatId = null,
        CancellationToken cancellationToken = default)
    {
        EnsureCredentials();
        if (!Guid.TryParse(tssId, out var scuId) || scuId == Guid.Empty)
            throw new ArgumentException("tssId must be a UUIDv4 (SIGN AT SCU id).", nameof(tssId));

        var resolvedVat = await ResolveVatIdAsync(vatId, cancellationToken).ConfigureAwait(false);
        try
        {
            var scu = await _client
                .CreateSignatureCreationUnitAsync(scuId, resolvedVat, "Regkasse", cancellationToken)
                .ConfigureAwait(false);
            _logger.LogInformation("fiskaly SCU created/retrieved {ScuId} state={State}", scu.Id, scu.State);
            return scu;
        }
        catch (FiskalyApiException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw Wrap(ex, "CreateTssAsync");
        }
    }

    public async Task<FiskalyCashRegisterInfo> CreateClientAsync(
        string tssId,
        string clientId,
        CancellationToken cancellationToken = default)
    {
        EnsureCredentials();
        _ = tssId; // SIGN AT cash registers are not nested under an SCU id.
        if (!Guid.TryParse(clientId, out var cashRegisterId) || cashRegisterId == Guid.Empty)
            throw new ArgumentException("clientId must be a UUIDv4 (SIGN AT cash-register id).", nameof(clientId));

        try
        {
            var created = await _client
                .CreateCashRegisterAsync(cashRegisterId, "Regkasse POS", cancellationToken)
                .ConfigureAwait(false);
            _logger.LogInformation(
                "fiskaly cash register created/retrieved {CashRegisterId} state={State}",
                created.Id,
                created.State);
            return created;
        }
        catch (FiskalyApiException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw Wrap(ex, "CreateClientAsync");
        }
    }

    public async Task<FiskalySignedReceipt> SignTransactionAsync(
        string tssId,
        string txId,
        FiskalyTransactionData data,
        CancellationToken cancellationToken = default)
    {
        EnsureCredentials();
        ArgumentNullException.ThrowIfNull(data);
        _ = tssId;

        if (!Guid.TryParse(txId, out var receiptId) || receiptId == Guid.Empty)
            throw new ArgumentException("txId must be a UUIDv4 (SIGN AT receipt id).", nameof(txId));

        var cashRegisterRaw = string.IsNullOrWhiteSpace(data.CashRegisterId)
            ? throw new ArgumentException("TransactionData.CashRegisterId is required.", nameof(data))
            : data.CashRegisterId;
        if (!Guid.TryParse(cashRegisterRaw, out var cashRegisterId) || cashRegisterId == Guid.Empty)
            throw new ArgumentException("TransactionData.CashRegisterId must be a UUIDv4.", nameof(data));

        try
        {
            return await _client
                .SignReceiptAsync(cashRegisterId, receiptId, data, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (FiskalyApiException ex)
        {
            _logger.LogWarning(
                ex,
                "fiskaly SignTransactionAsync failed ({Environment}). SCU/cash register must be INITIALIZED to sign.",
                DescribeEnvironment());
            throw;
        }
        catch (Exception ex)
        {
            throw Wrap(ex, "SignTransactionAsync");
        }
    }

    public async Task<FiskalyResourceEnsureResult> EnsureResourcesForCashRegisterAsync(
        Guid tenantId,
        Guid cashRegisterId,
        string registerNumber,
        CancellationToken cancellationToken = default)
    {
        if (!_options.CurrentValue.HasCredentials)
        {
            return new FiskalyResourceEnsureResult
            {
                Success = false,
                Message = "Fiskaly credentials are not configured."
            };
        }

        try
        {
            await AuthenticateAsync(cancellationToken).ConfigureAwait(false);

            var scuId = await ResolveScuIdAsync(tenantId, cancellationToken).ConfigureAwait(false);
            var scu = await CreateTssAsync(scuId.ToString("D"), vatId: null, cancellationToken)
                .ConfigureAwait(false);
            var client = await CreateClientAsync(scu.Id, cashRegisterId.ToString("D"), cancellationToken)
                .ConfigureAwait(false);

            return new FiskalyResourceEnsureResult
            {
                Success = true,
                ScuId = scu.Id,
                ScuState = scu.State,
                CashRegisterId = client.Id,
                CashRegisterState = client.State,
                Message = $"SCU {scu.State}; cash register {client.State} ({registerNumber})."
            };
        }
        catch (FiskalyApiException ex)
        {
            _logger.LogWarning(
                ex,
                "fiskaly EnsureResourcesForCashRegisterAsync failed for tenant {TenantId} register {CashRegisterId}",
                tenantId,
                cashRegisterId);
            return new FiskalyResourceEnsureResult
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    private async Task<Guid> ResolveScuIdAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var configured = _options.CurrentValue.SignatureCreationUnitId;
        if (Guid.TryParse(configured, out var configuredId) && configuredId != Guid.Empty)
            return configuredId;

        var existing = await _db.TseDevices
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(d => d.TenantId == tenantId && d.DeviceType == "fiskaly" && d.IsActive)
            .Select(d => d.DeviceId ?? d.SerialNumber)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (Guid.TryParse(existing, out var existingId) && existingId != Guid.Empty)
            return existingId;

        return Guid.NewGuid();
    }

    private async Task<string> ResolveVatIdAsync(string? vatId, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(vatId) && AustrianVatId.IsMatch(vatId.Trim().ToUpperInvariant()))
            return vatId.Trim().ToUpperInvariant();

        var fromSettings = await _db.CompanySettings
            .AsNoTracking()
            .Select(s => s.CompanyTaxNumber)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(fromSettings)
            && AustrianVatId.IsMatch(fromSettings.Trim().ToUpperInvariant()))
        {
            return fromSettings.Trim().ToUpperInvariant();
        }

        return FiskalyConnectionProbe.DefaultTestVatId;
    }

    private void EnsureCredentials()
    {
        if (!_options.CurrentValue.HasCredentials)
        {
            throw new FiskalyApiException(
                "Fiskaly:Enabled is false or ApiKey/ApiSecret are missing. "
                + "Set user-secrets Fiskaly:ApiKey / Fiskaly:ApiSecret.");
        }
    }

    private string DescribeEnvironment()
    {
        var tseEnv = string.IsNullOrWhiteSpace(_options.CurrentValue.ApiBaseUrl)
            ? "unknown"
            : _options.CurrentValue.ApiBaseUrl;
        return tseEnv.Contains("rksv.fiskaly.com", StringComparison.OrdinalIgnoreCase) ? "TEST/SIGN-AT" : tseEnv;
    }

    private FiskalyApiException Wrap(Exception ex, string operation) =>
        new($"fiskaly {operation} failed ({DescribeEnvironment()}): {ex.Message}");
}
