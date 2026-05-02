import { Product } from '@/api/generated/model';

/** Backend TaxType enum: 1=Standard(20%), 2=Reduced(10%), 3=Special(13%), 4=ZeroRate(0%). */
export const TAX_TYPE_ENUM = {
    Standard: 1,
    Reduced: 2,
    Special: 3,
    ZeroRate: 4,
} as const;

/** Single source: enum id to tax rate percentage (aligned with backend TaxTypes.GetTaxRate). */
export function taxTypeToRate(taxType: number): number {
    switch (taxType) {
        case 1: return 20;
        case 2: return 10;
        case 3: return 13;
        case 4: return 0;
        default: return 20;
    }
}

/** Translate tax type for list/tooltips; uses `products.taxLabels.*` with {{rate}}. */
export function formatTaxTypeLabelForLocale(
    taxType: number,
    taxRate: number | undefined | null,
    t: (key: string, options?: Record<string, string | number>) => string,
): string {
    const rate =
        taxRate != null && Number.isFinite(Number(taxRate)) ? Number(taxRate) : taxTypeToRate(taxType);
    switch (taxType) {
        case TAX_TYPE_ENUM.Standard:
            return t('products.taxLabels.standard', { rate });
        case TAX_TYPE_ENUM.Reduced:
            return t('products.taxLabels.reduced', { rate });
        case TAX_TYPE_ENUM.Special:
            return t('products.taxLabels.special', { rate });
        case TAX_TYPE_ENUM.ZeroRate:
            return t('products.taxLabels.zero', { rate });
        default:
            return t('products.taxLabels.fallback', { rate });
    }
}

type ProductsTranslateFn = (key: string, options?: Record<string, string | number>) => string;

/** Piece-unit tokens often returned by the API in English; map to localized label without changing stored/API values. */
const PIECE_UNIT_SYNONYMS = new Set([
    'piece',
    'pieces',
    'pc',
    'pcs',
    'stk',
    'stück',
    'stueck',
]);

function isPieceUnitSynonym(unit: string): boolean {
    const u = unit.trim().toLowerCase();
    return PIECE_UNIT_SYNONYMS.has(u);
}

/** Stock unit for display: known piece synonyms use `products.table.unitPieces`; other values pass through (e.g. kg, custom labels). */
export function formatProductUnitLabelForLocale(unit: string | undefined | null, t: ProductsTranslateFn): string {
    const raw = typeof unit === 'string' ? unit.trim() : '';
    if (!raw || isPieceUnitSynonym(raw)) {
        return t('products.table.unitPieces');
    }
    return raw;
}

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
    CategoryId?: string | null;
    TaxType: number; // Backend: int enum 1,2,3,4
    TaxRate?: number;
    IsActive?: boolean;
    Barcode?: string | null;
    Cost?: number;
    [key: string]: any;
}

/** Maps API TaxType (int) and TaxRate to UI Product; taxType is kept as number. */
export const mapApiProductToUi = (apiProduct: ApiProduct | any): Product => {
    if (!apiProduct) return {} as Product;
    const taxType = Number(apiProduct.TaxType ?? apiProduct.taxType ?? 1);
    const taxRate = apiProduct.TaxRate ?? apiProduct.taxRate ?? taxTypeToRate(taxType);

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
        categoryId: apiProduct.CategoryId || apiProduct.categoryId,
        taxType,
        taxRate,
        isActive: apiProduct.IsActive ?? apiProduct.isActive ?? true,
        barcode: apiProduct.Barcode || apiProduct.barcode,
        cost: apiProduct.Cost ?? apiProduct.cost ?? 0,
        createdAt: apiProduct.CreatedAt || apiProduct.createdAt || new Date().toISOString(),
        updatedAt: apiProduct.UpdatedAt || apiProduct.updatedAt,
        createdBy: apiProduct.CreatedBy || apiProduct.createdBy,
        updatedBy: apiProduct.UpdatedBy || apiProduct.updatedBy,
    };
};

function normalizeImageUrlForApi(v: unknown): string | null {
    if (v === undefined || v === null) return null;
    const s = String(v).trim();
    if (s === '') return null;
    return s;
}

/** Payload for backend: taxType integer (1,2,3), taxRate consistent with enum. Sends both categoryId and category (name) because Product.Category is [Required]. */
export const mapUiProductToApi = (uiProduct: Product & { categoryId?: string; category?: string; taxType?: number }): Record<string, unknown> => {
    const taxType = Number(uiProduct.taxType ?? (uiProduct as any).taxType ?? 1);
    const taxRate = taxTypeToRate(taxType);
    const category = typeof uiProduct.category === 'string' && uiProduct.category.trim() ? uiProduct.category.trim() : '';

    return {
        id: uiProduct.id,
        name: uiProduct.name,
        price: Number(uiProduct.price),
        description: uiProduct.description ?? null,
        category,
        categoryId: uiProduct.categoryId,
        stockQuantity: Number(uiProduct.stockQuantity ?? 0),
        minStockLevel: Number(uiProduct.minStockLevel ?? 0),
        unit: uiProduct.unit || 'pcs',
        cost: Number(uiProduct.cost ?? 0),
        isActive: uiProduct.isActive ?? true,
        barcode: uiProduct.barcode ?? '',
        imageUrl: normalizeImageUrlForApi(uiProduct.imageUrl),
        taxType, // Backend int enum bekliyor (1, 2, 3)
        taxRate,
        isFiscalCompliant: true,
        isTaxable: true,
        rksvProductType: 'Standard',
    };
};
