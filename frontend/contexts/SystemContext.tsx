import NetInfo from '@react-native-community/netinfo';
import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react';
import { Alert } from 'react-native';

import { defaultSystemConfig, type SystemConfiguration } from './systemConfiguration';
import { isDevSimulatePosNetworkOffline } from '../constants/devSimulatePosOffline';
import { fetchIsNetworkOnline, isNetworkOnline } from '../utils/isNetworkOnline';
import { storage } from '../utils/storage';

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

  useEffect(() => {
    let mounted = true;

    const applyOnline = (online: boolean) => {
      if (mounted) {
        setIsOnline(online);
      }
    };

    void fetchIsNetworkOnline().then(applyOnline);

    const unsubscribe = NetInfo.addEventListener((state) => {
      applyOnline(isNetworkOnline(state));
    });

    const handleBrowserOnline = () => {
      void fetchIsNetworkOnline().then(applyOnline);
    };
    const handleBrowserOffline = () => {
      applyOnline(false);
    };

    if (typeof window !== 'undefined') {
      window.addEventListener('online', handleBrowserOnline);
      window.addEventListener('offline', handleBrowserOffline);
    }

    return () => {
      mounted = false;
      unsubscribe();
      if (typeof window !== 'undefined') {
        window.removeEventListener('online', handleBrowserOnline);
        window.removeEventListener('offline', handleBrowserOffline);
      }
    };
  }, []);

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

  const checkConnectivity = async (): Promise<boolean> => {
    return await fetchIsNetworkOnline();
  };

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

  return <SystemContext.Provider value={value}>{children}</SystemContext.Provider>;
};
