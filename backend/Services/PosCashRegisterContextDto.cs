namespace KasseAPI_Final.Services;

/// <summary>
/// JSON contract for POS cash-register readiness (POST /api/pos/cash-register/ensure-ready).
/// </summary>
/// <remarks>
/// <para><see cref="PreferredRegisterId"/> is an echo of persisted <c>UserSettings.CashRegisterId</c> (preference only).</para>
/// <para><see cref="EffectiveRegisterId"/> is the register row this readiness pass resolved for UX
/// (settings match, sole-register fallback, or tenant-default fallback — same preference order as FA auto-select).
/// It is not a standalone payment authorization; payment POST validates the body id with occupancy and policy rules.</para>
/// </remarks>
public sealed class PosCashRegisterContextDto
{
    /// <summary>
    /// Persisted user preference from <c>UserSettings.CashRegisterId</c> when it is a non-empty GUID string; otherwise null.
    /// Does not imply the register is operable for payment (see <see cref="EffectiveRegisterId"/>, <c>nextAction</c>, <c>messageCode</c>).
    /// </summary>
    public string? PreferredRegisterId { get; set; }

    /// <summary>
    /// Register id the readiness pipeline attached to this response (resolved from preference and/or sole-register rules).
    /// </summary>
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

    /// <summary>RKSV Startbeleg missing; client must create zero receipt before shift/sales.</summary>
    public const string StartbelegRequired = "CASH_REGISTER_STARTBELEG_REQUIRED";

    /// <summary>RKSV Monatsbeleg missing for the current Vienna calendar month; client must create zero receipt before shift/sales.</summary>
    public const string MonatsbelegRequired = "CASH_REGISTER_MONATSBELEG_REQUIRED";

    /// <summary>RKSV Schlussbeleg was issued; this register cannot be used for new sessions or receipts.</summary>
    public const string RegisterDecommissioned = "CASH_REGISTER_DECOMMISSIONED_RKSV";
}
