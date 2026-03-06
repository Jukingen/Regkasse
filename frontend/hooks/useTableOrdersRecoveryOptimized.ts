// Türkçe Açıklama: Optimize edilmiş masa siparişleri recovery hook'u - tek seferlik çağrı, 503'te retry yok
// TABLE_ORDERS_MISSING (503) durumunda retry YAPILMAZ, kullanıcıya bilgi mesajı gösterilir

import { useState, useCallback, useRef, useEffect } from 'react';
import { useApiManager } from './useApiManager';
import { useAuth } from '../contexts/AuthContext';
import { apiClient } from '../services/api/config';

// Table order recovery interfaces (backend table-orders-recovery items include selectedModifiers)
export interface TableOrderRecoveryItem {
  productId: string;
  productName: string;
  quantity: number;
  price: number;
  total: number;
  notes?: string;
  selectedModifiers?: Array<{ id: string; name: string; price: number }>;
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
  /** 503/TABLE_ORDERS_MISSING durumunda kullanıcıya gösterilecek bilgi mesajı */
  provisioningMessage: string | null;
}

/** Session-level guard ve cache: Çoklu hook instance'larında duplicate fetch önler */
const sessionStore: {
  lastFetchedUserId: string | null;
  cachedData: TableOrdersRecoveryData | null;
  fetchInProgress: boolean;
} = { lastFetchedUserId: null, cachedData: null, fetchInProgress: false };

/**
 * F5 sonrası masa siparişlerini geri yükleme hook'u - Tek kaynak, tek çağrı
 * 503 + TABLE_ORDERS_MISSING: retry YOK, provisioning mesajı göster
 */
export const useTableOrdersRecoveryOptimized = () => {
  const { user } = useAuth();
  const { getCachedData, setCachedData } = useApiManager();

  const [recoveryState, setRecoveryState] = useState<RecoveryState>({
    isLoading: false,
    error: null,
    recoveryData: null,
    isRecoveryCompleted: false,
    isInitialized: false,
    provisioningMessage: null,
  });

  const recoveryStateRef = useRef(recoveryState);

  const updateRecoveryState = useCallback((updates: Partial<RecoveryState>) => {
    setRecoveryState(prev => {
      const newState = { ...prev, ...updates };
      recoveryStateRef.current = newState;
      return newState;
    });
  }, []);

  /**
   * Backend'den tüm aktif masa siparişlerini getir - apiClient direkt kullanım (retry YOK)
   * 503/TABLE_ORDERS_MISSING geldiğinde retry yapılmaz.
   */
  const fetchTableOrdersRecovery = useCallback(async (): Promise<TableOrdersRecoveryData | null> => {
    if (!user) {
      console.warn('User not authenticated for table orders recovery');
      return null;
    }

    // Session guard: Başka instance zaten fetch ettiyse modül cache'inden dön
    if (sessionStore.lastFetchedUserId === user.id && sessionStore.cachedData) {
      updateRecoveryState({
        recoveryData: sessionStore.cachedData,
        isRecoveryCompleted: true,
        isInitialized: true,
      });
      return sessionStore.cachedData;
    }

    if (recoveryStateRef.current.isLoading || sessionStore.fetchInProgress) {
      return null;
    }

    const userCacheKey = `table-orders-recovery:${user.id}`;
    const cachedData = getCachedData<TableOrdersRecoveryData>(userCacheKey);
    if (cachedData) {
      sessionStore.lastFetchedUserId = user.id;
      sessionStore.cachedData = cachedData;
      updateRecoveryState({
        isLoading: false,
        error: null,
        recoveryData: cachedData,
        isRecoveryCompleted: true,
        isInitialized: true,
        provisioningMessage: null,
      });
      return cachedData;
    }

    updateRecoveryState({ isLoading: true, error: null, provisioningMessage: null });
    sessionStore.lastFetchedUserId = user.id;
    sessionStore.fetchInProgress = true;

    try {
      console.log('🔄 Fetching table orders for recovery (single call)...');

      // Direkt apiClient - useApiManager.apiCall KULLANILMAZ (503'te retry yapmasın)
      const response = await apiClient.get('/cart/table-orders-recovery');

      if (!response || typeof response !== 'object') {
        throw new Error(`Invalid response format: ${JSON.stringify(response)}`);
      }

      const recoveryData = response as TableOrdersRecoveryData;

      if (!recoveryData.success) {
        throw new Error(recoveryData.message || 'Failed to retrieve table orders');
      }

      console.log(`✅ Recovery completed: ${recoveryData.totalActiveTables} active table orders found`);

      updateRecoveryState({
        isLoading: false,
        error: null,
        recoveryData,
        isRecoveryCompleted: true,
        isInitialized: true,
        provisioningMessage: null,
      });

      sessionStore.cachedData = recoveryData;
      sessionStore.fetchInProgress = false;
      setCachedData(userCacheKey, recoveryData, 5);
      return recoveryData;
    } catch (error: any) {
      sessionStore.fetchInProgress = false;
      const errorMessage = error?.response?.data?.message ?? error.message ?? 'Unknown error during recovery';
      const errorCode = error?.response?.data?.errorCode;
      const status = error?.response?.status;
      const is503orTableMissing = errorCode === 'TABLE_ORDERS_MISSING' || status === 503;

      console.error('❌ Table orders recovery failed:', errorMessage);

      // 503 / TABLE_ORDERS_MISSING: RETRY YOK, fallback + kullanıcı bilgi mesajı
      if (is503orTableMissing) {
        console.warn('⚠️ TABLE_ORDERS_MISSING/503: No retry, falling back to empty recovery.');

        const fallbackData: TableOrdersRecoveryData = {
          success: true,
          message: 'Fallback recovery mode activated',
          userId: user.id,
          tableOrders: [],
          totalActiveTables: 0,
          retrievedAt: new Date().toISOString(),
        };

        sessionStore.cachedData = fallbackData;
        updateRecoveryState({
          isLoading: false,
          error: null,
          recoveryData: fallbackData,
          isRecoveryCompleted: true,
          isInitialized: true,
          provisioningMessage: 'System wird vorbereitet. Tische werden synchronisiert.',
        });

        return fallbackData;
      }

      // Diğer hatalar (network, 500 vs): Hata göster, retry yok (tek çağrı kuralı)
      updateRecoveryState({
        isLoading: false,
        error: errorMessage,
        recoveryData: null,
        isRecoveryCompleted: true,
        isInitialized: true,
        provisioningMessage: null,
      });

      return null;
    }
  }, [user, getCachedData, setCachedData, updateRecoveryState]);

  const refreshTableOrders = useCallback(async (): Promise<TableOrdersRecoveryData | null> => {
    if (!user) return null;

    sessionStore.lastFetchedUserId = null;
    sessionStore.cachedData = null;
    sessionStore.fetchInProgress = false;

    const userCacheKey = `table-orders-recovery:${user.id}`;
    setCachedData(userCacheKey, null as unknown as TableOrdersRecoveryData, 0);
    updateRecoveryState({ isInitialized: false, provisioningMessage: null });

    return await fetchTableOrdersRecovery();
  }, [user, fetchTableOrdersRecovery, setCachedData, updateRecoveryState]);

  const getOrderForTable = useCallback((tableNumber: number): TableOrderRecovery | null => {
    if (!recoveryStateRef.current.recoveryData) return null;
    return recoveryStateRef.current.recoveryData.tableOrders.find(
      order => order.tableNumber === tableNumber
    ) ?? null;
  }, []);

  const getActiveTableNumbers = useCallback((): number[] => {
    if (!recoveryStateRef.current.recoveryData) return [];
    return recoveryStateRef.current.recoveryData.tableOrders
      .map(order => order.tableNumber)
      .filter((tableNumber): tableNumber is number => tableNumber !== undefined)
      .sort((a, b) => a - b);
  }, []);

  const resetRecovery = useCallback(() => {
    sessionStore.lastFetchedUserId = null;
    sessionStore.cachedData = null;
    sessionStore.fetchInProgress = false;
    if (user?.id) {
      setCachedData(`table-orders-recovery:${user.id}`, null as unknown as TableOrdersRecoveryData, 0);
    }
    updateRecoveryState({
      isLoading: false,
      error: null,
      recoveryData: null,
      isRecoveryCompleted: false,
      isInitialized: false,
      provisioningMessage: null,
    });
  }, [setCachedData, user, updateRecoveryState]);

  /** Mount'ta tek seferlik fetch - user değişince yeniden dene */
  useEffect(() => {
    if (!user) return;

    // Yeni kullanıcı login olduysa session store sıfırla
    if (sessionStore.lastFetchedUserId && sessionStore.lastFetchedUserId !== user.id) {
      sessionStore.lastFetchedUserId = null;
      sessionStore.cachedData = null;
    }

    if (recoveryStateRef.current.isInitialized) return;
    if (sessionStore.lastFetchedUserId === user.id && sessionStore.cachedData) return;

    fetchTableOrdersRecovery();
  }, [user?.id, fetchTableOrdersRecovery]);

  return {
    isLoading: recoveryStateRef.current.isLoading,
    error: recoveryStateRef.current.error,
    recoveryData: recoveryStateRef.current.recoveryData,
    isRecoveryCompleted: recoveryStateRef.current.isRecoveryCompleted,
    isInitialized: recoveryStateRef.current.isInitialized,
    provisioningMessage: recoveryStateRef.current.provisioningMessage,
    fetchTableOrdersRecovery,
    refreshTableOrders,
    getOrderForTable,
    getActiveTableNumbers,
    resetRecovery,
    hasActiveOrders: (recoveryStateRef.current.recoveryData?.totalActiveTables ?? 0) > 0,
    totalActiveTables: recoveryStateRef.current.recoveryData?.totalActiveTables ?? 0,
  };
};
