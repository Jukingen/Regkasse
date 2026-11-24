using System.IO.Compression;
using System.Text;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AuditTypedExportTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly DateTime Day = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

    // ---------- storno ----------

    [Fact]
    public async Task Storno_ExportsOnlyStornoRowsOfTheTenant_WithOriginalReceipt()
    {
        await using var fx = await Fixture.CreateAsync();
        var original = fx.AddPayment(fx.RegisterA, "R-ORIG", Day.AddHours(-1), total: 20m);
        fx.AddPayment(fx.RegisterA, "R-SALE", Day, total: 5m);
        fx.AddPayment(fx.RegisterA, "R-REFUND", Day, total: 3m, isRefund: true);
        fx.AddPayment(fx.RegisterA, "R-STORNO", Day, total: -20m, isStorno: true, originalId: original.Id,
            reason: StornoReason.KundeStorniert, cancellationReason: "Kunde hat storniert");
        fx.AddPayment(fx.RegisterB, "R-OTHER-TENANT", Day, total: -9m, isStorno: true);
        await fx.SaveAsync();

        var lines = await fx.ExportCsvAsync(AuditExportType.Storno, new AuditTypedExportFilters());

        Assert.Equal(2, lines.Count);
        Assert.StartsWith("Id,ReceiptNumber,CreatedAtUtc,OriginalPaymentId,OriginalReceiptNumber", lines[0], StringComparison.Ordinal);
        Assert.Contains("R-STORNO", lines[1], StringComparison.Ordinal);
        Assert.Contains("R-ORIG", lines[1], StringComparison.Ordinal);
        Assert.Contains("KundeStorniert", lines[1], StringComparison.Ordinal);
        Assert.Contains("3600", lines[1], StringComparison.Ordinal);
        Assert.Contains("-20.00", lines[1], StringComparison.Ordinal);
        Assert.DoesNotContain("R-OTHER-TENANT", string.Join('\n', lines), StringComparison.Ordinal);
        Assert.DoesNotContain("R-REFUND", string.Join('\n', lines), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Storno_FiltersByReasonCashierRegisterDateAndAmount()
    {
        await using var fx = await Fixture.CreateAsync();
        var second = fx.AddRegister(TenantA, "K2");
        fx.AddPayment(fx.RegisterA, "S-1", Day, total: -10m, isStorno: true, reason: StornoReason.FalscherBetrag, cashier: "anna");
        fx.AddPayment(fx.RegisterA, "S-2", Day.AddDays(-5), total: -50m, isStorno: true, reason: StornoReason.Anderes, cashier: "bob");
        fx.AddPayment(second, "S-3", Day, total: -30m, isStorno: true, reason: StornoReason.FalscherBetrag, cashier: "bob");
        await fx.SaveAsync();

        Assert.Equal(["S-1", "S-3"], await fx.ReceiptsAsync(AuditExportType.Storno, new() { StornoReason = "falscherbetrag" }, orderAscending: true));
        Assert.Equal(["S-2", "S-3"], await fx.ReceiptsAsync(AuditExportType.Storno, new() { UserId = "bob" }, orderAscending: true));
        Assert.Equal(["S-3"], await fx.ReceiptsAsync(AuditExportType.Storno, new() { CashRegisterId = second.Id }));
        Assert.Equal(["S-1", "S-3"], await fx.ReceiptsAsync(AuditExportType.Storno,
            new() { StartDate = Day.Date, EndDate = Day.Date }, orderAscending: true));
        Assert.Equal(["S-3"], await fx.ReceiptsAsync(AuditExportType.Storno, new() { MinAmount = 20m, MaxAmount = 40m }));
    }

    // ---------- refund ----------

    [Fact]
    public async Task Refund_ExportsOnlyRefundRows_WithAmountAndReason()
    {
        await using var fx = await Fixture.CreateAsync();
        var original = fx.AddPayment(fx.RegisterA, "R-ORIG", Day.AddHours(-2), total: 20m);
        var refund = fx.AddPayment(fx.RegisterA, "R-REF", Day, total: 8m, isRefund: true, originalId: original.Id);
        refund.RefundAmount = 8m;
        refund.RefundReason = "Ware defekt";
        refund.RefundedAt = Day;
        fx.AddPayment(fx.RegisterA, "R-STORNO", Day, total: -20m, isStorno: true);
        fx.AddPayment(fx.RegisterB, "R-OTHER", Day, total: 1m, isRefund: true);
        await fx.SaveAsync();

        var lines = await fx.ExportCsvAsync(AuditExportType.Refund, new AuditTypedExportFilters());

        Assert.Equal(2, lines.Count);
        Assert.Contains("RefundAmount", lines[0], StringComparison.Ordinal);
        Assert.Contains("R-REF", lines[1], StringComparison.Ordinal);
        Assert.Contains("R-ORIG", lines[1], StringComparison.Ordinal);
        Assert.Contains("Ware defekt", lines[1], StringComparison.Ordinal);
        Assert.Contains("8.00", lines[1], StringComparison.Ordinal);
    }

    // ---------- voucher redemption ----------

    [Fact]
    public async Task VoucherRedemption_ExportsOnlyRedeemLines_WithoutCodeOrHash()
    {
        await using var fx = await Fixture.CreateAsync();
        var voucher = fx.AddVoucher(TenantA, "****1234", codeHash: "SECRET-HASH-AAAA");
        var otherVoucher = fx.AddVoucher(TenantB, "****9999", codeHash: "SECRET-HASH-BBBB");
        var payment = fx.AddPayment(fx.RegisterA, "R-PAID", Day, total: 25m);
        fx.AddLedger(voucher, VoucherTransactionType.Issue, 100m, 100m, Day.AddDays(-1), "issuer");
        fx.AddLedger(voucher, VoucherTransactionType.Redeem, -25m, 75m, Day, "cashier-1", payment: payment);
        fx.AddLedger(voucher, VoucherTransactionType.Refund, 5m, 80m, Day.AddHours(1), "cashier-1");
        fx.AddLedger(otherVoucher, VoucherTransactionType.Redeem, -10m, 90m, Day, "cashier-x", tenant: TenantB);
        await fx.SaveAsync();

        var lines = await fx.ExportCsvAsync(AuditExportType.VoucherRedemption, new AuditTypedExportFilters());
        var text = string.Join('\n', lines);

        Assert.Equal(2, lines.Count);
        Assert.StartsWith("LedgerEntryId,CreatedAtUtc,VoucherId,MaskedCode", lines[0], StringComparison.Ordinal);
        Assert.Contains("****1234", lines[1], StringComparison.Ordinal);
        Assert.Contains("25.00", lines[1], StringComparison.Ordinal);
        Assert.Contains("75.00", lines[1], StringComparison.Ordinal);
        Assert.Contains("R-PAID", lines[1], StringComparison.Ordinal);
        Assert.DoesNotContain("-25.00", lines[1], StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET-HASH", text, StringComparison.Ordinal);
        Assert.DoesNotContain("****9999", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VoucherRedemption_FiltersByVoucherUserDateAndAmount()
    {
        await using var fx = await Fixture.CreateAsync();
        var v1 = fx.AddVoucher(TenantA, "****0001");
        var v2 = fx.AddVoucher(TenantA, "****0002");
        fx.AddLedger(v1, VoucherTransactionType.Redeem, -10m, 90m, Day, "anna");
        fx.AddLedger(v2, VoucherTransactionType.Redeem, -40m, 60m, Day.AddDays(-4), "bob");
        fx.AddLedger(v2, VoucherTransactionType.Redeem, -20m, 40m, Day, "bob");
        await fx.SaveAsync();

        Assert.Equal(2, (await fx.ExportCsvAsync(AuditExportType.VoucherRedemption, new() { VoucherId = v2.Id })).Count - 1);
        Assert.Equal(2, (await fx.ExportCsvAsync(AuditExportType.VoucherRedemption, new() { UserId = "bob" })).Count - 1);
        Assert.Equal(2, (await fx.ExportCsvAsync(AuditExportType.VoucherRedemption,
            new() { StartDate = Day.Date, EndDate = Day.Date })).Count - 1);
        var amountLines = await fx.ExportCsvAsync(AuditExportType.VoucherRedemption, new() { MinAmount = 30m });
        Assert.Equal(2, amountLines.Count);
        Assert.Contains("40.00", amountLines[1], StringComparison.Ordinal);
    }

    // ---------- validation ----------

    [Theory]
    [InlineData(AuditExportType.Refund, "StornoReason")]
    [InlineData(AuditExportType.Storno, "VoucherId")]
    [InlineData(AuditExportType.VoucherRedemption, "CashRegisterId")]
    [InlineData(AuditExportType.Storno, "BadStornoReason")]
    [InlineData(AuditExportType.Storno, "DateRange")]
    [InlineData(AuditExportType.Storno, "AmountRange")]
    [InlineData(AuditExportType.Storno, "PageWithoutSize")]
    public async Task InvalidFilters_AreRejected_BeforeAnyByteIsWritten(AuditExportType type, string problem)
    {
        await using var fx = await Fixture.CreateAsync();
        var filters = problem switch
        {
            "StornoReason" => new AuditTypedExportFilters { StornoReason = "Anderes" },
            "VoucherId" => new AuditTypedExportFilters { VoucherId = Guid.NewGuid() },
            "CashRegisterId" => new AuditTypedExportFilters { CashRegisterId = Guid.NewGuid() },
            "BadStornoReason" => new AuditTypedExportFilters { StornoReason = "Nope" },
            "DateRange" => new AuditTypedExportFilters { StartDate = Day.Date, EndDate = Day.Date.AddDays(-1) },
            "AmountRange" => new AuditTypedExportFilters { MinAmount = 5m, MaxAmount = 1m },
            _ => new AuditTypedExportFilters { Page = 1 },
        };

        await using var output = new MemoryStream();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            fx.Service.StreamAsync(type, TenantA, filters, "csv", output));
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public async Task UnknownFormat_IsRejected()
    {
        await using var fx = await Fixture.CreateAsync();
        await using var output = new MemoryStream();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            fx.Service.StreamAsync(AuditExportType.Storno, TenantA, new AuditTypedExportFilters(), "pdf", output));
    }

    [Theory]
    [InlineData("storno", true, AuditExportType.Storno)]
    [InlineData("REFUND", true, AuditExportType.Refund)]
    [InlineData("voucher_redemption", true, AuditExportType.VoucherRedemption)]
    [InlineData("voucher-redemption", true, AuditExportType.VoucherRedemption)]
    [InlineData("payments", false, default(AuditExportType))]
    [InlineData("", false, default(AuditExportType))]
    public void TypeNames_ParseCaseInsensitively(string value, bool ok, AuditExportType expected)
    {
        Assert.Equal(ok, AuditExportTypes.TryParse(value, out var type));
        if (ok)
            Assert.Equal(expected, type);
    }

    // ---------- format, streaming ----------

    [Fact]
    public async Task Xlsx_HasTypedSheetNameHeadersAndRows()
    {
        await using var fx = await Fixture.CreateAsync();
        fx.AddPayment(fx.RegisterA, "R-X", Day, total: -4m, isStorno: true, reason: StornoReason.Anderes);
        await fx.SaveAsync();

        await using var xlsx = new MemoryStream();
        await fx.Service.StreamAsync(AuditExportType.Storno, TenantA, new AuditTypedExportFilters(), "xlsx", xlsx);
        xlsx.Position = 0;
        using var zip = new ZipArchive(xlsx, ZipArchiveMode.Read);

        using var workbook = new StreamReader(zip.GetEntry("xl/workbook.xml")!.Open());
        Assert.Contains("name=\"storno\"", await workbook.ReadToEndAsync(), StringComparison.Ordinal);

        using var sheet = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        var xml = await sheet.ReadToEndAsync();
        Assert.Contains(">ReceiptNumber<", xml, StringComparison.Ordinal);
        Assert.Contains(">StornoReason<", xml, StringComparison.Ordinal);
        Assert.Contains(">R-X<", xml, StringComparison.Ordinal);
        Assert.Contains(">Anderes<", xml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Xlsx_DropsControlCharactersThatWouldCorruptTheWorkbook()
    {
        await using var fx = await Fixture.CreateAsync();
        fx.AddPayment(fx.RegisterA, "R-CTRL", Day, total: -1m, isStorno: true, cancellationReason: "bad\u0001char");
        await fx.SaveAsync();

        await using var xlsx = new MemoryStream();
        await fx.Service.StreamAsync(AuditExportType.Storno, TenantA, new AuditTypedExportFilters(), "xlsx", xlsx);
        xlsx.Position = 0;
        using var zip = new ZipArchive(xlsx, ZipArchiveMode.Read);
        using var sheet = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        var xml = await sheet.ReadToEndAsync();

        Assert.Contains(">badchar<", xml, StringComparison.Ordinal);
        System.Xml.Linq.XDocument.Parse(xml); // must stay well-formed
    }

    [Fact]
    public async Task Csv_StreamsMoreRowsThanOneBatch_WithoutGapsOrDuplicates()
    {
        await using var fx = await Fixture.CreateAsync();
        const int total = 4_105; // two full batches of 2,000 plus a remainder
        for (var i = 0; i < total; i++)
            fx.AddPayment(fx.RegisterA, $"B-{i:D5}", Day.AddSeconds(i), total: -1m, isStorno: true);
        await fx.SaveAsync();

        var lines = await fx.ExportCsvAsync(AuditExportType.Storno, new AuditTypedExportFilters());
        var receipts = lines.Skip(1).Select(l => l.Split(',')[1]).ToList();

        Assert.Equal(total, receipts.Count);
        Assert.Equal(total, receipts.Distinct().Count());
        Assert.Equal($"B-{total - 1:D5}", receipts[0]); // newest first
        Assert.Equal("B-00000", receipts[^1]);
    }

    [Fact]
    public async Task Csv_PageAndPageSize_ReturnOnlyThatPage()
    {
        await using var fx = await Fixture.CreateAsync();
        for (var i = 0; i < 5; i++)
            fx.AddPayment(fx.RegisterA, $"P-{i}", Day.AddMinutes(i), total: -1m, isStorno: true);
        await fx.SaveAsync();

        var page2 = await fx.ReceiptsAsync(AuditExportType.Storno, new() { Page = 2, PageSize = 2 });

        Assert.Equal(["P-2", "P-1"], page2);
    }

    [Fact]
    public async Task Csv_NeutralizesFormulasAndQuotesSpecialCharacters()
    {
        await using var fx = await Fixture.CreateAsync();
        fx.AddPayment(fx.RegisterA, "R-F", Day, total: -1m, isStorno: true,
            cancellationReason: "=HYPERLINK(\"http://evil\")");
        fx.AddPayment(fx.RegisterA, "R-C", Day.AddSeconds(-1), total: -1m, isStorno: true,
            cancellationReason: "a, \"quoted\"\nline");
        await fx.SaveAsync();

        await using var output = new MemoryStream();
        await fx.Service.StreamAsync(AuditExportType.Storno, TenantA, new AuditTypedExportFilters(), "csv", output);
        var text = Encoding.UTF8.GetString(output.ToArray());

        Assert.Contains("\"'=HYPERLINK(\"\"http://evil\"\")\"", text, StringComparison.Ordinal);
        Assert.Contains("\"a, \"\"quoted\"\"\nline\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain(",=HYPERLINK", text, StringComparison.Ordinal);
    }

    // ---------- endpoint contract ----------

    [Theory]
    [InlineData(nameof(AdminAuditTypedExportsController.ExportStorno), "storno", AppPermissions.PaymentView)]
    [InlineData(nameof(AdminAuditTypedExportsController.ExportRefund), "refund", AppPermissions.PaymentView)]
    [InlineData(nameof(AdminAuditTypedExportsController.ExportVoucherRedemption), "voucher_redemption", AppPermissions.VoucherAuditView)]
    public void Endpoints_RequireAuditExportPlusTheViewPermissionOfTheData(string action, string route, string dataPermission)
    {
        var method = typeof(AdminAuditTypedExportsController).GetMethod(action);
        Assert.NotNull(method);

        var permissions = method!.GetCustomAttributes(typeof(HasPermissionAttribute), inherit: true)
            .Cast<HasPermissionAttribute>()
            .Select(a => a.Permission)
            .ToList();
        Assert.Contains(AppPermissions.AuditExport, permissions);
        Assert.Contains(dataPermission, permissions);

        var http = Assert.Single(method.GetCustomAttributes(typeof(HttpGetAttribute), inherit: true)) as HttpGetAttribute;
        Assert.Equal(route, http!.Template);

        var controllerRoute = typeof(AdminAuditTypedExportsController).GetCustomAttributes(typeof(RouteAttribute), inherit: true)
            .Cast<RouteAttribute>()
            .Single();
        Assert.Equal("api/admin/audit-logs/export", controllerRoute.Template);
    }

    // ---------- fixture ----------

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly AppDbContext _db;
        private readonly TenantTestDoubles.MutableTenantAccessor _accessor;

        private Fixture(AppDbContext db, TenantTestDoubles.MutableTenantAccessor accessor)
        {
            _db = db;
            _accessor = accessor;
            Service = new AuditTypedExportService(db);
            RegisterA = AddRegister(TenantA, "K1");
            RegisterB = AddRegister(TenantB, "KB");
        }

        public AuditTypedExportService Service { get; }
        public CashRegister RegisterA { get; }
        public CashRegister RegisterB { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            var accessor = (TenantTestDoubles.MutableTenantAccessor)TenantTestDoubles.TenantAccessorReturning(TenantA);
            var fixture = new Fixture(new AppDbContext(options, accessor), accessor);
            await fixture.SaveAsync();
            return fixture;
        }

        public Task SaveAsync() => _db.SaveChangesAsync();

        public CashRegister AddRegister(Guid tenantId, string number)
        {
            var register = new CashRegister
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RegisterNumber = number,
                Location = "L",
                StartingBalance = 0,
                CurrentBalance = 0,
                LastBalanceUpdate = Day,
                Status = RegisterStatus.Open,
                CreatedAt = Day,
                IsActive = true,
            };
            _db.CashRegisters.Add(register);
            return register;
        }

        public PaymentDetails AddPayment(
            CashRegister register,
            string receipt,
            DateTime createdAt,
            decimal total,
            bool isStorno = false,
            bool isRefund = false,
            Guid? originalId = null,
            StornoReason? reason = null,
            string? cancellationReason = null,
            string cashier = "cashier1")
        {
            var payment = new PaymentDetails
            {
                Id = Guid.NewGuid(),
                CustomerId = Guid.NewGuid(),
                CustomerName = "Test",
                TableNumber = 1,
                CashierId = cashier,
                TotalAmount = total,
                TaxAmount = 1m,
                PaymentMethodRaw = "0",
                Steuernummer = "ATU12345678",
                CashRegisterId = register.Id,
                TseSignature = "sig",
                TseTimestamp = createdAt,
                ReceiptNumber = receipt,
                CreatedAt = createdAt,
                IsActive = true,
                IsStorno = isStorno,
                IsRefund = isRefund,
                OriginalPaymentId = originalId,
                StornoReason = reason,
                CancellationReason = cancellationReason,
            };
            _db.PaymentDetails.Add(payment);
            return payment;
        }

        public Voucher AddVoucher(Guid tenantId, string maskedCode, string codeHash = "HASH")
        {
            var voucher = new Voucher
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CodeHash = codeHash,
                MaskedCode = maskedCode,
                InitialAmount = 100m,
                RemainingAmount = 100m,
                Currency = "EUR",
                ValidFromUtc = Day.AddDays(-30),
                ExpiresAtUtc = Day.AddDays(300),
                CreatedByUserId = "issuer",
                CreatedAtUtc = Day.AddDays(-30),
            };
            _db.Vouchers.Add(voucher);
            return voucher;
        }

        public void AddLedger(
            Voucher voucher,
            VoucherTransactionType type,
            decimal amount,
            decimal balanceAfter,
            DateTime createdAt,
            string userId,
            PaymentDetails? payment = null,
            Guid? tenant = null)
        {
            _db.VoucherLedgerEntries.Add(new VoucherLedgerEntry
            {
                Id = Guid.NewGuid(),
                TenantId = tenant ?? voucher.TenantId,
                VoucherId = voucher.Id,
                PaymentId = payment?.Id,
                Type = type,
                Amount = amount,
                BalanceAfter = balanceAfter,
                CreatedByUserId = userId,
                CreatedAtUtc = createdAt,
            });
        }

        /// <summary>CSV split into logical lines (a quoted newline stays inside its record).</summary>
        public async Task<List<string>> ExportCsvAsync(AuditExportType type, AuditTypedExportFilters filters)
        {
            await using var output = new MemoryStream();
            await Service.StreamAsync(type, TenantA, filters, "csv", output);
            return SplitRecords(Encoding.UTF8.GetString(output.ToArray()));
        }

        public async Task<List<string>> ReceiptsAsync(
            AuditExportType type,
            AuditTypedExportFilters filters,
            bool orderAscending = false)
        {
            var lines = await ExportCsvAsync(type, filters);
            var receipts = lines.Skip(1).Select(l => l.Split(',')[1]).ToList();
            if (orderAscending)
                receipts.Sort(StringComparer.Ordinal);
            return receipts;
        }

        private static List<string> SplitRecords(string csv)
        {
            var records = new List<string>();
            var current = new StringBuilder();
            var inQuotes = false;
            foreach (var c in csv)
            {
                if (c == '"')
                    inQuotes = !inQuotes;
                if (c == '\n' && !inQuotes)
                {
                    records.Add(current.ToString().TrimEnd('\r'));
                    current.Clear();
                    continue;
                }

                current.Append(c);
            }

            if (current.Length > 0)
                records.Add(current.ToString().TrimEnd('\r'));
            return records;
        }

        public ValueTask DisposeAsync() => _db.DisposeAsync();
    }
}
