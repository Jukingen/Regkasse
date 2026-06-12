namespace KasseAPI_Final.Models.DTOs;

public sealed class BulkDeactivateProductsRequest
{
    public List<Guid> ProductIds { get; set; } = [];
}

public sealed class BulkDeactivateProductsResult
{
    public int Deactivated { get; set; }
    public int AlreadyInactive { get; set; }
    public int NotFound { get; set; }
}
