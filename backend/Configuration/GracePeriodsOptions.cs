using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.Configuration;

/// <summary>
/// Undo / delayed-execution windows for critical admin actions. Bound from <c>GracePeriods</c>.
/// Fiscal actions (Schlussbeleg) use deferred execution — cancel before execute; never undo a signed receipt.
/// </summary>
public sealed class GracePeriodsOptions
{
    public const string SectionName = "GracePeriods";

    /// <summary>When false, schedule APIs still return config but FA may skip delayed flow.</summary>
    public bool Enabled { get; set; }

    /// <summary>How often the executor polls due rows (seconds).</summary>
    public int ExecutorPollSeconds { get; set; } = 30;

    public GracePeriodRuleOptions Schlussbeleg { get; set; } = new()
    {
        Duration = TimeSpan.FromMinutes(30),
        RequiresApproval = true,
    };

    public GracePeriodRuleOptions TenantDeletion { get; set; } = new()
    {
        Duration = TimeSpan.FromHours(24),
        RequiresApproval = true,
    };

    public GracePeriodRuleOptions BulkDelete { get; set; } = new()
    {
        Duration = TimeSpan.FromHours(1),
        RequiresApproval = false,
    };

    public GracePeriodRuleOptions PriceUpdate { get; set; } = new()
    {
        Duration = TimeSpan.FromMinutes(15),
        RequiresApproval = false,
    };

    public GracePeriodRuleOptions LicenseChange { get; set; } = new()
    {
        Duration = TimeSpan.FromHours(2),
        RequiresApproval = true,
    };

    public GracePeriodRuleOptions? Resolve(string actionKind) =>
        actionKind switch
        {
            GracePeriodActionKinds.Schlussbeleg => Schlussbeleg,
            GracePeriodActionKinds.TenantDeletion => TenantDeletion,
            GracePeriodActionKinds.BulkDelete => BulkDelete,
            GracePeriodActionKinds.PriceUpdate => PriceUpdate,
            GracePeriodActionKinds.LicenseChange => LicenseChange,
            _ => null,
        };
}

public sealed class GracePeriodRuleOptions
{
    /// <summary>Undo / delay window (ISO duration or TimeSpan string, e.g. 00:30:00).</summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>When true, scheduling may require critical-action approval header.</summary>
    public bool RequiresApproval { get; set; }
}

/// <summary>Canonical action kind strings stored on <c>grace_period_pendings.action_kind</c>.</summary>
public static class GracePeriodActionKinds
{
    public const string Schlussbeleg = "Schlussbeleg";
    public const string TenantDeletion = "TenantDeletion";
    public const string BulkDelete = "BulkDelete";
    public const string PriceUpdate = "PriceUpdate";
    public const string LicenseChange = "LicenseChange";
}

public static class GracePeriodStatuses
{
    public const string Pending = "Pending";
    public const string Cancelled = "Cancelled";
    public const string Executed = "Executed";
    public const string Failed = "Failed";
}
