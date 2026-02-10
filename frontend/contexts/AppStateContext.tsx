import React, { createContext, useContext, useState, useCallback, ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Alert } from 'react-native';

export interface AppState {
  // Global loading states
  globalLoading: boolean;
  networkLoading: boolean;

  // Error states
  globalError: string | null;
  networkError: string | null;

  // Success states
  globalSuccess: string | null;

  // Modal states
  showGlobalLoading: boolean;
  showErrorModal: boolean;
  showSuccessModal: boolean;

  // Notification states
  notifications: NotificationItem[];

  // Offline state
  isOffline: boolean;

  // App state
  isAppReady: boolean;
  isInitialized: boolean;
}

export interface NotificationItem {
  id: string;
  type: 'success' | 'error' | 'warning' | 'info';
  title: string;
  message: string;
  duration?: number;
  timestamp: Date;
}

export interface AppStateActions {
  // Loading actions
  setGlobalLoading: (loading: boolean) => void;
  setNetworkLoading: (loading: boolean) => void;
  showGlobalLoading: () => void;
  hideGlobalLoading: () => void;

  // Error actions
  setGlobalError: (error: string | null) => void;
  setNetworkError: (error: string | null) => void;
  showError: (error: string, title?: string) => void;
  clearError: () => void;

  // Success actions
  setGlobalSuccess: (message: string | null) => void;
  showSuccess: (message: string, title?: string) => void;
  clearSuccess: () => void;

  // Modal actions
  setShowErrorModal: (show: boolean) => void;
  setShowSuccessModal: (show: boolean) => void;

  // Notification actions
  addNotification: (notification: Omit<NotificationItem, 'id' | 'timestamp'>) => void;
  removeNotification: (id: string) => void;
  clearNotifications: () => void;

  // App state actions
  setOffline: (offline: boolean) => void;
  setAppReady: (ready: boolean) => void;
  setInitialized: (initialized: boolean) => void;

  // Utility actions
  reset: () => void;
}

const initialState: AppState = {
  globalLoading: false,
  networkLoading: false,
  globalError: null,
  networkError: null,
  globalSuccess: null,
  showGlobalLoading: false,
  showErrorModal: false,
  showSuccessModal: false,
  notifications: [],
  isOffline: false,
  isAppReady: false,
  isInitialized: false
};

const AppStateContext = createContext<AppState & AppStateActions | undefined>(undefined);

export const AppStateProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const { t } = useTranslation();
  const [state, setState] = useState<AppState>(initialState);

  // Loading actions
  const setGlobalLoading = useCallback((loading: boolean) => {
    setState(prev => ({ ...prev, globalLoading: loading }));
  }, []);

  const setNetworkLoading = useCallback((loading: boolean) => {
    setState(prev => ({ ...prev, networkLoading: loading }));
  }, []);

  const showGlobalLoading = useCallback(() => {
    setState(prev => ({ ...prev, showGlobalLoading: true }));
  }, []);

  const hideGlobalLoading = useCallback(() => {
    setState(prev => ({ ...prev, showGlobalLoading: false }));
  }, []);

  // Error actions
  const setGlobalError = useCallback((error: string | null) => {
    setState(prev => ({ ...prev, globalError: error }));
  }, []);

  const setNetworkError = useCallback((error: string | null) => {
    setState(prev => ({ ...prev, networkError: error }));
  }, []);

  const showError = useCallback((error: string, title: string = 'Error') => {
    setState(prev => ({ ...prev, globalError: error }));
    Alert.alert(title, error, [{ text: 'OK' }]);
  }, []);

  const clearError = useCallback(() => {
    setState(prev => ({
      ...prev,
      globalError: null,
      networkError: null,
      showErrorModal: false
    }));
  }, []);

  // Success actions
  const setGlobalSuccess = useCallback((message: string | null) => {
    setState(prev => ({ ...prev, globalSuccess: message }));
  }, []);

  const showSuccess = useCallback((message: string, title: string = 'Success') => {
    setState(prev => ({ ...prev, globalSuccess: message }));
    Alert.alert(title, message, [{ text: 'OK' }]);
  }, []);

  const clearSuccess = useCallback(() => {
    setState(prev => ({
      ...prev,
      globalSuccess: null,
      showSuccessModal: false
    }));
  }, []);

  // Modal actions
  const setShowErrorModal = useCallback((show: boolean) => {
    setState(prev => ({ ...prev, showErrorModal: show }));
  }, []);

  const setShowSuccessModal = useCallback((show: boolean) => {
    setState(prev => ({ ...prev, showSuccessModal: show }));
  }, []);

  // Notification actions
  const addNotification = useCallback((notification: Omit<NotificationItem, 'id' | 'timestamp'>) => {
    const newNotification: NotificationItem = {
      ...notification,
      id: Date.now().toString(),
      timestamp: new Date()
    };

    setState(prev => ({
      ...prev,
      notifications: [...prev.notifications, newNotification]
    }));

    // Auto-remove notification after duration
    if (notification.duration) {
      setTimeout(() => {
        removeNotification(newNotification.id);
      }, notification.duration);
    }
  }, []);

  const removeNotification = useCallback((id: string) => {
    setState(prev => ({
      ...prev,
      notifications: prev.notifications.filter(n => n.id !== id)
    }));
  }, []);

  const clearNotifications = useCallback(() => {
    setState(prev => ({ ...prev, notifications: [] }));
  }, []);

  // App state actions
  const setOffline = useCallback((offline: boolean) => {
    setState(prev => ({ ...prev, isOffline: offline }));
  }, []);

  const setAppReady = useCallback((ready: boolean) => {
    setState(prev => ({ ...prev, isAppReady: ready }));
  }, []);

  const setInitialized = useCallback((initialized: boolean) => {
    setState(prev => ({ ...prev, isInitialized: initialized }));
  }, []);

  // Utility actions
  const reset = useCallback(() => {
    setState(initialState);
  }, []);

  const contextValue: AppState & AppStateActions = {
    ...state,
    setGlobalLoading,
    setNetworkLoading,
    showGlobalLoading,
    hideGlobalLoading,
    setGlobalError,
    setNetworkError,
    showError,
    clearError,
    setGlobalSuccess,
    showSuccess,
    clearSuccess,
    setShowErrorModal,
    setShowSuccessModal,
    addNotification,
    removeNotification,
    clearNotifications,
    setOffline,
    setAppReady,
    setInitialized,
    reset
  };

  console.log('📱 APP STATE PROVIDER: Rendering children...');

  return (
    <AppStateContext.Provider value={contextValue}>
      {children}
    </AppStateContext.Provider>
  );
};

export const useAppState = () => {
  const context = useContext(AppStateContext);
  if (context === undefined) {
    throw new Error('useAppState must be used within an AppStateProvider');
  }
  return context;
}; 