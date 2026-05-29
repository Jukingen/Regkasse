using System.Data;
using System.Globalization;
using System.Text;
using ExcelDataReader;

namespace KasseAPI_Final.Services;

internal sealed class DemoTemplateParsedRow
{
    public int RowNumber { get; set; }
    public string RowType { get; set; } = string.Empty;
    public string? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Description { get; set; }
    public string? PriceRaw { get; set; }
    public string? TaxRateRaw { get; set; }
    public string? SortOrderRaw { get; set; }
    public string? VatRateRaw { get; set; }
}

/// <summary>Parses demo product template CSV or Excel (.xlsx) into raw rows.</summary>
internal static class DemoProductTemplateFileParser
{
    private static readonly string[] RequiredColumns = ["row_type", "name"];

    public static (List<DemoTemplateParsedRow> Rows, string? Error) Parse(Stream stream, string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".csv" => ParseCsv(stream),
            ".xlsx" or ".xls" => ParseExcel(stream),
            _ => ([], "Unsupported file type. Use .csv or .xlsx."),
        };
    }

    private static (List<DemoTemplateParsedRow>, string?) ParseCsv(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var lines = new List<string>();
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (line != null)
                lines.Add(line);
        }

        if (lines.Count == 0)
            return ([], "File is empty.");

        var headerCells = ParseCsvLine(lines[0]);
        var columnMap = BuildColumnMap(headerCells);
        if (columnMap == null)
            return ([], "Missing required columns: row_type, name.");

        var rows = new List<DemoTemplateParsedRow>();
        for (var i = 1; i < lines.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            var cells = ParseCsvLine(lines[i]);
            rows.Add(MapRow(i + 1, cells, columnMap));
        }

        return (rows, null);
    }

    private static (List<DemoTemplateParsedRow>, string?) ParseExcel(Stream stream)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true },
        });

        if (dataSet.Tables.Count == 0)
            return ([], "Excel file has no worksheets.");

        var table = dataSet.Tables[0];
        var headers = table.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
        var columnMap = BuildColumnMap(headers);
        if (columnMap == null)
            return ([], "Missing required columns: row_type, name.");

        var rows = new List<DemoTemplateParsedRow>();
        for (var i = 0; i < table.Rows.Count; i++)
        {
            var dataRow = table.Rows[i];
            if (dataRow.ItemArray.All(v => v == null || string.IsNullOrWhiteSpace(v.ToString())))
                continue;

            var cells = headers.Select(h => dataRow[h]?.ToString() ?? string.Empty).ToList();
            rows.Add(MapRow(i + 2, cells, columnMap));
        }

        return (rows, null);
    }

    private static Dictionary<string, int>? BuildColumnMap(IReadOnlyList<string> headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Count; i++)
        {
            var key = NormalizeHeader(headers[i]);
            if (!string.IsNullOrEmpty(key) && !map.ContainsKey(key))
                map[key] = i;
        }

        foreach (var required in RequiredColumns)
        {
            if (!map.ContainsKey(required))
                return null;
        }

        return map;
    }

    private static string NormalizeHeader(string header)
    {
        var h = header.Trim().ToLowerInvariant();
        return h switch
        {
            "rowtype" => "row_type",
            "taxrate" or "tax_rate_percent" => "tax_rate",
            "sortorder" => "sort_order",
            "vatrate" => "vat_rate",
            _ => h.Replace(" ", "_", StringComparison.Ordinal),
        };
    }

    private static DemoTemplateParsedRow MapRow(int rowNumber, IReadOnlyList<string> cells, Dictionary<string, int> map)
    {
        string Get(string key) =>
            map.TryGetValue(key, out var idx) && idx < cells.Count ? cells[idx].Trim() : string.Empty;

        return new DemoTemplateParsedRow
        {
            RowNumber = rowNumber,
            RowType = Get("row_type"),
            Id = NullIfEmpty(Get("id")),
            Name = Get("name"),
            Category = NullIfEmpty(Get("category")),
            Description = NullIfEmpty(Get("description")),
            PriceRaw = NullIfEmpty(Get("price")),
            TaxRateRaw = NullIfEmpty(Get("tax_rate")),
            SortOrderRaw = NullIfEmpty(Get("sort_order")),
            VatRateRaw = NullIfEmpty(Get("vat_rate")),
        };
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        result.Add(current.ToString());
        return result;
    }
}
