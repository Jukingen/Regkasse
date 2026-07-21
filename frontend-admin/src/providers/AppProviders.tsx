'use client';

import { QueryClientProvider } from '@tanstack/react-query';
import dynamic from 'next/dynamic';
import React, { type ReactNode, useState } from 'react';

import { LogContextBinder } from '@/components/logging/LogContextBinder';
import { AuthSessionInvalidationListener } from '@/features/auth/components/AuthSessionInvalidationListener';
import { TenantChangeListener } from '@/features/auth/components/TenantChangeListener';
import { TenantSwitchProvider } from '@/features/auth/contexts/TenantSwitchContext';
import { AuthProvider } from '@/features/auth/providers/AuthProvider';
import { I18nProvider } from '@/i18n';
import { PersonalizationProvider } from '@/lib/personalization/PersonalizationProvider';
import { createAppQueryClient } from '@/lib/queryClient';
import { ThemeProvider } from '@/providers/ThemeProvider';
import { logger } from '@/lib/logger';

/**
 * Dev-only chunk. `process.env.NODE_ENV` is inlined at build time so production
 * never pulls `@tanstack/react-query-devtools` into the main graph.
 * (Next.js — not Vite `import.meta.env.DEV`.)
 */
const ReactQueryDevtools =
  process.env.NODE_ENV === 'development'
    ? dynamic(() => import('@/providers/ReactQueryDevtoolsLazy'), { ssr: false })
    : null;

/** Root client providers: i18n → query → theme → user preferences. */
export function AppProviders({ children }: { children: ReactNode }) {
  const [queryClient] = useState(() => {
    if (process.env.NODE_ENV === 'development') {
      logger.debug('Initializing QueryClient', { component: 'AppProviders' });
    }
    return createAppQueryClient();
  });

  return (
    <I18nProvider>
      <TenantSwitchProvider>
        <QueryClientProvider client={queryClient}>
          <AuthProvider>
            <LogContextBinder />
            <ThemeProvider>
              <PersonalizationProvider>
                <AuthSessionInvalidationListener />
                <TenantChangeListener />
                {children}
                {ReactQueryDevtools ? <ReactQueryDevtools initialIsOpen={false} /> : null}
              </PersonalizationProvider>
            </ThemeProvider>
          </AuthProvider>
        </QueryClientProvider>
      </TenantSwitchProvider>
    </I18nProvider>
  );
}
