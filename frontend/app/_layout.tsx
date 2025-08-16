import '../i18n';
import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';

import React from 'react';
import { AuthProvider } from '../contexts/AuthContext';
import { SystemProvider } from '../contexts/SystemContext';
import { ThemeProvider } from '../contexts/ThemeContext';
import { AppStateProvider } from '../contexts/AppStateContext';

export default function RootLayout() {
  return (
    <AuthProvider>
      <SystemProvider>
        <ThemeProvider>
          <AppStateProvider>
            <Stack />
          </AppStateProvider>
        </ThemeProvider>
      </SystemProvider>
    </AuthProvider>
  );
} 