'use client';

import { useMemo } from 'react';

import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useTenantContext } from '@/features/tenancy/hooks/useTenantContext';
import { isBusinessTenantSlug } from '@/features/users/utils/userScope';
import { normalizeAdminPathname } from '@/shared/adminSidebarNavigation';

/** Routes reachable on platform admin host without mandant context (tenant pick / platform ops). */
export const SUPER_ADMIN_PLATFORM_ALLOWED_PREFIXES = [
  '/admin/tenants',
  '/admin/approvals',
  '/admin/users',
  '/admin/licenses',
  '/admin/license',
  '/admin/system',
  '/admin/digital',
] as const;

export function isPathAllowedWithoutTenant(pathname: string | null | undefined): boolean {
  const p = normalizeAdminPathname(pathname);
  if (p === '/admin') {
    return true;
  }
  return SUPER_ADMIN_PLATFORM_ALLOWED_PREFIXES.some(
    (prefix) => p === prefix || p.startsWith(`${prefix}/`)
  );
}

export type SuperAdminActiveTenantContextInput = {
  isImpersonating: boolean;
  isDevTenantOverride: boolean;
  isPlatformAdminHost: boolean;
  tenantSlug: string | null | undefined;
  jwtTenantSlug: string | null | undefined;
  tenantId: string | null | undefined;
};

/**
 * True when Super Admin already has a mandant session (JWT rebind, impersonation, or soft override).
 */
export function hasSuperAdminActiveTenantContext(
  input: SuperAdminActiveTenantContextInput
): boolean {
  if (input.isImpersonating || input.isDevTenantOverride) {
    return true;
  }
  if (isBusinessTenantSlug(input.jwtTenantSlug) && Boolean(input.tenantId?.trim())) {
    return true;
  }
  if (!input.isPlatformAdminHost && isBusinessTenantSlug(input.tenantSlug)) {
    return true;
  }
  return false;
}

/**
 * Super Admin on platform host (`admin.*`) without impersonation / JWT mandant / dev tenant override.
 */
export function useSuperAdminTenantMode() {
  const { user } = useAuth();
  const ctx = useTenantContext();

  return useMemo(() => {
    const isSuperAdminUser = isSuperAdmin(user?.role);

    const isPlatformAdminHost = ctx.hostSlug === 'admin';

    const hasActiveTenantContext = hasSuperAdminActiveTenantContext({
      isImpersonating: ctx.isImpersonating,
      isDevTenantOverride: ctx.isDevTenantOverride,
      isPlatformAdminHost,
      tenantSlug: ctx.tenantSlug,
      jwtTenantSlug: ctx.jwtTenantSlug,
      tenantId: ctx.tenantId,
    });

    const requiresTenantSelection =
      isSuperAdminUser && isPlatformAdminHost && !hasActiveTenantContext;

    const isSuperAdminPlatformMode = requiresTenantSelection;

    return {
      ...ctx,
      isSuperAdminUser,
      hasActiveTenantContext,
      requiresTenantSelection,
      isSuperAdminPlatformMode,
    };
  }, [user?.role, ctx]);
}
