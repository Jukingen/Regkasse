import { QueryClient } from '@tanstack/react-query';

/** Shared TanStack Query defaults for the admin app (see `app/providers.tsx`). */
export function createAppQueryClient() {
    return new QueryClient({
        defaultOptions: {
            queries: {
                staleTime: 1000 * 30,
                gcTime: 1000 * 60 * 5,
                retry: false,
                refetchOnWindowFocus: false,
                refetchOnMount: false,
            },
        },
    });
}
