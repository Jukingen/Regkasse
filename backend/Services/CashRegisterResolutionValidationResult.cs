namespace KasseAPI_Final.Services;

/// <summary>
/// Result of validating a cash register for assignment or payment.
/// </summary>
public sealed class CashRegisterResolutionValidationResult
{
    public bool Ok { get; init; }
    public string? Code { get; init; }
    public string Message { get; init; } = string.Empty;
    public Guid? ResolvedRegisterId { get; init; }
    public string? RegisterNumber { get; init; }

    public static CashRegisterResolutionValidationResult Success(
        Guid registerId,
        string registerNumber) =>
        new()
        {
            Ok = true,
            ResolvedRegisterId = registerId,
            RegisterNumber = registerNumber
        };

    public static CashRegisterResolutionValidationResult Failure(string code, string message) =>
        new() { Ok = false, Code = code, Message = message };
}
