/**
 * Cashier-facing TSE snapshot. Polling lives in TseHealthProvider (`POS_TSE_HEALTH_POLL_MS`);
 * this hook does not start a second interval.
 */
import { useTseHealth } from './useTseHealth';
import type { PosTseIndicatorStatus } from '../utils/posTseStatus';
import { shouldShowPosTseTestBadge } from '../utils/posTseStatus';

export function useTseStatus() {
  const health = useTseHealth();
  const status = String(health.indicatorStatus) as PosTseIndicatorStatus | string;

  return {
    status,
    message: health.message,
    isActive: status === 'Active',
    isDegraded: status === 'Degraded',
    isInactive: status === 'Inactive',
    loading: health.loading,
    details: {
      scuId: health.scuId,
      lastCheck: health.lastCheck,
      certificateValidUntil: health.certificateValidUntil,
      environment: health.environment,
      cached: health.cached,
    },
    showTestBadge: shouldShowPosTseTestBadge(health.environment),
    refetch: health.refresh,
    error: health.lastErrorMessageSafe,
  };
}
