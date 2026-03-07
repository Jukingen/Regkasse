using System.Text.Json;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Phase 2: Deprecated DTO fields are still accepted and serialized for backward compatibility.
/// </summary>
public class Phase2DtoCompatibilityTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Risk: ModifierGroupDto.Products and Modifiers both serialize/deserialize so clients can still read both.</summary>
    [Fact]
    public void ModifierGroupDto_DeprecatedModifiersField_SerializesAndDeserializes()
    {
        var dto = new ModifierGroupDto
        {
            Id = Guid.NewGuid(),
            Name = "Extras",
            Products = new List<AddOnGroupProductItemDto>
            {
                new() { ProductId = Guid.NewGuid(), ProductName = "Extra Käse", Price = 1.50m, TaxType = 2, SortOrder = 0 }
            },
            Modifiers = new List<ModifierDto>
            {
                new() { Id = Guid.NewGuid(), Name = "Ketchup", Price = 0.30m, TaxType = 2, SortOrder = 0 }
            }
        };
        var json = JsonSerializer.Serialize(dto);
        var roundTrip = JsonSerializer.Deserialize<ModifierGroupDto>(json);
        Assert.NotNull(roundTrip);
        Assert.Single(roundTrip.Products);
        Assert.Equal("Extra Käse", roundTrip.Products[0].ProductName);
        Assert.Single(roundTrip.Modifiers);
        Assert.Equal("Ketchup", roundTrip.Modifiers[0].Name);
    }

    /// <summary>Risk: PaymentItemRequest.ModifierIds and Modifiers still serialize/deserialize so legacy clients can send them.</summary>
    [Fact]
    public void PaymentItemRequest_DeprecatedModifierIdsAndModifiers_SerializesAndDeserializes()
    {
        var modifierId = Guid.NewGuid();
        var dto = new PaymentItemRequest
        {
            ProductId = Guid.NewGuid(),
            Quantity = 1,
            TaxType = TaxType.Reduced,
            ModifierIds = new List<Guid> { modifierId },
            Modifiers = new List<PaymentItemModifierRequest>
            {
                new() { ModifierId = modifierId, PriceDelta = 0.30m }
            }
        };
        var json = JsonSerializer.Serialize(dto);
        var roundTrip = JsonSerializer.Deserialize<PaymentItemRequest>(json);
        Assert.NotNull(roundTrip);
        Assert.Single(roundTrip.ModifierIds);
        Assert.Equal(modifierId, roundTrip.ModifierIds[0]);
        Assert.NotNull(roundTrip.Modifiers);
        Assert.Single(roundTrip.Modifiers);
        Assert.Equal(modifierId, roundTrip.Modifiers![0].ModifierId);
    }

    /// <summary>Risk: AddItemToCartRequest.SelectedModifiers still serializes/deserializes so legacy clients can send them.</summary>
    [Fact]
    public void AddItemToCartRequest_DeprecatedSelectedModifiers_SerializesAndDeserializes()
    {
        var modifierId = Guid.NewGuid();
        var request = new AddItemToCartRequest
        {
            ProductId = Guid.NewGuid(),
            Quantity = 1,
            TableNumber = 1,
            SelectedModifiers = new List<SelectedModifierInputDto>
            {
                new() { Id = modifierId, Quantity = 1 }
            }
        };
        var json = JsonSerializer.Serialize(request);
        var roundTrip = JsonSerializer.Deserialize<AddItemToCartRequest>(json);
        Assert.NotNull(roundTrip);
        Assert.NotNull(roundTrip.SelectedModifiers);
        Assert.Single(roundTrip.SelectedModifiers);
        Assert.Equal(modifierId, roundTrip.SelectedModifiers[0].Id);
    }
}
