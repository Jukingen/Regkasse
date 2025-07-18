using System;
using System.Collections.Generic;

namespace Registrierkasse_API.TempScaffold;

public partial class Discount
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string DiscountType { get; set; } = null!;

    public decimal Value { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public decimal? MinPurchaseAmount { get; set; }

    public decimal? MaxDiscountAmount { get; set; }

    public string Code { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsActive { get; set; }
}
