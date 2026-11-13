using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Cloud TSE auto-scaling evaluator. Default is recommendation-only;
/// soft backup stubs only when policy.AutoProvision is enabled (no live vendor TSS / Startbeleg).
/// </summary>
public interface ITseAutoScalingService
{
    Task<TseScalingResultDto> EvaluateAndScaleAsync(
        Guid tenantId,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);

    Task<TseScalingHistoryDto> GetScalingHistoryAsync(
        Guid tenantId,
        int take = 50,
        CancellationToken cancellationToken = default);

    Task<TseScalingPolicyDto> GetScalingPolicyAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<TseScalingPolicyDto> ConfigureScalingPolicyAsync(
        Guid tenantId,
        ConfigureTseScalingPolicyRequestDto policy,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);

    Task<TseScalingStatusDto> GetScalingStatusAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
