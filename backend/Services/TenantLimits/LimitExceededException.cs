using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Limits;

/// <summary>Thrown when an operation would exceed a configured <c>tenant_limits</c> cap.</summary>
public sealed class LimitExceededException : Exception
{
    public const string ErrorCodeValue = "LIMIT_EXCEEDED";

    public string ErrorCode => ErrorCodeValue;

    public string LimitKey { get; }

    public int Limit { get; }

    public int CurrentValue { get; }

    public decimal LimitAmount { get; }

    public decimal CurrentAmount { get; }

    public LimitExceededException(string limitKey, int limit, int currentValue, string message)
        : this(limitKey, limit, currentValue, limit, currentValue, message)
    {
    }

    public LimitExceededException(string limitKey, decimal limit, decimal currentValue, string message)
        : this(
            limitKey,
            decimal.ToInt32(decimal.Truncate(limit)),
            decimal.ToInt32(decimal.Truncate(currentValue)),
            limit,
            currentValue,
            message)
    {
    }

    private LimitExceededException(
        string limitKey,
        int limit,
        int currentValue,
        decimal limitAmount,
        decimal currentAmount,
        string message)
        : base(message)
    {
        LimitKey = limitKey;
        Limit = limit;
        CurrentValue = currentValue;
        LimitAmount = limitAmount;
        CurrentAmount = currentAmount;
    }

    public bool CanForce =>
        string.Equals(LimitKey, TenantLimitKeys.MaxActiveRegistersPerUser, StringComparison.Ordinal);

    public LimitErrorDto ToErrorDto() => new()
    {
        Code = ErrorCodeValue,
        LimitKey = LimitKey,
        Limit = Limit,
        Current = CurrentValue,
        Message = Message,
        CanForce = CanForce,
    };

    public LimitErrorDto ToConflictBody() => ToErrorDto();
}
