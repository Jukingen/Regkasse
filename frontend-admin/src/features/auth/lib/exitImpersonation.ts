import {
  buildTenantSubdomainOrigin,
  shouldUseProductionImpersonationRedirect,
} from '@/features/auth/lib/impersonationHandoff';
import { authStorage } from '@/features/auth/services/authStorage';
import { clearDevTenantOverride } from '@/features/auth/services/devTenant';

const ADMIN_PLATFORM_SLUG = 'admin';
const ADMIN_TENANTS_PATH = '/admin/tenants';

/** Super Admin FA origin (`https://admin.{baseDomain}`). */
export function buildAdminPlatformOrigin(protocol: 'https' | 'http' = 'https'): string {
  return buildTenantSubdomainOrigin(ADMIN_PLATFORM_SLUG, protocol);
}

/**
 * Ends an impersonation session on the tenant FA host: clears tokens and dev override,
 * then navigates back to platform tenant management on the admin host (production) or same origin (dev).
 */
export function exitImpersonation(): void {
  if (typeof window === 'undefined') {
    return;
  }

  authStorage.removeToken();
  clearDevTenantOverride();

  if (shouldUseProductionImpersonationRedirect()) {
    const protocol = window.location.protocol === 'http:' ? 'http' : 'https';
    window.location.assign(`${buildAdminPlatformOrigin(protocol)}${ADMIN_TENANTS_PATH}`);
    return;
  }

  window.location.assign(ADMIN_TENANTS_PATH);
}
