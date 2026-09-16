using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services;

/// <summary>POS cashier → Manager notify, daily missing reminders, optional Auto-Monatsbeleg.</summary>
public interface IMonatsbelegOpsService
{
    Task<NotifyMonatsbelegManagerResult> NotifyManagerAsync(
        string actorUserId,
        Guid cashRegisterId,
        CancellationToken cancellationToken = default);

    Task<int> PublishMissingRemindersAsync(CancellationToken cancellationToken = default);

    Task<int> RunAutoCreateAsync(CancellationToken cancellationToken = default);
}
