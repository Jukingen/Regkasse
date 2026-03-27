'use client';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import React, { useState, ReactNode } from 'react';
import { I18nProvider } from '@/i18n';
import { technicalConsole } from '@/shared/dev/technicalConsole';

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
            <QueryClientProvider client={queryClient}>
                {children}
                <ReactQueryDevtools initialIsOpen={false} />
            </QueryClientProvider>
        </I18nProvider>
    );
}
