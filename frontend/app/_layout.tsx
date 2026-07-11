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
import { LicenseStatusProvider } from '../contexts/LicenseStatusContext';
import { MandantLicenseWarningProvider } from '../contexts/MandantLicenseWarningContext';
import { PosStatusOverviewProvider } from '../contexts/PosStatusOverviewContext';
import { useMemoryMonitor } from '../hooks/useMemoryOptimization';
import { ErrorBoundary } from '../components/ErrorBoundary';
import { clearLegacyTenantSwitcherCache } from '../services/tenant/clearLegacyTenantSwitcherCache';

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
    void clearLegacyTenantSwitcherCache();
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
          <PosStatusOverviewProvider>
          <LicenseStatusProvider>
            <MandantLicenseWarningProvider>
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
            </MandantLicenseWarningProvider>
          </LicenseStatusProvider>
          </PosStatusOverviewProvider>
        </AuthProvider>
      </SafeAreaProvider>
    </ErrorBoundary>
    </GestureHandlerRootView>
  );
}