namespace KasseAPI_Final.Fiscal;

/// <summary>
/// Fiscal signing was requested for a country path that is gated or not implemented.
/// </summary>
public sealed class FiscalSigningNotAvailableException : InvalidOperationException
{
    public const string DeFlagOff = "DE_FLAG_OFF";
    public const string DeNotReady = "DE_NOT_READY";
    public const string ChNotImplemented = "CH_NOT_IMPLEMENTED";

    public FiscalSigningNotAvailableException(string code)
        : base(code)
    {
        Code = code;
    }

    public string Code { get; }
}
