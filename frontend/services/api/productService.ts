import { apiClient } from './config';

export interface Product {
    id: string;
    name: string;
    price: number;
    taxType: string;
    description: string;
    category: string;
    stockQuantity: number;
    unit: string;
    imageUrl?: string;
}

export const productService = {
    // Tüm ürünleri getir
    getAllProducts: async (): Promise<Product[]> => {
        return apiClient.get<Product[]>('/products');
    },

    // ID'ye göre ürün getir
    getProductById: async (id: string): Promise<Product> => {
        return apiClient.get<Product>(`/products/${id}`);
    },

    // Yeni ürün ekle
    createProduct: async (product: Omit<Product, 'id'>): Promise<Product> => {
        return apiClient.post<Product>('/products', product);
    },

    // Ürün güncelle
    updateProduct: async (id: string, product: Partial<Product>): Promise<Product> => {
        return apiClient.put<Product>(`/products/${id}`, product);
    },

    // Ürün sil
    deleteProduct: async (id: string): Promise<void> => {
        await apiClient.delete(`/products/${id}`);
    },

    // Ürün ara
    searchProducts: async (query: string): Promise<Product[]> => {
        return apiClient.get<Product[]>(`/products/search?query=${encodeURIComponent(query)}`);
    }
}; 