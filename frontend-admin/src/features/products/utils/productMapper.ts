import { Product } from '@/api/generated/model';

// Define the shape of the raw API response item (PascalCase)
export interface ApiProduct {
    Id: string;
    Name: string;
    Description?: string | null;
    Price: number;
    ImageUrl?: string | null;
    StockQuantity?: number;
    MinStockLevel?: number;
    Unit?: string | null;
    Category?: string | null;
    TaxType: any; // Using any to avoid strict enum mismatch during mapping
    TaxRate?: number;
    IsActive?: boolean;
    Barcode?: string | null;
    Cost?: number;
    [key: string]: any; // Allow extra fields
}

export const mapApiProductToUi = (apiProduct: ApiProduct | any): Product => {
    if (!apiProduct) return {} as Product;

    return {
        id: apiProduct.Id || apiProduct.id,
        name: apiProduct.Name || apiProduct.name || '',
        description: apiProduct.Description || apiProduct.description,
        price: apiProduct.Price ?? apiProduct.price ?? 0,
        imageUrl: apiProduct.ImageUrl || apiProduct.imageUrl,
        stockQuantity: apiProduct.StockQuantity ?? apiProduct.stockQuantity ?? 0,
        minStockLevel: apiProduct.MinStockLevel ?? apiProduct.minStockLevel ?? 0,
        unit: apiProduct.Unit || apiProduct.unit,
        category: apiProduct.Category || apiProduct.category,
        taxType: apiProduct.TaxType || apiProduct.taxType,
        taxRate: apiProduct.TaxRate ?? apiProduct.taxRate ?? 0,
        isActive: apiProduct.IsActive ?? apiProduct.isActive ?? true,
        barcode: apiProduct.Barcode || apiProduct.barcode,
        cost: apiProduct.Cost ?? apiProduct.cost ?? 0,
        createdAt: apiProduct.CreatedAt || apiProduct.createdAt || new Date().toISOString(),
        updatedAt: apiProduct.UpdatedAt || apiProduct.updatedAt,
        createdBy: apiProduct.CreatedBy || apiProduct.createdBy,
        updatedBy: apiProduct.UpdatedBy || apiProduct.updatedBy,
    };
};
