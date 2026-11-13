namespace KasseAPI_Final.Configuration;

public sealed class ElmahOptions
{
    public const string SectionName = "Elmah";

    public string ApplicationName { get; set; } = "Regkasse";

    /// <summary>Maximum rows retained per application in <c>elmah_error</c>.</summary>
    public int MaxLogEntries { get; set; } = 5000;

    /// <summary>Hosted retention sweep interval in minutes.</summary>
    public int RetentionCheckIntervalMinutes { get; set; } = 60;
}
