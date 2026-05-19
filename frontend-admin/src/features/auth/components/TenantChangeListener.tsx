'use client';

import { useTenantChangeListener } from '@/features/auth/hooks/useTenantChangeListener';

/** Mount once under `QueryClientProvider` to sync tenant switches across browser tabs. */
export function TenantChangeListener() {
    useTenantChangeListener();
    return null;
}
