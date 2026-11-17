using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Freezes PaymentService outcomes before the fiscal-boundary refactor.
/// Timestamps, generated ids, and the date segment of BelegNr are tokens so the
/// fixture stays stable across runs. Amounts, flags, stock, and ledger rows are not.
/// Regenerate with PAYMENT_GOLDEN_RECORD=1 only when the frozen behavior is intentional.
/// </summary>
public sealed class PaymentGoldenMasterTests
{
    private static readonly Guid CustomerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ProductId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid RegisterId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid CategoryId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid VoucherId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid CardIntentId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid PercentageBenefitId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid FreeAllowanceBenefitId = Guid.Parse("88888888-8888-8888-8888-888888888888");

    public static IEnumerable<object[]> Scenarios() =>
    [
        ["cash-sale"],
        ["card-intent"],
        ["voucher-full"],
        ["mixed-voucher-cash"],
        ["idempotent-replay"],
        ["tse-offline-queue"],
        ["full-storno"],
        ["partial-refund"],
        ["benefit-preview-payment"],
        ["finanzonline-retry"]
    ];

    [Theory]
    [MemberData(nameof(Scenarios))]
    public async Task Scenario_MatchesFrozenFixture(string scenario)
    {
        var snapshot = await CaptureAsync(scenario);
        var actual = Canonicalize(snapshot);
        var sourcePath = Path.Combine(SourceDirectory(), scenario + ".json");
        var record = string.Equals(
            Environment.GetEnvironmentVariable("PAYMENT_GOLDEN_RECORD"),
            "1",
            StringComparison.Ordinal);

        if (record)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            await File.WriteAllTextAsync(sourcePath, actual);
        }

        Assert.True(
            File.Exists(sourcePath),
            $"Missing golden fixture {sourcePath}. Run with PAYMENT_GOLDEN_RECORD=1 to capture current PaymentService behavior.");

        var expected = (await File.ReadAllTextAsync(sourcePath)).Replace("\r\n", "\n", StringComparison.Ordinal);
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            var actualPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PaymentGoldenMaster", scenario + ".actual.json");
            Directory.CreateDirectory(Path.GetDirectoryName(actualPath)!);
            await File.WriteAllTextAsync(actualPath, actual);
            Assert.Equal(expected, actual);
        }
    }

    private static async Task<JsonObject> CaptureAsync(string scenario)
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        await PaymentServiceCoverageHarness.SeedCatalogAsync(
            ctx,
            categoryId: CategoryId,
            productId: ProductId,
            customerId: CustomerId,
            cashRegisterId: RegisterId);
        var stockBefore = await StockAsync(ctx);
        var operations = new JsonArray();

        switch (scenario)
        {
            case "cash-sale":
            {
                var sut = Service(ctx);
                var result = await sut.CreatePaymentAsync(
                    PaymentServiceCoverageHarness.SaleRequest(
                        CustomerId, ProductId, RegisterId, idempotencyKey: "golden-cash-sale"),
                    PaymentServiceCoverageHarness.CashierId);
                Require(result, scenario);
                operations.Add(ResultNode("CreatePayment", result));
                break;
            }
            case "card-intent":
            {
                var sut = Service(ctx, CardOptions());
                var result = await sut.CreatePaymentAsync(
                    PaymentServiceCoverageHarness.SaleRequest(
                        CustomerId, ProductId, RegisterId,
                        method: "card",
                        idempotencyKey: "golden-card-intent",
                        cardPaymentIntentId: CardIntentId),
                    PaymentServiceCoverageHarness.CashierId);
                Require(result, scenario);
                operations.Add(ResultNode("CreatePayment", result));
                break;
            }
            case "voucher-full":
            {
                await PaymentServiceCoverageHarness.AddVoucherAsync(ctx, "GOLDEN-FULL", 50m, VoucherId);
                var sut = Service(ctx);
                var request = PaymentServiceCoverageHarness.SaleRequest(
                    CustomerId, ProductId, RegisterId, method: "voucher", idempotencyKey: "golden-voucher-full");
                request.Payment.VoucherCode = "GOLDEN-FULL";
                var result = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);
                Require(result, scenario);
                operations.Add(ResultNode("CreatePayment", result));
                break;
            }
            case "mixed-voucher-cash":
            {
                await PaymentServiceCoverageHarness.AddVoucherAsync(ctx, "GOLDEN-MIX", 50m, VoucherId);
                var sut = Service(ctx);
                var request = PaymentServiceCoverageHarness.SaleRequest(
                    CustomerId, ProductId, RegisterId, idempotencyKey: "golden-mixed-voucher-cash");
                request.Payment.Amount = 4m;
                request.Payment.VoucherRedemptions =
                [
                    new VoucherRedemptionRequestItem { Code = "GOLDEN-MIX", Amount = 6m }
                ];
                var result = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);
                Require(result, scenario);
                operations.Add(ResultNode("CreatePayment", result));
                break;
            }
            case "idempotent-replay":
            {
                var sut = Service(ctx);
                var request = PaymentServiceCoverageHarness.SaleRequest(
                    CustomerId, ProductId, RegisterId, idempotencyKey: "golden-idempotent-replay");
                var first = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);
                Require(first, scenario + " first");
                var second = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);
                Require(second, scenario + " replay");
                if (!second.IdempotentReplay)
                    throw new InvalidOperationException("Expected the second call to be an idempotent replay.");
                operations.Add(ResultNode("CreatePayment", first));
                operations.Add(ResultNode("CreatePaymentReplay", second));
                break;
            }
            case "tse-offline-queue":
            {
                var sut = Service(ctx, OfflineOptions());
                var result = await sut.CreatePaymentAsync(
                    PaymentServiceCoverageHarness.SaleRequest(
                        CustomerId, ProductId, RegisterId, idempotencyKey: "golden-tse-offline"),
                    PaymentServiceCoverageHarness.CashierId);
                Require(result, scenario);
                if (!result.NonFiscalOfflineQueued || result.DiagnosticCode != "NON_FISCAL_QUEUED")
                    throw new InvalidOperationException(
                        "Expected a non-fiscal offline queue. Diagnostic=" + result.DiagnosticCode);
                operations.Add(ResultNode("CreatePayment", result));
                break;
            }
            case "full-storno":
            {
                var sut = Service(ctx);
                var sale = await sut.CreatePaymentAsync(
                    PaymentServiceCoverageHarness.SaleRequest(
                        CustomerId, ProductId, RegisterId, idempotencyKey: "golden-storno-sale"),
                    PaymentServiceCoverageHarness.CashierId);
                Require(sale, scenario + " sale");
                var storno = await sut.CancelPaymentAsync(
                    sale.Payment!.Id,
                    "Kunde hat storniert",
                    PaymentServiceCoverageHarness.CashierId,
                    idempotencyKey: "golden-full-storno",
                    reasonCode: CancellationReasonCode.CustomerRequest);
                Require(storno, scenario);
                operations.Add(ResultNode("CreatePayment", sale));
                operations.Add(ResultNode("CancelPayment", storno));
                break;
            }
            case "partial-refund":
            {
                var sut = Service(ctx);
                var sale = await sut.CreatePaymentAsync(
                    PaymentServiceCoverageHarness.SaleRequest(
                        CustomerId, ProductId, RegisterId,
                        total: 20m,
                        quantity: 2,
                        idempotencyKey: "golden-refund-sale"),
                    PaymentServiceCoverageHarness.CashierId);
                Require(sale, scenario + " sale");
                var refund = await sut.RefundPaymentAsync(
                    sale.Payment!.Id,
                    10m,
                    "Teilweise Reklamation",
                    PaymentServiceCoverageHarness.CashierId,
                    idempotencyKey: "golden-partial-refund",
                    reasonCode: RefundReasonCode.CustomerComplaint);
                Require(refund, scenario);
                operations.Add(ResultNode("CreatePayment", sale));
                operations.Add(ResultNode("RefundPayment", refund));
                break;
            }
            case "benefit-preview-payment":
            {
                await SeedBenefitsAsync(ctx);
                var sut = Service(ctx);
                var preview = await sut.ComputeBenefitEligibilityPreviewAsync(new BenefitEligibilityPreviewRequest
                {
                    CustomerId = CustomerId,
                    CashRegisterId = RegisterId,
                    Items = [new BenefitEligibilityPreviewItemRequest { ProductId = ProductId, Quantity = 2 }]
                });
                if (preview == null)
                    throw new InvalidOperationException("Benefit preview returned null.");
                operations.Add(PreviewNode(preview));
                var payment = await sut.CreatePaymentAsync(
                    PaymentServiceCoverageHarness.SaleRequest(
                        CustomerId, ProductId, RegisterId,
                        total: 6m,
                        quantity: 2,
                        idempotencyKey: "golden-benefit-payment"),
                    PaymentServiceCoverageHarness.CashierId);
                Require(payment, scenario);
                operations.Add(ResultNode("CreatePayment", payment));
                break;
            }
            case "finanzonline-retry":
            {
                var finanz = new Mock<IFinanzOnlineService>();
                finanz.SetupSequence(x => x.SubmitInvoiceAsync(It.IsAny<Invoice>()))
                    .ReturnsAsync(new FinanzOnlineSubmitResponse
                    {
                        Success = false,
                        ErrorMessage = "FON timeout",
                        Status = "Failed",
                        SubmittedAt = new DateTime(2026, 1, 12, 8, 0, 0, DateTimeKind.Utc),
                        FailureKind = FinanzOnlineFailureKind.Transient
                    })
                    .ReturnsAsync(new FinanzOnlineSubmitResponse
                    {
                        Success = true,
                        ReferenceId = "FON-GOLDEN-RETRY",
                        Status = "Submitted",
                        SubmittedAt = new DateTime(2026, 1, 12, 8, 5, 0, DateTimeKind.Utc),
                        FailureKind = FinanzOnlineFailureKind.None
                    });
                var sut = Service(ctx, new PaymentServiceCoverageHarness.Options
                {
                    Inventory = EnforcedInventory(),
                    Finanz = finanz
                });
                var created = await sut.CreatePaymentAsync(
                    PaymentServiceCoverageHarness.SaleRequest(
                        CustomerId, ProductId, RegisterId, idempotencyKey: "golden-fon-sale"),
                    PaymentServiceCoverageHarness.CashierId);
                Require(created, scenario + " sale");
                var retry = await sut.RetryFinanzOnlineSubmitAsync(created.Payment!.Id);
                if (!retry.Success)
                    throw new InvalidOperationException("FinanzOnline retry failed: " + retry.ErrorMessage);
                operations.Add(ResultNode("CreatePayment", created));
                operations.Add(FinanzNode(retry));
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown golden scenario.");
        }

        var stockAfter = await StockAsync(ctx);
        return await SnapshotAsync(ctx, scenario, stockBefore, stockAfter, operations);
    }

    private static async Task<JsonObject> SnapshotAsync(
        AppDbContext ctx,
        string scenario,
        int stockBefore,
        int stockAfter,
        JsonArray operations)
    {
        var payments = await ctx.PaymentDetails.AsNoTracking().ToListAsync();
        var invoices = await ctx.Invoices.AsNoTracking().ToListAsync();
        var receipts = await ctx.Receipts.AsNoTracking().ToListAsync();
        var ledger = await ctx.VoucherLedgerEntries.AsNoTracking().ToListAsync();
        var vouchers = await ctx.Vouchers.AsNoTracking().ToListAsync();
        var usage = await ctx.BenefitDailyUsages.AsNoTracking().ToListAsync();
        var offline = await ctx.OfflineTransactions.AsNoTracking().ToListAsync();

        var root = new JsonObject();
        root["schemaVersion"] = JsonValue.Create("payment-golden-master.v1");
        root["scenario"] = JsonValue.Create(scenario);
        root["normalization"] = JsonValue.Create(
            "Timestamps, generated ids, and the date segment of AT-KASSE-01 receipt numbers are tokens.");
        var catalog = new JsonObject();
        catalog["customerId"] = JsonValue.Create(CustomerId.ToString("D"));
        catalog["productId"] = JsonValue.Create(ProductId.ToString("D"));
        catalog["cashRegisterId"] = JsonValue.Create(RegisterId.ToString("D"));
        catalog["categoryId"] = JsonValue.Create(CategoryId.ToString("D"));
        root["catalog"] = catalog;
        var stock = new JsonObject();
        stock["before"] = JsonValue.Create(stockBefore);
        stock["after"] = JsonValue.Create(stockAfter);
        root["stock"] = stock;
        root["operations"] = operations;
        root["payments"] = Rows(payments
            .OrderBy(p => p.IsStorno)
            .ThenBy(p => p.IsRefund)
            .ThenBy(p => p.TotalAmount)
            .ThenBy(p => p.ReceiptNumber, StringComparer.Ordinal));
        root["invoices"] = Rows(invoices
            .OrderBy(i => i.TotalAmount)
            .ThenBy(i => i.InvoiceNumber, StringComparer.Ordinal));
        root["receipts"] = Rows(receipts
            .OrderBy(r => r.ReceiptNumber, StringComparer.Ordinal));
        root["vouchers"] = Rows(vouchers
            .OrderBy(v => v.MaskedCode, StringComparer.Ordinal));
        root["voucherLedger"] = Rows(ledger
            .OrderBy(l => l.Type)
            .ThenBy(l => l.Amount));
        root["benefitDailyUsage"] = Rows(usage
            .OrderBy(u => u.BenefitDefinitionId)
            .ThenBy(u => u.QuantityUsed));
        root["offlineTransactions"] = Rows(offline
            .OrderBy(o => o.Status)
            .ThenBy(o => o.ServerReceivedAtUtc));
        return root;
    }

    private static async Task SeedBenefitsAsync(AppDbContext ctx)
    {
        ctx.BenefitDefinitions.Add(new BenefitDefinition
        {
            Id = PercentageBenefitId,
            Code = "PCT-20",
            Name = "Twenty percent",
            BenefitKind = AppliedBenefitKind.PercentageDiscount,
            PercentageValue = 20m
        });
        ctx.BenefitDefinitions.Add(new BenefitDefinition
        {
            Id = FreeAllowanceBenefitId,
            Code = "FREE-SPEISEN",
            Name = "Free Speisen",
            BenefitKind = AppliedBenefitKind.FreeAllowance,
            AllowanceQuantity = 1,
            AllowanceScope = "per_day",
            AllowanceCategoryId = CategoryId
        });
        ctx.BenefitAssignments.Add(new BenefitAssignment
        {
            BenefitDefinitionId = PercentageBenefitId,
            CustomerId = CustomerId,
            ValidFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Priority = 10
        });
        ctx.BenefitAssignments.Add(new BenefitAssignment
        {
            BenefitDefinitionId = FreeAllowanceBenefitId,
            CustomerId = CustomerId,
            ValidFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Priority = 1
        });
        await ctx.SaveChangesAsync();
    }

    private static PaymentService Service(AppDbContext ctx, PaymentServiceCoverageHarness.Options? options = null) =>
        PaymentServiceCoverageHarness.CreatePaymentService(ctx, options ?? new PaymentServiceCoverageHarness.Options
        {
            Inventory = EnforcedInventory()
        });

    private static InventoryOptions EnforcedInventory() => new() { EnforceStockOnSales = true };

    private static PaymentServiceCoverageHarness.Options CardOptions()
    {
        var card = new Mock<ICardPaymentService>();
        card.Setup(c => c.ValidateForFiscalPaymentAsync(
                CardIntentId, 10m, RegisterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new CardPaymentTransaction
            {
                Id = CardIntentId,
                TenantId = SystemTenantIds.Platform,
                Amount = 10m,
                Currency = "EUR",
                CashRegisterId = RegisterId,
                Status = CardPaymentTransactionStatuses.Succeeded,
                Gateway = "Mock"
            }, (string?)null, (string?)null));
        card.Setup(c => c.LinkToPaymentAsync(CardIntentId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return new PaymentServiceCoverageHarness.Options
        {
            Inventory = EnforcedInventory(),
            Card = card.Object
        };
    }

    private static PaymentServiceCoverageHarness.Options OfflineOptions()
    {
        var health = new Mock<ITseHealthMonitor>();
        health.SetupGet(m => m.Snapshot).Returns(new TseHealthSnapshot
        {
            Status = TseOperationalHealth.Offline,
            LastCheckUtc = new DateTime(2026, 1, 12, 8, 0, 0, DateTimeKind.Utc),
            ConsecutiveFailures = 5,
            LastErrorMessageSafe = "Fiskaly unreachable"
        });
        return new PaymentServiceCoverageHarness.Options
        {
            Inventory = EnforcedInventory(),
            Tse = new TseOptions { TseMode = "Device", OfflineModeEnabled = true },
            TseHealth = health.Object,
            Host = TenantTestDoubles.ProductionHostEnvironment
        };
    }

    private static async Task<int> StockAsync(AppDbContext ctx) =>
        await ctx.Products.AsNoTracking().Where(p => p.Id == ProductId).Select(p => p.StockQuantity).SingleAsync();

    private static void Require(PaymentResult result, string operation)
    {
        if (result.Success)
            return;
        throw new InvalidOperationException(
            operation + " failed: " + result.Message
            + " [" + result.DiagnosticCode + "] "
            + string.Join("; ", result.Errors));
    }

    private static JsonObject ResultNode(string operation, PaymentResult result)
    {
        var payment = result.Payment;
        var node = new JsonObject();
        node["operation"] = JsonValue.Create(operation);
        node["success"] = JsonValue.Create(result.Success);
        node["message"] = JsonValue.Create(result.Message);
        node["diagnosticCode"] = result.DiagnosticCode == null ? null : JsonValue.Create(result.DiagnosticCode);
        node["idempotentReplay"] = JsonValue.Create(result.IdempotentReplay);
        node["nonFiscalOfflineQueued"] = JsonValue.Create(result.NonFiscalOfflineQueued);
        node["isDemoFiscal"] = JsonValue.Create(result.IsDemoFiscal);
        node["tseProvider"] = result.TseProvider == null ? null : JsonValue.Create(result.TseProvider);
        node["invoicePersisted"] = JsonValue.Create(result.InvoicePersisted);
        node["isDeterministicFailure"] = JsonValue.Create(result.IsDeterministicFailure);
        node["requiresApproval"] = JsonValue.Create(result.RequiresApproval);
        node["totalAmount"] = payment == null ? null : JsonValue.Create(payment.TotalAmount);
        node["taxAmount"] = payment == null ? null : JsonValue.Create(payment.TaxAmount);
        node["paymentMethodRaw"] = payment?.PaymentMethodRaw == null ? null : JsonValue.Create(payment.PaymentMethodRaw);
        node["receiptNumber"] = payment?.ReceiptNumber == null ? null : JsonValue.Create(payment.ReceiptNumber);
        node["tseSignature"] = result.TseSignature == null ? null : JsonValue.Create(result.TseSignature);
        node["qrPayload"] = result.QrPayload == null ? null : JsonValue.Create(result.QrPayload);
        node["paymentId"] = JsonValue.Create((result.PaymentId ?? payment?.Id)?.ToString("D"));
        node["offlineTransactionId"] = result.OfflineTransactionId == null
            ? null
            : JsonValue.Create(result.OfflineTransactionId.Value.ToString("D"));
        var errors = new JsonArray();
        foreach (var error in result.Errors)
            errors.Add(JsonValue.Create(error));
        node["errors"] = errors;
        return node;
    }

    private static JsonObject PreviewNode(BenefitEligibilityPreviewResponse preview)
    {
        var node = new JsonObject();
        node["operation"] = JsonValue.Create("BenefitPreview");
        node["subtotalBeforeBenefits"] = JsonValue.Create(preview.SubtotalBeforeBenefits);
        node["totalDiscountAmount"] = JsonValue.Create(preview.TotalDiscountAmount);
        node["subtotalAfterBenefits"] = JsonValue.Create(preview.SubtotalAfterBenefits);
        var applicable = new JsonArray();
        foreach (var benefit in preview.ApplicableBenefits.OrderBy(b => b.BenefitDefinitionCode, StringComparer.Ordinal))
        {
            var row = new JsonObject();
            row["kind"] = JsonValue.Create(benefit.Kind.ToString());
            row["code"] = benefit.BenefitDefinitionCode == null ? null : JsonValue.Create(benefit.BenefitDefinitionCode);
            row["description"] = JsonValue.Create(benefit.Description);
            row["amount"] = JsonValue.Create(benefit.Amount);
            row["quantity"] = benefit.Quantity == null ? null : JsonValue.Create(benefit.Quantity.Value);
            applicable.Add(row);
        }
        node["applicable"] = applicable;
        var blocked = new JsonArray();
        foreach (var benefit in preview.BlockedBenefits.OrderBy(b => b.BenefitDefinitionCode, StringComparer.Ordinal))
        {
            var row = new JsonObject();
            row["kind"] = JsonValue.Create(benefit.Kind.ToString());
            row["code"] = benefit.BenefitDefinitionCode == null ? null : JsonValue.Create(benefit.BenefitDefinitionCode);
            row["blockedReasonCode"] = JsonValue.Create(benefit.BlockedReasonCode);
            row["message"] = benefit.Message == null ? null : JsonValue.Create(benefit.Message);
            row["requiredMoreQuantity"] = benefit.RequiredMoreQuantity == null
                ? null
                : JsonValue.Create(benefit.RequiredMoreQuantity.Value);
            blocked.Add(row);
        }
        node["blocked"] = blocked;
        return node;
    }

    private static JsonObject FinanzNode(FinanzOnlineSubmitResponse retry)
    {
        var node = new JsonObject();
        node["operation"] = JsonValue.Create("RetryFinanzOnline");
        node["success"] = JsonValue.Create(retry.Success);
        node["status"] = JsonValue.Create(retry.Status);
        node["referenceId"] = retry.ReferenceId == null ? null : JsonValue.Create(retry.ReferenceId);
        node["errorMessage"] = retry.ErrorMessage == null ? null : JsonValue.Create(retry.ErrorMessage);
        node["failureKind"] = JsonValue.Create(retry.FailureKind.ToString());
        node["submittedAt"] = JsonValue.Create(retry.SubmittedAt.ToString("o"));
        return node;
    }

    private static JsonArray Rows<T>(IEnumerable<T> entities)
    {
        var array = new JsonArray();
        foreach (var entity in entities)
            array.Add(Project(entity!));
        return array;
    }

    private static JsonObject Project(object entity)
    {
        var obj = new JsonObject();
        var props = entity.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && IsScalar(p.PropertyType))
            .OrderBy(p => p.Name, StringComparer.Ordinal);
        foreach (var prop in props)
        {
            object? value;
            try
            {
                value = prop.GetValue(entity);
            }
            catch (TargetInvocationException)
            {
                continue;
            }

            obj[prop.Name] = ToNode(value);
        }

        return obj;
    }

    private static bool IsScalar(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying.IsPrimitive
            || underlying.IsEnum
            || underlying == typeof(string)
            || underlying == typeof(decimal)
            || underlying == typeof(Guid)
            || underlying == typeof(DateTime)
            || underlying == typeof(DateTimeOffset)
            || underlying == typeof(byte[])
            || underlying == typeof(JsonDocument);
    }

    private static JsonNode? ToNode(object? value) => value switch
    {
        null => null,
        JsonDocument doc => JsonNode.Parse(doc.RootElement.GetRawText()),
        string s => JsonValue.Create(s),
        Guid g => JsonValue.Create(g.ToString("D")),
        DateTime dt => JsonValue.Create(dt.ToString("o")),
        DateTimeOffset dto => JsonValue.Create(dto.ToString("o")),
        byte[] bytes => JsonValue.Create(Convert.ToBase64String(bytes)),
        Enum e => JsonValue.Create(e.ToString()),
        bool b => JsonValue.Create(b),
        decimal d => JsonValue.Create(d),
        int i => JsonValue.Create(i),
        long l => JsonValue.Create(l),
        short s => JsonValue.Create(s),
        double d => JsonValue.Create(d),
        float f => JsonValue.Create(f),
        _ => JsonValue.Create(value.ToString())
    };

    private static string Canonicalize(JsonNode root)
    {
        var sorted = Sort(root)!;
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var next = 0;
        Alias(sorted, aliases, ref next);
        var json = sorted.ToJsonString(new JsonSerializerOptions { WriteIndented = true })
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        if (!json.EndsWith('\n'))
            json += "\n";
        return json;
    }

    private static JsonNode? Sort(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
            {
                var sorted = new JsonObject();
                foreach (var key in obj.Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal))
                    sorted[key] = Sort(Detach(obj[key]));
                return sorted;
            }
            case JsonArray array:
            {
                var sorted = new JsonArray();
                foreach (var item in array)
                    sorted.Add(Sort(Detach(item)));
                return sorted;
            }
            case JsonValue value when value.TryGetValue<string>(out var text):
            {
                var trimmed = text.Trim();
                if (trimmed.Length > 1 && (trimmed[0] == '{' || trimmed[0] == '['))
                {
                    try
                    {
                        return Sort(JsonNode.Parse(trimmed));
                    }
                    catch (JsonException)
                    {
                        return JsonValue.Create(text);
                    }
                }

                return JsonValue.Create(text);
            }
            default:
                return node == null ? null : JsonNode.Parse(node.ToJsonString());
        }
    }

    private static JsonNode? Detach(JsonNode? node) =>
        node == null ? null : JsonNode.Parse(node.ToJsonString());

    private static void Alias(JsonNode? node, Dictionary<string, string> aliases, ref int next)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(p => p.Key).ToList())
                {
                    if (obj[key] is JsonValue)
                        obj[key] = NormalizeLeaf(obj[key], aliases, ref next);
                    else
                        Alias(obj[key], aliases, ref next);
                }
                break;
            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    if (array[i] is JsonValue)
                        array[i] = NormalizeLeaf(array[i], aliases, ref next);
                    else
                        Alias(array[i], aliases, ref next);
                }
                break;
        }
    }

    private static JsonNode? NormalizeLeaf(JsonNode? node, Dictionary<string, string> aliases, ref int next)
    {
        if (node is not JsonValue value || !value.TryGetValue<string>(out var text) || text == null)
            return node;

        if (Guid.TryParseExact(text, "D", out var guid))
        {
            if (guid == Guid.Empty || guid == SystemTenantIds.Platform)
                return JsonValue.Create(guid.ToString("D"));

            var key = guid.ToString("D");
            if (!aliases.TryGetValue(key, out var alias))
            {
                next++;
                alias = "id-" + next.ToString(System.Globalization.CultureInfo.InvariantCulture);
                aliases[key] = alias;
            }

            return JsonValue.Create(alias);
        }

        if (Regex.IsMatch(
                text,
                @"^\d{4}-\d{2}-\d{2}(?:[T ]\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})?)?$",
                RegexOptions.CultureInvariant))
            return JsonValue.Create("<timestamp>");

        var receipt = Regex.Replace(
            text,
            @"AT-KASSE-01-\d{8}-(\d+)",
            "AT-KASSE-01-YYYYMMDD-$1",
            RegexOptions.CultureInvariant);
        return JsonValue.Create(receipt);
    }

    private static string SourceDirectory([CallerFilePath] string file = "") =>
        Path.Combine(Path.GetDirectoryName(file)!, "Fixtures", "PaymentGoldenMaster");
}
