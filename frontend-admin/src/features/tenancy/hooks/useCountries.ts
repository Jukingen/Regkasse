'use client';

import { useGetApiAdminCountries } from '@/api/generated/admin/admin';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useAuth } from '@/features/auth/hooks/useAuth';

/**
 * Super Admin country-profile catalog (`GET /api/admin/countries`).
 * Used by CreateTenantWizard and the tenant-detail country card.
 */
export function useCountries() {
  const { user } = useAuth();
  return useGetApiAdminCountries({
    query: { enabled: isSuperAdmin(user?.role) },
  });
}
