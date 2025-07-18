using System;
using System.Collections.Generic;

namespace Registrierkasse_API.TempScaffold;

public partial class TableReservation
{
    public Guid Id { get; set; }

    public Guid TableId { get; set; }

    public string CustomerName { get; set; } = null!;

    public string CustomerPhone { get; set; } = null!;

    public int GuestCount { get; set; }

    public DateTime ReservationTime { get; set; }

    public string Notes { get; set; } = null!;

    public int Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsActive { get; set; }

    public virtual Table Table { get; set; } = null!;
}
