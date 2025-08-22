import { apiClient } from './config';
import { handleAPIError, ErrorMessages } from '../errorService'; // ✅ YENİ: Standardize error handling

// Cache sistemi - API çağrılarının tekrarlanmasını önler
export const productCache = {
  products: null as Product[] | null,
  categories: null as string[] | null,
  lastFetch: null as number | null,
  cacheTimeout: 15 * 60 * 1000, // 15 dakika cache süresi
  
  isExpired: function() {
    if (!this.lastFetch) return true;
    return Date.now() - this.lastFetch > this.cacheTimeout;
  },
  
  clear: function() {
    this.products = null;
    this.categories = null;
    this.lastFetch = null;
  }
};

// RKSV uyumlu vergi tipleri - Backend ile senkron
export const TaxTypes = {
  Standard: 'Standard',    // %20
  Reduced: 'Reduced',      // %10 (gıda, kitap, vb.)
  Special: 'Special',      // %13 (konaklama, vb.)
  Exempt: 'Exempt'         // %0 (vergisiz)
} as const;

export type TaxType = typeof TaxTypes[keyof typeof TaxTypes];

// RKSV ürün tipleri - Avusturya kasa sistemi standartları
export const RksvProductTypes = {
  Standard: 'Standard',        // Standart ürün
  Reduced: 'Reduced',          // İndirimli vergi oranı
  Special: 'Special',          // Özel vergi oranı
  Exempt: 'Exempt',            // Vergi muaf
  Service: 'Service',          // Hizmet
  Digital: 'Digital'           // Dijital ürün
} as const;

export type RksvProductType = typeof RksvProductTypes[keyof typeof RksvProductTypes];

// RKSV uyumlu ürün interface'i - Backend ile senkron
export interface Product {
  id: string;
  name: string;
  description?: string;
  price: number;
  stockQuantity: number;
  minStockLevel: number; // Minimum stok seviyesi
  unit: string;
  category: string;
  taxType: TaxType; // RKSV vergi tipleri
  isActive: boolean;
  imageUrl?: string;
  createdAt: string;
  updatedAt: string;
  
  // Maliyet bilgileri
  cost: number; // Ürün maliyeti
  taxRate: number; // Vergi oranı
  
  // RKSV Compliance Fields - Avusturya vergi uyumu
  isFiscalCompliant: boolean; // RKSV fiscal compliance flag
  fiscalCategoryCode?: string; // Avusturya vergi kategorisi kodu
  isTaxable: boolean; // Vergiye tabi olup olmadığı
  taxExemptionReason?: string; // Vergi muafiyeti nedeni
  rksvProductType: RksvProductType; // RKSV ürün tipi
}

export interface ProductCategory {
  id: string;
  name: string;
  description?: string;
  isActive: boolean;
}

// Sayfalama response interface'i
export interface PaginatedResponse<T> {
  items: T[];
  pagination: {
    pageNumber: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
  };
}

// ❌ REMOVED: UpdateStockRequest, CreateProductRequest, UpdateProductRequest - kullanılmıyor

/**
 * Tüm aktif ürünleri getir (sayfalama ile)
 * @param pageNumber Sayfa numarası (varsayılan: 1)
 * @param pageSize Sayfa boyutu (varsayılan: 20)
 * @returns Sayfalanmış ürün listesi
 */
// Sunucudan gelen Product'ı frontend tipine dönüştür (PascalCase -> camelCase)
const mapProduct = (p: any): Product => ({
  id: p.Id ?? p.id,
  name: p.Name ?? p.name,
  description: p.Description ?? p.description,
  price: p.Price ?? p.price,
  stockQuantity: p.StockQuantity ?? p.stockQuantity ?? 0,
  minStockLevel: p.MinStockLevel ?? p.minStockLevel ?? 0,
  unit: p.Unit ?? p.unit ?? '',
  category: p.Category ?? p.category ?? '',
  taxType: (p.TaxType ?? p.taxType) as TaxType,
  isActive: p.IsActive ?? p.isActive ?? true,
  imageUrl: p.ImageUrl ?? p.imageUrl,
  createdAt: p.CreatedAt ?? p.createdAt ?? '',
  updatedAt: p.UpdatedAt ?? p.updatedAt ?? '',
  cost: p.Cost ?? p.cost ?? 0,
  taxRate: p.TaxRate ?? p.taxRate ?? 0,
  isFiscalCompliant: p.IsFiscalCompliant ?? p.isFiscalCompliant ?? true,
  fiscalCategoryCode: p.FiscalCategoryCode ?? p.fiscalCategoryCode,
  isTaxable: p.IsTaxable ?? p.isTaxable ?? true,
  taxExemptionReason: p.TaxExemptionReason ?? p.taxExemptionReason,
  rksvProductType: p.RksvProductType ?? p.rksvProductType,
});

const unwrapData = <T>(resp: any): T => {
  if (!resp) return [] as unknown as T;
  if (Array.isArray(resp)) return resp as T;
  if (Array.isArray(resp?.data)) return resp.data as T;
  if (Array.isArray(resp?.items)) return resp.items as T;
  return [] as unknown as T;
};

export const getAllProducts = async (
  // Parametreler geriye dönük uyumluluk için tutuluyor ancak kullanılmıyor
  pageNumber: number = 1,
  pageSize: number = 20
): Promise<Product[]> => {
  try {
    const resp = await apiClient.get<any>(`/products/all`);
    const arr = unwrapData<any[]>(resp);
    return arr.map(mapProduct);
  } catch (error) {
    const apiError = handleAPIError(error);
    console.error('Error fetching all products:', apiError);
    throw new Error(ErrorMessages.PRODUCTS_LOAD_FAILED);
  }
};

/**
 * Ana sayfa için tüm aktif ürünleri getir (kategori bazlı gruplandırılmış)
 * @returns Kategori bazlı gruplandırılmış ürünler
 */
export const getActiveProductsForHomePage = async (): Promise<{
  category: string;
  products: Product[];
}[]> => {
  try {
    const resp = await apiClient.get<any>('/products/active');
    const arr = unwrapData<any[]>(resp);
    // Backend grouped format olabilir: { Category, Products }
    return arr.map(g => ({
      category: g.Category ?? g.category,
      products: (g.Products ?? g.products ?? []).map(mapProduct)
    }));
  } catch (error) {
    const apiError = handleAPIError(error);
    console.error('Error fetching active products for home page:', apiError);
    throw new Error(ErrorMessages.PRODUCTS_LOAD_FAILED);
  }
};

/**
 * Tüm kategorileri getir - Cache ile optimize edilmiş
 * @returns Kategori listesi
 */
export const getAllCategories = async (): Promise<string[]> => {
  try {
    if (productCache.categories && !productCache.isExpired()) {
      console.log('📦 Returning categories from cache');
      return productCache.categories;
    }
    
    console.log('🔄 Fetching categories from API...');
    const resp = await apiClient.get<any>('/products/categories');
    const categories = unwrapData<string[]>(resp);
    
    productCache.categories = categories;
    productCache.lastFetch = Date.now();
    
    console.log(`✅ Fetched ${categories.length} categories and updated cache`);
    return categories;
  } catch (error) {
    console.error('Error fetching all categories:', error);
    throw new Error('Failed to load categories');
  }
};

/**
 * Kategoriye göre ürünleri getir
 * @param categoryName Kategori adı
 * @returns Kategori ürünleri
 */
export const getProductsByCategory = async (categoryName: string): Promise<Product[]> => {
  try {
    if (!categoryName || categoryName.trim() === '') {
      throw new Error('Category name cannot be empty');
    }
    const resp = await apiClient.get<any>(`/products/category/${categoryName}`);
    const arr = unwrapData<any[]>(resp);
    return arr.map(mapProduct);
  } catch (error) {
    console.error('Error fetching products by category:', error);
    throw new Error('Failed to load category products');
  }
};

// ❌ REMOVED: getProductsByStockStatus - kullanılmıyor

// ❌ REMOVED: getProductById - kullanılmıyor

/**
 * Ürün arama (çoklu kriter ile)
 * @param searchParams Arama parametreleri
 * @returns Arama sonuçları
 */
export const searchProducts = async (searchParams: {
  name?: string;
  category?: string;
}): Promise<Product[]> => {
  try {
    const params = new URLSearchParams();
    if (searchParams.name) params.append('name', searchParams.name);
    if (searchParams.category) params.append('category', searchParams.category);

    const resp = await apiClient.get<any>(`/products/search?${params.toString()}`);
    const arr = unwrapData<any[]>(resp);
    return arr.map(mapProduct);
  } catch (error) {
    console.error('Error searching products:', error);
    throw new Error('Product search failed');
  }
};

// ❌ REMOVED: createProduct - kullanılmıyor (sadece example'da var)

// ❌ REMOVED: updateProduct - kullanılmıyor

// ❌ REMOVED: updateProductStock - kullanılmıyor

// ❌ REMOVED: deleteProduct - kullanılmıyor

// ❌ REMOVED: getProductCount - kullanılmıyor

// ❌ REMOVED: productExists - kullanılmıyor

// Vergi oranını hesapla
export const getTaxRate = (taxType: TaxType): number => {
  switch (taxType) {
    case TaxTypes.Standard: return 20.0;
    case TaxTypes.Reduced: return 10.0;
    case TaxTypes.Special: return 13.0;
    case TaxTypes.Exempt: return 0.0;
    default: return 20.0;
  }
};

// RKSV validation fonksiyonları
export const isValidTaxType = (taxType: string): taxType is TaxType => {
  return Object.values(TaxTypes).includes(taxType as TaxType);
};

export const isValidRksvProductType = (rksvType: string): rksvType is RksvProductType => {
  return Object.values(RksvProductTypes).includes(rksvType as RksvProductType);
};

// Vergi tutarını hesapla
export const calculateTaxAmount = (price: number, taxType: TaxType): number => {
  const taxRate = getTaxRate(taxType);
  return (price * taxRate) / 100;
};

// Cache temizleme fonksiyonu
export const clearProductCache = () => {
  productCache.clear();
  console.log('🧹 Product cache cleared');
};

// Geriye uyumluluk için eski fonksiyonları koru
export const getProducts = getAllProducts;
export const getActiveProducts = getActiveProductsForHomePage; 