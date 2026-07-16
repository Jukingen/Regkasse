'use client';

import { useEffect } from 'react';
import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import {
    fetchLicenseTestSnapshot,
    licenseTestQueryKey,
    type LicenseTestSnapshot,
} from '@/features/license/api/licenseTest';
import { isDevelopment } from '@/features/auth/services/devTenant';
import { technicalConsole } from '@/shared/dev/technicalConsole';

const LICENSE_TEST_STALE_MS = 30 * 1000;

/**
 * Dev-only license QA snapshot for a mandant.
 * Uses GET `/api/admin/license/test?tenantId=` (not a path segment).
 */
export function useLicenseTest(
    tenantId?: string | null,
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
