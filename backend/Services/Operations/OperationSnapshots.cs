using System.Text.Json;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Operations;

public static class OperationSnapshots
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    public static T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return default;
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    public static ProductOperationSnapshot FromProduct(Product p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        NameDe = p.NameDe,
        NameEn = p.NameEn,
        NameTr = p.NameTr,
        Description = p.Description,
        DescriptionDe = p.DescriptionDe,
        DescriptionEn = p.DescriptionEn,
        DescriptionTr = p.DescriptionTr,
        Price = p.Price,
        TaxType = p.TaxType,
        TaxRate = p.TaxRate,
        CategoryId = p.CategoryId,
        Category = p.Category,
        StockQuantity = p.StockQuantity,
        MinStockLevel = p.MinStockLevel,
        Unit = p.Unit,
        Cost = p.Cost,
        Barcode = p.Barcode,
        ImageUrl = p.ImageUrl,
        IsActive = p.IsActive,
        IsFiscalCompliant = p.IsFiscalCompliant,
        IsTaxable = p.IsTaxable,
        FiscalCategoryCode = p.FiscalCategoryCode,
        TaxExemptionReason = p.TaxExemptionReason,
        RksvProductType = p.RksvProductType,
    };

    public static void ApplyProduct(Product target, ProductOperationSnapshot s)
    {
        target.Name = s.Name;
        target.NameDe = s.NameDe;
        target.NameEn = s.NameEn;
        target.NameTr = s.NameTr;
        target.Description = s.Description ?? string.Empty;
        target.DescriptionDe = s.DescriptionDe;
        target.DescriptionEn = s.DescriptionEn;
        target.DescriptionTr = s.DescriptionTr;
        target.Price = s.Price;
        target.TaxType = s.TaxType;
        target.TaxRate = s.TaxRate;
        target.CategoryId = s.CategoryId;
        target.Category = s.Category;
        target.StockQuantity = s.StockQuantity;
        target.MinStockLevel = s.MinStockLevel;
        target.Unit = s.Unit;
        target.Cost = s.Cost;
        target.Barcode = s.Barcode;
        target.ImageUrl = s.ImageUrl;
        target.IsActive = s.IsActive;
        target.IsFiscalCompliant = s.IsFiscalCompliant;
        target.IsTaxable = s.IsTaxable;
        target.FiscalCategoryCode = s.FiscalCategoryCode;
        target.TaxExemptionReason = s.TaxExemptionReason;
        target.RksvProductType = s.RksvProductType;
    }

    public static CustomerOperationSnapshot FromCustomer(Customer c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        CustomerNumber = c.CustomerNumber,
        Email = c.Email,
        Phone = c.Phone,
        Address = c.Address,
        TaxNumber = c.TaxNumber,
        Notes = c.Notes,
        IsVip = c.IsVip,
        IsActive = c.IsActive,
        DiscountPercentage = c.DiscountPercentage,
    };

    public static void ApplyCustomer(Customer target, CustomerOperationSnapshot s)
    {
        target.Name = s.Name;
        target.CustomerNumber = s.CustomerNumber;
        target.Email = s.Email;
        target.Phone = s.Phone;
        target.Address = s.Address;
        target.TaxNumber = s.TaxNumber;
        target.Notes = s.Notes;
        target.IsVip = s.IsVip;
        target.IsActive = s.IsActive;
        target.DiscountPercentage = s.DiscountPercentage;
    }

    public static CategoryOperationSnapshot FromCategory(Category c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        Key = c.Key,
        IsActive = c.IsActive,
    };

    public static VoucherOperationSnapshot FromVoucher(Voucher v) => new()
    {
        Id = v.Id,
        RemainingAmount = v.RemainingAmount,
        InitialAmount = v.InitialAmount,
        Status = (int)v.Status,
    };
}
