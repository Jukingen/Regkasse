import api from '@/services/api';

export interface Product {
  id: number;
  name: string;
  code: string;
  category: string;
  price: number;
  taxRate: number;
  description?: string;
  active: boolean;
  barcode?: string;
  stockQuantity?: number;
  unit?: string;
}

export interface CreateProductRequest {
  name: string;
  code: string;
  category: string;
  price: number;
  taxRate: number;
  description?: string;
  active?: boolean;
  barcode?: string;
  stockQuantity?: number;
  unit?: string;
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
  
  const response = await api.get<Product[]>(`/api/product?${params.toString()}`);
  return response.data;
}

export async function getProduct(id: number) {
  const response = await api.get<Product>(`/api/product/${id}`);
  return response.data;
}

export async function createProduct(data: CreateProductRequest) {
  const response = await api.post<Product>('/api/product', data);
  return response.data;
}

export async function updateProduct(id: number, data: CreateProductRequest) {
  const response = await api.put<Product>(`/api/product/${id}`, data);
  return response.data;
}

export async function deleteProduct(id: number) {
  const response = await api.delete(`/api/product/${id}`);
  return response.data;
}

// Kategori yönetimi
export async function getCategories() {
  const response = await api.get<string[]>('/api/product/categories');
  return response.data;
}

export async function getProductsByCategory(category: string) {
  const response = await api.get<Product[]>(`/api/product/by-category/${encodeURIComponent(category)}`);
  return response.data;
}

// Arama işlemleri
export async function searchProducts(query: string) {
  const response = await api.get<Product[]>(`/api/product/search?q=${encodeURIComponent(query)}`);
  return response.data;
}

// Önceden tanımlanmış kategoriler (Avusturya restoran için)
export const PREDEFINED_CATEGORIES = [
  'Hauptgerichte',      // Ana yemekler
  'Vorspeisen',         // Başlangıçlar
  'Suppen',             // Çorbalar
  'Salate',             // Salatalar
  'Desserts',           // Tatlılar
  'Getränke',           // İçecekler (alkolsüz)
  'Alkoholische Getränke', // Alkol içeren içecekler
  'Kaffee & Tee',       // Kahve ve çay
  'Süßigkeiten',        // Şekerlemeler
  'Spezialitäten',      // Özel ürünler
  'Snacks',             // Atıştırmalıklar
  'Brot & Gebäck'       // Ekmek ve hamur işleri
];

// Kategori renkleri
export const CATEGORY_COLORS: Record<string, string> = {
  'Hauptgerichte': '#ff6b6b',
  'Vorspeisen': '#4ecdc4',
  'Suppen': '#45b7d1',
  'Salate': '#96ceb4',
  'Desserts': '#feca57',
  'Getränke': '#ff9ff3',
  'Alkoholische Getränke': '#54a0ff',
  'Kaffee & Tee': '#5f27cd',
  'Süßigkeiten': '#ff9f43',
  'Spezialitäten': '#00d2d3',
  'Snacks': '#ff6348',
  'Brot & Gebäck': '#cd6133'
};

// Kategori açıklamaları
export const CATEGORY_DESCRIPTIONS: Record<string, string> = {
  'Hauptgerichte': 'Ana yemekler ve et yemekleri',
  'Vorspeisen': 'Başlangıç yemekleri ve mezeler',
  'Suppen': 'Çorbalar ve sıcak içecekler',
  'Salate': 'Taze salatalar ve yeşillikler',
  'Desserts': 'Tatlılar ve şekerlemeler',
  'Getränke': 'Alkolsüz içecekler',
  'Alkoholische Getränke': 'Bira, şarap ve diğer alkollü içecekler',
  'Kaffee & Tee': 'Kahve ve çay çeşitleri',
  'Süßigkeiten': 'Çikolata ve şekerlemeler',
  'Spezialitäten': 'Özel ve geleneksel ürünler',
  'Snacks': 'Atıştırmalık ve hafif yiyecekler',
  'Brot & Gebäck': 'Ekmek ve hamur işi ürünleri'
}; 