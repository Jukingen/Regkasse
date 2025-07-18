using System;
using System.Collections.Generic;

namespace Registrierkasse_API.TempScaffold;

public partial class UserSetting
{
    public Guid Id { get; set; }

    public string UserId { get; set; } = null!;

    public string? Language { get; set; }

    public string? Theme { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsActive { get; set; }

    public virtual AspNetUser User { get; set; } = null!;
}
