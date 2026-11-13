namespace KasseAPI_Final.Services.License;

/// <summary>
/// Calendar-day anchors for mandant grace-period reminder emails
/// (remaining grace days after <c>license_valid_until_utc</c>).
/// </summary>
public static class GracePeriodReminderMilestones
{
    /// <summary>Default non-urgent anchors: early / mid / late grace window.</summary>
    public static readonly int[] DefaultReminderDays = [6, 4, 2];

    /// <summary>Send urgent reminder when remaining grace days are at or below this value.</summary>
    public const int DefaultUrgentDaysInclusive = 1;

    /// <summary>
    /// Resolves remaining grace days using the same whole-day math as
    /// <c>LicenseService</c> / <see cref="Tenancy.TenantLicenseValidator"/>.
    /// Returns <c>null</c> when not currently in the grace window.
    /// </summary>
    public static int? ResolveGraceDaysRemaining(
        DateTime licenseValidUntilUtc,
        DateTime utcNow,
        int gracePeriodDays)
    {
        var until = licenseValidUntilUtc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(licenseValidUntilUtc, DateTimeKind.Utc)
            : licenseValidUntilUtc.ToUniversalTime();
        var now = utcNow.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(utcNow, DateTimeKind.Utc)
            : utcNow.ToUniversalTime();

        if (now < until)
            return null;

        var daysOverdue = Math.Max(0, (now - until).Days);
        if (daysOverdue == 0)
            daysOverdue = 1;

        var graceDays = Math.Max(1, gracePeriodDays);
        if (daysOverdue > graceDays)
            return null;

        return Math.Max(0, graceDays - daysOverdue);
    }

    public static DateTime ResolveLockdownDateUtc(DateTime licenseValidUntilUtc, int gracePeriodDays)
    {
        var until = licenseValidUntilUtc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(licenseValidUntilUtc, DateTimeKind.Utc)
            : licenseValidUntilUtc.ToUniversalTime();
        return until.AddDays(Math.Max(1, gracePeriodDays));
    }

    /// <summary>
    /// Whether a grace reminder should fire for the given remaining days
    /// (configured anchors and/or urgent ≤ threshold).
    /// </summary>
    public static bool ShouldSendReminder(
        int graceDaysRemaining,
        IReadOnlyCollection<int> anchors,
        bool sendUrgent,
        int urgentDaysInclusive)
    {
        if (sendUrgent && graceDaysRemaining <= Math.Max(0, urgentDaysInclusive))
            return true;

        return anchors.Contains(graceDaysRemaining);
    }

    public static string BuildDedupKey(
        Guid tenantId,
        DateTime licenseValidUntilUtc,
        int graceDaysRemaining)
    {
        var until = licenseValidUntilUtc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(licenseValidUntilUtc, DateTimeKind.Utc)
            : licenseValidUntilUtc.ToUniversalTime();
        return $"{tenantId:N}_{until:yyyyMMdd}_grace_{graceDaysRemaining}";
    }
}
