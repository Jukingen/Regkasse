using System.Text.Json;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Data.Seed;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

internal static class DemoVerticalSeedAssertions
{
    public static async Task AssertAsync(AppDbContext db, Guid tenantId)
    {
        await AssertBeherbergungAsync(db, tenantId);
        await AssertTicketSalesAsync(db, tenantId);
        await AssertHandyShopAsync(db, tenantId);
        await AssertClinicalAsync(db, tenantId);
        await AssertHairSalonAsync(db, tenantId);
        await AssertTaxiAsync(db, tenantId);
        await AssertMobileServicesAsync(db, tenantId);
        Assert.Equal(0, await db.TableOrders.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.PaymentDetails.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.Receipts.IgnoreQueryFilters().CountAsync(row => row.TenantId == tenantId));
        Assert.Equal(0, await db.Invoices.IgnoreQueryFilters().CountAsync(row => row.TenantId == tenantId));
    }

    private static async Task AssertBeherbergungAsync(AppDbContext db, Guid tenantId)
    {
        var rooms = await db.Rooms.IgnoreQueryFilters()
            .Where(row => row.TenantId == tenantId)
            .ToListAsync();
        Assert.Equal(3, rooms.Count);
        var room101 = Assert.Single(rooms, row => row.Number == "101");
        Assert.Equal(RoomStatus.Occupied, room101.Status);
        Assert.Equal("Deluxe", room101.Type);
        var room102 = Assert.Single(rooms, row => row.Number == "102");
        Assert.Equal(RoomStatus.Available, room102.Status);
        Assert.Equal("Standard", room102.Type);
        var room103 = Assert.Single(rooms, row => row.Number == "103");
        Assert.Equal(RoomStatus.Cleaning, room103.Status);
        Assert.Equal("Standard", room103.Type);

        var room101Id = room101.Id;
        var folio = await db.GuestFolios.IgnoreQueryFilters()
            .SingleAsync(row => row.TenantId == tenantId && row.RoomId == room101Id);
        var items = await db.GuestFolioItems.Where(row => row.FolioId == folio.Id).ToListAsync();
        Assert.Single(items);
        Assert.Equal(TenantDemoSeeder.BreakfastDescription, items[0].Description);
        Assert.Equal(TenantDemoSeeder.BreakfastCharge, items[0].Amount);
        Assert.Null(items[0].PaymentDetailId);
        Assert.Equal(items.Sum(row => row.Amount), folio.Balance);
    }

    private static async Task AssertTicketSalesAsync(AppDbContext db, Guid tenantId)
    {
        var concert = await db.TicketEvents.IgnoreQueryFilters()
            .SingleAsync(row => row.TenantId == tenantId && row.Name == TenantDemoSeeder.ConcertName);
        Assert.Equal(TenantDemoSeeder.ConcertVenue, concert.Venue);
        Assert.InRange((concert.StartAtUtc - DateTime.UtcNow).TotalDays, 6.5, 7.5);
        var seats = await db.TicketSeats.Where(row => row.EventId == concert.Id).ToListAsync();
        Assert.Equal(20, seats.Count);
        var vip = await db.TicketPriceCategories.IgnoreQueryFilters()
            .SingleAsync(row => row.TenantId == tenantId && row.Code == "vip");
        var standard = await db.TicketPriceCategories.IgnoreQueryFilters()
            .SingleAsync(row => row.TenantId == tenantId && row.Code == "standard");
        Assert.Equal(2, seats.Count(row => row.CategoryId == vip.Id));
        Assert.Equal(18, seats.Count(row => row.CategoryId == standard.Id));
        var standardPrice = await db.TicketEventCategoryPrices
            .SingleAsync(row => row.EventId == concert.Id && row.CategoryId == standard.Id);
        Assert.Equal(TenantDemoSeeder.StandardTicketPrice, standardPrice.Price);
        Assert.Equal(18, standardPrice.Capacity);
        var vipPrice = await db.TicketEventCategoryPrices
            .SingleAsync(row => row.EventId == concert.Id && row.CategoryId == vip.Id);
        Assert.Equal(TenantDemoSeeder.VipTicketPrice, vipPrice.Price);
        Assert.Equal(2, vipPrice.Capacity);
    }

    private static async Task AssertHandyShopAsync(AppDbContext db, Guid tenantId)
    {
        var phone = await db.Products.IgnoreQueryFilters()
            .SingleAsync(row => row.TenantId == tenantId && row.Barcode == TenantDemoSeeder.IphoneBarcode);
        Assert.Equal("iPhone 15", phone.Name);
        Assert.False(phone.IsTicket);
        Assert.True(phone.ImeiTracked);
        var imeis = await db.ProductImeis.IgnoreQueryFilters()
            .Where(row => row.TenantId == tenantId && row.ProductId == phone.Id)
            .ToListAsync();
        Assert.Equal(3, imeis.Count);
        Assert.All(imeis, row => Assert.Equal(ProductImeiStatus.InStock, row.Status));
        Assert.All(imeis, row => Assert.Null(row.SoldPaymentId));
    }

    private static async Task AssertClinicalAsync(AppDbContext db, Guid tenantId)
    {
        var vet = await db.Customers.IgnoreQueryFilters().SingleAsync(row =>
            row.TenantId == tenantId && row.CustomerNumber == DevTenantSeeder.CustomerNumber(VerticalProfileIds.Vet, 1));
        var vetJson = CustomerClinicalDataJson.Serialize(vet.PetData);
        Assert.NotNull(vetJson);
        using (var vetDoc = JsonDocument.Parse(vetJson))
        {
            Assert.Equal("Hund", vetDoc.RootElement.GetProperty("Species").GetString());
            Assert.Equal("Labrador", vetDoc.RootElement.GetProperty("Breed").GetString());
            Assert.Equal("2020-05-15", vetDoc.RootElement.GetProperty("BirthDate").GetString());
            var vaccines = vetDoc.RootElement.GetProperty("Vaccines").GetString();
            Assert.Contains("Tollwut", vaccines);
            Assert.Contains("Parvovirose", vaccines);
        }

        var doctor = await db.Customers.IgnoreQueryFilters().SingleAsync(row =>
            row.TenantId == tenantId && row.CustomerNumber == DevTenantSeeder.CustomerNumber(VerticalProfileIds.Doctor, 1));
        var doctorJson = CustomerClinicalDataJson.Serialize(doctor.PetData);
        Assert.NotNull(doctorJson);
        using var doctorDoc = JsonDocument.Parse(doctorJson);
        Assert.Equal("Penicillin", doctorDoc.RootElement.GetProperty("Allergies").GetString());
        Assert.Equal("Diabetes Typ 2", doctorDoc.RootElement.GetProperty("ChronicConditions").GetString());
    }

    private static async Task AssertHairSalonAsync(AppDbContext db, Guid tenantId)
    {
        var cut = await db.Products.IgnoreQueryFilters()
            .SingleAsync(row => row.TenantId == tenantId && row.Barcode == TenantDemoSeeder.HaircutBarcode);
        Assert.Equal("Haarschnitt Damen", cut.Name);
        Assert.Equal(TenantDemoSeeder.HaircutDurationMinutes, cut.DurationMinutes);

        var names = new[]
        {
            TenantDemoSeeder.HairWaiterUserName(tenantId, 1),
            TenantDemoSeeder.HairWaiterUserName(tenantId, 2),
        };
        var users = await db.Users.Where(row => names.Contains(row.UserName)).ToListAsync();
        Assert.Equal(2, users.Count);
        Assert.All(users, user =>
        {
            Assert.Equal(Roles.Waiter, user.Role);
            Assert.True(user.MustChangePasswordOnNextLogin);
            Assert.False(string.IsNullOrWhiteSpace(user.PasswordHash));
        });
    }

    private static async Task AssertTaxiAsync(AppDbContext db, Guid tenantId)
    {
        var ride = await db.Products.IgnoreQueryFilters().SingleAsync(row =>
            row.TenantId == tenantId && row.Barcode == DevTenantSeeder.Barcode(VerticalProfileIds.Taxi, 1));
        Assert.Equal("Stadtfahrt", ride.Name);
        Assert.Equal(TenantDemoSeeder.TaxiRidePrice, ride.Price);
    }

    private static async Task AssertMobileServicesAsync(AppDbContext db, Guid tenantId)
    {
        var customer = await db.Customers.IgnoreQueryFilters().SingleAsync(row =>
            row.TenantId == tenantId
            && row.CustomerNumber == DevTenantSeeder.CustomerNumber(VerticalProfileIds.MobileServices, 1));
        Assert.NotNull(customer.AddressData);
        var json = JsonSerializer.Serialize(customer.AddressData);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("Hauptstraße 1", doc.RootElement.GetProperty("Street").GetString());
        Assert.Equal("1010", doc.RootElement.GetProperty("PostalCode").GetString());
        Assert.Equal("Wien", doc.RootElement.GetProperty("City").GetString());
    }
}
