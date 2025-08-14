import { apiClient } from './config';

export interface Category {
  id: string;
  name: string;
  description?: string;
  color?: string;
  icon?: string;
  sortOrder: number;
  isActive: boolean;
  productCount?: number;
  createdAt: string;
  updatedAt?: string;
}

// Kategorileri getir
export const getCategories = async (includeProductCount: boolean = true): Promise<Category[]> => {
  try {
    const response = await apiClient.get<Category[]>(`/categories?includeProductCount=${includeProductCount}`);
    return response || [];
  } catch (error) {
    console.error('Error fetching categories:', error);
    throw new Error('Kategoriler yüklenemedi');
  }
};

// Kategori detayını getir
export const getCategoryById = async (categoryId: string): Promise<Category> => {
  try {
    const response = await apiClient.get<Category>(`/categories/${categoryId}`);
    return response;
  } catch (error) {
    console.error('Error fetching category:', error);
    throw new Error('Kategori detayı yüklenemedi');
  }
};

// Yeni kategori oluştur
export const createCategory = async (categoryData: Omit<Category, 'id' | 'createdAt' | 'updatedAt'>): Promise<Category> => {
  try {
    const response = await apiClient.post<Category>('/categories', categoryData);
    return response;
  } catch (error) {
    console.error('Error creating category:', error);
    throw new Error('Kategori oluşturulamadı');
  }
};

// Kategori güncelle
export const updateCategory = async (categoryId: string, categoryData: Partial<Category>): Promise<void> => {
  try {
    await apiClient.put(`/categories/${categoryId}`, categoryData);
  } catch (error) {
    console.error('Error updating category:', error);
    throw new Error('Kategori güncellenemedi');
  }
};

// Kategori sil
export const deleteCategory = async (categoryId: string): Promise<void> => {
  try {
    await apiClient.delete(`/categories/${categoryId}`);
  } catch (error) {
    console.error('Error deleting category:', error);
    throw new Error('Kategori silinemedi');
  }
};

// Kategori durumunu güncelle
export const updateCategoryStatus = async (categoryId: string, isActive: boolean): Promise<void> => {
  try {
    await apiClient.put(`/categories/${categoryId}/status`, { isActive });
  } catch (error) {
    console.error('Error updating category status:', error);
    throw new Error('Kategori durumu güncellenemedi');
  }
};

// Kategori isimlerini getir
export const getCategoryNames = async (): Promise<string[]> => {
  try {
    const response = await apiClient.get<string[]>('/categories/names');
    return response || [];
  } catch (error) {
    console.error('Error fetching category names:', error);
    throw new Error('Kategori isimleri yüklenemedi');
  }
}; 