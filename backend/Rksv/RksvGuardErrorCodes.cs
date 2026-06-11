namespace KasseAPI_Final.Rksv;

/// <summary>
/// Stable RKSV guardrail codes for service-level validation (POS / Sonderbelege).
/// </summary>
public static class RksvGuardErrorCodes
{
    public const string RegisterDecommissioned = "RKSV_REGISTER_DECOMMISSIONED";

    /// <summary>Schlussbeleg or similar when the register was already retired.</summary>
    public const string RegisterAlreadyDecommissioned = "RKSV_REGISTER_ALREADY_DECOMMISSIONED";

    public const string InvalidRegisterState = "RKSV_INVALID_STATE";

    public const string DuplicateStartbeleg = "RKSV_DUPLICATE_STARTBELEG";

    public const string DuplicateMonatsbeleg = "RKSV_DUPLICATE_MONATSBELEG";

    /// <summary>Past Vienna month requested without <c>force=true</c> admin override.</summary>
    public const string MonatsbelegPastMonthRequiresForce = "RKSV_MONATSBELEG_PAST_MONTH_REQUIRES_FORCE";

    public const string DuplicateJahresbeleg = "RKSV_DUPLICATE_JAHRESBELEG";

    public const string DuplicateSchlussbeleg = "RKSV_DUPLICATE_SCHLUSSBELEG";

    public const string VoucherCodeRequired = "RKSV_VOUCHER_CODE_REQUIRED";
}
