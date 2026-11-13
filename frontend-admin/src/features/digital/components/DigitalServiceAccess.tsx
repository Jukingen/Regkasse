'use client';

import type { ReactNode } from 'react';

import { AccessDenied } from '@/components/AccessDenied';
import { PageSkeleton } from '@/components/Skeleton';
import { useTenantDigitalService } from '@/features/digital-services/hooks/useTenantDigitalServices';
import { DigitalServices } from '@/features/digital/components/DigitalServices';
import {
  canAccessDigitalServices,
  isAnyDigitalServiceAvailable,
} from '@/features/digital/digitalServicePermissions';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';

type DigitalServiceAccessProps = {
  /**
   * Explicit tenant (Super Admin tenant page). When omitted, ambient JWT tenant is used
   * for service-status checks (Mandanten portal).
   */
  tenantId?: string;
  /**
   * When true (default), Mandanten are blocked if neither website nor app is available.
   * Set false on tenant detail pages that still show status / Super Admin controls.
   */
  blockWhenDisabled?: boolean;
  /** Optional custom content; defaults to Mandanten {@link DigitalServices} portal. */
  children?: ReactNode;
};

/**
 * Permission + service-status guard for digital services (website / app).
 *
 * - Requires digital.view/preview/request/create, website.manage, digital.manage, or SuperAdmin.
 * - Optionally blocks Mandanten when TenantServiceStatus makes both surfaces unavailable.
 */
export function DigitalServiceAccess({
  tenantId,
  blockWhenDisabled = true,
  children,
}: DigitalServiceAccessProps) {
  const { t } = useI18n();
  const { user, isSuperAdmin, hasPermission } = usePermissions();
  const currentTenant = useCurrentTenant();
  const effectiveTenantId = tenantId ?? currentTenant.tenantId ?? undefined;

  const canView =
    canAccessDigitalServices(user ? { permissions: user.permissions } : null, isSuperAdmin) ||
    hasPermission(PERMISSIONS.DIGITAL_VIEW) ||
    hasPermission(PERMISSIONS.DIGITAL_PREVIEW) ||
    hasPermission(PERMISSIONS.DIGITAL_REQUEST) ||
    hasPermission(PERMISSIONS.DIGITAL_CREATE) ||
    hasPermission(PERMISSIONS.DIGITAL_MANAGE) ||
    hasPermission(PERMISSIONS.WEBSITE_MANAGE);

  const shouldLoadStatus =
    canView && Boolean(effectiveTenantId) && blockWhenDisabled && !isSuperAdmin;

  const {
    data: serviceStatus,
    isLoading: statusLoading,
    isError: statusError,
    isFetched: statusFetched,
  } = useTenantDigitalService(shouldLoadStatus ? effectiveTenantId : undefined);

  if (!canView) {
    return <AccessDenied message={t('tenants.digitalServices.accessDenied')} />;
  }

  // Super Admin always proceeds (activation / pricing). Mandanten portal may hard-block.
  if (!isSuperAdmin && blockWhenDisabled && effectiveTenantId) {
    if (statusLoading || (!statusFetched && !statusError)) {
      return <PageSkeleton widgets={2} />;
    }

    if (statusError) {
      return <AccessDenied message={t('tenants.digitalServices.statusLoadFailed')} />;
    }

    if (!isAnyDigitalServiceAvailable(serviceStatus)) {
      return <AccessDenied message={t('tenants.digitalServices.servicesDisabled')} />;
    }
  }

  return <>{children ?? <DigitalServices tenantId={effectiveTenantId} />}</>;
}
