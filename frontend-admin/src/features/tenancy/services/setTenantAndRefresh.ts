import {
    isDevelopment,
    writeDevTenantSlug,
} from '@/features/auth/services/devTenant';
import { beginTenantSwitch } from '@/features/auth/services/tenantSwitchController';
import { tenantStorage } from '@/features/auth/services/tenantStorage';

/**
 * Persists tenant slug for API resolution and reloads the shell so all queries refetch.
 * Development: dispatches {@link DEV_TENANT_CHANGED_EVENT}; {@link useTenantChangeListener} clears cache, toasts, reloads.
 */
export function persistTenantSlugAndRefresh(slug: string): void {
    if (typeof window === 'undefined') return;

    const normalized = slug.trim().toLowerCase();
    if (!normalized) return;

    if (isDevelopment()) {
        writeDevTenantSlug(normalized);
        return;
    }

    tenantStorage.persistBootstrap({ tenantSlug: normalized });
    beginTenantSwitch();
    window.location.reload();
}
