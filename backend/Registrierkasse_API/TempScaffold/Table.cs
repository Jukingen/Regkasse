using System;
using System.Collections.Generic;

namespace Registrierkasse_API.TempScaffold;

public partial class Table
{
    public Guid Id { get; set; }

    public int Number { get; set; }

    public string Name { get; set; } = null!;

    public int Capacity { get; set; }

    public string Location { get; set; } = null!;

    public bool IsActive { get; set; }

    public Guid? CurrentOrderId { get; set; }

    public string Status { get; set; } = null!;

    public string CustomerName { get; set; } = null!;

    public DateTime? StartTime { get; set; }

    public DateTime? LastOrderTime { get; set; }

    public decimal TotalPaid { get; set; }

    public decimal CurrentTotal { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    public virtual Order? CurrentOrder { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<TableReservation> TableReservations { get; set; } = new List<TableReservation>();
}
