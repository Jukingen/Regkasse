using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace KasseAPI_Final.Services;

/// <summary>Minimal Office Open XML workbook. Rows are written as inline strings, one sheet.</summary>
internal static class AuditXlsxWriter
{
    public static async Task WriteAsync(
        Stream output,
        IAsyncEnumerable<string[]> rows,
        CancellationToken cancellationToken)
    {
        var sheet = new MemoryStream();
        await using (var sheetWriter = new StreamWriter(sheet, new UTF8Encoding(false), leaveOpen: true))
        {
            await sheetWriter.WriteAsync(
                """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>""")
                .ConfigureAwait(false);

            var rowIndex = 1;
            await foreach (var row in rows.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                await sheetWriter.WriteAsync("<row r=\"" + rowIndex.ToString(CultureInfo.InvariantCulture) + "\">").ConfigureAwait(false);
                for (var column = 0; column < row.Length; column++)
                {
                    var reference = ColumnName(column) + rowIndex.ToString(CultureInfo.InvariantCulture);
                    await sheetWriter.WriteAsync(
                        $"<c r=\"{reference}\" t=\"inlineStr\"><is><t>{Escape(row[column])}</t></is></c>")
                        .ConfigureAwait(false);
                }

                await sheetWriter.WriteAsync("</row>").ConfigureAwait(false);
                rowIndex++;
            }

            await sheetWriter.WriteAsync("</sheetData></worksheet>").ConfigureAwait(false);
            await sheetWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        sheet.Position = 0;
        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        WriteText(zip, "[Content_Types].xml", ContentTypes);
        WriteText(zip, "_rels/.rels", RootRels);
        WriteText(zip, "xl/workbook.xml", Workbook);
        WriteText(zip, "xl/_rels/workbook.xml.rels", WorkbookRels);
        var sheetEntry = zip.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Fastest);
        await using var sheetEntryStream = sheetEntry.Open();
        await sheet.CopyToAsync(sheetEntryStream, cancellationToken).ConfigureAwait(false);
    }

    private static void WriteText(ZipArchive zip, string name, string contents)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(contents);
    }

    private static string Escape(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : System.Security.SecurityElement.Escape(value) ?? string.Empty;

    private static string ColumnName(int index)
    {
        var name = string.Empty;
        var current = index;
        do
        {
            name = (char)('A' + (current % 26)) + name;
            current = current / 26 - 1;
        }
        while (current >= 0);
        return name;
    }

    private const string ContentTypes =
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>""";

    private const string RootRels =
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""";

    private const string Workbook =
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Audit" sheetId="1" r:id="rId1"/></sheets></workbook>""";

    private const string WorkbookRels =
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>""";
}
