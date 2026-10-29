namespace KasseAPI_Final.DTOs;

/// <summary>
/// Inbound body for the placeholder webhook. Field names are Regkasse's contract,
/// pending Deliverect confirmation. A tenant id in the JSON is ignored.
/// </summary>
public sealed class OnlineOrderIntegrationWebhookBody
{
    public string? ExternalOrderId { get; set; }

    public string? ExternalLocationId { get; set; }

    public string? ExternalChannel { get; set; }

    public decimal ChannelTotal { get; set; }

    public string? CustomerName { get; set; }

    public string? CustomerPhone { get; set; }

    public string? CustomerEmail { get; set; }

    public string? OrderType { get; set; }

    public string? DeliveryAddress { get; set; }

    public string? Notes { get; set; }

    public decimal DeliveryFee { get; set; }

    public decimal ServiceFee { get; set; }

    public List<OnlineOrderIntegrationWebhookItem>? Items { get; set; }
}

public sealed class OnlineOrderIntegrationWebhookItem
{
    /// <summary>Filled by the processor after a product mapping hit. The webhook body is not the source.</summary>
    public Guid ProductId { get; set; }

    public string? ExternalProductId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public string? Name { get; set; }
}

public sealed class UpsertOnlineOrderProductMappingRequest
{
    public string? Provider { get; set; }

    public string? ExternalProductId { get; set; }

    public Guid ProductId { get; set; }
}

public sealed class OnlineOrderProductMappingDto
{
    public Guid Id { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string ExternalProductId { get; set; } = string.Empty;

    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

public sealed class OnlineOrderProductMappingListDto
{
    public List<OnlineOrderProductMappingDto> Items { get; set; } = new();
}

public sealed class UpsertOnlineOrderIntegrationLocationRequest
{
    public string? Provider { get; set; }

    public string? ExternalLocationId { get; set; }

    public string? ClaimingUserId { get; set; }

    public bool Enabled { get; set; }

    public Guid? DeliveryFeeProductId { get; set; }

    public Guid? ServiceFeeProductId { get; set; }
}

public sealed class OnlineOrderIntegrationCommandResult
{
    public bool Succeeded { get; init; }

    public int StatusCode { get; init; }

    public string? Code { get; init; }

    public string? Error { get; init; }

    public Guid? Id { get; init; }

    public static OnlineOrderIntegrationCommandResult Ok(int statusCode, Guid? id = null) =>
        new() { Succeeded = true, StatusCode = statusCode, Id = id };

    public static OnlineOrderIntegrationCommandResult Fail(int statusCode, string code, string error) =>
        new() { Succeeded = false, StatusCode = statusCode, Code = code, Error = error };
}
