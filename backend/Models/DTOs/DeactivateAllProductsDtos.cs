namespace KasseAPI_Final.Models.DTOs;

public sealed class DeactivateAllProductsRequest
{
    /// <summary>
    /// Required confirmation phrase to prevent accidental API calls.
    /// </summary>
    public string ConfirmPhrase { get; set; } = string.Empty;
}

public sealed class DeactivateAllProductsResult
{
    public int Deactivated { get; set; }
    public int AlreadyInactive { get; set; }
    public int TotalProducts { get; set; }
}
