'use client';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import React, { useState, ReactNode } from 'react';

export default function QueryProvider({ children }: { children: ReactNode }) {
    // Standard Next.js 14 pattern for QueryClient stability
    const [queryClient] = useState(() => {
        if (process.env.NODE_ENV === 'development') {
            console.log('⚡ [QueryClient] Initializing new instance');
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
        <QueryClientProvider client={queryClient}>
            {children}
            <ReactQueryDevtools initialIsOpen={false} />
        </QueryClientProvider>
    );
}
