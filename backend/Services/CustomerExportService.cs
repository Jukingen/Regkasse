using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using KasseAPI_Final.Constants;
using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed class CustomerExportResult
{
    public required byte[] Content { get; init; }
    public required string ContentType { get; init; }
    public required string FileName { get; init; }
    public int RowCount { get; init; }
}

public interface ICustomerExportService
{
    const int MaxExportRows = 50_000;

    Task<CustomerExportResult> ExportAsync(
        Guid tenantId,
        string? tenantSlug,
        string format,
        bool? isActive = null,
        bool excludeSystemCustomers = true,
        CancellationToken cancellationToken = default);
}

/// <summary>Customer catalog export (CSV/JSON) for the effective tenant.</summary>
public sealed class CustomerExportService : ICustomerExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    private readonly AppDbContext _context;
    private readonly IFileNamingService _fileNaming;
    private readonly ILogger<CustomerExportService> _logger;

    public CustomerExportService(
        AppDbContext context,
        IFileNamingService fileNaming,
        ILogger<CustomerExportService> logger)
    {
        _context = context;
        _fileNaming = fileNaming;
        _logger = logger;
    }

    public async Task<CustomerExportResult> ExportAsync(
        Guid tenantId,
        string? tenantSlug,
        string format,
        bool? isActive = null,
        bool excludeSystemCustomers = true,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Customers.AsNoTracking().Where(c => c.TenantId == tenantId);
        if (excludeSystemCustomers)
        {
            query = query.Where(c =>
                !c.IsSystem && c.Id != WalkInCustomerConstants.GuestCustomerId);
        }

        if (isActive.HasValue)
            query = query.Where(c => c.IsActive == isActive.Value);

        var count = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        if (count > ICustomerExportService.MaxExportRows)
        {
            throw new InvalidOperationException(
                $"Export exceeds maximum of {ICustomerExportService.MaxExportRows} rows ({count} matched). Narrow your filters.");
        }

        var rows = await query
            .OrderBy(c => c.Name)
            .Take(ICustomerExportService.MaxExportRows)
            .Select(c => new CustomerExportRow
            {
                Id = c.Id,
                Name = c.Name,
                CustomerNumber = c.CustomerNumber,
                Email = c.Email,
                Phone = c.Phone,
                Address = c.Address,
                TaxNumber = c.TaxNumber,
                Category = c.Category.ToString(),
                LoyaltyPoints = c.LoyaltyPoints,
                TotalSpent = c.TotalSpent,
                VisitCount = c.VisitCount,
                LastVisit = c.LastVisit,
                Notes = c.Notes,
                IsVip = c.IsVip,
                IsSystem = c.IsSystem,
                DiscountPercentage = c.DiscountPercentage,
                BirthDate = c.BirthDate,
                PreferredPaymentMethod = c.PreferredPaymentMethod.ToString(),
                IsActive = c.IsActive,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var normalized = CustomerExportFileNames.NormalizeExtension(format);
        var fileName = _fileNaming.GenerateFileName(
            CustomerExportFileNames.Prefix,
            normalized,
            tenantSlug: tenantSlug);
        byte[] content = normalized == "json"
            ? JsonSerializer.SerializeToUtf8Bytes(rows, JsonOptions)
            : Encoding.UTF8.GetBytes(BuildCsv(rows));

        _logger.LogInformation(
            "Customer export created. TenantId={TenantId}, Rows={Rows}, Format={Format}, FileName={FileName}",
            tenantId,
            rows.Count,
            normalized,
            fileName);

        return new CustomerExportResult
        {
            Content = content,
            ContentType = CustomerExportFileNames.ContentTypeForFormat(normalized),
            FileName = fileName,
            RowCount = rows.Count,
        };
    }

    private static string BuildCsv(IReadOnlyList<CustomerExportRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', new[]
        {
            "Id", "Name", "CustomerNumber", "Email", "Phone", "Address", "TaxNumber",
            "Category", "LoyaltyPoints", "TotalSpent", "VisitCount", "LastVisit", "Notes",
            "IsVip", "IsSystem", "DiscountPercentage", "BirthDate", "PreferredPaymentMethod",
            "IsActive", "CreatedAt", "UpdatedAt",
        }));

        foreach (var r in rows)
        {
            sb.Append(Esc(r.Id.ToString())).Append(',');
            sb.Append(Esc(r.Name)).Append(',');
            sb.Append(Esc(r.CustomerNumber)).Append(',');
            sb.Append(Esc(r.Email)).Append(',');
            sb.Append(Esc(r.Phone)).Append(',');
            sb.Append(Esc(r.Address)).Append(',');
            sb.Append(Esc(r.TaxNumber)).Append(',');
            sb.Append(Esc(r.Category)).Append(',');
            sb.Append(Esc(r.LoyaltyPoints.ToString(CultureInfo.InvariantCulture))).Append(',');
            sb.Append(Esc(r.TotalSpent.ToString(CultureInfo.InvariantCulture))).Append(',');
            sb.Append(Esc(r.VisitCount.ToString(CultureInfo.InvariantCulture))).Append(',');
            sb.Append(Esc(r.LastVisit?.ToString("o", CultureInfo.InvariantCulture))).Append(',');
            sb.Append(Esc(r.Notes)).Append(',');
            sb.Append(Esc(r.IsVip ? "true" : "false")).Append(',');
            sb.Append(Esc(r.IsSystem ? "true" : "false")).Append(',');
            sb.Append(Esc(r.DiscountPercentage.ToString(CultureInfo.InvariantCulture))).Append(',');
            sb.Append(Esc(r.BirthDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))).Append(',');
            sb.Append(Esc(r.PreferredPaymentMethod)).Append(',');
            sb.Append(Esc(r.IsActive ? "true" : "false")).Append(',');
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

    private sealed class CustomerExportRow
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string CustomerNumber { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string Phone { get; init; } = string.Empty;
        public string Address { get; init; } = string.Empty;
        public string TaxNumber { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public int LoyaltyPoints { get; init; }
        public decimal TotalSpent { get; init; }
        public int VisitCount { get; init; }
        public DateTime? LastVisit { get; init; }
        public string Notes { get; init; } = string.Empty;
        public bool IsVip { get; init; }
        public bool IsSystem { get; init; }
        public decimal DiscountPercentage { get; init; }
        public DateTime? BirthDate { get; init; }
        public string PreferredPaymentMethod { get; init; } = string.Empty;
        public bool IsActive { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }
    }
}
