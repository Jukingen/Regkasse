import api from './api';

export interface Product {
  id: number;
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

export interface ProductFilters {
  category?: string;
  search?: string;
}

// Temel CRUD işlemleri
export async function getProducts(filters?: ProductFilters) {
  const params = new URLSearchParams();
  if (filters?.category) params.append('category', filters.category);
  if (filters?.search) params.append('search', filters.search);
  
  const response = await api.get<Product[]>(`/api/products?${params.toString()}`);
  return response.data;
}

export async function getProduct(id: number) {
  const response = await api.get<Product>(`/api/products/${id}`);
  return response.data;
}

export async function createProduct(data: CreateProductRequest) {
  const response = await api.post<Product>('/api/products/create', data);
  return response.data;
}

export async function updateProduct(id: number, data: CreateProductRequest) {
  const response = await api.put<Product>(`/api/products/update/${id}`, data);
  return response.data;
}

export async function deleteProduct(id: number) {
  const response = await api.delete(`/api/products/${id}`);
  return response.data;
}

// Kategori yönetimi
export async function getCategories() {
  const response = await api.get<string[]>('/api/products/categories');
  return response.data;
}

export async function getProductsByCategory(category: string) {
  const response = await api.get<Product[]>(`/api/products/category/${encodeURIComponent(category)}`);
  return response.data;
}

// Arama işlemleri
export async function searchProducts(query: string) {
  const response = await api.get<Product[]>(`/api/products/search?q=${encodeURIComponent(query)}`);
  return response.data;
}

// Önceden tanımlanmış kategoriler (Avusturya restoran için)
export const PREDEFINED_CATEGORIES = [
  'hauptgerichte',      // Ana yemekler
  'vorspeisen',         // Başlangıçlar
  'suppen',             // Çorbalar
  'salate',             // Salatalar
  'desserts',           // Tatlılar
  'getraenke',          // İçecekler (alkolsüz)
  'alkoholischeGetraenke', // Alkol içeren içecekler
  'kaffeeTee',          // Kahve ve çay
  'suessigkeiten',      // Şekerlemeler
  'spezialitaeten',     // Özel ürünler
  'snacks',             // Atıştırmalıklar
  'brotGebaeck'         // Ekmek ve hamur işleri
];

// Kategori renkleri
export const CATEGORY_COLORS: Record<string, string> = {
  'hauptgerichte': '#ff6b6b',
  'vorspeisen': '#4ecdc4',
  'suppen': '#45b7d1',
  'salate': '#96ceb4',
  'desserts': '#feca57',
  'getraenke': '#ff9ff3',
  'alkoholischeGetraenke': '#54a0ff',
  'kaffeeTee': '#5f27cd',
  'suessigkeiten': '#ff9f43',
  'spezialitaeten': '#00d2d3',
  'snacks': '#ff6348',
  'brotGebaeck': '#cd6133'
};

// Kategori açıklamaları (i18n key'leri)
export const CATEGORY_DESCRIPTIONS: Record<string, string> = {
  'hauptgerichte': 'categoryDescriptions.hauptgerichte',
  'vorspeisen': 'categoryDescriptions.vorspeisen',
  'suppen': 'categoryDescriptions.suppen',
  'salate': 'categoryDescriptions.salate',
  'desserts': 'categoryDescriptions.desserts',
  'getraenke': 'categoryDescriptions.getraenke',
  'alkoholischeGetraenke': 'categoryDescriptions.alkoholischeGetraenke',
  'kaffeeTee': 'categoryDescriptions.kaffeeTee',
  'suessigkeiten': 'categoryDescriptions.suessigkeiten',
  'spezialitaeten': 'categoryDescriptions.spezialitaeten',
  'snacks': 'categoryDescriptions.snacks',
  'brotGebaeck': 'categoryDescriptions.brotGebaeck'
}; 