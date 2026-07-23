using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed class ProductExportResult
{
    public required byte[] Content { get; init; }
    public required string ContentType { get; init; }
    public required string FileName { get; init; }
    public int RowCount { get; init; }
}

public interface IProductExportService
{
    const int MaxExportRows = 50_000;

    Task<ProductExportResult> ExportAsync(
        Guid tenantId,
        string? tenantSlug,
        string format,
        bool? isActive = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Admin product catalog export (CSV/JSON).</summary>
public sealed class ProductExportService : IProductExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    private readonly AppDbContext _context;
    private readonly IFileNamingService _fileNaming;
    private readonly ILogger<ProductExportService> _logger;

    public ProductExportService(
        AppDbContext context,
        IFileNamingService fileNaming,
        ILogger<ProductExportService> logger)
    {
        _context = context;
        _fileNaming = fileNaming;
        _logger = logger;
    }

    public async Task<ProductExportResult> ExportAsync(
        Guid tenantId,
        string? tenantSlug,
        string format,
        bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Products.AsNoTracking().Where(p => p.TenantId == tenantId);
        if (isActive.HasValue)
            query = query.Where(p => p.IsActive == isActive.Value);

        var count = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        if (count > IProductExportService.MaxExportRows)
        {
            throw new InvalidOperationException(
                $"Export exceeds maximum of {IProductExportService.MaxExportRows} rows ({count} matched). Narrow your filters.");
        }

        var rows = await query
            .OrderBy(p => p.Name)
            .Take(IProductExportService.MaxExportRows)
            .Select(p => new ProductExportRow
            {
                Id = p.Id,
                Name = p.Name,
                NameDe = p.NameDe,
                NameEn = p.NameEn,
                NameTr = p.NameTr,
                Price = p.Price,
                Cost = p.Cost,
                TaxType = p.TaxType,
                TaxRate = p.TaxRate,
                Barcode = p.Barcode,
                Category = p.Category,
                CategoryId = p.CategoryId,
                Unit = p.Unit,
                StockQuantity = p.StockQuantity,
                MinStockLevel = p.MinStockLevel,
                MaxStockLevel = p.MaxStockLevel,
                IsActive = p.IsActive,
                IsTaxable = p.IsTaxable,
                IsFiscalCompliant = p.IsFiscalCompliant,
                FiscalCategoryCode = p.FiscalCategoryCode,
                RksvProductType = p.RksvProductType,
                IsSellableAddOn = p.IsSellableAddOn,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var normalized = ProductExportFileNames.NormalizeExtension(format);
        var fileName = _fileNaming.GenerateFileName(
            ProductExportFileNames.Prefix,
            normalized,
            tenantSlug: tenantSlug);
        byte[] content = normalized == "json"
            ? JsonSerializer.SerializeToUtf8Bytes(rows, JsonOptions)
            : Encoding.UTF8.GetBytes(BuildCsv(rows));

        _logger.LogInformation(
            "Product export created. TenantId={TenantId}, Rows={Rows}, Format={Format}, FileName={FileName}",
            tenantId,
            rows.Count,
            normalized,
            fileName);

        return new ProductExportResult
        {
            Content = content,
            ContentType = ProductExportFileNames.ContentTypeForFormat(normalized),
            FileName = fileName,
            RowCount = rows.Count,
        };
    }

    private static string BuildCsv(IReadOnlyList<ProductExportRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', new[]
        {
            "Id", "Name", "NameDe", "NameEn", "NameTr", "Price", "Cost", "TaxType", "TaxRate",
            "Barcode", "Category", "CategoryId", "Unit", "StockQuantity", "MinStockLevel",
            "MaxStockLevel", "IsActive", "IsTaxable", "IsFiscalCompliant", "FiscalCategoryCode",
            "RksvProductType", "IsSellableAddOn", "CreatedAt", "UpdatedAt",
        }));

        foreach (var r in rows)
        {
            sb.Append(Esc(r.Id.ToString())).Append(',');
            sb.Append(Esc(r.Name)).Append(',');
            sb.Append(Esc(r.NameDe)).Append(',');
            sb.Append(Esc(r.NameEn)).Append(',');
            sb.Append(Esc(r.NameTr)).Append(',');
            sb.Append(Esc(r.Price.ToString(CultureInfo.InvariantCulture))).Append(',');
            sb.Append(Esc(r.Cost.ToString(CultureInfo.InvariantCulture))).Append(',');
            sb.Append(Esc(r.TaxType.ToString(CultureInfo.InvariantCulture))).Append(',');
            sb.Append(Esc(r.TaxRate.ToString(CultureInfo.InvariantCulture))).Append(',');
            sb.Append(Esc(r.Barcode)).Append(',');
            sb.Append(Esc(r.Category)).Append(',');
            sb.Append(Esc(r.CategoryId.ToString())).Append(',');
            sb.Append(Esc(r.Unit)).Append(',');
            sb.Append(Esc(r.StockQuantity.ToString(CultureInfo.InvariantCulture))).Append(',');
            sb.Append(Esc(r.MinStockLevel.ToString(CultureInfo.InvariantCulture))).Append(',');
            sb.Append(Esc(r.MaxStockLevel?.ToString(CultureInfo.InvariantCulture))).Append(',');
            sb.Append(Esc(r.IsActive ? "true" : "false")).Append(',');
            sb.Append(Esc(r.IsTaxable ? "true" : "false")).Append(',');
            sb.Append(Esc(r.IsFiscalCompliant ? "true" : "false")).Append(',');
            sb.Append(Esc(r.FiscalCategoryCode)).Append(',');
            sb.Append(Esc(r.RksvProductType)).Append(',');
            sb.Append(Esc(r.IsSellableAddOn ? "true" : "false")).Append(',');
            sb.Append(Esc(r.CreatedAt.ToString("o", CultureInfo.InvariantCulture))).Append(',');
            sb.Append(Esc(r.UpdatedAt?.ToString("o", CultureInfo.InvariantCulture)));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string Esc(string? v)
    {
        if (string.IsNullOrEmpty(v))
            return "";
        var s = v.Replace("\"", "\"\"", StringComparison.Ordinal);
        return $"\"{s}\"";
    }

    private sealed class ProductExportRow
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? NameDe { get; init; }
        public string? NameEn { get; init; }
        public string? NameTr { get; init; }
        public decimal Price { get; init; }
        public decimal Cost { get; init; }
        public int TaxType { get; init; }
        public decimal TaxRate { get; init; }
        public string Barcode { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public Guid CategoryId { get; init; }
        public string Unit { get; init; } = string.Empty;
        public int StockQuantity { get; init; }
        public int MinStockLevel { get; init; }
        public int? MaxStockLevel { get; init; }
        public bool IsActive { get; init; }
        public bool IsTaxable { get; init; }
        public bool IsFiscalCompliant { get; init; }
        public string? FiscalCategoryCode { get; init; }
        public string RksvProductType { get; init; } = "Standard";
        public bool IsSellableAddOn { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }
    }
}
