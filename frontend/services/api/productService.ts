import { apiClient } from './config';

export interface Product {
    id: string;
    name: string;
    price: number;
    taxType: 'standard' | 'reduced' | 'special';
    description: string;
    category: string;
    stockQuantity: number;
    stock: number; // Alias for stockQuantity
    unit: string;
    imageUrl?: string;
    barcode?: string;
}

export const productService = {
    // Tüm ürünleri getir
    getAllProducts: async (): Promise<Product[]> => {
        const response = await apiClient.get<Product[]>('/products');
        return response.data;
    },

    // ID'ye göre ürün getir
    getProductById: async (id: string): Promise<Product> => {
        const response = await apiClient.get<Product>(`/products/${id}`);
        return response.data;
    },

    // Yeni ürün ekle
    createProduct: async (product: Omit<Product, 'id'>): Promise<Product> => {
        const response = await apiClient.post<Product>('/products', product);
        return response.data;
    },

    // Ürün güncelle
    updateProduct: async (id: string, product: Partial<Product>): Promise<Product> => {
        const response = await apiClient.put<Product>(`/products/${id}`, product);
        return response.data;
    },

    // Ürün sil
    deleteProduct: async (id: string): Promise<void> => {
        await apiClient.delete(`/products/${id}`);
    },

    // Ürün ara
    searchProducts: async (query: string): Promise<Product[]> => {
        const response = await apiClient.get<Product[]>(`/products/search?query=${encodeURIComponent(query)}`);
        return response.data;
    }
}; 