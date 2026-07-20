import { describe, expect, it } from 'vitest';
import {
  mapPlatformOverviewToStats,
  mapTenantSummaryToStats,
} from '@/features/data-management/logic/dataRetentionStats';
import type { TenantDataManagementOverview } from '@/features/data-management/api/adminDataManagement';
import type { TenantDataManagementSummary } from '@/features/data-management/api/tenantDataManagement';

describe('dataRetentionStats mappers', () => {
  it('maps platform overview metrics', () => {
    const overview: TenantDataManagementOverview = {
      totalTenants: 5,
      inGraceCount: 1,
      lockedCount: 2,
      pendingDeletionRequestCount: 1,
      purgedCount: 0,
      items: [
        {
          tenantId: 'a',
          tenantSlug: 'a',
          tenantName: 'A',
          lifecycleState: 'Active',
          daysOverdue: 0,
          isInGracePeriod: false,
          gracePeriodRemainingDays: 0,
          isLocked: false,
          isArchived: false,
          hasPendingDeletionRequest: false,
          rksvPaymentCount: 10,
          oldestRksvPaymentDate: '2019-01-01T00:00:00Z',
        },
        {
          tenantId: 'b',
          tenantSlug: 'b',
          tenantName: 'B',
          lifecycleState: 'Grace',
          daysOverdue: 3,
          isInGracePeriod: true,
          gracePeriodRemainingDays: 4,
          isLocked: false,
          isArchived: false,
          hasPendingDeletionRequest: false,
          rksvPaymentCount: 2,
          oldestRksvPaymentDate: '2018-06-01T00:00:00Z',
        },
      ],
    };

    const stats = mapPlatformOverviewToStats(overview);
    expect(stats.totalTenants).toBe(5);
    expect(stats.inGraceCount).toBe(1);
    expect(stats.lockedCount).toBe(2);
    expect(stats.pendingDeletionRequestCount).toBe(1);
    expect(stats.activeCount).toBe(1);
    expect(stats.oldestRksvData).toBe('2018-06-01T00:00:00Z');
    expect(stats.scope).toBe('platform');
  });

  it('maps tenant summary to single-tenant counts', () => {
    const summary: TenantDataManagementSummary = {
      tenantId: 't1',
      tenantSlug: 'cafe',
      tenantName: 'Cafe',
      lifecycleState: 'Grace',
      daysOverdue: 2,
      isInGracePeriod: true,
      gracePeriodRemainingDays: 5,
      isLocked: false,
      rksvRetentionYears: 7,
      rksvRetentionNote: 'note',
      canExport: true,
      canRequestDeletion: false,
      dataTypes: [],
      latestDeletionRequest: {
        id: 'r1',
        status: 'confirmed',
        requestedAtUtc: '2026-07-01T00:00:00Z',
      },
      retention: {
        retentionYears: 7,
        rksvData: {
          paymentDetailsCount: 3,
          oldestPaymentDate: '2020-01-15T00:00:00Z',
        },
        nonRksvData: { productsCount: 1, customersCount: 2, canBeDeleted: true },
      },
    };

    const stats = mapTenantSummaryToStats(summary);
    expect(stats.totalTenants).toBe(1);
    expect(stats.inGraceCount).toBe(1);
    expect(stats.lockedCount).toBe(0);
    expect(stats.pendingDeletionRequestCount).toBe(1);
    expect(stats.activeCount).toBe(0);
    expect(stats.oldestRksvData).toBe('2020-01-15T00:00:00Z');
    expect(stats.scope).toBe('tenant');
  });
});
