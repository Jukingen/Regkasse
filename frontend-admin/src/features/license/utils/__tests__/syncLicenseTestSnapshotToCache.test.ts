import { QueryClient } from '@tanstack/react-query';
import { describe, expect, it } from 'vitest';

import { tenantLicenseUnifiedQueryKeyFor } from '@/api/manual/adminLicense';
import type { LicenseTestSnapshot } from '@/features/license/api/licenseTest';
import { tenantLicenseQueryKeys } from '@/features/license/api/tenantLicense';
import { syncLicenseTestSnapshotToCache } from '@/features/license/utils/syncLicenseTestSnapshotToCache';

describe('syncLicenseTestSnapshotToCache', () => {
    it('writes unified and admin detail caches from test snapshot', () => {
        const queryClient = new QueryClient();
        const tenantId = 'b0000001-0001-4001-8001-000000000001';
        const snapshot: LicenseTestSnapshot = {
            tenant: {
                tenantId,
                slug: 'dev',
                name: 'Dev',
                licenseKey: 'TEST-abc',
                validUntilUtc: '2026-07-16T00:00:00Z',
                status: 'active',
                daysRemaining: 1,
                daysOverdue: 0,
                isActive: true,
                isInGracePeriod: false,
                canAccess: true,
                canTransact: true,
                statusMessage: '1 day',
            },
            deployment: {
                isValid: true,
                isTrial: false,
                isExpired: false,
                daysRemaining: 999,
                expiryDateUtc: null,
                licenseKey: null,
                isDevelopmentBypass: false,
                mode: 'Production',
            },
            developmentModeBypassActive: false,
            refreshedAtUtc: '2026-07-15T00:00:00Z',
        };

        syncLicenseTestSnapshotToCache(queryClient, snapshot);

        const detail = queryClient.getQueryData(tenantLicenseQueryKeys.detail(tenantId));
        const publicStatus = queryClient.getQueryData(
            tenantLicenseUnifiedQueryKeyFor(tenantId, 'public'),
        );

        expect(detail).toMatchObject({
            status: {
                kind: 'active',
                daysRemaining: 1,
                licenseKey: 'TEST-abc',
            },
        });
        expect(publicStatus).toMatchObject({
            daysRemaining: 1,
            canAccess: true,
            validUntil: '2026-07-16T00:00:00Z',
        });
    });
});
