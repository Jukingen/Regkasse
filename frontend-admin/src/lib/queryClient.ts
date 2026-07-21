import { MutationCache, QueryCache, QueryClient } from '@tanstack/react-query';

import { invokeQueryClientErrorHandler } from '@/lib/queryErrorHandling';

/** Shared TanStack Query defaults for the admin app (see `providers/AppProviders.tsx`). */
export function createAppQueryClient() {
  return new QueryClient({
    queryCache: new QueryCache({
      onError: (error, query) => {
        invokeQueryClientErrorHandler(error, query.meta);
      },
    }),
    mutationCache: new MutationCache({
      onError: (error, _variables, _context, mutation) => {
        invokeQueryClientErrorHandler(error, mutation.meta);
      },
    }),
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
