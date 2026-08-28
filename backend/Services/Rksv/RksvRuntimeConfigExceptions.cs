namespace KasseAPI_Final.Services.Rksv;

public sealed class RksvRuntimeConfigLockException : InvalidOperationException
{
    public const string ErrorCode = "RKSV_PRODUCTION_LOCK";

    public RksvRuntimeConfigLockException(IReadOnlyList<string> reasons)
        : base("RKSV runtime config is not allowed on this host (TSE production lock).")
    {
        Reasons = reasons;
    }

    public IReadOnlyList<string> Reasons { get; }
}

public sealed class RksvRuntimeConfigValidationException : InvalidOperationException
{
    public const string ErrorCode = "RKSV_CONFIG_INVALID";

    public RksvRuntimeConfigValidationException(string message)
        : base(message)
    {
    }
}
