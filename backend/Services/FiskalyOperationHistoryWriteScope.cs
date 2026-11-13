namespace KasseAPI_Final.Services;

/// <summary>Request-scoped context for Fiskaly history writes (retry parent + current row).</summary>
public sealed class FiskalyOperationHistoryWriteScope
{
    public Guid? CurrentHistoryId { get; set; }

    public Guid? RetriedFromId { get; set; }
}
