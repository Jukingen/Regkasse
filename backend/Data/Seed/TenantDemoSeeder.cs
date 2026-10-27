using System.Text.Json;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Helpers;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace KasseAPI_Final.Data.Seed;

public interface ITenantDemoSeeder
{
    Task<TenantDemoSeedResult> SeedAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public sealed record TenantDemoSeedResult(
    int ProductsAdded,
    int CustomersAdded,
    Guid? CashRegisterId,
    string? FailedProfile = null);

/// <summary>
/// Vertical demo catalog for one tenant. Matches existing rows by category key, barcode,
/// customer number, room number, event name, and IMEI. Does not write receipts, TSE signatures, or payments.
/// A helper failure rolls the seed back and records <see cref="AuditEventType.TenantDemoSeedPartial"/>.
/// </summary>
public sealed class TenantDemoSeeder : ITenantDemoSeeder
{
    public const decimal BreakfastCharge = 12.00m;
    public const string BreakfastDescription = "Frühstück";
    public const string ConcertName = "Konzert A";
    public const string ConcertVenue = "Wiener Konzerthaus";
    public const decimal StandardTicketPrice = 49m;
    public const decimal VipTicketPrice = 89m;
    public const string IphoneBarcode = "DV-handy-shop-iphone";
    public const string HaircutBarcode = "DV-hair-salon-damen";
    public const decimal TaxiRidePrice = 25.00m;
    public const int HaircutDurationMinutes = 60;
    public static readonly string[] DemoImeis = ["356938035643809", "490154203237518", "353918052107564"];

    private readonly AppDbContext _db;
    private readonly ILogger _logger;

    public TenantDemoSeeder(AppDbContext db, ILogger<TenantDemoSeeder>? logger = null)
    {
        _db = db;
        _logger = logger ?? NullLogger<TenantDemoSeeder>.Instance;
    }

    public static string HairWaiterUserName(Guid tenantId, int index) =>
        $"hs-waiter-{index}-{tenantId:N}";

    public Task<TenantDemoSeedResult> SeedAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        SeedTenantAsync(_db, tenantId, _logger, cancellationToken);

    public static Task<TenantDemoSeedResult> SeedTenantAsync(
        AppDbContext db,
        Guid tenantId,
        CancellationToken cancellationToken = default) =>
        SeedTenantAsync(db, tenantId, NullLogger.Instance, cancellationToken);

    private static async Task<TenantDemoSeedResult> SeedTenantAsync(
        AppDbContext db,
        Guid tenantId,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                row => row.Id == tenantId && row.DeletedAtUtc == null,
                cancellationToken)
            .ConfigureAwait(false);
        if (tenant is null)
            return new TenantDemoSeedResult(0, 0, null);

        try
        {
            if (db.Database.IsRelational())
            {
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken)
                    .ConfigureAwait(false);
                try
                {
                    var result = await SeedCoreAsync(db, tenant.Id, cancellationToken).ConfigureAwait(false);
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                    return result;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    await RollbackQuietlyAsync(transaction, logger, cancellationToken).ConfigureAwait(false);
                    return await FailAsync(db, tenant.Id, ex, logger, cancellationToken).ConfigureAwait(false);
                }
            }

            return await SeedCoreAsync(db, tenant.Id, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return await FailAsync(db, tenant.Id, ex, logger, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<TenantDemoSeedResult> SeedCoreAsync(
        AppDbContext db,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await RunProfileAsync("catalog", () => EnsureTaxGroupAsync(db, tenantId, cancellationToken)).ConfigureAwait(false);
        var taxGroupId = await db.TaxGroups
            .IgnoreQueryFilters()
            .Where(row => row.TenantId == tenantId && row.IsActive)
            .OrderByDescending(row => row.IsDefault)
            .Select(row => row.Id)
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);

        var categories = await EnsureCategoriesAsync(db, tenantId, now, cancellationToken).ConfigureAwait(false);
        var productsAdded = await EnsureProductsAsync(db, tenantId, taxGroupId, categories, now, cancellationToken)
            .ConfigureAwait(false);
        var customersAdded = await EnsureCustomersAsync(db, tenantId, now, cancellationToken).ConfigureAwait(false);
        var cashRegisterId = await EnsureOpenRegisterAsync(db, tenantId, now, cancellationToken).ConfigureAwait(false);

        var verticalProducts = 0;
        await RunProfileAsync(VerticalProfileIds.Beherbergung, () => SeedBeherbergungAsync(db, tenantId, now, cancellationToken)).ConfigureAwait(false);
        await RunProfileAsync(VerticalProfileIds.TicketSales, () => SeedTicketSalesAsync(db, tenantId, now, cancellationToken)).ConfigureAwait(false);
        await RunProfileAsync(VerticalProfileIds.HandyShop, async () =>
        {
            verticalProducts += await SeedHandyShopAsync(db, tenantId, taxGroupId, categories, now, cancellationToken)
                .ConfigureAwait(false);
        }).ConfigureAwait(false);
        await RunProfileAsync(VerticalProfileIds.Vet, () => SeedVetAsync(db, tenantId, now, cancellationToken)).ConfigureAwait(false);
        await RunProfileAsync(VerticalProfileIds.Doctor, () => SeedDoctorAsync(db, tenantId, now, cancellationToken)).ConfigureAwait(false);
        await RunProfileAsync(VerticalProfileIds.HairSalon, async () =>
        {
            verticalProducts += await SeedHairSalonAsync(db, tenantId, taxGroupId, categories, now, cancellationToken)
                .ConfigureAwait(false);
        }).ConfigureAwait(false);
        await RunProfileAsync(VerticalProfileIds.GastronomyTables, () => SeedGastronomyTablesAsync(db, cancellationToken)).ConfigureAwait(false);
        await RunProfileAsync(VerticalProfileIds.Taxi, () => SeedTaxiAsync(db, tenantId, now, cancellationToken)).ConfigureAwait(false);
        await RunProfileAsync(VerticalProfileIds.MobileServices, () => SeedMobileServicesAsync(db, tenantId, now, cancellationToken)).ConfigureAwait(false);
        await RunProfileAsync(VerticalProfileIds.Gastronomy, () => SeedGastronomyAsync()).ConfigureAwait(false);
        await RunProfileAsync(VerticalProfileIds.Retail, () => SeedRetailAsync()).ConfigureAwait(false);

        return new TenantDemoSeedResult(productsAdded + verticalProducts, customersAdded, cashRegisterId);
    }

    private static async Task EnsureTaxGroupAsync(AppDbContext db, Guid tenantId, CancellationToken cancellationToken)
    {
        if (await db.TaxGroups.IgnoreQueryFilters().AnyAsync(row => row.TenantId == tenantId, cancellationToken).ConfigureAwait(false))
            return;

        db.TaxGroups.AddRange(TaxGroupSeedData.GetTaxGroups(tenantId));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Dictionary<string, Category>> EnsureCategoriesAsync(
        AppDbContext db,
        Guid tenantId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var existing = await db.Categories
            .IgnoreQueryFilters()
            .Where(row => row.TenantId == tenantId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var byKey = existing.ToDictionary(row => row.Key, StringComparer.Ordinal);

        foreach (var profile in DevTenantSeeder.Catalog)
        {
            if (byKey.ContainsKey(profile.ProfileId))
                continue;

            var category = new Category
            {
                TenantId = tenantId,
                Key = profile.ProfileId,
                Name = profile.CategoryName,
                Description = "Demo-Katalog",
                VatRate = 20m,
                FiscalCategory = profile.FiscalCategory,
                IsSystemCategory = true,
                IsActive = true,
                SortOrder = byKey.Count + 1,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = DevTenantSeeder.CreatedBy,
            };
            db.Categories.Add(category);
            byKey[profile.ProfileId] = category;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return byKey;
    }

    private static async Task<int> EnsureProductsAsync(
        AppDbContext db,
        Guid tenantId,
        Guid taxGroupId,
        IReadOnlyDictionary<string, Category> categories,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var barcodes = await db.Products
            .IgnoreQueryFilters()
            .Where(row => row.TenantId == tenantId && row.Barcode != null)
            .Select(row => row.Barcode!)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var known = barcodes.ToHashSet(StringComparer.Ordinal);
        var added = 0;

        foreach (var profile in DevTenantSeeder.Catalog)
        {
            var category = categories[profile.ProfileId];
            for (var index = 0; index < profile.ProductNames.Count; index++)
            {
                var barcode = DevTenantSeeder.Barcode(profile.ProfileId, index + 1);
                if (!known.Add(barcode))
                    continue;

                db.Products.Add(new Product
                {
                    TenantId = tenantId,
                    Name = profile.ProductNames[index],
                    Description = profile.CategoryName,
                    Price = 9.90m + index,
                    TaxType = (int)TaxType.Standard,
                    TaxRate = 20m,
                    Category = category.Name,
                    CategoryId = category.Id,
                    TaxGroupId = taxGroupId,
                    Barcode = barcode,
                    IsActive = true,
                    StockQuantity = 50,
                    MinStockLevel = 0,
                    Unit = "Stk",
                    Cost = 0,
                    IsFiscalCompliant = true,
                    IsTaxable = true,
                    RksvProductType = RksvProductTypes.Standard,
                    IsSellableAddOn = false,
                    CreatedAt = now,
                    UpdatedAt = now,
                    CreatedBy = DevTenantSeeder.CreatedBy,
                    UpdatedBy = DevTenantSeeder.CreatedBy,
                });
                added++;
            }
        }

        if (added > 0)
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return added;
    }

    private static async Task<int> EnsureCustomersAsync(
        AppDbContext db,
        Guid tenantId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var numbers = await db.Customers
            .IgnoreQueryFilters()
            .Where(row => row.TenantId == tenantId && row.CustomerNumber != "")
            .Select(row => row.CustomerNumber)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var known = numbers.ToHashSet(StringComparer.Ordinal);
        var added = 0;

        foreach (var profile in DevTenantSeeder.Catalog)
        {
            for (var index = 0; index < profile.CustomerNames.Count; index++)
            {
                var number = DevTenantSeeder.CustomerNumber(profile.ProfileId, index + 1);
                if (!known.Add(number))
                    continue;

                db.Customers.Add(new Customer
                {
                    TenantId = tenantId,
                    Name = profile.CustomerNames[index],
                    CustomerNumber = number,
                    Email = "",
                    Phone = "",
                    Address = "",
                    TaxNumber = "",
                    Notes = "",
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                    CreatedBy = DevTenantSeeder.CreatedBy,
                });
                added++;
            }
        }

        if (added > 0)
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return added;
    }

    private static async Task<Guid> EnsureOpenRegisterAsync(
        AppDbContext db,
        Guid tenantId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var registers = await db.CashRegisters
            .IgnoreQueryFilters()
            .Where(row => row.TenantId == tenantId && row.IsActive && row.DecommissionedAtUtc == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var register = registers.FirstOrDefault(row => row.Status == RegisterStatus.Open);
        if (register is null)
        {
            register = registers.FirstOrDefault(row => row.IsDefaultForTenant)
                ?? registers.FirstOrDefault(row => row.Status == RegisterStatus.Closed);
            if (register is null)
            {
                register = new CashRegister
                {
                    TenantId = tenantId,
                    RegisterNumber = DevTenantSeeder.RegisterNumber,
                    Location = "Hauptkasse",
                    StartingBalance = 0,
                    CurrentBalance = 0,
                    LastBalanceUpdate = now,
                    Status = RegisterStatus.Open,
                    IsDefaultForTenant = true,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                    CreatedBy = DevTenantSeeder.CreatedBy,
                };
                db.CashRegisters.Add(register);
            }
            else if (register.Status != RegisterStatus.Decommissioned)
            {
                register.Status = RegisterStatus.Open;
                register.LastBalanceUpdate = now;
                register.UpdatedAt = now;
                register.UpdatedBy = DevTenantSeeder.CreatedBy;
            }
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var hasOpenShift = await db.CashierShifts
            .IgnoreQueryFilters()
            .AnyAsync(
                row => row.CashRegisterId == register.Id
                    && row.Status == CashierShiftStatuses.Active
                    && row.EndedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
        if (!hasOpenShift)
        {
            db.CashierShifts.Add(new CashierShift
            {
                TenantId = tenantId,
                CashRegisterId = register.Id,
                CashierId = DemoTenantAdminUserIds.Dev,
                CashierName = "Demo Kasse",
                StartBalance = register.CurrentBalance,
                StartedAt = now,
                Status = CashierShiftStatuses.Active,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = DevTenantSeeder.CreatedBy,
            });
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return register.Id;
    }

    private static async Task SeedBeherbergungAsync(
        AppDbContext db,
        Guid tenantId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var room101 = await EnsureRoomAsync(db, tenantId, "101", "Deluxe", RoomStatus.Occupied, now, cancellationToken)
            .ConfigureAwait(false);
        await EnsureRoomAsync(db, tenantId, "102", "Standard", RoomStatus.Available, now, cancellationToken)
            .ConfigureAwait(false);
        await EnsureRoomAsync(db, tenantId, "103", "Standard", RoomStatus.Cleaning, now, cancellationToken)
            .ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var guestNumber = DevTenantSeeder.CustomerNumber(VerticalProfileIds.Beherbergung, 1);
        var guestId = await db.Customers
            .IgnoreQueryFilters()
            .Where(row => row.TenantId == tenantId && row.CustomerNumber == guestNumber)
            .Select(row => row.Id)
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);

        var folio = await db.GuestFolios
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                row => row.TenantId == tenantId && row.RoomId == room101.Id,
                cancellationToken)
            .ConfigureAwait(false);
        if (folio is null)
        {
            folio = new GuestFolio
            {
                TenantId = tenantId,
                CustomerId = guestId,
                RoomId = room101.Id,
                CheckIn = now,
                Status = GuestFolioStatus.Open,
                Balance = 0m,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };
            db.GuestFolios.Add(folio);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var hasBreakfast = await db.GuestFolioItems
            .AnyAsync(
                row => row.FolioId == folio.Id && row.Description == BreakfastDescription,
                cancellationToken)
            .ConfigureAwait(false);
        if (!hasBreakfast)
        {
            db.GuestFolioItems.Add(new GuestFolioItem
            {
                FolioId = folio.Id,
                PaymentDetailId = null,
                Description = BreakfastDescription,
                Amount = BreakfastCharge,
                CreatedAtUtc = now,
            });
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var balance = await db.GuestFolioItems
            .Where(row => row.FolioId == folio.Id)
            .SumAsync(row => row.Amount, cancellationToken)
            .ConfigureAwait(false);
        if (folio.Balance != balance)
        {
            folio.Balance = balance;
            folio.UpdatedAtUtc = now;
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<Room> EnsureRoomAsync(
        AppDbContext db,
        Guid tenantId,
        string number,
        string type,
        RoomStatus status,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var room = await db.Rooms
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(row => row.TenantId == tenantId && row.Number == number, cancellationToken)
            .ConfigureAwait(false);
        if (room is not null)
            return room;

        room = new Room
        {
            TenantId = tenantId,
            Number = number,
            Type = type,
            Capacity = 2,
            Status = status,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        db.Rooms.Add(room);
        return room;
    }

    private static async Task SeedTicketSalesAsync(
        AppDbContext db,
        Guid tenantId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var concert = await db.TicketEvents
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                row => row.TenantId == tenantId && row.Name == ConcertName,
                cancellationToken)
            .ConfigureAwait(false);
        if (concert is null)
        {
            var start = now.AddDays(7);
            concert = new TicketEvent
            {
                TenantId = tenantId,
                Name = ConcertName,
                StartAtUtc = start,
                EndAtUtc = start.AddHours(3),
                Venue = ConcertVenue,
                Capacity = 20,
                IsActive = true,
                StageEdge = "top",
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };
            db.TicketEvents.Add(concert);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var standard = await EnsurePriceCategoryAsync(db, tenantId, "standard", "Standard", 0, now, cancellationToken)
            .ConfigureAwait(false);
        var vip = await EnsurePriceCategoryAsync(db, tenantId, "vip", "VIP", 1, now, cancellationToken)
            .ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await EnsureEventPriceAsync(db, concert.Id, standard.Id, StandardTicketPrice, 18, now, cancellationToken)
            .ConfigureAwait(false);
        await EnsureEventPriceAsync(db, concert.Id, vip.Id, VipTicketPrice, 2, now, cancellationToken)
            .ConfigureAwait(false);

        var existingSeats = await db.TicketSeats
            .Where(row => row.EventId == concert.Id)
            .Select(row => row.Row + row.Number)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var knownSeats = existingSeats.ToHashSet(StringComparer.Ordinal);
        foreach (var rowLabel in new[] { "A", "B" })
        {
            for (var number = 1; number <= 10; number++)
            {
                var key = rowLabel + number.ToString();
                if (!knownSeats.Add(key))
                    continue;

                var isVip = rowLabel == "A" && number <= 2;
                db.TicketSeats.Add(new TicketSeat
                {
                    EventId = concert.Id,
                    Row = rowLabel,
                    Number = number.ToString(),
                    Section = "Saal",
                    Status = TicketSeatStatus.Available,
                    CategoryId = isVip ? vip.Id : standard.Id,
                    GridColumn = number,
                    SeatRole = TicketSeatRole.Standard,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now,
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<TicketPriceCategory> EnsurePriceCategoryAsync(
        AppDbContext db,
        Guid tenantId,
        string code,
        string name,
        int sortOrder,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var category = await db.TicketPriceCategories
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(row => row.TenantId == tenantId && row.Code == code, cancellationToken)
            .ConfigureAwait(false);
        if (category is not null)
            return category;

        category = new TicketPriceCategory
        {
            TenantId = tenantId,
            Code = code,
            Name = name,
            SortOrder = sortOrder,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        db.TicketPriceCategories.Add(category);
        return category;
    }

    private static async Task EnsureEventPriceAsync(
        AppDbContext db,
        Guid eventId,
        Guid categoryId,
        decimal price,
        int capacity,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var exists = await db.TicketEventCategoryPrices
            .AnyAsync(row => row.EventId == eventId && row.CategoryId == categoryId, cancellationToken)
            .ConfigureAwait(false);
        if (exists)
            return;

        db.TicketEventCategoryPrices.Add(new TicketEventCategoryPrice
        {
            EventId = eventId,
            CategoryId = categoryId,
            Price = price,
            Capacity = capacity,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        });
    }

    private static async Task<int> SeedHandyShopAsync(
        AppDbContext db,
        Guid tenantId,
        Guid taxGroupId,
        IReadOnlyDictionary<string, Category> categories,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var added = 0;
        var product = await db.Products
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                row => row.TenantId == tenantId && row.Barcode == IphoneBarcode,
                cancellationToken)
            .ConfigureAwait(false);
        if (product is null)
        {
            var category = categories[VerticalProfileIds.HandyShop];
            product = NewDemoProduct(tenantId, taxGroupId, category, "iPhone 15", 899.00m, IphoneBarcode, now);
            product.ImeiTracked = true;
            product.IsTicket = false;
            product.StockQuantity = DemoImeis.Length;
            db.Products.Add(product);
            added = 1;
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        else if (!product.ImeiTracked || product.IsTicket)
        {
            product.ImeiTracked = true;
            product.IsTicket = false;
            product.UpdatedAt = now;
            product.UpdatedBy = DevTenantSeeder.CreatedBy;
        }

        var knownImeis = await db.ProductImeis
            .IgnoreQueryFilters()
            .Where(row => row.TenantId == tenantId)
            .Select(row => row.Imei)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var known = knownImeis.ToHashSet(StringComparer.Ordinal);
        foreach (var imei in DemoImeis)
        {
            if (!known.Add(imei))
                continue;

            db.ProductImeis.Add(new ProductImei
            {
                TenantId = tenantId,
                ProductId = product.Id,
                Imei = imei,
                Status = ProductImeiStatus.InStock,
                SoldPaymentId = null,
                WarrantyMonths = 12,
                CreatedAtUtc = now,
            });
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return added;
    }

    private static async Task SeedVetAsync(
        AppDbContext db,
        Guid tenantId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var customer = await FindProfileCustomerAsync(db, tenantId, VerticalProfileIds.Vet, 1, cancellationToken)
            .ConfigureAwait(false);
        if (customer.PetData?.Species is not null)
            return;

        customer.PetData = new CustomerPetData
        {
            Species = "Hund",
            Breed = "Labrador",
            BirthDate = new DateOnly(2020, 5, 15),
            Vaccines = "Tollwut, Parvovirose",
        };
        customer.PetData.AlignLegacyKeys();
        customer.UpdatedAt = now;
        customer.UpdatedBy = DevTenantSeeder.CreatedBy;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task SeedDoctorAsync(
        AppDbContext db,
        Guid tenantId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var customer = await FindProfileCustomerAsync(db, tenantId, VerticalProfileIds.Doctor, 1, cancellationToken)
            .ConfigureAwait(false);
        if (customer.PetData?.Allergies is not null)
            return;

        customer.PetData = new CustomerPetData
        {
            Allergies = "Penicillin",
            ChronicConditions = "Diabetes Typ 2",
        };
        customer.PetData.AlignLegacyKeys();
        customer.UpdatedAt = now;
        customer.UpdatedBy = DevTenantSeeder.CreatedBy;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> SeedHairSalonAsync(
        AppDbContext db,
        Guid tenantId,
        Guid taxGroupId,
        IReadOnlyDictionary<string, Category> categories,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var added = 0;
        var product = await db.Products
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                row => row.TenantId == tenantId && row.Barcode == HaircutBarcode,
                cancellationToken)
            .ConfigureAwait(false);
        if (product is null)
        {
            var category = categories[VerticalProfileIds.HairSalon];
            product = NewDemoProduct(tenantId, taxGroupId, category, "Haarschnitt Damen", 49.00m, HaircutBarcode, now);
            product.DurationMinutes = HaircutDurationMinutes;
            product.IsTicket = false;
            db.Products.Add(product);
            added = 1;
        }
        else if (product.DurationMinutes is null)
        {
            product.DurationMinutes = HaircutDurationMinutes;
            product.UpdatedAt = now;
            product.UpdatedBy = DevTenantSeeder.CreatedBy;
        }

        await EnsureHairWaiterAsync(db, tenantId, 1, "Mira", "Schnitt", now, cancellationToken).ConfigureAwait(false);
        await EnsureHairWaiterAsync(db, tenantId, 2, "Leo", "Farbe", now, cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return added;
    }

    private static async Task EnsureHairWaiterAsync(
        AppDbContext db,
        Guid tenantId,
        int index,
        string firstName,
        string lastName,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var userName = HairWaiterUserName(tenantId, index);
        var user = await db.Users
            .FirstOrDefaultAsync(row => row.UserName == userName, cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString("D"),
                UserName = userName,
                Email = userName + "@demo.regkasse.invalid",
                FirstName = firstName,
                LastName = lastName,
                EmployeeNumber = $"HS{index}-{tenantId.ToString("N")[..8]}",
                Role = Roles.Waiter,
                Notes = "",
                IsActive = true,
                EmailConfirmed = true,
                AccountType = "demo",
                IsDemo = true,
                CreatedAt = now,
                UpdatedAt = now,
                MustChangePasswordOnNextLogin = true,
            };
            user.NormalizedUserName = userName.ToUpperInvariant();
            user.NormalizedEmail = user.Email.ToUpperInvariant();
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            user.ConcurrencyStamp = Guid.NewGuid().ToString("N");
            user.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(
                user,
                PasswordGenerator.GenerateSecurePassword(16));
            db.Users.Add(user);
        }
        else if (!user.MustChangePasswordOnNextLogin)
        {
            user.MustChangePasswordOnNextLogin = true;
            user.UpdatedAt = now;
        }

        var member = await db.UserTenantMemberships
            .IgnoreQueryFilters()
            .AnyAsync(row => row.UserId == user.Id && row.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (!member)
        {
            db.UserTenantMemberships.Add(new UserTenantMembership
            {
                UserId = user.Id,
                TenantId = tenantId,
                IsActive = true,
                IsOwner = false,
                CreatedAtUtc = now,
            });
        }
    }

    private static Task SeedGastronomyAsync() => Task.CompletedTask;

    private static Task SeedRetailAsync() => Task.CompletedTask;

    /// <summary>
    /// No dining-table catalog exists. <c>table_orders</c> stores orders, not tables, so this profile adds nothing.
    /// </summary>
    private static Task SeedGastronomyTablesAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        _ = db;
        _ = cancellationToken;
        return Task.CompletedTask;
    }

    private static async Task SeedTaxiAsync(
        AppDbContext db,
        Guid tenantId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var barcode = DevTenantSeeder.Barcode(VerticalProfileIds.Taxi, 1);
        var ride = await db.Products
            .IgnoreQueryFilters()
            .FirstAsync(row => row.TenantId == tenantId && row.Barcode == barcode, cancellationToken)
            .ConfigureAwait(false);
        if (ride.Price == TaxiRidePrice)
            return;

        ride.Price = TaxiRidePrice;
        ride.UpdatedAt = now;
        ride.UpdatedBy = DevTenantSeeder.CreatedBy;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task SeedMobileServicesAsync(
        AppDbContext db,
        Guid tenantId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var customer = await FindProfileCustomerAsync(db, tenantId, VerticalProfileIds.MobileServices, 1, cancellationToken)
            .ConfigureAwait(false);
        if (customer.AddressData?.Street is not null)
            return;

        customer.AddressData = new CustomerAddressData
        {
            Street = "Hauptstraße 1",
            PostalCode = "1010",
            City = "Wien",
        };
        customer.UpdatedAt = now;
        customer.UpdatedBy = DevTenantSeeder.CreatedBy;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Customer> FindProfileCustomerAsync(
        AppDbContext db,
        Guid tenantId,
        string profileId,
        int index,
        CancellationToken cancellationToken)
    {
        var number = DevTenantSeeder.CustomerNumber(profileId, index);
        return await db.Customers
            .IgnoreQueryFilters()
            .FirstAsync(row => row.TenantId == tenantId && row.CustomerNumber == number, cancellationToken)
            .ConfigureAwait(false);
    }

    private static Product NewDemoProduct(
        Guid tenantId,
        Guid taxGroupId,
        Category category,
        string name,
        decimal price,
        string barcode,
        DateTime now) => new()
    {
        TenantId = tenantId,
        Name = name,
        Description = category.Name,
        Price = price,
        TaxType = (int)TaxType.Standard,
        TaxRate = 20m,
        Category = category.Name,
        CategoryId = category.Id,
        TaxGroupId = taxGroupId,
        Barcode = barcode,
        IsActive = true,
        StockQuantity = 50,
        MinStockLevel = 0,
        Unit = "Stk",
        Cost = 0,
        IsFiscalCompliant = true,
        IsTaxable = true,
        RksvProductType = RksvProductTypes.Standard,
        IsSellableAddOn = false,
        IsTicket = false,
        CreatedAt = now,
        UpdatedAt = now,
        CreatedBy = DevTenantSeeder.CreatedBy,
        UpdatedBy = DevTenantSeeder.CreatedBy,
    };

    private static async Task RunProfileAsync(string profileId, Func<Task> seed)
    {
        try
        {
            await seed().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new VerticalSeedException(profileId, ex);
        }
    }

    private static async Task<TenantDemoSeedResult> FailAsync(
        AppDbContext db,
        Guid tenantId,
        Exception exception,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var profile = exception is VerticalSeedException vertical
            ? vertical.ProfileId
            : "catalog";
        logger.LogWarning(
            exception,
            "Demo seed rolled back for tenant {TenantId}. Failed profile: {FailedProfile}",
            tenantId,
            profile);
        db.ChangeTracker.Clear();
        try
        {
            db.AuditLogs.Add(new AuditLog
            {
                TenantId = tenantId,
                SessionId = Guid.NewGuid().ToString("N"),
                UserId = DevTenantSeeder.CreatedBy,
                UserRole = "system",
                Action = "TENANT_DEMO_SEED_PARTIAL",
                EntityType = "Tenant",
                EntityId = tenantId,
                ActionType = AuditEventType.TenantDemoSeedPartial,
                NewValues = JsonSerializer.Serialize(new { tenantId, failedProfile = profile }),
                Status = AuditLogStatus.Warning,
                Timestamp = DateTime.UtcNow,
                Description = "Demo catalog seed rolled back",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = DevTenantSeeder.CreatedBy,
            });
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception auditEx) when (auditEx is not OperationCanceledException)
        {
            logger.LogWarning(auditEx, "Demo seed partial audit could not be written for tenant {TenantId}", tenantId);
        }

        return new TenantDemoSeedResult(0, 0, null, profile);
    }

    private static async Task RollbackQuietlyAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Demo seed rollback did not complete");
        }
    }

    private sealed class VerticalSeedException : Exception
    {
        public VerticalSeedException(string profileId, Exception inner)
            : base($"Demo seed failed for profile {profileId}", inner)
        {
            ProfileId = profileId;
        }

        public string ProfileId { get; }
    }
}
