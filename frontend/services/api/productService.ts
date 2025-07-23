import { apiClient } from './config';

export interface Product {
  id: string;
  name: string;
  description: string;
  price: number;
  stockQuantity: number;
  unit: string;
  category: string;
  taxType: 'Standard' | 'Reduced' | 'Special';
  isActive: boolean;
  imageUrl?: string;
  barcode?: string;
  createdAt: string;
  updatedAt: string;
  variations?: ProductVariation[];
  options?: ProductOption[];
}

export interface ProductVariation {
  id: string;
  name: string;
  description?: string;
  priceModifier: number;
  priceMultiplier: number;
  isDefault: boolean;
  sortOrder: number;
  isActive: boolean;
  icon?: string;
  color?: string;
}

export interface ProductOptionValue {
  id: string;
  value: string;
  priceModifier: number;
  isDefault: boolean;
  sortOrder: number;
  isActive: boolean;
}

export interface ProductOption {
  id: string;
  name: string;
  description?: string;
  optionType: 'SingleChoice' | 'MultipleChoice' | 'TextInput' | 'NumberInput';
  isRequired: boolean;
  maxSelections: number;
  sortOrder: number;
  isActive: boolean;
  optionValues: ProductOptionValue[];
}

export interface ProductCategory {
  id: string;
  name: string;
  description?: string;
  isActive: boolean;
}

// Ürünleri getir
export const getProducts = async (): Promise<Product[]> => {
  try {
    const response = await apiClient.get<Product[]>('/api/product');
    return response || [];
  } catch (error) {
    console.error('Error fetching products:', error);
    throw new Error('Ürünler yüklenemedi');
  }
};

// Aktif ürünleri getir
export const getActiveProducts = async (): Promise<Product[]> => {
  try {
    const response = await apiClient.get<Product[]>('/api/product');
    return response || [];
  } catch (error) {
    console.error('Error fetching active products:', error);
    throw new Error('Aktif ürünler yüklenemedi');
  }
};

// Kategoriye göre ürünleri getir
export const getProductsByCategory = async (categoryId: string): Promise<Product[]> => {
  try {
    const response = await apiClient.get<Product[]>(`/api/product/category/${categoryId}`);
    return response || [];
  } catch (error) {
    console.error('Error fetching products by category:', error);
    throw new Error('Kategori ürünleri yüklenemedi');
  }
};

// Ürün detayını getir
export const getProductById = async (productId: string): Promise<Product> => {
  try {
    const response = await apiClient.get<Product>(`/api/product/${productId}`);
    return response;
  } catch (error) {
    console.error('Error fetching product:', error);
    throw new Error('Ürün detayı yüklenemedi');
  }
};

// Kategorileri getir
export const getCategories = async (): Promise<ProductCategory[]> => {
  try {
    const response = await apiClient.get<ProductCategory[]>('/api/categories');
    return response || [];
  } catch (error) {
    console.error('Error fetching categories:', error);
    throw new Error('Kategoriler yüklenemedi');
  }
};

// Ürün arama
export const searchProducts = async (query: string): Promise<Product[]> => {
  try {
    const response = await apiClient.get<Product[]>(`/api/product/search?q=${encodeURIComponent(query)}`);
    return response || [];
  } catch (error) {
    console.error('Error searching products:', error);
    throw new Error('Ürün arama başarısız');
  }
}; 