using System.Globalization;
using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using CsvHelper;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TenantDataExportServiceTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task Csv_WritesHeadersAndOnlyTheAmbientTenant()
    {
        await using var db = await SeedAsync();
        var csv = await ExportCsvAsync(db, includeAllTenants: false);

        AssertHeadersAndCount(csv["products"], TenantDataExportHeaders.Products, 2);
        Assert.Contains(csv["products"].Rows, row => row[2] == "Alpha");
        Assert.DoesNotContain(csv["products"].Rows, row => row[2] == "Other");

        AssertHeadersAndCount(csv["customers"], TenantDataExportHeaders.Customers, 1);
        Assert.Equal("Ada", csv["customers"].Rows[0][2]);

        AssertHeadersAndCount(csv["receipts"], TenantDataExportHeaders.Receipts, 1);
        Assert.Equal("R-A", csv["receipts"].Rows[0][2]);

        AssertHeadersAndCount(csv["payments"], TenantDataExportHeaders.Payments, 1);
        Assert.Equal("Cash", csv["payments"].Rows[0][6]);
        Assert.Equal("Guest A", csv["payments"].Rows[0][3]);

        AssertHeadersAndCount(csv["users"], TenantDataExportHeaders.Users, 1);
        Assert.Equal("ada@a.test", csv["users"].Rows[0][2]);
        Assert.DoesNotContain(csv["users"].Headers, header => header.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Csv_NewTypes_KeepTheAmbientTenantAndOmitSecrets()
    {
        await using var db = await SeedAsync();
        var csv = await ExportCsvAsync(db, includeAllTenants: false, TenantDataExportHeaders.ExtendedTypes);

        AssertHeadersAndCount(csv["orders"], TenantDataExportHeaders.Orders, 1);
        Assert.Equal("pending", csv["orders"].Rows[0][1]);
        Assert.Equal("Ada Guest", csv["orders"].Rows[0][3]);
        Assert.DoesNotContain(csv["orders"].Rows, row => row[3] == "Bea Guest");

        AssertHeadersAndCount(csv["audit_logs"], TenantDataExportHeaders.AuditLogs, 1);
        Assert.Equal("UPDATE", csv["audit_logs"].Rows[0][0]);
        Assert.Equal("Customer", csv["audit_logs"].Rows[0][1]);
        Assert.Equal("user-a", csv["audit_logs"].Rows[0][2]);
        Assert.DoesNotContain(csv["audit_logs"].Rows.SelectMany(row => row), cell => cell.Contains("do-not-export", StringComparison.Ordinal));

        AssertHeadersAndCount(csv["inventory"], TenantDataExportHeaders.Inventory, 2);
        Assert.Equal("Alpha", csv["inventory"].Rows[0][0]);
        Assert.Equal("4", csv["inventory"].Rows[0][1]);
        Assert.Equal("2", csv["inventory"].Rows[0][2]);
        Assert.DoesNotContain(csv["inventory"].Rows, row => row[0] == "Other");

        AssertHeadersAndCount(csv["tse_signatures"], TenantDataExportHeaders.TseSignatures, 1);
        Assert.Equal("R-A", csv["tse_signatures"].Rows[0][0]);
        Assert.Equal("11111111-1111-1111-1111-111111111111", csv["tse_signatures"].Rows[0][2]);
        Assert.DoesNotContain(csv["tse_signatures"].Rows.SelectMany(row => row), cell => cell.Contains("jws-secret", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Csv_NewTypes_IncludeAllTenants_ReturnsEveryMandant()
    {
        await using var db = await SeedAsync();
        var csv = await ExportCsvAsync(db, includeAllTenants: true, TenantDataExportHeaders.ExtendedTypes);

        Assert.Equal(2, csv["orders"].Rows.Count);
        Assert.Equal(2, csv["audit_logs"].Rows.Count);
        Assert.Equal(3, csv["inventory"].Rows.Count);
        Assert.Equal(2, csv["tse_signatures"].Rows.Count);
        Assert.Contains(csv["orders"].Rows, row => row[3] == "Bea Guest");
    }

    [Fact]
    public async Task Csv_NewTypes_StartWithUtf8Bom()
    {
        await using var db = await SeedAsync();
        var service = new TenantDataExportService(db, NullLogger<TenantDataExportService>.Instance);
        foreach (var type in TenantDataExportHeaders.ExtendedTypes)
        {
            using var ms = new MemoryStream();
            await service.WriteAsync(ms, type, "csv", includeAllTenants: false);
            var bytes = ms.ToArray();
            Assert.True(bytes.Length >= 3, type);
            Assert.Equal(0xEF, bytes[0]);
            Assert.Equal(0xBB, bytes[1]);
            Assert.Equal(0xBF, bytes[2]);
        }
    }

    [Fact]
    public async Task Xlsx_StreamsOrderRowsWithoutBufferingASecondTenant()
    {
        await using var db = await SeedAsync();
        var service = new TenantDataExportService(db, NullLogger<TenantDataExportService>.Instance);
        using var ms = new MemoryStream();
        await service.WriteAsync(ms, "orders", "xlsx", includeAllTenants: false);

        ms.Position = 0;
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        var sheet = zip.GetEntry("xl/worksheets/sheet1.xml");
        Assert.NotNull(sheet);
        using var reader = new StreamReader(sheet.Open());
        var xml = await reader.ReadToEndAsync();
        Assert.Contains(">CustomerName<", xml);
        Assert.Contains(">Ada Guest<", xml);
        Assert.DoesNotContain(">Bea Guest<", xml);
    }

    [Fact]
    public async Task Csv_RecipesSuppliersAndStockMovements_KeepTheAmbientTenant()
    {
        await using var db = await SeedAsync();
        var csv = await ExportCsvAsync(db, includeAllTenants: false, TenantDataExportHeaders.CatalogOpsTypes);

        AssertHeadersAndCount(csv["recipes"], TenantDataExportHeaders.Recipes, 1);
        Assert.Equal("Alpha", csv["recipes"].Rows[0][0]);
        Assert.Equal("Milk", csv["recipes"].Rows[0][1]);
        Assert.Equal("0.2", csv["recipes"].Rows[0][2]);
        Assert.DoesNotContain(csv["recipes"].Rows, row => row[1] == "Sugar");

        AssertHeadersAndCount(csv["suppliers"], TenantDataExportHeaders.Suppliers, 1);
        Assert.Equal("Mill", csv["suppliers"].Rows[0][0]);
        Assert.Equal("mill@a.test", csv["suppliers"].Rows[0][1]);
        Assert.Equal("111", csv["suppliers"].Rows[0][2]);
        Assert.Equal("Wien", csv["suppliers"].Rows[0][3]);
        Assert.DoesNotContain(csv["suppliers"].Rows, row => row[0] == "Other Mill");

        AssertHeadersAndCount(csv["stock_movements"], TenantDataExportHeaders.StockMovements, 1);
        Assert.Equal("Alpha", csv["stock_movements"].Rows[0][0]);
        Assert.Equal("3", csv["stock_movements"].Rows[0][1]);
        Assert.Equal("delivery", csv["stock_movements"].Rows[0][2]);
        Assert.DoesNotContain(csv["stock_movements"].Rows, row => row[2] == "other-delivery");
    }

    [Fact]
    public async Task Csv_RecipesSuppliersAndStockMovements_IncludeAllTenants()
    {
        await using var db = await SeedAsync();
        var csv = await ExportCsvAsync(db, includeAllTenants: true, TenantDataExportHeaders.CatalogOpsTypes);

        Assert.Equal(2, csv["recipes"].Rows.Count);
        Assert.Equal(2, csv["suppliers"].Rows.Count);
        Assert.Equal(2, csv["stock_movements"].Rows.Count);
        Assert.Contains(csv["recipes"].Rows, row => row[1] == "Sugar");
        Assert.Contains(csv["suppliers"].Rows, row => row[0] == "Other Mill");
        Assert.Contains(csv["stock_movements"].Rows, row => row[2] == "other-delivery");
    }

    [Fact]
    public async Task Csv_RecipesSuppliersAndStockMovements_StartWithUtf8Bom()
    {
        await using var db = await SeedAsync();
        var service = new TenantDataExportService(db, NullLogger<TenantDataExportService>.Instance);
        foreach (var type in TenantDataExportHeaders.CatalogOpsTypes)
        {
            using var ms = new MemoryStream();
            await service.WriteAsync(ms, type, "csv", includeAllTenants: false);
            var bytes = ms.ToArray();
            Assert.True(bytes.Length >= 3, type);
            Assert.Equal(0xEF, bytes[0]);
            Assert.Equal(0xBB, bytes[1]);
            Assert.Equal(0xBF, bytes[2]);
        }
    }

    [Fact]
    public async Task Xlsx_StreamsRecipeRowsWithoutTheOtherTenant()
    {
        await using var db = await SeedAsync();
        var service = new TenantDataExportService(db, NullLogger<TenantDataExportService>.Instance);
        using var ms = new MemoryStream();
        await service.WriteAsync(ms, "recipes", "xlsx", includeAllTenants: false);

        ms.Position = 0;
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        var sheet = zip.GetEntry("xl/worksheets/sheet1.xml");
        Assert.NotNull(sheet);
        using var reader = new StreamReader(sheet.Open());
        var xml = await reader.ReadToEndAsync();
        Assert.Contains(">Ingredient<", xml);
        Assert.Contains(">Milk<", xml);
        Assert.DoesNotContain(">Sugar<", xml);
    }

    [Fact]
    public async Task Csv_IncludeAllTenants_ReturnsEveryMandant()
    {
        await using var db = await SeedAsync();
        var csv = await ExportCsvAsync(db, includeAllTenants: true);

        Assert.Equal(3, csv["products"].Rows.Count);
        Assert.Equal(3, csv["customers"].Rows.Count);
        Assert.Equal(2, csv["receipts"].Rows.Count);
        Assert.Equal(2, csv["payments"].Rows.Count);
        Assert.Equal(2, csv["users"].Rows.Count);
        Assert.Contains(csv["products"].Rows, row => row[2] == "Other");
        Assert.Contains(csv["customers"].Rows, row => row[2] == "WalkIn");
    }

    [Fact]
    public async Task Xlsx_StreamsTheProductHeaderAndRows()
    {
        await using var db = await SeedAsync();
        var service = new TenantDataExportService(db, NullLogger<TenantDataExportService>.Instance);
        using var ms = new MemoryStream();
        await service.WriteAsync(ms, "products", "xlsx", includeAllTenants: false);

        ms.Position = 0;
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        var sheet = zip.GetEntry("xl/worksheets/sheet1.xml");
        Assert.NotNull(sheet);
        using var reader = new StreamReader(sheet.Open());
        var xml = await reader.ReadToEndAsync();
        Assert.Contains(">Name<", xml);
        Assert.Contains(">Alpha<", xml);
        Assert.DoesNotContain(">Other<", xml);
    }

    private static async Task<Dictionary<string, CsvTable>> ExportCsvAsync(
        AppDbContext db,
        bool includeAllTenants,
        IReadOnlyList<string>? types = null)
    {
        var service = new TenantDataExportService(db, NullLogger<TenantDataExportService>.Instance);
        var result = new Dictionary<string, CsvTable>(StringComparer.Ordinal);
        foreach (var type in types ?? TenantDataExportHeaders.OriginalTypes)
        {
            using var ms = new MemoryStream();
            await service.WriteAsync(ms, type, "csv", includeAllTenants);
            result[type] = Parse(ms.ToArray());
        }

        return result;
    }

    private static void AssertHeadersAndCount(CsvTable table, string[] expectedHeaders, int expectedRows)
    {
        Assert.Equal(expectedHeaders, table.Headers);
        Assert.Equal(expectedRows, table.Rows.Count);
    }

    private static CsvTable Parse(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        Assert.True(csv.Read());
        Assert.True(csv.ReadHeader());
        var headers = csv.HeaderRecord ?? [];
        var rows = new List<string[]>();
        while (csv.Read())
            rows.Add(csv.Parser.Record ?? []);
        return new CsvTable(headers, rows);
    }

    private static async Task<AppDbContext> SeedAsync()
    {
        var database = $"TenantExport_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(database)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(TenantA));
        TenantTestDoubles.EnsureTenant(db, TenantA, "cafe-a");
        TenantTestDoubles.EnsureTenant(db, TenantB, "cafe-b");
        var taxA = TenantTestDoubles.EnsureTaxGroup(db, TenantA);
        var taxB = TenantTestDoubles.EnsureTaxGroup(db, TenantB);

        var alpha = Product(TenantA, "Alpha", taxA);
        var beta = Product(TenantA, "Beta", taxA);
        var other = Product(TenantB, "Other", taxB);
        db.Products.AddRange(alpha, beta, other);

        db.Customers.AddRange(
            Customer(TenantA, "Ada", isSystem: false),
            Customer(TenantA, "WalkIn", isSystem: true),
            Customer(TenantB, "Bea", isSystem: false));

        var registerA = Guid.NewGuid();
        var registerB = Guid.NewGuid();
        db.CashRegisters.AddRange(
            Register(registerA, TenantA, "A1"),
            Register(registerB, TenantB, "B1"));

        var issued = new DateTime(2026, 11, 13, 8, 0, 0, DateTimeKind.Utc);
        db.Receipts.AddRange(
            Receipt(TenantA, "R-A", registerA, issued),
            Receipt(TenantB, "R-B", registerB, issued.AddHours(1)));

        db.PaymentDetails.AddRange(
            Payment(registerA, "Guest A", issued),
            Payment(registerB, "Guest B", issued.AddHours(1)));

        db.Users.AddRange(
            User("user-a", "ada@a.test", "Ada", "A"),
            User("user-b", "bea@b.test", "Bea", "B"));
        db.UserTenantMemberships.AddRange(
            Membership("user-a", TenantA),
            Membership("user-b", TenantB));

        var created = new DateTime(2026, 11, 14, 9, 0, 0, DateTimeKind.Utc);
        db.OnlineOrders.AddRange(
            OnlineOrderRow(TenantA, "Ada Guest", created),
            OnlineOrderRow(TenantB, "Bea Guest", created.AddHours(1)));

        db.AuditLogs.AddRange(
            AuditRow(TenantA, "user-a", "UPDATE", "Customer", created),
            AuditRow(TenantB, "user-b", "CREATE", "Product", created.AddHours(1)));

        db.TseSignatures.AddRange(
            TseRow(TenantA, "R-A", created),
            TseRow(TenantB, "R-B", created.AddHours(1)));

        db.ProductRecipeLines.AddRange(
            RecipeLine(TenantA, alpha.Id, "Milk", 0.2m),
            RecipeLine(TenantB, other.Id, "Sugar", 1m));
        db.Suppliers.AddRange(
            SupplierRow(TenantA, "Mill", "mill@a.test", "111", "Wien"),
            SupplierRow(TenantB, "Other Mill", "mill@b.test", "222", "Graz"));

        var stockA = InventoryRow(alpha.Id);
        var stockB = InventoryRow(other.Id);
        db.Inventory.AddRange(stockA, stockB);
        db.InventoryTransactions.AddRange(
            Movement(stockA.Id, 3, "delivery", created),
            Movement(stockB.Id, 9, "other-delivery", created.AddHours(1)));

        await db.SaveChangesAsync();
        return db;
    }

    private static Product Product(Guid tenantId, string name, Guid taxGroupId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        Name = name,
        Price = 2.5m,
        TaxRate = 10m,
        TaxGroupId = taxGroupId,
        Category = "Food",
        StockQuantity = 4,
        MinStockLevel = 2,
        Barcode = "bc-" + name,
        Unit = "Stk",
        TaxType = TaxTypes.Reduced,
        IsFiscalCompliant = true,
        IsTaxable = true,
        RksvProductType = RksvProductTypes.Standard,
        IsActive = true,
    };

    private static Customer Customer(Guid tenantId, string name, bool isSystem) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        Name = name,
        CustomerNumber = name,
        Email = name.ToLowerInvariant() + "@guest.test",
        Phone = "1",
        IsSystem = isSystem,
        IsActive = true,
    };

    private static CashRegister Register(Guid id, Guid tenantId, string number) => new()
    {
        Id = id,
        TenantId = tenantId,
        RegisterNumber = number,
        Location = number,
        StartingBalance = 0m,
        CurrentBalance = 0m,
        LastBalanceUpdate = DateTime.UtcNow,
        Status = RegisterStatus.Closed,
        IsActive = true,
    };

    private static Receipt Receipt(Guid tenantId, string number, Guid registerId, DateTime issued) => new()
    {
        ReceiptId = Guid.NewGuid(),
        TenantId = tenantId,
        ReceiptNumber = number,
        IssuedAt = issued,
        CashRegisterId = registerId,
        PaymentId = Guid.NewGuid(),
        SubTotal = 10m,
        TaxTotal = 2m,
        GrandTotal = 12m,
    };

    private static PaymentDetails Payment(Guid registerId, string customerName, DateTime created) => new()
    {
        Id = Guid.NewGuid(),
        CustomerId = Guid.NewGuid(),
        CustomerName = customerName,
        TableNumber = 1,
        CashierId = "cashier",
        TotalAmount = 12m,
        TaxAmount = 2m,
        PaymentMethodRaw = "0",
        CashRegisterId = registerId,
        ReceiptNumber = "P-" + customerName,
        CreatedAt = created,
        IsStorno = false,
        IsActive = true,
    };

    private static ApplicationUser User(string id, string email, string first, string last) => new()
    {
        Id = id,
        UserName = email,
        Email = email,
        FirstName = first,
        LastName = last,
        Role = Roles.Cashier,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
    };

    private static OnlineOrder OnlineOrderRow(Guid tenantId, string customerName, DateTime created) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        OrderNumber = "ORD-" + customerName,
        CustomerName = customerName,
        CustomerPhone = "1",
        OrderStatus = OnlineOrderStatuses.Pending,
        Total = 12.5m,
        Subtotal = 12.5m,
        PaymentMethod = OnlineOrderPaymentMethods.Cash,
        PaymentStatus = OnlineOrderPaymentStatuses.Pending,
        OrderType = OnlineOrderTypes.Takeaway,
        Source = OnlineOrderSources.Web,
        CreatedAt = created,
    };

    private static AuditLog AuditRow(Guid tenantId, string userId, string action, string entity, DateTime timestamp) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        SessionId = "session",
        UserId = userId,
        UserRole = Roles.Manager,
        Action = action,
        EntityType = entity,
        Status = AuditLogStatus.Success,
        Timestamp = timestamp,
        OldValues = "password-secret-do-not-export",
        TseSignature = "audit-jws-secret-do-not-export",
        IsActive = true,
        CreatedAt = timestamp,
    };

    private static ProductRecipeLine RecipeLine(Guid tenantId, Guid productId, string ingredient, decimal quantity) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        ProductId = productId,
        IngredientName = ingredient,
        Quantity = quantity,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
    };

    private static Supplier SupplierRow(Guid tenantId, string name, string email, string phone, string address) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        Name = name,
        Email = email,
        Phone = phone,
        Address = address,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
    };

    private static InventoryItem InventoryRow(Guid productId) => new()
    {
        Id = Guid.NewGuid(),
        ProductId = productId,
        CurrentStock = 4,
        MinStockLevel = 2,
        UnitCost = 1m,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
    };

    private static InventoryTransaction Movement(Guid inventoryId, int quantity, string reason, DateTime when) => new()
    {
        Id = Guid.NewGuid(),
        InventoryId = inventoryId,
        TransactionType = TransactionType.Restock,
        Quantity = quantity,
        UnitCost = 1m,
        TotalCost = quantity,
        Notes = reason,
        TransactionDate = when,
        IsActive = true,
        CreatedAt = when,
    };

    private static TseSignature TseRow(Guid tenantId, string receiptNumber, DateTime signedAt) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        Signature = "jws-secret-do-not-export",
        CashRegisterId = Guid.NewGuid(),
        InvoiceNumber = receiptNumber,
        Amount = 12m,
        SignatureType = "Invoice",
        TseDeviceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        CreatedAt = signedAt,
    };

    private static UserTenantMembership Membership(string userId, Guid tenantId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        TenantId = tenantId,
        IsActive = true,
        CreatedAtUtc = DateTime.UtcNow,
    };

    private sealed record CsvTable(string[] Headers, List<string[]> Rows);
}

/// <summary>Header row shared with the service. Kept here so a column change fails the test on purpose.</summary>
file static class TenantDataExportHeaders
{
    public static readonly string[] Products =
    [
        "Id", "TenantId", "Name", "Price", "TaxRate", "Barcode", "Category", "StockQuantity", "IsActive",
    ];

    public static readonly string[] Customers =
    [
        "Id", "TenantId", "Name", "CustomerNumber", "Email", "Phone", "IsActive",
    ];

    public static readonly string[] Receipts =
    [
        "ReceiptId", "TenantId", "ReceiptNumber", "IssuedAt", "CashRegisterId", "PaymentId", "SubTotal", "TaxTotal", "GrandTotal",
    ];

    public static readonly string[] Payments =
    [
        "Id", "TenantId", "CashRegisterId", "CustomerName", "TotalAmount", "TaxAmount", "PaymentMethod", "ReceiptNumber", "CreatedAt", "IsStorno",
    ];

    public static readonly string[] Users =
    [
        "UserId", "TenantId", "Email", "UserName", "FirstName", "LastName", "Role", "IsActive",
    ];

    public static readonly string[] Orders =
    [
        "Id", "Status", "Total", "CustomerName", "CreatedAt", "TenantId",
    ];

    public static readonly string[] AuditLogs =
    [
        "Action", "Entity", "User", "Timestamp", "TenantId",
    ];

    public static readonly string[] Inventory =
    [
        "Product", "Stock", "ReorderLevel", "TenantId",
    ];

    public static readonly string[] TseSignatures =
    [
        "ReceiptNumber", "SignatureDate", "TseId", "TenantId",
    ];

    public static readonly string[] OriginalTypes = ["products", "customers", "receipts", "payments", "users"];

    public static readonly string[] ExtendedTypes = ["orders", "audit_logs", "inventory", "tse_signatures"];

    public static readonly string[] Recipes =
    [
        "Product", "Ingredient", "Quantity", "TenantId",
    ];

    public static readonly string[] Suppliers =
    [
        "Name", "Email", "Phone", "Address", "TenantId",
    ];

    public static readonly string[] StockMovements =
    [
        "Product", "Quantity", "Reason", "Date", "TenantId",
    ];

    public static readonly string[] CatalogOpsTypes = ["recipes", "suppliers", "stock_movements"];
}

public sealed class AdminTenantDataExportControllerTests
{
    [Fact]
    public async Task Export_RejectsUnknownType()
    {
        var controller = CreateController(authorized: true, isSuperAdmin: false, out _, out _);
        var result = await controller.Export("invoices", "csv");
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("TENANT_EXPORT_TYPE", bad.Value?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_RejectsUnknownFormat()
    {
        var controller = CreateController(authorized: true, isSuperAdmin: true, out _, out _);
        var result = await controller.Export("products", "json");
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Export_ForbiddenWithoutViewPermission()
    {
        var controller = CreateController(authorized: false, isSuperAdmin: true, out _, out _);
        var result = await controller.Export("products", "csv");
        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, status.StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Export_StreamsWithRoleScope(bool isSuperAdmin)
    {
        var controller = CreateController(authorized: true, isSuperAdmin, out var export, out _);
        var http = controller.ControllerContext.HttpContext;
        var result = await controller.Export("receipts", "xlsx");
        await result.ExecuteResultAsync(new ActionContext(
            http,
            new RouteData(),
            new ControllerActionDescriptor()));

        Assert.Equal(TenantDataExportService.XlsxContentType, http.Response.ContentType);
        Assert.Contains("receipts-", http.Response.Headers.ContentDisposition.ToString(), StringComparison.Ordinal);
        export.Verify(x => x.WriteAsync(
            It.IsAny<Stream>(),
            "receipts",
            "xlsx",
            isSuperAdmin,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("orders", AppPermissions.OrderView)]
    [InlineData("audit_logs", AppPermissions.AuditView)]
    [InlineData("inventory", AppPermissions.InventoryView)]
    [InlineData("tse_signatures", AppPermissions.SystemCritical)]
    [InlineData("recipes", AppPermissions.ProductView)]
    [InlineData("suppliers", AppPermissions.SupplierView)]
    [InlineData("stock_movements", AppPermissions.InventoryView)]
    public async Task Export_ChecksThePermissionForEachNewType(string type, string permission)
    {
        var controller = CreateController(authorized: true, isSuperAdmin: true, out _, out var auth);
        var result = await controller.Export(type, "csv");

        Assert.DoesNotContain("BadRequest", result.GetType().Name, StringComparison.Ordinal);
        auth.Verify(x => x.AuthorizeAsync(
            It.IsAny<ClaimsPrincipal>(),
            null,
            PermissionCatalog.PolicyPrefix + permission), Times.Once);
    }

    [Fact]
    public async Task Export_TseSignatures_ForbiddenUnlessSuperAdmin()
    {
        var controller = CreateController(authorized: true, isSuperAdmin: false, out var export, out _);
        var result = await controller.Export("tse_signatures", "csv");
        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, status.StatusCode);
        export.Verify(x => x.WriteAsync(
            It.IsAny<Stream>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static AdminTenantDataExportController CreateController(
        bool authorized,
        bool isSuperAdmin,
        out Mock<ITenantDataExportService> export,
        out Mock<IAuthorizationService> auth)
    {
        export = new Mock<ITenantDataExportService>();
        export
            .Setup(x => x.WriteAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        auth = new Mock<IAuthorizationService>();
        auth
            .Setup(x => x.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), null, It.IsAny<string>()))
            .ReturnsAsync(authorized ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        var identity = new ClaimsIdentity(
            isSuperAdmin ? [new Claim(ClaimTypes.Role, Roles.SuperAdmin)] : [],
            authenticationType: "test");
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity),
            Response = { Body = new MemoryStream() },
        };

        return new AdminTenantDataExportController(export.Object, auth.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
        };
    }
}
