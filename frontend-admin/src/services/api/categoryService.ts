import { apiClient } from './apiClient';

export interface Category {
  id: string;
  name: string;
  description?: string;
  color?: string;
  icon?: string;
  sortOrder: number;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  productCount?: number;
  products?: Product[];
}

export interface Product {
  id: string;
  name: string;
  price: number;
  stockQuantity: number;
  isActive: boolean;
}

export interface CreateCategoryRequest {
  name: string;
  description?: string;
  color?: string;
  icon?: string;
  sortOrder: number;
}

export interface UpdateCategoryRequest {
  name: string;
  description?: string;
  color?: string;
  icon?: string;
  sortOrder: number;
}

class CategoryService {
  // Tüm kategorileri getir
  async getCategories(): Promise<Category[]> {
    const response = await apiClient.get('/categories');
    return response.data;
  }

  // Tek kategori getir
  async getCategory(id: string): Promise<Category> {
    const response = await apiClient.get(`/categories/${id}`);
    return response.data;
  }

  // Yeni kategori oluştur
  async createCategory(data: CreateCategoryRequest): Promise<Category> {
    const response = await apiClient.post('/categories', data);
    return response.data;
  }

  // Kategori güncelle
  async updateCategory(id: string, data: UpdateCategoryRequest): Promise<Category> {
    const response = await apiClient.put(`/categories/${id}`, data);
    return response.data;
  }

  // Kategori sil
  async deleteCategory(id: string): Promise<void> {
    await apiClient.delete(`/categories/${id}`);
  }

  // Kategori durumu güncelle
  async updateCategoryStatus(id: string, isActive: boolean): Promise<Partial<Category>> {
    const response = await apiClient.put(`/categories/${id}/status`, { isActive });
    return response.data;
  }

  // Kategorinin ürünlerini getir
  async getCategoryProducts(id: string): Promise<{ category: Category; products: Product[] }> {
    const response = await apiClient.get(`/categories/${id}/products`);
    return response.data;
  }

  // İstatistikler
  async getCategoryStats(): Promise<{
    total: number;
    active: number;
    inactive: number;
    withProducts: number;
    empty: number;
  }> {
    const categories = await this.getCategories();
    
    const stats = {
      total: categories.length,
      active: categories.filter(c => c.isActive).length,
      inactive: categories.filter(c => !c.isActive).length,
      withProducts: categories.filter(c => (c.productCount || 0) > 0).length,
      empty: categories.filter(c => (c.productCount || 0) === 0).length,
    };

    return stats;
  }
}

export const categoryService = new CategoryService(); 