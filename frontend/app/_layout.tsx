import '../i18n';
import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';

import React from 'react';
import { AuthProvider } from '../contexts/AuthContext';
import { SystemProvider } from '../contexts/SystemContext';
import { ThemeProvider } from '../contexts/ThemeContext';
import { AppStateProvider } from '../contexts/AppStateContext';
import { TableSlotProvider } from '../contexts/TableSlotContext';
// import LanguageSelector from '../components/LanguageSelector';

export default function RootLayout() {
  return (
    <AuthProvider>
      <SystemProvider>
        <ThemeProvider>
          <AppStateProvider>
            <TableSlotProvider>
              <Stack />
            </TableSlotProvider>
          </AppStateProvider>
        </ThemeProvider>
      </SystemProvider>
    </AuthProvider>
  );
} 