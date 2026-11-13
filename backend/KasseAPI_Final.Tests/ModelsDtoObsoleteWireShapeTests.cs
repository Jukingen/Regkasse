using System.Text.Json;
using KasseAPI_Final.Models.DTOs;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Wire-shape guard for Models/DTOs obsolete fields: JSON property names stay camelCase
/// (same naming policy as ApplicationHost) so FA/POS payloads remain unchanged until removal.
/// </summary>
public sealed class ModelsDtoObsoleteWireShapeTests
{
    private static readonly JsonSerializerOptions ApiJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public void CancellationResponse_ObsoleteApprovalId_StillSerializesAsApprovalId()
    {
#pragma warning disable CS0618
        var dto = new CancellationResponse
        {
            Success = false,
            RequiresApproval = true,
            ApprovalId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Message = "pending",
        };
#pragma warning restore CS0618

        var json = JsonSerializer.Serialize(dto, ApiJson);
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("approvalId", out var id));
        Assert.Equal("11111111-1111-1111-1111-111111111111", id.GetString());
        Assert.True(doc.RootElement.GetProperty("requiresApproval").GetBoolean());
    }

    [Fact]
    public void RefundResponse_ObsoleteApprovalId_StillSerializesAsApprovalId()
    {
#pragma warning disable CS0618
        var dto = new RefundResponse
        {
            Success = false,
            RequiresApproval = true,
            ApprovalId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Message = "pending",
        };
#pragma warning restore CS0618

        var json = JsonSerializer.Serialize(dto, ApiJson);
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("approvalId", out var id));
        Assert.Equal("22222222-2222-2222-2222-222222222222", id.GetString());
    }

    [Fact]
    public void PaymentListItemDto_ObsoleteHasVoucherRedemption_StillSerializes()
    {
#pragma warning disable CS0618
        var dto = new PaymentListItemDto
        {
            Id = Guid.NewGuid(),
            VoucherRedeemedAmount = 5m,
            HasVoucherRedemption = true,
        };
#pragma warning restore CS0618

        var json = JsonSerializer.Serialize(dto, ApiJson);
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("hasVoucherRedemption").GetBoolean());
        Assert.Equal(5m, doc.RootElement.GetProperty("voucherRedeemedAmount").GetDecimal());
    }

    [Fact]
    public void TrendDataPoint_ObsoleteWeekNumber_StillSerializes()
    {
#pragma warning disable CS0618
        var dto = new TrendDataPoint
        {
            Date = new DateTime(2026, 7, 20),
            TotalAmount = 10m,
            TransactionCount = 1,
            AverageAmount = 10m,
            WeekNumber = 30,
            Label = "KW 30",
        };
#pragma warning restore CS0618

        var json = JsonSerializer.Serialize(dto, ApiJson);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(30, doc.RootElement.GetProperty("weekNumber").GetInt32());
        Assert.Equal("KW 30", doc.RootElement.GetProperty("label").GetString());
    }

    [Fact]
    public void UserUsernameHistoryDto_ObsoleteChangedByEmail_StillSerializes()
    {
#pragma warning disable CS0618
        var dto = new UserUsernameHistoryDto
        {
            Id = Guid.NewGuid(),
            NewUsername = "manager1",
            ChangedByUserId = "actor-1",
            ChangedByEmail = "actor@example.com",
            ChangedAtUtc = DateTime.UtcNow,
        };
#pragma warning restore CS0618

        var json = JsonSerializer.Serialize(dto, ApiJson);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("actor@example.com", doc.RootElement.GetProperty("changedByEmail").GetString());
        Assert.Equal("actor-1", doc.RootElement.GetProperty("changedByUserId").GetString());
    }

    [Fact]
    public void LoginModel_ObsoleteEmail_StillDeserializes()
    {
#pragma warning disable CS0618
        const string json = """{"email":"legacy@example.com","password":"secret123"}""";
        var model = JsonSerializer.Deserialize<LoginModel>(json, ApiJson);
        Assert.NotNull(model);
        Assert.Equal("legacy@example.com", model.Email);
#pragma warning restore CS0618
        Assert.Equal("secret123", model.Password);
    }
}
