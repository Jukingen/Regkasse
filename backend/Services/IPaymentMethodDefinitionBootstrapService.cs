namespace KasseAPI_Final.Services;

/// <summary>Seeds default payment method catalog rows for a cash register when none exist.</summary>
public interface IPaymentMethodDefinitionBootstrapService
{
    /// <summary>
    /// When the register has no rows yet: copy from another register in the tenant if available, otherwise insert defaults.
    /// No-op when rows already exist for <paramref name="cashRegisterId"/>.
    /// </summary>
    Task EnsureDefaultsForCashRegisterAsync(Guid tenantId, Guid cashRegisterId, CancellationToken cancellationToken = default);
}
