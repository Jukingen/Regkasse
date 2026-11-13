namespace KasseAPI_Final.Tse.Fiskaly;

/// <summary>
/// Development credential probe against fiskaly SIGN AT (Austria RKSV).
/// TSS (SIGN DE) maps to SCU; "client" maps to cash-register.
/// </summary>
public sealed class FiskalyConnectionProbeRequest
{
    /// <summary>When true, also PUT an SCU and a cash register (CREATED only — not FON-initialized).</summary>
    public bool CreateResources { get; init; } = true;

    /// <summary>Austrian VAT id (<c>ATU########</c>) required to create an SCU.</summary>
    public string? VatId { get; init; }
}

public sealed class FiskalyConnectionProbeResult
{
    public bool Success { get; init; }

    public FiskalyConnectionStepResult Authentication { get; init; } = new();

    public FiskalyConnectionStepResult ScuCreation { get; init; } = new();

    public FiskalyConnectionStepResult CashRegisterCreation { get; init; } = new();

    public string? ScuId { get; init; }

    public string? CashRegisterId { get; init; }

    public string? VatIdUsed { get; init; }

    public string ApiBaseUrl { get; init; } = string.Empty;
}

public sealed class FiskalyConnectionStepResult
{
    public string Name { get; init; } = string.Empty;

    /// <summary>Succeeded, Failed, or Skipped.</summary>
    public string Status { get; init; } = "Skipped";

    public bool Success => string.Equals(Status, "Succeeded", StringComparison.OrdinalIgnoreCase);

    public int? HttpStatus { get; init; }

    public string Message { get; init; } = string.Empty;
}
