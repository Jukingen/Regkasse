import { apiClient } from './config';
import { useSystem } from '../../contexts/SystemContext';

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

class ProductService {
    private baseUrl = '/products';

    // Tüm ürünleri getir (mod kontrolü ile)
    async getAllProducts(): Promise<Product[]> {
        try {
            const response = await apiClient.get<Product[]>(`${this.baseUrl}`);
            return response.data;
        } catch (error) {
            console.error('Online product fetch failed:', error);
            
            // Çevrimdışı modda çalışıyorsa offline verileri kullan
            const { offlineManager } = await import('../offline/OfflineManager');
            const offlineProducts = await offlineManager.getOfflineProducts();
            
            if (offlineProducts.length > 0) {
                console.log('Using offline products:', offlineProducts.length);
                return offlineProducts;
            }
            
            throw error;
        }
    }

    // ID'ye göre ürün getir
    async getProductById(id: string): Promise<Product> {
        try {
            const response = await apiClient.get<Product>(`${this.baseUrl}/${id}`);
            return response.data;
        } catch (error) {
            console.error('Online product fetch failed:', error);
            
            // Çevrimdışı modda çalışıyorsa offline verileri kullan
            const { offlineManager } = await import('../offline/OfflineManager');
            const offlineProducts = await offlineManager.getOfflineProducts();
            const product = offlineProducts.find(p => p.id === id);
            
            if (product) {
                console.log('Using offline product:', product.name);
                return product;
            }
            
            throw error;
        }
    }

    // Yeni ürün ekle (mod kontrolü ile)
    async createProduct(product: Omit<Product, 'id'>): Promise<Product> {
        try {
            const response = await apiClient.post<Product>(`${this.baseUrl}`, product);
            return response.data;
        } catch (error) {
            console.error('Online product creation failed:', error);
            
            // Çevrimdışı modda çalışıyorsa offline kaydet
            const { offlineManager } = await import('../offline/OfflineManager');
            const offlineProducts = await offlineManager.getOfflineProducts();
            
            const newProduct: Product = {
                ...product,
                id: `offline_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`,
                stock: product.stockQuantity,
                stockQuantity: product.stockQuantity
            };
            
            // Offline ürünleri güncelle
            const updatedProducts = [...offlineProducts, newProduct];
            await offlineManager.saveProductsOffline(updatedProducts);
            
            console.log('Product saved offline:', newProduct.name);
            return newProduct;
        }
    }

    // Ürün güncelle (mod kontrolü ile)
    async updateProduct(id: string, product: Partial<Product>): Promise<Product> {
        try {
            const response = await apiClient.put<Product>(`${this.baseUrl}/${id}`, product);
            return response.data;
        } catch (error) {
            console.error('Online product update failed:', error);
            
            // Çevrimdışı modda çalışıyorsa offline güncelle
            const { offlineManager } = await import('../offline/OfflineManager');
            const offlineProducts = await offlineManager.getOfflineProducts();
            const productIndex = offlineProducts.findIndex(p => p.id === id);
            
            if (productIndex !== -1) {
                const updatedProduct = { ...offlineProducts[productIndex], ...product };
                offlineProducts[productIndex] = updatedProduct;
                await offlineManager.saveProductsOffline(offlineProducts);
                
                console.log('Product updated offline:', updatedProduct.name);
                return updatedProduct;
            }
            
            throw error;
        }
    }

    // Ürün sil (mod kontrolü ile)
    async deleteProduct(id: string): Promise<void> {
        try {
            await apiClient.delete(`${this.baseUrl}/${id}`);
        } catch (error) {
            console.error('Online product deletion failed:', error);
            
            // Çevrimdışı modda çalışıyorsa offline sil
            const { offlineManager } = await import('../offline/OfflineManager');
            const offlineProducts = await offlineManager.getOfflineProducts();
            const filteredProducts = offlineProducts.filter(p => p.id !== id);
            
            await offlineManager.saveProductsOffline(filteredProducts);
            console.log('Product deleted offline:', id);
        }
    }

    // Ürün ara (mod kontrolü ile)
    async searchProducts(query: string): Promise<Product[]> {
        try {
            const response = await apiClient.get<Product[]>(`${this.baseUrl}/search?query=${encodeURIComponent(query)}`);
            return response.data;
        } catch (error) {
            console.error('Online product search failed:', error);
            
            // Çevrimdışı modda çalışıyorsa offline ara
            const { offlineManager } = await import('../offline/OfflineManager');
            const offlineProducts = await offlineManager.getOfflineProducts();
            
            const filteredProducts = offlineProducts.filter(product =>
                product.name.toLowerCase().includes(query.toLowerCase()) ||
                product.barcode?.includes(query)
            );
            
            console.log('Searching offline products:', filteredProducts.length);
            return filteredProducts;
        }
    }

    // Stok güncelle (mod kontrolü ile)
    async updateStock(productId: string, quantity: number, operation: 'add' | 'subtract'): Promise<Product> {
        try {
            const response = await apiClient.post<Product>(`${this.baseUrl}/${productId}/stock`, {
                quantity,
                operation
            });
            return response.data;
        } catch (error) {
            console.error('Online stock update failed:', error);
            
            // Çevrimdışı modda çalışıyorsa offline güncelle
            const { offlineManager } = await import('../offline/OfflineManager');
            const offlineProducts = await offlineManager.getOfflineProducts();
            const productIndex = offlineProducts.findIndex(p => p.id === productId);
            
            if (productIndex !== -1) {
                const product = offlineProducts[productIndex];
                const newStock = operation === 'add' ? product.stock + quantity : product.stock - quantity;
                
                const updatedProduct = {
                    ...product,
                    stock: Math.max(0, newStock),
                    stockQuantity: Math.max(0, newStock)
                };
                
                offlineProducts[productIndex] = updatedProduct;
                await offlineManager.saveProductsOffline(offlineProducts);
                
                console.log('Stock updated offline:', updatedProduct.name, 'New stock:', updatedProduct.stock);
                return updatedProduct;
            }
            
            throw error;
        }
    }

    // Ürünleri çevrimdışı senkronize et
    async syncOfflineProducts(): Promise<number> {
        try {
            const { offlineManager } = await import('../offline/OfflineManager');
            const offlineProducts = await offlineManager.getOfflineProducts();
            
            let syncedCount = 0;
            
            for (const product of offlineProducts) {
                try {
                    // Online'da ürün var mı kontrol et
                    await this.getProductById(product.id);
                    // Varsa güncelle
                    await this.updateProduct(product.id, product);
                } catch {
                    // Yoksa oluştur
                    await this.createProduct({
                        name: product.name,
                        price: product.price,
                        taxType: product.taxType,
                        description: product.description,
                        category: product.category,
                        stockQuantity: product.stockQuantity,
                        stock: product.stock,
                        unit: product.unit,
                        imageUrl: product.imageUrl,
                        barcode: product.barcode
                    });
                }
                syncedCount++;
            }
            
            console.log('Products synced:', syncedCount);
            return syncedCount;
        } catch (error) {
            console.error('Product sync failed:', error);
            return 0;
        }
    }
}

export const productService = new ProductService();
export default productService; 