import { afterEach, describe, expect, it } from 'vitest';

import {
  __resetApiMetricsStoreForTests,
  getApiMetricsSummary,
  recordApiMetricSample,
} from '@/lib/monitoring/apiMetricsStore';
import { sanitizeApiPath } from '@/lib/monitoring/sanitizeApiPath';
import { MONITORING_THRESHOLDS } from '@/lib/monitoring/thresholds';

describe('sanitizeApiPath', () => {
  it('strips query and collapses UUIDs', () => {
    expect(
      sanitizeApiPath(
        '/api/admin/tenants/11111111-1111-4111-8111-111111111111/users?x=1',
      ),
    ).toBe('/api/admin/tenants/:id/users');
  });
});

describe('apiMetricsStore', () => {
  afterEach(() => {
    __resetApiMetricsStoreForTests();
  });

  it('computes error rate and flags alert above 5% with enough samples', () => {
    for (let i = 0; i < 19; i += 1) {
      recordApiMetricSample({
        durationMs: 100,
        status: 200,
        ok: true,
        method: 'GET',
        path: '/api/admin/x',
      });
    }
    // 19 ok — below min samples for alert even if we add errors later below threshold
    expect(getApiMetricsSummary().errorRateAlert).toBe(false);

    for (let i = 0; i < 5; i += 1) {
      recordApiMetricSample({
        durationMs: 100,
        status: 500,
        ok: false,
        method: 'GET',
        path: '/api/admin/x',
      });
    }
    // 19 ok + 5 err = 24 samples, errorRate ≈ 20.8% > 5%
    const summary = getApiMetricsSummary();
    expect(summary.sampleCount).toBe(24);
    expect(summary.errorRate).toBeGreaterThan(MONITORING_THRESHOLDS.apiErrorRate);
    expect(summary.errorRateAlert).toBe(true);
  });

  it('tracks slow requests against 1s threshold', () => {
    recordApiMetricSample({
      durationMs: MONITORING_THRESHOLDS.apiResponseTimeMs + 50,
      status: 200,
      ok: true,
      method: 'GET',
      path: '/api/admin/slow',
    });
    expect(getApiMetricsSummary().hasSlowRequests).toBe(true);
    expect(getApiMetricsSummary().slowCount).toBe(1);
  });
});
