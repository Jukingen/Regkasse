using System.Globalization;
using KasseAPI_Final.Models.DTOs;

namespace KasseAPI_Final.Services;

internal static class DemoProductTemplateValidator
{
    private static readonly decimal[] AllowedTaxRates = [0m, 10m, 13m, 20m];

    public static DemoTemplateValidationResultDto Validate(
        List<DemoTemplateParsedRow> rows,
        string? parseError,
        int maxPreviewRows)
    {
        if (parseError != null)
        {
            return new DemoTemplateValidationResultDto
            {
                IsValid = false,
                ParseError = parseError,
            };
        }

        var issues = new List<DemoTemplateValidationIssueDto>();
        var categories = new Dictionary<string, DemoCategory>(StringComparer.Ordinal);
        var products = new List<DemoProduct>();
        var productKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var rowType = row.RowType.Trim().ToLowerInvariant();
            if (rowType is not ("category" or "product"))
            {
                issues.Add(Error(row.RowNumber, $"Unknown row_type '{row.RowType}'. Use 'category' or 'product'."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.Name))
            {
                issues.Add(Error(row.RowNumber, "Name is required."));
                continue;
            }

            if (rowType == "category")
            {
                if (!TryParseInt(row.SortOrderRaw, out var sortOrder))
                    sortOrder = categories.Count + 1;

                if (!TryParseDecimal(row.VatRateRaw, out var vatRate))
                    vatRate = 10m;

                categories[row.Name.Trim()] = new DemoCategory
                {
                    Name = row.Name.Trim(),
                    Description = row.Description,
                    SortOrder = sortOrder,
                    VatRate = vatRate,
                };
                continue;
            }

            // product row
            var categoryName = row.Category?.Trim();
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                issues.Add(Error(row.RowNumber, "Product row requires a category."));
                continue;
            }

            if (!TryParseDecimal(row.PriceRaw, out var price) || price < 0)
            {
                issues.Add(Error(row.RowNumber, "Product price must be a non-negative number."));
                continue;
            }

            if (!TryParseDecimal(row.TaxRateRaw, out var taxRate))
                taxRate = 10m;

            if (!AllowedTaxRates.Contains(taxRate))
            {
                issues.Add(Error(row.RowNumber, $"Tax rate must be one of: {string.Join(", ", AllowedTaxRates)}."));
                continue;
            }

            var productKey = $"{categoryName}\0{row.Name.Trim()}";
            if (!productKeys.Add(productKey))
                issues.Add(Warning(row.RowNumber, $"Duplicate product name '{row.Name}' in category '{categoryName}'."));

            Guid productId = Guid.Empty;
            if (!string.IsNullOrWhiteSpace(row.Id) && !Guid.TryParse(row.Id, out productId))
                issues.Add(Warning(row.RowNumber, $"Invalid product id '{row.Id}'; a new id will be assigned on import."));

            products.Add(new DemoProduct
            {
                Id = productId,
                Name = row.Name.Trim(),
                Category = categoryName,
                Description = row.Description,
                Price = price,
                TaxRate = taxRate,
            });
        }

        if (products.Count == 0)
            issues.Add(Error(null, "At least one product row is required."));

        // Auto-create categories referenced by products
        var nextSort = categories.Count > 0 ? categories.Values.Max(c => c.SortOrder) + 1 : 1;
        foreach (var product in products)
        {
            if (categories.ContainsKey(product.Category))
                continue;

            categories[product.Category] = new DemoCategory
            {
                Name = product.Category,
                Description = null,
                SortOrder = nextSort++,
                VatRate = product.TaxRate,
            };
        }

        var previewRows = rows
            .Take(Math.Clamp(maxPreviewRows, 1, 100))
            .Select(r => new DemoTemplatePreviewRowDto
            {
                Row = r.RowNumber,
                RowType = r.RowType,
                Name = r.Name,
                Category = r.Category,
                Description = r.Description,
                Price = TryParseDecimal(r.PriceRaw, out var p) ? p : null,
                TaxRate = TryParseDecimal(r.TaxRateRaw, out var t) ? t : null,
                SortOrder = TryParseInt(r.SortOrderRaw, out var s) ? s : null,
                VatRate = TryParseDecimal(r.VatRateRaw, out var v) ? v : null,
            })
            .ToList();

        var hasErrors = issues.Any(i => i.Severity == "error");

        return new DemoTemplateValidationResultDto
        {
            IsValid = !hasErrors,
            CategoryCount = categories.Count,
            ProductCount = products.Count,
            TotalRows = rows.Count,
            Issues = issues,
            PreviewRows = previewRows,
        };
    }

    public static (DemoData? Data, string? Error) BuildDemoData(List<DemoTemplateParsedRow> rows)
    {
        var validation = Validate(rows, parseError: null, maxPreviewRows: int.MaxValue);
        if (!validation.IsValid)
            return (null, validation.Issues.FirstOrDefault(i => i.Severity == "error")?.Message ?? "Validation failed.");

        var categories = new Dictionary<string, DemoCategory>(StringComparer.Ordinal);
        var products = new List<DemoProduct>();

        foreach (var row in rows)
        {
            var rowType = row.RowType.Trim().ToLowerInvariant();
            if (rowType == "category")
            {
                if (!TryParseInt(row.SortOrderRaw, out var sortOrder))
                    sortOrder = categories.Count + 1;
                if (!TryParseDecimal(row.VatRateRaw, out var vatRate))
                    vatRate = 10m;

                categories[row.Name.Trim()] = new DemoCategory
                {
                    Name = row.Name.Trim(),
                    Description = row.Description,
                    SortOrder = sortOrder,
                    VatRate = vatRate,
                };
            }
        }

        foreach (var row in rows)
        {
            if (!string.Equals(row.RowType.Trim(), "product", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!TryParseDecimal(row.PriceRaw, out var price))
                continue;
            if (!TryParseDecimal(row.TaxRateRaw, out var taxRate))
                taxRate = 10m;

            var categoryName = row.Category!.Trim();
            if (!categories.ContainsKey(categoryName))
            {
                categories[categoryName] = new DemoCategory
                {
                    Name = categoryName,
                    SortOrder = categories.Count + 1,
                    VatRate = taxRate,
                };
            }

            Guid.TryParse(row.Id, out var id);
            products.Add(new DemoProduct
            {
                Id = id,
                Name = row.Name.Trim(),
                Category = categoryName,
                Description = row.Description,
                Price = price,
                TaxRate = taxRate,
            });
        }

        var data = new DemoData
        {
            Categories = categories.Values.OrderBy(c => c.SortOrder).ToList(),
            Products = products,
        };
        DemoProductImportFilter.NormalizeDemoProductIds(data);
        return (data, null);
    }

    private static DemoTemplateValidationIssueDto Error(int? row, string message) =>
        new() { Row = row, Severity = "error", Message = message };

    private static DemoTemplateValidationIssueDto Warning(int row, string message) =>
        new() { Row = row, Severity = "warning", Message = message };

    private static bool TryParseDecimal(string? raw, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var normalized = raw.Trim().Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseInt(string? raw, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(raw))
            return false;
        return int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
