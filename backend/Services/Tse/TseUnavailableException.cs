namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Raised when a fiscal receipt must be signed but no allowed TSE path is ready
/// (Fiskaly not initialized, or Soft TSE forbidden outside Development).
/// </summary>
public sealed class TseUnavailableException : InvalidOperationException
{
    public TseUnavailableException(string message)
        : base(message)
    {
    }

    public TseUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
