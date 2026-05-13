import { storage } from '../utils/storage';
import { Platform } from 'react-native';

// Safe Native Module Imports
let NetInfo: any = {
  addEventListener: (cb: (state: any) => void) => {
    // Mock listener for web/ssr
    return () => { };
  },
  fetch: async () => ({ isConnected: true })
};

if (Platform.OS !== 'web') {
  try {
    const NativeNetInfo = require('@react-native-community/netinfo').default;
    if (NativeNetInfo) {
      NetInfo = NativeNetInfo;
    }
  } catch (e) {
    console.warn('NetInfo require failed:', e);
  }
}
import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react';
import { Alert } from 'react-native';

import { defaultSystemConfig, type SystemConfiguration } from './systemConfiguration';
import {
  isDevSimulatePosNetworkOffline,
} from '../constants/devSimulatePosOffline';

export type { SystemConfiguration } from './systemConfiguration';

interface SystemContextType {
  isOnline: boolean;
  systemConfig: SystemConfiguration;
  updateSystemConfig: (config: Partial<SystemConfiguration>) => Promise<void>;
  loadSystemConfig: () => Promise<void>;
  loading: boolean;
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
  const [isOnline, setIsOnline] = useState(() => !isDevSimulatePosNetworkOffline());
  const [systemConfig, setSystemConfig] = useState<SystemConfiguration>(defaultSystemConfig);
  const [loading, setLoading] = useState(true);

  // Ağ durumunu izle
  useEffect(() => {
    const unsubscribe = NetInfo.addEventListener((state: any) => {
      if (isDevSimulatePosNetworkOffline()) {
        setIsOnline(false);
        return;
      }
      setIsOnline(state.isConnected ?? false);
    });

    return () => unsubscribe();
  }, []);

  // Sistem konfigürasyonunu yükle
  const loadSystemConfig = async () => {
    try {
      setLoading(true);
      const savedConfig = await storage.getItem('systemConfig');
      if (savedConfig) {
        const parsedConfig = JSON.parse(savedConfig);
        setSystemConfig({ ...defaultSystemConfig, ...parsedConfig });
      }
    } catch (error) {
      console.error('System config load failed:', error);
    } finally {
      setLoading(false);
    }
  };

  // Sistem konfigürasyonunu güncelle
  const updateSystemConfig = async (newConfig: Partial<SystemConfiguration>) => {
    try {
      const updatedConfig = { ...systemConfig, ...newConfig };
      setSystemConfig(updatedConfig);
      await storage.setItem('systemConfig', JSON.stringify(updatedConfig));
    } catch (error) {
      console.error('System config update failed:', error);
      Alert.alert('Error', 'Failed to save settings. Please try again.');
    }
  };

  // Bağlantı durumunu kontrol et
  const checkConnectivity = async (): Promise<boolean> => {
    if (isDevSimulatePosNetworkOffline()) {
      return false;
    }
    try {
      const state = await NetInfo.fetch();
      return state.isConnected ?? false;
    } catch (error) {
      console.error('Connectivity check failed:', error);
      return false;
    }
  };

  // İlk yükleme
  useEffect(() => {
    loadSystemConfig();
  }, []);

  const value: SystemContextType = {
    isOnline,
    systemConfig,
    updateSystemConfig,
    loadSystemConfig,
    loading,
    checkConnectivity,
  };

  return (
    <SystemContext.Provider value={value}>
      {children}
    </SystemContext.Provider>
  );
}; 