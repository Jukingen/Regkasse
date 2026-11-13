namespace KasseAPI_Final.Services.Trial;

/// <summary>Thrown when a trial tenant exceeds configured register or user caps.</summary>
public sealed class TrialLimitExceededException : Exception
{
    public const string ErrorCodeValue = "TRIAL_LIMIT_EXCEEDED";

    public string ErrorCode => ErrorCodeValue;

    public string LimitKind { get; }

    public int MaxAllowed { get; }

    public TrialLimitExceededException(string limitKind, int maxAllowed, string message)
        : base(message)
    {
        LimitKind = limitKind;
        MaxAllowed = maxAllowed;
    }
}
