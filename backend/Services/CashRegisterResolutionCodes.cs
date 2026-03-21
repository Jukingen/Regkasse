namespace KasseAPI_Final.Services;

/// <summary>
/// Stable diagnostic codes for cash-register resolution (POS payment + settings assignment).
/// </summary>
public static class CashRegisterResolutionCodes
{
    public const string Required = "CASH_REGISTER_REQUIRED";
    public const string Invalid = "CASH_REGISTER_INVALID";
    public const string NotFound = "CASH_REGISTER_NOT_FOUND";
    public const string Forbidden = "CASH_REGISTER_FORBIDDEN";
    public const string Closed = "CASH_REGISTER_CLOSED";
    public const string SelectionRequired = "CASH_REGISTER_SELECTION_REQUIRED";
}
