using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.FeatureFlags;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Countries.KassenSicherheit;

/// <summary>
/// SIGN DE sandbox service. Calls the HTTP client only when the flag is on and
/// <c>Provider=fiskaly-de</c>. Reached from <c>PaymentService</c> via
/// <c>IFiscalSignatureRouter.SignAsync</c> → <c>SignDeAsync</c>. Start/Finish/Export
/// HTTP methods are implemented but not exposed on <c>IKassenSicherheitService</c>.
/// </summary>
public sealed class FiskalyDeKassenSicherheitService : IKassenSicherheitService
{
    private readonly IFeatureFlagService _featureFlags;
    private readonly IOptions<KassenSicherheitOptions> _options;
    private readonly IKassenSicherheitHttpClient _http;

    public FiskalyDeKassenSicherheitService(
        IFeatureFlagService featureFlags,
        IOptions<KassenSicherheitOptions> options,
        IKassenSicherheitHttpClient http)
    {
        _featureFlags = featureFlags ?? throw new ArgumentNullException(nameof(featureFlags));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    /// <summary>
    /// Upserts a SIGN DE transaction. <c>PaymentService</c> reaches it through
    /// <c>FiscalSignatureRouter.SignDeAsync</c>, not by calling this type directly.
    /// </summary>
    public Task<KassenSicherheitSignResult> SignAsync(
        KassenSicherheitSignRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return CallFiskalyAsync(
            request.TenantId,
            cancellationToken,
            () => _http.SignAsync(request, cancellationToken));
    }

    public Task<KassenSicherheitTransactionResult> StartTransactionAsync(
        KassenSicherheitStartTransactionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return DispatchAsync(
            request.TenantId,
            cancellationToken,
            onReady: async ct =>
            {
                await EnsureTssAndClientAsync(request.TenantId, request.TssId, request.ClientId, ct).ConfigureAwait(false);
                return await _http.StartTransactionAsync(request, ct).ConfigureAwait(false);
            },
            onNoOp: () => Task.FromResult(NoOpTransaction()));
    }

    public Task<KassenSicherheitTransactionResult> FinishTransactionAsync(
        KassenSicherheitFinishTransactionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return DispatchAsync(
            request.TenantId,
            cancellationToken,
            onReady: async ct =>
            {
                await EnsureTssAndClientAsync(request.TenantId, request.TssId, request.ClientId, ct).ConfigureAwait(false);
                return await _http.FinishTransactionAsync(request, ct).ConfigureAwait(false);
            },
            onNoOp: () => Task.FromResult(NoOpTransaction()));
    }

    public Task<KassenSicherheitExportResult> ExportDsfinvkAsync(
        KassenSicherheitExportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return DispatchAsync(
            request.TenantId,
            cancellationToken,
            onReady: ct => _http.ExportDsfinvkAsync(request, ct),
            onNoOp: () => Task.FromResult(new KassenSicherheitExportResult(
                Exported: false,
                ExportId: null,
                Provider: CountryFiscalLockEvaluator.SentinelNotConfigured)));
    }

    private Task<T> DispatchAsync<T>(
        Guid tenantId,
        CancellationToken cancellationToken,
        Func<CancellationToken, Task<T>> onReady,
        Func<Task<T>> onNoOp)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_featureFlags.IsEnabled(FeatureFlagNames.FiscalKassenSicherheitDe, tenantId.ToString("D")))
            throw new FeatureDisabledException(FeatureFlagNames.FiscalKassenSicherheitDe);

        var provider = _options.Value.Provider?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(provider)
            || string.Equals(provider, CountryFiscalLockEvaluator.SentinelNotConfigured, StringComparison.OrdinalIgnoreCase))
        {
            return onNoOp();
        }

        if (string.Equals(provider, FiskalyDeKassenSicherheitHttpClient.ProviderId, StringComparison.OrdinalIgnoreCase))
            return onReady(cancellationToken);

        throw new NotImplementedException(
            "DE KassenSicherheit provider is not implemented. See docs/FISCAL_GERMANY.md.");
    }

    public Task<KassenSicherheitStatusResult> GetStatusAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return CallFiskalyAsync(
            tenantId,
            cancellationToken,
            () => _http.GetStatusAsync(tenantId, cancellationToken));
    }

    public Task<KassenSicherheitCertificateChainResult> GetCertificateChainAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return CallFiskalyAsync(
            tenantId,
            cancellationToken,
            () => _http.GetCertificateChainAsync(tenantId, cancellationToken));
    }

    private Task<T> CallFiskalyAsync<T>(
        Guid tenantId,
        CancellationToken cancellationToken,
        Func<Task<T>> call)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_featureFlags.IsEnabled(FeatureFlagNames.FiscalKassenSicherheitDe, tenantId.ToString("D")))
            throw new FeatureDisabledException(FeatureFlagNames.FiscalKassenSicherheitDe);

        var provider = _options.Value.Provider?.Trim() ?? string.Empty;
        if (!string.Equals(provider, FiskalyDeKassenSicherheitHttpClient.ProviderId, StringComparison.OrdinalIgnoreCase))
            throw new KassenSicherheitNotConfiguredException();

        return call();
    }

    private async Task EnsureTssAndClientAsync(Guid tenantId, string tssId, string clientId, CancellationToken cancellationToken)
    {
        await _http.AuthenticateAsync(cancellationToken).ConfigureAwait(false);
        await _http.CreateAndInitializeTssAsync(
            new KassenSicherheitCreateTssRequest(tenantId, tssId),
            cancellationToken).ConfigureAwait(false);
        await _http.CreateClientAsync(
            new KassenSicherheitCreateClientRequest(tenantId, tssId, clientId, clientId),
            cancellationToken).ConfigureAwait(false);
    }

    private static KassenSicherheitTransactionResult NoOpTransaction() =>
        new(
            Completed: false,
            TransactionId: null,
            State: null,
            TxRevision: null,
            Signature: null,
            Provider: CountryFiscalLockEvaluator.SentinelNotConfigured);
}
