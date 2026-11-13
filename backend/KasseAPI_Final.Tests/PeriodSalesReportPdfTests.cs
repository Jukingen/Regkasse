using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Reports;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PeriodSalesReportPdfTests
{
    [Fact]
    public async Task Aggregate_MonthWithSales_SplitsVatAndPayments_AndIgnoresSpecialReceipts()
    {
        await using var db = CreateDb();
        var registerId = Guid.NewGuid();
        db.CashRegisters.Add(Register(registerId));

        var inMonth = new DateTime(2026, 10, 15, 10, 0, 0, DateTimeKind.Utc);
        db.PaymentDetails.Add(Sale(registerId, inMonth, 120m, 20m, PaymentMethod.Cash, TaxTypes.Standard));
        db.PaymentDetails.Add(Sale(registerId, inMonth, 55m, 5m, PaymentMethod.Card, TaxTypes.Reduced));
        db.PaymentDetails.Add(new PaymentDetails
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            CustomerName = "Sonder",
            TableNumber = 0,
            CashierId = "cashier",
            TotalAmount = 0m,
            TaxAmount = 0m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            CashRegisterId = registerId,
            ReceiptNumber = "MB-1",
            TseSignature = "sig",
            RksvSpecialReceiptKind = RksvSpecialReceiptKinds.Monatsbeleg,
            RksvSpecialReceiptYear = 2026,
            RksvSpecialReceiptMonth = 10,
            CreatedAt = inMonth,
            IsActive = true,
            TaxDetails = JsonDocument.Parse("{}"),
        });
        db.PaymentDetails.Add(Sale(
            registerId,
            new DateTime(2026, 11, 2, 10, 0, 0, DateTimeKind.Utc),
            999m,
            0m,
            PaymentMethod.Cash,
            TaxTypes.ZeroRate));
        await db.SaveChangesAsync();

        var (fromUtc, toUtc) = RksvClosingPeriodHelper.MonthUtcRange(2026, 10);
        var summary = await PeriodSalesSummaryAggregator.AggregateAsync(
            db,
            registerId,
            fromUtc,
            toUtc,
            PeriodSalesSummaryAggregator.MonthLabel(2026, 10));

        Assert.Equal(2, summary.ReceiptCount);
        Assert.Equal(175m, summary.TotalRevenue);
        Assert.Equal(175m, summary.GrossAmount);
        Assert.Equal(150m, summary.NetAmount);
        Assert.Equal(20m, summary.Vat20);
        Assert.Equal(5m, summary.Vat10);
        Assert.False(summary.ZeroSales);
        Assert.Contains(summary.PaymentMethods, row => row.Label == "Bar" && row.Amount == 120m);
        Assert.Contains(summary.PaymentMethods, row => row.Label == "Karte" && row.Amount == 55m);
        Assert.StartsWith("Monat:", summary.PeriodLabel, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Aggregate_MonthWithoutSales_IsZero()
    {
        await using var db = CreateDb();
        var registerId = Guid.NewGuid();
        db.CashRegisters.Add(Register(registerId));
        await db.SaveChangesAsync();

        var (fromUtc, toUtc) = RksvClosingPeriodHelper.MonthUtcRange(2026, 10);
        var summary = await PeriodSalesSummaryAggregator.AggregateAsync(
            db,
            registerId,
            fromUtc,
            toUtc,
            PeriodSalesSummaryAggregator.MonthLabel(2026, 10));

        Assert.Equal(0, summary.ReceiptCount);
        Assert.Equal(0m, summary.TotalRevenue);
        Assert.True(summary.ZeroSales);
    }

    [Fact]
    public void MonatsbelegPdf_WithSales_IncludesPeriodAndKeepsSignature()
    {
        const string signature = "compact.jws.signature-for-monatsbeleg-pdf-test-full-value";
        var pdf = MonatsbelegPdfGenerator.Generate(new SpecialReceiptPdfData
        {
            CompanyName = "Musterfirma GmbH",
            CompanyAddress = "Hauptstrasse 1",
            CompanyVatId = "ATU12345678",
            ReceiptType = "Monatsbeleg",
            CashRegisterId = Guid.NewGuid().ToString("N"),
            RegisterNumber = "KASSE-01",
            ReceiptNumber = "MB-2026-10",
            IssuedAt = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc),
            TotalAmount = 0m,
            PaymentMethod = "Cash",
            TseSignature = signature,
            QrContent = "_R1-AT1_UNCHANGED",
            RksvFooterLabel = "RKSV-konform",
            PeriodSales = new PeriodSalesSummary
            {
                PeriodLabel = "Monat: Oktober 2026",
                ReceiptCount = 2,
                TotalRevenue = 175m,
                NetAmount = 150m,
                Vat10 = 5m,
                Vat20 = 20m,
                GrossAmount = 175m,
                PaymentMethods =
                [
                    new PeriodSalesPaymentTotal { Label = "Bar", Amount = 120m },
                    new PeriodSalesPaymentTotal { Label = "Karte", Amount = 55m },
                ],
            },
        });

        AssertPdf(pdf);
        var text = PdfGlyphText.Extract(pdf);
        AssertContainsNormalized(text, "MB-2026-10");
        AssertContainsNormalized(text, "Monat: Oktober 2026");
        AssertContainsNormalized(text, "Umsatzübersicht");
        AssertContainsNormalized(text, "Anzahl der Belege");
        AssertContainsNormalized(text, "175,00");
        AssertContainsNormalized(text, "Bar");
        AssertContainsNormalized(text, "Karte");
        AssertContainsNormalized(text, signature);
        AssertContainsNormalized(text, "TSE-Signatur");
        AssertDoesNotContainNormalized(text, PeriodSalesSummary.ZeroSalesNote);

        var withoutSummary = MonatsbelegPdfGenerator.Generate(new SpecialReceiptPdfData
        {
            CompanyName = "Musterfirma GmbH",
            CompanyAddress = "Hauptstrasse 1",
            CompanyVatId = "ATU12345678",
            ReceiptType = "Monatsbeleg",
            RegisterNumber = "KASSE-01",
            ReceiptNumber = "MB-2026-10",
            IssuedAt = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc),
            TseSignature = signature,
            QrContent = "_R1-AT1_UNCHANGED",
            RksvFooterLabel = "RKSV-konform",
        });
        AssertPdf(withoutSummary);
        Assert.True(pdf.Length > withoutSummary.Length);
    }

    [Fact]
    public void MonatsbelegPdf_WithoutSales_PrintsZeroSalesNote()
    {
        const string signature = "sig-stays-intact";
        var pdf = MonatsbelegPdfGenerator.Generate(new SpecialReceiptPdfData
        {
            CompanyName = "Firma",
            CompanyAddress = "Wien",
            CompanyVatId = "ATU11111111",
            ReceiptType = "Monatsbeleg",
            RegisterNumber = "K1",
            ReceiptNumber = "MB-EMPTY",
            IssuedAt = DateTime.UtcNow,
            TseSignature = signature,
            RksvFooterLabel = "RKSV-konform",
            PeriodSales = new PeriodSalesSummary
            {
                PeriodLabel = "Monat: Oktober 2026",
                PaymentMethods =
                [
                    new PeriodSalesPaymentTotal { Label = "Bar", Amount = 0m },
                    new PeriodSalesPaymentTotal { Label = "Karte", Amount = 0m },
                ],
            },
        });

        AssertPdf(pdf);
        var text = PdfGlyphText.Extract(pdf);
        AssertContainsNormalized(text, "MB-EMPTY");
        AssertContainsNormalized(text, "Monat: Oktober 2026");
        AssertContainsNormalized(text, PeriodSalesSummary.ZeroSalesNote);
        AssertContainsNormalized(text, signature);
        AssertContainsNormalized(text, "TSE-Signatur");
    }

    [Fact]
    public void Tagesbericht_And_Jahresbericht_Pdfs_IncludePeriodSummary()
    {
        var empty = new PeriodSalesSummary
        {
            PeriodLabel = "Tag: 09.11.2026",
            PaymentMethods =
            [
                new PeriodSalesPaymentTotal { Label = "Bar", Amount = 0m },
                new PeriodSalesPaymentTotal { Label = "Karte", Amount = 0m },
            ],
        };
        var dayPdf = TagesberichtPdfGenerator.Generate("day-1", "KASSE-01", empty);
        AssertPdf(dayPdf);
        var dayText = PdfGlyphText.Extract(dayPdf);
        AssertContainsNormalized(dayText, "Tagesbericht");
        AssertContainsNormalized(dayText, "Tag: 09.11.2026");
        AssertContainsNormalized(dayText, PeriodSalesSummary.ZeroSalesNote);

        var year = new PeriodSalesSummary
        {
            PeriodLabel = "Jahr: 2026",
            ReceiptCount = 4,
            TotalRevenue = 40m,
            NetAmount = 33.33m,
            Vat20 = 6.67m,
            GrossAmount = 40m,
            PaymentMethods =
            [
                new PeriodSalesPaymentTotal { Label = "Bar", Amount = 40m },
                new PeriodSalesPaymentTotal { Label = "Karte", Amount = 0m },
            ],
        };
        var yearPdf = JahresberichtPdfGenerator.Generate("year-1", "KASSE-01", year);
        AssertPdf(yearPdf);
        var yearText = PdfGlyphText.Extract(yearPdf);
        AssertContainsNormalized(yearText, "Jahresbericht");
        AssertContainsNormalized(yearText, "Jahr: 2026");
        AssertContainsNormalized(yearText, "Umsatzübersicht");
        AssertDoesNotContainNormalized(yearText, PeriodSalesSummary.ZeroSalesNote);
    }

    private static void AssertContainsNormalized(string text, string expected)
    {
        var collapsed = Regex.Replace(text, @"\s+", " ");
        var tight = Regex.Replace(text, @"\s+", "");
        var expectedTight = Regex.Replace(expected, @"\s+", "");
        Assert.True(
            collapsed.Contains(expected, StringComparison.Ordinal)
            || tight.Contains(expectedTight, StringComparison.Ordinal),
            $"Missing '{expected}'. Extracted: {collapsed[..Math.Min(collapsed.Length, 900)]}");
    }

    private static void AssertDoesNotContainNormalized(string text, string unexpected)
    {
        var collapsed = Regex.Replace(text, @"\s+", " ");
        Assert.DoesNotContain(unexpected, collapsed, StringComparison.Ordinal);
    }

    private static void AssertPdf(byte[] pdf)
    {
        Assert.True(pdf.Length > 400);
        Assert.Equal((byte)'%', pdf[0]);
        Assert.Equal((byte)'P', pdf[1]);
        Assert.Equal((byte)'D', pdf[2]);
        Assert.Equal((byte)'F', pdf[3]);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"PeriodSales_{Guid.NewGuid():N}")
            .Options;
        var tenantId = SystemTenantIds.Platform;
        var db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
        TenantTestDoubles.EnsurePlatformTenant(db);
        return db;
    }

    private static CashRegister Register(Guid id) => new()
    {
        TenantId = SystemTenantIds.Platform,
        Id = id,
        RegisterNumber = "KASSE-01",
        Location = "Test",
        StartingBalance = 0,
        CurrentBalance = 0,
        LastBalanceUpdate = DateTime.UtcNow,
        Status = RegisterStatus.Open,
        CreatedAt = DateTime.UtcNow,
        IsActive = true,
    };

    private static PaymentDetails Sale(
        Guid registerId,
        DateTime createdAtUtc,
        decimal gross,
        decimal tax,
        PaymentMethod method,
        int taxType) => new()
    {
        Id = Guid.NewGuid(),
        CustomerId = Guid.NewGuid(),
        CustomerName = "Gast",
        TableNumber = 1,
        CashierId = "cashier",
        TotalAmount = gross,
        TaxAmount = tax,
        PaymentMethod = method,
        Steuernummer = "ATU12345678",
        CashRegisterId = registerId,
        ReceiptNumber = $"R-{Guid.NewGuid():N}"[..12],
        TseSignature = "sale-sig",
        CreatedAt = createdAtUtc,
        IsActive = true,
        TaxDetails = JsonDocument.Parse($$"""{"{{taxType}}":{{tax.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}"""),
    };

    /// <summary>
    /// QuestPDF embeds subset fonts with Identity-H glyph ids. This reads ToUnicode CMaps
    /// and the page content streams so tests can assert the visible report text.
    /// </summary>
    private static class PdfGlyphText
    {
        public static string Extract(byte[] pdf)
        {
            var objects = ReadObjects(pdf);
            var cmapByObject = new Dictionary<int, Dictionary<int, string>>();
            var fontObjectToCmap = new Dictionary<int, int>();
            var resourceToFontObject = new Dictionary<int, int>();

            foreach (var obj in objects)
            {
                var toUnicode = Regex.Match(obj.Header, @"/ToUnicode\s+(\d+)\s+0\s+R");
                if (toUnicode.Success)
                    fontObjectToCmap[obj.Id] = int.Parse(toUnicode.Groups[1].Value);

                if (obj.Header.Contains("/Font", StringComparison.Ordinal))
                {
                    foreach (Match font in Regex.Matches(obj.Header, @"/F(\d+)\s+(\d+)\s+0\s+R"))
                        resourceToFontObject[int.Parse(font.Groups[1].Value)] = int.Parse(font.Groups[2].Value);
                }

                if (obj.Data is { Length: > 0 })
                {
                    var body = Encoding.Latin1.GetString(obj.Data);
                    if (body.Contains("beginbfchar", StringComparison.Ordinal)
                        || body.Contains("beginbfrange", StringComparison.Ordinal))
                    {
                        cmapByObject[obj.Id] = ParseCMap(body);
                    }
                }
            }

            var text = new StringBuilder();
            foreach (var obj in objects)
            {
                if (obj.Data is not { Length: > 0 })
                    continue;
                var body = Encoding.Latin1.GetString(obj.Data);
                if (!body.Contains("BT", StringComparison.Ordinal))
                    continue;
                AppendContent(body, text, resourceToFontObject, fontObjectToCmap, cmapByObject);
            }

            return text.ToString();
        }

        private static void AppendContent(
            string body,
            StringBuilder text,
            Dictionary<int, int> resourceToFontObject,
            Dictionary<int, int> fontObjectToCmap,
            Dictionary<int, Dictionary<int, string>> cmapByObject)
        {
            Dictionary<int, string>? current = null;
            foreach (Match token in Regex.Matches(
                         body,
                         @"/F(\d+)\s+[0-9.]+\s+Tf|\[(.*?)\]\s*TJ|<([0-9A-Fa-f]+)>\s*Tj|\(((?:\\.|[^\\)])*)\)\s*Tj",
                         RegexOptions.Singleline))
            {
                if (token.Groups[1].Success)
                {
                    var resource = int.Parse(token.Groups[1].Value);
                    current = null;
                    if (resourceToFontObject.TryGetValue(resource, out var fontObject)
                        && fontObjectToCmap.TryGetValue(fontObject, out var cmapId)
                        && cmapByObject.TryGetValue(cmapId, out var map))
                    {
                        current = map;
                    }

                    continue;
                }

                if (token.Groups[2].Success)
                {
                    AppendShow(token.Groups[2].Value, current, text);
                    text.Append('\n');
                    continue;
                }

                if (token.Groups[3].Success)
                {
                    AppendHex(token.Groups[3].Value, current, text);
                    text.Append('\n');
                    continue;
                }

                if (token.Groups[4].Success)
                {
                    text.Append(Unescape(token.Groups[4].Value));
                    text.Append('\n');
                }
            }
        }

        private static void AppendShow(string arrayBody, Dictionary<int, string>? map, StringBuilder text)
        {
            foreach (Match piece in Regex.Matches(arrayBody, @"<([0-9A-Fa-f]+)>|\(((?:\\.|[^\\)])*)\)"))
            {
                if (piece.Groups[1].Success)
                    AppendHex(piece.Groups[1].Value, map, text);
                else
                    text.Append(Unescape(piece.Groups[2].Value));
            }
        }

        private static void AppendHex(string hex, Dictionary<int, string>? map, StringBuilder text)
        {
            if (map is null || hex.Length < 4)
                return;

            for (var i = 0; i + 4 <= hex.Length; i += 4)
            {
                var cid = Convert.ToInt32(hex.Substring(i, 4), 16);
                if (map.TryGetValue(cid, out var glyph))
                    text.Append(glyph);
            }
        }

        private static string Unescape(string value)
        {
            var builder = new StringBuilder(value.Length);
            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] != '\\' || i + 1 >= value.Length)
                {
                    builder.Append(value[i]);
                    continue;
                }

                builder.Append(value[++i] switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    var other => other,
                });
            }

            return builder.ToString();
        }

        private static Dictionary<int, string> ParseCMap(string cmap)
        {
            var map = new Dictionary<int, string>();
            foreach (Match section in Regex.Matches(cmap, @"beginbfchar\s*(.*?)\s*endbfchar", RegexOptions.Singleline))
            {
                foreach (Match pair in Regex.Matches(section.Groups[1].Value, @"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>"))
                    map[Convert.ToInt32(pair.Groups[1].Value, 16)] = DecodeUtf16(pair.Groups[2].Value);
            }

            foreach (Match section in Regex.Matches(cmap, @"beginbfrange\s*(.*?)\s*endbfrange", RegexOptions.Singleline))
            {
                var body = section.Groups[1].Value;
                foreach (Match range in Regex.Matches(
                             body,
                             @"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>"))
                {
                    var start = Convert.ToInt32(range.Groups[1].Value, 16);
                    var end = Convert.ToInt32(range.Groups[2].Value, 16);
                    var dest = DecodeUtf16(range.Groups[3].Value);
                    for (var cid = start; cid <= end; cid++)
                        map[cid] = Offset(dest, cid - start);
                }

                foreach (Match arrayRange in Regex.Matches(
                             body,
                             @"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>\s*\[(.*?)\]",
                             RegexOptions.Singleline))
                {
                    var start = Convert.ToInt32(arrayRange.Groups[1].Value, 16);
                    var values = Regex.Matches(arrayRange.Groups[3].Value, @"<([0-9A-Fa-f]+)>");
                    for (var i = 0; i < values.Count; i++)
                        map[start + i] = DecodeUtf16(values[i].Groups[1].Value);
                }
            }

            return map;
        }

        private static string Offset(string dest, int delta)
        {
            if (delta == 0 || dest.Length == 0)
                return dest;
            var chars = dest.ToCharArray();
            chars[^1] = (char)(chars[^1] + delta);
            return new string(chars);
        }

        private static string DecodeUtf16(string hex)
        {
            if (hex.Length % 4 != 0)
                return string.Empty;
            var bytes = new byte[hex.Length / 2];
            for (var i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return Encoding.BigEndianUnicode.GetString(bytes);
        }

        private readonly record struct PdfObject(int Id, string Header, byte[]? Data);

        private static List<PdfObject> ReadObjects(byte[] pdf)
        {
            var objects = new List<PdfObject>();
            var i = 0;
            while (i < pdf.Length - 8)
            {
                var at = IndexOfAscii(pdf, " 0 obj", i);
                if (at < 0)
                    break;

                var numberEnd = at;
                var numberStart = at - 1;
                while (numberStart >= 0 && pdf[numberStart] is >= (byte)'0' and <= (byte)'9')
                    numberStart--;
                if (numberStart == numberEnd - 1
                    || (numberStart >= 0 && pdf[numberStart] is not (byte)'\n' and not (byte)'\r'))
                {
                    i = at + 6;
                    continue;
                }

                var id = int.Parse(Encoding.ASCII.GetString(pdf[(numberStart + 1)..numberEnd]));
                var headerStart = at + " 0 obj".Length;
                var windowEnd = Math.Min(pdf.Length, headerStart + 8192);
                var window = Encoding.Latin1.GetString(pdf[headerStart..windowEnd]);
                var streamRel = window.IndexOf("stream", StringComparison.Ordinal);
                var endRel = window.IndexOf("endobj", StringComparison.Ordinal);
                if (streamRel >= 0 && (endRel < 0 || streamRel < endRel))
                {
                    var header = window[..streamRel];
                    var lengthMatch = Regex.Match(header, @"/Length\s+(\d+)");
                    if (!lengthMatch.Success)
                    {
                        i = headerStart + streamRel + 6;
                        continue;
                    }

                    var dataStart = headerStart + streamRel + "stream".Length;
                    if (dataStart < pdf.Length && pdf[dataStart] == (byte)'\r')
                        dataStart++;
                    if (dataStart < pdf.Length && pdf[dataStart] == (byte)'\n')
                        dataStart++;
                    var length = int.Parse(lengthMatch.Groups[1].Value);
                    if (dataStart + length > pdf.Length)
                        break;

                    var raw = pdf[dataStart..(dataStart + length)];
                    var data = header.Contains("/FlateDecode", StringComparison.Ordinal) ? Inflate(raw) : raw;
                    objects.Add(new PdfObject(id, header, data));
                    i = dataStart + length;
                    continue;
                }

                if (endRel >= 0)
                {
                    objects.Add(new PdfObject(id, window[..endRel], null));
                    i = headerStart + endRel + "endobj".Length;
                    continue;
                }

                i = at + 6;
            }

            return objects;
        }

        private static byte[] Inflate(byte[] raw)
        {
            if (raw.Length >= 2 && raw[0] == 0x78)
            {
                try
                {
                    using var input = new MemoryStream(raw, 2, raw.Length - 2, writable: false);
                    using var deflate = new DeflateStream(input, CompressionMode.Decompress);
                    using var output = new MemoryStream();
                    deflate.CopyTo(output);
                    return output.ToArray();
                }
                catch (InvalidDataException)
                {
                    // Fall through to the zlib wrapper.
                }
            }

            using var zlibInput = new MemoryStream(raw, writable: false);
            using var zlib = new ZLibStream(zlibInput, CompressionMode.Decompress);
            using var zlibOutput = new MemoryStream();
            zlib.CopyTo(zlibOutput);
            return zlibOutput.ToArray();
        }

        private static int IndexOfAscii(byte[] pdf, string value, int start)
        {
            var pattern = Encoding.ASCII.GetBytes(value);
            for (var i = start; i <= pdf.Length - pattern.Length; i++)
            {
                var match = true;
                for (var j = 0; j < pattern.Length; j++)
                {
                    if (pdf[i + j] != pattern[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                    return i;
            }

            return -1;
        }
    }
}
