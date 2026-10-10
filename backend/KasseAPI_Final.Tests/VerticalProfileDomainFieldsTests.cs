using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class VerticalProfileDomainFieldsTests
{
    [Fact]
    public void Model_MapsPetDataAndHairSalonProductFields()
    {
        using var db = CreateDb();
        var customer = db.Model.FindEntityType(typeof(Customer))!;
        var product = db.Model.FindEntityType(typeof(Product))!;

        Assert.Equal("pet_data", customer.FindProperty(nameof(Customer.PetData))!.GetColumnName());
        Assert.Equal("address_data", customer.FindProperty(nameof(Customer.AddressData))!.GetColumnName());
        Assert.Equal(
            "jsonb",
            customer.FindProperty(nameof(Customer.PetData))![
                RelationalAnnotationNames.ColumnType]);
        Assert.Equal(
            "jsonb",
            customer.FindProperty(nameof(Customer.AddressData))![
                RelationalAnnotationNames.ColumnType]);
        var order = db.Model.FindEntityType(typeof(Order))!;
        Assert.Equal("location_data", order.FindProperty(nameof(Order.LocationData))!.GetColumnName());
        Assert.Equal(
            "jsonb",
            order.FindProperty(nameof(Order.LocationData))![
                RelationalAnnotationNames.ColumnType]);
        Assert.Equal(
            "duration_minutes",
            product.FindProperty(nameof(Product.DurationMinutes))!.GetColumnName());
        Assert.Equal("staff_id", product.FindProperty(nameof(Product.StaffId))!.GetColumnName());
        Assert.Equal("imei_tracked", product.FindProperty(nameof(Product.ImeiTracked))!.GetColumnName());
        Assert.Equal("is_ticket", product.FindProperty(nameof(Product.IsTicket))!.GetColumnName());
        var tickets = db.Model.FindEntityType(typeof(TicketRedemption))!;
        Assert.Equal("ticket_redemptions", tickets.GetTableName());
        Assert.Equal("ticket_code_hash", tickets.FindProperty(nameof(TicketRedemption.TicketCodeHash))!.GetColumnName());
        var imei = db.Model.FindEntityType(typeof(ProductImei))!;
        Assert.Equal("product_imeis", imei.GetTableName());
        Assert.Equal("imei", imei.FindProperty(nameof(ProductImei.Imei))!.GetColumnName());
        var room = db.Model.FindEntityType(typeof(Room))!;
        Assert.Equal("rooms", room.GetTableName());
        Assert.Equal("number", room.FindProperty(nameof(Room.Number))!.GetColumnName());
        var folio = db.Model.FindEntityType(typeof(GuestFolio))!;
        Assert.Equal("guest_folios", folio.GetTableName());
        Assert.Equal("check_in", folio.FindProperty(nameof(GuestFolio.CheckIn))!.GetColumnName());
        Assert.Equal("balance", folio.FindProperty(nameof(GuestFolio.Balance))!.GetColumnName());
    }

    [Fact]
    public void Seeds_ContainVetAndHairSalonFields_AndProductDtoMapsServiceMetadata()
    {
        var vet = Assert.Single(
            VerticalProfileSeedData.All,
            profile => profile.Id == VerticalProfileIds.Vet);
        var hair = Assert.Single(
            VerticalProfileSeedData.All,
            profile => profile.Id == VerticalProfileIds.HairSalon);
        var mobile = Assert.Single(
            VerticalProfileSeedData.All,
            profile => profile.Id == VerticalProfileIds.MobileServices);
        var lodging = Assert.Single(
            VerticalProfileSeedData.All,
            profile => profile.Id == VerticalProfileIds.Beherbergung);
        var tickets = Assert.Single(
            VerticalProfileSeedData.All,
            profile => profile.Id == VerticalProfileIds.TicketSales);

        using var vetRequired = JsonDocument.Parse(vet.RequiredFields);
        using var vetOptional = JsonDocument.Parse(vet.OptionalFields);
        using var hairRequired = JsonDocument.Parse(hair.RequiredFields);
        using var hairOptional = JsonDocument.Parse(hair.OptionalFields);
        Assert.Contains(
            "petName",
            vetRequired.RootElement.GetProperty("customer").EnumerateArray()
                .Select(value => value.GetString()));
        Assert.Contains(
            "petBirthDate",
            vetOptional.RootElement.GetProperty("customer").EnumerateArray()
                .Select(value => value.GetString()));
        Assert.Contains(
            "durationMinutes",
            hairRequired.RootElement.GetProperty("product").EnumerateArray()
                .Select(value => value.GetString()));
        Assert.Contains(
            "staffId",
            hairOptional.RootElement.GetProperty("product").EnumerateArray()
                .Select(value => value.GetString()));
        using var mobileRequired = JsonDocument.Parse(mobile.RequiredFields);
        using var mobileOptional = JsonDocument.Parse(mobile.OptionalFields);
        Assert.Contains(
            "address",
            mobileRequired.RootElement.GetProperty("customer").EnumerateArray()
                .Select(value => value.GetString()));
        Assert.Contains(
            "street",
            mobileOptional.RootElement.GetProperty("customer").EnumerateArray()
                .Select(value => value.GetString()));
        Assert.Contains(
            "location",
            mobileOptional.RootElement.GetProperty("order").EnumerateArray()
                .Select(value => value.GetString()));
        using var lodgingFeatures = JsonDocument.Parse(lodging.PosFeatures);
        Assert.True(lodgingFeatures.RootElement.GetProperty("roomTracking").GetBoolean());
        Assert.True(lodgingFeatures.RootElement.GetProperty("kitchenDisplay").GetBoolean());
        Assert.False(lodgingFeatures.RootElement.GetProperty("tables").GetBoolean());
        Assert.False(lodgingFeatures.RootElement.GetProperty("appointment").GetBoolean());
        Assert.False(lodgingFeatures.RootElement.GetProperty("patientRecord").GetBoolean());
        Assert.Equal(VerticalProfileLayouts.Rooms, lodging.PosLayout);
        using var ticketFeatures = JsonDocument.Parse(tickets.PosFeatures);
        Assert.False(ticketFeatures.RootElement.GetProperty("roomTracking").GetBoolean());
        Assert.True(ticketFeatures.RootElement.GetProperty("ticketScan").GetBoolean());
        Assert.Equal(VerticalProfileLayouts.Ticket, tickets.PosLayout);

        var dto = AdminProductDto.FromProduct(new Product
        {
            DurationMinutes = 45,
            StaffId = "staff-1",
            ImeiTracked = true,
            IsTicket = true,
        });
        Assert.Equal(45, dto.DurationMinutes);
        Assert.Equal("staff-1", dto.StaffId);
        Assert.True(dto.ImeiTracked);
        Assert.True(dto.IsTicket);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"VerticalDomainFields_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(
            options,
            TenantTestDoubles.TenantAccessorReturning(Guid.NewGuid()));
    }
}
