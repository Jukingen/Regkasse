// Türkçe Açıklama: Optimize edilmiş masa siparişleri recovery hook'u - sonsuz döngü sorunlarını çözer
// useApiManager kullanarak duplicate API çağrılarını önler ve akıllı cache yönetimi sağlar

import { useState, useCallback, useRef, useEffect } from 'react';
import { useApiManager } from './useApiManager';
import { useAuth } from '../contexts/AuthContext';
import { apiClient } from '../services/api/config';

// Table order recovery interfaces
export interface TableOrderRecoveryItem {
  productId: string;
  productName: string;
  quantity: number;
  price: number;
  total: number;
  notes?: string;
}

export interface TableOrderRecovery {
  tableNumber?: number;
  cartId: string;
  customerName?: string;
  itemCount: number;
  totalAmount: number;
  status: string;
  createdAt: string;
  lastUpdated: string;
  items: TableOrderRecoveryItem[];
}

export interface TableOrdersRecoveryData {
  success: boolean;
  message: string;
  userId: string;
  tableOrders: TableOrderRecovery[];
  totalActiveTables: number;
  retrievedAt: string;
}

interface RecoveryState {
  isLoading: boolean;
  error: string | null;
  recoveryData: TableOrdersRecoveryData | null;
  isRecoveryCompleted: boolean;
  isInitialized: boolean;
}

/**
 * F5 sonrası masa siparişlerini geri yükleme hook'u - Optimize edilmiş versiyon
 * RKSV kurallarına uygun güvenlik kontrolü yapar
 * Yalnızca kullanıcının kendi masa siparişlerini getirir
 * OPTIMIZATION: useApiManager ile duplicate call'ları önler
 */
export const useTableOrdersRecoveryOptimized = () => {
  const { user } = useAuth();
  const { apiCall, getCachedData, setCachedData } = useApiManager();
  
  const [recoveryState, setRecoveryState] = useState<RecoveryState>({
    isLoading: false,
    error: null,
    recoveryData: null,
    isRecoveryCompleted: false,
    isInitialized: false,
  });

  // Ref'ler ile sürekli re-render'ı önle
  const recoveryStateRef = useRef(recoveryState);

  // State güncelleme fonksiyonları - batch update
  const updateRecoveryState = useCallback((updates: Partial<RecoveryState>) => {
    setRecoveryState(prev => {
      const newState = { ...prev, ...updates };
      recoveryStateRef.current = newState;
      return newState;
    });
  }, []);

  /**
   * Backend'den tüm aktif masa siparişlerini getir
   * RKSV uyumlu - yalnızca kullanıcının kendi siparişleri
   * OPTIMIZATION: useApiManager ile duplicate call'ları önler
   */
  const fetchTableOrdersRecovery = useCallback(async (): Promise<TableOrdersRecoveryData | null> => {
    if (!user) {
      console.warn('User not authenticated for table orders recovery');
      return null;
    }

    // OPTIMIZATION: Eğer zaten fetch edildiyse ve data varsa, tekrar fetch yapma
    if (recoveryStateRef.current.isInitialized && recoveryStateRef.current.recoveryData) {
      console.log('🔄 Table orders already fetched, returning cached data');
      return recoveryStateRef.current.recoveryData;
    }

    // Cache kontrolü
    const userCacheKey = `table-orders-recovery:${user.id}`;
    const cachedData = getCachedData<TableOrdersRecoveryData>(userCacheKey);
    if (cachedData) {
      console.log('✅ Cache hit for table orders recovery');
      updateRecoveryState({
        isLoading: false,
        error: null,
        recoveryData: cachedData,
        isRecoveryCompleted: true,
        isInitialized: true,
      });
      return cachedData;
    }

    updateRecoveryState({ isLoading: true, error: null });

    try {
      console.log('🔄 Fetching table orders for recovery...');
      
      // API çağrısı - useApiManager ile
      const result = await apiCall(
        'fetch-table-orders-recovery',
        async () => {
          const response = await apiClient.get('/cart/table-orders-recovery');
          
          // Debouncing kontrolü - null response handle et
          if (response === null) {
            console.log('⚠️ API response null (debouncing), throwing error for retry...');
            throw new Error('API response is null due to debouncing');
          }
          
          // Response format kontrolü
          if (!response || typeof response !== 'object') {
            throw new Error(`Invalid response format: ${JSON.stringify(response)}`);
          }
          
          const recoveryData = response as TableOrdersRecoveryData;

          if (recoveryData.success) {
            return recoveryData;
          } else {
            throw new Error(recoveryData.message || 'Failed to retrieve table orders');
          }
        },
        {
          cacheKey: userCacheKey,
          cacheTTL: 5, // 5 dakika cache
          skipDuplicate: true,
          retryCount: 2,
        }
      );

      if (result) {
        console.log(`✅ Recovery completed: ${result.totalActiveTables} active table orders found`);
        
        updateRecoveryState({
          isLoading: false,
          error: null,
          recoveryData: result,
          isRecoveryCompleted: true,
          isInitialized: true,
        });

        // Cache'e kaydet (user scoped)
        setCachedData(userCacheKey, result, 5);

        return result;
      }

      return null;
    } catch (error: any) {
      const errorMessage = error?.response?.data?.message ?? error.message ?? 'Unknown error during recovery';
      console.error('❌ Table orders recovery failed:', errorMessage);
      
      updateRecoveryState({
        isLoading: false,
        error: errorMessage,
        recoveryData: null,
        isRecoveryCompleted: false,
        isInitialized: false,
      });

      return null;
    }
  }, [user, apiCall, setCachedData, getCachedData, updateRecoveryState]);

  /**
   * Manuel refresh için - sadece gerektiğinde kullanılır
   */
  const refreshTableOrders = useCallback(async (): Promise<TableOrdersRecoveryData | null> => {
    if (!user) return null;
    
    console.log('🔄 Manual refresh of table orders...');
    
    // Cache'i temizle (user scoped)
    const userCacheKey = `table-orders-recovery:${user.id}`;
    setCachedData(userCacheKey, null as unknown as TableOrdersRecoveryData, 0);
    
    // Reset initialization flag to force fresh fetch
    updateRecoveryState({ isInitialized: false });
    
    return await fetchTableOrdersRecovery();
  }, [user, fetchTableOrdersRecovery, setCachedData, updateRecoveryState]);

  /**
   * Belirli bir masa için sipariş bilgilerini getir
   */
  const getOrderForTable = useCallback((tableNumber: number): TableOrderRecovery | null => {
    if (!recoveryStateRef.current.recoveryData) return null;
    
    return recoveryStateRef.current.recoveryData.tableOrders.find(
      order => order.tableNumber === tableNumber
    ) ?? null;
  }, []);

  /**
   * Tüm aktif masa numaralarını getir
   */
  const getActiveTableNumbers = useCallback((): number[] => {
    if (!recoveryStateRef.current.recoveryData) return [];
    
    return recoveryStateRef.current.recoveryData.tableOrders
      .map(order => order.tableNumber)
      .filter((tableNumber): tableNumber is number => tableNumber !== undefined)
      .sort((a, b) => a - b);
  }, []);

  /**
   * Recovery durumunu sıfırla
   */
  const resetRecovery = useCallback(() => {
    // Cache'i temizle (user scoped)
    if (user?.id) {
      const userCacheKey = `table-orders-recovery:${user.id}`;
      setCachedData(userCacheKey, null as unknown as TableOrdersRecoveryData, 0);
    }
    
    updateRecoveryState({
      isLoading: false,
      error: null,
      recoveryData: null,
      isRecoveryCompleted: false,
      isInitialized: false,
    });
  }, [setCachedData, user, updateRecoveryState]);

  /**
   * Sayfa yüklendiğinde otomatik recovery yapar
   * OPTIMIZATION: Sadece user değiştiğinde çalışır
   */
  useEffect(() => {
    let isMounted = true;

    const performRecovery = async () => {
      // Sadece user varsa ve henüz initialize edilmemişse çalış
      if (!user || recoveryStateRef.current.isInitialized) {
        console.log('🔄 Recovery skipped:', { 
          hasUser: !!user, 
          isInitialized: recoveryStateRef.current.isInitialized 
        });
        return;
      }

      console.log('🔄 Starting table orders recovery from backend...');
      await fetchTableOrdersRecovery();
    };

    if (isMounted) {
      performRecovery();
    }

    return () => {
      isMounted = false;
    };
  }, [user, fetchTableOrdersRecovery]); // fetchTableOrdersRecovery dependency'si eklendi

  // Token güncellenince recovery'yi tekrar dene (örn. login/refresh sonrası)
  useEffect(() => {
    const handler = () => {
      if (user) {
        console.log('🔄 auth-token-updated received, refreshing table orders');
        refreshTableOrders();
      }
    };
    if (typeof window !== 'undefined' && typeof window.addEventListener === 'function') {
      window.addEventListener('auth-token-updated', handler);
      return () => window.removeEventListener('auth-token-updated', handler);
    }
    return () => {};
  }, [user, refreshTableOrders]);

  return {
    // State
    isLoading: recoveryStateRef.current.isLoading,
    error: recoveryStateRef.current.error,
    recoveryData: recoveryStateRef.current.recoveryData,
    isRecoveryCompleted: recoveryStateRef.current.isRecoveryCompleted,
    isInitialized: recoveryStateRef.current.isInitialized,
    
    // Actions
    fetchTableOrdersRecovery,
    refreshTableOrders,
    getOrderForTable,
    getActiveTableNumbers,
    resetRecovery,
    
    // Helper properties
    hasActiveOrders: (recoveryStateRef.current.recoveryData?.totalActiveTables ?? 0) > 0,
    totalActiveTables: recoveryStateRef.current.recoveryData?.totalActiveTables ?? 0,
  };
};
