'use client';

import { useQuery } from '@tanstack/react-query';
import { useMemo } from 'react';

import { useGetApiCompanySettings } from '@/api/generated/company-settings/company-settings';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { resolveTenantRowLicenseStatus } from '@/features/license/utils/licenseStatus';
import { getAdminTenantById } from '@/features/super-admin/api/adminTenants';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';

/**
 * Mandant info for Verwaltung card: context slug/id, company or admin API metadata, license status.
 */
export function useTenantInfo() {
  const { user } = useAuth();
  const ctx = useCurrentTenant();
  const canFetchAdminTenant = isSuperAdmin(user?.role) && Boolean(ctx.tenantId);

  const companyQuery = useGetApiCompanySettings({
    query: { enabled: ctx.hasAuthToken, retry: false },
  });

  const adminTenantQuery = useQuery({
    queryKey: ['admin', 'tenant', ctx.tenantId] as const,
    queryFn: () => getAdminTenantById(ctx.tenantId!),
    enabled: ctx.hasAuthToken && canFetchAdminTenant,
    retry: false,
    staleTime: 60_000,
  });

  return useMemo(() => {
    const adminTenant = adminTenantQuery.data;
    const companyTenant = companyQuery.data?.tenant;

    const tenantId = ctx.tenantId ?? null;
    const tenantSlug = ctx.tenantSlug ?? companyTenant?.slug ?? null;

    const name =
      ctx.tenantName?.trim() || adminTenant?.name?.trim() || companyTenant?.name?.trim() || null;

    const registeredAt = adminTenant?.createdAt ?? companyTenant?.createdAt ?? null;
    const licenseStatus = resolveTenantRowLicenseStatus({
      licenseValidUntilUtc: adminTenant?.licenseValidUntilUtc ?? ctx.licenseValidUntilUtc ?? null,
      licenseKey: adminTenant?.licenseKey ?? ctx.licenseKey ?? null,
      licenseDaysRemaining: adminTenant?.licenseDaysRemaining ?? ctx.licenseDaysRemaining ?? null,
    });

    return {
      tenantSlug,
      tenantId,
      tenantName: name,
      registeredAt,
      licenseStatus,
      hasAuthToken: ctx.hasAuthToken,
      isTenantRecordLoading: ctx.isTenantRecordLoading,
      isLoading:
        ctx.isTenantRecordLoading ||
        (companyQuery.isLoading && companyQuery.fetchStatus !== 'idle') ||
        (adminTenantQuery.isLoading && adminTenantQuery.fetchStatus !== 'idle'),
    };
  }, [
    ctx.hasAuthToken,
    ctx.isTenantRecordLoading,
    ctx.licenseDaysRemaining,
    ctx.licenseKey,
    ctx.tenantId,
    ctx.tenantName,
    ctx.tenantSlug,
    ctx.licenseValidUntilUtc,
    adminTenantQuery.data,
    adminTenantQuery.fetchStatus,
    adminTenantQuery.isLoading,
    companyQuery.data,
    companyQuery.fetchStatus,
    companyQuery.isLoading,
  ]);
}
