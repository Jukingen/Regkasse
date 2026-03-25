using System.Text.Json;
using KasseAPI_Final.DTOs;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Contract shape tests for payment v2 envelope (serialization stability for OpenAPI / Orval).
/// </summary>
public class PaymentApiContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void PaymentApiEnvelope_RoundTrips_Json()
    {
        var original = new PaymentApiEnvelope<PaymentCreateSuccessData>
        {
            Success = true,
            Message = "ok",
            CorrelationId = "corr-1",
            Data = new PaymentCreateSuccessData
            {
                PaymentId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                InvoicePersisted = true,
                Tse = new PaymentCreateTseData { Provider = "Demo", ReceiptNumber = "AT-X-20260101-1" }
            },
            Idempotency = new PaymentIdempotencyInfo { Replay = true, Key = "k1" }
        };

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var back = JsonSerializer.Deserialize<PaymentApiEnvelope<PaymentCreateSuccessData>>(json, JsonOptions);

        Assert.NotNull(back);
        Assert.True(back!.Success);
        Assert.Equal("payment.v2", back.ApiVersion);
        Assert.Equal("corr-1", back.CorrelationId);
        Assert.NotNull(back.Data);
        Assert.Equal(original.Data!.PaymentId, back.Data.PaymentId);
        Assert.True(back.Idempotency!.Replay);
        Assert.Equal("k1", back.Idempotency.Key);
    }

    [Fact]
    public void PaymentApiErrorBody_Has_Stable_Code_Field()
    {
        var err = new PaymentApiErrorBody
        {
            Code = PaymentApiErrorCodes.Validation,
            Message = "bad",
            CorrelationId = "c",
            FieldErrors = new Dictionary<string, string[]> { ["totalAmount"] = new[] { "required" } }
        };
        var json = JsonSerializer.Serialize(err, JsonOptions);
        Assert.Contains("\"code\":\"PAYMENT_VALIDATION_FAILED\"", json);
        Assert.Contains("correlationId", json);
    }
}
