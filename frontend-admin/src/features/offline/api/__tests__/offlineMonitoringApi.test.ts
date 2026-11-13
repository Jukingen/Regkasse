import { describe, expect, it } from 'vitest';

import { mapStatus, mapSyncHealth } from '@/features/offline/api/offlineMonitoringApi';

describe('offlineMonitoringApi', () => {
  it('maps status and sync health from camelCase API', () => {
    const sync = mapSyncHealth({
      isHealthy: false,
      successRate: 72,
      totalAttempts: 10,
      failedAttempts: 3,
    });

    const status = mapStatus(
      {
        totalPendingOrders: 5,
        totalPendingTransactions: 2,
        hasCriticalIssues: true,
        oldestPendingOrder: '2026-06-27T10:00:00.000Z',
      },
      sync
    );

    expect(status.totalPendingOrders).toBe(5);
    expect(status.syncHealth.successRate).toBe(72);
    expect(status.hasCriticalIssues).toBe(true);
  });

  it('maps PascalCase API fallback', () => {
    const sync = mapSyncHealth({ IsHealthy: true, SuccessRate: 95 });
    const status = mapStatus({ TotalPendingOrders: 1 }, sync);
    expect(status.totalPendingOrders).toBe(1);
    expect(status.syncHealth.successRate).toBe(95);
  });
});
