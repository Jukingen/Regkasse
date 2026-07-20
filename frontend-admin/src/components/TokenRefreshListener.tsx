'use client';

import { useTokenRefresh } from '@/hooks/useTokenRefresh';

/**
 * Root-layout bridge: proactive JWT refresh for any authenticated session.
 * Mounted from `app/layout.tsx` (Server Component cannot call hooks directly).
 */
export function TokenRefreshListener(): null {
    useTokenRefresh();
    return null;
}
