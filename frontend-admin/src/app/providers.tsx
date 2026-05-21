'use client';

import { QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import React, { useState, ReactNode } from 'react';
import { I18nProvider } from '@/i18n';
import { createAppQueryClient } from '@/lib/queryClient';
import { technicalConsole } from '@/shared/dev/technicalConsole';
import { AuthSessionInvalidationListener } from '@/features/auth/components/AuthSessionInvalidationListener';
import { TenantChangeListener } from '@/features/auth/components/TenantChangeListener';
import { TenantSwitchProvider } from '@/features/auth/contexts/TenantSwitchContext';

export default function QueryProvider({ children }: { children: ReactNode }) {
    // Standard Next.js 14 pattern for QueryClient stability
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
                    <AuthSessionInvalidationListener />
                    <TenantChangeListener />
                    {children}
                    {process.env.NODE_ENV === 'development' ? (
                        <ReactQueryDevtools initialIsOpen={false} />
                    ) : null}
                </QueryClientProvider>
            </TenantSwitchProvider>
        </I18nProvider>
    );
}
