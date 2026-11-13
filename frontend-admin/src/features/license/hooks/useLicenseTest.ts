'use client';

import { type UseQueryResult, useQuery } from '@tanstack/react-query';
import { useEffect } from 'react';

import { isDevelopment } from '@/features/auth/services/devTenant';
import {
  type LicenseTestSnapshot,
  fetchLicenseTestSnapshot,
  licenseTestQueryKey,
} from '@/features/license/api/licenseTest';
import { technicalConsole } from '@/shared/dev/technicalConsole';

const LICENSE_TEST_STALE_MS = 30 * 1000;

/**
 * Dev-only license QA snapshot for a mandant.
 * Uses GET `/api/admin/license/test?tenantId=` (not a path segment).
 */
export function useLicenseTest(
  tenantId?: string | null
): UseQueryResult<LicenseTestSnapshot, Error> {
  const query = useQuery({
    queryKey: licenseTestQueryKey(tenantId),
    queryFn: async () => {
      if (!tenantId) {
        throw new Error('Tenant ID required');
      }
      return fetchLicenseTestSnapshot(tenantId);
    },
    enabled: isDevelopment() && Boolean(tenantId),
    staleTime: LICENSE_TEST_STALE_MS,
    refetchOnMount: true,
    refetchOnWindowFocus: true,
  });

  useEffect(() => {
    if (!isDevelopment()) {
      return;
    }
    technicalConsole.devLog('[License Test] tenantId:', tenantId ?? null);
    technicalConsole.devLog('[License Test] license data:', query.data ?? null);
  }, [tenantId, query.data]);

  return query;
}
