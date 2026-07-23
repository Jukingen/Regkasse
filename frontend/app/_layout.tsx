import { GestureHandlerRootView } from 'react-native-gesture-handler';
import { enableFreeze, enableScreens } from 'react-native-screens';

import 'intl-pluralrules';
import '../src/config/devFlags';
import { useFonts } from 'expo-font';
import { Stack } from 'expo-router';
import * as SplashScreen from 'expo-splash-screen';
import { StatusBar } from 'expo-status-bar';
import React from 'react';
import { SafeAreaProvider, initialWindowMetrics } from 'react-native-safe-area-context';

import { useDeepLinkNavigation } from '../hooks/useDeepLinkNavigation';
import { useMemoryMonitor } from '../hooks/useMemoryOptimization';
import { ErrorBoundary } from '../components/ErrorBoundary';
import { ThemedStatusBar } from '../components/ThemedStatusBar';
import { ThemedSystemUI } from '../components/ThemedSystemUI';
import { CUSTOM_FONT_MAP } from '../constants/fonts';
import { AppStateProvider } from '../contexts/AppStateContext';
import { AuthProvider } from '../contexts/AuthContext';
import { CartProvider } from '../contexts/CartContext';
import { DevelopmentModeProvider } from '../contexts/DevelopmentModeContext';
import { LicenseStatusProvider } from '../contexts/LicenseStatusContext';
import { MandantLicenseWarningProvider } from '../contexts/MandantLicenseWarningContext';
import { MaintenanceProvider } from '../contexts/MaintenanceContext';
import { PosStatusOverviewProvider } from '../contexts/PosStatusOverviewContext';
import { SystemProvider } from '../contexts/SystemContext';
import { ThemeProvider } from '../contexts/ThemeContext';
import { i18nReady } from '../i18n';
import { OfflineSyncService } from '../services/offline/offlineSyncService';
import { clearLegacyTenantSwitcherCache } from '../services/tenant/clearLegacyTenantSwitcherCache';

// Native screen containers + freeze inactive routes (react-native-screens / Expo Router).
// Must run before navigators mount — module scope of the root layout.
enableScreens(true);
enableFreeze(true);

// Keep native splash visible until i18n + fonts (and root tree) are ready.
// Call in module scope so it runs before React mounts (too late inside effects).
void SplashScreen.preventAutoHideAsync().catch(() => {
  // Native splash may already be hidden (web / unsupported); ignore.
});

try {
  SplashScreen.setOptions({
    duration: 400,
    fade: true,
  });
} catch {
  // setOptions is native-only; ignore on web / unsupported runtimes.
}

// Hide OS status bar while the splash screen is covering the UI.
try {
  StatusBar.setHidden(true);
} catch {
  // Web / unsupported; ignore.
}

console.log('🚀 ROOT LAYOUT: Module loaded successfully');

export default function RootLayout() {
  const [isI18nReady, setIsI18nReady] = React.useState(false);
  const [fontsLoaded, fontError] = useFonts(CUSTOM_FONT_MAP);

  // Memory kullanımını izle
  useMemoryMonitor();
  // Email / push / QR deep links → correct Expo Router screens
  useDeepLinkNavigation();

  React.useEffect(() => {
    let mounted = true;
    void i18nReady.finally(() => {
      if (mounted) setIsI18nReady(true);
    });
    return () => {
      mounted = false;
    };
  }, []);

  // Proceed even if a font fails so the app is not stuck on splash; styles use fallbacks.
  const fontsReady = fontsLoaded || fontError != null;
  const isAppReady = isI18nReady && fontsReady;

  React.useEffect(() => {
    if (fontError) {
      console.warn('[fonts] Custom font load failed; using fallback faces.', fontError);
    }
  }, [fontError]);

  React.useEffect(() => {
    if (!isAppReady) return;
    SplashScreen.hide();
    try {
      StatusBar.setHidden(false, 'fade');
    } catch {
      // Web / unsupported; ignore.
    }
  }, [isAppReady]);

  React.useEffect(() => {
    void clearLegacyTenantSwitcherCache();
  }, []);

  React.useEffect(() => {
    OfflineSyncService.getInstance();
  }, []);

  React.useEffect(() => {
    console.log('🌳 ROOT LAYOUT: Mounted');
    return () => {
      console.log('🌳 ROOT LAYOUT: Unmounted');
    };
  }, []);

  if (!isAppReady) {
    // Native splash still covers the screen; avoid flashing a placeholder UI.
    return null;
  }

  return (
    <GestureHandlerRootView style={{ flex: 1 }}>
      <ErrorBoundary>
        <SafeAreaProvider initialMetrics={initialWindowMetrics}>
          <AuthProvider>
            <PosStatusOverviewProvider>
              <LicenseStatusProvider>
                <MandantLicenseWarningProvider>
                  <MaintenanceProvider>
                  <SystemProvider>
                    <ThemeProvider>
                      <AppStateProvider>
                        <CartProvider>
                          <DevelopmentModeProvider>
                            <Stack
                              screenOptions={{
                                headerShown: false,
                                // With enableFreeze(true) above, inactive routes suspend (native iOS/Android).
                                freezeOnBlur: true,
                              }}>
                              <Stack.Screen name="index" />
                              <Stack.Screen name="(auth)" />
                              <Stack.Screen name="(tabs)" />
                              <Stack.Screen name="(screens)" />
                              <Stack.Screen name="customer" />
                              <Stack.Screen name="tenant/[slug]" />
                              <Stack.Screen name="order-tracker" />
                            </Stack>
                            <ThemedStatusBar />
                            <ThemedSystemUI />
                          </DevelopmentModeProvider>
                        </CartProvider>
                      </AppStateProvider>
                    </ThemeProvider>
                  </SystemProvider>
                  </MaintenanceProvider>
                </MandantLicenseWarningProvider>
              </LicenseStatusProvider>
            </PosStatusOverviewProvider>
          </AuthProvider>
        </SafeAreaProvider>
      </ErrorBoundary>
    </GestureHandlerRootView>
  );
}
