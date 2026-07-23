import { describe, expect, it } from 'vitest';

import {
  DownloadSpeedTracker,
  clampPercent,
  estimateEtaSeconds,
  formatSpeedLabel,
  isNetworkDownloadError,
  shouldShowDownloadProgress,
} from '@/lib/download/downloadProgressMath';

describe('downloadProgressMath', () => {
  it('shouldShowDownloadProgress respects threshold and force', () => {
    expect(shouldShowDownloadProgress(1024)).toBe(false);
    expect(shouldShowDownloadProgress(6 * 1024 * 1024)).toBe(true);
    expect(shouldShowDownloadProgress(100, true)).toBe(true);
    expect(shouldShowDownloadProgress(null)).toBe(false);
  });

  it('clampPercent handles edge cases', () => {
    expect(clampPercent(50, 100)).toBe(50);
    expect(clampPercent(0, 100)).toBe(0);
    expect(clampPercent(200, 100)).toBe(100);
    expect(clampPercent(10, null)).toBe(0);
  });

  it('DownloadSpeedTracker estimates bytes/sec', () => {
    const tracker = new DownloadSpeedTracker(5000);
    const t0 = 1_000_000;
    expect(tracker.update(0, t0)).toBe(0);
    const bps = tracker.update(10_000_000, t0 + 1000);
    expect(bps).toBeGreaterThan(9_000_000);
    expect(bps).toBeLessThan(11_000_000);
  });

  it('estimateEtaSeconds', () => {
    expect(estimateEtaSeconds(0, 1000)).toBe(0);
    expect(estimateEtaSeconds(10_000, 1000)).toBe(10);
    expect(estimateEtaSeconds(10_000, 0)).toBeNull();
  });

  it('formatSpeedLabel', () => {
    expect(formatSpeedLabel(0, (n) => `${n} B`)).toBe('0 B/s');
    expect(formatSpeedLabel(45_000_000, (n) => `${Math.round(n / 1e6)} MB`)).toBe('45 MB/s');
  });

  it('isNetworkDownloadError', () => {
    expect(isNetworkDownloadError({ isAxiosError: true, response: null, code: 'ERR_NETWORK' })).toBe(
      true
    );
    expect(isNetworkDownloadError({ name: 'AbortError' })).toBe(false);
    expect(isNetworkDownloadError({ isAxiosError: true, response: { status: 500 } })).toBe(false);
  });
});
