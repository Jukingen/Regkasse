using System.Data;
using System.Text;
using ExcelDataReader;
using KasseAPI_Final.Models.DTOs;

namespace KasseAPI_Final.Services;

/// <summary>Reads bulk user import rows from CSV or Excel (.xlsx).</summary>
public static class BulkUserImportFileParser
{
    private static readonly string[] RequiredColumns = ["email", "role", "tenantslug"];

    public static (List<BulkImportRow> Rows, string? Error) Parse(Stream stream, string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".csv" => ParseCsv(stream),
            ".xlsx" or ".xls" => ParseExcel(stream),
            _ => ([], "Unsupported file type. Use .csv or .xlsx."),
        };
    }

    public static BulkImportPreviewResponseDto BuildPreview(List<BulkImportRow> rows, string? parseError, int maxRows)
    {
        if (parseError != null)
        {
            return new BulkImportPreviewResponseDto
            {
                TotalRows = 0,
                ParseError = parseError,
            };
        }

        return new BulkImportPreviewResponseDto
        {
            TotalRows = rows.Count,
            PreviewRows = rows
                .Take(maxRows)
                .Select(r => new BulkImportPreviewRowDto
                {
                    Row = r.RowNumber,
                    Email = r.Email,
                    Username = r.Username,
                    FirstName = r.FirstName,
                    LastName = r.LastName,
                    Role = r.Role,
                    TenantSlug = r.TenantSlug,
                })
                .ToList(),
        };
    }

    private static (List<BulkImportRow>, string?) ParseCsv(Stream stream)
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
            return ([], "Missing required columns: email, role, tenantSlug.");

        var rows = new List<BulkImportRow>();
        for (var i = 1; i < lines.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            var cells = ParseCsvLine(lines[i]);
            rows.Add(MapRow(i + 1, cells, columnMap));
        }

        return (rows, null);
    }

    private static (List<BulkImportRow>, string?) ParseExcel(Stream stream)
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
            return ([], "Missing required columns: email, role, tenantSlug.");

        var rows = new List<BulkImportRow>();
        for (var i = 0; i < table.Rows.Count; i++)
        {
            var dataRow = table.Rows[i];
            if (IsEmptyRow(dataRow))
                continue;

            var cells = headers.Select(h => dataRow[h]?.ToString() ?? string.Empty).ToList();
            rows.Add(MapRow(i + 2, cells, columnMap));
        }

        return (rows, null);
    }

    private static bool IsEmptyRow(DataRow row) =>
        row.ItemArray.All(v => v == null || string.IsNullOrWhiteSpace(v.ToString()));

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
            "tenant_slug" => "tenantslug",
            "first_name" => "firstname",
            "last_name" => "lastname",
            "user_name" => "username",
            _ => h.Replace(" ", "", StringComparison.Ordinal),
        };
    }

    private static BulkImportRow MapRow(int rowNumber, IReadOnlyList<string> cells, Dictionary<string, int> map)
    {
        string Get(string key) =>
            map.TryGetValue(key, out var idx) && idx < cells.Count ? cells[idx].Trim() : string.Empty;

        return new BulkImportRow
        {
            RowNumber = rowNumber,
            Email = Get("email"),
            Username = NullIfEmpty(Get("username")),
            FirstName = NullIfEmpty(Get("firstname")),
            LastName = NullIfEmpty(Get("lastname")),
            Role = Get("role"),
            TenantSlug = Get("tenantslug"),
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
