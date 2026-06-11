using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.DTOs;

public sealed class SplitCartItemsRequest
{
    [Range(1, 99)]
    public int SourceTableNumber { get; set; }

    [Range(1, 99)]
    public int TargetTableNumber { get; set; }

    [Required]
    [MinLength(1)]
    public List<Guid> ItemIds { get; set; } = new();
}

public sealed class MergeTableCartsRequest
{
    [Range(1, 99)]
    public int SourceTableNumber { get; set; }

    [Range(1, 99)]
    public int TargetTableNumber { get; set; }
}

public sealed class CustomerQrLookupRequest
{
    [Required]
    [MaxLength(512)]
    public string QrPayload { get; set; } = string.Empty;
}
