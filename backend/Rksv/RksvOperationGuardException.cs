namespace KasseAPI_Final.Rksv;

/// <summary>
/// Thrown when an RKSV-related operation violates register or receipt guardrails.
/// </summary>
public sealed class RksvOperationGuardException : Exception
{
    public string ErrorCode { get; }

    public RksvOperationGuardException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public RksvOperationGuardException(string errorCode, string message, Exception inner)
        : base(message, inner)
    {
        ErrorCode = errorCode;
    }
}
