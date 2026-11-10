namespace KasseAPI_Final.DTOs;

public sealed class FiskalyTenantCredentialStatusDto
{
    public bool Configured { get; set; }

    public bool IsActive { get; set; }

    public string? ApiKeyHint { get; set; }

    public string? ScuId { get; set; }

    public string? Environment { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

public sealed class UpsertFiskalyTenantCredentialRequest
{
    public string? ApiKey { get; set; }

    public string? ApiSecret { get; set; }

    public string? ScuId { get; set; }

    public string? Environment { get; set; }
}
