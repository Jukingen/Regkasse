import { useState, useEffect, useCallback } from 'react';

import { useApiManager } from './useApiManager'; // ✅ YENİ: Cache stratejisi entegrasyonu
import { Product } from '../services/api/productService';
import {
  getAllProducts,
  getAllCategories,
  clearProductCache,
} from '../services/api/productService';

// Global cache state - tüm component'ler aynı veriyi paylaşır
let globalProducts: Product[] = [];
let globalCategories: string[] = [];
let globalLoading = false;
let globalError: string | null = null;
let globalInitialized = false;

// Global cache listeners - state değişikliklerini dinlemek için
const listeners: Set<() => void> = new Set();

// Global cache'i güncelle ve tüm listener'ları bilgilendir
const updateGlobalCache = () => {
  listeners.forEach((listener) => {
    listener();
  });
};

/**
 * Ürün cache hook'u - Global singleton pattern ile API çağrılarının tekrarlanmasını önler
 * Tüm component'ler aynı veriyi paylaşır
 */
export const useProductCache = () => {
  // ✅ YENİ: API Manager entegrasyonu
  const { apiCall, getCachedData, setCachedData } = useApiManager();

  const [, forceUpdate] = useState({});

  // Global cache'den veri al
  const products = globalProducts;
  const categories = globalCategories;
  const loading = globalLoading;
  const error = globalError;

  // Ürünleri yükle
  const loadProducts = useCallback(async () => {
    // Eğer zaten yükleniyorsa veya yüklenmişse tekrar yükleme
    if (globalLoading || globalInitialized) {
      return;
    }

    try {
      globalLoading = true;
      globalError = null;
      updateGlobalCache();

      const fetchedProducts = await getAllProducts();
      // Union response normalization: either array or { items, pagination }
      const normalized: typeof globalProducts = Array.isArray(fetchedProducts)
        ? fetchedProducts
        : ((fetchedProducts as { items?: typeof globalProducts })?.items ?? []);
      globalProducts = normalized;
      globalInitialized = true;

      console.log(`📦 Loaded ${normalized.length} products via global cache hook`);
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to load products';
      globalError = errorMessage;
      console.error('❌ Error loading products:', err);
    } finally {
      globalLoading = false;
      updateGlobalCache();
    }
  }, []);

  // Kategorileri yükle
  const loadCategories = useCallback(async () => {
    // Eğer zaten yüklenmişse tekrar yükleme
    if (globalCategories.length > 0) {
      return;
    }

    try {
      globalError = null;
      updateGlobalCache();

      const fetchedCategories = await getAllCategories();
      globalCategories = fetchedCategories;

      console.log(`📂 Loaded ${fetchedCategories.length} categories via global cache hook`);
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to load categories';
      globalError = errorMessage;
      console.error('❌ Error loading categories:', err);
    } finally {
      updateGlobalCache();
    }
  }, []);

  // Cache'i temizle ve yeniden yükle
  const refreshData = useCallback(async () => {
    console.log('🔄 Refreshing product data...');
    clearProductCache();
    globalProducts = [];
    globalCategories = [];
    globalInitialized = false;
    globalError = null;
    updateGlobalCache();

    await Promise.all([loadProducts(), loadCategories()]);
  }, [loadProducts, loadCategories]);

  // Component mount olduğunda listener ekle
  useEffect(() => {
    const listener = () => {
      forceUpdate({});
    };
    listeners.add(listener);

    // İlk yükleme - sadece bir kez
    if (!globalInitialized) {
      loadProducts();
      loadCategories();
    }

    return () => {
      listeners.delete(listener);
    };
  }, [loadProducts, loadCategories]);

  return {
    products,
    categories,
    loading,
    error,
    refreshData,
    loadProducts,
    loadCategories,
  };
};
