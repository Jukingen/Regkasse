namespace KasseAPI_Final.Services;

/// <summary>
/// JSON contract for POS cash-register readiness (POST /api/pos/cash-register/ensure-ready).
/// </summary>
public sealed class PosCashRegisterContextDto
{
    public string? EffectiveRegisterId { get; set; }
    public string Resolution { get; set; } = "none";
    public string? RegisterStatus { get; set; }
    public bool AutoOpened { get; set; }
    public string NextAction { get; set; } = "none";
    public string MessageCode { get; set; } = PosCashRegisterReadinessMessageCodes.CashRegisterRequired;
}

/// <summary>
/// Machine-readable readiness codes for POS clients.
/// </summary>
public static class PosCashRegisterReadinessMessageCodes
{
    public const string CashRegisterReady = "CASH_REGISTER_READY";
    public const string CashRegisterAutoOpened = "CASH_REGISTER_AUTO_OPENED";
    public const string CashRegisterRequired = "CASH_REGISTER_REQUIRED";
    public const string CashRegisterClosed = "CASH_REGISTER_CLOSED";
    public const string CashRegisterForbidden = "CASH_REGISTER_FORBIDDEN";
    public const string CashRegisterConflict = "CASH_REGISTER_CONFLICT";
    public const string CashRegisterNotFound = "CASH_REGISTER_NOT_FOUND";
    /// <summary>User already has another register open; close it before opening another.</summary>
    public const string CashRegisterActorAlreadyOpenElsewhere = "CASH_REGISTER_ACTOR_ALREADY_OPEN";
}
