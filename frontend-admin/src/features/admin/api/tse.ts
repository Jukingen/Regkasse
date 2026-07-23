/**
 * Sketch-compatible re-export for TSE failover admin API.
 * Prefer importing from `@/features/tse-failover/api/tse` in new code.
 */
export {
  getFailoverHistory,
  getFailoverStatus,
  getTseDevices,
  manualFailover,
  revertFailover,
} from '@/features/tse-failover/api/tse';
