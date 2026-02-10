import 'intl-pluralrules';
import '../i18n';
import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';

import React from 'react';
import { Text, View } from 'react-native';
import { AuthProvider } from '../contexts/AuthContext';
import { SystemProvider } from '../contexts/SystemContext';
import { ThemeProvider } from '../contexts/ThemeContext';
import { AppStateProvider } from '../contexts/AppStateContext';
import { CartProvider } from '../contexts/CartContext';
import { useMemoryMonitor } from '../hooks/useMemoryOptimization';
import { ErrorBoundary } from '../components/ErrorBoundary';

console.log('🚀 ROOT LAYOUT: Module loaded successfully');

export default function RootLayout() {
  // Memory kullanımını izle
  useMemoryMonitor();

  React.useEffect(() => {
    console.log('🌳 ROOT LAYOUT: Mounted');
    return () => console.log('🌳 ROOT LAYOUT: Unmounted');
  }, []);

  return (
    <ErrorBoundary>
      <AuthProvider>
        <SystemProvider>
          <ThemeProvider>
            <AppStateProvider>
              <CartProvider>
                {/* ✅ FIX: headerShown: false - "index" header gizlendi */}
                <Stack
                  screenOptions={{
                    headerShown: false,
                  }}
                />
                <StatusBar style="auto" />
              </CartProvider>
            </AppStateProvider>
          </ThemeProvider>
        </SystemProvider>
      </AuthProvider>
    </ErrorBoundary>
  );
}