import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react';
import { Alert } from 'react-native';
import { useTranslation } from 'react-i18next';
import { apiClient } from '../services/api/config';

export interface SystemConfig {
  operationMode: 'online-only' | 'offline-only' | 'hybrid';
  offlineSettings: {
    enabled: boolean;
    syncInterval: number;
    maxOfflineDays: number;
    autoSync: boolean;
  };
  tseSettings: {
    required: boolean;
    offlineAllowed: boolean;
    maxOfflineTransactions: number;
  };
  printerSettings: {
    required: boolean;
    offlineQueue: boolean;
    maxQueueSize: number;
  };
}

interface SystemContextType {
  config: SystemConfig | null;
  isOnline: boolean;
  isOfflineMode: boolean;
  isHybridMode: boolean;
  canWorkOffline: boolean;
  loading: boolean;
  refreshConfig: () => Promise<void>;
  checkConnectivity: () => Promise<boolean>;
}

const SystemContext = createContext<SystemContextType | undefined>(undefined);

export const useSystem = () => {
  const context = useContext(SystemContext);
  if (!context) {
    throw new Error('useSystem must be used within a SystemProvider');
  }
  return context;
};

interface SystemProviderProps {
  children: ReactNode;
}

export const SystemProvider: React.FC<SystemProviderProps> = ({ children }) => {
  const { t } = useTranslation();
  const [config, setConfig] = useState<SystemConfig | null>(null);
  const [isOnline, setIsOnline] = useState(true);
  const [loading, setLoading] = useState(true);

  // Sistem konfigürasyonunu yükle
  const loadConfig = async () => {
    try {
      const response = await apiClient.get<SystemConfig>('/system/config');
      setConfig(response.data);
    } catch (error) {
      console.error('System config load failed:', error);
      // Varsayılan konfigürasyon
      setConfig({
        operationMode: 'online-only',
        offlineSettings: {
          enabled: false,
          syncInterval: 5,
          maxOfflineDays: 7,
          autoSync: false
        },
        tseSettings: {
          required: true,
          offlineAllowed: false,
          maxOfflineTransactions: 100
        },
        printerSettings: {
          required: true,
          offlineQueue: false,
          maxQueueSize: 50
        }
      });
    }
  };

  // Bağlantı durumunu kontrol et
  const checkConnectivity = async (): Promise<boolean> => {
    try {
      const response = await fetch('/api/health', { 
        method: 'HEAD',
        signal: AbortSignal.timeout(5000) // 5 saniye timeout
      });
      const online = response.ok;
      setIsOnline(online);
      return online;
    } catch (error) {
      setIsOnline(false);
      return false;
    }
  };

  // Konfigürasyonu yenile
  const refreshConfig = async () => {
    setLoading(true);
    await loadConfig();
    await checkConnectivity();
    setLoading(false);
  };

  // Mod kontrolü
  const isOfflineMode = config?.operationMode === 'offline-only';
  const isHybridMode = config?.operationMode === 'hybrid';
  const canWorkOffline = isOfflineMode || (isHybridMode && config?.offlineSettings.enabled);

  // İlk yükleme
  useEffect(() => {
    refreshConfig();
  }, []);

  // Periyodik bağlantı kontrolü
  useEffect(() => {
    if (!config) return;

    const interval = setInterval(async () => {
      const online = await checkConnectivity();
      
      // Bağlantı durumu değiştiğinde kullanıcıyı bilgilendir
      if (online !== isOnline) {
        if (online) {
          // Çevrimdışı modda çalışıyorsa ve bağlantı geldiyse
          if (isOfflineMode || (isHybridMode && !isOnline)) {
            Alert.alert(
              t('system.connection_restored'),
              t('system.sync_in_progress'),
              [{ text: 'Tamam' }]
            );
          }
        } else {
          // Çevrimdışı modda değilse ve bağlantı kesildiyse
          if (!canWorkOffline) {
            Alert.alert(
              t('system.connection_lost'),
              t('system.offline_mode_required'),
              [{ text: 'Tamam' }]
            );
          }
        }
      }
    }, 30000); // Her 30 saniyede bir kontrol

    return () => clearInterval(interval);
  }, [config, isOnline, isOfflineMode, isHybridMode, canWorkOffline]);

  // Otomatik senkronizasyon (hybrid modda)
  useEffect(() => {
    if (!config || !isHybridMode || !config.offlineSettings.autoSync || !isOnline) {
      return;
    }

    const syncInterval = setInterval(async () => {
      try {
        // Çevrimdışı verileri senkronize et
        const { offlineManager } = await import('../services/offline/OfflineManager');
        const result = await offlineManager.syncAllOfflineData();
        
        if (result.errors.length > 0) {
          console.warn('Sync errors:', result.errors);
        }
        
        if (result.paymentsSynced > 0 || result.receiptsSynced > 0) {
          console.log(`Synced: ${result.paymentsSynced} payments, ${result.receiptsSynced} receipts`);
        }
      } catch (error) {
        console.error('Auto sync failed:', error);
      }
    }, config.offlineSettings.syncInterval * 60 * 1000); // Dakikayı milisaniyeye çevir

    return () => clearInterval(syncInterval);
  }, [config, isHybridMode, isOnline]);

  const value: SystemContextType = {
    config,
    isOnline,
    isOfflineMode,
    isHybridMode,
    canWorkOffline,
    loading,
    refreshConfig,
    checkConnectivity
  };

  return (
    <SystemContext.Provider value={value}>
      {children}
    </SystemContext.Provider>
  );
}; 