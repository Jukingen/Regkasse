using System.Globalization;
using System.Text.Json.Nodes;
using KasseAPI_Final.Models;
using KasseAPI_Final.Rksv;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Billing;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
using KasseAPI_Final.Services.Offline;
using KasseAPI_Final.Tests.Fixtures;
using KasseAPI_Final.Tse;
using Microsoft.Extensions.Logging.Abstractions;

namespace KasseAPI_Final.Tests.CountryBaseline;

/// <summary>
/// Deterministic snapshot of the Austrian fiscal chain as it behaves today, captured before any
/// country/tax/invoice abstraction layer exists.
///
/// Two values are irreducibly random in the live flow and therefore cannot be byte-frozen:
/// the ES256 signature segment (.NET ECDSA picks a fresh nonce per call) and, transitively,
/// Sig-Voriger-Beleg, which is SHA-256 over the <em>previous receipt's signature</em>.
/// So each step here is signed against a frozen synthetic predecessor instead of the live chain;
/// that keeps the machine code, signing input, QR wire and FinanzOnline beleg reproducible.
/// Real chaining and real signatures are asserted behaviourally in
/// <c>AtFiscalChainBaselineTests</c> rather than byte-compared.
/// </summary>
internal static class AtFiscalChainBaseline
{
    internal const string SchemaVersion = "regkasse.at-fiscal-chain-baseline.v1";

    internal const string SignaturePlaceholder = "<ES256-SIGNATURE>";

    private const string KassenId = "KASSE-BASELINE-01";
    private const string RegisterNumber = "KASSE-BASELINE-01";

    /// <summary>
    /// Fixed serial. <see cref="FixedPrueftoolTseKeyProvider"/> mints a self-signed certificate whose
    /// serial is random per construction, which would leak into the machine code.
    /// </summary>
    private const string CertificateSerialNumber = "BASELINE-AT-CERT-0001";

    private static readonly DateTime BusinessDay = new(2026, 1, 12, 0, 0, 0, DateTimeKind.Utc);

    private static readonly ChainStep[] ChainSteps =
    [
        new(
            "Startbeleg",
            "AT-KASSE-BASELINE-01-20260112-1",
            new DateTime(2026, 1, 12, 7, 0, 0, DateTimeKind.Utc),
            new RksvTaxSetAmounts(),
            TurnoverCounterCents: 0,
            FrozenPredecessorJws: null),
        new(
            "Sale-Standard20",
            "AT-KASSE-BASELINE-01-20260112-2",
            new DateTime(2026, 1, 12, 8, 15, 0, DateTimeKind.Utc),
            new RksvTaxSetAmounts { Normal = 25.00m },
            TurnoverCounterCents: 2500,
            FrozenPredecessorJws: FrozenPredecessor(1)),
        new(
            "Sale-Mixed20And10",
            "AT-KASSE-BASELINE-01-20260112-3",
            new DateTime(2026, 1, 12, 9, 30, 0, DateTimeKind.Utc),
            new RksvTaxSetAmounts { Normal = 2.50m, Ermaessigt1 = 6.90m },
            TurnoverCounterCents: 3440,
            FrozenPredecessorJws: FrozenPredecessor(2)),
        new(
            "Sale-Special13",
            "AT-KASSE-BASELINE-01-20260112-4",
            new DateTime(2026, 1, 12, 11, 0, 0, DateTimeKind.Utc),
            new RksvTaxSetAmounts { Ermaessigt2 = 113.00m },
            TurnoverCounterCents: 14740,
            FrozenPredecessorJws: FrozenPredecessor(3)),
        new(
            "Nullbeleg",
            "AT-KASSE-BASELINE-01-20260112-5",
            new DateTime(2026, 1, 12, 18, 0, 0, DateTimeKind.Utc),
            new RksvTaxSetAmounts(),
            TurnoverCounterCents: 14740,
            FrozenPredecessorJws: FrozenPredecessor(4)),
    ];

    /// <summary>
    /// Stand-in for the previous receipt's compact JWS. Only its bytes matter — Sig-Voriger-Beleg
    /// is SHA-256 over this string — so a frozen value keeps the snapshot reproducible.
    /// </summary>
    private static string FrozenPredecessor(int index) =>
        "eyJhbGciOiJFUzI1NiJ9"
        + ".X1IxLUFUMV9CQVNFTElORS1QUkVERUNFU1NPUg"
        + ".RlJPWkVOLVBSRURFQ0VTU09SLVNJR05BVFVSRS1CQVNFTElORS1PTkxZ"
        + index.ToString(CultureInfo.InvariantCulture);

    internal static BaselineCapture Capture()
    {
        var keyProvider = new FixedPrueftoolTseKeyProvider();
        var pipeline = new SignaturePipeline(keyProvider, NullLogger<SignaturePipeline>.Instance);
        var aesKey = keyProvider.GetTurnoverCounterAesKeyBytes()!;

        var signedReceipts = new List<SignedReceipt>(ChainSteps.Length);
        var chain = new JsonArray();

        foreach (var step in ChainSteps)
        {
            var payload = BelegdatenPayloadBuilder.Build(
                KassenId,
                step.Belegnummer,
                step.IssuedAtUtc,
                step.TaxSets,
                step.TurnoverCounterCents,
                step.FrozenPredecessorJws,
                CertificateSerialNumber,
                aesKey);

            var machineCode = SignaturePipeline.GetMachineCode(payload);
            var compactJws = pipeline.Sign(payload, correlationId: "at-baseline");
            var segments = compactJws.Split('.');
            var signingInput = $"{segments[0]}.{segments[1]}";

            if (!RksvReceiptQrPayloadBuilder.TryBuildFromCompactJws(compactJws, out var qrWire))
                throw new InvalidOperationException($"QR wire format could not be built for '{step.Name}'.");

            if (!RksvFinanzOnlineBelegMapper.TryResolveBeleg(qrWire, out var finanzOnlineBeleg, out var belegError))
                throw new InvalidOperationException($"FinanzOnline beleg mapping failed for '{step.Name}': {belegError}");

            signedReceipts.Add(new SignedReceipt(step.Name, compactJws, signingInput, segments[2]));

            chain.Add(new JsonObject
            {
                ["step"] = step.Name,
                ["belegnummer"] = payload.Belegnummer,
                ["belegDatumUhrzeitLocal"] = payload.BelegDatumUhrzeit,
                ["taxSets"] = new JsonObject
                {
                    ["normal20"] = Money(payload.BetragSatzNormal),
                    ["ermaessigt1_10"] = Money(payload.BetragSatzErmaessigt1),
                    ["ermaessigt2_13"] = Money(payload.BetragSatzErmaessigt2),
                    ["null0"] = Money(payload.BetragSatzNull),
                    ["besonders"] = Money(payload.BetragSatzBesonders),
                },
                ["turnoverCounterCents"] = step.TurnoverCounterCents,
                ["encryptedTurnoverCounterAes256Icm"] = payload.StandUmsatzZaehlerAes256Icm,
                ["certificateSerialNumber"] = payload.ZertifikatSeriennummer,
                ["frozenPredecessorJws"] = step.FrozenPredecessorJws,
                ["sigVorigerBeleg"] = payload.SigVorigerBeleg,
                ["machineCode"] = machineCode,
                ["jwsSigningInput"] = signingInput,
                ["qrWireFormat"] = MaskSignature(qrWire, compactJws, signingInput),
                ["finanzOnlineBeleg"] = finanzOnlineBeleg,
            });
        }

        var snapshot = new JsonObject
        {
            ["schemaVersion"] = SchemaVersion,
            ["description"] =
                "Austrian fiscal chain baseline captured before the country/tax/invoice layer exists. "
                + "ES256 signature segments are masked because .NET ECDSA is non-deterministic; "
                + "they are verified cryptographically instead of byte-compared.",
            ["fixedInputs"] = new JsonObject
            {
                ["kassenId"] = KassenId,
                ["registerNumber"] = RegisterNumber,
                ["certificateSerialNumber"] = CertificateSerialNumber,
                ["businessDayUtc"] = BusinessDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["rksvSuiteId"] = RksvSuite.SuiteId,
            },
            ["vatCalculation"] = BuildVatCalculation(),
            ["rksvTaxSetMapping"] = BuildTaxSetMapping(),
            ["fiscalNumbering"] = BuildFiscalNumbering(),
            ["billingNumbering"] = BuildBillingNumbering(),
            ["signatureChain"] = chain,
        };

        return new BaselineCapture(snapshot, signedReceipts, keyProvider);
    }

    /// <summary>
    /// Signs the same receipts as a genuine sequential chain (each receipt linked to the real
    /// predecessor signature). Not byte-comparable — used to assert chaining behaviour.
    /// </summary>
    internal static IReadOnlyList<LiveChainLink> BuildLiveChain()
    {
        var keyProvider = new FixedPrueftoolTseKeyProvider();
        var pipeline = new SignaturePipeline(keyProvider, NullLogger<SignaturePipeline>.Instance);
        var aesKey = keyProvider.GetTurnoverCounterAesKeyBytes()!;

        var links = new List<LiveChainLink>(ChainSteps.Length);
        string? previousJws = null;

        foreach (var step in ChainSteps)
        {
            var payload = BelegdatenPayloadBuilder.Build(
                KassenId,
                step.Belegnummer,
                step.IssuedAtUtc,
                step.TaxSets,
                step.TurnoverCounterCents,
                previousJws,
                CertificateSerialNumber,
                aesKey);

            var compactJws = pipeline.Sign(payload, correlationId: "at-baseline-live");
            links.Add(new LiveChainLink(step.Name, previousJws, payload.SigVorigerBeleg, compactJws));
            previousJws = compactJws;
        }

        return links;
    }

    internal static string KassenIdForChaining => KassenId;

    /// <summary>VAT math behind every AT receipt line (<see cref="CartMoneyHelper"/>).</summary>
    private static JsonArray BuildVatCalculation()
    {
        var cases = new (string Name, decimal UnitGross, int Quantity, decimal VatPercent)[]
        {
            ("standard-20-single", 2.50m, 1, 20m),
            ("standard-20-qty3", 2.50m, 3, 20m),
            ("reduced-10-single", 6.90m, 1, 10m),
            ("reduced-10-qty2", 6.90m, 2, 10m),
            ("special-13-single", 113.00m, 1, 13m),
            ("zero-rate-single", 4.00m, 1, 0m),
            ("rounding-edge-099-qty3", 0.99m, 3, 20m),
            ("rounding-edge-001", 0.01m, 1, 20m),
        };

        var result = new JsonArray();
        foreach (var (name, unitGross, quantity, vatPercent) in cases)
        {
            var line = CartMoneyHelper.ComputeLine(unitGross, quantity, vatPercent);
            var (totals, breakdown) = CartMoneyHelper.BuildReceiptTotalsAndBreakdown([line]);

            var buckets = new JsonArray();
            foreach (var bucket in breakdown)
            {
                buckets.Add(new JsonObject
                {
                    ["vatRatePercent"] = Money(bucket.VatRatePercent),
                    ["netAmount"] = Money(bucket.NetAmount),
                    ["vatAmount"] = Money(bucket.VatAmount),
                    ["grossAmount"] = Money(bucket.GrossAmount),
                });
            }

            result.Add(new JsonObject
            {
                ["case"] = name,
                ["unitGross"] = Money(unitGross),
                ["quantity"] = quantity,
                ["vatRatePercent"] = Money(vatPercent),
                ["lineNet"] = Money(line.LineNet),
                ["lineTax"] = Money(line.LineTax),
                ["lineGross"] = Money(line.LineGross),
                ["receiptTotalNet"] = Money(totals.TotalNet),
                ["receiptTotalVat"] = Money(totals.TotalVat),
                ["receiptTotalGross"] = Money(totals.TotalGross),
                ["breakdown"] = buckets,
            });
        }

        // Mixed-rate basket: the shape the country layer would have to re-derive per regime.
        var mixedLines = new[]
        {
            CartMoneyHelper.ComputeLine(6.90m, 1, 10m),
            CartMoneyHelper.ComputeLine(2.50m, 2, 20m),
            CartMoneyHelper.ComputeLine(113.00m, 1, 13m),
            CartMoneyHelper.ComputeLine(4.00m, 1, 0m),
        };
        var (mixedTotals, mixedBreakdown) = CartMoneyHelper.BuildReceiptTotalsAndBreakdown([.. mixedLines]);

        var mixedBuckets = new JsonArray();
        foreach (var bucket in mixedBreakdown)
        {
            mixedBuckets.Add(new JsonObject
            {
                ["vatRatePercent"] = Money(bucket.VatRatePercent),
                ["netAmount"] = Money(bucket.NetAmount),
                ["vatAmount"] = Money(bucket.VatAmount),
                ["grossAmount"] = Money(bucket.GrossAmount),
            });
        }

        result.Add(new JsonObject
        {
            ["case"] = "mixed-basket-20-10-13-0",
            ["receiptTotalNet"] = Money(mixedTotals.TotalNet),
            ["receiptTotalVat"] = Money(mixedTotals.TotalVat),
            ["receiptTotalGross"] = Money(mixedTotals.TotalGross),
            ["breakdown"] = mixedBuckets,
        });

        return result;
    }

    /// <summary>Austrian five-bucket projection (<see cref="RksvTaxSetMapper"/>) — the most country-specific step.</summary>
    private static JsonArray BuildTaxSetMapping()
    {
        var cases = new (string Name, string? TaxDetailsJson, decimal TotalAmount)[]
        {
            ("standard-only", $$"""{"{{TaxTypes.Standard}}":5.00}""", 30.00m),
            ("reduced-only", $$"""{"{{TaxTypes.Reduced}}":0.63}""", 6.90m),
            ("special-only", $$"""{"{{TaxTypes.Special}}":13.00}""", 113.00m),
            ("zero-rate-only", $$"""{"{{TaxTypes.ZeroRate}}":0.00}""", 4.00m),
            ("reduced-new-4_9", $$"""{"{{TaxTypes.ReducedNew}}":0.49}""", 10.49m),
            ("mixed-standard-reduced", $$"""{"{{TaxTypes.Standard}}":5.00,"{{TaxTypes.Reduced}}":0.63}""", 36.90m),
            ("empty-object-falls-back-to-normal", "{}", 42.00m),
            ("null-json-falls-back-to-normal", null, 42.00m),
            ("malformed-json-falls-back-to-normal", "not-json", 42.00m),
            ("zero-total-stays-zero", "{}", 0.00m),
        };

        var result = new JsonArray();
        foreach (var (name, taxDetailsJson, totalAmount) in cases)
        {
            var amounts = RksvTaxSetMapper.MapFromTaxDetailsJson(taxDetailsJson, totalAmount);
            result.Add(new JsonObject
            {
                ["case"] = name,
                ["taxDetailsJson"] = taxDetailsJson,
                ["totalAmount"] = Money(totalAmount),
                ["normal20"] = Money(amounts.Normal),
                ["ermaessigt1_10"] = Money(amounts.Ermaessigt1),
                ["ermaessigt2_13"] = Money(amounts.Ermaessigt2),
                ["null0"] = Money(amounts.Null),
                ["besonders"] = Money(amounts.Besonders),
                ["totalGross"] = Money(amounts.TotalGross),
                ["totalGrossCents"] = amounts.TotalGrossCents,
            });
        }

        return result;
    }

    /// <summary>RKSV BelegNr grammar: <c>AT-{register}-{yyyyMMdd}-{sequence}</c>.</summary>
    private static JsonArray BuildFiscalNumbering()
    {
        var cases = new (string Register, DateTime Date, int Sequence)[]
        {
            (RegisterNumber, BusinessDay, 1),
            (RegisterNumber, BusinessDay, 42),
            (RegisterNumber, BusinessDay, 9999),
            ("KASSE-01", new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc), 1),
            ("KASSE-01", new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc), 1),
        };

        var result = new JsonArray();
        foreach (var (register, date, sequence) in cases)
        {
            result.Add(new JsonObject
            {
                ["registerNumber"] = register,
                ["sequenceDate"] = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["sequence"] = sequence,
                ["belegNr"] = SequenceReservationService.FormatBelegNr(register, date, sequence),
            });
        }

        return result;
    }

    /// <summary>Super Admin license billing numbers: <c>RE{yyyy}{MM}{sequence}</c>. Not fiscal, but country-adjacent.</summary>
    private static JsonArray BuildBillingNumbering()
    {
        var cases = new (int Year, int Month, int Sequence)[]
        {
            (2026, 1, 1),
            (2026, 8, 41),
            (2026, 12, 999),
            (2027, 1, 1),
        };

        var result = new JsonArray();
        foreach (var (year, month, sequence) in cases)
        {
            result.Add(new JsonObject
            {
                ["year"] = year,
                ["month"] = month,
                ["sequence"] = sequence,
                ["prefix"] = InvoiceNumberGenerator.FormatPrefix(year, month),
                ["invoiceNumber"] = InvoiceNumberGenerator.FormatInvoiceNumber(year, month, sequence),
            });
        }

        return result;
    }

    /// <summary>Replaces the non-reproducible ES256 segment so the QR wire format itself stays comparable.</summary>
    private static string MaskSignature(string qrWire, string compactJws, string signingInput)
    {
        if (qrWire.Contains(compactJws, StringComparison.Ordinal))
            return qrWire.Replace(compactJws, $"{signingInput}.{SignaturePlaceholder}", StringComparison.Ordinal);

        var segments = compactJws.Split('.');
        if (segments.Length == 3)
        {
            var std = Convert.ToBase64String(TseCryptoHelper.FromBase64UrlNoPadding(segments[2]));
            var suffix = "_" + std;
            if (qrWire.EndsWith(suffix, StringComparison.Ordinal))
                return qrWire[..^std.Length] + SignaturePlaceholder;
        }

        return qrWire;
    }

    private static string Money(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    private sealed record ChainStep(
        string Name,
        string Belegnummer,
        DateTime IssuedAtUtc,
        RksvTaxSetAmounts TaxSets,
        long TurnoverCounterCents,
        string? FrozenPredecessorJws);

    internal sealed record SignedReceipt(
        string Step,
        string CompactJws,
        string SigningInput,
        string SignatureSegment);

    internal sealed record LiveChainLink(
        string Step,
        string? PredecessorJws,
        string SigVorigerBeleg,
        string CompactJws);

    internal sealed record BaselineCapture(
        JsonObject Snapshot,
        IReadOnlyList<SignedReceipt> SignedReceipts,
        FixedPrueftoolTseKeyProvider KeyProvider);
}
