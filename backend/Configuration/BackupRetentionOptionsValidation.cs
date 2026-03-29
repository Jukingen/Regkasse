namespace KasseAPI_Final.Configuration;

/// <summary>
/// Retention alanları için ValidateOnStart uyumu; silme yok, yalnızca tutarlılık.
/// </summary>
public static class BackupRetentionOptionsValidation
{
    public const int MinArtifactRetentionDays = 7;
    public const int MaxArtifactRetentionDays = 3650;

    /// <summary>Hata mesajı veya null = geçerli.</summary>
    public static string? Validate(BackupOptions options)
    {
        if (options.RetentionPolicyMode == BackupRetentionPolicyMode.Disabled)
        {
            if (options.ArtifactRetentionDays.HasValue)
                return "Backup:ArtifactRetentionDays must not be set when Backup:RetentionPolicyMode=Disabled.";
            return null;
        }

        if (!options.ArtifactRetentionDays.HasValue)
            return "Backup:ArtifactRetentionDays is required when Backup:RetentionPolicyMode is ReportOnly or ExecutionPlanned.";

        var d = options.ArtifactRetentionDays.Value;
        if (d < MinArtifactRetentionDays || d > MaxArtifactRetentionDays)
            return $"Backup:ArtifactRetentionDays must be between {MinArtifactRetentionDays} and {MaxArtifactRetentionDays} when retention policy is enabled.";

        return null;
    }
}
