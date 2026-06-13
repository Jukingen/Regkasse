'use client';

import { QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import React, { useState, type ReactNode } from 'react';
import { I18nProvider } from '@/i18n';
import { PersonalizationProvider } from '@/lib/personalization/PersonalizationProvider';
import { ThemeProvider } from '@/providers/ThemeProvider';
import { createAppQueryClient } from '@/lib/queryClient';
import { technicalConsole } from '@/shared/dev/technicalConsole';
import { AuthSessionInvalidationListener } from '@/features/auth/components/AuthSessionInvalidationListener';
import { TenantChangeListener } from '@/features/auth/components/TenantChangeListener';
import { TenantSwitchProvider } from '@/features/auth/contexts/TenantSwitchContext';
import { AuthProvider } from '@/features/auth/providers/AuthProvider';

/** Root client providers: i18n → query → theme → user preferences. */
export function AppProviders({ children }: { children: ReactNode }) {
  const [queryClient] = useState(() => {
    if (process.env.NODE_ENV === 'development') {
      technicalConsole.devLog('[QueryClient] Initializing new instance');
    }
    return createAppQueryClient();
  });

  return (
    <I18nProvider>
      <TenantSwitchProvider>
        <QueryClientProvider client={queryClient}>
          <AuthProvider>
            <ThemeProvider>
              <PersonalizationProvider>
                <AuthSessionInvalidationListener />
                <TenantChangeListener />
                {children}
                {process.env.NODE_ENV === 'development' ? (
                  <ReactQueryDevtools initialIsOpen={false} />
                ) : null}
              </PersonalizationProvider>
            </ThemeProvider>
          </AuthProvider>
        </QueryClientProvider>
      </TenantSwitchProvider>
    </I18nProvider>
  );
}
