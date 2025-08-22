// Türkçe Açıklama: Unified Product Hook - Tüm ürün API çağrılarını tek noktada birleştirir
// Duplicate hook'ları kaldırır ve consistent API kullanımı sağlar

import { useState, useEffect, useCallback, useRef } from 'react';
import { Product, getAllProducts, getAllCategories, clearProductCache } from '../services/api/productService';

interface UseProductsUnifiedState {
  products: Product[];
  categories: string[];
  loading: boolean;
  error: string | null;
  initialized: boolean;
}

// Singleton cache instance
class ProductCache {
  private static instance: ProductCache;
  private state: UseProductsUnifiedState = {
    products: [],
    categories: [],
    loading: false,
    error: null,
    initialized: false
  };
  private listeners = new Set<() => void>();
  private loadingPromise: Promise<void> | null = null;

  static getInstance(): ProductCache {
    if (!ProductCache.instance) {
      ProductCache.instance = new ProductCache();
    }
    return ProductCache.instance;
  }

  // State güncellemesi ve listener'ları bilgilendirme
  private updateState(newState: Partial<UseProductsUnifiedState>) {
    this.state = { ...this.state, ...newState };
    this.listeners.forEach(listener => listener());
  }

  // Listener ekle/kaldır
  addListener(listener: () => void) {
    this.listeners.add(listener);
  }

  removeListener(listener: () => void) {
    this.listeners.delete(listener);
  }

  // Mevcut state'i al
  getState(): UseProductsUnifiedState {
    return { ...this.state };
  }

  // Verileri yükle (duplicate çağrıları önler)
  async loadData(): Promise<void> {
    // Eğer zaten yükleniyorsa aynı promise'i döndür
    if (this.loadingPromise) {
      return this.loadingPromise;
    }

    // Eğer zaten yüklenmişse yüklemez
    if (this.state.initialized && this.state.products.length > 0) {
      console.log('📦 Products already loaded, skipping...');
      return Promise.resolve();
    }

    console.log('🔄 Loading products and categories...');
    
    this.loadingPromise = this._performLoad();
    
    try {
      await this.loadingPromise;
    } finally {
      this.loadingPromise = null;
    }
  }

  private async _performLoad(): Promise<void> {
    try {
      this.updateState({ loading: true, error: null });

      // Paralel olarak products ve categories yükle
      const [productsResponse, categoriesResponse] = await Promise.all([
        getAllProducts(1, 1000), // Sayfalama ile tüm ürünleri al
        getAllCategories()
      ]);

      // Response format kontrolü
      const products = productsResponse.items || productsResponse;
      const categories = categoriesResponse || [];

      this.updateState({
        products: Array.isArray(products) ? products : [],
        categories: Array.isArray(categories) ? categories : [],
        loading: false,
        initialized: true,
        error: null
      });

      console.log(`✅ Loaded ${products.length} products and ${categories.length} categories`);
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Failed to load data';
      this.updateState({
        loading: false,
        error: errorMessage,
        initialized: true
      });
      console.error('❌ Error loading products/categories:', error);
      throw error;
    }
  }

  // Cache'i temizle ve yeniden yükle
  async refreshData(): Promise<void> {
    console.log('🔄 Refreshing all product data...');
    
    // Frontend cache'i temizle
    clearProductCache();
    
    // Local state'i sıfırla
    this.state = {
      products: [],
      categories: [],
      loading: false,
      error: null,
      initialized: false
    };
    
    this.loadingPromise = null;
    this.updateState(this.state);
    
    // Yeniden yükle
    await this.loadData();
  }

  // Belirli bir kategorideki ürünleri filtrele
  getProductsByCategory(category: string): Product[] {
    if (category === 'all' || !category) {
      return this.state.products;
    }
    return this.state.products.filter(product => 
      product.category.toLowerCase() === category.toLowerCase()
    );
  }

  // Ürün arama
  searchProducts(query: string): Product[] {
    if (!query.trim()) {
      return this.state.products;
    }
    
    const searchTerm = query.toLowerCase();
    return this.state.products.filter(product =>
      product.name.toLowerCase().includes(searchTerm) ||
      product.description?.toLowerCase().includes(searchTerm) ||
      product.category.toLowerCase().includes(searchTerm)
    );
  }
}

/**
 * Unified Product Hook - Tüm ürün işlemlerini tek noktada yönetir
 * Duplicate API çağrılarını önler ve consistent state yönetimi sağlar
 */
export const useProductsUnified = () => {
  const cache = ProductCache.getInstance();
  const [, forceUpdate] = useState({});
  const isMountedRef = useRef(true);

  // Cache değişikliklerini dinle
  useEffect(() => {
    const listener = () => {
      if (isMountedRef.current) {
        forceUpdate({});
      }
    };

    cache.addListener(listener);
    
    // İlk yüklemeyi başlat
    cache.loadData().catch(console.error);

    return () => {
      isMountedRef.current = false;
      cache.removeListener(listener);
    };
  }, [cache]);

  // State'i al
  const state = cache.getState();

  // Fonksiyonları memoize et
  const refreshData = useCallback(() => cache.refreshData(), [cache]);
  const getProductsByCategory = useCallback((category: string) => 
    cache.getProductsByCategory(category), [cache]);
  const searchProducts = useCallback((query: string) => 
    cache.searchProducts(query), [cache]);

  return {
    ...state,
    refreshData,
    getProductsByCategory,
    searchProducts,
    
    // Geriye uyumluluk için eski isimleri de export et
    loadProducts: refreshData,
    loadCategories: refreshData,
  };
};

export default useProductsUnified;
