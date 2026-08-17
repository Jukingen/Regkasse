namespace KasseAPI_Final.DTOs;

/// <summary>Dashboard / health snapshot for the fiskaly SIGN AT integration (no secrets).</summary>
public sealed class FiskalyStatusDto
{
    public bool IsEnabled { get; set; }

    public bool IsConfigured { get; set; }

    public string Environment { get; set; } = "TEST";

    public bool IsAuthenticated { get; set; }

    public DateTime? LastCheck { get; set; }

    public string? Error { get; set; }

    /// <summary><c>config</c>, <c>tenant_override</c>, or <c>global_override</c>.</summary>
    public string Source { get; set; } = "config";

    public string? ScuId { get; set; }

    public string? ScuState { get; set; }

    public bool ScuInitialized { get; set; }

    public bool CashRegisterInitialized { get; set; }
}

public sealed class FiskalySettingsDto
{
    public bool Enabled { get; set; }

    public bool ConfigEnabled { get; set; }

    public bool? OverrideEnabled { get; set; }

    public string Environment { get; set; } = "TEST";

    public bool IsConfigured { get; set; }

    public string ApiBaseUrl { get; set; } = string.Empty;

    public string Source { get; set; } = "config";
}

public sealed class UpdateFiskalySettingsRequest
{
    public bool Enabled { get; set; }
}

public sealed class AuthenticateFonRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MinLength(8)]
    [System.ComponentModel.DataAnnotations.MaxLength(12)]
    [System.ComponentModel.DataAnnotations.RegularExpression("^[0-9A-Za-z]{8,12}$")]
    public string FonParticipantId { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MinLength(5)]
    [System.ComponentModel.DataAnnotations.MaxLength(12)]
    public string FonUserId { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MinLength(5)]
    [System.ComponentModel.DataAnnotations.MaxLength(128)]
    public string FonUserPin { get; set; } = string.Empty;
}

public sealed class FiskalyFonAuthDto
{
    public bool Authenticated { get; set; }

    public string AuthenticationStatus { get; set; } = "UNKNOWN";

    public string? ParticipantId { get; set; }

    public string? UserId { get; set; }

    public DateTimeOffset? AuthenticatedAt { get; set; }

    public string? Error { get; set; }
}

public sealed class FiskalyScuSetupDto
{
    public string? ScuId { get; set; }

    public string State { get; set; } = "UNKNOWN";

    public DateTimeOffset? InitializedAt { get; set; }
}

public sealed class FiskalyCashRegisterSetupDto
{
    public Guid CashRegisterId { get; set; }

    public string? RegisterNumber { get; set; }

    public string? Location { get; set; }

    public string State { get; set; } = "UNKNOWN";

    public DateTimeOffset? InitializedAt { get; set; }
}

public sealed class FiskalySetupStatusDto
{
    public bool Enabled { get; set; }

    public bool IsConfigured { get; set; }

    public string Environment { get; set; } = "TEST";

    public FiskalyFonAuthDto Fon { get; set; } = new();

    public FiskalyScuSetupDto Scu { get; set; } = new();

    public IReadOnlyList<FiskalyCashRegisterSetupDto> CashRegisters { get; set; } =
        Array.Empty<FiskalyCashRegisterSetupDto>();
}

public sealed class FiskalySignTestRequest
{
    public Guid CashRegisterId { get; set; }

    /// <summary>normal, cancellation, training, mixed_vat, zero_amount.</summary>
    public string Scenario { get; set; } = FiskalySignTestScenarioIds.Normal;
}

public sealed class FiskalyVerifyTestRequest
{
    public Guid CashRegisterId { get; set; }

    /// <summary>fiskaly receipt UUID or sequential receipt number.</summary>
    public string ReceiptId { get; set; } = string.Empty;
}

public static class FiskalySignTestScenarioIds
{
    public const string Normal = "normal";
    public const string Cancellation = "cancellation";
    public const string Training = "training";
    public const string MixedVat = "mixed_vat";
    public const string ZeroAmount = "zero_amount";
    public const string MonthlyClose = "monthly_close";
    public const string YearlyClose = "yearly_close";
}

public sealed class FiskalySignTestScenarioDto
{
    public string Id { get; init; } = string.Empty;

    public string ReceiptType { get; init; } = "NORMAL";

    public bool CanSign { get; init; } = true;

    public string Description { get; init; } = string.Empty;

    public IReadOnlyList<FiskalySignTestVatRowDto> Amounts { get; init; } =
        Array.Empty<FiskalySignTestVatRowDto>();
}

public sealed class FiskalySignTestVatRowDto
{
    public string VatRate { get; init; } = "STANDARD";

    public string Amount { get; init; } = "0.00";
}

public sealed class FiskalyQrValidationDto
{
    public bool IsValid { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public string? Prefix { get; init; }

    public string? CashRegisterSerial { get; init; }

    public string? ReceiptNumber { get; init; }

    public string? Timestamp { get; init; }
}

public sealed class FiskalyReceiptChecksDto
{
    public bool QrFormatValid { get; init; }

    public bool HasReceiptNumber { get; init; }

    public bool ReceiptNumberLooksSequential { get; init; }

    public bool HasTimeSignature { get; init; }

    public bool HasCashRegisterSerial { get; init; }

    public bool Signed { get; init; }
}

public sealed class FiskalySignTestResultDto
{
    public bool Success { get; init; }

    public string Scenario { get; init; } = string.Empty;

    public string ReceiptId { get; init; } = string.Empty;

    public string? ReceiptNumber { get; init; }

    public string? QrCodeData { get; init; }

    public long? TimeSignature { get; init; }

    public bool Signed { get; init; }

    public IReadOnlyList<string>? Hints { get; init; }

    public string? CashRegisterSerial { get; init; }

    public string? ReceiptType { get; init; }

    public string? Environment { get; init; }

    public string? FonValidationsJson { get; init; }

    public FiskalyQrValidationDto QrValidation { get; init; } = new();

    public FiskalyReceiptChecksDto Checks { get; init; } = new();
}

public sealed class FiskalyVerifyTestResultDto
{
    public string ReceiptId { get; init; } = string.Empty;

    public string? ReceiptNumber { get; init; }

    public string? QrCodeData { get; init; }

    public long? TimeSignature { get; init; }

    public bool Signed { get; init; }

    public IReadOnlyList<string>? Hints { get; init; }

    public string? CashRegisterSerial { get; init; }

    public string? ReceiptType { get; init; }

    public string? Environment { get; init; }

    public string? FonValidationsJson { get; init; }

    public FiskalyQrValidationDto QrValidation { get; init; } = new();

    public FiskalyReceiptChecksDto Checks { get; init; } = new();
}
