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

    /// <summary>RKSV: production TSE mode requires a Startbeleg before sales / shift on this register.</summary>
    public const string StartbelegRequired = "CASH_REGISTER_STARTBELEG_REQUIRED";

    /// <summary>RKSV: production TSE mode requires a Monatsbeleg for the current Vienna month before sales / shift.</summary>
    public const string MonatsbelegRequired = "CASH_REGISTER_MONATSBELEG_REQUIRED";

    /// <summary>RKSV Schlussbeleg was issued; register is permanently decommissioned.</summary>
    public const string Decommissioned = "CASH_REGISTER_DECOMMISSIONED";
}
