namespace KasseAPI_Final.DTOs;

public sealed class CreateCashRegisterOpenRequestBody
{
    public Guid RegisterId { get; set; }

    public string? Note { get; set; }
}

public sealed class ResolveCashRegisterOpenRequestBody
{
    public string? Note { get; set; }
}

public sealed class CashRegisterOpenRequestDto
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string? TenantName { get; init; }
    public string? TenantSlug { get; init; }
    public Guid CashRegisterId { get; init; }
    public string RegisterNumber { get; init; } = string.Empty;
    public string? Location { get; init; }
    public string Status { get; init; } = string.Empty;
    public string RequestedByUserId { get; init; } = string.Empty;
    public string? RequestedByUserName { get; init; }
    public DateTime RequestedAt { get; init; }
    public string? Note { get; init; }
    public string? ResolvedByUserId { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public string? ResolutionNote { get; init; }
}

public sealed class CashRegisterOpenRequestMutationResult
{
    public bool Succeeded { get; init; }
    public string? Code { get; init; }
    public string? Error { get; init; }
    public CashRegisterOpenRequestDto? Request { get; init; }
}
