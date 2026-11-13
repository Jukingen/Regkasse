namespace KasseAPI_Final.Services.DataAccess;

/// <summary>
/// In-memory / API projection of a GDPR data-access request
/// (persisted as <see cref="Models.TenantDataRightsRequest"/>).
/// </summary>
public sealed class DataAccessRequest
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public DataRequestType Type { get; set; }
    public string Status { get; set; } = "pending";
    public DateTime RequestedAt { get; set; }
    public Guid? RequestedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? Reason { get; set; }
}
