using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Limits;

/// <summary>
/// Builds Super Admin limit patches for the Development QA panel.
/// Does not create fiscal/payment/backup/offline rows — caps are moved relative to live usage.
/// </summary>
public static class DevLimitScenarioPlanner
{
    public static string NormalizeScenario(string scenario)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenario);
        return scenario.Trim().ToLowerInvariant() switch
        {
            "near" or "nearlimit" or "warning" => DevLimitScenarioNames.Near,
            "at" or "atlimit" or "block" or "blocknext" => DevLimitScenarioNames.At,
            "tiny" or "min" or "minimum" => DevLimitScenarioNames.Tiny,
            "reset" or "defaults" or "default" => DevLimitScenarioNames.Reset,
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown limit test scenario."),
        };
    }

    /// <summary>
    /// Returns a partial update, or <c>null</c> when the caller should run a full reset.
    /// </summary>
    public static UpdateTenantLimitsRequest? Build(
        TenantLimitUsageDto usage,
        string scenario,
        string? limitKey)
    {
        ArgumentNullException.ThrowIfNull(usage);
        var name = NormalizeScenario(scenario);
        if (name == DevLimitScenarioNames.Reset && string.IsNullOrWhiteSpace(limitKey))
            return null;

        var keys = string.IsNullOrWhiteSpace(limitKey)
            ? TenantLimitKeys.All
            : [TenantLimits.NormalizeLimitKey(limitKey)];

        var request = new UpdateTenantLimitsRequest();
        foreach (var key in keys)
            ApplyKey(request, usage, name, key);

        return request;
    }

    private static void ApplyKey(
        UpdateTenantLimitsRequest request,
        TenantLimitUsageDto usage,
        string scenario,
        string key)
    {
        switch (key)
        {
            case TenantLimitKeys.MaxActiveRegistersPerUser:
                request.MaxActiveRegistersPerUser = IntTarget(
                    scenario, usage.CurrentMaxAssignedRegistersPerUser, TenantLimits.DefaultMaxActiveRegistersPerUser);
                break;
            case TenantLimitKeys.MaxProductsPerTenant:
                request.MaxProductsPerTenant = IntTarget(
                    scenario, usage.CurrentProducts, TenantLimits.DefaultMaxProductsPerTenant);
                break;
            case TenantLimitKeys.MaxUsersPerTenant:
                request.MaxUsersPerTenant = IntTarget(
                    scenario, usage.CurrentUsers, TenantLimits.DefaultMaxUsersPerTenant);
                break;
            case TenantLimitKeys.DailyMaxTransactions:
                request.DailyMaxTransactions = IntTarget(
                    scenario, usage.CurrentDailyTransactions, TenantLimits.DefaultDailyMaxTransactions);
                break;
            case TenantLimitKeys.MaxTransactionAmount:
                request.MaxTransactionAmount = MoneyTarget(
                    scenario,
                    current: 0m,
                    @default: TenantLimits.DefaultMaxTransactionAmount,
                    tinyFallback: 1m,
                    nearWhenEmpty: 10m);
                break;
            case TenantLimitKeys.DailyMaxRevenue:
                request.DailyMaxRevenue = MoneyTarget(
                    scenario,
                    current: usage.CurrentDailyRevenue,
                    @default: TenantLimits.DefaultDailyMaxRevenue,
                    tinyFallback: 1m,
                    nearWhenEmpty: 100m);
                break;
            case TenantLimitKeys.MaxBackupsPerTenant:
                request.MaxBackupsPerTenant = IntTarget(
                    scenario, usage.CurrentBackups, TenantLimits.DefaultMaxBackupsPerTenant);
                break;
            case TenantLimitKeys.MaxBackupSizeMb:
                request.MaxBackupSizeMb = IntTarget(
                    scenario,
                    current: Math.Max(0, (int)decimal.Ceiling(usage.CurrentBackupSizeMb)),
                    @default: TenantLimits.DefaultMaxBackupSizeMb);
                break;
            case TenantLimitKeys.MaxOfflineTransactions:
                request.MaxOfflineTransactions = IntTarget(
                    scenario, usage.CurrentOfflineTransactions, TenantLimits.DefaultMaxOfflineTransactions);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown tenant limit key.");
        }
    }

    private static int IntTarget(string scenario, int current, int @default) =>
        scenario switch
        {
            DevLimitScenarioNames.Reset => @default,
            DevLimitScenarioNames.Tiny => 1,
            DevLimitScenarioNames.At => Math.Max(1, current),
            _ => NearInt(current),
        };

    private static decimal MoneyTarget(
        string scenario,
        decimal current,
        decimal @default,
        decimal tinyFallback,
        decimal nearWhenEmpty) =>
        scenario switch
        {
            DevLimitScenarioNames.Reset => @default,
            DevLimitScenarioNames.Tiny => tinyFallback,
            DevLimitScenarioNames.At => current > 0m ? Math.Max(0.01m, decimal.Round(current, 2)) : tinyFallback,
            _ => current > 0m
                ? Math.Max(0.01m, decimal.Round(current / 0.8m, 2, MidpointRounding.AwayFromZero))
                : nearWhenEmpty,
        };

    private static int NearInt(int current)
    {
        if (current <= 0)
            return 5;
        return Math.Max(current + 1, (int)Math.Ceiling(current / 0.8d));
    }
}
