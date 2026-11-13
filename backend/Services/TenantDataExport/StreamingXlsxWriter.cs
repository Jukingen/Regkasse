using System.IO.Compression;
using System.Text;

namespace KasseAPI_Final.Services;

/// <summary>
/// Writes one worksheet as an xlsx package without buffering the sheet in memory.
/// Inline strings avoid a shared-string table, so each row can be flushed as it arrives.
/// </summary>
internal static class StreamingXlsxWriter
{
    private const string ContentTypes = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
          <Default Extension="xml" ContentType="application/xml"/>
          <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
          <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
        </Types>
        """;

    private const string PackageRels = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
        </Relationships>
        """;

    private const string Workbook = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
          <sheets><sheet name="Export" sheetId="1" r:id="rId1"/></sheets>
        </workbook>
        """;

    private const string WorkbookRels = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
        </Relationships>
        """;

    public static async Task WriteAsync(
        Stream output,
        IReadOnlyList<string> headers,
        IAsyncEnumerable<string[]> rows,
        CancellationToken cancellationToken)
    {
        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        WriteStatic(zip, "[Content_Types].xml", ContentTypes);
        WriteStatic(zip, "_rels/.rels", PackageRels);
        WriteStatic(zip, "xl/workbook.xml", Workbook);
        WriteStatic(zip, "xl/_rels/workbook.xml.rels", WorkbookRels);

        var sheet = zip.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Fastest);
        await using var sheetStream = sheet.Open();
        await using var writer = new StreamWriter(sheetStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
        await writer.WriteAsync("""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>
            """).ConfigureAwait(false);

        await WriteRowAsync(writer, 1, headers, cancellationToken).ConfigureAwait(false);
        var rowIndex = 2;
        await foreach (var row in rows.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            await WriteRowAsync(writer, rowIndex, row, cancellationToken).ConfigureAwait(false);
            rowIndex++;
        }

        await writer.WriteAsync("</sheetData></worksheet>").ConfigureAwait(false);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void WriteStatic(ZipArchive zip, string name, string contents)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Fastest);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(contents);
    }

    private static async Task WriteRowAsync(
        StreamWriter writer,
        int rowIndex,
        IReadOnlyList<string> cells,
        CancellationToken cancellationToken)
    {
        await writer.WriteAsync("<row r=\"").ConfigureAwait(false);
        await writer.WriteAsync(rowIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)).ConfigureAwait(false);
        await writer.WriteAsync("\">").ConfigureAwait(false);
        for (var i = 0; i < cells.Count; i++)
        {
            var reference = ColumnName(i + 1) + rowIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await writer.WriteAsync("<c r=\"").ConfigureAwait(false);
            await writer.WriteAsync(reference).ConfigureAwait(false);
            await writer.WriteAsync("\" t=\"inlineStr\"><is><t xml:space=\"preserve\">").ConfigureAwait(false);
            await writer.WriteAsync(EscapeXml(cells[i])).ConfigureAwait(false);
            await writer.WriteAsync("</t></is></c>").ConfigureAwait(false);
        }

        await writer.WriteAsync("</row>").ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static string ColumnName(int index)
    {
        var name = string.Empty;
        var n = index;
        while (n > 0)
        {
            n--;
            name = (char)('A' + (n % 26)) + name;
            n /= 26;
        }

        return name;
    }

    private static string EscapeXml(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '&':
                    sb.Append("&amp;");
                    break;
                case '<':
                    sb.Append("&lt;");
                    break;
                case '>':
                    sb.Append("&gt;");
                    break;
                case '"':
                    sb.Append("&quot;");
                    break;
                default:
                    if (ch < 0x20 && ch != '\t')
                        continue;
                    sb.Append(ch);
                    break;
            }
        }

        return sb.ToString();
    }
}
