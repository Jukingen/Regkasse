'use client';

import { useGetApiAdminCountries } from '@/api/generated/admin/admin';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useAuth } from '@/features/auth/hooks/useAuth';

/**
 * Super Admin country-profile catalog (`GET /api/admin/countries`).
 * Thin Orval wrapper — not wired into any page yet (CreateTenantWizard country step is later).
 */
export function useCountries() {
  const { user } = useAuth();
  return useGetApiAdminCountries({
    query: { enabled: isSuperAdmin(user?.role) },
  });
}
