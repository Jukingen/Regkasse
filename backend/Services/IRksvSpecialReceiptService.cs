using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services;

public interface IRksvSpecialReceiptService
{
    /// <summary>
    /// RKSV Monats-Nullbeleg: zero TSE-signed receipt in normal Beleg sequence (not <c>DAILY_</c> closing prefix).
    /// </summary>
    /// <exception cref="InvalidOperationException">Duplicate month, TSE not ready, register missing, etc.</exception>
    Task<CreateNullbelegResponse> CreateNullbelegAsync(
        CreateNullbelegRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>RKSV Startbeleg: one active zero signed receipt per cash register (normal Beleg sequence).</summary>
    Task<CreateStartbelegResponse> CreateStartbelegAsync(
        CreateStartbelegRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>RKSV Monatsbeleg: one active zero signed receipt per cash register per Vienna calendar month.</summary>
    /// <param name="forcePastMonth">When true, allows creation for a past Vienna calendar month (admin override).</param>
    Task<CreateMonatsbelegResponse> CreateMonatsbelegAsync(
        CreateMonatsbelegRequest request,
        string actorUserId,
        bool forcePastMonth = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// RKSV Jahresbeleg: one active zero signed receipt per cash register per Vienna calendar year.
    /// December <see cref="CreateMonatsbelegAsync"/> delegates here (December Monatsbeleg is effectively Jahresbeleg).
    /// </summary>
    Task<CreateJahresbelegResponse> CreateJahresbelegAsync(
        CreateJahresbelegRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// RKSV Schlussbeleg: one per cash register; sets register to <see cref="RegisterStatus.Decommissioned"/> atomically with the signed zero receipt.
    /// </summary>
    Task<CreateSchlussbelegResponse> CreateSchlussbelegAsync(
        CreateSchlussbelegRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default);
}
