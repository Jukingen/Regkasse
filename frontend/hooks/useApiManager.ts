// Türkçe Açıklama: Bu hook tüm API çağrılarını merkezi olarak yönetir ve sonsuz döngü sorunlarını önler.
// RKSV uyumlu güvenlik kontrolü ve akıllı cache yönetimi sağlar.

import { useState, useCallback, useRef, useEffect } from 'react';
import NetInfo from '@react-native-community/netinfo';
import { API_BASE_URL } from '../services/api/config';
import { storage } from '../utils/storage';
import { useAuth } from '../contexts/AuthContext';

// API çağrı durumu
interface ApiCallStatus {
  isLoading: boolean;
  lastCall: number;
  error: string | null;
  retryCount: number;
}

// Cache item interface
interface CacheItem<T> {
  data: T;
  timestamp: number;
  expiresAt: number;
}

// API Manager State
interface ApiManagerState {
  isOnline: boolean;
  lastTokenCheck: number;
  tokenExpiryTime: number | null;
  activeCalls: Map<string, ApiCallStatus>;
  cache: Map<string, CacheItem<any>>;
}

export const useApiManager = () => {
  const { user, logout } = useAuth();
  const [state, setState] = useState<ApiManagerState>({
    isOnline: true,
    lastTokenCheck: 0,
    tokenExpiryTime: null,
    activeCalls: new Map(),
    cache: new Map(),
  });

  // Ref'ler ile sürekli re-render'ı önle
  const stateRef = useRef(state);
  const timeoutRefs = useRef<Map<string, NodeJS.Timeout>>(new Map());

  // Global interval guard to avoid multiple timers across many hook instances
  // Module-scoped flags
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const intervalsInitializedRef = useRef<boolean>((globalThis as any).__apiManagerIntervalsInitialized__ || false);

  // State güncelleme fonksiyonu - batch update
  const updateState = useCallback((updates: Partial<ApiManagerState>) => {
    setState(prev => {
      const newState = { ...prev, ...updates };
      stateRef.current = newState;
      return newState;
    });
  }, []);

  // Token expire kontrolü - sadece gerektiğinde
  const checkTokenExpiry = useCallback(async (): Promise<boolean> => {
    const now = Date.now();
    const lastCheck = stateRef.current.lastTokenCheck;

    // 5 dakikada bir kontrol et
    if (now - lastCheck < 5 * 60 * 1000) {
      return false; // Henüz kontrol edilmedi
    }

    try {
      const token = await storage.getItem('token');
      if (!token) {
        console.log('⚠️ Token bulunamadı');
        return true;
      }

      // JWT decode ve expire kontrolü
      const payload = JSON.parse(atob(token.split('.')[1]));
      const currentTime = Math.floor(now / 1000);

      if (payload.exp && payload.exp < currentTime) {
        console.log('⚠️ Token expired');
        return true;
      }

      // Token geçerli, son kontrol zamanını güncelle
      updateState({
        lastTokenCheck: now,
        tokenExpiryTime: payload.exp ? payload.exp * 1000 : null,
      });

      return false;
    } catch (error) {
      console.error('Token expire check error:', error);
      return true;
    }
  }, [updateState]);

  // Online/offline durumu kontrolü
  const checkOnlineStatus = useCallback(async (): Promise<boolean> => {
    try {
      // 1. Önce native network durumunu kontrol et (Hızlı ve maliyetsiz)
      const netInfo = await NetInfo.fetch();
      if (netInfo.isConnected === false) {
        updateState({ isOnline: false });
        // Eğer native olarak offline ise, fetch denemeye gerek yok
        return false;
      }

      // 2. İnternet var görünüyor, backend'e gerçekten erişebiliyor muyuz? (Health check)
      const apiRoot = API_BASE_URL.replace(/\/api\/?$/, '');
      const healthUrl = `${apiRoot}/health`;

      // AbortController ile timeout yönetimi (RN'de AbortSignal.timeout henüz tam desteklenmeyebilir)
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), 5000);

      try {
        const response = await fetch(healthUrl, {
          method: 'GET',
          headers: { 'Accept': 'text/plain' },
          signal: controller.signal
        });
        clearTimeout(timeoutId);

        const ok = response.ok;
        updateState({ isOnline: ok });
        return ok;
      } catch (fetchError) {
        clearTimeout(timeoutId);
        throw fetchError;
      }
    } catch (error) {
      console.log('⚠️ Health check failed:', error);
      updateState({ isOnline: false });
      return false;
    }
  }, [updateState]);

  // Cache yönetimi
  const getCachedData = useCallback(<T>(key: string): T | null => {
    const cached = stateRef.current.cache.get(key);
    if (!cached) return null;

    const now = Date.now();
    if (now > cached.expiresAt) {
      // Cache expired, temizle
      setState(prev => {
        const newCache = new Map(prev.cache);
        newCache.delete(key);
        return { ...prev, cache: newCache };
      });
      return null;
    }

    return cached.data;
  }, []);

  const setCachedData = useCallback(<T>(key: string, data: T, ttlMinutes: number = 5) => {
    const now = Date.now();
    const expiresAt = now + (ttlMinutes * 60 * 1000);

    setState(prev => {
      const newCache = new Map(prev.cache);
      newCache.set(key, { data, timestamp: now, expiresAt });
      return { ...prev, cache: newCache };
    });
  }, []);

  // API çağrı wrapper - duplicate call'ları önler
  const apiCall = useCallback(async <T>(
    key: string,
    apiFunction: () => Promise<T>,
    options: {
      cacheKey?: string;
      cacheTTL?: number;
      retryCount?: number;
      skipDuplicate?: boolean;
    } = {}
  ): Promise<T> => {
    const {
      cacheKey,
      cacheTTL = 5,
      retryCount = 3,
      skipDuplicate = true,
    } = options;

    // Cache kontrolü
    if (cacheKey) {
      const cached = getCachedData<T>(cacheKey);
      if (cached) {
        console.log(`✅ Cache hit for ${cacheKey}`);
        return cached;
      }
    }

    // Duplicate call kontrolü - daha akıllı kontrol
    if (skipDuplicate) {
      const activeCall = stateRef.current.activeCalls.get(key);
      if (activeCall && activeCall.isLoading) {
        // Eğer son call'dan 2 saniye geçtiyse duplicate olarak kabul etme
        const timeSinceLastCall = Date.now() - activeCall.lastCall;
        if (timeSinceLastCall < 2000) { // 2 saniye
          console.log(`⚠️ Duplicate API call prevented for ${key} (last call: ${timeSinceLastCall}ms ago)`);
          throw new Error('Duplicate API call prevented');
        } else {
          console.log(`🔄 Allowing API call for ${key} (last call: ${timeSinceLastCall}ms ago)`);
        }
      }
    }

    // Active call'ı kaydet
    setState(prev => {
      const newActiveCalls = new Map(prev.activeCalls);
      newActiveCalls.set(key, {
        isLoading: true,
        lastCall: Date.now(),
        error: null,
        retryCount: 0,
      });
      return { ...prev, activeCalls: newActiveCalls };
    });

    try {
      // Token kontrolü
      const tokenExpired = await checkTokenExpiry();
      if (tokenExpired) {
        console.log('❌ Token expired, logging out');
        logout();
        throw new Error('Session expired');
      }

      // Online durum kontrolü
      const isOnline = await checkOnlineStatus();
      if (!isOnline) {
        throw new Error('Network offline');
      }

      // API çağrısını yap
      const result = await apiFunction();

      // Cache'e kaydet
      if (cacheKey) {
        setCachedData(cacheKey, result, cacheTTL);
      }

      // Success state
      setState(prev => {
        const newActiveCalls = new Map(prev.activeCalls);
        newActiveCalls.set(key, {
          isLoading: false,
          lastCall: Date.now(),
          error: null,
          retryCount: 0,
        });
        return { ...prev, activeCalls: newActiveCalls };
      });

      return result;

    } catch (error: any) {
      const currentCall = stateRef.current.activeCalls.get(key);
      const newRetryCount = (currentCall?.retryCount || 0) + 1;

      if (newRetryCount <= retryCount) {
        // Retry logic
        console.log(`🔄 Retrying API call ${key} (${newRetryCount}/${retryCount})`);

        const timeoutId = setTimeout(() => {
          apiCall(key, apiFunction, options);
        }, Math.pow(2, newRetryCount) * 1000); // Exponential backoff

        timeoutRefs.current.set(key, timeoutId);
      }

      // Error state
      setState(prev => {
        const newActiveCalls = new Map(prev.activeCalls);
        newActiveCalls.set(key, {
          isLoading: false,
          lastCall: Date.now(),
          error: error.message,
          retryCount: newRetryCount,
        });
        return { ...prev, activeCalls: newActiveCalls };
      });

      throw error;
    }
  }, [checkTokenExpiry, checkOnlineStatus, getCachedData, setCachedData, logout]);

  // Cleanup fonksiyonları
  const clearCache = useCallback(() => {
    setState(prev => ({ ...prev, cache: new Map() }));
  }, []);

  const clearActiveCalls = useCallback(() => {
    // Tüm timeout'ları temizle
    timeoutRefs.current.forEach(timeout => clearTimeout(timeout));
    timeoutRefs.current.clear();

    setState(prev => ({ ...prev, activeCalls: new Map() }));
  }, []);

  // Component unmount'ta cleanup
  useEffect(() => {
    return () => {
      clearActiveCalls();
    };
  }, [clearActiveCalls]);

  // Periyodik kontroller - sadece gerektiğinde
  useEffect(() => {
    // Prevent creating multiple intervals if multiple components mount
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    if (!(globalThis as any).__apiManagerIntervalsInitialized__) {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      (globalThis as any).__apiManagerIntervalsInitialized__ = true;

      const tokenCheckInterval = setInterval(async () => {
        await checkTokenExpiry();
      }, 5 * 60 * 1000); // 5 dakika

      const onlineCheckInterval = setInterval(async () => {
        await checkOnlineStatus();
      }, 5 * 60 * 1000); // 5 dakika (Reduced from 30s to prevent spam)

      // Store on globalThis for cleanup on HMR if needed
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      (globalThis as any).__apiManagerTokenInterval__ = tokenCheckInterval;
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      (globalThis as any).__apiManagerOnlineInterval__ = onlineCheckInterval;
    }

    return () => {
      // We don't clear intervals here to keep a single global timer across mounts
      // Cleanup can be handled on app teardown or HMR
    };
  }, [checkTokenExpiry, checkOnlineStatus]);

  return {
    // State
    isOnline: state.isOnline,
    isTokenExpired: state.tokenExpiryTime ? Date.now() > state.tokenExpiryTime : false,

    // API Management
    apiCall,
    getCachedData,
    setCachedData,

    // Cache Management
    clearCache,
    clearActiveCalls,

    // Utility
    checkTokenExpiry,
    checkOnlineStatus,

    // Active calls info
    getActiveCallStatus: (key: string) => state.activeCalls.get(key),
    hasActiveCalls: state.activeCalls.size > 0,
  };
};
