import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react';
import { Alert } from 'react-native';
import { useTranslation } from 'react-i18next';
import { apiClient } from '../services/api/config';
import NetInfo from '@react-native-community/netinfo';
import AsyncStorage from '@react-native-async-storage/async-storage';

export interface SystemConfiguration {
  operationMode: 'online-only' | 'offline-only' | 'hybrid';
  tseEnabled: boolean;
  printerEnabled: boolean;
  finanzOnlineEnabled: boolean;
  offlineSettings: {
    enabled: boolean;
    autoSync: boolean;
    syncInterval: number; // dakika
    maxOfflineDays: number;
  };
  tseSettings: {
    model: string;
    connectionType: 'usb' | 'network';
    timeout: number; // saniye
    dailyReportTime: string; // HH:MM
    required: boolean;
    offlineAllowed: boolean;
    maxOfflineTransactions: number;
  };
  printerSettings: {
    model: string;
    connectionType: 'usb' | 'network' | 'bluetooth';
    paperSize: '80mm' | '58mm';
    autoPrint: boolean;
    required: boolean;
    offlineQueue: boolean;
    maxQueueSize: number;
  };
  finanzOnlineSettings: {
    apiUrl: string;
    clientId: string;
    clientSecret: string;
    certificatePath: string;
  };
}

interface SystemContextType {
  isOnline: boolean;
  systemConfig: SystemConfiguration;
  updateSystemConfig: (config: Partial<SystemConfiguration>) => Promise<void>;
  loadSystemConfig: () => Promise<void>;
  config: SystemConfiguration | null;
  isOfflineMode: boolean;
  isHybridMode: boolean;
  canWorkOffline: boolean;
  loading: boolean;
  refreshConfig: () => Promise<void>;
  checkConnectivity: () => Promise<boolean>;
}

const defaultConfig: SystemConfiguration = {
  operationMode: 'hybrid',
  tseEnabled: true,
  printerEnabled: true,
  finanzOnlineEnabled: false,
  offlineSettings: {
    enabled: true,
    autoSync: true,
    syncInterval: 30,
    maxOfflineDays: 7,
  },
  tseSettings: {
    model: 'EPSON-TSE',
    connectionType: 'usb',
    timeout: 30,
    dailyReportTime: '23:59',
    required: true,
    offlineAllowed: false,
    maxOfflineTransactions: 100,
  },
  printerSettings: {
    model: 'EPSON TM-T88VI',
    connectionType: 'usb',
    paperSize: '80mm',
    autoPrint: true,
    required: true,
    offlineQueue: false,
    maxQueueSize: 50,
  },
  finanzOnlineSettings: {
    apiUrl: 'https://finanzonline.bmf.gv.at',
    clientId: '',
    clientSecret: '',
    certificatePath: '',
  },
};

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
  const [isOnline, setIsOnline] = useState(true);
  const [systemConfig, setSystemConfig] = useState<SystemConfiguration>(defaultConfig);
  const [config, setConfig] = useState<SystemConfiguration | null>(null);
  const [loading, setLoading] = useState(true);

  // Ağ durumunu izle
  useEffect(() => {
    const unsubscribe = NetInfo.addEventListener(state => {
      setIsOnline(state.isConnected ?? false);
    });

    return () => unsubscribe();
  }, []);

  // Sistem konfigürasyonunu yükle
  const loadSystemConfig = async () => {
    try {
      const savedConfig = await AsyncStorage.getItem('systemConfig');
      if (savedConfig) {
        const parsedConfig = JSON.parse(savedConfig);
        setSystemConfig({ ...defaultConfig, ...parsedConfig });
      }
    } catch (error) {
      console.error('System config load failed:', error);
    }
  };

  // Sistem konfigürasyonunu güncelle
  const updateSystemConfig = async (newConfig: Partial<SystemConfiguration>) => {
    try {
      const updatedConfig = { ...systemConfig, ...newConfig };
      setSystemConfig(updatedConfig);
      await AsyncStorage.setItem('systemConfig', JSON.stringify(updatedConfig));
    } catch (error) {
      console.error('System config update failed:', error);
    }
  };

  // İlk yükleme
  useEffect(() => {
    loadSystemConfig();
  }, []);

  // Sistem konfigürasyonunu yükle
  const loadConfig = async () => {
    try {
      const response = await apiClient.get<SystemConfiguration>('/system/config');
      setConfig(response.data);
    } catch (error) {
      console.error('System config load failed:', error);
      // Varsayılan konfigürasyon
      setConfig({
        operationMode: 'online-only',
        tseEnabled: true,
        printerEnabled: true,
        finanzOnlineEnabled: false,
        offlineSettings: {
          enabled: false,
          autoSync: false,
          syncInterval: 5,
          maxOfflineDays: 7
        },
        tseSettings: {
          model: 'EPSON-TSE',
          connectionType: 'usb',
          timeout: 30,
          dailyReportTime: '23:59',
          required: true,
          offlineAllowed: false,
          maxOfflineTransactions: 100
        },
        printerSettings: {
          model: 'EPSON TM-T88VI',
          connectionType: 'usb',
          paperSize: '80mm',
          autoPrint: true,
          required: true,
          offlineQueue: false,
          maxQueueSize: 50
        },
        finanzOnlineSettings: {
          apiUrl: 'https://finanzonline.bmf.gv.at',
          clientId: '',
          clientSecret: '',
          certificatePath: ''
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
    isOnline,
    systemConfig,
    updateSystemConfig,
    loadSystemConfig,
    config,
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