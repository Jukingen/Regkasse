using System;
using System.Collections.Generic;

namespace Registrierkasse_API.TempScaffold;

public partial class InvoiceHistory
{
    public Guid Id { get; set; }

    public Guid InvoiceId { get; set; }

    public string Action { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string? Changes { get; set; }

    public string? PerformedById { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsActive { get; set; }

    public virtual Invoice Invoice { get; set; } = null!;

    public virtual AspNetUser? PerformedBy { get; set; }
}
