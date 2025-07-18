using System;
using System.Collections.Generic;

namespace Registrierkasse_API.TempScaffold;

public partial class Hardware
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public int Type { get; set; }

    public string Model { get; set; } = null!;

    public string SerialNumber { get; set; } = null!;

    public string ConnectionType { get; set; } = null!;

    public string Ipaddress { get; set; } = null!;

    public int? Port { get; set; }

    public string Status { get; set; } = null!;

    public string Location { get; set; } = null!;

    public string Configuration { get; set; } = null!;

    public DateTime? LastMaintenance { get; set; }

    public string Notes { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsActive { get; set; }
}
