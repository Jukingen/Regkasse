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

// Helper to map backend string TaxType to frontend number (TaxRate)
const mapTaxTypeToRate = (type: string): number => {
    switch (type) {
        case 'Reduced': return 10;
        case 'Special': return 13;
        case 'Standard': return 20;
        case 'Exempt': return 0;
        default: return 20;
    }
};

// Helper to map frontend number (TaxRate) to backend string TaxType
const mapRateToTaxType = (rate: number): string => {
    switch (rate) {
        case 10: return 'Reduced';
        case 13: return 'Special';
        case 20: return 'Standard';
        case 0: return 'Exempt';
        default: return 'Standard';
    }
};

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
        taxType: apiProduct.TaxType || apiProduct.taxType, // Keep raw if needed, or map
        taxRate: apiProduct.TaxRate ?? apiProduct.taxRate ?? mapTaxTypeToRate(apiProduct.TaxType || apiProduct.taxType),
        isActive: apiProduct.IsActive ?? apiProduct.isActive ?? true,
        barcode: apiProduct.Barcode || apiProduct.barcode,
        cost: apiProduct.Cost ?? apiProduct.cost ?? 0,
        createdAt: apiProduct.CreatedAt || apiProduct.createdAt || new Date().toISOString(),
        updatedAt: apiProduct.UpdatedAt || apiProduct.updatedAt,
        createdBy: apiProduct.CreatedBy || apiProduct.createdBy,
        updatedBy: apiProduct.UpdatedBy || apiProduct.updatedBy,
    };
};

export const mapUiProductToApi = (uiProduct: Product): any => {
    return {
        // Backend expects PascalCase (based on controller but we'll use property names matching DTO)
        // However, standard JSON serializer in .NET usually accepts camelCase if configured. 
        // We will send standard object matching the Interface but with corrected TaxType
        ...uiProduct,
        id: uiProduct.id,
        name: uiProduct.name,
        price: uiProduct.price,
        description: uiProduct.description,
        category: uiProduct.category,
        stockQuantity: uiProduct.stockQuantity,
        minStockLevel: uiProduct.minStockLevel,
        unit: uiProduct.unit,
        cost: uiProduct.cost,
        isActive: uiProduct.isActive,
        barcode: uiProduct.barcode,
        // CRITICAL FIX: Map numeric rate/type back to String Enum
        taxType: mapRateToTaxType(Number(uiProduct.taxRate || uiProduct.taxType || 20)),
        taxRate: Number(uiProduct.taxRate || 20),
        // Ensure RKSV fields are populated if missing
        isFiscalCompliant: true,
        isTaxable: true,
        rksvProductType: "Standard"
    };
};
