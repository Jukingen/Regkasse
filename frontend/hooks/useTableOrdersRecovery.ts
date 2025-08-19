// Bu hook, sayfa yenileme (F5) sonrası masa siparişlerini geri yüklemek için kullanılır
// RKSV uyumlu güvenlik kontrolü ve kullanıcı bazlı veri erişimi sağlar
// OPTIMIZATION: Sürekli API çağrısı yerine sadece gerekli durumlarda fetch yapar

import { useState, useEffect, useCallback } from 'react';

import { useAuth } from '../contexts/AuthContext';
import { apiClient } from '../services/api/config';

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
  isInitialized: boolean; // Yeni: Sadece bir kere fetch yapılsın
}

/**
 * F5 sonrası masa siparişlerini geri yükleme hook'u
 * RKSV kurallarına uygun güvenlik kontrolü yapar
 * Yalnızca kullanıcının kendi masa siparişlerini getirir
 * OPTIMIZATION: Sürekli API çağrısı yerine sadece gerekli durumlarda fetch yapar
 */
export const useTableOrdersRecovery = () => {
  const { user } = useAuth();
  const [recoveryState, setRecoveryState] = useState<RecoveryState>({
    isLoading: false,
    error: null,
    recoveryData: null,
    isRecoveryCompleted: false,
    isInitialized: false, // Yeni: Initialization flag
  });

  // ⚠️ GÜNCELLEME: AsyncStorage yerine tamamen backend-first yaklaşım
  // Recovery state'i sadece memory'de tutuyoruz, backend'den her zaman fresh data alıyoruz
  // Bu daha güvenli ve tutarlı bir yaklaşım

  /**
   * Backend'den tüm aktif masa siparişlerini getir
   * RKSV uyumlu - yalnızca kullanıcının kendi siparişleri
   * OPTIMIZATION: Sadece gerekli durumlarda fetch yapar
   */
  const fetchTableOrdersRecovery = useCallback(async (): Promise<TableOrdersRecoveryData | null> => {
    if (!user) {
      console.warn('User not authenticated for table orders recovery');
      return null;
    }

    // OPTIMIZATION: Eğer zaten fetch edildiyse ve data varsa, tekrar fetch yapma
    if (recoveryState.isInitialized && recoveryState.recoveryData) {
      console.log('🔄 Table orders already fetched, returning cached data');
      return recoveryState.recoveryData;
    }

    setRecoveryState(prev => ({ ...prev, isLoading: true, error: null }));

    try {
      console.log('🔄 Fetching table orders for recovery...');
      
      const response = await apiClient.get('/cart/table-orders-recovery');
      const recoveryData = response as TableOrdersRecoveryData;

      if (recoveryData.success) {
        console.log(`✅ Recovery completed: ${recoveryData.totalActiveTables} active table orders found`);
        
        setRecoveryState({
          isLoading: false,
          error: null,
          recoveryData,
          isRecoveryCompleted: true,
          isInitialized: true, // Yeni: Fetch tamamlandı olarak işaretle
        });

        return recoveryData;
      } else {
        throw new Error(recoveryData.message || 'Failed to retrieve table orders');
      }
    } catch (error: any) {
      const errorMessage = error?.response?.data?.message ?? error.message ?? 'Unknown error during recovery';
      console.error('❌ Table orders recovery failed:', errorMessage);
      
      setRecoveryState({
        isLoading: false,
        error: errorMessage,
        recoveryData: null,
        isRecoveryCompleted: false,
        isInitialized: false, // Hata durumunda false bırak
      });

      return null;
    }
  }, [user, recoveryState.isInitialized, recoveryState.recoveryData]);

  /**
   * Manuel refresh için - sadece gerektiğinde kullanılır
   */
  const refreshTableOrders = useCallback(async (): Promise<TableOrdersRecoveryData | null> => {
    if (!user) return null;
    
    console.log('🔄 Manual refresh of table orders...');
    
    // Reset initialization flag to force fresh fetch
    setRecoveryState(prev => ({ ...prev, isInitialized: false }));
    
    return await fetchTableOrdersRecovery();
  }, [user, fetchTableOrdersRecovery]);

  /**
   * Belirli bir masa için sipariş bilgilerini getir
   */
  const getOrderForTable = useCallback((tableNumber: number): TableOrderRecovery | null => {
    if (!recoveryState.recoveryData) return null;
    
    return recoveryState.recoveryData.tableOrders.find(
      order => order.tableNumber === tableNumber
    ) || null;
  }, [recoveryState.recoveryData]);

  /**
   * Tüm aktif masa numaralarını getir
   */
  const getActiveTableNumbers = useCallback((): number[] => {
    if (!recoveryState.recoveryData) return [];
    
    return recoveryState.recoveryData.tableOrders
      .map(order => order.tableNumber)
      .filter((tableNumber): tableNumber is number => tableNumber !== undefined)
      .sort((a, b) => a - b);
  }, [recoveryState.recoveryData]);

  /**
   * Recovery durumunu sıfırla - Artık sadece memory state'i temizliyoruz
   */
  const resetRecovery = useCallback(() => {
    setRecoveryState({
      isLoading: false,
      error: null,
      recoveryData: null,
      isRecoveryCompleted: false,
      isInitialized: false, // Reset initialization flag
    });
  }, []);

  /**
   * Sayfa yüklendiğinde otomatik recovery yapar
   * OPTIMIZATION: Sadece user değiştiğinde ve henüz initialize edilmemişse çalışır
   */
  useEffect(() => {
    let isMounted = true;

    const performRecovery = async () => {
      if (!user || recoveryState.isInitialized) return;

      console.log('🔄 Starting table orders recovery from backend...');
      // Direkt backend'den en güncel data'yı al
      await fetchTableOrdersRecovery();
    };

    if (isMounted) {
      performRecovery();
    }

    return () => {
      isMounted = false;
    };
  }, [user]); // OPTIMIZATION: fetchTableOrdersRecovery dependency'sini kaldırdık

  return {
    // State
    isLoading: recoveryState.isLoading,
    error: recoveryState.error,
    recoveryData: recoveryState.recoveryData,
    isRecoveryCompleted: recoveryState.isRecoveryCompleted,
    isInitialized: recoveryState.isInitialized, // Yeni: Initialization status
    
    // Actions
    fetchTableOrdersRecovery,
    refreshTableOrders, // Yeni: Manuel refresh fonksiyonu
    getOrderForTable,
    getActiveTableNumbers,
    resetRecovery,
    
    // Helper properties
    hasActiveOrders: (recoveryState.recoveryData?.totalActiveTables ?? 0) > 0,
    totalActiveTables: recoveryState.recoveryData?.totalActiveTables ?? 0,
  };
};
