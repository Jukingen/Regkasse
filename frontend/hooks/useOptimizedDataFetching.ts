// Bu hook, sürekli API çağrıları yerine sadece gerekli durumlarda fetch yapmak için kullanılır
// RKSV uyumlu güvenlik kontrolü ve akıllı cache yönetimi sağlar

import { useState, useEffect, useCallback, useRef } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { useNetInfo } from '@react-native-community/netinfo';

interface FetchOptions {
  enabled?: boolean;
  refetchOnFocus?: boolean;
  refetchOnAppStateChange?: boolean;
  cacheTime?: number; // Cache süresi (ms)
  staleTime?: number; // Data'nın ne kadar süre "fresh" kalacağı (ms)
}

interface FetchState<T> {
  data: T | null;
  loading: boolean;
  error: string | null;
  lastFetch: number;
  isInitialized: boolean;
}

/**
 * Optimized data fetching hook - sürekli API çağrısı yerine sadece gerekli durumlarda fetch yapar
 * @param fetchFn - Fetch fonksiyonu
 * @param dependencies - Hook'un ne zaman yeniden çalışacağını belirleyen dependencies
 * @param options - Fetch seçenekleri
 */
export function useOptimizedDataFetching<T>(
  fetchFn: () => Promise<T>,
  dependencies: any[] = [],
  options: FetchOptions = {}
) {
  const { user } = useAuth();
  const { isConnected, isInternetReachable } = useNetInfo();
  const [state, setState] = useState<FetchState<T>>({
    data: null,
    loading: false,
    error: null,
    lastFetch: 0,
    isInitialized: false,
  });
  
  const {
    enabled = true,
    refetchOnFocus = false,
    refetchOnAppStateChange = false,
    cacheTime = 5 * 60 * 1000, // 5 dakika default cache
    staleTime = 2 * 60 * 1000, // 2 dakika default stale time
  } = options;

  const mountedRef = useRef(true);
  const abortControllerRef = useRef<AbortController | null>(null);

  // Component unmount kontrolü
  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
      if (abortControllerRef.current) {
        abortControllerRef.current.abort();
      }
    };
  }, []);

  // Data'nın fetch edilmesi gerekip gerekmediğini kontrol et
  const shouldFetch = useCallback(() => {
    if (!enabled || !user || !isConnected || !isInternetReachable) {
      return false;
    }

    const now = Date.now();
    const timeSinceLastFetch = now - state.lastFetch;
    
    // Eğer data yoksa veya cache süresi dolmuşsa fetch yap
    if (!state.data || timeSinceLastFetch > cacheTime) {
      return true;
    }

    // Eğer data "stale" ise fetch yap
    if (timeSinceLastFetch > staleTime) {
      return true;
    }

    return false;
  }, [enabled, user, isConnected, isInternetReachable, state.data, state.lastFetch, cacheTime, staleTime]);

  // Ana fetch fonksiyonu
  const fetchData = useCallback(async (force = false) => {
    if (!enabled || !user) return state.data;

    // Force değilse ve fetch gerekmiyorsa cache'den döndür
    if (!force && !shouldFetch()) {
      console.log('🔄 Data already fresh, returning cached data');
      return state.data;
    }

    // Önceki request'i iptal et
    if (abortControllerRef.current) {
      abortControllerRef.current.abort();
    }

    // Yeni abort controller oluştur
    abortControllerRef.current = new AbortController();

    try {
      setState(prev => ({ ...prev, loading: true, error: null }));

      console.log('🔄 Fetching fresh data...');
      
      const result = await fetchFn();
      
      if (mountedRef.current) {
        setState(prev => ({
          ...prev,
          data: result,
          loading: false,
          lastFetch: Date.now(),
          isInitialized: true,
        }));
      }

      return result;
    } catch (error: any) {
      if (mountedRef.current && !abortControllerRef.current.signal.aborted) {
        const errorMessage = error?.message || 'Data fetch failed';
        setState(prev => ({
          ...prev,
          loading: false,
          error: errorMessage,
        }));
        console.error('❌ Data fetch error:', errorMessage);
      }
      return state.data; // Hata durumunda cache'den döndür
    }
  }, [enabled, user, shouldFetch, fetchFn, state.data]);

  // Manuel refresh fonksiyonu
  const refresh = useCallback(async () => {
    console.log('🔄 Manual refresh requested...');
    return await fetchData(true);
  }, [fetchData]);

  // Dependencies değiştiğinde fetch yap
  useEffect(() => {
    if (enabled && user && shouldFetch()) {
      fetchData();
    }
  }, dependencies); // fetchData dependency'sini kaldırdık

  // Network durumu değiştiğinde kontrol et
  useEffect(() => {
    if (isConnected && isInternetReachable && enabled && user && shouldFetch()) {
      console.log('🔄 Network restored, fetching fresh data...');
      fetchData();
    }
  }, [isConnected, isInternetReachable]);

  return {
    // State
    data: state.data,
    loading: state.loading,
    error: state.error,
    lastFetch: state.lastFetch,
    isInitialized: state.isInitialized,
    
    // Actions
    fetchData,
    refresh,
    
    // Helper functions
    shouldFetch: shouldFetch(),
    isStale: state.data && (Date.now() - state.lastFetch) > staleTime,
  };
}

/**
 * Table orders recovery için özel hook
 */
export function useOptimizedTableOrdersRecovery() {
  const { user } = useAuth();
  
  const fetchTableOrders = useCallback(async () => {
    if (!user) return null;
    
    try {
      const response = await fetch('/api/cart/table-orders-recovery', {
        headers: {
          'Authorization': `Bearer ${user.token}`,
          'Content-Type': 'application/json',
        },
      });
      
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
      }
      
      return await response.json();
    } catch (error) {
      console.error('Table orders fetch failed:', error);
      throw error;
    }
  }, [user]);

  return useOptimizedDataFetching(fetchTableOrders, [user], {
    cacheTime: 2 * 60 * 1000, // 2 dakika cache
    staleTime: 1 * 60 * 1000, // 1 dakika stale time
  });
}

/**
 * Payment methods için özel hook
 */
export function useOptimizedPaymentMethods() {
  const { user } = useAuth();
  
  const fetchPaymentMethods = useCallback(async () => {
    if (!user) return null;
    
    try {
      const response = await fetch('/api/Payment/methods', {
        headers: {
          'Authorization': `Bearer ${user.token}`,
          'Content-Type': 'application/json',
        },
      });
      
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
      }
      
      return await response.json();
    } catch (error) {
      console.error('Payment methods fetch failed:', error);
      throw error;
    }
  }, [user]);

  return useOptimizedDataFetching(fetchPaymentMethods, [user], {
    cacheTime: 10 * 60 * 1000, // 10 dakika cache (payment methods nadiren değişir)
    staleTime: 5 * 60 * 1000,  // 5 dakika stale time
  });
}
