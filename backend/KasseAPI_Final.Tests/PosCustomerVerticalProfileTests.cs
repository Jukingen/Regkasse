using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PosCustomerVerticalProfileTests
{
    [Fact]
    public async Task CreateCustomer_VetProfilePayload_PersistsPetData()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Vet Tenant",
            Slug = "vet-tenant",
        });
        await db.SaveChangesAsync();
        var controller = CreateController(db, tenantId);

        var result = await controller.CreateCustomer(
            new CreatePosCustomerRequest
            {
                Name = "Anna Beispiel",
                Email = "anna@example.test",
                PetData = new PosCustomerPetDataDto
                {
                    PetName = "Bello",
                    PetSpecies = "Hund",
                    PetBreed = "Labrador",
                    PetBirthDate = new DateOnly(2021, 5, 3),
                },
            },
            CancellationToken.None);

        var created = Assert.IsType<CreatedResult>(result.Result);
        var dto = Assert.IsType<PosCustomerDto>(created.Value);
        Assert.Equal("Bello", dto.PetData?.PetName);
        var saved = await db.Customers.SingleAsync(customer => customer.Id == dto.Id);
        Assert.Equal(tenantId, saved.TenantId);
        Assert.Equal("Hund", saved.PetData?.PetSpecies);
        Assert.Equal(new DateOnly(2021, 5, 3), saved.PetData?.PetBirthDate);
    }

    [Fact]
    public async Task CreateCustomer_MobileServicesPayload_PersistsAddressData()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Mobile Tenant",
            Slug = "mobile-tenant",
        });
        await db.SaveChangesAsync();
        var controller = CreateController(db, tenantId);

        var result = await controller.CreateCustomer(
            new CreatePosCustomerRequest
            {
                Name = "Lena Mobil",
                Phone = "+436601234",
                AddressData = new PosCustomerAddressDataDto
                {
                    Street = "Feldweg 4",
                    PostalCode = "5020",
                    City = "Salzburg",
                    Notes = "Hinterhof",
                },
            },
            CancellationToken.None);

        var created = Assert.IsType<CreatedResult>(result.Result);
        var dto = Assert.IsType<PosCustomerDto>(created.Value);
        Assert.Equal("Feldweg 4", dto.AddressData?.Street);
        Assert.Equal("Feldweg 4, 5020 Salzburg", dto.Address);
        var saved = await db.Customers.SingleAsync(customer => customer.Id == dto.Id);
        Assert.Equal("5020", saved.AddressData?.PostalCode);
        Assert.Equal("Hinterhof", saved.AddressData?.Notes);
    }

    [Fact]
    public async Task CreateCustomer_WithoutTenant_Returns404()
    {
        await using var db = CreateDb(null);
        var controller = CreateController(db, null);

        var result = await controller.CreateCustomer(
            new CreatePosCustomerRequest { Name = "No Tenant" },
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
        Assert.Empty(db.Customers.IgnoreQueryFilters());
    }

    private static PosCustomerController CreateController(AppDbContext db, Guid? tenantId) =>
        new(
            Mock.Of<IPosCustomerQrLookupService>(),
            db,
            TenantTestDoubles.TenantAccessorReturning(tenantId),
            Mock.Of<ILogger<PosCustomerController>>());

    private static AppDbContext CreateDb(Guid? tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"PosCustomerVertical_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
    }
}
