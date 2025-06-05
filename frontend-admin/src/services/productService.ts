import api from '@/services/api';

export interface Product {
  id: number;
  name: string;
  code: string;
  category: string;
  price: number;
  taxRate: number;
  description?: string;
  active: boolean; // Diğer alanlar backend'e göre eklenebilir
}

export interface CreateProductRequest {
  name: string;
  code: string;
  category: string;
  price: number;
  taxRate: number;
  description?: string;
  active?: boolean;
}

export async function getProducts() {
  const response = await api.get<Product[]>('/api/product');
  return (response.data);
}

export async function getProduct(id: number) {
  const response = await api.get<Product>(`/api/product/${id}`);
  return (response.data);
}

export async function createProduct(data: CreateProductRequest) {
  const response = await api.post<Product>('/api/product', data);
  return (response.data);
}

export async function updateProduct(id: number, data: CreateProductRequest) {
  const response = await api.put<Product>(`/api/product/${id}`, data);
  return (response.data);
}

export async function deleteProduct(id: number) {
  const response = await api.delete(`/api/product/${id}`);
  return (response.data);
} 