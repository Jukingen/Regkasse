using System.Globalization;
using System.Text;

namespace KasseAPI_Final.Services;

internal static class DemoProductTemplateExporter
{
    public const string CsvHeader = "row_type,id,name,category,description,price,tax_rate,sort_order,vat_rate";

    public static string BuildCsv(DemoData data)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CsvHeader);

        foreach (var category in data.Categories.OrderBy(c => c.SortOrder))
        {
            sb.Append("category,,");
            sb.Append(Escape(category.Name));
            sb.Append(',');
            sb.Append(',');
            sb.Append(Escape(category.Description));
            sb.Append(",,,");
            sb.Append(category.SortOrder.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.AppendLine(category.VatRate.ToString("0.##", CultureInfo.InvariantCulture));
        }

        foreach (var product in data.Products.OrderBy(p => p.Category).ThenBy(p => p.Name))
        {
            sb.Append("product,");
            sb.Append(product.Id != Guid.Empty ? product.Id.ToString() : string.Empty);
            sb.Append(',');
            sb.Append(Escape(product.Name));
            sb.Append(',');
            sb.Append(Escape(product.Category));
            sb.Append(',');
            sb.Append(Escape(product.Description));
            sb.Append(',');
            sb.Append(product.Price.ToString("0.##", CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.AppendLine(product.TaxRate.ToString("0.##", CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

        return value;
    }
}
