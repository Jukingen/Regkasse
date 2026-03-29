namespace KasseAPI_Final.Configuration;

/// <summary>Run lease / heartbeat / reaper aralıkları için ortak doğrulama.</summary>
public static class RunLeaseOptionsValidation
{
    public static string? Validate(TimeSpan runLeaseTimeout, TimeSpan heartbeatInterval, TimeSpan staleRecoveryScanInterval)
    {
        if (runLeaseTimeout < TimeSpan.FromMinutes(1))
            return "RunLeaseTimeout must be at least 1 minute.";

        if (heartbeatInterval < TimeSpan.FromSeconds(5))
            return "HeartbeatInterval must be at least 5 seconds.";

        if (staleRecoveryScanInterval < TimeSpan.FromSeconds(5))
            return "StaleRecoveryScanInterval must be at least 5 seconds.";

        if (heartbeatInterval >= runLeaseTimeout)
            return "HeartbeatInterval must be less than RunLeaseTimeout so the lease can be renewed before expiry.";

        return null;
    }

    /// <summary>Null lease satırlarını reaper’ın ne kadar bekleyeceği: RunLeaseTimeout * çarpan.</summary>
    public static string? ValidateStaleRecoveryNullLeaseGraceMultiplier(double multiplier)
    {
        if (double.IsNaN(multiplier) || double.IsInfinity(multiplier))
            return "StaleRecoveryNullLeaseGraceMultiplier must be a finite number.";

        if (multiplier < 1.0)
            return "StaleRecoveryNullLeaseGraceMultiplier must be at least 1.0.";

        if (multiplier > 48.0)
            return "StaleRecoveryNullLeaseGraceMultiplier must be at most 48.0.";

        return null;
    }
}
