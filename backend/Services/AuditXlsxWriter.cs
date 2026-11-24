using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace KasseAPI_Final.Services;

/// <summary>Minimal Office Open XML workbook. Rows are written as inline strings, one sheet.</summary>
internal static class AuditXlsxWriter
{
    /// <summary>
    /// Rows are written one at a time into a temporary spool file, so memory stays flat for large exports.
    /// The finished workbook is then copied to <paramref name="output"/> with async writes. That keeps the
    /// zip library off the HTTP response body, where synchronous writes are not allowed.
    /// </summary>
    public static async Task WriteAsync(
        Stream output,
        IAsyncEnumerable<string[]> rows,
        CancellationToken cancellationToken,
        string sheetName = "Audit")
    {
        var spoolPath = Path.GetTempFileName();
        await using var spool = new FileStream(
            spoolPath,
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 81_920,
            FileOptions.DeleteOnClose | FileOptions.Asynchronous);

        using (var zip = new ZipArchive(spool, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteText(zip, "[Content_Types].xml", ContentTypes);
            WriteText(zip, "_rels/.rels", RootRels);
            WriteText(zip, "xl/workbook.xml", BuildWorkbook(sheetName));
            WriteText(zip, "xl/_rels/workbook.xml.rels", WorkbookRels);

            var sheetEntry = zip.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Fastest);
            await using var sheetStream = sheetEntry.Open();
            await using var sheetWriter = new StreamWriter(sheetStream, new UTF8Encoding(false));
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

        spool.Position = 0;
        await spool.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void WriteText(ZipArchive zip, string name, string contents)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(contents);
    }

    /// <summary>XML 1.0 forbids most control characters; drop them so Excel can open the file.</summary>
    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            var valid = c is '\t' or '\n' or '\r'
                || (c >= 0x20 && c <= 0xD7FF && !char.IsSurrogate(c))
                || (c >= 0xE000 && c <= 0xFFFD)
                || (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]));
            if (!valid)
                continue;

            builder.Append(c);
            if (char.IsHighSurrogate(c))
                builder.Append(value[++i]);
        }

        return System.Security.SecurityElement.Escape(builder.ToString()) ?? string.Empty;
    }

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

    private static string BuildWorkbook(string sheetName) =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
        + "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">"
        + "<sheets><sheet name=\"" + Escape(SanitizeSheetName(sheetName)) + "\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>";

    /// <summary>Excel sheet names are at most 31 characters and cannot contain <c>\ / ? * [ ] :</c>.</summary>
    private static string SanitizeSheetName(string? name)
    {
        var cleaned = new string((name ?? string.Empty).Where(c => "\\/?*[]:".IndexOf(c) < 0).ToArray()).Trim();
        if (cleaned.Length == 0)
            cleaned = "Audit";
        return cleaned.Length > 31 ? cleaned[..31] : cleaned;
    }

    private const string ContentTypes =
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>""";

    private const string RootRels =
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""";

    private const string WorkbookRels =
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>""";
}
