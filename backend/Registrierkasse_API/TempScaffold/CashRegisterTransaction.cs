using System;
using System.Collections.Generic;

namespace Registrierkasse_API.TempScaffold;

public partial class CashRegisterTransaction
{
    public Guid Id { get; set; }

    public string CashRegisterId { get; set; } = null!;

    public Guid CashRegisterId1 { get; set; }

    public decimal Amount { get; set; }

    public int TransactionType { get; set; }

    public string? Description { get; set; }

    public string? Reference { get; set; }

    public string? UserId { get; set; }

    public decimal BalanceBefore { get; set; }

    public decimal BalanceAfter { get; set; }

    public string Tsesignature { get; set; } = null!;

    public int TsesignatureCounter { get; set; }

    public DateTime? Tsetime { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsActive { get; set; }

    public virtual CashRegister CashRegisterId1Navigation { get; set; } = null!;

    public virtual AspNetUser? User { get; set; }
}
