import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';

import React, { PropsWithChildren } from 'react';
import { AuthProvider } from '../contexts/AuthContext';
import { LanguageProvider } from '../contexts/LanguageContext';
import { SystemProvider } from '../contexts/SystemContext';
import { ThemeProvider } from '../contexts/ThemeContext';
import { AppStateProvider } from '../contexts/AppStateContext';
import { TableSlotProvider } from '../contexts/TableSlotContext';
// import LanguageSelector from '../components/LanguageSelector';

export default function RootLayout({ children }: PropsWithChildren<object>) {
  return (
    <AuthProvider>
      <LanguageProvider>
        <SystemProvider>
          <ThemeProvider>
            <AppStateProvider>
              <TableSlotProvider>
                <Stack />
              </TableSlotProvider>
            </AppStateProvider>
          </ThemeProvider>
        </SystemProvider>
      </LanguageProvider>
    </AuthProvider>
  );
} 