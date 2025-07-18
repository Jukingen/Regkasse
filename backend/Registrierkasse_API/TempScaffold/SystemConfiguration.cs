using System;
using System.Collections.Generic;

namespace Registrierkasse_API.TempScaffold;

public partial class SystemConfiguration
{
    public Guid Id { get; set; }

    public string Key { get; set; } = null!;

    public string? Value { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsActive { get; set; }
}
