import { router } from 'expo-router';
import { useState, useEffect } from 'react';

import { apiClient } from '../services/api/config';
import { sessionManager } from '../services/session/sessionManager';

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

export function useProductOperations() {
  const [products, setProducts] = useState<SimpleProduct[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Ürünleri yükle
  const loadProducts = async () => {
    setLoading(true);
    setError(null);

    try {
      // Token kontrolü
      const token = await sessionManager.getAccessToken();
      if (!token) {
        console.log('❌ Token bulunamadı, login sayfasına yönlendiriliyor...');
        router.replace('/(auth)/login');
        return;
      }

      console.log('🔄 Ürünler yükleniyor...');
      console.log('🔧 API URL:', apiClient.get.toString());

      const response = await apiClient.get('/products');
      console.log('✅ API Response:', response);

      // Response format kontrolü
      if (response) {
        let productsData: SimpleProduct[] = [];
        const responseAny = response as any;

        // Farklı response formatlarını destekle
        if (Array.isArray(responseAny)) {
          // Direkt array: [{...}, {...}]
          productsData = responseAny;
        } else if (responseAny.items && Array.isArray(responseAny.items)) {
          // { items: [...] } formatı
          productsData = responseAny.items;
        } else if (responseAny.data?.items && Array.isArray(responseAny.data.items)) {
          // { data: { items: [...] } } formatı
          productsData = responseAny.data.items;
        } else {
          console.warn('⚠️ Beklenmeyen response formatı:', responseAny);
          throw new Error('Unexpected API response format');
        }

        console.log(`✅ ${productsData.length} ürün başarıyla yüklendi`);
        setProducts(productsData);
      } else {
        throw new Error('API response is empty or invalid');
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

      setError(errorMessage);
    } finally {
      setLoading(false);
    }
  };

  // Component mount olduğunda ürünleri yükle
  useEffect(() => {
    console.log('🚀 useProductOperations mount - ürünler yükleniyor');
    loadProducts();
  }, []);

  // Manuel refresh için
  const refreshProducts = () => {
    console.log('🔄 Manuel refresh - ürünler yenileniyor...');
    loadProducts();
  };

  // Force refresh için
  const forceRefreshProducts = () => {
    console.log('🔄 Force refresh - ürünler zorla yenileniyor...');
    setError(null);
    loadProducts();
  };

  return {
    products,
    loading,
    error,
    refreshProducts,
    forceRefreshProducts,
  };
}
