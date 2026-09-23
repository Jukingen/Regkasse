using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.FeatureFlags;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Countries.KassenSicherheit;

/// <summary>
/// SIGN DE sandbox service. Calls the HTTP client only when the flag is on and
/// <c>Provider=fiskaly-de</c>. Not wired into <c>PaymentService</c>.
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
    /// Not the DE transaction path. <c>fiskaly-de</c> uses
    /// <see cref="StartTransactionAsync"/> and <see cref="FinishTransactionAsync"/>.
    /// </summary>
    public Task<KassenSicherheitSignResult> SignAsync(
        KassenSicherheitSignRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return DispatchAsync(
            request.TenantId,
            cancellationToken,
            onReady: _ => throw new NotImplementedException(
                "DE KassenSicherheit does not use SignAsync. Use StartTransactionAsync and FinishTransactionAsync."),
            onNoOp: () => Task.FromResult(new KassenSicherheitSignResult(
                Signed: false,
                Signature: null,
                Provider: CountryFiscalLockEvaluator.SentinelNotConfigured)));
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
