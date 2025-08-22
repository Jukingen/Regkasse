// Türkçe Açıklama: Optimize edilmiş ürün operasyonları hook'u - sonsuz döngü sorunlarını çözer
// useApiManager kullanarak duplicate API çağrılarını önler ve akıllı cache yönetimi sağlar

import { useState, useCallback, useRef, useEffect } from 'react';
import { router } from 'expo-router';
import { useApiManager } from './useApiManager';
import { apiClient } from '../services/api/config';

// Basit ürün tipi
export interface SimpleProduct {
  Id: string;
  Name: string;
  Price: number;
  Category: string;
  StockQuantity: number;
  Description: string;
  TaxType: number;
}

export function useProductOperationsOptimized() {
  const { apiCall, getCachedData, setCachedData } = useApiManager();
  
  const [products, setProducts] = useState<SimpleProduct[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  
  // Ref'ler ile sürekli re-render'ı önle
  const productsRef = useRef(products);
  const loadingRef = useRef(loading);
  const errorRef = useRef(error);

  // State güncelleme fonksiyonları - batch update
  const setProductsState = useCallback((newProducts: SimpleProduct[]) => {
    setProducts(newProducts);
    productsRef.current = newProducts;
  }, []);

  const setLoadingState = useCallback((isLoading: boolean) => {
    setLoading(isLoading);
    loadingRef.current = isLoading;
  }, []);

  const setErrorState = useCallback((errorMsg: string | null) => {
    setError(errorMsg);
    errorRef.current = errorMsg;
  }, []);

  // Ürünleri yükle - API Manager ile
  const loadProducts = useCallback(async () => {
    try {
      setLoadingState(true);
      setErrorState(null);

      console.log('🔄 Ürünler yükleniyor...');

      // Cache kontrolü
      const cachedProducts = getCachedData<SimpleProduct[]>('products');
      if (cachedProducts) {
        console.log('✅ Cache hit for products');
        setProductsState(cachedProducts);
        setLoadingState(false);
        return;
      }

      // API çağrısı - duplicate call'ları önler
      const result = await apiCall(
        'load-products',
        async () => {
          const response = await apiClient.get('/products');
          
          if (!response) {
            throw new Error('API response is empty or invalid');
          }

          // Response format kontrolü
          let productsData: SimpleProduct[] = [];
          const responseAny = response as any;
          
          if (Array.isArray(responseAny)) {
            // Direkt array: [{...}, {...}]
            productsData = responseAny;
          } else if (responseAny.items && Array.isArray(responseAny.items)) {
            // { items: [...] } formatı
            productsData = responseAny.items;
          } else if (responseAny.data && responseAny.data.items && Array.isArray(responseAny.data.items)) {
            // { data: { items: [...] } } formatı
            productsData = responseAny.data.items;
          } else {
            console.warn('⚠️ Beklenmeyen response formatı:', responseAny);
            throw new Error('Unexpected API response format');
          }

          return productsData;
        },
        {
          cacheKey: 'products',
          cacheTTL: 10, // 10 dakika cache
          skipDuplicate: true,
          retryCount: 2,
        }
      );

      if (result) {
        console.log(`✅ ${result.length} ürün başarıyla yüklendi`);
        setProductsState(result);
        
        // Cache'e kaydet
        setCachedData('products', result, 10);
      }

    } catch (error: any) {
      console.error('❌ Ürün yükleme hatası:', error);
      
      // Daha detaylı error mesajları
      let errorMessage = 'Ürünler yüklenemedi';
      if (error.response) {
        // HTTP error response
        errorMessage = `HTTP ${error.response.status}: ${error.response.data?.message || 'Server error'}`;
      } else if (error.request) {
        // Network error
        errorMessage = 'Network error - Backend bağlantısı kurulamadı';
      } else if (error.message) {
        // Other error
        errorMessage = error.message;
      }
      
      setErrorState(errorMessage);
    } finally {
      setLoadingState(false);
    }
  }, [apiCall, getCachedData, setCachedData, setProductsState, setLoadingState, setErrorState]);

  // Component mount olduğunda ürünleri yükle - sadece bir kere
  useEffect(() => {
    console.log('🚀 useProductOperationsOptimized mount - ürünler yükleniyor');
    loadProducts();
  }, []); // Sadece mount'ta çalışır

  // Manuel refresh için
  const refreshProducts = useCallback(() => {
    console.log('🔄 Manuel refresh - ürünler yenileniyor...');
    
    // Cache'i temizle ve yeniden yükle
    setCachedData('products', null, 0);
    loadProducts();
  }, [loadProducts, setCachedData]);

  // Force refresh için
  const forceRefreshProducts = useCallback(() => {
    console.log('🔄 Force refresh - ürünler zorla yenileniyor...');
    
    // Cache'i temizle
    setCachedData('products', null, 0);
    setErrorState(null);
    
    // Yeniden yükle
    loadProducts();
  }, [loadProducts, setCachedData, setErrorState]);

  return {
    products: productsRef.current,
    loading: loadingRef.current,
    error: errorRef.current,
    refreshProducts,
    forceRefreshProducts
  };
}
