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

    private static async Task<Dictionary<string, CsvTable>> ExportCsvAsync(AppDbContext db, bool includeAllTenants)
    {
        var service = new TenantDataExportService(db, NullLogger<TenantDataExportService>.Instance);
        var result = new Dictionary<string, CsvTable>(StringComparer.Ordinal);
        foreach (var type in new[] { "products", "customers", "receipts", "payments", "users" })
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

        db.Products.AddRange(
            Product(TenantA, "Alpha", taxA),
            Product(TenantA, "Beta", taxA),
            Product(TenantB, "Other", taxB));

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
}

public sealed class AdminTenantDataExportControllerTests
{
    [Fact]
    public async Task Export_RejectsUnknownType()
    {
        var controller = CreateController(authorized: true, isSuperAdmin: false, out _);
        var result = await controller.Export("invoices", "csv");
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("TENANT_EXPORT_TYPE", bad.Value?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_RejectsUnknownFormat()
    {
        var controller = CreateController(authorized: true, isSuperAdmin: true, out _);
        var result = await controller.Export("products", "json");
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Export_ForbiddenWithoutViewPermission()
    {
        var controller = CreateController(authorized: false, isSuperAdmin: true, out _);
        var result = await controller.Export("products", "csv");
        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, status.StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Export_StreamsWithRoleScope(bool isSuperAdmin)
    {
        var controller = CreateController(authorized: true, isSuperAdmin, out var export);
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

    private static AdminTenantDataExportController CreateController(
        bool authorized,
        bool isSuperAdmin,
        out Mock<ITenantDataExportService> export)
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

        var auth = new Mock<IAuthorizationService>();
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
