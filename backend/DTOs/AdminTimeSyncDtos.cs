namespace KasseAPI_Final.DTOs;

public sealed class AdminTimeSyncStatusDto
{
    public DateTime SystemTimeUtc { get; set; }

    /// <summary>Europe/Vienna wall-clock string for operators.</summary>
    public string SystemTimeLocalVienna { get; set; } = string.Empty;

    public DateTime? NtpTimeUtc { get; set; }

    public double? OffsetSeconds { get; set; }

    public bool IsSynchronized { get; set; }

    public DateTime? LastSyncAt { get; set; }

    public string WarningLevel { get; set; } = "ok";

    /// <summary>Synchronized | Warning | Critical (admin UI badge).</summary>
    public string StatusBadge { get; set; } = "Synchronized";

    public NtpAdminConfigurationDto EffectiveConfiguration { get; set; } = new();
}

public sealed class NtpAdminConfigurationDto
{
    public bool AutoSyncEnabled { get; set; }

    public int SyncIntervalMinutes { get; set; }

    public int MaxAllowedOffsetSeconds { get; set; }

    public int CriticalOffsetSeconds { get; set; }

    /// <summary>False until an admin row exists in <c>ntp_admin_settings</c>.</summary>
    public bool HasDatabaseOverride { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
}

public sealed class NtpAdminConfigurationUpdateDto
{
    public bool AutoSyncEnabled { get; set; }

    public int SyncIntervalMinutes { get; set; }

    public int MaxAllowedOffsetSeconds { get; set; }

    public int CriticalOffsetSeconds { get; set; }
}

public sealed class SystemTimeSyncLogEntryDto
{
    public Guid Id { get; set; }

    public DateTime SyncTimeUtc { get; set; }

    public double OffsetSeconds { get; set; }

    public string NtpServerUsed { get; set; } = string.Empty;

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }
}

public sealed class NtpManualSyncResponseDto
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public double? OffsetSeconds { get; set; }

    public DateTime SyncTimeUtc { get; set; }
}

public sealed class TimeSyncDriftSummaryDto
{
    public bool HasActiveDrift { get; set; }

    public int RegisterCountOverThreshold { get; set; }

    public double? LargestAbsoluteOffsetSeconds { get; set; }

    public int MaxAllowedOffsetSecondsThreshold { get; set; }
}

public sealed class CashRegisterTimeDriftRowDto
{
    public Guid CashRegisterId { get; set; }

    public string RegisterNumber { get; set; } = string.Empty;

    public double? LastServerTimeOffsetSeconds { get; set; }

    public DateTime? LastServerTimeDriftAtUtc { get; set; }
}
