namespace KasseAPI_Final.Services.AdminCashRegisters;

/// <summary>
/// Assignment was refused because the register is open on another user's shift.
/// The controller maps this to HTTP 409. The assigned user id is left unchanged.
/// </summary>
public sealed class CashRegisterAssignmentConflictException : Exception
{
    public const string ErrorCode = "REGISTER_OPEN_BY_OTHER_USER";

    public CashRegisterAssignmentConflictException(string message)
        : base(message)
    {
    }
}
