using KasseAPI_Final.Models;

namespace KasseAPI_Final.DTOs;

public sealed class MonatsbelegPolicyDto
{
    public string BlockingMode { get; set; } = MonatsbelegBlockingModeNames.Strict;

    public bool AutoMonatsbelegEnabled { get; set; } = true;

    public int MonatsbelegRetryCount { get; set; } = 3;

    public bool UseDecemberMonatsbelegAsJahresbeleg { get; set; } = true;
}

public sealed class UpdateMonatsbelegPolicyRequest
{
    public string? BlockingMode { get; set; }

    public bool? AutoMonatsbelegEnabled { get; set; }

    public int? MonatsbelegRetryCount { get; set; }
}

public sealed class NotifyMonatsbelegManagerRequest
{
    public Guid CashRegisterId { get; set; }
}

public sealed class NotifyMonatsbelegManagerResult
{
    public bool Ok { get; set; }

    public string Code { get; set; } = "OK";

    public string Message { get; set; } = string.Empty;
}
