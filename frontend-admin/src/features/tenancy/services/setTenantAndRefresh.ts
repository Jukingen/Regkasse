import { isDevelopment, writeDevTenantSlug } from '@/features/auth/services/devTenant';
import { tenantStorage } from '@/features/auth/services/tenantStorage';
import { beginTenantSwitch } from '@/features/auth/services/tenantSwitchController';
import { persistCashRegisterOnTenantSwitch } from '@/features/tenancy/services/persistCashRegisterOnTenantSwitch';

export type DevTenantSwitchTarget = {
  slug: string;
  id: string;
};

/**
 * Persists tenant slug for API resolution and reloads the shell so all queries refetch.
 * Development: dispatches {@link DEV_TENANT_CHANGED_EVENT}; {@link useTenantChangeListener} clears cache, toasts, reloads.
 */
export function persistTenantSlugAndRefresh(slug: string, tenantId?: string): void {
  if (typeof window === 'undefined') return;

  const normalized = slug.trim().toLowerCase();
  if (!normalized) return;

  if (isDevelopment()) {
    const slugChanged = writeDevTenantSlug(normalized, tenantId);
    // Same slug but corrected tenant id — listener ignores duplicate slug, so reload here.
    if (!slugChanged && tenantId?.trim()) {
      beginTenantSwitch();
      window.location.reload();
    }
    return;
  }

  tenantStorage.persistBootstrap({ tenantSlug: normalized, tenantId });
  beginTenantSwitch();
  window.location.reload();
}

/**
 * Dev header switcher: persist slug + id (`dev_tenant_id`, `rk_admin_tenant_*`), then reload.
 */
export async function switchDevTenantContext(
  tenant: DevTenantSwitchTarget,
  options?: { persistCashRegister?: boolean }
): Promise<void> {
  const normalizedSlug = tenant.slug.trim().toLowerCase();
  const normalizedId = tenant.id.trim();
  if (!normalizedSlug || !normalizedId) {
    return;
  }

  if (options?.persistCashRegister !== false) {
    await persistCashRegisterOnTenantSwitch(normalizedId);
  }

  persistTenantSlugAndRefresh(normalizedSlug, normalizedId);
}
