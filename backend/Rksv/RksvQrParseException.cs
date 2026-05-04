namespace KasseAPI_Final.Rksv;

/// <summary>Thrown by <see cref="RksvQrParser.ParseOrThrow"/> when the payload cannot be parsed.</summary>
public sealed class RksvQrParseException : Exception
{
    public IReadOnlyList<string> Errors { get; }

    public RksvQrParseException(IReadOnlyList<string> errors)
        : base(string.Join("; ", errors))
    {
        Errors = errors;
    }
}
