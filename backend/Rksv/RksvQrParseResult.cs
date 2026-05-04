namespace KasseAPI_Final.Rksv;

/// <summary>Outcome of <see cref="RksvQrParser.Parse"/> (no exceptions for malformed input).</summary>
public sealed record RksvQrParseResult(bool Success, RksvQrPayload? Payload, IReadOnlyList<string> Errors)
{
    public static RksvQrParseResult Ok(RksvQrPayload payload) =>
        new(true, payload, Array.Empty<string>());

    public static RksvQrParseResult Fail(IReadOnlyList<string> errors) =>
        new(false, null, errors);
}
