import 'react-native-gesture-handler';
import 'intl-pluralrules';
import '../src/config/devFlags';
import { i18nReady } from '../i18n';
import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { GestureHandlerRootView } from 'react-native-gesture-handler';

import React from 'react';
import { Text, View } from 'react-native';
import { AuthProvider } from '../contexts/AuthContext';
import { SystemProvider } from '../contexts/SystemContext';
import { ThemeProvider } from '../contexts/ThemeContext';
import { AppStateProvider } from '../contexts/AppStateContext';
import { CartProvider } from '../contexts/CartContext';
import { DevelopmentModeProvider } from '../contexts/DevelopmentModeContext';
import { useMemoryMonitor } from '../hooks/useMemoryOptimization';
import { ErrorBoundary } from '../components/ErrorBoundary';
import { clearLegacyTenantSwitcherCache } from '../services/tenant/clearLegacyTenantSwitcherCache';
import { fetchFreshTenants } from '../services/tenant/tenantStorage';
import { sessionManager } from '../services/session/sessionManager';

console.log('🚀 ROOT LAYOUT: Module loaded successfully');

export default function RootLayout() {
  const [isI18nReady, setIsI18nReady] = React.useState(false);

  // Memory kullanımını izle
  useMemoryMonitor();

  React.useEffect(() => {
    let mounted = true;
    void i18nReady.finally(() => {
      if (mounted) setIsI18nReady(true);
    });
    return () => {
      mounted = false;
    };
  }, []);

  React.useEffect(() => {
    void (async () => {
      await clearLegacyTenantSwitcherCache();
      if (!__DEV__) return;

      const token = await sessionManager.getAccessToken();
      if (!token) return;

      await fetchFreshTenants();
    })();
  }, []);

  React.useEffect(() => {
    console.log('🌳 ROOT LAYOUT: Mounted');
    return () => console.log('🌳 ROOT LAYOUT: Unmounted');
  }, []);

  if (!isI18nReady) {
    return (
      <SafeAreaProvider>
        <View style={{ flex: 1, alignItems: 'center', justifyContent: 'center' }}>
          <Text>...</Text>
        </View>
      </SafeAreaProvider>
    );
  }

  return (
    <GestureHandlerRootView style={{ flex: 1 }}>
    <ErrorBoundary>
      <SafeAreaProvider>
        <AuthProvider>
          <SystemProvider>
            <ThemeProvider>
              <AppStateProvider>
                <CartProvider>
                  <DevelopmentModeProvider>
                  {/* ✅ FIX: headerShown: false - "index" header gizlendi */}
                  <Stack
                  screenOptions={{
                    headerShown: false,
                  }}
                  />
                  <StatusBar style="auto" />
                  </DevelopmentModeProvider>
                </CartProvider>
              </AppStateProvider>
            </ThemeProvider>
          </SystemProvider>
        </AuthProvider>
      </SafeAreaProvider>
    </ErrorBoundary>
    </GestureHandlerRootView>
  );
}