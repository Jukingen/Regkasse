'use client';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import React, { useState, ReactNode } from 'react';
import { I18nProvider } from '@/i18n';
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
        return new QueryClient({
            defaultOptions: {
                queries: {
                    staleTime: 1000 * 30, // 30 seconds as requested
                    retry: false, // Prevent redundant calls on 401
                    refetchOnWindowFocus: false,
                    refetchOnMount: false,
                },
            },
        });
    });

    return (
        <I18nProvider>
            <TenantSwitchProvider>
                <QueryClientProvider client={queryClient}>
                    <AuthSessionInvalidationListener />
                    <TenantChangeListener />
                    {children}
                    <ReactQueryDevtools initialIsOpen={false} />
                </QueryClientProvider>
            </TenantSwitchProvider>
        </I18nProvider>
    );
}
