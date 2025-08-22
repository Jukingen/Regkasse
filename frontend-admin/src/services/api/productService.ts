import { apiClient } from './apiClient';

export interface Product {
  id: string;
  name: string;
  description: string;
  price: number;
  stockQuantity: number;
  minStockLevel: number;
  barcode: string;
  category: string;
  unit: string;
  taxRate: number;
  taxType: 'Standard' | 'Reduced' | 'Special';
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateProductRequest {
  name: string;
  description: string;
  price: number;
  stockQuantity: number;
  minStockLevel: number;
  barcode: string;
  category: string;
  unit: string;
  taxRate: number;
  taxType: 'Standard' | 'Reduced' | 'Special';
  maxStockLevel?: number;
  location?: string;
}

export interface UpdateProductRequest {
  name: string;
  description: string;
  price: number;
  stockQuantity: number;
  minStockLevel: number;
  barcode: string;
  category: string;
  unit: string;
  taxRate: number;
  taxType: 'Standard' | 'Reduced' | 'Special';
}

export interface ProductSearchParams {
  category?: string;
  search?: string;
  isActive?: boolean;
  lowStock?: boolean;
}

class ProductService {
  // Tüm ürünleri getir
  async getProducts(params?: ProductSearchParams): Promise<Product[]> {
    const queryParams = new URLSearchParams();
    if (params?.category) queryParams.append('category', params.category);
    if (params?.search) queryParams.append('search', params.search);
    if (params?.isActive !== undefined) queryParams.append('isActive', params.isActive.toString());

    const response = await apiClient.get(`/products?${queryParams.toString()}`);
    let products = response.data;

    // Low stock filtresi
    if (params?.lowStock) {
      products = products.filter((p: Product) => p.stockQuantity <= p.minStockLevel);
    }

    return products;
  }

  // Tek ürün getir
  async getProduct(id: string): Promise<Product> {
    const response = await apiClient.get(`/products/${id}`);
    return response.data;
  }

  // Yeni ürün oluştur
  async createProduct(data: CreateProductRequest): Promise<Product> {
    const response = await apiClient.post('/products/create', data);
    return response.data;
  }

  // Ürün güncelle
  async updateProduct(id: string, data: UpdateProductRequest): Promise<Product> {
    const response = await apiClient.put(`/products/update/${id}`, data);
    return response.data;
  }

  // Ürün sil
  async deleteProduct(id: string): Promise<void> {
    await apiClient.delete(`/products/${id}`);
  }

  // Ürün durumu güncelle
  async updateProductStatus(id: string, isActive: boolean): Promise<Partial<Product>> {
    const response = await apiClient.put(`/products/${id}/status`, { isActive });
    return response.data;
  }

  // Ürün arama
  async searchProducts(query: string): Promise<Product[]> {
    const response = await apiClient.get(`/products/search?q=${encodeURIComponent(query)}`);
    return response.data;
  }

  // Barkoda göre ürün getir
  async getProductByBarcode(barcode: string): Promise<Product | null> {
    try {
      const response = await apiClient.get(`/products/search?q=${encodeURIComponent(barcode)}`);
      const products = response.data;
      return products.find((p: Product) => p.barcode === barcode) || null;
    } catch (error) {
      return null;
    }
  }

  // Kategoriye göre ürünler
  async getProductsByCategory(category: string): Promise<Product[]> {
    const response = await apiClient.get(`/products/category/${encodeURIComponent(category)}`);
    return response.data;
  }

  // Kategorileri getir
  async getCategories(): Promise<string[]> {
    const response = await apiClient.get('/products/categories');
    return response.data;
  }

  // Düşük stok ürünleri
  async getLowStockProducts(): Promise<Product[]> {
    const products = await this.getProducts();
    return products.filter(product => product.stockQuantity <= product.minStockLevel);
  }

  // Stok güncelle
  async updateStock(id: string, newQuantity: number): Promise<Product> {
    const response = await apiClient.put(`/products/stock/${id}`, { quantity: newQuantity });
    return response.data;
  }

  // İstatistikler
  async getProductStats(): Promise<{
    total: number;
    active: number;
    inactive: number;
    lowStock: number;
    outOfStock: number;
    byCategory: { [key: string]: number };
  }> {
    const products = await this.getProducts();
    
    const stats = {
      total: products.length,
      active: products.filter(p => p.isActive).length,
      inactive: products.filter(p => !p.isActive).length,
      lowStock: products.filter(p => p.stockQuantity <= p.minStockLevel && p.stockQuantity > 0).length,
      outOfStock: products.filter(p => p.stockQuantity <= 0).length,
      byCategory: {} as { [key: string]: number }
    };

    // Kategori istatistikleri
    products.forEach(product => {
      stats.byCategory[product.category] = (stats.byCategory[product.category] || 0) + 1;
    });

    return stats;
  }
}

export const productService = new ProductService(); 